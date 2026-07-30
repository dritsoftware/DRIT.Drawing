using DRIT.Drawing.Metafile.Emf;
using DRIT.Drawing.Metafile.Emf.Records;

namespace DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes
{
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using System.IO;

    internal class EMR_DELETECOLORSPACE : Record
    {
        public uint IhCS;

        public EMR_DELETECOLORSPACE()
        {
            Header = new RecordHeader { Type = RecordType.EMR_DELETECOLORSPACE };
        }

        public override void Read(BinaryReader reader)
        {
            IhCS = reader.ReadUInt32();
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(IhCS);
        }
    }
}
