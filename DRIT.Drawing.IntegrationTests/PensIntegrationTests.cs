using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Brushes;

namespace DRIT.Drawing.IntegrationTests
{
    /// <summary>
    /// Integration tests for Pen class that produce visual output.
    /// 
    /// Human Tester Instructions:
    /// - Images are saved to the Out folder with descriptive names
    /// - Verify that line styles, joins, and caps are rendered correctly
    /// - Check that line widths are proportional and visible
    /// - Verify dash patterns are clearly distinguishable
    /// </summary>
    [TestClass]
    public class PensIntegrationTests : IntegrationTestBase
    {
        /// <summary>Tests different line widths (1, 3, 5, 10px) with horizontal lines.</summary>
        [TestMethod]
        public void Pen_LineWidth_VisibleOutput()
        {
            // Human Tester: Should see a 400x300 image with horizontal lines at:
            // - y=50: 1px red line (thin)
            // - y=100: 3px green line (medium)
            // - y=150: 5px blue line (thick)
            // - y=220: 10px black line (very thick)
            using var bmp = new Bitmap(400, 300);
            using var g = Graphics.FromImage(bmp);
            using var whiteBrush = new SolidBrush(Color.White);
            using var redPen = new Pens.Pen(Color.Red, 1f);
            using var greenPen = new Pens.Pen(Color.Green, 3f);
            using var bluePen = new Pens.Pen(Color.Blue, 5f);
            using var blackPen = new Pens.Pen(Color.Black, 10f);

            // Fill with white background
            g.FillRectangle(whiteBrush, 0, 0, 400, 300);

            g.DrawLine(redPen, 20, 50, 380, 50);
            g.DrawLine(greenPen, 20, 100, 380, 100);
            g.DrawLine(bluePen, 20, 150, 380, 150);
            g.DrawLine(blackPen, 20, 220, 380, 220);

            SaveImage(bmp, "Pen_LineWidth_VisibleOutput");
        }

        [TestMethod]
        public void Pen_DashStyle_VisibleOutput()
        {
            // Human Tester: Should see a 400x400 image with horizontal lines showing:
            // - y=30: Solid (no dashes)
            // - y=80: Dash (long dashes)
            // - y=130: Dot (dots)
            // - y=180: DashDot (dash-dot pattern)
            // - y=230: DashDotDot (dash-dot-dot pattern)
            // - y=280: Custom pattern (2-on, 1-off)
            using var bmp = new Bitmap(400, 400);
            using var g = Graphics.FromImage(bmp);
            using var whiteBrush = new SolidBrush(Color.White);
            using var pen = new Pens.Pen(Color.Black, 2f);

            // Fill with white background
            g.FillRectangle(whiteBrush, 0, 0, 400, 400);

            int y = 30;
            foreach (DashStyle style in Enum.GetValues(typeof(DashStyle)))
            {
                pen.DashStyle = style;
                g.DrawLine(pen, 20, y, 380, y);
                y += 50;
            }

            SaveImage(bmp, "Pen_DashStyle_VisibleOutput");
        }

        /// <summary>Tests different line join styles (Miter, Bevel, Round) on L-shaped lines.</summary>
        [TestMethod]
        public void Pen_LineJoin_VisibleOutput()
        {
            // Human Tester: Should see a 400x300 image with L-shaped lines showing:
            // - Top row (y=50): Miter join (sharp corner)
            // - Middle row (y=150): Bevel join (cut corner)
            // - Bottom row (y=250): Round join (rounded corner)
            using var bmp = new Bitmap(400, 300);
            using var g = Graphics.FromImage(bmp);
            using var whiteBrush = new SolidBrush(Color.White);
            using var pen = new Pens.Pen(Color.Black, 15f);

            // Fill with white background
            g.FillRectangle(whiteBrush, 0, 0, 400, 300);

            PointF[] miterPoints = { new(50, 50), new(150, 50), new(150, 150) };
            PointF[] bevelPoints = { new(200, 50), new(300, 50), new(300, 150) };
            PointF[] roundPoints = { new(350, 50), new(380, 50), new(380, 150) };

            pen.LineJoin = LineJoin.Miter;
            g.DrawLines(pen, miterPoints);

            pen.LineJoin = LineJoin.Bevel;
            g.DrawLines(pen, bevelPoints);

            pen.LineJoin = LineJoin.Round;
            g.DrawLines(pen, roundPoints);

            SaveImage(bmp, "Pen_LineJoin_VisibleOutput");
        }

        /// <summary>Tests different line cap styles (Flat, Square, Round, Arrow) on horizontal lines.</summary>
        [TestMethod]
        public void Pen_StartEndCap_VisibleOutput()
        {
            // Human Tester: Should see a 400x300 image with horizontal lines showing:
            // - y=50: Flat cap (default, squared off)
            // - y=100: Square cap (extends beyond endpoint)
            // - y=150: Round cap (semicircular end)
            // - y=200: Arrow cap (arrowhead at end)
            using var bmp = new Bitmap(400, 300);
            using var g = Graphics.FromImage(bmp);
            using var pen = new Pens.Pen(Color.Black, 10f);

            pen.StartCap = LineCap.Flat;
            pen.EndCap = LineCap.Flat;
            g.DrawLine(pen, 50, 50, 350, 50);

            pen.StartCap = LineCap.Square;
            pen.EndCap = LineCap.Square;
            g.DrawLine(pen, 50, 100, 350, 100);

            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            g.DrawLine(pen, 50, 150, 350, 150);

            pen.StartCap = LineCap.ArrowAnchor;
            pen.EndCap = LineCap.ArrowAnchor;
            g.DrawLine(pen, 50, 200, 350, 200);

            SaveImage(bmp, "Pen_StartEndCap_VisibleOutput");
        }
    }
}