using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RotatingLogs;

[BepInAutoPlugin(id: "io.github.flibber-hk.rotatinglogs")]
public partial class RotatingLogsPlugin : BaseUnityPlugin
{
    private const string MAX_LOGS_DESC = """
        At startup, delete all but the most recent X logs.
        Set the value to -1 to ignore this setting.
        This value is only used at startup.
        """;

    private const string MAX_DAYS_DESC = """
        At startup, delete logs older than X days.
        Set the value to -1 to ignore this setting.
        This value is only used at startup.
        """;

    private const string datetimeFormat = "yyyy-MM-dd_HH-mm-ss";

    private string logPath = Path.Combine(Paths.BepInExRootPath, "LogOutput.log");
    private string backupDir = Path.Combine(Paths.BepInExRootPath, "OldLogs");
    private string? logBackup;

    private ConfigEntry<int> MaxLogs;
    private ConfigEntry<int> MaxDays;

    private void Awake()
    {
        MaxLogs = Config.Bind("General", "MaxLogs", 20, MAX_LOGS_DESC);
        MaxDays = Config.Bind("General", "MaxDays", 7, MAX_DAYS_DESC);

        if (!File.Exists(logPath))
        {
            Logger.LogInfo("Not rotating logs: Log path not found");
            return;
        }
        Directory.CreateDirectory(backupDir);
        string datetimeString = DateTime.Now.ToString(datetimeFormat);
        logBackup = Path.Combine(backupDir, $"LogOutput_{datetimeString}.log");

        CleanupOldLogs();
        BackupLog();
        AddDiskLog();

        Logger.LogInfo($"Plugin {Name} ({Id}) has loaded!");
    }

    private void AddDiskLog()
    {
        // TODO - gather these values from the bepinex config
        LogLevel displayedLogLevel = LogLevel.All;
        bool includeUnityLog = true;

        DiskLogListener listener = new(logBackup, displayedLogLevel: displayedLogLevel, appendLog: true, includeUnityLog: includeUnityLog);
        BepInEx.Logging.Logger.Listeners.Add(listener);
    }

    private void CleanupOldLogs()
    {
        try
        {
            CleanupOldLogsInternal();
        }
        catch
        {
            // swallow errors — not critical
        }
    }

    private void CleanupOldLogsInternal()
    {
        List<FileInfo> files = Directory.GetFiles(backupDir, "LogOutput_*.log")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        foreach ((int idx, FileInfo fileInfo) in files.Select((file, idx) => (idx, file)))
        {
            if (idx == 0)
            {
                continue;
            }

            if (MaxLogs.Value != -1 && idx >= MaxLogs.Value)
            {
                try { fileInfo.Delete(); } catch { }
                continue;
            }

            DateTime logTime;
            try
            {
                logTime = DateTime.ParseExact(
                    fileInfo.Name.Substring(10, 19),
                    datetimeFormat,
                    System.Globalization.CultureInfo.InvariantCulture
                );
            }
            catch (Exception)
            {
                continue;
            }

            TimeSpan difference = DateTime.Now - logTime;
            
            if (MaxDays.Value != -1 && difference.Days > MaxDays.Value)
            {
                try { fileInfo.Delete(); } catch { }
                continue;
            }
        }
    }

    private void FlushLogs()
    {
        try
        {
            foreach (DiskLogListener listener in BepInEx.Logging.Logger.Listeners.OfType<DiskLogListener>())
            {
                listener.LogWriter.Flush();
            }
        }
        catch { }
    }

    private void BackupLog()
    {
        if (string.IsNullOrEmpty(logBackup)) return;

        try
        {
            FlushLogs();
            File.Copy(logPath, logBackup, true);
        }
        catch { }
    }
}
