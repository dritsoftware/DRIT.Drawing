using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Imaging;
using System;
using System.IO;
using System.Runtime.InteropServices;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Text;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Brushes;
using DRIT.Drawing.Pens;

namespace DRIT.Drawing.IntegrationTests
{
    /// <summary>
    /// Integration tests for text-on-path rendering that produce visual output.
    ///
    /// Human Tester Instructions:
    /// - Images are saved to the Out folder with descriptive names
    /// - Verify that text follows the baseline path
    /// - Check that glyphs are rotated to match the path tangent
    /// - Verify alignment (Near/Center/Far) places text correctly
    /// - Check perpendicular offset places text above/below the path
    /// </summary>
    [TestClass]
    public class TextOnPathIntegrationTests : IntegrationTestBase
    {
        /// <summary>Loads a Windows font from the Fonts directory into a PrivateFontCollection using AddMemoryFont.</summary>
        private static FontFamily LoadWindowsFont(string fontFileName)
        {
            string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), fontFileName);
            if (!File.Exists(fontPath))
                throw new FileNotFoundException($"Font file not found: {fontPath}", fontPath);

            byte[] fontData = File.ReadAllBytes(fontPath);
            IntPtr buffer = Marshal.AllocHGlobal(fontData.Length);
            try
            {
                Marshal.Copy(fontData, 0, buffer, fontData.Length);
                using var pfc = new PrivateFontCollection();
                pfc.AddMemoryFont(buffer, fontData.Length);
                return pfc.Families.Length > 0 ? pfc.Families[0] : FontFamily.GenericSansSerif;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>Tests text along a horizontal straight line. Glyphs should be upright, reading left→right.</summary>
        [TestMethod]
        public void TextOnPath_StraightLine_Horizontal()
        {
            using var bmp = new Bitmap(600, 300);
            using var g = Graphics.FromImage(bmp);
            using var bg = new SolidBrush(Color.White);
            using var fg = new SolidBrush(Color.Black);
            using var pen = new Pen(Color.LightGray, 1f);
            using var family = LoadWindowsFont("arial.ttf");
            using var font = new Text.Font(family, 20f, FontStyle.Regular, GraphicsUnit.Point);

            g.FillRectangle(bg, 0, 0, 600, 300);

            using var line = new GraphicsPath();
            line.AddLine(20f, 150f, 580f, 150f);

            g.DrawPath(pen, line);
            g.DrawStringOnPath("Text on a straight line", font, fg, line);

            SaveImage(bmp, "TextOnPath_StraightLine_Horizontal");
        }

        /// <summary>Tests text along a vertical line. Glyphs should be rotated 90°, reading top→bottom.</summary>
        [TestMethod]
        public void TextOnPath_StraightLine_Vertical()
        {
            using var bmp = new Bitmap(300, 600);
            using var g = Graphics.FromImage(bmp);
            using var bg = new SolidBrush(Color.White);
            using var fg = new SolidBrush(Color.Black);
            using var pen = new Pen(Color.LightGray, 1f);
            using var family = LoadWindowsFont("arial.ttf");
            using var font = new Text.Font(family, 20f, FontStyle.Regular, GraphicsUnit.Point);

            g.FillRectangle(bg, 0, 0, 300, 600);

            using var line = new GraphicsPath();
            line.AddLine(150f, 20f, 150f, 580f);

            g.DrawPath(pen, line);
            g.DrawStringOnPath("Vertical text", font, fg, line);

            SaveImage(bmp, "TextOnPath_StraightLine_Vertical");
        }

        /// <summary>Tests text around a circle, starting at the right (3 o'clock position).</summary>
        [TestMethod]
        public void TextOnPath_Circle_Top()
        {
            using var bmp = new Bitmap(400, 400);
            using var g = Graphics.FromImage(bmp);
            using var bg = new SolidBrush(Color.White);
            using var fg = new SolidBrush(Color.Black);
            using var pen = new Pen(Color.LightGray, 1f);
            using var family = LoadWindowsFont("arial.ttf");
            using var font = new Text.Font(family, 16f, FontStyle.Regular, GraphicsUnit.Point);

            g.FillRectangle(bg, 0, 0, 400, 400);

            using var circle = new GraphicsPath();
            circle.AddEllipse(new RectangleF(50f, 50f, 300f, 300f));

            g.DrawPath(pen, circle);
            g.DrawStringOnPath("TEXT ON A CIRCLE", font, fg, circle);

            SaveImage(bmp, "TextOnPath_Circle_Top");
        }

        /// <summary>Tests text around a circle with center alignment.</summary>
        [TestMethod]
        public void TextOnPath_Circle_Centered()
        {
            using var bmp = new Bitmap(400, 400);
            using var g = Graphics.FromImage(bmp);
            using var bg = new SolidBrush(Color.White);
            using var fg = new SolidBrush(Color.Black);
            using var pen = new Pen(Color.LightGray, 1f);
            using var family = LoadWindowsFont("arial.ttf");
            using var font = new Text.Font(family, 16f, FontStyle.Regular, GraphicsUnit.Point);

            g.FillRectangle(bg, 0, 0, 400, 400);

            using var circle = new GraphicsPath();
            circle.AddEllipse(new RectangleF(50f, 50f, 300f, 300f));

            g.DrawPath(pen, circle);
            g.DrawStringOnPath("Around the circle", font, fg, circle,
                new StringFormat { Alignment = StringAlignment.Center });

            SaveImage(bmp, "TextOnPath_Circle_Centered");
        }

        /// <summary>Tests text along a cubic Bézier S-curve.</summary>
        [TestMethod]
        public void TextOnPath_BezierCurve()
        {
            using var bmp = new Bitmap(600, 400);
            using var g = Graphics.FromImage(bmp);
            using var bg = new SolidBrush(Color.White);
            using var fg = new SolidBrush(Color.Black);
            using var pen = new Pen(Color.LightGray, 1f);
            using var family = LoadWindowsFont("arial.ttf");
            using var font = new Text.Font(family, 18f, FontStyle.Regular, GraphicsUnit.Point);

            g.FillRectangle(bg, 0, 0, 600, 400);

            using var curve = new GraphicsPath();
            curve.AddBezier(20f, 200f, 150f, 50f, 450f, 350f, 580f, 200f);

            g.DrawPath(pen, curve);
            g.DrawStringOnPath("Following the curve", font, fg, curve);

            SaveImage(bmp, "TextOnPath_BezierCurve");
        }

        /// <summary>Tests Near/Center/Far alignment on three identical line paths.</summary>
        [TestMethod]
        public void TextOnPath_Alignment_NearCenterFar()
        {
            using var bmp = new Bitmap(600, 300);
            using var g = Graphics.FromImage(bmp);
            using var bg = new SolidBrush(Color.White);
            using var fg = new SolidBrush(Color.Black);
            using var redBrush = new SolidBrush(Color.Red);
            using var blueBrush = new SolidBrush(Color.Blue);
            using var greenBrush = new SolidBrush(Color.Green);
            using var pen = new Pen(Color.LightGray, 1f);
            using var family = LoadWindowsFont("arial.ttf");
            using var font = new Text.Font(family, 16f, FontStyle.Regular, GraphicsUnit.Point);

            g.FillRectangle(bg, 0, 0, 600, 300);

            float y = 50;
            foreach (var (brush, align, label) in new[]
            {
                (redBrush, StringAlignment.Near, "Near"),
                (blueBrush, StringAlignment.Center, "Center"),
                (greenBrush, StringAlignment.Far, "Far"),
            })
            {
                using var line = new GraphicsPath();
                line.AddLine(20f, y, 580f, y);
                g.DrawPath(pen, line);
                g.DrawStringOnPath($"Alignment: {label}", font, brush, line,
                    new StringFormat { Alignment = align });
                y += 100;
            }

            SaveImage(bmp, "TextOnPath_Alignment_NearCenterFar");
        }

        /// <summary>Tests perpendicular offset placing text above the path.</summary>
        [TestMethod]
        public void TextOnPath_PerpendicularOffset_Above()
        {
            using var bmp = new Bitmap(600, 300);
            using var g = Graphics.FromImage(bmp);
            using var bg = new SolidBrush(Color.White);
            using var fg = new SolidBrush(Color.Black);
            using var pen = new Pen(Color.Red, 1f);
            using var family = LoadWindowsFont("arial.ttf");
            using var font = new Text.Font(family, 18f, FontStyle.Regular, GraphicsUnit.Point);

            g.FillRectangle(bg, 0, 0, 600, 300);

            using var line = new GraphicsPath();
            line.AddLine(20f, 200f, 580f, 200f);

            g.DrawPath(pen, line);
            g.DrawStringOnPath("Above the line", font, fg, line, null,
                new TextOnPathOptions { PerpendicularOffset = 25f });

            SaveImage(bmp, "TextOnPath_PerpendicularOffset_Above");
        }

        /// <summary>Tests perpendicular offset placing text below the path.</summary>
        [TestMethod]
        public void TextOnPath_PerpendicularOffset_Below()
        {
            using var bmp = new Bitmap(600, 300);
            using var g = Graphics.FromImage(bmp);
            using var bg = new SolidBrush(Color.White);
            using var fg = new SolidBrush(Color.Black);
            using var pen = new Pen(Color.Red, 1f);
            using var family = LoadWindowsFont("arial.ttf");
            using var font = new Text.Font(family, 18f, FontStyle.Regular, GraphicsUnit.Point);

            g.FillRectangle(bg, 0, 0, 600, 300);

            using var line = new GraphicsPath();
            line.AddLine(20f, 100f, 580f, 100f);

            g.DrawPath(pen, line);
            g.DrawStringOnPath("Below the line", font, fg, line, null,
                new TextOnPathOptions { PerpendicularOffset = -25f });

            SaveImage(bmp, "TextOnPath_PerpendicularOffset_Below");
        }

        /// <summary>Tests text around a closed rectangle perimeter; glyphs rotate at corners.</summary>
        [TestMethod]
        public void TextOnPath_ClosedRectangle()
        {
            using var bmp = new Bitmap(500, 400);
            using var g = Graphics.FromImage(bmp);
            using var bg = new SolidBrush(Color.White);
            using var fg = new SolidBrush(Color.Black);
            using var pen = new Pen(Color.LightGray, 1f);
            using var family = LoadWindowsFont("arial.ttf");
            using var font = new Text.Font(family, 14f, FontStyle.Regular, GraphicsUnit.Point);

            g.FillRectangle(bg, 0, 0, 500, 400);

            using var rect = new GraphicsPath();
            rect.AddRectangle(new RectangleF(50f, 50f, 400f, 300f));

            g.DrawPath(pen, rect);
            g.DrawStringOnPath("Text around a rectangle perimeter", font, fg, rect,
                new StringFormat { Alignment = StringAlignment.Center });

            SaveImage(bmp, "TextOnPath_ClosedRectangle");
        }

        /// <summary>Tests overflow: long text on a short line is truncated.</summary>
        [TestMethod]
        public void TextOnPath_Overflow_Stop()
        {
            using var bmp = new Bitmap(600, 300);
            using var g = Graphics.FromImage(bmp);
            using var bg = new SolidBrush(Color.White);
            using var fg = new SolidBrush(Color.Black);
            using var pen = new Pen(Color.Red, 1f);
            using var family = LoadWindowsFont("arial.ttf");
            using var font = new Text.Font(family, 16f, FontStyle.Regular, GraphicsUnit.Point);

            g.FillRectangle(bg, 0, 0, 600, 300);

            // Short line — text should be truncated.
            using var line = new GraphicsPath();
            line.AddLine(20f, 150f, 200f, 150f);

            g.DrawPath(pen, line);
            g.DrawStringOnPath("This text is too long for the path", font, fg, line, null,
                new TextOnPathOptions { Overflow = TextOnPathOverflow.Stop });

            SaveImage(bmp, "TextOnPath_Overflow_Stop");
        }

        /// <summary>Tests AddStringOnPath + FillPath + DrawPath for the baseline.</summary>
        [TestMethod]
        public void TextOnPath_AddStringOnPath_ThenFillPath()
        {
            using var bmp = new Bitmap(600, 400);
            using var g = Graphics.FromImage(bmp);
            using var bg = new SolidBrush(Color.White);
            using var fillBrush = new SolidBrush(Color.Red);
            using var baselinePen = new Pen(Color.Blue, 1f);
            using var family = LoadWindowsFont("arial.ttf");

            g.FillRectangle(bg, 0, 0, 600, 400);

            using var curve = new GraphicsPath();
            curve.AddBezier(20f, 300f, 150f, 100f, 450f, 500f, 580f, 300f);

            g.DrawPath(baselinePen, curve);

            // Build a fillable text path via AddStringOnPath, then fill it.
            using var textPath = new GraphicsPath();
            textPath.AddStringOnPath("Fillable", family, (int)FontStyle.Regular, 24f, curve);
            g.FillPath(fillBrush, textPath);

            SaveImage(bmp, "TextOnPath_AddStringOnPath_ThenFillPath");
        }

        /// <summary>Tests that DrawStringOnPath respects the current graphics transform (CTM).</summary>
        [TestMethod]
        public void TextOnPath_WithTransform()
        {
            using var bmp = new Bitmap(600, 400);
            using var g = Graphics.FromImage(bmp);
            using var bg = new SolidBrush(Color.White);
            using var fg = new SolidBrush(Color.Black);
            using var pen = new Pen(Color.LightGray, 1f);
            using var family = LoadWindowsFont("arial.ttf");
            using var font = new Text.Font(family, 18f, FontStyle.Regular, GraphicsUnit.Point);

            g.FillRectangle(bg, 0, 0, 600, 400);

            // Apply a translation + rotation before drawing.
            g.TranslateTransform(100f, 50f);
            g.RotateTransform(15f);

            using var line = new GraphicsPath();
            line.AddLine(0f, 150f, 400f, 150f);

            g.DrawPath(pen, line);
            g.DrawStringOnPath("Transformed text on path", font, fg, line);

            SaveImage(bmp, "TextOnPath_WithTransform");
        }

        /// <summary>Tests text on a circle with a start offset, beginning at the bottom.</summary>
        [TestMethod]
        public void TextOnPath_Circle_StartOffset()
        {
            using var bmp = new Bitmap(400, 400);
            using var g = Graphics.FromImage(bmp);
            using var bg = new SolidBrush(Color.White);
            using var fg = new SolidBrush(Color.Black);
            using var pen = new Pen(Color.LightGray, 1f);
            using var family = LoadWindowsFont("arial.ttf");
            using var font = new Text.Font(family, 14f, FontStyle.Regular, GraphicsUnit.Point);

            g.FillRectangle(bg, 0, 0, 400, 400);

            using var circle = new GraphicsPath();
            circle.AddEllipse(new RectangleF(50f, 50f, 300f, 300f));

            g.DrawPath(pen, circle);

            // Start at roughly 6 o'clock (3/4 of the way around).
            float circumference = (float)(Math.PI * 300f);
            g.DrawStringOnPath("Starting at the bottom", font, fg, circle, null,
                new TextOnPathOptions { StartOffset = circumference * 0.75f });

            SaveImage(bmp, "TextOnPath_Circle_StartOffset");
        }
    }
}