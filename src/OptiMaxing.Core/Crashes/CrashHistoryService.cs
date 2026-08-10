using System.Text.RegularExpressions;
using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Crashes;

public enum CrashKind { BugCheck, UnexpectedShutdown, Other }

public sealed record CrashEvent(DateTime TimestampUtc, CrashKind Kind, string Summary, string? BugCheckCode, string RawMessage);

public sealed class CrashHistoryService(IEventLogProvider eventLog)
{
    private const string LogName = "System";
    private const string WerProvider = "Microsoft-Windows-WER-SystemErrorReporting";
    private const string PowerProvider = "Microsoft-Windows-Kernel-Power";
    private const int BugCheckEventId = 1001;
    private const int UnexpectedShutdownEventId = 41;

    private static readonly Regex BugCheckCodePattern = new(@"0x[0-9A-Fa-f]{8}", RegexOptions.Compiled);

    public IReadOnlyList<CrashEvent> GetRecentCrashes(TimeSpan lookback)
    {
        var raw = eventLog.ReadRecent(LogName, lookback);
        var results = new List<CrashEvent>();

        foreach (var entry in raw)
        {
            if (entry.ProviderName == WerProvider && entry.EventId == BugCheckEventId)
            {
                var code = ExtractBugCheckCode(entry.Message);
                results.Add(new CrashEvent(
                    entry.TimestampUtc,
                    CrashKind.BugCheck,
                    code is null ? "Синий экран (BSOD)" : $"Синий экран (BSOD), код {code}",
                    code,
                    entry.Message));
            }
            else if (entry.ProviderName == PowerProvider && entry.EventId == UnexpectedShutdownEventId)
            {
                results.Add(new CrashEvent(
                    entry.TimestampUtc,
                    CrashKind.UnexpectedShutdown,
                    "Неожиданное выключение (не через штатное завершение работы)",
                    null,
                    entry.Message));
            }
        }

        return results.OrderByDescending(c => c.TimestampUtc).ToList();
    }

    private static string? ExtractBugCheckCode(string message)
    {
        var match = BugCheckCodePattern.Match(message);
        return match.Success ? match.Value : null;
    }
}
