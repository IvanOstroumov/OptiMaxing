using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class SysMainDisable(IServiceManager services) : ServiceDisableOptimization(services)
{
    public override string Id => "service-sysmain-disable";
    public override string DisplayName => "SysMain (Superfetch): отключить";
    public override string Description =>
        "Предзагружает в память часто используемые приложения. Изначально придуман для HDD; на NVMe/SSD выигрыша почти нет, а лишние фоновые обращения к диску мешают в играх.";
    public override string TradeOff =>
        "Приложения могут на доли секунды дольше запускаться в первый раз после перезагрузки.";
    public override Model.RiskLevel Risk => Model.RiskLevel.Safe;
    public override string Category => Categories.Services;
    protected override string ServiceName => "SysMain";
}
