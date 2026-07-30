using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Rendering;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Geometry;

namespace DRIT.Drawing.Tests.Rendering
{
    [TestClass]
    public class BrushSamplerTests
    {
        [TestMethod]
        public void BrushSampler_LinearGradient_MidpointIsInterpolated()
        {
            using var brush = new LinearGradientBrush(
                new PointF(0, 0),
                new PointF(10, 0),
                Color.Black,
                Color.White);

            Color mid = BrushSampler.Sample(brush, 5f, 0f, InterpolationMode.NearestNeighbor);
            Assert.IsTrue(mid.R >= 120 && mid.R <= 136);
            Assert.IsTrue(mid.G >= 120 && mid.G <= 136);
            Assert.IsTrue(mid.B >= 120 && mid.B <= 136);
        }
    }
}