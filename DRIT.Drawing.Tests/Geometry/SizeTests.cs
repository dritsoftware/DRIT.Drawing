using DRIT.Drawing.Geometry;

namespace DRIT.Drawing.Tests.Geometry;

[TestClass]
public class SizeTests
{
    [TestMethod]
    public void Size_Add_Works()
    {
        var s = Size.Add(new Size(10, 20), new Size(5, 5));
        Assert.AreEqual(new Size(15, 25), s);
    }
}