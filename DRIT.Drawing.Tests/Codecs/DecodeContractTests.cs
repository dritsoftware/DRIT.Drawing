using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;
using static DRIT.Drawing.Tests.Codecs.CodecTestBuilders;

namespace DRIT.Drawing.Tests.Codecs;

/// <summary>Decode limits, the exception contract of the registry, and the Bitmap API defects.</summary>
[TestClass]
public sealed class DecodeContractTests
{
    // ── Configurable limits ──────────────────────────────────────────────────

    [TestMethod]
    public void Limits_PerCallLimitsApplyToEveryCodec()
    {
        var tight = new ImageDecodeLimits(maxPixels: 100);
        using Bitmap source = Pattern(20, 20);   // 400 pixels

        foreach (ImageFormat format in new[] { ImageFormat.Png, ImageFormat.Bmp, ImageFormat.Gif, ImageFormat.Tiff, ImageFormat.Jpeg })
        {
            byte[] bytes = Encode(source, format);
            using var stream = new MemoryStream(bytes);
            Assert.ThrowsException<InvalidDataException>(() => Image.FromStream(stream, tight), format.Name);

            using var ok = new MemoryStream(bytes);
            using Image decoded = Image.FromStream(ok);   // default limits accept it
            Assert.AreEqual(20, decoded.Width, format.Name);
        }
    }

    [TestMethod]
    public void Limits_ScopeEndsWithTheCall()
    {
        using Bitmap source = Pattern(20, 20);
        byte[] png = Encode(source, ImageFormat.Png);
        using (var stream = new MemoryStream(png))
            Assert.ThrowsException<InvalidDataException>(() => new Bitmap(stream, new ImageDecodeLimits(maxPixels: 10)));

        using Bitmap afterwards = Decode(png);     // a later decode is back on the default limits
        Assert.AreEqual(20, afterwards.Width);
    }

    [TestMethod]
    public void Limits_DefaultCanBeRaisedAndRestored()
    {
        ImageDecodeLimits original = ImageDecodeLimits.Default;
        try
        {
            using Bitmap source = Pattern(30, 30);
            byte[] png = Encode(source, ImageFormat.Png);

            ImageDecodeLimits.Default = new ImageDecodeLimits(maxPixels: 100);
            Assert.ThrowsException<InvalidDataException>(() => Decode(png));

            ImageDecodeLimits.Default = new ImageDecodeLimits(maxPixels: 1000);
            using Bitmap decoded = Decode(png);
            Assert.AreEqual(30, decoded.Width);
        }
        finally
        {
            ImageDecodeLimits.Default = original;
        }
    }

    [TestMethod]
    public void Limits_AllowImagesAboveTheOldFiftyMegapixelCeiling()
    {
        // 8192 x 6144 = 50.3 MP used to be refused by every codec; a big panorama is a legitimate input.
        Assert.IsTrue(ImageDecodeLimits.Default.MaxPixels >= 8192L * 6144);
        DRIT.Drawing.Codecs.CodecValidation.ValidateDimensions(8192, 6144, "test");
    }

    [TestMethod]
    public void Limits_RejectNonsenseArguments()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ImageDecodeLimits(maxPixels: 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ImageDecodeLimits(maxFrameCount: -1));
        Assert.ThrowsException<ArgumentNullException>(() => ImageDecodeLimits.Default = null!);
    }

    // ── Exception contract ───────────────────────────────────────────────────

    [TestMethod]
    public void Decode_NullStream_ThrowsArgumentNull()
        => Assert.ThrowsException<ArgumentNullException>(() => Image.FromStream(null!));

    [TestMethod]
    public void Decode_EmptyStream_IsInvalidDataNotUnsupported()
        => Assert.ThrowsException<InvalidDataException>(() => Decode(Array.Empty<byte>()));

    [TestMethod]
    public void Decode_UnrecognisedBytes_AreNotSupported()
        => Assert.ThrowsException<NotSupportedException>(() => Decode(Ascii("this is not an image at all")));

    [TestMethod]
    public void Decode_StreamThatReturnsOneByteAtATime_IsStillRecognised()
    {
        // A single Read may return fewer bytes than requested; format detection must keep reading.
        using Bitmap source = Pattern(6, 4);
        byte[] png = Encode(source, ImageFormat.Png);
        using var stream = new OneByteAtATimeStream(png);
        using Bitmap decoded = new Bitmap(stream);
        AssertSamePixels(source, decoded);
    }

    private sealed class OneByteAtATimeStream : MemoryStream
    {
        public OneByteAtATimeStream(byte[] bytes) : base(bytes) { }
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 1));
    }

    [TestMethod]
    public void Decode_DamagedFiles_OnlyEverThrowInvalidDataOrNotSupported()
    {
        // Systematic damage (truncation and single-byte corruption) of every format: the contract is that a
        // malformed file surfaces as InvalidDataException (or NotSupported for well-formed but unsupported
        // content) and never as an index, overflow, end-of-stream or argument exception.
        using Bitmap source = Pattern(9, 7);
        var random = new Random(31);
        foreach (ImageFormat format in new[] { ImageFormat.Png, ImageFormat.Bmp, ImageFormat.Gif, ImageFormat.Tiff, ImageFormat.Jpeg })
        {
            byte[] good = Encode(source, format);
            for (int trial = 0; trial < 150; trial++)
            {
                byte[] bytes = (byte[])good.Clone();
                if (trial % 3 == 0)
                {
                    Array.Resize(ref bytes, random.Next(1, bytes.Length));
                }
                else
                {
                    for (int k = 0; k < 1 + trial % 4; k++)
                        bytes[random.Next(bytes.Length)] = (byte)random.Next(256);
                }

                try
                {
                    using var stream = new MemoryStream(bytes);
                    using Bitmap _ = new Bitmap(stream);
                }
                catch (Exception ex) when (ex is InvalidDataException || ex is NotSupportedException)
                {
                }
                catch (Exception ex)
                {
                    Assert.Fail($"{format.Name} trial {trial}: leaked {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
    }

    // ── Bitmap ───────────────────────────────────────────────────────────────

    [TestMethod]
    public void Bitmap_Clone_KeepsRawFormatResolutionAndPixelFormat()
    {
        using Bitmap loaded = Decode(Encode(Pattern(8, 6), ImageFormat.Png));
        loaded._dpiX = 150;
        using var clone = (Bitmap)loaded.Clone();

        Assert.AreEqual(ImageFormat.Png, clone.RawFormat);
        Assert.AreEqual(150f, clone.HorizontalResolution);
        Assert.AreEqual(loaded.PixelFormat, clone.PixelFormat);
        AssertSamePixels(loaded, clone);

        clone.SetPixel(0, 0, Color.Magenta);
        Assert.AreNotEqual(Color.Magenta.ToArgb(), loaded.GetPixel(0, 0).ToArgb(), "a clone must not share pixels");
    }

    [TestMethod]
    public void Bitmap_Clone_WorksForFormatsPixelAccessCannotWrite()
    {
        using var source = new Bitmap(3, 2, PixelFormat.Format16bppRgb565);
        for (int i = 0; i < source._pixels.Length; i++) source._pixels[i] = (byte)(i * 11);
        using var clone = (Bitmap)source.Clone();
        Assert.AreEqual(PixelFormat.Format16bppRgb565, clone.PixelFormat);
        CollectionAssert.AreEqual(source._pixels, clone._pixels);
    }

    [TestMethod]
    public void Bitmap_Clone_KeepsTheAnimation()
    {
        var frames = new List<AnimationFrame>();
        for (int i = 0; i < 3; i++)
        {
            var pixels = new byte[2 * 2 * 4];
            for (int p = 0; p < 4; p++) { pixels[p * 4] = (byte)(80 * (i + 1)); pixels[p * 4 + 3] = 255; }
            frames.Add(new AnimationFrame(new Rectangle(0, 0, 2, 2), pixels) { DelayMilliseconds = 50 * (i + 1) });
        }
        using var animated = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
        animated.SetAnimationFrames(frames, 5);
        animated.SelectActiveFrame(FrameDimension.Time, 1);

        using var clone = (Bitmap)animated.Clone();
        Assert.AreEqual(3, clone.GetFrameCount(FrameDimension.Time));
        Assert.AreEqual(5, clone.GetAnimationLoopCount());
        Assert.AreEqual(animated.GetPixel(0, 0).ToArgb(), clone.GetPixel(0, 0).ToArgb(), "the clone starts on the same frame");
        clone.SelectActiveFrame(FrameDimension.Time, 2);
        animated.SelectActiveFrame(FrameDimension.Time, 2);
        AssertSamePixels(animated, clone);
    }

    [TestMethod]
    public void Bitmap_LockBitsInAnotherFormat_WritesBackOnUnlock()
    {
        using var bitmap = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
        bitmap.SetPixel(0, 0, Color.FromArgb(200, 1, 2, 3));

        BitmapData data = bitmap.LockBits(new Rectangle(0, 0, 2, 2), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
        Marshal.WriteByte(data.Scan0, 0, 0);     // B
        Marshal.WriteByte(data.Scan0, 1, 255);   // G
        Marshal.WriteByte(data.Scan0, 2, 0);     // R
        bitmap.UnlockBits(data);

        Color c = bitmap.GetPixel(0, 0);
        Assert.AreEqual(0, c.R);
        Assert.AreEqual(255, c.G);
        Assert.AreEqual(0, c.B);
    }

    [TestMethod]
    public void Bitmap_ReadOnlyLockInAnotherFormat_DoesNotModifyTheBitmap()
    {
        using var bitmap = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        bitmap.SetPixel(0, 0, Color.FromArgb(255, 10, 20, 30));

        BitmapData data = bitmap.LockBits(new Rectangle(0, 0, 1, 1), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        Assert.AreEqual(30, Marshal.ReadByte(data.Scan0, 0));   // B
        Marshal.WriteByte(data.Scan0, 0, 99);                    // a stray write must be discarded
        bitmap.UnlockBits(data);

        Assert.AreEqual(Color.FromArgb(255, 10, 20, 30).ToArgb(), bitmap.GetPixel(0, 0).ToArgb());
    }

    [TestMethod]
    public void Bitmap_LockBitsInAnUnsupportedFormat_ThrowsInsteadOfReturningZeros()
    {
        using var bitmap = new Bitmap(2, 2, PixelFormat.Format32bppArgb);
        Assert.ThrowsException<NotSupportedException>(() =>
            bitmap.LockBits(new Rectangle(0, 0, 2, 2), ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed));
    }

    [TestMethod]
    public void Bitmap_PremultipliedLock_ConvertsBothWays()
    {
        using var bitmap = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        bitmap.SetPixel(0, 0, Color.FromArgb(128, 255, 0, 0));

        BitmapData data = bitmap.LockBits(new Rectangle(0, 0, 1, 1), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
        Assert.AreEqual(128, Marshal.ReadByte(data.Scan0, 2), "premultiplied red");
        bitmap.UnlockBits(data);

        Color c = bitmap.GetPixel(0, 0);
        Assert.AreEqual(128, c.A);
        Assert.IsTrue(c.R >= 254, $"R={c.R}");
    }

    [TestMethod]
    public void Bitmap_HugeDimensions_ThrowArgumentOutOfRangeInsteadOfOverflowing()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Bitmap(40_000, 40_000, PixelFormat.Format32bppArgb));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Bitmap(int.MaxValue, 1, PixelFormat.Format32bppArgb));
    }
}
