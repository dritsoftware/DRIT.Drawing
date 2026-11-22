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

/// <summary>
/// Contains application-oriented examples that combine several DRIT.Drawing features into practical image compositions.
/// </summary>
internal static class RecipeExamples
{
    /// <summary>
    /// Creates a photo-like image, adds a readable caption overlay, and exports the composed result.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region CaptionedPhoto
    internal static void RecipeCaptionedPhoto(ExampleContext context)
    {
        using var photo = ExampleSupport.CreatePhotoLike(800, 500);
        using var graphics = Graphics.FromImage(photo);
        using var panel = new SolidBrush(Color.FromArgb(190, 0, 0, 0));
        using var textBrush = new SolidBrush(Color.White);
        using var font = new DrawingFont(FontFamily.GenericSansSerif, 28, FontStyle.Bold);
        graphics.FillRectangle(panel, 0, 410, 800, 90);
        graphics.DrawString("A generated landscape caption", font, textBrush, 28, 435);
        ExampleSupport.Save(photo, context, "CaptionedPhoto-captioned-photo.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Draws two rectangular outlines over a generated image to create a decorative frame.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region FramedImage
    internal static void FramedImage(ExampleContext c)
    {
        using var bitmap = ExampleSupport.CreatePhotoLike(800, 500);
        using var g = Graphics.FromImage(bitmap);
        using var outer = new Pen(Color.DarkBlue, 12);
        using var inner = new Pen(Color.Gold, 4);
        g.DrawRectangle(outer, new RectangleF(12, 12, 776, 476));
        g.DrawRectangle(inner, new RectangleF(30, 30, 740, 440));
        ExampleSupport.Save(bitmap, c, "FramedImage-framed-image.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Draws a speech-bubble callout with an arrow and text annotation.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region Callout
    internal static void Callout(ExampleContext c)
    {
        using var bitmap = ExampleSupport.CreatePhotoLike(800, 500);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var body = new GraphicsPath();
        using var pointer = new GraphicsPath();
        using var fill = new SolidBrush(Color.AliceBlue);
        using var pen = new Pen(Color.DarkBlue, 8) { LineJoin = LineJoin.Round };   // 8 wide: only the outer 4 stay visible
        body.AddEllipse(new RectangleF(390, 50, 340, 150));
        pointer.AddPolygon(new[] { new PointF(470, 165), new PointF(430, 250), new PointF(530, 185) });

        // Stroke both shapes first, then fill both on top. The fill hides the inner half of every stroke,
        // including the stretch of the pointer's outline that runs inside the bubble, so one clean outline
        // is left around the combined shape.
        g.DrawPath(pen, body);
        g.DrawPath(pen, pointer);
        g.FillPath(fill, body);
        g.FillPath(fill, pointer);
        using var font = new DrawingFont(FontFamily.GenericSansSerif, 22, FontStyle.Bold);
        using var text = new SolidBrush(Color.DarkBlue);
        g.DrawString("A reusable callout", font, text, new RectangleF(430, 100, 260, 50));
        ExampleSupport.Save(bitmap, c, "Callout-callout.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Builds a chart card with axes, a data series, and a title.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region ChartCard
    internal static void ChartCard(ExampleContext c)
    {
        string[] periods = { "Q1", "Q2", "Q3", "Q4", "Q5", "Q6" };
        double[] output = { 12, 19, 15, 28, 24, 35 };

        using var bitmap = ExampleSupport.NewCanvas(800, 500, Color.White);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var axis = new Pen(Color.DarkSlateGray, 3);
        using var grid = new Pen(Color.Gainsboro, 1);
        using var line = new Pen(Color.DarkOrange, 5);
        using var title = new DrawingFont(FontFamily.GenericSansSerif, 18, FontStyle.Bold);
        using var label = new DrawingFont(FontFamily.GenericSansSerif, 12);
        using var black = new SolidBrush(Color.Black);
        using var marker = new SolidBrush(Color.DarkOrange);

        // The plot area in pixels, and the data range it represents (0..40, ticks every 10).
        const float left = 90, right = 760, top = 80, bottom = 420;
        const double axisMax = 40, tickStep = 10;

        // Data to canvas coordinates: x centres each period in its slot; y grows upwards from the baseline.
        float X(int index) => left + (right - left) * (index + 0.5f) / periods.Length;
        float Y(double value) => bottom - (float)(value / axisMax) * (bottom - top);

        for (double tick = 0; tick <= axisMax; tick += tickStep)
        {
            g.DrawLine(grid, left, Y(tick), right, Y(tick));
            g.DrawString(tick.ToString("0"), label, black, left - 34, Y(tick) - 9);
        }
        g.DrawLine(axis, left, bottom, right, bottom);
        g.DrawLine(axis, left, bottom, left, top);

        var points = new PointF[output.Length];
        for (int i = 0; i < output.Length; i++)
            points[i] = new PointF(X(i), Y(output[i]));
        g.DrawLines(line, points);
        for (int i = 0; i < points.Length; i++)
        {
            g.FillEllipse(marker, new RectangleF(points[i].X - 6, points[i].Y - 6, 12, 12));
            g.DrawString(output[i].ToString("0"), label, black, points[i].X - 8, points[i].Y - 28);
            g.DrawString(periods[i], label, black, points[i].X - 10, bottom + 8);
        }
        g.DrawString("Quarterly output (units)", title, black, left, 25);
        ExampleSupport.Save(bitmap, c, "ChartCard-chart-card.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Combines gradients, paths, text, and vector export to create a reusable badge.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region VectorBadge
    internal static void ReusableBadge(ExampleContext c)
    {
        using var svg = new SvgImage(640, 360);
        using (var g = svg.CreateGraphics())
        using (var brush = new LinearGradientBrush(new PointF(0, 0), new PointF(640, 360), Color.DarkBlue, Color.MediumPurple))
        using (var font = new DrawingFont(FontFamily.GenericSansSerif, 32, FontStyle.Bold))
        using (var text = new SolidBrush(Color.White))
        {
            g.FillRectangle(brush, new RectangleF(0, 0, 640, 360));
            using var path = new GraphicsPath();
            path.AddArc(new RectangleF(100, 90, 440, 160), 180, 180);
            using var centered = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawStringOnPath("Reusable vector badge", font, text, path, centered);
        }
        svg.Save(c.Path("VectorBadge-reusable-badge.svg"));
        using var raster = svg.RenderToBitmap();
        ExampleSupport.Save(raster, c, "VectorBadge-reusable-badge.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Processes generated local images into deterministically named thumbnails with a pixel limit.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region BatchThumbnails
    internal static void BatchThumbnails(ExampleContext c)
    {
        string inputDirectory = c.Path("BatchThumbnails-inputs");
        Directory.CreateDirectory(inputDirectory);
        for (int i = 0; i < 3; i++)
        {
            using var source = ExampleSupport.CreatePhotoLike(320 + i * 20, 200 + i * 10);
            source.Save(System.IO.Path.Combine(inputDirectory, $"source-{i}.png"), ImageFormat.Png);
        }
        foreach (string input in Directory.EnumerateFiles(inputDirectory, "*.png").OrderBy(path => path))
            using (var image = Image.FromFile(input, new ImageDecodeLimits(maxPixels: 1_000_000)))
            using (var thumbnail = ExampleSupport.NewCanvas(160, 100, Color.White))
            using (var g = Graphics.FromImage(thumbnail))
            {
                g.DrawImage(image, new Rectangle(0, 0, 160, 100));
                ExampleSupport.Save(thumbnail, c, $"BatchThumbnails-{System.IO.Path.GetFileNameWithoutExtension(input)}.png", ImageFormat.Png);
            }
    }
    #endregion
}


