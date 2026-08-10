using OptiMaxing.Core.Files;
using OptiMaxing.Core.Testing;
using Xunit;

namespace OptiMaxing.Tests;

public class DiskTreeServiceTests
{
    private static (DiskTreeService Service, InMemoryFileSystem FileSystem) Build()
    {
        var fileSystem = new InMemoryFileSystem();
        var service = new DiskTreeService(fileSystem);
        return (service, fileSystem);
    }

    [Fact]
    public void A_directory_size_rolls_up_from_its_files()
    {
        var (service, fs) = Build();
        fs.SeedFile(@"C:\data", "a.bin", 100, DateTime.UtcNow);
        fs.SeedFile(@"C:\data", "b.bin", 200, DateTime.UtcNow);

        var root = service.BuildTree(@"C:\data", null, CancellationToken.None);

        Assert.Equal(300, root.SizeBytes);
        Assert.Equal(2, root.Children.Count);
    }

    [Fact]
    public void Nested_subdirectories_are_created_and_rolled_up()
    {
        var (service, fs) = Build();
        fs.SeedFile(@"C:\data\sub", "a.bin", 100, DateTime.UtcNow);
        fs.SeedFile(@"C:\data", "b.bin", 50, DateTime.UtcNow);

        var root = service.BuildTree(@"C:\data", null, CancellationToken.None);

        Assert.Equal(150, root.SizeBytes);
        var sub = Assert.Single(root.Children, c => c.IsDirectory);
        Assert.Equal(100, sub.SizeBytes);
        Assert.Equal("sub", sub.Name);
    }

    [Fact]
    public void Children_are_sorted_largest_first()
    {
        var (service, fs) = Build();
        fs.SeedFile(@"C:\data", "small.bin", 10, DateTime.UtcNow);
        fs.SeedFile(@"C:\data", "big.bin", 1000, DateTime.UtcNow);

        var root = service.BuildTree(@"C:\data", null, CancellationToken.None);

        Assert.Equal("big.bin", root.Children[0].Name);
        Assert.Equal("small.bin", root.Children[1].Name);
    }
}
