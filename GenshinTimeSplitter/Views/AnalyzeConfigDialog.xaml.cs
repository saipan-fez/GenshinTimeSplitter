using GenshinTimeSplitter.Proc;
using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Rect = OpenCvSharp.Rect;
using Size = OpenCvSharp.Size;

namespace GenshinTimeSplitter.Views;

public partial class AnalyzeConfigDialog : Window
{
    private readonly ILogger<AnalyzeConfigDialog> _logger;
    private readonly string _movieFilePath;

    private LibVLC _libVlc;
    private MediaPlayer _mediaPlayer;
    private Media _media;
    private Size _movieResolution;

    public Rect[] Regions { get; private set; }

    public AnalyzeConfigDialog(
        ILogger<AnalyzeConfigDialog> logger,
        string movieFilePath)
    {
        _logger = logger;
        _movieFilePath = movieFilePath;

        InitializeComponent();

        Loaded += AnalyzeConfigDialog_Loaded;
        Closed += AnalyzeConfigDialog_Closed;
    }

    private async void AnalyzeConfigDialog_Loaded(object sender, RoutedEventArgs e)
    {
        _logger.LogDebug("prepair to play movie. file:{path}", _movieFilePath);

        await Task.Run(() =>
        {
            _libVlc = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVlc)
            {
                Volume = 0,
            };
            _media = new Media(_libVlc, _movieFilePath);

            Dispatcher.Invoke(() =>
            {
                player.MediaPlayer = _mediaPlayer;
            });

            _mediaPlayer.Media = _media;
            _mediaPlayer.Playing += _mediaPlayer_Playing;
            _mediaPlayer.EncounteredError += _mediaPlayer_EncounteredError;
            _mediaPlayer.Play();
        });

        _logger.LogDebug("movie started to play.");
    }

    private void AnalyzeConfigDialog_Closed(object sender, EventArgs e)
    {
        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _media?.Dispose();
        _libVlc?.Dispose();
    }

    private async void _mediaPlayer_Playing(object sender, EventArgs e)
    {
        _logger.LogDebug("movie is prepaired to start.");

        // MediaPlayer will not display the image if it is paused immediately,
        // so it waits 500ms before pausing and then seeks to the beginning to display the image.
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        _mediaPlayer.Pause();
        _mediaPlayer.Position = 0;

        uint width = 0, height = 0;
        _mediaPlayer.Size(0, ref width, ref height);
        _movieResolution = new(width, height);

        _logger.LogDebug("movie width:{width} height:{height}", width, height);

        Dispatcher.Invoke(() =>
        {
            player.Width  = width;
            player.Height = height;
            canvas.Width  = width;
            canvas.Height = height;
            PrepaireToAnalyzeRegion();

            // set seekbar
            Seekbar.Minimum = 0;
            Seekbar.Maximum = _mediaPlayer.Length;
            Seekbar.Value   = 0;
            Seekbar.ValueChanged += Seekbar_ValueChanged;

            LoadingGrid.Visibility = Visibility.Collapsed;

            _logger.LogDebug("enable to manipulate UI");
        });
    }

    private void _mediaPlayer_EncounteredError(object sender, EventArgs e)
    {
        _logger.LogError("media player is encountered error.");
    }

    private void PrepaireToAnalyzeRegion()
    {
        var config = AnalyzeConfig.GetDefault(_movieResolution);
        var regions = config.AnalyzeRegions;
        foreach (var item in new[] { R1, R2, R3, R4 }.Select((x, i) => new { ctrl = x, index = i }))
        {
            // [CAUTION]
            // Canvas Left/Top value type must be double.
            // If type is int, thrown exception.
            item.ctrl.Width  = regions[item.index].Width;
            item.ctrl.Height = regions[item.index].Height;
            item.ctrl.SetValue(Canvas.LeftProperty, (double)regions[item.index].Left);
            item.ctrl.SetValue(Canvas.TopProperty,  (double)regions[item.index].Top);

            item.ctrl.DragDelta += Region_DragDelta;
            item.ctrl.DragCompleted += Region_DragCompleted;
        }
    }

    private void Region_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is not FrameworkElement elm)
            return;

        var r     = _movieResolution;
        var left  = (double)elm.GetValue(Canvas.LeftProperty);
        var top   = (double)elm.GetValue(Canvas.TopProperty);

        double newLeft, newTop;
        newLeft = left + e.HorizontalChange;
        newLeft = Math.Min(r.Width - elm.Width, Math.Max(0, newLeft));
        newTop  = top + e.VerticalChange;
        newTop  = Math.Min(r.Height - elm.Height, Math.Max(0, newTop));

        elm.SetValue(Canvas.LeftProperty, newLeft);
        elm.SetValue(Canvas.TopProperty, newTop);
    }

    private void Region_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (sender is not FrameworkElement elm)
            return;

        // Convert the Left/Top values to integers upon completion of a drag.
        // [Reason] The range should be in pixels, which means it needs to be an integer.
        var left  = (double)elm.GetValue(Canvas.LeftProperty);
        var top   = (double)elm.GetValue(Canvas.TopProperty);
        elm.SetValue(Canvas.LeftProperty, Math.Round(left));
        elm.SetValue(Canvas.TopProperty, Math.Round(top));

        _logger.LogDebug("Region moved. name:{name} point:{x},{y}", elm.Name, left, top);
    }

    private void Seekbar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var time = TimeSpan.FromMilliseconds(Seekbar.Value);

        _logger.LogTrace("movie seeked from slider. time:{time}", time);

        _mediaPlayer.SeekTo(time);
        TimeTextBlock.Text = time.ToString(@"hh\:mm\:ss");
    }

    private void ControlButton_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag;
        if (tag is null || !int.TryParse(tag.ToString(), out var sec))
        {
            return;
        }

        var newValue = Seekbar.Value + TimeSpan.FromSeconds(sec).TotalMilliseconds;
        newValue = Math.Min(Seekbar.Maximum, Math.Max(newValue, Seekbar.Minimum));

        _logger.LogTrace("movie seeked from button. time_ms:{time}", newValue);

        Seekbar.Value = newValue;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag;
        if (tag?.ToString() is not "OK")
        {
            _logger.LogDebug("dialog closed [Cancel].");

            DialogResult = false;
            Close();
            return;
        }

        _logger.LogDebug("dialog closed [OK].");

        var regions = canvas.Children.OfType<FrameworkElement>()
            .Select(elm =>
            {
                var left = (double)elm.GetValue(Canvas.LeftProperty);
                var top  = (double)elm.GetValue(Canvas.TopProperty);
                return new Rect(
                    (int)left,
                    (int)top,
                    (int)elm.Width,
                    (int)elm.Height);
            })
            .ToArray();

        _logger.LogDebug("regions:{regions}", regions);

        Regions = regions;
        DialogResult = true;
        Close();
    }
}
