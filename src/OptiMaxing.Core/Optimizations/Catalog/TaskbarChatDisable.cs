using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class TaskbarChatDisable(IRegistryProvider registry) : RegistryOptimization(registry)
{
    public override string Id => "apps-taskbar-chat-disable";
    public override string DisplayName => "Chat (Meet Now / Teams): убрать с панели задач";
    public override string Description =>
        "Убирает значок чата/Teams с панели задач. Приложение остаётся установленным, просто скрывается кнопка.";
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string Category => Categories.Apps;

    protected override IReadOnlyList<RegistryTarget> Targets =>
    [
        new(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
            "TaskbarMn", 0, RegistryValueKind.DWord, OffValue: 1),
    ];
}
