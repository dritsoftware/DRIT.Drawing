using DRIT.Drawing.Colors;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Pens;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class LineCapRenderingTests
    {
        private const int Width = 400;
        private const int Height = 200;
        private const float Y = 100f;

        private static Bitmap DrawHorizontalLine(Action<Pen> configure, float width = 6f)
        {
            var bmp = new Bitmap(Width, Height);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            using var pen = new Pen(Color.Black, width);
            configure(pen);
            g.DrawLine(pen, 100, Y, 300, Y);
            return bmp;
        }

        private static bool IsInk(Bitmap bmp, int x, int y)
        {
            Color c = bmp.GetPixel(x, y);
            return c.R < 128 && c.G < 128 && c.B < 128;
        }

        /// <summary>Number of inked pixels in the column x.</summary>
        private static int ColumnInk(Bitmap bmp, int x)
        {
            int n = 0;
            for (int y = 0; y < bmp.Height; y++)
                if (IsInk(bmp, x, y)) n++;
            return n;
        }

        [TestMethod]
        public void FlatCaps_EndExactlyAtTheEndPoints()
        {
            using Bitmap bmp = DrawHorizontalLine(_ => { });
            Assert.IsTrue(ColumnInk(bmp, 299) > 0);
            Assert.AreEqual(0, ColumnInk(bmp, 303));
            Assert.AreEqual(0, ColumnInk(bmp, 96));
        }

        [TestMethod]
        public void SquareCap_IsSolidThroughTheCapAndExtendsByHalfThePenWidth()
        {
            using Bitmap bmp = DrawHorizontalLine(p => { p.StartCap = LineCap.Square; p.EndCap = LineCap.Square; }, 10f);
            for (int x = 96; x < 304; x++)
                Assert.IsTrue(IsInk(bmp, x, (int)Y), $"gap at x={x}");
            Assert.AreEqual(0, ColumnInk(bmp, 306));
        }

        [TestMethod]
        public void ArrowAnchor_IsWiderThanThePenAtTheEnd()
        {
            using Bitmap bmp = DrawHorizontalLine(p => p.EndCap = LineCap.ArrowAnchor);
            // Head: length 2 pen widths (x = 288..300), base 3 pen widths (18px) wide.
            Assert.IsTrue(ColumnInk(bmp, 290) > 12, "arrow head should be wider than the 6px pen");
            Assert.IsTrue(ColumnInk(bmp, 200) <= 7);
        }

        [TestMethod]
        public void AdjustableArrowCap_DrawsAnArrowHeadAtTheLineEnd()
        {
            using var arrow = new AdjustableArrowCap(3, 3);
            using Bitmap bmp = DrawHorizontalLine(p => p.CustomEndCap = arrow, 5f);

            // The base of the head is 3 pen widths wide and the tip sits on the end point.
            Assert.IsTrue(ColumnInk(bmp, 287) >= 12, $"head too narrow: {ColumnInk(bmp, 287)}");
            Assert.IsTrue(IsInk(bmp, 298, (int)Y));
            Assert.AreEqual(0, ColumnInk(bmp, 304));
            // The body does not poke out beside the tip.
            Assert.IsTrue(ColumnInk(bmp, 299) <= 3);
        }

        [TestMethod]
        public void AdjustableArrowCap_OnStartPointsBackwards()
        {
            using var arrow = new AdjustableArrowCap(3, 3);
            using Bitmap bmp = DrawHorizontalLine(p => p.CustomStartCap = arrow, 5f);
            Assert.IsTrue(ColumnInk(bmp, 113) >= 12);
            Assert.AreEqual(0, ColumnInk(bmp, 96));
        }

        [TestMethod]
        public void HollowArrowCap_HasAnUnfilledInterior()
        {
            using var arrow = new AdjustableArrowCap(4, 4, isFilled: false);
            using Bitmap bmp = DrawHorizontalLine(p => p.CustomEndCap = arrow, 4f);
            using var filled = new AdjustableArrowCap(4, 4, isFilled: true);
            using Bitmap solid = DrawHorizontalLine(p => p.CustomEndCap = filled, 4f);

            int hollowInk = 0, solidInk = 0;
            for (int x = 280; x < 304; x++)
            {
                hollowInk += ColumnInk(bmp, x);
                solidInk += ColumnInk(solid, x);
            }
            Assert.IsTrue(hollowInk < solidInk, $"hollow={hollowInk} solid={solidInk}");
        }

        [TestMethod]
        public void CustomLineCap_FillPath_IsDrawnAtTheEnd()
        {
            using var fill = new GraphicsPath();
            fill.AddEllipse(-1.5f, -1.5f, 3f, 3f);
            using var cap = new CustomLineCap(fill, null);
            using Bitmap bmp = DrawHorizontalLine(p => p.CustomEndCap = cap, 4f);
            Assert.IsTrue(ColumnInk(bmp, 300) >= 10, "a dot of 3 pen widths should be drawn at the end");
        }

        [TestMethod]
        public void ClosedFigures_GetNoCaps()
        {
            using var bmp = new Bitmap(Width, Height);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.White);
            using var pen = new Pen(Color.Black, 4f);
            pen.StartCap = LineCap.ArrowAnchor;
            pen.EndCap = LineCap.ArrowAnchor;
            g.DrawRectangle(pen, 100, 50, 200, 100);
            // An arrow anchor at the start corner would add ink well inside the rectangle.
            Assert.IsFalse(IsInk(bmp, 112, 62));
            Assert.IsFalse(IsInk(bmp, 112, 138));
        }
    }
}
