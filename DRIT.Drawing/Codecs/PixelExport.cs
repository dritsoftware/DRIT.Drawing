using System;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Codecs
{
    /// <summary>
    /// Reads a <see cref="Bitmap"/> in any supported pixel format as tightly packed, straight-alpha (not
    /// premultiplied) BGRA. All encoders go through it, so none of them has to know the bitmap's storage format.
    /// </summary>
    internal static class PixelExport
    {
        /// <summary>True when the bitmap's format carries meaningful alpha.</summary>
        public static bool HasAlpha(Bitmap bmp)
            => bmp._format == PixelFormat.Format32bppArgb || bmp._format == PixelFormat.Format32bppPArgb;

        /// <summary>The bitmap as width x height x 4 bytes in B, G, R, A order with straight alpha.</summary>
        public static byte[] ToStraightBgra(Bitmap bmp)
        {
            if (bmp == null) throw new ArgumentNullException(nameof(bmp));
            int width = bmp._width, height = bmp._height;
            var result = new byte[checked(width * height * 4)];
            byte[] source = bmp._pixels;
            int stride = bmp._stride;

            switch (bmp._format)
            {
                case PixelFormat.Format32bppArgb:
                    for (int y = 0; y < height; y++)
                        Buffer.BlockCopy(source, y * stride, result, y * width * 4, width * 4);
                    break;

                case PixelFormat.Format32bppRgb:
                    // The fourth byte of an RGB format is padding.
                    for (int y = 0; y < height; y++)
                    {
                        int src = y * stride, dst = y * width * 4;
                        for (int x = 0; x < width; x++, src += 4, dst += 4)
                        {
                            result[dst] = source[src];
                            result[dst + 1] = source[src + 1];
                            result[dst + 2] = source[src + 2];
                            result[dst + 3] = 255;
                        }
                    }
                    break;

                case PixelFormat.Format32bppPArgb:
                    for (int y = 0; y < height; y++)
                    {
                        int src = y * stride, dst = y * width * 4;
                        for (int x = 0; x < width; x++, src += 4, dst += 4)
                        {
                            int a = source[src + 3];
                            result[dst + 3] = (byte)a;
                            if (a == 0)
                                continue;
                            // Undo the premultiplication (rounded), clamped against rounding overshoot.
                            result[dst] = (byte)Math.Min(255, (source[src] * 255 + a / 2) / a);
                            result[dst + 1] = (byte)Math.Min(255, (source[src + 1] * 255 + a / 2) / a);
                            result[dst + 2] = (byte)Math.Min(255, (source[src + 2] * 255 + a / 2) / a);
                        }
                    }
                    break;

                case PixelFormat.Format24bppRgb:
                    for (int y = 0; y < height; y++)
                    {
                        int src = y * stride, dst = y * width * 4;
                        for (int x = 0; x < width; x++, src += 3, dst += 4)
                        {
                            result[dst] = source[src];
                            result[dst + 1] = source[src + 1];
                            result[dst + 2] = source[src + 2];
                            result[dst + 3] = 255;
                        }
                    }
                    break;

                default:
                    // Remaining formats go through GetPixel (which supports RGB565 and throws a clear
                    // NotSupportedException for the rest).
                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        Color c = bmp.GetPixel(x, y);
                        int dst = (y * width + x) * 4;
                        result[dst] = c.B;
                        result[dst + 1] = c.G;
                        result[dst + 2] = c.R;
                        result[dst + 3] = c.A;
                    }
                    break;
            }

            return result;
        }
    }
}
