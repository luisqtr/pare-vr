using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class CustomSlider : MonoBehaviour {

    Slider sliderObj;
    public Text textToDisplay;
    public int divisionFactor = 10;

    [Header("Min-Max Setup")]
    public Text minValueText;
    public Text maxValueText;

    public void Awake()
    {
        sliderObj = GetComponent<Slider>();

        ConvertSliderToText();
        if (minValueText != null)
            minValueText.text = sliderObj.minValue.ToString();

    }

    //// General options
    public void ConvertSliderToText()
    {
        if(textToDisplay != null)
            textToDisplay.text = (sliderObj.value / divisionFactor).ToString();
    }

    public void SetSliderValue(float value)
    {
        sliderObj.value = value * divisionFactor;
    }

    public float GetSliderValue()
    {
        return sliderObj.value / divisionFactor;
    }

    public void SetSliderMinValue(float value)
    {
        sliderObj.minValue = value * divisionFactor;
        if (minValueText != null)
            minValueText.text = sliderObj.minValue.ToString();
    }

    public float GetSliderMinValue()
    {
        return sliderObj.minValue / divisionFactor;
    }

    public void SetSliderMaxValue(float value)
    {
        sliderObj.maxValue = value * divisionFactor;
        if (maxValueText != null)
            maxValueText.text = sliderObj.maxValue.ToString();
    }

    public float GetSliderMaxValue()
    {
        return sliderObj.maxValue / divisionFactor;
    }
}
