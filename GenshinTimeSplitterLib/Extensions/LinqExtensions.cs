using System;
using System.Collections.Generic;

namespace GenshinTimeSplitter.Extensions;

public static class LinqExtensions
{
    public static void DisposeAll<T>(this IEnumerable<T> own) where T : IDisposable
    {
        foreach (var e in own)
            e.Dispose();
    }
}
