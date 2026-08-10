using System.Text;
using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Platform;
using OptiMaxing.Core.Model;
using OptiMaxing.Core.Optimizations;
using OptiMaxing.Core.Programs;
using OptiMaxing.Core.Safety;
using OptiMaxing.Core.Services;
using OptiMaxing.Core.Startup;

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
        DumpSecurity(new WindowsSecurityStatusProvider());

        using var sensors = new LibreHardwareSensorProvider();
        DumpSensors(sensors);

        DumpStartup(new StartupInventoryService(new WindowsRegistryProvider(), new RealFileSystem(), new ProcessRunner()));
        DumpProcesses(new ProcessMonitor(new WindowsProcessInspector()));
        DumpServices(new ServiceInventoryService(new WindowsServiceManager()));
        DumpPrograms(new InstalledProgramsService(new WindowsRegistryProvider(), new ProcessRunner()));
        DumpChoiceTweaks(new WindowsRegistryProvider());
        DumpTweakStates(new WindowsRegistryProvider());
    }

    /// <summary>Answers "what does the machine already have set", which is the whole point of the
    /// state scan: a tweak this app never applied can still be applied by Windows, by a driver
    /// installer, or by another tuning tool.</summary>
    private static void DumpTweakStates(IRegistryProvider registry)
    {
        Section("Состояние твиков на этой машине");

        var catalog = new OptimizationCatalog(
            registry, new WindowsServiceManager(), new ProcessRunner(), new RealFileSystem());

        var all = catalog.BuildAll().Where(o => o is not IChoiceOptimization).ToList();
        var states = all
            .Select(o => (Tweak: o, State: Probe(o)))
            .ToList();

        Console.WriteLine($"Всего пунктов: {all.Count}");
        foreach (var group in states.GroupBy(s => s.State).OrderBy(g => g.Key))
        {
            Console.WriteLine($"  {group.Key}: {group.Count()}");
        }

        foreach (var (tweak, state) in states
                     .Where(s => s.State is ApplyState.Applied or ApplyState.Modified)
                     .OrderBy(s => s.Tweak.Category))
        {
            Console.WriteLine($"  [{state}] {tweak.Category} / {tweak.DisplayName}");
        }
    }

    private static void DumpChoiceTweaks(IRegistryProvider registry)
    {
        Section("Твики со значением");

        var catalog = new OptimizationCatalog(
            registry, new WindowsServiceManager(), new ProcessRunner(), new RealFileSystem());

        foreach (var tweak in catalog.BuildAll().OfType<IChoiceOptimization>())
        {
            var raw = tweak.DescribeCurrentAsync(CancellationToken.None).GetAwaiter().GetResult();
            var known = tweak.GetCurrentChoiceAsync(CancellationToken.None).GetAwaiter().GetResult();

            Console.WriteLine();
            Console.WriteLine($"{tweak.DisplayName}");
            Console.WriteLine($"  сейчас в системе: {raw}" +
                              (known is null ? "  [значение задано не нами]" : $"  ({known.DisplayName})"));
            Console.WriteLine($"  вариантов: {tweak.Choices.Count}, по умолчанию выбран: {tweak.SelectedChoiceId}");
        }
    }

    /// <summary>The engine already swallows a failing probe; this dump has to do the same, or one
    /// broken tweak hides the state of all the others.</summary>
    private static ApplyState Probe(IOptimization tweak)
    {
        try
        {
            return tweak.GetStateAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  !! {tweak.Id}: {ex.GetType().Name}: {ex.Message}");
            return ApplyState.Unknown;
        }
    }

    private static void DumpPrograms(InstalledProgramsService programs)
    {
        Section("Установленные программы (топ-15 по размеру)");

        var all = programs.List();
        Console.WriteLine($"Всего: {all.Count}, " +
                          $"с деинсталлятором: {all.Count(p => p.CanUninstall)}, " +
                          $"системных компонентов: {all.Count(p => p.IsSystemComponent)}");

        foreach (var program in all.OrderByDescending(p => p.EstimatedSizeBytes ?? 0).Take(15))
        {
            var date = program.InstallDate?.ToString("yyyy-MM-dd") ?? "    —     ";
            Console.WriteLine($"  {program.SizeText,10}  {date}  {program.Name}" +
                              (program.CanUninstall ? string.Empty : "  [без деинсталлятора]"));
        }
    }

    private static void DumpServices(ServiceInventoryService services)
    {
        Section("Службы");

        var rows = services.List();
        Console.WriteLine($"Всего: {rows.Count}, " +
                          $"критичных: {rows.Count(r => r.Safety == ServiceSafety.Critical)}, " +
                          $"можно отключить: {rows.Count(r => r.Safety == ServiceSafety.SafeToDisable)}");

        foreach (var row in rows.Where(r => r.Safety == ServiceSafety.SafeToDisable))
        {
            var state = row.Info.IsRunning ? "работает" : "остановлена";
            Console.WriteLine($"  {row.Info.Name,-40} {ServiceInventoryService.Describe(row.Info.StartupType),-24} {state}");
        }
    }

    private static void DumpProcesses(ProcessMonitor monitor)
    {
        Section("Процессы (топ-15)");

        // The first poll only establishes a baseline; CPU percentages exist only relative to it.
        monitor.Poll();
        Thread.Sleep(1000);
        var samples = monitor.Poll();

        Console.WriteLine($"Всего процессов: {samples.Count}");

        foreach (var sample in samples
                     .OrderByDescending(s => s.CpuPercent ?? -1)
                     .ThenByDescending(s => s.Process.WorkingSetBytes)
                     .Take(15))
        {
            var cpu = sample.CpuPercent is { } percent ? $"{percent,5:N1}%" : "    —";
            var memory = sample.Process.WorkingSetBytes / 1024.0 / 1024.0;
            var flags = (sample.Process.IsSystemCritical ? " [системный]" : string.Empty)
                        + (sample.Process.IsResponding ? string.Empty : " [не отвечает]")
                        + (sample.Process.AccessFailure is null ? string.Empty : " [нет доступа]");

            Console.WriteLine($"  {cpu} {memory,8:N0} МБ  {sample.Process.Name} (PID {sample.Process.Id}){flags}");
        }
    }

    private static void DumpStartup(StartupInventoryService startup)
    {
        Section("Автозапуск");
        foreach (var e in startup.List())
        {
            var flags = new List<string>();
            if (!e.IsEnabled) flags.Add("отключено");
            if (!e.TargetExists) flags.Add("ФАЙЛ НЕ НАЙДЕН");
            if (e.IsCritical) flags.Add("системное");

            Console.WriteLine($"{e.Name,-32} {e.SourceText,-22} {e.ScopeText,-20} {string.Join(", ", flags)}");
            Console.WriteLine($"    {e.Command}");
            Console.WriteLine($"    exe: {e.ExecutablePath}");
        }
    }

    private static void DumpSensors(ISensorProvider sensors)
    {
        Section("Датчики");

        // Verified on this machine: without elevation LibreHardwareMonitor still opens and returns
        // GPU (via NVML) and memory data, but its ring0 driver does not load — so CPU temperature
        // and package power read a flat 0 and storage hardware is absent entirely. The GUI always
        // runs elevated; this note exists so those zeros are not mistaken for real measurements.
        if (!IsElevated())
        {
            Console.WriteLine("ВНИМАНИЕ: запущено без прав администратора. Драйвер мониторинга не поднимется,");
            Console.WriteLine("поэтому температура и мощность CPU будут нулями, а накопители не появятся вовсе.");
        }

        var readings = sensors.Read();

        if (!sensors.IsAvailable)
        {
            Console.WriteLine($"Датчики недоступны: {sensors.UnavailableReason}");
            return;
        }

        if (readings.Count == 0)
        {
            Console.WriteLine("Драйвер мониторинга поднялся, но ни одного значения не отдал.");
            return;
        }

        foreach (var group in readings.GroupBy(r => (r.Component, r.HardwareName)))
        {
            Console.WriteLine();
            Console.WriteLine($"-- {group.Key.Component}: {group.Key.HardwareName}");
            foreach (var r in group.OrderBy(r => r.Kind).ThenBy(r => r.SensorName))
            {
                Console.WriteLine($"   {r.Kind,-12} {r.SensorName,-32} {r.Value,10:F1} {r.Unit}");
            }
        }
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

    private static void DumpSecurity(ISecurityStatusProvider provider)
    {
        Section("Безопасность");

        var s = provider.Collect();
        Console.WriteLine($"Антивирус     : {s.Antivirus} ({s.AntivirusName ?? "не определён"})");
        Console.WriteLine($"Брандмауэр    : {s.Firewall}");
        Console.WriteLine($"UAC           : {s.UacEnabled}");
        Console.WriteLine($"Secure Boot   : {s.SecureBoot}");
        Console.WriteLine($"BitLocker (C:): {s.BitLockerSystemDrive}" +
                          (IsElevated() ? string.Empty : "  [нужны права администратора]"));
        Console.WriteLine($"Запущено от администратора: {(s.IsAdministrator ? "да" : "нет")}");
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

    private static bool IsElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity)
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {title} ===");
    }

    private static string Gb(ulong bytes) => $"{bytes / 1024.0 / 1024 / 1024:F1} ГБ";
}
