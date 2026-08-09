using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;
using OptiMaxing.Core.Optimizations.Catalog;

namespace OptiMaxing.Core.Optimizations;

/// <summary>Single place where every shipped optimization is registered.</summary>
public sealed class OptimizationCatalog(IRegistryProvider registry, IServiceManager services, IProcessRunner processRunner)
{
    public IReadOnlyList<IOptimization> BuildAll()
    {
        List<IOptimization> all =
        [
            // Safe
            new GameModeEnable(registry),
            new HagsEnable(registry),
            new MousePrecisionDisable(registry),
            new GameDvrDisable(registry),
            new SysMainDisable(services),
            new PowerPlanUltimatePerformance(processRunner),
            new AdvertisingIdDisable(registry),
            new TailoredExperiencesDisable(registry),
            new ActivityHistoryDisable(registry),
            new WidgetsDisable(registry),
            new TaskbarChatDisable(registry),
            new BingSearchSuggestionsDisable(registry),

            // Caution
            new PrintSpoolerDisable(services),
            new DiagTrackDisable(services),
            new WindowsSearchDisable(services),
            new DnsChangeCloudflare(processRunner),
            new CortanaSearchDisable(registry),
            new DiagnosticDataBasic(registry),
            new CopilotDisable(registry),
            new SettingSyncDisable(registry),
            new RecallDisable(registry),

            // Advanced
            new VbsMemoryIntegrityDisable(registry),
        ];

        // Bloatware removal: one optimization per curated whitelist entry.
        all.AddRange(BloatwareCatalog.Entries.Select(app => new AppxBloatwareRemove(processRunner, app)));

        return all;
    }
}
