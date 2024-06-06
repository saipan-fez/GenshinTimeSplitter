using OpenCvSharp;
using System;

namespace GenshinTimeSplitter.Extensions;

public static class MatExtension
{
    // ref: https://github.com/saipan-fez/fez_analyzer/blob/master/src/FEZAnalyzer/FEZAnalyzer.Common/MatExtension.cs
    public static bool CompareAsRgbColor(this Mat<Vec3b> mat1, Mat<Vec3b> mat2, int threshold)
    {
        // Using OpenCV functions for comparison is slow as it targets the entire image;
        // hence, I opt for sequential in-memory comparison.
        //
        // Benchmark
        // OpenCV： Avg 13.5ms, Max 96ms, Min 1ms 
        // Custom： Avg  0.1ms, Max 31ms, Min 0ms

        // -------------
        // OpenCV
        // -------------
        //using (var diff = new Mat())
        //using (var range = new Mat())
        //{
        //    Cv2.Absdiff(mat1, mat2, diff);
        //    Cv2.InRange(diff, new Scalar(0, 0, 0), new Scalar(threshold, threshold, threshold), range);
        //    return Cv2.CountNonZero(range) == range.Cols * range.Rows * range.Channels();
        //}

        // -------------
        // Custom
        // -------------
        if (mat1.Cols != mat2.Cols || mat1.Rows != mat2.Rows || mat1.Dims != mat2.Dims)
        {
            return false;
        }

        var indexer1 = mat1.GetIndexer();
        var indexer2 = mat2.GetIndexer();

        for (int y = 0; y < mat1.Height; y++)
        {
            for (int x = 0; x < mat1.Width; x++)
            {
                var d0 = indexer1[y, x].Item0 - indexer2[y, x].Item0;
                var d1 = indexer1[y, x].Item1 - indexer2[y, x].Item1;
                var d2 = indexer1[y, x].Item2 - indexer2[y, x].Item2;
                if (Math.Abs(d0) > threshold ||
                    Math.Abs(d1) > threshold ||
                    Math.Abs(d2) > threshold)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
