using DRIT.Drawing.Geometry;

namespace DRIT.Drawing.Tests.Geometry;

[TestClass]
public class RectangleFTests
{
    [TestMethod]
    public void RectangleF_Intersect_ReturnsOverlap()
    {
        var r = RectangleF.Intersect(new RectangleF(0, 0, 100, 100), new RectangleF(50, 50, 100, 100));
        Assert.AreEqual(new RectangleF(50, 50, 50, 50), r);
    }

    [TestMethod]
    public void RectangleF_Round_RoundsEachComponent()
    {
        // Avoid midpoint values – use clear round-down and round-up cases.
        var rf = new RectangleF(1.4f, 2.6f, 3.3f, 4.7f);
        var r  = RectangleF.Round(rf);
        Assert.AreEqual(new Rectangle(1, 3, 3, 5), r);
    }
}