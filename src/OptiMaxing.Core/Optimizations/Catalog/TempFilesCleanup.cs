using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class TempFilesCleanup(IFileSystem fileSystem) : FileCleanupOptimization(fileSystem)
{
    public override string Id => "cleanup-temp-files";
    public override string DisplayName => "Временные файлы (%TEMP%, C:\\Windows\\Temp): очистить";
    public override string Description =>
        "Удаляет файлы старше суток из пользовательской и системной папок временных файлов.";
    public override RiskLevel Risk => RiskLevel.Safe;

    protected override IReadOnlyList<CleanupTarget> Targets =>
    [
        new(Path.GetTempPath(), "*", TimeSpan.FromDays(1)),
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"), "*", TimeSpan.FromDays(1)),
    ];
}
