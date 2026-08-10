using LibreHardwareMonitor.Hardware;
using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Platform;

public sealed class LibreHardwareSensorProvider : ISensorProvider
{
    private Computer? _computer;
    private bool _disposed;

    public bool IsAvailable => _computer is not null;

    public string? UnavailableReason { get; private set; }

    public IReadOnlyList<SensorReading> Read()
    {
        if (_disposed)
        {
            return [];
        }

        if (_computer is null && UnavailableReason is null)
        {
            Open();
        }

        if (_computer is null)
        {
            return [];
        }

        var readings = new List<SensorReading>();
        foreach (var hardware in _computer.Hardware)
        {
            Collect(hardware, readings);
        }

        return readings;
    }

    private void Open()
    {
        try
        {
            var computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = true,
                IsControllerEnabled = true,
            };

            computer.Open();
            _computer = computer;
        }
        catch (Exception ex)
        {
            // Opening loads a ring0 driver. Without elevation, or with Secure Boot / security
            // software blocking it, this throws — the app must keep working without sensors, so
            // the reason is recorded once and no further attempt is made.
            UnavailableReason = ex.Message;
        }
    }

    private static void Collect(IHardware hardware, List<SensorReading> readings)
    {
        hardware.Update();

        var component = MapComponent(hardware.HardwareType);
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.Value is not { } value || float.IsNaN(value))
            {
                continue;
            }

            readings.Add(new SensorReading(
                component,
                hardware.Name,
                MapKind(sensor.SensorType),
                sensor.Name,
                value,
                UnitOf(sensor.SensorType)));
        }

        foreach (var sub in hardware.SubHardware)
        {
            Collect(sub, readings);
        }
    }

    private static SensorComponent MapComponent(HardwareType type) => type switch
    {
        HardwareType.Cpu => SensorComponent.Cpu,
        HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => SensorComponent.Gpu,
        HardwareType.Motherboard or HardwareType.SuperIO => SensorComponent.Motherboard,
        HardwareType.Memory => SensorComponent.Memory,
        HardwareType.Storage => SensorComponent.Storage,
        HardwareType.Network => SensorComponent.Network,
        _ => SensorComponent.Other,
    };

    private static SensorKind MapKind(SensorType type) => type switch
    {
        SensorType.Temperature => SensorKind.Temperature,
        SensorType.Clock => SensorKind.Clock,
        SensorType.Load => SensorKind.Load,
        SensorType.Fan or SensorType.Control => SensorKind.Fan,
        SensorType.Voltage => SensorKind.Voltage,
        SensorType.Power => SensorKind.Power,
        SensorType.Data or SensorType.SmallData => SensorKind.Data,
        SensorType.Level => SensorKind.Level,
        SensorType.Throughput => SensorKind.Throughput,
        _ => SensorKind.Other,
    };

    private static string UnitOf(SensorType type) => type switch
    {
        SensorType.Temperature => "°C",
        SensorType.Clock => "МГц",
        SensorType.Load or SensorType.Level => "%",
        SensorType.Fan => "об/мин",
        SensorType.Control => "%",
        SensorType.Voltage => "В",
        SensorType.Power => "Вт",
        SensorType.Data => "ГБ",
        SensorType.SmallData => "МБ",
        SensorType.Throughput => "Б/с",
        _ => string.Empty,
    };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _computer?.Close();
        _computer = null;
    }
}
