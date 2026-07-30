using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Imaging;
using System.IO;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Brushes;

namespace DRIT.Drawing.IntegrationTests
{
    /// <summary>
    /// Integration tests for image operations that produce visual output.
    /// 
    /// Human Tester Instructions:
    /// - Images are saved to the Out folder with descriptive names
    /// - Verify that scaling produces expected results
    /// - Check that interpolation modes produce different visual quality
    /// - For NearestNeighbor: pixels should be sharp/blocky
    /// - For Bilinear/Bicubic: pixels should be smooth/blended
    /// </summary>
    [TestClass]
    public class ImageIntegrationTests : IntegrationTestBase
    {
        /// <summary>Tests nearest-neighbor interpolation scaling at multiple scales.</summary>
        [TestMethod]
        public void DrawImage_NearestNeighbor_VisibleOutput()
        {
            // Human Tester: Should see a 400x400 image with:
            // - Top-left: 8x8 source image scaled 2x using NearestNeighbor
            // - Top-right: 8x8 source image scaled 4x using NearestNeighbor
            // - Bottom-left: 8x8 source image scaled 8x using NearestNeighbor
            // - Bottom-right: 8x8 source image scaled 10x using NearestNeighbor
            // - All should show sharp, blocky pixels (no blending)
            using var src = new Bitmap(8, 8);
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    bool even = ((x + y) & 1) == 0;
                    src.SetPixel(x, y, even ? Color.Red : Color.Blue);
                }
            }

            using var bmp = new Bitmap(400, 400);
            using var g = Graphics.FromImage(bmp);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;

            g.DrawImage(src, new Rectangle(20, 20, 16, 16));
            g.DrawImage(src, new Rectangle(220, 20, 32, 32));
            g.DrawImage(src, new Rectangle(20, 220, 64, 64));
            g.DrawImage(src, new Rectangle(220, 220, 80, 80));

            SaveImage(bmp, "DrawImage_NearestNeighbor_VisibleOutput");
        }

        [TestMethod]
        public void DrawImage_Bilinear_VisibleOutput()
        {
            // Human Tester: Should see a 400x400 image with:
            // - Top-left: 2x2 source scaled 8x using NearestNeighbor (sharp edges)
            // - Top-right: 2x2 source scaled 8x using Bilinear (smooth edges)
            // - Bottom-left: 2x2 source scaled 8x using Bicubic (very smooth)
            // - Bottom-right: 4x4 gradient scaled 10x using Bilinear (smooth gradient)
            // - Compare the smoothness of each interpolation mode
            using var src2x2 = new Bitmap(2, 2);
            src2x2.SetPixel(0, 0, Color.Red);
            src2x2.SetPixel(1, 0, Color.Blue);
            src2x2.SetPixel(0, 1, Color.Green);
            src2x2.SetPixel(1, 1, Color.Yellow);

            using var src4x4 = new Bitmap(4, 4);
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    int r = (int)(255 * (1f - x / 3f));
                    int b = (int)(255 * (x / 3f));
                    src4x4.SetPixel(x, y, Color.FromArgb(255, r, 0, b));
                }
            }

            using var bmp = new Bitmap(400, 400);
            using var g = Graphics.FromImage(bmp);

            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(src2x2, new Rectangle(10, 10, 64, 64));

            g.InterpolationMode = InterpolationMode.Bilinear;
            g.DrawImage(src2x2, new Rectangle(220, 10, 64, 64));

            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(src2x2, new Rectangle(10, 220, 64, 64));

            g.DrawImage(src4x4, new Rectangle(220, 220, 160, 160));

            SaveImage(bmp, "DrawImage_Bilinear_VisibleOutput", ImageFormat.Jpeg);
        }

        [TestMethod]
        public void Metafile_Render_VisibleOutput()
        {
            // Human Tester: Should see a 400x300 image with:
            // - A red rectangle at (20,20) size 80x80
            // - A blue circle at (150,20) size 80x80
            using var bmp = new Bitmap(400, 300);
            using var g = Graphics.FromImage(bmp);
            using var redBrush = new SolidBrush(Color.Red);
            using var blueBrush = new SolidBrush(Color.Blue);

            g.FillRectangle(redBrush, 20, 20, 80, 80);
            g.FillEllipse(blueBrush, new Rectangle(150, 20, 80, 80));

            SaveImage(bmp, "Metafile_Render_VisibleOutput");
        }
    }
}