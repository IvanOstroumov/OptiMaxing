using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>Share of CPU the MMCSS scheduler reserves for non-multimedia work. Zero is the value
/// every gaming guide recommends and the one Microsoft warns can starve background tasks.</summary>
public sealed class SystemResponsivenessChoice(IRegistryProvider registry)
    : RegistryChoiceOptimization(registry)
{
    public override string Id => "system-responsiveness";
    public override string DisplayName => "Резерв CPU для фоновых задач (SystemResponsiveness)";

    public override string Description =>
        "Процент процессорного времени, который планировщик мультимедийных задач держит " +
        "в резерве для фоновой работы, не отдавая играм и плеерам.";

    public override string? TradeOff =>
        "При нуле фоновые задачи (запись, стрим, антивирус) могут заметно подтормаживать. " +
        "Прирост FPS в независимых тестах не подтверждён.";

    public override RiskLevel Risk => RiskLevel.Caution;
    public override string Category => Categories.WindowsBase;
    public override bool RequiresRestart => true;

    protected override RegistryHive Hive => RegistryHive.LocalMachine;

    protected override string SubKey =>
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";

    protected override string ValueName => "SystemResponsiveness";
    protected override RegistryValueKind Kind => RegistryValueKind.DWord;

    public override IReadOnlyList<TweakChoice> Choices =>
    [
        new("default-20", "20 % — как в Windows", "Заводское значение для десктопа.",
            IsWindowsDefault: true),
        new("gaming-10", "10 % — умеренно в пользу игр",
            "Половина резерва отдана переднему плану, фон ещё дышит.", IsRecommended: true),
        new("max-0", "0 % — всё переднему плану",
            "Самый частый совет в гайдах. Стримить и записывать параллельно станет тяжелее."),
    ];

    protected override object ValueFor(string choiceId) => choiceId switch
    {
        "gaming-10" => 10,
        "max-0" => 0,
        _ => 20,
    };
}
