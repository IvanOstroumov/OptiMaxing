using System.Collections.ObjectModel;
using System.Windows;
using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Services;

namespace OptiMaxing.App.ViewModels;

public sealed class ServiceEntryRow(ServiceRow row) : ObservableObject
{
    public ServiceRow Row { get; } = row;

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

        Refresh();
    }

    public ObservableCollection<ServiceEntryRow> Entries { get; } = [];

    public RelayCommand RefreshCommand { get; }
    public RelayCommand DisableCommand { get; }
    public RelayCommand ManualCommand { get; }
    public RelayCommand AutomaticCommand { get; }
    public RelayCommand ToggleRunningCommand { get; }

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

    private void Refresh(string? message = null)
    {
        var all = _services.List();
        var selectedName = Selected?.Name;

        Entries.Clear();
        foreach (var row in all)
        {
            if (OnlyRecommended && row.Safety != ServiceSafety.SafeToDisable)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(Filter)
                && !row.Info.DisplayName.Contains(Filter, StringComparison.CurrentCultureIgnoreCase)
                && !row.Info.Name.Contains(Filter, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            Entries.Add(new ServiceEntryRow(row));
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

    private static bool Confirm(string message) =>
        MessageBox.Show(message, "OptiMaxing", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            == MessageBoxResult.Yes;
}
