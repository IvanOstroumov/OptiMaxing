using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Testing;

public sealed class InMemoryServiceManager : IServiceManager
{
    private readonly Dictionary<string, ServiceInfo> _services = [];

    public void Seed(string name, ServiceStartupType startupType, bool isRunning = true) =>
        _services[name] = new ServiceInfo(name, name, startupType, isRunning);

    public ServiceInfo? Get(string serviceName) =>
        _services.TryGetValue(serviceName, out var info) ? info : null;

    public IReadOnlyList<ServiceInfo> GetAll() => _services.Values.ToList();

    public void SetStartupType(string serviceName, ServiceStartupType startupType)
    {
        var existing = _services[serviceName];
        _services[serviceName] = existing with { StartupType = startupType };
    }

    public void Stop(string serviceName) =>
        _services[serviceName] = _services[serviceName] with { IsRunning = false };

    public void Start(string serviceName) =>
        _services[serviceName] = _services[serviceName] with { IsRunning = true };
}
