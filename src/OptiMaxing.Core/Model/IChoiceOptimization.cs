namespace OptiMaxing.Core.Model;

/// <param name="IsWindowsDefault">The value a stock Windows install has. Marked in the UI so the
/// user can always find their way back without needing a backup.</param>
/// <param name="IsRecommended">What we would pick for a gaming PC. Never auto-applied — a tweak
/// with several sensible values is exactly the kind that depends on the machine.</param>
public sealed record TweakChoice(
    string Id,
    string DisplayName,
    string Description,
    bool IsWindowsDefault = false,
    bool IsRecommended = false);

/// <summary>A tweak that is not on/off but picks one of several values. Everything the plain
/// IOptimization contract offers still applies; this only adds the list of values and what the
/// system currently holds, so the UI can show "Сейчас в системе: ..." instead of a bare checkbox.</summary>
public interface IChoiceOptimization : IOptimization
{
    IReadOnlyList<TweakChoice> Choices { get; }

    /// <summary>Which choice gets written on apply. Defaults to the recommended one.</summary>
    string SelectedChoiceId { get; set; }

    /// <summary>The choice currently in effect, or null when the system holds a value none of the
    /// choices describe — a third-party tweaker's value, which must not be silently relabelled.</summary>
    Task<TweakChoice?> GetCurrentChoiceAsync(CancellationToken ct);

    /// <summary>Raw system value as text, for the "Сейчас в системе" line. Shown even when it
    /// matches a known choice, because the number itself is what people compare against guides.</summary>
    Task<string> DescribeCurrentAsync(CancellationToken ct);
}
