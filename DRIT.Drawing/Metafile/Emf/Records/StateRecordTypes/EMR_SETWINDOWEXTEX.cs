using DRIT.Drawing.Metafile.Emf;
using DRIT.Drawing.Metafile.Emf.Records;

namespace DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes
{
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using System.IO;

    internal class EMR_SETWINDOWEXTEX : Record
    {
        public int XExtent;

        public int YExtent;

        public EMR_SETWINDOWEXTEX()
        {
            Header = new RecordHeader { Type = RecordType.EMR_SETWINDOWEXTEX };
        }

        public override void Read(BinaryReader reader)
        {
            XExtent = reader.ReadInt32();
            YExtent = reader.ReadInt32();
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(XExtent);
            writer.Write(YExtent);
        }
    }
}

