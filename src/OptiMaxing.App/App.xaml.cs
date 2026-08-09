using System.Windows;
using OptiMaxing.App.ViewModels;
using OptiMaxing.Core.Engine;
using OptiMaxing.Core.Optimizations;
using OptiMaxing.Core.Platform;
using OptiMaxing.Core.Safety;

namespace OptiMaxing.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
        var backups = new BackupService(logger);
        var journal = new OperationJournal(logger);
        var restorePoints = new RestorePointService(processRunner, logger);
        var engine = new OptimizationEngine(backups, journal, restorePoints, logger);
        var catalog = new OptimizationCatalog(registry);

        logger.Write(LogLevel.Info, "OptiMaxing started.");

        var viewModel = new MainViewModel(catalog, engine, restorePoints);
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        _ = viewModel.InitializeAsync();
    }
}
