using System.Collections.ObjectModel;
using OptiMaxing.Core.Advisory;
using OptiMaxing.Core.Optimizations;

namespace OptiMaxing.App.ViewModels;

/// <summary>Backs the "Инструменты и советы" tab: monitoring-tool launchers, advisory-only
/// tips that cannot be automated, and tweaks explicitly not implemented (with why).</summary>
public sealed class AdvisoryViewModel
{
    public ObservableCollection<ToolLauncherViewModel> Tools { get; } =
        new(ToolLauncherCatalog.Entries.Select(e => new ToolLauncherViewModel(e)));

    public ObservableCollection<AdvisoryEntry> Advisories { get; } =
        new(AdvisoryCatalog.Entries);

    public ObservableCollection<MythEntry> Myths { get; } =
        new(MythsCatalog.Entries);
}
