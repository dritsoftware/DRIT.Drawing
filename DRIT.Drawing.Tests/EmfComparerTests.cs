using System;
using System.IO;
using DRIT.Drawing.Metafile;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class EmfComparerTests
    {
        [TestMethod]
        public void Compare_IdenticalRecords_IsEqual()
        {
            byte[] emf = CreateRecords((1, new byte[] { 1, 2 }), (14, Array.Empty<byte>()));

            EmfComparisonResult result = new EmfComparer().Compare(emf, (byte[])emf.Clone());

            Assert.IsTrue(result.AreEqual);
            Assert.AreEqual(2, result.LeftRecords.Count);
            Assert.AreEqual(0, result.Differences.Count);
        }

        [TestMethod]
        public void Compare_ExactMode_ReportsPayloadDifference()
        {
            byte[] left = CreateRecords((1, new byte[] { 1, 2 }));
            byte[] right = CreateRecords((1, new byte[] { 1, 3 }));

            EmfComparisonResult result = new EmfComparer().Compare(left, right);

            Assert.IsFalse(result.AreEqual);
            Assert.AreEqual("record payload differs", result.Differences[0].Reason);
        }

        [TestMethod]
        public void Compare_StructuralMode_IgnoresPayloadValues()
        {
            byte[] left = CreateRecords((1, new byte[] { 1, 2 }));
            byte[] right = CreateRecords((1, new byte[] { 1, 3 }));

            EmfComparisonResult result = new EmfComparer().Compare(left, right,
                new EmfComparisonOptions { Mode = EmfComparisonMode.Structural });

            Assert.IsTrue(result.AreEqual);
        }

        [TestMethod]
        public void Compare_CanIgnoreHeaderAndEndOfFile()
        {
            byte[] left = CreateRecords((1, new byte[] { 1 }), (14, Array.Empty<byte>()), (20, new byte[] { 2 }));
            byte[] right = CreateRecords((1, new byte[] { 9 }), (14, Array.Empty<byte>()), (20, new byte[] { 2 }));

            EmfComparisonResult result = new EmfComparer().Compare(left, right,
                new EmfComparisonOptions
                {
                    IgnoreHeader = true,
                    IgnoreEndOfFile = true,
                });

            Assert.IsTrue(result.AreEqual);
            Assert.AreEqual(1, result.LeftRecords.Count);
        }

        [TestMethod]
        public void Compare_TruncatedRecord_ThrowsInvalidDataException()
        {
            byte[] invalid = { 1, 0, 0, 0, 12, 0, 0, 0, 1 };

            Assert.ThrowsException<InvalidDataException>(() => new EmfComparer().Compare(invalid, invalid));
        }

        [TestMethod]
        public void Compare_StructuralGeometry_ParsesPolyBezier16Bounds()
        {
            byte[] left = CreateGeometryRecord(85, 10, 20, 146, 156, 4);
            byte[] right = CreateGeometryRecord(85, 10, 20, 146, 156, 4);

            EmfComparisonResult result = new EmfComparer().Compare(left, right,
                new EmfComparisonOptions { Mode = EmfComparisonMode.StructuralGeometry });

            Assert.IsTrue(result.AreEqual);
            Assert.AreEqual(EmfOperationKind.PolyBezier16, result.LeftRecords[0].Operation.Kind);
            Assert.AreEqual(136, result.LeftRecords[0].Operation.Bounds.Right - result.LeftRecords[0].Operation.Bounds.Left);
            Assert.AreEqual(4, result.LeftRecords[0].Operation.PointCount);
        }

        [TestMethod]
        public void Compare_StructuralGeometry_ReportsGeometryDifference()
        {
            byte[] left = CreateGeometryRecord(85, 10, 20, 146, 156, 4);
            byte[] right = CreateGeometryRecord(85, 10, 20, 147, 156, 4);

            EmfComparisonResult result = new EmfComparer().Compare(left, right,
                new EmfComparisonOptions { Mode = EmfComparisonMode.StructuralGeometry });

            Assert.IsFalse(result.AreEqual);
            Assert.AreEqual("parsed operation geometry differs", result.Differences[0].Reason);
        }

        /// <summary>Verifies that non-graphical records do not shift operation matching.</summary>
        [TestMethod]
        public void Compare_GraphicalOperations_IgnoresInterleavedNonGraphicalRecords()
        {
            byte[] left = CreateRecords(
                (27, CreatePointPayload(10, 20)),
                (100, new byte[] { 1, 2, 3, 4 }),
                (54, CreatePointPayload(30, 40)));
            byte[] right = CreateRecords(
                (27, CreatePointPayload(10, 20)),
                (54, CreatePointPayload(30, 40)));

            EmfComparisonResult result = new EmfComparer().Compare(left, right,
                new EmfComparisonOptions { Mode = EmfComparisonMode.GraphicalOperations });

            Assert.IsTrue(result.AreEqual);
            Assert.AreEqual(3, result.LeftRecords.Count);
            Assert.AreEqual(2, result.RightRecords.Count);
            Assert.AreEqual(1, result.LeftRecords[0].Operation.Points.Count);
        }

        /// <summary>Verifies that UTF-16 text is decoded from ExtTextOutW records.</summary>
        [TestMethod]
        public void Read_ExtTextOutW_DecodesUnicodeText()
        {
            byte[] emf = CreateTextRecord("Title");

            IReadOnlyList<EmfRecordSignature> records = new EmfComparer().Read(
                new MemoryStream(emf));

            Assert.AreEqual(1, records.Count);
            Assert.AreEqual(EmfOperationKind.ExtTextOutW, records[0].Operation.Kind);
            Assert.AreEqual("Title", records[0].Operation.Text);
            Assert.AreEqual(5, records[0].Operation.PointCount);
        }

        /// <summary>Verifies that DRIT's EmrText-relative string offset is supported.</summary>
        [TestMethod]
        public void Read_ExtTextOutW_DecodesDritRelativeUnicodeTextOffset()
        {
            byte[] emf = CreateTextRecord("Subtitle", 40);

            IReadOnlyList<EmfRecordSignature> records = new EmfComparer().Read(
                new MemoryStream(emf));

            Assert.AreEqual("Subtitle", records[0].Operation.Text);
        }

        /// <summary>Verifies that text units carry the selected font and text color.</summary>
        [TestMethod]
        public void ReadSemanticUnits_TextCarriesEffectiveFontAndColor()
        {
            byte[] emf = CreateRecords(
                (82, CreateFontPayload(2, -400, 700, "Century Gothic")),
                (37, CreateUInt32Payload(2)),
                (24, CreateUInt32Payload(0x00332211)),
                (84, CreateTextPayload("Title")));

            IReadOnlyList<EmfSemanticUnit> units = new EmfSemanticReader().Read(emf);

            Assert.AreEqual(1, units.Count);
            Assert.AreEqual(EmfSemanticUnitKind.Text, units[0].Kind);
            Assert.AreEqual("Title", units[0].Text);
            Assert.AreEqual("Century Gothic", units[0].Font.FaceName);
            Assert.AreEqual(-400, units[0].Font.Height);
            Assert.AreEqual(700, units[0].Font.Weight);
            Assert.AreEqual("#112233", units[0].TextColor.ToString());
        }

        /// <summary>Verifies that path construction records become one semantic path unit.</summary>
        [TestMethod]
        public void ReadSemanticUnits_FillPathAggregatesPathPoints()
        {
            byte[] emf = CreateRecords(
                (59, Array.Empty<byte>()),
                (27, CreatePointPayload(10, 20)),
                (54, CreatePointPayload(30, 40)),
                (60, Array.Empty<byte>()),
                (62, CreateRectanglePayload(10, 20, 30, 40)));

            IReadOnlyList<EmfSemanticUnit> units = new EmfSemanticReader().Read(emf);

            Assert.AreEqual(1, units.Count);
            Assert.AreEqual(EmfSemanticUnitKind.Path, units[0].Kind);
            Assert.AreEqual(EmfOperationKind.FillPath, units[0].OperationKind);
            Assert.AreEqual(2, units[0].Points.Count);
            Assert.IsTrue(units[0].IsFilled);
            Assert.IsFalse(units[0].IsStroked);
        }

        /// <summary>Verifies that world-transformed geometry is normalized to the header canvas.</summary>
        [TestMethod]
        public void ReadSemanticUnits_NormalizesTransformedCoordinates()
        {
            byte[] transformPayload = new byte[24];
            Buffer.BlockCopy(BitConverter.GetBytes(0.25f), 0, transformPayload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(0.25f), 0, transformPayload, 12, 4);
            byte[] emf = CreateRecords(
                (1, CreateRectanglePayload(0, 0, 8000, 4500)),
                (35, transformPayload),
                (3, CreateGeometryPayload(3, 0, 0, 8000, 4500, 5, 32000, 18000)));

            IReadOnlyList<EmfSemanticUnit> units = new EmfSemanticReader().Read(emf);

            Assert.AreEqual(1, units.Count);
            Assert.AreEqual(1.0, units[0].NormalizedPoints[0].X, 0.000001);
            Assert.AreEqual(1.0, units[0].NormalizedPoints[0].Y, 0.000001);
            Assert.AreEqual(0.0, units[0].NormalizedBounds.Left, 0.000001);
            Assert.AreEqual(1.0, units[0].NormalizedBounds.Right, 0.000001);
        }

        private static byte[] CreateRecords(params (uint Type, byte[] Payload)[] records)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                foreach ((uint type, byte[] payload) in records)
                {
                    writer.Write(type);
                    writer.Write((uint)(payload.Length + 8));
                    writer.Write(payload);
                }
                return stream.ToArray();
            }
        }

        private static byte[] CreateGeometryRecord(uint type, int left, int top, int right, int bottom, int pointCount)
        {
            return CreateRecords((type, CreateGeometryPayload(type, left, top, right, bottom, pointCount)));
        }

        private static byte[] CreateGeometryPayload(uint type, int left, int top, int right, int bottom,
            int pointCount, int firstPointX = 0, int firstPointY = 0)
        {
            bool pointsAre16Bit = type == 85 || type == 86 || type == 91;
            int pointSize = pointsAre16Bit ? 4 : 8;
            byte[] payload = new byte[20 + pointCount * pointSize];
            Buffer.BlockCopy(BitConverter.GetBytes(left), 0, payload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(top), 0, payload, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(right), 0, payload, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(bottom), 0, payload, 12, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(pointCount), 0, payload, 16, 4);
            if (pointsAre16Bit)
            {
                Buffer.BlockCopy(BitConverter.GetBytes((short)firstPointX), 0, payload, 20, 2);
                Buffer.BlockCopy(BitConverter.GetBytes((short)firstPointY), 0, payload, 22, 2);
            }
            else
            {
                Buffer.BlockCopy(BitConverter.GetBytes(firstPointX), 0, payload, 20, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(firstPointY), 0, payload, 24, 4);
            }
            return payload;
        }

        private static byte[] CreatePointPayload(int x, int y)
        {
            byte[] payload = new byte[8];
            Buffer.BlockCopy(BitConverter.GetBytes(x), 0, payload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(y), 0, payload, 4, 4);
            return payload;
        }

        private static byte[] CreateUInt32Payload(uint value)
        {
            return BitConverter.GetBytes(value);
        }

        private static byte[] CreateRectanglePayload(int left, int top, int right, int bottom)
        {
            byte[] payload = new byte[16];
            Buffer.BlockCopy(BitConverter.GetBytes(left), 0, payload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(top), 0, payload, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(right), 0, payload, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(bottom), 0, payload, 12, 4);
            return payload;
        }

        private static byte[] CreateFontPayload(uint handle, int height, int weight, string faceName)
        {
            byte[] payload = new byte[360];
            Buffer.BlockCopy(BitConverter.GetBytes(handle), 0, payload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(height), 0, payload, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(weight), 0, payload, 20, 4);
            byte[] faceBytes = System.Text.Encoding.Unicode.GetBytes(faceName);
            Buffer.BlockCopy(faceBytes, 0, payload, 32, Math.Min(faceBytes.Length, 62));
            return payload;
        }

        private static byte[] CreateTextRecord(string text, int stringOffset = 76)
        {
            return CreateRecords((84, CreateTextPayload(text, stringOffset)));
        }

        private static byte[] CreateTextPayload(string text, int stringOffset = 76)
        {
            byte[] textBytes = System.Text.Encoding.Unicode.GetBytes(text);
            byte[] payload = new byte[68 + textBytes.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(0), 0, payload, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(0), 0, payload, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(100), 0, payload, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(40), 0, payload, 12, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(0), 0, payload, 16, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(0), 0, payload, 20, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(10), 0, payload, 28, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(20), 0, payload, 32, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(text.Length), 0, payload, 36, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(76), 0, payload, 40, 4);
            Buffer.BlockCopy(textBytes, 0, payload, 68, textBytes.Length);
            return payload;
        }
    }
}