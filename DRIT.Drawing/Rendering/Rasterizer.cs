using System;
using System.Collections.Generic;
using DRIT.Drawing.Brushes;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Rendering
{
    internal static class Rasterizer
    {
        public static void FillPath(
            Bitmap target,
            GraphicsPath path,
            Brush brush,
            Matrix? transform,
            FillMode fillMode,
            Rectangle? clip,
            SmoothingMode smoothingMode,
            CompositingMode compositingMode,
            InterpolationMode interpolationMode)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (brush == null) throw new ArgumentNullException(nameof(brush));

            bool aa = smoothingMode == SmoothingMode.AntiAlias || smoothingMode == SmoothingMode.HighQuality;
            if (aa)
            {
                FillPathAntialiased(target, path, brush, transform, fillMode, clip, compositingMode, interpolationMode);
                return;
            }

            GraphicsPath effectivePath = path.Clone();
            if (transform != null)
                effectivePath.Transform(transform);

            Brush deviceBrush = BrushInDeviceSpace(brush, transform);
            try
            {
                FillPathCore(target, effectivePath, deviceBrush, fillMode, clip, compositingMode, interpolationMode);
            }
            finally
            {
                if (!ReferenceEquals(deviceBrush, brush))
                    deviceBrush.Dispose();
            }
        }

        public static void DrawPath(
            Bitmap target,
            GraphicsPath path,
            Pens.Pen pen,
            Matrix? transform,
            Rectangle? clip,
            SmoothingMode smoothingMode,
            CompositingMode compositingMode,
            InterpolationMode interpolationMode)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (pen == null) throw new ArgumentNullException(nameof(pen));

            // Pen width, dashes and caps are measured in world units, so the stroke is built before the
            // transform is applied: a scaled transform then scales the pen with the geometry. The flatness
            // and the one-pixel minimum width are device quantities and are converted to world units.
            float maxScale = MaxScale(transform);
            GraphicsPath widened = StrokeExpander.Expand(path, pen, null, 0.25f / maxScale, 0.5f / maxScale);
            FillPath(target, widened, pen.Brush, transform, FillMode.Winding, clip, smoothingMode, compositingMode, interpolationMode);
        }

        /// <summary>The largest factor by which <paramref name="transform"/> stretches a vector.</summary>
        private static float MaxScale(Matrix? transform)
        {
            if (transform == null || transform.IsIdentity)
                return 1f;

            float[] m = transform.Elements;
            float sx = (float)Math.Sqrt(m[0] * m[0] + m[1] * m[1]);
            float sy = (float)Math.Sqrt(m[2] * m[2] + m[3] * m[3]);
            float scale = Math.Max(sx, sy);
            return scale > 1e-6f && !float.IsNaN(scale) && !float.IsInfinity(scale) ? scale : 1f;
        }

        /// <summary>
        /// Brushes live in world space, like the geometry they fill. Returns a copy of a gradient or
        /// texture brush whose transform also applies <paramref name="toDevice"/>, or the brush itself
        /// when nothing has to change. A returned copy must be disposed by the caller.
        /// </summary>
        private static Brush BrushInDeviceSpace(Brush brush, Matrix? toDevice)
        {
            if (toDevice == null || toDevice.IsIdentity)
                return brush;

            switch (brush)
            {
                case LinearGradientBrush _:
                case PathGradientBrush _:
                case TextureBrush _:
                    Brush copy = brush.Clone();
                    switch (copy)
                    {
                        case LinearGradientBrush linear: linear.MultiplyTransform(toDevice, MatrixOrder.Append); break;
                        case PathGradientBrush radial: radial.MultiplyTransform(toDevice, MatrixOrder.Append); break;
                        case TextureBrush texture: texture.MultiplyTransform(toDevice, MatrixOrder.Append); break;
                    }
                    return copy;
                default:
                    return brush;
            }
        }

        private static int ClampToInt(double value, int minimum, int maximum)
            => (int)Math.Max(minimum, Math.Min(maximum, value));

        private static void FillPathAntialiased(
            Bitmap target,
            GraphicsPath path,
            Brush brush,
            Matrix? transform,
            FillMode fillMode,
            Rectangle? clip,
            CompositingMode compositingMode,
            InterpolationMode interpolationMode)
        {
            // A solid colour does not depend on where an area sits in the bitmap, so the work can be limited to
            // the pixels the path can touch (a page-sized bitmap per path is very slow for a page of many paths).
            // Any other brush samples by bitmap position and keeps the whole bitmap.
            Rectangle clipRect = ClipToBounds(clip, target.Width, target.Height);
            Rectangle area = new Rectangle(0, 0, target.Width, target.Height);
            Rectangle? hiClip = null;
            if (brush is SolidBrush && compositingMode == CompositingMode.SourceOver)
            {
                RectangleF bounds = path.GetBounds(transform);
                if (float.IsNaN(bounds.Left) || float.IsNaN(bounds.Top) || float.IsNaN(bounds.Right) || float.IsNaN(bounds.Bottom))
                    return;

                // Clamp before converting: bounds beyond the int range (huge viewBoxes, extreme zoom) must
                // still cover the clip instead of wrapping around and drawing nothing.
                int left = ClampToInt(Math.Floor((double)bounds.Left) - 2, clipRect.X, clipRect.Right);
                int top = ClampToInt(Math.Floor((double)bounds.Top) - 2, clipRect.Y, clipRect.Bottom);
                int right = ClampToInt(Math.Ceiling((double)bounds.Right) + 2, clipRect.X, clipRect.Right);
                int bottom = ClampToInt(Math.Ceiling((double)bounds.Bottom) + 2, clipRect.Y, clipRect.Bottom);
                if (right <= left || bottom <= top)
                    return;

                clipRect = new Rectangle(left, top, right - left, bottom - top);
                area = clipRect;
            }
            else if (clip.HasValue)
            {
                hiClip = new Rectangle(clip.Value.X * 2, clip.Value.Y * 2, clip.Value.Width * 2, clip.Value.Height * 2);
            }

            // The high-resolution bitmap covers `area` at twice the size.
            using var hi = new Bitmap(area.Width * 2, area.Height * 2, PixelFormat.Format32bppArgb);
            GraphicsPath hiPath = path.Clone();

            var aaTransform = new Matrix(2f, 0f, 0f, 2f, 0f, 0f);
            if (transform != null)
                aaTransform.Multiply(transform, MatrixOrder.Append);
            aaTransform.Translate(-area.X * 2f, -area.Y * 2f, MatrixOrder.Append);
            hiPath.Transform(aaTransform);

            Brush hiBrush = BrushInDeviceSpace(brush, aaTransform);
            try
            {
                FillPathCore(hi, hiPath, hiBrush, fillMode, hiClip, CompositingMode.SourceOver, interpolationMode);
            }
            finally
            {
                if (!ReferenceEquals(hiBrush, brush))
                    hiBrush.Dispose();
            }

            for (int y = clipRect.Y; y < clipRect.Bottom; y++)
            {
                for (int x = clipRect.X; x < clipRect.Right; x++)
                {
                    int hx = (x - area.X) * 2;
                    int hy = (y - area.Y) * 2;
                    Color c00 = hi.GetPixel(hx, hy);
                    Color c10 = hi.GetPixel(hx + 1, hy);
                    Color c01 = hi.GetPixel(hx, hy + 1);
                    Color c11 = hi.GetPixel(hx + 1, hy + 1);

                    // The colour is the average of the covered samples, weighted by their alpha: a transparent
                    // sample has no colour, and counting it as black would darken every edge of a shape.
                    int alphaSum = c00.A + c10.A + c01.A + c11.A;
                    if (alphaSum == 0)
                    {
                        PixelCompositor.BlendPixel(target, x, y, Color.FromArgb(0, 0, 0, 0), compositingMode);
                        continue;
                    }

                    Color avg = Color.FromArgb(
                        (alphaSum + 2) / 4,
                        (c00.R * c00.A + c10.R * c10.A + c01.R * c01.A + c11.R * c11.A + alphaSum / 2) / alphaSum,
                        (c00.G * c00.A + c10.G * c10.A + c01.G * c01.A + c11.G * c11.A + alphaSum / 2) / alphaSum,
                        (c00.B * c00.A + c10.B * c10.A + c01.B * c01.A + c11.B * c11.A + alphaSum / 2) / alphaSum);

                    PixelCompositor.BlendPixel(target, x, y, avg, compositingMode);
                }
            }
        }

        private static void FillPathCore(
            Bitmap target,
            GraphicsPath path,
            Brush brush,
            FillMode fillMode,
            Rectangle? clip,
            CompositingMode compositingMode,
            InterpolationMode interpolationMode)
        {
            List<EdgeSeed> seeds = EdgeBuilder.Build(path, 0.25f);
            if (seeds.Count == 0)
                return;

            int minY = int.MaxValue;
            int maxYEx = int.MinValue;
            var buckets = new Dictionary<int, List<ActiveEdge>>();

            for (int i = 0; i < seeds.Count; i++)
            {
                EdgeSeed seed = seeds[i];
                if (!buckets.TryGetValue(seed.YStart, out List<ActiveEdge>? list))
                {
                    list = new List<ActiveEdge>();
                    buckets[seed.YStart] = list;
                }

                list.Add(seed.Edge);
                if (seed.YStart < minY) minY = seed.YStart;
                if (seed.Edge.YMaxExclusive > maxYEx) maxYEx = seed.Edge.YMaxExclusive;
            }

            Rectangle clipRect = ClipToBounds(clip, target.Width, target.Height);
            minY = Math.Max(minY, clipRect.Y);
            maxYEx = Math.Min(maxYEx, clipRect.Bottom);

            var active = new List<ActiveEdge>();
            var spans = new ScanlineBuffer();

            foreach (var bucket in buckets)
            {
                if (bucket.Key >= minY)
                    continue;

                foreach (var edge in bucket.Value)
                {
                    var visibleEdge = edge;
                    visibleEdge.CurrentX += visibleEdge.DxPerY * (minY - bucket.Key);
                    active.Add(visibleEdge);
                }
            }

            for (int y = minY; y < maxYEx; y++)
            {
                if (buckets.TryGetValue(y, out List<ActiveEdge>? startEdges))
                    active.AddRange(startEdges);

                for (int i = active.Count - 1; i >= 0; i--)
                {
                    if (active[i].YMaxExclusive <= y)
                        active.RemoveAt(i);
                }

                if (active.Count == 0)
                    continue;

                active.Sort((a, b) => a.CurrentX.CompareTo(b.CurrentX));
                spans.Clear();

                if (fillMode == FillMode.Alternate)
                {
                    for (int i = 0; i + 1 < active.Count; i += 2)
                        AddSpan(spans, active[i].CurrentX, active[i + 1].CurrentX, clipRect);
                }
                else
                {
                    int winding = 0;
                    float startX = 0f;
                    for (int i = 0; i < active.Count; i++)
                    {
                        int prev = winding;
                        winding += active[i].Winding;

                        if (prev == 0 && winding != 0)
                            startX = active[i].CurrentX;
                        else if (prev != 0 && winding == 0)
                            AddSpan(spans, startX, active[i].CurrentX, clipRect);
                    }
                }

                DrawSpans(target, y, spans, brush, compositingMode, interpolationMode);

                for (int i = 0; i < active.Count; i++)
                {
                    ActiveEdge edge = active[i];
                    edge.CurrentX += edge.DxPerY;
                    active[i] = edge;
                }
            }
        }

        private static void AddSpan(ScanlineBuffer spans, float x0, float x1, Rectangle clipRect)
        {
            if (x1 <= x0)
                return;

            // Clamp in double precision before converting: spans of huge shapes extend far beyond the int range.
            double xsd = Math.Ceiling((double)x0);
            double xed = Math.Floor((double)x1 - 1e-6);
            if (double.IsNaN(xsd) || double.IsNaN(xed) || xed < xsd)
                return;

            int xs = (int)Math.Max(clipRect.X, Math.Min(clipRect.Right, xsd));
            int xe = (int)Math.Min(clipRect.Right - 1, Math.Max(clipRect.X - 1, xed));
            if (xe < xs)
                return;

            spans.AddSpan(xs, xe);
        }

        private static void DrawSpans(
            Bitmap target,
            int y,
            ScanlineBuffer spans,
            Brush brush,
            CompositingMode compositingMode,
            InterpolationMode interpolationMode)
        {
            var list = spans.Spans;
            for (int i = 0; i < list.Count; i++)
            {
                int x0 = list[i].X0;
                int x1 = list[i].X1;
                for (int x = x0; x <= x1; x++)
                {
                    Color src = BrushSampler.Sample(brush, x + 0.5f, y + 0.5f, interpolationMode);
                    PixelCompositor.BlendPixel(target, x, y, src, compositingMode);
                }
            }
        }

        private static Rectangle ClipToBounds(Rectangle? clip, int width, int height)
        {
            Rectangle bounds = new Rectangle(0, 0, width, height);
            if (!clip.HasValue)
                return bounds;

            Rectangle c = clip.Value;
            int x = Math.Max(bounds.X, c.X);
            int y = Math.Max(bounds.Y, c.Y);
            int r = Math.Min(bounds.Right, c.Right);
            int b = Math.Min(bounds.Bottom, c.Bottom);

            if (r <= x || b <= y)
                return Rectangle.Empty;

            return Rectangle.FromLTRB(x, y, r, b);
        }
    }
}
