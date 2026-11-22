using System;
using System.IO;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;
using static DRIT.Drawing.Tests.Codecs.CodecTestBuilders;

namespace DRIT.Drawing.Tests.Codecs;

[TestClass]
public sealed class GifRobustnessTests
{
    [TestMethod]
    public void Gif_DeferredClearCode_DecodesWithTheFullDictionary()
    {
        // 70 x 70 random indices need well over 4096 dictionary entries, and this stream never clears.
        const int size = 70;
        var random = new Random(1234);
        var indices = new byte[size * size];
        random.NextBytes(indices);

        byte[] gif = GifWithDeferredClear(size, size, indices);

        using Bitmap decoded = Decode(gif);
        Assert.AreEqual(size, decoded.Width);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            byte expected = indices[y * size + x];
            Color c = decoded.GetPixel(x, y);
            Assert.AreEqual(expected, c.R, $"pixel ({x},{y})");
        }

        using var strict = new StrictConformanceScope();
        using Bitmap strictDecoded = Decode(gif);
        AssertSamePixels(decoded, strictDecoded);
    }

    private static byte[] GifWithTransparency()
    {
        using var bitmap = new Bitmap(3, 3, PixelFormat.Format32bppArgb);
        for (int y = 0; y < 3; y++)
        for (int x = 0; x < 3; x++)
            bitmap.SetPixel(x, y, x == 1 && y == 1 ? Color.FromArgb(0, 0, 0, 0) : Color.FromArgb(255, 200, 20 * x, 40 * y));
        return Encode(bitmap, ImageFormat.Gif);
    }

    private static int GceFlagsOffset(byte[] gif)
    {
        for (int i = 13; i + 3 < gif.Length; i++)
        {
            if (gif[i] == 0x21 && gif[i + 1] == 0xF9 && gif[i + 2] == 0x04)
                return i + 3;
        }
        throw new AssertFailedException("No graphic control extension found.");
    }

    [TestMethod]
    public void Gif_UserInputFlag_IsValid()
    {
        byte[] gif = GifWithTransparency();
        using Bitmap expected = Decode(gif);

        byte[] withFlag = (byte[])gif.Clone();
        withFlag[GceFlagsOffset(withFlag)] |= 0x02;   // "user input expected" is a defined flag

        using Bitmap actual = Decode(withFlag);
        AssertSamePixels(expected, actual);
        using var strict = new StrictConformanceScope();
        using Bitmap strictActual = Decode(withFlag);
        AssertSamePixels(expected, strictActual);
    }

    [TestMethod]
    public void Gif_ReservedGceBits_AreIgnoredUnlessStrict()
    {
        byte[] gif = GifWithTransparency();
        using Bitmap expected = Decode(gif);

        byte[] reserved = (byte[])gif.Clone();
        reserved[GceFlagsOffset(reserved)] |= 0x20;

        using Bitmap actual = Decode(reserved);
        AssertSamePixels(expected, actual);
        using var strict = new StrictConformanceScope();
        Assert.ThrowsException<InvalidDataException>(() => Decode(reserved));
    }

    [TestMethod]
    public void Gif_LzwStreamShorterThanTheFrame_KeepsTheDecodedPixelsByDefault()
    {
        // The descriptor says 70 x 70 but the LZW stream only holds the first 3000 pixels.
        const int size = 70, available = 3000;
        var random = new Random(99);
        var indices = new byte[available];
        random.NextBytes(indices);
        byte[] gif = GifWithDeferredClear(size, size, indices);

        using Bitmap decoded = Decode(gif);
        for (int i = 0; i < available; i += 97)
            Assert.AreEqual(indices[i], decoded.GetPixel(i % size, i / size).R, $"pixel {i}");
        Assert.AreEqual(0, decoded.GetPixel(size - 1, size - 1).A, "missing pixels stay transparent");

        using var strict = new StrictConformanceScope();
        Assert.ThrowsException<InvalidDataException>(() => Decode(gif));
    }

    // ── Encoder output must always be readable by this decoder ───────────────

    [TestMethod]
    public void GifEncoder_SolidColourImages_RoundTrip()
    {
        // The end-of-information code used to be written one bit too narrow at certain code counts;
        // a solid 8 x 8 image hit exactly that boundary.
        foreach (int size in new[] { 1, 2, 3, 7, 8, 9, 15, 16, 17, 31, 32, 33, 64 })
        {
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                bitmap.SetPixel(x, y, Color.FromArgb(255, 10, 200, 90));

            byte[] gif = Encode(bitmap, ImageFormat.Gif);
            using Bitmap decoded = Decode(gif);
            AssertSamePixels(bitmap, decoded, $"solid {size}x{size}");

            // The strict decoder requires a correctly sized end-of-information code.
            using var strict = new StrictConformanceScope();
            using Bitmap strictDecoded = Decode(gif);
            AssertSamePixels(bitmap, strictDecoded, $"solid {size}x{size} (strict)");
        }
    }

    [TestMethod]
    public void GifEncoder_RandomImages_RoundTripThroughBothModes()
    {
        var random = new Random(20261122);
        for (int iteration = 0; iteration < 250; iteration++)
        {
            int width = random.Next(1, 40), height = random.Next(1, 40), colours = random.Next(1, 9);
            var palette = new Color[colours];
            for (int i = 0; i < colours; i++)
                palette[i] = Color.FromArgb(255, random.Next(256), random.Next(256), random.Next(256));

            using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            bool noise = random.Next(2) == 0;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                bitmap.SetPixel(x, y, palette[noise ? random.Next(colours) : (x / 3 + y / 2) % colours]);

            byte[] gif = Encode(bitmap, ImageFormat.Gif);
            string context = $"iteration {iteration}: {width}x{height}, {colours} colours, noise={noise}";
            using (Bitmap decoded = Decode(gif))
                AssertSamePixels(bitmap, decoded, context);
            using var strict = new StrictConformanceScope();
            using (Bitmap decoded = Decode(gif))
                AssertSamePixels(bitmap, decoded, context + " (strict)");
        }
    }

    [TestMethod]
    public void GifEncoder_LargeImages_RoundTrip()
    {
        // Enough distinct runs to fill and reset the dictionary several times.
        using Bitmap bitmap = Pattern(160, 120);
        using Bitmap reduced = new Bitmap(160, 120, PixelFormat.Format32bppArgb);
        for (int y = 0; y < 120; y++)
        for (int x = 0; x < 160; x++)
        {
            Color c = bitmap.GetPixel(x, y);
            reduced.SetPixel(x, y, Color.FromArgb(255, c.R & 0xC0, c.G & 0xC0, c.B & 0xC0));   // <= 64 colours
        }

        using Bitmap decoded = Decode(Encode(reduced, ImageFormat.Gif));
        AssertSamePixels(reduced, decoded);
    }
}
