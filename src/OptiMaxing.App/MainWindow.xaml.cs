using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

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
}
