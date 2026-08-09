namespace OptiMaxing.Core.Abstractions;

public enum ServiceStartupType { Boot, System, Automatic, AutomaticDelayed, Manual, Disabled, Unknown }

public sealed record ServiceInfo(
    string Name,
    string DisplayName,
    ServiceStartupType StartupType,
    bool IsRunning);

/// <summary>
/// Services are only ever set to Disabled, never deleted, so that every change
/// stays reversible with a single click.
/// </summary>
public interface IServiceManager
{
    ServiceInfo? Get(string serviceName);
    IReadOnlyList<ServiceInfo> GetAll();
    void SetStartupType(string serviceName, ServiceStartupType startupType);
    void Stop(string serviceName);
    void Start(string serviceName);
}
