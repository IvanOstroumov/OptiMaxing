using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class WmpNetworkSharingDisable(IServiceManager services) : ServiceDisableOptimization(services)
{
    public override string Id => "service-wmpnetworksvc-disable";
    public override string DisplayName => "Общий доступ к Windows Media Player в сети: отключить";
    public override string Description => "Делится медиатекой Windows Media Player с другими устройствами в сети — почти никогда не используется.";
    public override string TradeOff => "Стриминг медиатеки на другие DLNA-устройства в сети перестанет работать.";
    public override Model.RiskLevel Risk => Model.RiskLevel.Safe;
    public override string Category => Categories.Services;
    protected override string ServiceName => "WMPNetworkSvc";
}
