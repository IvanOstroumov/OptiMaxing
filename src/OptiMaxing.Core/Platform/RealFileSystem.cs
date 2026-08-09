using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Platform;

public sealed class RealFileSystem : IFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<FileEntry> EnumerateFiles(string directory, string searchPattern)
    {
        if (!Directory.Exists(directory))
            yield break;

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        IEnumerator<string>? files;
        try
        {
            files = Directory.EnumerateFiles(directory, searchPattern, options).GetEnumerator();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            yield break;
        }

        using (files)
        {
            while (true)
            {
                string current;
                try
                {
                    if (!files.MoveNext())
                        break;
                    current = files.Current;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    // A file/folder vanished or is locked mid-scan; skip and keep going.
                    continue;
                }

                FileEntry? entry = null;
                try
                {
                    var info = new FileInfo(current);
                    entry = new FileEntry(current, info.Length, info.LastWriteTimeUtc);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    // Skip files we cannot stat.
                }

                if (entry is not null)
                    yield return entry;
            }
        }
    }

    public bool TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
