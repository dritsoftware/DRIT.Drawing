using System;
using System.Collections.Generic;
using System.IO;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;
using static DRIT.Drawing.Tests.Codecs.CodecTestBuilders;

namespace DRIT.Drawing.Tests.Codecs;

[TestClass]
public sealed class EncoderFidelityTests
{
    // ── Any pixel format reaches the encoders as straight colour ─────────────

    [DataTestMethod]
    [DataRow("Png")]
    [DataRow("Bmp")]
    [DataRow("Tiff")]
    public void Encoders_PremultipliedBitmaps_AreWrittenAsStraightAlpha(string formatName)
    {
        ImageFormat format = formatName == "Png" ? ImageFormat.Png : formatName == "Bmp" ? ImageFormat.Bmp : ImageFormat.Tiff;
        using var source = new Bitmap(2, 1, PixelFormat.Format32bppPArgb);
        source.SetPixel(0, 0, Color.FromArgb(128, 255, 0, 0));
        source.SetPixel(1, 0, Color.FromArgb(255, 10, 20, 30));

        using Bitmap decoded = Decode(Encode(source, format));
        Color c = decoded.GetPixel(0, 0);
        Assert.AreEqual(128, c.A);
        Assert.IsTrue(c.R >= 254, $"red was darkened to {c.R}");
        Assert.AreEqual(Color.FromArgb(255, 10, 20, 30).ToArgb(), decoded.GetPixel(1, 0).ToArgb());
    }

    [TestMethod]
    public void Encoders_SixteenBitRgb565Bitmaps_KeepTheirColours()
    {
        using var source = new Bitmap(2, 1, PixelFormat.Format16bppRgb565);
        // 0xF800 = pure red, 0x07E0 = pure green (little-endian pixel words).
        source._pixels[0] = 0x00; source._pixels[1] = 0xF8;
        source._pixels[2] = 0xE0; source._pixels[3] = 0x07;

        foreach (ImageFormat format in new[] { ImageFormat.Png, ImageFormat.Bmp, ImageFormat.Tiff })
        {
            using Bitmap decoded = Decode(Encode(source, format));
            Assert.AreEqual(Color.FromArgb(255, 255, 0, 0).ToArgb(), decoded.GetPixel(0, 0).ToArgb(), format.Name);
            Assert.AreEqual(Color.FromArgb(255, 0, 255, 0).ToArgb(), decoded.GetPixel(1, 0).ToArgb(), format.Name);
        }
    }

    [TestMethod]
    public void Bmp_ThirtyTwoBitRgbBitmapWithEmptyFourthByte_IsWrittenOpaque()
    {
        using var source = new Bitmap(1, 1, PixelFormat.Format32bppRgb);
        source._pixels[0] = 30; source._pixels[1] = 20; source._pixels[2] = 10; source._pixels[3] = 0;
        using Bitmap decoded = Decode(Encode(source, ImageFormat.Bmp));
        Assert.AreEqual(Color.FromArgb(255, 10, 20, 30).ToArgb(), decoded.GetPixel(0, 0).ToArgb());
    }

    // ── GIF colour reduction ─────────────────────────────────────────────────

    [TestMethod]
    public void Gif_ImagesWithMoreThan256Colours_AreQuantizedNotCorrupted()
    {
        // 64 x 64 smooth gradient with ~4000 distinct colours. Truncating the palette to the first 256 colours
        // turned every other pixel black (index 0); a proper reduction stays close to the original everywhere.
        using var source = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
            source.SetPixel(x, y, Color.FromArgb(255, x * 4, y * 4, (x + y) * 2));

        using Bitmap decoded = Decode(Encode(source, ImageFormat.Gif));
        long sum = 0;
        int worst = 0;
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            Color a = source.GetPixel(x, y), b = decoded.GetPixel(x, y);
            int error = Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
            sum += error;
            worst = Math.Max(worst, error);
            Assert.AreEqual(255, b.A);
        }

        Assert.IsTrue(sum / (64.0 * 64 * 3) < 6.0, $"mean error {sum / (64.0 * 64 * 3):F2}");
        Assert.IsTrue(worst < 60, $"worst pixel error {worst}");
    }

    [TestMethod]
    public void Gif_Quantization_KeepsTransparencyAndSmallPalettesExact()
    {
        using var source = new Bitmap(40, 40, PixelFormat.Format32bppArgb);
        for (int y = 0; y < 40; y++)
        for (int x = 0; x < 40; x++)
            source.SetPixel(x, y, (x + y) % 5 == 0 ? Color.FromArgb(0, 0, 0, 0) : Color.FromArgb(255, x * 6, y * 6, 100));

        using Bitmap decoded = Decode(Encode(source, ImageFormat.Gif));
        int transparent = 0;
        for (int y = 0; y < 40; y++)
        for (int x = 0; x < 40; x++)
        {
            if ((x + y) % 5 == 0)
            {
                Assert.AreEqual(0, decoded.GetPixel(x, y).A, $"({x},{y}) must stay transparent");
                transparent++;
            }
            else
            {
                Assert.AreEqual(255, decoded.GetPixel(x, y).A);
            }
        }
        Assert.IsTrue(transparent > 100);
    }

    [TestMethod]
    public void Gif_ImagesLargerThan65535Pixels_AreRejected()
    {
        using var wide = new Bitmap(65536, 1, PixelFormat.Format32bppArgb);
        Assert.ThrowsException<ArgumentException>(() => Encode(wide, ImageFormat.Gif));
    }

    // ── Animation survives a save ────────────────────────────────────────────

    private static Bitmap AnimatedBitmap(int loops, params int[] delays)
    {
        var frames = new List<AnimationFrame>();
        for (int i = 0; i < delays.Length; i++)
        {
            byte shade = (byte)(60 * (i + 1));
            var pixels = new byte[3 * 3 * 4];
            for (int p = 0; p < 9; p++)
            {
                pixels[p * 4] = shade; pixels[p * 4 + 1] = (byte)(255 - shade); pixels[p * 4 + 2] = 40; pixels[p * 4 + 3] = 255;
            }
            frames.Add(new AnimationFrame(new Rectangle(0, 0, 3, 3), pixels)
            {
                DelayMilliseconds = delays[i],
                BlendMethod = FrameBlendMethod.Source,
                DisposeMethod = FrameDisposeMethod.None,
            });
        }

        var bitmap = new Bitmap(3, 3, PixelFormat.Format32bppArgb);
        bitmap.SetAnimationFrames(frames, loops);
        return bitmap;
    }

    [DataTestMethod]
    [DataRow("Gif")]
    [DataRow("Png")]
    public void Animations_KeepFramesDelaysAndLoopCountThroughSave(string formatName)
    {
        ImageFormat format = formatName == "Gif" ? ImageFormat.Gif : ImageFormat.Png;
        using Bitmap source = AnimatedBitmap(3, 100, 250, 40);

        // Saving must not move the source's active frame.
        source.SelectActiveFrame(FrameDimension.Time, 1);
        Color before = source.GetPixel(1, 1);

        using Bitmap decoded = Decode(Encode(source, format));

        Assert.AreEqual(before.ToArgb(), source.GetPixel(1, 1).ToArgb(), "the source's active frame changed");
        Assert.AreEqual(3, decoded.GetFrameCount(FrameDimension.Time));
        Assert.AreEqual(3, decoded.GetAnimationLoopCount());
        List<AnimationFrame> frames = decoded.GetAnimationFrames()!;
        Assert.AreEqual(100, frames[0].DelayMilliseconds);
        Assert.AreEqual(250, frames[1].DelayMilliseconds);
        Assert.AreEqual(40, frames[2].DelayMilliseconds);

        for (int i = 0; i < 3; i++)
        {
            source.SelectActiveFrame(FrameDimension.Time, i);
            decoded.SelectActiveFrame(FrameDimension.Time, i);
            AssertSamePixels(source, decoded, $"{formatName} frame {i}");
        }
    }
}
