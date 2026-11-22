using System.IO;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Codecs.Png
{
    internal static class ApngFrameBuilder
    {
        public static AnimationFrame Build(
            ApngFrameControl fc,
            byte[] compressed,
            int bitDepth, int colorType,
            byte[]? palette, byte[]? trns,
            int interlace)
        {
            int channels = PngHelpers.GetChannels(colorType);
            long strideLong = ((long)fc.Width * bitDepth * channels + 7) / 8;
            int stride = CodecValidation.CheckedBufferLength(strideLong, "APNG frame row");
            int bpp = System.Math.Max(1, (bitDepth * channels) / 8);
            long expectedRaw = interlace == 0
                ? (long)fc.Height * (stride + 1)
                : PngHelpers.Adam7RawLength(fc.Width, fc.Height, bitDepth, channels);
            CodecValidation.CheckedBufferLength(expectedRaw, "APNG frame data");
            int bgraLength = CodecValidation.CheckedBufferLength((long)fc.Width * fc.Height * 4, "APNG frame pixels");

            byte[] raw = ZlibHelper.Decompress(compressed, expectedRaw);
            if (raw.Length != expectedRaw)
                throw new InvalidDataException("APNG frame data length does not match its frame control.");

            var bgra = new byte[bgraLength];
            if (interlace == 1)
            {
                // Every frame of an interlaced APNG is itself Adam7-interlaced.
                byte[] rgba = PngHelpers.ReconstructAdam7Rgba(raw, fc.Width, fc.Height, bitDepth, colorType, palette, trns);
                for (int i = 0; i < bgraLength; i += 4)
                {
                    bgra[i] = rgba[i + 2];
                    bgra[i + 1] = rgba[i + 1];
                    bgra[i + 2] = rgba[i];
                    bgra[i + 3] = rgba[i + 3];
                }
            }
            else
            {
                byte[] pixels = PngHelpers.ReconstructFilters(raw, fc.Width, fc.Height, stride, bpp);
                for (int y = 0; y < fc.Height; y++)
                for (int x = 0; x < fc.Width; x++)
                {
                    PngHelpers.GetPixelRgba(pixels, x, y, stride, bitDepth, colorType,
                                             palette, trns, out byte r, out byte g, out byte b, out byte a);
                    int off = y * fc.Width * 4 + x * 4;
                    bgra[off] = b;
                    bgra[off + 1] = g;
                    bgra[off + 2] = r;
                    bgra[off + 3] = a;
                }
            }

            var frame = new AnimationFrame(fc.Bounds, bgra)
            {
                DelayMilliseconds = fc.DelayMilliseconds,
                DisposeMethod = fc.DisposeMethod,
                BlendMethod = fc.BlendMethod
            };
            return frame;
        }
    }
}
