using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using OptiMaxing.Core.Engine;
using OptiMaxing.Core.Model;
using OptiMaxing.Core.Optimizations;
using OptiMaxing.Core.Safety;

namespace OptiMaxing.App.ViewModels;

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

    public MainViewModel(
        OptimizationCatalog catalog,
        OptimizationEngine engine,
        IRestorePointService restorePoints)
    {
        _engine = engine;
        _restorePoints = restorePoints;

        _all = catalog.BuildAll().Select(o => new OptimizationViewModel(o)).ToList();

        Categories = new ObservableCollection<string>(
            new[] { AllCategories }.Concat(_all.Select(o => o.Category).Distinct().OrderBy(c => c)));

        Visible = new ObservableCollection<OptimizationViewModel>(_all);

        ScanCommand = new RelayCommand(ScanAsync, () => !IsBusy);
        ApplyCommand = new RelayCommand(ApplyAsync, () => !IsBusy && SelectedItems.Count > 0);
        RevertCommand = new RelayCommand(RevertAsync, () => !IsBusy && SelectedItems.Count > 0);
        CreateRestorePointCommand = new RelayCommand(CreateRestorePointAsync, () => !IsBusy);
        SelectSafePresetCommand = new RelayCommand(() =>
        {
            foreach (var item in _all)
                item.IsSelected = item.Risk == RiskLevel.Safe;
            RefreshCommands();
            return Task.CompletedTask;
        });
        ClearSelectionCommand = new RelayCommand(() =>
        {
            foreach (var item in _all)
                item.IsSelected = false;
            RefreshCommands();
            return Task.CompletedTask;
        });
    }

    public const string AllCategories = "Все категории";

    public ObservableCollection<string> Categories { get; }
    public ObservableCollection<OptimizationViewModel> Visible { get; }
    public ObservableCollection<string> LogLines { get; } = [];

    public RelayCommand ScanCommand { get; }
    public RelayCommand ApplyCommand { get; }
    public RelayCommand RevertCommand { get; }
    public RelayCommand CreateRestorePointCommand { get; }
    public RelayCommand SelectSafePresetCommand { get; }
    public RelayCommand ClearSelectionCommand { get; }

    public IReadOnlyList<OptimizationViewModel> SelectedItems =>
        _all.Where(o => o.IsSelected).ToList();

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
