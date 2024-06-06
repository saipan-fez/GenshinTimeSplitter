using GenshinTimeSplitter.Extensions;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GenshinTimeSplitter.Proc;

public readonly record struct Progress(double Fps, int TotalFrame, int CurrentFrame, int SectionFoundCount)
{
    public static Progress Empty() => new() { IsEmpty = true };
    public bool IsEmpty { get; private init; } = false;
};

public class AnalyzeConfigException : Exception
{
    public AnalyzeConfigException(string message) : base(message) { }
}

public sealed class SectionStartAnalyzer : IDisposable
{
    private readonly ILogger<SectionStartAnalyzer> _logger;
    private readonly VideoCapture _videoCapture;

    public delegate void ProgressEvent(object sender, Progress progress);
    public event ProgressEvent ProgressChanged;

    public Size MovieResolution { get; }
    public TimeSpan MovieTimeSpan { get; }

    private SectionStartAnalyzer(
        ILogger<SectionStartAnalyzer> logger,
        string filePath,
        bool isUseHWAcc = false)
    {
        _logger = logger;

        if (isUseHWAcc)
        {
            // this setting is slower than default setting(no HWAcc).
            // probably memory copy speed from gpu to cpu is too slow.
            var param = new VideoCapturePara(VideoAccelerationType.D3D11, 0);
            _videoCapture = new VideoCapture(filePath, VideoCaptureAPIs.MSMF, param);
        }
        else
        {
            _videoCapture = new VideoCapture(filePath, VideoCaptureAPIs.ANY);
        }

        _logger.LogDebug("VideoCapture initialized.");
        _logger.LogDebug("file:{file}", filePath);
        _logger.LogDebug("width:{width} height:{height} frames:{frames} fps:{fps}",
            _videoCapture.FrameWidth,
            _videoCapture.FrameHeight,
            _videoCapture.FrameCount,
            _videoCapture.Fps);

        MovieResolution = new Size(_videoCapture.FrameWidth, _videoCapture.FrameHeight);
        MovieTimeSpan = TimeSpan.FromSeconds(_videoCapture.FrameCount / _videoCapture.Fps);
    }

    public void Dispose()
    {
        try
        {
            if (_videoCapture.IsEnabledDispose)
            {
                _videoCapture.Dispose();
            }
            _logger.LogDebug("disposed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "unexpected error.");
        }
    }

    public static async Task<SectionStartAnalyzer> LoadAsync(
        ILogger<SectionStartAnalyzer> logger,
        string filePath)
    {
        var splitter = await Task.Run(() => new SectionStartAnalyzer(logger, filePath));
        return splitter;
    }

    public async Task<AnalyzeResult> AnalyzeAsync(
        TimeSpan analyzeStartTimeSpan,
        TimeSpan analyzeEndTimeSpan,
        AnalyzeConfig config,
        CancellationToken token)
    {
        try
        {
            _logger.LogDebug("AnalyzeAsync is started.");
            _logger.LogDebug("start:{start} end:{end} config:{config}", analyzeStartTimeSpan, analyzeEndTimeSpan, config);

            // validate config
            ThrowIfInvalidAnalyzeConfig(config);

            // prepair to start
            using LoadingScreenAnalyzer loadingScreenAnalyzeRegions = new(config.AnalyzeRegions);
            _analyzedFrameCount = 0;
            _foundSectionFrameCount = 0;
            _fps = _videoCapture.Fps;
            _totalFrameCount = (int)((analyzeEndTimeSpan - analyzeStartTimeSpan).TotalSeconds * _fps);
            _frameInfoCollection.Clear();
#if DEBUG
            var parallelCount = 1;
#else
            var parallelCount = config.ParallelCount >= 1 ?
                config.ParallelCount :
                Environment.ProcessorCount;
#endif
            _logger.LogDebug("parallelCount:{count}", parallelCount);

            // seek start frame
            _logger.LogTrace("seek videocapture to start position");
            _videoCapture.PosMsec = (int)analyzeStartTimeSpan.TotalMilliseconds;

            // add start frame info
            _frameInfoCollection.Add(new FrameInfo(
                _videoCapture.PosFrames,
                TimeSpan.FromMilliseconds(_videoCapture.PosMsec),
                ScreenType.AnalyzeStart));

            // raise event at started
            InvokeProgressChanged(0, 0);

            // start to analyze frames by multi threads
            _logger.LogDebug("started to analyze frames.");
            var taskCollection = new List<Task>();
            for (var i = 0; i < parallelCount; i++)
            {
                var task = Task.Factory.StartNew(
                    () => Analyze(
                        analyzeEndTimeSpan,
                        loadingScreenAnalyzeRegions,
                        config,
                        token),
                    token);
                taskCollection.Add(task);
            }

            // wait threads
            await Task.WhenAll(taskCollection);
            token.ThrowIfCancellationRequested();

            // add end frame info
            _videoCapture.PosMsec = (int)analyzeEndTimeSpan.TotalMilliseconds;
            _frameInfoCollection.Add(new FrameInfo(
                _videoCapture.PosFrames,
                TimeSpan.FromMilliseconds(_videoCapture.PosMsec),
                ScreenType.AnalyzeEnd));

            _logger.LogDebug("finished to analyze frames.");

            // raise event at ended
            InvokeProgressChanged(_totalFrameCount, _foundSectionFrameCount);

            _logger.LogTrace("frameInfoCollection:{collection}", _frameInfoCollection);

            // get section start time from analyzed result
            var sectionInfoCollection = GetSectionInfoCollection(
                analyzeStartTimeSpan,
                analyzeEndTimeSpan,
                _frameInfoCollection);
            _logger.LogDebug("got section info. count:{count}", sectionInfoCollection.Length);

            return new AnalyzeResult(
                analyzeStartTimeSpan,
                analyzeEndTimeSpan,
                sectionInfoCollection);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AnalyzeConfigException ex)
        {
            _logger.LogError(ex, "config error");
            throw;
        }
        catch
        {
            throw;
        }
    }

    private void ThrowIfInvalidAnalyzeConfig(AnalyzeConfig config)
    {
        var w = _videoCapture.FrameWidth;
        var h = _videoCapture.FrameHeight;

        if (config.AnalyzeRegions.Length <= 0)
            throw new AnalyzeConfigException("AnalyzeRegions must be >0");

        if (config.AnalyzeRegions.Any(x => x.Width <= 0 || x.Height <= 0))
            throw new AnalyzeConfigException($"AnalyzeRegion sizes must be >0");

        if (config.AnalyzeRegions.Any(x =>
                x.Left   < 0 || w - 1 < x.Left  ||
                x.Right  < 0 || w - 1 < x.Right ||
                x.Top    < 0 || h - 1 < x.Top   ||
                x.Bottom < 0 || h - 1 < x.Bottom))
            throw new AnalyzeConfigException($"AnalyzeRegion points must be [Top/Bottom]0-{h - 1} [Left/Right]0-{w - 1}");
    }

    private SectionInfo[] GetSectionInfoCollection(
        TimeSpan analyzeStartTimeSpan,
        TimeSpan analyzeEndTimeSpan,
        IEnumerable<FrameInfo> frameInfoCollection)
    {
        const int accept_false_positives_frame_count = 3;

        var array = frameInfoCollection
            .Where(x => x.FrameTimeSpan >= analyzeStartTimeSpan)
            .Where(x => x.ScreenType is not ScreenType.Other)
            .OrderBy(x => x.FramePos)
            .Select((x, i) => new { Value = x, Index = i })
            .ToArray();

        // Yelan Q etc are occasionally misidentified as a LoadingScreen.
        // To address this issue, the frame that is more than "3" frames apart from the surrounding elements is excluded.
        var sortedFrameInfoArray = array
            .Where(x =>
            {
                if (x.Value.ScreenType is ScreenType.LoadingScreen &&
                    x.Index != 0 &&
                    x.Index != array.Length - 1)
                {
                    var currentFramePos = x.Value.FramePos;
                    var prevFramePos    = array[x.Index - 1].Value.FramePos;
                    var nextFramePos    = array[x.Index + 1].Value.FramePos;
                    return
                        Math.Abs(currentFramePos - prevFramePos) <= accept_false_positives_frame_count ||
                        Math.Abs(currentFramePos - nextFramePos) <= accept_false_positives_frame_count;
                }
                else
                {
                    return true;
                }
            })
            .Select(x => x.Value)
            .ToArray();

        var sectionInfo = new SectionInfo()
        {
            SectionStartedTimeSpan = analyzeStartTimeSpan
        };

        var i = 0;
        var sectionInfoCollection = new List<SectionInfo>();
        while (i < sortedFrameInfoArray.Length - 1)
        {
            var currentFrame = sortedFrameInfoArray[i];

            if (currentFrame.ScreenType is ScreenType.MapScreen &&
                sectionInfo.MapOpenedTimeSpan.HasValue is false)
            {
                _logger.LogTrace("MapScreen found. {frame}", currentFrame);
                sectionInfo = sectionInfo with { MapOpenedTimeSpan = currentFrame.FrameTimeSpan };
                i++;
            }
            else if (currentFrame.ScreenType is ScreenType.LoadingScreen)
            {
                _logger.LogTrace("LoadingScreen found. {frame}", currentFrame);

                sectionInfo = sectionInfo with { LoadScreenStartedTimeSpan = currentFrame.FrameTimeSpan };
                sectionInfoCollection.Add(sectionInfo);

                // seek to end of loading screen frame
                // - Next frame is not LoadingScreen
                // - Next frame position is over "3" frame
                //   -> There may be cases where analysis is not successful due to noise in the video.
                for (; i < sortedFrameInfoArray.Length - 1; i++)
                {
                    var f1 = sortedFrameInfoArray[i];
                    var f2 = sortedFrameInfoArray[i + 1];
                    bool IsLoadingScreenEnd()
                    {
                        return
                            f1.ScreenType is ScreenType.LoadingScreen &&
                            f2.ScreenType is ScreenType.LoadingScreen &&
                            f2.FramePos - f1.FramePos > accept_false_positives_frame_count;
                    }
                    if (f2.ScreenType is not ScreenType.LoadingScreen || IsLoadingScreenEnd())
                    {
                        // Update the sectionInfo for the next section
                        var nextSecionStartTime = f1.FrameTimeSpan + TimeSpan.FromSeconds(1d / _fps);
                        sectionInfo = new SectionInfo()
                        {
                            SectionStartedTimeSpan = nextSecionStartTime
                        };
                        i++;

                        _logger.LogTrace("LoadingScreen End found. {frame}", currentFrame);

                        break;
                    }
                }
            }
            else
            {
                i++;
            }
        }
        // add end
        if (sectionInfo.LoadScreenStartedTimeSpan is null)
        {
            sectionInfo = sectionInfo with { LoadScreenStartedTimeSpan = analyzeEndTimeSpan };
            sectionInfoCollection.Add(sectionInfo);
        }

        return sectionInfoCollection.ToArray();
    }

    private readonly record struct FrameInfo(int FramePos, TimeSpan FrameTimeSpan, ScreenType ScreenType);
    private readonly ConcurrentBag<FrameInfo> _frameInfoCollection = new();
    private readonly object _lockObj = new();
    private int _analyzedFrameCount = 0;
    private int _foundSectionFrameCount = 0;
    private double _fps = 0d;
    private int _totalFrameCount = 0;
    private void Analyze(
        TimeSpan analyzeEndTimeSpan,
        LoadingScreenAnalyzer loadingScreenAnalyzeRegions,
        AnalyzeConfig config,
        CancellationToken token)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            using var frameMat = new Mat<Vec3b>(new Size(_videoCapture.FrameWidth, _videoCapture.FrameHeight));
            while (true)
            {
                token.ThrowIfCancellationRequested();

                var startToReadFrameTime = sw.Elapsed;

                // read frame
                TimeSpan currentFrameTimeSpan;
                int currentFrameCount;
                bool result;
                lock (_lockObj)
                {
                    result = _videoCapture.Read(frameMat);
                    currentFrameTimeSpan = TimeSpan.FromMilliseconds(_videoCapture.PosMsec);
                    currentFrameCount = _videoCapture.PosFrames;
                    _logger.LogTrace("read frame. frame:{frame}", currentFrameCount);
                }

                var startToAnalyzeTime = sw.Elapsed;

                if (!result)
                {
                    _logger.LogTrace("finish to read frame to end.");
                    break;
                }
                if (currentFrameTimeSpan > analyzeEndTimeSpan)
                {
                    _logger.LogTrace("finish to read frame to EndTime.");
                    break;
                }

                // analyze whether the frame is loading screen
                var screenType = AnalyzeFrame(frameMat, config.DiffThreashold, loadingScreenAnalyzeRegions);
                _logger.LogTrace("analyzed frame. frame:{frame} type:{type}", currentFrameCount, screenType);

                // add frame info to collection if ScreenType is not Other
                if (screenType is ScreenType.LoadingScreen or ScreenType.MapScreen)
                {
                    _frameInfoCollection.Add(new(currentFrameCount, currentFrameTimeSpan, screenType));
                    Interlocked.Increment(ref _foundSectionFrameCount);
                }

                // report progress per 100 frames
                var analyzedFrameCount = Interlocked.Increment(ref _analyzedFrameCount);
                if (analyzedFrameCount % 100 == 0)
                {
                    InvokeProgressChanged(analyzedFrameCount, _foundSectionFrameCount);
                }

                var readTime = startToAnalyzeTime - startToReadFrameTime;
                var analyzeTime = sw.Elapsed - startToAnalyzeTime;
                _logger.LogTrace("read: {read_time} analyze:{analyze_time}", readTime, analyzeTime);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogTrace("task canceled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "unhandled error");
            throw;
        }
    }

    private void InvokeProgressChanged(int currentFrame, int sectionFoundCount)
    {
        _logger.LogTrace("ProgressChanged. current:{current} found:{found}", currentFrame, sectionFoundCount);
        ProgressChanged?.Invoke(this, new Progress(_fps, _totalFrameCount, currentFrame, sectionFoundCount));
    }

    private enum ScreenType
    {
        AnalyzeStart,
        AnalyzeEnd,
        Other,
        MapScreen,
        LoadingScreen
    }

    private ScreenType AnalyzeFrame(
        Mat<Vec3b> frameMat,
        int diffThreshold,
        LoadingScreenAnalyzer loadingScreenAnalyzer)
    {
        var isLoadingScreen = loadingScreenAnalyzer.IsLoadingScreen(frameMat, diffThreshold);
        if (isLoadingScreen)
        {
            return ScreenType.LoadingScreen;
        }

        // TODO: check map screen

        return ScreenType.Other;
    }

    private class LoadingScreenAnalyzer : IDisposable
    {
        private List<LoadingScreenAnalyzeRegion> _regions;

        public LoadingScreenAnalyzer(IEnumerable<Rect> regions)
        {
            _regions = new(regions.Select(x => new LoadingScreenAnalyzeRegion(x)));
        }

        public void Dispose()
        {
            _regions.DisposeAll();
        }

        public bool IsLoadingScreen(Mat<Vec3b> frameMat, int diffThreshold)
        {
            // new Mat<Vec3b>(frameMat, r.Rect) is only memory referenced of frameMat.
            // so not need to dispose for releasing memory.
            var blackLoadingCompares = _regions
                .Select(r => (new Mat<Vec3b>(frameMat, r.Region), r.BlackLoadingScreenMat));
            var whiteLoadingCompares = _regions
                .Select(r => (new Mat<Vec3b>(frameMat, r.Region), r.WhiteLoadingScreenMat));
            var blankLoadingCompares = _regions
                .Select(r => (new Mat<Vec3b>(frameMat, r.Region), r.BlankLoadingScreenMat));

            // WhiteLoadingScreen: Displayed when the in-game time is 06:00–18:00 (daytime).
            // BlackLoadingScreen: Displayed when the in-game time is 18:00–06:00 (nighttime).
            // BlankLoadingScreen: Displayed when the character's position and warp position are extremely close.
            //
            // Sample images are "doc/img/loading_screen_***.png"

            var isBlackLoadingScreen = IsLoadingScreen(blackLoadingCompares, diffThreshold);
            var isWhiteLoadingScreen = IsLoadingScreen(whiteLoadingCompares, diffThreshold);
            var isBlankLoadingScreen = IsLoadingScreen(blankLoadingCompares, diffThreshold);

            return isBlackLoadingScreen || isWhiteLoadingScreen || isBlankLoadingScreen;
        }

        private static bool IsLoadingScreen(
            IEnumerable<(Mat<Vec3b>, Mat<Vec3b>)> compares,
            int diffThreshold)
        {
            // One of the cropped Mats may include the mouse cursor;
            // thus, one false is acceptable.
            var notLoadingScreenCount = 0;
            foreach (var e in compares)
            {
                var result = e.Item1.CompareAsRgbColor(e.Item2, diffThreshold);
                if (result is false)
                {
                    notLoadingScreenCount++;

                    if (notLoadingScreenCount > 1)
                        return false;
                }
            }

            return true;
        }
    }

    private class LoadingScreenAnalyzeRegion : IDisposable
    {
        // color format: BGR
        private static readonly Vec3b _blackVec = new( 35,  27,  29);
        private static readonly Vec3b _whiteVec = new(255, 255, 255);
        private static readonly Vec3b _blankVec = new(  0,   0,   0);

        public Rect Region { get; }
        public Mat<Vec3b> BlackLoadingScreenMat { get; }
        public Mat<Vec3b> WhiteLoadingScreenMat { get; }
        public Mat<Vec3b> BlankLoadingScreenMat { get; }

        public LoadingScreenAnalyzeRegion(Rect region)
        {
            Region = region;
            BlackLoadingScreenMat = new(region.Height, region.Width);
            BlackLoadingScreenMat.SetTo(_blackVec);
            WhiteLoadingScreenMat = new(region.Height, region.Width);
            WhiteLoadingScreenMat.SetTo(_whiteVec);
            BlankLoadingScreenMat = new(region.Height, region.Width);
            BlankLoadingScreenMat.SetTo(_blankVec);
        }

        public void Dispose()
        {
            BlackLoadingScreenMat.Dispose();
            WhiteLoadingScreenMat.Dispose();
            BlankLoadingScreenMat.Dispose();
        }
    }
}
