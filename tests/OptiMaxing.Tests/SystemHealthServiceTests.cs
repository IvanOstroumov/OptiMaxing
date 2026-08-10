using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Safety;
using OptiMaxing.Core.Testing;

namespace OptiMaxing.Tests;

public class SystemHealthServiceTests
{
    private static (SystemHealthService Service, FakeSystemInfoProvider SysInfo, FakeRestorePointService RestorePoints, FakeHardwareInventoryProvider Hardware) Build()
    {
        var sysInfo = new FakeSystemInfoProvider();
        var restorePoints = new FakeRestorePointService();
        var hardware = new FakeHardwareInventoryProvider();
        return (new SystemHealthService(sysInfo, restorePoints, hardware), sysInfo, restorePoints, hardware);
    }

    [Fact]
    public void Healthy_machine_has_no_warnings()
    {
        var (service, sysInfo, restorePoints, _) = Build();
        sysInfo.Memory = new MemoryInfo(TotalBytes: 16_000_000_000, AvailableBytes: 12_000_000_000);
        sysInfo.Disks.Add(new DiskInfo("C:\\", FreeBytes: 500_000_000_000, TotalBytes: 1_000_000_000_000));
        restorePoints.Status = new RestorePointStatus(true, false, DateTimeOffset.UtcNow.AddHours(-1));

        var report = service.GetReport();

        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void High_memory_usage_produces_a_warning()
    {
        var (service, sysInfo, _, _) = Build();
        sysInfo.Memory = new MemoryInfo(TotalBytes: 16_000_000_000, AvailableBytes: 1_000_000_000); // ~94% used

        var report = service.GetReport();

        Assert.Contains(report.Warnings, w => w.Text.Contains("Оперативная память"));
    }

    [Fact]
    public void Low_disk_space_produces_a_warning_naming_the_drive()
    {
        var (service, sysInfo, _, _) = Build();
        sysInfo.Disks.Add(new DiskInfo("D:\\", FreeBytes: 5_000_000_000, TotalBytes: 1_000_000_000_000)); // 0.5% free

        var report = service.GetReport();

        Assert.Contains(report.Warnings, w => w.Text.Contains("D:\\"));
    }

    [Fact]
    public void Missing_restore_point_produces_a_warning()
    {
        var (service, _, restorePoints, _) = Build();
        restorePoints.Status = new RestorePointStatus(false, false, null);

        var report = service.GetReport();

        Assert.Contains(report.Warnings, w => w.Text.Contains("Точки восстановления нет"));
    }

    [Fact]
    public void Policy_block_takes_priority_over_missing_restore_point_message()
    {
        var (service, _, restorePoints, _) = Build();
        restorePoints.Status = new RestorePointStatus(false, true, null);

        var report = service.GetReport();

        Assert.Contains(report.Warnings, w => w.Text.Contains("групповой политикой"));
        Assert.DoesNotContain(report.Warnings, w => w.Text.Contains("Точки восстановления нет"));
    }

    private static MemoryModuleInventory Module(int configured, int rated, string partNumber) =>
        new("BANK 0", "DIMM 0", 16UL * 1024 * 1024 * 1024, configured, rated, "Kingston", partNumber);

    [Fact]
    public void Memory_running_below_its_reported_rating_is_called_out_as_missing_xmp()
    {
        var (service, _, _, hardware) = Build();
        hardware.MemoryModules.Add(Module(2133, 3200, "KF3200"));

        Assert.Contains(service.GetReport().Warnings, w => w.Text.Contains("XMP/EXPO"));
    }

    [Fact]
    public void Speed_grade_in_the_part_number_above_the_running_speed_raises_a_softer_hint()
    {
        // The case actually seen on the developer machine: a 3200 kit running at 2667 where
        // Windows reports 2667 as both the configured and the rated speed.
        var (service, _, _, hardware) = Build();
        hardware.MemoryModules.Add(Module(2667, 2667, "LD4BU016G-3200XG"));

        Assert.Contains(service.GetReport().Warnings, w => w.Text.Contains("маркировка обещает больше"));
    }

    [Fact]
    public void Memory_already_at_its_rated_speed_is_not_reported()
    {
        var (service, _, _, hardware) = Build();
        hardware.MemoryModules.Add(Module(3200, 3200, "LD4BU016G-3200XG"));

        Assert.DoesNotContain(service.GetReport().Warnings, w => w.Text.Contains("XMP/EXPO"));
    }

    [Fact]
    public void A_mechanical_drive_is_reported_as_the_dominant_bottleneck()
    {
        var (service, _, _, hardware) = Build();
        hardware.Disks.Add(new PhysicalDiskInventory("Seagate Barracuda", "SATA", "HDD", 2_000_000_000_000, "SN"));

        Assert.Contains(service.GetReport().Warnings, w => w.Text.Contains("HDD"));
    }

    [Fact]
    public void A_gpu_driver_older_than_a_year_is_reported()
    {
        var (service, _, _, hardware) = Build();
        hardware.Gpus.Add(new GpuInventory("RTX 3060", 12_884_901_888, "31.0.0.1", DateTime.Now.AddYears(-2), "RTX 3060"));

        Assert.Contains(service.GetReport().Warnings, w => w.Text.Contains("старше года"));
    }

    [Fact]
    public void A_recent_gpu_driver_is_not_reported()
    {
        var (service, _, _, hardware) = Build();
        hardware.Gpus.Add(new GpuInventory("RTX 3060", 12_884_901_888, "31.0.0.1", DateTime.Now.AddMonths(-2), "RTX 3060"));

        Assert.DoesNotContain(service.GetReport().Warnings, w => w.Text.Contains("старше года"));
    }
}
