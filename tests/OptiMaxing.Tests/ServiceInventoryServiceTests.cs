using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Services;
using OptiMaxing.Core.Testing;
using Xunit;

namespace OptiMaxing.Tests;

public class ServiceInventoryServiceTests
{
    [Fact]
    public void A_service_on_the_known_list_is_marked_critical()
    {
        var manager = new InMemoryServiceManager();
        manager.Seed("RpcSs", ServiceStartupType.Automatic);

        Assert.Equal(ServiceSafety.Critical, Assert.Single(new ServiceInventoryService(manager).List()).Safety);
    }

    [Theory]
    [InlineData(ServiceStartupType.Boot)]
    [InlineData(ServiceStartupType.System)]
    public void A_boot_or_system_service_is_critical_even_if_we_have_never_heard_of_it(ServiceStartupType type)
    {
        var manager = new InMemoryServiceManager();
        manager.Seed("SomeVendorDriver", type);

        Assert.Equal(ServiceSafety.Critical, Assert.Single(new ServiceInventoryService(manager).List()).Safety);
    }

    [Fact]
    public void A_service_recommended_for_disabling_carries_the_reason_why()
    {
        var manager = new InMemoryServiceManager();
        manager.Seed("DiagTrack", ServiceStartupType.Automatic);

        var row = Assert.Single(new ServiceInventoryService(manager).List());
        Assert.Equal(ServiceSafety.SafeToDisable, row.Safety);
        Assert.NotNull(row.Note);
    }

    [Fact]
    public void An_unrecognised_service_is_neither_endorsed_nor_condemned()
    {
        var manager = new InMemoryServiceManager();
        manager.Seed("SteamHelper", ServiceStartupType.Manual);

        var row = Assert.Single(new ServiceInventoryService(manager).List());
        Assert.Equal(ServiceSafety.Unknown, row.Safety);
        Assert.Null(row.Note);
    }

    [Fact]
    public void Disabling_changes_the_startup_type_and_leaves_the_service_installed()
    {
        var manager = new InMemoryServiceManager();
        manager.Seed("DiagTrack", ServiceStartupType.Automatic);
        var service = new ServiceInventoryService(manager);

        Assert.True(service.SetStartupType(service.List()[0], ServiceStartupType.Disabled).Succeeded);

        var info = Assert.Single(manager.GetAll());
        Assert.Equal(ServiceStartupType.Disabled, info.StartupType);
    }

    [Fact]
    public void Stopping_a_service_that_refuses_reports_the_failure_instead_of_throwing()
    {
        var manager = new ThrowingServiceManager();
        var service = new ServiceInventoryService(manager);
        var row = service.List()[0];

        var result = service.SetRunning(row, running: false);

        Assert.False(result.Succeeded);
        Assert.Contains("отказано", result.Message);
    }

    private sealed class ThrowingServiceManager : IServiceManager
    {
        public ServiceInfo? Get(string serviceName) => GetAll()[0];

        public IReadOnlyList<ServiceInfo> GetAll() =>
            [new ServiceInfo("Stubborn", "Stubborn", ServiceStartupType.Automatic, true)];

        public void SetStartupType(string serviceName, ServiceStartupType startupType) =>
            throw new UnauthorizedAccessException("в доступе отказано");

        public void Stop(string serviceName) => throw new InvalidOperationException("в доступе отказано");

        public void Start(string serviceName) => throw new InvalidOperationException("в доступе отказано");
    }
}
