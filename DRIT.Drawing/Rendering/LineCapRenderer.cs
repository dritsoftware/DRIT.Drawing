using System;
using System.Collections.Generic;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Pens;

namespace DRIT.Drawing.Rendering
{
    /// <summary>
    /// Builds the outline of line caps: the basic caps (round, square, triangle), the anchor caps
    /// (square, round, diamond, arrow) and custom caps (<see cref="AdjustableArrowCap"/> and
    /// <see cref="CustomLineCap"/>).
    /// </summary>
    /// <remarks>
    /// Anchor and custom caps are sized in multiples of the pen width. The cap's tip or centre is
    /// placed on the end of the line, and <see cref="GetBodyInset"/> reports how far the line body
    /// has to stop short of that end so that it does not show through the cap.
    /// </remarks>
    internal static class LineCapRenderer
    {
        private const float AnchorHalfSize = 1.25f;     // in pen widths (square / round / diamond anchors)
        private const float ArrowAnchorWidth = 3f;      // in pen widths
        private const float ArrowAnchorLength = 2f;     // in pen widths

        public static void AddCap(GraphicsPath path, PointF point, PointF direction, float halfWidth, LineCap cap)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (halfWidth <= 0f || cap == LineCap.Flat)
                return;

            switch (cap)
            {
                case LineCap.Round:
                    path.AddEllipse(point.X - halfWidth, point.Y - halfWidth, halfWidth * 2f, halfWidth * 2f);
                    break;
                case LineCap.Square:
                    AddOrientedSquare(path, point, direction, halfWidth);
                    break;
                case LineCap.Triangle:
                    AddOrientedTriangle(path, point, direction, halfWidth);
                    break;
                case LineCap.SquareAnchor:
                case LineCap.RoundAnchor:
                case LineCap.DiamondAnchor:
                case LineCap.ArrowAnchor:
                    AddAnchor(path, point, direction, halfWidth * 2f, cap);
                    break;
            }
        }

        /// <summary>
        /// Adds the cap of <paramref name="pen"/> for one end of a line. A custom cap takes
        /// precedence over the basic cap.
        /// </summary>
        /// <param name="path">The outline being built.</param>
        /// <param name="point">The end point of the line.</param>
        /// <param name="outward">Direction pointing away from the line, beyond the end point.</param>
        /// <param name="pen">The pen whose cap is drawn.</param>
        /// <param name="start">True for the start cap, false for the end cap.</param>
        /// <param name="halfWidth">Half the pen width.</param>
        public static void AddPenCap(GraphicsPath path, PointF point, PointF outward, Pen pen, bool start, float halfWidth)
        {
            CustomLineCap? custom = start ? pen.CustomStartCap : pen.CustomEndCap;
            if (custom != null)
            {
                AddCustomCap(path, point, outward, halfWidth * 2f, custom);
                return;
            }

            AddCap(path, point, outward, halfWidth, start ? pen.StartCap : pen.EndCap);
        }

        /// <summary>
        /// The distance the line body must stop short of the line end because a cap that
        /// points at the end point is drawn behind it.
        /// </summary>
        public static float GetBodyInset(Pen pen, bool start, float penWidth)
        {
            CustomLineCap? custom = start ? pen.CustomStartCap : pen.CustomEndCap;
            if (custom is AdjustableArrowCap arrow)
                return Math.Max(0f, arrow.Height * penWidth * arrow.WidthScale);
            if (custom != null)
                return Math.Max(0f, custom.BaseInset * penWidth * custom.WidthScale);

            LineCap cap = start ? pen.StartCap : pen.EndCap;
            return cap == LineCap.ArrowAnchor ? ArrowAnchorLength * penWidth : 0f;
        }

        private static void AddAnchor(GraphicsPath path, PointF p, PointF outward, float penWidth, LineCap cap)
        {
            PointF d = Normalize(outward);
            PointF n = new PointF(-d.Y, d.X);
            float half = AnchorHalfSize * penWidth;

            switch (cap)
            {
                case LineCap.RoundAnchor:
                    path.AddEllipse(p.X - half, p.Y - half, half * 2f, half * 2f);
                    break;
                case LineCap.SquareAnchor:
                    AddPolygon(path, new[]
                    {
                        Map(p, d, n, -half, -half), Map(p, d, n, -half, half),
                        Map(p, d, n, half, half), Map(p, d, n, half, -half),
                    });
                    break;
                case LineCap.DiamondAnchor:
                    AddPolygon(path, new[]
                    {
                        Map(p, d, n, half, 0f), Map(p, d, n, 0f, half),
                        Map(p, d, n, -half, 0f), Map(p, d, n, 0f, -half),
                    });
                    break;
                default: // ArrowAnchor: tip on the end point, base behind it.
                    float length = ArrowAnchorLength * penWidth;
                    float halfBase = ArrowAnchorWidth * penWidth * 0.5f;
                    AddPolygon(path, new[]
                    {
                        Map(p, d, n, 0f, 0f), Map(p, d, n, -length, halfBase), Map(p, d, n, -length, -halfBase),
                    });
                    break;
            }
        }

        private static void AddCustomCap(GraphicsPath path, PointF p, PointF outward, float penWidth, CustomLineCap cap)
        {
            PointF d = Normalize(outward);
            PointF n = new PointF(-d.Y, d.X);
            float scale = penWidth * (cap.WidthScale > 0f ? cap.WidthScale : 1f);

            if (cap is AdjustableArrowCap arrow)
            {
                AddArrowCap(path, p, d, n, arrow, scale);
                return;
            }

            // Cap paths use the GDI+ convention: the origin is the end of the line, +y points
            // away from the line and x runs across it. Coordinates are multiples of the pen width.
            if (cap.FillPath != null)
                AddFigures(path, cap.FillPath, pt => Map(p, d, n, pt.Y * scale, pt.X * scale));

            if (cap.StrokePath != null)
            {
                using (var outline = new GraphicsPath())
                {
                    AddFigures(outline, cap.StrokePath, pt => Map(p, d, n, pt.Y * scale, pt.X * scale), closeFigures: false);
                    using (var thin = new Pen(Colors.Color.Black, Math.Max(1f, scale * 0.25f)))
                    using (GraphicsPath stroked = StrokeExpander.Expand(outline, thin))
                        AddFigures(path, stroked, pt => pt);
                }
            }
        }

        private static void AddArrowCap(GraphicsPath path, PointF p, PointF d, PointF n, AdjustableArrowCap arrow, float scale)
        {
            float length = arrow.Height * scale;
            float halfBase = arrow.Width * scale * 0.5f;
            if (length <= 0f || halfBase <= 0f)
                return;

            float inset = Math.Max(0f, Math.Min(arrow.MiddleInset, 1f)) * length;
            var outer = new List<PointF>
            {
                Map(p, d, n, 0f, 0f),
                Map(p, d, n, -length, halfBase),
            };
            if (inset > 0f)
                outer.Add(Map(p, d, n, -length + inset, 0f));
            outer.Add(Map(p, d, n, -length, -halfBase));

            if (arrow.Filled)
            {
                AddPolygon(path, outer.ToArray());
                return;
            }

            // Hollow arrow: the outline is a ring between the arrow and a shrunken copy of it.
            float cx = 0f, cy = 0f;
            foreach (PointF pt in outer) { cx += pt.X; cy += pt.Y; }
            cx /= outer.Count; cy /= outer.Count;
            var inner = new PointF[outer.Count];
            for (int i = 0; i < inner.Length; i++)
                inner[i] = new PointF(cx + (outer[i].X - cx) * 0.5f, cy + (outer[i].Y - cy) * 0.5f);

            AddPolygon(path, outer.ToArray());
            AddPolygon(path, inner, reverse: true);
        }

        /// <summary>Maps (along, across) in the cap's frame to a point: along runs outward, across is perpendicular.</summary>
        private static PointF Map(PointF origin, PointF d, PointF n, float along, float across)
            => new PointF(origin.X + d.X * along + n.X * across, origin.Y + d.Y * along + n.Y * across);

        /// <summary>
        /// Copies the figures of <paramref name="source"/> into <paramref name="target"/>, mapping each
        /// point and giving every figure the same orientation as the stroke quads so that overlapping
        /// pieces add up under a winding fill instead of cancelling.
        /// </summary>
        private static void AddFigures(GraphicsPath target, GraphicsPath source, Func<PointF, PointF> map, bool closeFigures = true)
        {
            using (GraphicsPath flat = (GraphicsPath)source.Clone())
            {
                flat.Flatten(null, 0.25f);
                PointF[] points = flat.PathPoints;
                byte[] types = flat.PathTypes;
                var figure = new List<PointF>();
                for (int i = 0; i <= points.Length; i++)
                {
                    bool boundary = i == points.Length
                        || (types[i] & (byte)PathPointType.PathTypeMask) == (byte)PathPointType.Start;
                    if (boundary && figure.Count > 0)
                    {
                        if (figure.Count >= 3 && closeFigures)
                            AddPolygon(target, figure.ToArray());
                        else if (figure.Count >= 2)
                            target.AddLines(figure.ToArray());
                        figure.Clear();
                    }
                    if (i < points.Length)
                        figure.Add(map(points[i]));
                }
            }
        }

        private static void AddPolygon(GraphicsPath path, PointF[] points, bool reverse = false)
        {
            double area = 0;
            for (int i = 0; i < points.Length; i++)
            {
                PointF a = points[i], b = points[(i + 1) % points.Length];
                area += (double)a.X * b.Y - (double)b.X * a.Y;
            }

            // Positive area is the orientation used by the stroke quads.
            bool flip = (area < 0) != reverse;
            if (flip)
                Array.Reverse(points);
            path.AddPolygon(points);
        }

        private static void AddOrientedSquare(GraphicsPath path, PointF p, PointF dir, float half)
        {
            var d = Normalize(dir);
            var n = new PointF(-d.Y, d.X);

            PointF a = new PointF(p.X + (-d.X - n.X) * half, p.Y + (-d.Y - n.Y) * half);
            PointF b = new PointF(p.X + (-d.X + n.X) * half, p.Y + (-d.Y + n.Y) * half);
            PointF c = new PointF(p.X + ( d.X + n.X) * half, p.Y + ( d.Y + n.Y) * half);
            PointF dpt = new PointF(p.X + ( d.X - n.X) * half, p.Y + ( d.Y - n.Y) * half);
            AddPolygon(path, new[] { a, b, c, dpt });
        }

        private static void AddOrientedTriangle(GraphicsPath path, PointF p, PointF dir, float half)
        {
            var d = Normalize(dir);
            var n = new PointF(-d.Y, d.X);

            PointF tip = new PointF(p.X + d.X * (half * 5f), p.Y + d.Y * (half * 5f));
            PointF left = new PointF(p.X - d.X * half + n.X * (half * 3f),
                                     p.Y - d.Y * half + n.Y * (half * 3f));
            PointF right = new PointF(p.X - d.X * half - n.X * (half * 3f),
                                      p.Y - d.Y * half - n.Y * (half * 3f));
            AddPolygon(path, new[] { left, tip, right });
        }

        private static PointF Normalize(PointF p)
        {
            float len = (float)Math.Sqrt(p.X * p.X + p.Y * p.Y);
            if (len <= 1e-6f)
                return new PointF(1f, 0f);
            return new PointF(p.X / len, p.Y / len);
        }
    }
}
