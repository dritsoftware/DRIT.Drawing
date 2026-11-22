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
/// Contains runnable examples for geometric primitives, curves, paths, regions, and hit testing.
/// </summary>
internal static class DrawingExamples
{
    /// <summary>
    /// Draws connected line segments and multiple independent polylines to show the difference between continuous and grouped line geometry.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region LinesAndPolylines
    internal static void ShapesLinesAndPolylines(ExampleContext context)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 420, Color.White);
        using var graphics = Graphics.FromImage(bitmap);
        using var pen = new Pen(Color.DarkSlateBlue, 5);
        graphics.DrawLines(pen, new[] { new PointF(40, 70), new PointF(160, 25), new PointF(280, 70) });
        graphics.DrawPolylineGroup(pen, new[]
        {
            new[] { new PointF(60, 180), new PointF(160, 135), new PointF(260, 180) },
            new[] { new PointF(390, 180), new PointF(490, 135), new PointF(590, 180) },
        });
        ExampleSupport.Save(bitmap, context, "LinesAndPolylines-lines-polylines.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Draws and fills rectangles, ellipses, and polygons using pens and solid brushes.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region BasicShapes
    internal static void ShapesBasicGeometry(ExampleContext context)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 420, Color.WhiteSmoke);
        using var graphics = Graphics.FromImage(bitmap);
        using var outline = new Pen(Color.DarkBlue, 5);
        using var red = new SolidBrush(Color.FromArgb(220, 210, 70, 65));
        using var blue = new SolidBrush(Color.FromArgb(220, 65, 110, 210));
        graphics.DrawRectangle(outline, 45, 45, 180, 120);
        graphics.FillRectangle(red, 280, 45, 180, 120);
        graphics.FillEllipse(blue, new RectangleF(500, 45, 150, 120));
        graphics.DrawPolygon(outline, new[]
        {
            new PointF(110, 300), new PointF(220, 205), new PointF(330, 300),
        });
        ExampleSupport.Save(bitmap, context, "BasicShapes-basic-shapes.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Shows how start angles and sweep angles control arcs and pie-shaped geometry.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region ArcsAndPie
    internal static void ShapesArcsAndPie(ExampleContext context)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 420, Color.White);
        using var graphics = Graphics.FromImage(bitmap);
        using var outline = new Pen(Color.DarkGreen, 6);
        using var fill = new SolidBrush(Color.FromArgb(210, 90, 180, 110));
        graphics.DrawArc(outline, new RectangleF(50, 45, 220, 180), 20, 260);
        graphics.FillPie(fill, new RectangleF(370, 45, 260, 220), -35, 210);
        graphics.DrawPie(outline, new RectangleF(370, 45, 260, 220), -35, 210);
        ExampleSupport.Save(bitmap, context, "ArcsAndPie-arcs-pie.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Builds a reusable graphics path from multiple figures, fills and outlines it, and reports its bounds.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region GraphicsPath
    internal static void ShapesGraphicsPath(ExampleContext context)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 420, Color.White);
        using var graphics = Graphics.FromImage(bitmap);
        using var path = new GraphicsPath(FillMode.Winding);
        using var outline = new Pen(Color.DarkRed, 5);
        using var fill = new SolidBrush(Color.FromArgb(200, 240, 170, 70));
        path.AddEllipse(new RectangleF(110, 70, 260, 220));
        path.AddRectangle(new RectangleF(250, 175, 300, 120));
        path.CloseAllFigures();
        graphics.FillPath(fill, path);
        graphics.DrawPath(outline, path);
        Console.WriteLine($"  path bounds: {path.GetBounds()}");
        ExampleSupport.Save(bitmap, context, "GraphicsPath-graphics-path.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Draws a cubic Bézier curve so the effect of its control points can be inspected.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region BezierCurves
    internal static void BezierCurves(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 400, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var pen = new Pen(Color.DarkBlue, 5);
        g.DrawBezier(pen, new PointF(60, 300), new PointF(180, 30), new PointF(500, 370), new PointF(650, 90));
        ExampleSupport.Save(bitmap, c, "BezierCurves-bezier-curves.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Compares an open cardinal curve with a filled and outlined closed curve using the same points and tension.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region CardinalCurves
    internal static void CardinalAndClosedCurves(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 400, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var pen = new Pen(Color.DarkGreen, 4);
        using var brush = new SolidBrush(Color.FromArgb(160, 100, 190, 120));
        var points = new[] { new PointF(50, 230), new PointF(160, 80), new PointF(320, 290), new PointF(490, 100), new PointF(650, 230) };
        g.DrawCurve(pen, points, 0.35f);
        g.FillClosedCurve(brush, points, FillMode.Winding, 0.35f);
        g.DrawClosedCurve(pen, points, 0.35f, FillMode.Winding);
        ExampleSupport.Save(bitmap, c, "CardinalCurves-cardinal-curves.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Compares alternate and winding fill rules with nested overlapping figures.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region PathFillRules
    internal static void PathFillRules(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 360, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var alternate = new GraphicsPath(FillMode.Alternate);
        using var winding = new GraphicsPath(FillMode.Winding);
        using var brush = new SolidBrush(Color.FromArgb(200, 70, 130, 210));
        alternate.AddEllipse(new RectangleF(70, 70, 230, 190));
        alternate.AddEllipse(new RectangleF(130, 115, 110, 90));
        winding.AddEllipse(new RectangleF(420, 70, 230, 190));
        winding.AddEllipse(new RectangleF(480, 115, 110, 90));
        g.FillPath(brush, alternate);
        g.FillPath(new SolidBrush(Color.FromArgb(200, 210, 100, 70)), winding);
        ExampleSupport.Save(bitmap, c, "PathFillRules-path-fill-rules.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Excludes a rectangle from a region, fills the result, and reports the resulting bounds and visibility test.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region RegionOperations
    internal static void RegionOperations(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(720, 360, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var region = new Region(new RectangleF(70, 70, 330, 210));
        region.Exclude(new RectangleF(180, 120, 150, 110));
        using var brush = new SolidBrush(Color.FromArgb(210, 80, 150, 100));
        g.FillRegion(brush, region);
        using var outline = new Pen(Color.DarkSlateGray, 3);
        g.DrawRectangle(outline, new RectangleF(70, 70, 330, 210));
        Console.WriteLine($"  region bounds: {region.GetBounds()}; center visible={region.IsVisible(235, 175)}");
        ExampleSupport.Save(bitmap, c, "RegionOperations-region-operations.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Creates a path, renders it, and tests whether a point lies inside the path.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region PathHitTesting
    internal static void PathHitTesting(ExampleContext c)
    {
        using var path = new GraphicsPath();
        using var bitmap = ExampleSupport.NewCanvas(720, 360, Color.White);
        using var g = Graphics.FromImage(bitmap);
        using var fill = new SolidBrush(Color.FromArgb(190, 60, 140, 200));
        using var pen = new Pen(Color.DarkBlue, 4);
        path.AddEllipse(new RectangleF(120, 60, 480, 240));
        g.FillPath(fill, path);
        g.DrawPath(pen, path);
        bool inside = path.IsVisible(new PointF(360, 180));
        Console.WriteLine($"  path bounds={path.GetBounds()}, center visible={inside}");
        ExampleSupport.Save(bitmap, c, "PathHitTesting-path-hit-testing.png", ImageFormat.Png);
    }
    #endregion
}




