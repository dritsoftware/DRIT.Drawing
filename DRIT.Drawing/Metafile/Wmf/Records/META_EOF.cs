using System.IO;
using DRIT.Drawing.Metafile.Wmf;
using DRIT.Drawing.Metafile.Wmf.Enumerations;

namespace DRIT.Drawing.Metafile.Wmf.Records
{
    internal class META_EOF : Record
    {
        public META_EOF()
        {
            Header.RecordFunction = RecordType.META_EOF;
        }

        public override void Read(BinaryReader reader)
        {
            // no parameters
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
        }
    }
}
