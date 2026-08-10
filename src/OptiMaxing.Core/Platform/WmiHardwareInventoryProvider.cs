using System.Management;
using Microsoft.Win32;
using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Platform;

public sealed class WmiHardwareInventoryProvider : IHardwareInventoryProvider
{
    private const string DisplayAdapterClassKey =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    public HardwareInventory Collect()
    {
        var failures = new List<string>();

        return new HardwareInventory(
            Guard(CollectCpu, "Win32_Processor", failures),
            Guard(CollectGpus, "Win32_VideoController", failures) ?? [],
            Guard(CollectMotherboard, "Win32_BaseBoard", failures),
            Guard(CollectBios, "Win32_BIOS", failures),
            Guard(CollectMemoryModules, "Win32_PhysicalMemory", failures) ?? [],
            Guard(CollectDisks, "MSFT_PhysicalDisk/Win32_DiskDrive", failures) ?? [],
            failures);
    }

    private static T? Guard<T>(Func<T?> collect, string source, List<string> failures)
    {
        try
        {
            return collect();
        }
        catch (Exception ex)
        {
            failures.Add($"{source}: {ex.Message}");
            return default;
        }
    }

    private static CpuInventory? CollectCpu()
    {
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
        foreach (var o in searcher.Get().Cast<ManagementObject>())
        {
            using (o)
            {
                return new CpuInventory(
                    Str(o, "Name"),
                    Str(o, "Manufacturer"),
                    (int)Num(o, "NumberOfCores"),
                    (int)Num(o, "NumberOfLogicalProcessors"),
                    (int)Num(o, "MaxClockSpeed"),
                    Str(o, "SocketDesignation"),
                    (int)Num(o, "L2CacheSize"),
                    (int)Num(o, "L3CacheSize"),
                    o["VirtualizationFirmwareEnabled"] as bool? ?? false);
            }
        }

        return null;
    }

    private static IReadOnlyList<GpuInventory> CollectGpus()
    {
        var vramByAdapter = ReadVramFromDriverKeys();
        var result = new List<GpuInventory>();

        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
        foreach (var o in searcher.Get().Cast<ManagementObject>())
        {
            using (o)
            {
                // Win32_VideoController.AdapterRAM is a uint32 and therefore saturates at 4 GB,
                // which is wrong for most modern cards. The driver's own registry key stores the
                // real size as a 64-bit value, so prefer that and fall back to WMI only if absent.
                var name = Str(o, "Name");
                var vram = vramByAdapter.TryGetValue(name, out var real) && real > 0
                    ? real
                    : (ulong)Num(o, "AdapterRAM");

                result.Add(new GpuInventory(
                    name,
                    vram,
                    Str(o, "DriverVersion"),
                    Date(o, "DriverDate"),
                    Str(o, "VideoProcessor")));
            }
        }

        return result;
    }

    /// <summary>Keyed by adapter name rather than enumeration index: WMI's video-controller order
    /// does not line up with the driver class subkey order once virtual adapters (Parsec, RDP,
    /// OBS) are installed, and mismatching them assigns one card's VRAM to another. AdapterString
    /// is a vendor-supplied identifier, not localized UI text, so matching on it is locale-safe.</summary>
    private static Dictionary<string, ulong> ReadVramFromDriverKeys()
    {
        var map = new Dictionary<string, ulong>(StringComparer.OrdinalIgnoreCase);
        using var root = Registry.LocalMachine.OpenSubKey(DisplayAdapterClassKey);
        if (root is null)
        {
            return map;
        }

        foreach (var name in root.GetSubKeyNames().Where(n => n.Length == 4 && n.All(char.IsDigit)))
        {
            using var sub = root.OpenSubKey(name);
            if (sub?.GetValue("HardwareInformation.qwMemorySize") is not long size || size <= 0)
            {
                continue;
            }

            // AdapterString is stored as REG_BINARY holding a null-terminated UTF-16 string on
            // some drivers and as a plain REG_SZ on others.
            var adapter = sub.GetValue("HardwareInformation.AdapterString") switch
            {
                string s => s,
                byte[] bytes => System.Text.Encoding.Unicode.GetString(bytes).TrimEnd('\0'),
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(adapter))
            {
                map[adapter.Trim()] = (ulong)size;
            }
        }

        return map;
    }

    private static MotherboardInventory? CollectMotherboard()
    {
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
        foreach (var o in searcher.Get().Cast<ManagementObject>())
        {
            using (o)
            {
                return new MotherboardInventory(Str(o, "Manufacturer"), Str(o, "Product"), Str(o, "Version"));
            }
        }

        return null;
    }

    private static BiosInventory? CollectBios()
    {
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
        foreach (var o in searcher.Get().Cast<ManagementObject>())
        {
            using (o)
            {
                var version = Str(o, "SMBIOSBIOSVersion");
                return new BiosInventory(
                    Str(o, "Manufacturer"),
                    string.IsNullOrWhiteSpace(version) ? Str(o, "Version") : version,
                    Date(o, "ReleaseDate"));
            }
        }

        return null;
    }

    private static IReadOnlyList<MemoryModuleInventory> CollectMemoryModules()
    {
        var result = new List<MemoryModuleInventory>();
        using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
        foreach (var o in searcher.Get().Cast<ManagementObject>())
        {
            using (o)
            {
                result.Add(new MemoryModuleInventory(
                    Str(o, "BankLabel"),
                    Str(o, "DeviceLocator"),
                    (ulong)Num(o, "Capacity"),
                    (int)Num(o, "ConfiguredClockSpeed"),
                    (int)Num(o, "Speed"),
                    Str(o, "Manufacturer"),
                    Str(o, "PartNumber")));
            }
        }

        return result;
    }

    private static IReadOnlyList<PhysicalDiskInventory> CollectDisks()
    {
        // MSFT_PhysicalDisk distinguishes SSD from HDD; Win32_DiskDrive.MediaType only ever says
        // "Fixed hard disk media" and cannot. Storage namespace is missing on older builds, so
        // Win32_DiskDrive remains the fallback.
        try
        {
            var result = new List<PhysicalDiskInventory>();
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM MSFT_PhysicalDisk"));
            foreach (var o in searcher.Get().Cast<ManagementObject>())
            {
                using (o)
                {
                    result.Add(new PhysicalDiskInventory(
                        Str(o, "FriendlyName"),
                        BusTypeName((ushort)Num(o, "BusType")),
                        MediaTypeName((ushort)Num(o, "MediaType")),
                        (ulong)Num(o, "Size"),
                        Str(o, "SerialNumber")));
                }
            }

            if (result.Count > 0)
            {
                return result;
            }
        }
        catch (ManagementException)
        {
            // Fall through to Win32_DiskDrive.
        }

        var fallback = new List<PhysicalDiskInventory>();
        using var legacy = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
        foreach (var o in legacy.Get().Cast<ManagementObject>())
        {
            using (o)
            {
                fallback.Add(new PhysicalDiskInventory(
                    Str(o, "Model"),
                    Str(o, "InterfaceType"),
                    "неизвестно",
                    (ulong)Num(o, "Size"),
                    Str(o, "SerialNumber")));
            }
        }

        return fallback;
    }

    private static string MediaTypeName(ushort value) => value switch
    {
        3 => "HDD",
        4 => "SSD",
        5 => "SCM",
        _ => "неизвестно",
    };

    private static string BusTypeName(ushort value) => value switch
    {
        1 => "SCSI",
        3 => "ATA",
        7 => "USB",
        8 => "RAID",
        11 => "SATA",
        17 => "NVMe",
        _ => $"тип {value}",
    };

    private static string Str(ManagementObject o, string property) =>
        (o[property] as string)?.Trim() ?? string.Empty;

    private static ulong Num(ManagementObject o, string property) => o[property] switch
    {
        null => 0,
        var v => Convert.ToUInt64(v),
    };

    private static DateTime? Date(ManagementObject o, string property)
    {
        if (o[property] is not string raw || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return ManagementDateTimeConverter.ToDateTime(raw);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
