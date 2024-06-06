using GenshinTimeSplitter.Proc;
using GenshinTimeSplitter.ViewModels;
using GenshinTimeSplitter.Views;
using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Windows;

namespace GenshinTimeSplitter;

public partial class App : Application
{
    public readonly ServiceProvider _serviceProvider;
    private ILogger<App> _logger;

    public App()
    {
        // initialize VLC media player
        Core.Initialize();

        _serviceProvider = BuildServiceProvider();
        _logger = _serviceProvider.GetService<ILogger<App>>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        var fullname = GetType().Assembly.Location;
        var info = FileVersionInfo.GetVersionInfo(fullname);
        var verion = info.FileVersion;

        _logger.LogInformation("App started. version:{version}", verion);


        base.OnStartup(e);

        var mainWindow = _serviceProvider.GetService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);

        _serviceProvider.Dispose();

        _logger.LogInformation("App exited.");
        LogManager.Flush();
        LogManager.Shutdown();
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var serviceCollection = new ServiceCollection()
            .AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddNLog();
                builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
            })
            .AddTransient<MainWindow>()
            .AddTransient<MainWindowViewModel>()
            .AddTransient<AnalyzeResultStore>()
            .AddTransient<AnalyzeConfigStore>()
            ;

        return serviceCollection.BuildServiceProvider();
    }
}
