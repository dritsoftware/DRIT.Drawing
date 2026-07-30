using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Brushes;
using DRIT.Drawing.Geometry;

namespace DRIT.Drawing.IntegrationTests
{
    /// <summary>
    /// Integration tests for Region class that produce visual output.
    /// 
    /// Human Tester Instructions:
    /// - Images are saved to the Out folder with descriptive names
    /// - Verify that region combine operations produce expected shapes
    /// - Check that clipping restricts drawing correctly
    /// - Regions should be clearly visible with distinct colors
    /// </summary>
    [TestClass]
    public class RegionIntegrationTests : IntegrationTestBase
    {
        [TestMethod]
        public void Region_Combine_VisibleOutput()
        {
            // Human Tester: Should see a 600x400 image with 4 quadrants showing:
            // - Top-left: Union of two overlapping rectangles (both areas filled)
            // - Top-right: Intersection of two rectangles (only overlap filled)
            // - Bottom-left: Exclusion (first rect minus second)
            // - Bottom-right: Xor (areas in one but not both)
            using var bmp = new Bitmap(600, 400);
            using var g = Graphics.FromImage(bmp);
            using var whiteBrush = new SolidBrush(Color.White);
            using var redBrush = new SolidBrush(Color.Red);
            using var greenBrush = new SolidBrush(Color.Green);
            using var blueBrush = new SolidBrush(Color.Blue);
            using var yellowBrush = new SolidBrush(Color.Yellow);

            // Fill with white background
            g.FillRectangle(whiteBrush, 0, 0, 600, 400);

            var rect1 = new Rectangle(0, 0, 100, 100);
            var rect2 = new Rectangle(50, 50, 100, 100);

            // Union
            using var unionRegion = new Region(rect1);
            unionRegion.Union(rect2);
            g.SetClip(unionRegion);
            g.FillRectangle(redBrush, 20, 20, 150, 150);
            g.ResetClip();

            // Intersection
            using var intersectRegion = new Region(rect1);
            intersectRegion.Intersect(rect2);
            g.SetClip(intersectRegion);
            g.FillRectangle(greenBrush, 220, 20, 150, 150);
            g.ResetClip();

            // Exclude
            using var excludeRegion = new Region(rect1);
            excludeRegion.Exclude(rect2);
            g.SetClip(excludeRegion);
            g.FillRectangle(blueBrush, 20, 220, 150, 150);
            g.ResetClip();

            // Xor
            using var xorRegion = new Region(rect1);
            xorRegion.Xor(rect2);
            g.SetClip(xorRegion);
            g.FillRectangle(yellowBrush, 220, 220, 150, 150);
            g.ResetClip();

            SaveImage(bmp, "Region_Combine_VisibleOutput");
        }
    }
}