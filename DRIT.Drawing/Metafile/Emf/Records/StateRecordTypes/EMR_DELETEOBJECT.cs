using DRIT.Drawing.Metafile.Emf;
using DRIT.Drawing.Metafile.Emf.Records;

namespace DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes
{
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using System.IO;

    internal class EMR_DELETEOBJECT : Record
    {
        public uint ObjectIndex;

        public EMR_DELETEOBJECT()
        {
            Header = new RecordHeader { Type = RecordType.EMR_DELETEOBJECT };
        }

        public override void Read(BinaryReader reader)
        {
            ObjectIndex = reader.ReadUInt32();
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(ObjectIndex);
        }
    }
}

