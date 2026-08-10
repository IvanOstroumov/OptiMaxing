using System.Security.Cryptography;
using System.Text;
using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Testing;

/// <summary>In-memory filesystem so cleanup tests never touch a real disk.</summary>
public sealed class InMemoryFileSystem : IFileSystem
{
    private readonly Dictionary<string, FileEntry> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _lockedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _contents = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _hashes = new(StringComparer.OrdinalIgnoreCase);

    public void SeedDirectory(string path) => _directories.Add(path);

    public void SeedFile(string directory, string fileName, long length, DateTime lastWriteTimeUtc)
    {
        _directories.Add(directory);
        var path = Path.Combine(directory, fileName);
        _files[path] = new FileEntry(path, length, lastWriteTimeUtc);
    }

    public void LockFile(string directory, string fileName) =>
        _lockedFiles.Add(Path.Combine(directory, fileName));

    /// <summary>Explicitly pins the hash a seeded file reports, so duplicate-detection tests can
    /// make two same-size files collide (or not) without relying on real byte content.</summary>
    public void SeedHash(string path, string hash) => _hashes[path] = hash;

    public bool DirectoryExists(string path) => _directories.Contains(path);

    public bool FileExists(string path) => _files.ContainsKey(path);

    /// <summary>Seeds a file with readable content, for parsers rather than cleanup tests.</summary>
    public void SeedText(string path, string content)
    {
        // Every ancestor counts as existing, like a real disk: a file two folders down still means
        // the folder above it is there.
        for (var directory = Path.GetDirectoryName(path);
             !string.IsNullOrEmpty(directory);
             directory = Path.GetDirectoryName(directory))
        {
            _directories.Add(directory);
        }

        _files[path] = new FileEntry(path, content.Length, DateTime.UtcNow);
        _contents[path] = content;
    }

    // Recursive, matching RealFileSystem: a fake that enumerates less than the real thing hides
    // exactly the bugs it is supposed to catch.
    public IEnumerable<FileEntry> EnumerateFiles(string directory, string searchPattern) =>
        _files.Values.Where(f =>
            f.Path.StartsWith(directory.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));

    public string? TryReadAllText(string path) =>
        _contents.TryGetValue(path, out var content) ? content : null;

    public bool TryDeleteFile(string path)
    {
        if (_lockedFiles.Contains(path))
            return false;

        return _files.Remove(path);
    }

    public string? TryComputeFileHash(string path)
    {
        if (_lockedFiles.Contains(path) || !_files.ContainsKey(path))
            return null;

        if (_hashes.TryGetValue(path, out var pinned))
            return pinned;

        // No explicit content/hash seeded: hash the path itself so distinct fake files never
        // spuriously collide as duplicates purely because a test forgot to seed content.
        var source = _contents.TryGetValue(path, out var content) ? content : path;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }
}
