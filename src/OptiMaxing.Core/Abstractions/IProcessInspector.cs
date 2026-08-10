namespace OptiMaxing.Core.Abstractions;

public enum ProcessPriority { Idle, BelowNormal, Normal, AboveNormal, High, Realtime }

public sealed record ProcessInfo(
    int Id,
    string Name,
    string? ExecutablePath,
    long WorkingSetBytes,
    long PrivateBytes,
    int ThreadCount,
    TimeSpan? TotalProcessorTime,
    ProcessPriority? Priority,
    bool IsResponding,
    bool IsSystemCritical,
    string? AccessFailure);

public interface IProcessInspector
{
    IReadOnlyList<ProcessInfo> List();
    bool Kill(int processId);
    bool SetPriority(int processId, ProcessPriority priority);
}
