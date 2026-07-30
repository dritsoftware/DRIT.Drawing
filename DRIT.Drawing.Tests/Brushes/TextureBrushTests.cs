using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Brushes;

namespace DRIT.Drawing.Tests.Brushes
{
    [TestClass]
    public class TextureBrushTests
    {
        [TestMethod]
        public void TextureBrush_Ctor_StoresImage()
        {
            using var bmp = new Bitmap(10, 10);
            using var tb  = new TextureBrush(bmp);
            Assert.AreSame(bmp, tb.Image);
        }

        [TestMethod]
        public void TextureBrush_DefaultWrapMode_Tile()
        {
            using var bmp = new Bitmap(4, 4);
            using var tb  = new TextureBrush(bmp);
            Assert.AreEqual(WrapMode.Tile, tb.WrapMode);
        }

        [TestMethod]
        public void TextureBrush_SetWrapMode()
        {
            using var bmp = new Bitmap(4, 4);
            using var tb  = new TextureBrush(bmp);
            tb.WrapMode = WrapMode.Clamp;
            Assert.AreEqual(WrapMode.Clamp, tb.WrapMode);
        }

        [TestMethod]
        public void TextureBrush_TranslateTransform_SetsOffset()
        {
            using var bmp = new Bitmap(4, 4);
            using var tb  = new TextureBrush(bmp);
            tb.TranslateTransform(10f, 20f);
            Assert.AreEqual(10f, tb.Transform.OffsetX, 1e-4f);
            Assert.AreEqual(20f, tb.Transform.OffsetY, 1e-4f);
        }

        [TestMethod]
        public void TextureBrush_ResetTransform_Identity()
        {
            using var bmp = new Bitmap(4, 4);
            using var tb  = new TextureBrush(bmp);
            tb.TranslateTransform(5f, 5f);
            tb.ResetTransform();
            Assert.IsTrue(tb.Transform.IsIdentity);
        }

        [TestMethod]
        public void TextureBrush_Clone_IsIndependent()
        {
            using var bmp = new Bitmap(4, 4);
            using var tb  = new TextureBrush(bmp);
            tb.WrapMode = WrapMode.TileFlipX;
            using var clone = (TextureBrush)tb.Clone();
            tb.WrapMode = WrapMode.Clamp;
            Assert.AreEqual(WrapMode.TileFlipX, clone.WrapMode);
        }
    }
}