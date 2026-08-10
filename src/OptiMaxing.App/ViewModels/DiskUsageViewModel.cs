using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using OptiMaxing.Core.Files;

namespace OptiMaxing.App.ViewModels;

public sealed class TreemapRectRow(TreemapRect rect)
{
    public DiskNode Node { get; } = rect.Node;
    public double X { get; } = rect.X;
    public double Y { get; } = rect.Y;
    public double Width { get; } = rect.Width;
    public double Height { get; } = rect.Height;

    public string Name => Node.Name;
    public string SizeText => $"{Node.SizeBytes / 1024.0 / 1024.0:N0} МБ";
    public bool ShowLabel => Width >= 40 && Height >= 20;

    // Deterministic per-node color so a directory's rectangle doesn't visibly flicker between
    // colors as the user drills in and out — hashed from the path rather than random.
    public string ColorHex
    {
        get
        {
            var hash = Node.FullPath.GetHashCode();
            var hue = (hash & 0x7fffffff) % 360;
            return HsvToHex(hue, Node.IsDirectory ? 0.35 : 0.55, Node.IsDirectory ? 0.55 : 0.75);
        }
    }

    private static string HsvToHex(int hue, double saturation, double value)
    {
        var c = value * saturation;
        var x = c * (1 - Math.Abs(hue / 60.0 % 2 - 1));
        var m = value - c;

        var (r, g, b) = hue switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };

        return $"#{(byte)((r + m) * 255):X2}{(byte)((g + m) * 255):X2}{(byte)((b + m) * 255):X2}";
    }
}

/// <summary>Backs the "Диск" tab: a WinDirStat-style squarified treemap of a chosen directory.
/// Building the tree is a one-shot scan (not live), same as the file finder — disk usage doesn't
/// need second-by-second polling.</summary>
public sealed class DiskUsageViewModel : ObservableObject
{
    private readonly DiskTreeService _service;
    private readonly Stack<DiskNode> _drillStack = new();

    private string _rootPath = @"C:\";
    private bool _isBusy;
    private string _statusText = "Выбери папку и нажми «Сканировать».";
    private double _canvasWidth = 800;
    private double _canvasHeight = 500;
    private DiskNode? _current;

    public DiskUsageViewModel(DiskTreeService service)
    {
        _service = service;

        ScanCommand = new RelayCommand(ScanAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(RootPath));
        GoUpCommand = new RelayCommand(() => { GoUp(); return Task.CompletedTask; }, () => _drillStack.Count > 0);
        DrillInCommand = new RelayCommand<TreemapRectRow>(row => { DrillIn(row); return Task.CompletedTask; });
        OpenInExplorerCommand = new RelayCommand<TreemapRectRow>(row => { OpenInExplorer(row); return Task.CompletedTask; });

        SetRootDriveCCommand = new RelayCommand(() => { RootPath = @"C:\"; return Task.CompletedTask; });
    }

    public ObservableCollection<TreemapRectRow> Rectangles { get; } = [];
    public ObservableCollection<string> Breadcrumb { get; } = [];

    public RelayCommand ScanCommand { get; }
    public RelayCommand GoUpCommand { get; }
    public RelayCommand<TreemapRectRow> DrillInCommand { get; }
    public RelayCommand<TreemapRectRow> OpenInExplorerCommand { get; }
    public RelayCommand SetRootDriveCCommand { get; }

    public string RootPath
    {
        get => _rootPath;
        set { if (SetField(ref _rootPath, value)) ScanCommand.RaiseCanExecuteChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetField(ref _isBusy, value)) ScanCommand.RaiseCanExecuteChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public double CanvasWidth
    {
        get => _canvasWidth;
        set { if (SetField(ref _canvasWidth, value)) Relayout(); }
    }

    public double CanvasHeight
    {
        get => _canvasHeight;
        set { if (SetField(ref _canvasHeight, value)) Relayout(); }
    }

    private async Task ScanAsync()
    {
        IsBusy = true;
        StatusText = "Сканирую…";
        Rectangles.Clear();
        _drillStack.Clear();

        try
        {
            var progress = new Progress<string>(line => StatusText = line);
            var root = RootPath;

            _current = await Task.Run(() => _service.BuildTree(root, progress, CancellationToken.None));
            RefreshBreadcrumb();
            Relayout();
            StatusText = $"Готово. Всего: {_current.SizeBytes / 1024.0 / 1024.0 / 1024.0:N2} ГБ.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            StatusText = $"Не удалось просканировать «{RootPath}»: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            GoUpCommand.RaiseCanExecuteChanged();
        }
    }

    private void DrillIn(TreemapRectRow? row)
    {
        if (row is null || !row.Node.IsDirectory || row.Node.Children.Count == 0 || _current is null)
        {
            return;
        }

        _drillStack.Push(_current);
        _current = row.Node;
        RefreshBreadcrumb();
        Relayout();
        GoUpCommand.RaiseCanExecuteChanged();
    }

    private void GoUp()
    {
        if (_drillStack.Count == 0)
        {
            return;
        }

        _current = _drillStack.Pop();
        RefreshBreadcrumb();
        Relayout();
        GoUpCommand.RaiseCanExecuteChanged();
    }

    private void RefreshBreadcrumb()
    {
        Breadcrumb.Clear();
        foreach (var node in _drillStack.Reverse())
            Breadcrumb.Add(node.Name);

        if (_current is not null)
            Breadcrumb.Add(_current.Name);
    }

    private void Relayout()
    {
        Rectangles.Clear();

        if (_current is null || CanvasWidth <= 0 || CanvasHeight <= 0)
        {
            return;
        }

        var layout = TreemapLayout.Compute(_current.Children, 0, 0, CanvasWidth, CanvasHeight);
        foreach (var rect in layout)
            Rectangles.Add(new TreemapRectRow(rect));
    }

    private static void OpenInExplorer(TreemapRectRow? row)
    {
        if (row is null)
        {
            return;
        }

        try
        {
            var arg = row.Node.IsDirectory ? $"\"{row.Node.FullPath}\"" : $"/select,\"{row.Node.FullPath}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", arg) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException)
        {
            // Non-critical: Explorer failing to launch isn't worth an error dialog.
        }
    }
}
