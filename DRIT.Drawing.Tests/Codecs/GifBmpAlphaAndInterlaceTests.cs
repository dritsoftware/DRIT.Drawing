using System;
using System.IO;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;
using static DRIT.Drawing.Tests.Codecs.CodecTestBuilders;

namespace DRIT.Drawing.Tests.Codecs;

[TestClass]
public sealed class GifBmpAlphaAndInterlaceTests
{
    // ── GIF interlace ────────────────────────────────────────────────────────

    [TestMethod]
    public void Gif_InterlacedFrames_AreReassembledInDisplayOrder()
    {
        // Eight rows stored in pass order: file row k belongs on display row {0,4,2,6,1,3,5,7}[k].
        const int height = 8;
        var stored = new byte[height];
        for (int k = 0; k < height; k++) stored[k] = (byte)(10 * (k + 1));        // stored row k is labelled 10*(k+1)
        byte[] gif = GifWithDeferredClear(1, height, stored, interlaced: true);

        int[] displayRowOfStoredRow = { 0, 4, 2, 6, 1, 3, 5, 7 };
        using Bitmap decoded = Decode(gif);
        for (int k = 0; k < height; k++)
            Assert.AreEqual(stored[k], decoded.GetPixel(0, displayRowOfStoredRow[k]).R, $"stored row {k}");
    }

    [DataTestMethod]
    [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(5)] [DataRow(8)] [DataRow(9)] [DataRow(17)] [DataRow(100)]
    public void Gif_InterlacedRowMap_CoversEveryRowExactlyOnce(int height)
    {
        int[] map = DRIT.Drawing.Codecs.GifCodec.InterlacedRowMap(height);
        var seen = new bool[height];
        foreach (int storedRow in map)
        {
            Assert.IsTrue(storedRow >= 0 && storedRow < height);
            Assert.IsFalse(seen[storedRow], "stored row used twice");
            seen[storedRow] = true;
        }
        Assert.AreEqual(0, map[0], "display row 0 is stored first");
    }

    // ── BMP alpha policy ─────────────────────────────────────────────────────

    private static byte[] Bmp32(params byte[][] pixelsBgra)
    {
        byte[] data = Concat(pixelsBgra);
        var file = new byte[54 + data.Length];
        file[0] = (byte)'B'; file[1] = (byte)'M';
        BitConverter.GetBytes(file.Length).CopyTo(file, 2);
        BitConverter.GetBytes(54).CopyTo(file, 10);
        BitConverter.GetBytes(40).CopyTo(file, 14);
        BitConverter.GetBytes(pixelsBgra.Length).CopyTo(file, 18);   // width
        BitConverter.GetBytes(1).CopyTo(file, 22);                   // height
        BitConverter.GetBytes((short)1).CopyTo(file, 26);
        BitConverter.GetBytes((short)32).CopyTo(file, 28);
        data.CopyTo(file, 54);
        return file;
    }

    [TestMethod]
    public void Bmp_ThirtyTwoBitWithUnusedFourthByte_IsOpaque()
    {
        // X8R8G8B8: the fourth byte is padding (zero) and must not make the picture invisible.
        byte[] bmp = Bmp32(new byte[] { 0, 0, 255, 0 }, new byte[] { 0, 255, 0, 0 });
        using Bitmap decoded = Decode(bmp);
        Assert.AreEqual(255, decoded.GetPixel(0, 0).A);
        Assert.AreEqual(255, decoded.GetPixel(0, 0).R);
        Assert.AreEqual(255, decoded.GetPixel(1, 0).A);
        Assert.AreEqual(255, decoded.GetPixel(1, 0).G);
    }

    [TestMethod]
    public void Bmp_ThirtyTwoBitWithRealAlpha_KeepsIt()
    {
        byte[] bmp = Bmp32(new byte[] { 0, 0, 255, 128 }, new byte[] { 0, 255, 0, 0 });
        using Bitmap decoded = Decode(bmp);
        Assert.AreEqual(128, decoded.GetPixel(0, 0).A);
        Assert.AreEqual(0, decoded.GetPixel(1, 0).A, "a transparent pixel next to a translucent one stays transparent");
    }

    [TestMethod]
    public void Bmp_AlphaWrittenByTheEncoder_RoundTrips()
    {
        using var source = new Bitmap(3, 1, PixelFormat.Format32bppArgb);
        source.SetPixel(0, 0, Color.FromArgb(200, 10, 20, 30));
        source.SetPixel(1, 0, Color.FromArgb(0, 40, 50, 60));
        source.SetPixel(2, 0, Color.FromArgb(255, 70, 80, 90));
        using Bitmap decoded = Decode(Encode(source, ImageFormat.Bmp));
        AssertSamePixels(source, decoded);
    }
}
