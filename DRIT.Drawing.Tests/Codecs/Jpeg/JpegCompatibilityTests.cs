using System;
using System.IO;
using DRIT.Drawing.Codecs.Jpeg;
using DRIT.Drawing.Imaging;
using static DRIT.Drawing.Tests.Codecs.CodecTestBuilders;

namespace DRIT.Drawing.Tests.Codecs.Jpeg;

/// <summary>
/// The JPEG decoder must decode what libjpeg decodes. Rules that only exist to flag non-conforming files
/// (metadata validity, missing EOI, 0-based component ids, ...) apply in strict conformance mode only.
/// </summary>
[TestClass]
public sealed class JpegCompatibilityTests
{
    private static byte[] SmallJpeg()
    {
        using Bitmap bitmap = Pattern(32, 24);
        return Encode(bitmap, ImageFormat.Jpeg);
    }

    // ── Size limits ──────────────────────────────────────────────────────────

    private static byte[] FramePayload(int width, int height)
        => new byte[]
        {
            8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 3,
            1, 0x22, 0, 2, 0x11, 1, 3, 0x11, 1,
        };

    [TestMethod]
    public void FrameHeader_AcceptsFramesAboveFiveMegapixels()
    {
        // 12 MP camera frames and 24 MP DSLR frames are ordinary JPEGs.
        FrameHeader twelveMegapixel = FrameHeader.Parse(JpegMarker.SOF0, FramePayload(4000, 3000));
        Assert.AreEqual(4000, twelveMegapixel.Width);
        Assert.AreEqual(3000, twelveMegapixel.Height);
        Assert.AreEqual(6000, FrameHeader.Parse(JpegMarker.SOF2, FramePayload(6000, 4000)).Width);
    }

    [TestMethod]
    public void FrameHeader_StillRejectsFramesBeyondThePixelBudget()
    {
        Assert.ThrowsException<InvalidDataException>(() =>
            FrameHeader.Parse(JpegMarker.SOF0, FramePayload(65535, 65535)));
    }

    // ── Metadata is never a reason to refuse the pixels ──────────────────────

    private static byte[] WithXmp(byte[] jpeg, string xml)
        => Insert(jpeg, 2, JpegSegment(0xE1, Concat(Ascii("http://ns.adobe.com/xap/1.0/\0"), Ascii(xml))));

    private const string WrappedXmp =
        "<?xpacket begin=\"\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\"/><?xpacket end=\"w\"?>";

    [TestMethod]
    public void Jpeg_XmpWithoutPrecedingExif_DecodesNormally()
    {
        byte[] original = SmallJpeg();
        using Bitmap expected = Decode(original);
        using Bitmap actual = Decode(WithXmp(original, WrappedXmp));
        AssertSamePixels(expected, actual);
    }

    [TestMethod]
    public void Jpeg_XmpWithoutPacketWrapper_DecodesNormally()
    {
        byte[] original = SmallJpeg();
        using Bitmap expected = Decode(original);
        using Bitmap actual = Decode(WithXmp(original, "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"/>"));
        AssertSamePixels(expected, actual);
    }

    [TestMethod]
    public void Jpeg_CorruptExifHeader_DecodesNormally()
    {
        byte[] original = SmallJpeg();
        byte[] exif = Concat(Ascii("Exif\0\0II"), new byte[] { 99, 0, 8, 0, 0, 0 });
        using Bitmap expected = Decode(original);
        using Bitmap actual = Decode(Insert(original, 2, JpegSegment(0xE1, exif)));
        AssertSamePixels(expected, actual);
    }

    [TestMethod]
    public void Jpeg_JfxxThumbnailExtension_DecodesNormallyAndKeepsJfifDensity()
    {
        byte[] original = SmallJpeg();
        byte[] jfxx = JpegSegment(0xE0, Concat(Ascii("JFXX\0"), new byte[] { 0x10 }, new byte[16]));
        using Bitmap expected = Decode(original);
        using Bitmap actual = Decode(Insert(original, 2, jfxx));
        AssertSamePixels(expected, actual);
        Assert.AreEqual(expected.HorizontalResolution, actual.HorizontalResolution);
    }

    [TestMethod]
    public void Jpeg_MetadataRules_AreEnforcedInStrictMode()
    {
        byte[] original = SmallJpeg();
        using var strict = new StrictConformanceScope();

        Assert.ThrowsException<InvalidDataException>(() => Decode(WithXmp(original, WrappedXmp)));
        byte[] exif = Concat(Ascii("Exif\0\0II"), new byte[] { 99, 0, 8, 0, 0, 0 });
        Assert.ThrowsException<InvalidDataException>(() => Decode(Insert(original, 2, JpegSegment(0xE1, exif))));
        byte[] jfxx = JpegSegment(0xE0, Concat(Ascii("JFXX\0"), new byte[] { 0x10 }, new byte[16]));
        Assert.ThrowsException<NotSupportedException>(() => Decode(Insert(original, 2, jfxx)));
    }

    // ── Other structure that real files do not always satisfy ────────────────

    [TestMethod]
    public void Jpeg_ComponentIdsStartingAtZero_DecodeWithTheSameColours()
    {
        byte[] original = SmallJpeg();
        byte[] zeroBased = (byte[])original.Clone();
        int sof = IndexOfMarker(zeroBased, 0xC0);
        int sos = IndexOfMarker(zeroBased, 0xDA);
        Assert.AreEqual(3, zeroBased[sof + 9], "component count");
        for (int i = 0; i < 3; i++)
        {
            Assert.AreEqual(i + 1, zeroBased[sof + 10 + i * 3], "encoder writes ids 1..3");
            zeroBased[sof + 10 + i * 3] = (byte)i;
            zeroBased[sos + 5 + i * 2] = (byte)i;
        }

        using Bitmap expected = Decode(original);
        using Bitmap actual = Decode(zeroBased);
        AssertSamePixels(expected, actual);

        using var strict = new StrictConformanceScope();
        Assert.ThrowsException<InvalidDataException>(() => Decode(zeroBased));
    }

    [TestMethod]
    public void Jpeg_MissingEoi_DecodesNormallyUnlessStrict()
    {
        byte[] original = SmallJpeg();
        Assert.AreEqual(0xD9, original[original.Length - 1]);
        byte[] withoutEoi = new byte[original.Length - 2];
        Buffer.BlockCopy(original, 0, withoutEoi, 0, withoutEoi.Length);

        using Bitmap expected = Decode(original);
        using Bitmap actual = Decode(withoutEoi);
        AssertSamePixels(expected, actual);

        using var strict = new StrictConformanceScope();
        Assert.ThrowsException<InvalidDataException>(() => Decode(withoutEoi));
    }

    [TestMethod]
    public void Jpeg_UnusedEmptyHuffmanTable_DecodesUnlessStrict()
    {
        // A DHT that defines AC table 3 with no codes at all; no scan references it.
        byte[] original = SmallJpeg();
        byte[] emptyTable = new byte[17];
        emptyTable[0] = 0x13;
        byte[] withEmpty = Insert(original, IndexOfMarker(original, 0xDB), JpegSegment(0xC4, emptyTable));

        using Bitmap expected = Decode(original);
        using Bitmap actual = Decode(withEmpty);
        AssertSamePixels(expected, actual);

        using var strict = new StrictConformanceScope();
        Assert.ThrowsException<InvalidDataException>(() => Decode(withEmpty));
    }

    [TestMethod]
    public void HuffmanTable_EmptyTable_IsRejectedWhenUsedToDecode()
    {
        byte[] emptyTable = new byte[17];
        Assert.ThrowsException<InvalidDataException>(() => HuffmanTable.ParseDht(emptyTable));

        HuffmanTable table = HuffmanTable.ParseDht(emptyTable, allowEmpty: true)[0];
        var reader = new BitReader(new byte[] { 0xFF, 0x00, 0x12, 0x34 });
        Assert.ThrowsException<InvalidDataException>(() => table.Decode(reader));
    }

    [TestMethod]
    public void Jpeg_ScanUsingAnEmptyHuffmanTable_IsMalformedData()
    {
        // Redefine luminance DC table 0 as empty right before the scan: the scan needs it, so decoding must
        // fail as malformed data, not crash and not return a partial image.
        byte[] original = SmallJpeg();
        byte[] emptyDc0 = new byte[17];   // class 0, id 0, no codes
        byte[] damaged = Insert(original, IndexOfMarker(original, 0xDA), JpegSegment(0xC4, emptyDc0));

        Exception? failure = null;
        try { using Bitmap _ = Decode(damaged); }
        catch (Exception ex) { failure = ex; }
        Assert.IsInstanceOfType(failure, typeof(InvalidDataException));
    }

    // ── Bytes between segments ───────────────────────────────────────────────

    [TestMethod]
    public void Jpeg_StrayBytesBetweenSegments_AreSkippedUnlessStrict()
    {
        // libjpeg warns about "extraneous bytes before marker" and carries on.
        byte[] original = SmallJpeg();
        byte[] stray = { 0x00, 0x12, 0x34 };
        byte[] withStray = Insert(original, IndexOfMarker(original, 0xDB), stray);

        using Bitmap expected = Decode(original);
        using Bitmap actual = Decode(withStray);
        AssertSamePixels(expected, actual);

        using var strict = new StrictConformanceScope();
        Assert.ThrowsException<InvalidDataException>(() => Decode(withStray));
    }

    [TestMethod]
    public void JpegReader_FillBytesBeforeAMarker_AreAlwaysAccepted()
    {
        // 0xFF fill bytes before a marker are part of the format, not stray data.
        var stream = new MemoryStream(new byte[] { 0xFF, 0xFF, 0xFF, 0xD8 });
        var (marker, _) = new JpegReader(stream, strictMarkers: true).ReadMarker();
        Assert.AreEqual(JpegMarker.SOI, marker);
    }

    private static int IndexOfMarker(byte[] jpeg, byte marker)
    {
        for (int i = 2; i + 1 < jpeg.Length; i++)
        {
            if (jpeg[i] == 0xFF && jpeg[i + 1] == marker)
                return i;
        }
        throw new AssertFailedException($"Marker 0x{marker:X2} not found.");
    }

    // ── Restart intervals ────────────────────────────────────────────────────

    private static byte[] RestartStream(int intervals)
    {
        using var stream = new MemoryStream();
        for (int i = 0; i < intervals; i++)
        {
            stream.WriteByte(0x12);
            stream.WriteByte(0xFF);
            stream.WriteByte((byte)(0xD0 + (i & 7)));
        }
        stream.WriteByte(0x34);
        stream.WriteByte(0xFF);
        stream.WriteByte(0xD9);
        return stream.ToArray();
    }

    [TestMethod]
    public void JpegReader_RestartIntervalCount_IsBoundedByTheCallerNotByAGlobalConstant()
    {
        // 2000 intervals is a legitimate restart-interval-of-one image of only ~45 x 45 MCUs.
        using (var stream = new MemoryStream(RestartStream(2000)))
        {
            var segments = new JpegReader(stream).ReadEntropySegments(maxSegments: 5000);
            Assert.AreEqual(2001, segments.Count);
        }

        using (var stream = new MemoryStream(RestartStream(2000)))
        {
            Assert.ThrowsException<InvalidDataException>(() =>
                new JpegReader(stream).ReadEntropySegments(maxSegments: 100));
        }
    }

}
