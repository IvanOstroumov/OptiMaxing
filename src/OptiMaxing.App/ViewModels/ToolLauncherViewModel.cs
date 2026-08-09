using System.Diagnostics;
using System.IO;
using OptiMaxing.Core.Advisory;

namespace OptiMaxing.App.ViewModels;

public sealed class ToolLauncherViewModel : ObservableObject
{
    private readonly ToolEntry _entry;
    private readonly string? _foundPath;

    public ToolLauncherViewModel(ToolEntry entry)
    {
        _entry = entry;
        _foundPath = entry.CommonInstallPaths.FirstOrDefault(File.Exists);

        LaunchCommand = new RelayCommand(LaunchAsync);
    }

    public string Name => _entry.Name;
    public string Description => _entry.Description;
    public bool IsInstalled => _foundPath is not null;
    public string ActionText => IsInstalled ? "Запустить" : "Скачать";

    public RelayCommand LaunchCommand { get; }

    private Task LaunchAsync()
    {
        var target = _foundPath ?? _entry.DownloadUrl;
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        return Task.CompletedTask;
    }
}
