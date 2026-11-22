using System;
using System.Collections.Generic;
using System.IO;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;
using static DRIT.Drawing.Tests.Codecs.CodecTestBuilders;

namespace DRIT.Drawing.Tests.Codecs;

/// <summary>
/// The TIFF decoder understands uncompressed 8-bit RGB/RGBA strips. Everything else must be refused with a
/// precise exception instead of being decoded as garbage, and malformed offsets must never index out of range.
/// </summary>
[TestClass]
public sealed class TiffCapabilityTests
{
    // ── A little-endian TIFF with a known layout, patched per test ───────────

    private static byte[] BuildTiff(int width, int height, int spp, byte[] pixels, int strips = 1, ushort extraSamples = 2)
    {
        var tags = new List<(ushort tag, ushort type, uint count, uint value)>();
        int n = spp == 4 ? 15 : 14;
        int ifdSize = 2 + 12 * n + 4;
        int bpsOff = 8 + ifdSize;
        int next = bpsOff + spp * 2;
        int stripOffsetsOff = 0, stripCountsOff = 0;
        if (strips > 1)
        {
            stripOffsetsOff = next; next += strips * 4;
            stripCountsOff = next; next += strips * 4;
        }
        int xResOff = next; next += 8;
        int yResOff = next; next += 8;
        int pixelOff = next;

        int rowsPerStrip = (height + strips - 1) / strips;
        tags.Add((256, 4, 1, (uint)width));
        tags.Add((257, 4, 1, (uint)height));
        tags.Add((258, 3, (uint)spp, (uint)bpsOff));
        tags.Add((259, 3, 1, 1));
        tags.Add((262, 3, 1, 2));
        tags.Add((273, 4, (uint)strips, strips > 1 ? (uint)stripOffsetsOff : (uint)pixelOff));
        tags.Add((277, 3, 1, (uint)spp));
        tags.Add((278, 4, 1, (uint)rowsPerStrip));
        tags.Add((279, 4, (uint)strips, strips > 1 ? (uint)stripCountsOff : (uint)pixels.Length));
        tags.Add((282, 5, 1, (uint)xResOff));
        tags.Add((283, 5, 1, (uint)yResOff));
        tags.Add((284, 3, 1, 1));
        tags.Add((296, 3, 1, 2));
        if (spp == 4) tags.Add((338, 3, 1, extraSamples));
        tags.Add((339, 3, 1, 1));

        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        w.Write((byte)'I'); w.Write((byte)'I'); w.Write((ushort)42); w.Write((uint)8);
        w.Write((ushort)tags.Count);
        foreach (var (tag, type, count, value) in tags)
        {
            w.Write(tag); w.Write(type); w.Write(count); w.Write(value);
        }
        w.Write((uint)0);
        for (int i = 0; i < spp; i++) w.Write((ushort)8);
        if (strips > 1)
        {
            int bytesPerRow = width * spp;
            for (int s = 0; s < strips; s++) w.Write((uint)(pixelOff + s * rowsPerStrip * bytesPerRow));
            for (int s = 0; s < strips; s++)
            {
                int rows = Math.Min(rowsPerStrip, height - s * rowsPerStrip);
                w.Write((uint)(rows * bytesPerRow));
            }
        }
        w.Write((uint)72); w.Write((uint)1);
        w.Write((uint)72); w.Write((uint)1);
        w.Write(pixels);
        return stream.ToArray();
    }

    private static byte[] Gradient(int width, int height, int spp)
    {
        var pixels = new byte[width * height * spp];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = (byte)(i * 7 + 3);
        return pixels;
    }

    /// <summary>Rewrites an existing IFD entry in place.</summary>
    private static void SetEntry(byte[] tiff, ushort tag, ushort type, uint count, uint value)
    {
        int entries = tiff[8] | (tiff[9] << 8);
        for (int i = 0; i < entries; i++)
        {
            int e = 10 + i * 12;
            if ((ushort)(tiff[e] | (tiff[e + 1] << 8)) != tag)
                continue;
            BitConverter.GetBytes(type).CopyTo(tiff, e + 2);
            BitConverter.GetBytes(count).CopyTo(tiff, e + 4);
            BitConverter.GetBytes(value).CopyTo(tiff, e + 8);
            return;
        }
        throw new AssertFailedException($"Tag {tag} not found.");
    }

    private static void RenameEntry(byte[] tiff, ushort tag, ushort newTag)
    {
        int entries = tiff[8] | (tiff[9] << 8);
        for (int i = 0; i < entries; i++)
        {
            int e = 10 + i * 12;
            if ((ushort)(tiff[e] | (tiff[e + 1] << 8)) != tag)
                continue;
            BitConverter.GetBytes(newTag).CopyTo(tiff, e);
            return;
        }
        throw new AssertFailedException($"Tag {tag} not found.");
    }

    private static void AssertThrows<TException>(byte[] tiff, string context) where TException : Exception
    {
        Exception? failure = null;
        try { using Bitmap _ = Decode(tiff); }
        catch (Exception ex) { failure = ex; }
        Assert.IsNotNull(failure, context + ": expected an exception");
        Assert.IsInstanceOfType(failure, typeof(TException), $"{context}: {failure}");
    }

    // ── Baseline ─────────────────────────────────────────────────────────────

    [TestMethod]
    public void Tiff_UncompressedRgb_Decodes()
    {
        byte[] pixels = Gradient(5, 4, 3);
        using Bitmap decoded = Decode(BuildTiff(5, 4, 3, pixels));
        Assert.AreEqual(5, decoded.Width);
        Color c = decoded.GetPixel(2, 1);
        int o = (1 * 5 + 2) * 3;
        Assert.AreEqual(pixels[o], c.R);
        Assert.AreEqual(pixels[o + 1], c.G);
        Assert.AreEqual(pixels[o + 2], c.B);
        Assert.AreEqual(255, c.A);
    }

    [TestMethod]
    public void Tiff_MultipleStrips_DecodeLikeASingleStrip()
    {
        byte[] pixels = Gradient(6, 9, 3);
        using Bitmap single = Decode(BuildTiff(6, 9, 3, pixels, strips: 1));
        using Bitmap multi = Decode(BuildTiff(6, 9, 3, pixels, strips: 4));
        AssertSamePixels(single, multi);
    }

    [TestMethod]
    public void Tiff_EncoderOutput_RoundTripsAndKeepsResolution()
    {
        using Bitmap source = Pattern(7, 5);
        source._dpiX = 300;
        source._dpiY = 200;
        using Bitmap decoded = Decode(Encode(source, ImageFormat.Tiff));
        AssertSamePixels(source, decoded);
        Assert.AreEqual(300f, decoded.HorizontalResolution);
        Assert.AreEqual(200f, decoded.VerticalResolution);
    }

    // ── Layouts the decoder cannot interpret are refused ─────────────────────

    [TestMethod]
    public void Tiff_UnsupportedLayouts_ThrowNotSupported()
    {
        byte[] pixels = Gradient(4, 4, 3);
        byte[] baseline = BuildTiff(4, 4, 3, pixels);

        byte[] compressed = (byte[])baseline.Clone(); SetEntry(compressed, 259, 3, 1, 5);
        AssertThrows<NotSupportedException>(compressed, "LZW compression");

        byte[] cmyk = (byte[])baseline.Clone(); SetEntry(cmyk, 262, 3, 1, 5);
        AssertThrows<NotSupportedException>(cmyk, "CMYK photometric");

        byte[] grey = (byte[])baseline.Clone(); SetEntry(grey, 262, 3, 1, 1);
        AssertThrows<NotSupportedException>(grey, "grey photometric");

        byte[] planar = (byte[])baseline.Clone(); SetEntry(planar, 284, 3, 1, 2);
        AssertThrows<NotSupportedException>(planar, "planar configuration");

        byte[] floats = (byte[])baseline.Clone(); SetEntry(floats, 339, 3, 1, 3);
        AssertThrows<NotSupportedException>(floats, "floating-point samples");

        byte[] tiled = (byte[])baseline.Clone(); RenameEntry(tiled, 278, 324);
        AssertThrows<NotSupportedException>(tiled, "tiles");

        byte[] oneSample = (byte[])baseline.Clone(); SetEntry(oneSample, 277, 3, 1, 1);
        AssertThrows<NotSupportedException>(oneSample, "one sample per pixel");

        // 16-bit samples: BitsPerSample = {16,16,16} (the array lives right after the IFD).
        byte[] deep = (byte[])baseline.Clone();
        int bpsOff = 8 + 2 + 12 * 14 + 4;
        for (int i = 0; i < 3; i++) BitConverter.GetBytes((ushort)16).CopyTo(deep, bpsOff + i * 2);
        AssertThrows<NotSupportedException>(deep, "16-bit samples");
    }

    // ── Malformed files fail as InvalidDataException ─────────────────────────

    [TestMethod]
    public void Tiff_MalformedFiles_ThrowInvalidData()
    {
        byte[] pixels = Gradient(4, 4, 3);
        byte[] baseline = BuildTiff(4, 4, 3, pixels);

        byte[] noOffsets = (byte[])baseline.Clone(); RenameEntry(noOffsets, 273, 60000);
        AssertThrows<InvalidDataException>(noOffsets, "StripOffsets missing");

        byte[] noWidth = (byte[])baseline.Clone(); RenameEntry(noWidth, 256, 60000);
        AssertThrows<InvalidDataException>(noWidth, "ImageWidth missing");

        byte[] noPhotometric = (byte[])baseline.Clone(); RenameEntry(noPhotometric, 262, 60000);
        AssertThrows<InvalidDataException>(noPhotometric, "Photometric missing");

        byte[] outsideStrip = (byte[])baseline.Clone(); SetEntry(outsideStrip, 273, 4, 1, 1_000_000);
        AssertThrows<InvalidDataException>(outsideStrip, "strip beyond the file");

        byte[] shortStrip = (byte[])baseline.Clone(); SetEntry(shortStrip, 279, 4, 1, 10);
        AssertThrows<InvalidDataException>(shortStrip, "strip shorter than the image");

        byte[] hugeStripCount = (byte[])baseline.Clone(); SetEntry(hugeStripCount, 273, 4, 0x7FFFFFFF, 100);
        AssertThrows<InvalidDataException>(hugeStripCount, "billions of strips");

        byte[] bpsOutside = (byte[])baseline.Clone(); SetEntry(bpsOutside, 258, 3, 3, 0xFFFFFFF0);
        AssertThrows<InvalidDataException>(bpsOutside, "BitsPerSample offset beyond the file");

        byte[] hugeDimensions = (byte[])baseline.Clone(); SetEntry(hugeDimensions, 256, 4, 1, 0x7FFFFFFF);
        AssertThrows<InvalidDataException>(hugeDimensions, "enormous width");

        byte[] ifdOutside = (byte[])baseline.Clone(); BitConverter.GetBytes(0xFFFFFF00u).CopyTo(ifdOutside, 4);
        AssertThrows<InvalidDataException>(ifdOutside, "IFD offset beyond the file");

        for (int length = 8; length < baseline.Length; length += 9)
        {
            byte[] truncated = new byte[length];
            Buffer.BlockCopy(baseline, 0, truncated, 0, length);
            Exception? failure = null;
            try { using Bitmap _ = Decode(truncated); }
            catch (Exception ex) { failure = ex; }
            Assert.IsNotNull(failure, $"truncated to {length}");
            Assert.IsInstanceOfType(failure, typeof(InvalidDataException), $"truncated to {length}: {failure}");
        }
    }

    // ── Alpha semantics ──────────────────────────────────────────────────────

    [TestMethod]
    public void Tiff_ExtraSamples_SelectsHowTheFourthChannelIsRead()
    {
        byte[] pixels = { 100, 50, 25, 128 };

        using (Bitmap straight = Decode(BuildTiff(1, 1, 4, pixels, extraSamples: 2)))
        {
            Color c = straight.GetPixel(0, 0);
            Assert.AreEqual(128, c.A);
            Assert.AreEqual(100, c.R);
            Assert.AreEqual(50, c.G);
            Assert.AreEqual(25, c.B);
        }

        using (Bitmap premultiplied = Decode(BuildTiff(1, 1, 4, pixels, extraSamples: 1)))
        {
            Color c = premultiplied.GetPixel(0, 0);
            Assert.AreEqual(128, c.A);
            Assert.IsTrue(Math.Abs(c.R - 199) <= 1 && Math.Abs(c.G - 100) <= 1 && Math.Abs(c.B - 50) <= 1,
                $"associated alpha is divided out: {c.R},{c.G},{c.B}");
        }

        using (Bitmap unspecified = Decode(BuildTiff(1, 1, 4, pixels, extraSamples: 0)))
        {
            Assert.AreEqual(255, unspecified.GetPixel(0, 0).A, "an unspecified extra sample is not alpha");
        }
    }

    [TestMethod]
    public void Tiff_EncoderDeclaresUnassociatedAlpha()
    {
        using var source = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        source.SetPixel(0, 0, Color.FromArgb(128, 255, 0, 0));
        byte[] tiff = Encode(source, ImageFormat.Tiff);

        int entries = tiff[8] | (tiff[9] << 8);
        int extraSamples = -1;
        for (int i = 0; i < entries; i++)
        {
            int e = 10 + i * 12;
            if ((tiff[e] | (tiff[e + 1] << 8)) == 338)
                extraSamples = tiff[e + 8] | (tiff[e + 9] << 8);
        }
        Assert.AreEqual(2, extraSamples, "straight alpha must be declared as ExtraSamples = 2");

        using Bitmap decoded = Decode(tiff);
        Assert.AreEqual(Color.FromArgb(128, 255, 0, 0).ToArgb(), decoded.GetPixel(0, 0).ToArgb());
    }
}
