using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Tests.Codecs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DRIT.Drawing.Tests
{
    public class GifTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // GIF
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void Gif_CanDecode_Gif89aSignature()
        {
            byte[] header = { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 0, 0 };
            var codec = new DRIT.Drawing.Codecs.GifCodec();
            Assert.IsTrue(codec.CanDecode(header));
        }

        [TestMethod]
        public void Gif_Decode_SingleRedPixel()
        {
            // Minimal 1×1 GIF89a with a red pixel
            byte[] gifBytes =
            {
                0x47,0x49,0x46,0x38,0x39,0x61, // GIF89a
                0x01,0x00,0x01,0x00,0x80,0x00,0x00, // LSD: 1×1, GCT present (2 colours)
                0xFF,0x00,0x00, // colour 0: red
                0x00,0x00,0x00, // colour 1: black
                0x2C,0x00,0x00,0x00,0x00,0x01,0x00,0x01,0x00,0x00, // image descriptor
                0x02,           // min code size = 2
                0x02,           // sub-block length = 2
                0x44,0x01,      // LZW: CLEAR(4) + index0 + EOI(5) packed LSB-first
                0x00,           // block terminator
                0x3B            // trailer
            };

            using var ms = new MemoryStream(gifBytes);
            using var bmp = new Bitmap(ms);

            Assert.AreEqual(1, bmp.Width);
            Assert.AreEqual(1, bmp.Height);
            var px = bmp.GetPixel(0, 0);
            Assert.AreEqual(255, px.R);
            Assert.AreEqual(0, px.G);
            Assert.AreEqual(0, px.B);
            Assert.AreEqual(255, px.A);
        }

        [TestMethod]
        public void Gif_RoundTrip_4Colours()
        {
            using var src = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            src.SetPixel(0, 0, Color.FromArgb(255, 255, 0, 0)); // red
            src.SetPixel(1, 0, Color.FromArgb(255, 0, 255, 0)); // green
            src.SetPixel(0, 1, Color.FromArgb(255, 0, 0, 255)); // blue
            src.SetPixel(1, 1, Color.FromArgb(255, 255, 255, 0)); // yellow

            using var dst = CodecsTestHelpers.RoundTrip(src, ImageFormat.Gif);

            Assert.AreEqual(2, dst.Width);
            Assert.AreEqual(2, dst.Height);
            CodecsTestHelpers.AssertColorClose(Color.FromArgb(255, 255, 0, 0), dst.GetPixel(0, 0));
            CodecsTestHelpers.AssertColorClose(Color.FromArgb(255, 0, 255, 0), dst.GetPixel(1, 0));
            CodecsTestHelpers.AssertColorClose(Color.FromArgb(255, 0, 0, 255), dst.GetPixel(0, 1));
            CodecsTestHelpers.AssertColorClose(Color.FromArgb(255, 255, 255, 0), dst.GetPixel(1, 1));
        }
    }
}
