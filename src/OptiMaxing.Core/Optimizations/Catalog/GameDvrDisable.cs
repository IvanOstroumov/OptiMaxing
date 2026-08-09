using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class GameDvrDisable(IRegistryProvider registry) : RegistryOptimization(registry)
{
    public override string Id => "gamedvr-disable";
    public override string DisplayName => "Xbox Game Bar / Game DVR: отключить фоновую запись";
    public override string Description =>
        "Отключает фоновую запись игровых клипов (Game DVR) и оверлей Game Bar, которые держат постоянный хук в графическом конвейере даже когда запись не идёт.";
    public override string TradeOff =>
        "Быстрая запись клипа по Win+Alt+G и всплывающие уведомления о достижениях перестанут работать.";
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string Category => Categories.WindowsBase;

    protected override IReadOnlyList<RegistryTarget> Targets =>
    [
        new(RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 0, RegistryValueKind.DWord, OffValue: 1),
        new(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, RegistryValueKind.DWord, OffValue: 1),
    ];
}
