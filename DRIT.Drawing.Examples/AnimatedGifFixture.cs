using System;
using System.Collections.Generic;
using System.IO;

namespace DRIT.Drawing.Examples;

/// <summary>
/// Builds a small animated GIF (a ball bouncing across a field) so that the animation examples
/// have input whose frames differ visibly, carry delays and loop. The library reads and preserves
/// animations; it does not construct them, so the fixture is encoded here.
/// </summary>
internal static class AnimatedGifFixture
{
    internal const int Width = 160;
    internal const int Height = 100;
    internal const int FrameCount = 4;
    internal const int DelayHundredths = 60;

    private static readonly byte[][] Palette =
    {
        new byte[] { 0, 0, 0 },          // 0 unused
        new byte[] { 135, 206, 235 },    // 1 sky
        new byte[] { 56, 128, 83 },      // 2 grass
        new byte[] { 255, 215, 0 },      // 3 ball
        new byte[] { 0, 0, 139 },        // 4 ball outline / counter
        new byte[] { 255, 255, 255 },    // 5 counter background
        new byte[] { 90, 90, 90 },       // 6 shadow
    };

    internal static byte[] Create()
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        w.Write(new[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a' });
        w.Write((ushort)Width);
        w.Write((ushort)Height);
        w.Write((byte)0xF3);             // global colour table, 16 entries
        w.Write((byte)0);                // background colour index
        w.Write((byte)0);                // pixel aspect ratio
        for (int i = 0; i < 16; i++)
            w.Write(i < Palette.Length ? Palette[i] : new byte[] { 0, 0, 0 });

        // Loop forever.
        w.Write(new byte[] { 0x21, 0xFF, 0x0B });
        w.Write(System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));
        w.Write(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 });

        for (int frame = 0; frame < FrameCount; frame++)
        {
            // Graphic control extension: replace the previous frame, show for DelayHundredths.
            w.Write(new byte[] { 0x21, 0xF9, 0x04, 0x04 });
            w.Write((ushort)DelayHundredths);
            w.Write(new byte[] { 0x00, 0x00 });

            w.Write((byte)0x2C);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write((ushort)Width);
            w.Write((ushort)Height);
            w.Write((byte)0);            // no local colour table, not interlaced

            const int minCodeSize = 4;
            w.Write((byte)minCodeSize);
            byte[] data = LzwEncode(DrawFrame(frame), minCodeSize);
            for (int offset = 0; offset < data.Length; offset += 255)
            {
                int length = Math.Min(255, data.Length - offset);
                w.Write((byte)length);
                w.Write(data, offset, length);
            }
            w.Write((byte)0);
        }

        w.Write((byte)0x3B);
        w.Flush();
        return stream.ToArray();
    }

    /// <summary>Palette indices of one frame: sky, grass, a shadow, the ball and a frame counter.</summary>
    private static byte[] DrawFrame(int frame)
    {
        var pixels = new byte[Width * Height];
        int horizon = 72;
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                pixels[y * Width + x] = (byte)(y < horizon ? 1 : 2);

        int[] ballY = { 52, 24, 14, 38 };
        int cx = 24 + frame * 37;
        int cy = ballY[frame % ballY.Length];
        const int radius = 11;

        FillEllipse(pixels, cx, horizon + 8, 12 - (horizon - cy) / 12, 3, 6);
        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                int d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                if (d2 <= radius * radius)
                    pixels[y * Width + x] = (byte)(d2 >= (radius - 1) * (radius - 1) ? 4 : 3);
            }
        }

        // Frame counter: one dark square per frame shown so far.
        for (int i = 0; i <= frame; i++)
            for (int y = 6; y < 14; y++)
                for (int x = 6 + i * 12; x < 14 + i * 12; x++)
                    pixels[y * Width + x] = 4;
        return pixels;
    }

    private static void FillEllipse(byte[] pixels, int cx, int cy, int rx, int ry, byte index)
    {
        rx = Math.Max(rx, 2);
        for (int y = cy - ry; y <= cy + ry; y++)
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                double nx = (x - cx) / (double)rx, ny = (y - cy) / (double)ry;
                if (nx * nx + ny * ny <= 1.0)
                    pixels[y * Width + x] = index;
            }
    }

    /// <summary>The variable-width LZW coding used by GIF image data.</summary>
    private static byte[] LzwEncode(byte[] indices, int minCodeSize)
    {
        int clear = 1 << minCodeSize, end = clear + 1;
        var output = new List<byte>();
        int bitBuffer = 0, bitCount = 0;

        void Write(int code, int size)
        {
            bitBuffer |= code << bitCount;
            bitCount += size;
            while (bitCount >= 8)
            {
                output.Add((byte)(bitBuffer & 0xFF));
                bitBuffer >>= 8;
                bitCount -= 8;
            }
        }

        var table = new Dictionary<int, int>();
        int next = end + 1;
        int codeSize = minCodeSize + 1;
        Write(clear, codeSize);
        int prefix = indices[0];
        for (int i = 1; i < indices.Length; i++)
        {
            int k = indices[i];
            int key = (prefix << 8) | k;
            if (table.TryGetValue(key, out int existing))
            {
                prefix = existing;
                continue;
            }

            Write(prefix, codeSize);
            if (next >= (1 << codeSize) && codeSize < 12)
                codeSize++;
            if (next < 4096)
            {
                table[key] = next++;
            }
            else
            {
                Write(clear, codeSize);
                table.Clear();
                next = end + 1;
                codeSize = minCodeSize + 1;
            }
            prefix = k;
        }

        Write(prefix, codeSize);
        Write(end, codeSize);
        if (bitCount > 0)
            output.Add((byte)(bitBuffer & 0xFF));
        return output.ToArray();
    }
}
