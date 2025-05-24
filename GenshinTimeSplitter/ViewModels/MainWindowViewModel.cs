using GenshinTimeSplitter.Proc;
using GenshinTimeSplitter.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace GenshinTimeSplitter.ViewModels;

public enum AnalyzeStateType
{
    CanNotStart,
    CanStart,
    Analyzing,
}

public class MainWindowViewModel : IDisposable
{
    // The DateTimePicker does not work correctly when the value is set to DateTime.MinValue.
    // Therefore, it is necessary to initialize it with a suitable date, and for this purpose, we define a based DateTime.
    private static readonly DateTime _rangeBaseDateTime = DateTime.Parse("2024/01/01 00:00:00");

    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ReactiveProperty<DateTime> StartRange { get; } = new(_rangeBaseDateTime);
    public ReactiveProperty<DateTime> EndRange { get; } = new(_rangeBaseDateTime);
    public ReactiveProperty<byte> DiffThreshold { get; } = new(0);
    public ReactiveProperty<byte> ThreadNum { get; } = new(0);
    public ReactiveProperty<int> FalseDetectionMs { get; } = new(0);
    public ReactiveProperty<OutputSectionMovieMode> OutputSectionMovie { get; } = new(OutputSectionMovieMode.Disable);

    public ReadOnlyReactiveProperty<string> MovieFilePath { get; }
    public ReadOnlyReactiveProperty<AnalyzeStateType> AnalyzeState { get; }
    public ReadOnlyReactiveProperty<TimeSpan> RangeTotalTimeSpan { get; }
    public ReadOnlyReactiveProperty<string> ProgressRate { get; }
    public ReadOnlyReactiveProperty<string> RemainingTime { get; }
    public ReadOnlyReactiveProperty<string> Speed { get; }
    public ReadOnlyReactiveProperty<string> TotalFrame { get; }
    public ReadOnlyReactiveProperty<string> CurrentFrame { get; }
    public ReadOnlyReactiveProperty<string> FoundFrame { get; }
    public ReadOnlyReactiveProperty<string> OutputSectionMovieProgress { get; }

    public AsyncReactiveCommand BrowseMovieFileCommand { get; }
    public AsyncReactiveCommand StartAnalyzeCommand { get; }
    public AsyncReactiveCommand CancelAnalyzeCommand { get; }
    public AsyncReactiveCommand OpenSettingWindowCommand { get; }

    private readonly CompositeDisposable _disposables = new();
    private readonly ReactiveProperty<string> _movieFilePath;
    private readonly ReactiveProperty<Progress> _progress;
    private readonly ReactiveProperty<AnalyzeStateType> _analyzeState;

    private DateTime _maximumRange = _rangeBaseDateTime;
    private AnalyzeConfig? _analyzeConfig;
    private SectionStartAnalyzer _sectionStartAnalyzer;
    private CancellationTokenSource _cts;
    private DateTimeOffset _analyzeStartTime;

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        _movieFilePath = new ReactiveProperty<string>()
            .AddTo(_disposables);
        _progress = new ReactiveProperty<Progress>(Progress.Empty())
            .AddTo(_disposables);
        _analyzeState = new ReactiveProperty<AnalyzeStateType>(AnalyzeStateType.CanNotStart)
            .AddTo(_disposables);

        #region ReadOnlyReactiveProperty
        MovieFilePath = _movieFilePath
            .ToReadOnlyReactiveProperty()
            .AddTo(_disposables);
        AnalyzeState = _analyzeState
            .ToReadOnlyReactiveProperty()
            .AddTo(_disposables);
        RangeTotalTimeSpan = Observable.CombineLatest(
            StartRange,
            EndRange,
            (start, end) => end - start)
            .ToReadOnlyReactiveProperty()
            .AddTo(_disposables);
        ProgressRate = _progress
            .Select(x => x.IsEmpty ? "" : $"{(double)x.CurrentFrame / x.TotalFrame *100:F1}%")
            .ToReadOnlyReactiveProperty()
            .AddTo(_disposables);
        RemainingTime = _progress
            .Select(x =>
            {
                if (x.IsEmpty || x.CurrentFrame is 0)
                {
                    return "";
                }
                else
                {
                    var elapsedSec = (DateTimeOffset.Now - _analyzeStartTime).TotalSeconds;
                    var avgSecPerFrame = elapsedSec / x.CurrentFrame;
                    var remainingFrames = x.TotalFrame - x.CurrentFrame;
                    var remainingTimeSpan = TimeSpan.FromSeconds(avgSecPerFrame * remainingFrames);
                    return $"{remainingTimeSpan:hh\\:mm\\:ss}";
                }
            })
            .ToReadOnlyReactiveProperty()
            .AddTo(_disposables);
        Speed = _progress
            .Select(x => x.IsEmpty ? "" : $"x{(x.CurrentFrame / (DateTimeOffset.Now - _analyzeStartTime).TotalSeconds) / x.Fps:F1}")
            .ToReadOnlyReactiveProperty()
            .AddTo(_disposables);
        TotalFrame = _progress
            .Select(x => x.IsEmpty ? "" : $"{x.TotalFrame:#,0}")
            .ToReadOnlyReactiveProperty()
            .AddTo(_disposables);
        CurrentFrame = _progress
            .Select(x => x.IsEmpty ? "" : $"{x.CurrentFrame:#,0}")
            .ToReadOnlyReactiveProperty()
            .AddTo(_disposables);
        FoundFrame = _progress
            .Select(x => x.IsEmpty ? "" : $"{x.SectionFoundCount:#,0}")
            .ToReadOnlyReactiveProperty()
            .AddTo(_disposables);
        OutputSectionMovieProgress = _progress
            .Select(x =>
            {
                if (x.IsEmpty)
                    return "";
                if (OutputSectionMovie.Value is OutputSectionMovieMode.Disable)
                    return "-";
                else
                    return x.TotalOutputSectionMovieCount != -1 ?
                        $"{x.CurrentOutputSectionMovieCount}/{x.TotalOutputSectionMovieCount}":
                        $"-";
            })
            .ToReadOnlyReactiveProperty()
            .AddTo(_disposables);
        #endregion

        #region Command
        BrowseMovieFileCommand = _analyzeState
            .Select(x => x is AnalyzeStateType.CanNotStart or AnalyzeStateType.CanStart)
            .ToAsyncReactiveCommand()
            .WithSubscribe(SelectMovieFilePathAsync)
            .AddTo(_disposables);

        StartAnalyzeCommand = _analyzeState
            .Select(x => x is AnalyzeStateType.CanStart)
            .ToAsyncReactiveCommand()
            .WithSubscribe(StartAsync)
            .AddTo(_disposables);

        CancelAnalyzeCommand = _analyzeState
            .Select(x => x is AnalyzeStateType.Analyzing)
            .ToAsyncReactiveCommand()
            .WithSubscribe(CancelAsync)
            .AddTo(_disposables);

        OpenSettingWindowCommand = _analyzeState
            .Select(x => x is AnalyzeStateType.CanStart)
            .ToAsyncReactiveCommand()
            .WithSubscribe(OpenSettingWindowAsync)
            .AddTo(_disposables);
        #endregion

        #region value change subscriber
        StartRange.Subscribe(x =>
        {
            if (x < _rangeBaseDateTime)
                StartRange.Value = _rangeBaseDateTime;
            else if (x > _maximumRange)
                StartRange.Value = _maximumRange;

            _logger.LogDebug("StartRange is updated. value:{value}", StartRange.Value);
        }).AddTo(_disposables);
        EndRange.Subscribe(x =>
        {
            if (x < _rangeBaseDateTime)
                EndRange.Value = _rangeBaseDateTime;
            else if (x > _maximumRange)
                EndRange.Value = _maximumRange;

            _logger.LogDebug("EndRange is updated. value:{value}", EndRange.Value);
        }).AddTo(_disposables);

        DiffThreshold.Subscribe(async x =>
        {
            if (_analyzeConfig.HasValue && x != _analyzeConfig.Value.DiffThreashold)
            {
                _logger.LogDebug("DiffThreashold is updated. value:{value}", x);

                _analyzeConfig = _analyzeConfig.Value with { DiffThreashold = x };
                await SaveAnalyzeConfigAsync();
            }
        }).AddTo(_disposables);
        ThreadNum.Subscribe(async x =>
        {
            if (_analyzeConfig.HasValue && x != _analyzeConfig.Value.ParallelCount)
            {
                _logger.LogDebug("ThreadNum is updated. value:{value}", x);

                _analyzeConfig = _analyzeConfig.Value with { ParallelCount = x };
                await SaveAnalyzeConfigAsync();
            }
        }).AddTo(_disposables);
        FalseDetectionMs.Subscribe(async x =>
        {
            if (_analyzeConfig.HasValue && x != _analyzeConfig.Value.FalseDetectionMilliSeconds)
            {
                _logger.LogDebug("FalseDetectionMilliSeconds is updated. value:{value}", x);

                _analyzeConfig = _analyzeConfig.Value with { FalseDetectionMilliSeconds = x };
                await SaveAnalyzeConfigAsync();
            }
        }).AddTo(_disposables);
        OutputSectionMovie.Subscribe(async x =>
        {
            if (_analyzeConfig.HasValue && x != _analyzeConfig.Value.OutputSectionMovie)
            {
                _logger.LogDebug("OutputSectionMovie is updated. value:{value}", x);

                _analyzeConfig = _analyzeConfig.Value with { OutputSectionMovie = x };
                await SaveAnalyzeConfigAsync();
            }
        }).AddTo(_disposables);

        _analyzeState.Subscribe(s =>
        {
            _logger.LogDebug("Analayze state is updated. state:{state}", s);
        }).AddTo(_disposables);
        #endregion
    }

    private bool _disposed = false;
    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                CancelAnalyze();

                _disposables.Dispose();

                _disposed = true;

                _logger.LogDebug("disposed");
            }
            catch
            { }
        }
    }

    #region Command
    private async Task SelectMovieFilePathAsync()
    {
        _logger.LogInformation("SelectMovieFilePathAsync");

        if (_analyzeState.Value is not AnalyzeStateType.CanStart and not AnalyzeStateType.CanNotStart)
        {
            _logger.LogDebug("can not select. Reason: state is invalid. {state}", _analyzeState.Value);
            return;
        }

        using var scope = _serviceProvider.CreateScope();

        var dialog = new OpenFileDialog
        {
            DefaultExt = ".mp4",
            Multiselect = false,
            Filter = "Movie (.mp4)|*.mp4"
        };
        if (!(dialog.ShowDialog() ?? false))
        {
            _logger.LogDebug("can not start. Reason: user does not select file.");
            return;
        }

        try
        {
            var filePath = dialog.FileName;

            // initialize analyzer
            _sectionStartAnalyzer?.Dispose();
            _sectionStartAnalyzer = await SectionStartAnalyzer.LoadAsync(
                _serviceProvider.GetService<ILogger<SectionStartAnalyzer>>(),
                filePath);
            _sectionStartAnalyzer.ProgressChanged += (_, progress) => _progress.Value = progress;

            _logger.LogDebug("intialized SectionStartAnalyzer");

            // load analyze config
            var analyzeConfigStore = scope.ServiceProvider.GetService<AnalyzeConfigStore>();
            if (analyzeConfigStore.TryLoad(_sectionStartAnalyzer.MovieResolution, out var config))
            {
                _analyzeConfig = config;
                _logger.LogDebug("loaded analyze config");
            }
            else
            {
                _analyzeConfig = AnalyzeConfig.GetDefault(_sectionStartAnalyzer.MovieResolution);
                _logger.LogDebug("loaded default analyze config");
            }

            // update field values
            DiffThreshold.Value = _analyzeConfig.Value.DiffThreashold;
            ThreadNum.Value = _analyzeConfig.Value.ParallelCount;
            FalseDetectionMs.Value = _analyzeConfig.Value.FalseDetectionMilliSeconds;
            OutputSectionMovie.Value = _analyzeConfig.Value.OutputSectionMovie;

            var movieEndDateTime = _rangeBaseDateTime + _sectionStartAnalyzer.MovieTimeSpan;
            _maximumRange = movieEndDateTime;
            StartRange.Value = _rangeBaseDateTime;
            EndRange.Value = movieEndDateTime;

            _movieFilePath.Value = filePath;

            // finally update state
            _analyzeState.Value = AnalyzeStateType.CanStart;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "unexpected error.");
            ShowErrorMessage(ex);
        }
    }

    private async Task StartAsync()
    {
        _logger.LogInformation("StartAsync");

        if (_analyzeState.Value is not AnalyzeStateType.CanStart)
        {
            _logger.LogDebug("can not start. Reason: state is invalid. {state}", _analyzeState.Value);
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var analyzeResultStore = scope.ServiceProvider.GetService<AnalyzeResultStore>();
            if (analyzeResultStore.Exists(MovieFilePath.Value))
            {
                if (!ConfirmToOverwrite())
                {
                    _logger.LogDebug("can not start. Reason: user does not allow to overwrite");
                    return;
                }
            }

            // cancel previous analyze if exists
            CancelAnalyze();

            // update state
            _analyzeState.Value = AnalyzeStateType.Analyzing;

            // prepair to analyze
            _cts = new();
            var token = _cts.Token;
            _analyzeStartTime = DateTimeOffset.Now;

            _logger.LogDebug("starting to analyze.");

            // analyze
            var startRange = StartRange.Value - _rangeBaseDateTime;
            var endRange   = EndRange.Value   - _rangeBaseDateTime;
            var analyzeResult = await _sectionStartAnalyzer.AnalyzeAsync(
                startRange,
                endRange,
                _analyzeConfig.Value,
                _cts.Token);

            _logger.LogDebug("finished to analyze.");

            // save result
            await analyzeResultStore.SaveAsync(MovieFilePath.Value, analyzeResult);

            _logger.LogDebug("finished to save result files.");

            // show message
            ShowCompletedMessage();

            _logger.LogInformation("Succeeded to analyze.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Analyze is canceled.");
        }
        catch (AnalyzeConfigException ex)
        {
            _logger.LogError(ex, "analyze config error.");
            ShowErrorMessage(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "unexpected error.");
            ShowErrorMessage(ex);
        }
        finally
        {
            // update state
            _analyzeState.Value = AnalyzeStateType.CanStart;
        }
    }

    private async Task CancelAsync()
    {
        _logger.LogInformation("CancelAsync");

        if (_analyzeState.Value is not AnalyzeStateType.Analyzing)
        {
            _logger.LogDebug("can not cancel. Reason: state is invalid. {state}", _analyzeState.Value);
            return;
        }

        try
        {
            if (!ConfirmToCancel())
            {
                _logger.LogDebug("can not cancel. Reason: user does not allow to cancel");
                return;
            }

            CancelAnalyze();

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "unexpected error.");
            ShowErrorMessage(ex);
        }
    }

    private async Task OpenSettingWindowAsync()
    {
        _logger.LogInformation("Open SettingWindow");

        if (_analyzeState.Value is not AnalyzeStateType.CanStart)
        {
            _logger.LogDebug("can not open. Reason: state is invalid. {state}", _analyzeState.Value);
            return;
        }

        try
        {
            var analyzeConfigDialog = new AnalyzeConfigDialog(
                _serviceProvider.GetService<ILogger<AnalyzeConfigDialog>>(),
                MovieFilePath.Value);
            if (analyzeConfigDialog.ShowDialog() ?? false)
            {
                var regions = analyzeConfigDialog.Regions;
                _analyzeConfig = _analyzeConfig.HasValue ?
                    _analyzeConfig.Value with { AnalyzeRegions = regions } :
                    AnalyzeConfig.GetDefault(_sectionStartAnalyzer.MovieResolution) with { AnalyzeRegions = regions };

                await SaveAnalyzeConfigAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "unexpected error.");
            ShowErrorMessage(ex);
        }
    }
    #endregion

    #region Private Methods
    private async Task SaveAnalyzeConfigAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var analyzeConfigStore = scope.ServiceProvider.GetService<AnalyzeConfigStore>();
            await analyzeConfigStore.SaveAsync(_analyzeConfig.Value);

            _logger.LogDebug("saved config file. config:{config}", _analyzeConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "unexpected error.");
            ShowErrorMessage(ex);
        }
    }

    private void CancelAnalyze()
    {
        try
        {
            if (_cts is not null)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;

                _logger.LogDebug("canceled to analyze.");
            }

            _analyzeState.Value = AnalyzeStateType.CanStart;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "unexpected error.");
            ShowErrorMessage(ex);
        }
    }
    #endregion

    #region Message
    private void ShowCompletedMessage()
    {
        var elapsedTimeSpan = DateTime.Now - _analyzeStartTime;
        MessageBox.Show(
            $"""
            Finished to analyze sections.
            ElapsedTime - {elapsedTimeSpan:hh\:mm\:ss}
            """,
            "Completed!",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );
    }

    private void ShowErrorMessage(Exception ex)
    {
        MessageBox.Show(
            $"""
             Unexpected error has occured...

             {ex.GetType().FullName}
             {ex.Message}
             {ex.StackTrace}
             """,
            "Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );
    }

    private bool ConfirmToCancel()
    {
        var result = MessageBox.Show(
            "Cancel?",
            "Confirm",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );
        return result is MessageBoxResult.Yes;
    }

    private bool ConfirmToOverwrite()
    {
        var result = MessageBox.Show(
            $"""
            Result files is already exists.
            Overwrite?
            """,
            "Confirm",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        );
        return result is MessageBoxResult.Yes;
    }
    #endregion
}
