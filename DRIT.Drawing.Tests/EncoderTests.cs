using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class EncoderTests
    {
        [TestMethod]
        public void Encoder_StaticProps_NotNull()
        {
            Assert.IsNotNull(Encoder.Quality);
            Assert.IsNotNull(Encoder.Compression);
            Assert.IsNotNull(Encoder.ColorDepth);
            Assert.IsNotNull(Encoder.SaveFlag);
        }

        [TestMethod]
        public void EncoderParameter_LongValue()
        {
            var ep = new EncoderParameter(Encoder.Quality, 80L);
            Assert.AreEqual(Encoder.Quality.Guid, ep.Encoder);
            Assert.AreEqual(80L, ep._valueLong);
        }

        [TestMethod]
        public void EncoderParameters_DefaultCtor()
        {
            var eps = new EncoderParameters();
            Assert.AreEqual(0, eps.Param.Length);
        }

        [TestMethod]
        public void EncoderParameters_CountCtor()
        {
            var eps = new EncoderParameters(3);
            Assert.AreEqual(3, eps.Param.Length);
        }
    }
}