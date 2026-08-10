using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Startup;

/// <summary>Enumerates and controls autostart entries the way Autoruns/Task Manager do.</summary>
public sealed class StartupInventoryService(
    IRegistryProvider registry,
    IFileSystem fileSystem,
    IProcessRunner processRunner)
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

    /// <summary>Where the Task Scheduler keeps one XML file per task, mirroring the folder tree
    /// shown in taskschd.msc.</summary>
    public string ScheduledTasksFolder { get; init; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Tasks");

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
        entries.AddRange(ReadScheduledTasks());
        entries.AddRange(ReadWinlogon());

        return entries;
    }

    private const string WinlogonKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";

    /// <summary>Userinit and Shell decide what runs the moment a user logs in. Nothing legitimate
    /// appends itself here, so the value is worth showing even though it is never a candidate for
    /// switching off: an unexpected entry is the finding, not a tweak.</summary>
    private IEnumerable<StartupEntry> ReadWinlogon()
    {
        foreach (var name in new[] { "Userinit", "Shell" })
        {
            var command = registry.GetValue(RegistryHive.LocalMachine, WinlogonKey, name)?.ToString();
            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            // Userinit is stored as a comma-separated list; the first entry is the real one.
            var executable = ExtractExecutablePath(command.Split(',')[0]);

            yield return new StartupEntry(
                StartupSource.Winlogon,
                StartupScope.AllUsers,
                name,
                command,
                executable,
                IsEnabled: true,
                TargetExists(executable),
                IsCritical: true,
                $"HKLM\\{WinlogonKey}",
                RegistryHive.LocalMachine,
                WinlogonKey,
                Is32BitView: false);
        }
    }

    /// <summary>Only tasks that actually start something at logon or boot. The Task Scheduler holds
    /// several hundred entries on a stock Windows install, and listing maintenance jobs that fire
    /// once a month under "Автозапуск" would bury the handful that matter.</summary>
    private IEnumerable<StartupEntry> ReadScheduledTasks()
    {
        if (!fileSystem.DirectoryExists(ScheduledTasksFolder))
        {
            yield break;
        }

        var root = ScheduledTasksFolder.TrimEnd('\\');

        foreach (var file in fileSystem.EnumerateFiles(ScheduledTasksFolder, "*"))
        {
            var xml = fileSystem.TryReadAllText(file.Path);
            if (xml is null)
            {
                continue;
            }

            var task = ScheduledTaskDefinition.Parse(xml);
            if (task is null || !task.StartsAtLogonOrBoot)
            {
                continue;
            }

            var taskPath = file.Path[root.Length..].Replace('/', '\\');
            var executable = ExtractExecutablePath(task.Command);

            yield return new StartupEntry(
                StartupSource.ScheduledTask,
                StartupScope.AllUsers,
                Path.GetFileName(file.Path),
                string.IsNullOrWhiteSpace(task.Arguments) ? task.Command : $"{task.Command} {task.Arguments}",
                executable,
                task.IsEnabled,
                TargetExists(executable),
                IsCritical(executable),
                taskPath,
                RegistryHive.LocalMachine,
                taskPath,
                Is32BitView: false);
        }
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
                TargetExists(executable),
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

    public async Task<StartupActionResult> SetEnabledAsync(
        StartupEntry entry, bool enabled, CancellationToken ct)
    {
        switch (entry.Source)
        {
            case StartupSource.Winlogon:
                return new StartupActionResult(false,
                    "Userinit и Shell нельзя отключить: без них не запустится рабочий стол. " +
                    "Если здесь что-то незнакомое — это повод проверить систему на заражение, " +
                    "а не переключать флажок.");

            case StartupSource.ScheduledTask:
            {
                // schtasks decides by exit code, not by the message it prints, so this stays
                // correct on any Windows locale.
                var verb = enabled ? "/enable" : "/disable";
                var result = await processRunner.RunAsync(
                    "schtasks.exe", $"/change /tn \"{entry.SubKey}\" {verb}", ct);

                return new StartupActionResult(
                    result.Succeeded,
                    result.Succeeded
                        ? $"Задача «{entry.Name}» {(enabled ? "включена" : "отключена")}."
                        : $"schtasks вернул код {result.ExitCode}. Скорее всего не хватает прав администратора.");
            }

            default:
                SetApproved(entry, enabled);
                return new StartupActionResult(true,
                    $"«{entry.Name}» {(enabled ? "включено" : "отключено")}.");
        }
    }

    private void SetApproved(StartupEntry entry, bool enabled)
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
    public async Task<StartupActionResult> DeleteAsync(StartupEntry entry, CancellationToken ct)
    {
        switch (entry.Source)
        {
            case StartupSource.Winlogon:
                return new StartupActionResult(false,
                    "Userinit и Shell удалять нельзя: система перестанет загружать рабочий стол.");

            case StartupSource.ScheduledTask:
            {
                var result = await processRunner.RunAsync(
                    "schtasks.exe", $"/delete /tn \"{entry.SubKey}\" /f", ct);

                return new StartupActionResult(
                    result.Succeeded,
                    result.Succeeded
                        ? $"Задача «{entry.Name}» удалена."
                        : $"schtasks вернул код {result.ExitCode}. Часть системных задач защищена от удаления.");
            }

            case StartupSource.StartupFolder:
                return fileSystem.TryDeleteFile(entry.Command)
                    ? new StartupActionResult(true, $"Файл «{entry.Name}» удалён из папки автозагрузки.")
                    : new StartupActionResult(false, $"Не удалось удалить {entry.Name} — файл занят другим процессом.");

            default:
                registry.DeleteValue(entry.Hive, entry.SubKey, entry.Name);
                return new StartupActionResult(true, $"Запись «{entry.Name}» удалена из реестра.");
        }
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

    /// <summary>A bare name like "explorer.exe" is resolved by Windows through PATH, so treating it
    /// as a missing file would flag the shell itself as broken.</summary>
    private bool TargetExists(string? executablePath) =>
        executablePath is null
        || !Path.IsPathRooted(executablePath)
        || fileSystem.FileExists(executablePath);

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
