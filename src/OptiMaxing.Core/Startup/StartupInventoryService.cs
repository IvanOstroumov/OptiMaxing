using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Startup;

/// <summary>Enumerates and controls autostart entries the way Autoruns/Task Manager do.</summary>
public sealed class StartupInventoryService(IRegistryProvider registry, IFileSystem fileSystem)
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string RunOnceKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string RunKey32 = @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved";

    private readonly string _windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    public string UserStartupFolder { get; init; } =
        Environment.GetFolderPath(Environment.SpecialFolder.Startup);

    public string CommonStartupFolder { get; init; } =
        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);

    public IReadOnlyList<StartupEntry> List()
    {
        var entries = new List<StartupEntry>();

        entries.AddRange(ReadRegistry(RegistryHive.CurrentUser, RunKey, StartupSource.RegistryRun, StartupScope.CurrentUser, false));
        entries.AddRange(ReadRegistry(RegistryHive.CurrentUser, RunOnceKey, StartupSource.RegistryRunOnce, StartupScope.CurrentUser, false));
        entries.AddRange(ReadRegistry(RegistryHive.LocalMachine, RunKey, StartupSource.RegistryRun, StartupScope.AllUsers, false));
        entries.AddRange(ReadRegistry(RegistryHive.LocalMachine, RunOnceKey, StartupSource.RegistryRunOnce, StartupScope.AllUsers, false));
        entries.AddRange(ReadRegistry(RegistryHive.LocalMachine, RunKey32, StartupSource.RegistryRun, StartupScope.AllUsers, true));

        entries.AddRange(ReadFolder(UserStartupFolder, StartupScope.CurrentUser));
        entries.AddRange(ReadFolder(CommonStartupFolder, StartupScope.AllUsers));

        return entries;
    }

    private IEnumerable<StartupEntry> ReadRegistry(
        RegistryHive hive, string subKey, StartupSource source, StartupScope scope, bool is32Bit)
    {
        if (!registry.KeyExists(hive, subKey))
        {
            yield break;
        }

        foreach (var name in registry.GetValueNames(hive, subKey))
        {
            var command = registry.GetValue(hive, subKey, name)?.ToString() ?? string.Empty;
            var executable = ExtractExecutablePath(command);

            yield return new StartupEntry(
                source,
                scope,
                name,
                command,
                executable,
                IsApproved(hive, ApprovedSubKey(source, is32Bit), name),
                executable is not null && fileSystem.FileExists(executable),
                IsCritical(executable),
                $"{HiveText(hive)}\\{subKey}",
                hive,
                subKey,
                is32Bit);
        }
    }

    private IEnumerable<StartupEntry> ReadFolder(string folder, StartupScope scope)
    {
        if (!fileSystem.DirectoryExists(folder))
        {
            yield break;
        }

        var hive = scope == StartupScope.CurrentUser ? RegistryHive.CurrentUser : RegistryHive.LocalMachine;

        foreach (var file in fileSystem.EnumerateFiles(folder, "*"))
        {
            var name = Path.GetFileName(file.Path);
            if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return new StartupEntry(
                StartupSource.StartupFolder,
                scope,
                name,
                file.Path,
                file.Path,
                IsApproved(hive, "StartupFolder", name),
                fileSystem.FileExists(file.Path),
                IsCritical(file.Path),
                folder,
                hive,
                folder,
                false);
        }
    }

    public void SetEnabled(StartupEntry entry, bool enabled)
    {
        // Windows itself records "user switched this off" in StartupApproved rather than deleting
        // the entry, and both Task Manager and Settings read it from there. Writing the same place
        // means our toggle and theirs agree, and nothing is destroyed — unlike deleting the value,
        // which is what a naive implementation would do and could not be undone.
        var approvedKey = $@"{ApprovedRoot}\{ApprovedSubKey(entry.Source, entry.Is32BitView)}";
        var payload = new byte[12];
        payload[0] = enabled ? (byte)0x02 : (byte)0x03;

        if (!enabled)
        {
            var stamp = BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc());
            Array.Copy(stamp, 0, payload, 4, 8);
        }

        registry.SetValue(entry.Hive, approvedKey, entry.Name, payload, RegistryValueKind.Binary);
    }

    /// <summary>Irreversible: removes the entry itself, not just its approval flag.</summary>
    public bool Delete(StartupEntry entry)
    {
        if (entry.Source == StartupSource.StartupFolder)
        {
            return fileSystem.TryDeleteFile(entry.Command);
        }

        registry.DeleteValue(entry.Hive, entry.SubKey, entry.Name);
        return true;
    }

    private bool IsApproved(RegistryHive hive, string approvedSubKey, string name)
    {
        var value = registry.GetValue(hive, $@"{ApprovedRoot}\{approvedSubKey}", name);

        // No record at all means Windows has never been told otherwise, i.e. enabled. When a
        // record exists, the low bit of the first byte is the disabled flag (0x03 disabled,
        // 0x02/0x06 enabled).
        return value is not byte[] { Length: > 0 } flags || (flags[0] & 1) == 0;
    }

    private static string ApprovedSubKey(StartupSource source, bool is32Bit) => source switch
    {
        StartupSource.StartupFolder => "StartupFolder",
        _ => is32Bit ? "Run32" : "Run",
    };

    private static string HiveText(RegistryHive hive) =>
        hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM";

    private bool IsCritical(string? executablePath) =>
        executablePath is not null
        && _windowsDirectory.Length > 0
        && executablePath.StartsWith(_windowsDirectory, StringComparison.OrdinalIgnoreCase);

    /// <summary>Pulls the executable out of a command line. Quoted paths win outright; unquoted
    /// ones are walked token by token, because "C:\Program Files\App\app.exe -silent" contains
    /// spaces inside the path itself and splitting on the first space gets it wrong.</summary>
    public static string? ExtractExecutablePath(string command)
    {
        var trimmed = command.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed[0] == '"')
        {
            var end = trimmed.IndexOf('"', 1);
            return end > 1 ? trimmed[1..end] : null;
        }

        var candidate = trimmed;
        while (true)
        {
            if (candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }

            var lastSpace = candidate.LastIndexOf(' ');
            if (lastSpace <= 0)
            {
                return trimmed;
            }

            candidate = candidate[..lastSpace].TrimEnd();
        }
    }
}
