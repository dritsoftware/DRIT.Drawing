using System;
using System.IO;
using DRIT.Drawing.Codecs.Jpeg;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests.Codecs.Jpeg
{
    [TestClass]
    public sealed class JpegValidationTests
    {
        [TestMethod]
        public void FrameHeader_Parse_RejectsZeroDimensions()
        {
            var data = new byte[]
            {
                8, 0, 0, 0, 1, 1,
                1, 0x11, 0,
            };

            Assert.ThrowsException<InvalidDataException>(() => FrameHeader.Parse(JpegMarker.SOF0, data));
        }

        [TestMethod]
        public void ScanHeader_Parse_RejectsTrailingData()
        {
            var data = new byte[]
            {
                1, 1, 0,
                0, 63, 0,
                0xA5,
            };

            Assert.ThrowsException<InvalidDataException>(() => ScanHeader.Parse(data));
        }

        [TestMethod]
        public void HuffmanTable_ParseDht_RejectsTruncatedTable()
        {
            var data = new byte[17];
            data[0] = 0;
            data[1] = 1;

            Assert.ThrowsException<InvalidDataException>(() => HuffmanTable.ParseDht(data));
        }
    }
}
