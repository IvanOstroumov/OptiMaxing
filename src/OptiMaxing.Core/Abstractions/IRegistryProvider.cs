namespace OptiMaxing.Core.Abstractions;

public enum RegistryHive { ClassesRoot, CurrentUser, LocalMachine, Users }

public enum RegistryValueKind { String, ExpandString, Binary, DWord, MultiString, QWord }

/// <summary>
/// Every registry touch goes through this seam. Production uses the real hive;
/// tests use an in-memory fake so CI never mutates the machine.
/// </summary>
public interface IRegistryProvider
{
    object? GetValue(RegistryHive hive, string subKey, string valueName);

    void SetValue(RegistryHive hive, string subKey, string valueName, object value, RegistryValueKind kind);

    /// <summary>Removes a value. Used by revert when the value did not exist beforehand.</summary>
    void DeleteValue(RegistryHive hive, string subKey, string valueName);

    bool KeyExists(RegistryHive hive, string subKey);

    IReadOnlyList<string> GetSubKeyNames(RegistryHive hive, string subKey);

    IReadOnlyList<string> GetValueNames(RegistryHive hive, string subKey);
}
