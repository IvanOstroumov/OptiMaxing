using System.Diagnostics.Eventing.Reader;
using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Platform;

public sealed class RealEventLogProvider : IEventLogProvider
{
    public IReadOnlyList<EventLogEntryInfo> ReadRecent(string logName, TimeSpan lookback)
    {
        var results = new List<EventLogEntryInfo>();
        var cutoffUtc = DateTime.UtcNow - lookback;

        try
        {
            var query = new EventLogQuery(logName, PathType.LogName)
            {
                ReverseDirection = true,
            };

            using var reader = new EventLogReader(query);

            EventRecord? record;
            while ((record = reader.ReadEvent()) != null)
            {
                using (record)
                {
                    try
                    {
                        var timestampUtc = record.TimeCreated?.ToUniversalTime() ?? DateTime.MinValue;
                        if (timestampUtc < cutoffUtc)
                        {
                            // Log is read newest-first; once we're past the cutoff, stop scanning.
                            break;
                        }

                        results.Add(new EventLogEntryInfo(
                            logName,
                            record.ProviderName ?? string.Empty,
                            record.Id,
                            timestampUtc,
                            SafeFormatMessage(record)));
                    }
                    catch (EventLogException)
                    {
                        // Skip malformed individual records rather than aborting the whole scan.
                    }
                }
            }
        }
        catch (Exception ex) when (ex is EventLogException or UnauthorizedAccessException)
        {
            // No access to the log or it doesn't exist — degrade to an empty result.
        }

        return results;
    }

    private static string SafeFormatMessage(EventRecord record)
    {
        try
        {
            return record.FormatDescription() ?? string.Empty;
        }
        catch (EventLogException)
        {
            return string.Empty;
        }
    }
}
