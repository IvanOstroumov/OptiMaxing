namespace OptiMaxing.Core.Model;

/// <summary>One journal entry. Persisted so "undo" survives an application restart.</summary>
public sealed record OperationRecord
{
    public required string OptimizationId { get; init; }
    public required string DisplayName { get; init; }
    public required OperationKind Kind { get; init; }
    public required OperationOutcome Outcome { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public required Reversibility Reversibility { get; init; }
    public bool RequiresRestart { get; init; }
    public string? Error { get; init; }

    /// <summary>Backup slot id holding the pre-change values, when one was written.</summary>
    public string? SnapshotId { get; init; }

    /// <summary>True once this entry has been rolled back, so the UI stops offering undo twice.</summary>
    public bool Reverted { get; init; }
}
