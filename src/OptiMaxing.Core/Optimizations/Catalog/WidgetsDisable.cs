using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class WidgetsDisable(IRegistryProvider registry) : RegistryOptimization(registry)
{
    public override string Id => "apps-widgets-disable";
    public override string DisplayName => "Виджеты (News and Interests): убрать с панели задач";
    public override string Description =>
        "Убирает значок виджетов с панели задач. Ничего не удаляет — просто прячет кнопку.";
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string Category => Categories.Apps;

    protected override IReadOnlyList<RegistryTarget> Targets =>
    [
        new(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
            "TaskbarDa", 0, RegistryValueKind.DWord, OffValue: 1),
    ];
}
