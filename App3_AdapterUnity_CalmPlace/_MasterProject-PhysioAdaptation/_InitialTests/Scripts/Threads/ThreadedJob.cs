// Source: http://answers.unity3d.com/questions/357033/unity3d-and-c-coroutines-vs-threading.html
//
// *****************************************************************************
// Example Job - Inheret from this class and override ThreadFunction:
// *****************************************************************************
// public class Job : ThreadedJob
// {
//     public Vector3[] InData;  // arbitary job data
//     public Vector3[] OutData; // arbitary job data
// 
//     protected override void ThreadFunction()
//     {
//         // Do your threaded task. DON'T use the Unity API here
//         for (int i = 0; i < 100000000; i++)
//         {
//             InData[i % InData.Length] += InData[(i+1) % InData.Length];
//         }
//     }
//     protected override void OnFinished()
//     {
//         // This is executed by the Unity main thread when the job is finished
//         for (int i = 0; i < InData.Length; i++)
//         {
//             Debug.Log("Results(" + i + "): " + InData[i]);
//         }
//     }
// }
//
// *****************************************************************************
// Starting Job:
// *****************************************************************************
// Job myJob;
// void Start ()
// {
//     myJob = new Job();
//     myJob.InData = new Vector3[10];
//     myJob.Start(); // Don't touch any data in the job class after you called Start until IsDone is true.
// }
//
// *****************************************************************************
// Checking job status from Unity:
// *****************************************************************************
// void Update()
// {
//     if (myJob != null)
//     {
//         if (myJob.Update())
//         {
//             // Alternative to the OnFinished callback
//             myJob = null;
//         }
//     }
// }
//
// *****************************************************************************
// Wait for job in a coroutine:
// *****************************************************************************
// yield return StartCoroutine(myJob.WaitFor());
//

using System.Collections;
using System.Threading;

public class ThreadedJob
 {
     private bool m_IsDone = false;
     private object m_Handle = new object();
     private Thread m_Thread = null;
     public bool IsDone
     {
         get
         {
             bool tmp;
             lock (m_Handle)
             {
                 tmp = m_IsDone;
             }
             return tmp;
         }
         set
         {
             lock (m_Handle)
             {
                 m_IsDone = value;
             }
         }
     }
 
     public virtual void Start()
     {
         m_Thread = new Thread(Run);
         m_Thread.Start();
     }
     public virtual void Abort()
     {
         m_Thread.Abort();
     }
 
     protected virtual void ThreadFunction() { }
 
    /// <summary>
    /// Executed on Unity main thread so it's safe to use Unity API.
    /// </summary>
     protected virtual void OnFinished() { }
 
     public virtual bool Update()
     {
         if (IsDone)
         {
             OnFinished();
             return true;
         }
         return false;
     }
	 
     public IEnumerator WaitFor()
     {
		// WaitFor coroutine which allows you to easily wait in a coroutine for the thread to finish. Just use
		// yield return StartCoroutine(myJob.WaitFor());
		// inside another coroutine. This can be used instead of calling Update manually each frame.
         while(!Update())
         {
             yield return null;
         }
     }
     private void Run()
     {
         ThreadFunction();
         IsDone = true;
     }
 }