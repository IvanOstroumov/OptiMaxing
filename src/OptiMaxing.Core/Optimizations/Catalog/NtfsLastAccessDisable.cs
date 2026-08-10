using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>
/// fsutil is the only supported way to read/change this — it is not a plain registry
/// DWORD, so this does not use RegistryOptimization.
/// </summary>
public sealed class NtfsLastAccessDisable(IProcessRunner processRunner) : IOptimization
{
    public string Id => "storage-ntfs-last-access-disable";
    public string DisplayName => "NTFS: отключить обновление времени последнего доступа к файлам";
    public string Description =>
        "Отключает запись метки 'последний доступ' при каждом открытии файла — чуть меньше лишних записей на диск, особенно на HDD и при индексации/сканировании больших папок с игровыми ассетами.";
    public string? TradeOff =>
        "Некоторые бэкап-утилиты и криминалистический софт используют это поле; на игры и обычную работу не влияет.";
    public RiskLevel Risk => RiskLevel.Safe;
    public Reversibility Reversibility => Reversibility.Reversible;
    public string Category => Categories.Storage;
    public bool RequiresRestart => false;

    public async Task<ApplyState> GetStateAsync(CancellationToken ct)
    {
        var value = await QueryAsync(ct);
        return value switch
        {
            null => ApplyState.Unknown,
            1 => ApplyState.Applied,
            0 => ApplyState.NotApplied,
            _ => ApplyState.Modified, // e.g. "2" = disabled by Group Policy
        };
    }

    public async Task ApplyAsync(OptimizationContext context)
    {
        var before = await QueryAsync(context.Cancellation);
        context.Backup.Capture(Id, "previous", before?.ToString() ?? "0");

        var result = await processRunner.RunAsync("fsutil.exe", "behavior set disablelastaccess 1", context.Cancellation);
        if (!result.Succeeded)
            throw new InvalidOperationException($"fsutil behavior set disablelastaccess failed: {result.StandardError}");

        context.Log.Report("    disablelastaccess -> 1 (отключено)");
    }

    public async Task RevertAsync(OptimizationContext context)
    {
        var previous = context.Backup.Read(Id, "previous") ?? "0";

        var result = await processRunner.RunAsync("fsutil.exe", $"behavior set disablelastaccess {previous}", context.Cancellation);
        context.Log.Report(result.Succeeded
            ? $"    disablelastaccess восстановлен в {previous}"
            : $"    не удалось восстановить disablelastaccess: {result.StandardError}");
    }

    private async Task<int?> QueryAsync(CancellationToken ct)
    {
        var result = await processRunner.RunAsync("fsutil.exe", "behavior query disablelastaccess", ct);
        if (!result.Succeeded)
            return null;

        // Output is a locale-dependent sentence that always contains the numeric state
        // ("... = 1" / "... = 0" / "... DISABLE (2)" depending on build), so the last number wins
        // rather than any particular word.
        //
        // The class is spelled [0-9] on purpose: \d in .NET also matches Devanagari, Arabic-Indic
        // and other digit scripts, and a Russian-locale fsutil decoded through the wrong code page
        // produced exactly such a character here — int.Parse then threw and took the whole scan
        // down with it.
        var matches = System.Text.RegularExpressions.Regex.Matches(result.StandardOutput, "[0-9]+");
        if (matches.Count == 0)
            return null;

        return int.TryParse(
            matches[^1].Value,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }
}
