using DRIT.Drawing.Metafile.Wmf;
using DRIT.Drawing.Metafile.Wmf.Records;

namespace DRIT.Drawing.Metafile
{
    using DRIT.Drawing.Metafile.Wmf.Enumerations;
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Writes metafile documents to streams or files in WMF, EMF, or EMF+ format.
    /// </summary>
    public class MetafileWriter
    {
        /// <summary>
        /// Initializes a empty <see cref="MetafileWriter"/> instance.
        /// </summary>
        public MetafileWriter()
        {
        }

        private const uint DefaultEmfPlusCommentIdentifier = 0x2B464D45;

        /// <summary>
        /// Writes the specified metafile document to the specified stream.
        /// </summary>
        /// <param name="document">The metafile document to write.</param>
        /// <param name="stream">The stream to write to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> or <paramref name="stream"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the stream does not support writing.</exception>
        /// <exception cref="InvalidDataException">Thrown when the metafile format is unsupported.</exception>
        public void Write(MetafileDocument document, Stream stream)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanWrite)
            {
                throw new InvalidOperationException("The output stream must support writing.");
            }

            stream.SetLength(0);
            stream.Position = 0;

            switch (document.Format)
            {
                case MetafileFormat.Wmf:
                    WriteWmf(document, stream);
                    break;
                case MetafileFormat.Emf:
                    WriteEmf(document, stream);
                    break;
                case MetafileFormat.EmfPlus:
                    WriteEmfPlus(document, stream);
                    break;
                default:
                    throw new InvalidDataException("Unsupported or unknown metafile format.");
            }
        }

        /// <summary>
        /// Writes the specified metafile document to the specified file path.
        /// </summary>
        /// <param name="document">The metafile document to write.</param>
        /// <param name="filePath">The path to write the metafile to.</param>
        public void Write(MetafileDocument document, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("The file path cannot be null or whitespace.", nameof(filePath));
            }

            using var stream = File.Create(filePath);
            Write(document, stream);
        }

        /// <summary>
        /// Writes the specified metafile document to a byte array.
        /// </summary>
        /// <param name="document">The metafile document to write.</param>
        /// <returns>A byte array containing the metafile data.</returns>
        public byte[] WriteToBytes(MetafileDocument document)
        {
            using var stream = new MemoryStream();
            Write(document, stream);
            return stream.ToArray();
        }

        private static void WriteWmf(MetafileDocument document, Stream stream)
        {
            using var writer = new BinaryWriter(stream, System.Text.Encoding.Default, leaveOpen: true);

            if (document.PlaceableHeader != null)
            {
                document.PlaceableHeader.Write(writer);
            }

            var header = document.WmfHeader ?? CreateDefaultWmfHeader(document.WmfRecords);
            header.Write(writer);

            foreach (var record in document.WmfRecords)
            {
                WriteWmfRecord(writer, record);
            }
        }

        private static void WriteEmf(MetafileDocument document, Stream stream)
        {
            using var writer = new BinaryWriter(stream, System.Text.Encoding.Default, leaveOpen: true);
            var serializedRecords = new List<(Metafile.Emf.Records.Record Record, byte[] Payload, Metafile.Emf.Records.RecordHeader Header)>();
            long totalBytes = 0;

            foreach (var record in document.EmfRecords)
            {
                var payload = PadEmfPayload(SerializeEmfPayload(record));
                var header = record.Header;
                header.Size = checked((uint)(payload.Length + 8));
                serializedRecords.Add((record, payload, header));
                totalBytes = checked(totalBytes + header.Size);
            }

            if (serializedRecords.Count > 0 && serializedRecords[0].Record is Metafile.Emf.Records.StateRecordTypes.EMR_HEADER)
            {
                var headerRecord = (Metafile.Emf.Records.StateRecordTypes.EMR_HEADER)serializedRecords[0].Record;
                headerRecord.HeaderObject.Bytes = checked((uint)totalBytes);
                headerRecord.HeaderObject.Records = checked((uint)serializedRecords.Count);
                var headerPayload = serializedRecords[0].Payload;
                if (headerPayload.Length >= 48)
                    Buffer.BlockCopy(BitConverter.GetBytes(checked((uint)totalBytes)), 0, headerPayload, 40, 4);
                if (headerPayload.Length >= 52)
                    Buffer.BlockCopy(BitConverter.GetBytes(checked((uint)serializedRecords.Count)), 0, headerPayload, 44, 4);
            }

            foreach (var item in serializedRecords)
            {
                item.Header.Write(writer);
                writer.Write(item.Payload);
            }
        }

        private static byte[] PadEmfPayload(byte[] payload)
        {
            int paddedLength = (payload.Length + 3) & ~3;
            if (paddedLength == payload.Length) return payload;
            var padded = new byte[paddedLength];
            Buffer.BlockCopy(payload, 0, padded, 0, payload.Length);
            return padded;
        }

        private static void WriteEmfPlus(MetafileDocument document, Stream stream)
        {
            using var writer = new BinaryWriter(stream, System.Text.Encoding.Default, leaveOpen: true);

            var dataSize = document.EmfPlusDataSize;
            if (dataSize == 0)
            {
                dataSize = ComputeEmfPlusDataSize(document.EmfPlusRecords);
            }

            var commentIdentifier = document.EmfPlusCommentIdentifier == 0
                ? DefaultEmfPlusCommentIdentifier
                : document.EmfPlusCommentIdentifier;

            writer.Write(dataSize);
            writer.Write(commentIdentifier);

            foreach (var record in document.EmfPlusRecords)
            {
                var payload = SerializeEmfPlusPayload(record);
                var header = record.Header;

                if (header.DataSize == 0)
                {
                    header.DataSize = checked((uint)payload.Length);
                }

                if (header.Size == 0)
                {
                    header.Size = checked(header.DataSize + 12);
                }

                header.Write(writer);
                writer.Write(payload);
            }
        }

        private static void WriteWmfRecord(BinaryWriter writer, Record record)
        {
            var payload = SerializeWmfPayload(record);
            var paddedPayloadLength = payload.Length % 2 == 0 ? payload.Length : payload.Length + 1;

            var header = record.Header;
            if (header.RecordSize == 0)
            {
                header.RecordSize = checked((uint)((6 + paddedPayloadLength) / 2));
            }

            header.Write(writer);
            writer.Write(payload);

            if (paddedPayloadLength != payload.Length)
            {
                writer.Write((byte)0);
            }
        }

        private static MetaHeader CreateDefaultWmfHeader(IReadOnlyList<Record> records)
        {
            uint maxRecord = 0;
            ulong totalWords = 9;

            foreach (var record in records)
            {
                uint recordSize = GetWmfRecordSizeWords(record);
                totalWords += recordSize;
                if (recordSize > maxRecord)
                {
                    maxRecord = recordSize;
                }
            }

            var sizeLow = (ushort)(totalWords & 0xFFFF);
            var sizeHigh = (ushort)((totalWords >> 16) & 0xFFFF);

            return new MetaHeader
            {
                Type = MetafileType.DISKMETAFILE,
                HeaderSize = 9,
                Version = MetafileVersion.METAVERSION300,
                SizeLow = sizeLow,
                SizeHigh = sizeHigh,
                NumberOfObjects = 0,
                MaxRecord = maxRecord,
                NumberOfMembers = 0,
            };
        }

        private static uint GetWmfRecordSizeWords(Record record)
        {
            if (record.Header.RecordSize > 0)
            {
                return record.Header.RecordSize;
            }

            var payload = SerializeWmfPayload(record);
            var paddedPayloadLength = payload.Length % 2 == 0 ? payload.Length : payload.Length + 1;
            return checked((uint)((6 + paddedPayloadLength) / 2));
        }

        private static uint ComputeEmfPlusDataSize(IReadOnlyList<Metafile.EmfPlus.Records.Record> records)
        {
            uint total = 0;
            foreach (var record in records)
            {
                if (record.Header.Size > 0)
                {
                    total = checked(total + record.Header.Size);
                    continue;
                }

                var payload = SerializeEmfPlusPayload(record);
                total = checked(total + (uint)(payload.Length + 12));
            }

            return total;
        }

        private static byte[] SerializeWmfPayload(Record record)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            record.Write(writer);
            return ms.ToArray();
        }

        private static byte[] SerializeEmfPayload(Metafile.Emf.Records.Record record)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            record.Write(writer);
            return ms.ToArray();
        }

        private static byte[] SerializeEmfPlusPayload(Metafile.EmfPlus.Records.Record record)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            record.Write(writer);
            return ms.ToArray();
        }
    }
}
