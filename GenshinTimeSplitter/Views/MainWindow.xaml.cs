using GenshinTimeSplitter.ViewModels;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
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

    private async void InstallFFmpegButton_Click(object sender, RoutedEventArgs e)
    {
        var ffmpegPackageId = "BtbN.FFmpeg.GPL.7.1";
        var psScriptContent = $@"
Write-Host ""This will install FFmpeg using winget."" -ForegroundColor Yellow
$response = Read-Host -Prompt ""Do you want to proceed? (Y/N)""

if ($response -eq 'y' -or $response -eq 'Y' -or $response -eq 'yes' -or $response -eq 'Yes') {{
    Write-Host ""Proceeding with FFmpeg installation..."" -ForegroundColor Green
    winget install --id={ffmpegPackageId} -e --accept-source-agreements
}} else {{
    Write-Host ""FFmpeg installation cancelled."" -ForegroundColor Red
}}
Read-Host -Prompt ""Press Enter to close this window.""
";
        var tempScriptPath = Path.GetTempFileName() + ".ps1";
        File.WriteAllText(tempScriptPath, psScriptContent);
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-ExecutionPolicy Bypass -File \"{tempScriptPath}\"",
            UseShellExecute = true,
        };

        var p = Process.Start(psi);
        await p.WaitForExitAsync();

        MessageBox.Show(
            "Restart this application.\r\nApp can not use FFmpeg until restarted.",
            "Warning",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
