using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class AdvertisingIdDisable(IRegistryProvider registry) : RegistryOptimization(registry)
{
    public override string Id => "privacy-advertising-id-disable";
    public override string DisplayName => "Рекламный ID: отключить";
    public override string Description =>
        "Отключает уникальный идентификатор, который приложения используют для персонализированной рекламы.";
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string Category => Categories.Privacy;

    protected override IReadOnlyList<RegistryTarget> Targets =>
    [
        new(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo",
            "Enabled", 0, RegistryValueKind.DWord, OffValue: 1),
    ];
}
