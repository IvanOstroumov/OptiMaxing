using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>
/// Targets the first active Wi-Fi adapter via Set-NetAdapterPowerManagement, the same
/// switch as Device Manager's "Allow the computer to turn off this device to save
/// power". Wired adapters are left alone — power saving there rarely matters and
/// disabling it on a dock/USB NIC can be more disruptive than helpful.
/// </summary>
public sealed class WifiPowerSavingDisable(IProcessRunner processRunner) : IOptimization
{
    public string Id => "network-wifi-power-saving-disable";
    public string DisplayName => "Wi-Fi адаптер: отключить энергосбережение";
    public string Description =>
        "Запрещает Windows усыплять Wi-Fi адаптер для экономии энергии — на игровом ПК это иногда даёт заметные микро-лаги/скачки пинга в онлайн-играх.";
    public string? TradeOff =>
        "Чуть выше энергопотребление Wi-Fi модуля в простое; на настольном ПК несущественно, на ноутбуке от батареи — минус немного автономности.";
    public RiskLevel Risk => RiskLevel.Safe;
    public Reversibility Reversibility => Reversibility.Reversible;
    public string Category => Categories.Network;
    public bool RequiresRestart => false;

    public async Task<ApplyState> GetStateAsync(CancellationToken ct)
    {
        var adapter = await GetWifiAdapterNameAsync(ct);
        if (adapter is null)
            return ApplyState.NotApplicable;

        var value = await QueryAsync(adapter, ct);
        return value switch
        {
            "Disabled" => ApplyState.Applied,
            "Enabled" => ApplyState.NotApplied,
            _ => ApplyState.Unknown,
        };
    }

    public async Task ApplyAsync(OptimizationContext context)
    {
        var adapter = await GetWifiAdapterNameAsync(context.Cancellation)
            ?? throw new InvalidOperationException("Не найден активный Wi-Fi адаптер.");

        var before = await QueryAsync(adapter, context.Cancellation);
        context.Backup.Capture(Id, "adapter", adapter);
        context.Backup.Capture(Id, "previous-state", before ?? "Enabled");

        var set = await RunPowerShellAsync(
            $"Set-NetAdapterPowerManagement -Name '{Escape(adapter)}' -AllowComputerToTurnOffDevice Disabled",
            context.Cancellation);

        if (!set.Succeeded)
            throw new InvalidOperationException($"Set-NetAdapterPowerManagement failed: {set.StandardError}");

        context.Log.Report($"    {adapter}: энергосбережение отключено");
    }

    public async Task RevertAsync(OptimizationContext context)
    {
        var adapter = context.Backup.Read(Id, "adapter");
        if (adapter is null)
        {
            context.Log.Report("    нет бэкапа адаптера, ничего не делаю");
            return;
        }

        var previous = context.Backup.Read(Id, "previous-state") ?? "Enabled";

        var result = await RunPowerShellAsync(
            $"Set-NetAdapterPowerManagement -Name '{Escape(adapter)}' -AllowComputerToTurnOffDevice {previous}",
            context.Cancellation);

        context.Log.Report(result.Succeeded
            ? $"    {adapter}: энергосбережение восстановлено ({previous})"
            : $"    не удалось восстановить энергосбережение: {result.StandardError}");
    }

    private async Task<string?> GetWifiAdapterNameAsync(CancellationToken ct)
    {
        var result = await RunPowerShellAsync(
            "(Get-NetAdapter -Physical | Where-Object { $_.MediaType -eq 'Native 802.11' -and $_.Status -eq 'Up' } | Select-Object -First 1 -ExpandProperty Name)",
            ct);

        var name = result.StandardOutput.Trim();
        return result.Succeeded && name.Length > 0 ? name : null;
    }

    private async Task<string?> QueryAsync(string adapter, CancellationToken ct)
    {
        var result = await RunPowerShellAsync(
            $"(Get-NetAdapterPowerManagement -Name '{Escape(adapter)}').AllowComputerToTurnOffDevice",
            ct);

        var value = result.StandardOutput.Trim();
        return result.Succeeded && value.Length > 0 ? value : null;
    }

    private Task<ProcessResult> RunPowerShellAsync(string command, CancellationToken ct) =>
        processRunner.RunAsync("powershell.exe", $"-NoProfile -NonInteractive -Command \"{command}\"", ct);

    private static string Escape(string value) => value.Replace("'", "''");
}
