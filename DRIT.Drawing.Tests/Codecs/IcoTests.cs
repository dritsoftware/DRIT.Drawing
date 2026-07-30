using DRIT.Drawing.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DRIT.Drawing.Tests.Codecs
{
    [TestClass]
    public class IcoTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // ICO
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void Ico_CanDecode_IcoSignature()
        {
            byte[] header = { 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00 };
            var codec = new DRIT.Drawing.Codecs.IcoCodec();
            Assert.IsTrue(codec.CanDecode(header));
        }

        [TestMethod]
        public void Ico_Decode_2x2_32bpp_RedPixels()
        {
            // Minimal 2×2 ICO with 32bpp red pixels
            byte[] icoBytes =
            {
                // ICONDIR
                0x00,0x00,           // reserved
                0x01,0x00,           // type = 1 (icon)
                0x01,0x00,           // count = 1
                // ICONDIRENTRY
                0x02,                // width = 2
                0x02,                // height = 2
                0x00,                // colour count (0 = 32bpp)
                0x00,                // reserved
                0x01,0x00,           // planes = 1
                0x20,0x00,           // bit count = 32
                0x50,0x00,0x00,0x00, // bytes in resource = 80 (40 hdr + 32 xor + 8 and)
                0x16,0x00,0x00,0x00, // image offset = 22
                // BITMAPINFOHEADER (40 bytes)
                0x28,0x00,0x00,0x00, // header size = 40
                0x02,0x00,0x00,0x00, // width = 2
                0x04,0x00,0x00,0x00, // height = 4 (2 × actual, ICO convention)
                0x01,0x00,           // planes = 1
                0x20,0x00,           // bpp = 32
                0x00,0x00,0x00,0x00, // compression = BI_RGB
                0x00,0x00,0x00,0x00, // image size = 0
                0x00,0x00,0x00,0x00, // X PPM
                0x00,0x00,0x00,0x00, // Y PPM
                0x00,0x00,0x00,0x00, // colours used
                0x00,0x00,0x00,0x00, // colours important
                // XOR mask — BGRA, bottom-up (2 rows × 2 pixels × 4 bytes)
                0x00,0x00,0xFF,0xFF, 0x00,0x00,0xFF,0xFF, // row 0 (y=1): red,red
                0x00,0x00,0xFF,0xFF, 0x00,0x00,0xFF,0xFF, // row 1 (y=0): red,red
                // AND mask — 1 bpp, bottom-up (2 rows × 4 bytes each, all 0 = opaque)
                0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00
            };

            using var ms = new MemoryStream(icoBytes);
            using var bmp = new Bitmap(ms);

            Assert.AreEqual(2, bmp.Width);
            Assert.AreEqual(2, bmp.Height);

            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                {
                    var px = bmp.GetPixel(x, y);
                    Assert.AreEqual(255, px.R, $"R at ({x},{y})");
                    Assert.AreEqual(0, px.G, $"G at ({x},{y})");
                    Assert.AreEqual(0, px.B, $"B at ({x},{y})");
                    Assert.AreEqual(255, px.A, $"A at ({x},{y})");
                }
        }
    }
}
