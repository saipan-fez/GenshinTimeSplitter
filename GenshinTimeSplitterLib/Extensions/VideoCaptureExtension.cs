using OpenCvSharp;
using System;

namespace GenshinTimeSplitter.Extensions;

public static class VideoCaptureExtension
{
    // !!! ATTENTION !!!
    // In the VideoCapture class, the values of PosMsec and FrameCount can be inaccurate.
    // So we will calculate it originally from VideoCapture.PosFrames.
    // ref: https://github.com/opencv/opencv/issues/15749
    // ref: https://stackoverflow.com/a/41684390

    public static TimeSpan GetPosTimeSpan(this VideoCapture videoCapture)
    {
        var currentFramePos = videoCapture.PosFrames;
        var fps = videoCapture.Fps;
        return TimeSpan.FromSeconds(currentFramePos / fps);
    }

    public static void Seek(this VideoCapture videoCapture, TimeSpan timeSpan)
    {
        var fps = videoCapture.Fps;
        var seekFrame = (int)Math.Round(fps * timeSpan.TotalSeconds);
        videoCapture.PosFrames = seekFrame;
    }
}
