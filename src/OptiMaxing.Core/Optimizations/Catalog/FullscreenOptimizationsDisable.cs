using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class FullscreenOptimizationsDisable(IRegistryProvider registry) : RegistryOptimization(registry)
{
    public override string Id => "gpu-fullscreen-optimizations-disable";
    public override string DisplayName => "Fullscreen Optimizations (глобально): отключить";
    public override string Description =>
        "Меняет системный дефолт для 'полноэкранных оптимизаций' на выключенный — игры получают эксклюзивный полноэкранный режим вместо composited borderless, если сами это поддерживают.";
    public override string TradeOff =>
        "Alt-Tab и оверлеи (Discord, RTSS, GeForce Experience) могут работать чуть медленнее поверх настоящего эксклюзивного полноэкранного режима. Это системный дефолт — каждая игра всё ещё может переопределить его своей галочкой 'Disable fullscreen optimizations' в свойствах ярлыка.";
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string Category => Categories.Gpu;

    protected override IReadOnlyList<RegistryTarget> Targets =>
    [
        new(RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_FSEBehaviorMode", 2, RegistryValueKind.DWord, OffValue: 0),
        new(RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_HonorUserFSEBehaviorMode", 1, RegistryValueKind.DWord, OffValue: 0),
    ];
}
