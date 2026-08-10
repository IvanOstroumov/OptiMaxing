using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Programs;
using OptiMaxing.Core.Testing;
using Xunit;

namespace OptiMaxing.Tests;

public class InstalledProgramsServiceTests
{
    private const string Uninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string Uninstall32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    private static (InstalledProgramsService Service, InMemoryRegistryProvider Registry, RecordingRunner Runner) Build()
    {
        var registry = new InMemoryRegistryProvider();
        var runner = new RecordingRunner();
        return (new InstalledProgramsService(registry, runner), registry, runner);
    }

    private static void SeedProgram(
        InMemoryRegistryProvider registry, RegistryHive hive, string root, string key, string name)
    {
        registry.Seed(hive, $@"{root}\{key}", "DisplayName", name, RegistryValueKind.String);
        registry.Seed(hive, $@"{root}\{key}", "UninstallString", @"C:\App\unins.exe", RegistryValueKind.String);
    }

    [Fact]
    public void Programs_are_read_from_both_registry_views()
    {
        var (service, registry, _) = Build();
        SeedProgram(registry, RegistryHive.LocalMachine, Uninstall, "Alpha", "Alpha");
        SeedProgram(registry, RegistryHive.LocalMachine, Uninstall32, "Beta", "Beta");

        Assert.Equal(["Alpha", "Beta"], service.List().Select(p => p.Name));
    }

    [Fact]
    public void A_product_registered_in_both_views_is_listed_once()
    {
        var (service, registry, _) = Build();
        SeedProgram(registry, RegistryHive.LocalMachine, Uninstall, "Steam", "Steam");
        SeedProgram(registry, RegistryHive.LocalMachine, Uninstall32, "Steam", "Steam");

        Assert.Single(service.List());
    }

    [Fact]
    public void An_entry_without_a_display_name_is_an_update_record_and_is_skipped()
    {
        var (service, registry, _) = Build();
        registry.Seed(RegistryHive.LocalMachine, $@"{Uninstall}\KB5001234", "UninstallString",
            @"C:\Windows\patch.exe", RegistryValueKind.String);

        Assert.Empty(service.List());
    }

    [Fact]
    public void Size_and_install_date_are_decoded_from_their_registry_encodings()
    {
        var (service, registry, _) = Build();
        SeedProgram(registry, RegistryHive.LocalMachine, Uninstall, "Alpha", "Alpha");
        registry.Seed(RegistryHive.LocalMachine, $@"{Uninstall}\Alpha", "EstimatedSize", 2048, RegistryValueKind.DWord);
        registry.Seed(RegistryHive.LocalMachine, $@"{Uninstall}\Alpha", "InstallDate", "20260115", RegistryValueKind.String);

        var program = Assert.Single(service.List());
        Assert.Equal(2048L * 1024, program.EstimatedSizeBytes);
        Assert.Equal(new DateTime(2026, 1, 15), program.InstallDate);
    }

    [Fact]
    public void A_malformed_install_date_is_dropped_rather_than_throwing()
    {
        var (service, registry, _) = Build();
        SeedProgram(registry, RegistryHive.LocalMachine, Uninstall, "Alpha", "Alpha");
        registry.Seed(RegistryHive.LocalMachine, $@"{Uninstall}\Alpha", "InstallDate", "не дата", RegistryValueKind.String);

        Assert.Null(Assert.Single(service.List()).InstallDate);
    }

    [Fact]
    public async Task A_program_with_no_uninstaller_reports_that_instead_of_running_nothing()
    {
        var (service, registry, runner) = Build();
        registry.Seed(RegistryHive.LocalMachine, $@"{Uninstall}\Store", "DisplayName", "Store App", RegistryValueKind.String);

        var program = Assert.Single(service.List());
        Assert.False(program.CanUninstall);

        var result = await service.UninstallAsync(program, quiet: false, CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task The_quiet_uninstall_string_is_only_used_when_asked_for_and_present()
    {
        var (service, registry, runner) = Build();
        SeedProgram(registry, RegistryHive.LocalMachine, Uninstall, "Alpha", "Alpha");
        registry.Seed(RegistryHive.LocalMachine, $@"{Uninstall}\Alpha", "QuietUninstallString",
            @"C:\App\unins.exe /S", RegistryValueKind.String);

        var program = Assert.Single(service.List());

        await service.UninstallAsync(program, quiet: false, CancellationToken.None);
        await service.UninstallAsync(program, quiet: true, CancellationToken.None);

        Assert.Equal([(@"C:\App\unins.exe", ""), (@"C:\App\unins.exe", "/S")], runner.Calls);
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\App\\unins.exe\" /uninstall", @"C:\Program Files\App\unins.exe", "/uninstall")]
    [InlineData(@"C:\Program Files\App\unins.exe /S", @"C:\Program Files\App\unins.exe", "/S")]
    [InlineData(@"C:\App\unins.exe", @"C:\App\unins.exe", "")]
    [InlineData("MsiExec.exe /X{1234-5678}", "MsiExec.exe", "/X{1234-5678}")]
    public void Uninstall_strings_split_correctly_even_when_the_path_contains_spaces(
        string command, string expectedFile, string expectedArgs) =>
        Assert.Equal((expectedFile, expectedArgs), InstalledProgramsService.SplitCommand(command));

    private sealed class RecordingRunner : IProcessRunner
    {
        public List<(string FileName, string Arguments)> Calls { get; } = [];

        public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct)
        {
            Calls.Add((fileName, arguments));
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }
    }
}
