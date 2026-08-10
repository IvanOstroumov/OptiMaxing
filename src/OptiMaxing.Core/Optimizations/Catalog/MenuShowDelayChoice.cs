using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>Delay before a submenu opens. Purely a responsiveness-feel setting — it changes nothing
/// about performance, but it is the single most noticeable "the system got snappier" tweak.</summary>
public sealed class MenuShowDelayChoice(IRegistryProvider registry) : RegistryChoiceOptimization(registry)
{
    public override string Id => "menu-show-delay";
    public override string DisplayName => "Задержка открытия меню";

    public override string Description =>
        "Пауза перед появлением подменю и всплывающих меню, в миллисекундах.";

    public override string? TradeOff =>
        "На производительность не влияет вообще — меняется только ощущение отзывчивости. " +
        "При нуле меню могут открываться от случайного движения мыши.";

    public override RiskLevel Risk => RiskLevel.Safe;
    public override string Category => Categories.WindowsBase;
    public override bool RequiresRestart => true;

    protected override RegistryHive Hive => RegistryHive.CurrentUser;
    protected override string SubKey => @"Control Panel\Desktop";
    protected override string ValueName => "MenuShowDelay";
    protected override RegistryValueKind Kind => RegistryValueKind.String;

    public override IReadOnlyList<TweakChoice> Choices =>
    [
        new("default-400", "400 мс — как в Windows", "Заводское значение.", IsWindowsDefault: true),
        new("fast-100", "100 мс — быстро", "Заметно отзывчивее, случайных открытий почти нет.",
            IsRecommended: true),
        new("instant-0", "0 мс — мгновенно",
            "Меню открывается сразу. Иногда срабатывает от простого проезда курсором."),
    ];

    protected override object ValueFor(string choiceId) => choiceId switch
    {
        "fast-100" => "100",
        "instant-0" => "0",
        _ => "400",
    };
}
