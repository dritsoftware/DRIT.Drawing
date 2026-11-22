using System;

namespace DRIT.Drawing.Codecs.Jpeg
{
    internal static class ColorConverter
    {
        public static void YCbCrToRgb(int y, int cb, int cr, out byte r, out byte g, out byte b)
        {
            int d = cb - 128;
            int e = cr - 128;

            int rr = y + ((91881 * e + 32768) >> 16);
            int gg = y - ((22554 * d + 46802 * e + 32768) >> 16);
            int bb = y + ((116130 * d + 32768) >> 16);

            r = (byte)ClampToByte(rr);
            g = (byte)ClampToByte(gg);
            b = (byte)ClampToByte(bb);
        }

        public static void GrayscaleToRgb(int y, out byte r, out byte g, out byte b)
        {
            byte v = (byte)ClampToByte(y);
            r = v;
            g = v;
            b = v;
        }

        /// <summary>
        /// Adobe YCCK (APP14 transform 2) to RGB. The colour transform was applied to the complement of the stored
        /// (inverted) CMY values, so the inverse transform followed by "255 - x" recovers the stored values, which
        /// are then multiplied by the stored (inverted) K.
        /// </summary>
        public static void YcckToRgb(int y, int cb, int cr, int k, out byte r, out byte g, out byte b)
        {
            YCbCrToRgb(y, cb, cr, out var yccR, out var yccG, out var yccB);
            AdobeCmykToRgb(255 - yccR, 255 - yccG, 255 - yccB, k, out r, out g, out b);
        }

        /// <summary>
        /// Adobe CMYK JPEGs store inverted values (255 = no ink), so each colour channel is simply the product of the
        /// stored channel and the stored K.
        /// </summary>
        public static void AdobeCmykToRgb(int c, int m, int y, int k, out byte r, out byte g, out byte b)
        {
            k = Math.Max(0, Math.Min(255, k));
            r = (byte)((Math.Max(0, Math.Min(255, c)) * k + 127) / 255);
            g = (byte)((Math.Max(0, Math.Min(255, m)) * k + 127) / 255);
            b = (byte)((Math.Max(0, Math.Min(255, y)) * k + 127) / 255);
        }

        public static byte CmykChannelToRgb(int channel, int black)
        {
            var value = ((255 - Math.Max(0, Math.Min(255, channel)))
                * (255 - Math.Max(0, Math.Min(255, black))) + 127) / 255;
            return (byte)Math.Max(0, Math.Min(255, value));
        }

        private static int ClampToByte(int value) => Math.Max(0, Math.Min(255, value));
    }
}
