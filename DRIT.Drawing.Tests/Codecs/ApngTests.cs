using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Tests.Codecs
{
    [TestClass]
    public class ApngTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // APNG byte-stream builder (in-test, no fixture files)
        // ─────────────────────────────────────────────────────────────────────

        private static readonly byte[] PngSig = { 137, 80, 78, 71, 13, 10, 26, 10 };

        private static uint s_crcSeed = 0xFFFFFFFFu;

        private static void WriteChunk(MemoryStream ms, string type, byte[] data)
        {
            var typeBuf = Encoding.ASCII.GetBytes(type);
            var lenBuf = new byte[4];
            WriteBE(lenBuf, 0, (uint)data.Length);
            ms.Write(lenBuf, 0, 4);
            ms.Write(typeBuf, 0, 4);
            if (data.Length > 0) ms.Write(data, 0, data.Length);

            var combined = new byte[4 + data.Length];
            Buffer.BlockCopy(typeBuf, 0, combined, 0, 4);
            if (data.Length > 0) Buffer.BlockCopy(data, 0, combined, 4, data.Length);
            uint crc = Crc32(combined, 0, combined.Length);
            ms.WriteByte((byte)(crc >> 24));
            ms.WriteByte((byte)(crc >> 16));
            ms.WriteByte((byte)(crc >> 8));
            ms.WriteByte((byte)crc);
        }

        private static uint Crc32(byte[] buf, int off, int len)
        {
            uint crc = s_crcSeed;
            for (int i = off; i < off + len; i++)
            {
                crc = CrcTable[(crc ^ buf[i]) & 0xFF] ^ (crc >> 8);
            }
            return ~crc;
        }

        private static readonly uint[] CrcTable = BuildCrcTable();
        private static uint[] BuildCrcTable()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : (c >> 1);
                t[n] = c;
            }
            return t;
        }

        private static void WriteBE(byte[] buf, int off, uint v)
        {
            buf[off] = (byte)(v >> 24); buf[off + 1] = (byte)(v >> 16);
            buf[off + 2] = (byte)(v >> 8); buf[off + 3] = (byte)v;
        }

        private static void WriteBE16(byte[] buf, int off, ushort v)
        {
            buf[off] = (byte)(v >> 8); buf[off + 1] = (byte)v;
        }

        private static byte[] MakeIhdr(int w, int h, int bitDepth, int colorType)
        {
            var b = new byte[13];
            WriteBE(b, 0, (uint)w);
            WriteBE(b, 4, (uint)h);
            b[8] = (byte)bitDepth;
            b[9] = (byte)colorType;
            b[10] = 0; b[11] = 0; b[12] = 0;
            return b;
        }

        private static byte[] MakeAcTL(int numFrames, int numPlays)
        {
            var b = new byte[8];
            WriteBE(b, 0, (uint)numFrames);
            WriteBE(b, 4, (uint)numPlays);
            return b;
        }

        private static byte[] MakeFcTL(uint seq, int w, int h, int x, int y,
            ushort delayNum, ushort delayDen, byte dispose, byte blend)
        {
            var b = new byte[26];
            WriteBE(b, 0, seq);
            WriteBE(b, 4, (uint)w);
            WriteBE(b, 8, (uint)h);
            WriteBE(b, 12, (uint)x);
            WriteBE(b, 16, (uint)y);
            WriteBE16(b, 20, delayNum);
            WriteBE16(b, 22, delayDen);
            b[24] = dispose;
            b[25] = blend;
            return b;
        }

        private static byte[] MakeFdAT(uint seq, byte[] compressed)
        {
            var b = new byte[4 + compressed.Length];
            WriteBE(b, 0, seq);
            Buffer.BlockCopy(compressed, 0, b, 4, compressed.Length);
            return b;
        }

        private static byte[] SolidFrameRgba(int w, int h, byte r, byte g, byte b, byte a)
        {
            int rawStride = w * 4;
            var raw = new byte[(rawStride + 1) * h];
            for (int y = 0; y < h; y++)
            {
                int row = y * (rawStride + 1);
                raw[row] = 0;
                for (int x = 0; x < w; x++)
                {
                    int off = row + 1 + x * 4;
                    raw[off] = r; raw[off + 1] = g; raw[off + 2] = b; raw[off + 3] = a;
                }
            }
            return ZlibCompress(raw);
        }

        private static byte[] ZlibCompress(byte[] data)
        {
            using var output = new MemoryStream();
            output.WriteByte(0x78);
            output.WriteByte(0x9C);
            using (var ds = new System.IO.Compression.DeflateStream(output, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
                ds.Write(data, 0, data.Length);
            uint adler = Adler32(data, 0, data.Length);
            output.WriteByte((byte)(adler >> 24));
            output.WriteByte((byte)(adler >> 16));
            output.WriteByte((byte)(adler >> 8));
            output.WriteByte((byte)adler);
            return output.ToArray();
        }

        private static uint Adler32(byte[] data, int off, int len)
        {
            const uint Mod = 65521;
            uint a = 1, b = 0;
            for (int i = off; i < off + len; i++)
            {
                a = (a + data[i]) % Mod;
                b = (b + a) % Mod;
            }
            return (b << 16) | a;
        }

        private static Bitmap DecodeBytes(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            return new Bitmap(ms);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Tests
        // ─────────────────────────────────────────────────────────────────────

        [TestMethod]
        public void Apng_CanDecode_PngSignature()
        {
            byte[] header = { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13 };
            var codec = new DRIT.Drawing.Codecs.PngCodec();
            Assert.IsTrue(codec.CanDecode(header));
        }

        [TestMethod]
        public void Apng_Decode_StaticPng_SingleFrame()
        {
            var ms = new MemoryStream();
            ms.Write(PngSig, 0, 8);
            WriteChunk(ms, "IHDR", MakeIhdr(2, 2, 8, 6));
            WriteChunk(ms, "IDAT", SolidFrameRgba(2, 2, 255, 0, 0, 255));
            WriteChunk(ms, "IEND", Array.Empty<byte>());

            using var bmp = DecodeBytes(ms.ToArray());
            Assert.AreEqual(1, bmp.GetFrameCount(FrameDimension.Time));
            Assert.AreEqual(255, bmp.GetPixel(0, 0).R);
        }

        [TestMethod]
        public void Apng_Decode_DefaultImageIsFrame0()
        {
            var ms = new MemoryStream();
            ms.Write(PngSig, 0, 8);
            WriteChunk(ms, "IHDR", MakeIhdr(2, 2, 8, 6));
            WriteChunk(ms, "acTL", MakeAcTL(2, 0));
            WriteChunk(ms, "fcTL", MakeFcTL(0, 2, 2, 0, 0, 10, 100, 0, 0));
            WriteChunk(ms, "IDAT", SolidFrameRgba(2, 2, 255, 0, 0, 255));
            WriteChunk(ms, "fcTL", MakeFcTL(1, 2, 2, 0, 0, 10, 100, 0, 0));
            WriteChunk(ms, "fdAT", MakeFdAT(2, SolidFrameRgba(2, 2, 0, 255, 0, 255)));
            WriteChunk(ms, "IEND", Array.Empty<byte>());

            using var bmp = DecodeBytes(ms.ToArray());
            Assert.AreEqual(2, bmp.GetFrameCount(FrameDimension.Time));

            bmp.SelectActiveFrame(FrameDimension.Time, 0);
            Assert.AreEqual(255, bmp.GetPixel(0, 0).R);
            Assert.AreEqual(0, bmp.GetPixel(0, 0).G);

            bmp.SelectActiveFrame(FrameDimension.Time, 1);
            Assert.AreEqual(0, bmp.GetPixel(0, 0).R);
            Assert.AreEqual(255, bmp.GetPixel(0, 0).G);
        }

        [TestMethod]
        public void Apng_Decode_DefaultImageNotInAnimation()
        {
            var ms = new MemoryStream();
            ms.Write(PngSig, 0, 8);
            WriteChunk(ms, "IHDR", MakeIhdr(2, 2, 8, 6));
            WriteChunk(ms, "acTL", MakeAcTL(1, 0));
            WriteChunk(ms, "IDAT", SolidFrameRgba(2, 2, 255, 0, 0, 255));
            WriteChunk(ms, "fcTL", MakeFcTL(0, 2, 2, 0, 0, 10, 100, 0, 0));
            WriteChunk(ms, "fdAT", MakeFdAT(1, SolidFrameRgba(2, 2, 0, 255, 0, 255)));
            WriteChunk(ms, "IEND", Array.Empty<byte>());

            using var bmp = DecodeBytes(ms.ToArray());
            Assert.AreEqual(1, bmp.GetFrameCount(FrameDimension.Time));

            Assert.AreEqual(255, bmp.GetPixel(0, 0).R);

            bmp.SelectActiveFrame(FrameDimension.Time, 0);
            Assert.AreEqual(0, bmp.GetPixel(0, 0).R);
            Assert.AreEqual(255, bmp.GetPixel(0, 0).G);
        }

        [TestMethod]
        public void Apng_Decode_SubRectPlacement()
        {
            var ms = new MemoryStream();
            ms.Write(PngSig, 0, 8);
            WriteChunk(ms, "IHDR", MakeIhdr(4, 4, 8, 6));
            WriteChunk(ms, "acTL", MakeAcTL(2, 0));
            WriteChunk(ms, "fcTL", MakeFcTL(0, 4, 4, 0, 0, 10, 100, 0, 0));
            WriteChunk(ms, "IDAT", SolidFrameRgba(4, 4, 0, 0, 0, 255));
            WriteChunk(ms, "fcTL", MakeFcTL(1, 2, 2, 1, 1, 10, 100, 0, 0));
            WriteChunk(ms, "fdAT", MakeFdAT(2, SolidFrameRgba(2, 2, 255, 255, 255, 255)));
            WriteChunk(ms, "IEND", Array.Empty<byte>());

            using var bmp = DecodeBytes(ms.ToArray());
            bmp.SelectActiveFrame(FrameDimension.Time, 1);

            Assert.AreEqual(0, bmp.GetPixel(0, 0).R);
            Assert.AreEqual(255, bmp.GetPixel(1, 1).R);
            Assert.AreEqual(255, bmp.GetPixel(2, 2).R);
            Assert.AreEqual(0, bmp.GetPixel(3, 3).R);
        }

        [TestMethod]
        public void Apng_Decode_DisposeNone()
        {
            var ms = new MemoryStream();
            ms.Write(PngSig, 0, 8);
            WriteChunk(ms, "IHDR", MakeIhdr(2, 2, 8, 6));
            WriteChunk(ms, "acTL", MakeAcTL(2, 0));
            WriteChunk(ms, "fcTL", MakeFcTL(0, 2, 2, 0, 0, 10, 100, 0, 0));
            WriteChunk(ms, "IDAT", SolidFrameRgba(2, 2, 255, 0, 0, 255));
            WriteChunk(ms, "fcTL", MakeFcTL(1, 1, 1, 0, 0, 10, 100, 0, 0));
            WriteChunk(ms, "fdAT", MakeFdAT(2, SolidFrameRgba(1, 1, 0, 255, 0, 255)));
            WriteChunk(ms, "IEND", Array.Empty<byte>());

            using var bmp = DecodeBytes(ms.ToArray());
            bmp.SelectActiveFrame(FrameDimension.Time, 1);

            Assert.AreEqual(0, bmp.GetPixel(0, 0).R);
            Assert.AreEqual(255, bmp.GetPixel(0, 0).G);
            Assert.AreEqual(255, bmp.GetPixel(1, 1).R);
        }

        [TestMethod]
        public void Apng_Decode_DisposeBackground()
        {
            var ms = new MemoryStream();
            ms.Write(PngSig, 0, 8);
            WriteChunk(ms, "IHDR", MakeIhdr(2, 2, 8, 6));
            WriteChunk(ms, "acTL", MakeAcTL(2, 0));
            WriteChunk(ms, "fcTL", MakeFcTL(0, 2, 2, 0, 0, 10, 100, 1, 0));
            WriteChunk(ms, "IDAT", SolidFrameRgba(2, 2, 255, 0, 0, 255));
            WriteChunk(ms, "fcTL", MakeFcTL(1, 1, 1, 0, 0, 10, 100, 0, 0));
            WriteChunk(ms, "fdAT", MakeFdAT(2, SolidFrameRgba(1, 1, 0, 255, 0, 255)));
            WriteChunk(ms, "IEND", Array.Empty<byte>());

            using var bmp = DecodeBytes(ms.ToArray());
            bmp.SelectActiveFrame(FrameDimension.Time, 1);

            Assert.AreEqual(0, bmp.GetPixel(0, 0).R);
            Assert.AreEqual(255, bmp.GetPixel(0, 0).G);
            Assert.AreEqual(0, bmp.GetPixel(1, 1).A);
        }

        [TestMethod]
        public void Apng_Decode_DisposePrevious()
        {
            var ms = new MemoryStream();
            ms.Write(PngSig, 0, 8);
            WriteChunk(ms, "IHDR", MakeIhdr(2, 2, 8, 6));
            WriteChunk(ms, "acTL", MakeAcTL(3, 0));
            WriteChunk(ms, "fcTL", MakeFcTL(0, 2, 2, 0, 0, 10, 100, 0, 0));
            WriteChunk(ms, "IDAT", SolidFrameRgba(2, 2, 100, 100, 100, 255));
            WriteChunk(ms, "fcTL", MakeFcTL(1, 2, 2, 0, 0, 10, 100, 2, 0));
            WriteChunk(ms, "fdAT", MakeFdAT(2, SolidFrameRgba(2, 2, 200, 200, 200, 255)));
            WriteChunk(ms, "fcTL", MakeFcTL(3, 1, 1, 0, 0, 10, 100, 0, 0));
            WriteChunk(ms, "fdAT", MakeFdAT(4, SolidFrameRgba(1, 1, 50, 50, 50, 255)));
            WriteChunk(ms, "IEND", Array.Empty<byte>());

            using var bmp = DecodeBytes(ms.ToArray());
            bmp.SelectActiveFrame(FrameDimension.Time, 2);

            Assert.AreEqual(50, bmp.GetPixel(0, 0).R);
            Assert.AreEqual(100, bmp.GetPixel(1, 1).R);
        }

        [TestMethod]
        public void Apng_Decode_BlendSource()
        {
            var ms = new MemoryStream();
            ms.Write(PngSig, 0, 8);
            WriteChunk(ms, "IHDR", MakeIhdr(2, 2, 8, 6));
            WriteChunk(ms, "acTL", MakeAcTL(2, 0));
            WriteChunk(ms, "fcTL", MakeFcTL(0, 2, 2, 0, 0, 10, 100, 0, 0));
            WriteChunk(ms, "IDAT", SolidFrameRgba(2, 2, 255, 0, 0, 255));
            WriteChunk(ms, "fcTL", MakeFcTL(1, 1, 1, 0, 0, 10, 100, 0, 0));
            WriteChunk(ms, "fdAT", MakeFdAT(2, SolidFrameRgba(1, 1, 0, 255, 0, 128)));
            WriteChunk(ms, "IEND", Array.Empty<byte>());

            using var bmp = DecodeBytes(ms.ToArray());
            bmp.SelectActiveFrame(FrameDimension.Time, 1);

            Assert.AreEqual(0, bmp.GetPixel(0, 0).R);
            Assert.AreEqual(255, bmp.GetPixel(0, 0).G);
            Assert.AreEqual(128, bmp.GetPixel(0, 0).A);
        }

        [TestMethod]
        public void Apng_Decode_BlendOver()
        {
            var ms = new MemoryStream();
            ms.Write(PngSig, 0, 8);
            WriteChunk(ms, "IHDR", MakeIhdr(2, 2, 8, 6));
            WriteChunk(ms, "acTL", MakeAcTL(2, 0));
            WriteChunk(ms, "fcTL", MakeFcTL(0, 2, 2, 0, 0, 10, 100, 0, 0));
            WriteChunk(ms, "IDAT", SolidFrameRgba(2, 2, 255, 0, 0, 255));
            WriteChunk(ms, "fcTL", MakeFcTL(1, 1, 1, 0, 0, 10, 100, 0, 1));
            WriteChunk(ms, "fdAT", MakeFdAT(2, SolidFrameRgba(1, 1, 0, 255, 0, 128)));
            WriteChunk(ms, "IEND", Array.Empty<byte>());

            using var bmp = DecodeBytes(ms.ToArray());
            bmp.SelectActiveFrame(FrameDimension.Time, 1);

            int r = bmp.GetPixel(0, 0).R;
            int g = bmp.GetPixel(0, 0).G;
            Assert.IsTrue(r > 0 && r < 255, $"BlendOver R should be mixed, got {r}");
            Assert.IsTrue(g > 0 && g < 255, $"BlendOver G should be mixed, got {g}");
        }

        [TestMethod]
        public void Apng_Decode_DelayMilliseconds()
        {
            var ms = new MemoryStream();
            ms.Write(PngSig, 0, 8);
            WriteChunk(ms, "IHDR", MakeIhdr(2, 2, 8, 6));
            WriteChunk(ms, "acTL", MakeAcTL(1, 0));
            WriteChunk(ms, "fcTL", MakeFcTL(0, 2, 2, 0, 0, 2, 100, 0, 0));
            WriteChunk(ms, "IDAT", SolidFrameRgba(2, 2, 255, 0, 0, 255));
            WriteChunk(ms, "IEND", Array.Empty<byte>());

            using var bmp = DecodeBytes(ms.ToArray());
            var frames = bmp.GetAnimationFrames();
            Assert.IsNotNull(frames);
            Assert.AreEqual(1, frames.Count);
            Assert.AreEqual(20, frames[0].DelayMilliseconds);
        }

        [TestMethod]
        public void Apng_Decode_DelayDenZero_TreatedAs100()
        {
            var ms = new MemoryStream();
            ms.Write(PngSig, 0, 8);
            WriteChunk(ms, "IHDR", MakeIhdr(2, 2, 8, 6));
            WriteChunk(ms, "acTL", MakeAcTL(1, 0));
            WriteChunk(ms, "fcTL", MakeFcTL(0, 2, 2, 0, 0, 5, 0, 0, 0));
            WriteChunk(ms, "IDAT", SolidFrameRgba(2, 2, 255, 0, 0, 255));
            WriteChunk(ms, "IEND", Array.Empty<byte>());

            using var bmp = DecodeBytes(ms.ToArray());
            var frames = bmp.GetAnimationFrames();
            Assert.IsNotNull(frames);
            Assert.AreEqual(50, frames[0].DelayMilliseconds);
        }

        [TestMethod]
        public void Apng_Decode_LoopCount()
        {
            var ms = new MemoryStream();
            ms.Write(PngSig, 0, 8);
            WriteChunk(ms, "IHDR", MakeIhdr(2, 2, 8, 6));
            WriteChunk(ms, "acTL", MakeAcTL(1, 3));
            WriteChunk(ms, "fcTL", MakeFcTL(0, 2, 2, 0, 0, 10, 100, 0, 0));
            WriteChunk(ms, "IDAT", SolidFrameRgba(2, 2, 255, 0, 0, 255));
            WriteChunk(ms, "IEND", Array.Empty<byte>());

            using var bmp = DecodeBytes(ms.ToArray());
            Assert.AreEqual(3, bmp.GetAnimationLoopCount());
        }

        [TestMethod]
        public void Apng_RoundTrip_MultiFrame()
        {
            var f0 = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) f0.SetPixel(x, y, Color.FromArgb(255, 200, 0, 0));
            var f1 = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) f1.SetPixel(x, y, Color.FromArgb(255, 0, 200, 0));
            var f2 = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) f2.SetPixel(x, y, Color.FromArgb(255, 0, 0, 200));

            var frames = new List<AnimationFrame>
            {
                FrameFromBitmap(f0, 0),
                FrameFromBitmap(f1, 1),
                FrameFromBitmap(f2, 2),
            };

            var anim = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            anim.SetAnimationFrames(frames, 0);

            using var ms = new MemoryStream();
            anim.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            using var decoded = new Bitmap(ms);

            Assert.AreEqual(3, decoded.GetFrameCount(FrameDimension.Time));

            decoded.SelectActiveFrame(FrameDimension.Time, 0);
            Assert.AreEqual(200, decoded.GetPixel(0, 0).R);

            decoded.SelectActiveFrame(FrameDimension.Time, 1);
            Assert.AreEqual(200, decoded.GetPixel(0, 0).G);

            decoded.SelectActiveFrame(FrameDimension.Time, 2);
            Assert.AreEqual(200, decoded.GetPixel(0, 0).B);
        }

        [TestMethod]
        public void Apng_Encode_SingleFrame_IsPlainPng()
        {
            using var src = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            src.SetPixel(0, 0, Color.FromArgb(255, 100, 150, 200));

            using var ms = new MemoryStream();
            src.Save(ms, ImageFormat.Png);
            byte[] bytes = ms.ToArray();

            Assert.IsFalse(HasChunk(bytes, "acTL"), "Single-frame PNG must not contain acTL");
            Assert.IsFalse(HasChunk(bytes, "fcTL"), "Single-frame PNG must not contain fcTL");
            Assert.IsFalse(HasChunk(bytes, "fdAT"), "Single-frame PNG must not contain fdAT");
        }

        [TestMethod]
        public void Apng_Encode_WritesAcTlBeforeIdat()
        {
            var f0 = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) f0.SetPixel(x, y, Color.Red);
            var f1 = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) f1.SetPixel(x, y, Color.Blue);

            var frames = new List<AnimationFrame>
            {
                FrameFromBitmap(f0, 0),
                FrameFromBitmap(f1, 1),
            };
            var anim = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            anim.SetAnimationFrames(frames, 0);

            using var ms = new MemoryStream();
            anim.Save(ms, ImageFormat.Png);
            byte[] bytes = ms.ToArray();

            int actlPos = FindChunkPos(bytes, "acTL");
            int idatPos = FindChunkPos(bytes, "IDAT");
            Assert.IsTrue(actlPos > 0, "acTL must be present");
            Assert.IsTrue(idatPos > 0, "IDAT must be present");
            Assert.IsTrue(actlPos < idatPos, "acTL must precede first IDAT");
        }

        [TestMethod]
        public void Apng_Encode_SequenceNumbersMonotonic()
        {
            var f0 = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) f0.SetPixel(x, y, Color.Red);
            var f1 = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) f1.SetPixel(x, y, Color.Green);
            var f2 = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) f2.SetPixel(x, y, Color.Blue);

            var frames = new List<AnimationFrame>
            {
                FrameFromBitmap(f0, 0),
                FrameFromBitmap(f1, 1),
                FrameFromBitmap(f2, 2),
            };
            var anim = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            anim.SetAnimationFrames(frames, 0);

            using var ms = new MemoryStream();
            anim.Save(ms, ImageFormat.Png);
            byte[] bytes = ms.ToArray();

            var seqs = ReadSequenceNumbers(bytes);
            CollectionAssert.AreEqual(new uint[] { 0, 1, 2, 3, 4 }, seqs);
        }

        [TestMethod]
        public void Apng_Regression_StaticPngRoundTrip()
        {
            using var src = CodecsTestHelpers.MakeRgb(4, 4, Color.FromArgb(255, 200, 100, 50));
            using var dst = CodecsTestHelpers.RoundTrip(src, ImageFormat.Png);

            Assert.AreEqual(4, dst.Width);
            Assert.AreEqual(4, dst.Height);
            var px = dst.GetPixel(2, 2);
            Assert.AreEqual(200, px.R);
            Assert.AreEqual(100, px.G);
            Assert.AreEqual(50, px.B);
        }

        [TestMethod]
        public void Apng_Regression_PreservesAlpha()
        {
            using var src = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
            src.SetPixel(0, 0, Color.FromArgb(128, 255, 0, 0));
            src.SetPixel(1, 0, Color.FromArgb(0, 0, 255, 0));
            src.SetPixel(0, 1, Color.FromArgb(255, 0, 0, 255));
            src.SetPixel(1, 1, Color.FromArgb(64, 255, 255, 0));

            using var dst = CodecsTestHelpers.RoundTrip(src, ImageFormat.Png);

            Assert.AreEqual(128, dst.GetPixel(0, 0).A);
            Assert.AreEqual(0, dst.GetPixel(1, 0).A);
            Assert.AreEqual(255, dst.GetPixel(0, 1).A);
            Assert.AreEqual(64, dst.GetPixel(1, 1).A);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers for encode tests
        // ─────────────────────────────────────────────────────────────────────

        private static AnimationFrame FrameFromBitmap(Bitmap src, int delayMs)
        {
            int w = src.Width, h = src.Height;
            var pixels = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = src.GetPixel(x, y);
                int off = (y * w + x) * 4;
                pixels[off] = c.B;
                pixels[off + 1] = c.G;
                pixels[off + 2] = c.R;
                pixels[off + 3] = c.A;
            }
            return new AnimationFrame(new DRIT.Drawing.Geometry.Rectangle(0, 0, w, h), pixels)
            {
                DelayMilliseconds = delayMs,
                DisposeMethod = FrameDisposeMethod.None,
                BlendMethod = FrameBlendMethod.Source
            };
        }

        private static bool HasChunk(byte[] bytes, string type)
            => FindChunkPos(bytes, type) > 0;

        private static int FindChunkPos(byte[] bytes, string type)
        {
            for (int i = 8; i + 8 <= bytes.Length; )
            {
                int len = (bytes[i] << 24) | (bytes[i + 1] << 16) | (bytes[i + 2] << 8) | bytes[i + 3];
                string t = Encoding.ASCII.GetString(bytes, i + 4, 4);
                if (t == type) return i;
                i += 8 + len + 4;
                if (t == "IEND") break;
            }
            return -1;
        }

        private static List<uint> ReadSequenceNumbers(byte[] bytes)
        {
            var seqs = new List<uint>();
            for (int i = 8; i + 8 <= bytes.Length; )
            {
                int len = (bytes[i] << 24) | (bytes[i + 1] << 16) | (bytes[i + 2] << 8) | bytes[i + 3];
                string t = Encoding.ASCII.GetString(bytes, i + 4, 4);
                if (t == "fcTL" && len >= 26)
                {
                    uint seq = ((uint)bytes[i + 8] << 24) | ((uint)bytes[i + 9] << 16)
                             | ((uint)bytes[i + 10] << 8) | bytes[i + 11];
                    seqs.Add(seq);
                }
                else if (t == "fdAT" && len >= 4)
                {
                    uint seq = ((uint)bytes[i + 8] << 24) | ((uint)bytes[i + 9] << 16)
                             | ((uint)bytes[i + 10] << 8) | bytes[i + 11];
                    seqs.Add(seq);
                }
                i += 8 + len + 4;
                if (t == "IEND") break;
            }
            return seqs;
        }
    }
}