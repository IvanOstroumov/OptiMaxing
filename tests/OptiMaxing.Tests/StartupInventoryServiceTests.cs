using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Startup;
using OptiMaxing.Core.Testing;
using Xunit;

namespace OptiMaxing.Tests;

public class StartupInventoryServiceTests
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRunKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private static (StartupInventoryService Service, InMemoryRegistryProvider Registry, InMemoryFileSystem Files) Build()
    {
        var registry = new InMemoryRegistryProvider();
        var files = new InMemoryFileSystem();
        var service = new StartupInventoryService(registry, files)
        {
            UserStartupFolder = @"C:\Users\Test\Startup",
            CommonStartupFolder = @"C:\ProgramData\Startup",
        };

        return (service, registry, files);
    }

    [Fact]
    public void An_entry_with_no_approval_record_counts_as_enabled()
    {
        var (service, registry, _) = Build();
        registry.SetValue(RegistryHive.CurrentUser, RunKey, "Steam", @"C:\Steam\steam.exe", RegistryValueKind.String);

        var entry = Assert.Single(service.List());
        Assert.True(entry.IsEnabled);
    }

    [Theory]
    [InlineData(0x02, true)]
    [InlineData(0x06, true)]
    [InlineData(0x03, false)]
    [InlineData(0x07, false)]
    public void Approval_flag_low_bit_decides_enabled_state(byte flag, bool expectedEnabled)
    {
        var (service, registry, _) = Build();
        registry.SetValue(RegistryHive.CurrentUser, RunKey, "Steam", @"C:\Steam\steam.exe", RegistryValueKind.String);
        registry.SetValue(RegistryHive.CurrentUser, ApprovedRunKey, "Steam", new byte[12] { flag, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);

        Assert.Equal(expectedEnabled, Assert.Single(service.List()).IsEnabled);
    }

    [Fact]
    public void Disabling_writes_the_approval_flag_and_leaves_the_entry_itself_alone()
    {
        var (service, registry, _) = Build();
        registry.SetValue(RegistryHive.CurrentUser, RunKey, "Steam", @"C:\Steam\steam.exe", RegistryValueKind.String);

        service.SetEnabled(service.List()[0], false);

        Assert.False(service.List()[0].IsEnabled);
        Assert.Equal(@"C:\Steam\steam.exe", registry.GetValue(RegistryHive.CurrentUser, RunKey, "Steam"));
    }

    [Fact]
    public void Deleting_removes_the_entry_itself()
    {
        var (service, registry, _) = Build();
        registry.SetValue(RegistryHive.CurrentUser, RunKey, "Steam", @"C:\Steam\steam.exe", RegistryValueKind.String);

        Assert.True(service.Delete(service.List()[0]));
        Assert.Empty(service.List());
    }

    [Fact]
    public void An_entry_pointing_at_a_file_that_no_longer_exists_is_flagged()
    {
        var (service, registry, files) = Build();
        registry.SetValue(RegistryHive.CurrentUser, RunKey, "Ghost", @"C:\Gone\ghost.exe", RegistryValueKind.String);
        registry.SetValue(RegistryHive.CurrentUser, RunKey, "Real", @"C:\App\real.exe", RegistryValueKind.String);
        files.SeedFile(@"C:\App", "real.exe", 100, DateTime.UtcNow);

        var entries = service.List();
        Assert.False(entries.Single(e => e.Name == "Ghost").TargetExists);
        Assert.True(entries.Single(e => e.Name == "Real").TargetExists);
    }

    [Fact]
    public void Startup_folder_files_are_listed_and_deletable()
    {
        var (service, _, files) = Build();
        files.SeedFile(@"C:\Users\Test\Startup", "notes.lnk", 100, DateTime.UtcNow);

        var entry = Assert.Single(service.List());
        Assert.Equal(StartupSource.StartupFolder, entry.Source);

        Assert.True(service.Delete(entry));
        Assert.Empty(service.List());
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\App\\app.exe\" -silent", @"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\Program Files\App\app.exe -background", @"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\App\app.exe", @"C:\App\app.exe")]
    public void Executable_is_extracted_from_command_lines_containing_spaces(string command, string expected) =>
        Assert.Equal(expected, StartupInventoryService.ExtractExecutablePath(command));
}
