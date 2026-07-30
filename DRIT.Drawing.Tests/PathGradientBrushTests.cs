using DRIT.Drawing.Colors;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class PathGradientBrushTests
    {
        // -----------------------------------------------------------------------
        // PathGradientBrush
        // -----------------------------------------------------------------------

        [TestMethod]
        public void PathGradientBrush_PointsCtor_CentroidIsCenter()
        {
            var pts = new[] { new PointF(0, 0), new PointF(100, 0), new PointF(50, 100) };
            using var b = new PathGradientBrush(pts);
            // Centroid of the triangle
            Assert.AreEqual(50f, b.CenterPoint.X, 1e-4f);
            Assert.AreEqual(100f / 3f, b.CenterPoint.Y, 1e-4f);
        }

        [TestMethod]
        public void PathGradientBrush_DefaultColors()
        {
            var pts = new[] { new PointF(0, 0), new PointF(100, 0), new PointF(50, 100) };
            using var b = new PathGradientBrush(pts);
            Assert.AreEqual(Color.White, b.CenterColor);
            Assert.AreEqual(1, b.SurroundColors.Length);
            Assert.AreEqual(Color.Black, b.SurroundColors[0]);
        }

        [TestMethod]
        public void PathGradientBrush_SetCenterColor()
        {
            var pts = new[] { new PointF(0, 0), new PointF(100, 0), new PointF(50, 100) };
            using var b = new PathGradientBrush(pts);
            b.CenterColor = Color.Red;
            Assert.AreEqual(Color.Red, b.CenterColor);
        }

        [TestMethod]
        public void PathGradientBrush_SetSurroundColors()
        {
            var pts = new[] { new PointF(0, 0), new PointF(100, 0), new PointF(50, 100) };
            using var b = new PathGradientBrush(pts);
            b.SurroundColors = new[] { Color.Blue, Color.Green, Color.Red };
            Assert.AreEqual(3, b.SurroundColors.Length);
        }

        [TestMethod]
        public void PathGradientBrush_Rectangle_MatchesBoundingBox()
        {
            var pts = new[] { new PointF(10, 20), new PointF(110, 20), new PointF(110, 70), new PointF(10, 70) };
            using var b = new PathGradientBrush(pts);
            var rect = b.Rectangle;
            Assert.AreEqual(10f, rect.X, 1e-4f);
            Assert.AreEqual(20f, rect.Y, 1e-4f);
            Assert.AreEqual(100f, rect.Width, 1e-4f);
            Assert.AreEqual(50f, rect.Height, 1e-4f);
        }

        [TestMethod]
        public void PathGradientBrush_PathCtor_Works()
        {
            using var path = new GraphicsPath();
            path.AddRectangle(new RectangleF(0, 0, 100, 100));
            using var b = new PathGradientBrush(path);
            var rect = b.Rectangle;
            Assert.IsTrue(rect.Width > 0f);
        }

        [TestMethod]
        public void PathGradientBrush_Clone_IsIndependent()
        {
            var pts = new[] { new PointF(0, 0), new PointF(100, 0), new PointF(50, 100) };
            using var b = new PathGradientBrush(pts);
            b.CenterColor = Color.Red;
            using var c = (PathGradientBrush)b.Clone();
            b.CenterColor = Color.Blue;
            Assert.AreEqual(Color.Red, c.CenterColor); // clone unaffected
        }

        [TestMethod]
        public void PathGradientBrush_SetBlendTriangularShape_SetsBlend()
        {
            var pts = new[] { new PointF(0, 0), new PointF(100, 0), new PointF(50, 100) };
            using var b = new PathGradientBrush(pts);
            b.SetBlendTriangularShape(0.5f);
            Assert.IsNotNull(b.Blend);
            Assert.AreEqual(3, b.Blend!.Positions.Length);
            Assert.AreEqual(0.5f, b.Blend.Positions[1], 1e-4f);
        }

        [TestMethod]
        public void PathGradientBrush_SetSigmaBellShape_SetsBlend()
        {
            var pts = new[] { new PointF(0, 0), new PointF(100, 0), new PointF(50, 100) };
            using var b = new PathGradientBrush(pts);
            b.SetSigmaBellShape(0.5f);
            Assert.IsNotNull(b.Blend);
            Assert.IsTrue(b.Blend!.Positions.Length > 2);
        }

        [TestMethod]
        public void PathGradientBrush_WrapMode_DefaultClamp()
        {
            var pts = new[] { new PointF(0, 0), new PointF(100, 0), new PointF(50, 100) };
            using var b = new PathGradientBrush(pts);
            Assert.AreEqual(WrapMode.Clamp, b.WrapMode);
        }

        [TestMethod]
        public void PathGradientBrush_Transform_ResetTransform()
        {
            var pts = new[] { new PointF(0, 0), new PointF(100, 0), new PointF(50, 100) };
            using var b = new PathGradientBrush(pts);
            b.TranslateTransform(10f, 20f);
            Assert.AreEqual(10f, b.Transform.OffsetX, 1e-4f);
            b.ResetTransform();
            Assert.IsTrue(b.Transform.IsIdentity);
        }
    }
}
