using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>
/// Removes one stock AppX package (per-user, all users) and can reinstall it from the
/// install location captured before removal. Reinstall brings the app binary back but
/// not necessarily any user data/settings that Windows itself would have cleaned up on
/// removal — hence ReversibleWithCaveat rather than a plain Reversible promise.
/// </summary>
public sealed class AppxBloatwareRemove(IProcessRunner processRunner, BloatwareCatalog.Entry app) : IOptimization
{
    public string Id => $"apps-bloat-{app.PackageName.ToLowerInvariant()}-remove";
    public string DisplayName => $"{app.DisplayName}: удалить";
    public string Description => app.Description;
    public string? TradeOff =>
        "Удаление приложения, не системного компонента — на игры и производительность не влияет. Восстановление возможно через магазин Windows, если бэкап места установки недоступен.";
    public RiskLevel Risk => app.Risk;
    public Reversibility Reversibility => Reversibility.ReversibleWithCaveat;
    public string Category => Categories.Apps;
    public bool RequiresRestart => false;

    public async Task<ApplyState> GetStateAsync(CancellationToken ct)
    {
        var fullName = await GetInstalledPackageFullNameAsync(ct);
        // Absence is our goal state ("removed"); we cannot cheaply tell apart
        // "we removed it" from "this edition never shipped it", and that
        // distinction does not change what the user should be told to do next.
        return fullName is null ? ApplyState.Applied : ApplyState.NotApplied;
    }

    public async Task ApplyAsync(OptimizationContext context)
    {
        var fullName = await GetInstalledPackageFullNameAsync(context.Cancellation);
        if (fullName is null)
        {
            context.Log.Report($"    {app.PackageName}: уже отсутствует, пропускаю");
            return;
        }

        var installLocation = await RunPowerShellAsync(
            $"(Get-AppxPackage -AllUsers -Name '{Escape(app.PackageName)}').InstallLocation",
            context.Cancellation);
        context.Backup.Capture(Id, "install-location", installLocation.StandardOutput.Trim());
        context.Backup.Capture(Id, "package-full-name", fullName);

        var remove = await RunPowerShellAsync(
            $"Remove-AppxPackage -AllUsers -Package '{Escape(fullName)}'",
            context.Cancellation);

        if (!remove.Succeeded)
            throw new InvalidOperationException($"Remove-AppxPackage failed for {app.PackageName}: {remove.StandardError}");

        context.Log.Report($"    {app.PackageName} удалён ({fullName})");
    }

    public async Task RevertAsync(OptimizationContext context)
    {
        var installLocation = context.Backup.Read(Id, "install-location");
        if (string.IsNullOrWhiteSpace(installLocation))
        {
            context.Log.Report($"    нет сохранённого места установки для {app.PackageName} — переустанови вручную из Microsoft Store, если нужно");
            return;
        }

        var manifestPath = $"{installLocation}\\AppXManifest.xml";
        var register = await RunPowerShellAsync(
            $"Add-AppxPackage -DisableDevelopmentMode -Register '{Escape(manifestPath)}'",
            context.Cancellation);

        context.Log.Report(register.Succeeded
            ? $"    {app.PackageName} переустановлен из {installLocation}"
            : $"    не удалось переустановить {app.PackageName}: {register.StandardError} (переустанови вручную из Microsoft Store)");
    }

    private async Task<string?> GetInstalledPackageFullNameAsync(CancellationToken ct)
    {
        var result = await RunPowerShellAsync(
            $"(Get-AppxPackage -AllUsers -Name '{Escape(app.PackageName)}').PackageFullName",
            ct);

        var name = result.StandardOutput.Trim();
        return result.Succeeded && name.Length > 0 ? name : null;
    }

    private Task<ProcessResult> RunPowerShellAsync(string command, CancellationToken ct) =>
        processRunner.RunAsync("powershell.exe", $"-NoProfile -NonInteractive -Command \"{command}\"", ct);

    private static string Escape(string value) => value.Replace("'", "''");
}
