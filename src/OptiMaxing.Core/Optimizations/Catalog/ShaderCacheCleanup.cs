using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class ShaderCacheCleanup(IFileSystem fileSystem) : FileCleanupOptimization(fileSystem)
{
    public override string Id => "cleanup-shader-cache";
    public override string DisplayName => "Кэш шейдеров DirectX/NVIDIA: очистить устаревшее";
    public override string Description =>
        "Удаляет файлы кэша скомпилированных шейдеров старше 30 дней — для игр, в которые давно не играл.";
    public override string TradeOff =>
        "Для игр без свежего кэша первый запуск после очистки может пройти с кратким подтормаживанием, пока шейдеры перекомпилируются.";
    public override RiskLevel Risk => RiskLevel.Safe;

    protected override IReadOnlyList<CleanupTarget> Targets =>
    [
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache"), "*", TimeSpan.FromDays(30)),
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA", "DXCache"), "*", TimeSpan.FromDays(30)),
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA", "GLCache"), "*", TimeSpan.FromDays(30)),
    ];
}
