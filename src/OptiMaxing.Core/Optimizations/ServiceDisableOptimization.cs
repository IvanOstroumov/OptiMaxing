using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations;

/// <summary>
/// Base for tweaks that disable a Windows service. Per the safety contract, this
/// only ever sets StartupType, never deletes the service — a single click always
/// gets it back.
/// </summary>
public abstract class ServiceDisableOptimization(IServiceManager services) : IOptimization
{
    public abstract string Id { get; }
    public abstract string DisplayName { get; }
    public abstract string Description { get; }
    public virtual string? TradeOff => null;
    public abstract RiskLevel Risk { get; }
    public Reversibility Reversibility => Reversibility.Reversible;
    public abstract string Category { get; }
    public virtual bool RequiresRestart => false;

    protected abstract string ServiceName { get; }

    public Task<ApplyState> GetStateAsync(CancellationToken ct)
    {
        var info = services.Get(ServiceName);
        if (info is null)
            return Task.FromResult(ApplyState.NotApplicable);

        return Task.FromResult(info.StartupType == ServiceStartupType.Disabled
            ? ApplyState.Applied
            : ApplyState.NotApplied);
    }

    public Task ApplyAsync(OptimizationContext context)
    {
        var info = services.Get(ServiceName)
            ?? throw new InvalidOperationException($"Служба '{ServiceName}' не установлена на этой машине.");

        context.Backup.Capture(Id, ServiceName, info.StartupType.ToString());
        services.SetStartupType(ServiceName, ServiceStartupType.Disabled);
        context.Log.Report($"    служба {ServiceName}: {info.StartupType} -> Disabled");

        return Task.CompletedTask;
    }

    public Task RevertAsync(OptimizationContext context)
    {
        var previous = context.Backup.Read(Id, ServiceName);
        if (previous is null || !Enum.TryParse<ServiceStartupType>(previous, out var startupType))
        {
            context.Log.Report("    нет бэкапа, оставляю Manual как безопасный дефолт");
            startupType = ServiceStartupType.Manual;
        }

        services.SetStartupType(ServiceName, startupType);
        context.Log.Report($"    служба {ServiceName} восстановлена в {startupType}");

        return Task.CompletedTask;
    }
}
