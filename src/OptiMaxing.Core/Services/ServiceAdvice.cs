using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Services;

/// <summary>How safe it is to touch a service. Nothing is ever blocked outright — this only decides
/// how loudly we warn, per the "warn and confirm" rule.</summary>
public enum ServiceSafety
{
    /// <summary>Disabling breaks boot, login, networking or security. Warn in the strongest terms.</summary>
    Critical,

    /// <summary>Nothing is known about it — third-party, or a driver service. Warn generically.</summary>
    Unknown,

    /// <summary>Known to be safe to disable, with a stated reason.</summary>
    SafeToDisable,
}

public sealed record ServiceRow(
    ServiceInfo Info,
    ServiceSafety Safety,
    string? Note);

/// <summary>Names of services whose disabling is known to break the machine, and of services widely
/// recommended for disabling on a gaming PC. Matched by service name (not display name), because
/// display names are localized and would differ on a Russian Windows.</summary>
public static class ServiceAdvice
{
    private static readonly HashSet<string> CriticalServices = new(StringComparer.OrdinalIgnoreCase)
    {
        "RpcSs", "RpcEptMapper", "DcomLaunch", "LSM", "Power", "PlugPlay", "BrokerInfrastructure",
        "SamSs", "Netlogon", "EventLog", "Schedule", "ProfSvc", "Themes", "AudioSrv", "Audiosrv",
        "AudioEndpointBuilder", "Dhcp", "Dnscache", "NlaSvc", "nsi", "netprofm", "WinDefend",
        "SecurityHealthService", "mpssvc", "BFE", "CryptSvc", "TrustedInstaller", "wuauserv",
        "gpsvc", "UserManager", "SystemEventsBroker", "CoreMessagingRegistrar", "DispBrokerDesktopSvc",
    };

    private static readonly Dictionary<string, string> SafeToDisable = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DiagTrack"] = "Телеметрия Windows. Отключение не влияет на работу системы.",
        ["dmwappushservice"] = "Передача диагностики WAP. Не нужна на домашнем ПК.",
        ["diagnosticshub.standardcollector.service"] = "Сбор диагностики для разработчиков.",
        ["SysMain"] = "SuperFetch. На SSD выигрыша не даёт, но занимает диск и память.",
        ["WSearch"] = "Индексация поиска. Отключение освобождает диск, но ломает быстрый поиск в проводнике.",
        ["Spooler"] = "Диспетчер печати. Отключай, только если принтера нет.",
        ["Fax"] = "Факс. Практически всегда лишний.",
        ["RemoteRegistry"] = "Удалённый реестр. Отключён по умолчанию, лучше держать отключённым.",
        ["RemoteAccess"] = "Маршрутизация и удалённый доступ. Не нужна на игровом ПК.",
        ["MapsBroker"] = "Загрузка офлайн-карт.",
        ["RetailDemo"] = "Демо-режим для магазинов.",
        ["WalletService"] = "Кошелёк Windows.",
        ["PhoneSvc"] = "Телефония. Не нужна без модема.",
        ["XblAuthManager"] = "Авторизация Xbox Live. Нужна для Game Pass и игр из Microsoft Store.",
        ["XblGameSave"] = "Облачные сохранения Xbox. Нужна для Game Pass.",
        ["XboxNetApiSvc"] = "Сетевые сервисы Xbox. Нужна для Game Pass.",
        ["lfsvc"] = "Геолокация.",
        ["TabletInputService"] = "Рукописный и сенсорный ввод. Не нужна без тачскрина.",
        ["WerSvc"] = "Отчёты об ошибках Windows.",
        ["PcaSvc"] = "Помощник по совместимости программ.",
        ["SCardSvr"] = "Смарт-карты. Не нужна без картридера.",
        ["WbioSrvc"] = "Биометрия. Не нужна без сканера отпечатков или Windows Hello.",
        ["bthserv"] = "Bluetooth. Отключай, только если Bluetooth не используется.",
        ["stisvc"] = "Сканеры и камеры (WIA).",
        ["icssvc"] = "Мобильный хот-спот.",
    };

    public static ServiceRow Describe(ServiceInfo info)
    {
        if (CriticalServices.Contains(info.Name)
            || info.StartupType is ServiceStartupType.Boot or ServiceStartupType.System)
        {
            return new ServiceRow(info, ServiceSafety.Critical,
                "Служба нужна для загрузки, входа в систему, звука, сети или защиты.");
        }

        return SafeToDisable.TryGetValue(info.Name, out var note)
            ? new ServiceRow(info, ServiceSafety.SafeToDisable, note)
            : new ServiceRow(info, ServiceSafety.Unknown, null);
    }
}
