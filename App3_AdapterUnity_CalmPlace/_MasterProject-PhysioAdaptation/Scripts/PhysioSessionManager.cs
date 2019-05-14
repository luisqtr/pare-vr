using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysioSessionManager : MonoBehaviour {

	public GameObject objectThatGuidesBreathing;

	public static PhysioSessionManager instance;

	private float timeToChangeStage2 = 0.0f; // Time to activate guided breathing
	private bool isInStage1=false, isInStage2 = false, isSessionFinished = false; 

	private bool waitingForStartOfStage1 = false, waitingForStartOfStage2 = false, waitingForFinal = false;
	private string lastReceivedMessageFromSmartwatch;
	private bool receivedDataToStartStage1 = false, receivedDataToStartStage2 = false; // Set the timestamps of start of stage 1, and start of stage 2.

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

	void OnEnable()
	{
		// Register callbacks from Physio Manager Script
		PhysiologicalSignalsManager.OnConnectionStatusChanged += ConnectionStatusChanged;
	}

	void OnDisable()
	{
		// Register callbacks from Physio Manager Script
		PhysiologicalSignalsManager.OnConnectionStatusChanged -= ConnectionStatusChanged;
	}


	void Start()
	{
		// Start folder and files with recorded data
		LoggerController.instance.SetupNewLog();
		LoggerController.instance.SetIsLogging(true);
	}

	void Update()
	{
		// The GameManager is delayed to start. So, the setup is done when the GameManager is ready.
		if(!isInStage1 && !isInStage2 && GameManager.instance.fullSessionLength > 0.0f && !isSessionFinished)
		{
			// Start stage 1, baseline
			isInStage1 = true;
			isInStage2 = false;

			timeToChangeStage2 = GameManager.instance.fullSessionLength / 2.0f;
			Debug.Log("Time to start stage 2: " + timeToChangeStage2.ToString("F2"));

			if(objectThatGuidesBreathing != null)
				objectThatGuidesBreathing.SetActive(false);

			WriteEventLog("Starting Stage 1");
			WriteEventLog("Full session time: " + GameManager.instance.fullSessionLength.ToString("F2"));
			WriteEventLog("Time to start stage 2: " + timeToChangeStage2.ToString("F2"));

			waitingForStartOfStage1 = true;
		}

		if(isInStage1 && !isInStage2 && GameManager.instance.timeSinceStart > timeToChangeStage2)
		{
			// Start stage 2, guided breathing
			isInStage1 = false;
			isInStage2 = true;

			if(objectThatGuidesBreathing != null)
				objectThatGuidesBreathing.SetActive(true);
			
			WriteEventLog("Starting Stage 2");

			waitingForStartOfStage2 = true;
		}

		// Save data half a second before finishing the session
		if(!isInStage1 && isInStage2 && GameManager.instance.timeSinceStart + 1f > GameManager.instance.fullSessionLength)
		{
			isInStage1 = false;
			isInStage2 = false;
			isSessionFinished = true;
			
			WriteEventLog("Stopping Session");

			waitingForFinal = true;
		}
	}


	////////// CALLBACKS FROM EVENTS
	private void ConnectionStatusChanged(string message)
	{
		// THE NEXT MESSAGE RECEIVED THROUGH UDP IN EACH STAGE OF THE SESSION IS RECORDED TO SYNCH DATA WITH SESSIONS.

		if(waitingForStartOfStage1)
		{
			WriteEventLog("SYNCH:: START OF STAGE 1 in message: " + message);
			waitingForStartOfStage1 = false;
		}

		if(waitingForStartOfStage2)
		{
			WriteEventLog("SYNCH:: START OF STAGE 2 in message: " + message);
			waitingForStartOfStage2 = false;
		}

		if(waitingForFinal)
		{
			WriteEventLog("SYNCH:: END OF SESSION in message: " + message);
			waitingForFinal = false;
		}

		WritePhysioLog(message);
	}


	// METHODS
	public static void WriteEventLog(string line)
	{
		LoggerController.WriteLine(LoggerController.Type.Events, line);
	}

	public static void WritePhysioLog(string line)
	{
		LoggerController.WriteLine(LoggerController.Type.PhysioData, line);
	}
}
