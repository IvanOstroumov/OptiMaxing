using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;
using OptiMaxing.Core.Optimizations.Catalog;
using OptiMaxing.Core.Testing;

namespace OptiMaxing.Tests;

public class RegistryOptimizationTests
{
    private static OptimizationContext NewContext(InMemoryBackupWriter backup) => new()
    {
        Backup = backup,
        Log = new Progress<string>(),
        Cancellation = CancellationToken.None,
    };

    [Fact]
    public async Task Apply_then_revert_restores_the_original_value()
    {
        var registry = new InMemoryRegistryProvider();
        registry.Seed(RegistryHive.CurrentUser, @"Software\Microsoft\GameBar",
            "AutoGameModeEnabled", 0, RegistryValueKind.DWord);

        var backup = new InMemoryBackupWriter();
        var tweak = new GameModeEnable(registry);

        await tweak.ApplyAsync(NewContext(backup));
        Assert.Equal(ApplyState.Applied, await tweak.GetStateAsync(CancellationToken.None));

        await tweak.RevertAsync(NewContext(backup));

        var restored = registry.GetValue(RegistryHive.CurrentUser,
            @"Software\Microsoft\GameBar", "AutoGameModeEnabled");
        Assert.Equal(0, restored);
        Assert.Equal(ApplyState.NotApplied, await tweak.GetStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Revert_removes_a_value_that_did_not_exist_before_apply()
    {
        // HwSchMode is genuinely absent on a stock Windows 11 install, so revert
        // must delete it rather than invent a "previous" value.
        var registry = new InMemoryRegistryProvider();
        var backup = new InMemoryBackupWriter();
        var tweak = new HagsEnable(registry);

        Assert.Equal(ApplyState.NotApplied, await tweak.GetStateAsync(CancellationToken.None));

        await tweak.ApplyAsync(NewContext(backup));
        Assert.Equal(2, registry.GetValue(RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode"));

        await tweak.RevertAsync(NewContext(backup));
        Assert.Null(registry.GetValue(RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode"));
    }

    [Fact]
    public async Task Mouse_tweak_writes_strings_not_dwords()
    {
        var registry = new InMemoryRegistryProvider();
        registry.Seed(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "1", RegistryValueKind.String);

        var tweak = new MousePrecisionDisable(registry);
        await tweak.ApplyAsync(NewContext(new InMemoryBackupWriter()));

        Assert.Equal(RegistryValueKind.String,
            registry.KindOf(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed"));
        Assert.Equal("0", registry.GetValue(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed"));
    }

    [Fact]
    public async Task A_third_party_value_reports_Modified_rather_than_NotApplied()
    {
        var registry = new InMemoryRegistryProvider();
        registry.Seed(RegistryHive.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 1, RegistryValueKind.DWord);

        var tweak = new HagsEnable(registry);
        Assert.Equal(ApplyState.Modified, await tweak.GetStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Partially_applied_multi_value_tweak_is_not_reported_as_applied()
    {
        var registry = new InMemoryRegistryProvider();
        registry.Seed(RegistryHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "0", RegistryValueKind.String);
        // MouseThreshold1 and MouseThreshold2 left absent.

        var tweak = new MousePrecisionDisable(registry);
        Assert.Equal(ApplyState.Modified, await tweak.GetStateAsync(CancellationToken.None));
    }
}
