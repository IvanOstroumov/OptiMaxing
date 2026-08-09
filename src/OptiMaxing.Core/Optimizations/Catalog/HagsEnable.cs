using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class HagsEnable(IRegistryProvider registry) : RegistryOptimization(registry)
{
    public override string Id => "gpu-hags-enable";
    public override string DisplayName => "Аппаратное ускорение планирования GPU (HAGS)";
    public override string Description =>
        "Передаёт планирование задач GPU самой видеокарте вместо Windows. Может немного снизить задержку ввода.";
    public override string TradeOff =>
        "Эффект зависит от игры и драйвера — местами прирост нулевой. Известны конфликты со старыми оверлеями записи экрана.";
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string Category => Categories.Gpu;
    public override bool RequiresRestart => true;

    protected override IReadOnlyList<RegistryTarget> Targets =>
    [
        new(RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
            "HwSchMode",
            2,
            RegistryValueKind.DWord),
    ];
}
