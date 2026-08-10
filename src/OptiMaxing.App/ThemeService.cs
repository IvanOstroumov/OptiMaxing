using System.IO;
using System.Windows;
using OptiMaxing.Core.Safety;

namespace OptiMaxing.App;

public enum AppTheme { Dark, Light }

/// <summary>Swaps the palette ResourceDictionary (Palette.Dark.xaml / Palette.Light.xaml) that sits
/// at index 0 of Application.Resources.MergedDictionaries. Every color in Theme.xaml and the window
/// XAML is a DynamicResource lookup into that palette, so replacing the dictionary repaints the
/// whole running UI instantly — no window reload needed. The choice is persisted as a single word
/// in a text file under %LOCALAPPDATA%\OptiMaxing\Settings so it survives restarts.</summary>
public static class ThemeService
{
    private static readonly string SettingsFile = Path.Combine(AppPaths.Settings, "theme.txt");

    public static AppTheme Current { get; private set; } = AppTheme.Dark;

    public static AppTheme LoadSaved()
    {
        try
        {
            var saved = File.Exists(SettingsFile) ? File.ReadAllText(SettingsFile).Trim() : null;
            Current = saved == nameof(AppTheme.Light) ? AppTheme.Light : AppTheme.Dark;
        }
        catch (IOException)
        {
            Current = AppTheme.Dark;
        }

        Apply(Current);
        return Current;
    }

    public static void Toggle()
    {
        Apply(Current == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark);
    }

    private static void Apply(AppTheme theme)
    {
        Current = theme;

        var uri = new Uri(theme == AppTheme.Light
            ? "Resources/Palette.Light.xaml"
            : "Resources/Palette.Dark.xaml", UriKind.Relative);

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries[0] = new ResourceDictionary { Source = uri };

        try
        {
            File.WriteAllText(SettingsFile, theme.ToString());
        }
        catch (IOException)
        {
            // Theme still applies for this session even if persisting the choice fails.
        }
    }
}
