using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>
/// Deliberately Caution-tier and never part of the Safe preset: unlike temp/shader
/// caches, clearing a browser cache is directly noticeable (every site reloads
/// full-weight the next visit) even though it touches only the Cache folder, not
/// cookies/passwords/history.
/// </summary>
public sealed class BrowserCacheCleanup(IFileSystem fileSystem) : FileCleanupOptimization(fileSystem)
{
    public override string Id => "cleanup-browser-cache";
    public override string DisplayName => "Кэш браузеров (Chrome/Edge): очистить";
    public override string Description =>
        "Удаляет файлы папки Cache Chrome и Edge. Не трогает пароли, куки, историю и закладки — только временный кэш страниц.";
    public override string TradeOff =>
        "После очистки сайты будут ненадолго грузиться медленнее, пока кэш не наполнится заново.";
    public override RiskLevel Risk => RiskLevel.Caution;

    protected override IReadOnlyList<CleanupTarget> Targets =>
    [
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Google", "Chrome", "User Data", "Default", "Cache"), "*"),
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Edge", "User Data", "Default", "Cache"), "*"),
    ];
}
