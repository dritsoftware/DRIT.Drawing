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
/// Contains introductory examples for creating, loading, saving, and safely handling DRIT.Drawing images.
/// </summary>
internal static class GettingStartedExamples
{
    /// <summary>
    /// Creates a bitmap, clears its background, draws simple geometry, and saves a PNG.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region CreateImage
    internal static void GettingStartedCreateImage(ExampleContext context)
    {
        using var bitmap = new Bitmap(640, 400, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.WhiteSmoke);
        using var background = new SolidBrush(Color.FromArgb(255, 35, 88, 145));
        using var accent = new SolidBrush(Color.FromArgb(255, 241, 177, 63));
        using var pen = new Pen(Color.White, 5);
        graphics.FillRectangle(background, 0, 0, 640, 400);
        graphics.FillEllipse(accent, new RectangleF(230, 80, 180, 180));
        graphics.DrawEllipse(pen, new RectangleF(230, 80, 180, 180));
        ExampleSupport.Save(bitmap, context, "CreateImage-create-image.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Loads a generated image, reports its dimensions and format, and saves a copy.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region OpenInspectSave
    internal static void GettingStartedOpenInspectSave(ExampleContext context)
    {
        string input = context.EnsureGeneratedImage("OpenInspectSave-input.png");
        using var image = Image.FromFile(input);
        Console.WriteLine($"  source: {image.Width}x{image.Height}, format={image.RawFormat.Name}");
        image.Save(context.Path("OpenInspectSave-copy.png"), ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Demonstrates explicit disposal of images, graphics, fonts, pens, and brushes.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region ResourceLifetime
    internal static void GettingStartedResourceLifetime(ExampleContext context)
    {
        using (var bitmap = new Bitmap(560, 260, PixelFormat.Format32bppArgb))
        using (var graphics = Graphics.FromImage(bitmap))
        using (var pen = new Pen(Color.DarkBlue, 8))
        using (var brush = new HatchBrush(HatchStyle.DiagonalCross, Color.Gold, Color.White))
        using (var font = new DrawingFont(FontFamily.GenericSansSerif, 20, FontStyle.Bold))
        using (var textBrush = new SolidBrush(Color.DarkBlue))
        {
            graphics.Clear(Color.White);
            graphics.FillEllipse(brush, new RectangleF(40, 40, 180, 180));
            graphics.DrawEllipse(pen, new RectangleF(40, 40, 180, 180));
            graphics.DrawString("Disposed by scope", font, textBrush, 250, 110);
            ExampleSupport.Save(bitmap, context, "ResourceLifetime-resource-lifetime.png", ImageFormat.Png);
        }
    }
    #endregion

    /// <summary>
    /// Encodes an image to a memory stream, rewinds it, and reopens the encoded data.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region StreamRoundTrip
    internal static void GettingStartedStream(ExampleContext context)
    {
        using var source = new Bitmap(320, 200, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(source))
        using (var brush = new SolidBrush(Color.MediumSeaGreen))
        {
            graphics.Clear(Color.White);
            graphics.FillRectangle(brush, 35, 35, 250, 130);
        }

        using var encoded = new MemoryStream();
        source.Save(encoded, ImageFormat.Png);
        encoded.Position = 0;
        using var reopened = Image.FromStream(encoded);
        reopened.Save(context.Path("StreamRoundTrip-stream-reopened.png"), ImageFormat.Png);
        Console.WriteLine($"  stream round trip: {reopened.Width}x{reopened.Height}");
    }
    #endregion

    /// <summary>
    /// Uses rectangles and ARGB colours, including a translucent fill, in a small composition.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region GeometryAndColours
    internal static void GeometryAndColours(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 360, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var brush = new SolidBrush(Color.FromArgb(255, 40, 90, 160));
        using var pen = new Pen(Color.FromArgb(255, 230, 150, 40), 4);
        g.FillRectangle(brush, new RectangleF(80, 70, 560, 220));
        g.DrawRectangle(pen, new RectangleF(80, 70, 560, 220));
        g.FillEllipse(new SolidBrush(Color.FromArgb(180, Color.White)), new RectangleF(210, 110, 300, 140));
        ExampleSupport.Save(bitmap, c, "GeometryAndColours-geometry-colours.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Shows how a small System.Drawing-style drawing maps to the DRIT.Drawing API surface.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region SystemDrawingMigration
    internal static void SystemDrawingMigration(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(640, 300, Color.WhiteSmoke);
        using var g = Graphics.FromImage(bitmap);
        using var pen = new Pen(Color.DarkSlateBlue, 4);
        using var brush = new SolidBrush(Color.LightSteelBlue);
        g.FillEllipse(brush, new RectangleF(80, 50, 480, 180));
        g.DrawEllipse(pen, new RectangleF(80, 50, 480, 180));
        File.WriteAllText(c.Path("SystemDrawingMigration-system-drawing-migration.txt"),
            "The drawing calls use the System.Drawing-style API surface in the DRIT.Drawing namespace.\n" +
            "The same code can be moved by changing the namespace import; no native System.Drawing dependency is required.");
        ExampleSupport.Save(bitmap, c, "SystemDrawingMigration-system-drawing-migration.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Configures a strict decode limit and demonstrates safe rejection of an image that exceeds it.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region DecodeLimits
    internal static void DecodeLimits(ExampleContext c)
    {
        using var bitmap = ExampleSupport.CreatePhotoLike(240, 160);
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        try
        {
            using var ignored = Image.FromStream(stream, new ImageDecodeLimits(maxPixels: 100));
            throw new InvalidOperationException("The intentionally small decode limit did not reject the image.");
        }
        catch (InvalidDataException exception)
        {
            File.WriteAllText(c.Path("DecodeLimits-decode-limits.txt"), $"Rejected safely: {exception.Message}");
        }
    }
    #endregion
}


