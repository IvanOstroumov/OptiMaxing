using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations;

/// <summary>A registry tweak whose whole definition is data. Tweaks that need real behaviour still
/// get their own class; these exist because a one-file-per-value rule would mean dozens of files
/// that differ only in literals, and the differences would be harder to compare, not easier.</summary>
public sealed class SimpleRegistryOptimization(
    string id,
    string displayName,
    string description,
    RiskLevel risk,
    string category,
    IReadOnlyList<RegistryTarget> targets,
    IRegistryProvider registry,
    string? tradeOff = null,
    bool requiresRestart = false) : RegistryOptimization(registry)
{
    public override string Id => id;
    public override string DisplayName => displayName;
    public override string Description => description;
    public override string? TradeOff => tradeOff;
    public override RiskLevel Risk => risk;
    public override string Category => category;
    public override bool RequiresRestart => requiresRestart;

    protected override IReadOnlyList<RegistryTarget> Targets => targets;
}

/// <summary>Data-only counterpart of <see cref="SimpleRegistryOptimization"/> for value-picking
/// tweaks. Each option carries the raw value it writes, which the UI never sees.</summary>
public sealed class SimpleChoiceOptimization(
    string id,
    string displayName,
    string description,
    RiskLevel risk,
    string category,
    RegistryHive hive,
    string subKey,
    string valueName,
    RegistryValueKind kind,
    IReadOnlyList<(TweakChoice Choice, object Value)> options,
    IRegistryProvider registry,
    string? tradeOff = null,
    bool requiresRestart = false) : RegistryChoiceOptimization(registry)
{
    public override string Id => id;
    public override string DisplayName => displayName;
    public override string Description => description;
    public override string? TradeOff => tradeOff;
    public override RiskLevel Risk => risk;
    public override string Category => category;
    public override bool RequiresRestart => requiresRestart;

    public override IReadOnlyList<TweakChoice> Choices { get; } = options.Select(o => o.Choice).ToList();

    protected override RegistryHive Hive => hive;
    protected override string SubKey => subKey;
    protected override string ValueName => valueName;
    protected override RegistryValueKind Kind => kind;

    protected override object ValueFor(string choiceId) =>
        options.First(o => o.Choice.Id == choiceId).Value;
}
