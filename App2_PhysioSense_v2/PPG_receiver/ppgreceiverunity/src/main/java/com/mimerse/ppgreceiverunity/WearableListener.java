package com.mimerse.ppgreceiverunity;

import com.unity3d.player.UnityPlayer;

import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.ServiceConnection;
import android.os.Bundle;
import android.app.Fragment;
import android.os.IBinder;
import android.util.Log;

// Class that read the values from the wearable and send it to Unity
public class WearableListener extends Fragment {
    public static final String TAG = "PPG_LISTENER_SERVICE";

    private static String receivedPacketCallbackName = "onReceivedPacket";
    private static String debugMessageCallbackName = "onDebugMessage";
    private static String gameObjectName = "GameObject";
    private static Boolean setupDoneFromUnity = false;

    public static WearableListener instance;

    private static Boolean mIsBound = false;
    private static ConsumerService mConsumerService = null;

    public WearableListener() {
        // Required empty public constructor
    }

    private static final ServiceConnection mConnection = new ServiceConnection() {
        @Override
        public void onServiceConnected(ComponentName className, IBinder service) {
            mConsumerService = ((ConsumerService.LocalBinder) service).getService();
            mIsBound = true;
            Log.d(TAG, "onServiceConnected");
        }

        @Override
        public void onServiceDisconnected(ComponentName className) {
            mConsumerService = null;
            mIsBound = false;
            Log.d(TAG, "onServiceDisconnected");
        }
    };

    // UNITY CALLBACKS
    public static void Setup(String gameObject, String onDataReceivedCallback, String onDebugMessage) {
        // Instantiate and add to Unity Player Activity
        instance = new WearableListener();

        gameObjectName = gameObject;
        receivedPacketCallbackName = onDataReceivedCallback;
        debugMessageCallbackName = onDebugMessage;

        Log.d(TAG, "Started instance:" + gameObjectName);
        UnityPlayer.currentActivity.getFragmentManager().beginTransaction().add(instance, WearableListener.TAG).commit();

        // Bind service
        mIsBound = UnityPlayer.currentActivity.bindService(new Intent(UnityPlayer.currentActivity, ConsumerService.class), mConnection, Context.BIND_AUTO_CREATE);

        setupDoneFromUnity = true;

        instance.DebugUnity("Setup() done, mIsBound = " + Boolean.toString(mIsBound));
    }

    public static void FindPeers()
    {
        if (setupDoneFromUnity && mIsBound == true && mConsumerService != null) {
            mConsumerService.findPeers();
            Log.d(TAG, "Peers found");
        }
        DebugUnity("FindPeers() done");
    }

    public static String GetLastMessage()
    {
        if (setupDoneFromUnity && mIsBound == true && mConsumerService != null) {
            DebugUnity("GetCurrentHR() called...");
            return mConsumerService.lastMessage;
        }

        return "SAP not found...";
    }

    // Utilities
    public static void SendData(String data)
    {
        SendUnityMessage(receivedPacketCallbackName, data);
    }

    public static void DebugUnity(String message)
    {
        SendUnityMessage(debugMessageCallbackName, message);
    }

    private static void SendUnityMessage(String methodName, String parameter) {
        // Don't send messages if Setup was not called from Unity.
        if(setupDoneFromUnity)
        {
            Log.i(TAG, "SendUnityMessage(" + methodName + ", " + parameter + ")");
            UnityPlayer.UnitySendMessage(gameObjectName, methodName, parameter);
        }
    }



    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
    }

    @Override
    public void onDestroy() {
        // Clean up connections
        if (mIsBound&& mConsumerService != null) {
            Log.d(TAG, "onServiceDisconnected");
            mConsumerService.clearToast();
        }
        // Un-bind service
        if (mIsBound) {
            UnityPlayer.currentActivity.unbindService(mConnection);
            mIsBound = false;
        }
        super.onDestroy();
    }
}
