using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;
using OptiMaxing.Core.Optimizations;
using OptiMaxing.Core.Testing;
using Xunit;

namespace OptiMaxing.Tests;

/// <summary>Rules that must hold for every shipped tweak. The catalog is large and mostly data, so
/// these guard against the kind of mistake a copy-pasted entry makes: a duplicated id (which would
/// make two tweaks share one backup slot), an empty description, or a destructive action offered
/// as if it were harmless.</summary>
public class CatalogInvariantTests
{
    private static IReadOnlyList<IOptimization> All() =>
        new OptimizationCatalog(
                new InMemoryRegistryProvider(),
                new InMemoryServiceManager(),
                new NullRunner(),
                new InMemoryFileSystem())
            .BuildAll();

    [Fact]
    public void Ids_are_unique_across_the_whole_catalog()
    {
        var duplicates = All()
            .GroupBy(o => o.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        // Two tweaks sharing an id would write into the same backup slot and revert each other.
        Assert.Empty(duplicates);
    }

    [Fact]
    public void Every_tweak_explains_itself()
    {
        Assert.All(All(), o =>
        {
            Assert.False(string.IsNullOrWhiteSpace(o.DisplayName), o.Id);
            Assert.False(string.IsNullOrWhiteSpace(o.Description), o.Id);
            Assert.False(string.IsNullOrWhiteSpace(o.Category), o.Id);
        });
    }

    [Fact]
    public void Anything_irreversible_says_what_it_costs()
    {
        var silent = All()
            .Where(o => o.Reversibility == Reversibility.Irreversible)
            .Where(o => string.IsNullOrWhiteSpace(o.TradeOff))
            .Select(o => o.Id)
            .ToList();

        Assert.Empty(silent);
    }

    [Fact]
    public void The_catalog_is_actually_large()
    {
        // The customer asked for a tool that replaces WinUtil and friends outright; a couple of
        // dozen tweaks would not. This is a floor, not a target: it exists so a refactor that
        // silently drops a whole block of the catalog fails loudly.
        Assert.True(All().Count > 80, $"в каталоге всего {All().Count} пунктов");
    }

    private sealed class NullRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken ct) =>
            Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
    }
}
