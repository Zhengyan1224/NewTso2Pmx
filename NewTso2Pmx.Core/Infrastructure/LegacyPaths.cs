using System;
using System.IO;

namespace NewTso2Pmx.Core.Infrastructure;

public static class LegacyPaths
{
    public static string BaseDirectory { get; set; } = AppContext.BaseDirectory;

    public static string Resolve(params string[] segments)
    {
        var path = BaseDirectory;
        foreach (var segment in segments)
        {
            path = Path.Combine(path, segment);
        }

        return path;
    }
}
