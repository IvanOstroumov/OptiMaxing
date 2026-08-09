using System.Text.Json;
using System.Text.Json.Serialization;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Safety;

/// <summary>
/// Append-only journal of everything the app changed, so the user can undo a
/// single item rather than only "revert everything".
/// </summary>
public interface IOperationJournal
{
    void Append(OperationRecord record);
    void MarkReverted(string optimizationId, string snapshotId);
    IReadOnlyList<OperationRecord> ReadAll();
}

public sealed class OperationJournal(IAppLogger logger) : IOperationJournal
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _gate = new();
    private readonly string _path = Path.Combine(AppPaths.Journal, "operations.json");

    public void Append(OperationRecord record)
    {
        lock (_gate)
        {
            var all = ReadAllUnlocked().ToList();
            all.Add(record);
            Save(all);
        }
    }

    public void MarkReverted(string optimizationId, string snapshotId)
    {
        lock (_gate)
        {
            var all = ReadAllUnlocked()
                .Select(r => r.OptimizationId == optimizationId && r.SnapshotId == snapshotId
                    ? r with { Reverted = true }
                    : r)
                .ToList();
            Save(all);
        }
    }

    public IReadOnlyList<OperationRecord> ReadAll()
    {
        lock (_gate)
        {
            return ReadAllUnlocked();
        }
    }

    private IReadOnlyList<OperationRecord> ReadAllUnlocked()
    {
        if (!File.Exists(_path))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<OperationRecord>>(File.ReadAllText(_path), JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            logger.Write(LogLevel.Error, "Operation journal is unreadable; starting a fresh one.", ex);
            return [];
        }
    }

    private void Save(List<OperationRecord> records)
    {
        try
        {
            // Write-then-replace: a crash mid-write must not destroy the journal
            // that undo depends on.
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(records, JsonOptions));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            logger.Write(LogLevel.Error, "Failed to persist the operation journal.", ex);
        }
    }
}
