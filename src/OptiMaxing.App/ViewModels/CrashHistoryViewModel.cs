using System.Collections.ObjectModel;
using OptiMaxing.Core.Crashes;

namespace OptiMaxing.App.ViewModels;

public sealed class CrashEventRow(CrashEvent crashEvent)
{
    public CrashEvent CrashEvent { get; } = crashEvent;

    public string TimestampText => CrashEvent.TimestampUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");
    public string KindText => CrashEvent.Kind switch
    {
        CrashKind.BugCheck => "Синий экран",
        CrashKind.UnexpectedShutdown => "Неожиданное выключение",
        _ => "Прочее",
    };
    public string Summary => CrashEvent.Summary;
    public string? BugCheckCode => CrashEvent.BugCheckCode;
}

/// <summary>Backs the "Сбои (BSOD)" tab. Reads the last 90 days of BugCheck / unexpected-shutdown
/// events from the System event log on demand — no background polling, this is a look-back report.</summary>
public sealed class CrashHistoryViewModel : ObservableObject
{
    private readonly CrashHistoryService _service;
    private string _statusText = "Нажмите «Обновить», чтобы прочитать журнал событий.";

    public CrashHistoryViewModel(CrashHistoryService service)
    {
        _service = service;
        RefreshCommand = new RelayCommand(() => { Refresh(); return Task.CompletedTask; });
    }

    public ObservableCollection<CrashEventRow> Entries { get; } = [];

    public RelayCommand RefreshCommand { get; }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public void Refresh()
    {
        Entries.Clear();

        IReadOnlyList<CrashEvent> crashes;
        try
        {
            crashes = _service.GetRecentCrashes(TimeSpan.FromDays(90));
        }
        catch (Exception ex)
        {
            StatusText = $"Не удалось прочитать журнал событий: {ex.Message}";
            return;
        }

        foreach (var crash in crashes)
        {
            Entries.Add(new CrashEventRow(crash));
        }

        StatusText = Entries.Count == 0
            ? "За последние 90 дней сбоев (BSOD, неожиданных выключений) не найдено."
            : $"Найдено событий: {Entries.Count} за последние 90 дней.";
    }
}
