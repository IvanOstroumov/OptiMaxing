using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;
using OptiMaxing.Core.Optimizations.Catalog;
using OptiMaxing.Core.Testing;

namespace OptiMaxing.Tests;

public class ServiceOptimizationTests
{
    private static OptimizationContext NewContext(InMemoryBackupWriter backup) => new()
    {
        Backup = backup,
        Log = new Progress<string>(),
        Cancellation = CancellationToken.None,
    };

    [Fact]
    public async Task Disable_then_revert_restores_the_original_startup_type()
    {
        var services = new InMemoryServiceManager();
        services.Seed("SysMain", ServiceStartupType.Automatic);

        var tweak = new SysMainDisable(services);
        var backup = new InMemoryBackupWriter();

        Assert.Equal(ApplyState.NotApplied, await tweak.GetStateAsync(CancellationToken.None));

        await tweak.ApplyAsync(NewContext(backup));
        Assert.Equal(ServiceStartupType.Disabled, services.Get("SysMain")!.StartupType);
        Assert.Equal(ApplyState.Applied, await tweak.GetStateAsync(CancellationToken.None));

        await tweak.RevertAsync(NewContext(backup));
        Assert.Equal(ServiceStartupType.Automatic, services.Get("SysMain")!.StartupType);
    }

    [Fact]
    public async Task Missing_service_reports_NotApplicable_instead_of_crashing()
    {
        // A machine may lack a service entirely (e.g. Print Spooler stripped by
        // an unattended install image) — the tweak must degrade gracefully.
        var services = new InMemoryServiceManager();
        var tweak = new PrintSpoolerDisable(services);

        Assert.Equal(ApplyState.NotApplicable, await tweak.GetStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Service_disable_never_deletes_only_sets_startup_type()
    {
        var services = new InMemoryServiceManager();
        services.Seed("WSearch", ServiceStartupType.Automatic);

        var tweak = new WindowsSearchDisable(services);
        await tweak.ApplyAsync(NewContext(new InMemoryBackupWriter()));

        Assert.NotNull(services.Get("WSearch")); // still exists, just disabled
        Assert.Equal(ServiceStartupType.Disabled, services.Get("WSearch")!.StartupType);
    }
}
