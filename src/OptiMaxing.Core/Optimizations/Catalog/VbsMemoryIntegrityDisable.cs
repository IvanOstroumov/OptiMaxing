using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>
/// Turns off Core Isolation / Memory Integrity (VBS/HVCI). Several anti-cheats
/// (Valorant, Fortnite, etc.) refuse to launch without it, so the app must never
/// apply this blindly — see AntiCheatGuard, wired in at the UI layer for M2+.
/// </summary>
public sealed class VbsMemoryIntegrityDisable(IRegistryProvider registry) : RegistryOptimization(registry)
{
    public override string Id => "gpu-vbs-memory-integrity-disable";
    public override string DisplayName => "Core Isolation / Memory Integrity (VBS): выключить";
    public override string Description =>
        "Отключает виртуализацию безопасности ядра. В некоторых играх и на некоторых GPU/драйверах даёт заметный прирост FPS, ценой ослабления защиты от определённых видов вредоносного кода.";
    public override string TradeOff =>
        "Снижает защиту системы от эксплойтов уровня ядра. Многие античиты (Valorant/Vanguard, EAC в защищённом режиме, BattlEye HVCI-режим) требуют VBS включённым и откажутся запускать игру.";
    public override RiskLevel Risk => RiskLevel.Advanced;
    public override Reversibility Reversibility => Reversibility.ReversibleWithCaveat; // требует перезагрузки, чтобы вступило в силу
    public override string Category => Categories.Gpu;
    public override bool RequiresRestart => true;

    protected override IReadOnlyList<RegistryTarget> Targets =>
    [
        new(RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity",
            "Enabled", 0, RegistryValueKind.DWord, OffValue: 0),
    ];
}
