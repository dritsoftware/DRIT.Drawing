using DRIT.Drawing.Metafile.Emf;
using DRIT.Drawing.Metafile.Emf.Records;

namespace DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes
{
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using System.IO;

    internal class EMR_RESIZEPALETTE : Record
    {
        public uint IhPal;

        public uint NumberOfEntries;

        public EMR_RESIZEPALETTE()
        {
            Header = new RecordHeader { Type = RecordType.EMR_RESIZEPALETTE };
        }

        public override void Read(BinaryReader reader)
        {
            IhPal = reader.ReadUInt32();
            NumberOfEntries = reader.ReadUInt32();
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(IhPal);
            writer.Write(NumberOfEntries);
        }
    }
}
