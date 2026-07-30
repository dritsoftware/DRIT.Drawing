using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Brushes;

namespace DRIT.Drawing.IntegrationTests
{
    /// <summary>
    /// Integration tests for Graphics class that produce visual output.
    /// Images are saved to the Out folder for human verification.
    /// </summary>
    [TestClass]
    public class GraphicsIntegrationTests : IntegrationTestBase
    {
        /// <summary>Tests FillRectangle with overlapping rectangles showing red and blue fills.</summary>
        [TestMethod]
        public void Graphics_FillRectangle_VisibleOutput()
        {
            using var bmp = new Bitmap(400, 300);
            using var g = Graphics.FromImage(bmp);
            using var redBrush = new SolidBrush(Color.Red);
            using var blueBrush = new SolidBrush(Color.Blue);

            g.FillRectangle(redBrush, 50, 50, 200, 100);
            g.FillRectangle(blueBrush, 100, 100, 200, 100);

            SaveImage(bmp, "Graphics_FillRectangle_VisibleOutput");
        }

        /// <summary>Tests DrawLine with solid, dashed, and dotted line styles in different colors.</summary>
        [TestMethod]
        public void Graphics_DrawLine_VisibleOutput()
        {
            using var bmp = new Bitmap(400, 300);
            using var g = Graphics.FromImage(bmp);
            using var blackPen = new Pens.Pen(Color.Black, 2f);
            using var grayPen = new Pens.Pen(Color.Gray, 2f);
            using var greenPen = new Pens.Pen(Color.Green, 3f);
            using var bluePen = new Pens.Pen(Color.Blue, 2f);

            grayPen.DashStyle = DashStyle.Dash;
            bluePen.DashStyle = DashStyle.Dot;

            g.DrawLine(blackPen, 0, 50, 400, 50);
            g.DrawLine(grayPen, 0, 100, 400, 100);
            g.DrawLine(greenPen, 0, 150, 400, 299);
            g.DrawLine(bluePen, 0, 200, 400, 250);

            SaveImage(bmp, "Graphics_DrawLine_VisibleOutput");
        }

        /// <summary>Tests DrawEllipse rendering ellipses and circles with different sizes and colors.</summary>
        [TestMethod]
        public void Graphics_DrawEllipse_VisibleOutput()
        {
            using var bmp = new Bitmap(400, 300);
            using var g = Graphics.FromImage(bmp);
            using var redPen = new Pens.Pen(Color.Red, 3f);
            using var greenPen = new Pens.Pen(Color.Green, 3f);
            using var bluePen = new Pens.Pen(Color.Blue, 3f);

            g.DrawEllipse(redPen, new Rectangle(20, 20, 100, 80));
            g.DrawEllipse(greenPen, new Rectangle(150, 50, 150, 200));
            g.DrawEllipse(bluePen, new Rectangle(320, 50, 100, 100));

            SaveImage(bmp, "Graphics_DrawEllipse_VisibleOutput");
        }

        /// <summary>Tests DrawArc rendering arcs at different angle ranges (0-90, 90-180, 180-270, 270-360 degrees).</summary>
        [TestMethod]
        public void Graphics_DrawArc_VisibleOutput()
        {
            using var bmp = new Bitmap(400, 300);
            using var g = Graphics.FromImage(bmp);
            using var redPen = new Pens.Pen(Color.Red, 3f);
            using var greenPen = new Pens.Pen(Color.Green, 3f);
            using var bluePen = new Pens.Pen(Color.Blue, 3f);
            using var blackPen = new Pens.Pen(Color.Black, 3f);

            g.DrawArc(redPen, new Rectangle(20, 20, 100, 100), 0, 90);
            g.DrawArc(greenPen, new Rectangle(150, 20, 100, 100), 90, 90);
            g.DrawArc(bluePen, new Rectangle(280, 20, 100, 100), 180, 90);
            g.DrawArc(blackPen, new Rectangle(150, 150, 100, 100), 270, 90);

            SaveImage(bmp, "Graphics_DrawArc_VisibleOutput");
        }

        [TestMethod]
        public void Graphics_DrawBezier_VisibleOutput()
        {
            // Human Tester: Should see a 400x300 image with:
            // - Red Bezier curve from (20,50) through control points (80,20) and (320,20) to (380,50)
            // - Green Bezier curve from (20,150) through control points (100,100) and (300,200) to (380,150)
            // - Blue Bezier curve forming an S-shape
            using var bmp = new Bitmap(400, 300);
            using var g = Graphics.FromImage(bmp);
            using var redPen = new Pens.Pen(Color.Red, 2f);
            using var greenPen = new Pens.Pen(Color.Green, 2f);
            using var bluePen = new Pens.Pen(Color.Blue, 2f);

            g.DrawBezier(redPen, new PointF(20, 50), new PointF(80, 20), new PointF(320, 20), new PointF(380, 50));
            g.DrawBezier(greenPen, new PointF(20, 150), new PointF(100, 100), new PointF(300, 200), new PointF(380, 150));
            g.DrawBezier(bluePen, new PointF(50, 250), new PointF(150, 180), new PointF(250, 280), new PointF(350, 250));

            SaveImage(bmp, "Graphics_DrawBezier_VisibleOutput");
        }

        [TestMethod]
        public void Graphics_DrawPolygon_VisibleOutput()
        {
            // Human Tester: Should see a 400x300 image with:
            // - Red outlined triangle at left side
            // - Green filled triangle at center
            // - Blue outlined pentagon at right side
            using var bmp = new Bitmap(400, 300);
            using var g = Graphics.FromImage(bmp);
            using var redPen = new Pens.Pen(Color.Red, 2f);
            using var greenBrush = new SolidBrush(Color.Green);
            using var bluePen = new Pens.Pen(Color.Blue, 2f);

            PointF[] triangle = { new(50, 50), new(100, 200), new(20, 200) };
            PointF[] pentagon = { new(300, 50), new(380, 80), new(360, 150), new(300, 130), new(240, 150) };

            g.DrawPolygon(redPen, triangle);
            g.FillPolygon(greenBrush, triangle);
            g.DrawPolygon(bluePen, pentagon);

            SaveImage(bmp, "Graphics_DrawPolygon_VisibleOutput");
        }

        [TestMethod]
        public void Graphics_FillEllipse_VisibleOutput()
        {
            // Human Tester: Should see a 400x300 image with:
            // - Red filled ellipse at (20,20) size 100x80 - smooth anti-aliased edges
            // - Green filled circle at (150,50) size 100x100 - smooth anti-aliased edges
            // - Blue filled ellipse at (280,20) size 100x150 - smooth anti-aliased edges
            using var bmp = new Bitmap(400, 300);
            using var g = Graphics.FromImage(bmp);
            using var redBrush = new SolidBrush(Color.Red);
            using var greenBrush = new SolidBrush(Color.Green);
            using var blueBrush = new SolidBrush(Color.Blue);

            g.SmoothingMode = SmoothingMode.HighQuality;
            g.FillEllipse(redBrush, new Rectangle(20, 20, 100, 80));
            g.FillEllipse(greenBrush, new Rectangle(150, 50, 100, 100));
            g.FillEllipse(blueBrush, new Rectangle(280, 20, 100, 150));

            SaveImage(bmp, "Graphics_FillEllipse_VisibleOutput");
        }

        [TestMethod]
        public void Graphics_FillPath_VisibleOutput()
        {
            // Human Tester: Should see a 400x300 image with:
            // - Red filled path with a rectangle and circle (EvenOdd fill creates hole)
            // - Green filled path with a star shape
            using var bmp = new Bitmap(400, 300);
            using var g = Graphics.FromImage(bmp);
            using var redBrush = new SolidBrush(Color.Red);
            using var greenBrush = new SolidBrush(Color.Green);
            using var path1 = new GraphicsPath(FillMode.Alternate);
            using var path2 = new GraphicsPath();

            // EvenOdd fill - outer rectangle with inner hole
            path1.AddRectangle(new RectangleF(20, 20, 120, 120));
            path1.AddRectangle(new RectangleF(50, 50, 60, 60));

            // Star shape
            PointF[] star = {
                new(220, 20), new(260, 100), new(340, 100),
                new(280, 140), new(300, 220), new(220, 180),
                new(140, 220), new(160, 140), new(100, 100), new(180, 100)
            };
            path2.AddPolygon(star);

            g.FillPath(redBrush, path1);
            g.FillPath(greenBrush, path2);

            SaveImage(bmp, "Graphics_FillPath_VisibleOutput");
        }

        [TestMethod]
        public void Graphics_Transform_VisibleOutput()
        {
            // Human Tester: Should see a 400x300 image with:
            // - Red rectangle at original position (before transform)
            // - Green rectangle translated by (50,50)
            // - Blue rectangle rotated 45 degrees around center
            // - Black rectangle scaled by 1.5x
            using var bmp = new Bitmap(400, 300);
            using var g = Graphics.FromImage(bmp);
            using var redBrush = new SolidBrush(Color.Red);
            using var greenBrush = new SolidBrush(Color.Green);
            using var blueBrush = new SolidBrush(Color.Blue);
            using var blackBrush = new SolidBrush(Color.Black);

            g.FillRectangle(redBrush, 50, 50, 80, 50);

            g.TranslateTransform(50, 50);
            g.FillRectangle(greenBrush, 50, 50, 80, 50);

            g.ResetTransform();
            g.TranslateTransform(200, 150);
            g.RotateTransform(45);
            g.FillRectangle(blueBrush, -40, -25, 80, 50);

            g.ResetTransform();
            g.TranslateTransform(300, 150);
            g.ScaleTransform(1.5f, 1.5f);
            g.FillRectangle(blackBrush, -40, -25, 80, 50);

            SaveImage(bmp, "Graphics_Transform_VisibleOutput");
        }
    }
}