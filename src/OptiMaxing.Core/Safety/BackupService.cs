using System.Text.Json;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Safety;

/// <summary>
/// Persistent pre-change snapshots. Undo must survive an application restart,
/// so every captured value is flushed to disk rather than kept in memory.
/// </summary>
public interface IBackupService
{
    string CreateSnapshot();
    IBackupWriter GetWriter(string snapshotId);
    void Flush(string snapshotId);
    IReadOnlyDictionary<string, string?> ReadSnapshot(string snapshotId, string optimizationId);
    IReadOnlyList<string> ListSnapshots();
}

public sealed class BackupService(IAppLogger logger) : IBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Dictionary<string, SnapshotWriter> _open = [];

    public string CreateSnapshot()
    {
        var id = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss_fff");
        _open[id] = new SnapshotWriter();
        Directory.CreateDirectory(Path.Combine(AppPaths.Backups, id));
        return id;
    }

    public IBackupWriter GetWriter(string snapshotId)
    {
        if (!_open.TryGetValue(snapshotId, out var writer))
        {
            writer = new SnapshotWriter();
            _open[snapshotId] = writer;
        }
        return writer;
    }

    public void Flush(string snapshotId)
    {
        if (!_open.TryGetValue(snapshotId, out var writer))
            return;

        var dir = Path.Combine(AppPaths.Backups, snapshotId);
        Directory.CreateDirectory(dir);

        foreach (var (optimizationId, values) in writer.Entries)
        {
            try
            {
                var file = Path.Combine(dir, $"{Sanitize(optimizationId)}.json");
                File.WriteAllText(file, JsonSerializer.Serialize(values, JsonOptions));
            }
            catch (Exception ex)
            {
                logger.Write(LogLevel.Error, $"Failed to persist backup for '{optimizationId}'", ex);
            }
        }
    }

    public IReadOnlyDictionary<string, string?> ReadSnapshot(string snapshotId, string optimizationId)
    {
        var file = Path.Combine(AppPaths.Backups, snapshotId, $"{Sanitize(optimizationId)}.json");
        if (!File.Exists(file))
            return new Dictionary<string, string?>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(file))
                   ?? new Dictionary<string, string?>();
        }
        catch (Exception ex)
        {
            logger.Write(LogLevel.Error, $"Corrupt backup file '{file}'", ex);
            return new Dictionary<string, string?>();
        }
    }

    public IReadOnlyList<string> ListSnapshots()
    {
        if (!Directory.Exists(AppPaths.Backups))
            return [];

        return Directory.GetDirectories(AppPaths.Backups)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .OrderByDescending(name => name)
            .ToList();
    }

    private static string Sanitize(string id) =>
        string.Concat(id.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private sealed class SnapshotWriter : IBackupWriter
    {
        public Dictionary<string, Dictionary<string, string?>> Entries { get; } = [];

        public void Capture(string optimizationId, string key, string? previousValue)
        {
            if (!Entries.TryGetValue(optimizationId, out var values))
            {
                values = [];
                Entries[optimizationId] = values;
            }

            // Keep the earliest capture: re-applying must not overwrite the
            // original pre-change value with an already-modified one.
            values.TryAdd(key, previousValue);
        }

        public string? Read(string optimizationId, string key) =>
            Entries.TryGetValue(optimizationId, out var values) && values.TryGetValue(key, out var value)
                ? value
                : null;
    }
}
