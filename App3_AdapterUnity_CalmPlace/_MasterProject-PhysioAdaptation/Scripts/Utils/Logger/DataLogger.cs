using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Manage a file to log data
/// </summary>
public class DataLogger
{
    private string filepath = "";
    private TextWriter file;

    public bool isLogging = false;

    public DataLogger(List<string> headers, string filepath)
    {
        this.filepath = filepath;
        file = new StreamWriter(filepath, true);

        //builds the string that will be the _header of the csv _file
        var fillHeader = "timestamp";   // Start of the header file

        for (var i = 0; i < headers.Count; i++)
        {
            fillHeader = fillHeader + "," + headers[i];
        }
        isLogging = true;
        //writes the first line of the _file (_header)
        file.WriteLine(fillHeader);
    }

    ~DataLogger()
    {
        file.Close();
    }

    public bool WriteLine(string line)
    {
        if(isLogging && filepath != "")
        {
            string timestamp = GetTimestamp(DateTime.Now);

            file.WriteLine(timestamp + "," + line);
            return true;
        }
        // Should reach here only if the file cannot be written.
        return false;
    }

    public void Close()
    {
        file.Close();
    }

    public static string GetTimestamp(DateTime value)
    {
        return value.ToString("yyyy-MM-dd|HH:mm:ss.fff");
    }
}