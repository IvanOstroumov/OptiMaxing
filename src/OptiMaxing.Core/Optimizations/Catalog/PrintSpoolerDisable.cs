using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class PrintSpoolerDisable(IServiceManager services) : ServiceDisableOptimization(services)
{
    public override string Id => "service-spooler-disable";
    public override string DisplayName => "Диспетчер печати (Print Spooler): отключить";
    public override string Description =>
        "Держит в памяти службу печати, даже если принтера нет. Заодно закрывает известный класс уязвимостей (PrintNightmare) на машине без принтера.";
    public override string TradeOff =>
        "Если подключишь принтер или включишь виртуальный принтер (PDF) — печать перестанет работать, пока не включишь службу обратно.";
    public override Model.RiskLevel Risk => Model.RiskLevel.Caution;
    public override string Category => Categories.Services;
    protected override string ServiceName => "Spooler";
}
