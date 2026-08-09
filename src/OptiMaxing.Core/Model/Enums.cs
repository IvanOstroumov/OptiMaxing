namespace OptiMaxing.Core.Model;

public enum RiskLevel
{
    Safe,
    Caution,
    Advanced,
    /// <summary>Cannot be automated from Windows; UI shows instructions only.</summary>
    Advisory,
}

/// <summary>
/// How truthfully an operation can be undone. The engine refuses to fold
/// <see cref="Irreversible"/> items into bulk presets without separate consent.
/// </summary>
public enum Reversibility
{
    /// <summary>Prior state is captured and restoring it is exact (registry values, service start type).</summary>
    Reversible,

    /// <summary>Undo is attempted but may not fully succeed (e.g. AppX re-registration, driver-owned settings).</summary>
    ReversibleWithCaveat,

    /// <summary>Destroys data permanently (file deletion, cache purge). No undo exists.</summary>
    Irreversible,
}

/// <summary>
/// Tri-state replacement for a plain bool: a setting may hold a third-party value
/// that is neither our target nor the Windows default.
/// </summary>
public enum ApplyState
{
    Unknown,
    NotApplied,
    Applied,
    /// <summary>Present but set to a value we did not write.</summary>
    Modified,
    /// <summary>Not meaningful on this machine (wrong GPU vendor, no Wi-Fi adapter, ...).</summary>
    NotApplicable,
}

public enum OperationOutcome
{
    Success,
    Failed,
    Skipped,
}

public enum OperationKind
{
    Apply,
    Revert,
}
