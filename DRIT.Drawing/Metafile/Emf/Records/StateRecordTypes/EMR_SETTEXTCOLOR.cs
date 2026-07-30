using DRIT.Drawing.Metafile.Emf;
using DRIT.Drawing.Metafile.Emf.Records;

namespace DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes
{
    using System.IO;
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using DRIT.Drawing.Metafile.Wmf.Objects;

    internal class EMR_SETTEXTCOLOR : Record
    {
        public ColorRef Color;

        public EMR_SETTEXTCOLOR()
        {
            Header = new RecordHeader { Type = RecordType.EMR_SETTEXTCOLOR };
        }

        public override void Read(BinaryReader reader)
        {
            Color.Read(reader);
        }

        public override void Write(BinaryWriter writer)
        {
            Color.Write(writer);
        }
    }
}

