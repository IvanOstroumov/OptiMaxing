using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class MapsBrokerDisable(IServiceManager services) : ServiceDisableOptimization(services)
{
    public override string Id => "service-mapsbroker-disable";
    public override string DisplayName => "Диспетчер загруженных карт (MapsBroker): отключить";
    public override string Description => "Фоновая загрузка и обновление офлайн-карт Windows Maps.";
    public override string TradeOff => "Приложение Карты перестанет автоматически обновлять офлайн-данные.";
    public override Model.RiskLevel Risk => Model.RiskLevel.Safe;
    public override string Category => Categories.Services;
    protected override string ServiceName => "MapsBroker";
}
