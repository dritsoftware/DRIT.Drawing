using DRIT.Drawing.Geometry;

namespace DRIT.Drawing.Tests.Geometry;

[TestClass]
public class RectangleTests
{
    [TestMethod]
    public void Rectangle_Intersect_ReturnsOverlap()
    {
        var r = Rectangle.Intersect(new Rectangle(0, 0, 100, 100), new Rectangle(50, 50, 100, 100));
        Assert.AreEqual(new Rectangle(50, 50, 50, 50), r);
    }

    [TestMethod]
    public void Rectangle_Intersect_NoOverlap_ReturnsEmpty()
    {
        var r = Rectangle.Intersect(new Rectangle(0, 0, 10, 10), new Rectangle(20, 20, 10, 10));
        Assert.IsTrue(r.IsEmpty);
    }

    [TestMethod]
    public void Rectangle_Union_CoversAll()
    {
        var r = Rectangle.Union(new Rectangle(0, 0, 50, 50), new Rectangle(50, 50, 50, 50));
        Assert.AreEqual(new Rectangle(0, 0, 100, 100), r);
    }

    [TestMethod]
    public void Rectangle_Inflate_ExpandsBothAxes()
    {
        var r = Rectangle.Inflate(new Rectangle(10, 10, 20, 20), 5, 5);
        Assert.AreEqual(new Rectangle(5, 5, 30, 30), r);
    }

    [TestMethod]
    public void Rectangle_Contains_Point_InsideReturnsTrue()
    {
        var r = new Rectangle(0, 0, 100, 100);
        Assert.IsTrue(r.Contains(50, 50));
        Assert.IsFalse(r.Contains(100, 100)); // exclusive upper-right
    }

    [TestMethod]
    public void Rectangle_FromLTRB_Correct()
    {
        var r = Rectangle.FromLTRB(10, 20, 60, 80);
        Assert.AreEqual(new Rectangle(10, 20, 50, 60), r);
    }

    [TestMethod]
    public void Rectangle_Offset_Moves()
    {
        var r = new Rectangle(10, 10, 20, 20);
        r.Offset(5, -3);
        Assert.AreEqual(new Rectangle(15, 7, 20, 20), r);
    }
}