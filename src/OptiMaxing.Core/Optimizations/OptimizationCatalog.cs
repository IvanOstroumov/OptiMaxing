using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;
using OptiMaxing.Core.Optimizations.Catalog;

namespace OptiMaxing.Core.Optimizations;

/// <summary>Single place where every shipped optimization is registered.</summary>
public sealed class OptimizationCatalog(IRegistryProvider registry, IServiceManager services, IProcessRunner processRunner)
{
    public IReadOnlyList<IOptimization> BuildAll()
    {
        List<IOptimization> all =
        [
            // Safe
            new GameModeEnable(registry),
            new HagsEnable(registry),
            new MousePrecisionDisable(registry),
            new GameDvrDisable(registry),
            new SysMainDisable(services),
            new PowerPlanUltimatePerformance(processRunner),
            new AdvertisingIdDisable(registry),
            new TailoredExperiencesDisable(registry),
            new ActivityHistoryDisable(registry),
            new WidgetsDisable(registry),
            new TaskbarChatDisable(registry),
            new BingSearchSuggestionsDisable(registry),

            // Caution
            new PrintSpoolerDisable(services),
            new DiagTrackDisable(services),
            new WindowsSearchDisable(services),
            new DnsChangeCloudflare(processRunner),
            new CortanaSearchDisable(registry),
            new DiagnosticDataBasic(registry),
            new CopilotDisable(registry),
            new SettingSyncDisable(registry),
            new RecallDisable(registry),
            new FaxServiceDisable(services),
            new MapsBrokerDisable(services),
            new WmpNetworkSharingDisable(services),
            new RemoteRegistryDisable(services),

            // Advanced
            new VbsMemoryIntegrityDisable(registry),
        ];

        // Bloatware removal: one optimization per curated whitelist entry.
        all.AddRange(BloatwareCatalog.Entries.Select(app => new AppxBloatwareRemove(processRunner, app)));

        // Scheduled tasks: one optimization per curated whitelist entry.
        all.AddRange(ScheduledTaskCatalog.Entries.Select(task => new ScheduledTaskDisable(processRunner, task)));

        // Logon autostart entries: discovered live per machine, unlike the curated
        // lists above — there is no fixed catalog of "everyone's startup apps".
        all.AddRange(DiscoverStartupPrograms());

        return all;
    }

    private static readonly (Abstractions.RegistryHive Hive, string SubKey)[] RunKeys =
    [
        (Abstractions.RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run"),
        (Abstractions.RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run"),
    ];

    private IEnumerable<IOptimization> DiscoverStartupPrograms()
    {
        foreach (var (hive, subKey) in RunKeys)
        {
            IReadOnlyList<string> names;
            try
            {
                names = registry.GetValueNames(hive, subKey);
            }
            catch
            {
                continue; // key may not exist on a minimal/scanned machine
            }

            foreach (var name in names)
            {
                var command = registry.GetValue(hive, subKey, name)?.ToString() ?? string.Empty;
                yield return new StartupProgramDisable(registry, hive, subKey, name, command);
            }
        }
    }
}
