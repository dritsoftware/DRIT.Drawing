using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Rendering;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Brushes;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Pens;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class RasterizerTests
    {
        [TestMethod]
        public void Rasterizer_FillPath_SolidRectangle_FillsInterior()
        {
            using var bmp = new Bitmap(20, 20, PixelFormat.Format32bppArgb);
            using var path = new GraphicsPath();
            using var brush = new SolidBrush(Color.Red);

            path.AddRectangle(new RectangleF(2, 3, 10, 8));
            Rasterizer.FillPath(
                bmp,
                path,
                brush,
                transform: null,
                fillMode: FillMode.Alternate,
                clip: null,
                smoothingMode: SmoothingMode.None,
                compositingMode: CompositingMode.SourceOver,
                interpolationMode: InterpolationMode.NearestNeighbor);

            Assert.AreEqual(Color.Red.ToArgb(), bmp.GetPixel(5, 5).ToArgb());
            Assert.AreEqual(0, bmp.GetPixel(0, 0).A);
            Assert.AreEqual(0, bmp.GetPixel(19, 19).A);
        }

        [TestMethod]
        public void Rasterizer_FillPath_EvenOdd_CreatesHole()
        {
            using var bmp = new Bitmap(24, 24, PixelFormat.Format32bppArgb);
            using var path = new GraphicsPath(FillMode.Alternate);
            using var brush = new SolidBrush(Color.Blue);

            path.AddRectangle(new RectangleF(2, 2, 20, 20));
            path.AddRectangle(new RectangleF(8, 8, 8, 8));

            Rasterizer.FillPath(
                bmp,
                path,
                brush,
                transform: null,
                fillMode: FillMode.Alternate,
                clip: null,
                smoothingMode: SmoothingMode.None,
                compositingMode: CompositingMode.SourceOver,
                interpolationMode: InterpolationMode.NearestNeighbor);

            Assert.AreEqual(Color.Blue.ToArgb(), bmp.GetPixel(4, 4).ToArgb());
            Assert.AreEqual(0, bmp.GetPixel(12, 12).A);
        }

        [TestMethod]
        public void Rasterizer_DrawPath_UsesStrokeExpander()
        {
            using var bmp = new Bitmap(30, 30, PixelFormat.Format32bppArgb);
            using var path = new GraphicsPath();
            using var pen = new Pen(Color.Lime, 4f);

            path.AddLine(4, 15, 25, 15);

            Rasterizer.DrawPath(
                bmp,
                path,
                pen,
                transform: null,
                clip: null,
                smoothingMode: SmoothingMode.None,
                compositingMode: CompositingMode.SourceCopy,
                interpolationMode: InterpolationMode.NearestNeighbor);

            Assert.AreEqual(Color.Lime.ToArgb(), bmp.GetPixel(10, 15).ToArgb());
            Assert.AreEqual(0, bmp.GetPixel(10, 2).A);
        }
    }
}