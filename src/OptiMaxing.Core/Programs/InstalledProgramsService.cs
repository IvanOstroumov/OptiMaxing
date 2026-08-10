using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Programs;

/// <summary>Reads the Uninstall hives — the same source Settings and Control Panel use — and can
/// launch a program's own uninstaller. OptiMaxing never deletes a program's files itself: removing
/// an app means running the uninstaller the vendor shipped, so nothing is left half-removed.</summary>
public sealed class InstalledProgramsService(IRegistryProvider registry, IProcessRunner processRunner)
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string UninstallKey32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    public IReadOnlyList<InstalledProgram> List()
    {
        var programs = new List<InstalledProgram>();

        Collect(RegistryHive.LocalMachine, UninstallKey, is32Bit: false, isPerUser: false, programs);
        Collect(RegistryHive.LocalMachine, UninstallKey32, is32Bit: true, isPerUser: false, programs);
        Collect(RegistryHive.CurrentUser, UninstallKey, is32Bit: false, isPerUser: true, programs);
        Collect(RegistryHive.CurrentUser, UninstallKey32, is32Bit: true, isPerUser: true, programs);

        // The same product often registers in both the 64-bit and 32-bit views. Dedupe on
        // name+version so the list matches what the user sees in Settings.
        return programs
            .GroupBy(p => $"{p.Name}|{p.Version}", StringComparer.CurrentCultureIgnoreCase)
            .Select(g => g.First())
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private void Collect(RegistryHive hive, string root, bool is32Bit, bool isPerUser, List<InstalledProgram> into)
    {
        foreach (var subKey in registry.GetSubKeyNames(hive, root))
        {
            var path = $@"{root}\{subKey}";
            var name = Str(hive, path, "DisplayName");

            // Entries without a display name are patch/update records, not products; Settings
            // hides them too.
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            into.Add(new InstalledProgram(
                name,
                Str(hive, path, "Publisher"),
                Str(hive, path, "DisplayVersion"),
                ParseInstallDate(Str(hive, path, "InstallDate")),
                registry.GetValue(hive, path, "EstimatedSize") is int kb and > 0 ? kb * 1024L : null,
                Str(hive, path, "UninstallString"),
                Str(hive, path, "QuietUninstallString"),
                Str(hive, path, "InstallLocation"),
                registry.GetValue(hive, path, "SystemComponent") is 1
                    || Str(hive, path, "ParentKeyName") is not null,
                subKey,
                is32Bit,
                isPerUser));
        }
    }

    /// <summary>Runs the vendor's uninstaller. Deliberately not quiet by default: an unattended
    /// uninstall gives the user no chance to keep settings or cancel, and some installers
    /// interpret the quiet switch as "also wipe user data".</summary>
    public async Task<ProcessResult> UninstallAsync(InstalledProgram program, bool quiet, CancellationToken ct)
    {
        var command = quiet && !string.IsNullOrWhiteSpace(program.QuietUninstallCommand)
            ? program.QuietUninstallCommand
            : program.UninstallCommand;

        if (string.IsNullOrWhiteSpace(command))
        {
            return new ProcessResult(-1, string.Empty, "У программы нет собственного деинсталлятора.");
        }

        var (fileName, arguments) = SplitCommand(command);
        return await processRunner.RunAsync(fileName, arguments, ct);
    }

    /// <summary>Uninstall strings mix a possibly-quoted executable path with arguments; splitting on
    /// the first space breaks every program installed under "Program Files".</summary>
    public static (string FileName, string Arguments) SplitCommand(string command)
    {
        command = command.Trim();

        if (command.StartsWith('"'))
        {
            var closing = command.IndexOf('"', 1);
            return closing < 0
                ? (command.Trim('"'), string.Empty)
                : (command[1..closing], command[(closing + 1)..].Trim());
        }

        var exe = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exe >= 0)
        {
            var end = exe + 4;
            return (command[..end], command[end..].Trim());
        }

        var space = command.IndexOf(' ');
        return space < 0 ? (command, string.Empty) : (command[..space], command[(space + 1)..]);
    }

    /// <summary>InstallDate is stored as yyyyMMdd. Some vendors write garbage there, so a failed
    /// parse yields null rather than an exception.</summary>
    private static DateTime? ParseInstallDate(string? raw) =>
        DateTime.TryParseExact(raw, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date)
            ? date
            : null;

    private string? Str(RegistryHive hive, string path, string name) =>
        registry.GetValue(hive, path, name) as string is { Length: > 0 } value ? value : null;
}
