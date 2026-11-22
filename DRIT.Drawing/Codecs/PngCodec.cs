using System;
using System.Collections.Generic;
using System.IO;
using DRIT.Drawing.Codecs.Png;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Codecs
{
    internal sealed class PngCodec : IImageCodec
    {
        private static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        // Image-data chunks may be as large as the whole input; every other chunk is metadata.
        private static long MaxImageChunkLength => CodecValidation.MaxSourceBytes;
        private const long MaxMetadataChunkLength = CodecValidation.MaxMetadataBytes;
        private static long MaxCompressedDataLength => CodecValidation.MaxSourceBytes;

        public bool CanDecode(byte[] header)
        {
            if (header.Length < 8) return false;
            for (int i = 0; i < 8; i++)
                if (header[i] != Signature[i]) return false;
            return true;
        }

        // ─── Decode ──────────────────────────────────────────────────────────────

        public Bitmap Decode(Stream stream)
        {
            bool strict = DecodePolicy.Strict;
            var sig = new byte[8];
            PngHelpers.ReadFull(stream, sig, 0, 8);
            for (int i = 0; i < Signature.Length; i++)
            {
                if (sig[i] != Signature[i])
                    throw new InvalidDataException("Invalid PNG signature.");
            }

            int width = 0, height = 0, bitDepth = 0, colorType = 0, interlace = 0;
            byte[]? palette = null;
            byte[]? trnsData = null;
            int dpiX = 0, dpiY = 0;
            var idatBuffer = new MemoryStream();
            long compressedDataLength = 0;

            ApngAnimationControl? acTL = null;
            ApngFrameControl? pendingFC = null;
            ApngFrameControl? defaultImageFC = null;
            var frameDataList = new List<(ApngFrameControl fc, MemoryStream data)>();
            MemoryStream? pendingFrameData = null;
            bool defaultImageIsFrame0 = false;
            bool sawFirstIdat = false;
            bool sawIhdr = false;
            bool sawIend = false;
            bool hasApngSequence = false;
            uint nextApngSequence = 0;

            void CompletePendingFrame()
            {
                if (!pendingFC.HasValue)
                    return;

                if (pendingFrameData == null || pendingFrameData.Length == 0)
                    throw new InvalidDataException("APNG frame has no fdAT data.");
                CodecValidation.ValidateCount(frameDataList.Count + 1, CodecValidation.MaxFrameCount, "APNG frame count");
                frameDataList.Add((pendingFC.Value, pendingFrameData));
                pendingFC = null;
                pendingFrameData = null;
            }

            var chunkHeader = new byte[8];
            var crcBuf = new byte[4];
            while (true)
            {
                PngHelpers.ReadFull(stream, chunkHeader, 0, 8);
                uint chunkLengthValue = BinaryBE.ReadUInt32(chunkHeader, 0);
                string chunkType = System.Text.Encoding.ASCII.GetString(chunkHeader, 4, 4);
                ValidateChunkType(chunkType);

                if (!sawIhdr && chunkType != "IHDR")
                    throw new InvalidDataException("PNG IHDR must be the first chunk.");

                bool isImageData = chunkType == "IDAT" || chunkType == "fdAT";
                long maxChunkLength = isImageData ? MaxImageChunkLength : MaxMetadataChunkLength;
                if (chunkLengthValue > maxChunkLength)
                    throw new InvalidDataException($"PNG chunk {chunkType} is too large: {chunkLengthValue} bytes.");
                int chunkLen = (int)chunkLengthValue;

                // The payload is only allocated after the stream has proven it can supply it.
                byte[] chunkData = PngHelpers.ReadChunkPayload(stream, chunkLen);

                PngHelpers.ReadFull(stream, crcBuf, 0, 4);
                uint crc = Crc32.Update(Crc32.Start, chunkHeader, 4, 4);
                crc = Crc32.Update(crc, chunkData, 0, chunkLen);
                if (BinaryBE.ReadUInt32(crcBuf, 0) != Crc32.Finish(crc))
                {
                    // A damaged checksum on data the decoder needs is always an error. Damaged
                    // optional chunks (text, gamma, pHYs, tRNS, ...) are dropped, as libpng does.
                    if (strict || IsEssentialChunk(chunkType))
                        throw new InvalidDataException($"PNG CRC mismatch for {chunkType}.");
                    continue;
                }

                switch (chunkType)
                {
                    case "IHDR":
                        if (sawIhdr || chunkLen != 13)
                            throw new InvalidDataException("PNG must contain exactly one 13-byte IHDR chunk.");
                        width = (int)BinaryBE.ReadUInt32(chunkData, 0);
                        height = (int)BinaryBE.ReadUInt32(chunkData, 4);
                        bitDepth = chunkData[8];
                        colorType = chunkData[9];
                        interlace = chunkData[12];
                        ValidateHeader(width, height, bitDepth, colorType, chunkData[10], chunkData[11], interlace);
                        sawIhdr = true;
                        break;

                    case "PLTE":
                        if (sawFirstIdat || chunkLen == 0 || chunkLen % 3 != 0 || chunkLen / 3 > 256)
                            throw new InvalidDataException("Invalid or late PNG PLTE chunk.");
                        palette = chunkData;
                        break;

                    case "tRNS":
                        if (sawFirstIdat)
                        {
                            if (strict)
                                throw new InvalidDataException("PNG tRNS must precede image data.");
                            break;
                        }
                        try
                        {
                            ValidateTransparencyData(chunkData, colorType, palette);
                            trnsData = chunkData;
                        }
                        catch (InvalidDataException) when (!strict)
                        {
                            // An unusable transparency chunk only means "fully opaque".
                        }
                        break;

                    case "pHYs":
                        if (chunkLen != 9)
                        {
                            if (strict)
                                throw new InvalidDataException("PNG pHYs chunk must contain 9 bytes.");
                            break;
                        }
                        if (chunkData[8] == 1)
                        {
                            dpiX = (int)Math.Round(BinaryBE.ReadUInt32(chunkData, 0) * 0.0254);
                            dpiY = (int)Math.Round(BinaryBE.ReadUInt32(chunkData, 4) * 0.0254);
                        }
                        break;

                    case "acTL":
                        if (acTL.HasValue || sawFirstIdat || chunkLen != 8)
                            throw new InvalidDataException("APNG acTL is duplicated, late, or malformed.");
                        acTL = ApngAnimationControl.Parse(chunkData);
                        if (acTL.Value.NumFrames <= 0)
                            throw new InvalidDataException("APNG must declare at least one frame.");
                        hasApngSequence = true;
                        nextApngSequence = 0;
                        break;

                    case "fcTL":
                        if (!acTL.HasValue || chunkLen != 26)
                            throw new InvalidDataException("APNG fcTL requires a valid acTL and 26-byte payload.");
                        CompletePendingFrame();
                        pendingFC = ApngFrameControl.Parse(chunkData);
                        ValidateSequence(pendingFC.Value.SequenceNumber, ref nextApngSequence, hasApngSequence);
                        ValidateFrameControl(pendingFC.Value, width, height);
                        if (!sawFirstIdat && defaultImageFC == null)
                        {
                            defaultImageFC = pendingFC;
                            defaultImageIsFrame0 = true;
                            pendingFC = null;
                        }
                        else
                        {
                            pendingFrameData = new MemoryStream();
                        }
                        break;

                    case "IDAT":
                        if (chunkLen == 0 || (acTL.HasValue && pendingFC.HasValue && !defaultImageIsFrame0))
                            throw new InvalidDataException("PNG IDAT is empty or appears inside an APNG frame.");
                        compressedDataLength += chunkLen;
                        if (compressedDataLength > MaxCompressedDataLength)
                            throw new InvalidDataException("PNG compressed data exceeds the configured limit.");
                        idatBuffer.Write(chunkData, 0, chunkLen);
                        sawFirstIdat = true;
                        break;

                    case "fdAT":
                        if (!acTL.HasValue || !pendingFC.HasValue || pendingFrameData == null || chunkLen < 4)
                            throw new InvalidDataException("APNG fdAT has no active frame or is too short.");
                        ValidateSequence(BinaryBE.ReadUInt32(chunkData, 0), ref nextApngSequence, hasApngSequence);
                        compressedDataLength += chunkLen - 4;
                        if (compressedDataLength > MaxCompressedDataLength)
                            throw new InvalidDataException("PNG compressed data exceeds the configured limit.");
                        pendingFrameData.Write(chunkData, 4, chunkLen - 4);
                        break;

                    case "IEND":
                        if (chunkLen != 0)
                            throw new InvalidDataException("PNG IEND must be empty.");
                        CompletePendingFrame();
                        sawIend = true;
                        break;

                    default:
                        if (IsCriticalChunk(chunkType))
                            throw new InvalidDataException($"Unsupported critical PNG chunk {chunkType}.");
                        break;
                }

                if (sawIend)
                    break;
            }

            if (!sawIhdr || !sawIend || !sawFirstIdat || idatBuffer.Length == 0)
                throw new InvalidDataException("PNG is missing IHDR, IDAT, or IEND.");
            // Bytes after IEND are ignored by libpng and browsers (and legitimately occur when a PNG is
            // embedded in a larger stream); they are only an error when strict conformance is requested.
            if (strict && stream.ReadByte() != -1)
                throw new InvalidDataException("PNG contains data after IEND.");
            if (colorType == 3 && palette == null)
                throw new InvalidDataException("Palette PNG is missing PLTE.");

            int channels = PngHelpers.GetChannels(colorType);
            long strideLong = ((long)width * bitDepth * channels + 7) / 8;
            int stride = CodecValidation.CheckedBufferLength(strideLong, "PNG row");
            int bppFilter = Math.Max(1, (bitDepth * channels) / 8);
            long expectedRawLength = interlace == 0
                ? (long)height * (stride + 1)
                : PngHelpers.Adam7RawLength(width, height, bitDepth, channels);
            CodecValidation.CheckedBufferLength(expectedRawLength, "PNG filtered data");
            CodecValidation.CheckedBufferLength((long)width * height * 4, "PNG decoded pixels");

            byte[] compressed = idatBuffer.ToArray();
            idatBuffer.Dispose();
            // The inflated size is fully determined by IHDR: never let the stream produce more.
            byte[] raw = ZlibHelper.Decompress(compressed, expectedRawLength);
            if (raw.Length != expectedRawLength)
                throw new InvalidDataException("PNG decompressed data length does not match IHDR.");

            byte[]? interlacedRgba = interlace == 1
                ? PngHelpers.ReconstructAdam7Rgba(raw, width, height, bitDepth, colorType, palette, trnsData)
                : null;
            byte[] pixels = interlacedRgba ?? PngHelpers.ReconstructFilters(raw, width, height, stride, bppFilter);

            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            bmp._rawFormat = ImageFormat.Png;
            if (dpiX > 0) bmp._dpiX = dpiX;
            if (dpiY > 0) bmp._dpiY = dpiY;

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                byte r;
                byte g;
                byte b;
                byte a;
                if (interlacedRgba != null)
                {
                    int rgbaOffset = (y * width + x) * 4;
                    r = interlacedRgba[rgbaOffset];
                    g = interlacedRgba[rgbaOffset + 1];
                    b = interlacedRgba[rgbaOffset + 2];
                    a = interlacedRgba[rgbaOffset + 3];
                }
                else
                {
                    PngHelpers.GetPixelRgba(pixels, x, y, stride, bitDepth, colorType,
                        palette, trnsData, out r, out g, out b, out a);
                }
                int off = y * bmp._stride + x * 4;
                bmp._pixels[off] = b;
                bmp._pixels[off + 1] = g;
                bmp._pixels[off + 2] = r;
                bmp._pixels[off + 3] = a;
            }

            if (acTL.HasValue && acTL.Value.NumFrames > 0)
            {
                var frames = new List<AnimationFrame>();
                long decodedFrameBytes = 0;

                void AddFrame(ApngFrameControl fc, byte[] data)
                {
                    // Aggregate budget: the per-frame limits alone allow gigabytes from a small file.
                    decodedFrameBytes = CodecValidation.AddAnimationFrame(decodedFrameBytes, fc.Width, fc.Height, "APNG");
                    frames.Add(ApngFrameBuilder.Build(fc, data, bitDepth, colorType, palette, trnsData, interlace));
                }

                if (defaultImageIsFrame0 && defaultImageFC.HasValue)
                    AddFrame(defaultImageFC.Value, compressed);

                foreach (var (fc, data) in frameDataList)
                    AddFrame(fc, data.ToArray());

                if (frames.Count != acTL.Value.NumFrames)
                    throw new InvalidDataException("APNG frame count does not match acTL.");
                bmp.SetAnimationFrames(frames, acTL.Value.NumPlays);
            }

            return bmp;
        }

        private static void ValidateHeader(int width, int height, int bitDepth, int colorType, byte compression, byte filter, int interlace)
        {
            CodecValidation.ValidateDimensions(width, height, "PNG");
            if (compression != 0 || filter != 0 || (interlace != 0 && interlace != 1))
                throw new InvalidDataException("PNG uses an unsupported compression, filter, or interlace method.");

            bool valid = colorType switch
            {
                0 => bitDepth is 1 or 2 or 4 or 8 or 16,
                2 => bitDepth is 8 or 16,
                3 => bitDepth is 1 or 2 or 4 or 8,
                4 => bitDepth is 8 or 16,
                6 => bitDepth is 8 or 16,
                _ => false
            };
            if (!valid)
                throw new InvalidDataException($"PNG color type {colorType} and bit depth {bitDepth} are invalid.");
        }

        private static void ValidateTransparencyData(byte[] data, int colorType, byte[]? palette)
        {
            int maximum = colorType switch
            {
                0 => 2,
                2 => 6,
                3 => palette?.Length / 3 ?? 0,
                _ => 0
            };
            if (maximum == 0 || data.Length > maximum)
                throw new InvalidDataException("PNG tRNS payload is invalid for the color type.");
        }

        private static void ValidateFrameControl(ApngFrameControl frame, int canvasWidth, int canvasHeight)
        {
            if (frame.Width <= 0 || frame.Height <= 0
                || frame.XOffset < 0 || frame.YOffset < 0
                || (long)frame.XOffset + frame.Width > canvasWidth
                || (long)frame.YOffset + frame.Height > canvasHeight
                || (int)frame.DisposeMethod > 2
                || (int)frame.BlendMethod > 1)
            {
                throw new InvalidDataException("APNG frame control is outside the canvas or uses an invalid mode.");
            }
        }

        private static void ValidateSequence(uint actual, ref uint expected, bool enabled)
        {
            if (!enabled || actual != expected)
                throw new InvalidDataException($"APNG sequence number {actual} was expected to be {expected}.");
            expected++;
        }

        private static void ValidateChunkType(string chunkType)
        {
            if (chunkType.Length != 4)
                throw new InvalidDataException("PNG chunk type is invalid.");
            for (int i = 0; i < chunkType.Length; i++)
            {
                if (!char.IsLetter(chunkType[i]))
                    throw new InvalidDataException("PNG chunk type is invalid.");
            }
        }

        /// <summary>Chunks whose content the decoder cannot do without (critical chunks plus APNG control chunks).</summary>
        private static bool IsEssentialChunk(string chunkType)
            => chunkType == "IHDR" || chunkType == "PLTE" || chunkType == "IDAT" || chunkType == "IEND"
            || chunkType == "acTL" || chunkType == "fcTL" || chunkType == "fdAT"
            || IsCriticalChunk(chunkType);

        private static bool IsCriticalChunk(string chunkType)
            => chunkType.Length > 0 && char.IsUpper(chunkType[0]);

        // ─── Encode ──────────────────────────────────────────────────────────────

        public void Encode(Bitmap bmp, Stream stream, EncoderParameters? parameters)
        {
            int frameCount = bmp.GetFrameCount(FrameDimension.Time);
            if (frameCount > 1)
            {
                EncodeApng(bmp, stream, frameCount);
                return;
            }

            EncodeStatic(bmp, stream);
        }

        private static void EncodeStatic(Bitmap bmp, Stream stream)
        {
            bool hasAlpha = PixelExport.HasAlpha(bmp);
            int channels = hasAlpha ? 4 : 3;
            int colorTypeOut = hasAlpha ? 6 : 2;

            byte[] bgra = PixelExport.ToStraightBgra(bmp);
            int rawStride = checked(bmp._width * channels);
            var rawData = new byte[checked((rawStride + 1) * bmp._height)];
            for (int y = 0; y < bmp._height; y++)
            {
                int rawRow = y * (rawStride + 1);
                rawData[rawRow] = 0;
                for (int x = 0; x < bmp._width; x++)
                {
                    int src = (y * bmp._width + x) * 4;
                    int dst = rawRow + 1 + x * channels;
                    rawData[dst] = bgra[src + 2];
                    rawData[dst + 1] = bgra[src + 1];
                    rawData[dst + 2] = bgra[src];
                    if (hasAlpha) rawData[dst + 3] = bgra[src + 3];
                }
            }

            byte[] idatData = ZlibHelper.Compress(rawData);

            uint ppmX = (uint)Math.Round(bmp._dpiX / 0.0254);
            uint ppmY = (uint)Math.Round(bmp._dpiY / 0.0254);

            stream.Write(Signature, 0, 8);

            var ihdr = new byte[13];
            BinaryBE.WriteUInt32(ihdr, 0, (uint)bmp._width);
            BinaryBE.WriteUInt32(ihdr, 4, (uint)bmp._height);
            ihdr[8] = 8;
            ihdr[9] = (byte)colorTypeOut;
            ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
            PngHelpers.WriteChunk(stream, "IHDR", ihdr);

            var phys = new byte[9];
            BinaryBE.WriteUInt32(phys, 0, ppmX);
            BinaryBE.WriteUInt32(phys, 4, ppmY);
            phys[8] = 1;
            PngHelpers.WriteChunk(stream, "pHYs", phys);

            PngHelpers.WriteChunk(stream, "IDAT", idatData);
            PngHelpers.WriteChunk(stream, "IEND", Array.Empty<byte>());
        }

        /// <summary>
        /// Writes an APNG with every frame as a full-canvas image carrying its own delay, and the animation's
        /// loop count. Frames are composed on a private canvas, so the bitmap's active frame is left untouched.
        /// </summary>
        private static void EncodeApng(Bitmap bmp, Stream stream, int frameCount)
        {
            List<AnimationFrame> frames = bmp.GetAnimationFrames()
                ?? throw new InvalidOperationException("The bitmap reports several frames but holds none.");
            int canvasW = bmp._width;
            int canvasH = bmp._height;

            uint ppmX = (uint)Math.Round(bmp._dpiX / 0.0254);
            uint ppmY = (uint)Math.Round(bmp._dpiY / 0.0254);

            stream.Write(Signature, 0, 8);

            var ihdr = new byte[13];
            BinaryBE.WriteUInt32(ihdr, 0, (uint)canvasW);
            BinaryBE.WriteUInt32(ihdr, 4, (uint)canvasH);
            ihdr[8] = 8;
            ihdr[9] = 6;
            ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
            PngHelpers.WriteChunk(stream, "IHDR", ihdr);

            var phys = new byte[9];
            BinaryBE.WriteUInt32(phys, 0, ppmX);
            BinaryBE.WriteUInt32(phys, 4, ppmY);
            phys[8] = 1;
            PngHelpers.WriteChunk(stream, "pHYs", phys);

            var actl = new byte[8];
            BinaryBE.WriteUInt32(actl, 0, (uint)frameCount);
            BinaryBE.WriteUInt32(actl, 4, (uint)Math.Max(0, bmp.GetAnimationLoopCount()));
            PngHelpers.WriteChunk(stream, "acTL", actl);

            var animator = new FrameAnimator(canvasW, canvasH, frames);
            uint seq = 0;
            for (int i = 0; i < frameCount; i++)
            {
                byte[] canvas = animator.Compose(i);

                int rawStride = checked(canvasW * 4);
                var rawData = new byte[checked((rawStride + 1) * canvasH)];
                for (int y = 0; y < canvasH; y++)
                {
                    int rawRow = y * (rawStride + 1);
                    rawData[rawRow] = 0;
                    int srcRow = y * canvasW * 4;
                    for (int x = 0; x < canvasW; x++)
                    {
                        int src = srcRow + x * 4;
                        int dst = rawRow + 1 + x * 4;
                        rawData[dst] = canvas[src + 2];
                        rawData[dst + 1] = canvas[src + 1];
                        rawData[dst + 2] = canvas[src];
                        rawData[dst + 3] = canvas[src + 3];
                    }
                }

                byte[] compressed = ZlibHelper.Compress(rawData);

                var fctl = new byte[26];
                BinaryBE.WriteUInt32(fctl, 0, seq++);
                BinaryBE.WriteUInt32(fctl, 4, (uint)canvasW);
                BinaryBE.WriteUInt32(fctl, 8, (uint)canvasH);
                BinaryBE.WriteUInt32(fctl, 12, 0);
                BinaryBE.WriteUInt32(fctl, 16, 0);
                BinaryBE.WriteUInt16(fctl, 20, (ushort)Math.Max(0, Math.Min(65535, frames[i].DelayMilliseconds)));
                BinaryBE.WriteUInt16(fctl, 22, 1000);   // delay = milliseconds / 1000 s
                fctl[24] = 0;   // dispose: none (every frame is a complete canvas)
                fctl[25] = 0;   // blend: source
                PngHelpers.WriteChunk(stream, "fcTL", fctl);

                if (i == 0)
                {
                    PngHelpers.WriteChunk(stream, "IDAT", compressed);
                }
                else
                {
                    var fdat = new byte[4 + compressed.Length];
                    BinaryBE.WriteUInt32(fdat, 0, seq++);
                    Buffer.BlockCopy(compressed, 0, fdat, 4, compressed.Length);
                    PngHelpers.WriteChunk(stream, "fdAT", fdat);
                }
            }

            PngHelpers.WriteChunk(stream, "IEND", Array.Empty<byte>());
        }
    }
}
