using DRIT.Drawing.Geometry;

namespace DRIT.Drawing.Tests.Geometry;

[TestClass]
public class PointTests
{
    [TestMethod]
    public void Point_Offset_Works()
    {
        var p = new Point(10, 20);
        p.Offset(5, -3);
        Assert.AreEqual(new Point(15, 17), p);
    }

    [TestMethod]
    public void Point_ImplicitCast_ToPointF()
    {
        PointF pf = new Point(3, 4);
        Assert.AreEqual(3f, pf.X);
        Assert.AreEqual(4f, pf.Y);
    }

    [TestMethod]
    public void Point_AddSize_Works()
    {
        var p = new Point(1, 2) + new Size(3, 4);
        Assert.AreEqual(new Point(4, 6), p);
    }
}