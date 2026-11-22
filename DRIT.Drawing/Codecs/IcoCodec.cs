using System;
using System.IO;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Codecs
{
    /// <summary>ICO codec — decode only; selects the largest frame.</summary>
    internal sealed class IcoCodec : IImageCodec
    {
        private static readonly byte[] PngSig = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public bool CanDecode(byte[] header)
            => header.Length >= 4
            && header[0] == 0 && header[1] == 0
            && header[2] == 1 && header[3] == 0;

        // ─── Decode ──────────────────────────────────────────────────────────────

        public Bitmap Decode(Stream stream)
        {
            byte[] file = CodecValidation.ReadAll(stream, "ICO");
            if (file.Length < 6)
                throw new InvalidDataException("ICO header is truncated.");
            if (!CanDecode(file))
                throw new InvalidDataException("ICO signature is invalid.");

            // ICONDIR
            int count = U16(file, 4);
            if (count == 0) throw new InvalidDataException("ICO contains no images.");
            CodecValidation.ValidateRange(6, count * 16L, file.Length, "ICO directory");

            // Pick best entry: largest area, then highest bit-depth
            int bestIdx = 0, bestArea = -1, bestBpp = -1;
            for (int i = 0; i < count; i++)
            {
                int e = 6 + i * 16;
                int fw  = file[e]     == 0 ? 256 : file[e];
                int fh  = file[e + 1] == 0 ? 256 : file[e + 1];
                int bpp = U16(file, e + 6);
                int area = fw * fh;
                if (area > bestArea || (area == bestArea && bpp > bestBpp))
                { bestArea = area; bestBpp = bpp; bestIdx = i; }
            }

            int dirEntry = 6 + bestIdx * 16;
            int entryBpp = U16(file, dirEntry + 6);
            long bytesInRes = U32(file, dirEntry + 8);
            long imgOff = U32(file, dirEntry + 12);

            // The directory's size field is advisory (it is wrong in some files); never trust it beyond the file.
            if (imgOff >= file.Length)
                throw new InvalidDataException("ICO image offset is outside the file.");
            long available = file.Length - imgOff;
            int imgLength = (int)((bytesInRes > 0 && bytesInRes <= available) ? bytesInRes : available);
            int off = (int)imgOff;

            // Embedded PNG: hand the decoder exactly this entry, not the rest of the file.
            if (imgLength >= 8 && IsPng(file, off))
            {
                using var ms = new MemoryStream(file, off, imgLength, writable: false);
                Bitmap png = new PngCodec().Decode(ms);
                png._rawFormat = ImageFormat.Icon;
                return png;
            }

            return DecodeDib(file, off, imgLength, entryBpp);
        }

        private static Bitmap DecodeDib(byte[] file, int off, int length, int entryBpp)
        {
            if (length < 40)
                throw new InvalidDataException("ICO bitmap header is truncated.");
            long dibSize = U32(file, off);
            if (dibSize == 12)
                throw new NotSupportedException("OS/2 ICO bitmap headers are not supported.");
            if (dibSize < 40 || dibSize > length)
                throw new InvalidDataException("ICO bitmap header size is invalid.");

            int dibW = I32(file, off + 4);
            int rawH = I32(file, off + 8);
            int dibBpp = U16(file, off + 14);
            long compression = U32(file, off + 16);
            long colorsUsed = U32(file, off + 32);
            if (dibBpp == 0) dibBpp = entryBpp;
            if (dibW <= 0 || rawH == 0 || rawH == int.MinValue)
                throw new InvalidDataException("ICO bitmap dimensions are invalid.");

            // The bitmap height is doubled in an icon: the second half is the AND (transparency) mask.
            bool topDown = rawH < 0;
            int actualH = Math.Abs(rawH) / 2;
            if (actualH == 0)
                throw new InvalidDataException("ICO bitmap dimensions are invalid.");
            CodecValidation.ValidateDimensions(dibW, actualH, "ICO");

            if (compression != 0)
                throw new NotSupportedException($"ICO bitmap compression {compression} is not supported.");
            if (dibBpp != 1 && dibBpp != 4 && dibBpp != 8 && dibBpp != 24 && dibBpp != 32)
                throw new NotSupportedException($"ICO {dibBpp}-bit bitmaps are not supported.");

            // Palette (BGRX entries) follows the header for indexed bitmaps.
            int paletteEntries = 0;
            if (dibBpp <= 8)
            {
                paletteEntries = colorsUsed != 0 ? (int)Math.Min(colorsUsed, 1L << 20) : 1 << dibBpp;
                if (paletteEntries > (1 << dibBpp))
                    throw new InvalidDataException("ICO palette has more entries than the bit depth allows.");
            }

            long paletteOff = (long)off + dibSize;
            long xorOff = paletteOff + paletteEntries * 4L;
            long xorStride = (((long)dibW * dibBpp + 31) / 32) * 4;
            long andStride = (((long)dibW + 31) / 32) * 4;
            long xorBytes = xorStride * actualH;
            CodecValidation.ValidateRange(paletteOff, paletteEntries * 4L, off + (long)length, "ICO palette");
            CodecValidation.ValidateRange(xorOff, xorBytes, off + (long)length, "ICO pixel data");

            // The AND mask is optional in practice (32-bit icons often rely on alpha alone).
            long andOff = xorOff + xorBytes;
            bool hasMask = andOff + andStride * actualH <= off + (long)length;

            var bmp = new Bitmap(dibW, actualH, PixelFormat.Format32bppArgb);
            bmp._rawFormat = ImageFormat.Icon;
            bool anyAlpha = false;

            for (int y = 0; y < actualH; y++)
            {
                // Rows are stored bottom-up unless the height is negative.
                int srcY = topDown ? y : actualH - 1 - y;
                long xorRow = xorOff + srcY * xorStride;
                long andRow = andOff + srcY * andStride;
                for (int x = 0; x < dibW; x++)
                {
                    byte r, g, bl, a = 255;
                    if (dibBpp == 32)
                    {
                        long px = xorRow + (long)x * 4;
                        bl = file[px]; g = file[px + 1]; r = file[px + 2]; a = file[px + 3];
                        if (a != 0) anyAlpha = true;
                    }
                    else if (dibBpp == 24)
                    {
                        long px = xorRow + (long)x * 3;
                        bl = file[px]; g = file[px + 1]; r = file[px + 2];
                    }
                    else
                    {
                        int perByte = 8 / dibBpp;
                        int value = file[xorRow + x / perByte];
                        int shift = 8 - dibBpp - (x % perByte) * dibBpp;
                        int index = (value >> shift) & ((1 << dibBpp) - 1);
                        if (index < paletteEntries)
                        {
                            long pal = paletteOff + index * 4L;
                            bl = file[pal]; g = file[pal + 1]; r = file[pal + 2];
                        }
                        else
                        {
                            r = g = bl = 0;
                        }
                    }

                    if (dibBpp != 32 && hasMask)
                        a = MaskBit(file, andRow, x) ? (byte)0 : (byte)255;

                    int dst = y * bmp._stride + x * 4;
                    bmp._pixels[dst]     = bl;
                    bmp._pixels[dst + 1] = g;
                    bmp._pixels[dst + 2] = r;
                    bmp._pixels[dst + 3] = a;
                }
            }

            // A 32-bit icon whose alpha channel is entirely zero is an X8R8G8B8 bitmap: the AND mask (or,
            // without one, full opacity) defines transparency, not the unused fourth byte.
            if (dibBpp == 32 && !anyAlpha)
            {
                for (int y = 0; y < actualH; y++)
                {
                    int srcY = topDown ? y : actualH - 1 - y;
                    long andRow = andOff + srcY * andStride;
                    for (int x = 0; x < dibW; x++)
                        bmp._pixels[y * bmp._stride + x * 4 + 3] = hasMask && MaskBit(file, andRow, x) ? (byte)0 : (byte)255;
                }
            }

            return bmp;
        }

        // AND mask: bit = 1 means transparent, bit = 0 means opaque.
        private static bool MaskBit(byte[] file, long rowOffset, int x)
            => ((file[rowOffset + x / 8] >> (7 - (x % 8))) & 1) != 0;

        private static bool IsPng(byte[] file, int offset)
        {
            for (int i = 0; i < PngSig.Length; i++)
                if (file[offset + i] != PngSig[i]) return false;
            return true;
        }

        private static int U16(byte[] d, int o) => d[o] | (d[o + 1] << 8);

        private static long U32(byte[] d, int o)
            => (uint)(d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24));

        private static int I32(byte[] d, int o)
            => d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24);

        public void Encode(Bitmap bmp, Stream stream, EncoderParameters? parameters)
            => throw new NotSupportedException("ICO encoding is not supported.");
    }
}
