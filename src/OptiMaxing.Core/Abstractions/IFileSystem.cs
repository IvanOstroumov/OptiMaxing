namespace OptiMaxing.Core.Abstractions;

public sealed record FileEntry(string Path, long Length, DateTime LastWriteTimeUtc);

/// <summary>Wraps filesystem enumeration/deletion so cleanup tweaks can be tested
/// without touching a real disk.</summary>
public interface IFileSystem
{
    bool DirectoryExists(string path);

    /// <summary>
    /// Best-effort recursive enumeration: directories/files that error out (locked,
    /// access denied, deleted mid-scan) are silently skipped rather than aborting
    /// the whole scan — cleanup targets like %TEMP% routinely contain files another
    /// process has open.
    /// </summary>
    IEnumerable<FileEntry> EnumerateFiles(string directory, string searchPattern);

    /// <summary>Returns false (rather than throwing) if the file could not be
    /// deleted, e.g. because it is in use.</summary>
    bool TryDeleteFile(string path);
}
