using System.Diagnostics;
using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Platform;

public sealed class WindowsProcessInspector : IProcessInspector
{
    /// <summary>Killing any of these takes the machine down immediately (bugcheck CRITICAL_PROCESS_DIED)
    /// or logs the user out. Matched by image name, which is not localized.</summary>
    private static readonly HashSet<string> CriticalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "Memory Compression",
        "smss", "csrss", "wininit", "winlogon", "services", "lsass", "lsaiso",
        "svchost", "fontdrvhost", "dwm", "sihost", "ctfmon", "audiodg",
        "MsMpEng", "SecurityHealthService", "WUDFHost", "spoolsv",
    };

    public IReadOnlyList<ProcessInfo> List()
    {
        var result = new List<ProcessInfo>();

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                result.Add(Describe(process));
            }
        }

        return result;
    }

    private static ProcessInfo Describe(Process process)
    {
        // Any of these getters can throw for a process we are not allowed to open (protected
        // processes, or one that exited between enumeration and here). We still want the row —
        // showing "нет доступа" is more useful than silently dropping the process from the list.
        string? path = null;
        TimeSpan? cpu = null;
        ProcessPriority? priority = null;
        var responding = true;
        string? failure = null;

        try
        {
            path = process.MainModule?.FileName;
        }
        catch (Exception ex)
        {
            failure = ex.Message;
        }

        try
        {
            cpu = process.TotalProcessorTime;
            priority = MapPriority(process.PriorityClass);
            responding = process.Responding;
        }
        catch (Exception ex)
        {
            failure ??= ex.Message;
        }

        return new ProcessInfo(
            process.Id,
            process.ProcessName,
            path,
            process.WorkingSet64,
            process.PrivateMemorySize64,
            process.Threads.Count,
            cpu,
            priority,
            responding,
            CriticalNames.Contains(process.ProcessName),
            failure);
    }

    public bool Kill(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool SetPriority(int processId, ProcessPriority priority)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.PriorityClass = MapPriority(priority);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static ProcessPriority MapPriority(ProcessPriorityClass value) => value switch
    {
        ProcessPriorityClass.Idle => ProcessPriority.Idle,
        ProcessPriorityClass.BelowNormal => ProcessPriority.BelowNormal,
        ProcessPriorityClass.AboveNormal => ProcessPriority.AboveNormal,
        ProcessPriorityClass.High => ProcessPriority.High,
        ProcessPriorityClass.RealTime => ProcessPriority.Realtime,
        _ => ProcessPriority.Normal,
    };

    private static ProcessPriorityClass MapPriority(ProcessPriority value) => value switch
    {
        ProcessPriority.Idle => ProcessPriorityClass.Idle,
        ProcessPriority.BelowNormal => ProcessPriorityClass.BelowNormal,
        ProcessPriority.AboveNormal => ProcessPriorityClass.AboveNormal,
        ProcessPriority.High => ProcessPriorityClass.High,
        ProcessPriority.Realtime => ProcessPriorityClass.RealTime,
        _ => ProcessPriorityClass.Normal,
    };
}
