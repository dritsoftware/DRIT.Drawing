using System.IO;
using DRIT.Drawing.Codecs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests.Codecs
{
    [TestClass]
    public sealed class CodecValidationTests
    {
        [TestMethod]
        public void ValidateDimensions_RejectsOversizedImages()
        {
            Assert.ThrowsException<InvalidDataException>(() =>
                CodecValidation.ValidateDimensions(CodecValidation.MaxDimension + 1, 1, "test"));
            Assert.ThrowsException<InvalidDataException>(() =>
                CodecValidation.ValidateDimensions(10_000, 10_000, "test", 1_000));
        }

        [TestMethod]
        public void CheckedBufferLength_RejectsNegativeAndOversizedBuffers()
        {
            Assert.ThrowsException<InvalidDataException>(() => CodecValidation.CheckedBufferLength(-1, "test"));
            Assert.ThrowsException<InvalidDataException>(() =>
                CodecValidation.CheckedBufferLength(CodecValidation.MaxDecodedBufferBytes + 1, "test"));
        }

        [TestMethod]
        public void ValidateRange_RejectsOverflowingRanges()
        {
            Assert.ThrowsException<InvalidDataException>(() =>
                CodecValidation.ValidateRange(8, 4, 10, "test"));
        }
    }
}
