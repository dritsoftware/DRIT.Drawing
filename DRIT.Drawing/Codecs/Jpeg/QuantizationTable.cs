using System;
using System.IO;

namespace DRIT.Drawing.Codecs.Jpeg
{
    /// <summary>
    /// Holds one JPEG quantization table (64 coefficients in natural 8×8 row-major order).
    /// Parsed from a DQT segment; input zigzag order is converted to natural order on load.
    /// </summary>
    internal sealed class QuantizationTable
    {
        // ── Zigzag → natural-order mapping (JPEG ISO 10918-1 Figure A.6) ─────────
        internal static readonly int[] ZigZagOrder = {
             0,  1,  8, 16,  9,  2,  3, 10,
            17, 24, 32, 25, 18, 11,  4,  5,
            12, 19, 26, 33, 40, 48, 41, 34,
            27, 20, 13,  6,  7, 14, 21, 28,
            35, 42, 49, 56, 57, 50, 43, 36,
            29, 22, 15, 23, 30, 37, 44, 51,
            58, 59, 52, 45, 38, 31, 39, 46,
            53, 60, 61, 54, 47, 55, 62, 63
        };

        private readonly int[] _table = new int[64]; // natural row-major order

        /// <summary>Table destination identifier (0–3).</summary>
        public int Id { get; private set; }

        /// <summary>Element at natural-order index <paramref name="naturalIndex"/> (0–63).</summary>
        public int this[int naturalIndex] => _table[naturalIndex];

        /// <summary>Raw coefficients in natural row-major order (64 elements).</summary>
        public int[] Coefficients => _table;

        internal bool HasZeroCoefficient
        {
            get
            {
                for (int i = 0; i < _table.Length; i++)
                {
                    if (_table[i] == 0)
                        return true;
                }
                return false;
            }
        }

        // ── Factory ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses one or more quantization tables from a DQT segment payload.
        /// Returns all tables found (typically 1 or 2 per segment).
        /// </summary>
        public static QuantizationTable[] ParseDqt(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var tables = new System.Collections.Generic.List<QuantizationTable>(4);
            int pos = 0;
            while (pos < data.Length)
            {
                if (data.Length - pos < 1)
                    throw new InvalidDataException("DQT segment is truncated.");
                byte pqTq = data[pos++];
                int precision = (pqTq >> 4) & 0xF;  // 0 = 8-bit, 1 = 16-bit
                int id        =  pqTq       & 0xF;
                if (precision > 1 || id > 3)
                    throw new InvalidDataException("DQT precision or table identifier is invalid.");
                int bytesPerCoefficient = precision == 0 ? 1 : 2;
                int required = checked(64 * bytesPerCoefficient);
                if (data.Length - pos < required)
                    throw new InvalidDataException("DQT table data is truncated.");

                var qt = new QuantizationTable { Id = id };
                for (int z = 0; z < 64; z++)
                {
                    int value = precision == 0
                        ? data[pos++]
                        : (data[pos++] << 8) | data[pos++];
                    qt._table[ZigZagOrder[z]] = value;
                }
                tables.Add(qt);
            }
            return tables.ToArray();
        }
    }
}
