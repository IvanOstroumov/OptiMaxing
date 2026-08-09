using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class CortanaSearchDisable(IRegistryProvider registry) : RegistryOptimization(registry)
{
    public override string Id => "privacy-cortana-web-search-disable";
    public override string DisplayName => "Cortana и веб-результаты в поиске: отключить";
    public override string Description =>
        "Отключает Cortana и отправку поисковых запросов из меню Пуск/поиска в Bing.";
    public override string TradeOff =>
        "Поиск через меню Пуск будет искать только локально — веб-результаты и Cortana пропадут.";
    public override RiskLevel Risk => RiskLevel.Caution;
    public override string Category => Categories.Privacy;

    protected override IReadOnlyList<RegistryTarget> Targets =>
    [
        new(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search",
            "AllowCortana", 0, RegistryValueKind.DWord),
        new(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Windows Search",
            "ConnectedSearchUseWeb", 0, RegistryValueKind.DWord),
    ];
}
