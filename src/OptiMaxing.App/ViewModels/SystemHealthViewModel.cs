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
/// <summary>A group of live sensors belonging to one physical component.</summary>
public sealed class SensorGroupRow(string title)
{
    public string Title => title;
    public ObservableCollection<SensorRow> Sensors { get; } = [];
}

public sealed class SensorRow(SensorSample sample) : ObservableObject
{
    private SensorSample _sample = sample;

    public string Name => _sample.Reading.SensorName;
    public string Value => Format(_sample.Reading.Value, _sample.Reading.Unit);
    public string Max => Format(_sample.SessionMax, _sample.Reading.Unit);

    public void Update(SensorSample sample)
    {
        _sample = sample;
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(Max));
    }

    private static string Format(float value, string unit) => $"{value:F1} {unit}";
}

public sealed class SystemHealthViewModel : ObservableObject
{
    private readonly SystemHealthService _health;
    private readonly SensorMonitor _sensors;
    private readonly Dictionary<string, SensorRow> _sensorRows = [];
    private readonly System.Windows.Threading.DispatcherTimer _sensorTimer;

    private string _osDescription = string.Empty;
    private string _processorName = string.Empty;
    private string _uptimeText = string.Empty;
    private string _memorySummary = string.Empty;
    private double _memoryUsedPercent;

    public SystemHealthViewModel(SystemHealthService health, SensorMonitor sensors)
    {
        _health = health;
        _sensors = sensors;
        RefreshCommand = new RelayCommand(() =>
        {
            Refresh();
            return Task.CompletedTask;
        });

        // Polling sensors costs real CPU (it wakes the monitoring driver), so the timer only runs
        // while this tab is actually on screen — see IsActive, set from the view.
        _sensorTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _sensorTimer.Tick += (_, _) => PollSensors();

        Refresh();
    }

    public RelayCommand RefreshCommand { get; }

    public ObservableCollection<DiskHealthRow> Disks { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];
    public ObservableCollection<string> HardwareFacts { get; } = [];
    public ObservableCollection<SensorGroupRow> SensorGroups { get; } = [];

    private bool _isActive;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (!SetField(ref _isActive, value))
            {
                return;
            }

            if (value)
            {
                PollSensors();
                _sensorTimer.Start();
            }
            else
            {
                _sensorTimer.Stop();
            }
        }
    }

    private string _sensorStatus = string.Empty;

    public string SensorStatus
    {
        get => _sensorStatus;
        private set => SetField(ref _sensorStatus, value);
    }

    public bool HasSensorProblem => !string.IsNullOrEmpty(SensorStatus);

    private void PollSensors()
    {
        var samples = _sensors.Poll();

        if (samples.Count == 0)
        {
            SensorStatus = _sensors.UnavailableReason is { } reason
                ? $"Датчики недоступны: {reason}"
                : "Датчики недоступны — драйвер мониторинга не поднялся.";
            OnPropertyChanged(nameof(HasSensorProblem));
            return;
        }

        if (SensorStatus.Length > 0)
        {
            SensorStatus = string.Empty;
            OnPropertyChanged(nameof(HasSensorProblem));
        }

        // Rows are matched by key and updated in place rather than rebuilt: replacing the
        // collection once a second would reset scroll position and make the list unreadable.
        foreach (var sample in samples)
        {
            if (_sensorRows.TryGetValue(sample.Key, out var row))
            {
                row.Update(sample);
                continue;
            }

            row = new SensorRow(sample);
            _sensorRows[sample.Key] = row;

            var title = $"{sample.Reading.Component}: {sample.Reading.HardwareName}";
            var group = SensorGroups.FirstOrDefault(g => g.Title == title);
            if (group is null)
            {
                group = new SensorGroupRow(title);
                SensorGroups.Add(group);
            }

            group.Sensors.Add(row);
        }
    }

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

        HardwareFacts.Clear();
        foreach (var fact in DescribeHardware(report.Hardware))
            HardwareFacts.Add(fact);

        OnPropertyChanged(nameof(HasWarnings));
    }

    private static IEnumerable<string> DescribeHardware(HardwareInventory hw)
    {
        if (hw.Cpu is { } cpu)
        {
            yield return $"Процессор: {cpu.Name}, {cpu.PhysicalCores} ядер / {cpu.LogicalCores} потоков, " +
                         $"{cpu.BaseClockMhz} МГц, сокет {cpu.Socket}";
            yield return $"Кэш: L2 {cpu.L2CacheKb / 1024.0:F0} МБ, L3 {cpu.L3CacheKb / 1024.0:F0} МБ; " +
                         $"виртуализация {(cpu.VirtualizationEnabled ? "включена" : "выключена")}";
        }

        foreach (var gpu in hw.Gpus)
        {
            yield return $"Видеокарта: {gpu.Name}, {FormatGb(gpu.VideoMemoryBytes)} видеопамяти, " +
                         $"драйвер {gpu.DriverVersion} от {gpu.DriverDate:yyyy-MM-dd}";
        }

        if (hw.Motherboard is { } mb)
        {
            yield return $"Плата: {mb.Manufacturer} {mb.Product}";
        }

        if (hw.Bios is { } bios)
        {
            yield return $"BIOS: {bios.Version} от {bios.ReleaseDate:yyyy-MM-dd}";
        }

        foreach (var m in hw.MemoryModules)
        {
            yield return $"Память {m.DeviceLocator}: {FormatGb(m.CapacityBytes)} на {m.ConfiguredClockMhz} МГц, {m.PartNumber}";
        }

        foreach (var d in hw.Disks)
        {
            yield return $"Накопитель: {d.Model}, {d.MediaType} по {d.InterfaceType}, {FormatGb(d.SizeBytes)}";
        }
    }

    private static string FormatGb(ulong bytes) => $"{bytes / 1024.0 / 1024.0 / 1024.0:F1} ГБ";

    private static string FormatUptime(TimeSpan uptime) => uptime switch
    {
        { TotalDays: >= 1 } => $"{(int)uptime.TotalDays} дн {uptime.Hours} ч",
        { TotalHours: >= 1 } => $"{(int)uptime.TotalHours} ч {uptime.Minutes} мин",
        _ => $"{(int)uptime.TotalMinutes} мин",
    };
}
