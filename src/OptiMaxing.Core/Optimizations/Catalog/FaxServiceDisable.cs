using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class FaxServiceDisable(IServiceManager services) : ServiceDisableOptimization(services)
{
    public override string Id => "service-fax-disable";
    public override string DisplayName => "Факс (Fax): отключить";
    public override string Description => "Служба отправки и приёма факсов — практически никогда не нужна на игровом ПК.";
    public override string TradeOff => "Если когда-нибудь понадобится факс-модем — включи службу обратно.";
    public override Model.RiskLevel Risk => Model.RiskLevel.Safe;
    public override string Category => Categories.Services;
    protected override string ServiceName => "Fax";
}
