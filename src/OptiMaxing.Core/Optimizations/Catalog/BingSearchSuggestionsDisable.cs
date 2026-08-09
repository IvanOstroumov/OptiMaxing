using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class BingSearchSuggestionsDisable(IRegistryProvider registry) : RegistryOptimization(registry)
{
    public override string Id => "apps-bing-search-suggestions-disable";
    public override string DisplayName => "Подсказки Bing в поиске проводника: отключить";
    public override string Description =>
        "Отключает веб-подсказки Bing при вводе текста в адресную строку/поиск проводника.";
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string Category => Categories.Apps;

    protected override IReadOnlyList<RegistryTarget> Targets =>
    [
        new(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Explorer",
            "DisableSearchBoxSuggestions", 1, RegistryValueKind.DWord, OffValue: 0),
    ];
}
