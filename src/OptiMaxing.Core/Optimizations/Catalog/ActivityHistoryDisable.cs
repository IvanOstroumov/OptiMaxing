using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class ActivityHistoryDisable(IRegistryProvider registry) : RegistryOptimization(registry)
{
    public override string Id => "privacy-activity-history-disable";
    public override string DisplayName => "Журнал действий (Timeline/Activity Feed): отключить";
    public override string Description =>
        "Останавливает сбор и отправку в облако Microsoft истории того, какие приложения и документы ты открывал.";
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string Category => Categories.Privacy;

    protected override IReadOnlyList<RegistryTarget> Targets =>
    [
        // Absent by default = feature enabled, so no OffValue needed: null/missing
        // already reports NotApplied correctly via the base absent-is-off rule.
        new(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System",
            "EnableActivityFeed", 0, RegistryValueKind.DWord),
        new(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System",
            "PublishUserActivities", 0, RegistryValueKind.DWord),
        new(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System",
            "UploadUserActivities", 0, RegistryValueKind.DWord),
    ];
}
