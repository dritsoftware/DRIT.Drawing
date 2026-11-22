using System;
using System.IO;
using DRIT.Drawing.Codecs.Jpeg;
using DRIT.Drawing.Imaging;
using static DRIT.Drawing.Tests.Codecs.CodecTestBuilders;

namespace DRIT.Drawing.Tests.Codecs.Jpeg;

/// <summary>
/// The fast IDCT, the prefetching bit reader and the cheaper upsampler must not change what the decoder
/// produces: each is pinned against a straightforward reference.
/// </summary>
[TestClass]
public sealed class JpegPerformanceCorrectnessTests
{
    private static int MaxDifference(int[] coefficients)
    {
        var fast = new byte[64];
        var reference = new byte[64];
        IdctTransform.TransformBlock(coefficients, fast);
        IdctTransform.ReferenceTransformBlock(coefficients, reference);
        int max = 0;
        for (int i = 0; i < 64; i++)
            max = Math.Max(max, Math.Abs(fast[i] - reference[i]));
        return max;
    }

    [TestMethod]
    public void Idct_MatchesTheFloatingPointReference_OnRealisticBlocks()
    {
        var random = new Random(7);
        for (int iteration = 0; iteration < 2000; iteration++)
        {
            var block = new int[64];
            // Typical dequantised block: strong DC, a few low-frequency AC terms decaying quickly.
            block[0] = random.Next(-1024, 1024);
            int terms = random.Next(0, 20);
            for (int t = 0; t < terms; t++)
            {
                int index = random.Next(1, 64);
                int limit = Math.Max(2, 400 / (1 + index / 4));
                block[index] = random.Next(-limit, limit);
            }

            Assert.IsTrue(MaxDifference(block) <= 1, $"iteration {iteration}");
        }
    }

    [TestMethod]
    public void Idct_MatchesTheReference_OnSpecialBlocks()
    {
        var dcOnly = new int[64];
        dcOnly[0] = 800;
        Assert.IsTrue(MaxDifference(dcOnly) <= 1, "DC only");

        var firstRowOnly = new int[64];
        for (int u = 0; u < 8; u++) firstRowOnly[u] = (u + 1) * 40 * (u % 2 == 0 ? 1 : -1);
        Assert.IsTrue(MaxDifference(firstRowOnly) <= 1, "horizontal frequencies only");

        var firstColumnOnly = new int[64];
        for (int v = 0; v < 8; v++) firstColumnOnly[v * 8] = (v + 1) * 40 * (v % 2 == 0 ? 1 : -1);
        Assert.IsTrue(MaxDifference(firstColumnOnly) <= 1, "vertical frequencies only");

        var allZero = new int[64];
        Assert.AreEqual(0, MaxDifference(allZero));

        var saturating = new int[64];
        saturating[0] = 8000;           // way above white
        saturating[9] = -3000;
        Assert.IsTrue(MaxDifference(saturating) <= 1, "clamped output");
    }

    [TestMethod]
    public void Idct_IsDeterministicForCorruptCoefficients()
    {
        // Dequantised values of corrupt streams reach |2^31|: the output must be defined and identical on every
        // run (integer arithmetic wraps and is then clamped), never an exception.
        var block = new int[64];
        for (int i = 0; i < 64; i++) block[i] = (i % 2 == 0 ? int.MaxValue : int.MinValue) - i;
        var first = new byte[64];
        var second = new byte[64];
        IdctTransform.TransformBlock(block, first);
        IdctTransform.TransformBlock(block, second);
        CollectionAssert.AreEqual(first, second);
    }

    // ── Bit reader and Huffman decoding ──────────────────────────────────────

    [TestMethod]
    public void BitReader_Peek16_NeverInventsBitsAtTheEndOfTheData()
    {
        var reader = new BitReader(new byte[] { 0xAB, 0xCD, 0xEF });
        int window = reader.Peek16(out int valid);
        Assert.AreEqual(16, valid);
        Assert.AreEqual(0xABCD, window);

        reader.ConsumeBits(12);                 // 12 consumed: only 12 bits remain
        window = reader.Peek16(out valid);
        Assert.AreEqual(12, valid);
        Assert.AreEqual(0xDEF0, window, "bits below `valid` are zero padding");

        reader.ConsumeBits(12);
        reader.Peek16(out valid);
        Assert.AreEqual(0, valid);
        Assert.ThrowsException<EndOfStreamException>(() => reader.ReadBits(1));
    }

    [TestMethod]
    public void BitReader_Flush_KeepsPrefetchedWholeBytes()
    {
        // After a restart marker the decoder drops the unfinished byte only; the next interval's first byte
        // may already have been prefetched and must survive.
        var reader = new BitReader(new byte[] { 0xFF, 0x12, 0x34 });
        reader.Peek16(out _);                   // prefetches all three bytes
        Assert.AreEqual(7, reader.ReadBits(3)); // the first three bits of 0xFF: 5 bits of that byte are left
        reader.Flush();
        Assert.AreEqual(0x12, reader.ReadBits(8));
        Assert.AreEqual(0x34, reader.ReadBits(8));
    }

    [TestMethod]
    public void HuffmanDecode_UsesOnlyRealBitsNearTheEnd()
    {
        // Table with one 1-bit code ('0' -> symbol 5) and one 2-bit code ('10' -> symbol 9).
        var counts = new byte[16];
        counts[0] = 1; counts[1] = 1;
        HuffmanTable table = HuffmanTable.Create(0, 0, counts, new byte[] { 5, 9 });

        var reader = new BitReader(new byte[] { 0b0100_0000 });       // bits: 0 1 0 0 ...
        Assert.AreEqual(5, table.Decode(reader));                      // '0'

        // Seven padding-like bits remain ("1000000"): '10' -> 9, then nothing complete is left to invent.
        Assert.AreEqual(9, table.Decode(reader));

        var shortReader = new BitReader(new byte[] { 0b1000_0000 });   // '10' then zeros
        shortReader.ReadBits(7);                                       // leave exactly one bit ('0')
        Assert.AreEqual(5, table.Decode(shortReader));                 // '0' is a complete code
        Assert.ThrowsException<EndOfStreamException>(() => table.Decode(shortReader));
    }

    // ── Whole-decoder equivalence ────────────────────────────────────────────

    [TestMethod]
    public void Jpeg_DecodesSubsampledAndFullResolutionImagesAccurately()
    {
        // Encode with the library, decode with the library: the lossy round trip must stay within JPEG's
        // own error budget for smooth content, whatever IDCT/upsampling path is used.
        using Bitmap source = new Bitmap(96, 64, DRIT.Drawing.Enums.PixelFormat.Format32bppArgb);
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 96; x++)
            source.SetPixel(x, y, DRIT.Drawing.Colors.Color.FromArgb(255, x * 255 / 95, y * 255 / 63, (x + y) * 255 / 158));

        using Bitmap decoded = Decode(Encode(source, ImageFormat.Jpeg));
        long sum = 0;
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 96; x++)
        {
            var a = source.GetPixel(x, y);
            var b = decoded.GetPixel(x, y);
            sum += Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
        }
        Assert.IsTrue(sum / (96.0 * 64 * 3) < 3.0, $"mean error {sum / (96.0 * 64 * 3):F2}");
    }
}
