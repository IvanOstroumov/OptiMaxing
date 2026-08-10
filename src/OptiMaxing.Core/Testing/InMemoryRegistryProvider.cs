using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Testing;

/// <summary>In-memory registry so tests never touch the real machine.</summary>
public sealed class InMemoryRegistryProvider : IRegistryProvider
{
    private readonly Dictionary<string, object> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RegistryValueKind> _kinds = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, object> Snapshot => _values;

    public void Seed(RegistryHive hive, string subKey, string valueName, object value, RegistryValueKind kind)
    {
        _values[Key(hive, subKey, valueName)] = value;
        _kinds[Key(hive, subKey, valueName)] = kind;
    }

    public RegistryValueKind? KindOf(RegistryHive hive, string subKey, string valueName) =>
        _kinds.TryGetValue(Key(hive, subKey, valueName), out var kind) ? kind : null;

    public object? GetValue(RegistryHive hive, string subKey, string valueName) =>
        _values.TryGetValue(Key(hive, subKey, valueName), out var value) ? value : null;

    public void SetValue(RegistryHive hive, string subKey, string valueName, object value, RegistryValueKind kind)
    {
        _values[Key(hive, subKey, valueName)] = value;
        _kinds[Key(hive, subKey, valueName)] = kind;
    }

    public void DeleteValue(RegistryHive hive, string subKey, string valueName)
    {
        _values.Remove(Key(hive, subKey, valueName));
        _kinds.Remove(Key(hive, subKey, valueName));
    }

    public bool KeyExists(RegistryHive hive, string subKey) =>
        _values.Keys.Any(k => k.StartsWith($"{hive}\\{subKey}\\", StringComparison.OrdinalIgnoreCase));

    /// <summary>Keys are only implied by the values stored under them, so a subkey is any path
    /// segment that still has a further segment (the value name) after it.</summary>
    public IReadOnlyList<string> GetSubKeyNames(RegistryHive hive, string subKey)
    {
        var prefix = $"{hive}\\{subKey}\\";

        return _values.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(k => k[prefix.Length..].Split('\\'))
            .Where(segments => segments.Length > 1)
            .Select(segments => segments[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> GetValueNames(RegistryHive hive, string subKey) =>
        _values.Keys
            .Where(k => k.StartsWith($"{hive}\\{subKey}\\", StringComparison.OrdinalIgnoreCase))
            .Select(k => k[(k.LastIndexOf('\\') + 1)..])
            .ToList();

    private static string Key(RegistryHive hive, string subKey, string valueName) =>
        $"{hive}\\{subKey}\\{valueName}";
}

/// <summary>Backup writer that keeps captures in memory, for tests.</summary>
public sealed class InMemoryBackupWriter : Model.IBackupWriter
{
    private readonly Dictionary<(string, string), string?> _entries = [];

    public void Capture(string optimizationId, string key, string? previousValue) =>
        _entries.TryAdd((optimizationId, key), previousValue);

    public string? Read(string optimizationId, string key) =>
        _entries.TryGetValue((optimizationId, key), out var value) ? value : null;
}
