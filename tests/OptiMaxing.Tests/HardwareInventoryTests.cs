using OptiMaxing.Core.Abstractions;
using Xunit;

namespace OptiMaxing.Tests;

public class HardwareInventoryTests
{
    private static MemoryModuleInventory Module(int configured, int rated) =>
        new("BANK 0", "DIMM 0", 16UL * 1024 * 1024 * 1024, configured, rated, "Kingston", "KF3200");

    [Fact]
    public void Module_at_its_rated_speed_is_not_flagged() =>
        Assert.False(Module(3200, 3200).RunningBelowRatedSpeed);

    [Fact]
    public void Module_below_its_rated_speed_is_flagged() =>
        Assert.True(Module(2133, 3200).RunningBelowRatedSpeed);

    [Fact]
    public void Missing_speed_data_is_not_reported_as_a_problem()
    {
        Assert.False(Module(0, 3200).RunningBelowRatedSpeed);
        Assert.False(Module(3200, 0).RunningBelowRatedSpeed);
    }
}
