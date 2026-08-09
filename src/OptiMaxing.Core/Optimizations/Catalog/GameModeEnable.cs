using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class GameModeEnable(IRegistryProvider registry) : RegistryOptimization(registry)
{
    public override string Id => "game-mode-enable";
    public override string DisplayName => "Game Mode: включить";
    public override string Description =>
        "Windows приоритизирует игру и придерживает фоновые задачи и обновления во время игровой сессии.";
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string Category => Categories.WindowsBase;

    protected override IReadOnlyList<RegistryTarget> Targets =>
    [
        new(RegistryHive.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, RegistryValueKind.DWord, OffValue: 0),
    ];
}
