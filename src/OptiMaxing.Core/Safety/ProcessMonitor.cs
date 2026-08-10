using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Safety;

/// <summary>A process row plus the CPU load it drew since the previous poll. Windows only exposes
/// cumulative processor time, so a percentage only exists relative to a previous sample — the first
/// poll therefore reports null rather than a misleading zero.</summary>
public sealed record ProcessSample(ProcessInfo Process, double? CpuPercent);

public sealed class ProcessMonitor(IProcessInspector inspector)
{
    private readonly Dictionary<int, (TimeSpan Cpu, DateTime At)> _previous = [];

    /// <summary>Injectable so tests can drive elapsed time instead of sleeping.</summary>
    public Func<DateTime> Clock { get; init; } = () => DateTime.UtcNow;

    public IReadOnlyList<ProcessSample> Poll()
    {
        var now = Clock();
        var processors = Environment.ProcessorCount;
        var processes = inspector.List();
        var samples = new List<ProcessSample>(processes.Count);

        foreach (var process in processes)
        {
            double? percent = null;

            if (process.TotalProcessorTime is { } cpu)
            {
                if (_previous.TryGetValue(process.Id, out var last) && now > last.At)
                {
                    var elapsed = (now - last.At).TotalMilliseconds;
                    var used = (cpu - last.Cpu).TotalMilliseconds;
                    percent = Math.Clamp(used / (elapsed * processors) * 100.0, 0, 100);
                }

                _previous[process.Id] = (cpu, now);
            }

            samples.Add(new ProcessSample(process, percent));
        }

        // Without this the dictionary grows for the lifetime of the app on a machine that churns
        // through short-lived processes (build servers, shells).
        var alive = processes.Select(p => p.Id).ToHashSet();
        foreach (var dead in _previous.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _previous.Remove(dead);
        }

        return samples;
    }

    public bool Kill(int processId) => inspector.Kill(processId);

    public bool SetPriority(int processId, ProcessPriority priority) =>
        inspector.SetPriority(processId, priority);
}
