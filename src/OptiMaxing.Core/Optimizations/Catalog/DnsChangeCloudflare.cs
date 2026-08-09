using OptiMaxing.Core.Abstractions;
using OptiMaxing.Core.Model;

namespace OptiMaxing.Core.Optimizations.Catalog;

/// <summary>
/// Switches DNS on the adapter currently carrying the default route. Reset restores
/// "obtain automatically" (DHCP) rather than guessing the previous static servers,
/// since Windows does not expose the pre-change value through a simple query.
/// </summary>
public sealed class DnsChangeCloudflare(IProcessRunner processRunner) : IOptimization
{
    public string Id => "network-dns-cloudflare";
    public string DisplayName => "DNS: переключить на Cloudflare (1.1.1.1 / 1.0.0.1)";
    public string Description =>
        "Меняет DNS-серверы активного сетевого адаптера на Cloudflare — часто быстрее и приватнее DNS провайдера.";
    public string? TradeOff =>
        "Если провайдер использует DNS для внутренних сервисов (личный кабинет, некоторые локальные CDN) — они могут перестать резолвиться.";
    public RiskLevel Risk => RiskLevel.Caution;
    public Reversibility Reversibility => Reversibility.Reversible;
    public string Category => Categories.Network;
    public bool RequiresRestart => false;

    public async Task<ApplyState> GetStateAsync(CancellationToken ct)
    {
        var adapter = await GetActiveAdapterAliasAsync(ct);
        if (adapter is null)
            return ApplyState.NotApplicable;

        var result = await processRunner.RunAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -Command \"(Get-DnsClientServerAddress -InterfaceAlias '{Escape(adapter)}' -AddressFamily IPv4).ServerAddresses -join ','\"",
            ct);

        if (!result.Succeeded)
            return ApplyState.Unknown;

        var servers = result.StandardOutput.Trim();
        return servers switch
        {
            "1.1.1.1,1.0.0.1" => ApplyState.Applied,
            "" => ApplyState.NotApplied, // empty means DHCP-assigned, our baseline
            _ => ApplyState.Modified,
        };
    }

    public async Task ApplyAsync(OptimizationContext context)
    {
        var adapter = await GetActiveAdapterAliasAsync(context.Cancellation)
            ?? throw new InvalidOperationException("Не найден активный сетевой адаптер.");

        var current = await processRunner.RunAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -Command \"(Get-DnsClientServerAddress -InterfaceAlias '{Escape(adapter)}' -AddressFamily IPv4).ServerAddresses -join ','\"",
            context.Cancellation);

        context.Backup.Capture(Id, "adapter", adapter);
        context.Backup.Capture(Id, "previous-servers", current.StandardOutput.Trim());

        var set = await processRunner.RunAsync(
            "powershell.exe",
            $"-NoProfile -NonInteractive -Command \"Set-DnsClientServerAddress -InterfaceAlias '{Escape(adapter)}' -ServerAddresses ('1.1.1.1','1.0.0.1')\"",
            context.Cancellation);

        if (!set.Succeeded)
            throw new InvalidOperationException($"Set-DnsClientServerAddress failed: {set.StandardError}");

        context.Log.Report($"    {adapter}: DNS -> 1.1.1.1, 1.0.0.1");
    }

    public async Task RevertAsync(OptimizationContext context)
    {
        var adapter = context.Backup.Read(Id, "adapter");
        if (adapter is null)
        {
            context.Log.Report("    нет бэкапа адаптера, ничего не делаю");
            return;
        }

        var previousServers = context.Backup.Read(Id, "previous-servers");

        var command = string.IsNullOrWhiteSpace(previousServers)
            ? $"Set-DnsClientServerAddress -InterfaceAlias '{Escape(adapter)}' -ResetServerAddresses"
            : $"Set-DnsClientServerAddress -InterfaceAlias '{Escape(adapter)}' -ServerAddresses ('{string.Join("','", previousServers.Split(','))}')";

        var result = await processRunner.RunAsync(
            "powershell.exe", $"-NoProfile -NonInteractive -Command \"{command}\"", context.Cancellation);

        context.Log.Report(result.Succeeded
            ? $"    {adapter}: DNS восстановлен"
            : $"    не удалось восстановить DNS: {result.StandardError}");
    }

    private async Task<string?> GetActiveAdapterAliasAsync(CancellationToken ct)
    {
        var result = await processRunner.RunAsync(
            "powershell.exe",
            "-NoProfile -NonInteractive -Command \"(Get-NetRoute -DestinationPrefix '0.0.0.0/0' | Sort-Object -Property RouteMetric | Select-Object -First 1 -ExpandProperty InterfaceAlias)\"",
            ct);

        var alias = result.StandardOutput.Trim();
        return result.Succeeded && alias.Length > 0 ? alias : null;
    }

    private static string Escape(string value) => value.Replace("'", "''");
}
