using ConsoleAppFramework;
using GenshinTimeSplitter.Proc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using OpenCvSharp;
using System.Text;

var app = ConsoleApp.Create();
app.Add<MyCommands>();
app.ConfigureServices(serviceCollection =>
{
    serviceCollection
        .AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddNLog();
                builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
            })
        .AddTransient<AnalyzeResultStore>()
        .AddTransient<AnalyzeConfigStore>();
});
app.Run(args);

public record Region(int x, int y, int w, int h);

public class MyCommands(
    ILogger<MyCommands> logger,
    IServiceProvider serviceProvider,
    AnalyzeConfigStore analyzeConfigStore)
{
    private readonly ILogger<MyCommands> _logger = logger;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly AnalyzeConfigStore _analyzeConfigStore = analyzeConfigStore;

    /// <summary>
    /// run to split times
    /// </summary>
    /// <param name="filePath">-f,
    /// mp4 file path.
    /// </param>
    /// <param name="fromTime">-from,
    /// start of an analyze range.
    /// Format : "hh:mm:ss"
    /// Example: 00:01:23
    /// </param>
    /// <param name="toTime">-to,
    /// end of an analyze range.
    /// Format: "hh:mm:ss"
    /// Example: 00:01:23
    /// </param>
    /// <param name="confFilePath">-conf,
    /// configration file path.
    /// </param>
    /// <param name="regions">-r,
    /// region for detected a load screen.
    /// Format : [{"x": RectLeftTopX, "y": RectLeftTopY, "w": RectWidth, "h": RectHeight}, ...]
    /// Example: [{"x": 100, "y": 200, "w": 100, "h": 100}, {"x": 1000, "y": 300, "w": 100, "h": 100}]
    /// JSON array for {x, y, width, height}
    /// If none, use default regions.
    /// </param>
    /// <param name="diffThreashold">
    /// threshold for recognizing loading screens.
    /// </param>
    /// <param name="falseDetectionMilliSeconds">
    /// the time (in milliseconds) used to determine if a warp is a false detection
    /// </param>
    /// <param name="parallelCount">
    /// the number of threads to use for analysis.
    /// </param>
    /// <param name="forceOverWritten">-y,
    /// overwrite result files.
    /// </param>
    /// <param name="token"></param>
    /// <returns></returns>
    [Command("")]
    public async Task<int> Root(
        string filePath,
        TimeSpan? fromTime = null,
        TimeSpan? toTime = null,
        string confFilePath = null,
        Region[] regions = null,
        byte diffThreashold = 3,
        int falseDetectionMilliSeconds = 200,
        byte parallelCount = 0,
        bool forceOverWritten = false,
        CancellationToken token = default)
    {
        _logger.LogDebug("arguments");
        _logger.LogDebug("filePath: {filePath}", filePath);
        _logger.LogDebug("fromTime: {fromTime}", fromTime);
        _logger.LogDebug("toTime: {toTime}", toTime);
        _logger.LogDebug("regions: {regions}", regions);
        _logger.LogDebug("confFilePath: {confFilePath}", confFilePath);
        _logger.LogDebug("diffThreashold: {diffThreashold}", diffThreashold);
        _logger.LogDebug("falseDetectionMilliSeconds: {falseDetectionMilliSeconds}", falseDetectionMilliSeconds);
        _logger.LogDebug("parallelCount: {parallelCount}", parallelCount);
        _logger.LogDebug("forceOverWritten: {forceOverWritten}", forceOverWritten);

        // validate inputs
        //   validation of regions is at SectionStartAnalyzer.AnalyzeAsync()
        if (!File.Exists(filePath))
        {
            Console.WriteLine("file is not found.");
            return -1;
        }
        if (toTime - fromTime < TimeSpan.Zero)
        {
            Console.WriteLine("analyze range must be from < to.");
            return -1;
        }
        if (confFilePath is not null && !File.Exists(confFilePath))
        {
            Console.WriteLine("configration file is not found.");
            return -1;
        }

        try
        {
            _logger.LogInformation("Start to analyze.");

            filePath = Path.GetFullPath(filePath);

            using var scope = _serviceProvider.CreateScope();
            var analyzeResultStore = scope.ServiceProvider.GetService<AnalyzeResultStore>();

            // check result files exists
            if (analyzeResultStore.Exists(filePath) && forceOverWritten is false)
            {
                if (!ConfirmOverwrite())
                {
                    // if disallow overwrite, exit -1.
                    _logger.LogInformation("user disallow to overwrite. exit.");
                    return -1;
                }
            }

            // prepare analyze
            try
            {
                // cursor is invalid on CI/CD machine.
                Console.CursorVisible = false;
            }
            catch (IOException)
            { }

            var startDateTime = DateTime.Now;
            using var analyzer = await SectionStartAnalyzer.LoadAsync(
                scope.ServiceProvider.GetService<ILogger<SectionStartAnalyzer>>(),
                filePath);
            analyzer.ProgressChanged += (_, progress) =>
            {
                PrintProgress(startDateTime, progress);
            };

            if (!fromTime.HasValue)
            {
                fromTime = TimeSpan.Zero;
            }
            if (!toTime.HasValue)
            {
                toTime = analyzer.MovieTimeSpan;
            }

            // build analyze configration
            _logger.LogDebug("start to build config.");
            AnalyzeConfig analyzeConfig;
            if (confFilePath is not null)
            {
                if (_analyzeConfigStore.TryLoad(analyzer.MovieResolution, confFilePath, out var result))
                {
                    analyzeConfig = result;
                }
                else
                {
                    throw new AnalyzeConfigException("config file is not valid.");
                }
            }
            else
            {
                if (regions is null || regions.Length == 0)
                {
                    analyzeConfig = AnalyzeConfig.GetDefault(analyzer.MovieResolution) with
                    {
                        DiffThreashold = diffThreashold,
                        FalseDetectionMilliSeconds = falseDetectionMilliSeconds,
                        ParallelCount = parallelCount,
                    };
                }
                else
                {
                    analyzeConfig = new AnalyzeConfig()
                    {
                        TargetMovieResolution = analyzer.MovieResolution,
                        AnalyzeRegions = regions.Select(x => new Rect(x.x, x.y, x.w, x.h)).ToArray(),
                        DiffThreashold = diffThreashold,
                        FalseDetectionMilliSeconds = falseDetectionMilliSeconds,
                        ParallelCount = parallelCount,
                    };
                }
            }
            _logger.LogDebug("finish to build config. config:{config}", analyzeConfig);

            // print regions
            Console.WriteLine("Regions:");
            Console.WriteLine(string.Join(
                Environment.NewLine,
                analyzeConfig.AnalyzeRegions.Select(r => $"    x:{r.X} y:{r.Y} w:{r.Width} h:{r.Height}")));

            // start to analyze
            _logger.LogDebug("start to analyze.");
            var analyzeResult = await analyzer.AnalyzeAsync(
                fromTime.Value,
                toTime.Value,
                analyzeConfig,
                token);
            _logger.LogDebug("finish to analyze.");

            // save analyze result
            await analyzeResultStore.SaveAsync(filePath, analyzeResult);
            _logger.LogDebug("finished to save result files.");

            // show completed message
            var elapsedTimeSpan = DateTime.Now - startDateTime;
            Console.WriteLine($"Finished to analyze sections.");
            Console.WriteLine($"ElapsedTime - {elapsedTimeSpan:hh\\:mm\\:ss}");

            _logger.LogInformation("Succeeded to analyze.");

            return 0;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Analyze is canceled.");
            Console.WriteLine("canceled by user");

            return 0;
        }
        catch (AnalyzeConfigException ex)
        {
            _logger.LogError(ex, "analyze config error.");
            Console.WriteLine(ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "unexpected error.");
            throw;
        }
    }

    private int? _cursorTop = null;
    private int? _cursorLeft = null;
    private void PrintProgress(DateTime startDateTime, Progress progress)
    {
        if (!_cursorTop.HasValue || !_cursorLeft.HasValue)
        {
            try
            {
                // cursor is invalid on CI/CD machine.
                _cursorTop = Console.CursorTop;
                _cursorLeft = Console.CursorLeft;
            }
            catch (IOException)
            { }
        }

        var rate = progress.IsEmpty ?
            "" :
            $"{(double)progress.CurrentFrame / progress.TotalFrame *100:F1}%";
        var remainingTime = "";
        if (!progress.IsEmpty && progress.CurrentFrame is not 0)
        {
            var elapsedSec = (DateTimeOffset.Now - startDateTime).TotalSeconds;
            var avgSecPerFrame = elapsedSec / progress.CurrentFrame;
            var remainingFrames = progress.TotalFrame - progress.CurrentFrame;
            var remainingTimeSpan = TimeSpan.FromSeconds(avgSecPerFrame * remainingFrames);
            remainingTime = $"{remainingTimeSpan:hh\\:mm\\:ss}";
        }
        var speed = progress.IsEmpty ?
            "" :
            $"x{(progress.CurrentFrame / (DateTimeOffset.Now - startDateTime).TotalSeconds) / progress.Fps:F1}";

        var sb = new StringBuilder();
        sb.AppendLine($"Progress Rate : {rate}");
        sb.AppendLine($"Remaining Time: {remainingTime}");
        sb.AppendLine($"Speed         : {speed}");
        sb.AppendLine($"Total Frame   : {progress.TotalFrame:#,0}");
        sb.AppendLine($"Current Frame : {progress.CurrentFrame:#,0}");
        sb.AppendLine($"Found Frame   : {progress.SectionFoundCount:#,0}");
        try
        {
            // cursor is invalid on CI/CD machine.
            if (_cursorTop.HasValue && _cursorLeft.HasValue)
            {
                Console.SetCursorPosition(_cursorLeft.Value, _cursorTop.Value);
            }
        }
        catch (IOException)
        { }
        Console.WriteLine(sb.ToString());
    }

    private bool ConfirmOverwrite()
    {
        Console.WriteLine("Result files is already exists.");
        Console.Write("Overwrite?(y/n): ");
        while (true)
        {
            switch (Console.ReadLine().ToLower())
            {
                case "y" or "yes":
                    return true;
                case "n" or "no":
                    return false;
                default:
                    continue;
            }
        }
    }
}