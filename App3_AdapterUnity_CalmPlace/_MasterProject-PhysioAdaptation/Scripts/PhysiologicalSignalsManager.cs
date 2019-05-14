//////////////////////////////////////
// Filename: PhysiologicalSignals.cs
// Author: Luis Quintero // levelezq@gmail.com
// Date: April 2019
// Description:
 /*
Controls the acquisition and processing of physiological data
through UDP communication, to be used in real-time adaptation of
the virtual reality environments.
 
 Part of Master Thesis Project:
"Facilitating the Development of Technology for Mental Health
with Mobile Virtual Reality and Wearable Systems"
Master Program in Health Informatics 2019
@ Karolinska Institutet & Stockholm University
 */
//////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysiologicalSignalsManager : MonoBehaviour 
{
	// GENERAL VARIABLES

    [Header("Signal information")]
    public string HR_TAG = "HR";
    public string HRV_TAG = "HRV";


    [Header("General Options")]
    [SerializeField]
    private float m_baselineTime = 15.0f;
    public float baselineTime{
        get { return m_baselineTime; }
        set { m_baselineTime = value; }
    }
    [SerializeField]
    private float m_calculationInterval = 5.0f;
    public float calculationInterval{
        get { return m_calculationInterval; }
        set { m_calculationInterval = value; }
    }
    [SerializeField]
    private AdaptationGoalOptions m_adaptationGoal = AdaptationGoalOptions.None;
    public AdaptationGoalOptions adaptationGoal {
        get {return m_adaptationGoal; }
        set {m_adaptationGoal = value; }
    }
    [SerializeField]
    private float m_thresholdDecreaseHR = 0.1f;
    public float thresholdDecreaseHR{
        get { return m_thresholdDecreaseHR; }
        set { m_thresholdDecreaseHR = value; }
    }
    [SerializeField]
    private float m_thresholdMaximizeHRV = 0.1f;
    public float thresholdMaximizeHRV{
        get { return m_thresholdMaximizeHRV; }
        set { m_thresholdMaximizeHRV = value; }
    }
    
    private bool isActive = true;   // Whether signals receiving is active or not
    private bool isDelayedCoroutineExecuting = false;      // Coroutine for delayed execution
    private bool isCurrentTaskDisplayBusy = false;         // To avoid overriding different task messages at the same time
    //private ProcessingJob threadedCalculationJob;
    private CalculationJob calculationJobs;

    private UDPReceiver2 udpListener;
    private PhysioPacket lastReceivedPacket = new PhysioPacket();
    private PhysioPacket adaptationPacket = new PhysioPacket();
    private List<BiofeedbackVariable> bfVariables;


    private TaskOptions currentTask = TaskOptions.Stopped;
    private float remainingTimeForBaseline = 0.0f;
    private float remainingTimeForCalculation = 0.0f;


    private float calculatedBaseline = 0.0f;
    private float calculatedTarget = 0.0f;


    // EVENTS
    public delegate void PhysioMessage(string message);
    public static event PhysioMessage OnConnectionStatusChanged;
    public static event PhysioMessage OnCurrentTaskChanged;

    public delegate void PhysioBaseline(float baselineValue, float targetValue);
    public static event PhysioBaseline OnPhysioBaselineSet;

    public delegate void PhysioAdaptation(PhysioPacket value);
    public static event PhysioAdaptation OnPhysioAdaptationNotified;

    // SINGLETON
	public static PhysiologicalSignalsManager instance;

    // <summary>
    /// Set instance for settings object and initialize callbacks of UI
    /// </summary>
    private void Awake()
    {
        // Check singleton, each time the menu scene is loaded, the instance is replaced with the newest script
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            // Update Singleton when loading a new scene
            Destroy(instance.gameObject);
            instance = this;
        }

        // Add processing jobs coroutine in runtime to the same object
        calculationJobs = gameObject.AddComponent<CalculationJob>();

        // Start entities
        udpListener = new UDPReceiver2();
        bfVariables = new List<BiofeedbackVariable>();
    }

    private void OnEnable()
    {
        CalculationJob.OnCalculationFinished += CalculationFinished;
    }

    private void OnDisabled()
    {
        CalculationJob.OnCalculationFinished -= CalculationFinished;
    }

    public void OnDestroy()
    {
        udpListener.CloseReceiver();
    }

    public void Update()
    {
        // Only process if it is active
        if(!isActive)
            return;

        // Read if UDP message was received
        if(udpListener._messagesQueued)
        {
            List<string> messages = udpListener.ReadQueuedDatagrams();
            Debug.Log("MessageQueued: Total = " + messages.Count);

            foreach(string datagram in messages)
            {
                OnConnectionStatusChanged("Receiving messages through UDP:\n[" + datagram + "]");
                ParseData(datagram);
                ProcessBioFeedbackPacket(lastReceivedPacket);
            }
        }

        // Check current mode to know how to process data. If it is in baseline mode or in calculation mode.
        if(currentTask == TaskOptions.CalculatingBaseline)
        {
            remainingTimeForBaseline -= Time.deltaTime;
            if(!isCurrentTaskDisplayBusy)
                OnCurrentTaskChanged("Calculating Baseline (" + baselineTime.ToString("F0") + "secs)...\nRemaining: " + remainingTimeForBaseline.ToString("F1") + "secs.");

            if(remainingTimeForBaseline < 0.0f)
            {
                currentTask = TaskOptions.CollectingData;
                remainingTimeForCalculation = calculationInterval;

                if(!calculationJobs.isRunning)
                {
                    if(bfVariables.Count == 0)
                    {
                        OnCurrentTaskChanged("No received data for baseline. Restarting...");
                        StartCoroutine(LockCurrentTaskMessage(2f,UnlockCurrentTaskMessage));
                        currentTask = TaskOptions.CalculatingBaseline;
                        remainingTimeForBaseline = baselineTime;
                    }
                    else
                    {
                        /// There is data for baseline    
                        switch(adaptationGoal)
                        {
                            case AdaptationGoalOptions.None:
                                OnCurrentTaskChanged("No adaptation was set");
                                // Show text for a while before
                                StartCoroutine(LockCurrentTaskMessage(2f,UnlockCurrentTaskMessage));
                                break;
                            case AdaptationGoalOptions.DecreaseHR:
                                // ExecuteThreadedProcessingJob(HR_TAG, ProcessingJob.MathOperation.Mean, ProcessingJob.TypeOfProcessing.BaselineCalculation);
                                ExecuteCoroutineCalculationJob(HR_TAG, CalculationJob.MathOperation.Mean, CalculationJob.TypeOfProcessing.BaselineCalculation);
                                break;
                            case AdaptationGoalOptions.MaximizeHRV:
                                // ExecuteThreadedProcessingJob(HRV_TAG, ProcessingJob.MathOperation.MaxMinDifference, ProcessingJob.TypeOfProcessing.BaselineCalculation);
                                ExecuteCoroutineCalculationJob(HRV_TAG, CalculationJob.MathOperation.MaxMinDifference, CalculationJob.TypeOfProcessing.BaselineCalculation);
                                break;
                            default:
                                Debug.LogError("Adaptation goal invalid option");
                                break;
                        }
                    }

                    
                }
                else
                {
                    Debug.Log("Baseline: Job is running");
                }
            }
        }
        else if(currentTask == TaskOptions.CollectingData)
        {
            remainingTimeForCalculation -= Time.deltaTime;

            if(!isCurrentTaskDisplayBusy)
            {
                // The delayed coroutine allows to see the adaptation message for a moment, while the collection of data is still in progress...
                OnCurrentTaskChanged("Preparing next adaptation (" + calculationInterval.ToString("F0") + "secs)...\nRemaining: " + remainingTimeForCalculation.ToString("F1") + "secs.");
            }
            
            if(remainingTimeForCalculation < 0.0f)
            {
                currentTask = TaskOptions.CalculatingNewAdaptation;
                remainingTimeForCalculation = calculationInterval;

                // When time is up, the "currentTask" changes and the next update cycle will go in the calculation stage
            }
        }
        else if(currentTask == TaskOptions.CalculatingNewAdaptation)
        {
            // Continue collecting data while the adaptation calculation is done
            remainingTimeForCalculation -= Time.deltaTime;

            if(!calculationJobs.isRunning)
            {
                if(bfVariables.Count == 0)
                {
                    OnCurrentTaskChanged("No received data for adaptation...");
                    StartCoroutine(LockCurrentTaskMessage(2f,UnlockCurrentTaskMessage));
                }
                else
                {
                    // There is data for adaptation
                    switch(adaptationGoal)
                    {
                        case AdaptationGoalOptions.None:
                            OnCurrentTaskChanged("No adaptation was set");
                            // Show text for a while before
                            StartCoroutine(LockCurrentTaskMessage(2f,UnlockCurrentTaskMessage));
                            break;
                        case AdaptationGoalOptions.DecreaseHR:
                            OnCurrentTaskChanged("Calculating adaptation based on HR...");
                            // Thread with calculation of data to set adaptation.
                            // ExecuteThreadedProcessingJob(HR_TAG, ProcessingJob.MathOperation.Mean, ProcessingJob.TypeOfProcessing.AdaptationCalculation);
                            ExecuteCoroutineCalculationJob(HR_TAG, CalculationJob.MathOperation.Mean, CalculationJob.TypeOfProcessing.AdaptationCalculation);
                            break;
                        case AdaptationGoalOptions.MaximizeHRV:
                            OnCurrentTaskChanged("Calculating adaptation based on HRV...");
                            // Thread with calculation of data to set adaptation.
                            // ExecuteThreadedProcessingJob(HRV_TAG, ProcessingJob.MathOperation.MaxMinDifference, ProcessingJob.TypeOfProcessing.AdaptationCalculation);
                            ExecuteCoroutineCalculationJob(HRV_TAG, CalculationJob.MathOperation.MaxMinDifference, CalculationJob.TypeOfProcessing.AdaptationCalculation);
                            break;
                        default:
                            Debug.LogError("Adaptation goal invalid option");
                            break;
                    }
                }

                
                // Continue collecting data
                currentTask = TaskOptions.CollectingData;
            }
            else
            {
                Debug.Log("Adaptation: Job is running");
            }
        }
    }

    ////// MAIN CALLBACKS FOR SCRIPT THAT MANAGES UI
    public void StartDataCollection()
    {
        Debug.Log("Starting Data Collection");
        udpListener.OpenReceiver();
        isActive = true;
        OnConnectionStatusChanged("UDP receiver is OPEN.");
    }

    public void StopDataCollection()
    {
        Debug.Log("Stopping Data Collection");
        udpListener.CloseReceiver();
        isActive = false;
        OnConnectionStatusChanged("UDP receiver is CLOSED.");
    }

    public void RestartDataAnalysis()
    {
        Debug.Log("Restaring Data Analysis");
        
        // Restart baseline analysis
        ClearVariables();
        currentTask = TaskOptions.CalculatingBaseline;
        remainingTimeForBaseline = baselineTime;
        remainingTimeForCalculation = calculationInterval;

        // Stop current coroutines
        StopAllCoroutines();
        isDelayedCoroutineExecuting = false;
        isCurrentTaskDisplayBusy = false;

        StopDataCollection();
        StartDataCollection();
    }

    /////////// OTHER CALLBACKS
    public void GetInfoAboutThread()
    {
        Debug.Log(udpListener.GetInfo());
    }


    /// <summary>
    /// Splits the incoming data and instantiates/updates variables
    /// </summary>
    /// <param name="message"></param>
    public void ParseData(string message)
    {
        string[] separators = { ",", ";", "|"};

        string[] words = message.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        
        // Packet format for data received from PhysioSense
        lastReceivedPacket.type = PhysioPacket.TypeOfPacket.StreamingVariable;
        lastReceivedPacket.variableName = words[0];
        lastReceivedPacket.timestamp = long.Parse(words[1]);
        lastReceivedPacket.value = float.Parse(words[2]);
        lastReceivedPacket.accuracy = int.Parse(words[3]);
    }

    /// <summary>
    /// Process new packets that are related with BioFeedback Variables
    /// </summary>
    /// <param name="packet"></param>
    public void ProcessBioFeedbackPacket(PhysioPacket packet)
    {
        bool existingVariable = false;

        // Check whether the variable was already configured
        for(int i=0; i<bfVariables.Count; i++)
        {
            // If the variable name already exists...
            if(bfVariables[i].name.CompareTo(packet.variableName) == 0)
            {
                existingVariable = true;
                bfVariables[i].values.Add(packet.value);
                break;
            }
        }

        // New variable to save
        if(!existingVariable)
        {
            BiofeedbackVariable newBfVariable = new BiofeedbackVariable();
            newBfVariable.name = packet.variableName;
            newBfVariable.values = new List<float>(){packet.value};
            bfVariables.Add(newBfVariable);
        }
    }
    
    /// <summary>
    /// Restart biofeedback received variables
    /// </summary>
    public void ClearVariables()
    {
        if(bfVariables != null)
            bfVariables.Clear();
    }

    private void UnlockCurrentTaskMessage()
    {
        isCurrentTaskDisplayBusy = false;
    }

    private void ExecuteCoroutineCalculationJob(string variableNameToProcess, CalculationJob.MathOperation mathOperation, CalculationJob.TypeOfProcessing processingType)
    {
        // Run the thread to make the calculation only if it is not running.
        // THIS IMPLIES THAT the threaded job needs to be finished before the next calculation. worst case, in 5sec
        if(!calculationJobs.isRunning)
        {
            // Starts the thread to make the calculation
            
            ///// Threads
            //threadedCalculationJob = new ProcessingJob(bfVariables,variableNameToProcess,mathOperation, processingType);
            //threadedCalculationJob.Start();
            // StartCoroutine(CheckCalculationThread());

            // Calculation coroutine
            calculationJobs.SetProcessingParameters(bfVariables,variableNameToProcess,mathOperation, processingType);
            calculationJobs.StartCalculation();

            // Empty array to start make next adaptation with new data.
            ClearVariables();
        }
        else
        {
            Debug.LogError("Calculation not possible, job is still running...");
        }
    }

    private void CalculationFinished(CalculationJob.ProcessingPacket packet)
    {
        Debug.Log("Calculation Finished: " + packet.processedVariable + "," + packet.result.ToString("F2"));

        switch(packet.typeOfProcessing)
        {
			case CalculationJob.TypeOfProcessing.BaselineCalculation:
                calculatedBaseline = packet.result;
                // Baseline depending on type of adaptation
                if(adaptationGoal == AdaptationGoalOptions.DecreaseHR && packet.mathOperation == CalculationJob.MathOperation.Mean)
                {
                    calculatedTarget = (1.0f-thresholdDecreaseHR)*calculatedBaseline;
                }
                else if(adaptationGoal == AdaptationGoalOptions.MaximizeHRV && packet.mathOperation == CalculationJob.MathOperation.MaxMinDifference)
                {
                    calculatedTarget = (1.0f+thresholdMaximizeHRV)*calculatedBaseline;
                }

                OnPhysioBaselineSet(calculatedBaseline, calculatedTarget);
                OnCurrentTaskChanged("Baseline finished... Variable: " + packet.processedVariable +
                                    " New baseline= " + calculatedBaseline.ToString("F1") + "]\n" +
                                    " New target= " + calculatedTarget.ToString("F1"));
			break;
			case CalculationJob.TypeOfProcessing.AdaptationCalculation:
                adaptationPacket = new PhysioPacket();
                adaptationPacket.variableName = packet.processedVariable;
                adaptationPacket.value = packet.result;
                adaptationPacket.baseline = calculatedBaseline;
                adaptationPacket.target = calculatedTarget;

                // Send adaptation values to be processed by receiver
                OnPhysioAdaptationNotified(adaptationPacket);
                OnCurrentTaskChanged("Calculation finished... New adaptation: [" + adaptationPacket.variableName + "=" + adaptationPacket.value + "]");

			break;
        }
        StartCoroutine(LockCurrentTaskMessage(1.5f,UnlockCurrentTaskMessage));
    }

    // private void CalculationFinished(ProcessingJob.ProcessingPacket packet)
    // {
    //     Debug.Log("Calculation Finished: " + packet.processedVariable + "," + packet.result.ToString("F2"));

    //     switch(packet.typeOfProcessing)
    //     {
	// 		case ProcessingJob.TypeOfProcessing.BaselineCalculation:
    //             calculatedBaseline = packet.result;
    //             // Baseline depending on type of adaptation
    //             if(adaptationGoal == AdaptationGoalOptions.DecreaseHR && packet.mathOperation == ProcessingJob.MathOperation.Mean)
    //             {
    //                 calculatedTarget = (1.0f-thresholdDecreaseHR)*calculatedBaseline;
    //             }
    //             else if(adaptationGoal == AdaptationGoalOptions.MaximizeHRV && packet.mathOperation == ProcessingJob.MathOperation.MaxMinDifference)
    //             {
    //                 calculatedTarget = (1.0f-thresholdMaximizeHRV)*calculatedBaseline;
    //             }

    //             OnPhysioBaselineSet(calculatedBaseline, calculatedTarget);
    //             OnCurrentTaskChanged("Baseline finished... Variable: " + packet.processedVariable +
    //                                 "New baseline= " + calculatedBaseline.ToString("F1") + "]\n" +
    //                                 "New target= " + calculatedTarget.ToString("F1"));
	// 		break;
	// 		case ProcessingJob.TypeOfProcessing.AdaptationCalculation:
    //             adaptationPacket = new PhysioPacket();
    //             adaptationPacket.variableName = packet.processedVariable;
    //             adaptationPacket.value = packet.result;
    //             OnPhysioAdaptationNotified(adaptationPacket);
    //             OnCurrentTaskChanged("Calculation finished...\nNew adaptation: [" + adaptationPacket.variableName + "=" + adaptationPacket.value + "]");
	// 		break;
    //     }
    //     StartCoroutine(LockCurrentTaskMessage(1.5f,UnlockCurrentTaskMessage));
    // }

    //////////// COROUTINES
    // Avoid overlap of messages about the current task
    IEnumerator LockCurrentTaskMessage(float time, Action task)
    {
        if (isDelayedCoroutineExecuting)
            yield break;

        isDelayedCoroutineExecuting = true;
        isCurrentTaskDisplayBusy = true;
        yield return new WaitForSeconds(time);
        task();
        isDelayedCoroutineExecuting = false;
    }

    // IEnumerator CheckCalculationThread()
    // {
    //     if (isThreadCoroutineExecuting)
    //         yield break;

    //     isThreadCoroutineExecuting = true;
    //     yield return StartCoroutine(threadedCalculationJob.WaitFor());
    //     CalculationFinished(threadedCalculationJob.result);
    //     isThreadCoroutineExecuting = false;
    // }
}