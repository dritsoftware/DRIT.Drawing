using DRIT.Drawing.Metafile.Emf;

namespace DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes
{
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using System.IO;

    internal class EMR_SELECTPALETTE : Record
    {
        public uint IhPal;

        public EMR_SELECTPALETTE()
        {
            Header = new RecordHeader { Type = RecordType.EMR_SELECTPALETTE };
        }

        public override void Read(BinaryReader reader)
        {
            IhPal = reader.ReadUInt32();
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(IhPal);
        }
    }
}
