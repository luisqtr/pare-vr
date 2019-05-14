//////////////////////////////////////
// Filename: CalculationJob.cs
// Author: Luis Quintero // levelezq@gmail.com
// Date: April 2019
// Description:
 /*
 File that contains the coroutines that analyze 
 mathematically the physiological data collected from the
 smartwatch sensors through UDP.
 
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

public class CalculationJob : MonoBehaviour
{
	public enum TypeOfProcessing
	{
		BaselineCalculation,
		AdaptationCalculation,
	}

	public enum MathOperation
	{
		Mean,		// Processes the mean of the array. (Used for HR)
		MaxMinDifference,		// Processes the difference between the maximum and minimum of the array. (Used for HRV)
	}

	private TypeOfProcessing typeOfProcessing;

	private MathOperation mathOperation;

	private List<BiofeedbackVariable> bfVariables;

	private string variableNameToProcess = "";

	private float calculationResult;
	private int maximumElementsPerCycle = 5;		// Process N array elements per coroutine cycle
	private int counterElementsPerCycle = 0;

	public struct ProcessingPacket
	{
		public string processedVariable;
		public float result;
		public MathOperation mathOperation;
		public TypeOfProcessing typeOfProcessing;
	}
	public ProcessingPacket result;

	public delegate void CalculationResult(ProcessingPacket packet);
	public static event CalculationResult OnCalculationFinished;


	private int indexThatContainsVariable = -1; // Searchs for the index in the "bfVariables" that has the requested tag.
	private bool variableNameWasFound = false;
	public bool isRunning = false;


	public void SetProcessingParameters(List<BiofeedbackVariable> variables, string variableNameToProcess, MathOperation mathOperation, TypeOfProcessing typeOfProcessing)
	{
		this.bfVariables = new List<BiofeedbackVariable>(variables);
		this.variableNameToProcess = variableNameToProcess;
		this.mathOperation = mathOperation;
		this.typeOfProcessing = typeOfProcessing;
	}
 
	public void StartCalculation()
	{
		StartCoroutine(ProcessingCoroutine());
	}
	
	public void StopCalculation()
	{
		StopCoroutine(ProcessingCoroutine());
		isRunning = false;
	}

	// Replaced thread by IEnumerator
	private IEnumerator ProcessingCoroutine()
	{
		isRunning = true;
		yield return null;

		// Check names of the variables to see if the required variable name is in the array
		for(int i=0; i<bfVariables.Count; i++)
		{
			if(bfVariables[i].name.CompareTo(variableNameToProcess) == 0)
			{
				indexThatContainsVariable = i;
				variableNameWasFound = true;
			}
		}

		yield return null;

		// If the variable name was not found in the variables
		if(variableNameWasFound)
		{
			// List of mathematical operations HERE!!
			switch(mathOperation)
			{
				case MathOperation.Mean:
					yield return StartCoroutine(CalculateMean(indexThatContainsVariable));
				break;
				case MathOperation.MaxMinDifference:
					yield return StartCoroutine(CalculateMaxMinDifference(indexThatContainsVariable));
				break;
			}
		}
		OnFinished();
	}
	
	protected void OnFinished()
	{
		result = new ProcessingPacket();
		result.processedVariable = variableNameToProcess;
		result.result = calculationResult;
		result.mathOperation = mathOperation;
		result.typeOfProcessing = typeOfProcessing;
		isRunning = false;

		// Send notification
		OnCalculationFinished(result);
	}


	//// MATH OPERATIONS
	private IEnumerator CalculateMean(int index)
	{
		counterElementsPerCycle = 0;
		float sum = 0.0f;
		for(int i=0; i<bfVariables[index].values.Count; i++)
		{
			sum += bfVariables[index].values[i];

			// These variables allow to process more than one value per cycle
			counterElementsPerCycle++;
			if(counterElementsPerCycle > maximumElementsPerCycle)
			{
				counterElementsPerCycle = 0;
				yield return null;
			}
		}
		calculationResult = sum/bfVariables[index].values.Count;
	}

	private IEnumerator CalculateMaxMinDifference(int index)
	{
		counterElementsPerCycle = 0;
		// Initialize max and min
		float max = bfVariables[index].values[0];
		float min = bfVariables[index].values[0];

		for(int i=1; i<bfVariables[index].values.Count; i++)
		{
			if(bfVariables[index].values[i] > max)
			{
				max = bfVariables[index].values[i];
			}
			else if(bfVariables[index].values[i] < min)
			{
				min = bfVariables[index].values[i];
			}

			// These variables allow to process more than one value per cycle
			counterElementsPerCycle++;
			if(counterElementsPerCycle > maximumElementsPerCycle)
			{
				counterElementsPerCycle = 0;
				yield return null;
			}
		}
		calculationResult = max-min;
	}
}
