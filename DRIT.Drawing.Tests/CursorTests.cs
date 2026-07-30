using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class CursorTests
    {
        // ¦¦ Cursor ¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦¦

        [TestMethod]
        public void Cursor_Current_IsNotNull()
        {
            Assert.IsNotNull(Cursor.Current);
        }

        [TestMethod]
        public void Cursor_Current_HasPositiveSize()
        {
            var c = Cursor.Current;
            Assert.IsTrue(c.Size.Width > 0, "Size.Width > 0");
            Assert.IsTrue(c.Size.Height > 0, "Size.Height > 0");
        }

        [TestMethod]
        public void Cursor_Current_DefaultHotSpotIsZero()
        {
            var c = Cursor.Current;
            Assert.AreEqual(0, c.HotSpot.X, "HotSpot.X");
            Assert.AreEqual(0, c.HotSpot.Y, "HotSpot.Y");
        }

        [TestMethod]
        public void Cursor_Draw_DoesNotThrow()
        {
            using var bmp = new Bitmap(32, 32);
            using var g = Graphics.FromImage(bmp);

            Cursor.Current.Draw(g, new Rectangle(0, 0, 16, 16));
        }

        [TestMethod]
        public void Cursor_DrawStretched_DoesNotThrow()
        {
            using var bmp = new Bitmap(64, 64);
            using var g = Graphics.FromImage(bmp);

            Cursor.Current.DrawStretched(g, new Rectangle(0, 0, 64, 64));
        }

        [TestMethod]
        public void Cursor_FromIco_LoadsCorrectly()
        {
            // An ICO file (type=1) is also valid for Cursor — hotspot defaults to (0,0).
            using var ms = new MemoryStream(IconTests.Make2x2RedIcoBytes());
            using var cur = new Cursor(ms);

            Assert.AreEqual(2, cur.Size.Width);
            Assert.AreEqual(2, cur.Size.Height);
            Assert.AreEqual(0, cur.HotSpot.X);
            Assert.AreEqual(0, cur.HotSpot.Y);
        }

        [TestMethod]
        public void Cursor_Set_Current_Roundtrip()
        {
            var original = Cursor.Current;
            using var bmp = new Bitmap(8, 8);
            using var newCur = new Cursor(new MemoryStream(IconTests.Make2x2RedIcoBytes()));

            Cursor.Current = newCur;
            Assert.AreSame(newCur, Cursor.Current);

            // Restore
            Cursor.Current = original;
        }

        [TestMethod]
        public void Cursor_Dispose_ThrowsOnDraw()
        {
            using var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);

            var cur = new Cursor(new MemoryStream(IconTests.Make2x2RedIcoBytes()));
            cur.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(
                () => cur.Draw(g, new Rectangle(0, 0, 16, 16)));
        }
    }
}
