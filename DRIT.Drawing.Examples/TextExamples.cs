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
/// Contains runnable examples for text measurement, layout, font selection, multilingual text, rich text, and text on paths.
/// </summary>
internal static class TextExamples
{
    /// <summary>
    /// Draws a heading and reports its measured bounds for use in layout calculations.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region TextMeasurement
    internal static void TextDrawAndMeasure(ExampleContext context)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 300, Color.White);
        using var graphics = Graphics.FromImage(bitmap);
        using var font = new DrawingFont(FontFamily.GenericSansSerif, 34, FontStyle.Bold);
        using var brush = new SolidBrush(Color.DarkBlue);
        using var boundsPen = new Pen(Color.DarkOrange, 2);
        string text = "Measured heading";
        SizeF measured = graphics.MeasureString(text, font);
        graphics.DrawString(text, font, brush, 50, 80);
        graphics.DrawRectangle(boundsPen, new RectangleF(50, 80, measured.Width, measured.Height));
        Console.WriteLine($"  measured text: {measured.Width:0.0} x {measured.Height:0.0}");
        ExampleSupport.Save(bitmap, context, "TextMeasurement-text-measure.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Renders text in bounded rectangles with alignment, wrapping, and trimming options.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region TextLayout
    internal static void TextAlignmentWrappingAndTrimming(ExampleContext context)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 420, Color.WhiteSmoke);
        using var graphics = Graphics.FromImage(bitmap);
        using var font = new DrawingFont(FontFamily.GenericSerif, 22, FontStyle.Regular);
        using var brush = new SolidBrush(Color.Black);
        using var outline = new Pen(Color.LightSlateGray, 2);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisWord,
        };
        var box = new RectangleF(70, 60, 580, 260);
        graphics.DrawRectangle(outline, box);
        graphics.DrawString("Text can wrap, align, and trim inside a layout rectangle.", font, brush, box, format);
        ExampleSupport.Save(bitmap, context, "TextLayout-text-layout.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Renders representative font families and styles while using the available font fallback behavior.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region FontStyles
    internal static void FontFamiliesAndStyles(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(760, 360, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var brush = new SolidBrush(Color.Black);
        using var regular = new DrawingFont(FontFamily.GenericSansSerif, 24, FontStyle.Regular);
        using var bold = new DrawingFont(FontFamily.GenericSerif, 24, FontStyle.Bold);
        using var italic = new DrawingFont(FontFamily.GenericMonospace, 24, FontStyle.Italic);
        g.DrawString("Sans serif regular", regular, brush, 50, 45);
        g.DrawString("Serif bold", bold, brush, 50, 125);
        g.DrawString("Monospace italic", italic, brush, 50, 205);
        File.WriteAllText(c.Path("FontStyles-font-families.txt"), "GenericSansSerif, GenericSerif, and GenericMonospace rendered.");
        ExampleSupport.Save(bitmap, c, "FontStyles-font-families.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Loads a private font collection when a font fixture is available and renders a sample.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region PrivateFonts
    internal static void PrivateFontCollection(ExampleContext c)
    {
        // Prefer a widely installed font so the result looks the same everywhere; otherwise take any .ttf.
        string fontsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        string? fontPath = null;
        if (Directory.Exists(fontsFolder))
        {
            fontPath = new[] { "verdana.ttf", "arial.ttf", "DejaVuSans.ttf", "LiberationSans-Regular.ttf" }
                .Select(name => System.IO.Path.Combine(fontsFolder, name))
                .FirstOrDefault(File.Exists)
                ?? Directory.EnumerateFiles(fontsFolder, "*.ttf").FirstOrDefault();
        }
        using var collection = new PrivateFontCollection();
        FontFamily family = FontFamily.GenericSansSerif;
        if (fontPath != null)
        {
            collection.AddFontFile(fontPath);
            family = collection.Families.FirstOrDefault() ?? family;
        }
        using var font = new DrawingFont(family, 28, FontStyle.Regular);
        using var bitmap = ExampleSupport.NewCanvas(720, 260, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var brush = new SolidBrush(Color.DarkBlue);
        g.DrawString(fontPath == null ? "Generic fallback font" : "Private font collection", font, brush, 50, 90);
        ExampleSupport.Save(bitmap, c, "PrivateFonts-private-font.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Renders bidirectional and multilingual text to exercise mixed-script layout.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region MultilingualText
    internal static void BidiAndMultilingualText(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(760, 280, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var font = new DrawingFont(FontFamily.GenericSansSerif, 24);
        using var brush = new SolidBrush(Color.DarkSlateBlue);
        g.DrawString("English — العربية — עברית — Ελληνικά", font, brush, 35, 100);
        ExampleSupport.Save(bitmap, c, "MultilingualText-bidi-multilingual.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Renders text requiring more than one glyph range and records the resulting fallback output.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region FontFallback
    internal static void FontFallback(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(760, 280, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var font = new DrawingFont(FontFamily.GenericSansSerif, 24);
        using var brush = new SolidBrush(Color.DarkGreen);
        g.FontFallback = FontFallbackChain.CreateDefault();
        g.DrawString("Fallback chain: Latin • symbols • العربية", font, brush, 35, 100);
        ExampleSupport.Save(bitmap, c, "FontFallback-font-fallback.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Combines multiple fonts, sizes, styles, and colours in one text layout.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region RichText
    internal static void RichTextRuns(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(760, 300, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var defaultBrush = new SolidBrush(Color.Black);
        using var redBrush = new SolidBrush(Color.DarkRed);
        using var blueBrush = new SolidBrush(Color.DarkBlue);
        var options = new RichTextOptions(FontFamily.GenericSansSerif, 24, FontStyle.Regular);
        const string text = "Rich text runs";
        // A run is (start index, length) in the string. Uncovered characters keep the defaults.
        options.Runs.Add(new RichTextRun(0, 4) { FontStyle = FontStyle.Bold, Brush = redBrush });      // "Rich"
        options.Runs.Add(new RichTextRun(5, 4) { FontFamily = FontFamily.GenericSerif, EmSize = 34 }); // "text"
        options.Runs.Add(new RichTextRun(10, 4) { FontStyle = FontStyle.Italic, Brush = blueBrush });  // "runs"
        g.DrawRichText(text, options, defaultBrush, new RectangleF(50, 90, 650, 100));
        ExampleSupport.Save(bitmap, c, "RichText-rich-text-runs.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Places text along a curved path and exports the resulting composition.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region TextOnPath
    internal static void TextOnPath(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(760, 360, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var path = new GraphicsPath();
        using var font = new DrawingFont(FontFamily.GenericSansSerif, 25, FontStyle.Bold);
        using var brush = new SolidBrush(Color.DarkBlue);
        path.AddArc(new RectangleF(100, 40, 560, 260), 180, 180);
        // Centre the text along the path instead of starting at its beginning.
        using var centered = new StringFormat { Alignment = StringAlignment.Center };
        g.DrawStringOnPath("Text follows a vector path", font, brush, path, centered);
        ExampleSupport.Save(bitmap, c, "TextOnPath-text-on-path.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Draws the same text with a single-bit hint and with an antialiasing hint, and enlarges one letter of each.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region TextRendering
    internal static void TextRenderingSettings(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(760, 330, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var font = new DrawingFont(DRIT.Drawing.Text.FontFamily.GenericSansSerif, 28);
        using var brush = new SolidBrush(Color.DarkBlue);

        // The hint is part of the Graphics state: it applies to the text drawn after it is set.
        g.TextRenderingHint = DRIT.Drawing.Text.TextRenderingHint.SingleBitPerPixel;
        g.DrawString("Hard edges (SingleBitPerPixel)", font, brush, 30, 25);
        g.TextRenderingHint = DRIT.Drawing.Text.TextRenderingHint.AntiAlias;
        g.DrawString("Smooth edges (AntiAlias)", font, brush, 30, 85);

        // Enlarge the first letter of each line (nearest neighbour) to show the individual pixels.
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImage(bitmap, new RectangleF(30, 150, 160, 160), new RectangleF(29, 26, 32, 32), GraphicsUnit.Pixel);
        g.DrawImage(bitmap, new RectangleF(230, 150, 160, 160), new RectangleF(29, 86, 32, 32), GraphicsUnit.Pixel);
        File.WriteAllText(c.Path("TextRendering-text-rendering.txt"), $"TextRenderingHint={g.TextRenderingHint}");
        ExampleSupport.Save(bitmap, c, "TextRendering-text-rendering-settings.png", ImageFormat.Png);
    }
    #endregion
}




