using DRIT.Drawing.Metafile.Emf;

namespace DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes
{
    using System.IO;
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using DRIT.Drawing.Metafile.Wmf.Objects;

    internal class EMR_INTERSECTCLIPRECT : Record
    {
        public RectL Clip;

        public EMR_INTERSECTCLIPRECT()
        {
            Header = new RecordHeader { Type = RecordType.EMR_INTERSECTCLIPRECT };
        }

        public override void Read(BinaryReader reader)
        {
            Clip = new RectL();
            Clip.Read(reader);
        }

        public override void Write(BinaryWriter writer)
        {
            Clip.Write(writer);
        }
    }
}
