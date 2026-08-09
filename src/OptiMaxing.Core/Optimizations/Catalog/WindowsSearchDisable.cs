using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class WindowsSearchDisable(IServiceManager services) : ServiceDisableOptimization(services)
{
    public override string Id => "service-wsearch-disable";
    public override string DisplayName => "Windows Search: отключить";
    public override string Description =>
        "Индексирует файлы для быстрого поиска через меню Пуск и Проводник.";
    public override string TradeOff =>
        "Поиск в Пуске и Проводнике станет заметно медленнее — будет искать по файлам напрямую, а не по индексу.";
    public override Model.RiskLevel Risk => Model.RiskLevel.Caution;
    public override string Category => Categories.Services;
    protected override string ServiceName => "WSearch";
}
