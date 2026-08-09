using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Testing;

/// <summary>In-memory ISystemInfoProvider for tests — every field is settable so a test can
/// exercise the "low disk space" / "low RAM" warning thresholds in SystemHealthService without
/// touching the real machine.</summary>
public sealed class FakeSystemInfoProvider : ISystemInfoProvider
{
    public string Os { get; set; } = "Windows 11 Pro 25H2 (build 26200)";
    public string Processor { get; set; } = "Fake CPU";
    public TimeSpan UptimeValue { get; set; } = TimeSpan.FromHours(3);
    public MemoryInfo Memory { get; set; } = new(16_000_000_000, 8_000_000_000);
    public List<DiskInfo> Disks { get; } = [];

    public string OsDescription() => Os;
    public string ProcessorName() => Processor;
    public TimeSpan Uptime() => UptimeValue;
    public MemoryInfo GetMemoryInfo() => Memory;
    public IReadOnlyList<DiskInfo> GetFixedDrives() => Disks;
}
