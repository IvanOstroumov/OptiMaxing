using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>
/// One entry from a Run autostart key (the same list Task Manager's Startup tab and
/// Autoruns show). Built dynamically per machine in <see cref="OptimizationCatalog"/> —
/// unlike the curated bloatware list, autostart entries are inherently machine-specific,
/// so there is no fixed catalog to hardcode.
///
/// Disabling removes the value from the Run key rather than flipping Task Manager's
/// StartupApproved binary flag: the flag format is undocumented and version-fragile,
/// while "value present/absent" is simple, transparent, and trivially reversible via
/// the same backup mechanism every other registry tweak uses.
/// </summary>
public sealed class StartupProgramDisable(
    IRegistryProvider registry,
    RegistryHive hive,
    string subKey,
    string valueName,
    string command) : IOptimization
{
    public string Id => $"startup-{hive}-{valueName}".Replace(' ', '_').ToLowerInvariant();
    public string DisplayName => $"Автозагрузка: {valueName}";
    public string Description => $"Программа запускается при входе в систему: {command}";
    public string? TradeOff => "Программа перестанет запускаться автоматически при входе — сама программа не удаляется.";
    public RiskLevel Risk => RiskLevel.Safe;
    public Reversibility Reversibility => Reversibility.Reversible;
    public string Category => Categories.Startup;
    public bool RequiresRestart => false;

    public Task<ApplyState> GetStateAsync(CancellationToken ct)
    {
        var current = registry.GetValue(hive, subKey, valueName);
        return Task.FromResult(current is null ? ApplyState.Applied : ApplyState.NotApplied);
    }

    public Task ApplyAsync(OptimizationContext context)
    {
        var current = registry.GetValue(hive, subKey, valueName);
        if (current is null)
        {
            context.Log.Report($"    {valueName}: уже не в автозагрузке, пропускаю");
            return Task.CompletedTask;
        }

        context.Backup.Capture(Id, "value", current.ToString());
        registry.DeleteValue(hive, subKey, valueName);
        context.Log.Report($"    {valueName} убран из автозагрузки");
        return Task.CompletedTask;
    }

    public Task RevertAsync(OptimizationContext context)
    {
        var previous = context.Backup.Read(Id, "value");
        if (previous is null)
        {
            context.Log.Report($"    нет бэкапа для {valueName}, оставляю как есть");
            return Task.CompletedTask;
        }

        // Run-key entries are command lines (REG_SZ/REG_EXPAND_SZ); either kind
        // round-trips fine as ExpandString since Windows resolves plain strings too.
        registry.SetValue(hive, subKey, valueName, previous, RegistryValueKind.ExpandString);
        context.Log.Report($"    {valueName} возвращён в автозагрузку");
        return Task.CompletedTask;
    }
}
