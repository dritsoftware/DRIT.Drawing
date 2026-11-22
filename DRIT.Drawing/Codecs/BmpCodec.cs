using System;
using System.IO;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Codecs
{
    /// <summary>BMP / DIB codec (native, no third-party library).</summary>
    internal sealed class BmpCodec : IImageCodec
    {
        public bool CanDecode(byte[] header)
            => header.Length >= 2 && header[0] == (byte)'B' && header[1] == (byte)'M';

        public Bitmap Decode(Stream stream)
        {
            byte[] file = ReadAll(stream);
            if (file.Length < 14)
                throw new InvalidDataException("BMP file header is truncated.");
            if (file[0] != (byte)'B' || file[1] != (byte)'M')
                throw new InvalidDataException("BMP signature is invalid.");

            uint declaredFileSize = ReadUInt32(file, 2, "file size");
            uint pixelOffsetValue = ReadUInt32(file, 10, "pixel offset");
            int pixelOffset = CheckedInt(pixelOffsetValue, "pixel offset");
            uint headerSize = ReadUInt32(file, 14, "DIB header size");
            if (headerSize == 12)
                throw new NotSupportedException("OS/2 BMP core headers are not supported.");
            if (headerSize < 40)
                throw new InvalidDataException($"Unsupported BMP DIB header size {headerSize}.");

            long headerEnd = 14L + headerSize;
            if (headerEnd > file.Length || headerEnd > pixelOffset)
                throw new InvalidDataException("BMP DIB header exceeds the file or pixel offset.");

            int width = ReadInt32(file, 18, "width");
            int signedHeight = ReadInt32(file, 22, "height");
            if (width <= 0 || signedHeight == 0 || signedHeight == int.MinValue)
                throw new InvalidDataException("BMP dimensions are invalid.");

            bool topDown = signedHeight < 0;
            int height = topDown ? -signedHeight : signedHeight;
            CodecValidation.ValidateDimensions(width, height, "BMP");

            ushort planes = ReadUInt16(file, 26, "planes");
            ushort bpp = ReadUInt16(file, 28, "bits per pixel");
            uint compression = ReadUInt32(file, 30, "compression");
            int xPPM = ReadInt32(file, 38, "horizontal resolution");
            int yPPM = ReadInt32(file, 42, "vertical resolution");

            if (planes != 1)
                throw new InvalidDataException("BMP must contain exactly one color plane.");
            if (compression != 0)
            {
                if (topDown)
                    throw new InvalidDataException("Compressed top-down BMPs are invalid.");
                throw new NotSupportedException($"BMP compression {compression} is not supported.");
            }
            if (bpp != 24 && bpp != 32)
                throw new NotSupportedException($"BMP {bpp}-bit indexed or packed pixels are not supported.");
            if (pixelOffset < headerEnd || pixelOffset > file.Length)
                throw new InvalidDataException("BMP pixel offset is outside the file.");

            long bitsPerRow = checked((long)width * bpp);
            long sourceStride = checked(((bitsPerRow + 31) / 32) * 4);
            long sourceBytes = checked(sourceStride * height);
            long pixelEnd = checked((long)pixelOffset + sourceBytes);
            CodecValidation.ValidateRange(pixelOffset, sourceBytes, file.Length, "BMP pixel data");
            CodecValidation.CheckedBufferLength(checked((long)width * height * 4), "BMP decoded pixels");
            // The size field is redundant (the pixel range was validated against the real length above)
            // and several writers get it wrong, so it is only enforced when strict conformance is requested.
            if (DecodePolicy.Strict && declaredFileSize != 0 && (declaredFileSize < pixelEnd || declaredFileSize > file.Length))
                throw new InvalidDataException("BMP declared file size is inconsistent with the input.");

            float dpiX = xPPM > 0 ? xPPM * 0.0254f : 96f;
            float dpiY = yPPM > 0 ? yPPM * 0.0254f : 96f;
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb)
            {
                _dpiX = dpiX,
                _dpiY = dpiY,
                _rawFormat = ImageFormat.Bmp
            };

            int sourceBytesPerPixel = bpp / 8;
            int sourceStrideInt = CheckedInt(sourceStride, "row stride");
            bool anyAlpha = false;
            for (int sourceY = 0; sourceY < height; sourceY++)
            {
                int destinationY = topDown ? sourceY : height - 1 - sourceY;
                int sourceBase = checked(pixelOffset + sourceY * sourceStrideInt);
                int destinationBase = checked(destinationY * bmp._stride);
                for (int x = 0; x < width; x++)
                {
                    int source = checked(sourceBase + x * sourceBytesPerPixel);
                    int destination = checked(destinationBase + x * 4);
                    bmp._pixels[destination] = file[source];
                    bmp._pixels[destination + 1] = file[source + 1];
                    bmp._pixels[destination + 2] = file[source + 2];
                    bmp._pixels[destination + 3] = sourceBytesPerPixel == 4 ? file[source + 3] : (byte)255;
                    if (sourceBytesPerPixel == 4 && file[source + 3] != 0)
                        anyAlpha = true;
                }
            }

            // Most 32-bit BI_RGB writers leave the fourth byte at zero (X8R8G8B8) and GDI+ ignores it; reading it as
            // alpha would make such images invisible. A bitmap whose fourth byte is zero everywhere is opaque.
            if (sourceBytesPerPixel == 4 && !anyAlpha)
            {
                for (int i = 3; i < bmp._pixels.Length; i += 4)
                    bmp._pixels[i] = 255;
            }

            return bmp;
        }

        private static byte[] ReadAll(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return CodecValidation.ReadAll(stream, "BMP");
        }

        private static ushort ReadUInt16(byte[] file, int offset, string field)
        {
            EnsureRange(file, offset, 2, field);
            return (ushort)(file[offset] | (file[offset + 1] << 8));
        }

        private static uint ReadUInt32(byte[] file, int offset, string field)
        {
            EnsureRange(file, offset, 4, field);
            return (uint)(file[offset]
                | (file[offset + 1] << 8)
                | (file[offset + 2] << 16)
                | (file[offset + 3] << 24));
        }

        private static int ReadInt32(byte[] file, int offset, string field)
            => unchecked((int)ReadUInt32(file, offset, field));

        private static int CheckedInt(long value, string field)
        {
            if (value < 0 || value > int.MaxValue)
                throw new InvalidDataException($"BMP {field} is outside the supported range.");
            return (int)value;
        }

        private static void EnsureRange(byte[] file, int offset, int length, string field)
        {
            if (offset < 0 || length < 0 || offset > file.Length - length)
                throw new InvalidDataException($"BMP {field} is truncated.");
        }

        public void Encode(Bitmap bmp, Stream stream, EncoderParameters? parameters)
        {
            // 32-bit output for formats with a fourth byte, 24-bit for everything else.
            bool wide = PixelExport.HasAlpha(bmp) || bmp._format == PixelFormat.Format32bppRgb;
            int bpp = wide ? 32 : 24;
            long rowBytesLong = (((long)bmp._width * bpp + 31) / 32) * 4;
            long dataSizeLong = rowBytesLong * bmp._height;
            if (54 + dataSizeLong > uint.MaxValue)
                throw new ArgumentException("The bitmap is too large for the BMP format.", nameof(bmp));
            int rowBytes = (int)rowBytesLong;
            uint dataSize = (uint)dataSizeLong;

            byte[] bgra = PixelExport.ToStraightBgra(bmp);

            using var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

            // File header (14 bytes)
            bw.Write((byte)'B'); bw.Write((byte)'M');
            bw.Write(54 + dataSize);
            bw.Write((ushort)0); bw.Write((ushort)0);
            bw.Write((uint)54);

            // DIB header – BITMAPINFOHEADER (40 bytes)
            bw.Write((uint)40);
            bw.Write((int)bmp._width);
            bw.Write((int)bmp._height);  // positive = bottom-up
            bw.Write((ushort)1);          // color planes
            bw.Write((ushort)bpp);
            bw.Write((uint)0);            // BI_RGB
            bw.Write((uint)0);            // image size (0 for BI_RGB)
            bw.Write((int)Math.Round(bmp._dpiX / 0.0254));
            bw.Write((int)Math.Round(bmp._dpiY / 0.0254));
            bw.Write((uint)0);
            bw.Write((uint)0);

            // Pixel data – BMP stores rows bottom-up
            int dstBpp = bpp / 8;
            var rowBuf = new byte[rowBytes];

            for (int yi = bmp._height - 1; yi >= 0; yi--)
            {
                for (int x = 0; x < bmp._width; x++)
                {
                    int src = (yi * bmp._width + x) * 4;
                    int dst = x * dstBpp;
                    rowBuf[dst] = bgra[src];
                    rowBuf[dst + 1] = bgra[src + 1];
                    rowBuf[dst + 2] = bgra[src + 2];
                    if (dstBpp == 4)
                        rowBuf[dst + 3] = bgra[src + 3];
                }
                for (int p = bmp._width * dstBpp; p < rowBytes; p++) rowBuf[p] = 0;
                bw.Write(rowBuf, 0, rowBytes);
            }
        }
    }
}
