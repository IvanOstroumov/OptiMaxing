using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>
/// Disables one scheduled task via schtasks /Change /Disable — never deletes the task
/// definition, mirroring the same never-delete rule as service tweaks. Revert just
/// re-enables it.
/// </summary>
public sealed class ScheduledTaskDisable(IProcessRunner processRunner, ScheduledTaskCatalog.Entry task) : IOptimization
{
    public string Id => $"task-{task.TaskPath}-disable".Replace('\\', '_').Replace(' ', '_').ToLowerInvariant();
    public string DisplayName => $"Задача планировщика: {task.DisplayName} — отключить";
    public string Description => task.Description;
    public string? TradeOff => "Отключает именно эту фоновую задачу; остальные задачи по тому же пути не затрагиваются.";
    public RiskLevel Risk => RiskLevel.Safe;
    public Reversibility Reversibility => Reversibility.Reversible;
    public string Category => Categories.Startup;
    public bool RequiresRestart => false;

    public async Task<ApplyState> GetStateAsync(CancellationToken ct)
    {
        var state = await QueryStateAsync(ct);
        return state switch
        {
            "Disabled" => ApplyState.Applied,
            null => ApplyState.NotApplicable,
            _ => ApplyState.NotApplied,
        };
    }

    public async Task ApplyAsync(OptimizationContext context)
    {
        var before = await QueryStateAsync(context.Cancellation);
        if (before is null)
        {
            context.Log.Report($"    задача {task.TaskPath} не найдена, пропускаю");
            return;
        }

        context.Backup.Capture(Id, "previous-state", before);

        var result = await processRunner.RunAsync(
            "schtasks.exe", $"/Change /TN \"{task.TaskPath}\" /Disable", context.Cancellation);

        if (!result.Succeeded)
            throw new InvalidOperationException($"schtasks /Disable failed for {task.TaskPath}: {result.StandardError}");

        context.Log.Report($"    {task.TaskPath}: {before} -> Disabled");
    }

    public async Task RevertAsync(OptimizationContext context)
    {
        var previous = context.Backup.Read(Id, "previous-state");
        if (previous == "Disabled")
        {
            context.Log.Report($"    {task.TaskPath} уже был отключён до нас, оставляю выключенным");
            return;
        }

        var result = await processRunner.RunAsync(
            "schtasks.exe", $"/Change /TN \"{task.TaskPath}\" /Enable", context.Cancellation);

        context.Log.Report(result.Succeeded
            ? $"    {task.TaskPath} снова включена"
            : $"    не удалось включить {task.TaskPath}: {result.StandardError}");
    }

    // schtasks' text output (Status:/Состояние:) is localized, same trap as the
    // PowerPlan bug fixed earlier. Get-ScheduledTask's .State is a .NET enum, so its
    // value (Ready/Disabled/Running/...) stays in English regardless of UI culture.
    private async Task<string?> QueryStateAsync(CancellationToken ct)
    {
        var splitAt = task.TaskPath.LastIndexOf('\\');
        var folder = task.TaskPath[..(splitAt + 1)];
        var name = task.TaskPath[(splitAt + 1)..];

        var result = await processRunner.RunAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -Command \"(Get-ScheduledTask -TaskPath '{Escape(folder)}' -TaskName '{Escape(name)}' -ErrorAction SilentlyContinue).State\"",
            ct);

        var state = result.StandardOutput.Trim();
        return result.Succeeded && state.Length > 0 ? state : null;
    }

    private static string Escape(string value) => value.Replace("'", "''");
}
