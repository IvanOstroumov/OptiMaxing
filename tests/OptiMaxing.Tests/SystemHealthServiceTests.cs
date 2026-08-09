using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Safety;
using OptiMaxing.Core.Testing;

namespace OptiMaxing.Tests;

public class SystemHealthServiceTests
{
    private static (SystemHealthService Service, FakeSystemInfoProvider SysInfo, FakeRestorePointService RestorePoints) Build()
    {
        var sysInfo = new FakeSystemInfoProvider();
        var restorePoints = new FakeRestorePointService();
        return (new SystemHealthService(sysInfo, restorePoints), sysInfo, restorePoints);
    }

    [Fact]
    public void Healthy_machine_has_no_warnings()
    {
        var (service, sysInfo, restorePoints) = Build();
        sysInfo.Memory = new MemoryInfo(TotalBytes: 16_000_000_000, AvailableBytes: 12_000_000_000);
        sysInfo.Disks.Add(new DiskInfo("C:\\", FreeBytes: 500_000_000_000, TotalBytes: 1_000_000_000_000));
        restorePoints.Status = new RestorePointStatus(true, false, DateTimeOffset.UtcNow.AddHours(-1));

        var report = service.GetReport();

        Assert.Empty(report.Warnings);
    }

    [Fact]
    public void High_memory_usage_produces_a_warning()
    {
        var (service, sysInfo, _) = Build();
        sysInfo.Memory = new MemoryInfo(TotalBytes: 16_000_000_000, AvailableBytes: 1_000_000_000); // ~94% used

        var report = service.GetReport();

        Assert.Contains(report.Warnings, w => w.Text.Contains("Оперативная память"));
    }

    [Fact]
    public void Low_disk_space_produces_a_warning_naming_the_drive()
    {
        var (service, sysInfo, _) = Build();
        sysInfo.Disks.Add(new DiskInfo("D:\\", FreeBytes: 5_000_000_000, TotalBytes: 1_000_000_000_000)); // 0.5% free

        var report = service.GetReport();

        Assert.Contains(report.Warnings, w => w.Text.Contains("D:\\"));
    }

    [Fact]
    public void Missing_restore_point_produces_a_warning()
    {
        var (service, _, restorePoints) = Build();
        restorePoints.Status = new RestorePointStatus(false, false, null);

        var report = service.GetReport();

        Assert.Contains(report.Warnings, w => w.Text.Contains("Точки восстановления нет"));
    }

    [Fact]
    public void Policy_block_takes_priority_over_missing_restore_point_message()
    {
        var (service, _, restorePoints) = Build();
        restorePoints.Status = new RestorePointStatus(false, true, null);

        var report = service.GetReport();

        Assert.Contains(report.Warnings, w => w.Text.Contains("групповой политикой"));
        Assert.DoesNotContain(report.Warnings, w => w.Text.Contains("Точки восстановления нет"));
    }
}
