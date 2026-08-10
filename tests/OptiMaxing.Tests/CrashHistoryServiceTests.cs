using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Crashes;
using OptiMaxing.Core.Testing;
using Xunit;

namespace OptiMaxing.Tests;

public class CrashHistoryServiceTests
{
    private static (CrashHistoryService Service, InMemoryEventLogProvider EventLog) Build()
    {
        var eventLog = new InMemoryEventLogProvider();
        var service = new CrashHistoryService(eventLog);
        return (service, eventLog);
    }

    [Fact]
    public void A_WER_bugcheck_event_is_reported_with_its_hex_code()
    {
        var (service, eventLog) = Build();
        eventLog.Seed(new EventLogEntryInfo(
            "System", "Microsoft-Windows-WER-SystemErrorReporting", 1001,
            DateTime.UtcNow.AddDays(-1),
            "The computer has rebooted from a bugcheck. Bugcheck code was 0x0000009F. Bug check details."));

        var crashes = service.GetRecentCrashes(TimeSpan.FromDays(7));

        var crash = Assert.Single(crashes);
        Assert.Equal(CrashKind.BugCheck, crash.Kind);
        Assert.Equal("0x0000009F", crash.BugCheckCode);
    }

    [Fact]
    public void A_kernel_power_41_event_is_reported_as_unexpected_shutdown()
    {
        var (service, eventLog) = Build();
        eventLog.Seed(new EventLogEntryInfo(
            "System", "Microsoft-Windows-Kernel-Power", 41,
            DateTime.UtcNow.AddDays(-2),
            "The system has rebooted without cleanly shutting down first."));

        var crashes = service.GetRecentCrashes(TimeSpan.FromDays(7));

        var crash = Assert.Single(crashes);
        Assert.Equal(CrashKind.UnexpectedShutdown, crash.Kind);
        Assert.Null(crash.BugCheckCode);
    }

    [Fact]
    public void Unrelated_events_from_other_providers_are_ignored()
    {
        var (service, eventLog) = Build();
        eventLog.Seed(new EventLogEntryInfo(
            "System", "Microsoft-Windows-Kernel-General", 1,
            DateTime.UtcNow.AddDays(-1),
            "Unrelated informational event."));

        var crashes = service.GetRecentCrashes(TimeSpan.FromDays(7));

        Assert.Empty(crashes);
    }

    [Fact]
    public void Results_are_ordered_newest_first()
    {
        var (service, eventLog) = Build();
        eventLog.Seed(new EventLogEntryInfo(
            "System", "Microsoft-Windows-Kernel-Power", 41, DateTime.UtcNow.AddDays(-5), "older"));
        eventLog.Seed(new EventLogEntryInfo(
            "System", "Microsoft-Windows-Kernel-Power", 41, DateTime.UtcNow.AddDays(-1), "newer"));

        var crashes = service.GetRecentCrashes(TimeSpan.FromDays(7));

        Assert.Equal(2, crashes.Count);
        Assert.True(crashes[0].TimestampUtc > crashes[1].TimestampUtc);
    }
}
