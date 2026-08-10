using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Startup;

public enum StartupSource
{
    RegistryRun,
    RegistryRunOnce,
    StartupFolder,
}

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
    public string Id => $"{Source}|{Scope}|{(Is32BitView ? "32" : "64")}|{Name}";

    public string SourceText => Source switch
    {
        StartupSource.RegistryRun => Is32BitView ? "Реестр Run (32-бит)" : "Реестр Run",
        StartupSource.RegistryRunOnce => "Реестр RunOnce",
        StartupSource.StartupFolder => "Папка автозагрузки",
        _ => "Неизвестно",
    };

    public string ScopeText => Scope == StartupScope.CurrentUser ? "текущий пользователь" : "все пользователи";
}
