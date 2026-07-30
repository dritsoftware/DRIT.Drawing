using DRIT.Drawing.Metafile.Emf;
using DRIT.Drawing.Metafile.Emf.Records;

namespace DRIT.Drawing.Metafile.Emf.Records.DrawingRecordTypes
{
    using System.IO;
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using DRIT.Drawing.Metafile.Wmf.Objects;

    internal class EMR_ROUNDRECT : Record
    {
        public RectL Box;

        public SizeL Corner;

        public EMR_ROUNDRECT()
        {
            Header = new RecordHeader { Type = RecordType.EMR_ROUNDRECT };
        }

        public override void Read(BinaryReader reader)
        {
            Box = new RectL();
            Box.Read(reader);
            Corner = new SizeL();
            Corner.Read(reader);
        }

        public override void Write(BinaryWriter writer)
        {
            Box.Write(writer);
            Corner.Write(writer);
        }
    }
}
