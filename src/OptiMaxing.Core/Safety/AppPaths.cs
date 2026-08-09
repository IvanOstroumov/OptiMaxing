namespace OptiMaxing.Core.Safety;

public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OptiMaxing");

    public static string Backups => EnsureCreated(Path.Combine(Root, "Backups"));
    public static string Logs => EnsureCreated(Path.Combine(Root, "Logs"));
    public static string Journal => EnsureCreated(Path.Combine(Root, "Journal"));
    public static string Profiles => EnsureCreated(Path.Combine(Root, "Profiles"));
    public static string Reports => EnsureCreated(Path.Combine(Root, "Reports"));

    private static string EnsureCreated(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
