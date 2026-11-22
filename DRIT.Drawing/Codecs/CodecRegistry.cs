using System;
using System.IO;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Codecs
{
    /// <summary>
    /// Central registry that routes encode/decode operations to the appropriate codec.
    /// </summary>
    internal static class CodecRegistry
    {
        private static readonly IImageCodec[] Codecs =
        {
            new BmpCodec(),
            new PngCodec(),
            new GifCodec(),
            new JpegCodec(),
            new TiffCodec(),
            new IcoCodec(),
            new EmfCodec(),
        };

        // ─── Decode ──────────────────────────────────────────────────────────────

        public static Bitmap Decode(Stream stream, ImageDecodeLimits? limits = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            using (CodecValidation.Scope(limits))
                return DecodeWithCurrentLimits(stream);
        }

        private static Bitmap DecodeWithCurrentLimits(Stream stream)
        {
            // Ensure seekable
            Stream seekable;
            bool   ownStream = false;
            if (!stream.CanSeek)
            {
                var ms = new MemoryStream(CodecValidation.ReadAll(stream, "image"));
                ms.Position = 0;
                seekable  = ms;
                ownStream = true;
            }
            else
            {
                seekable = stream;
            }

            try
            {
                // A single Read may legitimately return fewer bytes than asked for.
                var header = new byte[12];
                int n = 0;
                while (n < header.Length)
                {
                    int read = seekable.Read(header, n, header.Length - n);
                    if (read <= 0)
                        break;
                    n += read;
                }
                if (n == 0)
                    throw new InvalidDataException("The stream contains no image data.");
                seekable.Seek(-n, SeekOrigin.Current);

                foreach (var codec in Codecs)
                {
                    if (codec.CanDecode(header))
                        return DecodeWithCodec(codec, seekable);
                }

                throw new NotSupportedException("Unknown or unsupported image format.");
            }
            finally
            {
                if (ownStream) seekable.Dispose();
            }
        }

        /// <summary>
        /// Runs a decoder and reports every kind of "the bytes do not describe a valid image" as
        /// <see cref="InvalidDataException"/>: a decoder that trips over truncated or inconsistent input with an
        /// end-of-stream, index, overflow or argument exception must not leak that detail to callers.
        /// <see cref="NotSupportedException"/> (valid but unsupported) and out-of-memory conditions pass through.
        /// </summary>
        private static Bitmap DecodeWithCodec(IImageCodec codec, Stream stream)
        {
            try
            {
                return codec.Decode(stream);
            }
            catch (Exception ex) when (ex is EndOfStreamException
                || ex is IndexOutOfRangeException
                || ex is OverflowException
                || ex is DivideByZeroException
                || ex is FormatException
                || (ex is ArgumentException && !(ex is ArgumentNullException)))
            {
                throw new InvalidDataException("The image data is malformed: " + ex.Message, ex);
            }
        }

        // ─── Encode ──────────────────────────────────────────────────────────────

        public static void Encode(Bitmap bmp, Stream stream, ImageFormat format,
                                  EncoderParameters? parameters)
        {
            var codec = FindEncoder(format);
            if (codec == null)
                throw new NotSupportedException($"No encoder found for format {format?.Name}.");
            codec.Encode(bmp, stream, parameters);
        }

        private static IImageCodec? FindEncoder(ImageFormat? format)
        {
            if (format == null) return null;
            if (format.Equals(ImageFormat.Bmp)      || format.Equals(ImageFormat.MemoryBmp))
                return Codecs[0]; // BmpCodec
            if (format.Equals(ImageFormat.Png))  return Codecs[1]; // PngCodec
            if (format.Equals(ImageFormat.Gif))  return Codecs[2]; // GifCodec
            if (format.Equals(ImageFormat.Jpeg)) return Codecs[3]; // JpegCodec
            if (format.Equals(ImageFormat.Tiff)) return Codecs[4]; // TiffCodec
            return null;
        }
    }
}
