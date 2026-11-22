using System;
using System.IO;

namespace DRIT.Drawing.Codecs.Jpeg
{
    /// <summary>
    /// Parsed payload of a JPEG SOS (Start-of-Scan) marker.
    /// </summary>
    internal sealed class ScanHeader
    {
        /// <summary>Components included in this scan.</summary>
        public ScanComponent[] Components { get; private set; } = Array.Empty<ScanComponent>();

        /// <summary>Start of spectral selection (0 for sequential).</summary>
        public int Ss { get; private set; }

        /// <summary>End of spectral selection (63 for sequential).</summary>
        public int Se { get; private set; }

        /// <summary>Successive approximation high (0 for first/only pass).</summary>
        public int Ah { get; private set; }

        /// <summary>Successive approximation low (0 for sequential).</summary>
        public int Al { get; private set; }

        // ── Factory ──────────────────────────────────────────────────────────────

        /// <summary>Parses a SOS segment payload.</summary>
        public static ScanHeader Parse(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length < 4)
                throw new InvalidDataException("SOS segment too short.");

            int ns = data[0];
            if (ns < 1 || ns > 4)
                throw new InvalidDataException("SOS component count is out of range.");
            if (data.Length != 1 + ns * 2 + 3)
                throw new InvalidDataException("SOS segment length does not match its component count.");

            var sh = new ScanHeader();
            sh.Components = new ScanComponent[ns];
            for (int i = 0; i < ns; i++)
            {
                int off  = 1 + i * 2;
                byte sel = data[off + 1];
                int dcTable = (sel >> 4) & 0xF;
                int acTable = sel & 0xF;
                if (dcTable > 3 || acTable > 3)
                    throw new InvalidDataException("SOS Huffman table identifier is out of range.");
                for (int previous = 0; previous < i; previous++)
                {
                    if (sh.Components[previous].ComponentId == data[off])
                        throw new InvalidDataException("SOS contains duplicate component identifiers.");
                }
                sh.Components[i] = new ScanComponent
                {
                    ComponentId = data[off],
                    DcTableId   = (byte)dcTable,
                    AcTableId   = (byte)acTable,
                };
            }

            int tail = 1 + ns * 2;
            sh.Ss = data[tail];
            sh.Se = data[tail + 1];
            byte ahAl = data[tail + 2];
            sh.Ah = (ahAl >> 4) & 0xF;
            sh.Al =  ahAl       & 0xF;
            if (sh.Ss > 63 || sh.Se > 63 || sh.Ss > sh.Se)
                throw new InvalidDataException("SOS spectral selection range is invalid.");
            if (sh.Ah > 13 || sh.Al > 13)
                throw new InvalidDataException("SOS successive approximation is out of range.");
            if (sh.Ss == 0 && sh.Se == 0 && sh.Ah != 0 && sh.Ah != sh.Al + 1)
                throw new InvalidDataException("SOS DC successive approximation is invalid.");
            if (sh.Ss != 0 && sh.Ah != 0 && sh.Ah != sh.Al + 1)
                throw new InvalidDataException("SOS AC successive approximation is invalid.");
            return sh;
        }
    }
}
