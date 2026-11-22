using System;

namespace DRIT.Drawing.Codecs.Jpeg
{
    internal static class UpsampleBuffer
    {
        public static byte[] UpsampleToFullResolution(
            byte[] source,
            int sourceWidth,
            int sourceHeight,
            int targetWidth,
            int targetHeight,
            int horizontalSampling,
            int verticalSampling,
            int maxHorizontalSampling,
            int maxVerticalSampling)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (sourceWidth <= 0 || sourceHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceWidth));
            if (targetWidth <= 0 || targetHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(targetWidth));

            var upsampled = new byte[checked(targetWidth * targetHeight)];

            // Column map computed once instead of two divisions per pixel.
            bool identityColumns = horizontalSampling == maxHorizontalSampling && sourceWidth >= targetWidth;
            int[]? columns = null;
            if (!identityColumns)
            {
                columns = new int[targetWidth];
                for (int x = 0; x < targetWidth; x++)
                {
                    int sx = (x * horizontalSampling) / maxHorizontalSampling;
                    columns[x] = sx >= sourceWidth ? sourceWidth - 1 : sx;
                }
            }

            for (int y = 0; y < targetHeight; y++)
            {
                int sy = (y * verticalSampling) / maxVerticalSampling;
                if (sy >= sourceHeight) sy = sourceHeight - 1;

                int dstRow = y * targetWidth;
                int srcRow = sy * sourceWidth;
                if (identityColumns)
                {
                    Buffer.BlockCopy(source, srcRow, upsampled, dstRow, targetWidth);
                    continue;
                }

                for (int x = 0; x < targetWidth; x++)
                    upsampled[dstRow + x] = source[srcRow + columns![x]];
            }

            return upsampled;
        }
    }
}
