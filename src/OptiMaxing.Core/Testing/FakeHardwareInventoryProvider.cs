using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Testing;

public sealed class FakeHardwareInventoryProvider : IHardwareInventoryProvider
{
    public CpuInventory? Cpu { get; set; }
    public List<GpuInventory> Gpus { get; } = [];
    public MotherboardInventory? Motherboard { get; set; }
    public BiosInventory? Bios { get; set; }
    public List<MemoryModuleInventory> MemoryModules { get; } = [];
    public List<PhysicalDiskInventory> Disks { get; } = [];
    public List<string> Failures { get; } = [];

    public HardwareInventory Collect() =>
        new(Cpu, Gpus, Motherboard, Bios, MemoryModules, Disks, Failures);
}
