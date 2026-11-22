using System;
using System.IO;
using System.IO.Compression;

namespace DRIT.Drawing.Codecs
{
    // ─────────────────────────────────────────────────────────────────────────
    // CRC-32 (ISO 3309 / PNG use)
    // ─────────────────────────────────────────────────────────────────────────
    internal static class Crc32
    {
        private static readonly uint[] _table = BuildTable();

        private static uint[] BuildTable()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : (c >> 1);
                t[n] = c;
            }
            return t;
        }

        public static uint Compute(byte[] buf, int offset, int length)
            => Finish(Update(Start, buf, offset, length));

        /// <summary>Initial running value for <see cref="Update"/>.</summary>
        public const uint Start = 0xFFFFFFFF;

        /// <summary>Folds <paramref name="length"/> bytes into a running CRC (see <see cref="Start"/>).</summary>
        public static uint Update(uint crc, byte[] buf, int offset, int length)
        {
            for (int i = offset; i < offset + length; i++)
                crc = _table[(crc ^ buf[i]) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        /// <summary>Converts a running value into the final CRC.</summary>
        public static uint Finish(uint crc) => ~crc;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Zlib helpers (wrap DeflateStream, add 2-byte header and Adler-32 tail)
    // ─────────────────────────────────────────────────────────────────────────
    internal static class ZlibHelper
    {
        /// <summary>
        /// Decompress a zlib-framed deflate block (2-byte header + deflate + 4-byte Adler-32).
        /// The header and the Adler-32 checksum are validated, and decompression stops with an
        /// <see cref="InvalidDataException"/> as soon as the output would exceed
        /// <paramref name="maxOutputBytes"/>, so a small compressed stream cannot force a large allocation.
        /// </summary>
        public static byte[] Decompress(byte[] zlibData, long maxOutputBytes)
        {
            if (zlibData == null || zlibData.Length < 6)
                throw new InvalidDataException("Zlib stream too short.");
            if (maxOutputBytes < 0 || maxOutputBytes > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(maxOutputBytes));

            int cmf = zlibData[0], flg = zlibData[1];
            if ((cmf & 0x0F) != 8 || (cmf >> 4) > 7)
                throw new InvalidDataException("Zlib header uses an unsupported compression method or window size.");
            if (((cmf << 8) | flg) % 31 != 0)
                throw new InvalidDataException("Zlib header checksum is invalid.");
            if ((flg & 0x20) != 0)
                throw new InvalidDataException("Zlib preset dictionaries are not valid in PNG data.");

            int deflateLength = zlibData.Length - 6;
            using var input = new MemoryStream(zlibData, 2, deflateLength);
            using var ds = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream((int)Math.Min(maxOutputBytes, 1 << 20));
            var buffer = new byte[81920];
            long total = 0;
            while (true)
            {
                int read = ds.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    break;
                total += read;
                if (total > maxOutputBytes)
                    throw new InvalidDataException("Zlib data inflates to more than the size declared by the image header.");
                output.Write(buffer, 0, read);
            }

            byte[] result = output.ToArray();
            uint expected = BinaryBE.ReadUInt32(zlibData, zlibData.Length - 4);
            if (expected != ComputeAdler32(result, 0, result.Length))
                throw new InvalidDataException("Zlib Adler-32 checksum mismatch.");
            return result;
        }

        /// <summary>
        /// Compress <paramref name="data"/> into a zlib-framed deflate block.
        /// </summary>
        public static byte[] Compress(byte[] data)
        {
            using var output = new MemoryStream();

            // zlib header: CMF = 0x78 (deflate, window 32 K), FLG = 0x9C (default, no dict)
            // 0x789C is divisible by 31, as required by the spec.
            output.WriteByte(0x78);
            output.WriteByte(0x9C);

            using (var ds = new DeflateStream(output, CompressionMode.Compress, leaveOpen: true))
                ds.Write(data, 0, data.Length);

            // Adler-32 checksum of original data, big-endian
            uint adler = ComputeAdler32(data, 0, data.Length);
            output.WriteByte((byte)(adler >> 24));
            output.WriteByte((byte)(adler >> 16));
            output.WriteByte((byte)(adler >>  8));
            output.WriteByte((byte) adler);

            return output.ToArray();
        }

        internal static uint ComputeAdler32(byte[] data, int offset, int length)
        {
            const uint ModAdler = 65521;
            const int Nmax = 5552; // largest n such that 255n(n+1)/2 + (n+1)(65520) fits in 32 bits
            uint a = 1, b = 0;
            int i = offset;
            int remaining = length;
            while (remaining > 0)
            {
                int n = Math.Min(remaining, Nmax);
                remaining -= n;
                for (int end = i + n; i < end; i++)
                {
                    a += data[i];
                    b += a;
                }
                a %= ModAdler;
                b %= ModAdler;
            }
            return (b << 16) | a;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Big-endian read helpers shared by PNG / TIFF
    // ─────────────────────────────────────────────────────────────────────────
    internal static class BinaryBE
    {
        public static uint ReadUInt32(byte[] buf, int off)
            => ((uint)buf[off] << 24) | ((uint)buf[off+1] << 16)
             | ((uint)buf[off+2] << 8) | buf[off+3];

        public static ushort ReadUInt16(byte[] buf, int off)
            => (ushort)(((ushort)buf[off] << 8) | buf[off+1]);

        public static void WriteUInt32(byte[] buf, int off, uint v)
        {
            buf[off]   = (byte)(v >> 24); buf[off+1] = (byte)(v >> 16);
            buf[off+2] = (byte)(v >>  8); buf[off+3] = (byte) v;
        }

        public static void WriteUInt16(byte[] buf, int off, ushort v)
        { buf[off] = (byte)(v >> 8); buf[off+1] = (byte)v; }
    }
}
