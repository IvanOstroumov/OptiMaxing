using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Safety;
using OptiMaxing.Core.Testing;
using Xunit;

namespace OptiMaxing.Tests;

public class SensorMonitorTests
{
    private static SensorReading Temp(float value) =>
        new(SensorComponent.Cpu, "CPU", SensorKind.Temperature, "Core", value, "°C");

    [Fact]
    public void Session_max_keeps_the_peak_after_the_value_drops()
    {
        var provider = new FakeSensorProvider();
        var monitor = new SensorMonitor(provider);

        provider.Readings.Add(Temp(50));
        monitor.Poll();

        provider.Readings[0] = Temp(88);
        monitor.Poll();

        provider.Readings[0] = Temp(40);
        var samples = monitor.Poll();

        Assert.Equal(40, samples[0].Reading.Value);
        Assert.Equal(88, samples[0].SessionMax);
    }

    [Fact]
    public void Resetting_forgets_the_previous_peak()
    {
        var provider = new FakeSensorProvider();
        var monitor = new SensorMonitor(provider);

        provider.Readings.Add(Temp(88));
        monitor.Poll();
        monitor.ResetSessionMax();

        provider.Readings[0] = Temp(40);
        Assert.Equal(40, monitor.Poll()[0].SessionMax);
    }

    [Fact]
    public void Unavailable_sensors_produce_no_samples_instead_of_throwing()
    {
        var provider = new FakeSensorProvider { IsAvailable = false, UnavailableReason = "драйвер не загрузился" };
        var monitor = new SensorMonitor(provider);

        Assert.Empty(monitor.Poll());
        Assert.False(monitor.IsAvailable);
        Assert.Equal("драйвер не загрузился", monitor.UnavailableReason);
    }
}
