using OptiMaxing.Core.Model;
using OptiMaxing.Core.Safety;

namespace OptiMaxing.Core.Engine;

public sealed record BatchResult(
    IReadOnlyList<OperationRecord> Records,
    string SnapshotId)
{
    public int SucceededCount => Records.Count(r => r.Outcome == OperationOutcome.Success);
    public int FailedCount => Records.Count(r => r.Outcome == OperationOutcome.Failed);
    public bool RestartRequired => Records.Any(r => r.Outcome == OperationOutcome.Success && r.RequiresRestart);
}

public sealed class OptimizationEngine(
    IBackupService backupService,
    IOperationJournal journal,
    IRestorePointService restorePointService,
    IAppLogger logger)
{
    /// <summary>
    /// Advanced tweaks are refused unless a restore point exists, per the safety
    /// contract. Returns null when the batch is allowed to proceed.
    /// </summary>
    public string? CheckGate(IReadOnlyList<IOptimization> selection)
    {
        if (!selection.Any(o => o.Risk == RiskLevel.Advanced))
            return null;

        var status = restorePointService.GetStatus();
        if (status.BlockedByPolicy)
            return "System Protection is disabled by group policy, so no restore point can be created. Advanced tweaks stay locked.";

        if (status.LastRestorePointUtc is null)
            return "No restore point exists. Create one before applying Advanced tweaks.";

        return null;
    }

    /// <summary>
    /// Hard block (not a soft warning, per the customer's decision) when a selected
    /// tweak conflicts with anti-cheat requirements of a game found on this machine.
    /// </summary>
    public string? CheckAntiCheatConflicts(IReadOnlyList<IOptimization> selection, IEnumerable<string> libraryRoots)
    {
        var conflicting = selection.Where(o => AntiCheatGuard.VbsDependentOptimizationIds.Contains(o.Id)).ToList();
        if (conflicting.Count == 0)
            return null;

        var detected = AntiCheatGuard.DetectInstalled(libraryRoots);
        if (detected.Count == 0)
            return null;

        var tweakNames = string.Join(", ", conflicting.Select(o => o.DisplayName));
        var gameNames = string.Join(", ", detected.Select(g => g.DisplayName));
        return $"Заблокировано: найдены игры, требующие VBS/Secure Boot ({gameNames}). " +
               $"Применение '{tweakNames}' сломает их анти-чит. Убери игру или сними этот твик из выбора.";
    }

    public async Task<BatchResult> ApplyBatchAsync(
        IReadOnlyList<IOptimization> selection,
        IProgress<string> log,
        CancellationToken ct)
        => await RunBatchAsync(selection, OperationKind.Apply, log, ct);

    public async Task<BatchResult> RevertBatchAsync(
        IReadOnlyList<IOptimization> selection,
        IProgress<string> log,
        CancellationToken ct)
        => await RunBatchAsync(selection, OperationKind.Revert, log, ct);

    private async Task<BatchResult> RunBatchAsync(
        IReadOnlyList<IOptimization> selection,
        OperationKind kind,
        IProgress<string> log,
        CancellationToken ct)
    {
        var snapshotId = backupService.CreateSnapshot();
        var writer = backupService.GetWriter(snapshotId);
        var records = new List<OperationRecord>(selection.Count);

        foreach (var optimization in selection)
        {
            ct.ThrowIfCancellationRequested();

            var context = new OptimizationContext
            {
                Backup = writer,
                Log = log,
                Cancellation = ct,
            };

            OperationOutcome outcome;
            string? error = null;

            try
            {
                log.Report($"[{kind}] {optimization.DisplayName}");

                if (kind == OperationKind.Apply)
                    await optimization.ApplyAsync(context);
                else
                    await optimization.RevertAsync(context);

                outcome = OperationOutcome.Success;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One failing tweak must never abort the rest of the batch.
                outcome = OperationOutcome.Failed;
                error = ex.Message;
                log.Report($"    FAILED: {ex.Message}");
                logger.Write(LogLevel.Error, $"{kind} failed for '{optimization.Id}'", ex);
            }

            var record = new OperationRecord
            {
                OptimizationId = optimization.Id,
                DisplayName = optimization.DisplayName,
                Kind = kind,
                Outcome = outcome,
                TimestampUtc = DateTimeOffset.UtcNow,
                Reversibility = optimization.Reversibility,
                RequiresRestart = optimization.RequiresRestart,
                Error = error,
                SnapshotId = snapshotId,
            };

            records.Add(record);
            journal.Append(record);
        }

        backupService.Flush(snapshotId);
        return new BatchResult(records, snapshotId);
    }

    /// <summary>
    /// Refreshes state for every optimization in parallel with bounded concurrency:
    /// a serial scan of 300 registry/WMI probes takes tens of seconds.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, ApplyState>> ScanStatesAsync(
        IReadOnlyList<IOptimization> optimizations,
        IProgress<(string Id, ApplyState State)>? progress,
        CancellationToken ct,
        int maxConcurrency = 8)
    {
        using var throttle = new SemaphoreSlim(maxConcurrency);
        var results = new System.Collections.Concurrent.ConcurrentDictionary<string, ApplyState>();

        var tasks = optimizations.Select(async optimization =>
        {
            await throttle.WaitAsync(ct);
            try
            {
                var state = await optimization.GetStateAsync(ct);
                results[optimization.Id] = state;
                progress?.Report((optimization.Id, state));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                results[optimization.Id] = ApplyState.Unknown;
                logger.Write(LogLevel.Warning, $"State probe failed for '{optimization.Id}'", ex);
                progress?.Report((optimization.Id, ApplyState.Unknown));
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }
}
