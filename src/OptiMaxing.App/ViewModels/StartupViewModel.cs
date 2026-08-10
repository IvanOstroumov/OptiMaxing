using System.Collections.ObjectModel;
using System.Windows;
using OptiMaxing.Core.Startup;

namespace OptiMaxing.App.ViewModels;

public sealed class StartupEntryRow(StartupEntry entry) : ObservableObject
{
    public StartupEntry Entry { get; private set; } = entry;

    public string Name => Entry.Name;
    public string Command => Entry.Command;
    public string Origin => $"{Entry.SourceText} · {Entry.ScopeText}";
    public bool IsEnabled => Entry.IsEnabled;
    public bool IsCritical => Entry.IsCritical;
    public bool IsBroken => !Entry.TargetExists;
    public string StateText => Entry.IsEnabled ? "включено" : "отключено";
    public string ToggleText => Entry.IsEnabled ? "Отключить" : "Включить";

    public void Replace(StartupEntry updated)
    {
        Entry = updated;
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(ToggleText));
    }
}

/// <summary>Backs the "Автозапуск" tab. Every destructive action goes through a confirmation
/// rather than being blocked: the user asked to be warned, not prevented.</summary>
public sealed class StartupViewModel : ObservableObject
{
    private readonly StartupInventoryService _startup;
    private StartupEntryRow? _selected;
    private string _statusText = string.Empty;

    public StartupViewModel(StartupInventoryService startup)
    {
        _startup = startup;

        RefreshCommand = new RelayCommand(() =>
        {
            Refresh();
            return Task.CompletedTask;
        });

        ToggleCommand = new RelayCommand(ToggleAsync, () => Selected is not null);
        DeleteCommand = new RelayCommand(DeleteAsync, () => Selected is not null);

        Refresh();
    }

    public ObservableCollection<StartupEntryRow> Entries { get; } = [];

    public RelayCommand RefreshCommand { get; }
    public RelayCommand ToggleCommand { get; }
    public RelayCommand DeleteCommand { get; }

    public StartupEntryRow? Selected
    {
        get => _selected;
        set
        {
            if (SetField(ref _selected, value))
            {
                ToggleCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    private void Refresh()
    {
        var selectedId = Selected?.Entry.Id;

        Entries.Clear();
        foreach (var entry in _startup.List().OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Entries.Add(new StartupEntryRow(entry));
        }

        Selected = Entries.FirstOrDefault(e => e.Entry.Id == selectedId);

        var enabled = Entries.Count(e => e.IsEnabled);
        var broken = Entries.Count(e => e.IsBroken);
        StatusText = $"Записей: {Entries.Count}, включено: {enabled}" +
                     (broken > 0 ? $", с отсутствующим файлом: {broken}" : string.Empty);
    }

    private async Task ToggleAsync()
    {
        if (Selected is not { } row)
        {
            return;
        }

        var turningOff = row.IsEnabled;
        if (turningOff && row.IsCritical && !ConfirmCritical(
                $"«{row.Name}» запускается из системной папки Windows.\n\n" +
                "Отключение может сломать звук, ввод или защиту системы.\n\nВсё равно отключить?"))
        {
            return;
        }

        var result = await _startup.SetEnabledAsync(row.Entry, !row.IsEnabled, CancellationToken.None);
        Refresh();
        StatusText = result.Message;
    }

    private async Task DeleteAsync()
    {
        if (Selected is not { } row)
        {
            return;
        }

        // Deleting is not undoable — unlike the toggle, which only flips a flag Windows itself
        // maintains — so the wording here says so plainly instead of asking a generic "are you
        // sure". Broken entries are the safe case and get a milder prompt.
        var warning = row.IsBroken
            ? $"Запись «{row.Name}» ссылается на несуществующий файл. Удалить её?"
            : $"Удалить запись автозапуска «{row.Name}»?\n\n{row.Command}\n\n" +
              "Это необратимо: восстановить запись сможет только переустановка программы." +
              (row.IsCritical
                  ? "\n\nВНИМАНИЕ: запись системная. Удаление может сломать звук, ввод или защиту системы."
                  : string.Empty);

        if (!ConfirmCritical(warning))
        {
            return;
        }

        var result = await _startup.DeleteAsync(row.Entry, CancellationToken.None);
        Refresh();
        StatusText = result.Message;
    }

    private static bool ConfirmCritical(string message) =>
        MessageBox.Show(message, "OptiMaxing", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            == MessageBoxResult.Yes;
}
