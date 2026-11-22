using System;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Geometry;

namespace DRIT.Drawing.Rendering
{
    internal static class StrokeExpander
    {
        public static GraphicsPath Expand(GraphicsPath source, Pens.Pen pen, Matrix? transform = null, float flatness = 0.25f, float minHalfWidth = 0.5f)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (pen == null) throw new ArgumentNullException(nameof(pen));

            GraphicsPath effective = DashExpander.Expand(source, pen, flatness);
            if (transform != null)
                effective.Transform(transform);

            effective.Flatten(null, flatness);

            PointF[] points = effective.PathPoints;
            byte[] types = effective.PathTypes;
            var outline = new GraphicsPath(FillMode.Winding);

            float halfWidth = Math.Max(minHalfWidth, pen.Width * 0.5f);
            float penWidth = halfWidth * 2f;
            int subpathStart = -1;
            bool subpathClosed = false;
            PointF? lastDir = null;

            for (int i = 0; i < points.Length; i++)
            {
                byte raw = (byte)(types[i] & (byte)PathPointType.PathTypeMask);
                if (raw == (byte)PathPointType.Start)
                {
                    subpathStart = i;
                    subpathClosed = IsSubpathClosed(types, i);
                    lastDir = null;
                    continue;
                }

                PointF a = points[i - 1];
                PointF b = points[i];
                PointF dir = new PointF(b.X - a.X, b.Y - a.Y);
                float length = (float)Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
                if (length < 1e-6f)
                    continue;

                lastDir = dir;
                bool closes = (types[i] & (byte)PathPointType.CloseSubpath) != 0;
                bool isFirstSegment = subpathStart >= 0 && i == subpathStart + 1;
                bool endOfOpenSubpath = !closes && !subpathClosed
                    && (i + 1 >= points.Length || (types[i + 1] & (byte)PathPointType.PathTypeMask) == (byte)PathPointType.Start);

                // The body of an open line stops short of an end that carries an arrow-like cap.
                PointF bodyStart = a;
                PointF bodyEnd = b;
                float unitX = dir.X / length, unitY = dir.Y / length;
                if (isFirstSegment && !subpathClosed)
                {
                    float inset = Math.Min(LineCapRenderer.GetBodyInset(pen, true, penWidth), length);
                    bodyStart = new PointF(a.X + unitX * inset, a.Y + unitY * inset);
                }
                if (endOfOpenSubpath)
                {
                    float room = length - (float)Math.Sqrt(
                        (bodyStart.X - a.X) * (bodyStart.X - a.X) + (bodyStart.Y - a.Y) * (bodyStart.Y - a.Y));
                    float inset = Math.Min(LineCapRenderer.GetBodyInset(pen, false, penWidth), room);
                    bodyEnd = new PointF(b.X - unitX * inset, b.Y - unitY * inset);
                }

                AddSegmentQuad(outline, bodyStart, bodyEnd, halfWidth);

                if (closes && subpathStart >= 0 && subpathStart != i)
                {
                    PointF c = points[subpathStart];
                    PointF closeDir = new PointF(c.X - b.X, c.Y - b.Y);
                    if (Math.Abs(closeDir.X) > 1e-6f || Math.Abs(closeDir.Y) > 1e-6f)
                        AddSegmentQuad(outline, b, c, halfWidth);
                }

                if (isFirstSegment && !subpathClosed)
                    LineCapRenderer.AddPenCap(outline, a, new PointF(-dir.X, -dir.Y), pen, true, halfWidth);

                if (endOfOpenSubpath && lastDir != null)
                    LineCapRenderer.AddPenCap(outline, b, lastDir.Value, pen, false, halfWidth);
            }

            return outline;
        }

        /// <summary>True when the figure that starts at <paramref name="start"/> ends with a close flag.</summary>
        private static bool IsSubpathClosed(byte[] types, int start)
        {
            int end = start;
            while (end + 1 < types.Length
                   && (types[end + 1] & (byte)PathPointType.PathTypeMask) != (byte)PathPointType.Start)
                end++;
            return end > start && (types[end] & (byte)PathPointType.CloseSubpath) != 0;
        }

        private static void AddSegmentQuad(GraphicsPath path, PointF a, PointF b, float halfWidth)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len <= 1e-6f)
                return;

            float nx = -dy / len * halfWidth;
            float ny = dx / len * halfWidth;

            PointF p0 = new PointF(a.X + nx, a.Y + ny);
            PointF p1 = new PointF(a.X - nx, a.Y - ny);
            PointF p2 = new PointF(b.X - nx, b.Y - ny);
            PointF p3 = new PointF(b.X + nx, b.Y + ny);
            path.AddPolygon(new[] { p0, p1, p2, p3 });
        }
    }
}
