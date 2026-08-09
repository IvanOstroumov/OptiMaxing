using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class RemoteRegistryDisable(IServiceManager services) : ServiceDisableOptimization(services)
{
    public override string Id => "service-remoteregistry-disable";
    public override string DisplayName => "Удалённый реестр (RemoteRegistry): отключить";
    public override string Description => "Позволяет другим машинам в сети редактировать реестр этого ПК. На домашнем игровом ПК почти всегда не нужно, к тому же снижает поверхность атаки.";
    public override string TradeOff => "Удалённое администрирование реестра с других машин перестанет работать.";
    public override Model.RiskLevel Risk => Model.RiskLevel.Safe;
    public override string Category => Categories.Services;
    protected override string ServiceName => "RemoteRegistry";
}
