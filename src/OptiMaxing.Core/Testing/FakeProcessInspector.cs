using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Testing;

public sealed class FakeProcessInspector : IProcessInspector
{
    private readonly List<ProcessInfo> _processes = [];

    public List<int> Killed { get; } = [];
    public List<(int Id, ProcessPriority Priority)> PriorityChanges { get; } = [];
    public bool KillSucceeds { get; set; } = true;

    public void Seed(ProcessInfo process) => _processes.Add(process);

    public void Replace(int id, ProcessInfo updated)
    {
        _processes.RemoveAll(p => p.Id == id);
        _processes.Add(updated);
    }

    public IReadOnlyList<ProcessInfo> List() => _processes.ToList();

    public bool Kill(int processId)
    {
        if (!KillSucceeds)
        {
            return false;
        }

        Killed.Add(processId);
        _processes.RemoveAll(p => p.Id == processId);
        return true;
    }

    public bool SetPriority(int processId, ProcessPriority priority)
    {
        PriorityChanges.Add((processId, priority));
        return true;
    }
}
