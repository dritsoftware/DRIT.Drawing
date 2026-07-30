using DRIT.Drawing.Metafile.Emf;
using DRIT.Drawing.Metafile.Emf.Records;

namespace DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes
{
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using System.IO;

    internal class EMR_SETTEXTJUSTIFICATION : Record
    {
        public uint BreakExtra;

        public uint BreakCount;

        public EMR_SETTEXTJUSTIFICATION()
        {
            Header = new RecordHeader { Type = RecordType.EMR_SETTEXTJUSTIFICATION };
        }

        public override void Read(BinaryReader reader)
        {
            BreakExtra = reader.ReadUInt32();
            BreakCount = reader.ReadUInt32();
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(BreakExtra);
            writer.Write(BreakCount);
        }
    }
}

