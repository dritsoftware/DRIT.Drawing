using System;
using System.IO;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;
using static DRIT.Drawing.Tests.Codecs.CodecTestBuilders;

namespace DRIT.Drawing.Tests.Codecs;

[TestClass]
public sealed class IcoRobustnessTests
{
    private static byte[] SolidPng(int size, Color color)
    {
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            bitmap.SetPixel(x, y, color);
        return Encode(bitmap, ImageFormat.Png);
    }

    private static void AssertThrowsInvalidData(byte[] ico, string context)
    {
        Exception? failure = null;
        try { using Bitmap _ = Decode(ico); }
        catch (Exception ex) { failure = ex; }
        Assert.IsInstanceOfType(failure, typeof(InvalidDataException), context);
    }

    // ── Malformed input fails as InvalidDataException, never IndexOutOfRange ─

    [TestMethod]
    public void Ico_TruncatedHeader_ThrowsInvalidData()
        => AssertThrowsInvalidData(new byte[] { 0, 0, 1, 0, 1 }, "5-byte file");

    [TestMethod]
    public void Ico_DirectoryBeyondTheFile_ThrowsInvalidData()
        => AssertThrowsInvalidData(Concat(IcoHeader(40), IcoEntry(16, 16, 32, 100, 700)), "40 entries, 1 present");

    [TestMethod]
    public void Ico_ImageOffsetOutsideTheFile_ThrowsInvalidData()
        => AssertThrowsInvalidData(Concat(IcoHeader(1), IcoEntry(16, 16, 32, 100, 100000)), "offset 100000");

    [TestMethod]
    public void Ico_BitmapHeaderCutOff_ThrowsInvalidData()
        => AssertThrowsInvalidData(Concat(IcoHeader(1), IcoEntry(16, 16, 32, 10, 22), new byte[10]), "10 header bytes");

    [TestMethod]
    public void Ico_AbsurdBitmapSize_ThrowsInvalidDataWithoutAllocating()
    {
        byte[] dib = IcoDib(1, 1, 32, Array.Empty<byte>(), new byte[4], new byte[4]);
        dib[4] = dib[5] = dib[6] = 0x7F;   // width ~ 2^23, with a single pixel of data
        dib[7] = 0x00;
        long before = GC.GetAllocatedBytesForCurrentThread();
        AssertThrowsInvalidData(Concat(IcoHeader(1), IcoEntry(0, 0, 32, dib.Length, 22), dib), "huge width");
        Assert.IsTrue(GC.GetAllocatedBytesForCurrentThread() - before < 4 * 1024 * 1024);
    }

    // ── Embedded PNG entries ─────────────────────────────────────────────────

    [TestMethod]
    public void Ico_PngEntryThatIsNotStoredLast_Decodes()
    {
        byte[] large = SolidPng(64, Color.FromArgb(255, 255, 0, 0));
        byte[] small = SolidPng(16, Color.FromArgb(255, 0, 0, 255));
        int dataStart = 6 + 2 * 16;
        byte[] ico = Concat(
            IcoHeader(2),
            IcoEntry(64, 64, 32, large.Length, dataStart),
            IcoEntry(16, 16, 32, small.Length, dataStart + large.Length),
            large,
            small);

        foreach (bool strict in new[] { false, true })
        {
            using var scope = strict ? new StrictConformanceScope() : null;
            using Bitmap decoded = Decode(ico);
            Assert.AreEqual(64, decoded.Width);
            Assert.AreEqual(255, decoded.GetPixel(10, 10).R);
            Assert.AreEqual(0, decoded.GetPixel(10, 10).B);
        }
    }

    // ── Indexed and masked bitmaps ───────────────────────────────────────────

    private static byte[] Palette(params (byte r, byte g, byte b)[] colours)
    {
        var bytes = new byte[colours.Length * 4];
        for (int i = 0; i < colours.Length; i++)
        {
            bytes[i * 4] = colours[i].b;
            bytes[i * 4 + 1] = colours[i].g;
            bytes[i * 4 + 2] = colours[i].r;
        }
        return bytes;
    }

    [TestMethod]
    public void Ico_EightBitPaletteIcon_UsesThePalette()
    {
        // 4 x 4, palette index 1 = green everywhere, AND mask all zero (opaque).
        byte[] palette = Palette(Enumerate(256, i => i == 1 ? ((byte)0, (byte)255, (byte)0) : ((byte)i, (byte)0, (byte)0)));
        byte[] xor = new byte[4 * 4];            // stride 4 per row
        for (int i = 0; i < xor.Length; i++) xor[i] = 1;
        byte[] dib = IcoDib(4, 4, 8, palette, xor, new byte[4 * 4]);
        byte[] ico = Concat(IcoHeader(1), IcoEntry(4, 4, 8, dib.Length, 22), dib);

        using Bitmap decoded = Decode(ico);
        Color c = decoded.GetPixel(2, 2);
        Assert.AreEqual(255, c.A);
        Assert.AreEqual(0, c.R);
        Assert.AreEqual(255, c.G);
        Assert.AreEqual(0, c.B);
    }

    [TestMethod]
    public void Ico_FourBitAndOneBitIcons_DecodePackedIndices()
    {
        // 8 x 1 four-bit: indices 0..7 packed two per byte; palette entry i = (i*30, 0, 0).
        byte[] palette4 = Palette(Enumerate(16, i => ((byte)(i * 30), (byte)0, (byte)0)));
        byte[] xor4 = { 0x01, 0x23, 0x45, 0x67 };            // one row, stride 4
        byte[] dib4 = IcoDib(8, 1, 4, palette4, xor4, new byte[4]);
        using (Bitmap decoded = Decode(Concat(IcoHeader(1), IcoEntry(8, 1, 4, dib4.Length, 22), dib4)))
        {
            for (int x = 0; x < 8; x++)
                Assert.AreEqual(x * 30, decoded.GetPixel(x, 0).R, $"4-bit pixel {x}");
        }

        // 8 x 1 one-bit: pattern 10110000, palette 0 = black, 1 = white; mask hides pixel 7.
        byte[] palette1 = Palette(((byte)0, (byte)0, (byte)0), ((byte)255, (byte)255, (byte)255));
        byte[] xor1 = { 0b10110000, 0, 0, 0 };
        byte[] and1 = { 0b00000001, 0, 0, 0 };
        byte[] dib1 = IcoDib(8, 1, 1, palette1, xor1, and1);
        using (Bitmap decoded = Decode(Concat(IcoHeader(1), IcoEntry(8, 1, 1, dib1.Length, 22), dib1)))
        {
            int[] expected = { 255, 0, 255, 255, 0, 0, 0, 0 };
            for (int x = 0; x < 8; x++)
                Assert.AreEqual(expected[x], decoded.GetPixel(x, 0).R, $"1-bit pixel {x}");
            Assert.AreEqual(0, decoded.GetPixel(7, 0).A, "AND mask bit hides the pixel");
            Assert.AreEqual(255, decoded.GetPixel(0, 0).A);
        }
    }

    [TestMethod]
    public void Ico_ThirtyTwoBitIconWithoutAlpha_UsesTheAndMask()
    {
        // X8R8G8B8 pixels (fourth byte 0): visibility comes from the AND mask.
        byte[] xor = { 0, 0, 255, 0, 0, 255, 0, 0 };       // red, green; stride 8
        byte[] and = { 0b01000000, 0, 0, 0 };              // second pixel masked out
        byte[] dib = IcoDib(2, 1, 32, Array.Empty<byte>(), xor, and);
        using Bitmap decoded = Decode(Concat(IcoHeader(1), IcoEntry(2, 1, 32, dib.Length, 22), dib));
        Assert.AreEqual(255, decoded.GetPixel(0, 0).A);
        Assert.AreEqual(255, decoded.GetPixel(0, 0).R);
        Assert.AreEqual(0, decoded.GetPixel(1, 0).A);
    }

    private static (byte r, byte g, byte b)[] Enumerate(int count, Func<int, (byte r, byte g, byte b)> make)
    {
        var result = new (byte r, byte g, byte b)[count];
        for (int i = 0; i < count; i++) result[i] = make(i);
        return result;
    }
}
