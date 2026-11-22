using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using DRIT.Drawing.Brushes;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Pens;
using DRIT.Drawing.Text;
using DRIT.Drawing.Vector.Svg;
using DrawingFont = DRIT.Drawing.Text.Font;
using DrawingMetafile = DRIT.Drawing.Imaging.Metafile;

namespace DRIT.Drawing.Examples;

internal static class ExampleSupport
{
    internal static Bitmap NewCanvas(int width, int height, Color color)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(color);
        return bitmap;
    }

    internal static Bitmap CreatePhotoLike(int width = 480, int height = 300)
    {
        var bitmap = NewCanvas(width, height, Color.SkyBlue);
        using var graphics = Graphics.FromImage(bitmap);
        using var sun = new SolidBrush(Color.Gold);
        using var hills = new SolidBrush(Color.FromArgb(255, 56, 128, 83));
        using var foreground = new SolidBrush(Color.FromArgb(255, 35, 89, 62));
        graphics.FillEllipse(sun, new RectangleF(width - 130, 30, 80, 80));
        graphics.FillPolygon(hills, new[]
        {
            new PointF(0, height * .68f), new PointF(width * .28f, height * .30f),
            new PointF(width * .53f, height * .70f), new PointF(width * .78f, height * .38f),
            new PointF(width, height * .68f), new PointF(width, height), new PointF(0, height),
        });
        graphics.FillRectangle(foreground, 0, (int)(height * .8f), width, (int)(height * .2f));
        return bitmap;
    }

    internal static void Save(Bitmap bitmap, ExampleContext context, string name, ImageFormat format)
        => bitmap.Save(context.Path(name), format);
}

internal sealed class ExampleContext
{
    public ExampleContext(string outputDirectory)
        => OutputDirectory = System.IO.Path.GetFullPath(outputDirectory);

    public string OutputDirectory { get; }

    public string Path(string fileName) => System.IO.Path.Combine(OutputDirectory, fileName);

    public string EnsureGeneratedImage(string fileName)
    {
        string path = Path(fileName);
        if (!File.Exists(path))
        {
            using var bitmap = new Bitmap(360, 220, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            using var brush = new LinearGradientBrush(new PointF(0, 0), new PointF(360, 220), Color.SteelBlue, Color.LightYellow);
            graphics.FillRectangle(brush, 0, 0, 360, 220);
            bitmap.Save(path, ImageFormat.Png);
        }
        return path;
    }

    /// <summary>Writes the generated four-frame animated GIF used by the animation examples.</summary>
    public string EnsureAnimationFixture()
    {
        string path = Path("AnimationFrames-input.gif");
        File.WriteAllBytes(path, AnimatedGifFixture.Create());
        return path;
    }
}
