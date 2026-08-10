using System.Windows;
using OptiMaxing.App.ViewModels;
using OptiMaxing.Core.Crashes;
using OptiMaxing.Core.Engine;
using OptiMaxing.Core.Optimizations;
using OptiMaxing.Core.Platform;
using OptiMaxing.Core.Programs;
using OptiMaxing.Core.Safety;
using OptiMaxing.Core.Services;
using OptiMaxing.Core.Startup;

namespace OptiMaxing.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ThemeService.LoadSaved();

        var logger = new FileLogger();

        DispatcherUnhandledException += (_, args) =>
        {
            logger.Write(LogLevel.Error, "Unhandled UI exception", args.Exception);
            MessageBox.Show(
                $"Непредвиденная ошибка:\n\n{args.Exception.Message}\n\nПодробности в журнале: {AppPaths.Logs}",
                "OptiMaxing",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        var registry = new WindowsRegistryProvider();
        var processRunner = new ProcessRunner();
        var services = new WindowsServiceManager();
        var fileSystem = new RealFileSystem();
        var backups = new BackupService(logger);
        var journal = new OperationJournal(logger);
        var restorePoints = new RestorePointService(processRunner, logger);
        var engine = new OptimizationEngine(backups, journal, restorePoints, logger);
        var catalog = new OptimizationCatalog(registry, services, processRunner, fileSystem);
        var systemInfo = new WindowsSystemInfoProvider();
        var hardware = new WmiHardwareInventoryProvider();
        var health = new SystemHealthService(systemInfo, restorePoints, hardware, new WindowsSecurityStatusProvider());
        var sensors = new SensorMonitor(new LibreHardwareSensorProvider());

        logger.Write(LogLevel.Info, "OptiMaxing started.");

        var startup = new StartupInventoryService(registry, fileSystem, processRunner);
        var processes = new ProcessMonitor(new WindowsProcessInspector());
        var serviceInventory = new ServiceInventoryService(services);
        var programs = new InstalledProgramsService(registry, processRunner);
        var crashHistory = new CrashHistoryService(new RealEventLogProvider());
        var viewModel = new MainViewModel(
            catalog, engine, restorePoints, health, sensors, startup, processes, serviceInventory, programs,
            crashHistory);
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        _ = viewModel.InitializeAsync();
    }
}
