using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Rendering;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Brushes;
using DRIT.Drawing.Geometry;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class RasterizerAntialiasTests
    {
        private static void FillAntiAliased(Bitmap target, GraphicsPath path, Color color)
        {
            using var brush = new SolidBrush(color);
            Rasterizer.FillPath(
                target, path, brush,
                transform: null,
                fillMode: FillMode.Alternate,
                clip: null,
                smoothingMode: SmoothingMode.AntiAlias,
                compositingMode: CompositingMode.SourceOver,
                interpolationMode: InterpolationMode.NearestNeighbor);
        }

        [DataTestMethod]
        [DataRow(1e3f)]
        [DataRow(1e6f)]
        [DataRow(1e9f)]
        [DataRow(1e10f)]   // beyond the int range: used to wrap around and draw nothing
        public void FillPath_AntiAliased_PathLargerThanTheBitmap_CoversIt(float extent)
        {
            using var bmp = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
            using var path = new GraphicsPath();
            path.AddRectangle(new RectangleF(-extent, -extent, 2 * extent, 2 * extent));

            FillAntiAliased(bmp, path, Color.Red);

            Assert.AreEqual(Color.Red.ToArgb(), bmp.GetPixel(32, 32).ToArgb());
            Assert.AreEqual(Color.Red.ToArgb(), bmp.GetPixel(0, 0).ToArgb());
            Assert.AreEqual(Color.Red.ToArgb(), bmp.GetPixel(63, 63).ToArgb());
        }

        [TestMethod]
        public void FillPath_AntiAliased_PathOutsideTheBitmap_DrawsNothing()
        {
            using var bmp = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
            using var path = new GraphicsPath();
            path.AddRectangle(new RectangleF(1e10f, 1e10f, 10, 10));

            FillAntiAliased(bmp, path, Color.Red);

            Assert.AreEqual(0, bmp.GetPixel(32, 32).A);
        }

        [TestMethod]
        public void FillPath_AntiAliased_EdgePixelsKeepTheBrushColour()
        {
            // Transparent samples have no colour: averaging them in as black used to darken every edge pixel.
            using var bmp = new Bitmap(40, 40, PixelFormat.Format32bppArgb);
            using var path = new GraphicsPath();
            path.AddEllipse(new RectangleF(4.3f, 5.1f, 30.4f, 28.7f));

            FillAntiAliased(bmp, path, Color.FromArgb(255, 255, 40, 10));

            int edgePixels = 0;
            for (int y = 0; y < 40; y++)
            for (int x = 0; x < 40; x++)
            {
                Color c = bmp.GetPixel(x, y);
                if (c.A == 0 || c.A == 255)
                    continue;
                edgePixels++;
                Assert.IsTrue(c.R >= 253 && c.G is >= 38 and <= 42 && c.B is >= 8 and <= 12,
                    $"edge pixel ({x},{y}) = A{c.A} R{c.R} G{c.G} B{c.B}");
            }
            Assert.IsTrue(edgePixels > 20, "the ellipse must have anti-aliased edge pixels");
        }
    }
}
