using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class RecallDisable(IRegistryProvider registry) : RegistryOptimization(registry)
{
    public override string Id => "apps-recall-disable";
    public override string DisplayName => "Windows Recall (AI-снимки экрана): отключить";
    public override string Description =>
        "Запрещает Windows делать периодические AI-снимки экрана для функции Recall через групповую политику.";
    public override string TradeOff =>
        "На машинах без NPU/без Copilot+ Recall обычно и так недоступен — этот переключатель работает как страховка на будущее обновление.";
    public override RiskLevel Risk => RiskLevel.Caution;
    public override string Category => Categories.Apps;

    protected override IReadOnlyList<RegistryTarget> Targets =>
    [
        new(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
            "DisableAIDataAnalysis", 1, RegistryValueKind.DWord, OffValue: 0),
        new(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
            "AllowRecallEnablement", 0, RegistryValueKind.DWord, OffValue: 1),
    ];
}
