using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>
/// Curated whitelist of stock Windows 11 AppX packages that are safe to remove for
/// a dedicated gaming PC — pure clutter with no gameplay-relevant function. Deliberately
/// excludes anything that could double as a dependency (e.g. the Store itself, the
/// Xbox/Game Bar family, .NET runtime packages) — see AntiCheatGuard/PowerShell for why
/// touching those is out of scope for v1.
/// </summary>
public static class BloatwareCatalog
{
    public sealed record Entry(string PackageName, string DisplayName, string Description, RiskLevel Risk);

    public static readonly IReadOnlyList<Entry> Entries =
    [
        new("Microsoft.3DBuilder", "3D Builder", "Устаревшее приложение для 3D-печати, замененное Print 3D.", RiskLevel.Safe),
        new("Microsoft.MicrosoftSolitaireCollection", "Microsoft Solitaire Collection", "Предустановленный пасьянс с рекламой.", RiskLevel.Safe),
        new("Microsoft.BingWeather", "Погода (MSN Weather)", "Предустановленное приложение погоды.", RiskLevel.Safe),
        new("Microsoft.BingNews", "Новости (MSN News)", "Предустановленная лента новостей.", RiskLevel.Safe),
        new("Microsoft.GetHelp", "Получить справку", "Приложение поддержки, дублирующее веб-справку Microsoft.", RiskLevel.Safe),
        new("Microsoft.Getstarted", "Советы (Tips)", "Обучающее приложение для новых пользователей Windows.", RiskLevel.Safe),
        new("Microsoft.MixedReality.Portal", "Mixed Reality Portal", "Не нужно без гарнитуры смешанной реальности.", RiskLevel.Safe),
        new("Microsoft.WindowsAlarms", "Будильники и часы", "Предустановленное приложение будильника.", RiskLevel.Safe),
        new("Microsoft.WindowsMaps", "Карты", "Предустановленное приложение офлайн-карт.", RiskLevel.Safe),
        new("Microsoft.ZuneMusic", "Groove Music / Media Player (медиатека)", "Встроенный медиаплеер, обычно заменяется Spotify/foobar2000/VLC.", RiskLevel.Caution),
        new("Microsoft.ZuneVideo", "Кино и ТВ (Movies & TV)", "Встроенный видеоплеер/магазин, обычно не используется на игровом ПК.", RiskLevel.Safe),
        new("Microsoft.YourPhone", "Связь с телефоном (Phone Link)", "Синхронизация с Android/iPhone — удаляй только если не пользуешься.", RiskLevel.Caution),
        new("Microsoft.Todos", "Microsoft To Do", "Приложение списка задач, обычно заменяется сторонним трекером.", RiskLevel.Safe),
        new("Microsoft.PowerAutomateDesktop", "Power Automate", "Автоматизация рабочих задач, не нужна на игровом ПК.", RiskLevel.Safe),
        new("Clipchamp.Clipchamp", "Clipchamp", "Предустановленный видеоредактор с ограничениями бесплатной версии.", RiskLevel.Safe),
        new("MicrosoftTeams", "Teams (потребительская версия)", "Личная версия Teams, отдельная от корпоративного клиента.", RiskLevel.Caution),
        new("Microsoft.549981C3F5F10", "Cortana (приложение)", "Отдельное приложение Cortana, независимое от голосового ассистента в поиске.", RiskLevel.Safe),
    ];
}
