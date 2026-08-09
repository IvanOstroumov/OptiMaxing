using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>
/// wuauserv keeps files under SoftwareDistribution\Download open while running, so
/// deletion is stopped/restarted around it — otherwise most files would silently be
/// skipped as "in use" and the cleanup would look like it did nothing.
/// </summary>
public sealed class WindowsUpdateCacheCleanup(IFileSystem fileSystem, IServiceManager services)
    : FileCleanupOptimization(fileSystem)
{
    public override string Id => "cleanup-windows-update-cache";
    public override string DisplayName => "Кэш загрузок Windows Update: очистить";
    public override string Description =>
        "Удаляет уже установленные/устаревшие файлы обновлений из SoftwareDistribution\\Download.";
    public override string TradeOff =>
        "Уже применённые обновления не переустановятся заново — при следующем обновлении файлы просто скачаются повторно. Служба Windows Update кратко останавливается на время очистки.";
    public override RiskLevel Risk => RiskLevel.Caution;

    protected override IReadOnlyList<CleanupTarget> Targets =>
    [
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download"), "*"),
    ];

    protected override Task BeforeApplyAsync(OptimizationContext context)
    {
        if (services.Get("wuauserv") is { IsRunning: true })
        {
            services.Stop("wuauserv");
            context.Log.Report("    служба Windows Update временно остановлена");
        }

        return Task.CompletedTask;
    }

    protected override Task AfterApplyAsync(OptimizationContext context)
    {
        if (services.Get("wuauserv") is { IsRunning: false })
        {
            services.Start("wuauserv");
            context.Log.Report("    служба Windows Update запущена обратно");
        }

        return Task.CompletedTask;
    }
}
