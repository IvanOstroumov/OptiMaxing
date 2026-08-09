namespace OptiMaxing.Core.Abstractions;

public sealed record MemoryInfo(ulong TotalBytes, ulong AvailableBytes);

public sealed record DiskInfo(string Name, long FreeBytes, long TotalBytes)
{
    public double FreePercent => TotalBytes == 0 ? 0 : FreeBytes * 100.0 / TotalBytes;
}

/// <summary>Read-only machine facts for the "Здоровье системы" screen. Kept separate from
/// IRegistryProvider/IProcessRunner etc. because it's a pure read seam with no apply/revert
/// concept — nothing here is ever "applied", so it doesn't belong in the optimization model.</summary>
public interface ISystemInfoProvider
{
    string OsDescription();
    TimeSpan Uptime();
    MemoryInfo GetMemoryInfo();
    IReadOnlyList<DiskInfo> GetFixedDrives();
    string ProcessorName();
}
