using System;
using System.Collections.Generic;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Tests.Imaging;

[TestClass]
public sealed class FrameAnimatorTests
{
    private static AnimationFrame Solid(Rectangle bounds, byte b, byte g, byte r, byte a,
        FrameDisposeMethod dispose = FrameDisposeMethod.None, FrameBlendMethod blend = FrameBlendMethod.Source)
    {
        var pixels = new byte[bounds.Width * bounds.Height * 4];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = b; pixels[i + 1] = g; pixels[i + 2] = r; pixels[i + 3] = a;
        }
        return new AnimationFrame(bounds, pixels) { DisposeMethod = dispose, BlendMethod = blend };
    }

    private static int Alpha(byte[] canvas, int width, int x, int y) => canvas[(y * width + x) * 4 + 3];
    private static int Red(byte[] canvas, int width, int x, int y) => canvas[(y * width + x) * 4 + 2];

    [TestMethod]
    public void Compose_DisposeBackground_ClearsTheFrameBeforeTheNextOneWhenSteppingForward()
    {
        var frames = new List<AnimationFrame>
        {
            Solid(new Rectangle(0, 0, 2, 2), 0, 0, 255, 255, FrameDisposeMethod.Background),
            Solid(new Rectangle(2, 2, 1, 1), 255, 0, 0, 255),
        };
        var animator = new FrameAnimator(3, 3, frames);

        byte[] first = (byte[])animator.Compose(0).Clone();
        Assert.AreEqual(255, Alpha(first, 3, 0, 0));

        // Selecting frame 1 right after frame 0 (normal playback) must dispose frame 0 first.
        byte[] second = animator.Compose(1);
        Assert.AreEqual(0, Alpha(second, 3, 0, 0), "frame 0 was not disposed");
        Assert.AreEqual(255, Alpha(second, 3, 2, 2));
    }

    [TestMethod]
    public void Compose_DisposePrevious_RestoresWhatWasUnderTheFrame()
    {
        var frames = new List<AnimationFrame>
        {
            Solid(new Rectangle(0, 0, 3, 1), 0, 0, 200, 255),                                         // red bar
            Solid(new Rectangle(0, 0, 1, 1), 0, 255, 0, 255, FrameDisposeMethod.Previous),            // green dot, then undo
            Solid(new Rectangle(2, 0, 1, 1), 255, 0, 0, 255),
        };
        var animator = new FrameAnimator(3, 1, frames);
        animator.Compose(0);
        animator.Compose(1);
        byte[] third = animator.Compose(2);

        Assert.AreEqual(200, Red(third, 3, 0, 0), "the dot must be replaced by the red bar again");
        Assert.AreEqual(0, third[1], "no green left at (0,0)");
    }

    [TestMethod]
    public void Compose_SteppingForwardEqualsComposingFromTheStart_ForRandomAnimations()
    {
        var random = new Random(4242);
        for (int trial = 0; trial < 300; trial++)
        {
            int width = random.Next(1, 7), height = random.Next(1, 7), count = random.Next(2, 7);
            var frames = new List<AnimationFrame>();
            for (int f = 0; f < count; f++)
            {
                int w = random.Next(1, width + 1), h = random.Next(1, height + 1);
                var bounds = new Rectangle(random.Next(0, width - w + 1), random.Next(0, height - h + 1), w, h);
                var pixels = new byte[w * h * 4];
                random.NextBytes(pixels);
                for (int i = 3; i < pixels.Length; i += 4)
                    pixels[i] = random.Next(3) == 0 ? (byte)0 : (byte)(random.Next(2) == 0 ? 255 : random.Next(256));
                frames.Add(new AnimationFrame(bounds, pixels)
                {
                    DisposeMethod = (FrameDisposeMethod)random.Next(3),
                    BlendMethod = (FrameBlendMethod)random.Next(2),
                });
            }

            var stepping = new FrameAnimator(width, height, frames);
            for (int k = 0; k < count; k++)
            {
                byte[] stepped = (byte[])stepping.Compose(k).Clone();
                byte[] direct = (byte[])new FrameAnimator(width, height, frames).Compose(k).Clone();
                CollectionAssert.AreEqual(direct, stepped, $"trial {trial}, frame {k}");
            }
        }
    }

    [TestMethod]
    public void Compose_GoingBackwardsRestartsCleanly()
    {
        var frames = new List<AnimationFrame>
        {
            Solid(new Rectangle(0, 0, 2, 2), 0, 0, 255, 255, FrameDisposeMethod.Background),
            Solid(new Rectangle(1, 1, 1, 1), 255, 0, 0, 255),
            Solid(new Rectangle(0, 1, 1, 1), 0, 255, 0, 255),
        };
        var animator = new FrameAnimator(2, 2, frames);
        byte[] expectedFrame1 = (byte[])new FrameAnimator(2, 2, frames).Compose(1).Clone();

        animator.Compose(2);
        CollectionAssert.AreEqual(expectedFrame1, animator.Compose(1));
        byte[] frame0 = animator.Compose(0);
        Assert.AreEqual(255, Alpha(frame0, 2, 1, 1), "frame 0 re-composed from a clean canvas");
    }
}
