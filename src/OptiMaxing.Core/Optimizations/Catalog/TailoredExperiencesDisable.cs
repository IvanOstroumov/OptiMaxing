using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class TailoredExperiencesDisable(IRegistryProvider registry) : RegistryOptimization(registry)
{
    public override string Id => "privacy-tailored-experiences-disable";
    public override string DisplayName => "Персонализированные советы и предложения: отключить";
    public override string Description =>
        "Отключает подобранные под тебя советы, рекламу приложений в меню Пуск и в Параметрах на основе диагностических данных.";
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string Category => Categories.Privacy;

    protected override IReadOnlyList<RegistryTarget> Targets =>
    [
        new(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
            "SubscribedContent-338388Enabled", 0, RegistryValueKind.DWord, OffValue: 1),
        new(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Privacy",
            "TailoredExperiencesWithDiagnosticDataEnabled", 0, RegistryValueKind.DWord, OffValue: 1),
    ];
}
