package com.mimerse.physiosense;

import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.ServiceConnection;
import android.content.pm.PackageManager;
import android.os.AsyncTask;
import android.os.Build;
import android.os.Environment;
import android.os.IBinder;
import android.support.annotation.RequiresPermission;
import android.support.v4.app.ActivityCompat;
import android.support.v7.app.AppCompatActivity;
import android.os.Bundle;
import android.util.Log;
import android.view.KeyEvent;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.BaseAdapter;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.TextView;
import android.widget.Toast;

import com.mimerse.physiosense.dwt.StringUtils;
import com.mimerse.physiosense.services.WearListenerService;       // Android Wear OS Smartwatch
import com.mimerse.physiosense.services.SAPConsumerService;        // Tizen OS Samsung Smartwatches
import com.mimerse.physiosense.services.MuseListenerService;       // EEG Muse
import com.mimerse.physiosense.services.myo.MYOListenerService;    // EMG Myo
import com.mimerse.physiosense.signalproc.SignalAnalyzer;
import com.mimerse.physiosense.signalproc.SignalProcessingResponse;
import com.mimerse.physiosense.signalproc.SignalProcessingThread;

import java.io.BufferedWriter;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;
import java.net.SocketException;
import java.net.UnknownHostException;
import java.util.ArrayList;
import java.util.Collections;
import java.util.DoubleSummaryStatistics;
import java.util.List;
import java.util.Timer;
import java.util.TimerTask;

public class MainActivity extends AppCompatActivity {
    public static final String TAG = "PhysioSense";
    public static final String MAIN_FOLDER_NAME = "PhysioSense_Logs";

    public enum Device{
        NONE,
        TIZEN,
        WEAR,
        MUSE,
        MYO,
    }

    // Services
    final MuseListenerService museListenerService = new MuseListenerService();
    final MYOListenerService myoListenerService = new MYOListenerService();
    final WearListenerService wearListenerService = new WearListenerService();
    private SAPConsumerService sapConsumerService = null;

    // Main UI elements
    public ImageView imgLogo;
    public ImageView imgTizen, imgWear, imgMuse, imgMyo;
    public TextView textTizen, textWear, textMuse, textMyo;
    public LinearLayout layTizen, layWear, layMuse, layMyo;
    public Button btnTizen, btnWear, btnMuse, btnMyo;

    // SAP Consumer Activity UI
    private static TextView mSapStatus;
    private static MessageAdapter mMessageAdapter;
    private boolean mIsBound = false;
    private ListView mMessageListView;
    private TextView mLogStatus;
    public EditText mLogFolderName;
    private static boolean sendButtonClicked;

    // Setup Other Services
    Timer udpTimer;
    UDPSender udpSender;
    int frequency = 2000;

    // LOGGERS
    private static boolean firstLogger = true;
    private static boolean areLoggersReady = false;
    private static String appFolderPath = "";
    private static String signalLoggerFilename, processedSignalLoggerFilename, peaksAndHRVLoggerFilename;
    private static File signalLogger, processedSignalLogger, peaksAndHRVLogger, debugLogger;
    private static BufferedWriter writerSignal, writerProcSgn, writerPeaksAndHRV, writerDebug;

    // UDP Setup
    final static int UDP_SERVER_PORT = 1111;
    private static DatagramSocket udpSocket = null;
    private static InetAddress serverAddr = null;
    private static DatagramPacket udpPacket = null;

    // SIGNAL ANALYZER
    private static AsyncTask SignalProcessor;
    private static int DATA_BLOCK_TO_PROCESS = 1024; // Power of 2 to facilitate DWT calculation
    private static int WINDOW_OVERLAP = 112;         //Allow to process only the 800 samples in the middle using 1024 samples
    private static double timeStampPeak = 0.0, timeStampLastPeak = 0.0, calculatedHRV = 0.0, lastValidHRV = 0.0;
    private static final int HRV_LOWER_LIMIT_MS = 200, HRV_UPPER_LIMIT_MS = 1400; // HRV values outside the range will be dismissed and set to the previous valid value

    // Store timestamps as text to avoid rounding the numbers when trying to map the indexes of the peaks in the original signal again.
    private static List<String> timeSet = new ArrayList<String>();
    private static List<Double> valueSet = new ArrayList<Double>();
    private static double[] signalValues = new double[2];

    private static int incomingDataCounter = 0;
    private static boolean firstProcessingRound = true;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        //setContentView(R.layout.activity_main);
        setContentView(R.layout.activity_phone);

        //////////////// Find objects in Main View
        imgLogo = (ImageView) findViewById(R.id.imgLogo);

        // status icons and texts
        textTizen = (TextView) findViewById(R.id.txtTizenStatus);
        imgTizen = (ImageView) findViewById(R.id.imgTizen);
        btnTizen = (Button) findViewById(R.id.btnTizen);
        layTizen = (LinearLayout) findViewById(R.id.LayDetailsTizen);

        textWear = (TextView) findViewById(R.id.txtWearStatus);
        imgWear = (ImageView) findViewById(R.id.imgWear);
        btnWear = (Button) findViewById(R.id.btnWear);
        layWear = (LinearLayout) findViewById(R.id.LayDetailsWear);

        textMuse = (TextView) findViewById(R.id.txtMuseStatus);
        imgMuse = (ImageView) findViewById(R.id.imgMuse);
        btnMuse = (Button) findViewById(R.id.btnMuse);
        layMuse = (LinearLayout) findViewById(R.id.LayDetailsMuse);

        textMyo = (TextView) findViewById(R.id.txtMyoStatus);
        imgMyo = (ImageView) findViewById(R.id.imgMyo);
        btnMyo = (Button) findViewById(R.id.btnMyo);
        layMyo = (LinearLayout) findViewById(R.id.LayDetailsMyo);

        //////////////// Objects in Details Layouts

        // Find objects in Tizen Details Layout
        mSapStatus = (TextView) findViewById(R.id.tvStatus);
        mMessageListView = (ListView) findViewById(R.id.lvMessage);
        mLogStatus = (TextView) findViewById(R.id.tvLogState);
        mLogFolderName = (EditText) findViewById(R.id.editLogFilename);

        // ToDo: Find objects in other detailed layouts.

        //////////////// Connect to Services
        // Connect to Wear, Muse, Myo services
        // ToDo: Activate Timer again to send data from other sensors.
        /*udpTimer = new Timer();
        udpSender = new UDPSender();
        udpTimer.scheduleAtFixedRate(udpSender, 0, frequency);*/

        // Setup UDP sender
        SetupUDPSender();

        // Connect to Tizen SAP service
        mMessageAdapter = new MessageAdapter();
        mMessageListView.setAdapter(mMessageAdapter);
        sendButtonClicked = false;
        mIsBound = bindService(new Intent(MainActivity.this, SAPConsumerService.class), mConnection, Context.BIND_AUTO_CREATE);

        // Check folder path to save logs
        boolean permissionIsGranted = isWriteStoragePermissionGranted();
        if(permissionIsGranted)
            CheckMainFolder();

        // Initial state of GUI
        layTizen.setVisibility(View.INVISIBLE);
        UpdateDetailedLayoutsView(Device.TIZEN);
    }

    @Override
    protected void onDestroy() {
        // Clean up connections from Tizen SAP
        if (mIsBound == true && sapConsumerService != null) {
            updateTextView("Disconnected");
            mMessageAdapter.clear();
            sapConsumerService.clearToast();
        }
        // Un-bind service
        if (mIsBound) {
            unbindService(mConnection);
            mIsBound = false;
        }
        sendButtonClicked = false;

        // Clean up connections from Wear, Muse, Myo
        disconnectSensors();

        // UDP Close
        if (udpSocket != null) {
            udpSocket.close();
        }

        // SIGNAL PROCESSOR
        // Cancel signal processor
        SignalProcessor.cancel(true);

        // FILE MANAGEMENT
        areLoggersReady = false;
        try {
            // Log was running. Stop it and create new ones.
            if(writerSignal != null)
                writerSignal.close();
            if(writerProcSgn != null)
                writerProcSgn.close();
            if(writerPeaksAndHRV != null)
                writerPeaksAndHRV.close();
            if(writerDebug != null)
                writerDebug.close();

        } catch (IOException e) {
            e.printStackTrace();
        }


        super.onDestroy();
    }

    @Override
    public boolean onKeyDown(int keyCode, KeyEvent event) {
        switch(keyCode) {
            case KeyEvent.KEYCODE_BACK:
                //minimize application
                // Treating back button as a home button
                Intent startMain = new Intent(Intent.ACTION_MAIN);
                startMain.addCategory(Intent.CATEGORY_HOME);
                startMain.setFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                startActivity(startMain);
                return true;
        }
        return super.onKeyDown(keyCode, event);
    }

    /////////// PERMISSIONS
    public  boolean isWriteStoragePermissionGranted() {
        if (Build.VERSION.SDK_INT >= 23) {
            if (checkSelfPermission(android.Manifest.permission.WRITE_EXTERNAL_STORAGE)
                    == PackageManager.PERMISSION_GRANTED) {
                Log.v(TAG,"Permission is granted2");
                return true;
            } else {
                Log.v(TAG,"Permission is revoked2");
                ActivityCompat.requestPermissions(this, new String[]{android.Manifest.permission.WRITE_EXTERNAL_STORAGE}, 2);
                return false;
            }
        }
        else { //permission is automatically granted on sdk<23 upon installation
            Log.v(TAG,"Permission is granted2");
            return true;
        }
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        switch (requestCode) {
            case 2: // Write External Storage
                Log.d(TAG, "External storage2");
                if(grantResults[0]== PackageManager.PERMISSION_GRANTED){
                    Log.v(TAG, "Permission: " + permissions[0] + "was " + grantResults[0]);
                    //resume tasks needing this permission
                    CheckMainFolder();
                }else{
                    Toast.makeText(this, "The application won't work without granting permissions", Toast.LENGTH_SHORT).show();
                }
                break;
        }
    }

    ///////////////////// UDP Connection
    private void SetupUDPSender() {
        try {
            udpSocket = new DatagramSocket();
            serverAddr = InetAddress.getByName("127.0.0.1");
        } catch (SocketException e) {
            e.printStackTrace();
        } catch (UnknownHostException e) {
            e.printStackTrace();
        } catch (IOException e) {
            e.printStackTrace();
        } catch (Exception e) {
            e.printStackTrace();
        }
        finally{

        }
    }

    public static void ProcessIncomingMessage(String incomingMessage) {
        // String incomingMessage = "museValues" + " | " + "wearHRate" + " | " + "myoValues";

        // Only when the loggers are ready the analysis is done.
        if(areLoggersReady)
        {
            AnalyzeMessage(incomingMessage);
        }

        // In any case, the incoming data is bridged to the UDP port.
        SendThroughUDP(incomingMessage);
    }

    private static void SendThroughUDP(String datagram) {
        try {
            udpPacket = new DatagramPacket(datagram.getBytes(), datagram.length(), serverAddr, UDP_SERVER_PORT);
            udpSocket.send(udpPacket);

            Log.d(TAG, datagram + " via " + UDP_SERVER_PORT + " to: " + serverAddr);
        } catch (SocketException e) {
            e.printStackTrace();
        } catch (UnknownHostException e) {
            e.printStackTrace();
        } catch (IOException e) {
            e.printStackTrace();
        } catch (Exception e) {
            e.printStackTrace();
        } finally {

        }
    }

    // Signal Processing
    private static void AnalyzeMessage(String line) {
        incomingDataCounter++;

        String[] lineValues = line.split(",");
        if (lineValues.length != 4 || (lineValues[0].compareTo("PPG") != 0 && lineValues[0].compareTo("HR") != 0)) {
            WriteLog("Line skipped: " + incomingDataCounter + "\t" + line + "\t||NOT PROCESSED!");
            return;
        }

        // Write line
        try {
            writerSignal.write(line);
            //writerSignal.newLine(); // Not needed, the line comes with a "\n" at the end
            writerSignal.flush();
        } catch (IOException e) {
            e.printStackTrace();
        }

        // If it is Heart Rate instead of PPG info, don't process anything and continue.
        if (lineValues[0].compareTo("HR") == 0)
            return;

        //// IF IT IS A VALID SAMPLE
        // Add timestamps and values of signal
        String timestampStr = lineValues[1];
        String valueStr = lineValues[2];
        if (StringUtils.isNumeric(timestampStr)) {
            timeSet.add(timestampStr);
        }
        if (StringUtils.isNumeric(valueStr)) {
            valueSet.add(Double.valueOf(valueStr));
        }

        //System.out.println("Line Count: " + incomingDataCounter);

        // When it reaches the amount of samples to process, then calculate peaks.
        int timeSetSize = timeSet.size();
        int valueSetSize = valueSet.size();
        if(valueSetSize == DATA_BLOCK_TO_PROCESS)
        {
            if(valueSetSize == timeSetSize)
            {
                // Transform from List<double> to double[]
                signalValues = new double[valueSetSize];
                for(int j=0; j<valueSet.size(); j++)
                {
                    signalValues[j] = valueSet.get(j);
                }

/*
                //////// TODO: ERROR WHEN SENDING CALCULATION TO AsyncTask
                SignalProcessingThread.Params params = new SignalProcessingThread().new Params(signalValues, 50, WINDOW_OVERLAP, firstProcessingRound, writerProcSgn,
                        timeSet, writerPeaksAndHRV, incomingDataCounter, DATA_BLOCK_TO_PROCESS,
                        new SignalProcessingResponse<List<String>>() {
                            @Override
                            public void OnProcessFinished(List<String> result){
                                WhenSignalProcesingDone(result);
                            }
                });

                // Start AsyncTask
                SignalProcessor= new SignalProcessingThread().execute(params);
                // First samples were already written
                firstProcessingRound = false;
                //////// TODO: ----- END OF ASYNC TASK
*/

                ///// TODO: ERROR_START: START OF SECTION WITH BUG
                ///// Calculate peaks, the list contains the indexes where a peak was found, from 0 to DATA_BLOCK_TO_PROCESS (1024)
                // Processing without threads. Problem: Each 16 secs, the HRV is higher due to missed values during processing time.
                List<Integer> validPeakPositions = SignalAnalyzer.ProcessSignals(signalValues, 50, WINDOW_OVERLAP, firstProcessingRound, writerProcSgn);

                // First samples were already written
                firstProcessingRound = false;

                String peakTimeStampStr;
                int peakPosition;
                //////////////////// LOOP TO PROCESS DETECTED PEAKS
                // The indexes are mapped in the array of timestamps to specify the specific time when the peak was detected
                for(int k=0; k<validPeakPositions.size(); k++) {

                    peakPosition = validPeakPositions.get(k);
                    peakTimeStampStr = timeSet.get(peakPosition);
                    WriteLog("PEAK " + k + ": " + peakPosition + " = " + peakTimeStampStr);

                    // Get the time of the last detected peak
                    if(StringUtils.isNumeric(peakTimeStampStr)) {
                        timeStampPeak = Double.parseDouble(peakTimeStampStr);
                    }
                    else {
                        WriteLog("Error parsing peak timestamp, is not numeric. Value = " + timeStampLastPeak);
                        timeStampPeak = 0.0;
                    }

                    // Is the first peak that was detected in the signal acquisition, no HRV is calculated
                    if(timeStampPeak == 0.0 || timeStampLastPeak == 0.0) {
                        calculatedHRV = 0.0;
                    }
                    else
                    {
                        calculatedHRV = timeStampPeak - timeStampLastPeak;
                        if(calculatedHRV/1000 >= HRV_LOWER_LIMIT_MS && calculatedHRV/1000 <= HRV_UPPER_LIMIT_MS)
                        {
                            lastValidHRV = calculatedHRV;
                        }
                    }

                    // Send to receiver only valid HRV, meaning within the ranges.
                    SendThroughUDP("HRV," + timeSet.get(peakPosition) + "," + Double.toString(calculatedHRV) + "\n");

                    // Store the calculated HRV even if the value is outside the ranges. To be processed offline.
                    try{
                        // Write the timestamp corresponding to the detected peak
                        writerPeaksAndHRV.write(Integer.toString(incomingDataCounter - DATA_BLOCK_TO_PROCESS + peakPosition) + "," +
                                                                    timeSet.get(peakPosition) + "," + Double.toString(calculatedHRV));
                        writerPeaksAndHRV.newLine();
                        writerPeaksAndHRV.flush();
                    } catch (IOException e) {
                        e.printStackTrace();
                    }

                    // Swap variables of last and second last detected peaks.
                    timeStampLastPeak = timeStampPeak;

                }
                ///// TODO: ERROR_END: END OF SECTION WITH BUG

                // Clear the samples from the lists for the next calculation, but leaving the overlapping samples necessary for the next calculation
                timeSet.subList(0,timeSetSize-2*WINDOW_OVERLAP).clear();
                valueSet.subList(0,valueSetSize-2*WINDOW_OVERLAP).clear();
                WriteLog("Peaks are being calculated, remaining samples in array to overlap with next calculation. timSetSize=" + timeSet.size() + " and valueSetSize=" + valueSet.size());
            }
            else {
                WriteLog("Array of timestamps and values are not the same, deleting arrays: " + valueSetSize + " vs. " + timeSetSize);
                timeSet.clear();
                valueSet.clear();
            }
        }

        // Read new line
        //line = reader.readLine();
    }

    // Callback when asynchronous signal processing is done
    private static void WhenSignalProcesingDone(List<String> hrv_lines) {
        WriteLog("HRV calculation finished. Number of lines: " + Integer.toString(hrv_lines.size()));
    }


    //// Folder and File Management
    private void CheckMainFolder()
    {
        appFolderPath= Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS).getAbsolutePath() + "/" + MAIN_FOLDER_NAME;
        File f = new File(appFolderPath);
        if(!f.exists()){
            if(!f.mkdir()){
                Toast.makeText(this, appFolderPath+" can't be created. PERMISSION ERROR", Toast.LENGTH_SHORT).show();
            }
            else
                Toast.makeText(this, appFolderPath+" was created.", Toast.LENGTH_SHORT).show();
        }
        else
            Toast.makeText(this, appFolderPath+" already exists.", Toast.LENGTH_SHORT).show();

        appFolderPath = appFolderPath + "/";
    }

    private void CreateOrResetLoggers()
    {
        areLoggersReady = false;
        if(!firstLogger) {
            // Log was not running. It is the first set of logs
            try {
                // Log was running. Stop it and create new ones.
                if(writerSignal != null)
                    writerSignal.close();
                if(writerProcSgn != null)
                    writerProcSgn.close();
                if(writerPeaksAndHRV != null)
                    writerPeaksAndHRV.close();
                if(writerDebug != null)
                    writerDebug.close();
            } catch (IOException e) {
                e.printStackTrace();
            }
        }

        // CREATE NEW FOLDER NAME WITH THE EDIT FIELD TEXT
        String currentLogFolderPath = StringUtils.CreateTimestamp(mLogFolderName.getText().toString());
        String fullFolderPath= appFolderPath + currentLogFolderPath;

        File f = new File(fullFolderPath);
        if(!f.exists())
            if(!f.mkdir()){
                Toast.makeText(this, fullFolderPath+" can't be created.", Toast.LENGTH_SHORT).show();
            }
            else
                Toast.makeText(this, fullFolderPath+" was created.", Toast.LENGTH_SHORT).show();
        else
            Toast.makeText(this, fullFolderPath+" already exits.", Toast.LENGTH_SHORT).show();

        fullFolderPath = fullFolderPath + "/";
        mLogStatus.setText("Logging in: " + currentLogFolderPath);


        // CREATE NEW FILENAMES
        signalLoggerFilename = StringUtils.CreateTimestamp("signal","csv");
        processedSignalLoggerFilename = StringUtils.CreateTimestamp("processedSignal","csv");
        peaksAndHRVLoggerFilename = StringUtils.CreateTimestamp("peaksAndHRV","csv");

        signalLogger = new File(fullFolderPath + signalLoggerFilename);
        processedSignalLogger = new File(fullFolderPath + processedSignalLoggerFilename);
        peaksAndHRVLogger = new File(fullFolderPath + peaksAndHRVLoggerFilename);
        debugLogger = new File(fullFolderPath + "debugLogger.txt");

        try {
            // Result of peaks
            writerSignal = new BufferedWriter(new FileWriter(signalLogger, true));
            writerSignal.write("type,timestamp_usec,value,accuracy");
            writerSignal.newLine();
            writerSignal.flush();

            // Result of processed signal
            writerProcSgn = new BufferedWriter(new FileWriter(processedSignalLogger, true));
            writerProcSgn.write("denoisedSignal");
            writerProcSgn.newLine();
            writerProcSgn.flush();

            // Result of peaks and HRV
            writerPeaksAndHRV = new BufferedWriter(new FileWriter(peaksAndHRVLogger, true));
            writerPeaksAndHRV.write("lineNumber,timestampValue,calculatedHRV");
            writerPeaksAndHRV.newLine();
            writerPeaksAndHRV.flush();

            // DEBUG
            writerDebug = new BufferedWriter(new FileWriter(debugLogger, true));
            writerDebug.write("SESSION LOG");
            writerDebug.newLine();
            writerDebug.flush();
            areLoggersReady = true;

        } catch (IOException e) {
            e.printStackTrace();
        }

        // The next resets, the application needs to close the files to create new Log files
        firstLogger = false;

        // Restart Signal Analyzer info
        timeSet = new ArrayList<String>();
        valueSet = new ArrayList<Double>();
        incomingDataCounter = 0;
        firstProcessingRound = true;
    }

    public static void WriteLog(String message)
    {
        try {
            // Avoid writing when there is a new set of log files in execution
            if(areLoggersReady)
            {
                writerDebug.write(message);
                writerDebug.newLine();
            }
        } catch (IOException e){
            e.printStackTrace();
        }
    }

    //// MAIN INTERFACE CALLBACKS
    public void connectSensors(View view) {
        // TODO: Setup muse again
        // connect to Muse
        //museListenerService.museConnect();
        //Toast.makeText(getApplicationContext(), "Connecting", Toast.LENGTH_SHORT).show();
        Toast.makeText(getApplicationContext(), "Option disabled!", Toast.LENGTH_SHORT).show();
    }

    public void disconnectSensors() {
        museListenerService.museDisconnect();
    }

    public void physioVersion (View view) {
        Toast.makeText(getApplicationContext(), "PhysioSense v1.1 alpha", Toast.LENGTH_SHORT).show();
    }

    public void exitService(View view) {
        Log.d(TAG, "STOPPED by the USER");
        this.finish();
        System.exit(0);
    }

    public void RestartLogs(View v) {
        CreateOrResetLoggers();
    }

    public void UpdateDetailedLayoutsView(View v) {
        switch (v.getId()) {
            case R.id.btnTizen: {
                UpdateDetailedLayoutsView(Device.TIZEN);
                break;
            }
            case R.id.btnWear: {
                UpdateDetailedLayoutsView(Device.WEAR);
                break;
            }
            case R.id.btnMuse: {
                UpdateDetailedLayoutsView(Device.MUSE);
                break;
            }
            case R.id.btnMyo: {
                UpdateDetailedLayoutsView(Device.MYO);
                break;
            }
            default:

        }
    }

    private void UpdateDetailedLayoutsView(Device device) {

        switch (device) {
            case TIZEN: {
                if(layTizen.getVisibility() == View.INVISIBLE)
                {
                    btnTizen.setText(R.string.buttonHideDetails);
                    layTizen.setVisibility(View.VISIBLE);
                }
                else
                {
                    btnTizen.setText(R.string.buttonShowDetails);
                    layTizen.setVisibility(View.INVISIBLE);
                }

                //btnTizen.setText(R.string.buttonHideDetails);
                btnWear.setText(R.string.buttonShowDetails);
                btnMuse.setText(R.string.buttonShowDetails);
                btnMyo.setText(R.string.buttonShowDetails);

                //layTizen.setVisibility(View.INVISIBLE);
                layWear.setVisibility(View.INVISIBLE);
                layMuse.setVisibility(View.INVISIBLE);
                layMyo.setVisibility(View.INVISIBLE);

                break;
            }
            case WEAR: {
                if(layWear.getVisibility() == View.INVISIBLE)
                {
                    btnWear.setText(R.string.buttonHideDetails);
                    layWear.setVisibility(View.VISIBLE);
                }
                else
                {
                    btnWear.setText(R.string.buttonShowDetails);
                    layWear.setVisibility(View.INVISIBLE);
                }

                btnTizen.setText(R.string.buttonShowDetails);
//                btnWear.setText(R.string.buttonShowDetails);
                btnMuse.setText(R.string.buttonShowDetails);
                btnMyo.setText(R.string.buttonShowDetails);

                layTizen.setVisibility(View.INVISIBLE);
//                layWear.setVisibility(View.INVISIBLE);
                layMuse.setVisibility(View.INVISIBLE);
                layMyo.setVisibility(View.INVISIBLE);
                break;
            }
            case MUSE: {
                if(layMuse.getVisibility() == View.INVISIBLE)
                {
                    btnMuse.setText(R.string.buttonHideDetails);
                    layMuse.setVisibility(View.VISIBLE);
                }
                else
                {
                    btnMuse.setText(R.string.buttonShowDetails);
                    layMuse.setVisibility(View.INVISIBLE);
                }

                btnTizen.setText(R.string.buttonShowDetails);
                btnWear.setText(R.string.buttonShowDetails);
//                btnMuse.setText(R.string.buttonShowDetails);
                btnMyo.setText(R.string.buttonShowDetails);

                layTizen.setVisibility(View.INVISIBLE);
                layWear.setVisibility(View.INVISIBLE);
//                layMuse.setVisibility(View.INVISIBLE);
                layMyo.setVisibility(View.INVISIBLE);
                break;
            }
            case MYO: {
                if(layMyo.getVisibility() == View.INVISIBLE)
                {
                    btnMyo.setText(R.string.buttonHideDetails);
                    layMyo.setVisibility(View.VISIBLE);
                }
                else
                {
                    btnMyo.setText(R.string.buttonShowDetails);
                    layMyo.setVisibility(View.INVISIBLE);
                }

                btnTizen.setText(R.string.buttonShowDetails);
                btnWear.setText(R.string.buttonShowDetails);
                btnMuse.setText(R.string.buttonShowDetails);
//                btnMyo.setText(R.string.buttonShowDetails);

                layTizen.setVisibility(View.INVISIBLE);
                layWear.setVisibility(View.INVISIBLE);
                layMuse.setVisibility(View.INVISIBLE);
//                layMyo.setVisibility(View.INVISIBLE);
                break;
            }

            default:{
                btnTizen.setText(R.string.buttonShowDetails);
                btnWear.setText(R.string.buttonShowDetails);
                btnMuse.setText(R.string.buttonShowDetails);
                btnMyo.setText(R.string.buttonShowDetails);

                layTizen.setVisibility(View.INVISIBLE);
                layWear.setVisibility(View.INVISIBLE);
                layMuse.setVisibility(View.INVISIBLE);
                layMyo.setVisibility(View.INVISIBLE);
            }
        }

        imgLogo.setVisibility(View.INVISIBLE);

        // Show the logo when there are not active layouts
        if(layTizen.getVisibility() == View.INVISIBLE &&
                layWear.getVisibility() == View.INVISIBLE &&
                layMuse.getVisibility() == View.INVISIBLE &&
                layMyo.getVisibility() == View.INVISIBLE)
        {
            imgLogo.setVisibility(View.VISIBLE);
        }

    }


    // Tizen Details Callbacks
    public void Connect(View view) {
        // Setup everything if the logs are not configured
        if(!areLoggersReady)
            CreateOrResetLoggers();

        // Find Peers
        if (mIsBound == true && sapConsumerService != null) {
            sapConsumerService.findPeers();
            sendButtonClicked = false;
        }
    }

    public void SendTestMessage(View view) {
        if (mIsBound == true && sendButtonClicked == false && sapConsumerService != null) {
            if (sapConsumerService.sendData("Hello Message!") != -1) {
                sendButtonClicked = true;
            }else {
                sendButtonClicked = false;
            }
        }
    }

    //// TIZEN SAP CONNECTION METHODS
    private final ServiceConnection mConnection = new ServiceConnection() {
        @Override
        public void onServiceConnected(ComponentName className, IBinder service) {
            sapConsumerService = ((SAPConsumerService.LocalBinder) service).getService();
            updateTextView("onServiceConnected");
        }

        @Override
        public void onServiceDisconnected(ComponentName className) {
            sapConsumerService = null;
            mIsBound = false;
            updateTextView("onServiceDisconnected");
        }
    };

    public static void addMessage(String data) {
        mMessageAdapter.addMessage(new Message(data));
    }

    public static void updateTextView(final String str) {
        mSapStatus.setText(str);
    }

    public static void updateButtonState(boolean enable) {
        sendButtonClicked = enable;
    }

    private class MessageAdapter extends BaseAdapter {
        private static final int MAX_MESSAGES_TO_DISPLAY = 20;
        private List<Message> mMessages;

        public MessageAdapter() {
            mMessages = Collections.synchronizedList(new ArrayList<Message>());
        }

        void addMessage(final Message msg) {
            runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    if (mMessages.size() == MAX_MESSAGES_TO_DISPLAY) {
                        mMessages.remove(0);
                        mMessages.add(msg);
                    } else {
                        mMessages.add(msg);
                    }
                    notifyDataSetChanged();
                    mMessageListView.setSelection(getCount() - 1);
                }
            });
        }

        void clear() {
            runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    mMessages.clear();
                    notifyDataSetChanged();
                }
            });
        }

        @Override
        public int getCount() {
            return mMessages.size();
        }

        @Override
        public Object getItem(int position) {
            return mMessages.get(position);
        }

        @Override
        public long getItemId(int position) {
            return 0;
        }

        @Override
        public View getView(int position, View convertView, ViewGroup parent) {
            LayoutInflater inflator = (LayoutInflater) getSystemService(Context.LAYOUT_INFLATER_SERVICE);
            View messageRecordView = null;
            if (inflator != null) {
                messageRecordView = inflator.inflate(R.layout.message, null);
                TextView tvData = (TextView) messageRecordView.findViewById(R.id.tvData);
                Message message = (Message) getItem(position);
                tvData.setText(message.data);
            }
            return messageRecordView;
        }
    }

    private static final class Message {
        String data;

        public Message(String data) {
            super();
            this.data = data;
        }
    }


    //// TIMER TO CONNECT TO OTHER SERVICES
    class UDPSender extends TimerTask {
        @Override
        public void run() {

            updateStatus();

            /////////////////////////////
            //////// UDP Sender /////////
            /////////////////////////////
            // String values from the Services

            String wearHRate = "HR: " + wearListenerService.getWearHRate();
            String museValues = "MUSE: " + museListenerService.getMuseValues();
            String myoValues = "MYO" + myoListenerService.getMYOValues();

            int UDP_SERVER_PORT = 11111;

            String udpMsg = museValues + " | " + wearHRate + " | " + myoValues;
            Log.d(TAG, wearHRate + " | " + museValues + " | " + myoValues);

            DatagramSocket ds = null;
            try {
                ds = new DatagramSocket();
                InetAddress serverAddr = InetAddress.getByName("127.0.0.1");

                DatagramPacket dp;
                dp = new DatagramPacket(udpMsg.getBytes(), udpMsg.length(), serverAddr, UDP_SERVER_PORT);
                ds.send(dp);

                Log.d(TAG, udpMsg + " via " + UDP_SERVER_PORT + "to: " + serverAddr);

            } catch (SocketException e) {
                e.printStackTrace();
            } catch (UnknownHostException e) {
                e.printStackTrace();
            } catch (IOException e) {
                e.printStackTrace();
            } catch (Exception e) {
                e.printStackTrace();
            } finally {
                if (ds != null) {
                    ds.close();
                }
            }
        }

        String wearStatus = wearListenerService.getWearStatus();
        String museStatus = museListenerService.getMuseStatus();
        String myoStatus = myoListenerService.getMyoStatus();

        public void updateStatus() {

            runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    textWear.setText(wearStatus);
                    if (wearStatus.equals("Connected")) {
                        imgWear.setImageResource(R.drawable.hr_on);
                    } else {
                        imgWear.setImageResource(R.drawable.hr_off);
                    }

                    textMuse.setText(museStatus);
                    if (museStatus.equals("Connected")) {
                        imgMuse.setImageResource(R.drawable.bci_on);
                    } else {
                        imgMuse.setImageResource(R.drawable.bci_off);
                    }

                    textMyo.setText(myoStatus);
                    if (myoStatus.equals("Connected")) {
                        imgMyo.setImageResource(R.drawable.emg_on);
                    } else {
                        imgMyo.setImageResource(R.drawable.emg_off);
                    }
                }
            });
        }
    }
}
