using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;
using OptiMaxing.Core.Optimizations.Catalog;
using OptiMaxing.Core.Testing;

namespace OptiMaxing.Tests;

public class StartupProgramDisableTests
{
    [Fact]
    public async Task Disable_then_revert_restores_the_run_entry()
    {
        var registry = new InMemoryRegistryProvider();
        const string subKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        registry.Seed(RegistryHive.CurrentUser, subKey, "SomeApp", @"C:\Program Files\SomeApp\app.exe", RegistryValueKind.String);

        var tweak = new StartupProgramDisable(registry, RegistryHive.CurrentUser, subKey, "SomeApp", @"C:\Program Files\SomeApp\app.exe");
        var backup = new InMemoryBackupWriter();
        var context = new OptimizationContext { Backup = backup, Log = new Progress<string>(), Cancellation = default };

        Assert.Equal(ApplyState.NotApplied, await tweak.GetStateAsync(CancellationToken.None));

        await tweak.ApplyAsync(context);
        Assert.Null(registry.GetValue(RegistryHive.CurrentUser, subKey, "SomeApp"));
        Assert.Equal(ApplyState.Applied, await tweak.GetStateAsync(CancellationToken.None));

        await tweak.RevertAsync(context);
        Assert.Equal(@"C:\Program Files\SomeApp\app.exe", registry.GetValue(RegistryHive.CurrentUser, subKey, "SomeApp"));
        Assert.Equal(ApplyState.NotApplied, await tweak.GetStateAsync(CancellationToken.None));
    }
}
