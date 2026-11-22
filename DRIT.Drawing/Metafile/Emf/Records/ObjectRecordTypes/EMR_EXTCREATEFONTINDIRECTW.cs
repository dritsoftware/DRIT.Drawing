using DRIT.Drawing.Metafile.Emf;
using DRIT.Drawing.Metafile.Emf.Records;

namespace DRIT.Drawing.Metafile.Emf.Records.ObjectRecordTypes
{
    using System;
    using System.IO;
    using System.Text;
    using DRIT.Drawing.Metafile.Emf.Enumerations;
    using DRIT.Drawing.Metafile.Wmf.Enumerations;
    using DRIT.Drawing.Metafile.Wmf.Objects;
    using DRIT.Drawing.Metafile.Wmf.Flags;
    using EmfRecordType = DRIT.Drawing.Metafile.Emf.Enumerations.RecordType;

    internal class EMR_EXTCREATEFONTINDIRECTW : Record
    {
        public uint IhFont;

        public Font Font { get; set; } = new();

        public EMR_EXTCREATEFONTINDIRECTW()
        {
            Header = new RecordHeader { Type = EmfRecordType.EMR_EXTCREATEFONTINDIRECTW };
        }

        public override void Read(BinaryReader reader)
        {
            IhFont = reader.ReadUInt32();
            if (Header.Size < 332)
            {
                Font.Read(reader);
                return;
            }

            Font.Height = ClampToInt16(reader.ReadInt32());
            Font.Width = ClampToInt16(reader.ReadInt32());
            Font.Escapement = ClampToInt16(reader.ReadInt32());
            Font.Orientation = ClampToInt16(reader.ReadInt32());
            Font.Weight = ClampToInt16(reader.ReadInt32());
            Font.Italic = reader.ReadByte();
            Font.Underline = reader.ReadByte();
            Font.StrikeOut = reader.ReadByte();
            Font.CharSet = (CharacterSet)reader.ReadByte();
            Font.OutPrecision = (OutPrecision)reader.ReadByte();
            Font.ClipPrecision = (ClipPrecision)reader.ReadByte();
            reader.ReadByte();
            Font.PitchAndFamily = reader.ReadByte();
            Font.Facename = ReadFixedUnicode(reader, 32);
            ReadFixedUnicode(reader, 64);
            ReadFixedUnicode(reader, 32);
            reader.ReadBytes(24);
            reader.ReadBytes(12);
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(IhFont);
            writer.Write((int)Font.Height);
            writer.Write((int)Font.Width);
            writer.Write((int)Font.Escapement);
            writer.Write((int)Font.Orientation);
            writer.Write((int)Font.Weight);
            writer.Write(Font.Italic);
            writer.Write(Font.Underline);
            writer.Write(Font.StrikeOut);
            writer.Write((byte)Font.CharSet);
            writer.Write((byte)Font.OutPrecision);
            writer.Write((byte)Font.ClipPrecision);
            writer.Write((byte)FontQuality.CLEARTYPE_QUALITY);
            writer.Write(Font.PitchAndFamily);
            WriteFixedUnicode(writer, Font.Facename, 32);
            WriteFixedUnicode(writer, string.Empty, 64);
            WriteFixedUnicode(writer, string.Empty, 32);
            WriteFixedUnicode(writer, string.Empty, 32);
            writer.Write(new byte[] { 0x64, 0x76, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00 });
        }

        private static short ClampToInt16(int value)
            => (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, value));

        private static string ReadFixedUnicode(BinaryReader reader, int charCount)
        {
            byte[] bytes = reader.ReadBytes(charCount * 2);
            string value = Encoding.Unicode.GetString(bytes);
            int terminator = value.IndexOf('\0');
            return terminator >= 0 ? value.Substring(0, terminator) : value;
        }

        private static void WriteFixedUnicode(BinaryWriter writer, string value, int charCount)
        {
            var bytes = new byte[charCount * 2];
            if (!string.IsNullOrEmpty(value))
            {
                int length = Math.Min(value.Length, charCount - 1);
                byte[] encoded = Encoding.Unicode.GetBytes(value.Substring(0, length));
                Buffer.BlockCopy(encoded, 0, bytes, 0, encoded.Length);
            }
            writer.Write(bytes);
        }
    }
}
