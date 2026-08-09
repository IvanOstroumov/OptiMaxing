using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class DiagnosticDataBasic(IRegistryProvider registry) : RegistryOptimization(registry)
{
    public override string Id => "privacy-diagnostic-data-basic";
    public override string DisplayName => "Диагностические данные: минимальный уровень";
    public override string Description =>
        "Ограничивает объём телеметрии, отправляемой в Microsoft, минимально необходимым уровнем (Required/Basic вместо Full/Optional).";
    public override string TradeOff =>
        "Некоторые функции 'на основе данных об использовании' (адаптивные подсказки, часть автодиагностики) станут менее точными. На FPS не влияет.";
    public override RiskLevel Risk => RiskLevel.Caution;
    public override string Category => Categories.Privacy;

    protected override IReadOnlyList<RegistryTarget> Targets =>
    [
        // Windows 11 collapsed the old 0-3 telemetry levels into effectively
        // Required(1)/Optional(3); absent commonly means Optional(3) on non-managed
        // Home/Pro installs, so we treat 3 as the stock "off" baseline.
        new(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection",
            "AllowTelemetry", 1, RegistryValueKind.DWord, OffValue: 3),
    ];
}
