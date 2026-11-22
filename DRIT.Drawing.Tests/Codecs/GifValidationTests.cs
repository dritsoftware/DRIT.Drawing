using System;
using System.IO;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Tests.Codecs;

[TestClass]
public sealed class GifValidationTests
{
    [TestMethod]
    public void Gif_MissingTrailer_ThrowsInvalidDataWhenStrict()
    {
        byte[] bytes = MakeGif();
        Array.Resize(ref bytes, bytes.Length - 1);

        using var strict = new StrictConformanceScope();
        Assert.ThrowsException<InvalidDataException>(() => Decode(bytes));
    }

    [TestMethod]
    public void Gif_MissingTrailer_DecodesByDefault()
    {
        byte[] complete = MakeGif();
        byte[] truncated = (byte[])complete.Clone();
        Array.Resize(ref truncated, truncated.Length - 1);

        using Bitmap expected = Decode(complete);
        using Bitmap actual = Decode(truncated);
        CodecTestBuilders.AssertSamePixels(expected, actual);
    }

    [TestMethod]
    public void Gif_ZeroLogicalScreen_ThrowsInvalidDataWhenStrict()
    {
        byte[] bytes = MakeGif();
        bytes[6] = 0;
        bytes[7] = 0;

        using var strict = new StrictConformanceScope();
        Assert.ThrowsException<InvalidDataException>(() => Decode(bytes));
    }

    [TestMethod]
    public void Gif_ZeroLogicalScreen_TakesItsSizeFromTheFramesByDefault()
    {
        byte[] complete = MakeGif();
        byte[] zeroScreen = (byte[])complete.Clone();
        zeroScreen[6] = 0; zeroScreen[7] = 0; zeroScreen[8] = 0; zeroScreen[9] = 0;

        using Bitmap expected = Decode(complete);
        using Bitmap actual = Decode(zeroScreen);
        CodecTestBuilders.AssertSamePixels(expected, actual);
    }

    [TestMethod]
    public void Gif_FrameWiderThanTheLogicalScreen_IsCroppedByDefault()
    {
        byte[] complete = MakeGif();      // 2x2 frame
        byte[] narrow = (byte[])complete.Clone();
        narrow[6] = 1;                    // logical screen 1 pixel wide, 2 high

        using Bitmap expected = Decode(complete);
        using Bitmap actual = Decode(narrow);
        Assert.AreEqual(1, actual.Width);
        Assert.AreEqual(2, actual.Height);
        for (int y = 0; y < 2; y++)
            Assert.AreEqual(expected.GetPixel(0, y).ToArgb(), actual.GetPixel(0, y).ToArgb());

        using var strict = new StrictConformanceScope();
        Assert.ThrowsException<InvalidDataException>(() => Decode(narrow));
    }

    [TestMethod]
    public void Gif_TruncatedImageSubBlock_ThrowsInvalidData()
    {
        byte[] bytes = MakeGif();
        int imageDescriptor = Array.IndexOf(bytes, (byte)0x2C);
        Assert.IsTrue(imageDescriptor >= 0);
        int subBlockSize = imageDescriptor + 10 + 1;
        bytes[subBlockSize] = 0xFF;

        Assert.ThrowsException<InvalidDataException>(() => Decode(bytes));
    }

    private static Bitmap Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return new Bitmap(stream);
    }

    private static byte[] MakeGif()
    {
        using var source = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
        source.SetPixel(0, 0, Color.Red);
        source.SetPixel(1, 0, Color.Green);
        source.SetPixel(0, 1, Color.Blue);
        source.SetPixel(1, 1, Color.White);
        using var stream = new MemoryStream();
        source.Save(stream, ImageFormat.Gif);
        return stream.ToArray();
    }
}
