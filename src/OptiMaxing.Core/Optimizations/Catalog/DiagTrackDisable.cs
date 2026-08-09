using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class DiagTrackDisable(IServiceManager services) : ServiceDisableOptimization(services)
{
    public override string Id => "service-diagtrack-disable";
    public override string DisplayName => "Служба телеметрии (DiagTrack): отключить";
    public override string Description =>
        "Собирает и отправляет диагностические данные использования Windows в Microsoft.";
    public override string TradeOff =>
        "Часть функций 'на основе данных об использовании' (рекомендации, часть диагностики проблем) перестанет работать. На FPS не влияет — это про приватность, не про производительность.";
    public override Model.RiskLevel Risk => Model.RiskLevel.Caution;
    public override string Category => Categories.Privacy;
    protected override string ServiceName => "DiagTrack";
}
