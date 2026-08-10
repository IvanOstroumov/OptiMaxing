namespace OptiMaxing.Core.Files;

/// <summary>One node in the disk-usage tree. Mutable: size is rolled up bottom-up after the
/// tree is built from a flat file listing.</summary>
public sealed class DiskNode(string name, string fullPath, bool isDirectory)
{
    public string Name { get; } = name;
    public string FullPath { get; } = fullPath;
    public bool IsDirectory { get; } = isDirectory;
    public long SizeBytes { get; set; }
    public List<DiskNode> Children { get; } = [];
}
