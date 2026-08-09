using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Safety;

public sealed record HealthWarning(string Text);

public sealed record SystemHealthReport(
    string OsDescription,
    string ProcessorName,
    TimeSpan Uptime,
    MemoryInfo Memory,
    IReadOnlyList<DiskInfo> Disks,
    RestorePointStatus RestorePoint,
    IReadOnlyList<HealthWarning> Warnings)
{
    public double MemoryUsedPercent => Memory.TotalBytes == 0
        ? 0
        : (Memory.TotalBytes - Memory.AvailableBytes) * 100.0 / Memory.TotalBytes;
}

/// <summary>Builds the "Здоровье системы" snapshot shown on startup — pure read + a few
/// threshold checks, no apply/revert. Deliberately separate from OptimizationEngine: nothing
/// here is a toggleable tweak, it's context that helps the user decide which tweaks matter.</summary>
public sealed class SystemHealthService(ISystemInfoProvider systemInfo, IRestorePointService restorePoints)
{
    private const double LowDiskFreePercentThreshold = 10.0;
    private const double HighMemoryUsedPercentThreshold = 90.0;

    public SystemHealthReport GetReport()
    {
        var memory = systemInfo.GetMemoryInfo();
        var disks = systemInfo.GetFixedDrives();
        var restorePointStatus = restorePoints.GetStatus();

        var warnings = new List<HealthWarning>();

        var memoryUsedPercent = memory.TotalBytes == 0
            ? 0
            : (memory.TotalBytes - memory.AvailableBytes) * 100.0 / memory.TotalBytes;
        if (memoryUsedPercent >= HighMemoryUsedPercentThreshold)
            warnings.Add(new HealthWarning(
                $"Оперативная память загружена на {memoryUsedPercent:F0}% — это может влиять на стабильность FPS сильнее любого твика."));

        foreach (var disk in disks.Where(d => d.FreePercent < LowDiskFreePercentThreshold))
            warnings.Add(new HealthWarning(
                $"На диске {disk.Name} осталось {disk.FreePercent:F0}% свободного места — очистка кэшей во вкладке «Твики» может помочь."));

        if (restorePointStatus.BlockedByPolicy)
            warnings.Add(new HealthWarning(
                "Восстановление системы запрещено групповой политикой — продвинутые твики останутся заблокированы."));
        else if (restorePointStatus.LastRestorePointUtc is null)
            warnings.Add(new HealthWarning(
                "Точки восстановления нет — создай её перед применением твиков."));

        return new SystemHealthReport(
            systemInfo.OsDescription(),
            systemInfo.ProcessorName(),
            systemInfo.Uptime(),
            memory,
            disks,
            restorePointStatus,
            warnings);
    }
}
