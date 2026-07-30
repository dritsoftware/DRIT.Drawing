using DRIT.Drawing.Metafile.Emf;

namespace DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes
{
    using System.IO;
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using DRIT.Drawing.Metafile.Wmf.Objects;

    internal class EMR_STROKEANDFILLPATH : Record
    {
        public RectL Bounds;

        public EMR_STROKEANDFILLPATH()
        {
            Header = new RecordHeader { Type = RecordType.EMR_STROKEANDFILLPATH };
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
