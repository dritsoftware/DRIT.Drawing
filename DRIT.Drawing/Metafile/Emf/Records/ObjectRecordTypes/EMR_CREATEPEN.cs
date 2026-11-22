using DRIT.Drawing.Metafile.Emf;

namespace DRIT.Drawing.Metafile.Emf.Records.ObjectRecordTypes
{
    using System.IO;
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using DRIT.Drawing.Metafile.Wmf.Objects;
    using PenStyle = DRIT.Drawing.Metafile.Wmf.Enumerations.PenStyle;

    internal class EMR_CREATEPEN : Record
    {
        internal uint IhPen;

        internal PenStyle PenStyle;

        internal int WidthX;

        internal int WidthY;

        internal ColorRef ColorRef = new();

        public EMR_CREATEPEN()
        {
            Header = new RecordHeader { Type = RecordType.EMR_CREATEPEN };
        }

        public override void Read(BinaryReader reader)
        {
            IhPen = reader.ReadUInt32();
            PenStyle = (PenStyle)reader.ReadUInt32();
            WidthX = reader.ReadInt32();
            WidthY = reader.ReadInt32();
            ColorRef.Read(reader);
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(IhPen);
            writer.Write((uint)PenStyle);
            writer.Write(WidthX);
            writer.Write(WidthY);
            ColorRef.Write(writer);
        }
    }
}
