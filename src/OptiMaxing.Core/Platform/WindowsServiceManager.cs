using System.ServiceProcess;
using Microsoft.Win32;
using OptiMaxing.Core.Abstractions;

namespace OptiMaxing.Core.Platform;

public sealed class WindowsServiceManager : IServiceManager
{
    private const string ServicesKey = @"SYSTEM\CurrentControlSet\Services";

    public ServiceInfo? Get(string serviceName)
    {
        try
        {
            using var controller = new ServiceController(serviceName);
            return new ServiceInfo(
                controller.ServiceName,
                controller.DisplayName,
                ReadStartupType(serviceName),
                controller.Status == ServiceControllerStatus.Running);
        }
        catch (InvalidOperationException)
        {
            return null; // service is not installed on this machine
        }
    }

    public IReadOnlyList<ServiceInfo> GetAll() =>
        ServiceController.GetServices()
            .Select(c =>
            {
                using (c)
                {
                    return new ServiceInfo(
                        c.ServiceName,
                        c.DisplayName,
                        ReadStartupType(c.ServiceName),
                        c.Status == ServiceControllerStatus.Running);
                }
            })
            .ToList();

    public void SetStartupType(string serviceName, ServiceStartupType startupType)
    {
        // Written through the registry rather than sc.exe so the delayed-autostart
        // flag can be set in the same transaction.
        using var key = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey($@"{ServicesKey}\{serviceName}", writable: true)
            ?? throw new InvalidOperationException($"Service '{serviceName}' not found.");

        key.SetValue("Start", ToStartValue(startupType), Microsoft.Win32.RegistryValueKind.DWord);

        if (startupType == ServiceStartupType.AutomaticDelayed)
            key.SetValue("DelayedAutoStart", 1, Microsoft.Win32.RegistryValueKind.DWord);
        else
            key.DeleteValue("DelayedAutoStart", throwOnMissingValue: false);
    }

    public void Stop(string serviceName)
    {
        using var controller = new ServiceController(serviceName);
        if (!controller.CanStop || controller.Status == ServiceControllerStatus.Stopped)
            return;

        controller.Stop();
        controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
    }

    public void Start(string serviceName)
    {
        using var controller = new ServiceController(serviceName);
        if (controller.Status == ServiceControllerStatus.Running)
            return;

        controller.Start();
        controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
    }

    private static ServiceStartupType ReadStartupType(string serviceName)
    {
        using var key = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey($@"{ServicesKey}\{serviceName}", writable: false);

        if (key?.GetValue("Start") is not int start)
            return ServiceStartupType.Unknown;

        return start switch
        {
            0 => ServiceStartupType.Boot,
            1 => ServiceStartupType.System,
            2 => key.GetValue("DelayedAutoStart") is 1
                ? ServiceStartupType.AutomaticDelayed
                : ServiceStartupType.Automatic,
            3 => ServiceStartupType.Manual,
            4 => ServiceStartupType.Disabled,
            _ => ServiceStartupType.Unknown,
        };
    }

    private static int ToStartValue(ServiceStartupType type) => type switch
    {
        ServiceStartupType.Boot => 0,
        ServiceStartupType.System => 1,
        ServiceStartupType.Automatic or ServiceStartupType.AutomaticDelayed => 2,
        ServiceStartupType.Manual => 3,
        ServiceStartupType.Disabled => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}
