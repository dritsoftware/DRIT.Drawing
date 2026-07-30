using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Brushes;

namespace DRIT.Drawing.IntegrationTests
{
    /// <summary>
    /// Integration tests for Color class that produce visual output.
    /// 
    /// Human Tester Instructions:
    /// - Images are saved to the Out folder with descriptive names
    /// - Verify that color swatches are accurate and distinct
    /// - Check that alpha gradients show proper transparency
    /// - Verify gradient transitions are smooth
    /// </summary>
    [TestClass]
    public class ColorIntegrationTests : IntegrationTestBase
    {
        [TestMethod]
        public void Color_KnownColors_VisibleOutput()
        {
            // Human Tester: Should see a 400x400 image with a grid of color swatches
            // Each swatch shows a different KnownColor value
            // Colors should be clearly distinguishable and accurate
            using var bmp = new Bitmap(400, 400);
            using var g = Graphics.FromImage(bmp);

            var knownColors = Enum.GetValues(typeof(KnownColor));
            int cols = 8;
            int cellSize = 40;
            int margin = 5;

            int count = 0;
            foreach (KnownColor kc in knownColors)
            {
                if (count >= 256) break; // Limit to fit in image

                int row = count / cols;
                int col = count % cols;
                int x = margin + col * (cellSize + margin);
                int y = margin + row * (cellSize + margin);

                using var brush = new SolidBrush(Color.FromKnownColor(kc));
                g.FillRectangle(brush, x, y, cellSize, cellSize);

                count++;
            }

            SaveImage(bmp, "Color_KnownColors_VisibleOutput");
        }

        [TestMethod]
        public void Color_ArgbGradients_VisibleOutput()
        {
            // Human Tester: Should see a 400x400 image with:
            // - Top half: Alpha gradient from fully transparent (top-left) to fully opaque (bottom-right)
            // - Bottom half: Color gradient from red to blue to green
            // - Use JPEG for smooth gradient output
            using var bmp = new Bitmap(400, 400);

            // Alpha gradient (top half)
            for (int y = 0; y < 200; y++)
            {
                for (int x = 0; x < 400; x++)
                {
                    int alpha = (int)(255 * x / 399f);
                    bmp.SetPixel(x, y, Color.FromArgb(alpha, 255, 0, 0));
                }
            }

            // Color gradient (bottom half)
            for (int y = 200; y < 400; y++)
            {
                for (int x = 0; x < 400; x++)
                {
                    int r = (int)(255 * (1f - x / 399f));
                    int g = (int)(255 * (x / 399f));
                    int b = (int)(128 * Math.Abs(Math.Sin(x * Math.PI / 200)));
                    bmp.SetPixel(x, y, Color.FromArgb(255, r, g, b));
                }
            }

            SaveImage(bmp, "Color_ArgbGradients_VisibleOutput", ImageFormat.Jpeg);
        }
    }
}