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
    private string logPath = Path.Combine(Paths.BepInExRootPath, "LogOutput.log");
    private string backupDir = Path.Combine(Paths.BepInExRootPath, "OldLogs");
    private string? logBackup;

    private ConfigEntry<int> MaxLogs;

    private void Awake()
    {
        MaxLogs = Config.Bind("General", "MaxLogs", 20, "The number of old logs to keep. This value is only used at startup.");

        if (!File.Exists(logPath))
        {
            Logger.LogInfo("Not rotating logs: Log path not found");
            return;
        }
        Directory.CreateDirectory(backupDir);
        logBackup = Path.Combine(backupDir, $"LogOutput_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");

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
            List<FileInfo> files = Directory.GetFiles(backupDir, "LogOutput_*.log")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            if (files.Count <= MaxLogs.Value)
            {
                return;
            }

            foreach (FileInfo file in files.Skip(MaxLogs.Value))
            {
                try { file.Delete(); } catch { }
            }
        }
        catch
        {
            // swallow errors — not critical
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
