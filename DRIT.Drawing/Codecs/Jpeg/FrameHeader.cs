using System;
using System.IO;

namespace DRIT.Drawing.Codecs.Jpeg
{
    /// <summary>JPEG Start-of-Frame type.</summary>
    internal enum SofType
    {
        Baseline          = 0,  // SOF0
        ExtendedSequential = 1, // SOF1
        Progressive        = 2, // SOF2
    }

    /// <summary>
    /// Parsed payload of a JPEG SOF0 / SOF1 / SOF2 marker.
    /// </summary>
    internal sealed class FrameHeader
    {
        /// <summary>Sample precision in bits (typically 8).</summary>
        public int Precision { get; private set; }

        /// <summary>Frame height in pixels.</summary>
        public int Height { get; private set; }

        /// <summary>Frame width in pixels.</summary>
        public int Width { get; private set; }

        /// <summary>Image components (1 = grayscale, 3 = YCbCr, 4 = CMYK).</summary>
        public FrameComponent[] Components { get; private set; } = Array.Empty<FrameComponent>();

        /// <summary>SOF type that was used to produce this header.</summary>
        public SofType Type { get; private set; }

        // ── Factory ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses a SOF0/SOF1/SOF2 segment payload.
        /// <paramref name="marker"/> determines the <see cref="SofType"/>.
        /// </summary>
        public static FrameHeader Parse(JpegMarker marker, byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length < 6)
                throw new InvalidDataException("SOF segment too short.");

            if (marker != JpegMarker.SOF0 && marker != JpegMarker.SOF1 && marker != JpegMarker.SOF2)
                throw new NotSupportedException($"JPEG frame marker {marker} is not supported.");

            var fh = new FrameHeader
            {
                Type      = MarkerToSofType(marker),
                Precision = data[0],
                Height    = (data[1] << 8) | data[2],
                Width     = (data[3] << 8) | data[4],
            };

            int nf = data[5];
            // 12-bit data needs 12-bit coefficient ranges, IDCT scaling and output conversion; decoding it with the
            // 8-bit pipeline would silently clip it, so it is refused until it is implemented.
            if (fh.Precision != 8)
                throw new NotSupportedException($"JPEG sample precision {fh.Precision} is not supported.");
            if (fh.Width <= 0 || fh.Height <= 0)
                throw new InvalidDataException("JPEG frame dimensions must be positive.");
            CodecValidation.ValidateDimensions(fh.Width, fh.Height, "JPEG");
            if (nf != 1 && nf != 3 && nf != 4)
                throw new NotSupportedException($"JPEG component count {nf} is not supported.");
            if (data.Length != 6 + nf * 3)
                throw new InvalidDataException("SOF segment length does not match its component count.");

            fh.Components = new FrameComponent[nf];
            for (int i = 0; i < nf; i++)
            {
                int off = 6 + i * 3;
                byte samp = data[off + 1];
                int hSampling = (samp >> 4) & 0xF;
                int vSampling = samp & 0xF;
                if (hSampling < 1 || hSampling > 4 || vSampling < 1 || vSampling > 4)
                    throw new InvalidDataException("JPEG sampling factors must be in the range 1..4.");
                if (data[off] == 0 && DecodePolicy.Strict)
                    throw new InvalidDataException("JPEG component identifiers must be non-zero.");
                for (int previous = 0; previous < i; previous++)
                {
                    if (fh.Components[previous].Id == data[off])
                        throw new InvalidDataException("JPEG frame contains duplicate component identifiers.");
                }
                if (data[off + 2] > 3)
                    throw new InvalidDataException("JPEG quantization table identifier is out of range.");

                fh.Components[i] = new FrameComponent
                {
                    Id                  = data[off],
                    HSampling           = (byte)hSampling,
                    VSampling           = (byte)vSampling,
                    QuantizationTableId = data[off + 2],
                };
            }
            if (nf == 1 && fh.Components.Length == 1
                && (fh.Components[0].HSampling > 1 || fh.Components[0].VSampling > 1))
            {
                // A few PDF producers emit Adobe grayscale JPEGs with chroma-style
                // sampling factors even though the scan contains one luminance plane.
                // Treating those factors as chroma subsampling tiles the decoded image.
                fh.Components[0].HSampling = 1;
                fh.Components[0].VSampling = 1;
            }
            return fh;
        }

        private static SofType MarkerToSofType(JpegMarker m) => m switch
        {
            JpegMarker.SOF0 => SofType.Baseline,
            JpegMarker.SOF1 => SofType.ExtendedSequential,
            JpegMarker.SOF2 => SofType.Progressive,
            _               => SofType.Baseline,
        };
    }
}
