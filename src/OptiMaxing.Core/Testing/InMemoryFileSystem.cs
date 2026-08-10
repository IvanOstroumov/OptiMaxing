using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Testing;

/// <summary>In-memory filesystem so cleanup tests never touch a real disk.</summary>
public sealed class InMemoryFileSystem : IFileSystem
{
    private readonly Dictionary<string, FileEntry> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _lockedFiles = new(StringComparer.OrdinalIgnoreCase);

    public void SeedDirectory(string path) => _directories.Add(path);

    public void SeedFile(string directory, string fileName, long length, DateTime lastWriteTimeUtc)
    {
        _directories.Add(directory);
        var path = Path.Combine(directory, fileName);
        _files[path] = new FileEntry(path, length, lastWriteTimeUtc);
    }

    public void LockFile(string directory, string fileName) =>
        _lockedFiles.Add(Path.Combine(directory, fileName));

    public bool DirectoryExists(string path) => _directories.Contains(path);

    public bool FileExists(string path) => _files.ContainsKey(path);

    public IEnumerable<FileEntry> EnumerateFiles(string directory, string searchPattern) =>
        _files.Values.Where(f => Path.GetDirectoryName(f.Path) == directory);

    public bool TryDeleteFile(string path)
    {
        if (_lockedFiles.Contains(path))
            return false;

        return _files.Remove(path);
    }
}
