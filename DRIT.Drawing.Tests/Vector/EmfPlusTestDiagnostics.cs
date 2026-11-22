using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DRIT.Drawing.Metafile;
using DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes;

namespace DRIT.Drawing.Tests.Vector
{
    internal sealed class EmfPlusRecordSnapshot
    {
        internal ushort Type { get; set; }
        internal ushort Flags { get; set; }
        internal uint Size { get; set; }
        internal uint DataSize { get; set; }
        internal byte[] Payload { get; set; } = Array.Empty<byte>();

        internal string NormalizedKey()
            => string.Format("{0:X4}:{1:X4}:{2}:{3}:{4}",
                Type, NormalizeFlags(Type, Flags), Size, DataSize,
                Convert.ToHexString(Payload));

        private static ushort NormalizeFlags(ushort type, ushort flags)
        {
            const ushort objectType = 0x4008;
            if (type == objectType)
                return (ushort)(flags & 0xFF00);

            return flags;
        }
    }

    internal static class EmfPlusTestDiagnostics
    {
        private const uint CommentIdentifier = 0x2B464D45;
        private const int RecordHeaderSize = 12;

        internal static IReadOnlyList<IReadOnlyList<EmfPlusRecordSnapshot>> Decode(
            MetafileDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            var comments = new List<IReadOnlyList<EmfPlusRecordSnapshot>>();
            foreach (var comment in document.EmfRecords.OfType<EMR_COMMENT>())
            {
                if (!StartsWithEmfPlusIdentifier(comment.CommentData))
                    continue;

                comments.Add(DecodeComment(comment.CommentData));
            }

            return comments;
        }

        internal static void AssertValid(
            IReadOnlyList<IReadOnlyList<EmfPlusRecordSnapshot>> comments)
        {
            if (comments == null) throw new ArgumentNullException(nameof(comments));

            foreach (var comment in comments)
            foreach (var record in comment)
            {
                if (record.Size < RecordHeaderSize)
                    throw new InvalidDataException("The EMF+ record size is smaller than its header.");
                if (record.Size % 4 != 0)
                    throw new InvalidDataException("The EMF+ record size is not DWORD aligned.");
                if (record.DataSize > record.Size - RecordHeaderSize)
                    throw new InvalidDataException("The EMF+ payload exceeds its record size.");
                if (record.Payload.Length != record.DataSize)
                    throw new InvalidDataException("The EMF+ payload length does not match DataSize.");
            }
        }

        internal static IEnumerable<string> Normalize(
            IReadOnlyList<IReadOnlyList<EmfPlusRecordSnapshot>> comments)
        {
            if (comments == null) throw new ArgumentNullException(nameof(comments));

            return comments.SelectMany((comment, commentIndex) =>
                comment.Select(record => string.Format("{0}|{1}",
                    commentIndex, record.NormalizedKey())));
        }

        private static IReadOnlyList<EmfPlusRecordSnapshot> DecodeComment(byte[] data)
        {
            using var stream = new MemoryStream(data, writable: false);
            using var reader = new BinaryReader(stream);
            if (reader.ReadUInt32() != CommentIdentifier)
                throw new InvalidDataException("The EMF+ comment identifier is invalid.");

            var records = new List<EmfPlusRecordSnapshot>();
            while (stream.Position < stream.Length)
            {
                if (stream.Length - stream.Position < RecordHeaderSize)
                    throw new InvalidDataException("The EMF+ comment has a truncated record header.");

                ushort type = reader.ReadUInt16();
                ushort flags = reader.ReadUInt16();
                uint size = reader.ReadUInt32();
                uint dataSize = reader.ReadUInt32();
                if (size < RecordHeaderSize || dataSize > size - RecordHeaderSize)
                    throw new InvalidDataException("The EMF+ record declares an invalid envelope.");

                long recordEnd = stream.Position + size - RecordHeaderSize;
                if (recordEnd > stream.Length)
                    throw new InvalidDataException("The EMF+ record exceeds its comment boundary.");

                byte[] payload = reader.ReadBytes(checked((int)dataSize));
                if (payload.Length != dataSize)
                    throw new InvalidDataException("The EMF+ record payload is truncated.");

                records.Add(new EmfPlusRecordSnapshot
                {
                    Type = type,
                    Flags = flags,
                    Size = size,
                    DataSize = dataSize,
                    Payload = payload,
                });
                stream.Position = recordEnd;
            }

            return records;
        }

        private static bool StartsWithEmfPlusIdentifier(byte[] data)
            => data != null && data.Length >= sizeof(uint) &&
               BitConverter.ToUInt32(data, 0) == CommentIdentifier;
    }
}