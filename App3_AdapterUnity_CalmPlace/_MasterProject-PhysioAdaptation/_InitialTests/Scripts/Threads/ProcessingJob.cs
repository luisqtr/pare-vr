using System.Collections.Generic;
using UnityEngine;

public class ProcessingJob : ThreadedJob 
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
	
	public struct ProcessingPacket
	{
		public string processedVariable;
		public float result;
		public MathOperation mathOperation;
		public TypeOfProcessing typeOfProcessing;
	}
	public ProcessingPacket result;

	private int indexThatContainsVariable = -1; // Searchs for the index in the "bfVariables" that has the requested tag.
	private bool variableNameWasFound = false;

	public ProcessingJob(List<BiofeedbackVariable> variables, string variableNameToProcess, MathOperation mathOperation, TypeOfProcessing typeOfProcessing)
	{
		this.bfVariables = variables;
		this.variableNameToProcess = variableNameToProcess;
		this.mathOperation = mathOperation;
		this.typeOfProcessing = typeOfProcessing;
	}

	// Do your threaded task. DON'T use the Unity API here
	protected override void ThreadFunction()
	{
		// Check names of the variables to see if the required variable name is in the array
		for(int i=0; i<bfVariables.Count; i++)
		{
			if(bfVariables[i].name.CompareTo(variableNameToProcess) == 0)
			{
				indexThatContainsVariable = i;
				variableNameWasFound = true;
			}
		}

		// If the variable name was not found in the variables
		if(variableNameWasFound)
		{
			// List of mathematical operations HERE!!
			switch(mathOperation)
			{
				case MathOperation.Mean:
					calculationResult = CalculateMean(indexThatContainsVariable);
				break;
				case MathOperation.MaxMinDifference:
					calculationResult = CalculateMaxMinDifference(indexThatContainsVariable);
				break;
			}
		}
	}
	
	// Thread-safe. CAN use the Unity API here
	protected override void OnFinished()
	{
		result = new ProcessingPacket();
		result.processedVariable = variableNameToProcess;
		result.result = calculationResult;
		result.mathOperation = mathOperation;
		result.typeOfProcessing = typeOfProcessing;

		// TODO: Consider deleting this Debug messages
		switch(typeOfProcessing)
		{
			case TypeOfProcessing.BaselineCalculation:
				Debug.Log("FINISHED THREAD: Baseline Calculation!!");
			break;
			case TypeOfProcessing.AdaptationCalculation:
				Debug.Log("FINISHED THREAD: Adaptation Calculation!!");
			break;
		}
	}

	//// MATH OPERATIONS

	private float CalculateMean(int index)
	{
		float sum = 0.0f;
		for(int i=0; i<bfVariables[index].values.Count; i++)
		{
			sum += bfVariables[index].values[i];
		}
		return sum/bfVariables[index].values.Count;
	}

	private float CalculateMaxMinDifference(int index)
	{
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
		}

		return max-min;
	}
}
