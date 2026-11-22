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
/// Contains runnable examples for inspecting, preserving, composing, extracting, and transforming animated images.
/// </summary>
internal static class AnimationExamples
{
    /// <summary>
    /// Enumerates the frames of a generated animation, selects frames, and exports representative images.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region AnimationFrames
    internal static void AnimationInspectFrames(ExampleContext context)
    {
        string input = context.EnsureAnimationFixture();
        using var image = Image.FromFile(input);
        int frameCount = image.GetFrameCount(FrameDimension.Time);
        Console.WriteLine($"  animation frames: {frameCount}");
        for (int i = 0; i < frameCount; i++)
        {
            image.SelectActiveFrame(FrameDimension.Time, i);
            using var frame = new Bitmap(image);
            frame.Save(context.Path($"AnimationFrames-frame-{i:00}.png"), ImageFormat.Png);
        }
    }
    #endregion

    /// <summary>
    /// Loads an animated image and saves it through the supported animation-preserving path.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region PreserveAnimation
    internal static void PreserveAnimation(ExampleContext c)
    {
        string gifPath = c.Path("PreserveAnimation-preserved.gif");
        string pngPath = c.Path("PreserveAnimation-preserved.png");
        int sourceFrames;
        using (var image = Image.FromFile(c.EnsureAnimationFixture()))
        {
            sourceFrames = image.GetFrameCount(FrameDimension.Time);
            image.Save(gifPath, ImageFormat.Gif);
            image.Save(pngPath, ImageFormat.Png);
        }

        // Verify by reopening what was written rather than trusting the source image.
        using var savedGif = Image.FromFile(gifPath);
        using var savedPng = Image.FromFile(pngPath);
        File.WriteAllText(c.Path("PreserveAnimation-animation.txt"),
            $"Source frames={sourceFrames}\n" +
            $"Saved GIF frames={savedGif.GetFrameCount(FrameDimension.Time)}\n" +
            $"Saved PNG frames={savedPng.GetFrameCount(FrameDimension.Time)}\n");
    }
    #endregion

    /// <summary>
    /// Selects frames from a generated animation and draws their snapshots side by side.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region ComposeFrames
    internal static void FrameComposition(ExampleContext c)
    {
        using var image = Image.FromFile(c.EnsureAnimationFixture());
        using var output = ExampleSupport.NewCanvas(image.Width * image.GetFrameCount(FrameDimension.Time), image.Height, Color.White);
        using var g = Graphics.FromImage(output);
        for (int i = 0; i < image.GetFrameCount(FrameDimension.Time); i++)
        {
            image.SelectActiveFrame(FrameDimension.Time, i);
            using var frame = new Bitmap(image);
            g.DrawImage(frame, new Rectangle(i * image.Width, 0, image.Width, image.Height));
        }
        ExampleSupport.Save(output, c, "ComposeFrames-frame-composition.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Extracts animation frames as numbered PNG files without modifying the source animation.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region ExtractFrames
    internal static void ExtractAnimationFrames(ExampleContext c)
    {
        using var image = Image.FromFile(c.EnsureAnimationFixture());
        for (int i = 0; i < image.GetFrameCount(FrameDimension.Time); i++)
        {
            image.SelectActiveFrame(FrameDimension.Time, i);
            using var frame = new Bitmap(image);
            ExampleSupport.Save(frame, c, $"ExtractFrames-extracted-frame-{i:00}.png", ImageFormat.Png);
        }
    }
    #endregion

    /// <summary>
    /// Records the current public API capability for constructing an animation from independent bitmaps.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region AnimationConstruction
    internal static void InvestigateAnimationConstruction(ExampleContext c)
    {
        File.WriteAllText(c.Path("AnimationConstruction-capability-note.txt"),
            "Independent bitmap-to-animation construction is deferred: the public API exposes frame selection and preservation, " +
            "but no public AnimationFrame/SetAnimationFrames construction surface. Use PreserveAnimation/ExtractFrames for supported workflows.");
    }
    #endregion

    /// <summary>
    /// Loads animation input, adds a banner to each selected frame, and exports separate PNG files.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region AnimatedBanner
    internal static void AnimatedBanner(ExampleContext c)
    {
        using var image = Image.FromFile(c.EnsureAnimationFixture());
        for (int i = 0; i < image.GetFrameCount(FrameDimension.Time); i++)
        {
            image.SelectActiveFrame(FrameDimension.Time, i);
            using var frame = new Bitmap(image);
            using var banner = ExampleSupport.NewCanvas(frame.Width, frame.Height + 32, Color.White);
            using var g = Graphics.FromImage(banner);
            g.DrawImage(frame, new Rectangle(0, 32, frame.Width, frame.Height));
            using var font = new DrawingFont(DRIT.Drawing.Text.FontFamily.GenericSansSerif, 14, FontStyle.Bold);
            using var brush = new SolidBrush(Color.DarkBlue);
            g.DrawString($"Frame {i + 1}", font, brush, 4, 6);
            ExampleSupport.Save(banner, c, $"AnimatedBanner-animated-banner-{i:00}.png", ImageFormat.Png);
        }
    }
    #endregion
}




