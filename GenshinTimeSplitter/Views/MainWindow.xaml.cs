using GenshinTimeSplitter.ViewModels;
using Microsoft.Extensions.Logging;
using System.Windows;

namespace GenshinTimeSplitter.Views;

public partial class MainWindow : Window
{
    private readonly ILogger<MainWindow> _logger;

    public MainWindow(
        ILogger<MainWindow> logger,
        MainWindowViewModel mainWindowViewModel)
    {
        _logger = logger;

        InitializeComponent();
        DataContext = mainWindowViewModel;

        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _logger.LogInformation("MainWindow Opened");
    }

    private void MainWindow_Closed(object sender, System.EventArgs e)
    {
        _logger.LogInformation("MainWindow Closed");
    }
}
