using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations;

/// <summary>Base for tweaks that write one registry value chosen from a list. Unlike
/// RegistryOptimization, "applied" is not a single target state: any of the choices may be what the
/// user wants, so state is reported against the selected choice and the raw value is always shown.</summary>
public abstract class RegistryChoiceOptimization(IRegistryProvider registry) : IChoiceOptimization
{
    private const string AbsentMarker = " <absent>";
    private string? _selectedChoiceId;

    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public virtual string? TradeOff => null;
    public abstract RiskLevel Risk { get; }
    public virtual Reversibility Reversibility => Reversibility.Reversible;
    public abstract string Category { get; }
    public virtual bool RequiresRestart => false;

    public abstract IReadOnlyList<TweakChoice> Choices { get; }

    protected abstract RegistryHive Hive { get; }
    protected abstract string SubKey { get; }
    protected abstract string ValueName { get; }
    protected abstract RegistryValueKind Kind { get; }

    /// <summary>The registry value each choice writes. Kept separate from the choice list so the
    /// UI never has to know about raw values.</summary>
    protected abstract object ValueFor(string choiceId);

    protected IRegistryProvider Registry { get; } = registry;

    public string SelectedChoiceId
    {
        get => _selectedChoiceId ??= (Choices.FirstOrDefault(c => c.IsRecommended) ?? Choices[0]).Id;
        set => _selectedChoiceId = value;
    }

    public Task<TweakChoice?> GetCurrentChoiceAsync(CancellationToken ct)
    {
        var current = Registry.GetValue(Hive, SubKey, ValueName);

        if (current is null)
        {
            // An absent value means Windows is using its built-in default, which is exactly what
            // the choice flagged as the default describes.
            return Task.FromResult(Choices.FirstOrDefault(c => c.IsWindowsDefault));
        }

        return Task.FromResult(Choices.FirstOrDefault(c => Matches(current, ValueFor(c.Id))));
    }

    public Task<string> DescribeCurrentAsync(CancellationToken ct)
    {
        var current = Registry.GetValue(Hive, SubKey, ValueName);
        return Task.FromResult(current?.ToString() ?? "значение не задано (по умолчанию)");
    }

    public async Task<ApplyState> GetStateAsync(CancellationToken ct)
    {
        var current = await GetCurrentChoiceAsync(ct);

        if (current is null)
        {
            return ApplyState.Modified;
        }

        return current.Id == SelectedChoiceId ? ApplyState.Applied : ApplyState.NotApplied;
    }

    public Task ApplyAsync(OptimizationContext context)
    {
        context.Cancellation.ThrowIfCancellationRequested();

        var previous = Registry.GetValue(Hive, SubKey, ValueName);
        context.Backup.Capture(Id, BackupKey, previous?.ToString() ?? AbsentMarker);

        var value = ValueFor(SelectedChoiceId);
        Registry.SetValue(Hive, SubKey, ValueName, value, Kind);
        context.Log.Report($"    {SubKey}\\{ValueName} = {value} ({SelectedChoiceId})");

        return Task.CompletedTask;
    }

    public Task RevertAsync(OptimizationContext context)
    {
        context.Cancellation.ThrowIfCancellationRequested();

        var previous = context.Backup.Read(Id, BackupKey);

        if (previous is null)
        {
            context.Log.Report($"    нет резервной копии для {ValueName}, значение не тронуто");
            return Task.CompletedTask;
        }

        if (previous == AbsentMarker)
        {
            Registry.DeleteValue(Hive, SubKey, ValueName);
            context.Log.Report($"    {SubKey}\\{ValueName} удалено (раньше значения не было)");
            return Task.CompletedTask;
        }

        Registry.SetValue(Hive, SubKey, ValueName, Coerce(previous), Kind);
        context.Log.Report($"    {SubKey}\\{ValueName} возвращено к {previous}");

        return Task.CompletedTask;
    }

    private string BackupKey => $"{Hive}\\{SubKey}\\{ValueName}";

    private static bool Matches(object current, object desired) =>
        string.Equals(current.ToString(), desired.ToString(), StringComparison.OrdinalIgnoreCase);

    private object Coerce(string raw) => Kind switch
    {
        RegistryValueKind.DWord => int.Parse(raw),
        RegistryValueKind.QWord => long.Parse(raw),
        _ => raw,
    };
}
