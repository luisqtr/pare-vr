//////////////////////////////////////
// Filename: PhysiologicalSignalsPanelSettings.cs
// Author: Luis Quintero // levelezq@gmail.com
// Date: April 2019
// Description:
 /*
 File that controls the UI that uses PhysiologicalSignalsManager.cs
 to collect signals from smartwatches through UDP communication.
 
 Part of Master Thesis Project:
"Facilitating the Development of Technology for Mental Health
with Mobile Virtual Reality and Wearable Systems"
Master Program in Health Informatics 2019
@ Karolinska Institutet & Stockholm University
 */
//////////////////////////////////////

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// This class manages the UI for Physiological Adaptation, the processing class is "PhysiologicalSignalsManager"
public class PhysiologicalSignalsPanelSettings : MonoBehaviour {

	// ENUMS
	private enum BaselineOptions {
		timeNull = -1,
		time5secs = 0,
		time15secs = 1,
		time30secs = 2,
		time60secs = 3,
		time120secs = 4,
	}

	private enum CalculationOptions {
		timeNull = -1,
		time5secs = 0,
		time10secs = 1,
		time15secs = 2,
		time20secs = 3,
	}


	// BIOFEEDBACK CONTROLLER
	[SerializeField]
	private PhysiologicalSignalsManager physioManager;


	// UI PUBLIC VARIABLES

	public CanvasGroup biofeedbackPanel;

	public GameObject settingsSubpanel;
	public Slider sliderTOD;
	public TextMeshProUGUI labelTOD;

	[Header("Adaptation Goal")]
	public GameObject decreaseHRPanel;
	public GameObject maximizeHRVPanel;

	[Header("Current task and connection status")]
	public TextMeshProUGUI connectionStatusDisplay;
	public TextMeshProUGUI currentTaskDisplay;

	[Header("Button texts")]
	public TextMeshProUGUI startStopBtnText;
	private readonly string BTN_TXT_START = "Start Data Collection";
	private readonly string BTN_TXT_STOP = "Stop Data Collection";

	public TextMeshProUGUI applyRestartBtnText;
	private readonly string BTN_TXT_APPLY = "Apply Changes";
	private readonly string BTN_TXT_RESTART = "Restart Data Analysis";

	[Header("Values Texts")]
	public TextMeshProUGUI baselineValueText;
	public TextMeshProUGUI currentValueText;
	public TextMeshProUGUI targetValueText;
	

	private bool isRunning = false; 	// Is data collection and adaptation being executed?
	private bool areChangesApplied = true; // When the user changes values in the UI, where the changes applied in the processing script?

	// Temporal values that are going to be applied to the processing script.
	private float tempBaselineTime = 0.0f, tempCalculationInterval = 0.0f, tempDecreaseHR = 0.0f, tempMaximizeHRV = 0.0f;
	private AdaptationGoalOptions tempAdaptationGoal;
	private bool baselineChanged = false, calculationIntervChanged = false, adaptGoalChanged = false, decreaseHRChanged = false, maximizeHRVChanged = false;

	void OnEnable()
	{
		// Register callbacks from Physio Manager Script
		PhysiologicalSignalsManager.OnConnectionStatusChanged += ConnectionStatusChanged;
		PhysiologicalSignalsManager.OnCurrentTaskChanged += CurrentTaskChanged;
		PhysiologicalSignalsManager.OnPhysioBaselineSet += BaselineSet;
		PhysiologicalSignalsManager.OnPhysioAdaptationNotified += AdaptationNotified;
	}

	void OnDisable()
	{
		// Register callbacks from Physio Manager Script
		PhysiologicalSignalsManager.OnConnectionStatusChanged -= ConnectionStatusChanged;
		PhysiologicalSignalsManager.OnCurrentTaskChanged -= CurrentTaskChanged;
		PhysiologicalSignalsManager.OnPhysioBaselineSet -= BaselineSet;
		PhysiologicalSignalsManager.OnPhysioAdaptationNotified -= AdaptationNotified;
	}

	void Start()
	{
		InitializeUI();
	}

	// Default visibility options
	private void InitializeUI()
	{
		// Set darkness at the beginning of the scenario
		sliderTOD.value = 0.0f;

		// Hide main Panel to be opened when the user clicks
        if(biofeedbackPanel != null)
            biofeedbackPanel.alpha = 0.0f;

		// Baseline time
		ChangeBaselineTime(2); 	// The slider in position 2 means 30 secs of baseline

		// Calculation interval
		ChangeCalculationInterval(2); // The slider in position 2 means 15 secs of calculation window

		// ADAPTATION GOAL DEFAULTS
		// Choose Decrease HR as default option
		ChangeAdaptationGoal(AdaptationGoalOptions.DecreaseHR);
		// Decrease HR in 10%, setting its slider to value=2
		ChangeDecreaseHrThreshold(2);
		// Maximize HRV in 10%, setting its slider to value=2
		ChangeMaximizeHrvThreshold(2);

		// First to apply changes and then to restart acquisition
		// Equivalent to press the UI button twice
		isRunning = true;
		ApplyRestartDataAnalysis();
		ApplyRestartDataAnalysis();
		startStopBtnText.text = BTN_TXT_STOP;
	}
	
	// Apply changes to processing script, identifying only those values that were modified
	private void ApplyChangesToProcessingScript()
	{
		if(baselineChanged)
		{
			physioManager.baselineTime = tempBaselineTime;
			baselineChanged = false;	// Default again in case of new changes
		}
			
		if(calculationIntervChanged)
		{
			physioManager.calculationInterval = tempCalculationInterval;
			calculationIntervChanged = false;
		}

		if(adaptGoalChanged)
		{
			physioManager.adaptationGoal = tempAdaptationGoal;
			adaptGoalChanged = false;
		}

		if(decreaseHRChanged)
		{
			physioManager.thresholdDecreaseHR = tempDecreaseHR;
			decreaseHRChanged = false;
		}

		if(maximizeHRVChanged)
		{
			physioManager.thresholdMaximizeHRV = tempMaximizeHRV;
			maximizeHRVChanged = false;
		}

		Debug.Log("Applying changes");
	}

	////////// CALLBACKS FROM EVENTS
	private void ConnectionStatusChanged(string message)
	{
		connectionStatusDisplay.text = message;
		Debug.Log("Connection status changed: " + message);
	}

	private void CurrentTaskChanged(string message)
	{
		currentTaskDisplay.text = message;
		//Debug.Log("Current task changed: " + message);
	}

	private void BaselineSet(float baselineValue, float targetValue)
	{
		baselineValueText.text = baselineValue.ToString("F2");
		currentValueText.text = baselineValue.ToString("F2");
		targetValueText.text = targetValue.ToString("F2");

		Debug.Log("Baseline set: " + baselineValue.ToString("F2") + " . Target set: " + targetValue.ToString("F2"));
	}

	private void AdaptationNotified(PhysioPacket packet)
	{
		currentValueText.text = packet.value.ToString("F2");
		Debug.Log("Adaptation notified: " + packet.variableName + "=" + packet.value);

		CalculateDayLightAdaptation(packet.value, packet.baseline, packet.target);
	}

	// Formula to adapt the TOD depending on physiological signal
	private void CalculateDayLightAdaptation(float current, float baseline, float target)
	{
		if(sliderTOD != null)
		{
			// Linear function to adapt the values from 0-1 scale.
			float valueFrom0to1 = ((current-baseline)/(target-baseline));

			// The slider goes from "baseline = 0.0 (night)" to "target = 0.5 (daylight)"
			sliderTOD.value = valueFrom0to1/2.0f;
		} 
		else
		{
			Debug.Log("TOD Slider not set...");
		}
	}


	/////////// UI CALLBACKS
	// TOD CALLBACKS
	public void ChangeTOD(float value)
	{
		//Our slider output value needs to be adjusted into a timeline value
        if (value < 0.5)
        {
            value += 0.5f;
        }
        else
        {
            value -= 0.5f;
        }
        double scaledTime = value * 288;

        //Convert our time value into a string representing the time of day and apply it to the panel for the user to see.
        string timeString = EnvironmentManager.UpdateTimeOfDay(scaledTime);
        labelTOD.text = timeString;

        //Convert the slider value into a time value for the director.
        GlobalEvents.OnTimeLineChange(this, scaledTime);
	}

    public void ToggleVisibilityBiofeedbackPanel()
    {
        if(biofeedbackPanel != null)
        {
            biofeedbackPanel.alpha = biofeedbackPanel.alpha == 0.0f? 1.0f:0.0f;
        }
    }

	public void ToggleActivePhysiologicalAdaptation()
	{
		if(settingsSubpanel != null)
        {
            settingsSubpanel.SetActive(!settingsSubpanel.activeSelf);
        }
	}

    public void StartStopDataAcquisition()
    {
		isRunning = !isRunning;
		if(isRunning)
		{
			Debug.Log("Start Data Collection");
			startStopBtnText.text = BTN_TXT_STOP;
			if(areChangesApplied)
			{
				applyRestartBtnText.text = BTN_TXT_RESTART;
			}
			else
			{
				applyRestartBtnText.text = BTN_TXT_APPLY;
			}

			// PHYSIO MANAGER SCRIPT: Start signal acquisition
			physioManager.StartDataCollection();
		}
		else
		{
			Debug.Log("Stop Data Collection");
			startStopBtnText.text = BTN_TXT_START;

			// PHYSIO MANAGER SCRIPT: Stop signal acquisition
			physioManager.StopDataCollection();
		}

		// Show/Hide settings objects
		ToggleActivePhysiologicalAdaptation();
    }


    public void ApplyRestartDataAnalysis()
    {
		if(areChangesApplied)
		{
			sliderTOD.value = 0.0f;

			// PHYSIO MANAGER SCRIPT: Restart acquisition
			physioManager.RestartDataAnalysis();
			baselineValueText.text = "[Non-set]";
			currentValueText.text = "[Non-set]";
			targetValueText.text = "[Non-set]";
		}
		else
		{
			areChangesApplied = true;
			applyRestartBtnText.text = BTN_TXT_RESTART;

			ApplyChangesToProcessingScript();

		}
        Debug.Log("Apply/Restart Data Processing");
    }

	private void ChangesInUIWereDone()
	{
		applyRestartBtnText.text = BTN_TXT_APPLY;
		areChangesApplied = false;
	}

    public void ChangeBaselineTime(float value)
    {
		float newTime = 0.0f;
		
		BaselineOptions choice = (BaselineOptions)Mathf.RoundToInt(value);

		switch(choice)
		{
			case BaselineOptions.time5secs:
				newTime = 5.0f;
			break;
			case BaselineOptions.time15secs:
				newTime = 15.0f;
			break;
			case BaselineOptions.time30secs:
				newTime = 30.0f;
			break;
			case BaselineOptions.time60secs:
				newTime = 60.0f;
			break;
			case BaselineOptions.time120secs:
				newTime = 120.0f;
			break;
			default:
				Debug.LogError("Invalid baseline time");
				break;
		}
        Debug.Log("ChangeBaseline: " + newTime);

		// Store data to apply changes
		tempBaselineTime = newTime;
		baselineChanged = true;

		// Notify changes in UI
		ChangesInUIWereDone();
    }


	public void ChangeCalculationInterval(float value)
    {
		float newTime = 0.0f;
		
		CalculationOptions choice = (CalculationOptions)Mathf.RoundToInt(value);

		switch(choice)
		{
			case CalculationOptions.time5secs:
				newTime = 5.0f;
			break;
			case CalculationOptions.time10secs:
				newTime = 10.0f;
			break;
			case CalculationOptions.time15secs:
				newTime = 15.0f;
			break;
			case CalculationOptions.time20secs:
				newTime = 20.0f;
			break;
			default:
				Debug.LogError("Invalid calculation interval");
				break;
		}
        Debug.Log("ChangeBaseline: " + newTime);
		
		// Store data to apply changes
		tempCalculationInterval = newTime;
		calculationIntervChanged = true;

		// Notify changes in UI
		ChangesInUIWereDone();
    }

	// Used for the radiobuttons in the UI, this int value is translated to its corresponding enum
	public void ChangeAdaptationGoal(int adaptationOption)
	{
		ChangeAdaptationGoal((AdaptationGoalOptions)adaptationOption);
	}

	public void ChangeAdaptationGoal(AdaptationGoalOptions value)
    {
		// Change goal in physiological manager
		switch(value)
		{
			case AdaptationGoalOptions.None:
				// Hide both panels
				decreaseHRPanel.SetActive(false);
				maximizeHRVPanel.SetActive(false);
			break;
			case AdaptationGoalOptions.DecreaseHR:
				//Show panel HR
				decreaseHRPanel.SetActive(true);
				maximizeHRVPanel.SetActive(false);
			break;
			case AdaptationGoalOptions.MaximizeHRV:
				//Show panel HRV
				decreaseHRPanel.SetActive(false);
				maximizeHRVPanel.SetActive(true);
			break;
			default:
				Debug.LogError("Invalid adaptation goal");
				break;
		}
        Debug.Log("Change adaptation goal.");
		
		// Store data to apply changes
		tempAdaptationGoal = value;
		adaptGoalChanged = true;

		// Notify changes in UI
		ChangesInUIWereDone();
    }

	public void ChangeDecreaseHrThreshold(float value)
    {
		float newPercentage = 0.0f;

		switch(Mathf.RoundToInt(value))
		{
			case 0:
				newPercentage = 0.02f;
			break;
			case 1:
				newPercentage = 0.05f;
			break;
			case 2:
				newPercentage = 0.1f;
			break;
			case 3:
				newPercentage = 0.2f;
			break;
			default:
				Debug.LogError("Invalid HR threshold");
				break;
		}
        Debug.Log("Change decrease HR Threshold: " + newPercentage);

		// Store data to apply changes
		tempDecreaseHR = newPercentage;
		decreaseHRChanged = true;

		// Notify changes in UI
		ChangesInUIWereDone();
    }

	public void ChangeMaximizeHrvThreshold(float value)
    {
		float newPercentage = 0.0f;

		switch(Mathf.RoundToInt(value))
		{
			case 0:
				newPercentage = 0.02f;
			break;
			case 1:
				newPercentage = 0.05f;
			break;
			case 2:
				newPercentage = 0.1f;
			break;
			case 3:
				newPercentage = 0.2f;
			break;
			default:
				Debug.LogError("Invalid HRV threshold");
				break;
		}
        Debug.Log("Change Maximize HRV Threshold: " + newPercentage);

		// Store data to apply changes
		tempMaximizeHRV = newPercentage;
		maximizeHRVChanged = true;

		// Notify changes in UI
		ChangesInUIWereDone();
    }
}
