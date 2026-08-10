using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>The bulk of the tweak list. Everything here is a plain registry write, so it is
/// described as data rather than as one class per value. Tweaks whose effect is disputed are kept
/// — the customer asked for them — but their TradeOff says so outright instead of quietly implying
/// a benefit that measurements do not show.</summary>
public static class ExtraTweaksCatalog
{
    private const string ContentDelivery =
        @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";

    private const string Advanced =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    private const string Unproven = "Эффект не доказан: замеров, подтверждающих прирост, нет.";

    public static IEnumerable<IOptimization> Build(IRegistryProvider registry)
    {
        SimpleRegistryOptimization Switch(
            string id, string name, string description, RiskLevel risk, string category,
            IReadOnlyList<RegistryTarget> targets, string? tradeOff = null, bool restart = false) =>
            new(id, name, description, risk, category, targets, registry, tradeOff, restart);

        static RegistryTarget Dword(RegistryHive hive, string key, string value, int on, int off) =>
            new(hive, key, value, on, RegistryValueKind.DWord, off);

        static RegistryTarget Str(RegistryHive hive, string key, string value, string on, string off) =>
            new(hive, key, value, on, RegistryValueKind.String, off);

        const RegistryHive user = RegistryHive.CurrentUser;
        const RegistryHive machine = RegistryHive.LocalMachine;

        // ---------- Приватность ----------

        yield return Switch(
            "privacy-feedback-frequency",
            "Опросы «Как вам Windows?»: отключить",
            "Windows периодически показывает опрос об удовлетворённости системой. Твик выставляет частоту в ноль.",
            RiskLevel.Safe, Categories.Privacy,
            [Dword(user, @"Software\Microsoft\Siuf\Rules", "NumberOfSIUFInPeriod", 0, 1)]);

        yield return Switch(
            "privacy-start-suggestions",
            "Рекомендации и реклама приложений в меню «Пуск»",
            "Убирает предложения приложений в «Пуске», подсказки Windows, рекламу на экране блокировки " +
            "и тихую фоновую установку рекомендованных приложений.",
            RiskLevel.Safe, Categories.Privacy,
            [
                Dword(user, ContentDelivery, "SystemPaneSuggestionsEnabled", 0, 1),
                Dword(user, ContentDelivery, "SilentInstalledAppsEnabled", 0, 1),
                Dword(user, ContentDelivery, "SoftLandingEnabled", 0, 1),
                Dword(user, ContentDelivery, "RotatingLockScreenOverlayEnabled", 0, 1),
                Dword(user, ContentDelivery, "SubscribedContent-338389Enabled", 0, 1),
                Dword(user, ContentDelivery, "SubscribedContent-338393Enabled", 0, 1),
                Dword(user, ContentDelivery, "SubscribedContent-353694Enabled", 0, 1),
                Dword(user, ContentDelivery, "SubscribedContent-353696Enabled", 0, 1),
            ]);

        yield return Switch(
            "privacy-inking-typing",
            "Сбор рукописного и печатного ввода: отключить",
            "Windows собирает образцы того, что вы печатаете, чтобы улучшать словарь. Твик это запрещает.",
            RiskLevel.Safe, Categories.Privacy,
            [
                Dword(user, @"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1, 0),
                Dword(user, @"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1, 0),
                Dword(user, @"Software\Microsoft\Personalization\Settings", "AcceptedPrivacyPolicy", 0, 1),
            ],
            "Автодополнение и исправления при вводе станут хуже подстраиваться под вас.");

        yield return Switch(
            "privacy-online-speech",
            "Распознавание речи через облако: отключить",
            "Отключает отправку голоса в облако Microsoft. Локальное распознавание продолжает работать.",
            RiskLevel.Safe, Categories.Privacy,
            [Dword(user, @"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted", 0, 1)],
            "Голосовой ввод и диктовка перестанут работать точнее локального движка.");

        yield return Switch(
            "privacy-location-deny",
            "Доступ к геолокации: запретить всем приложениям",
            "Ставит общий запрет на определение местоположения на уровне системы.",
            RiskLevel.Caution, Categories.Privacy,
            [
                Str(machine,
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location",
                    "Value", "Deny", "Allow"),
            ],
            "«Погода», «Карты» и поиск ближайших мест перестанут знать, где вы находитесь.");

        yield return Switch(
            "privacy-error-reporting",
            "Отправка отчётов об ошибках в Microsoft: отключить",
            "Windows Error Reporting перестаёт отсылать дампы упавших программ.",
            RiskLevel.Caution, Categories.Privacy,
            [Dword(machine, @"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1, 0)],
            "Производители драйверов и игр не увидят ваших падений — чинить их будет некому.");

        yield return Switch(
            "privacy-clipboard-history",
            "История буфера обмена и синхронизация с облаком: отключить",
            "Windows хранит последние скопированные фрагменты и может синхронизировать их между устройствами.",
            RiskLevel.Caution, Categories.Privacy,
            [
                Dword(user, @"Software\Microsoft\Clipboard", "EnableClipboardHistory", 0, 1),
                Dword(user, @"Software\Microsoft\Clipboard", "CloudClipboardAutomaticUpload", 0, 1),
            ],
            "Win+V перестанет показывать историю копирования.");

        // ---------- Обновления и магазин ----------

        yield return Switch(
            "update-delivery-optimization",
            "Раздача обновлений другим ПК (P2P): отключить",
            "Delivery Optimization по умолчанию раздаёт скачанные обновления в интернет. Твик оставляет " +
            "только скачивание напрямую с серверов Microsoft.",
            RiskLevel.Safe, Categories.Network,
            [
                Dword(machine, @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization",
                    "DODownloadMode", 0, 1),
            ],
            "Обновления могут качаться чуть дольше, зато исходящий канал остаётся свободным для игры.");

        yield return Switch(
            "update-no-auto-reboot",
            "Автоматическая перезагрузка после обновлений при активном сеансе: запретить",
            "Windows не станет перезагружать компьютер, пока пользователь залогинен.",
            RiskLevel.Safe, Categories.WindowsBase,
            [
                Dword(machine, @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU",
                    "NoAutoRebootWithLoggedOnUsers", 1, 0),
            ]);

        yield return Switch(
            "store-auto-update-off",
            "Автообновление приложений из Microsoft Store: отключить",
            "Store перестаёт качать обновления приложений в фоне.",
            RiskLevel.Caution, Categories.Apps,
            [Dword(machine, @"SOFTWARE\Policies\Microsoft\WindowsStore", "AutoDownload", 2, 4)],
            "Приложения, включая Xbox и игровые сервисы, придётся обновлять вручную.");

        // ---------- Интерфейс и Проводник ----------

        yield return Switch(
            "explorer-show-extensions",
            "Показывать расширения файлов",
            "Проводник перестаёт прятать .exe, .scr и прочее — базовая защита от файлов, " +
            "притворяющихся картинками.",
            RiskLevel.Safe, Categories.WindowsBase,
            [Dword(user, Advanced, "HideFileExt", 0, 1)]);

        yield return Switch(
            "explorer-launch-to-thispc",
            "Открывать Проводник на «Этот компьютер», а не на «Главная»",
            "Возвращает привычный экран с дисками вместо ленты недавних файлов.",
            RiskLevel.Safe, Categories.WindowsBase,
            [Dword(user, Advanced, "LaunchTo", 1, 2)]);

        yield return Switch(
            "explorer-taskview-button",
            "Кнопка «Представление задач» на панели задач: убрать",
            "Прячет кнопку. Сам Alt+Tab и Win+Tab продолжают работать.",
            RiskLevel.Safe, Categories.WindowsBase,
            [Dword(user, Advanced, "ShowTaskViewButton", 0, 1)]);

        yield return Switch(
            "explorer-search-box",
            "Поле поиска на панели задач: убрать",
            "Освобождает место на панели. Поиск по-прежнему открывается по Win+S.",
            RiskLevel.Safe, Categories.WindowsBase,
            [Dword(user, @"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 0, 1)]);

        yield return Switch(
            "explorer-web-search-off",
            "Веб-результаты в поиске «Пуска»: убрать",
            "Поиск перестаёт лезть в интернет и начинает отвечать мгновенно из локального индекса.",
            RiskLevel.Safe, Categories.WindowsBase,
            [
                Dword(user, @"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, 0),
            ]);

        yield return Switch(
            "explorer-sync-notifications",
            "Реклама OneDrive и подсказки в Проводнике: убрать",
            "«Уведомления поставщика синхронизации» — это баннеры OneDrive и Office внутри окна Проводника.",
            RiskLevel.Safe, Categories.WindowsBase,
            [Dword(user, Advanced, "ShowSyncProviderNotifications", 0, 1)]);

        yield return Switch(
            "explorer-aero-shake-off",
            "Aero Shake (свернуть всё встряхиванием окна): отключить",
            "Убирает случайное сворачивание всех окон при резком движении мышью с зажатым заголовком.",
            RiskLevel.Safe, Categories.WindowsBase,
            [Dword(user, Advanced, "DisallowShaking", 1, 0)]);

        yield return Switch(
            "explorer-verbose-status",
            "Подробные сообщения при входе и выключении",
            "Вместо «Пожалуйста, подождите» Windows пишет, чем именно она занята. Помогает найти, " +
            "что тормозит загрузку.",
            RiskLevel.Safe, Categories.WindowsBase,
            [
                Dword(machine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                    "VerboseStatus", 1, 0),
            ]);

        yield return Switch(
            "explorer-classic-context-menu",
            "Windows 11: вернуть полное контекстное меню (без «Показать доп. параметры»)",
            "Убирает урезанное меню Windows 11 и возвращает старое, где сразу видны все пункты.",
            RiskLevel.Caution, Categories.WindowsBase,
            [
                Str(user,
                    @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32",
                    string.Empty, string.Empty, "-"),
            ],
            "Требуется перезапуск Проводника. Отдельные приложения Windows 11 рассчитывают на новое меню.",
            restart: true);

        yield return Switch(
            "ui-transparency-off",
            "Эффекты прозрачности: отключить",
            "Отключает размытие и прозрачность в панели задач и меню. Немного разгружает видеокарту " +
            "на рабочем столе.",
            RiskLevel.Safe, Categories.Display,
            [
                Dword(user, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "EnableTransparency", 0, 1),
            ],
            "В играх это ничего не даст — эффекты и так не работают в полноэкранном режиме.");

        yield return Switch(
            "ui-window-animations-off",
            "Анимации сворачивания и разворачивания окон: отключить",
            "Окна начинают появляться мгновенно. Заметно на рабочем столе, особенно при Alt+Tab из игры.",
            RiskLevel.Safe, Categories.Display,
            [Str(user, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", "1")]);

        // ---------- Ввод ----------

        yield return Switch(
            "input-sticky-keys-prompt",
            "Окно «Включить залипание клавиш?» по пяти Shift: отключить",
            "Убирает всплывающее окно, которое в игре сворачивает всё на рабочий стол.",
            RiskLevel.Safe, Categories.Input,
            [
                Str(user, @"Control Panel\Accessibility\StickyKeys", "Flags", "506", "510"),
                Str(user, @"Control Panel\Accessibility\Keyboard Response", "Flags", "122", "126"),
                Str(user, @"Control Panel\Accessibility\ToggleKeys", "Flags", "58", "62"),
            ]);

        // ---------- Загрузка и питание ----------

        yield return Switch(
            "boot-fast-startup-off",
            "Быстрый запуск (гибридное выключение): отключить",
            "При быстром запуске «выключение» на самом деле усыпляет ядро. Это переносит из сеанса в сеанс " +
            "утечки драйверов и мешает диагностике.",
            RiskLevel.Caution, Categories.Power,
            [
                Dword(machine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Power",
                    "HiberbootEnabled", 0, 1),
            ],
            "Холодная загрузка станет на несколько секунд дольше.",
            restart: true);

        yield return Switch(
            "boot-startup-delay-off",
            "Задержка запуска автозагрузки: убрать",
            "Windows намеренно откладывает программы автозагрузки примерно на 10 секунд после входа.",
            RiskLevel.Caution, Categories.Startup,
            [
                Dword(user,
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize",
                    "StartupDelayInMSec", 0, 1),
            ],
            "Рабочий стол станет отзывчивым позже: всё стартует разом и борется за диск.");

        // ---------- Твики со значением ----------

        yield return new SimpleChoiceOptimization(
            "choice-visual-effects",
            "Визуальные эффекты: набор",
            "То же, что вкладка «Быстродействие» в свойствах системы, но одним переключателем.",
            RiskLevel.Safe, Categories.Display,
            user, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
            "VisualFXSetting", RegistryValueKind.DWord,
            [
                (new TweakChoice("auto", "Автоматически (как в Windows)",
                    "Windows сама решает по мощности машины.", IsWindowsDefault: true), 0),
                (new TweakChoice("best-look", "Наилучший вид",
                    "Все эффекты включены."), 1),
                (new TweakChoice("best-perf", "Наилучшее быстродействие",
                    "Все эффекты выключены. Интерфейс резкий и голый.", IsRecommended: true), 2),
                (new TweakChoice("custom", "Пользовательский набор",
                    "Оставляет то, что настроено вручную."), 3),
            ],
            registry);

        yield return new SimpleChoiceOptimization(
            "choice-keyboard-delay",
            "Задержка перед автоповтором клавиши",
            "Сколько держать клавишу, прежде чем символ начнёт повторяться.",
            RiskLevel.Safe, Categories.Input,
            user, @"Control Panel\Keyboard", "KeyboardDelay", RegistryValueKind.String,
            [
                (new TweakChoice("0", "Минимальная (~250 мс)",
                    "Самый быстрый повтор.", IsRecommended: true), "0"),
                (new TweakChoice("1", "Короткая (~500 мс) — как в Windows",
                    "Значение по умолчанию.", IsWindowsDefault: true), "1"),
                (new TweakChoice("2", "Средняя (~750 мс)", "Компромисс."), "2"),
                (new TweakChoice("3", "Длинная (~1000 мс)",
                    "Для тех, кто задевает клавиши."), "3"),
            ],
            registry);

        yield return new SimpleChoiceOptimization(
            "choice-svchost-split",
            "Порог разделения svchost по объёму памяти",
            "Windows запускает каждую службу отдельным процессом, если оперативной памяти больше порога. " +
            "Порог задан в килобайтах и по умолчанию равен ~3,5 ГБ, поэтому на любой современной машине " +
            "процессов svchost десятки.",
            RiskLevel.Advanced, Categories.Storage,
            machine, @"SYSTEM\CurrentControlSet\Control", "SvcHostSplitThresholdInKB",
            RegistryValueKind.DWord,
            [
                (new TweakChoice("default", "3,5 ГБ — как в Windows",
                    "Службы разделены по процессам: падение одной не роняет остальные.",
                    IsWindowsDefault: true, IsRecommended: true), 0x380000),
                (new TweakChoice("group-16", "Группировать при памяти до 16 ГБ",
                    "Меньше процессов svchost, меньше накладных расходов на диспетчеризацию."), 0x1000000),
                (new TweakChoice("group-always", "Группировать всегда",
                    "Максимальная группировка служб в общие процессы."), unchecked((int)0xFFFFFFFF)),
            ],
            registry,
            "Сгруппированные службы теряют изоляцию: сбой одной может утащить соседние, " +
            "и в диспетчере задач станет труднее понять, какая именно служба грузит систему.",
            requiresRestart: true);

        yield return new SimpleChoiceOptimization(
            "choice-prefetcher",
            "Prefetcher: режим",
            "Предзагрузка часто используемых файлов. На SSD выигрыш околонулевой, на HDD — заметный.",
            RiskLevel.Caution, Categories.Storage,
            machine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters",
            "EnablePrefetcher", RegistryValueKind.DWord,
            [
                (new TweakChoice("off", "Выключен", "Ничего не предзагружается."), 0),
                (new TweakChoice("apps", "Только приложения", "Предзагрузка запуска программ."), 1),
                (new TweakChoice("boot", "Только загрузка системы", "Предзагрузка при старте Windows."), 2),
                (new TweakChoice("both", "Приложения и загрузка — как в Windows",
                    "Значение по умолчанию.", IsWindowsDefault: true, IsRecommended: true), 3),
            ],
            registry,
            Unproven + " Отключение Prefetcher на SSD — популярный совет, но замеры прироста не показывают, " +
            "а на HDD он делает загрузку ощутимо медленнее.",
            requiresRestart: true);
    }
}
