using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Safety;

namespace OptiMaxing.App.ViewModels;

public enum ProcessSort { Cpu, Memory, Name, Threads, Pid }

public sealed record SortOption<T>(T Value, string Label);

public sealed class ProcessRow(ProcessSample sample) : ObservableObject
{
    private bool _isChecked;

    public ProcessSample Sample { get; private set; } = sample;

    /// <summary>Independent of ListBox.SelectedItem — lets the user tick several rows and apply a
    /// priority/kill action to all of them at once, while single-click selection still drives the
    /// existing per-row toolbar buttons.</summary>
    public bool IsChecked
    {
        get => _isChecked;
        set => SetField(ref _isChecked, value);
    }

    public int Id => Sample.Process.Id;
    public string Name => Sample.Process.Name;
    public string Path => Sample.Process.ExecutablePath ?? Sample.Process.AccessFailure ?? "нет доступа";
    public bool IsCritical => Sample.Process.IsSystemCritical;
    public bool IsHung => !Sample.Process.IsResponding;
    public string MemoryText => $"{Sample.Process.WorkingSetBytes / 1024.0 / 1024.0:N0} МБ";
    public string CpuText => Sample.CpuPercent is { } percent ? $"{percent:N1} %" : "—";
    public string ThreadsText => $"{Sample.Process.ThreadCount} пот.";
    public string PriorityText => Sample.Process.Priority?.ToString() ?? "—";

    public void Update(ProcessSample updated)
    {
        Sample = updated;
        OnPropertyChanged(nameof(MemoryText));
        OnPropertyChanged(nameof(CpuText));
        OnPropertyChanged(nameof(ThreadsText));
        OnPropertyChanged(nameof(PriorityText));
        OnPropertyChanged(nameof(IsHung));
    }
}

/// <summary>Backs the "Процессы" tab. Polls once a second while the tab is visible, and updates rows
/// in place instead of rebuilding the collection so the selection and scroll position survive.</summary>
public sealed class ProcessesViewModel : ObservableObject
{
    private readonly ProcessMonitor _monitor;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<int, ProcessRow> _rows = [];

    private ProcessRow? _selected;
    private bool _isActive;
    private string _statusText = string.Empty;
    private string _filter = string.Empty;
    private ProcessSort _sortMode = ProcessSort.Cpu;

    public ProcessesViewModel(ProcessMonitor monitor)
    {
        _monitor = monitor;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();

        RefreshCommand = new RelayCommand(() => { Refresh(); return Task.CompletedTask; });
        KillCommand = new RelayCommand(() => { Kill(); return Task.CompletedTask; }, () => Selected is not null);
        SetHighPriorityCommand = new RelayCommand(
            () => { SetPriority(ProcessPriority.High); return Task.CompletedTask; }, () => Selected is not null);
        SetNormalPriorityCommand = new RelayCommand(
            () => { SetPriority(ProcessPriority.Normal); return Task.CompletedTask; }, () => Selected is not null);
        SetLowPriorityCommand = new RelayCommand(
            () => { SetPriority(ProcessPriority.BelowNormal); return Task.CompletedTask; }, () => Selected is not null);

        KillCheckedCommand = new RelayCommand(
            () => { KillChecked(); return Task.CompletedTask; }, () => Processes.Any(p => p.IsChecked));
        SetHighPriorityCheckedCommand = new RelayCommand(
            () => { SetPriorityChecked(ProcessPriority.High); return Task.CompletedTask; }, () => Processes.Any(p => p.IsChecked));
        SetNormalPriorityCheckedCommand = new RelayCommand(
            () => { SetPriorityChecked(ProcessPriority.Normal); return Task.CompletedTask; }, () => Processes.Any(p => p.IsChecked));
        SetLowPriorityCheckedCommand = new RelayCommand(
            () => { SetPriorityChecked(ProcessPriority.BelowNormal); return Task.CompletedTask; }, () => Processes.Any(p => p.IsChecked));

        Refresh();
    }

    public ObservableCollection<ProcessRow> Processes { get; } = [];

    public RelayCommand RefreshCommand { get; }
    public RelayCommand KillCommand { get; }
    public RelayCommand SetHighPriorityCommand { get; }
    public RelayCommand SetNormalPriorityCommand { get; }
    public RelayCommand SetLowPriorityCommand { get; }
    public RelayCommand KillCheckedCommand { get; }
    public RelayCommand SetHighPriorityCheckedCommand { get; }
    public RelayCommand SetNormalPriorityCheckedCommand { get; }
    public RelayCommand SetLowPriorityCheckedCommand { get; }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (!SetField(ref _isActive, value))
            {
                return;
            }

            if (value)
            {
                Refresh();
                _timer.Start();
            }
            else
            {
                _timer.Stop();
            }
        }
    }

    public ProcessRow? Selected
    {
        get => _selected;
        set
        {
            if (SetField(ref _selected, value))
            {
                RefreshCommands();
            }
        }
    }

    public string Filter
    {
        get => _filter;
        set { if (SetField(ref _filter, value)) Refresh(); }
    }

    public ProcessSort SortMode
    {
        get => _sortMode;
        set { if (SetField(ref _sortMode, value)) Refresh(); }
    }

    public IReadOnlyList<SortOption<ProcessSort>> SortOptions { get; } =
    [
        new(ProcessSort.Cpu, "По загрузке CPU"),
        new(ProcessSort.Memory, "По памяти"),
        new(ProcessSort.Name, "По имени"),
        new(ProcessSort.Threads, "По потокам"),
        new(ProcessSort.Pid, "По PID"),
    ];

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    private void Refresh()
    {
        var samples = _monitor.Poll();
        var filtered = samples
            .Where(s => string.IsNullOrWhiteSpace(Filter)
                        || s.Process.Name.Contains(Filter, StringComparison.CurrentCultureIgnoreCase));

        IEnumerable<ProcessSample> sorted = SortMode switch
        {
            ProcessSort.Memory => filtered.OrderByDescending(s => s.Process.WorkingSetBytes),
            ProcessSort.Name => filtered.OrderBy(s => s.Process.Name, StringComparer.CurrentCultureIgnoreCase),
            ProcessSort.Threads => filtered.OrderByDescending(s => s.Process.ThreadCount),
            ProcessSort.Pid => filtered.OrderBy(s => s.Process.Id),
            _ => filtered.OrderByDescending(s => s.CpuPercent ?? -1).ThenByDescending(s => s.Process.WorkingSetBytes),
        };

        var matching = sorted.ToList();

        var selectedId = Selected?.Id;
        var seen = new HashSet<int>();

        for (var index = 0; index < matching.Count; index++)
        {
            var sample = matching[index];
            seen.Add(sample.Process.Id);

            if (_rows.TryGetValue(sample.Process.Id, out var row))
            {
                row.Update(sample);

                var current = Processes.IndexOf(row);
                if (current != index && current >= 0)
                {
                    Processes.Move(current, index);
                }
            }
            else
            {
                row = new ProcessRow(sample);
                row.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(ProcessRow.IsChecked))
                        RefreshCheckedCommands();
                };
                _rows[sample.Process.Id] = row;
                Processes.Insert(index, row);
            }
        }

        foreach (var gone in _rows.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            Processes.Remove(_rows[gone]);
            _rows.Remove(gone);
        }

        Selected = selectedId is { } id && _rows.TryGetValue(id, out var still) ? still : null;

        var totalMemory = samples.Sum(s => s.Process.WorkingSetBytes) / 1024.0 / 1024.0 / 1024.0;
        StatusText = $"Процессов: {samples.Count}, показано: {matching.Count}, " +
                     $"память суммарно: {totalMemory:N1} ГБ";
    }

    private void Kill()
    {
        if (Selected is not { } row)
        {
            return;
        }

        // Per the "warn and confirm" rule: critical processes are not blocked, but killing one of
        // them bugchecks the machine outright, so the wording says exactly that.
        var warning = row.IsCritical
            ? $"«{row.Name}» (PID {row.Id}) — системный процесс Windows.\n\n" +
              "Его завершение почти наверняка вызовет синий экран или выкинет из сеанса, " +
              "несохранённые данные будут потеряны.\n\nВсё равно завершить?"
            : $"Завершить процесс «{row.Name}» (PID {row.Id})?\n\n" +
              "Несохранённые данные в этой программе будут потеряны.";

        if (MessageBox.Show(warning, "OptiMaxing", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            != MessageBoxResult.Yes)
        {
            return;
        }

        var name = row.Name;
        var killed = _monitor.Kill(row.Id);
        Refresh();
        StatusText = killed
            ? $"Завершено: {name}"
            : $"Не удалось завершить {name} — нет прав или процесс защищён Windows.";
    }

    private void SetPriority(ProcessPriority priority)
    {
        if (Selected is not { } row)
        {
            return;
        }

        var applied = _monitor.SetPriority(row.Id, priority);
        Refresh();
        StatusText = applied
            ? $"Приоритет «{row.Name}» — {priority}. Сбросится при перезапуске программы."
            : $"Не удалось изменить приоритет {row.Name} — нет прав.";
    }

    private void KillChecked()
    {
        var checkedRows = Processes.Where(p => p.IsChecked).ToList();
        if (checkedRows.Count == 0)
        {
            return;
        }

        var anyCritical = checkedRows.Any(r => r.IsCritical);
        var names = string.Join("\n  • ", checkedRows.Select(r => $"{r.Name} (PID {r.Id})"));
        var warning = $"Завершить {checkedRows.Count} процесс(ов)?\n  • {names}\n\n"
                     + "Несохранённые данные в этих программах будут потеряны."
                     + (anyCritical
                         ? "\n\nСреди них есть системные процессы — их завершение почти наверняка вызовет синий экран."
                         : string.Empty);

        if (MessageBox.Show(warning, "OptiMaxing", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            != MessageBoxResult.Yes)
        {
            return;
        }

        var killedCount = checkedRows.Count(row => _monitor.Kill(row.Id));
        Refresh();
        StatusText = $"Завершено: {killedCount} из {checkedRows.Count}.";
    }

    private void SetPriorityChecked(ProcessPriority priority)
    {
        var checkedRows = Processes.Where(p => p.IsChecked).ToList();
        if (checkedRows.Count == 0)
        {
            return;
        }

        var appliedCount = checkedRows.Count(row => _monitor.SetPriority(row.Id, priority));
        Refresh();
        StatusText = $"Приоритет «{priority}» применён к {appliedCount} из {checkedRows.Count}. Сбросится при перезапуске программы.";
    }

    private void RefreshCheckedCommands()
    {
        KillCheckedCommand.RaiseCanExecuteChanged();
        SetHighPriorityCheckedCommand.RaiseCanExecuteChanged();
        SetNormalPriorityCheckedCommand.RaiseCanExecuteChanged();
        SetLowPriorityCheckedCommand.RaiseCanExecuteChanged();
    }

    private void RefreshCommands()
    {
        KillCommand.RaiseCanExecuteChanged();
        SetHighPriorityCommand.RaiseCanExecuteChanged();
        SetNormalPriorityCommand.RaiseCanExecuteChanged();
        SetLowPriorityCommand.RaiseCanExecuteChanged();
    }
}
