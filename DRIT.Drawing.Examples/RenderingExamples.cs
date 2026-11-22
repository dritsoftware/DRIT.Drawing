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
/// Contains runnable examples for antialiasing, alpha compositing, clipping, transforms, graphics state, and coordinate units.
/// </summary>
internal static class RenderingExamples
{
    /// <summary>
    /// Renders the same geometry with different smoothing modes so the antialiasing effect can be compared.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region Antialiasing
    internal static void RenderingAntialiasing(ExampleContext context)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 420, Color.White);
        using var graphics = Graphics.FromImage(bitmap);
        using var pen = new Pen(Color.DarkBlue, 3);
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.DrawEllipse(pen, new RectangleF(45, 50, 260, 260));
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.DrawEllipse(pen, new RectangleF(410, 50, 260, 260));
        ExampleSupport.Save(bitmap, context, "Antialiasing-antialiasing.png", ImageFormat.Png);

        // At 1:1 the difference is subtle, so also enlarge the same 40 x 40 pixel patch of each
        // circle's edge eight times, without smoothing, to make the individual pixels visible.
        using var zoomed = ExampleSupport.NewCanvas(680, 340, Color.White);
        using var zoom = Graphics.FromImage(zoomed);
        zoom.InterpolationMode = InterpolationMode.NearestNeighbor;
        zoom.DrawImage(bitmap, new RectangleF(10, 10, 320, 320), new RectangleF(30, 150, 40, 40), GraphicsUnit.Pixel);
        zoom.DrawImage(bitmap, new RectangleF(350, 10, 320, 320), new RectangleF(395, 150, 40, 40), GraphicsUnit.Pixel);
        ExampleSupport.Save(zoomed, context, "Antialiasing-zoomed-edges.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Layers translucent shapes over an opaque background to demonstrate alpha blending.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region AlphaBlending
    internal static void RenderingAlphaBlending(ExampleContext context)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 420, Color.White);
        using var graphics = Graphics.FromImage(bitmap);
        using var back = new SolidBrush(Color.DarkBlue);
        using var red = new SolidBrush(Color.FromArgb(150, 220, 45, 45));
        using var yellow = new SolidBrush(Color.FromArgb(150, 245, 200, 50));
        graphics.FillRectangle(back, 65, 65, 590, 290);
        graphics.FillEllipse(red, new RectangleF(130, 100, 260, 220));
        graphics.FillEllipse(yellow, new RectangleF(330, 100, 260, 220));
        ExampleSupport.Save(bitmap, context, "AlphaBlending-alpha-blending.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Applies a clipping region while drawing, then resets the clip to show the scope of clipping.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region Clipping
    internal static void RenderingClipping(ExampleContext context)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 420, Color.White);
        using var graphics = Graphics.FromImage(bitmap);
        using var brush = new SolidBrush(Color.FromArgb(210, 50, 150, 100));
        graphics.SetClip(new Rectangle(105, 65, 510, 290), CombineMode.Replace);
        for (int x = -400; x < 900; x += 45)
            graphics.FillRectangle(brush, x, 30, 25, 370);
        graphics.ResetClip();
        using var outline = new Pen(Color.DarkSlateGray, 4);
        graphics.DrawRectangle(outline, 105, 65, 510, 290);
        ExampleSupport.Save(bitmap, context, "Clipping-clipping.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Draws an ellipse under combined translation, rotation, and scaling transforms.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region Transforms
    internal static void RenderingTransforms(ExampleContext context)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 420, Color.White);
        using var graphics = Graphics.FromImage(bitmap);
        using var pen = new Pen(Color.DarkBlue, 6);
        using var brush = new SolidBrush(Color.FromArgb(170, 240, 160, 40));
        graphics.TranslateTransform(360, 210);
        graphics.RotateTransform(-20);
        graphics.ScaleTransform(1.3f, 0.8f);
        graphics.FillEllipse(brush, new RectangleF(-150, -90, 300, 180));
        graphics.DrawEllipse(pen, new RectangleF(-150, -90, 300, 180));
        ExampleSupport.Save(bitmap, context, "Transforms-transforms.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Intersects two rectangular clips and draws through the resulting region.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region CombinedClips
    internal static void CombinedClips(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 360, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var brush = new SolidBrush(Color.FromArgb(190, 80, 130, 210));
        g.SetClip(new Rectangle(70, 60, 450, 230), CombineMode.Replace);
        g.IntersectClip(new Rectangle(250, 110, 400, 190));
        g.FillRectangle(brush, new RectangleF(0, 0, 720, 360));
        g.ResetClip();
        ExampleSupport.Save(bitmap, c, "CombinedClips-combined-clips.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Maps points through a translation and rotation and then through the inverse matrix.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region MatrixTransforms
    internal static void MatrixOrderAndInverse(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 420, Color.White);
        using var g = Graphics.FromImage(bitmap);
        var points = new[] { new PointF(40, 40), new PointF(160, 40), new PointF(40, 140) };

        // Prepend (the default): the points are rotated first, then translated.
        using var rotateThenMove = new Matrix();
        rotateThenMove.Translate(300, 120);
        rotateThenMove.Rotate(30);

        // Append: the points are translated first, then rotated about the origin.
        using var moveThenRotate = new Matrix();
        moveThenRotate.Translate(300, 120);
        moveThenRotate.Rotate(30, MatrixOrder.Append);

        var first = (PointF[])points.Clone();
        rotateThenMove.TransformPoints(first);
        var second = (PointF[])points.Clone();
        moveThenRotate.TransformPoints(second);

        // The inverse maps the first result back onto the original points.
        var restored = (PointF[])first.Clone();
        rotateThenMove.Invert();
        rotateThenMove.TransformPoints(restored);

        using var original = new Pen(Color.DarkBlue, 4);
        using var firstPen = new Pen(Color.DarkOrange, 4);
        using var secondPen = new Pen(Color.DarkGreen, 4);
        using var restoredPen = new Pen(Color.Red, 2) { DashStyle = DashStyle.Dash };
        g.DrawPolygon(original, points);
        g.DrawPolygon(firstPen, first);
        g.DrawPolygon(secondPen, second);
        g.DrawPolygon(restoredPen, restored);
        Console.WriteLine($"  inverse restored first point: {restored[0].X:0.0},{restored[0].Y:0.0}");
        ExampleSupport.Save(bitmap, c, "MatrixTransforms-matrix-order-inverse.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Uses saved graphics state to scope a transform without affecting later drawing.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region GraphicsState
    internal static void SaveRestoreState(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 360, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var pen = new Pen(Color.DarkBlue, 5);
        var state = g.Save();
        g.TranslateTransform(350, 170);
        g.RotateTransform(25);
        g.DrawRectangle(pen, new RectangleF(-120, -70, 240, 140));
        g.Restore(state);
        g.DrawRectangle(new Pen(Color.DarkOrange, 5), new RectangleF(40, 40, 200, 120));
        ExampleSupport.Save(bitmap, c, "GraphicsState-save-restore-state.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Demonstrates page units and page-coordinate settings supported by the drawing backend.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region PageUnits
    internal static void UnitsAndPageCoordinates(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(760, 400, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var pen = new Pen(Color.DarkGreen, 3);
        using var label = new DrawingFont(DRIT.Drawing.Text.FontFamily.GenericSansSerif, 16);
        using var text = new SolidBrush(Color.Black);
        g.PageUnit = GraphicsUnit.Millimeter;
        g.PageScale = 1f;
        g.DrawRectangle(pen, new RectangleF(10, 10, 80, 45));
        g.DrawString("80 × 45 mm page-unit rectangle", label, text, 12, 60);
        File.WriteAllText(c.Path("PageUnits-units.txt"), $"PageUnit={g.PageUnit}; PageScale={g.PageScale}");
        ExampleSupport.Save(bitmap, c, "PageUnits-units-page-coordinates.png", ImageFormat.Png);
    }
    #endregion
}




