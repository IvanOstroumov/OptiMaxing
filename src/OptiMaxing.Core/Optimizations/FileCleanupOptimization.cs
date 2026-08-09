using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations;

/// <summary>One folder this cleanup tweak sweeps.</summary>
/// <param name="MinAge">
/// Only files older than this are touched. Skipping recently-written files avoids
/// deleting something a running process is mid-way through using, e.g. a shader
/// compiled seconds ago for the game currently running.
/// </param>
public sealed record CleanupTarget(string Directory, string SearchPattern, TimeSpan? MinAge = null);

/// <summary>
/// Base for tweaks that free disk space by deleting regenerable files (temp files,
/// caches). Unlike registry/service tweaks this is never reversible — there is no
/// backup of a deleted file's bytes — so every subclass is Reversibility.Irreversible
/// and the UI must get an explicit confirmation before running it (same gate as any
/// other irreversible action).
/// </summary>
public abstract class FileCleanupOptimization(IFileSystem fileSystem) : IOptimization
{
    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public virtual string? TradeOff =>
        "Удалённые файлы не восстановить через откат приложения — это обычный, безопасный для системы кэш/мусор, который Windows или приложения создадут заново по мере надобности.";
    public abstract RiskLevel Risk { get; }
    public Reversibility Reversibility => Reversibility.Irreversible;
    public string Category => Optimizations.Catalog.Categories.Cleanup;
    public bool RequiresRestart => false;

    protected abstract IReadOnlyList<CleanupTarget> Targets { get; }

    public Task<ApplyState> GetStateAsync(CancellationToken ct)
    {
        if (!Targets.Any(t => fileSystem.DirectoryExists(t.Directory)))
            return Task.FromResult(ApplyState.NotApplicable);

        var reclaimable = MatchingFiles().Any();
        return Task.FromResult(reclaimable ? ApplyState.NotApplied : ApplyState.Applied);
    }

    public async Task ApplyAsync(OptimizationContext context)
    {
        await BeforeApplyAsync(context);

        long freedBytes = 0;
        var deletedCount = 0;
        var skippedCount = 0;

        try
        {
            foreach (var file in MatchingFiles())
            {
                context.Cancellation.ThrowIfCancellationRequested();

                if (fileSystem.TryDeleteFile(file.Path))
                {
                    freedBytes += file.Length;
                    deletedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }
        }
        finally
        {
            await AfterApplyAsync(context);
        }

        context.Log.Report(
            $"    удалено файлов: {deletedCount}, освобождено: {freedBytes / (1024.0 * 1024.0):F1} МБ"
            + (skippedCount > 0 ? $", пропущено (заняты другим процессом): {skippedCount}" : string.Empty));
    }

    /// <summary>Hook for subclasses that need to quiesce something (e.g. stop a
    /// service holding the target files open) before deletion.</summary>
    protected virtual Task BeforeApplyAsync(OptimizationContext context) => Task.CompletedTask;

    /// <summary>Always runs after the delete pass, even if it threw or was
    /// cancelled, so a stopped service/paused process is never left that way.</summary>
    protected virtual Task AfterApplyAsync(OptimizationContext context) => Task.CompletedTask;

    public Task RevertAsync(OptimizationContext context)
    {
        context.Log.Report("    очистка необратима — удалённые файлы Windows/приложения создадут заново при необходимости");
        return Task.CompletedTask;
    }

    private IEnumerable<FileEntry> MatchingFiles()
    {
        var now = DateTime.UtcNow;

        foreach (var target in Targets)
        {
            foreach (var file in fileSystem.EnumerateFiles(target.Directory, target.SearchPattern))
            {
                if (target.MinAge is { } minAge && now - file.LastWriteTimeUtc < minAge)
                    continue;

                yield return file;
            }
        }
    }
}
