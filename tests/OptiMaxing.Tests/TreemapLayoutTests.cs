using OptiMaxing.Core.Files;
using Xunit;

namespace OptiMaxing.Tests;

public class TreemapLayoutTests
{
    [Fact]
    public void A_single_node_fills_the_whole_available_rectangle()
    {
        var node = new DiskNode("only", @"C:\only", isDirectory: false) { SizeBytes = 100 };

        var rects = TreemapLayout.Compute([node], 0, 0, 200, 100);

        var rect = Assert.Single(rects);
        Assert.Equal(0, rect.X);
        Assert.Equal(0, rect.Y);
        Assert.Equal(200, rect.Width, precision: 6);
        Assert.Equal(100, rect.Height, precision: 6);
    }

    [Fact]
    public void Rectangle_areas_are_proportional_to_node_size()
    {
        var big = new DiskNode("big", @"C:\big", isDirectory: false) { SizeBytes = 300 };
        var small = new DiskNode("small", @"C:\small", isDirectory: false) { SizeBytes = 100 };

        var rects = TreemapLayout.Compute([big, small], 0, 0, 400, 100);

        var totalArea = 400.0 * 100.0;
        var bigRect = rects.Single(r => r.Node == big);
        var smallRect = rects.Single(r => r.Node == small);

        Assert.Equal(totalArea * 0.75, bigRect.Width * bigRect.Height, precision: 1);
        Assert.Equal(totalArea * 0.25, smallRect.Width * smallRect.Height, precision: 1);
    }

    [Fact]
    public void Rectangles_do_not_overlap_and_stay_within_bounds()
    {
        var nodes = new[]
        {
            new DiskNode("a", @"C:\a", false) { SizeBytes = 400 },
            new DiskNode("b", @"C:\b", false) { SizeBytes = 300 },
            new DiskNode("c", @"C:\c", false) { SizeBytes = 200 },
            new DiskNode("d", @"C:\d", false) { SizeBytes = 100 },
        };

        var rects = TreemapLayout.Compute(nodes, 0, 0, 300, 200);

        Assert.Equal(4, rects.Count);
        foreach (var rect in rects)
        {
            Assert.True(rect.X >= -0.001);
            Assert.True(rect.Y >= -0.001);
            Assert.True(rect.X + rect.Width <= 300.001);
            Assert.True(rect.Y + rect.Height <= 200.001);
        }
    }

    [Fact]
    public void Zero_size_nodes_are_excluded_from_layout()
    {
        var real = new DiskNode("real", @"C:\real", false) { SizeBytes = 100 };
        var empty = new DiskNode("empty", @"C:\empty", true) { SizeBytes = 0 };

        var rects = TreemapLayout.Compute([real, empty], 0, 0, 100, 100);

        Assert.Single(rects);
        Assert.Equal(real, rects[0].Node);
    }

    [Fact]
    public void An_empty_node_list_produces_no_rectangles()
    {
        var rects = TreemapLayout.Compute([], 0, 0, 100, 100);
        Assert.Empty(rects);
    }
}
