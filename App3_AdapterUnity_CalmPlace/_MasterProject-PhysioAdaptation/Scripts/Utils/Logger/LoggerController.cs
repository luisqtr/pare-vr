using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Configures the files that are going to be written during loggging.
/// </summary>
public class LoggerController : MonoBehaviour
{
    [Tooltip("Name of the folder that will save the Logs, this folder is located in the same path that [YourApplication].exe file.")]
    public string logFolderName = "LogFiles_App";


    public enum Type
    {
        PhysioData,
        Events,
        
    }

    [Header("FILENAMES")]
    
    [HideInInspector] 
    public string thisLogPath = "";
    public string logFilenamePhysioData = "HRM.csv";
    public string logFilenameEvents = "Events.txt";
    private static bool currentlyLogging = false;

    /// <summary>
    /// Log events from HRM
    /// </summary>
    private static DataLogger loggerHRM;
    private static DataLogger loggerEvents;

    public static LoggerController instance;

	// Use this for initialization
	void Awake ()
    {
        if(instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
	}

    public void SetupNewLog()
    {
#if UNITY_EDITOR
        string folderToSaveLogs = Application.streamingAssetsPath + "/" + logFolderName + "/";
#elif UNITY_ANDROID
        //  /storage/emulated/0/Android/data/<packagename>/files
        string folderToSaveLogs = Application.persistentDataPath + "/" + logFolderName + "/";
#else
        string folderToSaveLogs = Application.streamingAssetsPath + "/../" + logFolderName + "/";
#endif

        if (!File.Exists(folderToSaveLogs))
        {
            Directory.CreateDirectory(folderToSaveLogs);
        }

        string timeStamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        thisLogPath = folderToSaveLogs + "/" + timeStamp + "_" + "Username" + "/";
        Directory.CreateDirectory(thisLogPath);

        loggerHRM = new DataLogger(new List<string> { "PhysioData" }, thisLogPath + logFilenamePhysioData);
        loggerEvents = new DataLogger(new List<string> {"EVENTS"}, thisLogPath + logFilenameEvents);
    }

#if UNITY_ANDROID
    public string GetAndroidExternalStoragePath()
    {
             string path = "";
            #if UNITY_ANDROID && !UNITY_EDITOR
            try {
                    IntPtr obj_context = AndroidJNI.FindClass("android/content/ContextWrapper");
                    IntPtr method_getFilesDir = AndroidJNIHelper.GetMethodID(obj_context, "getFilesDir", "()Ljava/io/File;");
            
                    using (AndroidJavaClass cls_UnityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer")) {
                        using (AndroidJavaObject obj_Activity = cls_UnityPlayer.GetStatic<AndroidJavaObject>("currentActivity")) {
                            IntPtr file = AndroidJNI.CallObjectMethod(obj_Activity.GetRawObject(), method_getFilesDir, new jvalue[0]);
                            IntPtr obj_file = AndroidJNI.FindClass("java/io/File");
                            IntPtr method_getAbsolutePath = AndroidJNIHelper.GetMethodID(obj_file, "getAbsolutePath", "()Ljava/lang/String;");   
                                            
                            path = AndroidJNI.CallStringMethod(file, method_getAbsolutePath, new jvalue[0]);                    
            
                            if(path != null) {
                            Debug.Log("Got internal path: " + path);
                            }
                            else {
                            Debug.Log("Using fallback path");
                            path = "/data/data/CalmPlace_Physio/files";
                            }
                        }
                    }
                }
                catch(Exception e) {
                    Debug.Log(e.ToString());
                }
            #else
                path = Application.persistentDataPath;
            #endif
            return path;
    }
#endif

    public static bool WriteLine(Type type, string line)
    {
        if(!currentlyLogging)
            return false;

        // When logging is active
        switch (type)
        {
            case Type.PhysioData:
                return loggerHRM.WriteLine(line);
            case Type.Events:
                return loggerEvents.WriteLine(line);
        }
        return false;
    }

    public void SetIsLogging(bool state)
    {
        currentlyLogging = state;

        if (loggerHRM != null)
            loggerHRM.isLogging = state;

        if (loggerEvents != null)
            loggerEvents.isLogging = state;
    }

    public void CloseLoggingFiles()
    {
        if (loggerHRM != null) loggerHRM.Close();
        if (loggerEvents != null) loggerEvents.Close();

        /*
        if (loggerSummary != null)
        {
            logTimeSeconds = (DateTime.Now - initialTimeLog).TotalSeconds;
            
            loggerSummary.WriteLine("User: " + SettingsManager.Values.logSettings.userName);
            loggerSummary.WriteLine("Number of shot bullets: " + numberOfBullets);
            loggerSummary.WriteLine("Number of hits: " + numberOfHits);
            loggerSummary.WriteLine("Number of destroyed targets: " + numberOfDestroyedTargets);
            loggerSummary.WriteLine("Number of targets destroyed by head shot: " + numberOfDestroyedByHeadshots);
            loggerSummary.WriteLine("-----------------------------");
            loggerSummary.WriteLine("Effectivity ratio (Hits/Shot bullets): " + (float)numberOfHits/ (float)numberOfBullets);
            loggerSummary.WriteLine("Headshot ratio: " + (float)numberOfDestroyedByHeadshots / (float)numberOfDestroyedTargets);
            loggerSummary.WriteLine("Seconds per destroyed target: " + (logTimeSeconds/numberOfDestroyedTargets).ToString("F3"));
            loggerSummary.WriteLine("TOTAL TIME: " + logTimeSeconds);
            loggerSummary.Close();
        }
        */
    }

    private void OnDestroy()
    {
        CloseLoggingFiles();
    }
}
