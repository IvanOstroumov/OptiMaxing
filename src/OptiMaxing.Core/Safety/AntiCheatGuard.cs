namespace OptiMaxing.Core.Safety;

/// <summary>
/// Curated list of games whose anti-cheat refuses to run without VBS/HVCI or Secure
/// Boot. The user approved hard-blocking the conflicting tweak (not just warning)
/// when one of these is detected installed.
/// </summary>
public static class AntiCheatGuard
{
    /// <summary>Optimization ids that a detected game below can veto.</summary>
    public static readonly IReadOnlySet<string> VbsDependentOptimizationIds = new HashSet<string>
    {
        "gpu-vbs-memory-integrity-disable",
    };

    public sealed record KnownGame(string DisplayName, IReadOnlyList<string> ExecutableNames);

    public static readonly IReadOnlyList<KnownGame> RequiresVbs =
    [
        new("VALORANT (Vanguard)", ["VALORANT-Win64-Shipping.exe", "vgc.exe", "vgk.sys"]),
        new("Fortnite (Easy Anti-Cheat, protected mode)", ["FortniteClient-Win64-Shipping.exe"]),
        new("Call of Duty (Ricochet)", ["cod.exe", "ModernWarfare.exe"]),
    ];

    /// <summary>
    /// Scans common launcher library roots for known executables. Best-effort:
    /// a miss here is a false negative, not a false positive, so it never blocks
    /// unnecessarily — it only ever fails to warn.
    /// </summary>
    public static IReadOnlyList<KnownGame> DetectInstalled(IEnumerable<string> libraryRoots)
    {
        var found = new List<KnownGame>();

        foreach (var game in RequiresVbs)
        {
            var installed = libraryRoots
                .Where(Directory.Exists)
                .Any(root => game.ExecutableNames.Any(exe =>
                    Directory.EnumerateFiles(root, exe, SearchOption.AllDirectories).Any()));

            if (installed)
                found.Add(game);
        }

        return found;
    }
}
