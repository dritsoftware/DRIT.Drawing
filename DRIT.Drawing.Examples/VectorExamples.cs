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
using DRIT.Drawing.Metafile;
using DRIT.Drawing.Pens;
using DRIT.Drawing.Text;
using DRIT.Drawing.Vector.Svg;
using DrawingFont = DRIT.Drawing.Text.Font;
using DrawingMetafile = DRIT.Drawing.Imaging.Metafile;

namespace DRIT.Drawing.Examples;

/// <summary>
/// Contains runnable examples for recording and loading SVG, reusing vector scenes, working with metafiles, and exporting multiple formats.
/// </summary>
internal static class VectorExamples
{
    /// <summary>
    /// Records drawing commands and saves them as true vector SVG output.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region RecordSvg
    internal static void VectorRecordAsSvg(ExampleContext context)
    {
        using var svg = new SvgImage(720, 420);
        using (var graphics = svg.CreateGraphics())
        using (var pen = new Pen(Color.DarkBlue, 5))
        using (var brush = new LinearGradientBrush(new PointF(0, 0), new PointF(720, 420), Color.LightSkyBlue, Color.MediumPurple))
        {
            graphics.FillRectangle(brush, 0, 0, 720, 420);
            graphics.DrawEllipse(pen, new RectangleF(120, 85, 480, 250));
            graphics.DrawLine(pen, 150, 300, 570, 120);
        }
        svg.Save(context.Path("RecordSvg-recorded.svg"));
        using var rendered = svg.RenderToBitmap();
        ExampleSupport.Save(rendered, context, "RecordSvg-recorded.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Loads an SVG fixture, rasterizes it at a chosen size, and saves a PNG preview.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region LoadSvg
    internal static void LoadAndRasterizeSvg(ExampleContext c)
    {
        string input = CreateSvgFile(c, "LoadSvg-input.svg");
        using var svg = new SvgImage(input);
        using var bitmap = svg.RenderToBitmap(900, 520);
        ExampleSupport.Save(bitmap, c, "LoadSvg-rasterized.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Saves SVG with explicit encoder parameters to demonstrate configurable vector output.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region SvgOptions
    internal static void SvgOutputOptions(ExampleContext c)
    {
        using var svg = new SvgImage(500, 260);
        using (var g = svg.CreateGraphics())
        using (var pen = new Pen(Color.DarkBlue, 4))
        using (var font = new DrawingFont(FontFamily.GenericSansSerif, 28, FontStyle.Bold))
        using (var brush = new SolidBrush(Color.DarkBlue))
        {
            g.DrawEllipse(pen, new RectangleF(80, 40, 340, 170));
            g.DrawString("Vector text", font, brush, 150, 100);
        }

        // The same scene written twice: text as <text> elements, and text outlined to <path> elements.
        var asText = new SvgEncoderParameters { TextMode = SvgTextMode.Text, Indent = true, CoordinatePrecision = 2 };
        var asPaths = new SvgEncoderParameters { TextMode = SvgTextMode.Path, Indent = true, CoordinatePrecision = 2 };
        svg.Save(c.Path("SvgOptions-svg-options.svg"), asText);
        svg.Save(c.Path("SvgOptions-text-as-paths.svg"), asPaths);
        File.WriteAllText(c.Path("SvgOptions-summary.txt"),
            $"TextMode.Text: {new FileInfo(c.Path("SvgOptions-svg-options.svg")).Length} bytes\n" +
            $"TextMode.Path: {new FileInfo(c.Path("SvgOptions-text-as-paths.svg")).Length} bytes\n");
    }
    #endregion

    /// <summary>
    /// Records a vector scene once and renders it through multiple supported consumers.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region ReuseVectorScene
    internal static void ReuseVectorScene(ExampleContext c)
    {
        using var svg = new SvgImage(640, 360);
        using (var g = svg.CreateGraphics())
        using (var brush = new SolidBrush(Color.LightSeaGreen))
            g.FillEllipse(brush, new RectangleF(100, 60, 440, 240));
        using var raster = svg.RenderToBitmap();
        svg.Save(c.Path("ReuseVectorScene-reused-scene.svg"));
        ExampleSupport.Save(raster, c, "ReuseVectorScene-reused-scene.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Loads a configured metafile fixture and renders it to a raster preview.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region ReadMetafile
    internal static void ReadMetafile(ExampleContext c)
    {
        string emf = CreateMetafile(c, "ReadMetafile-input.emf");
        using var metafile = new DrawingMetafile(emf);
        using var bitmap = metafile.ToBitmap();
        ExampleSupport.Save(bitmap, c, "ReadMetafile-metafile-rendered.png", ImageFormat.Png);
        File.WriteAllText(c.Path("ReadMetafile-metafile-info.txt"), $"Dimensions={metafile.Width}x{metafile.Height}\nFormat={metafile.RawFormat.Name}\n");
    }
    #endregion

    /// <summary>
    /// Records drawing commands into a metafile using the public recording API.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region RecordMetafile
    internal static void RecordMetafile(ExampleContext c)
    {
        string emf = CreateMetafile(c, "RecordMetafile-recorded.emf");
        File.WriteAllText(c.Path("RecordMetafile-recorded.txt"), $"Recorded vector output: {System.IO.Path.GetFileName(emf)}\n");
    }
    #endregion

    /// <summary>
    /// Runs a tested conversion path between SVG or metafile input and a raster output.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region ConvertVector
    internal static void ConvertSvgAndMetafile(ExampleContext c)
    {
        string svgPath = CreateSvg(c, "ConvertVector-conversion-input.svg");
        using var svg = new SvgImage(svgPath);
        string emfPath = c.Path("ConvertVector-conversion.emf");
        svg.Save(emfPath, ImageFormat.Emf);
        using var metafile = new DrawingMetafile(emfPath);
        using var bitmap = metafile.ToBitmap();
        ExampleSupport.Save(bitmap, c, "ConvertVector-conversion.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Generates metafile output and records comparable diagnostic information for the result.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region CompareMetafile
    internal static void CompareMetafileOutput(ExampleContext c)
    {
        string first = CreateMetafile(c, "CompareMetafile-left.emf", Color.DarkBlue);
        string same = CreateMetafile(c, "CompareMetafile-same.emf", Color.DarkBlue);
        string other = CreateMetafile(c, "CompareMetafile-right.emf", Color.DarkRed);

        var identical = new EmfComparer().Compare(first, same);
        var changed = new EmfComparer().Compare(first, other);
        var report = new System.Text.StringBuilder();
        report.AppendLine($"Same drawing:   Equal={identical.AreEqual}, Differences={identical.Differences.Count}");
        report.AppendLine($"Pen colour changed: Equal={changed.AreEqual}, Differences={changed.Differences.Count}");
        foreach (var difference in changed.Differences)
            report.AppendLine("  " + difference);
        File.WriteAllText(c.Path("CompareMetafile-comparison.txt"), report.ToString());
    }
    #endregion

    /// <summary>
    /// Reuses one drawing routine to export the same composition to multiple supported formats.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region MultiFormatExport
    internal static void DrawOnceExportMultiple(ExampleContext c)
    {
        using var svg = new SvgImage(640, 360);
        using (var g = svg.CreateGraphics())
        using (var brush = new LinearGradientBrush(new PointF(0, 0), new PointF(640, 360), Color.SkyBlue, Color.MediumPurple))
        using (var pen = new Pen(Color.DarkBlue, 5))
        {
            g.FillRectangle(brush, new RectangleF(0, 0, 640, 360));
            g.DrawEllipse(pen, new RectangleF(120, 60, 400, 240));
        }
        svg.Save(c.Path("MultiFormatExport-draw-once.svg"));
        svg.Save(c.Path("MultiFormatExport-draw-once.emf"), ImageFormat.Emf);
        using var raster = svg.RenderToBitmap();
        ExampleSupport.Save(raster, c, "MultiFormatExport-draw-once.png", ImageFormat.Png);
    }
    #endregion

    internal static string CreateSvgFile(ExampleContext c, string name)
    {
        using var svg = new SvgImage(640, 360);
        using (var g = svg.CreateGraphics())
        using (var brush = new LinearGradientBrush(new PointF(0, 0), new PointF(640, 360), Color.SkyBlue, Color.MediumPurple))
        using (var pen = new Pen(Color.DarkBlue, 5))
        {
            g.FillRectangle(brush, new RectangleF(0, 0, 640, 360));
            g.DrawEllipse(pen, new RectangleF(120, 50, 400, 260));
        }
        string path = c.Path(name);
        svg.Save(path);
        return path;
    }

    internal static string CreateMetafile(ExampleContext c, string name, Color color)
    {
        using var svg = new SvgImage(480, 260);
        using (var g = svg.CreateGraphics())
        using (var brush = new SolidBrush(Color.LightSteelBlue))
        using (var pen = new Pen(color, 4))
        {
            g.FillRectangle(brush, new RectangleF(30, 30, 420, 200));
            g.DrawEllipse(pen, new RectangleF(100, 60, 280, 140));
        }
        string path = c.Path(name);
        svg.Save(path, ImageFormat.Emf);
        return path;
    }

    internal static string CreateMetafile(ExampleContext c, string name)
        => CreateMetafile(c, name, Color.DarkBlue);

    internal static string CreateSvg(ExampleContext c, string name)
    {
        using var svg = new SvgImage(520, 280);
        using (var g = svg.CreateGraphics())
        using (var brush = new SolidBrush(Color.LightSteelBlue))
        using (var pen = new Pen(Color.DarkBlue, 4))
        {
            g.FillRectangle(brush, new RectangleF(30, 30, 460, 220));
            g.DrawEllipse(pen, new RectangleF(100, 60, 320, 160));
        }
        string path = c.Path(name);
        svg.Save(path);
        return path;
    }
}




