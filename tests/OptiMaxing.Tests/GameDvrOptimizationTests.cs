using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;
using OptiMaxing.Core.Optimizations.Catalog;
using OptiMaxing.Core.Testing;

namespace OptiMaxing.Tests;

public class GameDvrOptimizationTests
{
    [Fact]
    public async Task Reports_NotApplied_for_the_actual_stock_default_of_1_not_just_absence()
    {
        // Verified on the target machine: GameDVR_Enabled defaults to 1 (enabled),
        // it is not absent. A tweak that only recognises "absent" as NotApplied
        // would misreport a stock install as Modified.
        var registry = new InMemoryRegistryProvider();
        registry.Seed(RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 1, RegistryValueKind.DWord);
        registry.Seed(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 1, RegistryValueKind.DWord);

        var tweak = new GameDvrDisable(registry);
        Assert.Equal(ApplyState.NotApplied, await tweak.GetStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Apply_then_revert_restores_stock_enabled_state()
    {
        var registry = new InMemoryRegistryProvider();
        registry.Seed(RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 1, RegistryValueKind.DWord);
        registry.Seed(RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 1, RegistryValueKind.DWord);

        var tweak = new GameDvrDisable(registry);
        var backup = new InMemoryBackupWriter();
        var context = new OptimizationContext { Backup = backup, Log = new Progress<string>(), Cancellation = default };

        await tweak.ApplyAsync(context);
        Assert.Equal(ApplyState.Applied, await tweak.GetStateAsync(CancellationToken.None));

        await tweak.RevertAsync(context);
        Assert.Equal(1, registry.GetValue(RegistryHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled"));
        Assert.Equal(ApplyState.NotApplied, await tweak.GetStateAsync(CancellationToken.None));
    }
}
