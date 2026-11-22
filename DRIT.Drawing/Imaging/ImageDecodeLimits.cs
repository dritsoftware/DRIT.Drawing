using System;

namespace DRIT.Drawing.Imaging
{
    /// <summary>
    /// Resource limits applied while decoding untrusted images. A decoder never allocates on the strength of a
    /// size read from the file unless it is within these limits; a file that exceeds them fails with
    /// <see cref="System.IO.InvalidDataException"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Default"/> applies to every decode that does not pass its own limits. Applications that
    /// open large trusted files (scans, maps, panoramas) can raise it once at start-up; services that decode
    /// uploads can lower it, or pass a stricter instance per call to <see cref="Image.FromStream(System.IO.Stream, ImageDecodeLimits)"/>.
    /// </remarks>
    public sealed class ImageDecodeLimits
    {
        private static ImageDecodeLimits _default = new ImageDecodeLimits();

        /// <summary>
        /// Creates a set of limits. Every argument defaults to the library's standard value.
        /// </summary>
        /// <param name="maxPixels">Largest number of pixels in one image or frame.</param>
        /// <param name="maxDimension">Largest width or height in pixels.</param>
        /// <param name="maxDecodedBytes">Largest single buffer a decoder may allocate for decoded or intermediate data.</param>
        /// <param name="maxSourceBytes">Largest input (or compressed image data) a decoder will read.</param>
        /// <param name="maxFrameCount">Largest number of frames in an animation.</param>
        /// <param name="maxAnimationBytes">Largest total of decoded pixel data over all frames of one animation.</param>
        public ImageDecodeLimits(
            long maxPixels = 100L * 1000 * 1000,
            int maxDimension = 100_000,
            long maxDecodedBytes = 1024L * 1024 * 1024,
            long maxSourceBytes = 512L * 1024 * 1024,
            int maxFrameCount = 1024,
            long maxAnimationBytes = 512L * 1024 * 1024)
        {
            if (maxPixels <= 0) throw new ArgumentOutOfRangeException(nameof(maxPixels));
            if (maxDimension <= 0) throw new ArgumentOutOfRangeException(nameof(maxDimension));
            if (maxDecodedBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxDecodedBytes));
            if (maxSourceBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxSourceBytes));
            if (maxFrameCount <= 0) throw new ArgumentOutOfRangeException(nameof(maxFrameCount));
            if (maxAnimationBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxAnimationBytes));

            MaxPixels = maxPixels;
            MaxDimension = maxDimension;
            MaxDecodedBytes = maxDecodedBytes;
            MaxSourceBytes = maxSourceBytes;
            MaxFrameCount = maxFrameCount;
            MaxAnimationBytes = maxAnimationBytes;
        }

        /// <summary>The limits used when a decode does not specify its own. Settable once at application start-up.</summary>
        public static ImageDecodeLimits Default
        {
            get => _default;
            set => _default = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Largest number of pixels in one image or frame (default 100 million).</summary>
        public long MaxPixels { get; }

        /// <summary>Largest width or height in pixels (default 100 000).</summary>
        public int MaxDimension { get; }

        /// <summary>Largest single decoded or intermediate buffer in bytes (default 1 GiB; never more than 2 GiB).</summary>
        public long MaxDecodedBytes { get; }

        /// <summary>Largest input or compressed image data in bytes (default 512 MiB).</summary>
        public long MaxSourceBytes { get; }

        /// <summary>Largest number of animation frames (default 1024).</summary>
        public int MaxFrameCount { get; }

        /// <summary>Largest total decoded size of all frames of one animation in bytes (default 512 MiB).</summary>
        public long MaxAnimationBytes { get; }
    }
}
