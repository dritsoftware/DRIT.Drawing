using System;
using System.IO;
using DRIT.Drawing.Codecs;
using DRIT.Drawing.Imaging;
using static DRIT.Drawing.Tests.Codecs.CodecTestBuilders;

namespace DRIT.Drawing.Tests.Codecs;

[TestClass]
public sealed class PngRobustnessTests
{
    private static byte[] SmallPng()
    {
        using Bitmap bitmap = Pattern(5, 4);
        return Encode(bitmap, ImageFormat.Png);
    }

    private static int FirstChunkAfterIhdr => 8 + 12 + 13;

    // ── Leniency: what libpng and browsers still display ─────────────────────

    [TestMethod]
    public void Png_BytesAfterIend_AreIgnoredUnlessStrict()
    {
        byte[] png = SmallPng();
        byte[] padded = Concat(png, new byte[] { 0, 0, 0, 7, 8, 9 });

        using Bitmap expected = Decode(png);
        using Bitmap actual = Decode(padded);
        AssertSamePixels(expected, actual);

        using var strict = new StrictConformanceScope();
        Assert.ThrowsException<InvalidDataException>(() => Decode(padded));
    }

    [TestMethod]
    public void Png_DamagedAncillaryChunkCrc_IsDroppedUnlessStrict()
    {
        byte[] png = SmallPng();
        byte[] text = PngChunk("tEXt", Ascii("Comment\0hello"));
        text[text.Length - 1] ^= 0xFF;
        byte[] damaged = Insert(png, FirstChunkAfterIhdr, text);

        using Bitmap expected = Decode(png);
        using Bitmap actual = Decode(damaged);
        AssertSamePixels(expected, actual);

        using var strict = new StrictConformanceScope();
        Assert.ThrowsException<InvalidDataException>(() => Decode(damaged));
    }

    [TestMethod]
    public void Png_UnusablePhysAndTransparencyChunks_AreIgnoredUnlessStrict()
    {
        byte[] png = SmallPng();
        byte[] badPhys = PngChunk("pHYs", new byte[3]);
        byte[] badTrns = PngChunk("tRNS", new byte[40]);   // truecolour allows at most 6 bytes
        byte[] damaged = Insert(Insert(png, FirstChunkAfterIhdr, badTrns), FirstChunkAfterIhdr, badPhys);

        using Bitmap expected = Decode(png);
        using Bitmap actual = Decode(damaged);
        AssertSamePixels(expected, actual);

        using var strict = new StrictConformanceScope();
        Assert.ThrowsException<InvalidDataException>(() => Decode(damaged));
    }

    [TestMethod]
    public void Png_DamagedImageDataCrc_IsAlwaysRejected()
    {
        byte[] png = SmallPng();
        int idat = IndexOf(png, "IDAT");
        png[idat + 4 + 2] ^= 0x01;   // flip a payload bit; the stored CRC no longer matches

        Assert.ThrowsException<InvalidDataException>(() => Decode(png));
        using var strict = new StrictConformanceScope();
        Assert.ThrowsException<InvalidDataException>(() => Decode(png));
    }

    [TestMethod]
    public void Png_Physical300Dpi_ReportsExactly300()
    {
        using Bitmap source = Pattern(4, 4);
        source._dpiX = 300;
        source._dpiY = 300;
        using Bitmap decoded = Decode(Encode(source, ImageFormat.Png));
        Assert.AreEqual(300f, decoded.HorizontalResolution);
        Assert.AreEqual(300f, decoded.VerticalResolution);
    }

    // ── Safety: nothing is allocated on the strength of an untrusted size ────

    [TestMethod]
    public void Png_TruncatedStream_ThrowsInvalidData()
    {
        byte[] png = SmallPng();
        // From the 8-byte signature on, the stream is recognised as PNG and must fail as malformed data.
        for (int length = 8; length < png.Length; length += 7)
        {
            byte[] truncated = new byte[length];
            Buffer.BlockCopy(png, 0, truncated, 0, length);
            Exception? failure = null;
            try { using Bitmap _ = Decode(truncated); }
            catch (Exception ex) { failure = ex; }
            Assert.IsInstanceOfType(failure, typeof(InvalidDataException), $"truncated to {length} bytes");
        }
    }

    [TestMethod]
    public void Png_ChunkLengthBeyondTheStream_IsRejectedWithoutAllocatingIt()
    {
        // A 12-byte chunk header claiming 10 MB of data on a stream that ends immediately.
        var header = new byte[8];
        WriteUInt32BE(header, 0, 10 * 1024 * 1024);
        Buffer.BlockCopy(Ascii("tEXt"), 0, header, 4, 4);
        byte[] png = Concat(PngSignature, PngIhdr(1, 1, 8, 0), header);

        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.ThrowsException<InvalidDataException>(() => Decode(png));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.IsTrue(allocated < 1024 * 1024, $"allocated {allocated} bytes for a 10 MB claim");
    }

    [TestMethod]
    public void Png_InflateBomb_IsStoppedAtTheDeclaredImageSize()
    {
        // 1x1 greyscale needs two inflated bytes; this IDAT inflates to 64 MB.
        byte[] bomb = ZlibHelper.Compress(new byte[64 * 1024 * 1024]);
        byte[] png = Concat(PngSignature, PngIhdr(1, 1, 8, 0), PngChunk("IDAT", bomb), PngChunk("IEND", Array.Empty<byte>()));

        long before = GC.GetAllocatedBytesForCurrentThread();
        Assert.ThrowsException<InvalidDataException>(() => Decode(png));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.IsTrue(allocated < 8 * 1024 * 1024, $"inflating allocated {allocated} bytes");
    }

    [TestMethod]
    public void Png_InvalidScanlineFilterType_ThrowsInvalidData()
    {
        byte[] png = Png(2, 1, 8, 0, new byte[] { 7, 10, 20 });
        Assert.ThrowsException<InvalidDataException>(() => Decode(png));
    }

    [TestMethod]
    public void Png_ScanlineDataShorterThanDeclared_ThrowsInvalidData()
    {
        byte[] png = Png(2, 3, 8, 0, new byte[] { 0, 10, 20 });
        Assert.ThrowsException<InvalidDataException>(() => Decode(png));
    }

    [TestMethod]
    public void Png_WideSixteenBitRgba_IsRejectedWithoutOverflowException()
    {
        // A width whose row length would overflow int must surface as InvalidDataException,
        // not as an OverflowException from checked arithmetic.
        byte[] png = Concat(PngSignature, PngIhdr(int.MaxValue, 1, 16, 6));
        Exception? failure = null;
        try { using Bitmap _ = Decode(png); }
        catch (Exception ex) { failure = ex; }
        Assert.IsInstanceOfType(failure, typeof(InvalidDataException));
    }

    // ── zlib wrapper ─────────────────────────────────────────────────────────

    [TestMethod]
    public void Zlib_RoundTripsAndRespectsTheOutputCap()
    {
        byte[] data = new byte[10_000];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)(i * 31);
        byte[] compressed = ZlibHelper.Compress(data);

        CollectionAssert.AreEqual(data, ZlibHelper.Decompress(compressed, data.Length));
        Assert.ThrowsException<InvalidDataException>(() => ZlibHelper.Decompress(compressed, data.Length - 1));
    }

    [TestMethod]
    public void Zlib_RejectsBadHeaderAndBadChecksum()
    {
        byte[] data = new byte[100];
        byte[] compressed = ZlibHelper.Compress(data);

        byte[] badMethod = (byte[])compressed.Clone();
        badMethod[0] = 0x79;
        Assert.ThrowsException<InvalidDataException>(() => ZlibHelper.Decompress(badMethod, 1000));

        byte[] badCheck = (byte[])compressed.Clone();
        badCheck[1] ^= 0x01;
        Assert.ThrowsException<InvalidDataException>(() => ZlibHelper.Decompress(badCheck, 1000));

        byte[] badAdler = (byte[])compressed.Clone();
        badAdler[badAdler.Length - 1] ^= 0x01;
        Assert.ThrowsException<InvalidDataException>(() => ZlibHelper.Decompress(badAdler, 1000));
    }

    [TestMethod]
    public void Adler32_MatchesTheReferenceValueForLongInputs()
    {
        // Longer than the 5552-byte block the optimised loop uses between modulo reductions.
        byte[] data = new byte[100_000];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)(i * 7 + 1);
        uint a = 1, b = 0;
        foreach (byte value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }
        Assert.AreEqual((b << 16) | a, ZlibHelper.ComputeAdler32(data, 0, data.Length));
    }

    // ── Animation budget ─────────────────────────────────────────────────────

    [TestMethod]
    public void AnimationBudget_RejectsAggregatesThatEachFrameLimitWouldAllow()
    {
        // 40 frames of 3000 x 3000 (36 MB each): every per-frame limit passes, the aggregate must not.
        long total = 0;
        Assert.ThrowsException<InvalidDataException>(() =>
        {
            for (int i = 0; i < 40; i++)
                total = CodecValidation.AddAnimationFrame(total, 3000, 3000, "test");
        });
        Assert.IsTrue(total <= CodecValidation.MaxAnimationBytes);
        Assert.AreEqual(4L * 100 * 100 * 3, CodecValidation.AddAnimationFrame(4L * 100 * 100 * 2, 100, 100, "test"));
    }

    private static int IndexOf(byte[] bytes, string chunkType)
    {
        byte[] type = Ascii(chunkType);
        for (int i = 8; i + 4 <= bytes.Length; i++)
        {
            if (bytes[i] == type[0] && bytes[i + 1] == type[1] && bytes[i + 2] == type[2] && bytes[i + 3] == type[3])
                return i;
        }
        throw new AssertFailedException($"Chunk {chunkType} not found.");
    }
}
