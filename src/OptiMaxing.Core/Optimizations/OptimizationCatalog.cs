using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;
using OptiMaxing.Core.Optimizations.Catalog;

namespace OptiMaxing.Core.Optimizations;

/// <summary>Single place where every shipped optimization is registered.</summary>
public sealed class OptimizationCatalog(IRegistryProvider registry)
{
    public IReadOnlyList<IOptimization> BuildAll() =>
    [
        new GameModeEnable(registry),
        new HagsEnable(registry),
        new MousePrecisionDisable(registry),
    ];
}
