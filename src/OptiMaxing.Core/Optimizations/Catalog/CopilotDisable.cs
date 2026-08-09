using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class CopilotDisable(IRegistryProvider registry) : RegistryOptimization(registry)
{
    public override string Id => "privacy-copilot-disable";
    public override string DisplayName => "Windows Copilot: отключить";
    public override string Description =>
        "Убирает кнопку и запуск Copilot из панели задач через групповую политику.";
    public override string TradeOff =>
        "Copilot станет недоступен из панели задач. Ничего не удаляется — переключатель обратим.";
    public override RiskLevel Risk => RiskLevel.Caution;
    public override string Category => Categories.Privacy;

    protected override IReadOnlyList<RegistryTarget> Targets =>
    [
        new(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot",
            "TurnOffWindowsCopilot", 1, RegistryValueKind.DWord, OffValue: 0),
    ];
}
