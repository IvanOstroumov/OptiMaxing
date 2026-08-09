using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;
using OptiMaxing.Core.Optimizations.Catalog;

namespace OptiMaxing.Core.Optimizations;

/// <summary>Single place where every shipped optimization is registered.</summary>
public sealed class OptimizationCatalog(IRegistryProvider registry, IServiceManager services, IProcessRunner processRunner)
{
    public IReadOnlyList<IOptimization> BuildAll() =>
    [
        // Safe
        new GameModeEnable(registry),
        new HagsEnable(registry),
        new MousePrecisionDisable(registry),
        new GameDvrDisable(registry),
        new SysMainDisable(services),
        new PowerPlanUltimatePerformance(processRunner),

        // Caution
        new PrintSpoolerDisable(services),
        new DiagTrackDisable(services),
        new WindowsSearchDisable(services),
        new DnsChangeCloudflare(processRunner),

        // Advanced
        new VbsMemoryIntegrityDisable(registry),
    ];
}
