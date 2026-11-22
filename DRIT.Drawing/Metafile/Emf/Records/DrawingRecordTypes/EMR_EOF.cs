using DRIT.Drawing.Metafile.Emf;

namespace DRIT.Drawing.Metafile.Emf.Records.DrawingRecordTypes
{
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using System;
    using System.IO;

    internal class EMR_EOF : Record
    {
        public uint NumberOfPalEntries;

        public uint OffsetToPalEntries;

        public uint SizeLast = 20;

        public byte[] EndOfFileData { get; set; } = Array.Empty<byte>();

        public EMR_EOF()
        {
            Header = new RecordHeader { Type = RecordType.EMR_EOF };
        }

        public override void Read(BinaryReader reader)
        {
            NumberOfPalEntries = reader.ReadUInt32();
            OffsetToPalEntries = reader.ReadUInt32();
            SizeLast = reader.ReadUInt32();

            int extraBytes = Convert.ToInt32(Header.Size) - 20;
            if (extraBytes > 0)
            {
                EndOfFileData = reader.ReadBytes(extraBytes);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(NumberOfPalEntries);
            writer.Write(OffsetToPalEntries);
            writer.Write(SizeLast);
            writer.Write(EndOfFileData);
        }
    }
}
