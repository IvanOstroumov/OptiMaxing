using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Testing;

public sealed class InMemorySecurityStatusProvider : ISecurityStatusProvider
{
    public SecurityStatus Status { get; set; } = new(
        ProtectionState.On, "Windows Defender",
        ProtectionState.On,
        ProtectionState.On,
        IsAdministrator: false,
        ProtectionState.On,
        ProtectionState.On);

    public SecurityStatus Collect() => Status;
}
