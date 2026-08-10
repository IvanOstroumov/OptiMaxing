using System.Collections.ObjectModel;
using System.Windows;
using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Services;

namespace OptiMaxing.App.ViewModels;

public enum ServiceSort { Name, Status, StartupType }

public sealed class ServiceEntryRow(ServiceRow row) : ObservableObject
{
    private bool _isChecked;

    public ServiceRow Row { get; } = row;

    public bool IsChecked
    {
        get => _isChecked;
        set => SetField(ref _isChecked, value);
    }

    public string Name => Row.Info.Name;
    public string DisplayName => Row.Info.DisplayName;
    public string StartupText => ServiceInventoryService.Describe(Row.Info.StartupType);
    public string StateText => Row.Info.IsRunning ? "работает" : "остановлена";
    public bool IsRunning => Row.Info.IsRunning;
    public bool IsCritical => Row.Safety == ServiceSafety.Critical;
    public bool IsSafeToDisable => Row.Safety == ServiceSafety.SafeToDisable;
    public bool HasNote => Row.Note is not null;
    public string? Note => Row.Note;
    public string RunToggleText => Row.Info.IsRunning ? "Остановить" : "Запустить";
}

/// <summary>Backs the "Службы" tab. Unlike processes and startup entries, nothing here is ever
/// deleted: the destructive end of the scale is Start=Disabled, which one click undoes.</summary>
public sealed class ServicesViewModel : ObservableObject
{
    private readonly ServiceInventoryService _services;
    private ServiceEntryRow? _selected;
    private string _statusText = string.Empty;
    private string _filter = string.Empty;
    private bool _onlyRecommended;
    private ServiceSort _sortMode = ServiceSort.Name;

    public ServicesViewModel(ServiceInventoryService services)
    {
        _services = services;

        RefreshCommand = new RelayCommand(() => { Refresh(); return Task.CompletedTask; });
        DisableCommand = new RelayCommand(
            () => { SetStartup(ServiceStartupType.Disabled); return Task.CompletedTask; }, () => Selected is not null);
        ManualCommand = new RelayCommand(
            () => { SetStartup(ServiceStartupType.Manual); return Task.CompletedTask; }, () => Selected is not null);
        AutomaticCommand = new RelayCommand(
            () => { SetStartup(ServiceStartupType.Automatic); return Task.CompletedTask; }, () => Selected is not null);
        ToggleRunningCommand = new RelayCommand(
            () => { ToggleRunning(); return Task.CompletedTask; }, () => Selected is not null);

        DisableCheckedCommand = new RelayCommand(
            () => { SetStartupChecked(ServiceStartupType.Disabled); return Task.CompletedTask; }, () => Entries.Any(e => e.IsChecked));
        ManualCheckedCommand = new RelayCommand(
            () => { SetStartupChecked(ServiceStartupType.Manual); return Task.CompletedTask; }, () => Entries.Any(e => e.IsChecked));
        AutomaticCheckedCommand = new RelayCommand(
            () => { SetStartupChecked(ServiceStartupType.Automatic); return Task.CompletedTask; }, () => Entries.Any(e => e.IsChecked));

        Refresh();
    }

    public ObservableCollection<ServiceEntryRow> Entries { get; } = [];

    public RelayCommand RefreshCommand { get; }
    public RelayCommand DisableCommand { get; }
    public RelayCommand ManualCommand { get; }
    public RelayCommand AutomaticCommand { get; }
    public RelayCommand ToggleRunningCommand { get; }
    public RelayCommand DisableCheckedCommand { get; }
    public RelayCommand ManualCheckedCommand { get; }
    public RelayCommand AutomaticCheckedCommand { get; }

    public ServiceEntryRow? Selected
    {
        get => _selected;
        set
        {
            if (SetField(ref _selected, value))
            {
                DisableCommand.RaiseCanExecuteChanged();
                ManualCommand.RaiseCanExecuteChanged();
                AutomaticCommand.RaiseCanExecuteChanged();
                ToggleRunningCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(Selected));
            }
        }
    }

    public string Filter
    {
        get => _filter;
        set { if (SetField(ref _filter, value)) Refresh(); }
    }

    /// <summary>Narrows the list to services we have a documented reason to disable — the only ones
    /// a user can act on without research.</summary>
    public bool OnlyRecommended
    {
        get => _onlyRecommended;
        set { if (SetField(ref _onlyRecommended, value)) Refresh(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public ServiceSort SortMode
    {
        get => _sortMode;
        set { if (SetField(ref _sortMode, value)) Refresh(); }
    }

    public IReadOnlyList<SortOption<ServiceSort>> SortOptions { get; } =
    [
        new(ServiceSort.Name, "По имени"),
        new(ServiceSort.Status, "По состоянию (работает вперёд)"),
        new(ServiceSort.StartupType, "По типу запуска"),
    ];

    private void Refresh(string? message = null)
    {
        var all = _services.List();
        var selectedName = Selected?.Name;

        var filtered = all.Where(row =>
            (!OnlyRecommended || row.Safety == ServiceSafety.SafeToDisable)
            && (string.IsNullOrWhiteSpace(Filter)
                || row.Info.DisplayName.Contains(Filter, StringComparison.CurrentCultureIgnoreCase)
                || row.Info.Name.Contains(Filter, StringComparison.CurrentCultureIgnoreCase)));

        IEnumerable<ServiceRow> sorted = SortMode switch
        {
            ServiceSort.Status => filtered.OrderByDescending(r => r.Info.IsRunning)
                .ThenBy(r => r.Info.DisplayName, StringComparer.CurrentCultureIgnoreCase),
            ServiceSort.StartupType => filtered.OrderBy(r => r.Info.StartupType)
                .ThenBy(r => r.Info.DisplayName, StringComparer.CurrentCultureIgnoreCase),
            _ => filtered.OrderBy(r => r.Info.DisplayName, StringComparer.CurrentCultureIgnoreCase),
        };

        Entries.Clear();
        foreach (var row in sorted)
        {
            var entry = new ServiceEntryRow(row);
            entry.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ServiceEntryRow.IsChecked))
                    RefreshCheckedCommands();
            };
            Entries.Add(entry);
        }

        Selected = Entries.FirstOrDefault(e => e.Name == selectedName);

        StatusText = message ?? $"Служб: {all.Count}, показано: {Entries.Count}, " +
                                $"работает: {all.Count(r => r.Info.IsRunning)}, " +
                                $"отключено: {all.Count(r => r.Info.StartupType == ServiceStartupType.Disabled)}";
    }

    private void SetStartup(ServiceStartupType type)
    {
        if (Selected is not { } row)
        {
            return;
        }

        if (type == ServiceStartupType.Disabled && row.IsCritical && !Confirm(
                $"«{row.DisplayName}» — критичная служба Windows.\n\n" +
                "Её отключение может сломать загрузку, вход в систему, звук, сеть или защиту.\n\n" +
                "Всё равно отключить?"))
        {
            return;
        }

        var result = _services.SetStartupType(row.Row, type);
        Refresh(result.Message);
    }

    private void ToggleRunning()
    {
        if (Selected is not { } row)
        {
            return;
        }

        var stopping = row.IsRunning;
        if (stopping && row.IsCritical && !Confirm(
                $"«{row.DisplayName}» — критичная служба Windows.\n\n" +
                "Её остановка может немедленно сломать звук, сеть или вход в систему.\n\n" +
                "Всё равно остановить?"))
        {
            return;
        }

        var result = _services.SetRunning(row.Row, !stopping);
        Refresh(result.Message);
    }

    private void SetStartupChecked(ServiceStartupType type)
    {
        var checkedRows = Entries.Where(e => e.IsChecked).ToList();
        if (checkedRows.Count == 0)
        {
            return;
        }

        var anyCritical = checkedRows.Any(r => r.IsCritical);
        if (type == ServiceStartupType.Disabled && anyCritical)
        {
            var names = string.Join("\n  • ", checkedRows.Where(r => r.IsCritical).Select(r => r.DisplayName));
            if (!Confirm(
                $"Среди отмеченных служб есть критичные для Windows:\n  • {names}\n\n" +
                "Их отключение может сломать загрузку, вход в систему, звук, сеть или защиту.\n\n" +
                "Всё равно отключить все отмеченные службы?"))
            {
                return;
            }
        }

        var appliedCount = 0;
        foreach (var entry in checkedRows)
        {
            var result = _services.SetStartupType(entry.Row, type);
            if (result.Succeeded)
                appliedCount++;
        }

        Refresh($"Тип запуска изменён у {appliedCount} из {checkedRows.Count} отмеченных служб.");
    }

    private void RefreshCheckedCommands()
    {
        DisableCheckedCommand.RaiseCanExecuteChanged();
        ManualCheckedCommand.RaiseCanExecuteChanged();
        AutomaticCheckedCommand.RaiseCanExecuteChanged();
    }

    private static bool Confirm(string message) =>
        MessageBox.Show(message, "OptiMaxing", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            == MessageBoxResult.Yes;
}
