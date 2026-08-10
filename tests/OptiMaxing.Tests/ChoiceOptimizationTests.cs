using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;
using OptiMaxing.Core.Optimizations;
using OptiMaxing.Core.Optimizations.Catalog;
using OptiMaxing.Core.Testing;
using Xunit;

namespace OptiMaxing.Tests;

public class ChoiceOptimizationTests
{
    private const string PriorityKey = @"SYSTEM\CurrentControlSet\Control\PriorityControl";

    private static (Win32PrioritySeparationChoice Tweak, InMemoryRegistryProvider Registry) Build()
    {
        var registry = new InMemoryRegistryProvider();
        return (new Win32PrioritySeparationChoice(registry), registry);
    }

    private static OptimizationContext Context(InMemoryBackupWriter backup) => new()
    {
        Backup = backup,
        Log = new Progress<string>(_ => { }),
    };

    [Fact]
    public async Task An_absent_value_reads_as_the_windows_default_choice()
    {
        var (tweak, _) = Build();

        var current = await tweak.GetCurrentChoiceAsync(CancellationToken.None);

        Assert.NotNull(current);
        Assert.True(current.IsWindowsDefault);
    }

    [Fact]
    public async Task A_value_no_choice_describes_is_reported_as_modified_not_relabelled()
    {
        var (tweak, registry) = Build();
        registry.Seed(RegistryHive.LocalMachine, PriorityKey, "Win32PrioritySeparation", 0x15,
            RegistryValueKind.DWord);

        Assert.Null(await tweak.GetCurrentChoiceAsync(CancellationToken.None));
        Assert.Equal(ApplyState.Modified, await tweak.GetStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task State_is_measured_against_the_selected_choice_not_a_fixed_target()
    {
        var (tweak, registry) = Build();
        registry.Seed(RegistryHive.LocalMachine, PriorityKey, "Win32PrioritySeparation", 0x02,
            RegistryValueKind.DWord);

        tweak.SelectedChoiceId = "balanced-2";
        Assert.Equal(ApplyState.Applied, await tweak.GetStateAsync(CancellationToken.None));

        tweak.SelectedChoiceId = "gaming-2a";
        Assert.Equal(ApplyState.NotApplied, await tweak.GetStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Applying_writes_the_value_of_the_selected_choice()
    {
        var (tweak, registry) = Build();
        tweak.SelectedChoiceId = "gaming-2a";

        await tweak.ApplyAsync(Context(new InMemoryBackupWriter()));

        Assert.Equal(0x2A, registry.GetValue(RegistryHive.LocalMachine, PriorityKey, "Win32PrioritySeparation"));
    }

    [Fact]
    public async Task Reverting_a_value_that_did_not_exist_removes_it_again()
    {
        var (tweak, registry) = Build();
        var backup = new InMemoryBackupWriter();
        var context = Context(backup);

        await tweak.ApplyAsync(context);
        await tweak.RevertAsync(context);

        Assert.Null(registry.GetValue(RegistryHive.LocalMachine, PriorityKey, "Win32PrioritySeparation"));
    }

    [Fact]
    public async Task Reverting_restores_the_previous_value_as_a_number_not_as_text()
    {
        var (tweak, registry) = Build();
        registry.Seed(RegistryHive.LocalMachine, PriorityKey, "Win32PrioritySeparation", 0x26,
            RegistryValueKind.DWord);

        var context = Context(new InMemoryBackupWriter());
        tweak.SelectedChoiceId = "gaming-2a";
        await tweak.ApplyAsync(context);
        await tweak.RevertAsync(context);

        // A string here would silently corrupt a DWORD slot on a real machine.
        Assert.Equal(0x26, registry.GetValue(RegistryHive.LocalMachine, PriorityKey, "Win32PrioritySeparation"));
    }

    [Fact]
    public async Task The_current_raw_value_is_shown_even_when_it_matches_a_known_choice()
    {
        var (tweak, registry) = Build();
        registry.Seed(RegistryHive.LocalMachine, PriorityKey, "Win32PrioritySeparation", 42,
            RegistryValueKind.DWord);

        Assert.Equal("42", await tweak.DescribeCurrentAsync(CancellationToken.None));
    }

    [Fact]
    public void The_default_selection_is_the_recommended_choice() =>
        Assert.Equal("gaming-2a", Build().Tweak.SelectedChoiceId);

    [Fact]
    public void Every_choice_tweak_offers_a_windows_default_so_the_user_can_always_get_back()
    {
        var registry = new InMemoryRegistryProvider();
        var catalog = new OptimizationCatalog(
            registry, new InMemoryServiceManager(), new NullRunner(), new InMemoryFileSystem());

        var choices = catalog.BuildAll().OfType<IChoiceOptimization>().ToList();

        Assert.NotEmpty(choices);
        Assert.All(choices, tweak =>
        {
            Assert.Contains(tweak.Choices, c => c.IsWindowsDefault);
            Assert.Contains(tweak.Choices, c => c.IsRecommended);
        });
    }

    [Fact]
    public void Choice_ids_within_a_tweak_are_unique()
    {
        var registry = new InMemoryRegistryProvider();
        var catalog = new OptimizationCatalog(
            registry, new InMemoryServiceManager(), new NullRunner(), new InMemoryFileSystem());

        foreach (var tweak in catalog.BuildAll().OfType<IChoiceOptimization>())
        {
            Assert.Equal(tweak.Choices.Count, tweak.Choices.Select(c => c.Id).Distinct().Count());
        }
    }

    private sealed class NullRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct) =>
            Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
    }
}
