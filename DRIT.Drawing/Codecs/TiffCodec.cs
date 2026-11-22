using System;
using System.Collections.Generic;
using System.IO;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Codecs
{
    /// <summary>
    /// TIFF codec. Decodes the first image of a classic TIFF (either byte order) that is uncompressed, contiguous
    /// 8-bit RGB or RGBA stored in strips; every other layout is reported with <see cref="NotSupportedException"/>
    /// instead of being misread. Encodes uncompressed little-endian RGB / RGBA.
    /// </summary>
    internal sealed class TiffCodec : IImageCodec
    {
        private const int MaxIfdEntries = 4096;
        private const int MaxStrips = 1 << 20;

        public bool CanDecode(byte[] header)
            => header.Length >= 4
            && ((header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A && header[3] == 0x00) // LE
             || (header[0] == 0x4D && header[1] == 0x4D && header[2] == 0x00 && header[3] == 0x2A)); // BE

        // One parsed IFD entry; the value field itself is interpreted lazily by ReadValues.
        private readonly struct Entry
        {
            public Entry(int type, long count, int valueFieldOffset)
            {
                Type = type;
                Count = count;
                ValueFieldOffset = valueFieldOffset;
            }

            public int Type { get; }
            public long Count { get; }
            public int ValueFieldOffset { get; }
        }

        // ─── Decode ──────────────────────────────────────────────────────────────

        public Bitmap Decode(Stream stream)
        {
            byte[] file = CodecValidation.ReadAll(stream, "TIFF");
            if (file.Length < 8)
                throw new InvalidDataException("TIFF header is truncated.");
            bool isLE = file[0] == 0x49;

            uint ifdOff = ReadU32(file, 4, isLE);
            CodecValidation.ValidateRange(ifdOff, 2, file.Length, "TIFF IFD");
            int ifdCount = (int)ReadU16(file, (int)ifdOff, isLE);
            CodecValidation.ValidateCount(ifdCount, MaxIfdEntries, "TIFF IFD entry count");

            int entryBase = (int)ifdOff + 2;
            CodecValidation.ValidateRange(entryBase, ifdCount * 12L, file.Length, "TIFF IFD entries");
            var entries = new Dictionary<int, Entry>(ifdCount);
            for (int i = 0; i < ifdCount; i++)
            {
                int e = entryBase + i * 12;
                entries[(int)ReadU16(file, e, isLE)] = new Entry((int)ReadU16(file, e + 2, isLE), ReadU32(file, e + 4, isLE), e + 8);
            }

            // Capabilities first: a file this decoder cannot interpret is refused, never guessed at.
            long compression = ReadSingle(file, entries, 259, isLE, 1);
            if (compression != 1)
                throw new NotSupportedException($"TIFF compression {compression} is not supported (only uncompressed data).");
            if (entries.ContainsKey(322) || entries.ContainsKey(323) || entries.ContainsKey(324) || entries.ContainsKey(325))
                throw new NotSupportedException("Tiled TIFF images are not supported.");
            if (!entries.ContainsKey(262))
                throw new InvalidDataException("TIFF PhotometricInterpretation is missing.");
            long photometric = ReadSingle(file, entries, 262, isLE, 2);
            if (photometric != 2)
                throw new NotSupportedException($"TIFF photometric interpretation {photometric} is not supported (only RGB).");
            long planar = ReadSingle(file, entries, 284, isLE, 1);
            if (planar != 1)
                throw new NotSupportedException("Planar (separate colour plane) TIFF images are not supported.");

            long samplesPerPixel = ReadSingle(file, entries, 277, isLE, 1);
            if (samplesPerPixel != 3 && samplesPerPixel != 4)
                throw new NotSupportedException($"TIFF with {samplesPerPixel} samples per pixel is not supported (RGB or RGBA only).");
            int spp = (int)samplesPerPixel;

            long[] bits = ReadValues(file, entries, 258, isLE, defaultValue: 1);
            foreach (long b in bits)
            {
                if (b != 8)
                    throw new NotSupportedException($"TIFF with {b} bits per sample is not supported (8 only).");
            }
            if (bits.Length != 1 && bits.Length != spp)
                throw new InvalidDataException("TIFF BitsPerSample count does not match SamplesPerPixel.");

            foreach (long format in ReadValues(file, entries, 339, isLE, defaultValue: 1))
            {
                if (format != 1)
                    throw new NotSupportedException("TIFF sample formats other than unsigned integer are not supported.");
            }

            long width = RequireSingle(file, entries, 256, isLE, "ImageWidth");
            long height = RequireSingle(file, entries, 257, isLE, "ImageLength");
            if (width > int.MaxValue || height > int.MaxValue)
                throw new InvalidDataException("TIFF dimensions exceed the supported range.");
            int w = (int)width, h = (int)height;
            CodecValidation.ValidateDimensions(w, h, "TIFF");
            int needed = CodecValidation.CheckedBufferLength((long)w * h * spp, "TIFF pixel data");
            CodecValidation.CheckedBufferLength((long)w * h * 4, "TIFF decoded pixels");

            // Alpha: ExtraSamples 2 = unassociated (straight), 1 = associated (premultiplied), 0/absent = unspecified.
            long extra = 0;
            if (spp == 4)
            {
                long[] extras = ReadValues(file, entries, 338, isLE, defaultValue: 0);
                extra = extras.Length > 0 ? extras[0] : 0;
            }

            byte[] pixels = ReadStrips(file, entries, isLE, needed);

            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            bmp._rawFormat = ImageFormat.Tiff;
            ReadResolution(file, entries, isLE, bmp);

            for (int y = 0; y < h; y++)
            {
                int src = y * w * spp;
                int dst = y * bmp._stride;
                for (int x = 0; x < w; x++, src += spp, dst += 4)
                {
                    int r = pixels[src], g = pixels[src + 1], bl = pixels[src + 2];
                    int a = 255;
                    if (spp == 4 && (extra == 1 || extra == 2))
                    {
                        a = pixels[src + 3];
                        if (extra == 1)
                        {
                            // Associated alpha is premultiplied; the bitmap stores straight alpha.
                            if (a == 0) { r = g = bl = 0; }
                            else
                            {
                                r = Math.Min(255, (r * 255 + a / 2) / a);
                                g = Math.Min(255, (g * 255 + a / 2) / a);
                                bl = Math.Min(255, (bl * 255 + a / 2) / a);
                            }
                        }
                    }

                    bmp._pixels[dst] = (byte)bl;
                    bmp._pixels[dst + 1] = (byte)g;
                    bmp._pixels[dst + 2] = (byte)r;
                    bmp._pixels[dst + 3] = (byte)a;
                }
            }

            return bmp;
        }

        /// <summary>Concatenates the strips (validated one by one) into <paramref name="needed"/> bytes of pixel data.</summary>
        private static byte[] ReadStrips(byte[] file, Dictionary<int, Entry> entries, bool isLE, int needed)
        {
            if (!entries.ContainsKey(273) || !entries.ContainsKey(279))
                throw new InvalidDataException("TIFF StripOffsets or StripByteCounts is missing.");
            long[] offsets = ReadValues(file, entries, 273, isLE, defaultValue: 0, maxCount: MaxStrips);
            long[] counts = ReadValues(file, entries, 279, isLE, defaultValue: 0, maxCount: MaxStrips);
            if (offsets.Length == 0 || offsets.Length != counts.Length)
                throw new InvalidDataException("TIFF strip offsets and byte counts do not match.");

            var pixels = new byte[needed];
            int copied = 0;
            for (int i = 0; i < offsets.Length && copied < needed; i++)
            {
                CodecValidation.ValidateRange(offsets[i], counts[i], file.Length, "TIFF strip");
                int take = (int)Math.Min(counts[i], needed - copied);
                Buffer.BlockCopy(file, (int)offsets[i], pixels, copied, take);
                copied += take;
            }

            if (copied < needed)
                throw new InvalidDataException("TIFF strips hold fewer bytes than the image needs.");
            return pixels;
        }

        private static void ReadResolution(byte[] file, Dictionary<int, Entry> entries, bool isLE, Bitmap bmp)
        {
            if (!entries.TryGetValue(282, out Entry xEntry) || !entries.TryGetValue(283, out Entry yEntry))
                return;
            long unit = ReadSingle(file, entries, 296, isLE, 2);
            if (unit != 2 && unit != 3)
                return;
            double scale = unit == 3 ? 2.54 : 1.0;
            double x = ReadRational(file, xEntry, isLE), y = ReadRational(file, yEntry, isLE);
            if (x > 0 && y > 0 && x < 1e6 && y < 1e6)
            {
                bmp._dpiX = (float)(x * scale);
                bmp._dpiY = (float)(y * scale);
            }
        }

        // ── Tag access with bounds checks ────────────────────────────────────────

        private static int ElementSize(int type)
        {
            switch (type)
            {
                case 1: return 1;   // BYTE
                case 3: return 2;   // SHORT
                case 4: return 4;   // LONG
                default: throw new NotSupportedException($"TIFF field type {type} is not supported for this tag.");
            }
        }

        private static long[] ReadValues(byte[] file, Dictionary<int, Entry> entries, int tag, bool isLE, long defaultValue, int maxCount = 1024)
        {
            if (!entries.TryGetValue(tag, out Entry entry))
                return new[] { defaultValue };
            int size = ElementSize(entry.Type);
            CodecValidation.ValidateCount(entry.Count > int.MaxValue ? int.MaxValue : (int)entry.Count, maxCount, $"TIFF tag {tag} value count");
            long total = entry.Count * size;
            int start = entry.ValueFieldOffset;
            if (total > 4)
            {
                long valueOffset = ReadU32(file, entry.ValueFieldOffset, isLE);
                CodecValidation.ValidateRange(valueOffset, total, file.Length, $"TIFF tag {tag} values");
                start = (int)valueOffset;
            }

            var values = new long[entry.Count];
            for (int i = 0; i < values.Length; i++)
            {
                int o = start + i * size;
                values[i] = size == 1 ? file[o] : size == 2 ? ReadU16(file, o, isLE) : ReadU32(file, o, isLE);
            }
            return values;
        }

        private static long ReadSingle(byte[] file, Dictionary<int, Entry> entries, int tag, bool isLE, long defaultValue)
        {
            long[] values = ReadValues(file, entries, tag, isLE, defaultValue);
            return values.Length > 0 ? values[0] : defaultValue;
        }

        private static long RequireSingle(byte[] file, Dictionary<int, Entry> entries, int tag, bool isLE, string name)
        {
            if (!entries.ContainsKey(tag))
                throw new InvalidDataException($"TIFF {name} is missing.");
            return ReadSingle(file, entries, tag, isLE, 0);
        }

        private static double ReadRational(byte[] file, Entry entry, bool isLE)
        {
            if (entry.Type != 5 || entry.Count < 1)
                return 0;
            long offset = ReadU32(file, entry.ValueFieldOffset, isLE);
            CodecValidation.ValidateRange(offset, 8, file.Length, "TIFF rational value");
            uint numerator = ReadU32(file, (int)offset, isLE), denominator = ReadU32(file, (int)offset + 4, isLE);
            return denominator == 0 ? 0 : numerator / (double)denominator;
        }

        // ─── Encode ──────────────────────────────────────────────────────────────
        //
        // Layout (LE, uncompressed):
        //   [8 header] [IFD: 2+12*n+4] [BitsPerSample array] [XRes RATIONAL] [YRes RATIONAL] [pixels]
        //
        public void Encode(Bitmap bmp, Stream stream, EncoderParameters? parameters)
        {
            bool hasAlpha = PixelExport.HasAlpha(bmp);
            int spp       = hasAlpha ? 4 : 3;
            int w         = bmp._width, h = bmp._height;
            long pixelSizeLong = (long)w * h * spp;
            if (pixelSizeLong > int.MaxValue - 4096)
                throw new ArgumentException("The bitmap is too large for an uncompressed TIFF.", nameof(bmp));
            int pixelSize = (int)pixelSizeLong;
            int numTags   = hasAlpha ? 13 : 12;

            // Offsets (all relative to file start)
            // header=8, IFD=8, ifd size = 2 + 12*numTags + 4
            int ifdOff    = 8;
            int ifdSize   = 2 + 12 * numTags + 4;
            int bpsOff    = ifdOff + ifdSize;          // BitsPerSample SHORT[spp]
            int bpsSize   = spp * 2;
            int xResOff   = bpsOff + bpsSize;          // XResolution RATIONAL
            int yResOff   = xResOff + 8;               // YResolution RATIONAL
            int pixelOff  = yResOff + 8;

            // Build IFD entries (sorted by tag)
            var tags = new List<(ushort tag, ushort type, uint count, uint val)>();

            void AddTag(ushort tag, ushort type, uint count, uint val)
                => tags.Add((tag, type, count, val));

            const ushort SHORT  = 3;
            const ushort LONG   = 4;
            const ushort RATIONAL = 5;

            AddTag(256, LONG,     1,          (uint)w);
            AddTag(257, LONG,     1,          (uint)h);
            AddTag(258, SHORT,    (uint)spp,  (uint)bpsOff); // offset to BPS array
            AddTag(259, SHORT,    1,          1);             // Compression = none
            AddTag(262, SHORT,    1,          2);             // RGB
            AddTag(273, LONG,     1,          (uint)pixelOff);
            AddTag(277, SHORT,    1,          (uint)spp);
            AddTag(278, LONG,     1,          (uint)h);
            AddTag(279, LONG,     1,          (uint)pixelSize);
            AddTag(282, RATIONAL, 1,          (uint)xResOff);
            AddTag(283, RATIONAL, 1,          (uint)yResOff);
            AddTag(296, SHORT,    1,          2);             // ResolutionUnit = inch
            if (hasAlpha)
                AddTag(338, SHORT, 1,         2);             // ExtraSamples = 2: unassociated (straight) alpha

            byte[] bgra = PixelExport.ToStraightBgra(bmp);

            // ── Write ────────────────────────────────────────────────────────────
            using var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);

            // Header
            bw.Write((byte)'I'); bw.Write((byte)'I'); // little-endian
            bw.Write((ushort)42);
            bw.Write((uint)ifdOff);

            // IFD
            bw.Write((ushort)numTags);
            foreach (var (tag, type, count, val) in tags)
            {
                bw.Write(tag); bw.Write(type); bw.Write(count); bw.Write(val);
            }
            bw.Write((uint)0); // next IFD offset = 0

            // BitsPerSample array (8,8,8 or 8,8,8,8)
            for (int i = 0; i < spp; i++) bw.Write((ushort)8);

            // XResolution and YResolution RATIONALs
            uint dpiN = (uint)Math.Round(bmp._dpiX);
            bw.Write(dpiN); bw.Write((uint)1);
            uint dpiNy = (uint)Math.Round(bmp._dpiY);
            bw.Write(dpiNy); bw.Write((uint)1);

            // Pixel data: top-down, RGB or RGBA (not BGRA), one row per write
            var row = new byte[w * spp];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int src = (y * w + x) * 4;
                    int dst = x * spp;
                    row[dst] = bgra[src + 2];
                    row[dst + 1] = bgra[src + 1];
                    row[dst + 2] = bgra[src];
                    if (hasAlpha) row[dst + 3] = bgra[src + 3];
                }
                bw.Write(row, 0, row.Length);
            }
        }

        // ─── Private helpers ─────────────────────────────────────────────────────

        private static uint ReadU32(byte[] d, int off, bool isLE)
        {
            if (off < 0 || off > d.Length - 4)
                throw new InvalidDataException("TIFF value lies outside the file.");
            return isLE
             ? (uint)(d[off] | (d[off+1]<<8) | (d[off+2]<<16) | (d[off+3]<<24))
             : (uint)((d[off]<<24) | (d[off+1]<<16) | (d[off+2]<<8) | d[off+3]);
        }

        private static uint ReadU16(byte[] d, int off, bool isLE)
        {
            if (off < 0 || off > d.Length - 2)
                throw new InvalidDataException("TIFF value lies outside the file.");
            return isLE ? (uint)(d[off] | (d[off+1] << 8))
                        : (uint)((d[off] << 8) | d[off+1]);
        }
    }
}
