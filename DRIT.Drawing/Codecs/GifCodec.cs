using System;
using System.Collections.Generic;
using System.IO;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Codecs
{
    /// <summary>
    /// GIF87a / GIF89a codec — reads and writes indexed colour with LZW.
    /// </summary>
    internal sealed class GifCodec : IImageCodec
    {
        public bool CanDecode(byte[] header)
            => header.Length >= 6
            && header[0] == 'G' && header[1] == 'I' && header[2] == 'F'
            && header[3] == '8' && (header[4] == '7' || header[4] == '9') && header[5] == 'a';

        // ─── Decode ──────────────────────────────────────────────────────────────

        public Bitmap Decode(Stream stream)
        {
            bool strict = DecodePolicy.Strict;
            var data = CodecValidation.ReadAll(stream, "GIF");
            if (data.Length < 13)
                throw new InvalidDataException("GIF header is truncated.");
            if (!CanDecode(data))
                throw new InvalidDataException("GIF signature is invalid.");
            int pos  = 0;

            // Header (6 bytes)
            pos += 6;

            // Logical Screen Descriptor (7 bytes)
            int canvasW   = data[pos] | (data[pos+1] << 8); pos += 2;
            int canvasH   = data[pos] | (data[pos+1] << 8); pos += 2;
            // A logical screen of 0x0 is meaningless but occurs in the wild (the frames then define the
            // size). Strict mode rejects it; otherwise the canvas is derived from the frames below.
            bool unsizedCanvas = canvasW == 0 || canvasH == 0;
            if (strict || !unsizedCanvas)
                CodecValidation.ValidateDimensions(canvasW, canvasH, "GIF");
            byte packed   = data[pos++];
            bool hasGct   = (packed & 0x80) != 0;
            int  gctSz    = hasGct ? 2 << (packed & 0x07) : 0;
            /*bgIndex*/     pos++; // background colour index
            /*aspect*/      pos++; // aspect ratio

            // Global Colour Table
            byte[]? gct = null;
            if (hasGct)
            {
                gct = new byte[gctSz * 3];
                EnsureRange(data, pos, gct.Length, "global color table");
                Buffer.BlockCopy(data, pos, gct, 0, gct.Length);
                pos += gct.Length;
            }

            // GCE fields (set by Graphic Control Extension)
            bool transpFlag  = false;
            int  transpIndex = 0;
            int delayMilliseconds = 0;
            FrameDisposeMethod disposeMethod = FrameDisposeMethod.None;
            var frames = new List<AnimationFrame>();
            int loopCount = 0;
            bool sawTrailer = false;
            long decodedFrameBytes = 0;
            int extentRight = 0, extentBottom = 0;

            // Parse blocks until the trailer.
            while (pos < data.Length)
            {
                byte blockId = data[pos++];

                if (blockId == 0x3B)
                {
                    sawTrailer = true;
                    break;
                }

                if (blockId == 0x21) // extension
                {
                    if (pos >= data.Length)
                        throw new InvalidDataException("GIF extension label is truncated.");
                    byte extLabel = data[pos++];
                    if (extLabel == 0xF9) // Graphic Control Extension
                    {
                        if (pos >= data.Length)
                            throw new InvalidDataException("GIF graphic control extension is truncated.");
                        int bsize = data[pos++];
                        if (bsize != 4)
                            throw new InvalidDataException("GIF graphic control extension must contain four bytes.");
                        EnsureRange(data, pos, bsize, "graphic control extension data");
                        byte gceFlags = data[pos];
                        // Bit 1 is the legal "user input expected" flag; only the three reserved bits
                        // (7-5) are checked, and only when strict conformance is requested.
                        if (strict && (gceFlags & 0xE0) != 0)
                            throw new InvalidDataException("GIF graphic control extension has reserved flags set.");
                        transpFlag    = (gceFlags & 1) != 0;
                        int disposal = (gceFlags >> 2) & 0x07;
                        disposeMethod = disposal == 2
                            ? FrameDisposeMethod.Background
                            : disposal == 3
                                ? FrameDisposeMethod.Previous
                                : FrameDisposeMethod.None;
                        delayMilliseconds = (data[pos + 1] | (data[pos + 2] << 8)) * 10;
                        transpIndex   = data[pos + 3];

                        // The block size describes the bytes that were just read. Leave
                        // the position at the following sub-block size byte; otherwise the
                        // first GCE data byte is interpreted as a sub-block length and the
                        // following image descriptor is skipped.
                        pos += bsize;
                        if (pos >= data.Length || data[pos++] != 0)
                            throw new InvalidDataException("GIF graphic control extension is missing its terminator.");
                    }
                    else if (extLabel == 0xFF) // Application extension (NETSCAPE2.0 carries the loop count)
                    {
                        ReadApplicationExtension(data, ref pos, ref loopCount);
                    }
                    else
                    {
                        // Other extensions consist of a sequence of data sub-blocks.
                        SkipSubBlocks(data, ref pos);
                    }
                    continue;
                }

                if (blockId == 0x2C) // image descriptor
                {
                    EnsureRange(data, pos, 9, "image descriptor");
                    int imgLeft = data[pos] | (data[pos+1] << 8); pos += 2;
                    int imgTop  = data[pos] | (data[pos+1] << 8); pos += 2;
                    int imgW    = data[pos] | (data[pos+1] << 8); pos += 2;
                    int imgH    = data[pos] | (data[pos+1] << 8); pos += 2;
                    byte imgPacked = data[pos++];
                    bool interlaced = (imgPacked & 0x40) != 0;
                    bool hasLct  = (imgPacked & 0x80) != 0;
                    int  lctSz   = hasLct ? 2 << (imgPacked & 0x07) : 0;

                    byte[]? lct = null;
                    if (hasLct)
                    {
                        lct = new byte[lctSz * 3];
                        EnsureRange(data, pos, lct.Length, "local color table");
                        Buffer.BlockCopy(data, pos, lct, 0, lct.Length);
                        pos += lct.Length;
                    }

                    if (imgW <= 0 || imgH <= 0 || (long)imgW * imgH > CodecValidation.MaxDecodedPixels)
                        throw new InvalidDataException("GIF image frame is empty or exceeds limits.");

                    // Frames that extend past the logical screen are rejected when strict; otherwise they
                    // are cropped to the screen, which is what browsers display.
                    int visibleW = imgW, visibleH = imgH;
                    bool outsideScreen = !unsizedCanvas
                        && ((long)imgLeft + imgW > canvasW || (long)imgTop + imgH > canvasH);
                    if (outsideScreen)
                    {
                        if (strict)
                            throw new InvalidDataException("GIF image frame is outside the logical screen.");
                        visibleW = Math.Max(0, Math.Min(imgW, canvasW - imgLeft));
                        visibleH = Math.Max(0, Math.Min(imgH, canvasH - imgTop));
                    }

                    byte[] palette = lct ?? gct ?? new byte[0];
                    if (palette.Length == 0 || palette.Length % 3 != 0)
                        throw new InvalidDataException("GIF image frame has no valid color table.");

                    if (pos >= data.Length)
                        throw new InvalidDataException("GIF LZW minimum code size is missing.");
                    int minCodeSize = data[pos++];
                    byte[] lzwData  = ReadSubBlocks(data, ref pos);

                    // Aggregate budget: per-frame and frame-count limits alone allow gigabytes from a small file.
                    decodedFrameBytes = CodecValidation.AddAnimationFrame(decodedFrameBytes, visibleW, visibleH, "GIF");

                    // Decompress LZW
                    int expectedPixels = checked(imgW * imgH);
                    var indices = LzwDecode(lzwData, minCodeSize, expectedPixels, strict);

                    int framePixelBytes = CodecValidation.CheckedBufferLength(checked((long)visibleW * visibleH * 4), "GIF frame pixels");
                    var framePixels = new byte[framePixelBytes];

                    // Interlaced frames store their rows in four passes; map each display row to its stored row.
                    int[]? rowMap = interlaced ? InterlacedRowMap(imgH) : null;

                    for (int y = 0; y < visibleH; y++)
                    for (int x = 0; x < visibleW; x++)
                    {
                        int idx = indices[(rowMap != null ? rowMap[y] : y) * imgW + x];
                        byte r = 0, g = 0, b = 0, a = 255;
                        if (idx < 0)
                        {
                            // Pixel the (truncated) LZW stream never produced: leave it transparent.
                            a = 0;
                        }
                        else if (idx * 3 + 2 >= palette.Length)
                        {
                            if (strict)
                                throw new InvalidDataException("GIF pixel index exceeds the active color table.");
                            a = 0; // undefined by the specification; browsers leave it transparent
                        }
                        else
                        {
                            r = palette[idx*3]; g = palette[idx*3+1]; b = palette[idx*3+2];
                            if (transpFlag && idx == transpIndex) a = 0;
                        }
                        int off = (y * visibleW + x) * 4;
                        framePixels[off] = b;
                        framePixels[off + 1] = g;
                        framePixels[off + 2] = r;
                        framePixels[off + 3] = a;
                    }

                    CodecValidation.ValidateCount(frames.Count + 1, CodecValidation.MaxFrameCount, "GIF frame count");
                    frames.Add(new AnimationFrame(new Rectangle(Math.Min(imgLeft, canvasW), Math.Min(imgTop, canvasH), visibleW, visibleH), framePixels)
                    {
                        DelayMilliseconds = delayMilliseconds,
                        DisposeMethod = disposeMethod,
                        BlendMethod = transpFlag ? FrameBlendMethod.Over : FrameBlendMethod.Source
                    });
                    extentRight = Math.Max(extentRight, imgLeft + visibleW);
                    extentBottom = Math.Max(extentBottom, imgTop + visibleH);

                    // GCE state applies to one following image descriptor only.
                    transpFlag = false;
                    transpIndex = 0;
                    delayMilliseconds = 0;
                    disposeMethod = FrameDisposeMethod.None;
                    continue;
                }

                // Unknown block: strict rejects; otherwise whatever was decoded so far is the image.
                if (strict || frames.Count == 0)
                    throw new InvalidDataException($"Unknown GIF block 0x{blockId:X2}.");
                break;
            }

            // A missing trailer (truncated download, minimal encoders) is only an error when strict.
            if (strict && !sawTrailer)
                throw new InvalidDataException("GIF trailer is missing.");
            if (frames.Count == 0)
                throw new InvalidDataException("No image frame found in GIF.");

            int bmpWidth = unsizedCanvas ? extentRight : canvasW;
            int bmpHeight = unsizedCanvas ? extentBottom : canvasH;
            CodecValidation.ValidateDimensions(bmpWidth, bmpHeight, "GIF");
            var result = new Bitmap(bmpWidth, bmpHeight, PixelFormat.Format32bppArgb);
            result._rawFormat = ImageFormat.Gif;
            result.SetAnimationFrames(frames, loopCount);
            result.SelectActiveFrame(FrameDimension.Time, 0);
            return result;
        }

        // ─── Encode ──────────────────────────────────────────────────────────────

        public void Encode(Bitmap bmp, Stream stream, EncoderParameters? parameters)
        {
            if (bmp._width > 65535 || bmp._height > 65535)
                throw new ArgumentException("GIF images cannot be larger than 65535 pixels in either dimension.", nameof(bmp));

            List<AnimationFrame>? frames = bmp.GetAnimationFrames();
            if (frames != null && frames.Count > 1)
            {
                EncodeAnimation(bmp, frames, stream);
                return;
            }

            // Single image: one global colour table, written exactly as before for images that fit in 256 colours.
            byte[] bgra = PixelExport.ToStraightBgra(bmp);
            GifPalette palette = GifQuantizer.Quantize(bgra, bmp._width * bmp._height);

            int gctPow = CodeSizeForColours(palette.ColorCount, out int tableSize);
            int minCodeSize = Math.Max(2, gctPow + 1);
            byte[] lzwData = LzwEncode(palette.Indices, minCodeSize);

            var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

            // Header
            stream.Write(System.Text.Encoding.ASCII.GetBytes("GIF89a"), 0, 6);

            // Logical Screen Descriptor
            w.Write((ushort)bmp._width);
            w.Write((ushort)bmp._height);
            byte lsdPacked = (byte)(0x80 | (gctPow & 7) | ((gctPow & 7) << 4));
            w.Write(lsdPacked);
            w.Write((byte)0); // background index
            w.Write((byte)0); // aspect ratio

            // Global Colour Table (padded to tableSize entries)
            for (int i = 0; i < tableSize; i++)
            {
                if (i < palette.ColorCount)
                { w.Write(palette.Rgb[i * 3]); w.Write(palette.Rgb[i * 3 + 1]); w.Write(palette.Rgb[i * 3 + 2]); }
                else
                { w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); }
            }

            // Graphic Control Extension (only if transparent)
            if (palette.TransparentIndex >= 0)
                WriteGraphicControl(w, 0, 0, palette.TransparentIndex);

            WriteImage(w, stream, 0, 0, bmp._width, bmp._height, null, 0, minCodeSize, lzwData);

            // Trailer
            w.Write((byte)0x3B);
        }

        /// <summary>
        /// Writes every frame as a full-canvas image with its own local colour table, plus a NETSCAPE2.0 loop
        /// extension. Frames are composed on a private canvas, so the bitmap's active frame is left untouched.
        /// </summary>
        private static void EncodeAnimation(Bitmap bmp, List<AnimationFrame> frames, Stream stream)
        {
            var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            stream.Write(System.Text.Encoding.ASCII.GetBytes("GIF89a"), 0, 6);

            w.Write((ushort)bmp._width);
            w.Write((ushort)bmp._height);
            w.Write((byte)0x70);   // no global colour table, 8 bits of colour resolution
            w.Write((byte)0);
            w.Write((byte)0);

            // NETSCAPE2.0 application extension: loop count (0 = forever)
            int loops = Math.Max(0, Math.Min(65535, bmp.GetAnimationLoopCount()));
            w.Write(new byte[] { 0x21, 0xFF, 0x0B });
            w.Write(System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));
            w.Write(new byte[] { 0x03, 0x01, (byte)loops, (byte)(loops >> 8), 0x00 });

            var animator = new FrameAnimator(bmp._width, bmp._height, frames);
            for (int i = 0; i < frames.Count; i++)
            {
                byte[] canvas = animator.Compose(i);
                GifPalette palette = GifQuantizer.Quantize(canvas, bmp._width * bmp._height);
                int tablePow = CodeSizeForColours(palette.ColorCount, out int tableSize);
                int minCodeSize = Math.Max(2, tablePow + 1);

                int centiseconds = Math.Max(0, Math.Min(65535, (frames[i].DelayMilliseconds + 5) / 10));
                WriteGraphicControl(w, centiseconds, 1, palette.TransparentIndex);

                var localTable = new byte[tableSize * 3];
                Buffer.BlockCopy(palette.Rgb, 0, localTable, 0, palette.ColorCount * 3);
                WriteImage(w, stream, 0, 0, bmp._width, bmp._height, localTable, tablePow, minCodeSize, LzwEncode(palette.Indices, minCodeSize));
            }

            w.Write((byte)0x3B);
        }

        /// <summary>Smallest n such that 2^(n+1) entries hold <paramref name="colours"/> (capped at 7), and that table size.</summary>
        private static int CodeSizeForColours(int colours, out int tableSize)
        {
            int pow = 0;
            tableSize = 2;
            while (tableSize < colours && pow < 7)
            {
                pow++;
                tableSize *= 2;
            }
            return pow;
        }

        private static void WriteGraphicControl(BinaryWriter w, int centiseconds, int disposal, int transparentIndex)
        {
            w.Write((byte)0x21); w.Write((byte)0xF9); w.Write((byte)4);
            w.Write((byte)((disposal << 2) | (transparentIndex >= 0 ? 1 : 0)));
            w.Write((ushort)centiseconds);
            w.Write((byte)(transparentIndex >= 0 ? transparentIndex : 0));
            w.Write((byte)0); // block terminator
        }

        private static void WriteImage(
            BinaryWriter w, Stream stream, int left, int top, int width, int height,
            byte[]? localTable, int localTablePow, int minCodeSize, byte[] lzwData)
        {
            w.Write((byte)0x2C);
            w.Write((ushort)left); w.Write((ushort)top);
            w.Write((ushort)width);
            w.Write((ushort)height);
            w.Write((byte)(localTable != null ? 0x80 | (localTablePow & 7) : 0));   // local table flag, not interlaced
            if (localTable != null)
                w.Write(localTable);
            w.Write((byte)minCodeSize);
            WriteSubBlocks(stream, lzwData);
        }

        // ─── Private helpers ─────────────────────────────────────────────────────

        private static void SkipSubBlocks(byte[] data, ref int pos)
        {
            while (true)
            {
                if (pos >= data.Length)
                    throw new InvalidDataException("GIF sub-block sequence is truncated.");
                int sz = data[pos++];
                if (sz == 0) return;
                EnsureRange(data, pos, sz, "GIF sub-block");
                pos += sz;
            }
        }

        private static byte[] ReadSubBlocks(byte[] data, ref int pos)
        {
            var result = new List<byte>();
            while (true)
            {
                if (pos >= data.Length)
                    throw new InvalidDataException("GIF image data sub-blocks are truncated.");
                int sz = data[pos++];
                if (sz == 0) return result.ToArray();
                EnsureRange(data, pos, sz, "GIF image data sub-block");
                for (int i = 0; i < sz; i++)
                    result.Add(data[pos++]);
            }
        }

        private static void WriteSubBlocks(Stream s, byte[] data)
        {
            int i = 0;
            while (i < data.Length)
            {
                int sz = Math.Min(255, data.Length - i);
                s.WriteByte((byte)sz);
                s.Write(data, i, sz);
                i += sz;
            }
            s.WriteByte(0); // block terminator
        }

        // ── GIF LZW decoder (LSB-first bit stream) ────────────────────────────

        /// <summary>
        /// Decodes the LZW stream of one frame into palette indices.
        /// Strict mode requires a clear code, an end-of-information code and exactly
        /// <paramref name="expectedLength"/> indices; otherwise a stream that ends early or runs long still
        /// yields the pixels it contains, and the missing ones are returned as -1 (transparent).
        /// </summary>
        private static int[] LzwDecode(byte[] data, int minCodeSize, int expectedLength, bool strict)
        {
            if (minCodeSize < 2 || minCodeSize > 8)
                throw new InvalidDataException("GIF LZW minimum code size is invalid.");
            if (expectedLength <= 0)
                throw new InvalidDataException("GIF image dimensions produce no pixels.");

            int clearCode = 1 << minCodeSize;
            int eoiCode   = clearCode + 1;
            int codeSize  = minCodeSize + 1;
            int codeMask  = (1 << codeSize) - 1;

            // String table: code → list of indices
            var table = new List<int[]>();

            void InitTable()
            {
                table.Clear();
                for (int i = 0; i <= eoiCode; i++)
                    table.Add(i < clearCode ? new[] { i } : Array.Empty<int>());
                codeSize = minCodeSize + 1;
                codeMask = (1 << codeSize) - 1;
            }

            InitTable();

            var output = new int[expectedLength];
            int outputCount = 0;
            int bitBuf  = 0, bitsLeft = 0, dataPos = 0;

            // Returns -1 when the stream ends before a complete code (reported by the caller).
            int ReadCode()
            {
                while (bitsLeft < codeSize && dataPos < data.Length)
                {
                    bitBuf   |= data[dataPos++] << bitsLeft;
                    bitsLeft += 8;
                }
                if (bitsLeft < codeSize)
                    return -1;
                int code  = bitBuf & codeMask;
                bitBuf  >>= codeSize;
                bitsLeft -= codeSize;
                return code;
            }

            int prevCode = -1;
            // Some encoders omit the leading clear code; the initial table is then the right one.
            bool sawClear = !strict;
            bool sawEnd = false;
            int iterations = 0;
            int maximumIterations = checked(Math.Max(64, expectedLength * 2 + data.Length * 16));

            while (iterations++ < maximumIterations)
            {
                int code = ReadCode();
                if (code < 0)
                {
                    if (strict)
                        throw new InvalidDataException("GIF LZW data ended before a complete code was read.");
                    break;
                }

                if (code == eoiCode)
                {
                    sawEnd = true;
                    break;
                }

                if (code == clearCode)
                {
                    InitTable();
                    prevCode = -1;
                    sawClear = true;
                    continue;
                }
                if (!sawClear)
                    throw new InvalidDataException("GIF LZW stream is missing its clear code.");

                int[] entry;
                if (code < table.Count && table[code].Length > 0)
                {
                    entry = table[code];
                }
                else if (code == table.Count && prevCode >= 0 && table.Count < 4096)
                {
                    // Special case: code == next available, entry = prev + prev[0]
                    var prev2 = table[prevCode];
                    entry = new int[prev2.Length + 1];
                    Buffer.BlockCopy(prev2, 0, entry, 0, prev2.Length * sizeof(int));
                    entry[prev2.Length] = prev2[0];
                }
                else
                {
                    throw new InvalidDataException("GIF LZW code is outside the active dictionary.");
                }

                if (entry.Length > expectedLength - outputCount)
                {
                    if (strict)
                        throw new InvalidDataException("GIF LZW output exceeds the image dimensions.");
                    // More data than the frame holds: keep what fits (as browsers do) and stop.
                    Array.Copy(entry, 0, output, outputCount, expectedLength - outputCount);
                    outputCount = expectedLength;
                    break;
                }
                Array.Copy(entry, 0, output, outputCount, entry.Length);
                outputCount += entry.Length;

                // Once the dictionary is full (4096 entries) no further entries are added and decoding
                // continues with 12-bit codes: GIF allows an encoder to defer the clear code.
                if (prevCode >= 0 && table.Count < 4096)
                {
                    var prevEntry = table[prevCode];
                    var newEntry  = new int[prevEntry.Length + 1];
                    Buffer.BlockCopy(prevEntry, 0, newEntry, 0, prevEntry.Length * sizeof(int));
                    newEntry[prevEntry.Length] = entry[0];
                    table.Add(newEntry);

                    if (table.Count == (1 << codeSize) && codeSize < 12)
                    {
                        codeSize++;
                        codeMask = (1 << codeSize) - 1;
                    }
                }

                prevCode = code;
            }

            if (iterations >= maximumIterations)
                throw new InvalidDataException("GIF LZW decoder exceeded its progress limit.");
            if (strict && (!sawEnd || outputCount != expectedLength))
                throw new InvalidDataException("GIF LZW stream did not produce exactly one image frame.");
            for (int i = outputCount; i < expectedLength; i++)
                output[i] = -1;
            if (outputCount == 0)
                throw new InvalidDataException("GIF LZW stream contains no image data.");
            return output;
        }

        /// <summary>
        /// Reads an application extension. A NETSCAPE2.0 / ANIMEXTS1.0 block carries the animation loop
        /// count (0 = loop forever); every other application block is skipped.
        /// </summary>
        private static void ReadApplicationExtension(byte[] data, ref int pos, ref int loopCount)
        {
            if (pos >= data.Length)
                throw new InvalidDataException("GIF application extension is truncated.");
            int firstSize = data[pos++];
            EnsureRange(data, pos, firstSize, "GIF application extension identifier");
            bool isLoopBlock = firstSize == 11
                && (System.Text.Encoding.ASCII.GetString(data, pos, 11) == "NETSCAPE2.0"
                    || System.Text.Encoding.ASCII.GetString(data, pos, 11) == "ANIMEXTS1.0");
            pos += firstSize;

            while (true)
            {
                if (pos >= data.Length)
                    throw new InvalidDataException("GIF sub-block sequence is truncated.");
                int sz = data[pos++];
                if (sz == 0)
                    return;
                EnsureRange(data, pos, sz, "GIF sub-block");
                if (isLoopBlock && sz >= 3 && data[pos] == 1)
                    loopCount = data[pos + 1] | (data[pos + 2] << 8);
                pos += sz;
            }
        }

        /// <summary>
        /// For an interlaced image of <paramref name="height"/> rows, the stored-row index of every display row:
        /// pass 1 holds rows 0, 8, 16, ...; pass 2 rows 4, 12, ...; pass 3 rows 2, 6, ...; pass 4 rows 1, 3, 5, ...
        /// </summary>
        internal static int[] InterlacedRowMap(int height)
        {
            var map = new int[height];
            int stored = 0;
            int[] starts = { 0, 4, 2, 1 };
            int[] steps = { 8, 8, 4, 2 };
            for (int pass = 0; pass < 4; pass++)
            {
                for (int row = starts[pass]; row < height; row += steps[pass])
                    map[row] = stored++;
            }
            return map;
        }

        private static void EnsureRange(byte[] data, int offset, int length, string context)
        {
            if (offset < 0 || length < 0 || offset > data.Length - length)
                throw new InvalidDataException($"{context} exceeds the GIF input.");
        }

        // ── GIF LZW encoder (LSB-first bit stream) ────────────────────────────

        private static byte[] LzwEncode(byte[] indices, int minCodeSize)
        {
            int clearCode = 1 << minCodeSize;
            int eoiCode   = clearCode + 1;

            var  output    = new List<byte>();
            int  bitBuf    = 0, bitsLeft = 0;
            int  codeSize  = minCodeSize + 1;
            int  nextCode  = eoiCode + 1;
            var  dict      = new Dictionary<(int prefix, byte suffix), int>();

            void Flush()
            {
                while (bitsLeft >= 8) { output.Add((byte)(bitBuf & 0xFF)); bitBuf >>= 8; bitsLeft -= 8; }
            }

            void EmitCode(int code)
            {
                bitBuf   |= code << bitsLeft;
                bitsLeft += codeSize;
                Flush();
            }

            void Reset()
            {
                dict.Clear();
                nextCode  = eoiCode + 1;
                codeSize  = minCodeSize + 1;
            }

            EmitCode(clearCode);

            if (indices.Length == 0)
            {
                EmitCode(eoiCode);
                if (bitsLeft > 0) { output.Add((byte)(bitBuf & 0xFF)); }
                return output.ToArray();
            }

            int cur = indices[0];

            for (int i = 1; i < indices.Length; i++)
            {
                byte b = indices[i];
                if (dict.TryGetValue((cur, b), out int next))
                {
                    cur = next;
                }
                else
                {
                    EmitCode(cur);
                    if (nextCode < 4096)
                    {
                        dict[(cur, b)] = nextCode++;
                        if (nextCode > (1 << codeSize) && codeSize < 12)
                            codeSize++;
                    }
                    else
                    {
                        EmitCode(clearCode);
                        Reset();
                    }
                    cur = b;
                }
            }

            EmitCode(cur);

            // A decoder adds a dictionary entry when it reads the final data code and widens its codes
            // when that entry makes the table reach 2^codeSize. The encoder widens one entry later (it runs
            // one ahead and adds nothing for the final code), so without this the end-of-information code
            // would be written one bit narrower than the decoder reads it exactly at that boundary.
            if (nextCode == (1 << codeSize) && codeSize < 12)
                codeSize++;
            EmitCode(eoiCode);
            if (bitsLeft > 0) { output.Add((byte)(bitBuf & 0xFF)); }

            return output.ToArray();
        }
    }
}
