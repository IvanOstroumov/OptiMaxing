using System.Management;
using System.Security.Principal;
using Microsoft.Win32;
using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Platform;

/// <summary>Reads the same facts the "Безопасность Windows" app shows, from the sources it
/// actually uses under the hood rather than by shelling out and parsing localized text.</summary>
public sealed class WindowsSecurityStatusProvider : ISecurityStatusProvider
{
    public SecurityStatus Collect() => new(
        CollectAntivirusState(out var name),
        name,
        CollectFirewallState(),
        CollectUacState(),
        IsAdministrator(),
        CollectSecureBootState(),
        CollectBitLockerState());

    /// <summary>root\SecurityCenter2 is what Windows Security itself reads from; querying it
    /// beats shelling out to a tool whose output text changes with the display language.</summary>
    private static ProtectionState CollectAntivirusState(out string? name)
    {
        name = null;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\SecurityCenter2", "SELECT displayName, productState FROM AntiVirusProduct");

            ProtectionState? best = null;
            foreach (ManagementObject product in searcher.Get())
            {
                name ??= product["displayName"]?.ToString();

                // productState packs three bytes; the middle byte's low nibble is 0x1 when the
                // product is enabled. This bit layout is undocumented but has been stable since
                // Vista and is what every third-party monitor of this WMI class relies on.
                if (product["productState"] is not uint state)
                {
                    continue;
                }

                var enabled = (state & 0x1000) != 0 || (state & 0x0010) != 0;
                best = enabled ? ProtectionState.On : (best ?? ProtectionState.Off);
            }

            return best ?? ProtectionState.Off;
        }
        catch (ManagementException)
        {
            return ProtectionState.Unknown;
        }
    }

    private static ProtectionState CollectFirewallState()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\SecurityCenter2", "SELECT displayName, productState FROM FirewallProduct");

            foreach (ManagementObject product in searcher.Get())
            {
                if (product["productState"] is uint state && ((state & 0x1000) != 0 || (state & 0x0010) != 0))
                {
                    return ProtectionState.On;
                }
            }

            return ProtectionState.Off;
        }
        catch (ManagementException)
        {
            return ProtectionState.Unknown;
        }
    }

    private static ProtectionState CollectUacState()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                "EnableLUA", null);

            return value switch
            {
                int i => i != 0 ? ProtectionState.On : ProtectionState.Off,
                _ => ProtectionState.Unknown,
            };
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or System.IO.IOException)
        {
            return ProtectionState.Unknown;
        }
    }

    private static ProtectionState CollectSecureBootState()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecureBoot\State",
                "UEFISecureBootEnabled", null);

            // Absent on legacy BIOS/CSM systems — that is a real "off", not an unknown.
            return value switch
            {
                int i => i != 0 ? ProtectionState.On : ProtectionState.Off,
                null => ProtectionState.Off,
                _ => ProtectionState.Unknown,
            };
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or System.IO.IOException)
        {
            return ProtectionState.Unknown;
        }
    }

    /// <summary>The BitLocker WMI namespace requires elevation even to open, so this is expected
    /// to read Unknown when this code runs unelevated (e.g. under the diagnostics console).</summary>
    private static ProtectionState CollectBitLockerState()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\cimv2\Security\MicrosoftVolumeEncryption",
                "SELECT ProtectionStatus FROM Win32_EncryptableVolume WHERE DriveLetter = 'C:'");

            foreach (ManagementObject volume in searcher.Get())
            {
                if (volume["ProtectionStatus"] is uint status)
                {
                    return status == 1 ? ProtectionState.On : ProtectionState.Off;
                }
            }

            return ProtectionState.Unknown;
        }
        catch (ManagementException)
        {
            return ProtectionState.Unknown;
        }
        catch (UnauthorizedAccessException)
        {
            return ProtectionState.Unknown;
        }
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
