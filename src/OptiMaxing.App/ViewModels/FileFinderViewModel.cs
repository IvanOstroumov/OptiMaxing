using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using OptiMaxing.Core.Files;

namespace OptiMaxing.App.ViewModels;

public enum FileFinderMode { Largest, Old, Duplicates }

public sealed class FoundFileRow(string path, long sizeBytes, DateTime lastWriteTimeUtc, string? note) : ObservableObject
{
    private bool _isChecked;

    public string Path { get; } = path;
    public long SizeBytes { get; } = sizeBytes;
    public DateTime LastWriteTimeUtc { get; } = lastWriteTimeUtc;
    public string? Note { get; } = note;

    public string Name => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? Path;
    public string SizeText => $"{SizeBytes / 1024.0 / 1024.0:N1} МБ";
    public string DateText => LastWriteTimeUtc.ToLocalTime().ToString("dd.MM.yyyy");

    public bool IsChecked
    {
        get => _isChecked;
        set => SetField(ref _isChecked, value);
    }
}

/// <summary>Backs the "Поиск файлов" tab: on-demand scan (not background) of a chosen root
/// directory for the largest files, files untouched for N+ years, and byte-identical duplicates.
/// Deletion is permanent (via IFileSystem.TryDeleteFile) — no recycle-bin dependency was added for
/// this, so the confirmation dialog says so explicitly rather than implying it's reversible.</summary>
public sealed class FileFinderViewModel : ObservableObject
{
    private readonly FileFinderService _service;

    private string _rootPath = @"C:\";
    private int _minAgeYears = 2;
    private FileFinderMode _mode = FileFinderMode.Largest;
    private bool _isBusy;
    private string _statusText = "Выбери папку и нажми «Сканировать».";
    private FileFinderReport? _report;

    public FileFinderViewModel(FileFinderService service)
    {
        _service = service;

        ScanCommand = new RelayCommand(ScanAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(RootPath));
        DeleteSelectedCommand = new RelayCommand(DeleteSelectedAsync, () => !IsBusy && Visible.Any(v => v.IsChecked));
        OpenFolderCommand = new RelayCommand<FoundFileRow>(row => { OpenFolder(row); return Task.CompletedTask; });

        SetRootDriveCCommand = new RelayCommand(() => { RootPath = @"C:\"; return Task.CompletedTask; });
        SetRootDownloadsCommand = new RelayCommand(() =>
        {
            RootPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            return Task.CompletedTask;
        });
        SetRootDocumentsCommand = new RelayCommand(() =>
        {
            RootPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Task.CompletedTask;
        });
    }

    public ObservableCollection<FoundFileRow> Visible { get; } = [];

    public RelayCommand ScanCommand { get; }
    public RelayCommand DeleteSelectedCommand { get; }
    public RelayCommand<FoundFileRow> OpenFolderCommand { get; }
    public RelayCommand SetRootDriveCCommand { get; }
    public RelayCommand SetRootDownloadsCommand { get; }
    public RelayCommand SetRootDocumentsCommand { get; }

    public IReadOnlyList<SortOption<FileFinderMode>> Modes { get; } =
    [
        new(FileFinderMode.Largest, "Самые большие"),
        new(FileFinderMode.Old, "Давно не открывались"),
        new(FileFinderMode.Duplicates, "Дубликаты"),
    ];

    public string RootPath
    {
        get => _rootPath;
        set { if (SetField(ref _rootPath, value)) ScanCommand.RaiseCanExecuteChanged(); }
    }

    public int MinAgeYears
    {
        get => _minAgeYears;
        set => SetField(ref _minAgeYears, value);
    }

    public FileFinderMode Mode
    {
        get => _mode;
        set { if (SetField(ref _mode, value)) ApplyMode(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetField(ref _isBusy, value)) RefreshCommands(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    private async Task ScanAsync()
    {
        IsBusy = true;
        StatusText = "Сканирую…";
        Visible.Clear();

        try
        {
            var progress = new Progress<string>(line => StatusText = line);
            var root = RootPath;
            var minAge = MinAgeYears;

            _report = await Task.Run(() => _service.Scan(root, topLargest: 200, minAge, progress, CancellationToken.None));
            ApplyMode();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            StatusText = $"Не удалось просканировать «{RootPath}»: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyMode()
    {
        Visible.Clear();

        if (_report is null)
        {
            return;
        }

        switch (Mode)
        {
            case FileFinderMode.Largest:
                foreach (var f in _report.Largest)
                    Visible.Add(new FoundFileRow(f.Path, f.SizeBytes, f.LastWriteTimeUtc, null));
                StatusText = $"Крупнейших файлов: {_report.Largest.Count}.";
                break;

            case FileFinderMode.Old:
                foreach (var f in _report.Old)
                    Visible.Add(new FoundFileRow(f.Path, f.SizeBytes, f.LastWriteTimeUtc, null));
                StatusText = $"Файлов старше {MinAgeYears} лет: {_report.Old.Count}.";
                break;

            case FileFinderMode.Duplicates:
                foreach (var group in _report.Duplicates)
                {
                    // First copy in each group is the one we'd keep by default; the rest are the
                    // "extra" copies, called out so the user isn't left guessing which to delete.
                    for (var i = 0; i < group.Paths.Count; i++)
                    {
                        var note = i == 0 ? "оставить" : "дубликат";
                        Visible.Add(new FoundFileRow(group.Paths[i], group.SizeBytes, DateTime.MinValue, note));
                    }
                }
                StatusText = $"Групп дубликатов: {_report.Duplicates.Count}.";
                break;
        }

        RefreshCommands();
    }

    private async Task DeleteSelectedAsync()
    {
        var selected = Visible.Where(v => v.IsChecked).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var names = string.Join("\n  • ", selected.Take(10).Select(s => s.Path));
        var more = selected.Count > 10 ? $"\n  … и ещё {selected.Count - 10}" : string.Empty;

        var warning = $"Удалить {selected.Count} файл(ов) без возможности восстановления через корзину?\n\n" +
                      $"  • {names}{more}";

        if (MessageBox.Show(warning, "Безвозвратное удаление", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var deleted = 0;
            foreach (var row in selected)
            {
                if (_service.TryDeleteFile(row.Path))
                {
                    deleted++;
                    Visible.Remove(row);
                }
            }

            StatusText = $"Удалено: {deleted} из {selected.Count}.";
        }
        finally
        {
            IsBusy = false;
        }

        await Task.CompletedTask;
    }

    private static void OpenFolder(FoundFileRow? row)
    {
        if (row is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{row.Path}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            // Explorer failing to launch isn't worth surfacing as an error dialog — non-critical.
        }
    }

    private void RefreshCommands()
    {
        DeleteSelectedCommand.RaiseCanExecuteChanged();
    }
}
