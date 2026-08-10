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
    IReadOnlyList<HealthWarning> Warnings,
    HardwareInventory Hardware)
{
    public double MemoryUsedPercent => Memory.TotalBytes == 0
        ? 0
        : (Memory.TotalBytes - Memory.AvailableBytes) * 100.0 / Memory.TotalBytes;
}

/// <summary>Builds the "Здоровье системы" snapshot shown on startup — pure read + a few
/// threshold checks, no apply/revert. Deliberately separate from OptimizationEngine: nothing
/// here is a toggleable tweak, it's context that helps the user decide which tweaks matter.</summary>
public sealed class SystemHealthService(
    ISystemInfoProvider systemInfo,
    IRestorePointService restorePoints,
    IHardwareInventoryProvider hardware)
{
    private const double LowDiskFreePercentThreshold = 10.0;
    private const double HighMemoryUsedPercentThreshold = 90.0;
    private const int StaleGpuDriverMonths = 12;

    public SystemHealthReport GetReport()
    {
        var memory = systemInfo.GetMemoryInfo();
        var disks = systemInfo.GetFixedDrives();
        var restorePointStatus = restorePoints.GetStatus();
        var inventory = hardware.Collect();

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

        warnings.AddRange(InspectHardware(inventory));

        return new SystemHealthReport(
            systemInfo.OsDescription(),
            systemInfo.ProcessorName(),
            systemInfo.Uptime(),
            memory,
            disks,
            restorePointStatus,
            warnings,
            inventory);
    }

    /// <summary>Turns raw inventory into the handful of conclusions worth acting on. Deliberately
    /// conservative: each check is worded as an observation the user can verify, because every
    /// underlying source here is a hint rather than proof.</summary>
    private static IEnumerable<HealthWarning> InspectHardware(HardwareInventory inventory)
    {
        foreach (var module in inventory.MemoryModules.Where(m => m.RunningBelowRatedSpeed))
        {
            yield return new HealthWarning(
                $"Модуль памяти в слоте {module.DeviceLocator} работает на {module.ConfiguredClockMhz} МГц " +
                $"при заявленных {module.RatedClockMhz} МГц — похоже, в BIOS не включён профиль XMP/EXPO.");
        }

        // Windows reports the memory's *current* speed in both SMBIOS fields, so a kit sold as
        // 3200 running at 2667 often shows 2667/2667 and the check above stays silent. The part
        // number is the only remaining hint, and it is a vendor-formatted string, so this only
        // ever produces a "check it yourself" note — never a claim that XMP is off.
        foreach (var module in inventory.MemoryModules.Where(HasSpeedInPartNumberAboveConfigured))
        {
            yield return new HealthWarning(
                $"Модуль {module.PartNumber} в слоте {module.DeviceLocator} работает на {module.ConfiguredClockMhz} МГц, " +
                "хотя его маркировка обещает больше — стоит проверить, включён ли XMP/EXPO в BIOS.");
        }

        foreach (var gpu in inventory.Gpus.Where(g => g.DriverDate is not null))
        {
            var age = DateTime.Now - gpu.DriverDate!.Value;
            if (age.TotalDays > StaleGpuDriverMonths * 30)
            {
                yield return new HealthWarning(
                    $"Драйвер {gpu.Name} от {gpu.DriverDate:yyyy-MM-dd} — старше года. " +
                    "Обновление драйвера обычно даёт больше, чем любой твик в этом приложении.");
            }
        }

        foreach (var disk in inventory.Disks.Where(d => d.MediaType == "HDD"))
        {
            yield return new HealthWarning(
                $"{disk.Model} — это HDD. Игры с него будут грузиться в разы дольше, чем с SSD; " +
                "никакая программная оптимизация этого не компенсирует.");
        }

        foreach (var failure in inventory.Failures)
        {
            yield return new HealthWarning($"Не удалось прочитать данные о железе — {failure}");
        }
    }

    private static bool HasSpeedInPartNumberAboveConfigured(MemoryModuleInventory module)
    {
        if (module.ConfiguredClockMhz <= 0 || module.RunningBelowRatedSpeed)
        {
            return false;
        }

        // Look for a plausible DDR speed grade embedded in the part number (e.g. "…-3200XG").
        var digits = new string(module.PartNumber.Where(char.IsDigit).ToArray());
        for (var start = 0; start + 4 <= digits.Length; start++)
        {
            if (int.TryParse(digits.AsSpan(start, 4), out var candidate)
                && candidate is >= 1600 and <= 8400
                && candidate % 100 == 0
                && candidate > module.ConfiguredClockMhz)
            {
                return true;
            }
        }

        return false;
    }
}
