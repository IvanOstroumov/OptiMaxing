namespace OptiMaxing.Core.Programs;

public sealed record InstalledProgram(
    string Name,
    string? Publisher,
    string? Version,
    DateTime? InstallDate,
    long? EstimatedSizeBytes,
    string? UninstallCommand,
    string? QuietUninstallCommand,
    string? InstallLocation,
    bool IsSystemComponent,
    string RegistryKey,
    bool Is32Bit,
    bool IsPerUser)
{
    public string Id => $"{(IsPerUser ? "user" : "machine")}|{(Is32Bit ? "32" : "64")}|{RegistryKey}";

    /// <summary>True when we have any way to launch an uninstaller. Windows Store apps and some
    /// driver bundles have no uninstall string at all, and pretending otherwise would give the user
    /// a button that silently does nothing.</summary>
    public bool CanUninstall => !string.IsNullOrWhiteSpace(QuietUninstallCommand)
                                || !string.IsNullOrWhiteSpace(UninstallCommand);

    public string SizeText => EstimatedSizeBytes is { } bytes and > 0
        ? bytes >= 1024L * 1024 * 1024
            ? $"{bytes / 1024.0 / 1024.0 / 1024.0:N1} ГБ"
            : $"{bytes / 1024.0 / 1024.0:N0} МБ"
        : "—";

    public string ScopeText => IsPerUser ? "текущий пользователь" : "все пользователи";
}
