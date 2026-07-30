using DRIT.Drawing.Metafile.Emf;
using DRIT.Drawing.Metafile.Emf.Records;

namespace DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes
{
    using System.IO;
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using DRIT.Drawing.Metafile.Wmf.Objects;

    internal class EMR_MOVETOEX : Record
    {
        public PointL Offset;

        public EMR_MOVETOEX()
        {
            Header = new RecordHeader { Type = RecordType.EMR_MOVETOEX };
        }

        public override void Read(BinaryReader reader)
        {
            Offset = new PointL();
            Offset.Read(reader);
        }

        public override void Write(BinaryWriter writer)
        {
            Offset.Write(writer);
        }
    }
}

