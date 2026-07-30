using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Geometry;

namespace DRIT.Drawing.Tests.Drawing2D
{
    [TestClass]
    public class LinearGradientBrushTests
    {
        [TestMethod]
        public void LinearGradientBrush_FromPoints()
        {
            var b = new LinearGradientBrush(
                new PointF(0, 0), new PointF(100, 0),
                Color.Red, Color.Blue);
            Assert.AreEqual(Color.Red,  b.LinearColors[0]);
            Assert.AreEqual(Color.Blue, b.LinearColors[1]);
        }

        [TestMethod]
        public void LinearGradientBrush_FromRectMode()
        {
            var rect = new RectangleF(0, 0, 100, 50);
            var b = new LinearGradientBrush(rect, Color.White, Color.Black, LinearGradientMode.Horizontal);
            Assert.AreEqual(Color.White, b.LinearColors[0]);
            Assert.AreEqual(Color.Black, b.LinearColors[1]);
            Assert.AreEqual(rect, b.Rectangle);
        }

        [TestMethod]
        public void LinearGradientBrush_LinearColors_Set()
        {
            var b = new LinearGradientBrush(new PointF(0,0), new PointF(1,0), Color.Red, Color.Blue);
            b.LinearColors = new[] { Color.Green, Color.Yellow };
            Assert.AreEqual(Color.Green, b.LinearColors[0]);
            Assert.AreEqual(Color.Yellow, b.LinearColors[1]);
        }

        [TestMethod]
        public void LinearGradientBrush_WrapMode()
        {
            var b = new LinearGradientBrush(new PointF(0,0), new PointF(1,0), Color.Red, Color.Blue);
            b.WrapMode = WrapMode.TileFlipX;
            Assert.AreEqual(WrapMode.TileFlipX, b.WrapMode);
        }

        [TestMethod]
        public void LinearGradientBrush_Transform_Identity()
        {
            var b = new LinearGradientBrush(new PointF(0,0), new PointF(1,0), Color.Red, Color.Blue);
            Assert.IsTrue(b.Transform.IsIdentity);
        }

        [TestMethod]
        public void LinearGradientBrush_ResetTransform()
        {
            var b = new LinearGradientBrush(new PointF(0,0), new PointF(1,0), Color.Red, Color.Blue);
            b.RotateTransform(45f);
            b.ResetTransform();
            Assert.IsTrue(b.Transform.IsIdentity);
        }

        [TestMethod]
        public void LinearGradientBrush_Clone()
        {
            var b = new LinearGradientBrush(new PointF(0,0), new PointF(1,0), Color.Red, Color.Blue);
            var c = (LinearGradientBrush)b.Clone();
            Assert.AreNotSame(b, c);
            Assert.AreEqual(Color.Red, c.LinearColors[0]);
        }
    }
}