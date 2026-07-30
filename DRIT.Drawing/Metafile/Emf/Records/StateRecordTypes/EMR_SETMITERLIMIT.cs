using DRIT.Drawing.Metafile.Emf;

namespace DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes
{
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using System.IO;

    internal class EMR_SETMITERLIMIT : Record
    {
        public float MiterLimit;

        public EMR_SETMITERLIMIT()
        {
            Header = new RecordHeader { Type = RecordType.EMR_SETMITERLIMIT };
        }

        public override void Read(BinaryReader reader)
        {
            MiterLimit = reader.ReadSingle();
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(MiterLimit);
        }
    }
}

