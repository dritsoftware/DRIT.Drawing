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
/// Contains runnable examples for pens, line styles, brush fills, gradients, textures, and drawing resource reuse.
/// </summary>
internal static class BrushesExamples
{
    /// <summary>
    /// Draws identical lines with different colours and widths to demonstrate basic pen configuration.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region PenStyles
    internal static void PensColourAndWidth(ExampleContext context)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 420, Color.White);
        using var graphics = Graphics.FromImage(bitmap);
        Color[] colours = { Color.DarkBlue, Color.DarkOrange, Color.DarkGreen };
        float[] widths = { 2, 6, 12 };
        for (int i = 0; i < colours.Length; i++)
        {
            using var pen = new Pen(colours[i], widths[i]);
            graphics.DrawLine(pen, 80, 90 + i * 90, 640, 90 + i * 90);
        }
        ExampleSupport.Save(bitmap, context, "PenStyles-pen-colour-width.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Compares a translucent solid brush with a hatch brush when filling geometric areas.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region SolidAndHatchBrushes
    internal static void BrushesSolidAndHatch(ExampleContext context)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 420, Color.White);
        using var graphics = Graphics.FromImage(bitmap);
        using var solid = new SolidBrush(Color.FromArgb(220, 65, 135, 200));
        using var hatch = new HatchBrush(HatchStyle.Cross, Color.DarkRed, Color.LightYellow);
        graphics.FillRectangle(solid, 70, 70, 250, 250);
        graphics.FillEllipse(hatch, new RectangleF(400, 70, 250, 250));
        ExampleSupport.Save(bitmap, context, "SolidAndHatchBrushes-solid-hatch.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Draws grouped polylines with a custom dash pattern and bevel joins.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region PenDashesAndJoins
    internal static void DashesAndJoins(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 400, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var pen = new Pen(Color.DarkBlue, 12) { DashStyle = DashStyle.Custom, DashPattern = new[] { 8f, 4f }, LineJoin = LineJoin.Bevel };
        g.DrawPolylineGroup(pen, new[] { new[] { new PointF(50, 100), new PointF(220, 290), new PointF(390, 100) }, new[] { new PointF(450, 100), new PointF(620, 290), new PointF(680, 100) } });
        ExampleSupport.Save(bitmap, c, "PenDashesAndJoins-dashes-joins.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Adds an arrow-shaped line cap to demonstrate directional annotations.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region ArrowCaps
    internal static void ArrowCap(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 260, Color.White);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // A built-in anchor; its size follows the pen width.
        using var anchorPen = new Pen(Color.DarkOrange, 5) { EndCap = LineCap.ArrowAnchor };
        g.DrawLine(anchorPen, 80, 50, 630, 50);

        // An adjustable arrow: width and height are multiples of the pen width (3 x 5 = 15 px).
        using var filledArrow = new AdjustableArrowCap(3, 3);
        using var filledPen = new Pen(Color.DarkBlue, 5) { CustomEndCap = filledArrow };
        g.DrawLine(filledPen, 80, 110, 630, 110);

        // Unfilled head, with a different cap at each end of the line.
        using var hollowArrow = new AdjustableArrowCap(4, 4, isFilled: false);
        using var hollowPen = new Pen(Color.DarkGreen, 4) { StartCap = LineCap.RoundAnchor, CustomEndCap = hollowArrow };
        g.DrawLine(hollowPen, 80, 170, 630, 170);

        // The caps also work on curves.
        using var curvePen = new Pen(Color.Purple, 4) { CustomEndCap = filledArrow };
        g.DrawBezier(curvePen, new PointF(80, 230), new PointF(250, 190), new PointF(450, 255), new PointF(630, 215));
        ExampleSupport.Save(bitmap, c, "ArrowCaps-arrow-cap.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Fills a rectangle with a linear gradient between two colours.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region LinearGradients
    internal static void LinearGradients(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 360, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var brush = new LinearGradientBrush(new RectangleF(60, 60, 600, 240), Color.DarkBlue, Color.LightSkyBlue, 0f);
        g.FillRectangle(brush, new RectangleF(60, 60, 600, 240));
        ExampleSupport.Save(bitmap, c, "LinearGradients-linear-gradient.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Fills a closed path with a centre colour and surrounding boundary colours.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region PathGradients
    internal static void PathGradients(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 360, Color.White);
        using var g = Graphics.FromImage(bitmap);
        var points = new[] { new PointF(360, 35), new PointF(650, 170), new PointF(520, 315), new PointF(200, 315), new PointF(70, 170) };
        using var brush = new PathGradientBrush(points) { CenterColor = Color.Gold, SurroundColors = new[] { Color.DarkRed, Color.DarkOrange, Color.Purple, Color.DarkBlue, Color.Teal } };
        g.FillPolygon(brush, points);
        ExampleSupport.Save(bitmap, c, "PathGradients-path-gradient.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Creates a generated texture and tiles it across a larger drawing with a texture brush.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region TextureBrushes
    internal static void TextureBrushes(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 360, Color.White);
        using var texture = ExampleSupport.CreatePhotoLike(80, 60);
        using var brush = new TextureBrush(texture);
        using var g = Graphics.FromImage(bitmap);
        g.FillRectangle(brush, new RectangleF(60, 50, 600, 260));
        ExampleSupport.Save(bitmap, c, "TextureBrushes-texture-brush.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Reuses a pen and brush across multiple drawing operations while keeping ownership and disposal explicit.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region ResourceReuse
    internal static void ResourceReuse(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 360, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var pen = new Pen(Color.DarkBlue, 4);
        using var brush = new SolidBrush(Color.FromArgb(190, 220, 100, 50));
        for (int i = 0; i < 5; i++)
        {
            float x = 50 + i * 135;
            g.FillEllipse(brush, new RectangleF(x, 80, 100, 100));
            g.DrawEllipse(pen, new RectangleF(x, 80, 100, 100));
            g.DrawLine(pen, x, 240, x + 100, 240);
        }
        ExampleSupport.Save(bitmap, c, "ResourceReuse-resource-reuse.png", ImageFormat.Png);
    }
    #endregion
}




