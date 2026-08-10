using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Testing;

public sealed class InMemoryEventLogProvider : IEventLogProvider
{
    private readonly List<EventLogEntryInfo> _entries = new();

    public void Seed(EventLogEntryInfo entry) => _entries.Add(entry);

    public IReadOnlyList<EventLogEntryInfo> ReadRecent(string logName, TimeSpan lookback)
    {
        var cutoffUtc = DateTime.UtcNow - lookback;
        return _entries
            .Where(e => e.LogName == logName && e.TimestampUtc >= cutoffUtc)
            .OrderByDescending(e => e.TimestampUtc)
            .ToList();
    }
}
