using System;
using System.IO;

namespace DRIT.Drawing.Codecs.Png
{
    internal static class PngHelpers
    {
        public static int GetChannels(int colorType)
        {
            switch (colorType)
            {
                case 0: return 1;
                case 2: return 3;
                case 3: return 1;
                case 4: return 2;
                case 6: return 4;
                default: return 3;
            }
        }

        public static byte[] ReconstructFilters(byte[] raw, int width, int height, int stride, int bpp)
        {
            long required = (long)height * (stride + 1);
            if (raw.Length < required)
                throw new InvalidDataException("PNG scanline data is shorter than the image requires.");
            var result = new byte[checked(height * stride)];
            var prev = new byte[stride];

            for (int y = 0; y < height; y++)
            {
                int rowOff = y * (stride + 1);
                int filterType = raw[rowOff];
                int dstOff = y * stride;

                switch (filterType)
                {
                    case 0:
                        Buffer.BlockCopy(raw, rowOff + 1, result, dstOff, stride);
                        break;
                    case 1:
                        for (int i = 0; i < stride; i++)
                        {
                            byte a = i >= bpp ? result[dstOff + i - bpp] : (byte)0;
                            result[dstOff + i] = (byte)(raw[rowOff + 1 + i] + a);
                        }
                        break;
                    case 2:
                        for (int i = 0; i < stride; i++)
                            result[dstOff + i] = (byte)(raw[rowOff + 1 + i] + prev[i]);
                        break;
                    case 3:
                        for (int i = 0; i < stride; i++)
                        {
                            byte a = i >= bpp ? result[dstOff + i - bpp] : (byte)0;
                            byte b = prev[i];
                            result[dstOff + i] = (byte)(raw[rowOff + 1 + i] + (a + b) / 2);
                        }
                        break;
                    case 4:
                        for (int i = 0; i < stride; i++)
                        {
                            byte a = i >= bpp ? result[dstOff + i - bpp] : (byte)0;
                            byte b = prev[i];
                            byte c = i >= bpp ? prev[i - bpp] : (byte)0;
                            result[dstOff + i] = (byte)(raw[rowOff + 1 + i] + PaethPredictor(a, b, c));
                        }
                        break;
                    default:
                        throw new InvalidDataException($"PNG scanline uses invalid filter type {filterType}.");
                }

                Buffer.BlockCopy(result, dstOff, prev, 0, stride);
            }

            return result;
        }

        public static byte[] ReconstructAdam7Rgba(
            byte[] raw,
            int width,
            int height,
            int bitDepth,
            int colorType,
            byte[]? palette,
            byte[]? trns)
        {
            int channels = GetChannels(colorType);
            int[] startX = { 0, 4, 0, 2, 0, 1, 0 };
            int[] startY = { 0, 0, 4, 0, 2, 0, 1 };
            int[] stepX = { 8, 8, 4, 4, 2, 2, 1 };
            int[] stepY = { 8, 8, 8, 4, 4, 2, 2 };
            byte[] result = new byte[checked(width * height * 4)];
            int rawOffset = 0;

            for (int pass = 0; pass < 7; pass++)
            {
                int passWidth = width <= startX[pass] ? 0 : (width - startX[pass] + stepX[pass] - 1) / stepX[pass];
                int passHeight = height <= startY[pass] ? 0 : (height - startY[pass] + stepY[pass] - 1) / stepY[pass];
                if (passWidth == 0 || passHeight == 0)
                    continue;

                int passStride = checked((int)(((long)passWidth * bitDepth * channels + 7) / 8));
                int passRawLength = checked(passHeight * (passStride + 1));
                if (rawOffset + passRawLength > raw.Length)
                    throw new InvalidDataException("Adam7 PNG data is truncated.");

                var passRaw = new byte[passRawLength];
                Buffer.BlockCopy(raw, rawOffset, passRaw, 0, passRawLength);
                rawOffset += passRawLength;

                byte[] passPixels = ReconstructFilters(
                    passRaw,
                    passWidth,
                    passHeight,
                    passStride,
                    Math.Max(1, (bitDepth * channels) / 8));

                for (int y = 0; y < passHeight; y++)
                for (int x = 0; x < passWidth; x++)
                {
                    GetPixelRgba(
                        passPixels,
                        x,
                        y,
                        passStride,
                        bitDepth,
                        colorType,
                        palette,
                        trns,
                        out byte r,
                        out byte g,
                        out byte b,
                        out byte a);

                    int dstX = startX[pass] + x * stepX[pass];
                    int dstY = startY[pass] + y * stepY[pass];
                    int dst = (dstY * width + dstX) * 4;
                    result[dst] = r;
                    result[dst + 1] = g;
                    result[dst + 2] = b;
                    result[dst + 3] = a;
                }
            }

            return result;
        }

        /// <summary>Exact size of the filtered data of an Adam7-interlaced image (all seven passes).</summary>
        public static long Adam7RawLength(int width, int height, int bitDepth, int channels)
        {
            int[] startX = { 0, 4, 0, 2, 0, 1, 0 };
            int[] startY = { 0, 0, 4, 0, 2, 0, 1 };
            int[] stepX = { 8, 8, 4, 4, 2, 2, 1 };
            int[] stepY = { 8, 8, 8, 4, 4, 2, 2 };
            long total = 0;
            for (int pass = 0; pass < 7; pass++)
            {
                long passWidth = width <= startX[pass] ? 0 : (width - startX[pass] + stepX[pass] - 1) / stepX[pass];
                long passHeight = height <= startY[pass] ? 0 : (height - startY[pass] + stepY[pass] - 1) / stepY[pass];
                if (passWidth == 0 || passHeight == 0)
                    continue;
                long passStride = (passWidth * bitDepth * channels + 7) / 8;
                total += passHeight * (passStride + 1);
            }
            return total;
        }

        public static byte PaethPredictor(byte a, byte b, byte c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a);
            int pb = Math.Abs(p - b);
            int pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc) return a;
            if (pb <= pc) return b;
            return c;
        }

        public static void GetPixelRgba(byte[] pixels, int x, int y,
            int stride, int bitDepth, int colorType,
            byte[]? palette, byte[]? trns,
            out byte r, out byte g, out byte b, out byte a)
        {
            int rowOff = y * stride;
            r = g = b = 0; a = 255;

            switch (colorType)
            {
                case 0:
                {
                    // The tRNS colour key is compared with the sample at its native depth (before it is
                    // scaled to 8 bits), using only the bits the depth defines.
                    int sample;
                    byte grey;
                    if (bitDepth == 8)
                    {
                        sample = pixels[rowOff + x];
                        grey = (byte)sample;
                    }
                    else if (bitDepth == 16)
                    {
                        sample = (pixels[rowOff + x * 2] << 8) | pixels[rowOff + x * 2 + 1];
                        grey = pixels[rowOff + x * 2];
                    }
                    else
                    {
                        int samplesPerByte = 8 / bitDepth;
                        int byteIdx = x / samplesPerByte;
                        int bitShift = 8 - bitDepth - (x % samplesPerByte) * bitDepth;
                        int mask = (1 << bitDepth) - 1;
                        sample = (pixels[rowOff + byteIdx] >> bitShift) & mask;
                        grey = (byte)(sample * 255 / mask);
                    }
                    r = g = b = grey;
                    if (trns != null && trns.Length >= 2)
                    {
                        int key = ((trns[0] << 8) | trns[1]) & ((1 << bitDepth) - 1);
                        if (sample == key) a = 0;
                    }
                    break;
                }
                case 2:
                {
                    int sr, sg, sb;
                    if (bitDepth == 8)
                    {
                        int off = rowOff + x * 3;
                        sr = pixels[off]; sg = pixels[off + 1]; sb = pixels[off + 2];
                        r = (byte)sr; g = (byte)sg; b = (byte)sb;
                    }
                    else
                    {
                        int off = rowOff + x * 6;
                        sr = (pixels[off] << 8) | pixels[off + 1];
                        sg = (pixels[off + 2] << 8) | pixels[off + 3];
                        sb = (pixels[off + 4] << 8) | pixels[off + 5];
                        r = pixels[off]; g = pixels[off + 2]; b = pixels[off + 4];
                    }
                    if (trns != null && trns.Length >= 6)
                    {
                        int mask = bitDepth == 16 ? 0xFFFF : 0xFF;
                        if (sr == (((trns[0] << 8) | trns[1]) & mask)
                            && sg == (((trns[2] << 8) | trns[3]) & mask)
                            && sb == (((trns[4] << 8) | trns[5]) & mask))
                            a = 0;
                    }
                    break;
                }
                case 3:
                {
                    int idx;
                    if (bitDepth == 8)
                    {
                        idx = pixels[rowOff + x];
                    }
                    else
                    {
                        int samplesPerByte = 8 / bitDepth;
                        int byteIdx = x / samplesPerByte;
                        int bitShift = 8 - bitDepth - (x % samplesPerByte) * bitDepth;
                        int mask = (1 << bitDepth) - 1;
                        idx = (pixels[rowOff + byteIdx] >> bitShift) & mask;
                    }
                    if (palette != null && idx * 3 + 2 < palette.Length)
                    {
                        r = palette[idx * 3];
                        g = palette[idx * 3 + 1];
                        b = palette[idx * 3 + 2];
                    }
                    a = (trns != null && idx < trns.Length) ? trns[idx] : (byte)255;
                    break;
                }
                case 4:
                {
                    if (bitDepth == 8)
                    {
                        int off = rowOff + x * 2;
                        r = g = b = pixels[off]; a = pixels[off + 1];
                    }
                    else
                    {
                        int off = rowOff + x * 4;
                        r = g = b = pixels[off]; a = pixels[off + 2];
                    }
                    break;
                }
                case 6:
                {
                    if (bitDepth == 8)
                    {
                        int off = rowOff + x * 4;
                        r = pixels[off]; g = pixels[off + 1]; b = pixels[off + 2]; a = pixels[off + 3];
                    }
                    else
                    {
                        int off = rowOff + x * 8;
                        r = pixels[off]; g = pixels[off + 2]; b = pixels[off + 4]; a = pixels[off + 6];
                    }
                    break;
                }
            }
        }

        public static void ReadFull(Stream s, byte[] buf, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = s.Read(buf, offset + total, count - total);
                if (n <= 0) throw new InvalidDataException("Unexpected end of PNG stream.");
                total += n;
            }
        }

        /// <summary>
        /// Reads a chunk payload without trusting the declared length: on a seekable stream the length is
        /// compared with the bytes that remain before anything is allocated, otherwise the payload is read
        /// in bounded blocks so that memory grows only with data that actually exists.
        /// </summary>
        public static byte[] ReadChunkPayload(Stream s, int length)
        {
            if (length == 0)
                return Array.Empty<byte>();

            if (s.CanSeek)
            {
                // The payload is followed by a four-byte CRC.
                if (length + 4L > s.Length - s.Position)
                    throw new InvalidDataException("PNG chunk extends beyond the end of the stream.");
                var data = new byte[length];
                ReadFull(s, data, 0, length);
                return data;
            }

            const int Block = 64 * 1024;
            using var buffer = new MemoryStream();
            var block = new byte[Math.Min(length, Block)];
            int remaining = length;
            while (remaining > 0)
            {
                int n = s.Read(block, 0, Math.Min(block.Length, remaining));
                if (n <= 0) throw new InvalidDataException("Unexpected end of PNG stream.");
                buffer.Write(block, 0, n);
                remaining -= n;
            }
            return buffer.ToArray();
        }

        public static void WriteChunk(Stream s, string type, byte[] data)
        {
            var typeBuf = System.Text.Encoding.ASCII.GetBytes(type);

            var lenBuf = new byte[4];
            BinaryBE.WriteUInt32(lenBuf, 0, (uint)data.Length);
            s.Write(lenBuf, 0, 4);
            s.Write(typeBuf, 0, 4);
            if (data.Length > 0) s.Write(data, 0, data.Length);

            var combined = new byte[4 + data.Length];
            Buffer.BlockCopy(typeBuf, 0, combined, 0, 4);
            if (data.Length > 0) Buffer.BlockCopy(data, 0, combined, 4, data.Length);
            uint crc = Crc32.Compute(combined, 0, combined.Length);

            s.WriteByte((byte)(crc >> 24));
            s.WriteByte((byte)(crc >> 16));
            s.WriteByte((byte)(crc >> 8));
            s.WriteByte((byte)crc);
        }
    }
}
