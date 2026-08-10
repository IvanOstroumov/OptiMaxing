using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>Caps how many network packets a non-multimedia process may handle per millisecond.
/// Kept as a choice alongside the plain on/off tweak because the middle values are what actually
/// differ between guides; the switch only knows "стандарт" and "снят".</summary>
public sealed class NetworkThrottlingIndexChoice(IRegistryProvider registry)
    : RegistryChoiceOptimization(registry)
{
    public override string Id => "network-throttling-index-choice";
    public override string DisplayName => "Сетевой троттлинг (NetworkThrottlingIndex): значение";

    public override string Description =>
        "Ограничение числа сетевых пакетов в миллисекунду для обычных программ. Введено, чтобы " +
        "проигрывание аудио и видео не заикалось под сетевой нагрузкой.";

    public override string? TradeOff =>
        "Снятие ограничения помогает только там, где сеть реально упирается в этот лимит " +
        "(файловый сервер, торренты). В играх эффект чаще всего нулевой.";

    public override RiskLevel Risk => RiskLevel.Caution;
    public override string Category => Categories.Network;
    public override bool RequiresRestart => true;

    protected override RegistryHive Hive => RegistryHive.LocalMachine;

    protected override string SubKey =>
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";

    protected override string ValueName => "NetworkThrottlingIndex";
    protected override RegistryValueKind Kind => RegistryValueKind.DWord;

    public override IReadOnlyList<TweakChoice> Choices =>
    [
        new("default-10", "10 — как в Windows", "Заводской лимит: 10 пакетов на миллисекунду.",
            IsWindowsDefault: true),
        new("relaxed-70", "70 — ослабленный лимит",
            "Компромисс: сеть свободнее, защита от заикания аудио частично сохраняется.",
            IsRecommended: true),
        new("off-ffffffff", "Снять полностью (0xFFFFFFFF)",
            "Ограничение отключено. Под тяжёлой сетевой нагрузкой возможны заикания звука."),
    ];

    protected override object ValueFor(string choiceId) => choiceId switch
    {
        "relaxed-70" => 70,
        // Stored as a signed DWORD: 0xFFFFFFFF is -1 in the registry API's int world.
        "off-ffffffff" => unchecked((int)0xFFFFFFFF),
        _ => 10,
    };
}
