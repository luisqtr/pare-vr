/*
    -----------------------
    based on UDP-Receive
    -----------------------
     [url]http://msdn.microsoft.com/de-de/library/bb979228.aspx#ID0E3BAC[/url]
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Assets.Manager;

public class UDPReceiver2 {

    public delegate void UDPMessageEvent(String data);      /**< Type of event that contains the data. */
    public static event UDPMessageEvent OnDatagramReceived; /**< Specific implementation of the event that is triggered when a new Datagram is received. */

    private Thread _receiveThread;
    private UdpClient _client;
    public bool _isConnected; 

    private int _port; 
    public static string Port = "1111";

    private string _message = "";
    private List<string> messagesQueue;
    public bool _messagesQueued = false;
    public bool _messagesRead = false;
    
    /// <summary>
    /// create new thread to receive incoming messages.
    /// </summary>
    private void Init()
    {
        messagesQueue = new List<string>();

        _receiveThread = new Thread(new ThreadStart(ReceiveData));
        _receiveThread.IsBackground = true;
        _receiveThread.Start();
    }

    public void CloseReceiver()
    {
        if (_receiveThread != null)
        {
            _receiveThread.Abort();
            _client.Close();
            _isConnected = false;
        }
    }

    public void OpenReceiver()
    {
        if (!_isConnected)
        {
            _port = int.Parse(Port);
            Init();
            _isConnected = true;
        }
    }

    /// <summary>
    /// receive thread
    /// </summary>
    private void ReceiveData()
    {
        _client = new UdpClient(_port);

        while (_isConnected)
        {
            try
            {
                // receive Bytes from 127.0.0.1
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Loopback, 0);

                byte[] data = _client.Receive(ref anyIP);

                //  UTF8 encoding in the text format.
                _message = Encoding.UTF8.GetString(data);

                // Restart queue if main thread read the data
                if(_messagesRead)
                    messagesQueue.Clear();

                messagesQueue.Add(_message);
                _messagesQueued = true;

                //DataManager.ParseData(_message);
                // OnDatagramReceived(_message);
            }
            catch (Exception err)
            {
                err.ToString();
            }
        }
    }

    public List<string> ReadQueuedDatagrams()
    {
        _messagesRead = true;
        _messagesQueued = false;
        return messagesQueue;
    }

    //get local ip address
    public static string LocalIPAddress()
    {
        IPHostEntry host;
        string localIP = "";
        host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (IPAddress ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                localIP = ip.ToString();
        }

        return localIP;
    }

    public string GetInfo()
    {
        return "Running:" + _isConnected + "|lastMessage:" + _message + "|queuedMessages?:" + _messagesQueued;
    }

}
