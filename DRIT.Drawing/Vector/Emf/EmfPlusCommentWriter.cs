using System;
using System.IO;

namespace DRIT.Drawing.Vector.Emf
{
    internal static class EmfPlusCommentWriter
    {
        private const uint CommentIdentifier = 0x2B464D45;
        private const ushort HeaderType = 0x4001;
        private const ushort HeaderFlags = 0x0001;
        private const uint HeaderVersion = 0xDBC01002;

        public static byte[] CreateHeaderComment(float dpiX, float dpiY)
        {
            return CreateComment(writer =>
            {
                using var payload = new MemoryStream();
                using var payloadWriter = new BinaryWriter(payload);
                payloadWriter.Write(HeaderVersion);
                payloadWriter.Write((uint)HeaderFlags);
                payloadWriter.Write((uint)Math.Max(1, Math.Round(dpiX)));
                payloadWriter.Write((uint)Math.Max(1, Math.Round(dpiY)));
                WriteRecord(writer, HeaderType, HeaderFlags, payload.ToArray());
            });
        }

        public static byte[] CreateComment(Action<BinaryWriter> writeRecords)
        {
            if (writeRecords == null) throw new ArgumentNullException(nameof(writeRecords));

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(CommentIdentifier);
            writeRecords(writer);
            return stream.ToArray();
        }

        public static void WriteRecord(
            BinaryWriter writer, ushort type, ushort flags, byte[] data = null)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            data = data ?? Array.Empty<byte>();
            int paddedLength = (data.Length + 3) & ~3;
            writer.Write(type);
            writer.Write(flags);
            writer.Write((uint)(12 + paddedLength));
            writer.Write((uint)data.Length);
            writer.Write(data);
            for (int i = data.Length; i < paddedLength; i++)
                writer.Write((byte)0);
        }
    }
}