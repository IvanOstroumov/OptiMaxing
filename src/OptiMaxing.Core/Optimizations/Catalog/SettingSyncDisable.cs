using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class SettingSyncDisable(IRegistryProvider registry) : RegistryOptimization(registry)
{
    public override string Id => "privacy-setting-sync-disable";
    public override string DisplayName => "Синхронизация настроек между устройствами: отключить";
    public override string Description =>
        "Останавливает отправку в облако Microsoft тем, паролей, языковых настроек и других параметров для синхронизации между устройствами.";
    public override string TradeOff =>
        "Настройки перестанут переноситься автоматически на другие устройства с тем же аккаунтом Microsoft.";
    public override RiskLevel Risk => RiskLevel.Caution;
    public override string Category => Categories.Privacy;

    protected override IReadOnlyList<RegistryTarget> Targets =>
    [
        new(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\SettingSync",
            "DisableSettingSync", 2, RegistryValueKind.DWord, OffValue: 0),
        new(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\SettingSync",
            "DisableSettingSyncUserOverride", 1, RegistryValueKind.DWord, OffValue: 0),
    ];
}
