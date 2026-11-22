using System;
using System.Collections.Generic;
using System.IO;

namespace DRIT.Drawing.Codecs.Jpeg
{
    /// <summary>
    /// Reads the JPEG marker stream (SOI … EOI) segment by segment.
    /// After positioning the stream at the start of entropy-coded data,
    /// obtain a <see cref="BitReader"/> via <see cref="CreateBitReader"/>.
    /// </summary>
    internal sealed class JpegReader
    {
        private readonly Stream _stream;
        private readonly bool _strictMarkers;

        /// <param name="stream">The JPEG stream.</param>
        /// <param name="strictMarkers">
        /// When true, any byte between segments that is not the start of a marker is an error. By default such
        /// bytes are skipped until the next marker, which is what libjpeg does (it only warns).
        /// </param>
        public JpegReader(Stream stream, bool strictMarkers = false)
        {
            _stream = ValidateStream(stream);
            _strictMarkers = strictMarkers;
        }

        private static Stream ValidateStream(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (stream.CanSeek && stream.Length - stream.Position > CodecValidation.MaxSourceBytes)
                throw new InvalidDataException("JPEG source exceeds the configured input limit.");
            return stream;
        }

        // ── Public position ──────────────────────────────────────────────────────

        /// <summary>Current byte position in the underlying stream.</summary>
        public long Position => _stream.Position;

        // ── Marker reading ───────────────────────────────────────────────────────

        /// <summary>
        /// Reads the next JPEG marker and its payload.
        /// Stand-alone markers (SOI, EOI, RSTn) have an empty payload byte[].
        /// Skips any 0xFF fill bytes before the marker byte.
        /// </summary>
        public (JpegMarker marker, byte[] payload) ReadMarker()
        {
            // Skip padding / find leading 0xFF
            int b = ReadByteOrThrow();
            while (b != 0xFF)
            {
                if (_strictMarkers)
                    throw new InvalidDataException("Unexpected data between JPEG segments.");
                b = ReadByteOrThrow();
            }

            // Skip 0xFF fill bytes
            int marker;
            do { marker = ReadByteOrThrow(); }
            while (marker == 0xFF);

            var m = (JpegMarker)(byte)marker;

            // Stand-alone markers carry no length field
            if (IsStandAlone(m))
                return (m, Array.Empty<byte>());

            // Read 2-byte big-endian length (includes the 2 length bytes)
            int hi = ReadByteOrThrow();
            int lo = ReadByteOrThrow();
            int length = (hi << 8) | lo;
            if (length < 2)
                throw new InvalidDataException($"Invalid JPEG segment length {length} for marker 0x{marker:X2}.");

            int dataLen = length - 2;
            byte[] payload = new byte[dataLen];
            int read = 0;
            while (read < dataLen)
            {
                int n = _stream.Read(payload, read, dataLen - read);
                if (n == 0) throw new EndOfStreamException("Unexpected end of JPEG stream.");
                read += n;
            }
            return (m, payload);
        }

        /// <summary>
        /// Reads entropy-coded bytes after SOS until the next non-restart marker.
        /// The returned data is de-stuffed (0xFF 0x00 becomes 0xFF) and restart
        /// markers are removed. Stream position is left at the next marker.
        /// </summary>
        /// <param name="maxSegments">Upper bound for the number of restart intervals (see <see cref="ReadEntropySegments"/>).</param>
        /// <param name="allowMissingEoi">See <see cref="ReadEntropySegments"/>.</param>
        public byte[] ReadEntropyData(int maxSegments = int.MaxValue, bool allowMissingEoi = false)
        {
            var segments = ReadEntropySegments(maxSegments, allowMissingEoi);
            long totalLength = 0;
            foreach (var segment in segments)
                totalLength += segment.Length;

            int length = CodecValidation.CheckedBufferLength(totalLength, "JPEG entropy data");
            var data = new byte[length];
            int offset = 0;
            foreach (var segment in segments)
            {
                Buffer.BlockCopy(segment, 0, data, offset, segment.Length);
                offset += segment.Length;
            }

            return data;
        }

        /// <summary>
        /// Reads entropy-coded data as independently byte-aligned segments separated by
        /// restart markers. The returned segments are de-stuffed and do not include markers.
        /// </summary>
        /// <param name="maxSegments">
        /// Largest number of restart intervals the frame can contain (at most one per MCU or block).
        /// The caller derives it from the frame header; exceeding it means the input is not a valid scan.
        /// </param>
        /// <param name="allowMissingEoi">
        /// When true, entropy data that runs to the end of the stream (a missing EOI marker, as written by
        /// some cameras and seen in truncated downloads) ends the scan instead of being an error.
        /// </param>
        public IReadOnlyList<byte[]> ReadEntropySegments(int maxSegments = int.MaxValue, bool allowMissingEoi = false)
        {
            if (!_stream.CanSeek)
                throw new NotSupportedException("Entropy segment reading requires a seekable stream.");

            var segments = new List<byte[]>();
            using var ms = new MemoryStream();
            int expectedRestart = 0;
            while (true)
            {
                int b = _stream.ReadByte();
                if (b < 0)
                {
                    if (allowMissingEoi)
                        break;
                    throw new EndOfStreamException("Unexpected end of JPEG entropy data; EOI marker is missing.");
                }

                if (b != 0xFF)
                {
                    ms.WriteByte((byte)b);
                    continue;
                }

                int next = _stream.ReadByte();
                if (next < 0)
                {
                    if (allowMissingEoi)
                        break;
                    throw new EndOfStreamException("Unexpected end of JPEG entropy data; EOI marker is missing.");
                }

                // Stuffed entropy byte.
                if (next == 0x00)
                {
                    ms.WriteByte(0xFF);
                    continue;
                }

                // Fill bytes may appear before a marker.
                while (next == 0xFF)
                {
                    next = _stream.ReadByte();
                    if (next < 0)
                    {
                        if (allowMissingEoi)
                            goto EndOfData;
                        throw new EndOfStreamException("Unexpected end of JPEG entropy data; EOI marker is missing.");
                    }
                }

                // Restart markers belong to entropy coding; skip marker bytes and continue.
                if (next >= 0xD0 && next <= 0xD7)
                {
                    // A restart interval holds at least one MCU, so the frame geometry bounds the count.
                    CodecValidation.ValidateCount(segments.Count + 1, maxSegments, "JPEG restart interval count");
                    segments.Add(ms.ToArray());
                    ms.SetLength(0);
                    if (next != 0xD0 + expectedRestart)
                        throw new InvalidDataException($"Unexpected JPEG restart marker RST{next - 0xD0}; expected RST{expectedRestart}.");
                    expectedRestart = (expectedRestart + 1) & 7;
                    continue;
                }

                // Rewind to the marker prefix so ReadMarker() can consume it.
                _stream.Seek(-2, SeekOrigin.Current);
                break;
            }

        EndOfData:
            segments.Add(ms.ToArray());
            return segments;
        }

        /// <summary>
        /// Returns a <see cref="BitReader"/> that reads entropy-coded data
        /// from the stream at its current position (right after a SOS payload).
        /// </summary>
        public BitReader CreateBitReader() => new BitReader(_stream);

        // ── Helpers ──────────────────────────────────────────────────────────────

        private int ReadByteOrThrow()
        {
            int b = _stream.ReadByte();
            if (b < 0) throw new EndOfStreamException("Unexpected end of JPEG stream.");
            return b;
        }

        private static bool IsStandAlone(JpegMarker m) =>
            m == JpegMarker.SOI || m == JpegMarker.EOI ||
            (m >= JpegMarker.RST0 && m <= JpegMarker.RST7);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // BitReader — reads individual bits from a JPEG entropy-coded stream.
    // Handles byte-stuffing: 0xFF 0x00 → single 0xFF byte.
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Bit-level reader for JPEG entropy-coded data.
    /// Reads bytes MSB-first; transparently strips 0xFF 0x00 stuffing bytes.
    /// </summary>
    internal sealed class BitReader
    {
        private readonly Stream? _stream;
        private readonly byte[]? _bytes;
        private int _byteIndex;
        private int _buffer;    // up to 32 bits buffered
        private int _bitsLeft;  // valid bits in _buffer
        private bool _streamEnded;

        public BitReader(Stream stream) =>
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));

        public BitReader(byte[] bytes) =>
            _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));

        // ── Bit access ───────────────────────────────────────────────────────────

        /// <summary>Read exactly <paramref name="n"/> bits (1–16) MSB-first.</summary>
        public int ReadBits(int n)
        {
            EnsureBits(n);
            _bitsLeft -= n;
            return (_buffer >> _bitsLeft) & ((1 << n) - 1);
        }

        /// <summary>Peek at the next <paramref name="n"/> bits without consuming them.</summary>
        public int PeekBits(int n)
        {
            EnsureBits(n);
            return (_buffer >> (_bitsLeft - n)) & ((1 << n) - 1);
        }

        /// <summary>Discard <paramref name="n"/> bits that were already peeked.</summary>
        public void ConsumeBits(int n) => _bitsLeft -= n;

        internal int BitsAvailable => _bitsLeft;

        internal void EnsureOnlyPaddingRemains()
        {
            // Bits are loaded a whole byte at a time, so anything beyond the unfinished byte (the low
            // `_bitsLeft & 7` ... bits are the padding of the last byte) is unread data.
            int padding = _bitsLeft & 7;
            if (_bitsLeft - padding > 0 || (_bytes != null && _byteIndex < _bytes.Length))
                throw new InvalidDataException("JPEG entropy contains data after the final decoded symbol.");

            if (padding > 0)
            {
                int mask = (1 << padding) - 1;
                if ((_buffer & mask) != mask)
                    throw new InvalidDataException("JPEG entropy padding bits are invalid.");
            }
        }

        /// <summary>
        /// Returns the next 16 bits of entropy data without consuming them, loading as many whole bytes as the
        /// buffer can hold. <paramref name="valid"/> is the number of those bits that really come from the
        /// stream (fewer than 16 only at the very end); the remaining low bits are zero and must never be
        /// treated as data.
        /// </summary>
        internal int Peek16(out int valid)
        {
            while (_bitsLeft <= 24 && TryFillByte())
            {
            }

            if (_bitsLeft >= 16)
            {
                valid = 16;
                return (_buffer >> (_bitsLeft - 16)) & 0xFFFF;
            }

            valid = _bitsLeft;
            return (_buffer << (16 - _bitsLeft)) & 0xFFFF;
        }

        /// <summary>Read a single bit (0 or 1).</summary>
        public int ReadBit() => ReadBits(1);

        // ── Byte-aligned access (used after restart markers) ─────────────────────

        /// <summary>Discard any partial byte; reset buffer. Call after a restart marker.</summary>
        public void Flush()
        {
            // Drop only the unfinished byte; whole bytes that were prefetched belong to the next interval.
            _bitsLeft -= _bitsLeft & 7;
        }

        // ── Private ──────────────────────────────────────────────────────────────

        private void EnsureBits(int n)
        {
            while (_bitsLeft < n)
                FillByte();
        }

        /// <summary>Loads one byte if the entropy data has one left; false at the end of the data.</summary>
        private bool TryFillByte()
        {
            if (_bytes != null)
            {
                if (_byteIndex >= _bytes.Length)
                    return false;
                _buffer = (_buffer << 8) | _bytes[_byteIndex++];
                _bitsLeft += 8;
                return true;
            }

            // Stream mode: a marker or the end of the stream ends the data; the error is raised when a
            // caller actually needs more bits (FillByte).
            if (_streamEnded)
                return false;
            try
            {
                FillByte();
                return true;
            }
            catch (EndOfStreamException)
            {
                _streamEnded = true;
                return false;
            }
        }

        private void FillByte()
        {
            if (_bytes != null)
            {
                if (_byteIndex >= _bytes.Length)
                    throw new EndOfStreamException("Unexpected end of JPEG entropy data.");

                int value = _bytes[_byteIndex++];
                _buffer = (_buffer << 8) | value;
                _bitsLeft += 8;
                return;
            }

            if (_streamEnded)
                throw new EndOfStreamException("Unexpected end of JPEG entropy data.");
            int b = _stream!.ReadByte();
            if (b < 0)
                throw new EndOfStreamException("Unexpected end of JPEG entropy data.");

            if (b == 0xFF)
            {
                int next = _stream.ReadByte();
                if (next == 0x00)
                {
                    // Byte stuffing: 0xFF 0x00 → 0xFF
                    b = 0xFF;
                }
                else if (next >= 0xD0 && next <= 0xD7)
                {
                    throw new InvalidDataException("Restart marker encountered while reading a non-segmented JPEG entropy stream.");
                }
                else if (next == 0xD9 || next == 0xDA)
                {
                    throw new EndOfStreamException("JPEG entropy data ended before the next symbol was complete.");
                }
                else
                {
                    throw new InvalidDataException($"Unexpected marker 0xFF{next:X2} in JPEG entropy data.");
                }
            }
            _buffer = (_buffer << 8) | b;
            _bitsLeft += 8;
        }
    }
}
