using DRIT.Drawing.Geometry;

namespace DRIT.Drawing.Tests.Geometry;

[TestClass]
public class SizeFTests
{
    [TestMethod]
    public void SizeF_ToSize_Truncates()
    {
        var sf = new SizeF(3.9f, 4.1f);
        var s  = sf.ToSize();
        Assert.AreEqual(new Size(3, 4), s);
    }
}