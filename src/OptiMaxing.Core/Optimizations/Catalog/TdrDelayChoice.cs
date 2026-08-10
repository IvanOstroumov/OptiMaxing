using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>How long Windows waits for a hung GPU before resetting the driver. Raising it is a
/// workaround for false-positive resets under heavy load, not a performance tweak — and it delays
/// recovery from a genuinely dead driver by the same amount.</summary>
public sealed class TdrDelayChoice(IRegistryProvider registry) : RegistryChoiceOptimization(registry)
{
    public override string Id => "gpu-tdr-delay";
    public override string DisplayName => "Таймаут сброса видеодрайвера (TDR Delay)";

    public override string Description =>
        "Сколько секунд Windows ждёт зависшую видеокарту, прежде чем перезапустить драйвер " +
        "и показать «Видеодрайвер перестал отвечать».";

    public override string? TradeOff =>
        "Это не прирост FPS. Увеличение помогает от ложных срабатываний при тяжёлой нагрузке, " +
        "но ровно на столько же затягивает зависание при настоящем отказе драйвера.";

    public override RiskLevel Risk => RiskLevel.Advanced;
    public override string Category => Categories.Gpu;
    public override bool RequiresRestart => true;

    protected override RegistryHive Hive => RegistryHive.LocalMachine;
    protected override string SubKey => @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
    protected override string ValueName => "TdrDelay";
    protected override RegistryValueKind Kind => RegistryValueKind.DWord;

    public override IReadOnlyList<TweakChoice> Choices =>
    [
        new("default-2", "2 секунды — как в Windows", "Заводское поведение.",
            IsWindowsDefault: true, IsRecommended: true),
        new("relaxed-8", "8 секунд",
            "Разумный компромисс, если драйвер срывается на тяжёлых сценах, а видеокарта исправна."),
        new("long-60", "60 секунд",
            "Практически отключает автосброс. Использовать только для диагностики: при реальном " +
            "зависании система будет стоять минуту."),
    ];

    protected override object ValueFor(string choiceId) => choiceId switch
    {
        "relaxed-8" => 8,
        "long-60" => 60,
        _ => 2,
    };
}
