using OptiMaxing.Core.Files;
using OptiMaxing.Core.Testing;
using Xunit;

namespace OptiMaxing.Tests;

public class FileFinderServiceTests
{
    private static (FileFinderService Service, InMemoryFileSystem FileSystem) Build()
    {
        var fileSystem = new InMemoryFileSystem();
        var service = new FileFinderService(fileSystem);
        return (service, fileSystem);
    }

    [Fact]
    public void Largest_files_are_ordered_descending_by_size_and_capped_at_topLargest()
    {
        var (service, fs) = Build();
        fs.SeedFile(@"C:\data", "small.bin", 100, DateTime.UtcNow);
        fs.SeedFile(@"C:\data", "big.bin", 900, DateTime.UtcNow);
        fs.SeedFile(@"C:\data", "medium.bin", 500, DateTime.UtcNow);

        var report = service.Scan(@"C:\data", topLargest: 2, minAgeYears: 10, null, CancellationToken.None);

        Assert.Equal(2, report.Largest.Count);
        Assert.Equal("big.bin", System.IO.Path.GetFileName(report.Largest[0].Path));
        Assert.Equal("medium.bin", System.IO.Path.GetFileName(report.Largest[1].Path));
    }

    [Fact]
    public void Old_files_only_include_those_at_or_before_the_age_cutoff()
    {
        var (service, fs) = Build();
        fs.SeedFile(@"C:\data", "recent.bin", 100, DateTime.UtcNow);
        fs.SeedFile(@"C:\data", "ancient.bin", 100, DateTime.UtcNow.AddYears(-5));

        var report = service.Scan(@"C:\data", topLargest: 10, minAgeYears: 2, null, CancellationToken.None);

        var old = Assert.Single(report.Old);
        Assert.Equal("ancient.bin", System.IO.Path.GetFileName(old.Path));
    }

    [Fact]
    public void Files_with_matching_size_and_hash_are_grouped_as_duplicates()
    {
        var (service, fs) = Build();
        fs.SeedFile(@"C:\data", "a.bin", 100, DateTime.UtcNow);
        fs.SeedFile(@"C:\data", "b.bin", 100, DateTime.UtcNow);
        fs.SeedHash(@"C:\data\a.bin", "SAME");
        fs.SeedHash(@"C:\data\b.bin", "SAME");

        var report = service.Scan(@"C:\data", topLargest: 10, minAgeYears: 10, null, CancellationToken.None);

        var group = Assert.Single(report.Duplicates);
        Assert.Equal(2, group.Paths.Count);
    }

    [Fact]
    public void Matching_size_but_different_hash_is_not_reported_as_duplicate()
    {
        var (service, fs) = Build();
        fs.SeedFile(@"C:\data", "a.bin", 100, DateTime.UtcNow);
        fs.SeedFile(@"C:\data", "b.bin", 100, DateTime.UtcNow);
        fs.SeedHash(@"C:\data\a.bin", "ONE");
        fs.SeedHash(@"C:\data\b.bin", "TWO");

        var report = service.Scan(@"C:\data", topLargest: 10, minAgeYears: 10, null, CancellationToken.None);

        Assert.Empty(report.Duplicates);
    }

    [Fact]
    public void TryDeleteFile_delegates_to_the_underlying_file_system()
    {
        var (service, fs) = Build();
        fs.SeedFile(@"C:\data", "a.bin", 100, DateTime.UtcNow);

        Assert.True(service.TryDeleteFile(@"C:\data\a.bin"));
        Assert.False(fs.FileExists(@"C:\data\a.bin"));
    }
}
