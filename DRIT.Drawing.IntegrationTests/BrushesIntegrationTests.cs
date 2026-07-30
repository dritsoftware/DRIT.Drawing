using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Brushes;
using DRIT.Drawing.Geometry;

namespace DRIT.Drawing.IntegrationTests
{
    /// <summary>
    /// Integration tests for Brush classes that produce visual output.
    /// 
    /// Human Tester Instructions:
    /// - Images are saved to the Out folder with descriptive names
    /// - Verify that brush patterns and gradients are rendered correctly
    /// - Check that hatch patterns are clearly visible and distinct
    /// - Verify gradient transitions are smooth
    /// </summary>
    [TestClass]
    public class BrushesIntegrationTests : IntegrationTestBase
    {
        /// <summary>Tests SolidBrush fill with multiple colored rectangles.</summary>
        [TestMethod]
        public void SolidBrush_Fill_VisibleOutput()
        {
            // Human Tester: Should see a 400x300 image with:
            // - Red filled rectangle at (20,20) size 100x80
            // - Green filled rectangle at (150,50) size 100x80
            // - Blue filled rectangle at (280,20) size 100x80
            using var bmp = new Bitmap(400, 300);
            using var g = Graphics.FromImage(bmp);
            using var redBrush = new SolidBrush(Color.Red);
            using var greenBrush = new SolidBrush(Color.Green);
            using var blueBrush = new SolidBrush(Color.Blue);

            g.FillRectangle(redBrush, 20, 20, 100, 80);
            g.FillRectangle(greenBrush, 150, 50, 100, 80);
            g.FillRectangle(blueBrush, 280, 20, 100, 80);

            SaveImage(bmp, "SolidBrush_Fill_VisibleOutput");
        }

        /// <summary>Tests all HatchStyle patterns in a grid layout.</summary>
        [TestMethod]
        public void HatchBrush_Patterns_VisibleOutput()
        {
            // Human Tester: Should see a 600x400 image with a grid of all hatch styles
            // Each cell shows a different HatchStyle pattern in 60x60 cells
            // Patterns should be clearly visible and distinct
            using var bmp = new Bitmap(600, 400);
            using var g = Graphics.FromImage(bmp);

            var hatchStyles = (HatchStyle[])Enum.GetValues(typeof(HatchStyle));
            int cols = 8;
            int cellSize = 60;
            int margin = 10;

            for (int i = 0; i < hatchStyles.Length && i < 48; i++)
            {
                int row = i / cols;
                int col = i % cols;
                int x = margin + col * (cellSize + margin);
                int y = margin + row * (cellSize + margin);

                using var brush = new HatchBrush(hatchStyles[i], Color.Black, Color.White);
                g.FillRectangle(brush, x, y, cellSize, cellSize);
            }

            SaveImage(bmp, "HatchBrush_Patterns_VisibleOutput");
        }

        /// <summary>Tests horizontal linear gradient from red to blue.</summary>
        [TestMethod]
        public void LinearGradientBrush_Horizontal_VisibleOutput()
        {
            // Human Tester: Should see a 400x200 image with a smooth horizontal gradient
            // from red (left) to blue (right) - use JPEG for smooth gradient
            using var bmp = new Bitmap(400, 200);
            using var g = Graphics.FromImage(bmp);
            using var brush = new LinearGradientBrush(
                new PointF(0, 0),
                new PointF(400, 0),
                Color.Red,
                Color.Blue);

            g.FillRectangle(brush, 0, 0, 400, 200);

            SaveImage(bmp, "LinearGradientBrush_Horizontal_VisibleOutput", ImageFormat.Jpeg);
        }

        /// <summary>Tests vertical linear gradient from green to yellow.</summary>
        [TestMethod]
        public void LinearGradientBrush_Vertical_VisibleOutput()
        {
            // Human Tester: Should see a 200x400 image with a smooth vertical gradient
            // from green (top) to yellow (bottom) - use JPEG for smooth gradient
            using var bmp = new Bitmap(200, 400);
            using var g = Graphics.FromImage(bmp);
            using var brush = new LinearGradientBrush(
                new PointF(0, 0),
                new PointF(0, 400),
                Color.Green,
                Color.Yellow);

            g.FillRectangle(brush, 0, 0, 200, 400);

            SaveImage(bmp, "LinearGradientBrush_Vertical_VisibleOutput", ImageFormat.Jpeg);
        }

        /// <summary>Tests diagonal linear gradient from white to black.</summary>
        [TestMethod]
        public void LinearGradientBrush_Angular_VisibleOutput()
        {
            // Human Tester: Should see a 400x400 image with a diagonal gradient
            // from white (top-left) to black (bottom-right) - use JPEG for smooth gradient
            using var bmp = new Bitmap(400, 400);
            using var g = Graphics.FromImage(bmp);
            using var brush = new LinearGradientBrush(
                new PointF(0, 0),
                new PointF(400, 400),
                Color.White,
                Color.Black);

            g.FillRectangle(brush, 0, 0, 400, 400);

            SaveImage(bmp, "LinearGradientBrush_Angular_VisibleOutput", ImageFormat.Jpeg);
        }

        /// <summary>Tests TextureBrush with tiled checkerboard pattern.</summary>
        [TestMethod]
        public void TextureBrush_Tile_VisibleOutput()
        {
            // Human Tester: Should see a 400x300 image with a 50x50 checkerboard pattern
            // tiled across the entire surface
            using var pattern = new Bitmap(50, 50);
            using var pg = Graphics.FromImage(pattern);
            using var blackBrush = new SolidBrush(Color.Black);
            using var whiteBrush = new SolidBrush(Color.White);

            // Create checkerboard pattern
            for (int y = 0; y < 50; y++)
            {
                for (int x = 0; x < 50; x++)
                {
                    bool isBlack = ((x / 25) + (y / 25)) % 2 == 0;
                    pattern.SetPixel(x, y, isBlack ? Color.Black : Color.White);
                }
            }

            using var bmp = new Bitmap(400, 300);
            using var g = Graphics.FromImage(bmp);
            using var brush = new TextureBrush(pattern);

            g.FillRectangle(brush, 0, 0, 400, 300);

            SaveImage(bmp, "TextureBrush_Tile_VisibleOutput");
        }
    }
}