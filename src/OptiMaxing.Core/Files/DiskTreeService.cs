using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Files;

/// <summary>Builds a directory-size tree (WinDirStat-style) from a flat file listing. Walks each
/// file's path segments through a dictionary lookup rather than doing per-directory scans, since
/// IFileSystem.EnumerateFiles already recurses in one pass.</summary>
public sealed class DiskTreeService(IFileSystem fileSystem)
{
    public DiskNode BuildTree(string rootPath, IProgress<string>? progress, CancellationToken ct)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var root = new DiskNode(normalizedRoot, normalizedRoot, isDirectory: true);
        var nodesByPath = new Dictionary<string, DiskNode>(StringComparer.OrdinalIgnoreCase) { [normalizedRoot] = root };

        progress?.Report($"Считаю размеры в {normalizedRoot}…");

        var fileCount = 0;
        foreach (var entry in fileSystem.EnumerateFiles(normalizedRoot, "*"))
        {
            ct.ThrowIfCancellationRequested();

            var parent = GetOrCreateDirectory(nodesByPath, root, normalizedRoot, Path.GetDirectoryName(entry.Path));
            var fileNode = new DiskNode(Path.GetFileName(entry.Path), entry.Path, isDirectory: false)
            {
                SizeBytes = entry.Length,
            };
            parent.Children.Add(fileNode);

            fileCount++;
            if (fileCount % 5000 == 0)
                progress?.Report($"Обработано файлов: {fileCount}…");
        }

        RollUpSizes(root);
        SortDescending(root);

        progress?.Report($"Готово. Файлов: {fileCount}.");
        return root;
    }

    private static DiskNode GetOrCreateDirectory(
        Dictionary<string, DiskNode> nodesByPath, DiskNode root, string rootPath, string? directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath))
            return root;

        var normalized = Path.TrimEndingDirectorySeparator(directoryPath);
        if (nodesByPath.TryGetValue(normalized, out var existing))
            return existing;

        var parentPath = Path.GetDirectoryName(normalized);
        var parent = string.IsNullOrEmpty(parentPath) || normalized.Length <= rootPath.Length
            ? root
            : GetOrCreateDirectory(nodesByPath, root, rootPath, parentPath);

        var node = new DiskNode(Path.GetFileName(normalized), normalized, isDirectory: true);
        parent.Children.Add(node);
        nodesByPath[normalized] = node;
        return node;
    }

    private static long RollUpSizes(DiskNode node)
    {
        if (!node.IsDirectory)
            return node.SizeBytes;

        var total = node.Children.Sum(RollUpSizes);
        node.SizeBytes = total;
        return total;
    }

    private static void SortDescending(DiskNode node)
    {
        node.Children.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
        foreach (var child in node.Children)
            SortDescending(child);
    }
}
