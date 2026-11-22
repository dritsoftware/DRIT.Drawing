using System;
using System.IO;

namespace DRIT.Drawing.Tests;

internal static class TestFont
{
    public static string Path
    {
        get
        {
            string path = System.IO.Path.Combine(AppContext.BaseDirectory, "TestAssets", "Fonts", "Lato-Regular.ttf");
            Assert.IsTrue(File.Exists(path), $"Required test font is missing: {path}");
            return path;
        }
    }
}
