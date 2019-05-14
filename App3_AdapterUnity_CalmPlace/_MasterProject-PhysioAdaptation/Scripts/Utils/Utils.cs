using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// STRUCTS
public struct PhysioPacket {
	public enum TypeOfPacket
	{
		None,
		BaselineCalculation,	// Transmit packet after baseline
		StreamingVariable,		// Transmit raw data from the sensor
		AdaptedGameVariable,	// Transmit values of an adapted game variable after calculation vs. baseline
	}

    public TypeOfPacket type;
	
	// Variables used for Type=StreamingVariable
	public string variableName;
	public long timestamp;  // Monotonic timestamp
	public int accuracy;
	public float value;

	// Variables used for Type=AdaptedVariable
	public float baseline;
	public float target;

	public PhysioPacket(TypeOfPacket type)
	{
		this.type = type;
		variableName = "NoName";
		timestamp = 0L;
		accuracy = 0;
		value = 0.0f;
		baseline = 0.0f;
		target = 0.0f;
	}
}

public struct BiofeedbackVariable
{
	public String name;
	public List<float> values;
}

// ENUMS
public enum AdaptationGoalOptions {
	None = 0,
	DecreaseHR = 1,
	MaximizeHRV = 2,
}

public enum TaskOptions {
	Stopped = 0,
	CalculatingBaseline,
	CollectingData,
	CalculatingNewAdaptation,
}