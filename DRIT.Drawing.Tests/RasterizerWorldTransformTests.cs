using DRIT.Drawing.Colors;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Pens;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class RasterizerWorldTransformTests
    {
        private static Bitmap RenderGradient(SmoothingMode smoothing, float scale)
        {
            var bmp = new Bitmap(200, 100);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            g.SmoothingMode = smoothing;
            g.ScaleTransform(scale, scale);
            using var brush = new LinearGradientBrush(
                new PointF(0, 0), new PointF(200 / scale, 0), Color.Black, Color.White);
            g.FillRectangle(brush, 0, 0, 200 / scale, 100 / scale);
            return bmp;
        }

        [DataTestMethod]
        [DataRow(SmoothingMode.None, 1f)]
        [DataRow(SmoothingMode.None, 2f)]
        [DataRow(SmoothingMode.AntiAlias, 1f)]
        [DataRow(SmoothingMode.AntiAlias, 2f)]
        [DataRow(SmoothingMode.AntiAlias, 0.5f)]
        public void LinearGradient_FollowsTheWorldTransformAndSmoothingMode(SmoothingMode smoothing, float scale)
        {
            using Bitmap bmp = RenderGradient(smoothing, scale);
            Assert.AreEqual(64, bmp.GetPixel(50, 10).R, 3);
            Assert.AreEqual(128, bmp.GetPixel(100, 10).R, 3);
            Assert.AreEqual(192, bmp.GetPixel(150, 10).R, 3);
        }

        [DataTestMethod]
        [DataRow(SmoothingMode.None, 1f, 4)]
        [DataRow(SmoothingMode.None, 2f, 8)]
        [DataRow(SmoothingMode.AntiAlias, 3f, 12)]
        public void PenWidth_ScalesWithTheWorldTransform(SmoothingMode smoothing, float scale, int expectedRows)
        {
            using var bmp = new Bitmap(200, 100);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            g.SmoothingMode = smoothing;
            g.ScaleTransform(scale, scale);
            using var pen = new Pen(Color.Black, 4f);
            g.DrawLine(pen, 10 / scale, 50 / scale, 190 / scale, 50 / scale);

            int rows = 0;
            for (int y = 0; y < bmp.Height; y++)
                if (bmp.GetPixel(100, y).R < 128)
                    rows++;
            Assert.AreEqual(expectedRows, rows, 1);
        }

        [TestMethod]
        public void PenWidth_UnderNonUniformScale_StretchesLikeTheGeometry()
        {
            using var bmp = new Bitmap(200, 200);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            g.ScaleTransform(1f, 3f);
            using var pen = new Pen(Color.Black, 4f);
            g.DrawLine(pen, 20, 10, 180, 10);   // horizontal: thickness is stretched vertically
            g.DrawLine(pen, 100, 20, 100, 60);  // vertical: thickness is not stretched horizontally

            int rows = 0;
            for (int y = 0; y < 60; y++)
                if (bmp.GetPixel(50, y).R < 128) rows++;
            int columns = 0;
            for (int x = 0; x < 200; x++)
                if (bmp.GetPixel(x, 120).R < 128) columns++;
            Assert.AreEqual(12, rows, 1);
            Assert.AreEqual(4, columns, 1);
        }

        [TestMethod]
        public void PenWidth_NeverDropsBelowOneDevicePixel()
        {
            using var bmp = new Bitmap(200, 100);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            g.ScaleTransform(0.1f, 0.1f);
            using var pen = new Pen(Color.Black, 1f);
            g.DrawLine(pen, 100, 500, 1900, 500);

            int rows = 0;
            for (int y = 0; y < bmp.Height; y++)
                if (bmp.GetPixel(100, y).R < 128) rows++;
            Assert.AreEqual(1, rows, 1);
        }
    }
}
