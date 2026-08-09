using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;
using OptiMaxing.Core.Optimizations;
using OptiMaxing.Core.Testing;

namespace OptiMaxing.Tests;

public class FileCleanupOptimizationTests
{
    private sealed class FakeCleanup(IFileSystem fs, string dir, TimeSpan? minAge = null) : FileCleanupOptimization(fs)
    {
        public override string Id => "fake-cleanup";
        public override string DisplayName => "Fake cleanup";
        public override string Description => "test";
        public override RiskLevel Risk => RiskLevel.Safe;

        protected override IReadOnlyList<CleanupTarget> Targets => [new(dir, "*", minAge)];
    }

    [Fact]
    public async Task Reports_NotApplicable_when_target_directory_is_absent()
    {
        var fs = new InMemoryFileSystem();
        var tweak = new FakeCleanup(fs, @"C:\Temp\DoesNotExist");

        Assert.Equal(ApplyState.NotApplicable, await tweak.GetStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Apply_deletes_matching_files_and_reports_NotApplied_then_Applied()
    {
        var fs = new InMemoryFileSystem();
        const string dir = @"C:\Temp";
        fs.SeedDirectory(dir);
        fs.SeedFile(dir, "a.tmp", 1024, DateTime.UtcNow.AddDays(-2));
        fs.SeedFile(dir, "b.tmp", 2048, DateTime.UtcNow.AddDays(-2));

        var tweak = new FakeCleanup(fs, dir);
        var context = new OptimizationContext
        {
            Backup = new InMemoryBackupWriter(),
            Log = new Progress<string>(),
            Cancellation = default,
        };

        Assert.Equal(ApplyState.NotApplied, await tweak.GetStateAsync(CancellationToken.None));

        await tweak.ApplyAsync(context);

        Assert.Equal(ApplyState.Applied, await tweak.GetStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Skips_files_younger_than_MinAge()
    {
        var fs = new InMemoryFileSystem();
        const string dir = @"C:\Temp";
        fs.SeedDirectory(dir);
        fs.SeedFile(dir, "fresh.tmp", 1024, DateTime.UtcNow); // just written

        var tweak = new FakeCleanup(fs, dir, TimeSpan.FromDays(1));

        // Nothing eligible yet: reports as if already clean, and Apply must not
        // touch a file a running process might still be writing to.
        Assert.Equal(ApplyState.Applied, await tweak.GetStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Locked_files_are_skipped_without_throwing()
    {
        var fs = new InMemoryFileSystem();
        const string dir = @"C:\Temp";
        fs.SeedDirectory(dir);
        fs.SeedFile(dir, "locked.tmp", 1024, DateTime.UtcNow.AddDays(-2));
        fs.LockFile(dir, "locked.tmp");

        var tweak = new FakeCleanup(fs, dir);
        var context = new OptimizationContext
        {
            Backup = new InMemoryBackupWriter(),
            Log = new Progress<string>(),
            Cancellation = default,
        };

        await tweak.ApplyAsync(context);

        // Locked file survives, so the tweak honestly still reports NotApplied.
        Assert.Equal(ApplyState.NotApplied, await tweak.GetStateAsync(CancellationToken.None));
    }
}
