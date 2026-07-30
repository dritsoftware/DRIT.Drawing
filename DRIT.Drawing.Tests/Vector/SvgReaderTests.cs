using System;
using System.IO;
using System.Xml.Linq;
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
    public class SvgReaderTests
    {
        private static VectorScene ReadSvg(string svg)
        {
            return SvgReader.ReadString(svg);
        }

        private static VectorScene ReadSvgWithRoot(int w, int h, string inner)
        {
            return ReadSvg($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{w}\" height=\"{h}\">{inner}</svg>");
        }

        [TestMethod]
        public void Svg_Read_RootDimensions()
        {
            var scene = ReadSvg("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"800\" height=\"600\"></svg>");
            Assert.AreEqual(800, scene.Width);
            Assert.AreEqual(600, scene.Height);
        }

        [TestMethod]
        public void Svg_Read_Rect()
        {
            var scene = ReadSvgWithRoot(100, 100, "<rect x=\"10\" y=\"10\" width=\"50\" height=\"50\" fill=\"red\"/>");
            Assert.IsTrue(scene.Commands.Count > 0);
            var fillCmd = FindCommand<FillPathCommand>(scene);
            Assert.IsNotNull(fillCmd);
            Assert.IsTrue(fillCmd.Path.PointCount > 0);
        }

        [TestMethod]
        public void Svg_Read_Circle()
        {
            var scene = ReadSvgWithRoot(100, 100, "<circle cx=\"50\" cy=\"50\" r=\"20\" fill=\"blue\"/>");
            var fillCmd = FindCommand<FillPathCommand>(scene);
            Assert.IsNotNull(fillCmd);
            Assert.IsTrue(fillCmd.Path.PointCount > 0);
        }

        [TestMethod]
        public void Svg_Read_Ellipse()
        {
            var scene = ReadSvgWithRoot(100, 100, "<ellipse cx=\"50\" cy=\"50\" rx=\"30\" ry=\"20\" fill=\"green\"/>");
            var fillCmd = FindCommand<FillPathCommand>(scene);
            Assert.IsNotNull(fillCmd);
        }

        [TestMethod]
        public void Svg_Read_Line()
        {
            var scene = ReadSvgWithRoot(100, 100, "<line x1=\"10\" y1=\"10\" x2=\"90\" y2=\"90\" stroke=\"black\"/>");
            var drawCmd = FindCommand<DrawPathCommand>(scene);
            Assert.IsNotNull(drawCmd);
            Assert.IsTrue(drawCmd.Path.PointCount >= 2);
        }

        [TestMethod]
        public void Svg_Read_Polygon()
        {
            var scene = ReadSvgWithRoot(100, 100, "<polygon points=\"10,10 50,10 50,50\" fill=\"red\"/>");
            var fillCmd = FindCommand<FillPathCommand>(scene);
            Assert.IsNotNull(fillCmd);
        }

        [TestMethod]
        public void Svg_Read_Polyline()
        {
            var scene = ReadSvgWithRoot(100, 100, "<polyline points=\"60,60 70,70 80,60\" stroke=\"blue\"/>");
            var drawCmd = FindCommand<DrawPathCommand>(scene);
            Assert.IsNotNull(drawCmd);
        }

        [TestMethod]
        public void Svg_Read_Path_M_L_C_Z()
        {
            var scene = ReadSvgWithRoot(100, 100, "<path d=\"M 0 0 L 100 0 C 100 50 50 100 0 100 Z\" fill=\"red\"/>");
            var fillCmd = FindCommand<FillPathCommand>(scene);
            Assert.IsNotNull(fillCmd);
            Assert.IsTrue(fillCmd.Path.PointCount >= 4);
        }

        [TestMethod]
        public void Svg_Read_Path_Relative()
        {
            var scene = ReadSvgWithRoot(100, 100, "<path d=\"m 10 10 l 80 0 l 0 80 z\" fill=\"red\"/>");
            var fillCmd = FindCommand<FillPathCommand>(scene);
            Assert.IsNotNull(fillCmd);
            var pts = fillCmd.Path.PathPoints;
            Assert.IsTrue(pts.Length >= 3);
            Assert.AreEqual(10f, pts[0].X, 0.01f);
        }

        [TestMethod]
        public void Svg_Read_Transform_Group()
        {
            var scene = ReadSvgWithRoot(100, 100, "<g transform=\"translate(10,20)\"><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"red\"/></g>");
            Assert.IsTrue(scene.Commands.Count >= 2);
        }

        [TestMethod]
        public void Svg_Read_LinearGradient()
        {
            var scene = ReadSvgWithRoot(100, 100, "<defs><linearGradient id=\"g1\" x1=\"0\" y1=\"0\" x2=\"100\" y2=\"100\" gradientUnits=\"userSpaceOnUse\"><stop offset=\"0\" stop-color=\"red\"/><stop offset=\"1\" stop-color=\"blue\"/></linearGradient></defs><rect x=\"0\" y=\"0\" width=\"100\" height=\"100\" fill=\"url(#g1)\"/>");
            var fillCmd = FindCommand<FillPathCommand>(scene);
            Assert.IsNotNull(fillCmd);
            Assert.IsInstanceOfType(fillCmd.Brush, typeof(LinearGradientBrush));
        }

        [TestMethod]
        public void Svg_Read_ClipPath()
        {
            var scene = ReadSvgWithRoot(100, 100, "<defs><clipPath id=\"c1\"><rect x=\"0\" y=\"0\" width=\"50\" height=\"50\"/></clipPath></defs><g clip-path=\"url(#c1)\"><rect x=\"0\" y=\"0\" width=\"100\" height=\"100\" fill=\"red\"/></g>");
            Assert.IsTrue(scene.Commands.Count > 0);
        }

        [TestMethod]
        public void Svg_Read_Use()
        {
            var scene = ReadSvgWithRoot(100, 100, "<defs><rect id=\"r1\" x=\"0\" y=\"0\" width=\"20\" height=\"20\" fill=\"red\"/></defs><use href=\"#r1\" x=\"10\" y=\"10\"/>");
            Assert.IsTrue(scene.Commands.Count > 0);
        }

        [TestMethod]
        public void Svg_Read_Style_Attr()
        {
            var scene = ReadSvgWithRoot(100, 100, "<rect x=\"0\" y=\"0\" width=\"50\" height=\"50\" style=\"fill:red;stroke:blue\"/>");
            var fillCmd = FindCommand<FillPathCommand>(scene);
            Assert.IsNotNull(fillCmd);
            Assert.IsInstanceOfType(fillCmd.Brush, typeof(SolidBrush));
            var sb = (SolidBrush)fillCmd.Brush;
            Assert.AreEqual(255, sb.Color.R);
        }

        [TestMethod]
        public void Svg_Read_Text()
        {
            var scene = ReadSvgWithRoot(200, 100, "<text x=\"10\" y=\"50\" font-family=\"Arial\" font-size=\"16\" fill=\"black\">Hello</text>");
            var textCmd = FindCommand<DrawStringCommand>(scene);
            Assert.IsNotNull(textCmd);
            Assert.AreEqual("Hello", textCmd.Text);
        }

        [TestMethod]
        public void Svg_Read_UnknownElement_Skipped()
        {
            var scene = ReadSvgWithRoot(100, 100, "<fooBar unknown=\"true\"/><rect x=\"0\" y=\"0\" width=\"10\" height=\"10\" fill=\"red\"/>");
            var fillCmd = FindCommand<FillPathCommand>(scene);
            Assert.IsNotNull(fillCmd, "Rect after unknown element should still be parsed");
        }

        [TestMethod]
        public void Svg_Read_MalformedPath_PartialParse()
        {
            var scene = ReadSvgWithRoot(100, 100, "<path d=\"M 0 0 L 50 0 XYZBAD\" fill=\"red\"/>");
            var fillCmd = FindCommand<FillPathCommand>(scene);
            Assert.IsNotNull(fillCmd, "Should parse up to the error");
            Assert.IsTrue(fillCmd.Path.PointCount >= 2);
        }

        [TestMethod]
        public void Svg_Read_RoundTrip()
        {
            int w = 60, h = 60;
            var scene = new VectorScene { Width = w, Height = h };
            using (var rec = new RecordingGraphics(scene, w, h))
            {
                rec.Clear(Color.White);
                using var brush = new SolidBrush(Color.Red);
                rec.FillRectangle(brush, new RectangleF(5, 5, 20, 20));
            }
            using var ms = new MemoryStream();
            SvgWriter.Write(ms, scene, SvgEncoderParameters.Default);
            ms.Position = 0;
            var readScene = SvgReader.Read(ms);
            Assert.AreEqual(w, readScene.Width);
            Assert.AreEqual(h, readScene.Height);
            Assert.IsTrue(readScene.Commands.Count > 0);

            var originalBmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(originalBmp))
                VectorScenePlayer.Play(scene, g);

            var readBmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(readBmp))
                VectorScenePlayer.Play(readScene, g);

            AssertBitmapsEqual(originalBmp, readBmp, tolerance: 5);
        }

        private static T? FindCommand<T>(VectorScene scene) where T : DrawingCommand
        {
            foreach (var cmd in scene.Commands)
                if (cmd is T t) return t;
            return null;
        }

        private static System.Collections.Generic.List<T> FindAllCommands<T>(VectorScene scene) where T : DrawingCommand
        {
            var list = new System.Collections.Generic.List<T>();
            foreach (var cmd in scene.Commands)
                if (cmd is T t) list.Add(t);
            return list;
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