namespace OptiMaxing.Core.Files;

public readonly record struct TreemapRect(double X, double Y, double Width, double Height, DiskNode Node);

/// <summary>Squarified treemap layout (Bruls/Huizing/van Wijk): lays out rectangles whose area is
/// proportional to each node's size, minimizing aspect-ratio distortion by building rows along the
/// shorter side of the remaining space and closing a row once adding the next item would make its
/// worst aspect ratio worse rather than better.</summary>
public static class TreemapLayout
{
    public static List<TreemapRect> Compute(IReadOnlyList<DiskNode> nodes, double x, double y, double width, double height)
    {
        var result = new List<TreemapRect>();
        if (nodes.Count == 0 || width <= 0 || height <= 0)
            return result;

        var totalSize = nodes.Sum(n => n.SizeBytes);
        if (totalSize <= 0)
            return result;

        var totalArea = width * height;
        var items = nodes
            .Where(n => n.SizeBytes > 0)
            .Select(n => (Node: n, Area: n.SizeBytes / (double)totalSize * totalArea))
            .OrderByDescending(i => i.Area)
            .ToList();

        Squarify(items, x, y, width, height, result);
        return result;
    }

    private static void Squarify(
        List<(DiskNode Node, double Area)> items, double x, double y, double width, double height,
        List<TreemapRect> result)
    {
        while (items.Count > 0)
        {
            var side = Math.Min(width, height);
            var row = new List<(DiskNode Node, double Area)> { items[0] };
            var rowSum = items[0].Area;

            var i = 1;
            while (i < items.Count)
            {
                var candidateSum = rowSum + items[i].Area;
                if (Worst(row, side, rowSum) <= Worst(row.Append(items[i]).ToList(), side, candidateSum))
                {
                    break;
                }

                row.Add(items[i]);
                rowSum = candidateSum;
                i++;
            }

            items.RemoveRange(0, row.Count);

            var rowThickness = side <= 0 ? 0 : rowSum / side;
            var layoutHorizontally = width >= height;

            var cursor = layoutHorizontally ? y : x;
            foreach (var (node, area) in row)
            {
                var length = rowThickness <= 0 ? 0 : area / rowThickness;
                result.Add(layoutHorizontally
                    ? new TreemapRect(x, cursor, rowThickness, length, node)
                    : new TreemapRect(cursor, y, length, rowThickness, node));
                cursor += length;
            }

            if (layoutHorizontally)
            {
                x += rowThickness;
                width -= rowThickness;
            }
            else
            {
                y += rowThickness;
                height -= rowThickness;
            }

            if (width <= 0 || height <= 0)
                break;
        }
    }

    private static double Worst(List<(DiskNode Node, double Area)> row, double side, double rowSum)
    {
        if (row.Count == 0 || side <= 0 || rowSum <= 0)
            return double.MaxValue;

        var maxArea = row.Max(r => r.Area);
        var minArea = row.Min(r => r.Area);
        var sideSquared = side * side;
        var rowSumSquared = rowSum * rowSum;

        return Math.Max(
            sideSquared * maxArea / rowSumSquared,
            rowSumSquared / (sideSquared * minArea));
    }
}
