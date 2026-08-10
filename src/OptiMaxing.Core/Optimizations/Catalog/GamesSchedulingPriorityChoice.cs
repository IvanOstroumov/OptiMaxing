using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>The MMCSS "Games" task's GPU Priority. Windows ships it at 8; guides push 8 or 2 with
/// equal confidence, so both are offered instead of one being presented as the truth.</summary>
public sealed class GamesSchedulingPriorityChoice(IRegistryProvider registry)
    : RegistryChoiceOptimization(registry)
{
    public override string Id => "mmcss-games-gpu-priority";
    public override string DisplayName => "Планировщик MMCSS: приоритет GPU для игр";

    public override string Description =>
        "Профиль «Games» в планировщике мультимедийных задач. Значение влияет на то, " +
        "насколько агрессивно игра получает доступ к видеокарте относительно других задач.";

    public override string? TradeOff =>
        "Эффект не доказан: измеримой разницы в FPS в независимых тестах не показано. " +
        "Профиль работает только для программ, которые сами регистрируются в MMCSS.";

    public override RiskLevel Risk => RiskLevel.Caution;
    public override string Category => Categories.Gpu;
    public override bool RequiresRestart => true;

    protected override RegistryHive Hive => RegistryHive.LocalMachine;

    protected override string SubKey =>
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";

    protected override string ValueName => "GPU Priority";
    protected override RegistryValueKind Kind => RegistryValueKind.DWord;

    public override IReadOnlyList<TweakChoice> Choices =>
    [
        new("default-8", "8 — как в Windows", "Заводское значение профиля Games.",
            IsWindowsDefault: true, IsRecommended: true),
        new("high-2", "2 — часто советуют в гайдах",
            "Популярное значение из руководств по «оптимизации». Независимого подтверждения выигрыша нет."),
        new("max-31", "31 — максимум",
            "Крайнее значение. Может ухудшить плавность записи и стрима, идущих параллельно."),
    ];

    protected override object ValueFor(string choiceId) => choiceId switch
    {
        "high-2" => 2,
        "max-31" => 31,
        _ => 8,
    };
}
