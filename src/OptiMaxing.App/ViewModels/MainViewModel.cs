using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using OptiMaxing.Core.Crashes;
using OptiMaxing.Core.Engine;
using OptiMaxing.Core.Files;
using OptiMaxing.Core.Model;
using OptiMaxing.Core.Optimizations;
using OptiMaxing.Core.Optimizations.Catalog;
using OptiMaxing.Core.Programs;
using OptiMaxing.Core.Safety;
using OptiMaxing.Core.Services;
using OptiMaxing.Core.Startup;

namespace OptiMaxing.App.ViewModels;

/// <summary>One row in the left-side category sidebar: category name (or AllCategories) and
/// how many tweaks fall into it, for the "tree with counts" from the original spec. Categories
/// are flat (no sub-categories exist in the catalog), so a single-level list serves the same
/// purpose as a tree without the added complexity of a real TreeView/HierarchicalDataTemplate.</summary>
public sealed record CategoryEntry(string Name, int Count);

public sealed class MainViewModel : ObservableObject
{
    private readonly OptimizationEngine _engine;
    private readonly IRestorePointService _restorePoints;
    private readonly List<OptimizationViewModel> _all;

    private string _searchQuery = string.Empty;
    private string? _selectedCategory;
    private RiskLevel? _riskFilter;
    private string _statusText = "Готово";
    private bool _isBusy;
    private string _restorePointBanner = string.Empty;
    private bool _advancedUnlocked;

    public AdvisoryViewModel Advisory { get; } = new();
    public SystemHealthViewModel Health { get; }
    public StartupViewModel Startup { get; }
    public ProcessesViewModel Processes { get; }
    public ServicesViewModel Services { get; }
    public ProgramsViewModel Programs { get; }
    public CrashHistoryViewModel CrashHistory { get; }
    public FileFinderViewModel FileFinder { get; }
    public DiskUsageViewModel DiskUsage { get; }

    public MainViewModel(
        OptimizationCatalog catalog,
        OptimizationEngine engine,
        IRestorePointService restorePoints,
        SystemHealthService health,
        SensorMonitor sensors,
        StartupInventoryService startup,
        ProcessMonitor processes,
        ServiceInventoryService services,
        InstalledProgramsService programs,
        CrashHistoryService crashHistory,
        FileFinderService fileFinder,
        DiskTreeService diskTree)
    {
        Programs = new ProgramsViewModel(programs);
        Startup = new StartupViewModel(startup);
        Processes = new ProcessesViewModel(processes);
        Services = new ServicesViewModel(services);
        CrashHistory = new CrashHistoryViewModel(crashHistory);
        FileFinder = new FileFinderViewModel(fileFinder);
        DiskUsage = new DiskUsageViewModel(diskTree);
        _engine = engine;
        _restorePoints = restorePoints;
        Health = new SystemHealthViewModel(health, sensors);

        _all = catalog.BuildAll().Select(o => new OptimizationViewModel(o)).ToList();

        // Ticking a checkbox changes state on the item, not on this view model, so neither the
        // "Выбрано: N" counter nor the Apply/Revert CanExecute would ever re-evaluate without
        // listening in. RelayCommand is hand-rolled and does not hook CommandManager.
        foreach (var item in _all)
        {
            item.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(OptimizationViewModel.IsSelected))
                {
                    OnPropertyChanged(nameof(SelectionSummary));
                    RefreshCommands();
                }
            };
        }

        var perCategory = _all.GroupBy(o => o.Category).OrderBy(g => g.Key);
        CategoryEntries = new ObservableCollection<CategoryEntry>(
            new[] { new CategoryEntry(AllCategories, _all.Count) }
                .Concat(perCategory.Select(g => new CategoryEntry(g.Key, g.Count()))));

        Visible = new ObservableCollection<OptimizationViewModel>(_all);

        ScanCommand = new RelayCommand(ScanAsync, () => !IsBusy);
        ApplyCommand = new RelayCommand(ApplyAsync, () => !IsBusy && SelectedItems.Count > 0);
        RevertCommand = new RelayCommand(RevertAsync, () => !IsBusy && SelectedItems.Count > 0);
        CreateRestorePointCommand = new RelayCommand(CreateRestorePointAsync, () => !IsBusy);
        SelectSafePresetCommand = new RelayCommand(() => ApplyPreset(item => item.Risk == RiskLevel.Safe));

        SelectPerformancePresetCommand = new RelayCommand(() => ApplyPreset(item =>
            item.Risk != RiskLevel.Advanced && item.Risk != RiskLevel.Advisory
            && PerformanceCategories.Contains(item.Category)));

        SelectPrivacyPresetCommand = new RelayCommand(() => ApplyPreset(item =>
            item.Risk != RiskLevel.Advanced && item.Risk != RiskLevel.Advisory
            && item.Category == Categories.Privacy));

        SelectCleanupPresetCommand = new RelayCommand(() => ApplyPreset(item =>
            item.Risk != RiskLevel.Advanced && item.Risk != RiskLevel.Advisory
            && (item.Category == Categories.Apps || item.Category == Categories.Cleanup)));

        ClearSelectionCommand = new RelayCommand(() => ApplyPreset(_ => false));

        ExportPresetCommand = new RelayCommand(ExportPresetAsync);
        ImportPresetCommand = new RelayCommand(ImportPresetAsync);

        ToggleThemeCommand = new RelayCommand(() =>
        {
            ThemeService.Toggle();
            OnPropertyChanged(nameof(ThemeToggleText));
            return Task.CompletedTask;
        });
    }

    public RelayCommand ToggleThemeCommand { get; }

    public string ThemeToggleText => ThemeService.Current == AppTheme.Dark
        ? "Светлая тема"
        : "Тёмная тема";

    private sealed record TweakExportEntry(string Id, bool IsSelected, string? SelectedChoiceId);

    private Task ExportPresetAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = "optimaxing-preset.json",
        };

        if (dialog.ShowDialog() != true)
        {
            return Task.CompletedTask;
        }

        var entries = _all
            .Select(o => new TweakExportEntry(o.Model.Id, o.IsSelected, o.Choice?.SelectedChoiceId))
            .ToList();

        try
        {
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dialog.FileName, json);
            StatusText = $"Набор твиков сохранён: {dialog.FileName}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"Не удалось сохранить файл: {ex.Message}", "OptiMaxing",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return Task.CompletedTask;
    }

    private Task ImportPresetAsync()
    {
        var dialog = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };

        if (dialog.ShowDialog() != true)
        {
            return Task.CompletedTask;
        }

        List<TweakExportEntry>? entries;
        try
        {
            var json = File.ReadAllText(dialog.FileName);
            entries = JsonSerializer.Deserialize<List<TweakExportEntry>>(json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            MessageBox.Show($"Не удалось загрузить файл: {ex.Message}", "OptiMaxing",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return Task.CompletedTask;
        }

        if (entries is null)
        {
            return Task.CompletedTask;
        }

        var byId = entries.ToDictionary(e => e.Id);
        var matched = 0;

        foreach (var item in _all)
        {
            if (!byId.TryGetValue(item.Model.Id, out var entry))
            {
                item.IsSelected = false;
                continue;
            }

            matched++;
            item.IsSelected = entry.IsSelected;

            if (entry.SelectedChoiceId is not null && item.Choice is not null)
            {
                var choice = item.Choice.Choices.FirstOrDefault(c => c.Id == entry.SelectedChoiceId);
                if (choice is not null)
                {
                    item.SelectedChoice = choice;
                }
            }
        }

        RefreshCommands();
        StatusText = $"Набор твиков загружен: {matched} из {entries.Count} пунктов найдены в текущем каталоге.";
        return Task.CompletedTask;
    }

    // Categories.Power/Gpu/Storage/Network/Startup/Services cover the tweaks that
    // actually move the needle for gaming performance; Privacy/Apps/Cleanup presets deliberately
    // stay narrow to their own category so each preset button does one clearly-named thing.
    private static readonly HashSet<string> PerformanceCategories =
    [
        Categories.Power,
        Categories.Gpu,
        Categories.Storage,
        Categories.Network,
        Categories.Startup,
        Categories.Services,
    ];

    private Task ApplyPreset(Func<OptimizationViewModel, bool> predicate)
    {
        foreach (var item in _all)
            item.IsSelected = predicate(item);
        RefreshCommands();
        return Task.CompletedTask;
    }

    public const string AllCategories = "Все категории";

    /// <summary>Left-side category tree: name + count of tweaks in it, computed once from the
    /// full catalog (counts don't change as selection/state changes, only as filter changes).</summary>
    public ObservableCollection<CategoryEntry> CategoryEntries { get; }
    public ObservableCollection<OptimizationViewModel> Visible { get; }
    public ObservableCollection<string> LogLines { get; } = [];

    public RelayCommand ScanCommand { get; }
    public RelayCommand ApplyCommand { get; }
    public RelayCommand RevertCommand { get; }
    public RelayCommand CreateRestorePointCommand { get; }
    public RelayCommand SelectSafePresetCommand { get; }
    public RelayCommand SelectPerformancePresetCommand { get; }
    public RelayCommand SelectPrivacyPresetCommand { get; }
    public RelayCommand SelectCleanupPresetCommand { get; }
    public RelayCommand ClearSelectionCommand { get; }
    public RelayCommand ExportPresetCommand { get; }
    public RelayCommand ImportPresetCommand { get; }

    public IReadOnlyList<OptimizationViewModel> SelectedItems =>
        _all.Where(o => o.IsSelected).ToList();

    public string SelectionSummary => $"Выбрано: {_all.Count(o => o.IsSelected)} из {_all.Count}";

    public string SearchQuery
    {
        get => _searchQuery;
        set { if (SetField(ref _searchQuery, value)) ApplyFilter(); }
    }

    public string? SelectedCategory
    {
        get => _selectedCategory;
        set { if (SetField(ref _selectedCategory, value)) ApplyFilter(); }
    }

    public RiskLevel? RiskFilter
    {
        get => _riskFilter;
        set { if (SetField(ref _riskFilter, value)) ApplyFilter(); }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetField(ref _isBusy, value)) RefreshCommands(); }
    }

    public string RestorePointBanner
    {
        get => _restorePointBanner;
        private set => SetField(ref _restorePointBanner, value);
    }

    /// <summary>False until a restore point exists; gates the Advanced tier in the UI.</summary>
    public bool AdvancedUnlocked
    {
        get => _advancedUnlocked;
        private set => SetField(ref _advancedUnlocked, value);
    }

    public async Task InitializeAsync()
    {
        RefreshRestorePointBanner();
        await ScanAsync();
    }

    private void ApplyFilter()
    {
        var filtered = _all.Where(o =>
            o.Matches(SearchQuery)
            && (SelectedCategory is null or AllCategories || o.Category == SelectedCategory)
            && (RiskFilter is null || o.Risk == RiskFilter));

        Visible.Clear();
        foreach (var item in filtered)
            Visible.Add(item);
    }

    private void RefreshRestorePointBanner()
    {
        var status = _restorePoints.GetStatus();
        AdvancedUnlocked = status.LastRestorePointUtc is not null && !status.BlockedByPolicy;

        RestorePointBanner = status switch
        {
            { BlockedByPolicy: true } =>
                "Восстановление системы запрещено групповой политикой. Продвинутые твики останутся заблокированы.",
            { LastRestorePointUtc: null } =>
                "Точки восстановления нет. Создай её, чтобы разблокировать продвинутые твики.",
            { Age: { } age } =>
                $"Последняя точка восстановления создана {FormatAge(age)} назад.",
            _ => string.Empty,
        };
    }

    private static string FormatAge(TimeSpan age) => age switch
    {
        { TotalHours: < 1 } => $"{(int)age.TotalMinutes} мин",
        { TotalDays: < 1 } => $"{(int)age.TotalHours} ч",
        _ => $"{(int)age.TotalDays} дн",
    };

    private async Task ScanAsync()
    {
        IsBusy = true;
        StatusText = "Проверяю текущее состояние…";

        try
        {
            var progress = new Progress<(string Id, ApplyState State)>(update =>
            {
                var target = _all.FirstOrDefault(o => o.Model.Id == update.Id);
                if (target is not null)
                    target.State = update.State;
            });

            await _engine.ScanStatesAsync(_all.Select(o => o.Model).ToList(), progress, CancellationToken.None);

            // Choice tweaks additionally show the raw system value, which the engine's state scan
            // does not carry.
            foreach (var item in _all.Where(o => o.HasChoices))
            {
                await item.RefreshCurrentValueAsync();
            }

            StatusText = $"Проверено пунктов: {_all.Count}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplyAsync()
    {
        var selection = SelectedItems;

        var gate = _engine.CheckGate(selection.Select(o => o.Model).ToList());
        if (gate is not null)
        {
            MessageBox.Show(gate, "Заблокировано", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var antiCheatBlock = _engine.CheckAntiCheatConflicts(
            selection.Select(o => o.Model).ToList(), GameLibraryRoots());
        if (antiCheatBlock is not null)
        {
            MessageBox.Show(antiCheatBlock, "Заблокировано: конфликт с анти-читом", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var irreversible = selection.Where(o => o.Reversibility == Reversibility.Irreversible).ToList();
        if (irreversible.Count > 0)
        {
            var names = string.Join("\n  • ", irreversible.Select(o => o.DisplayName));
            var answer = MessageBox.Show(
                $"Эти действия НЕЛЬЗЯ отменить:\n  • {names}\n\nПродолжить?",
                "Необратимые действия",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
                return;
        }

        await RunBatchAsync("Применяю", (log, ct) =>
            _engine.ApplyBatchAsync(selection.Select(o => o.Model).ToList(), log, ct));
    }

    private async Task RevertAsync()
    {
        var selection = SelectedItems;
        await RunBatchAsync("Откатываю", (log, ct) =>
            _engine.RevertBatchAsync(selection.Select(o => o.Model).ToList(), log, ct));
    }

    private async Task RunBatchAsync(
        string verb,
        Func<IProgress<string>, CancellationToken, Task<BatchResult>> operation)
    {
        IsBusy = true;
        StatusText = $"{verb}…";
        LogLines.Clear();

        try
        {
            var log = new Progress<string>(line => LogLines.Add(line));
            var result = await operation(log, CancellationToken.None);

            StatusText = $"Успешно: {result.SucceededCount}, с ошибкой: {result.FailedCount}"
                         + (result.RestartRequired ? ". Нужна перезагрузка." : string.Empty);

            LogLines.Add(string.Empty);
            LogLines.Add(StatusText);
        }
        finally
        {
            IsBusy = false;
            await ScanAsync();
        }
    }

    private async Task CreateRestorePointAsync()
    {
        IsBusy = true;
        StatusText = "Создаю точку восстановления…";

        try
        {
            var created = await _restorePoints.CreateAsync("OptiMaxing", CancellationToken.None);
            StatusText = created
                ? "Точка восстановления создана."
                : "Не удалось создать точку восстановления (Windows разрешает не чаще одной за 24 часа).";
            RefreshRestorePointBanner();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static IEnumerable<string> GameLibraryRoots()
    {
        var drives = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed).Select(d => d.Name);

        foreach (var drive in drives)
        {
            yield return Path.Combine(drive, "Program Files (x86)", "Steam", "steamapps", "common");
            yield return Path.Combine(drive, "Program Files", "Epic Games");
            yield return Path.Combine(drive, "Program Files (x86)", "Battle.net");
            yield return Path.Combine(drive, "Games");
        }
    }

    private void RefreshCommands()
    {
        ScanCommand.RaiseCanExecuteChanged();
        ApplyCommand.RaiseCanExecuteChanged();
        RevertCommand.RaiseCanExecuteChanged();
        CreateRestorePointCommand.RaiseCanExecuteChanged();
    }
}
