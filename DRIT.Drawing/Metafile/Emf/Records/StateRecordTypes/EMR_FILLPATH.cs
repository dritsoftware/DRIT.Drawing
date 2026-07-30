using DRIT.Drawing.Metafile.Emf;

namespace DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes
{
    using System.IO;
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using DRIT.Drawing.Metafile.Wmf.Objects;

    internal class EMR_FILLPATH : Record
    {
        public RectL Bounds;

        public EMR_FILLPATH()
        {
            Header = new RecordHeader { Type = RecordType.EMR_FILLPATH };
        }

        public override void Read(BinaryReader reader)
        {
            Bounds = new RectL();
            Bounds.Read(reader);
        }

        public override void Write(BinaryWriter writer)
        {
            Bounds.Write(writer);
        }
    }
}
