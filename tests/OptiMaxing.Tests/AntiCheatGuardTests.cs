using OptiMaxing.Core.Safety;

namespace OptiMaxing.Tests;

public class AntiCheatGuardTests
{
    [Fact]
    public void Detects_a_known_game_by_executable_name_in_a_library_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "OptiMaxingTests_" + Guid.NewGuid());
        var gameDir = Path.Combine(root, "VALORANT", "live");
        Directory.CreateDirectory(gameDir);
        File.WriteAllText(Path.Combine(gameDir, "VALORANT-Win64-Shipping.exe"), "stub");

        try
        {
            var found = AntiCheatGuard.DetectInstalled([root]);
            Assert.Contains(found, g => g.DisplayName.Contains("VALORANT"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Reports_nothing_when_library_root_does_not_exist()
    {
        var missing = Path.Combine(Path.GetTempPath(), "OptiMaxingTests_does_not_exist_" + Guid.NewGuid());
        var found = AntiCheatGuard.DetectInstalled([missing]);
        Assert.Empty(found);
    }
}
