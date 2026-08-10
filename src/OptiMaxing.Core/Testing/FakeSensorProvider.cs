using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Testing;

public sealed class FakeSensorProvider : ISensorProvider
{
    public bool IsAvailable { get; set; } = true;
    public string? UnavailableReason { get; set; }
    public List<SensorReading> Readings { get; } = [];

    public IReadOnlyList<SensorReading> Read() => IsAvailable ? Readings : [];

    public void Dispose()
    {
    }
}
