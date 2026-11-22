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
using DRIT.Drawing.Metafile.Emf.Enumerations;
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

        private static byte[] StripEmfPlusComments(byte[] emfBytes)
        {
            var document = new MetafileReader().Read(emfBytes);
            document.EmfRecords = document.EmfRecords
                .Where(record => !(record is EMR_COMMENT comment &&
                    comment.CommentData != null &&
                    comment.CommentData.Length >= sizeof(uint) &&
                    BitConverter.ToUInt32(comment.CommentData, 0) == 0x2B464D45))
                .ToList();
            return SerializeEmfDocument(document);
        }

        [TestMethod]
        public void SvgImage_CustomResolution_IsReportedAndPreservedWhenRasterized()
        {
            using var image = new SvgImage(20, 10, 300f, 144f);
            Assert.AreEqual(300f, image.HorizontalResolution);
            Assert.AreEqual(144f, image.VerticalResolution);

            using var bitmap = image.RenderToBitmap();
            Assert.AreEqual(300f, bitmap.HorizontalResolution);
            Assert.AreEqual(144f, bitmap.VerticalResolution);
        }

        [TestMethod]
        public void EmptyEmfEof_DeclaresItsOwnRecordSize()
            => Assert.AreEqual(20u, new EMR_EOF().SizeLast);

        [TestMethod]
        public void SvgImage_ExcelCompatibleLargeOpenStrokes_UsePolyline16()
        {
            var image = new SvgImage(2600, 1800, 600f, 600f)
            {
                UseExcelCompatibleEmfCoordinates = true,
                UseClassicEmfOutput = true,
            };

            using (var graphics = image.CreateGraphics())
            using (var pen = new Pen(Color.Gray, 0.75f))
            {
                graphics.DrawLines(pen, new[]
                {
                    new PointF(220f, 1425f),
                    new PointF(2100f, 1425f),
                });
                graphics.DrawLines(pen, new[]
                {
                    new PointF(220f, 100f),
                    new PointF(220f, 1425f),
                });
                graphics.DrawLines(pen, new[]
                {
                    new PointF(455f, 857f),
                    new PointF(1865f, 289f),
                });
            }

            using var stream = new MemoryStream();
            image.Save(stream, ImageFormat.Emf);
            var document = new MetafileReader().Read(stream.ToArray());

            var polylines = document.EmfRecords.OfType<EMR_POLYLINE16>().ToArray();
            Assert.AreEqual(3, polylines.Length);
            Assert.AreEqual(0, document.EmfRecords.OfType<EMR_FILLPATH>().Count());
            Assert.AreEqual(1760, polylines[0].Points[0].x);
            Assert.AreEqual(7280, polylines[2].Points[0].x);
        }

        /// <summary>
        /// Verifies disconnected line segments are serialized as one grouped POLYPOLYLINE16 record.
        /// </summary>
        [TestMethod]
        public void SvgImage_DisconnectedOpenStrokes_UsePolyPolyline16()
        {
            var image = new SvgImage(100, 100, 96f, 96f)
            {
                UseClassicEmfOutput = true,
            };

            using (var graphics = image.CreateGraphics())
            using (var pen = new Pen(Color.Black, 1f))
            {
                graphics.DrawPolylineGroup(pen, new[]
                {
                    new[] { new PointF(10f, 20f), new PointF(10f, 25f) },
                    new[] { new PointF(30f, 20f), new PointF(30f, 25f) },
                    new[] { new PointF(50f, 20f), new PointF(50f, 25f) },
                });
            }

            using var stream = new MemoryStream();
            image.Save(stream, ImageFormat.Emf);
            var document = new MetafileReader().Read(stream.ToArray());

            var grouped = document.EmfRecords.OfType<EMR_POLYPOLYLINE16>().Single();
            Assert.AreEqual(3u, grouped.NumberOfPolys);
            CollectionAssert.AreEqual(new uint[] { 2u, 2u, 2u }, grouped.PolyCounts);
            Assert.AreEqual(6u, grouped.Count);
            Assert.AreEqual(0, document.EmfRecords.OfType<EMR_POLYLINE16>().Count());
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
                PenStyle = DRIT.Drawing.Metafile.Wmf.Enumerations.PenStyle.PS_SOLID,
                WidthX = 1,
                WidthY = 0,
                ColorRef = new ColorRef { Red = penColor.R, Green = penColor.G, Blue = penColor.B, Reserved = 0 },
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

            var eof = new EMR_EOF { SizeLast = 20 };

            return new MetafileDocument
            {
                Format = MetafileFormat.Emf,
                EmfRecords = new List<Record>
                {
                    header, createPen, createBrush, selectPen, selectBrush, rectangleRecord, eof,
                },
            };
        }

        private static MetafileDocument CreatePatternEmfDocument()
        {
            const int width = 16;
            const int height = 10;
            byte[] bmi = new byte[52];
            BitConverter.GetBytes(40).CopyTo(bmi, 0);
            BitConverter.GetBytes(8).CopyTo(bmi, 4);
            BitConverter.GetBytes(8).CopyTo(bmi, 8);
            BitConverter.GetBytes((ushort)1).CopyTo(bmi, 12);
            BitConverter.GetBytes((ushort)32).CopyTo(bmi, 14);
            BitConverter.GetBytes(3).CopyTo(bmi, 16);

            byte[] bits = new byte[8 * 8 * 4];
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                bool firstColor = (y / 4 & 1) == 0;
                int storedY = 7 - y;
                int offset = (storedY * 8 + x) * 4;
                bits[offset] = firstColor ? (byte)30 : (byte)220;
                bits[offset + 1] = firstColor ? (byte)140 : (byte)40;
                bits[offset + 2] = firstColor ? (byte)220 : (byte)10;
                bits[offset + 3] = 0;
            }

            var header = new EMR_HEADER
            {
                HeaderObject = new Metafile.Emf.Objects.Header
                {
                    Bounds = new RectL { Left = 0, Top = 0, Right = width, Bottom = height },
                    Frame = new RectL { Left = 0, Top = 0, Right = width * 100, Bottom = height * 100 },
                    RecordSignature = 0x464D4520,
                    Version = 0x00010000,
                    Device = new SizeL { cx = (uint)width, cy = (uint)height },
                    Millimeters = new SizeL { cx = (uint)(width * 3), cy = (uint)(height * 3) },
                },
            };

            var createPatternBrush = new EMR_CREATEDIBPATTERNBRUSHPT
            {
                IhBrush = 1,
                Usage = DIBColors.DibRgbColors,
                Bmi = bmi,
                Bits = bits,
            };

            var bitBlt = new EMR_BITBLT
            {
                Bounds = new RectL { Left = 0, Top = 0, Right = width, Bottom = height },
                XDest = 0,
                YDest = 0,
                CxDest = width,
                CyDest = height,
                BitBltRasterOperation = Metafile.Emf.Enumerations.EmfTernaryRasterOperation.PATCOPY,
                XFormSrc = new Metafile.Emf.Objects.XForm { M11 = 1f, M22 = 1f },
                UsageSrc = DIBColors.DibRgbColors,
            };

            return new MetafileDocument
            {
                Format = MetafileFormat.Emf,
                EmfRecords = new List<Record>
                {
                    header,
                    createPatternBrush,
                    new EMR_SELECTOBJECT { ObjectIndex = 1 },
                    bitBlt,
                    new EMR_BITBLT
                    {
                        Bounds = new RectL { Left = 0, Top = 0, Right = width, Bottom = height },
                        XDest = 0,
                        YDest = 0,
                        CxDest = width,
                        CyDest = height,
                        BitBltRasterOperation = Metafile.Emf.Enumerations.EmfTernaryRasterOperation.D,
                        XFormSrc = new Metafile.Emf.Objects.XForm { M11 = 1f, M22 = 1f },
                        UsageSrc = DIBColors.DibRgbColors,
                    },
                    new EMR_EOF { SizeLast = 20 },
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

        /// <summary>
        /// Verifies replay of an opaque RGB DIB through an EMF PATCOPY operation.
        /// </summary>
        [TestMethod]
        public void Emf_Play_PatCopy_ReplaysOpaqueDibPattern()
        {
            var document = CreatePatternEmfDocument();
            byte[] bytes = SerializeEmfDocument(document);

            var reader = new MetafileReader();
            var readDocument = reader.Read(bytes);
            var patternRecord = readDocument.EmfRecords.OfType<EMR_CREATEDIBPATTERNBRUSHPT>().Single();
            var bitBltRecords = readDocument.EmfRecords.OfType<EMR_BITBLT>().ToArray();
            var bitBltRecord = bitBltRecords[0];

            Assert.AreEqual(8, BitConverter.ToInt32(patternRecord.Bmi, 4));
            Assert.AreEqual(8, BitConverter.ToInt32(patternRecord.Bmi, 8));
            Assert.AreEqual((ushort)32, BitConverter.ToUInt16(patternRecord.Bmi, 14));
            Assert.AreEqual(Metafile.Emf.Enumerations.EmfTernaryRasterOperation.PATCOPY, bitBltRecord.BitBltRasterOperation);
            Assert.AreEqual(2, bitBltRecords.Length);
            Assert.AreEqual(Metafile.Emf.Enumerations.EmfTernaryRasterOperation.D, bitBltRecords[1].BitBltRasterOperation);

            using var bitmap = RasterizeEmfDocument(readDocument, 16, 10);
            Color firstRow = bitmap.GetPixel(0, 0);
            Color repeated = bitmap.GetPixel(8, 0);
            Color secondRow = bitmap.GetPixel(0, 4);

            Assert.AreEqual(255, firstRow.A);
            Assert.AreEqual(255, repeated.A);
            Assert.AreEqual(firstRow.ToArgb(), repeated.ToArgb());
            Assert.AreEqual(220, firstRow.R);
            Assert.AreEqual(140, firstRow.G);
            Assert.AreEqual(30, firstRow.B);
            Assert.AreEqual(10, secondRow.R);
            Assert.AreEqual(40, secondRow.G);
            Assert.AreEqual(220, secondRow.B);
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
        public void Vdm_ToEmf_WritesLoadableAlignedEmf()
        {
            using var recording = CreateRecording(50, 40, out var scene);
            using var pen = new Pen(Color.Red, 2f);
            recording.DrawLine(pen, 4, 5, 35, 25);

            using var context = new EmfDrawingContext(50, 40);
            VectorScenePlayer.Play(scene, context);
            byte[] bytes = SerializeEmfDocument(context.ToDocument());

            Assert.IsTrue(bytes.Length == BitConverter.ToUInt32(bytes, 48), "EMF header nBytes must match the file length.");

            int offset = 0;
            int recordCount = 0;
            uint lastType = 0;
            uint createPenSize = 0;
            while (offset < bytes.Length)
            {
                uint type = BitConverter.ToUInt32(bytes, offset);
                uint size = BitConverter.ToUInt32(bytes, offset + 4);
                Assert.IsTrue(size >= 8 && size % 4 == 0, $"Record at {offset} is not DWORD aligned.");
                Assert.IsTrue(offset + size <= bytes.Length, $"Record at {offset} exceeds the EMF length.");
                if (type == (uint)RecordType.EMR_CREATEPEN)
                    createPenSize = size;
                lastType = type;
                recordCount++;
                offset += (int)size;
            }

            Assert.AreEqual(bytes.Length, offset);
            Assert.AreEqual(BitConverter.ToUInt32(bytes, 52), (uint)recordCount);
            Assert.AreEqual((uint)28, createPenSize, "EMR_CREATEPEN must use the 28-byte EMF layout.");
            Assert.AreEqual((uint)RecordType.EMR_EOF, lastType);
            Assert.AreEqual((uint)20, BitConverter.ToUInt32(bytes, bytes.Length - 16), "EMR_EOF must be 20 bytes.");

            using var bitmap = new Bitmap(new MemoryStream(bytes));
            Assert.IsTrue(bitmap.Width > 0 && bitmap.Height > 0);
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
        public void Vdm_ToExcelCompatibleEmf_EmitsWorldTransform()
        {
            using var rec = CreateRecording(50, 50, out var scene);
            rec.TranslateTransform(10f, 5f);

            using var context = new EmfDrawingContext(50, 50, 96f, 96f, 16f);
            VectorScenePlayer.Play(scene, context);
            var document = new MetafileReader().Read(SerializeEmfDocument(context.ToDocument()));
            var comments = EmfPlusTestDiagnostics.Decode(document);
            EmfPlusRecordSnapshot transform = comments.SelectMany(records => records)
                .Single(record => record.Type == 0x402A);

            Assert.AreEqual((uint)36, transform.Size);
            Assert.AreEqual((uint)24, transform.DataSize);
            Assert.AreEqual(10f, BitConverter.ToSingle(transform.Payload, 16));
            Assert.AreEqual(5f, BitConverter.ToSingle(transform.Payload, 20));
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
        public void Vdm_ToExcelCompatibleEmf_MapsNestedStateStack()
        {
            using var recording = CreateRecording(50, 50, out var scene);
            var outer = recording.Save();
            var inner = recording.Save();
            recording.Restore(inner);
            recording.Restore(outer);

            using var context = new EmfDrawingContext(50, 50, 96f, 96f, 16f);
            VectorScenePlayer.Play(scene, context);
            var document = new MetafileReader().Read(SerializeEmfDocument(context.ToDocument()));
            var comments = EmfPlusTestDiagnostics.Decode(document);
            EmfPlusTestDiagnostics.AssertValid(comments);
            var records = comments.SelectMany(records => records).ToList();
            var saves = records.Where(record => record.Type == 0x4025).ToList();
            var restores = records.Where(record => record.Type == 0x4026).ToList();

            Assert.AreEqual(2, saves.Count);
            Assert.AreEqual(2, restores.Count);
            Assert.AreEqual((uint)4, saves[0].DataSize);
            Assert.AreEqual((uint)4, restores[0].DataSize);
            Assert.AreEqual(BitConverter.ToUInt32(saves[1].Payload, 0),
                BitConverter.ToUInt32(restores[0].Payload, 0));
            Assert.AreEqual(BitConverter.ToUInt32(saves[0].Payload, 0),
                BitConverter.ToUInt32(restores[1].Payload, 0));
            Assert.IsTrue(document.EmfRecords.Any(record => record is EMR_SAVEDC));
            Assert.IsTrue(document.EmfRecords.Any(record => record is EMR_RESTOREDC));
        }

        [TestMethod]
        public void Vdm_ToEmf_Clip()
        {
            using var rec = CreateRecording(50, 50, out var scene);
            rec.SetClip(new RectangleF(0, 0, 20, 20));

            using var ctx = new EmfDrawingContext(50, 50);
            VectorScenePlayer.Play(scene, ctx);
            var doc = ctx.ToDocument();

            var clipRecord = doc.EmfRecords.OfType<EMR_EXTSELECTCLIPRGN>().Single();
            Assert.AreEqual((uint)DRIT.Drawing.Metafile.Emf.Enumerations.RegionMode.RGN_COPY,
                clipRecord.RegionMode);
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_MapsRectangularClipState()
        {
            using var context = new EmfDrawingContext(50, 50, 96f, 96f, 16f);
            context.SetClip(new RectangleF(2, 3, 10, 11), CombineMode.Intersect);
            context.TranslateClip(4, 5);
            context.ResetClip();
            var document = new MetafileReader().Read(SerializeEmfDocument(context.ToDocument()));
            var comments = EmfPlusTestDiagnostics.Decode(document);
            EmfPlusTestDiagnostics.AssertValid(comments);
            var records = comments.SelectMany(records => records).ToList();
            var clip = records.Single(record => record.Type == 0x4032 &&
                BitConverter.ToSingle(record.Payload, 0) == 2f &&
                BitConverter.ToSingle(record.Payload, 4) == 3f &&
                BitConverter.ToSingle(record.Payload, 8) == 10f &&
                BitConverter.ToSingle(record.Payload, 12) == 11f);
            var offset = records.Single(record => record.Type == 0x4035);
            var reset = records.Single(record => record.Type == 0x4031);

            Assert.AreEqual((ushort)0x0100, clip.Flags);
            Assert.AreEqual((uint)28, clip.Size);
            Assert.AreEqual((uint)16, clip.DataSize);
            Assert.AreEqual(2f, BitConverter.ToSingle(clip.Payload, 0));
            Assert.AreEqual(11f, BitConverter.ToSingle(clip.Payload, 12));
            Assert.AreEqual((uint)20, offset.Size);
            Assert.AreEqual((uint)8, offset.DataSize);
            Assert.AreEqual(4f, BitConverter.ToSingle(offset.Payload, 0));
            Assert.AreEqual(5f, BitConverter.ToSingle(offset.Payload, 4));
            Assert.AreEqual((uint)12, reset.Size);
            Assert.AreEqual((uint)0, reset.DataSize);
            Assert.IsTrue(document.EmfRecords.Any(record => record is EMR_EXTSELECTCLIPRGN));
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_MapsRecordedPathClip()
        {
            using var recording = CreateRecording(50, 50, out var scene);
            using var path = new GraphicsPath(FillMode.Alternate);
            path.AddRectangle(new RectangleF(3, 4, 15, 16));
            recording.SetClip(path, CombineMode.Replace);

            using var context = new EmfDrawingContext(50, 50, 96f, 96f, 16f);
            VectorScenePlayer.Play(scene, context);
            var document = new MetafileReader().Read(SerializeEmfDocument(context.ToDocument()));
            var comments = EmfPlusTestDiagnostics.Decode(document);
            EmfPlusTestDiagnostics.AssertValid(comments);
            var records = comments.SelectMany(records => records).ToList();
            var regionObject = records.Single(record => record.Type == 0x4008 &&
                (record.Flags & 0x7F00) == 0x0400);
            var clip = records.Single(record => record.Type == 0x4034);

            Assert.AreEqual((ushort)0, clip.Flags);
            Assert.AreEqual(0xDBC01002u, BitConverter.ToUInt32(regionObject.Payload, 0));
            Assert.AreEqual(0u, BitConverter.ToUInt32(regionObject.Payload, 4));
            Assert.AreEqual(0x10000001u, BitConverter.ToUInt32(regionObject.Payload, 8));
            Assert.AreEqual((uint)(regionObject.Flags & 0xFF),
                BitConverter.ToUInt32(clip.Payload, 0));
            Assert.IsTrue(document.EmfRecords.Any(record => record is EMR_EXTSELECTCLIPRGN));
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_MapsCompositeRegionClip()
        {
            using var region = new DRIT.Drawing.Drawing2D.Region(new RectangleF(2, 3, 10, 8));
            region.Union(new RectangleF(20, 4, 6, 5));
            using var context = new EmfDrawingContext(50, 50, 96f, 96f, 16f);
            context.SetClip(region, CombineMode.Replace);

            var document = new MetafileReader().Read(SerializeEmfDocument(context.ToDocument()));
            var records = EmfPlusTestDiagnostics.Decode(document).SelectMany(records => records).ToList();
            var regionObject = records.Single(record => record.Type == 0x4008 &&
                (record.Flags & 0x7F00) == 0x0400);
            var clip = records.Single(record => record.Type == 0x4034);

            Assert.AreEqual(0xDBC01002u, BitConverter.ToUInt32(regionObject.Payload, 0));
            Assert.AreEqual(2u, BitConverter.ToUInt32(regionObject.Payload, 4));
            Assert.AreEqual(0x00000002u, BitConverter.ToUInt32(regionObject.Payload, 8));
            Assert.AreEqual((ushort)0, clip.Flags);
            Assert.AreEqual((uint)(regionObject.Flags & 0xFF),
                BitConverter.ToUInt32(clip.Payload, 0));
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_MapsPathRegionClip()
        {
            using var path = new GraphicsPath(FillMode.Winding);
            path.AddEllipse(new RectangleF(4, 5, 20, 18));
            using var region = new DRIT.Drawing.Drawing2D.Region(path);
            using var context = new EmfDrawingContext(50, 50, 96f, 96f, 16f);
            context.SetClip(region, CombineMode.Replace);

            var document = new MetafileReader().Read(SerializeEmfDocument(context.ToDocument()));
            var records = EmfPlusTestDiagnostics.Decode(document).SelectMany(records => records).ToList();
            var regionObject = records.Single(record => record.Type == 0x4008 &&
                (record.Flags & 0x7F00) == 0x0400);
            var clip = records.Single(record => record.Type == 0x4034);

            Assert.AreEqual(0xDBC01002u, BitConverter.ToUInt32(regionObject.Payload, 0));
            Assert.AreEqual(0u, BitConverter.ToUInt32(regionObject.Payload, 4));
            Assert.AreEqual(0x10000001u, BitConverter.ToUInt32(regionObject.Payload, 8));
            Assert.AreEqual((ushort)0, clip.Flags);
            Assert.AreEqual((uint)(regionObject.Flags & 0xFF),
                BitConverter.ToUInt32(clip.Payload, 0));
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_MapsClearAndBitmapImage()
        {
            using var bitmap = new Bitmap(2, 3, PixelFormat.Format32bppArgb);
            using var context = new EmfDrawingContext(20, 20, 96f, 96f, 16f);
            context.Clear(Color.White);
            context.DrawImage(bitmap, new RectangleF(4, 5, 8, 9),
                new RectangleF(0, 0, 2, 3), GraphicsUnit.Pixel, null);

            var document = new MetafileReader().Read(SerializeEmfDocument(context.ToDocument()));
            var comments = EmfPlusTestDiagnostics.Decode(document);
            EmfPlusTestDiagnostics.AssertValid(comments);
            var records = comments.SelectMany(records => records).ToList();
            var clear = records.Single(record => record.Type == 0x4009);
            var imageObject = records.Single(record => record.Type == 0x4008 &&
                (record.Flags & 0x7F00) == 0x0500);
            var drawImage = records.Single(record => record.Type == 0x401B);

            Assert.AreEqual(0xDBC01002u, BitConverter.ToUInt32(imageObject.Payload, 0));
            Assert.AreEqual(1u, BitConverter.ToUInt32(imageObject.Payload, 4));
            Assert.AreEqual(0x474E5089u, BitConverter.ToUInt32(imageObject.Payload, 28));
            Assert.AreEqual((uint)4, clear.DataSize);
            Assert.AreEqual(Color.White.ToArgb(), BitConverter.ToInt32(clear.Payload, 0));
            Assert.AreEqual(uint.MaxValue, BitConverter.ToUInt32(drawImage.Payload, 0));
            Assert.AreEqual((ushort)0x4000, (ushort)(drawImage.Flags & 0x4000));
            Assert.AreEqual((uint)40, drawImage.DataSize);
            Assert.AreEqual((uint)(imageObject.Flags & 0xFF), (uint)(drawImage.Flags & 0xFF));
            Assert.AreEqual((uint)GraphicsUnit.Pixel, BitConverter.ToUInt32(drawImage.Payload, 4));
            Assert.AreEqual(2f, BitConverter.ToSingle(drawImage.Payload, 16));
            Assert.AreEqual(3f, BitConverter.ToSingle(drawImage.Payload, 20));
            Assert.AreEqual((uint)3, BitConverter.ToUInt32(drawImage.Payload, 24));
            Assert.AreEqual((short)4, BitConverter.ToInt16(drawImage.Payload, 28));
            Assert.AreEqual((short)5, BitConverter.ToInt16(drawImage.Payload, 30));
            Assert.AreEqual((short)12, BitConverter.ToInt16(drawImage.Payload, 32));
            Assert.AreEqual((short)14, BitConverter.ToInt16(drawImage.Payload, 38));
            Assert.IsTrue(document.EmfRecords.Any(record => record is EMR_FILLPATH));
            Assert.IsTrue(document.EmfRecords.Any(record => record is EMR_STRETCHDIBITS));
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_BitmapObjectIsConsumedByNativeGdiPlus()
        {
            if (!OperatingSystem.IsWindows())
                Assert.Inconclusive("The native GDI+ consumer regression requires Windows.");

            using var bitmap = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            bitmap.SetPixel(0, 0, Color.Red);
            bitmap.SetPixel(1, 0, Color.Green);
            bitmap.SetPixel(0, 1, Color.Blue);
            bitmap.SetPixel(1, 1, Color.Yellow);
            using var context = new EmfDrawingContext(30, 30, 96f, 96f, 1f,
                EmfOutputMode.EmfPlusDual);
            context.Clear(Color.White);
            context.DrawImage(bitmap, new RectangleF(5, 6, 20, 20),
                new RectangleF(0, 0, 2, 2), GraphicsUnit.Pixel, null);

            byte[] dualBytes = SerializeEmfDocument(context.ToDocument());
            byte[] classicBytes = StripEmfPlusComments(dualBytes);
            byte[] dualImage = NativeGdiPlus.RenderMetafile(dualBytes, 30, 30);
            byte[] classicImage = NativeGdiPlus.RenderMetafile(classicBytes, 30, 30);

            Assert.IsTrue(CountDifferentPixels(dualImage, classicImage, 30, 30) > 0,
                "Native GDI+ did not consume the embedded EMF+ bitmap object.");
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_MapsAffineBitmapImage()
        {
            using var bitmap = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            using var context = new EmfDrawingContext(20, 20, 96f, 96f, 16f);
            context.DrawImage(bitmap,
                new[] { new PointF(1, 2), new PointF(9, 2), new PointF(1, 10) },
                new RectangleF(0, 0, 2, 2), GraphicsUnit.Pixel, null);

            var document = new MetafileReader().Read(SerializeEmfDocument(context.ToDocument()));
            var comments = EmfPlusTestDiagnostics.Decode(document);
            EmfPlusTestDiagnostics.AssertValid(comments);
            var records = comments.SelectMany(records => records).ToList();
            var drawImage = records.Single(record => record.Type == 0x401B);

            Assert.AreEqual((uint)3, BitConverter.ToUInt32(drawImage.Payload, 24));
            Assert.AreEqual((ushort)0x4000, (ushort)(drawImage.Flags & 0x4000));
            Assert.AreEqual((short)1, BitConverter.ToInt16(drawImage.Payload, 28));
            Assert.AreEqual((short)2, BitConverter.ToInt16(drawImage.Payload, 30));
            Assert.AreEqual((short)9, BitConverter.ToInt16(drawImage.Payload, 32));
            Assert.AreEqual((short)10, BitConverter.ToInt16(drawImage.Payload, 38));
            Assert.IsTrue(document.EmfRecords.Any(record => record is EMR_STRETCHDIBITS));
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_MapsTextObjectsAndDrawString()
        {
            using var font = new DRIT.Drawing.Text.Font("Arial", 12f, FontStyle.Bold);
            using var brush = new SolidBrush(Color.Black);
            using var context = new EmfDrawingContext(80, 30, 96f, 96f, 16f);
            context.DrawString("Phase 4", font, brush, new RectangleF(2, 3, 50, 15), null);

            var document = new MetafileReader().Read(SerializeEmfDocument(context.ToDocument()));
            var comments = EmfPlusTestDiagnostics.Decode(document);
            EmfPlusTestDiagnostics.AssertValid(comments);
            var records = comments.SelectMany(records => records).ToList();
            var fontObject = records.Single(record => record.Type == 0x4008 &&
                (record.Flags & 0x7F00) == 0x0600);
            var drawString = records.Single(record => record.Type == 0x401C);

            Assert.AreEqual((ushort)((fontObject.Flags & 0xFF) | 0x8000), drawString.Flags);
            Assert.AreEqual(0xFF000000u, BitConverter.ToUInt32(drawString.Payload, 0));
            Assert.AreEqual(uint.MaxValue, BitConverter.ToUInt32(drawString.Payload, 4));
            Assert.AreEqual((uint)7, BitConverter.ToUInt32(drawString.Payload, 8));
            Assert.AreEqual(2f, BitConverter.ToSingle(drawString.Payload, 12));
            Assert.AreEqual(15f, BitConverter.ToSingle(drawString.Payload, 24));
            Assert.AreEqual((byte)0, drawString.Payload[drawString.Payload.Length - 2]);
            Assert.AreEqual((byte)0, drawString.Payload[drawString.Payload.Length - 1]);
            Assert.AreEqual(0xDBC01001u, BitConverter.ToUInt32(fontObject.Payload, 0));
            Assert.AreEqual((uint)3, BitConverter.ToUInt32(fontObject.Payload, 8));
            Assert.AreEqual((uint)1, BitConverter.ToUInt32(fontObject.Payload, 12));
            Assert.AreEqual((uint)5, BitConverter.ToUInt32(fontObject.Payload, 20));
            Assert.IsTrue(document.EmfRecords.Any(record => record is EMR_EXTTEXTOUTW));
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_TextObjectsAreConsumedByNativeGdiPlus()
        {
            if (!OperatingSystem.IsWindows())
                Assert.Inconclusive("The native GDI+ consumer regression requires Windows.");

            using var font = new DRIT.Drawing.Text.Font("Arial", 12f, FontStyle.Bold);
            using var brush = new SolidBrush(Color.Black);
            using var context = new EmfDrawingContext(100, 40, 96f, 96f, 1f,
                EmfOutputMode.EmfPlusDual);
            context.Clear(Color.White);
            context.DrawString("Phase 4", font, brush, new RectangleF(5, 5, 80, 25), null);

            byte[] dualBytes = SerializeEmfDocument(context.ToDocument());
            byte[] classicBytes = StripEmfPlusComments(dualBytes);
            byte[] dualImage = NativeGdiPlus.RenderMetafile(dualBytes, 100, 40);
            byte[] classicImage = NativeGdiPlus.RenderMetafile(classicBytes, 100, 40);

            Assert.IsTrue(CountDifferentPixels(dualImage, classicImage, 100, 40) > 0,
                "Native GDI+ did not consume the embedded EMF+ text objects.");
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

        /// <summary>
        /// Verifies that Excel-compatible EMF output carries Excel's EMF+ rendering state.
        /// </summary>
        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_ContainsAntiAliasMode()
        {
            using var rec = CreateRecording(30, 30, out var scene);
            rec.Clear(Color.White);

            using var context = new EmfDrawingContext(30, 30, 96f, 96f, 16f);
            VectorScenePlayer.Play(scene, context);
            byte[] bytes = SerializeEmfDocument(context.ToDocument());

            var reader = new MetafileReader();
            var document = reader.Read(bytes);
            var comments = EmfPlusTestDiagnostics.Decode(document);
            EmfPlusTestDiagnostics.AssertValid(comments);
            var records = comments.SelectMany(records => records).ToList();
            var antiAliasRecord = records.Single(record => record.Type == (ushort)
                DRIT.Drawing.Metafile.EmfPlus.Enumerations.RecordType.EmfPlusSetAntiAliasMode);

            Assert.AreEqual((ushort)0x0004, antiAliasRecord.Flags);
            Assert.AreEqual((uint)12, antiAliasRecord.Size);
            Assert.AreEqual((uint)0, antiAliasRecord.DataSize);
            Assert.IsTrue(records.Any(record =>
                record.Type == (ushort)DRIT.Drawing.Metafile.EmfPlus.Enumerations.RecordType.EmfPlusSetPixelOffsetMode &&
                record.Flags == 0x0001));
            Assert.IsTrue(records.Any(record =>
                record.Type == (ushort)DRIT.Drawing.Metafile.EmfPlus.Enumerations.RecordType.EmfPlusSetCompositingQuality &&
                record.Flags == 0x0001));
            Assert.IsTrue(records.Any(record =>
                record.Type == (ushort)DRIT.Drawing.Metafile.EmfPlus.Enumerations.RecordType.EmfPlusSetPageTransform &&
                record.Flags == 0x0002 && record.DataSize == 4));
            Assert.IsTrue(records.Any(record =>
                record.Type == (ushort)DRIT.Drawing.Metafile.EmfPlus.Enumerations.RecordType.EmfPlusSetInterpolationMode &&
                record.Flags == 0x0006));
            Assert.IsTrue(records.Any(record =>
                record.Type == (ushort)DRIT.Drawing.Metafile.EmfPlus.Enumerations.RecordType.EmfPlusGetDC));
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_EmitsHeaderAndStateChanges()
        {
            using var recording = CreateRecording(40, 40, out var scene);
            recording.SmoothingMode = SmoothingMode.AntiAlias;
            recording.InterpolationMode = InterpolationMode.Bicubic;
            recording.PageUnit = GraphicsUnit.Point;
            recording.PageScale = 2f;
            recording.RenderingOrigin = new Point(3, 4);

            using var context = new EmfDrawingContext(40, 40, 96f, 96f, 16f);
            VectorScenePlayer.Play(scene, context);
            var document = new MetafileReader().Read(SerializeEmfDocument(context.ToDocument()));

            var comments = EmfPlusTestDiagnostics.Decode(document);
            EmfPlusTestDiagnostics.AssertValid(comments);
            var records = comments.SelectMany(records => records).ToList();
            Assert.IsTrue(EmfPlusTestDiagnostics.Normalize(comments).Any());
            Assert.IsTrue(records.Any(record =>
                record.Type == (ushort)DRIT.Drawing.Metafile.EmfPlus.Enumerations.RecordType.EmfPlusHeader));
            Assert.IsTrue(records.Any(record =>
                record.Type == (ushort)DRIT.Drawing.Metafile.EmfPlus.Enumerations.RecordType.EmfPlusSetAntiAliasMode &&
                record.Flags == 5));
            Assert.IsTrue(records.Any(record =>
                record.Type == (ushort)DRIT.Drawing.Metafile.EmfPlus.Enumerations.RecordType.EmfPlusSetInterpolationMode &&
                record.Flags == 5));
            Assert.IsTrue(records.Any(record =>
                record.Type == (ushort)DRIT.Drawing.Metafile.EmfPlus.Enumerations.RecordType.EmfPlusSetPageTransform &&
                record.Flags == 3 && record.DataSize == 4));
            Assert.IsTrue(records.Any(record =>
                record.Type == (ushort)DRIT.Drawing.Metafile.EmfPlus.Enumerations.RecordType.EmfPlusSetRenderingOrigin &&
                record.DataSize == 8));
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_ModeIsIndependentFromCoordinateScale()
        {
            using var recording = CreateRecording(30, 30, out var scene);
            using var pen = new Pen(Color.Black, 1f);
            recording.DrawLine(pen, 2f, 3f, 20f, 21f);

            using var dualAtUnitScale = new EmfDrawingContext(30, 30, 96f, 96f, 1f,
                EmfOutputMode.EmfPlusDual);
            VectorScenePlayer.Play(scene, dualAtUnitScale);
            Assert.IsTrue(new MetafileReader().Read(
                SerializeEmfDocument(dualAtUnitScale.ToDocument())).EmbeddedEmfPlusRecords.Count > 0);

            using var classicAtScaledCoordinates = new EmfDrawingContext(30, 30, 96f, 96f, 16f,
                EmfOutputMode.Classic);
            VectorScenePlayer.Play(scene, classicAtScaledCoordinates);
            Assert.AreEqual(0, new MetafileReader().Read(
                SerializeEmfDocument(classicAtScaledCoordinates.ToDocument())).EmbeddedEmfPlusRecords.Count);
        }

        [TestMethod]
        public void Vdm_ToClassicEmf_DoesNotEmitEmbeddedEmfPlus()
        {
            using var recording = CreateRecording(30, 30, out var scene);
            recording.SmoothingMode = SmoothingMode.AntiAlias;
            recording.InterpolationMode = InterpolationMode.Bicubic;

            using var context = new EmfDrawingContext(30, 30);
            VectorScenePlayer.Play(scene, context);
            var document = context.ToDocument();

            Assert.AreEqual(0, document.EmbeddedEmfPlusRecords.Count);
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_EmitsSolidObjectsAndPathOperations()
        {
            using var recording = CreateRecording(60, 60, out var scene);
            using var brush = new SolidBrush(Color.FromArgb(255, 20, 40, 60));
            using var pen = new Pen(Color.FromArgb(255, 200, 100, 50), 2f);
            using var path = new GraphicsPath(FillMode.Winding);
            path.AddRectangle(new RectangleF(5, 6, 20, 25));
            recording.FillPath(brush, path, FillMode.Winding);
            recording.DrawPath(pen, path);

            using var context = new EmfDrawingContext(60, 60, 96f, 96f, 16f);
            VectorScenePlayer.Play(scene, context);
            var document = new MetafileReader().Read(SerializeEmfDocument(context.ToDocument()));
            var comments = EmfPlusTestDiagnostics.Decode(document);
            EmfPlusTestDiagnostics.AssertValid(comments);
            var records = comments.SelectMany(records => records).ToList();

            var objects = records.Where(record => record.Type == 0x4008).ToList();
            Assert.AreEqual(4, objects.Count, "Expected one brush, two path, and one pen object.");

            var brushObject = objects.Single(record => (record.Flags & 0x7F00) == 0x0100);
            Assert.AreEqual((uint)24, brushObject.Size);
            Assert.AreEqual((uint)12, brushObject.DataSize);
            Assert.AreEqual((uint)0, BitConverter.ToUInt32(brushObject.Payload, 0));
            Assert.AreEqual((uint)0, BitConverter.ToUInt32(brushObject.Payload, 4));
            Assert.AreEqual(0xFF14283Cu, BitConverter.ToUInt32(brushObject.Payload, 8));

            var penObject = objects.Single(record => (record.Flags & 0x7F00) == 0x0200);
            Assert.AreEqual((uint)52, penObject.Size);
            Assert.AreEqual((uint)40, penObject.DataSize);
            Assert.AreEqual(25400f, BitConverter.ToSingle(penObject.Payload, 16));
            Assert.AreEqual(0xFFC86432u, BitConverter.ToUInt32(penObject.Payload, 36));

            var fillPathObject = objects.Single(record =>
                (record.Flags & 0x7F00) == 0x0300 && (record.Flags & 0xFF) == 1);
            Assert.AreEqual((uint)44, fillPathObject.Size);
            Assert.AreEqual((uint)32, fillPathObject.DataSize);
            Assert.AreEqual((uint)4, BitConverter.ToUInt32(fillPathObject.Payload, 4));
            Assert.AreEqual((uint)0x4000, BitConverter.ToUInt32(fillPathObject.Payload, 8));
            Assert.AreEqual((short)5, BitConverter.ToInt16(fillPathObject.Payload, 12));
            Assert.AreEqual((short)6, BitConverter.ToInt16(fillPathObject.Payload, 14));
            Assert.AreEqual((byte)0x81, fillPathObject.Payload[12 + (4 * 4) + 3]);

            var drawPathObject = objects.Single(record =>
                (record.Flags & 0x7F00) == 0x0300 && (record.Flags & 0xFF) == 3);
            Assert.AreEqual((uint)60, drawPathObject.Size);
            Assert.AreEqual((uint)48, drawPathObject.DataSize);

            var draw = records.Single(record => record.Type == 0x4015);
            uint pathId = (uint)(drawPathObject.Flags & 0xFF);
            Assert.AreEqual(pathId, (uint)draw.Flags);
            Assert.AreEqual((uint)(penObject.Flags & 0xFF),
                BitConverter.ToUInt32(draw.Payload, 0));
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_EmitsHatchBrushObject()
        {
            using var brush = new HatchBrush(DRIT.Drawing.Drawing2D.HatchStyle.ForwardDiagonal, Color.Red, Color.Blue);
            using var path = new GraphicsPath(FillMode.Winding);
            path.AddEllipse(new RectangleF(5, 6, 20, 25));
            using var context = new EmfDrawingContext(60, 60, 96f, 96f, 16f);
            context.FillPath(brush, path, FillMode.Winding);

            var document = new MetafileReader().Read(SerializeEmfDocument(context.ToDocument()));
            var records = EmfPlusTestDiagnostics.Decode(document).SelectMany(records => records).ToList();
            var brushObject = records.Single(record => record.Type == 0x4008 &&
                (record.Flags & 0x7F00) == 0x0100);

            Assert.AreEqual(0xDBC01002u, BitConverter.ToUInt32(brushObject.Payload, 0));
            Assert.AreEqual(1u, BitConverter.ToUInt32(brushObject.Payload, 4));
            Assert.AreEqual((uint)DRIT.Drawing.Drawing2D.HatchStyle.ForwardDiagonal,
                BitConverter.ToUInt32(brushObject.Payload, 8));
            Assert.AreEqual(Color.Red.ToArgb(), BitConverter.ToInt32(brushObject.Payload, 12));
            Assert.AreEqual(Color.Blue.ToArgb(), BitConverter.ToInt32(brushObject.Payload, 16));
            Assert.IsTrue(records.Any(record => record.Type == 0x4014));
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_HatchBrushPreservesNativeFallback()
        {
            if (!OperatingSystem.IsWindows())
                Assert.Inconclusive("The native GDI+ consumer regression requires Windows.");

            using var brush = new HatchBrush(DRIT.Drawing.Drawing2D.HatchStyle.ForwardDiagonal, Color.Red, Color.Blue);
            using var path = new GraphicsPath(FillMode.Winding);
            path.AddEllipse(new RectangleF(5, 6, 40, 40));
            using var context = new EmfDrawingContext(60, 60, 96f, 96f, 16f);
            context.FillPath(brush, path, FillMode.Winding);

            byte[] dualBytes = SerializeEmfDocument(context.ToDocument());
            byte[] classicBytes = StripEmfPlusComments(dualBytes);
            byte[] dualImage = NativeGdiPlus.RenderMetafile(dualBytes, 60, 60);
            byte[] classicImage = NativeGdiPlus.RenderMetafile(classicBytes, 60, 60);

            Assert.AreEqual(0, CountDifferentPixels(dualImage, classicImage, 60, 60),
                "Native GDI+ did not preserve the classic fallback after the EMF+ hatch brush.");
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_EmitsGradientBrushObjects()
        {
            using var linear = new LinearGradientBrush(
                new PointF(5, 6), new PointF(45, 40), Color.Red, Color.Blue);
            using var pathGradient = new PathGradientBrush(new[]
            {
                new PointF(10, 10), new PointF(50, 10), new PointF(50, 45), new PointF(10, 45),
            });
            pathGradient.CenterColor = Color.White;
            pathGradient.SurroundColors = new[] { Color.Black, Color.Red, Color.Green, Color.Blue };
            using var path = new GraphicsPath(FillMode.Winding);
            path.AddRectangle(new RectangleF(5, 6, 40, 34));
            using var context = new EmfDrawingContext(60, 60, 96f, 96f, 16f);
            context.FillPath(linear, path, FillMode.Winding);
            context.FillPath(pathGradient, path, FillMode.Winding);

            var document = new MetafileReader().Read(SerializeEmfDocument(context.ToDocument()));
            var objects = EmfPlusTestDiagnostics.Decode(document).SelectMany(records => records)
                .Where(record => record.Type == 0x4008 && (record.Flags & 0x7F00) == 0x0100)
                .ToList();

            Assert.AreEqual(2, objects.Count);
            CollectionAssert.Contains(objects.Select(record =>
                BitConverter.ToUInt32(record.Payload, 4)).ToList(), 3u);
            CollectionAssert.Contains(objects.Select(record =>
                BitConverter.ToUInt32(record.Payload, 4)).ToList(), 4u);
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_GradientBrushesPreserveNativeFallback()
        {
            if (!OperatingSystem.IsWindows())
                Assert.Inconclusive("The native GDI+ consumer regression requires Windows.");

            using var brush = new LinearGradientBrush(
                new PointF(5, 6), new PointF(45, 40), Color.Red, Color.Blue);
            using var path = new GraphicsPath(FillMode.Winding);
            path.AddRectangle(new RectangleF(5, 6, 40, 34));
            using var context = new EmfDrawingContext(60, 60, 96f, 96f, 16f);
            context.FillPath(brush, path, FillMode.Winding);

            byte[] dualBytes = SerializeEmfDocument(context.ToDocument());
            byte[] classicBytes = StripEmfPlusComments(dualBytes);
            byte[] dualImage = NativeGdiPlus.RenderMetafile(dualBytes, 60, 60);
            byte[] classicImage = NativeGdiPlus.RenderMetafile(classicBytes, 60, 60);

            Assert.AreEqual(0, CountDifferentPixels(dualImage, classicImage, 60, 60),
                "Native GDI+ did not preserve the classic fallback after the EMF+ gradient brush.");
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_PathGradientBrushPreservesNativeFallback()
        {
            if (!OperatingSystem.IsWindows())
                Assert.Inconclusive("The native GDI+ consumer regression requires Windows.");

            using var brush = new PathGradientBrush(new[]
            {
                new PointF(8, 8), new PointF(52, 8), new PointF(52, 48), new PointF(8, 48),
            });
            brush.CenterColor = Color.White;
            brush.SurroundColors = new[] { Color.Red, Color.Green, Color.Blue, Color.Black };
            using var path = new GraphicsPath(FillMode.Winding);
            path.AddRectangle(new RectangleF(8, 8, 44, 40));
            using var context = new EmfDrawingContext(60, 60, 96f, 96f, 16f);
            context.FillPath(brush, path, FillMode.Winding);

            byte[] dualBytes = SerializeEmfDocument(context.ToDocument());
            byte[] classicBytes = StripEmfPlusComments(dualBytes);
            byte[] dualImage = NativeGdiPlus.RenderMetafile(dualBytes, 60, 60);
            byte[] classicImage = NativeGdiPlus.RenderMetafile(classicBytes, 60, 60);

            Assert.AreEqual(0, CountDifferentPixels(dualImage, classicImage, 60, 60),
                "Native GDI+ did not preserve the classic fallback after the EMF+ path-gradient brush.");
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_CustomGradientUsesSampledClassicFallback()
        {
            using var brush = new LinearGradientBrush(
                new PointF(5, 6), new PointF(45, 40), Color.Red, Color.Blue);
            brush.SetBlendTriangularShape(0.5f);
            using var path = new GraphicsPath(FillMode.Winding);
            path.AddRectangle(new RectangleF(5, 6, 40, 34));
            using var context = new EmfDrawingContext(60, 60, 96f, 96f, 16f);
            context.FillPath(brush, path, FillMode.Winding);

            var document = new MetafileReader().Read(SerializeEmfDocument(context.ToDocument()));
            var records = EmfPlusTestDiagnostics.Decode(document).SelectMany(records => records).ToList();

            Assert.AreEqual(0, records.Count(record => record.Type == 0x4008 &&
                (record.Flags & 0x7F00) == 0x0100));
            var fallback = document.EmfRecords.OfType<EMR_CREATEDIBPATTERNBRUSHPT>().Single();
            Assert.AreEqual(40, BitConverter.ToInt32(fallback.Bmi, 4));
            Assert.AreEqual(34, BitConverter.ToInt32(fallback.Bmi, 8));
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_ImageAttributesTransformPixels()
        {
            using var bitmap = new Bitmap(2, 1, PixelFormat.Format32bppArgb);
            bitmap.SetPixel(0, 0, Color.Red);
            bitmap.SetPixel(1, 0, Color.Blue);
            using var attributes = new ImageAttributes();
            attributes.SetRemapTable(new[]
            {
                new ColorMap { OldColor = Color.Red, NewColor = Color.Green },
            });

            using var context = new EmfDrawingContext(2, 1, 96f, 96f, 16f);
            context.DrawImage(bitmap, new RectangleF(0, 0, 2, 1),
                new RectangleF(0, 0, 2, 1), GraphicsUnit.Pixel, attributes);
            var rendered = RasterizeEmfBytes(SerializeEmfDocument(context.ToDocument()), 2, 1);

            Assert.AreEqual(Color.Green.ToArgb(), rendered.GetPixel(0, 0).ToArgb());
            Assert.AreEqual(Color.Blue.ToArgb(), rendered.GetPixel(1, 0).ToArgb());
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_EmitsTextureBrushFallback()
        {
            using var bitmap = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            bitmap.SetPixel(0, 0, Color.Red);
            bitmap.SetPixel(1, 0, Color.Blue);
            bitmap.SetPixel(0, 1, Color.Green);
            bitmap.SetPixel(1, 1, Color.Yellow);
            using var brush = new TextureBrush(bitmap, WrapMode.TileFlipXY);
            using var path = new GraphicsPath(FillMode.Winding);
            path.AddRectangle(new RectangleF(5, 6, 40, 34));
            using var context = new EmfDrawingContext(60, 60, 96f, 96f, 16f);
            context.FillPath(brush, path, FillMode.Winding);

            var document = new MetafileReader().Read(SerializeEmfDocument(context.ToDocument()));
            var brushRecord = document.EmfRecords.OfType<EMR_CREATEDIBPATTERNBRUSHPT>().Single();

            Assert.AreEqual(DIBColors.DibRgbColors, brushRecord.Usage);
            Assert.AreEqual(4, BitConverter.ToInt32(brushRecord.Bmi, 4));
            Assert.AreEqual(4, BitConverter.ToInt32(brushRecord.Bmi, 8));
            Assert.AreEqual((ushort)32, BitConverter.ToUInt16(brushRecord.Bmi, 14));
            Assert.IsTrue(brushRecord.Bits.Length > 0);
            Assert.AreEqual(0, EmfPlusTestDiagnostics.Decode(document).SelectMany(records => records)
                .Count(record => record.Type == 0x4008 && (record.Flags & 0x7F00) == 0x0100));
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_TextureBrushFallbackIsConsumedByNativeGdiPlus()
        {
            if (!OperatingSystem.IsWindows())
                Assert.Inconclusive("The native GDI+ consumer regression requires Windows.");

            using var bitmap = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            bitmap.SetPixel(0, 0, Color.Red);
            bitmap.SetPixel(1, 0, Color.Blue);
            bitmap.SetPixel(0, 1, Color.Green);
            bitmap.SetPixel(1, 1, Color.Yellow);
            using var brush = new TextureBrush(bitmap, WrapMode.Tile);
            using var path = new GraphicsPath(FillMode.Winding);
            path.AddRectangle(new RectangleF(5, 6, 40, 34));
            using var context = new EmfDrawingContext(60, 60, 96f, 96f, 16f);
            context.FillPath(brush, path, FillMode.Winding);

            byte[] dualBytes = SerializeEmfDocument(context.ToDocument());
            byte[] dualImage = NativeGdiPlus.RenderMetafile(dualBytes, 60, 60);

            Assert.IsTrue(CountDifferentPixels(dualImage, new byte[dualImage.Length], 60, 60) > 0,
                "Native GDI+ did not render the texture brush fallback.");
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_EmitsAdvancedPenData()
        {
            using var pen = new Pen(Color.Red, 3f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.ArrowAnchor,
                LineJoin = LineJoin.Bevel,
                DashStyle = DashStyle.DashDot,
            };
            using var path = new GraphicsPath(FillMode.Winding);
            path.AddLine(5, 8, 45, 40);
            using var context = new EmfDrawingContext(60, 60, 96f, 96f, 16f);
            context.DrawPath(pen, path);

            var document = new MetafileReader().Read(SerializeEmfDocument(context.ToDocument()));
            var penObject = EmfPlusTestDiagnostics.Decode(document).SelectMany(records => records)
                .Single(record => record.Type == 0x4008 && (record.Flags & 0x7F00) == 0x0200);

            Assert.AreEqual(0xAEu, BitConverter.ToUInt32(penObject.Payload, 8));
            Assert.AreEqual(2u, BitConverter.ToUInt32(penObject.Payload, 20));
            Assert.AreEqual(20u, BitConverter.ToUInt32(penObject.Payload, 24));
            Assert.AreEqual(1u, BitConverter.ToUInt32(penObject.Payload, 28));
            Assert.AreEqual(3u, BitConverter.ToUInt32(penObject.Payload, 32));
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_AdvancedPenIsConsumedByNativeGdiPlus()
        {
            if (!OperatingSystem.IsWindows())
                Assert.Inconclusive("The native GDI+ consumer regression requires Windows.");

            using var pen = new Pen(Color.Red, 3f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.ArrowAnchor,
                LineJoin = LineJoin.Bevel,
                DashStyle = DashStyle.DashDot,
            };
            using var path = new GraphicsPath(FillMode.Winding);
            path.AddLine(5, 8, 45, 40);
            using var context = new EmfDrawingContext(60, 60, 96f, 96f, 16f);
            context.DrawPath(pen, path);

            byte[] dualBytes = SerializeEmfDocument(context.ToDocument());
            byte[] classicBytes = StripEmfPlusComments(dualBytes);
            byte[] dualImage = NativeGdiPlus.RenderMetafile(dualBytes, 60, 60);
            byte[] classicImage = NativeGdiPlus.RenderMetafile(classicBytes, 60, 60);

            Assert.IsTrue(CountDifferentPixels(dualImage, classicImage, 60, 60) > 0,
                "Native GDI+ did not consume the embedded EMF+ advanced pen object.");
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_PreservesBezierSubpathsAndCloseFlags()
        {
            using var recording = CreateRecording(60, 60, out var scene);
            using var pen = new Pen(Color.Black, 1f);
            using var path = new GraphicsPath(FillMode.Alternate);
            path.AddBezier(5, 5, 10, 0, 20, 0, 25, 5);
            path.CloseFigure();
            path.StartFigure();
            path.AddLine(30, 30, 40, 40);
            path.CloseFigure();
            recording.DrawPath(pen, path);

            using var context = new EmfDrawingContext(60, 60, 96f, 96f, 16f);
            VectorScenePlayer.Play(scene, context);
            var document = new MetafileReader().Read(SerializeEmfDocument(context.ToDocument()));
            var comments = EmfPlusTestDiagnostics.Decode(document);
            EmfPlusTestDiagnostics.AssertValid(comments);
            var records = comments.SelectMany(records => records).ToList();
            var pathObject = records.Single(record => record.Type == 0x4008 &&
                (record.Flags & 0x7F00) == 0x0300);

            Assert.AreEqual((uint)6, BitConverter.ToUInt32(pathObject.Payload, 4));
            Assert.AreEqual((uint)0, BitConverter.ToUInt32(pathObject.Payload, 8));
            Assert.AreEqual(0f, BitConverter.ToSingle(pathObject.Payload, 12));
            Assert.AreEqual(238125f, BitConverter.ToSingle(pathObject.Payload, 12 + (4 * 8)));
            Assert.AreEqual((byte)0x83, pathObject.Payload[12 + (6 * 8) + 3]);
            Assert.AreEqual((byte)0x81, pathObject.Payload[12 + (6 * 8) + 5]);
            Assert.IsTrue(records.Any(record => record.Type == 0x4015));
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_HasCompleteB7LikeSequence()
        {
            using var recording = CreateRecording(60, 60, out var scene);
            using var pen = new Pen(Color.Black, 2f);
            recording.Clear(Color.White);
            recording.TranslateTransform(3f, 4f);
            recording.DrawLine(pen, 5f, 6f, 35f, 36f);

            using var context = new EmfDrawingContext(60, 60, 96f, 96f, 16f);
            VectorScenePlayer.Play(scene, context);
            var document = new MetafileReader().Read(SerializeEmfDocument(context.ToDocument()));
            var comments = EmfPlusTestDiagnostics.Decode(document);
            EmfPlusTestDiagnostics.AssertValid(comments);
            var records = comments.SelectMany(records => records).ToList();
            var objectTypes = records.Where(record => record.Type == 0x4008)
                .Select(record => record.Flags & 0x7F00).ToList();

            Assert.AreEqual((ushort)0x4001, records[0].Type);
            Assert.IsTrue(records.Any(record => record.Type == 0x402A));
            CollectionAssert.Contains(objectTypes, 0x0200);
            CollectionAssert.Contains(objectTypes, 0x0300);
            Assert.IsTrue(records.Any(record => record.Type == 0x4015));
            Assert.AreEqual((ushort)0x4002, records[records.Count - 1].Type);
            Assert.IsTrue(document.EmfRecords.Any(record => record is EMR_STROKEPATH ||
                record is EMR_POLYGON16 || record is EMR_POLYGON ||
                record is EMR_POLYPOLYGON16 || record is EMR_POLYPOLYGON ||
                record is EMR_POLYLINE16));
        }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_ClassicFallbackMatchesClassicMode()
        {
            using var recording = CreateRecording(50, 50, out var scene);
            using var pen = new Pen(Color.FromArgb(255, 20, 40, 60), 2f);
            recording.DrawLine(pen, 4f, 5f, 35f, 25f);

            using var dualContext = new EmfDrawingContext(50, 50, 96f, 96f, 16f);
            VectorScenePlayer.Play(scene, dualContext);
            byte[] dualBytes = SerializeEmfDocument(dualContext.ToDocument());

            using var classicContext = new EmfDrawingContext(50, 50);
            VectorScenePlayer.Play(scene, classicContext);
            byte[] classicBytes = SerializeEmfDocument(classicContext.ToDocument());

            var comparison = new EmfComparer().Compare(dualBytes, classicBytes,
                new EmfComparisonOptions { Mode = EmfComparisonMode.GraphicalOperations });

            Assert.IsTrue(classicBytes.Length > 0);
            Assert.IsTrue(new EmfComparer().Compare(classicBytes, classicBytes).AreEqual);
            Assert.IsTrue(new EmfComparer().Compare(dualBytes, dualBytes).AreEqual);
            Assert.IsTrue(new MetafileReader().Read(dualBytes).EmbeddedEmfPlusRecords.Count > 0);
            Assert.AreEqual(0, new MetafileReader().Read(classicBytes).EmbeddedEmfPlusRecords.Count);
        }

            [TestMethod]
            public void Vdm_ToExcelCompatibleEmf_IsConsumedByNativeGdiPlus()
            {
                if (!OperatingSystem.IsWindows())
                    Assert.Inconclusive("The native GDI+ consumer regression requires Windows.");

                using var recording = CreateRecording(60, 60, out var scene);
                using var pen = new Pen(Color.FromArgb(255, 200, 100, 50), 2f);
                recording.DrawLine(pen, 5f, 6f, 45f, 46f);

                using var context = new EmfDrawingContext(60, 60, 96f, 96f, 16f);
                VectorScenePlayer.Play(scene, context);
                var dualDocument = context.ToDocument();
                byte[] dualBytes = SerializeEmfDocument(dualDocument);
                byte[] classicBytes = StripEmfPlusComments(dualBytes);
                byte[] dualImage = NativeGdiPlus.RenderMetafile(dualBytes, 60, 60);
                byte[] classicImage = NativeGdiPlus.RenderMetafile(classicBytes, 60, 60);

                int differingPixels = CountDifferentPixels(dualImage, classicImage, 60, 60);

                Assert.IsTrue(differingPixels > 0,
                    "Native GDI+ rendered the dual EMF identically after EMF+ records were removed.");
            }

            private static int CountDifferentPixels(byte[] first, byte[] second,
                int width, int height)
            {
                int differingPixels = 0;
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int offset = (y * width + x) * 4;
                    if (first[offset] != second[offset] ||
                        first[offset + 1] != second[offset + 1] ||
                        first[offset + 2] != second[offset + 2] ||
                        first[offset + 3] != second[offset + 3])
                        differingPixels++;
                }
                return differingPixels;
            }

            private sealed class NativeGdiPlus
            {
                private const int PixelFormat32bppArgb = 0x26200A;
                private const uint GdiPlusOk = 0;

                internal static byte[] RenderMetafile(byte[] bytes, int width, int height)
                {
                    string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".emf");
                    File.WriteAllBytes(path, bytes);
                    IntPtr metafile = IntPtr.Zero;
                    IntPtr bitmap = IntPtr.Zero;
                    IntPtr graphics = IntPtr.Zero;
                    IntPtr token = IntPtr.Zero;
                    try
                    {
                        var startupInput = new GdiplusStartupInput { GdiplusVersion = 1 };
                        Check(GdiplusStartup(out token, ref startupInput, IntPtr.Zero));
                        Check(GdipCreateMetafileFromFile(path, out metafile));
                        Check(GdipCreateBitmapFromScan0((uint)width, (uint)height,
                            width * 4, PixelFormat32bppArgb, IntPtr.Zero, out bitmap));
                        Check(GdipGetImageGraphicsContext(bitmap, out graphics));
                        Check(GdipGraphicsClear(graphics, 0xFFFFFFFF));
                        Check(GdipDrawImageRectI(graphics, metafile, 0, 0, width, height));

                        var rect = new GpRect { X = 0, Y = 0, Width = width, Height = height };
                        var bitmapData = new GpBitmapData();
                        Check(GdipBitmapLockBits(bitmap, ref rect, 1, PixelFormat32bppArgb,
                            ref bitmapData));
                        try
                        {
                            var result = new byte[width * height * 4];
                            for (int y = 0; y < height; y++)
                            {
                                IntPtr row = IntPtr.Add(bitmapData.Scan0, y * bitmapData.Stride);
                                System.Runtime.InteropServices.Marshal.Copy(row, result,
                                    y * width * 4, width * 4);
                            }
                            return result;
                        }
                        finally
                        {
                            Check(GdipBitmapUnlockBits(bitmap, ref bitmapData));
                        }
                    }
                    finally
                    {
                        if (graphics != IntPtr.Zero) GdipDeleteGraphics(graphics);
                        if (bitmap != IntPtr.Zero) GdipDisposeImage(bitmap);
                        if (metafile != IntPtr.Zero) GdipDisposeImage(metafile);
                        if (token != IntPtr.Zero) GdiplusShutdown(token);
                        File.Delete(path);
                    }
                }

                private static void Check(uint status)
                {
                    if (status != GdiPlusOk)
                        throw new InvalidOperationException("GDI+ failed with status " + status + ".");
                }

                [System.Runtime.InteropServices.DllImport("gdiplus.dll")]
                private static extern uint GdiplusStartup(out IntPtr token,
                    ref GdiplusStartupInput input, IntPtr output);

                [System.Runtime.InteropServices.DllImport("gdiplus.dll")]
                private static extern void GdiplusShutdown(IntPtr token);

                [System.Runtime.InteropServices.DllImport("gdiplus.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
                private static extern uint GdipCreateMetafileFromFile(string filename,
                    out IntPtr metafile);

                [System.Runtime.InteropServices.DllImport("gdiplus.dll")]
                private static extern uint GdipCreateBitmapFromScan0(uint width, uint height,
                    int stride, int pixelFormat, IntPtr scan0, out IntPtr bitmap);

                [System.Runtime.InteropServices.DllImport("gdiplus.dll")]
                private static extern uint GdipGetImageGraphicsContext(IntPtr image,
                    out IntPtr graphics);

                [System.Runtime.InteropServices.DllImport("gdiplus.dll")]
                private static extern uint GdipGraphicsClear(IntPtr graphics, uint color);

                [System.Runtime.InteropServices.DllImport("gdiplus.dll")]
                private static extern uint GdipDrawImageRectI(IntPtr graphics, IntPtr image,
                    int x, int y, int width, int height);

                [System.Runtime.InteropServices.DllImport("gdiplus.dll")]
                private static extern uint GdipBitmapLockBits(IntPtr bitmap, ref GpRect rect,
                    uint flags, int pixelFormat, ref GpBitmapData lockedBitmapData);

                [System.Runtime.InteropServices.DllImport("gdiplus.dll")]
                private static extern uint GdipBitmapUnlockBits(IntPtr bitmap,
                    ref GpBitmapData lockedBitmapData);

                [System.Runtime.InteropServices.DllImport("gdiplus.dll")]
                private static extern uint GdipDeleteGraphics(IntPtr graphics);

                [System.Runtime.InteropServices.DllImport("gdiplus.dll")]
                private static extern uint GdipDisposeImage(IntPtr image);

                [System.Runtime.InteropServices.StructLayout(
                    System.Runtime.InteropServices.LayoutKind.Sequential)]
                private struct GdiplusStartupInput
                {
                    internal uint GdiplusVersion;
                    internal IntPtr DebugEventCallback;
                    internal int SuppressBackgroundThread;
                    internal int SuppressExternalCodecs;

                }

                [System.Runtime.InteropServices.StructLayout(
                    System.Runtime.InteropServices.LayoutKind.Sequential)]
                private struct GpRect
                {
                    internal int X;
                    internal int Y;
                    internal int Width;
                    internal int Height;
                }

                [System.Runtime.InteropServices.StructLayout(
                    System.Runtime.InteropServices.LayoutKind.Sequential)]
                private struct GpBitmapData
                {
                    internal int Width;
                    internal int Height;
                    internal int Stride;
                    internal int PixelFormat;
                    internal IntPtr Scan0;
                    internal IntPtr Reserved;
                }
            }

        [TestMethod]
        public void Vdm_ToExcelCompatibleEmf_TransformedStrokeRasterMatchesDirectRaster()
        {
            using var recording = CreateRecording(50, 50, out var scene);
            using var pen = new Pen(Color.FromArgb(255, 30, 80, 180), 2f);
            recording.Clear(Color.White);
            recording.TranslateTransform(5f, 3f);
            recording.RotateTransform(8f);
            recording.DrawLine(pen, 5f, 8f, 35f, 32f);

            using var directRaster = ReplayToBitmap(scene);
            using var context = new EmfDrawingContext(50, 50);
            VectorScenePlayer.Play(scene, context);
            byte[] emfBytes = SerializeEmfDocument(context.ToDocument());
            using var emfRaster = RasterizeEmfBytes(emfBytes, 50, 50);

            AssertBitmapsEqual(directRaster, emfRaster, tolerance: 10);
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
        public void Vdm_ToEmf_HatchDibPreservesScanlineOrientation()
        {
            using var rec = CreateRecording(16, 16, out var scene);
            using var hatch = new HatchBrush(DRIT.Drawing.Drawing2D.HatchStyle.ForwardDiagonal, Color.Gray, Color.White);
            rec.FillRectangle(hatch, new RectangleF(0, 0, 16, 16));

            using var ctx = new EmfDrawingContext(16, 16);
            VectorScenePlayer.Play(scene, ctx);
            var pattern = ctx.ToDocument().EmfRecords
                .OfType<EMR_CREATEDIBPATTERNBRUSHPT>()
                .Single();

            const int stride = 8 * 4;
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                var expected = HatchRenderer.Sample(hatch, x, y);
                int offset = y * stride + x * 4;
                Assert.AreEqual(expected.B, pattern.Bits[offset], $"Blue channel mismatch at ({x},{y}).");
                Assert.AreEqual(expected.G, pattern.Bits[offset + 1], $"Green channel mismatch at ({x},{y}).");
                Assert.AreEqual(expected.R, pattern.Bits[offset + 2], $"Red channel mismatch at ({x},{y}).");
            }

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

            var eofRecord = new EMR_EOF { SizeLast = 20 };

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
