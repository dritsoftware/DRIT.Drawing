using System;
using DRIT.Drawing;
using DRIT.Drawing.Brushes;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Pens;
using DRIT.Drawing.Vector;
using DRIT.Drawing.Vector.Commands;

namespace DRIT.Drawing.Tests.Vector
{
    [TestClass]
    public class VectorSceneTests
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        private static RecordingGraphics CreateRecording(int w, int h, out VectorScene scene)
        {
            scene = new VectorScene { Width = w, Height = h };
            return new RecordingGraphics(scene, w, h);
        }

        private static Bitmap ReplayToBitmap(VectorScene scene)
        {
            var bmp = new Bitmap(scene.Width, scene.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            VectorScenePlayer.Play(scene, g);
            return bmp;
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

        private static Bitmap DrawDirect(int w, int h, Action<Graphics> draw)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            draw(g);
            return bmp;
        }

        // ── Primitive replay tests ─────────────────────────────────────────────

        [TestMethod]
        public void Record_DrawPath_ReplaysIdentically()
        {
            using var rec = CreateRecording(30, 30, out var scene);
            using var pen = new Pen(Color.Red, 2f);

            rec.DrawLine(pen, 2f, 5f, 25f, 5f);
            rec.DrawEllipse(pen, new RectangleF(2, 10, 20, 15));

            var expected = DrawDirect(30, 30, g =>
            {
                using var p = new Pen(Color.Red, 2f);
                g.DrawLine(p, 2f, 5f, 25f, 5f);
                g.DrawEllipse(p, new RectangleF(2, 10, 20, 15));
            });

            using var actual = ReplayToBitmap(scene);
            AssertBitmapsEqual(expected, actual);
        }

        [TestMethod]
        public void Record_FillPath_ReplaysIdentically()
        {
            using var rec = CreateRecording(30, 30, out var scene);
            using var brush = new SolidBrush(Color.Blue);

            rec.FillRectangle(brush, new RectangleF(2, 2, 10, 10));
            rec.FillEllipse(brush, new RectangleF(15, 15, 12, 12));

            var expected = DrawDirect(30, 30, g =>
            {
                using var b = new SolidBrush(Color.Blue);
                g.FillRectangle(b, new RectangleF(2, 2, 10, 10));
                g.FillEllipse(b, new RectangleF(15, 15, 12, 12));
            });

            using var actual = ReplayToBitmap(scene);
            AssertBitmapsEqual(expected, actual);
        }

        [TestMethod]
        public void Record_DrawImage_ReplaysIdentically()
        {
            using var src = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
            src.SetPixel(0, 0, Color.Red);
            src.SetPixel(3, 3, Color.Green);

            using var rec = CreateRecording(20, 20, out var scene);
            rec.DrawImage(src, new RectangleF(0, 0, 20, 20));

            var expected = DrawDirect(20, 20, g =>
            {
                g.DrawImage(src, new RectangleF(0, 0, 20, 20));
            });

            using var actual = ReplayToBitmap(scene);
            AssertBitmapsEqual(expected, actual);
        }

        [TestMethod]
        public void Record_Clear_ReplaysIdentically()
        {
            using var rec = CreateRecording(10, 10, out var scene);
            rec.Clear(Color.FromArgb(255, 10, 20, 30));

            var expected = DrawDirect(10, 10, g =>
            {
                g.Clear(Color.FromArgb(255, 10, 20, 30));
            });

            using var actual = ReplayToBitmap(scene);
            AssertBitmapsEqual(expected, actual);
        }

        // ── State stack tests ──────────────────────────────────────────────────

        [TestMethod]
        public void Record_TransformStack_RestoresCorrectly()
        {
            using var rec = CreateRecording(30, 30, out var scene);
            using var brush = new SolidBrush(Color.Lime);

            rec.FillRectangle(brush, new RectangleF(0, 0, 5, 5));
            var state = rec.Save();
            rec.TranslateTransform(10f, 0f);
            rec.FillRectangle(brush, new RectangleF(0, 0, 5, 5));
            rec.Restore(state);
            rec.FillRectangle(brush, new RectangleF(0, 0, 5, 5));

            var expected = DrawDirect(30, 30, g =>
            {
                using var b = new SolidBrush(Color.Lime);
                g.FillRectangle(b, new RectangleF(0, 0, 5, 5));
                var s = g.Save();
                g.TranslateTransform(10f, 0f);
                g.FillRectangle(b, new RectangleF(0, 0, 5, 5));
                g.Restore(s);
                g.FillRectangle(b, new RectangleF(0, 0, 5, 5));
            });

            using var actual = ReplayToBitmap(scene);
            AssertBitmapsEqual(expected, actual);
        }

        [TestMethod]
        public void Record_ClipStack_RestoresCorrectly()
        {
            using var rec = CreateRecording(30, 30, out var scene);
            using var brush = new SolidBrush(Color.Red);

            rec.SetClip(new RectangleF(0, 0, 10, 10));
            rec.FillRectangle(brush, new RectangleF(0, 0, 30, 30));
            var state = rec.Save();
            rec.SetClip(new RectangleF(20, 20, 10, 10));
            rec.FillRectangle(brush, new RectangleF(0, 0, 30, 30));
            rec.Restore(state);
            rec.FillRectangle(brush, new RectangleF(0, 0, 30, 30));

            var expected = DrawDirect(30, 30, g =>
            {
                using var b = new SolidBrush(Color.Red);
                g.SetClip(new RectangleF(0, 0, 10, 10));
                g.FillRectangle(b, new RectangleF(0, 0, 30, 30));
                var s = g.Save();
                g.SetClip(new RectangleF(20, 20, 10, 10));
                g.FillRectangle(b, new RectangleF(0, 0, 30, 30));
                g.Restore(s);
                g.FillRectangle(b, new RectangleF(0, 0, 30, 30));
            });

            using var actual = ReplayToBitmap(scene);
            AssertBitmapsEqual(expected, actual);
        }

        [TestMethod]
        public void Record_Container_Nested()
        {
            using var rec = CreateRecording(40, 40, out var scene);
            using var brush = new SolidBrush(Color.Cyan);

            rec.FillRectangle(brush, new RectangleF(0, 0, 5, 5));
            var c1 = rec.BeginContainer();
            rec.TranslateTransform(10f, 0f);
            rec.FillRectangle(brush, new RectangleF(0, 0, 5, 5));
            var c2 = rec.BeginContainer();
            rec.TranslateTransform(0f, 10f);
            rec.FillRectangle(brush, new RectangleF(0, 0, 5, 5));
            rec.EndContainer(c2);
            rec.EndContainer(c1);
            rec.FillRectangle(brush, new RectangleF(0, 0, 5, 5));

            var expected = DrawDirect(40, 40, g =>
            {
                using var b = new SolidBrush(Color.Cyan);
                g.FillRectangle(b, new RectangleF(0, 0, 5, 5));
                var c1 = g.BeginContainer();
                g.TranslateTransform(10f, 0f);
                g.FillRectangle(b, new RectangleF(0, 0, 5, 5));
                var c2 = g.BeginContainer();
                g.TranslateTransform(0f, 10f);
                g.FillRectangle(b, new RectangleF(0, 0, 5, 5));
                g.EndContainer(c2);
                g.EndContainer(c1);
                g.FillRectangle(b, new RectangleF(0, 0, 5, 5));
            });

            using var actual = ReplayToBitmap(scene);
            AssertBitmapsEqual(expected, actual);
        }

        // ── Cloning / immutability tests ───────────────────────────────────────

        [TestMethod]
        public void Record_ClonesState()
        {
            using var rec = CreateRecording(20, 20, out var scene);
            using var pen = new Pen(Color.Red, 3f);
            using var brush = new SolidBrush(Color.Blue);

            rec.DrawLine(pen, 0, 0, 15, 15);
            rec.FillRectangle(brush, new RectangleF(0, 0, 8, 8));

            // Mutate the source pen/brush after recording
            pen.Color = Color.Green;
            brush.GetType().GetProperty("Color")?.SetValue(brush, Color.Yellow);

            var expected = DrawDirect(20, 20, g =>
            {
                using var p = new Pen(Color.Red, 3f);
                using var b = new SolidBrush(Color.Blue);
                g.DrawLine(p, 0, 0, 15, 15);
                g.FillRectangle(b, new RectangleF(0, 0, 8, 8));
            });

            using var actual = ReplayToBitmap(scene);
            AssertBitmapsEqual(expected, actual);
        }

        [TestMethod]
        public void Record_MultipleReplays_Idempotent()
        {
            using var rec = CreateRecording(20, 20, out var scene);
            using var brush = new SolidBrush(Color.Magenta);
            using var pen = new Pen(Color.White, 1f);

            rec.FillEllipse(brush, new RectangleF(2, 2, 15, 15));
            rec.DrawRectangle(pen, new RectangleF(2, 2, 15, 15));

            using var bmp1 = ReplayToBitmap(scene);
            using var bmp2 = ReplayToBitmap(scene);
            using var bmp3 = ReplayToBitmap(scene);

            AssertBitmapsEqual(bmp1, bmp2);
            AssertBitmapsEqual(bmp2, bmp3);
        }

        // ── Graphics unsealed test ─────────────────────────────────────────────

        [TestMethod]
        public void Graphics_Unsealed_SubclassWorks()
        {
            using var bmp = new Bitmap(10, 10, PixelFormat.Format32bppArgb);
            using var g = new TestGraphics(bmp);
            using var pen = new Pen(Color.Red, 1f);

            g.DrawLine(pen, 0, 0, 9, 9);

            Assert.AreEqual(1, g.DrawPathCoreCallCount);
        }

        private sealed class TestGraphics : Graphics
        {
            public int DrawPathCoreCallCount;

            public TestGraphics(Bitmap target) : base(target) { }

            protected override void DrawPathCore(Pen pen, GraphicsPath path)
            {
                DrawPathCoreCallCount++;
            }
        }

        // ── Scene structure tests ──────────────────────────────────────────────

        [TestMethod]
        public void Scene_RecordsCorrectCommandTypes()
        {
            using var rec = CreateRecording(20, 20, out var scene);
            using var pen = new Pen(Color.Red);
            using var brush = new SolidBrush(Color.Blue);

            rec.Clear(Color.White);
            rec.DrawLine(pen, 0, 0, 10, 10);
            rec.FillRectangle(brush, new RectangleF(0, 0, 5, 5));

            Assert.AreEqual(3, scene.Commands.Count);
            Assert.IsInstanceOfType(scene.Commands[0], typeof(ClearCommand));
            Assert.IsInstanceOfType(scene.Commands[1], typeof(DrawPathCommand));
            Assert.IsInstanceOfType(scene.Commands[2], typeof(FillPathCommand));
        }

        [TestMethod]
        public void Scene_TransformRecordsCommand()
        {
            using var rec = CreateRecording(20, 20, out var scene);

            rec.TranslateTransform(5f, 3f);
            rec.ScaleTransform(2f, 2f);

            // Each transform mutator records a SetTransformCommand
            Assert.AreEqual(2, scene.Commands.Count);
            Assert.IsInstanceOfType(scene.Commands[0], typeof(SetTransformCommand));
            Assert.IsInstanceOfType(scene.Commands[1], typeof(SetTransformCommand));
        }

        [TestMethod]
        public void Scene_SaveRestoreRecordsCommands()
        {
            using var rec = CreateRecording(20, 20, out var scene);

            var state = rec.Save();
            rec.Restore(state);

            Assert.AreEqual(2, scene.Commands.Count);
            Assert.IsInstanceOfType(scene.Commands[0], typeof(SaveStateCommand));
            Assert.IsInstanceOfType(scene.Commands[1], typeof(RestoreStateCommand));
        }

        [TestMethod]
        public void Scene_Clone_ProducesIndependentCopy()
        {
            using var rec = CreateRecording(20, 20, out var scene);
            using var brush = new SolidBrush(Color.Red);

            rec.FillRectangle(brush, new RectangleF(0, 0, 5, 5));

            var clone = scene.Clone();

            Assert.AreEqual(scene.Width, clone.Width);
            Assert.AreEqual(scene.Height, clone.Height);
            Assert.AreEqual(scene.Commands.Count, clone.Commands.Count);
            Assert.AreNotSame(scene.Commands[0], clone.Commands[0]);
        }
    }
}