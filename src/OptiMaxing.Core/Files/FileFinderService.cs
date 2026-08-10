using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Files;

public sealed record FoundFile(string Path, long SizeBytes, DateTime LastWriteTimeUtc);

public sealed record DuplicateGroup(long SizeBytes, IReadOnlyList<string> Paths);

public sealed record FileFinderReport(
    IReadOnlyList<FoundFile> Largest,
    IReadOnlyList<FoundFile> Old,
    IReadOnlyList<DuplicateGroup> Duplicates);

/// <summary>Scans a directory tree once and reports the largest files, files untouched for
/// N+ years, and byte-identical duplicates (confirmed by hash, not just matching size).</summary>
public sealed class FileFinderService(IFileSystem fileSystem)
{
    public FileFinderReport Scan(
        string rootDirectory,
        int topLargest,
        int minAgeYears,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        progress?.Report($"Сканирую {rootDirectory}…");

        var all = fileSystem.EnumerateFiles(rootDirectory, "*").ToList();
        ct.ThrowIfCancellationRequested();

        var largest = all
            .OrderByDescending(f => f.Length)
            .Take(topLargest)
            .Select(f => new FoundFile(f.Path, f.Length, f.LastWriteTimeUtc))
            .ToList();

        var ageCutoffUtc = DateTime.UtcNow.AddYears(-minAgeYears);
        var old = all
            .Where(f => f.LastWriteTimeUtc <= ageCutoffUtc)
            .OrderBy(f => f.LastWriteTimeUtc)
            .Select(f => new FoundFile(f.Path, f.Length, f.LastWriteTimeUtc))
            .ToList();

        progress?.Report("Ищу дубликаты…");
        var duplicates = FindDuplicates(all, ct);

        return new FileFinderReport(largest, old, duplicates);
    }

    /// <summary>Permanent delete (no recycle bin) — callers must confirm with the user first.</summary>
    public bool TryDeleteFile(string path) => fileSystem.TryDeleteFile(path);

    private List<DuplicateGroup> FindDuplicates(List<FileEntry> all, CancellationToken ct)
    {
        var duplicates = new List<DuplicateGroup>();

        // Only files sharing an exact size can possibly be duplicates — hashing every file up
        // front would be far slower for no benefit, since a size mismatch already rules it out.
        var bySize = all.Where(f => f.Length > 0).GroupBy(f => f.Length).Where(g => g.Count() > 1);

        foreach (var sizeGroup in bySize)
        {
            ct.ThrowIfCancellationRequested();

            var byHash = new Dictionary<string, List<string>>();
            foreach (var entry in sizeGroup)
            {
                var hash = fileSystem.TryComputeFileHash(entry.Path);
                if (hash is null)
                    continue;

                if (!byHash.TryGetValue(hash, out var paths))
                {
                    paths = [];
                    byHash[hash] = paths;
                }

                paths.Add(entry.Path);
            }

            foreach (var (_, paths) in byHash)
            {
                if (paths.Count > 1)
                    duplicates.Add(new DuplicateGroup(sizeGroup.Key, paths));
            }
        }

        return duplicates.OrderByDescending(d => d.SizeBytes).ToList();
    }
}
