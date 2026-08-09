using System.Collections.ObjectModel;
using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Safety;

namespace OptiMaxing.App.ViewModels;

/// <summary>One row for a fixed drive in the health screen, with a pre-formatted string and a
/// bool flag the view uses to color low-space drives.</summary>
public sealed class DiskHealthRow(DiskInfo disk)
{
    public string Name => disk.Name;
    public bool IsLow => disk.FreePercent < 10.0;
    public double FreePercent => disk.FreePercent;
    public string Summary =>
        $"{FormatGb(disk.FreeBytes)} свободно из {FormatGb(disk.TotalBytes)} ({disk.FreePercent:F0}%)";

    private static string FormatGb(long bytes) => $"{bytes / 1024.0 / 1024.0 / 1024.0:F0} ГБ";
}

/// <summary>Backs the "Здоровье системы" tab — a startup snapshot (OS/CPU/RAM/disks/restore
/// point) plus derived warnings, refreshed on demand via RefreshCommand. Pure presentation over
/// SystemHealthService; no apply/revert semantics belong here.</summary>
public sealed class SystemHealthViewModel : ObservableObject
{
    private readonly SystemHealthService _health;

    private string _osDescription = string.Empty;
    private string _processorName = string.Empty;
    private string _uptimeText = string.Empty;
    private string _memorySummary = string.Empty;
    private double _memoryUsedPercent;

    public SystemHealthViewModel(SystemHealthService health)
    {
        _health = health;
        RefreshCommand = new RelayCommand(() =>
        {
            Refresh();
            return Task.CompletedTask;
        });
        Refresh();
    }

    public RelayCommand RefreshCommand { get; }

    public ObservableCollection<DiskHealthRow> Disks { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];

    public string OsDescription
    {
        get => _osDescription;
        private set => SetField(ref _osDescription, value);
    }

    public string ProcessorName
    {
        get => _processorName;
        private set => SetField(ref _processorName, value);
    }

    public string UptimeText
    {
        get => _uptimeText;
        private set => SetField(ref _uptimeText, value);
    }

    public string MemorySummary
    {
        get => _memorySummary;
        private set => SetField(ref _memorySummary, value);
    }

    public double MemoryUsedPercent
    {
        get => _memoryUsedPercent;
        private set => SetField(ref _memoryUsedPercent, value);
    }

    public bool HasWarnings => Warnings.Count > 0;

    private void Refresh()
    {
        var report = _health.GetReport();

        OsDescription = report.OsDescription;
        ProcessorName = report.ProcessorName;
        UptimeText = FormatUptime(report.Uptime);
        MemoryUsedPercent = report.MemoryUsedPercent;
        MemorySummary =
            $"{FormatGb(report.Memory.TotalBytes - report.Memory.AvailableBytes)} занято из {FormatGb(report.Memory.TotalBytes)} ({report.MemoryUsedPercent:F0}%)";

        Disks.Clear();
        foreach (var disk in report.Disks)
            Disks.Add(new DiskHealthRow(disk));

        Warnings.Clear();
        foreach (var warning in report.Warnings)
            Warnings.Add(warning.Text);

        OnPropertyChanged(nameof(HasWarnings));
    }

    private static string FormatGb(ulong bytes) => $"{bytes / 1024.0 / 1024.0 / 1024.0:F1} ГБ";

    private static string FormatUptime(TimeSpan uptime) => uptime switch
    {
        { TotalDays: >= 1 } => $"{(int)uptime.TotalDays} дн {uptime.Hours} ч",
        { TotalHours: >= 1 } => $"{(int)uptime.TotalHours} ч {uptime.Minutes} мин",
        _ => $"{(int)uptime.TotalMinutes} мин",
    };
}
