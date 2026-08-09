using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;
using OptiMaxing.Core.Safety;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>
/// Ultimate Performance is a hidden plan template that must be duplicated into the
/// visible list before it can be selected. Revert removes the duplicated plan and
/// restores whichever plan was active before, rather than guessing a "default".
///
/// State detection cannot rely on the scheme's display name: powercfg localises it
/// (e.g. "Максимальная производительность" on RU Windows), and a *different*,
/// pre-existing scheme can legitimately carry that same localized name without being
/// the one we created. It also cannot rely on the created scheme's GUID being stable,
/// because /duplicatescheme mints a fresh random GUID every time. Instead we persist
/// the GUID we created in a small state file (independent of the per-run backup
/// snapshot, so it survives across the app restarting) and compare against that.
/// </summary>
public sealed class PowerPlanUltimatePerformance(IProcessRunner processRunner) : IOptimization
{
    private const string UltimatePerformanceTemplateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    private static readonly string StateFile = Path.Combine(AppPaths.Root, "power-ultimate-performance.guid");

    public string Id => "power-ultimate-performance";
    public string DisplayName => "Электропитание: Ultimate Performance";
    public string Description =>
        "Отключает большинство энергосберегающих переходов CPU/USB, снижая микро-задержки ценой большего энергопотребления и нагрева.";
    public string? TradeOff =>
        "Заметно выше энергопотребление и тепловыделение в простое. На ноутбуке без сети — короче автономная работа.";
    public RiskLevel Risk => RiskLevel.Safe;
    public Reversibility Reversibility => Reversibility.Reversible;
    public string Category => Categories.Power;
    public bool RequiresRestart => false;

    public async Task<ApplyState> GetStateAsync(CancellationToken ct)
    {
        var createdGuid = ReadPersistedCreatedGuid();
        if (createdGuid is null)
            return ApplyState.NotApplied;

        var result = await processRunner.RunAsync("powercfg", "/getactivescheme", ct);
        if (!result.Succeeded)
            return ApplyState.Unknown;

        var activeGuid = ExtractGuid(result.StandardOutput);
        return string.Equals(activeGuid, createdGuid, StringComparison.OrdinalIgnoreCase)
            ? ApplyState.Applied
            : ApplyState.NotApplied;
    }

    public async Task ApplyAsync(OptimizationContext context)
    {
        var before = await processRunner.RunAsync("powercfg", "/getactivescheme", context.Cancellation);
        var previousGuid = ExtractGuid(before.StandardOutput);
        context.Backup.Capture(Id, "previous-scheme-guid", previousGuid);

        // Ultimate Performance is hidden until duplicated once; duplicating it
        // again on a later apply is harmless — Windows just creates a second copy,
        // which is why we check GetStateAsync before calling this.
        var duplicate = await processRunner.RunAsync(
            "powercfg", $"/duplicatescheme {UltimatePerformanceTemplateGuid}", context.Cancellation);

        var newGuid = ExtractGuid(duplicate.StandardOutput) ?? UltimatePerformanceTemplateGuid;
        context.Backup.Capture(Id, "created-scheme-guid", newGuid);
        WritePersistedCreatedGuid(newGuid);

        var setActive = await processRunner.RunAsync("powercfg", $"/setactive {newGuid}", context.Cancellation);
        if (!setActive.Succeeded)
            throw new InvalidOperationException($"powercfg /setactive failed: {setActive.StandardError}");

        context.Log.Report($"    активная схема электропитания -> Ultimate Performance ({newGuid})");
    }

    public async Task RevertAsync(OptimizationContext context)
    {
        var previousGuid = context.Backup.Read(Id, "previous-scheme-guid");
        var createdGuid = context.Backup.Read(Id, "created-scheme-guid");

        if (previousGuid is not null)
        {
            var restore = await processRunner.RunAsync("powercfg", $"/setactive {previousGuid}", context.Cancellation);
            context.Log.Report(restore.Succeeded
                ? $"    активная схема восстановлена ({previousGuid})"
                : $"    не удалось восстановить прежнюю схему: {restore.StandardError}");
        }

        if (createdGuid is not null)
        {
            // Deleting the *scheme entry* is not a data-loss risk — it is a settings
            // container we created, distinct from deleting user files or history.
            var delete = await processRunner.RunAsync("powercfg", $"/delete {createdGuid}", context.Cancellation);
            context.Log.Report(delete.Succeeded
                ? "    временная схема Ultimate Performance удалена"
                : $"    не удалось удалить временную схему: {delete.StandardError}");
        }

        ClearPersistedCreatedGuid();
    }

    private static string? ReadPersistedCreatedGuid()
    {
        try
        {
            return File.Exists(StateFile) ? File.ReadAllText(StateFile).Trim() : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static void WritePersistedCreatedGuid(string guid)
    {
        try
        {
            File.WriteAllText(StateFile, guid);
        }
        catch (IOException)
        {
            // Best-effort: if we can't persist it, GetStateAsync will just report
            // NotApplied on next launch, which is safe (never a false "Applied").
        }
    }

    private static void ClearPersistedCreatedGuid()
    {
        try
        {
            if (File.Exists(StateFile))
                File.Delete(StateFile);
        }
        catch (IOException)
        {
            // Non-fatal — worst case a stale GUID lingers and GetStateAsync will
            // simply report NotApplied once the scheme no longer exists/matches.
        }
    }

    private static string? ExtractGuid(string powercfgOutput)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            powercfgOutput,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        return match.Success ? match.Value : null;
    }
}
