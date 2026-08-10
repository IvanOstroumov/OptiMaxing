using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Startup;

public enum StartupSource
{
    RegistryRun,
    RegistryRunOnce,
    StartupFolder,
    ScheduledTask,

    /// <summary>Userinit/Shell under Winlogon. Not something a normal program registers itself in —
    /// it is a classic malware persistence point, so the tab shows it even though nothing here is
    /// meant to be switched off.</summary>
    Winlogon,
}

/// <summary>Outcome of a toggle or delete. Carries a message because several sources refuse the
/// action outright, and "nothing happened" without a reason is the worst possible answer.</summary>
public sealed record StartupActionResult(bool Succeeded, string Message);

public enum StartupScope
{
    CurrentUser,
    AllUsers,
}

public sealed record StartupEntry(
    StartupSource Source,
    StartupScope Scope,
    string Name,
    string Command,
    string? ExecutablePath,
    bool IsEnabled,
    bool TargetExists,
    bool IsCritical,
    string Location,
    RegistryHive Hive,
    string SubKey,
    bool Is32BitView)
{
    /// <summary>Stable identity across rescans, so the UI can keep selection and the service can
    /// find the entry again without holding a reference to a stale snapshot.</summary>
    // SubKey is part of the identity because two scheduled tasks in different folders may share a
    // name, and a collision here would let the UI act on the wrong entry.
    public string Id => $"{Source}|{Scope}|{(Is32BitView ? "32" : "64")}|{SubKey}|{Name}";

    public string SourceText => Source switch
    {
        StartupSource.RegistryRun => Is32BitView ? "Реестр Run (32-бит)" : "Реестр Run",
        StartupSource.RegistryRunOnce => "Реестр RunOnce",
        StartupSource.StartupFolder => "Папка автозагрузки",
        StartupSource.ScheduledTask => "Планировщик заданий",
        StartupSource.Winlogon => "Winlogon",
        _ => "Неизвестно",
    };

    public string ScopeText => Scope == StartupScope.CurrentUser ? "текущий пользователь" : "все пользователи";
}
