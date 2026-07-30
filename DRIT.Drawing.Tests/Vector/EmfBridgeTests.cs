using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DRIT.Drawing;
using DRIT.Drawing.Brushes;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Metafile;
using DRIT.Drawing.Metafile.Emf.Records;
using DRIT.Drawing.Metafile.Emf.Records.DrawingRecordTypes;
using DRIT.Drawing.Metafile.Emf.Records.ObjectRecordTypes;
using DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes;
using DRIT.Drawing.Metafile.Wmf.Objects;
using DRIT.Drawing.Pens;
using DRIT.Drawing.Rendering;
using DRIT.Drawing.Vector;
using DRIT.Drawing.Vector.Commands;
using DRIT.Drawing.Vector.Emf;
using Pen = DRIT.Drawing.Pens.Pen;

namespace DRIT.Drawing.Tests.Vector
{
    [TestClass]
    public class EmfBridgeTests
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

        private static Bitmap RasterizeEmfDocument(MetafileDocument doc, int width, int height)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var bounds = new Rectangle(0, 0, width, height);
            EmfPlayer.Render(doc, bmp, bounds);
            return bmp;
        }

        private static Bitmap RasterizeEmfBytes(byte[] emfBytes, int width, int height)
        {
            using var ms = new MemoryStream(emfBytes);
            var reader = new MetafileReader();
            var doc = reader.Read(ms);
            return RasterizeEmfDocument(doc, width, height);
        }

        private static byte[] SerializeEmfDocument(MetafileDocument doc)
        {
            var writer = new MetafileWriter();
            return writer.WriteToBytes(doc);
        }

        private static void AssertBitmapsEqual(Bitmap expected, Bitmap actual, int tolerance = 3)
        {
            Assert.AreEqual(expected.Width, actual.Width, "Width mismatch");
            Assert.AreEqual(expected.Height, actual.Height, "Height mismatch");
            for (int y = 0; y < expected.Height; y++)
            for (int x = 0; x < expected.Width; x++)
            {
                Color e = expected.GetPixel(x, y);
                Color a = actual.GetPixel(x, y);
                Assert.IsTrue(Math.Abs(e.A - a.A) <= tolerance, $"Alpha at ({x},{y}): {e} vs {a}");
                Assert.IsTrue(Math.Abs(e.R - a.R) <= tolerance, $"Red at ({x},{y}): {e} vs {a}");
                Assert.IsTrue(Math.Abs(e.G - a.G) <= tolerance, $"Green at ({x},{y}): {e} vs {a}");
                Assert.IsTrue(Math.Abs(e.B - a.B) <= tolerance, $"Blue at ({x},{y}): {e} vs {a}");
            }
        }

        private static MetafileDocument CreateEmfDocumentWithRectangle(int w, int h, RectangleF rect, Color penColor, Color brushColor)
        {
            var header = new EMR_HEADER
            {
                HeaderObject = new Metafile.Emf.Objects.Header
                {
                    Bounds = new RectL { Left = 0, Top = 0, Right = w, Bottom = h },
                    Frame = new RectL { Left = 0, Top = 0, Right = w * 100, Bottom = h * 100 },
                    RecordSignature = 0x464D4520,
                    Version = 0x00010000,
                    Records = 0,
                    Handles = 2,
                    Device = new SizeL { cx = (uint)w, cy = (uint)h },
                    Millimeters = new SizeL { cx = (uint)Math.Max(1, w * 3), cy = (uint)Math.Max(1, h * 3) },
                },
            };

            var createPen = new EMR_CREATEPEN
            {
                IhPen = 1,
                Pen = new Metafile.Wmf.Objects.Pen
                {
                    PenStyle = DRIT.Drawing.Metafile.Wmf.Enumerations.PenStyle.PS_SOLID,
                    Width = new PointS { x = 1, y = 0 },
                    ColorRef = new ColorRef { Red = penColor.R, Green = penColor.G, Blue = penColor.B, Reserved = 0 },
                },
            };

            var createBrush = new EMR_CREATEBRUSHINDIRECT
            {
                IhBrush = 2,
                LogBrush = new LogBrush
                {
                    BrushStyle = DRIT.Drawing.Metafile.Wmf.Enumerations.BrushStyle.BS_SOLID,
                    ColorRef = new ColorRef { Red = brushColor.R, Green = brushColor.G, Blue = brushColor.B, Reserved = 0 },
                    BrushHatch = DRIT.Drawing.Metafile.Wmf.Enumerations.HatchStyle.HS_HORIZONTAL,
                },
            };

            var selectPen = new EMR_SELECTOBJECT { ObjectIndex = 1 };
            var selectBrush = new EMR_SELECTOBJECT { ObjectIndex = 2 };

            var rectangleRecord = new EMR_RECTANGLE
            {
                Box = new RectL
                {
                    Left = (int)rect.X,
                    Top = (int)rect.Y,
                    Right = (int)(rect.X + rect.Width),
                    Bottom = (int)(rect.Y + rect.Height),
                },
            };

            var eof = new EMR_EOF { NumberOfRecords = 7 };

            return new MetafileDocument
            {
                Format = MetafileFormat.Emf,
                EmfRecords = new List<Record>
                {
                    header, createPen, createBrush, selectPen, selectBrush, rectangleRecord, eof,
                },
            };
        }

        // ── EMF → VDM tests ────────────────────────────────────────────────────

        [TestMethod]
        public void Emf_Play_ToRecording_EqualsDirectRaster()
        {
            int w = 50, h = 50;
            var doc = CreateEmfDocumentWithRectangle(w, h, new RectangleF(5, 5, 30, 30), Color.Red, Color.Blue);

            var directBmp = RasterizeEmfDocument(doc, w, h);

            var scene = new VectorScene { Width = w, Height = h };
            using (var rec = new RecordingGraphics(scene, w, h))
            {
                EmfPlayer.Play(doc, rec, new Rectangle(0, 0, w, h));
            }

            var replayBmp = ReplayToBitmap(scene);

            AssertBitmapsEqual(directBmp, replayBmp, tolerance: 5);
        }

        [TestMethod]
        public void Emf_Play_RecordsCorrectCommands()
        {
            int w = 20, h = 20;
            var doc = CreateEmfDocumentWithRectangle(w, h, new RectangleF(2, 2, 10, 10), Color.Black, Color.Red);

            var scene = new VectorScene { Width = w, Height = h };
            using (var rec = new RecordingGraphics(scene, w, h))
            {
                EmfPlayer.Play(doc, rec, new Rectangle(0, 0, w, h));
            }

            Assert.IsTrue(scene.Commands.Count > 0, "Scene should have recorded commands");
            Assert.IsInstanceOfType(scene.Commands[0], typeof(ClearCommand));
        }

        // ── VDM → EMF tests ────────────────────────────────────────────────────

        [TestMethod]
        public void Vdm_ToEmf_PenBrushObjects()
        {
            using var rec = CreateRecording(50, 50, out var scene);
            using var pen = new Pen(Color.Red, 2f);
            using var brush = new SolidBrush(Color.Blue);

            rec.DrawRectangle(pen, new RectangleF(5, 5, 20, 20));
            rec.FillRectangle(brush, new RectangleF(10, 10, 15, 15));

            using var ctx = new EmfDrawingContext(50, 50);
            VectorScenePlayer.Play(scene, ctx);
            var doc = ctx.ToDocument();

            bool hasCreatePen = doc.EmfRecords.Any(r => r is EMR_CREATEPEN);
            bool hasCreateBrush = doc.EmfRecords.Any(r => r is EMR_CREATEBRUSHINDIRECT);
            bool hasSelectObject = doc.EmfRecords.Any(r => r is EMR_SELECTOBJECT);

            Assert.IsTrue(hasCreatePen, "EMF output should contain EMR_CREATEPEN");
            Assert.IsTrue(hasCreateBrush, "EMF output should contain EMR_CREATEBRUSHINDIRECT");
            Assert.IsTrue(hasSelectObject, "EMF output should contain EMR_SELECTOBJECT");
        }

        [TestMethod]
        public void Vdm_ToEmf_Transform()
        {
            using var rec = CreateRecording(50, 50, out var scene);
            rec.TranslateTransform(10f, 5f);

            using var ctx = new EmfDrawingContext(50, 50);
            VectorScenePlayer.Play(scene, ctx);
            var doc = ctx.ToDocument();

            bool hasWorldTransform = doc.EmfRecords.Any(r => r is EMR_SETWORLDTRANSFORM);
            Assert.IsTrue(hasWorldTransform, "EMF output should contain EMR_SETWORLDTRANSFORM");
        }

        [TestMethod]
        public void Vdm_ToEmf_StateStack()
        {
            using var rec = CreateRecording(50, 50, out var scene);
            var state = rec.Save();
            rec.Restore(state);

            using var ctx = new EmfDrawingContext(50, 50);
            VectorScenePlayer.Play(scene, ctx);
            var doc = ctx.ToDocument();

            bool hasSaveDC = doc.EmfRecords.Any(r => r is EMR_SAVEDC);
            bool hasRestoreDC = doc.EmfRecords.Any(r => r is EMR_RESTOREDC);

            Assert.IsTrue(hasSaveDC, "EMF output should contain EMR_SAVEDC");
            Assert.IsTrue(hasRestoreDC, "EMF output should contain EMR_RESTOREDC");
        }

        [TestMethod]
        public void Vdm_ToEmf_Clip()
        {
            using var rec = CreateRecording(50, 50, out var scene);
            rec.SetClip(new RectangleF(0, 0, 20, 20));

            using var ctx = new EmfDrawingContext(50, 50);
            VectorScenePlayer.Play(scene, ctx);
            var doc = ctx.ToDocument();

            bool hasIntersectClip = doc.EmfRecords.Any(r => r is EMR_INTERSECTCLIPRECT);
            Assert.IsTrue(hasIntersectClip, "EMF output should contain EMR_INTERSECTCLIPRECT");
        }

        [TestMethod]
        public void Vdm_ToEmF_PathRecords()
        {
            using var rec = CreateRecording(50, 50, out var scene);
            using var pen = new Pen(Color.Black, 1f);
            rec.DrawLine(pen, 5, 5, 25, 25);

            using var ctx = new EmfDrawingContext(50, 50);
            VectorScenePlayer.Play(scene, ctx);
            var doc = ctx.ToDocument();

            bool hasBeginPath = doc.EmfRecords.Any(r => r is EMR_BEGINPATH);
            bool hasEndPath = doc.EmfRecords.Any(r => r is EMR_ENDPATH);
            bool hasStrokePath = doc.EmfRecords.Any(r => r is EMR_STROKEPATH);

            Assert.IsTrue(hasBeginPath, "EMF output should contain EMR_BEGINPATH");
            Assert.IsTrue(hasEndPath, "EMF output should contain EMR_ENDPATH");
            Assert.IsTrue(hasStrokePath, "EMF output should contain EMR_STROKEPATH");
        }

        [TestMethod]
        public void Vdm_ToEmf_HasHeaderAndEof()
        {
            using var rec = CreateRecording(30, 30, out var scene);
            rec.Clear(Color.White);

            using var ctx = new EmfDrawingContext(30, 30);
            VectorScenePlayer.Play(scene, ctx);
            var doc = ctx.ToDocument();

            Assert.IsTrue(doc.EmfRecords.Count > 0, "Document should have records");
            Assert.IsInstanceOfType(doc.EmfRecords[0], typeof(EMR_HEADER), "First record should be EMR_HEADER");

            var lastRecord = doc.EmfRecords[doc.EmfRecords.Count - 1];
            Assert.IsInstanceOfType(lastRecord, typeof(EMR_EOF), "Last record should be EMR_EOF");

            var header = (EMR_HEADER)doc.EmfRecords[0];
            Assert.AreEqual(30, header.HeaderObject.Bounds.Right, "Header bounds width mismatch");
            Assert.AreEqual(30, header.HeaderObject.Bounds.Bottom, "Header bounds height mismatch");
        }

        [TestMethod]
        public void Vdm_ToEmf_SerializationRoundTrip()
        {
            using var rec = CreateRecording(40, 40, out var scene);
            using var brush = new SolidBrush(Color.Green);
            rec.FillRectangle(brush, new RectangleF(5, 5, 20, 20));

            using var ctx = new EmfDrawingContext(40, 40);
            VectorScenePlayer.Play(scene, ctx);
            var doc = ctx.ToDocument();

            byte[] emfBytes = SerializeEmfDocument(doc);
            Assert.IsTrue(emfBytes.Length > 0, "Serialized EMF should be non-empty");

            var reader = new MetafileReader();
            using var ms = new MemoryStream(emfBytes);
            var readDoc = reader.Read(ms);

            Assert.AreEqual(MetafileFormat.Emf, readDoc.Format, "Read-back format should be EMF");
            Assert.IsTrue(readDoc.EmfRecords.Count > 0, "Read-back document should have records");
            Assert.IsInstanceOfType(readDoc.EmfRecords[0], typeof(EMR_HEADER), "First read record should be EMR_HEADER");
        }

        [TestMethod]
        public void Vdm_ToEmf_ToRaster_EqualsVdmToRaster()
        {
            using var rec = CreateRecording(40, 40, out var scene);
            using var brush = new SolidBrush(Color.Red);
            rec.Clear(Color.White);
            rec.FillRectangle(brush, new RectangleF(5, 5, 20, 20));

            var directRaster = ReplayToBitmap(scene);

            using var ctx = new EmfDrawingContext(40, 40);
            VectorScenePlayer.Play(scene, ctx);
            var doc = ctx.ToDocument();

            byte[] emfBytes = SerializeEmfDocument(doc);
            var emfRaster = RasterizeEmfBytes(emfBytes, 40, 40);

            AssertBitmapsEqual(directRaster, emfRaster, tolerance: 10);
        }

        // ── Regression: existing metafile tests still pass ─────────────────────

        [TestMethod]
        public void Emf_Regression_ExistingMetafileTestsStillPass()
        {
            var doc = CreateEmfDocumentWithRectangle(20, 20, new RectangleF(2, 2, 10, 10), Color.Red, Color.Blue);
            var bmp = RasterizeEmfDocument(doc, 20, 20);

            Assert.AreEqual(20, bmp.Width);
            Assert.AreEqual(20, bmp.Height);
        }

        [TestMethod]
        public void Emf_Regression_SetPixelFixture()
        {
            int w = 16, h = 16, x = 6, y = 7;
            var color = Color.FromArgb(255, 30, 140, 220);

            var header = new EMR_HEADER
            {
                HeaderObject = new Metafile.Emf.Objects.Header
                {
                    Bounds = new RectL { Left = 0, Top = 0, Right = w, Bottom = h },
                    Frame = new RectL { Left = 0, Top = 0, Right = w * 100, Bottom = h * 100 },
                    RecordSignature = 0x464D4520,
                    Version = 0x00010000,
                    Records = 3,
                    Handles = 1,
                    Device = new SizeL { cx = (uint)w, cy = (uint)h },
                    Millimeters = new SizeL { cx = (uint)Math.Max(1, w * 3), cy = (uint)Math.Max(1, h * 3) },
                },
            };

            var pixelRecord = new EMR_SETPIXELV
            {
                Position = new PointL { x = x, y = y },
                Color = new ColorRef { Red = color.R, Green = color.G, Blue = color.B, Reserved = 0 },
            };

            var eofRecord = new EMR_EOF { NumberOfRecords = 3 };

            var doc = new MetafileDocument
            {
                Format = MetafileFormat.Emf,
                EmfRecords = new List<Record> { header, pixelRecord, eofRecord },
            };

            var bmp = RasterizeEmfDocument(doc, w, h);
            Color pixel = bmp.GetPixel(x, y);

            Assert.AreEqual(30, pixel.R, 2);
            Assert.AreEqual(140, pixel.G, 2);
            Assert.AreEqual(220, pixel.B, 2);
        }
    }
}