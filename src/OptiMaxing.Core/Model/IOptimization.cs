namespace OptiMaxing.Core.Model;

/// <summary>Context handed to every optimization so it can log, back up and honour cancellation.</summary>
public sealed class OptimizationContext
{
    public required IBackupWriter Backup { get; init; }
    public required IProgress<string> Log { get; init; }
    public CancellationToken Cancellation { get; init; }
}

/// <summary>Records the pre-change state of one setting so it can be restored later.</summary>
public interface IBackupWriter
{
    void Capture(string optimizationId, string key, string? previousValue);
    string? Read(string optimizationId, string key);
}

public interface IOptimization
{
    string Id { get; }
    string DisplayName { get; }
    string Description { get; }

    /// <summary>Honest statement of what the user gives up. Shown under the toggle.</summary>
    string? TradeOff { get; }

    RiskLevel Risk { get; }
    Reversibility Reversibility { get; }
    string Category { get; }
    bool RequiresRestart { get; }

    Task<ApplyState> GetStateAsync(CancellationToken ct);
    Task ApplyAsync(OptimizationContext context);
    Task RevertAsync(OptimizationContext context);
}
