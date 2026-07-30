using DRIT.Drawing.Metafile.Emf;

namespace DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes
{
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using System.IO;

    internal class EMR_REALIZEPALETTE : Record
    {
        public EMR_REALIZEPALETTE()
        {
            Header = new RecordHeader { Type = RecordType.EMR_REALIZEPALETTE };
        }

        public override void Read(BinaryReader reader)
        {
        }

        public override void Write(BinaryWriter writer)
        {
        }
    }
}
