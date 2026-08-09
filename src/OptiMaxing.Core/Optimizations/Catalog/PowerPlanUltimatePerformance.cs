using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>
/// Ultimate Performance is a hidden plan template that must be duplicated into the
/// visible list before it can be selected. Revert removes the duplicated plan and
/// restores whichever plan was active before, rather than guessing a "default".
/// </summary>
public sealed class PowerPlanUltimatePerformance(IProcessRunner processRunner) : IOptimization
{
    private const string UltimatePerformanceTemplateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

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
        var result = await processRunner.RunAsync("powercfg", "/getactivescheme", ct);
        if (!result.Succeeded)
            return ApplyState.Unknown;

        return result.StandardOutput.Contains(FindDuplicatedGuidHint, StringComparison.OrdinalIgnoreCase)
               || await IsUltimateActiveAsync(result.StandardOutput, ct)
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
    }

    private const string FindDuplicatedGuidHint = "Ultimate Performance";

    private async Task<bool> IsUltimateActiveAsync(string activeSchemeOutput, CancellationToken ct)
    {
        var list = await processRunner.RunAsync("powercfg", "/list", ct);
        var activeGuid = ExtractGuid(activeSchemeOutput);
        return activeGuid is not null
            && list.StandardOutput.Contains(activeGuid, StringComparison.OrdinalIgnoreCase)
            && list.StandardOutput.Contains("Ultimate Performance", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractGuid(string powercfgOutput)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            powercfgOutput,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
        return match.Success ? match.Value : null;
    }
}
