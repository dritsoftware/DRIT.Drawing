using System;
using System.IO;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Codecs
{
    /// <summary>
    /// Shared resource and range validation used by native image codecs.
    /// Limits are intentionally conservative because codec input is untrusted.
    /// </summary>
    internal static class CodecValidation
    {
        // The limits of the decode in progress: the per-call limits when one is running with its own,
        // otherwise the process-wide default.
        [ThreadStatic]
        private static ImageDecodeLimits? _scopedLimits;

        internal static ImageDecodeLimits Limits => _scopedLimits ?? ImageDecodeLimits.Default;

        internal static int MaxDimension => Limits.MaxDimension;
        internal static long MaxDecodedPixels => Limits.MaxPixels;
        internal static long MaxDecodedBufferBytes => Math.Min(Limits.MaxDecodedBytes, int.MaxValue);
        internal static long MaxSourceBytes => Limits.MaxSourceBytes;
        internal static int MaxFrameCount => Limits.MaxFrameCount;
        internal static long MaxAnimationBytes => Limits.MaxAnimationBytes;

        /// <summary>Makes <paramref name="limits"/> (when not null) the limits of this thread's decodes until disposed.</summary>
        internal static IDisposable Scope(ImageDecodeLimits? limits) => new LimitsScope(limits);

        private sealed class LimitsScope : IDisposable
        {
            private readonly ImageDecodeLimits? _previous;

            public LimitsScope(ImageDecodeLimits? limits)
            {
                _previous = _scopedLimits;
                if (limits != null)
                    _scopedLimits = limits;
            }

            public void Dispose() => _scopedLimits = _previous;
        }

        /// <summary>
        /// Largest accepted metadata/ancillary payload (PNG chunks other than image data, ...).
        /// Image data is bounded by <see cref="MaxSourceBytes"/>; metadata never needs more than this.
        /// </summary>
        internal const int MaxMetadataBytes = 16 * 1024 * 1024;


        internal static void ValidateDimensions(
            int width,
            int height,
            string formatName,
            long? maxPixels = null)
        {
            if (width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension)
                throw new InvalidDataException($"{formatName} dimensions are outside the supported range.");
            if ((long)width * height > (maxPixels ?? MaxDecodedPixels))
                throw new InvalidDataException($"{formatName} dimensions exceed the configured pixel limit.");
        }

        /// <summary>
        /// Adds one animation frame (<paramref name="width"/> x <paramref name="height"/> pixels at 4 bytes each)
        /// to the running decoded-size total and fails once the aggregate budget
        /// (<see cref="MaxAnimationBytes"/>) is exceeded. Returns the new total.
        /// </summary>
        internal static long AddAnimationFrame(long totalBytes, long width, long height, string formatName)
        {
            long frameBytes = checked(width * height * 4);
            if (frameBytes < 0 || totalBytes < 0 || totalBytes > MaxAnimationBytes - frameBytes)
                throw new InvalidDataException($"{formatName} frames exceed the configured decoded-size budget.");
            return totalBytes + frameBytes;
        }

        internal static int CheckedBufferLength(long length, string context)
        {
            if (length < 0 || length > int.MaxValue || length > MaxDecodedBufferBytes)
                throw new InvalidDataException($"{context} exceeds the configured allocation limit.");
            return (int)length;
        }

        internal static void ValidateRange(long offset, long length, long totalLength, string context)
        {
            if (offset < 0 || length < 0 || totalLength < 0 || offset > totalLength - length)
                throw new InvalidDataException($"{context} is outside the input range.");
        }

        internal static void ValidateCount(int count, int maximum, string context)
        {
            if (count < 0 || count > maximum)
                throw new InvalidDataException($"{context} exceeds the configured count limit.");
        }

        internal static byte[] ReadAll(Stream stream, string formatName)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            long total = 0;
            while (true)
            {
                int read = stream.Read(chunk, 0, chunk.Length);
                if (read == 0)
                    break;
                total += read;
                if (total > MaxSourceBytes)
                    throw new InvalidDataException($"{formatName} source exceeds the configured input limit.");
                buffer.Write(chunk, 0, read);
            }
            return buffer.ToArray();
        }
    }
}
