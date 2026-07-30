using DRIT.Drawing.Metafile.Emf;
using DRIT.Drawing.Metafile.Emf.Records;

namespace DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes
{
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using System.IO;

    internal class EMR_FORCEUFIMAPPING : Record
    {
        public EMR_FORCEUFIMAPPING()
        {
            Header = new RecordHeader { Type = RecordType.EMR_FORCEUFIMAPPING };
        }

        public override void Read(BinaryReader reader)
        {
        }

        public override void Write(BinaryWriter writer)
        {
        }
    }
}
