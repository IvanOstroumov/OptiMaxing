using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using OptiMaxing.App.ViewModels;

namespace OptiMaxing.App;

public partial class MainWindow : Window
{
    // Without this, Windows 11 draws the title bar in the system light-theme white regardless of
    // the app's own dark content — the "white bar at the top" the app otherwise looks unthemed
    // next to. DWMWA_USE_IMMERSIVE_DARK_MODE (20) tells DWM to paint the native chrome dark too.
    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var enabled = 1;
            DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
        };
    }

    // The treemap canvas has no intrinsic size of its own (an ItemsControl over a Canvas panel
    // doesn't report DesiredSize from its children's Canvas.Left/Top positions), so the view model
    // needs to be told the available pixel area explicitly whenever the host border resizes.
    private void DiskUsageCanvasHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is DiskUsageViewModel diskUsage)
        {
            diskUsage.CanvasWidth = e.NewSize.Width;
            diskUsage.CanvasHeight = e.NewSize.Height;
        }
    }
}
