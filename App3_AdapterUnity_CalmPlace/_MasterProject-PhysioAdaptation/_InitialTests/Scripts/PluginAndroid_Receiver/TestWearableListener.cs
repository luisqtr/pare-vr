using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class TestWearableListener : MonoBehaviour
{
    public Text result;
    public Text debug;
    // public int showEachPackets = 200;
    // private int elapsedPackets = 0;

    private AndroidJavaClass wearableListener;
    private AndroidJavaObject instance
    {
        get {
            return wearableListener.GetStatic<AndroidJavaObject>("instance"); 
        }
    }

    public void Start()
    {
        wearableListener = new AndroidJavaClass("com.mimerse.ppgreceiverunity.WearableListener");
    }

    public void Setup()
    {
        // Name of the function in the Android Plugin "Setup", the object that receives the callbacks from the plugin, the name of the two callback functions.
        wearableListener.CallStatic("Setup", gameObject.name, "ReceivedPacket", "DebugMessage");
    }

    public void FindPeers()
    {
        wearableListener.CallStatic("FindPeers");
    }

    /// Instead of waiting for the callback, ask from Unity to the App for the current value.
    public void GetLastMessage()
    {
        ReceivedPacket("GLM:" + wearableListener.CallStatic<string>("GetLastMessage"));
    }


    /////// Callback from Android Plugin
    public void ReceivedPacket(string data)
    {
        // if(elapsedPackets < showEachPackets)
        // {
        //     elapsedPackets++;
        // }
        // else
        // {
        //     result.text = data;
        //     elapsedPackets = 0;
        // }

        result.text = data;

        // Call event hooked in.
        //Debug.Log("RECEIVED: " + data);   
    }

    public void DebugMessage(string message)
    {
        debug.text = message;
    }
}
