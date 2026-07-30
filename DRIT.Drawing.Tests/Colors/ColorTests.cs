using DRIT.Drawing.Colors;

namespace DRIT.Drawing.Tests.Colors;

[TestClass]
public class ColorTests
{
    [TestMethod]
    public void Color_FromArgb_SetsChannels()
    {
        var c = Color.FromArgb(128, 255, 0, 0);
        Assert.AreEqual(128, c.A);
        Assert.AreEqual(255, c.R);
        Assert.AreEqual(0,   c.G);
        Assert.AreEqual(0,   c.B);
    }

    [TestMethod]
    public void Color_FromKnownColor_Blue()
    {
        var c = Color.FromKnownColor(KnownColor.Blue);
        Assert.AreEqual(255, c.B);
        Assert.AreEqual(0,   c.R);
        Assert.AreEqual(0,   c.G);
        Assert.AreEqual(255, c.A);
        Assert.IsTrue(c.IsKnownColor);
        Assert.IsFalse(c.IsSystemColor);
    }

    [TestMethod]
    public void Color_Empty_IsEmpty()
    {
        Assert.IsTrue(Color.Empty.IsEmpty);
        Assert.IsFalse(Color.Red.IsEmpty);
    }

    [TestMethod]
    public void Color_FromArgb_ToArgb_RoundTrip()
    {
        var c = Color.FromArgb(unchecked((int)0x80FF8040));
        Assert.AreEqual(unchecked((int)0x80FF8040), c.ToArgb());
    }

    [TestMethod]
    public void Color_Static_Red_IsCorrect()
    {
        var c = Color.Red;
        Assert.AreEqual(255, c.R);
        Assert.AreEqual(0,   c.G);
        Assert.AreEqual(0,   c.B);
        Assert.AreEqual(255, c.A);
    }

    [TestMethod]
    public void Color_Static_Transparent_HasZeroAlpha()
    {
        Assert.AreEqual(0, Color.Transparent.A);
    }

    [TestMethod]
    public void Color_GetBrightness_White()
    {
        Assert.AreEqual(1f, Color.White.GetBrightness(), 1e-4f);
    }

    [TestMethod]
    public void Color_GetBrightness_Black()
    {
        Assert.AreEqual(0f, Color.Black.GetBrightness(), 1e-4f);
    }

    [TestMethod]
    public void Color_GetHue_Red()
    {
        Assert.AreEqual(0f, Color.Red.GetHue(), 1e-3f);
    }

    [TestMethod]
    public void Color_GetHue_Green()
    {
        // Pure green (0,128,0) hue = 120°
        Assert.AreEqual(120f, Color.Green.GetHue(), 1e-3f);
    }

    [TestMethod]
    public void Color_Equality_ByARGB()
    {
        var a = Color.FromArgb(255, 255, 0, 0);
        var b = Color.FromArgb(255, 255, 0, 0);
        Assert.AreEqual(a, b);
        Assert.IsTrue(a == b);
    }

    [TestMethod]
    public void Color_Inequality_DifferentAlpha()
    {
        var a = Color.FromArgb(100, 255, 0, 0);
        var b = Color.FromArgb(200, 255, 0, 0);
        Assert.AreNotEqual(a, b);
        Assert.IsTrue(a != b);
    }

    [TestMethod]
    public void Color_FromArgb_InvalidAlpha_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => Color.FromArgb(256, 0, 0, 0));
    }

    [TestMethod]
    public void Color_KnownColorRoundTrip_AndArgbHsbSanity()
    {
        Array knownColors = Enum.GetValues(typeof(KnownColor));
        Assert.IsTrue(knownColors.Length >= 174, $"Expected at least 174 known colors, got {knownColors.Length}.");

        foreach (KnownColor known in knownColors)
        {
            Color c = Color.FromKnownColor(known);
            Assert.IsTrue(c.IsKnownColor, $"Color {known} should be marked as known.");
            Assert.AreEqual(known, c.ToKnownColor(), $"Known color round-trip failed for {known}.");
        }

        Color argb = Color.FromArgb(128, 10, 20, 30);
        Assert.AreEqual(128, argb.A);
        Assert.AreEqual(10, argb.R);
        Assert.AreEqual(20, argb.G);
        Assert.AreEqual(30, argb.B);

        Assert.AreEqual(0f, Color.Red.GetHue(), 1e-3f);
        Assert.AreEqual(1f, Color.Red.GetSaturation(), 1e-3f);
        Assert.AreEqual(0.5f, Color.Red.GetBrightness(), 1e-3f);
    }
}