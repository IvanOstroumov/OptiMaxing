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
        var service = new StartupInventoryService(registry, files, new NullRunner())
        {
            UserStartupFolder = @"C:\Users\Test\Startup",
            CommonStartupFolder = @"C:\ProgramData\Startup",
            ScheduledTasksFolder = TasksFolder,
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
    public async Task Disabling_writes_the_approval_flag_and_leaves_the_entry_itself_alone()
    {
        var (service, registry, _) = Build();
        registry.SetValue(RegistryHive.CurrentUser, RunKey, "Steam", @"C:\Steam\steam.exe", RegistryValueKind.String);

        await service.SetEnabledAsync(service.List()[0], false, CancellationToken.None);

        Assert.False(service.List()[0].IsEnabled);
        Assert.Equal(@"C:\Steam\steam.exe", registry.GetValue(RegistryHive.CurrentUser, RunKey, "Steam"));
    }

    [Fact]
    public async Task Deleting_removes_the_entry_itself()
    {
        var (service, registry, _) = Build();
        registry.SetValue(RegistryHive.CurrentUser, RunKey, "Steam", @"C:\Steam\steam.exe", RegistryValueKind.String);

        Assert.True((await service.DeleteAsync(service.List()[0], CancellationToken.None)).Succeeded);
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
    public async Task Startup_folder_files_are_listed_and_deletable()
    {
        var (service, _, files) = Build();
        files.SeedFile(@"C:\Users\Test\Startup", "notes.lnk", 100, DateTime.UtcNow);

        var entry = Assert.Single(service.List());
        Assert.Equal(StartupSource.StartupFolder, entry.Source);

        Assert.True((await service.DeleteAsync(entry, CancellationToken.None)).Succeeded);
        Assert.Empty(service.List());
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\App\\app.exe\" -silent", @"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\Program Files\App\app.exe -background", @"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\App\app.exe", @"C:\App\app.exe")]
    public void Executable_is_extracted_from_command_lines_containing_spaces(string command, string expected) =>
        Assert.Equal(expected, StartupInventoryService.ExtractExecutablePath(command));

    private const string TasksFolder = @"C:\Windows\System32\Tasks";

    private static string TaskXml(string trigger, string command, bool enabled = true) => $"""
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <Triggers><{trigger}><Enabled>true</Enabled></{trigger}></Triggers>
          <Settings><Enabled>{(enabled ? "true" : "false")}</Enabled></Settings>
          <Actions><Exec><Command>{command}</Command><Arguments>-silent</Arguments></Exec></Actions>
        </Task>
        """;

    [Fact]
    public void Only_tasks_that_run_at_logon_or_boot_show_up_in_autostart()
    {
        var (service, _, files) = Build();
        files.SeedText($@"{TasksFolder}\AtLogon", TaskXml("LogonTrigger", @"C:\App\a.exe"));
        files.SeedText($@"{TasksFolder}\AtBoot", TaskXml("BootTrigger", @"C:\App\b.exe"));
        // A monthly maintenance job is a scheduled task, but it is not autostart.
        files.SeedText($@"{TasksFolder}\Monthly", TaskXml("CalendarTrigger", @"C:\App\c.exe"));

        var names = service.List()
            .Where(e => e.Source == StartupSource.ScheduledTask)
            .Select(e => e.Name)
            .ToList();

        Assert.Equal(["AtBoot", "AtLogon"], names.Order());
    }

    [Fact]
    public void A_task_switched_off_in_its_xml_reads_as_disabled()
    {
        var (service, _, files) = Build();
        files.SeedText($@"{TasksFolder}\Off", TaskXml("LogonTrigger", @"C:\App\a.exe", enabled: false));

        Assert.False(Assert.Single(service.List()).IsEnabled);
    }

    [Fact]
    public void Tasks_in_subfolders_keep_their_full_path_as_identity()
    {
        var (service, _, files) = Build();
        files.SeedText($@"{TasksFolder}\Vendor\Update", TaskXml("LogonTrigger", @"C:\App\a.exe"));
        files.SeedText($@"{TasksFolder}\Other\Update", TaskXml("LogonTrigger", @"C:\App\b.exe"));

        var entries = service.List();

        // Same name, different folders: distinct ids, or the UI would act on the wrong one.
        Assert.Equal(2, entries.Select(e => e.Id).Distinct().Count());
        Assert.Contains(entries, e => e.SubKey == @"\Vendor\Update");
    }

    [Fact]
    public void Unreadable_or_broken_task_files_are_skipped_rather_than_aborting_the_scan()
    {
        var (service, _, files) = Build();
        files.SeedText($@"{TasksFolder}\Broken", "this is not xml at all");
        files.SeedText($@"{TasksFolder}\Good", TaskXml("LogonTrigger", @"C:\App\a.exe"));

        Assert.Equal("Good", Assert.Single(service.List()).Name);
    }

    [Fact]
    public async Task Winlogon_entries_are_shown_but_refuse_to_be_switched_off()
    {
        var (service, registry, _) = Build();
        registry.SetValue(RegistryHive.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon", "Userinit",
            @"C:\Windows\system32\userinit.exe,", RegistryValueKind.String);

        var entry = Assert.Single(service.List());
        Assert.Equal(StartupSource.Winlogon, entry.Source);
        // The trailing comma is part of the stored format, not part of the path.
        Assert.Equal(@"C:\Windows\system32\userinit.exe", entry.ExecutablePath);

        var result = await service.SetEnabledAsync(entry, false, CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.False((await service.DeleteAsync(entry, CancellationToken.None)).Succeeded);
    }

    private sealed class NullRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct) =>
            Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
    }
}
