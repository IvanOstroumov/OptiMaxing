using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class MousePrecisionDisable(IRegistryProvider registry) : RegistryOptimization(registry)
{
    public override string Id => "input-mouse-accel-disable";
    public override string DisplayName => "Отключить ускорение мыши (Enhance Pointer Precision)";
    public override string Description =>
        "Курсор начинает двигаться на одинаковое расстояние при одинаковом движении мыши, независимо от скорости. Основа для мышечной памяти в шутерах.";
    public override string TradeOff =>
        "На рабочем столе придётся сильнее двигать мышью, чтобы пройти весь экран. Многим первые пару дней некомфортно.";
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string Category => Categories.Input;

    // These three are REG_SZ, not DWORD, despite holding numbers. Writing them as
    // DWORD leaves the Control Panel checkbox stuck and the setting inert.
    protected override IReadOnlyList<RegistryTarget> Targets =>
    [
        new(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "0", RegistryValueKind.String, OffValue: "1"),
        new(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", "0", RegistryValueKind.String, OffValue: "6"),
        new(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", "0", RegistryValueKind.String, OffValue: "10"),
    ];
}
