namespace OptiMaxing.Core.Abstractions;

public sealed record CpuInventory(
    string Name,
    string Manufacturer,
    int PhysicalCores,
    int LogicalCores,
    int BaseClockMhz,
    string Socket,
    int L2CacheKb,
    int L3CacheKb,
    bool VirtualizationEnabled);

public sealed record GpuInventory(
    string Name,
    ulong VideoMemoryBytes,
    string DriverVersion,
    DateTime? DriverDate,
    string VideoProcessor);

public sealed record MotherboardInventory(string Manufacturer, string Product, string Version);

public sealed record BiosInventory(string Manufacturer, string Version, DateTime? ReleaseDate);

public sealed record MemoryModuleInventory(
    string BankLabel,
    string DeviceLocator,
    ulong CapacityBytes,
    int ConfiguredClockMhz,
    int RatedClockMhz,
    string Manufacturer,
    string PartNumber)
{
    /// <summary>A module running below the speed it is rated for is the classic "XMP/EXPO is off"
    /// signature. Both numbers come from SMBIOS, so this is a hint, not proof — some boards report
    /// the rated speed as the configured one even when the profile is not active.</summary>
    public bool RunningBelowRatedSpeed => RatedClockMhz > 0 && ConfiguredClockMhz > 0 && ConfiguredClockMhz < RatedClockMhz;
}

public sealed record PhysicalDiskInventory(
    string Model,
    string InterfaceType,
    string MediaType,
    ulong SizeBytes,
    string SerialNumber);

public sealed record HardwareInventory(
    CpuInventory? Cpu,
    IReadOnlyList<GpuInventory> Gpus,
    MotherboardInventory? Motherboard,
    BiosInventory? Bios,
    IReadOnlyList<MemoryModuleInventory> MemoryModules,
    IReadOnlyList<PhysicalDiskInventory> Disks,
    IReadOnlyList<string> Failures);

/// <summary>One-shot hardware inventory. Separate from <see cref="ISystemInfoProvider"/> because
/// this is expensive (WMI round-trips) and read once at startup, while system info is polled.</summary>
public interface IHardwareInventoryProvider
{
    HardwareInventory Collect();
}
