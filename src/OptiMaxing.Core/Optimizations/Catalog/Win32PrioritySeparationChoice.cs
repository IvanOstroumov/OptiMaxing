using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>Win32PrioritySeparation packs three fields into one DWORD: quantum length, whether
/// quanta are fixed or variable, and how much extra the foreground process gets. Every "best value"
/// guide picks a different one, which is precisely why this is a choice and not a switch.</summary>
public sealed class Win32PrioritySeparationChoice(IRegistryProvider registry)
    : RegistryChoiceOptimization(registry)
{
    public override string Id => "win32-priority-separation";
    public override string DisplayName => "Приоритет активного окна (Win32PrioritySeparation)";

    public override string Description =>
        "Определяет, насколько активная программа получает больше процессорного времени, чем фоновые, " +
        "и какими порциями оно нарезается.";

    public override string? TradeOff =>
        "Разница заметна в основном на слабых процессорах и при загруженном фоне. " +
        "На современном 6-ядерном CPU эффект чаще всего в пределах погрешности.";

    public override RiskLevel Risk => RiskLevel.Caution;
    public override string Category => Categories.WindowsBase;
    public override bool RequiresRestart => true;

    protected override RegistryHive Hive => RegistryHive.LocalMachine;
    protected override string SubKey => @"SYSTEM\CurrentControlSet\Control\PriorityControl";
    protected override string ValueName => "Win32PrioritySeparation";
    protected override RegistryValueKind Kind => RegistryValueKind.DWord;

    public override IReadOnlyList<TweakChoice> Choices =>
    [
        new("windows-default", "26 — как в Windows (короткие переменные кванты)",
            "Заводское значение для десктопной Windows: активное окно получает тройную прибавку.",
            IsWindowsDefault: true),

        new("gaming-2a", "42 (2A) — длинные кванты, максимум активному окну",
            "Самый частый совет для игр: реже переключения, больше времени игре подряд. " +
            "Фоновые задачи (запись, стрим, распаковка) заметнее тормозят.",
            IsRecommended: true),

        new("balanced-2", "2 — короткие фиксированные кванты, без прибавки",
            "Всем поровну. Имеет смысл, если параллельно с игрой всегда работает что-то тяжёлое."),

        new("server-18", "24 (18) — как на сервере: длинные кванты, без прибавки",
            "Фоновые задачи не проседают. Для игрового ПК обычно худший вариант."),
    ];

    protected override object ValueFor(string choiceId) => choiceId switch
    {
        "gaming-2a" => 0x2A,
        "balanced-2" => 0x02,
        "server-18" => 0x18,
        _ => 0x26,
    };
}
