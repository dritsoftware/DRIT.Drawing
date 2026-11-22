using System;
using System.IO;

namespace DRIT.Drawing.Codecs.Jpeg
{
    /// <summary>
    /// JPEG Huffman table built from a DHT segment.
    /// Uses a 512-entry (9-bit lookahead) fast-decode table for codes ≤ 9 bits;
    /// falls back to a linear search for longer codes.
    /// </summary>
    internal sealed class HuffmanTable
    {
        // Fast-path: 512 entries, indexed by top 9 bits of the bit-stream.
        // Entry format: bits[11:8] = code length (0 = slow path), bits[7:0] = symbol.
        private readonly int[] _fast = new int[512];

        // Slow-path data (codes > 9 bits)
        private readonly int[]  _maxCode  = new int[17];   // max code at each length
        private readonly int[]  _minCode  = new int[17];
        private readonly int[]  _valPtr   = new int[17];   // index into _huffVal
        private readonly byte[] _huffVal;                   // all symbol values in order

        /// <summary>Table class: 0 = DC, 1 = AC.</summary>
        public int Class { get; private set; }

        /// <summary>Table destination id (0–3).</summary>
        public int Id { get; private set; }

        // ── Factory ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses one or more Huffman tables from a DHT segment payload.
        /// Returns all tables found.
        /// </summary>
        /// <param name="data">The DHT segment payload.</param>
        /// <param name="allowEmpty">
        /// When true a table that defines no codes is accepted (libjpeg does too) and only fails if a scan
        /// actually decodes a symbol with it; strict conformance rejects it at definition time.
        /// </param>
        public static HuffmanTable[] ParseDht(byte[] data, bool allowEmpty = false)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var tables = new System.Collections.Generic.List<HuffmanTable>(4);
            int pos = 0;
            while (pos < data.Length)
            {
                if (data.Length - pos < 17)
                    throw new InvalidDataException("DHT segment is truncated.");
                byte tcTh = data[pos++];
                int tc = (tcTh >> 4) & 0xF;   // table class (0=DC,1=AC)
                int th =  tcTh       & 0xF;   // table destination id
                if (tc > 1 || th > 3)
                    throw new InvalidDataException("DHT table class or identifier is invalid.");

                // BITS[1..16] — count of codes of each length
                int[] bits = new int[17];
                int totalSymbols = 0;
                for (int i = 1; i <= 16; i++)
                {
                    bits[i] = data[pos++];
                    totalSymbols += bits[i];
                }
                if ((totalSymbols == 0 && !allowEmpty) || totalSymbols > 256 || data.Length - pos < totalSymbols)
                    throw new InvalidDataException("DHT symbol count is invalid or truncated.");

                int code = 0;
                for (int length = 1; length <= 16; length++)
                {
                    code = (code + bits[length]) << 1;
                    if (code > (1 << (length + 1)))
                        throw new InvalidDataException("DHT code lengths overflow the canonical Huffman code space.");
                }

                // HUFFVAL — all symbol bytes in code-length order
                byte[] huffVal = new byte[totalSymbols];
                for (int i = 0; i < totalSymbols; i++)
                    huffVal[i] = data[pos++];

                tables.Add(Build(tc, th, bits, huffVal, allowEmpty));
            }
            return tables.ToArray();
        }

        internal static HuffmanTable Create(int tc, int th, byte[] counts, byte[] values)
        {
            if (counts == null) throw new ArgumentNullException(nameof(counts));
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (counts.Length != 16) throw new ArgumentException("Huffman counts must contain 16 entries.", nameof(counts));

            int[] bits = new int[17];
            for (int i = 0; i < counts.Length; i++)
                bits[i + 1] = counts[i];

            return Build(tc, th, bits, values);
        }

        /// <summary>Decode the next symbol from <paramref name="reader"/>.</summary>
        public int Decode(BitReader reader)
        {
            if (_huffVal.Length == 0)
                throw new InvalidDataException("JPEG scan uses a Huffman table that defines no codes.");

            // Look at the next 16 bits (prefetched, so this works for most symbols). Only bits that really
            // exist in the entropy data may be used: near the end the window is zero-padded and `valid`
            // tells how much of it is data, so no padding bytes are ever invented.
            int window = reader.Peek16(out int valid);

            int entry = _fast[window >> 7];
            int len   = entry >> 8;
            if (len > 0 && len <= valid)
            {
                reader.ConsumeBits(len);
                return entry & 0xFF;
            }

            // Slow path: canonical decode over the window, lengths 1..16
            for (int l = 1; l <= 16; l++)
            {
                if (l > valid)
                    throw new EndOfStreamException("Unexpected end of JPEG entropy data.");
                int code = window >> (16 - l);
                if (code <= _maxCode[l] && _maxCode[l] != -1)
                {
                    reader.ConsumeBits(l);
                    return _huffVal[_valPtr[l] + (code - _minCode[l])];
                }
            }
            throw new InvalidDataException("Invalid Huffman code in JPEG stream.");
        }

        // ── Construction ─────────────────────────────────────────────────────────

        private static HuffmanTable Build(int tc, int th, int[] bits, byte[] huffVal, bool allowEmpty = false)
        {
            if (tc < 0 || tc > 1 || th < 0 || th > 3)
                throw new InvalidDataException("Invalid JPEG Huffman table identifier.");
            int symbolCount = 0;
            for (int length = 1; length <= 16; length++)
                symbolCount += bits[length];
            if (symbolCount != huffVal.Length || (symbolCount == 0 && !allowEmpty) || symbolCount > 256)
                throw new InvalidDataException("Invalid JPEG Huffman symbol count.");

            var t = new HuffmanTable(huffVal) { Class = tc, Id = th };

            // Build canonical codes and min/max/valPtr tables
            int code   = 0;
            int valIdx = 0;
            for (int l = 1; l <= 16; l++)
            {
                if (bits[l] > 0)
                {
                    t._minCode[l] = code;
                    t._valPtr[l]  = valIdx;
                    code   += bits[l];
                    valIdx += bits[l];
                    if (code > (1 << l))
                        throw new InvalidDataException("Invalid JPEG Huffman code lengths.");
                    t._maxCode[l] = code - 1;
                }
                else
                {
                    t._maxCode[l] = -1; // no codes of this length
                }
                code <<= 1;
            }

            // Fill fast 9-bit lookup table
            code   = 0;
            valIdx = 0;
            for (int l = 1; l <= 9; l++)
            {
                for (int i = 0; i < bits[l]; i++)
                {
                    // Every 9-bit prefix that starts with this code
                    int baseIdx = code << (9 - l);
                    int count   = 1 << (9 - l);
                    int entry   = (l << 8) | huffVal[valIdx];
                    for (int k = 0; k < count; k++)
                        t._fast[baseIdx + k] = entry;
                    code++;
                    valIdx++;
                }
                code <<= 1;
            }
            return t;
        }

        private HuffmanTable(byte[] huffVal) => _huffVal = huffVal;
    }
}
