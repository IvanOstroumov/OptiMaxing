using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>
/// Curated whitelist of stock scheduled tasks that are safe to disable on a gaming
/// PC — telemetry/CEIP collectors and background maintenance jobs, not anything that
/// backs a visible feature. Mirrors the same "curate, don't scan-and-guess" approach
/// as <see cref="BloatwareCatalog"/> for the same reason: silently disabling an
/// arbitrary discovered task is far riskier than disabling an arbitrary Run entry.
/// </summary>
public static class ScheduledTaskCatalog
{
    public sealed record Entry(string TaskPath, string DisplayName, string Description);

    public static readonly IReadOnlyList<Entry> Entries =
    [
        new(@"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
            "Microsoft Compatibility Appraiser",
            "Периодически сканирует систему и приложения для программы совместимости/телеметрии Microsoft."),
        new(@"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
            "Program Data Updater",
            "Собирает данные об установленных программах для той же программы совместимости."),
        new(@"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
            "CEIP Consolidator",
            "Программа улучшения качества ПО — собирает и отправляет данные об использовании."),
        new(@"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
            "CEIP UsbCeip",
            "Собирает статистику использования USB-устройств для CEIP."),
        new(@"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector",
            "Disk Diagnostic Data Collector",
            "Собирает диагностические данные SMART/диска и отправляет отчёты в Microsoft."),
        new(@"\Microsoft\Windows\Feedback\Siuf\DmClient",
            "Siuf DmClient",
            "Периодически проверяет, нужно ли показать пользователю запрос на обратную связь."),
        new(@"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload",
            "Siuf DmClientOnScenarioDownload",
            "Триггерит запрос обратной связи после определённых сценариев использования."),
        new(@"\Microsoft\Windows\Windows Error Reporting\QueueReporting",
            "Windows Error Reporting: QueueReporting",
            "Отправляет накопленные отчёты об ошибках в Microsoft в фоне."),
    ];
}
