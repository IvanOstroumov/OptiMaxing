using Microsoft.Win32;
using OptiMaxing.Core.Abstractions;
using Win32Kind = Microsoft.Win32.RegistryValueKind;
using OwnKind = OptiMaxing.Core.Abstractions.RegistryValueKind;
using OwnHive = OptiMaxing.Core.Abstractions.RegistryHive;

namespace OptiMaxing.Core.Platform;

public sealed class WindowsRegistryProvider : IRegistryProvider
{
    public object? GetValue(OwnHive hive, string subKey, string valueName)
    {
        using var key = OpenBase(hive).OpenSubKey(subKey, writable: false);
        return key?.GetValue(valueName);
    }

    public void SetValue(OwnHive hive, string subKey, string valueName, object value, OwnKind kind)
    {
        using var key = OpenBase(hive).CreateSubKey(subKey, writable: true)
            ?? throw new InvalidOperationException($"Cannot open or create {hive}\\{subKey}");
        key.SetValue(valueName, value, Translate(kind));
    }

    public void DeleteValue(OwnHive hive, string subKey, string valueName)
    {
        using var key = OpenBase(hive).OpenSubKey(subKey, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    public bool KeyExists(OwnHive hive, string subKey)
    {
        using var key = OpenBase(hive).OpenSubKey(subKey, writable: false);
        return key is not null;
    }

    public IReadOnlyList<string> GetSubKeyNames(OwnHive hive, string subKey)
    {
        using var key = OpenBase(hive).OpenSubKey(subKey, writable: false);
        return key?.GetSubKeyNames() ?? [];
    }

    public IReadOnlyList<string> GetValueNames(OwnHive hive, string subKey)
    {
        using var key = OpenBase(hive).OpenSubKey(subKey, writable: false);
        return key?.GetValueNames() ?? [];
    }

    // 64-bit view explicitly: the app is x64 but several GPU/driver keys are
    // redirected under WOW6432Node when opened through the default view.
    private static RegistryKey OpenBase(OwnHive hive) => hive switch
    {
        OwnHive.ClassesRoot => RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.ClassesRoot, RegistryView.Registry64),
        OwnHive.CurrentUser => RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.CurrentUser, RegistryView.Registry64),
        OwnHive.LocalMachine => RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64),
        OwnHive.Users => RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.Users, RegistryView.Registry64),
        _ => throw new ArgumentOutOfRangeException(nameof(hive)),
    };

    private static Win32Kind Translate(OwnKind kind) => kind switch
    {
        OwnKind.String => Win32Kind.String,
        OwnKind.ExpandString => Win32Kind.ExpandString,
        OwnKind.Binary => Win32Kind.Binary,
        OwnKind.DWord => Win32Kind.DWord,
        OwnKind.MultiString => Win32Kind.MultiString,
        OwnKind.QWord => Win32Kind.QWord,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
