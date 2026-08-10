namespace OptiMaxing.Core.Abstractions;

public enum SensorComponent
{
    Cpu,
    Gpu,
    Motherboard,
    Memory,
    Storage,
    Network,
    Other,
}

public enum SensorKind
{
    Temperature,
    Clock,
    Load,
    Fan,
    Voltage,
    Power,
    Data,
    Level,
    Throughput,
    Other,
}

public sealed record SensorReading(
    SensorComponent Component,
    string HardwareName,
    SensorKind Kind,
    string SensorName,
    float Value,
    string Unit);

/// <summary>Live hardware sensors. Reading these needs a kernel-mode driver, which may fail to
/// load (no elevation, Secure Boot policy, driver blocked by security software). That is an
/// expected outcome, not an error: callers must handle <see cref="IsAvailable"/> being false and
/// still show everything else, so the app degrades to "sensors unavailable" instead of failing.</summary>
public interface ISensorProvider : IDisposable
{
    bool IsAvailable { get; }
    string? UnavailableReason { get; }

    IReadOnlyList<SensorReading> Read();
}
