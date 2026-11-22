using System;
using DRIT.Drawing.Codecs.Jpeg;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;
using static DRIT.Drawing.Tests.Codecs.CodecTestBuilders;

namespace DRIT.Drawing.Tests.Codecs.Jpeg;

[TestClass]
public sealed class JpegColorTests
{
    // ── Adobe CMYK / YCCK ────────────────────────────────────────────────────

    [TestMethod]
    public void ColorConverter_AdobeCmyk_StoredInvertedValuesMultiply()
    {
        ColorConverter.AdobeCmykToRgb(255, 255, 255, 255, out byte r, out byte g, out byte b);
        Assert.AreEqual((255, 255, 255), ((int)r, (int)g, (int)b), "no ink is white");

        ColorConverter.AdobeCmykToRgb(255, 255, 255, 0, out r, out g, out b);
        Assert.AreEqual((0, 0, 0), ((int)r, (int)g, (int)b), "full black ink is black");

        ColorConverter.AdobeCmykToRgb(0, 255, 255, 255, out r, out g, out b);
        Assert.AreEqual((0, 255, 255), ((int)r, (int)g, (int)b), "full cyan removes red");
    }

    [TestMethod]
    public void ColorConverter_Ycck_RecoversTheStoredValues()
    {
        // YCC (0,128,128) is RGB (0,0,0): the stored (inverted) CMY are 255, i.e. no ink; K = 255 is no black.
        ColorConverter.YcckToRgb(0, 128, 128, 255, out byte r, out byte g, out byte b);
        Assert.AreEqual((255, 255, 255), ((int)r, (int)g, (int)b), "no ink is white");

        ColorConverter.YcckToRgb(0, 128, 128, 0, out r, out g, out b);
        Assert.AreEqual((0, 0, 0), ((int)r, (int)g, (int)b), "black ink is black");

        // YCC (255,128,128) is RGB (255,255,255): stored CMY are 0, i.e. full ink on every channel.
        ColorConverter.YcckToRgb(255, 128, 128, 255, out r, out g, out b);
        Assert.AreEqual((0, 0, 0), ((int)r, (int)g, (int)b), "full CMY ink is black");
    }

    // ── Three-component RGB (Adobe transform 0) ──────────────────────────────

    private static byte[] AdobeSegment(byte transform)
    {
        var payload = new byte[12];
        byte[] name = Ascii("Adobe");
        Buffer.BlockCopy(name, 0, payload, 0, name.Length);
        payload[11] = transform;
        return JpegSegment(0xEE, payload);
    }

    private static byte[] SolidJpeg(Color color)
    {
        using var bitmap = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
            bitmap.SetPixel(x, y, color);
        return Encode(bitmap, ImageFormat.Jpeg);
    }

    [TestMethod]
    public void Jpeg_ThreeComponentsWithAdobeTransformZero_AreNotRunThroughYCbCr()
    {
        // The encoder stores Y, Cb, Cr = (124, 86, 182) for (200,100,50). With an Adobe marker saying "no
        // transform" those numbers *are* the red, green and blue samples.
        byte[] jpeg = SolidJpeg(Color.FromArgb(255, 200, 100, 50));

        using (Bitmap asYcc = Decode(jpeg))
        {
            Color c = asYcc.GetPixel(8, 8);
            Assert.IsTrue(Math.Abs(c.R - 200) <= 4 && Math.Abs(c.G - 100) <= 4 && Math.Abs(c.B - 50) <= 4, "without the marker: YCbCr");
        }

        using Bitmap asRgb = Decode(Insert(jpeg, 2, AdobeSegment(0)));
        Color rgb = asRgb.GetPixel(8, 8);
        Assert.IsTrue(Math.Abs(rgb.R - 124) <= 4, $"R={rgb.R}");
        Assert.IsTrue(Math.Abs(rgb.G - 86) <= 4, $"G={rgb.G}");
        Assert.IsTrue(Math.Abs(rgb.B - 182) <= 4, $"B={rgb.B}");

        using Bitmap transformOne = Decode(Insert(jpeg, 2, AdobeSegment(1)));
        Color ycc = transformOne.GetPixel(8, 8);
        Assert.IsTrue(Math.Abs(ycc.R - 200) <= 4 && Math.Abs(ycc.G - 100) <= 4 && Math.Abs(ycc.B - 50) <= 4, "Adobe transform 1 is YCbCr");
    }

    // ── 12-bit data is refused, not clipped ──────────────────────────────────

    [TestMethod]
    public void Jpeg_TwelveBitPrecision_IsNotSupported()
    {
        byte[] jpeg = SolidJpeg(Color.FromArgb(255, 10, 20, 30));
        int sof = FindMarker(jpeg, 0xC0);
        Assert.AreEqual(8, jpeg[sof + 4], "precision byte");
        jpeg[sof + 4] = 12;

        Assert.ThrowsException<NotSupportedException>(() => Decode(jpeg));
    }

}
