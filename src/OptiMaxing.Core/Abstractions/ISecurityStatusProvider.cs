namespace OptiMaxing.Core.Abstractions;

public enum ProtectionState { On, Off, Unknown }

/// <summary>What every competing "system health" screen leads with and OptiMaxing's did not have
/// at all: is the machine actually protected. None of this is a tweak — it is read-only context,
/// same as the rest of the health report.</summary>
public sealed record SecurityStatus(
    ProtectionState Antivirus,
    string? AntivirusName,
    ProtectionState Firewall,
    ProtectionState UacEnabled,
    bool IsAdministrator,
    ProtectionState SecureBoot,
    ProtectionState BitLockerSystemDrive);

public interface ISecurityStatusProvider
{
    SecurityStatus Collect();
}
