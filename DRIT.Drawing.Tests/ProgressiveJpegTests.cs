using System;
using System.IO;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class ProgressiveJpegTests
    {
        private const string ProgressiveSolid16x16JpegBase64 =
            "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wgARCAAQABADASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAX/xAAVAQEBAAAAAAAAAAAAAAAAAAAGB//aAAwDAQACEAMQAAABigjTP//EABQQAQAAAAAAAAAAAAAAAAAAACD/2gAIAQEAAQUCH//EABQRAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQMBAT8Bf//EABQRAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQIBAT8Bf//EABQQAQAAAAAAAAAAAAAAAAAAACD/2gAIAQEABj8CH//EABQQAQAAAAAAAAAAAAAAAAAAACD/2gAIAQEAAT8hH//aAAwDAQACAAMAAAAQ+//EABQRAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQMBAT8Qf//EABQRAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQIBAT8Qf//EABQQAQAAAAAAAAAAAAAAAAAAACD/2gAIAQEAAT8QH//Z";

        private const string ProgressiveGray16x16JpegBase64 =
            "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/wgALCAAQABABAREA/8QAFgABAQEAAAAAAAAAAAAAAAAAAgAF/9oACAEBAAAAAccR/8QAGBABAQEBAQAAAAAAAAAAAAAAAwIEBSP/2gAIAQEAAQUC8uIABOeACc8ES9Z//8QAIRAAAgECBgMAAAAAAAAAAAAAAQIEAAMRFCEiQVFhwfD/2gAIAQEABj8CUBRclONlv2fFPOnOWJOp5Y9CnnTnLEnU8sehWYkYW7SDauO1B9zX/8QAHxABAAEEAQUAAAAAAAAAAAAAASEAEUGBMWFxkcHw/9oACAEBAAE/IQlyDx9u/lL4AyF8cNQcAYCr4AyF8cNQcAYCpCOV6nOp9AD/2gAIAQEAAAAQb//EABkQAQEAAwEAAAAAAAAAAAAAAAERITFBAP/aAAgBAQABPxC6mmgNYMgRhhYhJ4GvoGQOeLICdAIa+gZA54sgJ0AmsFDB5SoLAtLOeB//2Q==";

        private static byte[] DecodeFixture(string base64) => Convert.FromBase64String(base64);

        [TestMethod]
        public void ProgressiveJpeg_Decode_SolidColor_CenterPixelClose()
        {
            byte[] bytes = DecodeFixture(ProgressiveSolid16x16JpegBase64);

            using var decoded = new Bitmap(new MemoryStream(bytes));
            Assert.AreEqual(16, decoded.Width);
            Assert.AreEqual(16, decoded.Height);

            Color c = decoded.GetPixel(8, 8);
            Assert.IsTrue(Math.Abs(c.R - 190) <= 18, $"R expected near 190, actual {c.R}");
            Assert.IsTrue(Math.Abs(c.G - 80) <= 18, $"G expected near 80, actual {c.G}");
            Assert.IsTrue(Math.Abs(c.B - 50) <= 18, $"B expected near 50, actual {c.B}");
        }

        [TestMethod]
        public void ProgressiveJpeg_Decode_Grayscale_CenterPixelGray()
        {
            byte[] bytes = DecodeFixture(ProgressiveGray16x16JpegBase64);

            using var decoded = new Bitmap(new MemoryStream(bytes));
            Assert.AreEqual(16, decoded.Width);
            Assert.AreEqual(16, decoded.Height);

            Color c = decoded.GetPixel(8, 8);
            Assert.IsTrue(Math.Abs(c.R - c.G) <= 2, $"R/G mismatch: {c.R}/{c.G}");
            Assert.IsTrue(Math.Abs(c.G - c.B) <= 2, $"G/B mismatch: {c.G}/{c.B}");
        }

        [TestMethod]
        public void ProgressiveJpeg_Decode_SolidFixture_SampleGridWithinTolerance()
        {
            byte[] bytes = DecodeFixture(ProgressiveSolid16x16JpegBase64);
            using var own = new Bitmap(new MemoryStream(bytes));
            Assert.AreEqual(16, own.Width);
            Assert.AreEqual(16, own.Height);

            for (int y = 2; y < own.Height; y += 4)
            {
                for (int x = 2; x < own.Width; x += 4)
                {
                    Color actual = own.GetPixel(x, y);
                    Assert.IsTrue(Math.Abs(actual.R - 190) <= 20, $"R mismatch at ({x},{y}): {actual.R}");
                    Assert.IsTrue(Math.Abs(actual.G - 80) <= 20, $"G mismatch at ({x},{y}): {actual.G}");
                    Assert.IsTrue(Math.Abs(actual.B - 50) <= 20, $"B mismatch at ({x},{y}): {actual.B}");
                }
            }
        }

        // The Issue #1 fixture is verified pixel-wise against libjpeg in
        // Codecs/Jpeg/JpegCompatibilityTests.ProgressiveJpeg_Issue1Flowers_MatchesReferenceDecoder.
    }
}
