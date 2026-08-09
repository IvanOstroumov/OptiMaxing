using System.Management;
using Microsoft.Win32;
using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Safety;

public sealed record RestorePointStatus(
    bool ProtectionEnabled,
    bool BlockedByPolicy,
    DateTimeOffset? LastRestorePointUtc)
{
    public TimeSpan? Age => LastRestorePointUtc is { } t ? DateTimeOffset.UtcNow - t : null;
}

public interface IRestorePointService
{
    RestorePointStatus GetStatus();
    Task<bool> CreateAsync(string description, CancellationToken ct);
    Task<bool> EnableProtectionAsync(string driveLetter, CancellationToken ct);
}

public sealed class RestorePointService(IProcessRunner processRunner, IAppLogger logger) : IRestorePointService
{
    private const string PolicyKey = @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore";

    public RestorePointStatus GetStatus()
    {
        var blockedByPolicy = ReadPolicyDword("DisableSR") == 1;

        DateTimeOffset? last = null;
        var protectionEnabled = false;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"\\.\root\default",
                "SELECT CreationTime FROM SystemRestore");

            foreach (var item in searcher.Get().Cast<ManagementObject>())
            {
                using (item)
                {
                    protectionEnabled = true;
                    if (item["CreationTime"] is string raw &&
                        ManagementDateTimeConverter.ToDateTime(raw) is { } dt)
                    {
                        var utc = new DateTimeOffset(dt.ToUniversalTime(), TimeSpan.Zero);
                        if (last is null || utc > last)
                            last = utc;
                    }
                }
            }
        }
        catch (ManagementException ex)
        {
            // Querying restore points fails outright when System Protection is off.
            logger.Write(LogLevel.Warning, "Unable to enumerate restore points.", ex);
        }

        return new RestorePointStatus(protectionEnabled, blockedByPolicy, last);
    }

    public async Task<bool> CreateAsync(string description, CancellationToken ct)
    {
        // Windows silently refuses a second restore point within 24 hours. We
        // respect that limit rather than editing SystemRestorePointCreationFrequency,
        // so the app never weakens a system-wide safety setting behind the user's back.
        var script =
            $"Checkpoint-Computer -Description '{description.Replace("'", "''")}' " +
            "-RestorePointType 'MODIFY_SETTINGS'";

        var result = await processRunner.RunAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script}\"",
            ct);

        if (!result.Succeeded)
            logger.Write(LogLevel.Error, $"Checkpoint-Computer failed: {result.StandardError}");

        return result.Succeeded;
    }

    public async Task<bool> EnableProtectionAsync(string driveLetter, CancellationToken ct)
    {
        var result = await processRunner.RunAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Enable-ComputerRestore -Drive '{driveLetter}\\'\"",
            ct);

        if (!result.Succeeded)
            logger.Write(LogLevel.Error, $"Enable-ComputerRestore failed: {result.StandardError}");

        return result.Succeeded;
    }

    private static int? ReadPolicyDword(string valueName)
    {
        using var key = RegistryKey
            .OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey(PolicyKey, writable: false);
        return key?.GetValue(valueName) as int?;
    }
}
