using System;
using System.IO;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;
using static DRIT.Drawing.Tests.Codecs.CodecTestBuilders;

namespace DRIT.Drawing.Tests.Codecs;

[TestClass]
public sealed class PngColorTests
{
    private static byte[] Be16(params int[] values)
    {
        var bytes = new byte[values.Length * 2];
        for (int i = 0; i < values.Length; i++)
        {
            bytes[i * 2] = (byte)(values[i] >> 8);
            bytes[i * 2 + 1] = (byte)values[i];
        }
        return bytes;
    }

    private static byte[] Row(params byte[][] parts) => Concat(new byte[] { 0 }, Concat(parts));

    private static void AssertAlphas(byte[] png, params int[] expected)
    {
        using Bitmap decoded = Decode(png);
        for (int x = 0; x < expected.Length; x++)
            Assert.AreEqual(expected[x], decoded.GetPixel(x, 0).A, $"alpha of pixel {x}");
    }

    // ── tRNS colour keys are compared with the sample at its native depth ────

    [TestMethod]
    public void Png_SixteenBitRgbColourKey_MatchesTheFullSample()
    {
        byte[] trns = PngChunk("tRNS", Be16(0x0012, 0x0034, 0x0056));
        byte[] png = Png(3, 1, 16, 2,
            Row(Be16(0x0012, 0x0034, 0x0056),    // equals the key
                Be16(0x1200, 0x3400, 0x5600),    // same high bytes, different samples
                Be16(0x0012, 0x0034, 0x0057)),   // differs in the low byte only
            trns);

        AssertAlphas(png, 0, 255, 255);
    }

    [TestMethod]
    public void Png_EightBitRgbColourKey_Matches()
    {
        byte[] trns = PngChunk("tRNS", Be16(7, 8, 9));
        byte[] png = Png(2, 1, 8, 2, Row(new byte[] { 7, 8, 9 }, new byte[] { 7, 8, 10 }), trns);

        AssertAlphas(png, 0, 255);
    }

    [TestMethod]
    public void Png_FourBitGreyColourKey_UsesTheRawSampleNotTheScaledOne()
    {
        // Samples 0, 5, 10, 15 with key 5: only the second pixel is transparent.
        byte[] trns = PngChunk("tRNS", Be16(5));
        byte[] png = Png(4, 1, 4, 0, Row(new byte[] { 0x05, 0xAF }), trns);

        AssertAlphas(png, 255, 0, 255, 255);
    }

    [TestMethod]
    public void Png_TwoBitAndOneBitGreyColourKeys_Match()
    {
        // 2-bit samples 0,1,2,3 (0x1B) with key 2.
        AssertAlphas(Png(4, 1, 2, 0, Row(new byte[] { 0x1B }), PngChunk("tRNS", Be16(2))), 255, 255, 0, 255);
        // 1-bit samples 1,0,1,1 (0xB0) with key 0.
        AssertAlphas(Png(4, 1, 1, 0, Row(new byte[] { 0xB0 }), PngChunk("tRNS", Be16(0))), 255, 0, 255, 255);
    }

    [TestMethod]
    public void Png_SixteenBitGreyColourKey_MatchesTheFullSample()
    {
        byte[] trns = PngChunk("tRNS", Be16(0x1234));
        byte[] png = Png(3, 1, 16, 0, Row(Be16(0x1234), Be16(0x12FF), Be16(0x3412)), trns);

        AssertAlphas(png, 0, 255, 255);
    }

    // ── APNG "blend over" uses straight-alpha compositing ────────────────────

    private static byte[] ApngFrameControl(uint sequence, int width, int height, byte blend)
    {
        var data = new byte[26];
        WriteUInt32BE(data, 0, sequence);
        WriteUInt32BE(data, 4, (uint)width);
        WriteUInt32BE(data, 8, (uint)height);
        data[20] = 0; data[21] = 1;      // delay 1/10 s
        data[22] = 0; data[23] = 10;
        data[24] = 0;                    // dispose: none
        data[25] = blend;
        return PngChunk("fcTL", data);
    }

    private static byte[] Rgba(byte r, byte g, byte b, byte a) => Concat(new byte[] { 0 }, new[] { r, g, b, a });

    private static byte[] Apng(params byte[][] chunksAfterIhdr)
        => Concat(PngSignature, PngIhdr(1, 1, 8, 6), Concat(chunksAfterIhdr), PngChunk("IEND", Array.Empty<byte>()));

    private static byte[] FrameData(uint sequence, byte[] filteredRows)
    {
        byte[] compressed = DRIT.Drawing.Codecs.ZlibHelper.Compress(filteredRows);
        var fdat = new byte[4 + compressed.Length];
        WriteUInt32BE(fdat, 0, sequence);
        Buffer.BlockCopy(compressed, 0, fdat, 4, compressed.Length);
        return PngChunk("fdAT", fdat);
    }

    private static byte[] ActlOneFrame(int frames)
    {
        var data = new byte[8];
        WriteUInt32BE(data, 0, (uint)frames);
        return PngChunk("acTL", data);
    }

    [TestMethod]
    public void Apng_BlendOverOntoATransparentCanvas_KeepsTheStraightColour()
    {
        byte[] png = Apng(
            ActlOneFrame(1),
            ApngFrameControl(0, 1, 1, blend: 1),
            PngChunk("IDAT", DRIT.Drawing.Codecs.ZlibHelper.Compress(Rgba(255, 0, 0, 128))));

        using Bitmap bitmap = Decode(png);
        bitmap.SelectActiveFrame(FrameDimension.Time, 0);
        Color c = bitmap.GetPixel(0, 0);
        Assert.AreEqual(128, c.A);
        Assert.AreEqual(255, c.R, "colour must not be darkened by the alpha");
        Assert.AreEqual(0, c.G);
        Assert.AreEqual(0, c.B);
    }

    [TestMethod]
    public void Apng_BlendOverOntoAnOpaqueFrame_MixesTheColours()
    {
        byte[] png = Apng(
            ActlOneFrame(2),
            ApngFrameControl(0, 1, 1, blend: 0),
            PngChunk("IDAT", DRIT.Drawing.Codecs.ZlibHelper.Compress(Rgba(0, 0, 255, 255))),
            ApngFrameControl(1, 1, 1, blend: 1),
            FrameData(2, Rgba(255, 0, 0, 128)));

        using Bitmap bitmap = Decode(png);
        bitmap.SelectActiveFrame(FrameDimension.Time, 1);
        Color c = bitmap.GetPixel(0, 0);
        Assert.AreEqual(255, c.A);
        Assert.IsTrue(Math.Abs(c.R - 128) <= 1, $"R={c.R}");
        Assert.IsTrue(Math.Abs(c.B - 127) <= 1, $"B={c.B}");
    }

    [TestMethod]
    public void Apng_BlendOverOntoAPartlyTransparentPixel_WeightsTheBackdropByItsAlpha()
    {
        // backdrop (0,0,255) a=128, source (255,0,0) a=128: outA = 128 + 128*127/255 = 192;
        // colour = (255*128, 0, 255*128*127/255) / 192.
        byte[] png = Apng(
            ActlOneFrame(2),
            ApngFrameControl(0, 1, 1, blend: 0),
            PngChunk("IDAT", DRIT.Drawing.Codecs.ZlibHelper.Compress(Rgba(0, 0, 255, 128))),
            ApngFrameControl(1, 1, 1, blend: 1),
            FrameData(2, Rgba(255, 0, 0, 128)));

        using Bitmap bitmap = Decode(png);
        bitmap.SelectActiveFrame(FrameDimension.Time, 1);
        Color c = bitmap.GetPixel(0, 0);
        Assert.IsTrue(Math.Abs(c.A - 192) <= 1, $"A={c.A}");
        Assert.IsTrue(Math.Abs(c.R - 170) <= 2, $"R={c.R}");
        Assert.IsTrue(Math.Abs(c.B - 85) <= 2, $"B={c.B}");
    }
}
