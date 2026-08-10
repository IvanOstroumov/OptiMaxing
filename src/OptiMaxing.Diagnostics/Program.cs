using System.Text;
using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Platform;

namespace OptiMaxing.Diagnostics;

/// <summary>Console dump of everything OptiMaxing can read about the machine. Deliberately has no
/// admin manifest: the GUI requires elevation, which makes the data-collection code impossible to
/// inspect during development. Everything here is read-only — nothing is ever written or applied.</summary>
internal static class Program
{
    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        DumpSystemInfo(new WindowsSystemInfoProvider());
        DumpHardware(new WmiHardwareInventoryProvider());
    }

    private static void DumpSystemInfo(ISystemInfoProvider info)
    {
        Section("Система");
        Console.WriteLine($"ОС            : {info.OsDescription()}");
        Console.WriteLine($"Процессор     : {info.ProcessorName()}");
        Console.WriteLine($"Аптайм        : {info.Uptime():d\\.hh\\:mm\\:ss}");

        var memory = info.GetMemoryInfo();
        Console.WriteLine($"Память        : {Gb(memory.TotalBytes)} всего, {Gb(memory.AvailableBytes)} свободно");

        Section("Логические диски");
        foreach (var disk in info.GetFixedDrives())
        {
            Console.WriteLine($"{disk.Name,-6} {Gb((ulong)disk.TotalBytes),10} всего, {Gb((ulong)disk.FreeBytes),10} свободно ({disk.FreePercent:F1}%)");
        }
    }

    private static void DumpHardware(IHardwareInventoryProvider provider)
    {
        var hw = provider.Collect();

        Section("Процессор (WMI)");
        if (hw.Cpu is { } cpu)
        {
            Console.WriteLine($"Модель        : {cpu.Name}");
            Console.WriteLine($"Производитель : {cpu.Manufacturer}");
            Console.WriteLine($"Ядра/потоки   : {cpu.PhysicalCores}/{cpu.LogicalCores}");
            Console.WriteLine($"Частота базовая: {cpu.BaseClockMhz} МГц");
            Console.WriteLine($"Сокет         : {cpu.Socket}");
            Console.WriteLine($"Кэш L2/L3     : {cpu.L2CacheKb} КБ / {cpu.L3CacheKb} КБ");
            Console.WriteLine($"Виртуализация : {(cpu.VirtualizationEnabled ? "включена" : "выключена")}");
        }
        else
        {
            Console.WriteLine("(нет данных)");
        }

        Section("Видеоадаптеры");
        foreach (var gpu in hw.Gpus)
        {
            Console.WriteLine($"Модель        : {gpu.Name}");
            Console.WriteLine($"VRAM          : {Gb(gpu.VideoMemoryBytes)}");
            Console.WriteLine($"Драйвер       : {gpu.DriverVersion} от {gpu.DriverDate:yyyy-MM-dd}");
            Console.WriteLine($"Видеопроцессор: {gpu.VideoProcessor}");
            Console.WriteLine();
        }

        Section("Материнская плата и BIOS");
        if (hw.Motherboard is { } mb)
        {
            Console.WriteLine($"Плата         : {mb.Manufacturer} {mb.Product} {mb.Version}");
        }

        if (hw.Bios is { } bios)
        {
            Console.WriteLine($"BIOS          : {bios.Manufacturer} {bios.Version} от {bios.ReleaseDate:yyyy-MM-dd}");
        }

        Section("Модули памяти");
        foreach (var m in hw.MemoryModules)
        {
            var xmp = m.RunningBelowRatedSpeed ? "  ← работает ниже заявленной (похоже, XMP/EXPO выключен)" : string.Empty;
            Console.WriteLine($"{m.DeviceLocator,-12} {Gb(m.CapacityBytes),8}  {m.ConfiguredClockMhz} МГц из {m.RatedClockMhz} МГц  {m.Manufacturer} {m.PartNumber}{xmp}");
        }

        Section("Физические накопители");
        foreach (var d in hw.Disks)
        {
            Console.WriteLine($"{d.Model,-40} {d.MediaType,-10} {d.InterfaceType,-8} {Gb(d.SizeBytes),10}");
        }

        if (hw.Failures.Count > 0)
        {
            Section("Сбои сбора данных");
            foreach (var f in hw.Failures)
            {
                Console.WriteLine(f);
            }
        }
    }

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {title} ===");
    }

    private static string Gb(ulong bytes) => $"{bytes / 1024.0 / 1024 / 1024:F1} ГБ";
}
