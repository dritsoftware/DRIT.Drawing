using System;
using System.Collections.Generic;
using System.IO;
using DRIT.Drawing.Codecs;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Tests.Codecs;

/// <summary>Turns on strict conformance checking (the corpus-probe mode) for the duration of a test.</summary>
internal sealed class StrictConformanceScope : IDisposable
{
    public StrictConformanceScope() => AppContext.SetSwitch(DecodePolicy.StrictSwitchName, true);

    public void Dispose() => AppContext.SetSwitch(DecodePolicy.StrictSwitchName, false);
}

/// <summary>Byte-level builders and helpers shared by the codec robustness tests.</summary>
internal static class CodecTestBuilders
{
    // ── Bitmaps ──────────────────────────────────────────────────────────────

    public static Bitmap Pattern(int width, int height)
    {
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            bitmap.SetPixel(x, y, Color.FromArgb(255, (x * 37 + y * 11) & 255, (x * 5 + y * 53) & 255, (x * 91 + y * 7) & 255));
        return bitmap;
    }

    public static byte[] Encode(Bitmap bitmap, ImageFormat format)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, format);
        return stream.ToArray();
    }

    public static Bitmap Decode(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        return new Bitmap(stream);
    }

    public static void AssertSamePixels(Bitmap expected, Bitmap actual, string context = "")
    {
        Assert.AreEqual(expected.Width, actual.Width, context);
        Assert.AreEqual(expected.Height, actual.Height, context);
        for (int y = 0; y < expected.Height; y++)
        for (int x = 0; x < expected.Width; x++)
            Assert.AreEqual(expected.GetPixel(x, y).ToArgb(), actual.GetPixel(x, y).ToArgb(), $"{context} pixel ({x},{y})");
    }

    // ── Byte helpers ─────────────────────────────────────────────────────────

    public static byte[] Insert(byte[] bytes, int offset, byte[] segment)
    {
        var result = new byte[bytes.Length + segment.Length];
        Buffer.BlockCopy(bytes, 0, result, 0, offset);
        Buffer.BlockCopy(segment, 0, result, offset, segment.Length);
        Buffer.BlockCopy(bytes, offset, result, offset + segment.Length, bytes.Length - offset);
        return result;
    }

    public static byte[] Concat(params byte[][] parts)
    {
        using var stream = new MemoryStream();
        foreach (byte[] part in parts)
            stream.Write(part, 0, part.Length);
        return stream.ToArray();
    }

    public static byte[] FixtureBytes(string relativePath)
        => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TestData", relativePath));

    // ── JPEG ─────────────────────────────────────────────────────────────────

    /// <summary>A JPEG marker segment (0xFF, marker, big-endian length, payload).</summary>
    public static byte[] JpegSegment(byte marker, byte[] payload)
    {
        int length = payload.Length + 2;
        var segment = new byte[payload.Length + 4];
        segment[0] = 0xFF;
        segment[1] = marker;
        segment[2] = (byte)(length >> 8);
        segment[3] = (byte)length;
        Buffer.BlockCopy(payload, 0, segment, 4, payload.Length);
        return segment;
    }

    /// <summary>Offset of the 0xFF byte of the first occurrence of a marker (0xFF, marker) after the SOI.</summary>
    public static int FindMarker(byte[] jpeg, byte marker)
    {
        for (int i = 2; i + 1 < jpeg.Length; i++)
        {
            if (jpeg[i] == 0xFF && jpeg[i + 1] == marker)
                return i;
        }
        throw new AssertFailedException($"Marker 0x{marker:X2} not found.");
    }

    public static byte[] Ascii(string text)
    {
        var bytes = new byte[text.Length];
        for (int i = 0; i < text.Length; i++)
            bytes[i] = (byte)text[i];
        return bytes;
    }

    /// <summary>Mean absolute per-channel difference between a bitmap and raw packed RGB bytes.</summary>
    public static double MeanAbsDiffRgb(Bitmap bitmap, byte[] rgb)
    {
        Assert.AreEqual(bitmap.Width * bitmap.Height * 3, rgb.Length, "reference size");
        long sum = 0;
        int i = 0;
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++, i += 3)
        {
            Color c = bitmap.GetPixel(x, y);
            sum += Math.Abs(c.R - rgb[i]) + Math.Abs(c.G - rgb[i + 1]) + Math.Abs(c.B - rgb[i + 2]);
        }
        return sum / (double)rgb.Length;
    }

    /// <summary>
    /// Mean absolute difference between the 8x8 block means of a bitmap and a reference grid file
    /// (uint16 blocksX, uint16 blocksY, then RGB block means).
    /// </summary>
    public static double MeanAbsDiffBlockGrid(Bitmap bitmap, byte[] grid)
    {
        int blocksX = grid[0] | (grid[1] << 8);
        int blocksY = grid[2] | (grid[3] << 8);
        Assert.AreEqual(blocksX * blocksY * 3 + 4, grid.Length, "grid size");
        Assert.IsTrue(bitmap.Width >= blocksX * 8 && bitmap.Height >= blocksY * 8, "bitmap smaller than grid");

        long sum = 0;
        int index = 4;
        for (int by = 0; by < blocksY; by++)
        for (int bx = 0; bx < blocksX; bx++, index += 3)
        {
            int r = 0, g = 0, b = 0;
            for (int y = by * 8; y < by * 8 + 8; y++)
            for (int x = bx * 8; x < bx * 8 + 8; x++)
            {
                Color c = bitmap.GetPixel(x, y);
                r += c.R; g += c.G; b += c.B;
            }
            sum += Math.Abs(r / 64 - grid[index]) + Math.Abs(g / 64 - grid[index + 1]) + Math.Abs(b / 64 - grid[index + 2]);
        }
        return sum / (double)(blocksX * blocksY * 3);
    }

    // ── PNG ──────────────────────────────────────────────────────────────────

    public static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    public static byte[] PngChunk(string type, byte[] data)
    {
        byte[] typeBytes = Ascii(type);
        var chunk = new byte[12 + data.Length];
        WriteUInt32BE(chunk, 0, (uint)data.Length);
        Buffer.BlockCopy(typeBytes, 0, chunk, 4, 4);
        Buffer.BlockCopy(data, 0, chunk, 8, data.Length);
        uint crc = Crc32.Compute(chunk, 4, 4 + data.Length);
        WriteUInt32BE(chunk, 8 + data.Length, crc);
        return chunk;
    }

    public static byte[] PngIhdr(int width, int height, byte bitDepth, byte colorType)
    {
        var ihdr = new byte[13];
        WriteUInt32BE(ihdr, 0, (uint)width);
        WriteUInt32BE(ihdr, 4, (uint)height);
        ihdr[8] = bitDepth;
        ihdr[9] = colorType;
        return PngChunk("IHDR", ihdr);
    }

    /// <summary>A PNG built from already-filtered scanline bytes (including each row's filter byte).</summary>
    public static byte[] Png(int width, int height, byte bitDepth, byte colorType, byte[] filteredRows, params byte[][] chunksBeforeIdat)
        => Concat(
            PngSignature,
            PngIhdr(width, height, bitDepth, colorType),
            Concat(chunksBeforeIdat),
            PngChunk("IDAT", ZlibHelper.Compress(filteredRows)),
            PngChunk("IEND", Array.Empty<byte>()));

    public static void WriteUInt32BE(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    // ── GIF ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// A single-frame GIF with a 256-entry greyscale palette whose LZW stream never emits a clear code once the
    /// dictionary is full (a "deferred clear", which the GIF specification allows).
    /// </summary>
    public static byte[] GifWithDeferredClear(int width, int height, byte[] indices, bool interlaced = false)
    {
        const int minCodeSize = 8;
        int clear = 1 << minCodeSize, eoi = clear + 1;
        var lzw = new List<byte>();
        int bitBuffer = 0, bits = 0;

        void Emit(int code, int size)
        {
            bitBuffer |= code << bits;
            bits += size;
            while (bits >= 8)
            {
                lzw.Add((byte)(bitBuffer & 0xFF));
                bitBuffer >>= 8;
                bits -= 8;
            }
        }

        int codeSize = minCodeSize + 1, next = eoi + 1;
        var table = new Dictionary<(int prefix, byte symbol), int>();
        Emit(clear, codeSize);
        int current = indices[0];
        for (int i = 1; i < indices.Length; i++)
        {
            byte symbol = indices[i];
            if (table.TryGetValue((current, symbol), out int existing))
            {
                current = existing;
                continue;
            }

            Emit(current, codeSize);
            if (next < 4096)
            {
                table[(current, symbol)] = next++;
                if (next > (1 << codeSize) && codeSize < 12)
                    codeSize++;
            }
            current = symbol;
        }
        Emit(current, codeSize);
        Emit(eoi, codeSize);
        if (bits > 0)
            lzw.Add((byte)(bitBuffer & 0xFF));

        using var stream = new MemoryStream();
        stream.Write(Ascii("GIF89a"), 0, 6);
        stream.Write(new byte[] { (byte)width, (byte)(width >> 8), (byte)height, (byte)(height >> 8), 0xF7, 0, 0 }, 0, 7);
        for (int i = 0; i < 256; i++)
            stream.Write(new[] { (byte)i, (byte)i, (byte)i }, 0, 3);
        stream.Write(new byte[] { 0x2C, 0, 0, 0, 0, (byte)width, (byte)(width >> 8), (byte)height, (byte)(height >> 8), (byte)(interlaced ? 0x40 : 0), minCodeSize }, 0, 11);
        for (int offset = 0; offset < lzw.Count; offset += 255)
        {
            int size = Math.Min(255, lzw.Count - offset);
            stream.WriteByte((byte)size);
            stream.Write(lzw.GetRange(offset, size).ToArray(), 0, size);
        }
        stream.WriteByte(0);
        stream.WriteByte(0x3B);
        return stream.ToArray();
    }

    // ── ICO ──────────────────────────────────────────────────────────────────

    public static byte[] IcoEntry(int width, int height, int bitCount, int bytesInRes, int offset)
        => new[]
        {
            (byte)width, (byte)height, (byte)0, (byte)0, (byte)1, (byte)0, (byte)bitCount, (byte)(bitCount >> 8),
            (byte)bytesInRes, (byte)(bytesInRes >> 8), (byte)(bytesInRes >> 16), (byte)(bytesInRes >> 24),
            (byte)offset, (byte)(offset >> 8), (byte)(offset >> 16), (byte)(offset >> 24),
        };

    public static byte[] IcoHeader(int count) => new byte[] { 0, 0, 1, 0, (byte)count, (byte)(count >> 8) };

    /// <summary>
    /// A BITMAPINFOHEADER-based icon image (height doubled) with an optional palette, XOR pixel rows
    /// (bottom-up, already padded) and AND mask rows.
    /// </summary>
    public static byte[] IcoDib(int width, int height, int bitCount, byte[] paletteBgrx, byte[] xorRows, byte[] andRows)
    {
        var header = new byte[40];
        header[0] = 40;
        header[4] = (byte)width; header[5] = (byte)(width >> 8);
        int doubled = height * 2;
        header[8] = (byte)doubled; header[9] = (byte)(doubled >> 8);
        header[12] = 1;
        header[14] = (byte)bitCount;
        return Concat(header, paletteBgrx, xorRows, andRows);
    }
}
