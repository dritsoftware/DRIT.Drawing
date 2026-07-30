using DRIT.Drawing.Metafile.Emf;

namespace DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes
{
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using System.IO;

    internal class EMR_COLORCORRECTPALETTE : Record
    {
        public uint IhPalette;

        public uint NumberOfEntries;

        public EMR_COLORCORRECTPALETTE()
        {
            Header = new RecordHeader { Type = RecordType.EMR_COLORCORRECTPALETTE };
        }

        public override void Read(BinaryReader reader)
        {
            IhPalette = reader.ReadUInt32();
            NumberOfEntries = reader.ReadUInt32();
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(IhPalette);
            writer.Write(NumberOfEntries);
        }
    }
}
