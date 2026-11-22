using DRIT.Drawing.Metafile.Emf;
using DRIT.Drawing.Metafile.Emf.Records;

namespace DRIT.Drawing.Metafile.Emf.Records.DrawingRecordTypes
{
    using System.IO;
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using DRIT.Drawing.Metafile.Emf.Objects;
    using DRIT.Drawing.Metafile.Wmf.Objects;

    internal class EMR_EXTTEXTOUTW : Record
    {
        public RectL Bounds;

        public GraphicsMode IGraphicsMode;

        public float ExScale;

        public float EyScale;

        public EmrText EmrText { get; set; } = new();

        public EMR_EXTTEXTOUTW()
        {
            Header = new RecordHeader { Type = RecordType.EMR_EXTTEXTOUTW };
        }

        public override void Read(BinaryReader reader)
        {
            Bounds = new RectL();
            Bounds.Read(reader);
            IGraphicsMode = (GraphicsMode)reader.ReadUInt32();
            ExScale = reader.ReadSingle();
            EyScale = reader.ReadSingle();
            EmrText.Read(reader, 8 + 28);
        }

        public override void Write(BinaryWriter writer)
        {
            Bounds.Write(writer);
            writer.Write((uint)IGraphicsMode);
            writer.Write(ExScale);
            writer.Write(EyScale);
            EmrText.Write(writer, 8 + 28);
        }
    }
}
