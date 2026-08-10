using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Safety;
using OptiMaxing.Core.Testing;
using Xunit;

namespace OptiMaxing.Tests;

public class ProcessMonitorTests
{
    private static ProcessInfo Process(int id, string name, TimeSpan? cpu, bool critical = false) =>
        new(id, name, @"C:\app.exe", 1024, 1024, 4, cpu, ProcessPriority.Normal, true, critical, null);

    [Fact]
    public void The_first_poll_reports_no_percentage_because_there_is_nothing_to_compare_against()
    {
        var inspector = new FakeProcessInspector();
        inspector.Seed(Process(1, "game", TimeSpan.FromSeconds(10)));
        var monitor = new ProcessMonitor(inspector) { Clock = () => new DateTime(2026, 1, 1) };

        Assert.Null(Assert.Single(monitor.Poll()).CpuPercent);
    }

    [Fact]
    public void Cpu_percent_is_the_processor_time_delta_over_wall_time_and_core_count()
    {
        var now = new DateTime(2026, 1, 1);
        var inspector = new FakeProcessInspector();
        inspector.Seed(Process(1, "game", TimeSpan.Zero));
        var monitor = new ProcessMonitor(inspector) { Clock = () => now };

        monitor.Poll();

        now = now.AddSeconds(1);
        // One full second of CPU across one second of wall time = one core saturated.
        inspector.Replace(1, Process(1, "game", TimeSpan.FromSeconds(1)));

        var expected = 100.0 / Environment.ProcessorCount;
        Assert.Equal(expected, Assert.Single(monitor.Poll()).CpuPercent!.Value, 3);
    }

    [Fact]
    public void A_process_that_exits_stops_being_reported()
    {
        var inspector = new FakeProcessInspector();
        inspector.Seed(Process(1, "game", TimeSpan.Zero));
        var monitor = new ProcessMonitor(inspector);

        Assert.True(monitor.Kill(1));
        Assert.Empty(monitor.Poll());
        Assert.Equal([1], inspector.Killed);
    }

    [Fact]
    public void Priority_changes_are_forwarded_to_the_inspector()
    {
        var inspector = new FakeProcessInspector();
        inspector.Seed(Process(1, "game", TimeSpan.Zero));
        var monitor = new ProcessMonitor(inspector);

        Assert.True(monitor.SetPriority(1, ProcessPriority.High));
        Assert.Equal((1, ProcessPriority.High), Assert.Single(inspector.PriorityChanges));
    }

    [Fact]
    public void A_failed_kill_is_reported_rather_than_thrown()
    {
        var inspector = new FakeProcessInspector { KillSucceeds = false };
        inspector.Seed(Process(1, "csrss", TimeSpan.Zero, critical: true));

        Assert.False(new ProcessMonitor(inspector).Kill(1));
    }
}
