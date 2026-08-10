namespace OptiMaxing.Core.Abstractions;

public sealed record EventLogEntryInfo(string LogName, string ProviderName, int EventId, DateTime TimestampUtc, string Message);

/// <summary>Wraps Windows Event Log reads so crash-history detection can be tested
/// without touching the real System event log.</summary>
public interface IEventLogProvider
{
    IReadOnlyList<EventLogEntryInfo> ReadRecent(string logName, TimeSpan lookback);
}
