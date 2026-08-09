using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

public sealed class NetworkThrottlingIndexDisable(IRegistryProvider registry) : RegistryOptimization(registry)
{
    public override string Id => "network-throttling-index-disable";
    public override string DisplayName => "Ограничение сетевого трафика для мультимедиа (NetworkThrottlingIndex): отключить";
    public override string Description =>
        "Windows по умолчанию придерживает обработку сетевых пакетов, чтобы освободить CPU для мультимедиа-потоков (MMCSS). На игровом ПК с запасом по CPU это ограничение обычно не нужно.";
    public override string TradeOff =>
        "Эффект на реальный пинг/фреймтайм почти всегда неощутим — это старая настройка эпохи Vista/7. Безопасно, но не жди чудес.";
    public override RiskLevel Risk => RiskLevel.Safe;
    public override string Category => Categories.Network;

    protected override IReadOnlyList<RegistryTarget> Targets =>
    [
        new(RegistryHive.LocalMachine,
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
            "NetworkThrottlingIndex", unchecked((int)0xffffffff), RegistryValueKind.DWord, OffValue: 10),
    ];
}
