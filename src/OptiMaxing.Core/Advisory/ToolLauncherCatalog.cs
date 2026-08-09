namespace OptiMaxing.Core.Advisory;

/// <summary>
/// Third-party monitoring/tuning tools OptiMaxing does not reimplement (per the
/// approved v1 scope) — instead it detects an existing install by common path and
/// offers a one-click launch, or a link to the official download if not found.
/// </summary>
public sealed record ToolEntry(
    string Name,
    string Description,
    IReadOnlyList<string> CommonInstallPaths,
    string DownloadUrl);

public static class ToolLauncherCatalog
{
    public static readonly IReadOnlyList<ToolEntry> Entries =
    [
        new("HWiNFO",
            "Подробный мониторинг датчиков CPU/GPU/материнской платы — температуры, частоты, throttling.",
            [
                @"C:\Program Files\HWiNFO64\HWiNFO64.exe",
                @"C:\Program Files (x86)\HWiNFO64\HWiNFO64.exe",
            ],
            "https://www.hwinfo.com/download/"),

        new("CapFrameX",
            "Запись и анализ фреймтаймов (1%/0.1% low, стабильность кадров), поверх PresentMon.",
            [
                @"C:\Program Files\CapFrameX\CapFrameX.exe",
            ],
            "https://www.capframex.com/"),

        new("PresentMon",
            "Консольный инструмент замера кадров/задержек от Intel — движок под капотом CapFrameX и многих оверлеев.",
            [
                @"C:\Program Files\PresentMon\PresentMon.exe",
            ],
            "https://github.com/GameTechDev/PresentMon/releases"),

        new("MSI Afterburner + RTSS",
            "Разгон/мониторинг видеокарты (Afterburner) и оверлей FPS/фреймтайма и лимитер кадров (RivaTuner Statistics Server).",
            [
                @"C:\Program Files (x86)\MSI Afterburner\MSIAfterburner.exe",
            ],
            "https://www.msi.com/Landing/afterburner/graphics-cards"),

        new("Process Lasso (ParkControl)",
            "Управление парковкой ядер CPU и приоритетами процессов в реальном времени.",
            [
                @"C:\Program Files\ParkControl\ParkControl.exe",
                @"C:\Program Files\Process Lasso\ProcessLasso.exe",
            ],
            "https://bitsum.com/parkcontrol/"),

        new("Intel LatencyMon",
            "Диагностика причин микро-фризов/аудио-щелчков — показывает, какой драйвер держит систему в DPC/ISR дольше всего.",
            [
                @"C:\Program Files\LatencyMon\LatMon.exe",
                @"C:\Program Files (x86)\LatencyMon\LatMon.exe",
            ],
            "https://www.resplendence.com/latencymon"),
    ];
}
