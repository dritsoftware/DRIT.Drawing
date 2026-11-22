namespace DRIT.Drawing.Metafile.EmfPlus.Records
{
    using System.IO;

    internal class EmfPlusDrawPathRecord : Record
    {
        public ushort PathId => Header.Flags;

        public uint PenId;

        public override void Read(BinaryReader reader)
        {
            if (Header.DataSize >= 4)
            {
                PenId = reader.ReadUInt32();
                var remaining = (int)Header.DataSize - 4;
                if (remaining > 0)
                {
                    Data = reader.ReadBytes(remaining);
                }
            }
            else
            {
                base.Read(reader);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(PenId);
            writer.Write(Data);
        }
    }
}
