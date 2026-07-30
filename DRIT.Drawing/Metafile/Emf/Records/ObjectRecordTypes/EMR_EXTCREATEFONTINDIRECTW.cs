using DRIT.Drawing.Metafile.Emf;
using DRIT.Drawing.Metafile.Emf.Records;

namespace DRIT.Drawing.Metafile.Emf.Records.ObjectRecordTypes
{
    using System.IO;
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using DRIT.Drawing.Metafile.Wmf.Objects;

    internal class EMR_EXTCREATEFONTINDIRECTW : Record
    {
        public uint IhFont;

        public Font Font { get; set; } = new();

        public EMR_EXTCREATEFONTINDIRECTW()
        {
            Header = new RecordHeader { Type = RecordType.EMR_EXTCREATEFONTINDIRECTW };
        }

        public override void Read(BinaryReader reader)
        {
            IhFont = reader.ReadUInt32();
            Font.Read(reader);
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(IhFont);
            Font.Write(writer);
        }
    }
}
