using System;
using System.IO;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Tests.Codecs;

[TestClass]
public sealed class BmpValidationTests
{
    [TestMethod]
    public void Bmp_TruncatedFile_ThrowsInvalidData()
    {
        byte[] bytes = MakeBmp();
        using var stream = new MemoryStream(bytes, 0, 20, writable: false);
        Assert.ThrowsException<InvalidDataException>(() => new Bitmap(stream));
    }

    [TestMethod]
    public void Bmp_InvalidPlanes_ThrowsInvalidData()
    {
        byte[] bytes = MakeBmp();
        bytes[26] = 2;
        bytes[27] = 0;

        Assert.ThrowsException<InvalidDataException>(() => Decode(bytes));
    }

    [TestMethod]
    public void Bmp_ExcessiveDimensions_ThrowsInvalidData()
    {
        byte[] bytes = MakeBmp();
        WriteInt32LE(bytes, 18, 100_000);
        WriteInt32LE(bytes, 22, 100_000);

        Assert.ThrowsException<InvalidDataException>(() => Decode(bytes));
    }

    [TestMethod]
    public void Bmp_DeclaredFileSizeLargerThanTheFile_DecodesUnlessStrict()
    {
        byte[] complete = MakeBmp();
        byte[] wrongSize = (byte[])complete.Clone();
        WriteInt32LE(wrongSize, 2, complete.Length + 1000);

        using Bitmap expected = Decode(complete);
        using Bitmap actual = Decode(wrongSize);
        CodecTestBuilders.AssertSamePixels(expected, actual);

        using var strict = new StrictConformanceScope();
        Assert.ThrowsException<InvalidDataException>(() => Decode(wrongSize));
    }

    private static Bitmap Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return new Bitmap(stream);
    }

    private static byte[] MakeBmp()
    {
        using var source = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
        source.SetPixel(0, 0, Color.Red);
        source.SetPixel(1, 0, Color.Green);
        source.SetPixel(0, 1, Color.Blue);
        source.SetPixel(1, 1, Color.White);
        using var stream = new MemoryStream();
        source.Save(stream, ImageFormat.Bmp);
        return stream.ToArray();
    }

    private static void WriteInt32LE(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }
}
