namespace OptiMaxing.Core.Optimizations;

/// <summary>
/// Tweaks explicitly NOT implemented, with the reason, so the UI can explain why
/// rather than silently omitting them (п.24b of the spec).
/// </summary>
public sealed record MythEntry(string Title, string Reason);

public static class MythsCatalog
{
    public static readonly IReadOnlyList<MythEntry> Entries =
    [
        new("BCDEdit timer-хаки (useplatformclock/disabledynamictick)",
            "Актуальны были для старых HPET-проблем Windows 7/8. На современных билдах Windows 11 либо не дают эффекта, либо ухудшают стабильность таймеров."),

        new("ReadyBoost",
            "Спроектирован для систем с HDD и медленной RAM. На SSD/NVMe-системе флешка в разы медленнее оперативной памяти — эффект отрицательный."),

        new("Win32PrioritySeparation = 'Background services' профиль",
            "Часто советуют переключить в 'оптимизацию для фоновых служб', что на самом деле СНИЖАЕТ приоритет активных приложений на переднем плане — обратный эффект тому, что нужно геймеру."),

        new("TcpAckFrequency / TCPNoDelay реестровые твики",
            "Формально существуют, но независимые бенчмарки не показывают измеримого эффекта на современных сетевых стеках Windows 11; часто применяются 'по карго-культу' из гайдов 2012-2015 годов."),

        new("Отключение файла подкачки полностью",
            "Уменьшает не оперативную память, а стабильность: некоторые игры и драйверы рассчитывают на наличие pagefile даже при большом объёме RAM и падают в редких случаях без него."),
    ];
}
