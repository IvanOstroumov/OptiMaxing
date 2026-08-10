using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Services;

public sealed record ServiceActionResult(bool Succeeded, string Message);

/// <summary>Read/modify facade over IServiceManager that adds safety classification and turns
/// exceptions into messages the UI can show. Services are never deleted — the worst OptiMaxing will
/// do is set Start=Disabled, which is one click away from being undone.</summary>
public sealed class ServiceInventoryService(IServiceManager services)
{
    public IReadOnlyList<ServiceRow> List() =>
        services.GetAll()
            .Select(ServiceAdvice.Describe)
            .OrderBy(r => r.Info.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public ServiceActionResult SetStartupType(ServiceRow row, ServiceStartupType startupType)
    {
        try
        {
            services.SetStartupType(row.Info.Name, startupType);
            return new ServiceActionResult(true, $"«{row.Info.DisplayName}»: тип запуска — {Describe(startupType)}.");
        }
        catch (Exception ex)
        {
            return new ServiceActionResult(false, $"Не удалось изменить «{row.Info.DisplayName}»: {ex.Message}");
        }
    }

    public ServiceActionResult SetRunning(ServiceRow row, bool running)
    {
        try
        {
            if (running)
            {
                services.Start(row.Info.Name);
                return new ServiceActionResult(true, $"«{row.Info.DisplayName}» запущена.");
            }

            services.Stop(row.Info.Name);
            return new ServiceActionResult(true, $"«{row.Info.DisplayName}» остановлена.");
        }
        catch (Exception ex)
        {
            // A stop can also time out because the service refuses to stop; the message carries
            // whatever Windows said rather than a generic failure.
            return new ServiceActionResult(false, $"Не удалось {(running ? "запустить" : "остановить")} " +
                                                  $"«{row.Info.DisplayName}»: {ex.Message}");
        }
    }

    public static string Describe(ServiceStartupType type) => type switch
    {
        ServiceStartupType.Boot => "при загрузке",
        ServiceStartupType.System => "системный",
        ServiceStartupType.Automatic => "автоматически",
        ServiceStartupType.AutomaticDelayed => "автоматически (отложенно)",
        ServiceStartupType.Manual => "вручную",
        ServiceStartupType.Disabled => "отключена",
        _ => "неизвестно",
    };
}
