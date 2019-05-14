using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BiofeedbackManager : MonoBehaviour {

    
    public Text lastMessageUI;
    public Text debugMessageUI;

    [Header("UI Biofeedback")]
    public Text bfText1;
    public CustomSlider bfSlider1;
    public Text bfText2;
    public CustomSlider bfSlider2;
    public Text bfText3;
    public CustomSlider bfSlider3;


    private const int numberOfBfvariables = 3;
    private BiofeedbackVariable[] bfVariables;

    private UDPReceiver2 udpListener;

    public static BiofeedbackManager instance;

    // Public general variables
    bool isActive = true;   // Whether signals receiving is active or not


    private struct BiofeedbackVariable
    {
        public bool slotOccupied;
        public Text label;
        public CustomSlider slider;
    }

    public struct Packet {
        public enum TypeOfPacket
        {
            StreamingVariable,
            AdaptedGameVariable,
        }

        public TypeOfPacket type;
        public string variableName;
        public long timestamp;  // Monotonic timestamp
        public int accuracy;
        public float value;
        public float min, max;
    }



    /// <summary>
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


        // Variables
        bfVariables = new BiofeedbackVariable[numberOfBfvariables];

        BiofeedbackVariable bf1 = new BiofeedbackVariable();
        bf1.slotOccupied = false;
        bf1.label = bfText1;
        bf1.slider = bfSlider1;

        BiofeedbackVariable bf2 = new BiofeedbackVariable();
        bf2.slotOccupied = false;
        bf2.label = bfText2;
        bf2.slider = bfSlider2;

        BiofeedbackVariable bf3 = new BiofeedbackVariable();
        bf3.slotOccupied = false;
        bf3.label = bfText3;
        bf3.slider = bfSlider3;

        bfVariables[0] = bf1;
        bfVariables[1] = bf2;
        bfVariables[2] = bf3;
    }

    public void Start()
    {
        udpListener = new UDPReceiver2();

        ClearVariables();

        SetupUDP();
    }

    public void SetupUDP()
    {
        if(udpListener._isConnected)
        {
            debugMessageUI.text = "UDP listener ENABLED!";
            udpListener.OpenReceiver();
        }
        else
        {
            debugMessageUI.text = "UDP listener DISABLED!";
            udpListener.CloseReceiver();
        }
        // Handheld.Vibrate();
    }

    public void OnDestroy()
    {
        udpListener.CloseReceiver();
    }

    public void Update()
    {
        if(udpListener._messagesQueued)
        {
            List<string> messages = udpListener.ReadQueuedDatagrams();
            Debug.Log("MessageQueued: Total = " + messages.Count);

            foreach(string datagram in messages)
            {
                lastMessageUI.text = datagram;
                ParseData(datagram);
            }
        }
    }

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
        
        // Packet format for data received from Tizen Smartwatch
        Packet lastPacket = new Packet();
        lastPacket.type = Packet.TypeOfPacket.StreamingVariable;
        lastPacket.variableName = words[0];
        lastPacket.timestamp = long.Parse(words[1]);
        lastPacket.value = float.Parse(words[2]);
        lastPacket.accuracy = int.Parse(words[3]);
        lastPacket.min = 40.0f;
        lastPacket.max = 150.0f;

        if(lastPacket.variableName.CompareTo("PPG") == 0)
        {
            lastPacket.value = lastPacket.value/10000;
            lastPacket.min = 0.0f;
            lastPacket.max = 300.0f;
        }

        /*
        // Check min-max values to parse
        float testParsing;
        if (float.TryParse(words[3], out testParsing))
            packet.min = testParsing;
        else
            packet.min = 0f;

        if (float.TryParse(words[4], out testParsing))
            packet.max = testParsing;
        else
            packet.max = 100f;
        */

        ProcessBioFeedbackPacket(lastPacket);
    }


    /// <summary>
    /// Process new packets that are related with BioFeedback Variables
    /// </summary>
    /// <param name="packet"></param>
    public void ProcessBioFeedbackPacket(Packet packet)
    {
        bool existingVariable = false;
        // Check whether the variable was already configured
        for(int i=0; i< numberOfBfvariables; i++)
        {
            if(bfVariables[i].slotOccupied && bfVariables[i].label.text.CompareTo(packet.variableName) == 0)
            {
                existingVariable = true;
                // Packet already exists in list
                bfVariables[i].slider.SetSliderMinValue(packet.min);
                bfVariables[i].slider.SetSliderMaxValue(packet.max);
                bfVariables[i].slider.SetSliderValue(packet.value);
            }
        }

        if(!existingVariable)
        {
            // New variable to save
            for (int i = 0; i < numberOfBfvariables; i++)
            {
                // Occupy the first empty slot
                if (!bfVariables[i].slotOccupied)
                {
                    bfVariables[i].slotOccupied = true;
                    bfVariables[i].label.text = packet.variableName;
                    bfVariables[i].slider.SetSliderMinValue(packet.min);
                    bfVariables[i].slider.SetSliderMaxValue(packet.max);
                    bfVariables[i].slider.SetSliderValue(packet.value);

                    bfVariables[i].label.gameObject.SetActive(true);
                    bfVariables[i].slider.gameObject.SetActive(true);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Restart biofeedback received variables
    /// </summary>
    public void ClearVariables()
    {
        for (int i = 0; i < numberOfBfvariables; i++)
        {
            bfVariables[i].slotOccupied = false;
            bfVariables[i].label.text = "Biofeedback Variable :";
            bfVariables[i].slider.SetSliderMinValue(0f);
            bfVariables[i].slider.SetSliderMaxValue(100f);
            bfVariables[i].slider.SetSliderValue(50f);

            bfVariables[i].label.gameObject.SetActive(false);
            bfVariables[i].slider.gameObject.SetActive(false);
        }
    }

    
}
