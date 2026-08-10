using System.Collections.ObjectModel;
using System.Windows;
using OptiMaxing.Core.Programs;

namespace OptiMaxing.App.ViewModels;

public sealed class ProgramRow(InstalledProgram program) : ObservableObject
{
    public InstalledProgram Program { get; } = program;

    public string Name => Program.Name;
    public string Publisher => Program.Publisher ?? "издатель не указан";
    public string Version => Program.Version ?? string.Empty;
    public string SizeText => Program.SizeText;
    public string InstallDateText => Program.InstallDate?.ToString("dd.MM.yyyy") ?? "—";
    public string Details => $"{Publisher} · {Program.ScopeText}"
                             + (Program.Version is null ? string.Empty : $" · версия {Program.Version}");
    public bool CanUninstall => Program.CanUninstall;
    public bool HasNoUninstaller => !Program.CanUninstall;
    public bool IsSystemComponent => Program.IsSystemComponent;
}

/// <summary>Backs the "Программы" tab. Uninstalling runs the vendor's own uninstaller — OptiMaxing
/// never deletes program files itself, so nothing is left half-removed with dead registry entries.</summary>
public sealed class ProgramsViewModel : ObservableObject
{
    private readonly InstalledProgramsService _programs;
    private ProgramRow? _selected;
    private string _statusText = string.Empty;
    private string _filter = string.Empty;
    private bool _showSystemComponents;
    private bool _isBusy;

    public ProgramsViewModel(InstalledProgramsService programs)
    {
        _programs = programs;

        RefreshCommand = new RelayCommand(() => { Refresh(); return Task.CompletedTask; }, () => !IsBusy);
        UninstallCommand = new RelayCommand(UninstallAsync, () => !IsBusy && Selected?.CanUninstall == true);

        Refresh();
    }

    public ObservableCollection<ProgramRow> Entries { get; } = [];

    public RelayCommand RefreshCommand { get; }
    public RelayCommand UninstallCommand { get; }

    public ProgramRow? Selected
    {
        get => _selected;
        set { if (SetField(ref _selected, value)) UninstallCommand.RaiseCanExecuteChanged(); }
    }

    public string Filter
    {
        get => _filter;
        set { if (SetField(ref _filter, value)) Refresh(); }
    }

    /// <summary>Off by default: most of the Uninstall hive is redistributables and update records
    /// that Settings hides too, and removing one of those breaks the program that installed it.</summary>
    public bool ShowSystemComponents
    {
        get => _showSystemComponents;
        set { if (SetField(ref _showSystemComponents, value)) Refresh(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
                UninstallCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    private void Refresh(string? message = null)
    {
        var all = _programs.List();
        var selectedId = Selected?.Program.Id;

        Entries.Clear();
        foreach (var program in all)
        {
            if (!ShowSystemComponents && program.IsSystemComponent)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(Filter)
                && !program.Name.Contains(Filter, StringComparison.CurrentCultureIgnoreCase)
                && program.Publisher?.Contains(Filter, StringComparison.CurrentCultureIgnoreCase) != true)
            {
                continue;
            }

            Entries.Add(new ProgramRow(program));
        }

        Selected = Entries.FirstOrDefault(e => e.Program.Id == selectedId);

        var totalSize = Entries.Sum(e => e.Program.EstimatedSizeBytes ?? 0) / 1024.0 / 1024.0 / 1024.0;
        StatusText = message ?? $"Программ: {all.Count}, показано: {Entries.Count}, " +
                                $"занимают около {totalSize:N1} ГБ";
    }

    private async Task UninstallAsync()
    {
        if (Selected is not { } row)
        {
            return;
        }

        var answer = MessageBox.Show(
            $"Удалить «{row.Name}»?\n\n" +
            "Запустится собственный деинсталлятор программы — он может задать свои вопросы " +
            "и потребовать перезагрузку. Сохранения и настройки программа удаляет на своё усмотрение.",
            "OptiMaxing",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        StatusText = $"Запущен деинсталлятор «{row.Name}». Дождись его завершения…";

        try
        {
            var result = await _programs.UninstallAsync(row.Program, quiet: false, CancellationToken.None);

            // A non-zero exit code usually means the user cancelled the uninstaller, which is not an
            // error worth alarming them about — so the wording stays neutral either way.
            Refresh(result.Succeeded
                ? $"Деинсталлятор «{row.Name}» завершился. Список обновлён."
                : $"Деинсталлятор «{row.Name}» завершился с кодом {result.ExitCode} " +
                  "(возможно, удаление отменено).");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
