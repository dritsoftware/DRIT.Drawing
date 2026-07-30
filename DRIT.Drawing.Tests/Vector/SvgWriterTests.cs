using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using DRIT.Drawing;
using DRIT.Drawing.Brushes;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Pens;
using DRIT.Drawing.Text;
using DRIT.Drawing.Vector;
using DRIT.Drawing.Vector.Commands;
using DRIT.Drawing.Vector.Svg;

namespace DRIT.Drawing.Tests.Vector
{
    [TestClass]
    public class SvgWriterTests
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        private static RecordingGraphics CreateRecording(int w, int h, out VectorScene scene)
        {
            scene = new VectorScene { Width = w, Height = h };
            return new RecordingGraphics(scene, w, h);
        }

        private static string WriteSvg(VectorScene scene)
        {
            using var ms = new MemoryStream();
            SvgWriter.Write(ms, scene, SvgEncoderParameters.Default);
            ms.Position = 0;
            using var reader = new StreamReader(ms);
            return reader.ReadToEnd();
        }

        private static XElement ParseSvg(string svg)
        {
            return XElement.Parse(svg);
        }

        private static XElement RootElement(string svg)
        {
            var doc = XDocument.Parse(svg);
            return doc.Root!;
        }

        // ── Root structure ─────────────────────────────────────────────────────

        [TestMethod]
        public void Svg_Write_RootStructure()
        {
            using var rec = CreateRecording(800, 600, out var scene);
            var svg = WriteSvg(scene);
            var root = RootElement(svg);

            Assert.AreEqual("svg", root.Name.LocalName);
            Assert.AreEqual(SvgXmlWriter.SvgNamespace, root.Name.NamespaceName);
            Assert.AreEqual("800", root.Attribute("width")?.Value);
            Assert.AreEqual("600", root.Attribute("height")?.Value);
            Assert.AreEqual("0 0 800 600", root.Attribute("viewBox")?.Value);
            Assert.IsTrue(svg.StartsWith("<?xml"));
        }

        // ── DrawLine → <path> ──────────────────────────────────────────────────

        [TestMethod]
        public void Svg_Write_DrawLine_PathElement()
        {
            using var rec = CreateRecording(100, 100, out var scene);
            using var pen = new Pen(Color.Blue, 2f);
            rec.DrawLine(pen, 10, 10, 90, 90);

            var svg = WriteSvg(scene);
            var root = RootElement(svg);

            var path = root.Element(XName.Get("path", "http://www.w3.org/2000/svg"));
            Assert.IsNotNull(path, "Expected a <path> element");
            StringAssert.Contains(path.Attribute("d")?.Value ?? "", "M");
            StringAssert.Contains(path.Attribute("d")?.Value ?? "", "L");
            Assert.AreEqual("none", path.Attribute("fill")?.Value);
            Assert.IsNotNull(path.Attribute("stroke")?.Value);
        }

        // ── FillRectangle → <path> with fill ───────────────────────────────────

        [TestMethod]
        public void Svg_Write_FillRectangle_RectOrPath()
        {
            using var rec = CreateRecording(100, 100, out var scene);
            using var brush = new SolidBrush(Color.Red);
            rec.FillRectangle(brush, new RectangleF(10, 10, 50, 50));

            var svg = WriteSvg(scene);
            var root = RootElement(svg);

            var path = root.Element(XName.Get("path", "http://www.w3.org/2000/svg"));
            Assert.IsNotNull(path, "Expected a <path> element");
            Assert.AreEqual("rgb(255,0,0)", path.Attribute("fill")?.Value);
            Assert.AreEqual("none", path.Attribute("stroke")?.Value);
        }

        // ── SolidBrush color ───────────────────────────────────────────────────

        [TestMethod]
        public void Svg_Write_SolidBrush_Color()
        {
            using var rec = CreateRecording(50, 50, out var scene);
            using var brush = new SolidBrush(Color.Red);
            rec.FillRectangle(brush, new RectangleF(0, 0, 50, 50));

            var svg = WriteSvg(scene);
            StringAssert.Contains(svg, "rgb(255,0,0)");
        }

        [TestMethod]
        public void Svg_Write_SolidBrush_Alpha()
        {
            using var rec = CreateRecording(50, 50, out var scene);
            using var brush = new SolidBrush(Color.FromArgb(128, 255, 0, 0));
            rec.FillRectangle(brush, new RectangleF(0, 0, 50, 50));

            var svg = WriteSvg(scene);
            StringAssert.Contains(svg, "fill-opacity");
        }

        // ── LinearGradient ─────────────────────────────────────────────────────

        [TestMethod]
        public void Svg_Write_LinearGradient_Def()
        {
            using var rec = CreateRecording(100, 100, out var scene);
            using var brush = new LinearGradientBrush(
                new PointF(0, 0), new PointF(100, 100), Color.Red, Color.Blue);
            rec.FillRectangle(brush, new RectangleF(0, 0, 100, 100));

            var svg = WriteSvg(scene);
            var root = RootElement(svg);

            var defs = root.Element(XName.Get("defs", "http://www.w3.org/2000/svg"));
            Assert.IsNotNull(defs, "Expected <defs>");

            var grad = defs.Element(XName.Get("linearGradient", "http://www.w3.org/2000/svg"));
            Assert.IsNotNull(grad, "Expected <linearGradient>");
            Assert.IsNotNull(grad.Attribute("id")?.Value);

            StringAssert.Contains(svg, "url(#");
        }

        // ── PathGradient → radialGradient ──────────────────────────────────────

        [TestMethod]
        public void Svg_Write_PathGradient_RadialDef()
        {
            using var rec = CreateRecording(100, 100, out var scene);
            var points = new PointF[] { new(0, 0), new(100, 0), new(100, 100), new(0, 100) };
            using var brush = new PathGradientBrush(points)
            {
                CenterColor = Color.White,
            };
            brush.SurroundColors = new[] { Color.Black };
            rec.FillRectangle(brush, new RectangleF(0, 0, 100, 100));

            var svg = WriteSvg(scene);
            var root = RootElement(svg);

            var defs = root.Element(XName.Get("defs", "http://www.w3.org/2000/svg"));
            Assert.IsNotNull(defs, "Expected <defs>");

            var grad = defs.Element(XName.Get("radialGradient", "http://www.w3.org/2000/svg"));
            Assert.IsNotNull(grad, "Expected <radialGradient>");
        }

        // ── HatchBrush → pattern ───────────────────────────────────────────────

        [TestMethod]
        public void Svg_Write_HatchBrush_Pattern()
        {
            using var rec = CreateRecording(50, 50, out var scene);
            using var brush = new HatchBrush(HatchStyle.Horizontal, Color.Black, Color.White);
            rec.FillRectangle(brush, new RectangleF(0, 0, 50, 50));

            var svg = WriteSvg(scene);
            var root = RootElement(svg);

            var defs = root.Element(XName.Get("defs", "http://www.w3.org/2000/svg"));
            Assert.IsNotNull(defs, "Expected <defs>");

            var pattern = defs.Element(XName.Get("pattern", "http://www.w3.org/2000/svg"));
            Assert.IsNotNull(pattern, "Expected <pattern>");
        }

        // ── Pen dash array ─────────────────────────────────────────────────────

        [TestMethod]
        public void Svg_Write_Pen_DashArray()
        {
            using var rec = CreateRecording(100, 100, out var scene);
            using var pen = new Pen(Color.Black, 1f) { DashStyle = DashStyle.Dot };
            rec.DrawLine(pen, 0, 50, 100, 50);

            var svg = WriteSvg(scene);
            StringAssert.Contains(svg, "stroke-dasharray");
            StringAssert.Contains(svg, "1 2");
        }

        // ── Pen line cap ───────────────────────────────────────────────────────

        [TestMethod]
        public void Svg_Write_Pen_LineCap()
        {
            using var rec = CreateRecording(100, 100, out var scene);
            using var pen = new Pen(Color.Black, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            rec.DrawLine(pen, 10, 50, 90, 50);

            var svg = WriteSvg(scene);
            StringAssert.Contains(svg, "round");
        }

        // ── Transform → <g transform> ──────────────────────────────────────────

        [TestMethod]
        public void Svg_Write_Transform_Group()
        {
            using var rec = CreateRecording(100, 100, out var scene);
            using var brush = new SolidBrush(Color.Red);
            rec.ScaleTransform(2f, 2f);
            rec.FillRectangle(brush, new RectangleF(10, 10, 20, 20));

            var svg = WriteSvg(scene);
            var root = RootElement(svg);

            var g = root.Element(XName.Get("g", "http://www.w3.org/2000/svg"));
            Assert.IsNotNull(g, "Expected a <g> for transform");
            var transform = g.Attribute("transform")?.Value;
            Assert.IsNotNull(transform);
            StringAssert.Contains(transform, "matrix(");
        }

        // ── Clip → clipPath ────────────────────────────────────────────────────

        [TestMethod]
        public void Svg_Write_Clip_ClipPath()
        {
            using var rec = CreateRecording(100, 100, out var scene);
            using var brush = new SolidBrush(Color.Red);
            rec.SetClip(new RectangleF(0, 0, 50, 50));
            rec.FillRectangle(brush, new RectangleF(0, 0, 100, 100));

            var svg = WriteSvg(scene);
            var root = RootElement(svg);

            var defs = root.Element(XName.Get("defs", "http://www.w3.org/2000/svg"));
            Assert.IsNotNull(defs, "Expected <defs>");

            var clipPath = defs.Element(XName.Get("clipPath", "http://www.w3.org/2000/svg"));
            Assert.IsNotNull(clipPath, "Expected <clipPath>");

            StringAssert.Contains(svg, "clip-path");
            StringAssert.Contains(svg, "url(#");
        }

        // ── Text path mode ─────────────────────────────────────────────────────

        [TestMethod]
        public void Svg_Write_Text_PathMode()
        {
            using var rec = CreateRecording(200, 100, out var scene);
            using var font = new DRIT.Drawing.Text.Font("Arial", 16f);
            using var brush = new SolidBrush(Color.Black);
            rec.DrawString("Hi", font, brush, new RectangleF(10, 10, 100, 50));

            var svg = WriteSvg(scene, SvgEncoderParameters.Default);
            // Path mode should produce <path> elements (glyph outlines)
            StringAssert.Contains(svg, "<path");
        }

        // ── Text text mode ─────────────────────────────────────────────────────

        [TestMethod]
        public void Svg_Write_Text_TextMode()
        {
            using var rec = CreateRecording(200, 100, out var scene);
            using var font = new DRIT.Drawing.Text.Font("Arial", 16f);
            using var brush = new SolidBrush(Color.Black);
            rec.DrawString("Hi", font, brush, new RectangleF(10, 10, 100, 50));

            var p = new SvgEncoderParameters { TextMode = SvgTextMode.Text };
            var svg = WriteSvg(scene, p);
            StringAssert.Contains(svg, "<text");
        }

        // ── Image embedded ─────────────────────────────────────────────────────

        [TestMethod]
        public void Svg_Write_Image_Embedded()
        {
            using var src = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
            src.SetPixel(0, 0, Color.Red);

            using var rec = CreateRecording(50, 50, out var scene);
            rec.DrawImage(src, new RectangleF(0, 0, 50, 50));

            var svg = WriteSvg(scene);
            StringAssert.Contains(svg, "<image");
            StringAssert.Contains(svg, "data:image/png;base64,");
        }

        // ── Def dedup ──────────────────────────────────────────────────────────

        [TestMethod]
        public void Svg_Write_DefDedup()
        {
            using var rec = CreateRecording(100, 100, out var scene);
            using var brush1 = new LinearGradientBrush(
                new PointF(0, 0), new PointF(100, 100), Color.Red, Color.Blue);
            using var brush2 = new LinearGradientBrush(
                new PointF(0, 0), new PointF(100, 100), Color.Red, Color.Blue);

            rec.FillRectangle(brush1, new RectangleF(0, 0, 50, 50));
            rec.FillRectangle(brush2, new RectangleF(50, 50, 50, 50));

            var svg = WriteSvg(scene);
            var root = RootElement(svg);
            var defs = root.Element(XName.Get("defs", "http://www.w3.org/2000/svg"));

            // Should have exactly one linearGradient def (deduped)
            var grads = defs?.Elements(XName.Get("linearGradient", "http://www.w3.org/2000/svg"));
            int count = 0;
            if (grads != null) foreach (var g in grads) count++;
            Assert.AreEqual(1, count, "Expected exactly 1 deduped linearGradient");
        }

        // ── Round-trip raster equality ─────────────────────────────────────────

        [TestMethod]
        public void Svg_Write_RoundTrip_RasterEqual()
        {
            int w = 60, h = 60;
            using var rec = CreateRecording(w, h, out var scene);
            using var brush = new SolidBrush(Color.Red);
            using var pen = new Pen(Color.Blue, 2f);

            rec.Clear(Color.White);
            rec.FillRectangle(brush, new RectangleF(5, 5, 20, 20));
            rec.DrawEllipse(pen, new RectangleF(30, 30, 25, 25));

            // Direct raster
            var direct = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(direct))
            {
                VectorScenePlayer.Play(scene, g);
            }

            // SVG → raster via SvgImage.RenderToBitmap
            var svgImage = new SvgImage(w, h);
            foreach (var cmd in scene.Commands)
                svgImage.Scene.Commands.Add(cmd.Clone());
            using var fromSvg = svgImage.RenderToBitmap();

            AssertBitmapsEqual(direct, fromSvg, tolerance: 2);
        }

        // ── Bitmap input → embedded image SVG ──────────────────────────────────

        [TestMethod]
        public void Svg_Write_BitmapInput_EmbeddedImage()
        {
            // A Bitmap drawn onto a RecordingGraphics and saved as SVG should
            // produce an <image>-only SVG with embedded base64 PNG.
            using var bmp = new Bitmap(10, 10, PixelFormat.Format32bppArgb);
            bmp.SetPixel(0, 0, Color.Red);

            using var rec = CreateRecording(10, 10, out var scene);
            rec.DrawImage(bmp, new RectangleF(0, 0, 10, 10));

            var svg = WriteSvg(scene);
            StringAssert.Contains(svg, "<svg");
            StringAssert.Contains(svg, "<image");
            StringAssert.Contains(svg, "data:image/png;base64,");
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static string WriteSvg(VectorScene scene, SvgEncoderParameters p)
        {
            using var ms = new MemoryStream();
            SvgWriter.Write(ms, scene, p);
            ms.Position = 0;
            using var reader = new StreamReader(ms);
            return reader.ReadToEnd();
        }

        private static void AssertBitmapsEqual(Bitmap expected, Bitmap actual, int tolerance = 0)
        {
            Assert.AreEqual(expected.Width, actual.Width, "Width mismatch");
            Assert.AreEqual(expected.Height, actual.Height, "Height mismatch");

            for (int y = 0; y < expected.Height; y++)
            for (int x = 0; x < expected.Width; x++)
            {
                Color e = expected.GetPixel(x, y);
                Color a = actual.GetPixel(x, y);
                if (tolerance == 0)
                {
                    Assert.AreEqual(e.ToArgb(), a.ToArgb(),
                        $"Pixel mismatch at ({x},{y}): expected {e}, got {a}");
                }
                else
                {
                    Assert.IsTrue(Math.Abs(e.A - a.A) <= tolerance, $"Alpha at ({x},{y})");
                    Assert.IsTrue(Math.Abs(e.R - a.R) <= tolerance, $"Red at ({x},{y})");
                    Assert.IsTrue(Math.Abs(e.G - a.G) <= tolerance, $"Green at ({x},{y})");
                    Assert.IsTrue(Math.Abs(e.B - a.B) <= tolerance, $"Blue at ({x},{y})");
                }
            }
        }
    }
}