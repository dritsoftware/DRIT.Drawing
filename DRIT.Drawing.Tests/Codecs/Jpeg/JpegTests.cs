using DRIT.Drawing.Codecs.Jpeg;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using Encoder = DRIT.Drawing.Imaging.Encoder;

namespace DRIT.Drawing.Tests.Codecs.Jpeg
{
    [TestClass]
    public class JpegTests
    {
        // ── 1. JpegMarker enum values ─────────────────────────────────────────────

        [TestMethod]
        public void JpegMarker_Soi_HasCorrectValue()
        {
            Assert.AreEqual(0xD8, (byte)JpegMarker.SOI);
        }

        [TestMethod]
        public void JpegMarker_Eoi_HasCorrectValue()
        {
            Assert.AreEqual(0xD9, (byte)JpegMarker.EOI);
        }

        // ── 2. JpegReader — marker stream ─────────────────────────────────────────

        [TestMethod]
        public void JpegReader_ReadMarkers_FromMinimalStream()
        {
            // SOI (FF D8) + APP0 (FF E0) with 4-byte payload + EOI (FF D9)
            byte[] stream = {
                0xFF, 0xD8,                          // SOI
                0xFF, 0xE0, 0x00, 0x06, 0xAA, 0xBB, 0xCC, 0xDD, // APP0 length=6 → 4 payload bytes
                0xFF, 0xD9,                          // EOI
            };
            var reader = new JpegReader(new MemoryStream(stream));

            var (m0, p0) = reader.ReadMarker();
            Assert.AreEqual(JpegMarker.SOI, m0);
            Assert.AreEqual(0, p0.Length, "SOI is stand-alone; no payload.");

            var (m1, p1) = reader.ReadMarker();
            Assert.AreEqual(JpegMarker.APP0, m1);
            Assert.AreEqual(4, p1.Length);
            Assert.AreEqual(0xAA, p1[0]);

            var (m2, p2) = reader.ReadMarker();
            Assert.AreEqual(JpegMarker.EOI, m2);
            Assert.AreEqual(0, p2.Length, "EOI is stand-alone; no payload.");
        }

        // ── 3. JfifHeader ─────────────────────────────────────────────────────────

        [TestMethod]
        public void JfifHeader_TryParse_ExtractsFields()
        {
            byte[] jfifData = {
                0x4A, 0x46, 0x49, 0x46, 0x00, // "JFIF\0"
                0x01, 0x01,                    // version 1.1
                0x01,                          // units = DPI
                0x00, 0x48,                    // X density = 72
                0x00, 0x48,                    // Y density = 72
                0x00, 0x00                     // no thumbnail
            };
            var hdr = JfifHeader.TryParse(jfifData);
            Assert.IsNotNull(hdr);
            Assert.AreEqual(1, hdr!.MajorVersion);
            Assert.AreEqual(1, hdr.MinorVersion);
            Assert.AreEqual(1, hdr.DensityUnits);
            Assert.AreEqual(72, hdr.XDensity);
            Assert.AreEqual(72, hdr.YDensity);
            Assert.AreEqual(0, hdr.ThumbnailWidth);
        }

        [TestMethod]
        public void JfifHeader_TryParse_ReturnsNull_ForNonJfif()
        {
            byte[] notJfif = new byte[14]; // all zeros
            Assert.IsNull(JfifHeader.TryParse(notJfif));
        }

        // ── 4. QuantizationTable — DQT parsing ───────────────────────────────────

        [TestMethod]
        public void QuantizationTable_ParseDqt_StoresInNaturalOrder()
        {
            // All zigzag values = 8 → natural[0][0] == 8
            var dqtData = new byte[65];
            dqtData[0] = 0x00; // Pq=0 (8-bit), Tq=0
            for (int i = 1; i <= 64; i++) dqtData[i] = 8;

            var tables = QuantizationTable.ParseDqt(dqtData);
            Assert.AreEqual(1, tables.Length);
            Assert.AreEqual(0, tables[0].Id);
            Assert.AreEqual(8, tables[0][0]); // natural index 0 = (0,0)
        }

        [TestMethod]
        public void QuantizationTable_ParseDqt_DezigzagsCorrectly()
        {
            // Set zigzag[1]=10 → natural position 1 (row 0, col 1)
            // Set zigzag[2]=20 → natural position 8 (row 1, col 0)
            var dqtData = new byte[65];
            dqtData[0] = 0x00;
            for (int i = 1; i <= 64; i++) dqtData[i] = 0;
            dqtData[1 + 1] = 10; // zigzag index 1 → natural 1
            dqtData[1 + 2] = 20; // zigzag index 2 → natural 8

            var tables = QuantizationTable.ParseDqt(dqtData);
            var t = tables[0];
            Assert.AreEqual(10, t[QuantizationTable.ZigZagOrder[1]]); // natural[1]
            Assert.AreEqual(20, t[QuantizationTable.ZigZagOrder[2]]); // natural[8]
        }

        [TestMethod]
        public void QuantizationTable_MultipleTablesInSegment()
        {
            // Two tables: Tq=0 (all 1) and Tq=1 (all 2)
            var dqtData = new byte[130];
            dqtData[0] = 0x00; for (int i = 1; i <= 64; i++) dqtData[i] = 1;
            dqtData[65] = 0x01; for (int i = 66; i <= 129; i++) dqtData[i] = 2;

            var tables = QuantizationTable.ParseDqt(dqtData);
            Assert.AreEqual(2, tables.Length);
            Assert.AreEqual(0, tables[0].Id);
            Assert.AreEqual(1, tables[0][0]);
            Assert.AreEqual(1, tables[1].Id);
            Assert.AreEqual(2, tables[1][0]);
        }

        // ── 5. HuffmanTable — DHT parsing and decoding ────────────────────────────

        /// <summary>
        /// Standard JPEG luminance DC Huffman table (Annex K.3.3.1).
        /// BITS[1..16] = {0,1,5,1,1,1,1,1,1,0,0,0,0,0,0,0}
        /// HUFFVAL     = {0,1,2,3,4,5,6,7,8,9,10,11}
        /// Code for symbol 0 : length 2, bit pattern 00.
        /// </summary>
        private static byte[] LuminanceDcDhtSegment() => new byte[]
        {
            0x00,                                           // Tc=0 (DC), Th=0
            0,1,5,1,1,1,1,1,1,0,0,0,0,0,0,0,               // BITS[1..16]
            0,1,2,3,4,5,6,7,8,9,10,11                       // HUFFVAL (12 symbols)
        };

        [TestMethod]
        public void HuffmanTable_ParseDht_CanDecodeSymbol()
        {
            var tables = HuffmanTable.ParseDht(LuminanceDcDhtSegment());
            Assert.AreEqual(1, tables.Length);
            var ht = tables[0];
            Assert.AreEqual(0, ht.Class); // DC
            Assert.AreEqual(0, ht.Id);

            // Symbol 0 has code 00 (2 bits). Feed 0b00000000 into a BitReader.
            var br = new BitReader(new MemoryStream(new byte[] { 0x00 }));
            int sym = ht.Decode(br);
            Assert.AreEqual(0, sym);
        }

        [TestMethod]
        public void HuffmanTable_MultipleTablesInSegment()
        {
            // DC luma (Tc=0,Th=0) + AC luma (Tc=1,Th=0) — for AC use trivial 1-symbol table
            // AC trivial: 1 symbol of length 1
            byte[] acTable = { 0x10, 1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0, 0x00 };
            var dhtData = new byte[LuminanceDcDhtSegment().Length + acTable.Length];
            Buffer.BlockCopy(LuminanceDcDhtSegment(), 0, dhtData, 0, LuminanceDcDhtSegment().Length);
            Buffer.BlockCopy(acTable, 0, dhtData, LuminanceDcDhtSegment().Length, acTable.Length);

            var tables = HuffmanTable.ParseDht(dhtData);
            Assert.AreEqual(2, tables.Length);
            Assert.AreEqual(0, tables[0].Class); // DC
            Assert.AreEqual(1, tables[1].Class); // AC
        }

        // ── 6. FrameHeader — SOF0 parsing ────────────────────────────────────────

        [TestMethod]
        public void FrameHeader_Parse_Sof0_ExtractsDimensions()
        {
            byte[] sof0Data = {
                0x08,       // precision = 8
                0x00, 0x02, // height = 2
                0x00, 0x02, // width  = 2
                0x01,       // 1 component
                0x01, 0x11, 0x00 // id=1, H=1 V=1, qtable=0
            };
            var fh = FrameHeader.Parse(JpegMarker.SOF0, sof0Data);
            Assert.AreEqual(SofType.Baseline, fh.Type);
            Assert.AreEqual(8, fh.Precision);
            Assert.AreEqual(2, fh.Width);
            Assert.AreEqual(2, fh.Height);
            Assert.AreEqual(1, fh.Components.Length);
            Assert.AreEqual(1, fh.Components[0].Id);
            Assert.AreEqual(1, fh.Components[0].HSampling);
            Assert.AreEqual(1, fh.Components[0].VSampling);
            Assert.AreEqual(0, fh.Components[0].QuantizationTableId);
        }

        // ── 7. ScanHeader — SOS parsing ───────────────────────────────────────────

        [TestMethod]
        public void ScanHeader_Parse_ExtractsComponents()
        {
            byte[] sosData = {
                0x01,       // 1 component
                0x01, 0x00, // component id=1, DC table=0, AC table=0
                0x00,       // Ss = 0
                0x3F,       // Se = 63
                0x00        // Ah=0, Al=0
            };
            var sh = ScanHeader.Parse(sosData);
            Assert.AreEqual(1, sh.Components.Length);
            Assert.AreEqual(1, sh.Components[0].ComponentId);
            Assert.AreEqual(0, sh.Components[0].DcTableId);
            Assert.AreEqual(0, sh.Components[0].AcTableId);
            Assert.AreEqual(0,  sh.Ss);
            Assert.AreEqual(63, sh.Se);
            Assert.AreEqual(0,  sh.Ah);
            Assert.AreEqual(0,  sh.Al);
        }

        // ── 8. ExifReader — Orientation tag ──────────────────────────────────────

        [TestMethod]
        public void ExifReader_ReadOrientation_ReturnsCorrectValue()
        {
            // Minimal Exif APP1 with Orientation = 6
            byte[] exifData = {
                0x45,0x78,0x69,0x66,0x00,0x00, // "Exif\0\0"
                0x49,0x49,                      // "II" (little-endian)
                0x2A,0x00,                      // TIFF magic = 42
                0x08,0x00,0x00,0x00,            // IFD0 offset = 8
                0x01,0x00,                      // 1 directory entry
                0x12,0x01,                      // tag = 274 (Orientation)
                0x03,0x00,                      // type = SHORT
                0x01,0x00,0x00,0x00,            // count = 1
                0x06,0x00,0x00,0x00,            // value = 6
                0x00,0x00,0x00,0x00             // next IFD = 0 (none)
            };
            int orientation = ExifReader.ReadOrientation(exifData);
            Assert.AreEqual(6, orientation);
        }

        [TestMethod]
        public void ExifReader_ReadOrientation_ReturnsOne_WhenAbsent()
        {
            Assert.AreEqual(1, ExifReader.ReadOrientation(null!));
            Assert.AreEqual(1, ExifReader.ReadOrientation(new byte[4]));
        }

        // ── 9. BitReader — bit order and byte-stuffing ────────────────────────────

        [TestMethod]
        public void BitReader_ReadBits_CorrectOrder()
        {
            // 0xB4 = 1011 0100, 0x00 = 0000 0000
            var br = new BitReader(new MemoryStream(new byte[] { 0xB4, 0x00 }));
            Assert.AreEqual(1, br.ReadBit());   // MSB first
            Assert.AreEqual(0, br.ReadBit());
            Assert.AreEqual(1, br.ReadBit());
            Assert.AreEqual(1, br.ReadBit());
            Assert.AreEqual(4, br.ReadBits(4)); // 0100
        }

        [TestMethod]
        public void BitReader_ByteStuffing_HandlesFF00()
        {
            // 0xFF 0x00 must produce 8 bits of 0xFF (not treated as a marker)
            var br = new BitReader(new MemoryStream(new byte[] { 0xFF, 0x00, 0x00 }));
            int high = br.ReadBits(8); // should be 0xFF (byte stuffing)
            Assert.AreEqual(0xFF, high);
        }

        [TestMethod]
        public void JpegReader_ReadEntropySegments_PreservesRestartBoundaries()
        {
            byte[] entropy =
            {
                0xA5, 0xFF, 0x00, 0xFF, 0xD0,
                0x5A, 0xFF, 0xD1,
                0xC0, 0xFF, 0xD9
            };
            var reader = new JpegReader(new MemoryStream(entropy));

            var segments = reader.ReadEntropySegments();

            Assert.AreEqual(3, segments.Count);
            CollectionAssert.AreEqual(new byte[] { 0xA5, 0xFF }, segments[0]);
            CollectionAssert.AreEqual(new byte[] { 0x5A }, segments[1]);
            CollectionAssert.AreEqual(new byte[] { 0xC0 }, segments[2]);

            var (marker, _) = reader.ReadMarker();
            Assert.AreEqual(JpegMarker.EOI, marker);
        }

        // ─────────────────────────────────────────────────────────────────────
        // JPEG
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void Jpeg_CanDecode_JpegSignature()
        {
            byte[] header = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F' };
            var codec = new DRIT.Drawing.Codecs.JpegCodec();
            Assert.IsTrue(codec.CanDecode(header));
        }

        [TestMethod]
        public void Jpeg_RoundTrip_ApproximateColors()
        {
            using var src = CodecsTestHelpers.MakeRgb(8, 8, Color.FromArgb(255, 200, 100, 50));
            using var dst = CodecsTestHelpers.RoundTrip(src, ImageFormat.Jpeg);

            Assert.AreEqual(8, dst.Width);
            Assert.AreEqual(8, dst.Height);
            CodecsTestHelpers.AssertColorClose(Color.FromArgb(255, 200, 100, 50), dst.GetPixel(4, 4), tolerance: 15);
        }

        [TestMethod]
        public void Jpeg_RoundTrip_QualityParameter()
        {
            using var src = CodecsTestHelpers.MakeRgb(16, 16, Color.FromArgb(255, 128, 64, 32));

            // Encode with explicit quality = 90
            using var ms = new MemoryStream();
            var ep = new EncoderParameters(1);
            ep.Param[0] = new EncoderParameter(Encoder.Quality, 90L);
            src.Save(ms, ImageFormat.Jpeg, ep);
            ms.Position = 0;
            using var dst = new Bitmap(ms);

            Assert.AreEqual(16, dst.Width);
            Assert.AreEqual(16, dst.Height);
            CodecsTestHelpers.AssertColorClose(Color.FromArgb(255, 128, 64, 32), dst.GetPixel(8, 8), tolerance: 15);
        }

        [TestMethod]
        public void Jpeg_FromStream_SetsRawFormatToJpeg()
        {
            using var src = CodecsTestHelpers.MakeRgb(4, 4, Color.Red);
            using var ms = new MemoryStream();
            src.Save(ms, ImageFormat.Jpeg);
            ms.Position = 0;
            using var dst = new Bitmap(ms);
            Assert.IsTrue(dst.RawFormat.Equals(ImageFormat.Jpeg));
        }
    }
}
