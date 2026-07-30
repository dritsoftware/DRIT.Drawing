using System;
using System.Collections.Generic;
using System.IO;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Metafile;
using DRIT.Drawing.Metafile.Emf.Records.DrawingRecordTypes;
using DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes;
using DRIT.Drawing.Metafile.Wmf.Objects;
using DRIT.Drawing.Metafile.Wmf.Records;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class MetafileTests
    {
        [TestMethod]
        public void Metafile_Wmf_SetPixel_DecodesThroughBitmapCtor()
        {
            byte[] bytes = CreateWmfSetPixelFixture(16, 16, 3, 4, Color.FromArgb(255, 200, 30, 10));

            using var bmp = new Bitmap(new MemoryStream(bytes));
            Color pixel = bmp.GetPixel(3, 4);

            Assert.AreEqual(200, pixel.R, 2);
            Assert.AreEqual(30, pixel.G, 2);
            Assert.AreEqual(10, pixel.B, 2);
            Assert.IsTrue(bmp.RawFormat.Equals(ImageFormat.Wmf));
        }

        [TestMethod]
        public void Metafile_Emf_SetPixel_DecodesThroughBitmapCtor()
        {
            byte[] bytes = CreateEmfSetPixelFixture(16, 16, 6, 7, Color.FromArgb(255, 30, 140, 220));

            using var bmp = new Bitmap(new MemoryStream(bytes));
            Color pixel = bmp.GetPixel(6, 7);

            Assert.AreEqual(30, pixel.R, 2);
            Assert.AreEqual(140, pixel.G, 2);
            Assert.AreEqual(220, pixel.B, 2);
            Assert.IsTrue(bmp.RawFormat.Equals(ImageFormat.Emf));
        }

        [TestMethod]
        public void Graphics_DrawImage_WithMetafile_RendersPixels()
        {
            byte[] bytes = CreateWmfSetPixelFixture(16, 16, 5, 6, Color.FromArgb(255, 12, 180, 40));

            using var metafile = new Drawing.Imaging.Metafile(new MemoryStream(bytes));
            using var canvas = new Bitmap(16, 16);

            using (var g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.Black);
                g.DrawImage(metafile, new Rectangle(0, 0, 16, 16));
            }

            Color pixel = canvas.GetPixel(5, 6);
            Assert.AreEqual(12, pixel.R, 2);
            Assert.AreEqual(180, pixel.G, 2);
            Assert.AreEqual(40, pixel.B, 2);

            MetafileHeader header = metafile.GetMetafileHeader();
            Assert.IsTrue(header.IsWmf);
            Assert.AreEqual(16, metafile.Width);
            Assert.AreEqual(16, metafile.Height);
        }

        private static byte[] CreateWmfSetPixelFixture(int width, int height, int x, int y, Color color)
        {
            var document = new MetafileDocument
            {
                Format = MetafileFormat.Wmf,
                PlaceableHeader = new META_PLACEABLE
                {
                    BoundingBox = new Rect
                    {
                        Left = 0,
                        Top = 0,
                        Right = (short)width,
                        Bottom = (short)height,
                    },
                    Inch = 1440,
                },
                WmfRecords = new List<Drawing.Metafile.Wmf.Records.Record>
                {
                    new META_SETPIXEL
                    {
                        X = (short)x,
                        Y = (short)y,
                        ColorRef = new ColorRef
                        {
                            Red = color.R,
                            Green = color.G,
                            Blue = color.B,
                            Reserved = 0,
                        },
                    },
                    new META_EOF(),
                },
            };

            var writer = new MetafileWriter();
            return writer.WriteToBytes(document);
        }

        private static byte[] CreateEmfSetPixelFixture(int width, int height, int x, int y, Color color)
        {
            var headerRecord = new EMR_HEADER
            {
                HeaderObject =
                {
                    Bounds = new RectL
                    {
                        Left = 0,
                        Top = 0,
                        Right = width,
                        Bottom = height,
                    },
                    Frame = new RectL
                    {
                        Left = 0,
                        Top = 0,
                        Right = width * 100,
                        Bottom = height * 100,
                    },
                    RecordSignature = 0x464D4520,
                    Version = 0x00010000,
                    Records = 3,
                    Handles = 1,
                    Device = new SizeL
                    {
                        cx = (uint)width,
                        cy = (uint)height,
                    },
                    Millimeters = new SizeL
                    {
                        cx = (uint)Math.Max(1, width * 3),
                        cy = (uint)Math.Max(1, height * 3),
                    },
                },
            };

            var pixelRecord = new EMR_SETPIXELV
            {
                Position = new PointL { x = x, y = y },
                Color = new ColorRef
                {
                    Red = color.R,
                    Green = color.G,
                    Blue = color.B,
                    Reserved = 0,
                },
            };

            var eofRecord = new EMR_EOF { NumberOfRecords = 3 };

            var document = new MetafileDocument
            {
                Format = MetafileFormat.Emf,
                EmfRecords = new List<Drawing.Metafile.Emf.Records.Record>
                {
                    headerRecord,
                    pixelRecord,
                    eofRecord,
                },
            };

            var writer = new MetafileWriter();
            return writer.WriteToBytes(document);
        }
    }
}
