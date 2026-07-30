using DRIT.Drawing.Metafile.Wmf;

namespace DRIT.Drawing.Metafile.Wmf.Records.StateRecordTypes
{
    using DRIT.Drawing.Metafile.Wmf.Enumerations;
    using DRIT.Drawing.Metafile.Wmf.Records;
    using System.IO;

    internal class META_SAVEDC : Record
    {
        public META_SAVEDC()
        {
            Header.RecordFunction = RecordType.META_SAVEDC;
        }

        public override void Read(BinaryReader reader)
        {
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
        }
    }
}
