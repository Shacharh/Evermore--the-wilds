using System;
using System.IO;
using UnityEngine;
using Unity.Services.Analytics;

public class FileLogger : MonoBehaviour
{
    public static FileLogger Instance { get; private set; }

    private static string logDirectory = "Logs"; // Folder to store logs
    private static string logFileName => $"log_{DateTime.Now:yyyy-MM-dd}.log"; // log_2026-01-16.log
    private static string logFilePath => Path.Combine(Application.persistentDataPath, logDirectory, logFileName);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Ensure the Logs directory exists
        string fullDirectoryPath = Path.Combine(Application.persistentDataPath, logDirectory);
        if (!Directory.Exists(fullDirectoryPath))
        {
            Directory.CreateDirectory(fullDirectoryPath);
        }
        Debug.Log("Log file path: " + logFilePath);
        // Subscribe to Unity's log message events
        Application.logMessageReceived += HandleLog;
    }

    private void OnDestroy()
    {
        // Unsubscribe when this object is destroyed
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Only write errors and exceptions to file (optional)
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            WriteLog($"{DateTime.Now:HH:mm:ss} [{type}] {logString}\n{stackTrace}\n");
        }
    }

    private void WriteLog(string message)
    {
        try
        {
            string msg = $"{message}\n=============================================================================\n\n";
            File.AppendAllText(logFilePath, msg);
            //LogSimpleEvent(msg);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to write log file: " + e.Message);
        }
    }

    public void LogSimpleEvent(string msg)
    {
        AnalyticsService.Instance.RecordEvent(msg);
        Debug.Log("Event Sent!");
    }


}
