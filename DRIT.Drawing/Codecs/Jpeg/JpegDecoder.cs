using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Codecs.Jpeg
{
    internal sealed class JpegDecoder
    {
        private readonly QuantizationTable?[] _quantTables = new QuantizationTable[4];
        private readonly HuffmanTable?[] _dcTables = new HuffmanTable[4];
        private readonly HuffmanTable?[] _acTables = new HuffmanTable[4];

        private FrameHeader? _frame;
        private JfifHeader? _jfif;
        private int _adobeTransform = -1;
        private int _restartInterval;
        private bool _allowDefaultHuffmanTables;
        private bool _sawFrame;
        private bool _sawScan;
        private bool _sawExifApp1;
        private bool _strict;
        private int[][]? _progression;     // per component: Al last used for each coefficient, -1 = not coded yet
        private readonly bool[] _definedQuantTables = new bool[4];
        private readonly bool[] _definedDcTables = new bool[4];
        private readonly bool[] _definedAcTables = new bool[4];

        public Bitmap Decode(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            _strict = DecodePolicy.Strict;
            var reader = new JpegReader(stream, _strict);
            CoefficientBuffer? progressiveBuffer = null;
            ProgressiveDecoder? progressiveDecoder = null;
            var progressiveScans = new List<ScanHeader>();

            var (firstMarker, _) = reader.ReadMarker();
            if (firstMarker != JpegMarker.SOI)
                throw new InvalidDataException("Invalid JPEG stream: SOI marker missing.");

            while (true)
            {
                JpegMarker marker;
                byte[] payload;
                try
                {
                    (marker, payload) = reader.ReadMarker();
                }
                catch (EndOfStreamException) when (!_strict && _sawScan)
                {
                    // The stream ended after at least one scan without an EOI marker; treat it as the end.
                    marker = JpegMarker.EOI;
                    payload = Array.Empty<byte>();
                }

                if (marker == JpegMarker.SOI)
                    throw new InvalidDataException("Unexpected SOI marker inside a JPEG image.");
                if (marker == JpegMarker.RST0 || marker == JpegMarker.RST1 || marker == JpegMarker.RST2
                    || marker == JpegMarker.RST3 || marker == JpegMarker.RST4 || marker == JpegMarker.RST5
                    || marker == JpegMarker.RST6 || marker == JpegMarker.RST7)
                    throw new InvalidDataException("Restart markers are only valid inside entropy-coded data.");
                if (IsUnsupportedFrameMarker(marker))
                    throw new NotSupportedException($"JPEG frame marker {marker} is not supported.");

                switch (marker)
                {
                    case JpegMarker.APP0:
                        if (HasPrefix(payload, "JFXX\0"))
                        {
                            // Optional JFIF extension (embedded thumbnail). The pixels do not depend on it.
                            if (_strict)
                                throw new NotSupportedException("JFXX JPEG thumbnails are not supported.");
                            break;
                        }
                        _jfif = JfifHeader.TryParse(payload) ?? _jfif;
                        if (HasPrefix(payload, "AVI1"))
                        {
                            _allowDefaultHuffmanTables = true;
                            EnsureDefaultHuffmanTables();
                        }
                        break;

                    case JpegMarker.APP14:
                        if (payload.Length >= 12
                            && payload[0] == (byte)'A' && payload[1] == (byte)'d'
                            && payload[2] == (byte)'o' && payload[3] == (byte)'b' && payload[4] == (byte)'e')
                        {
                            _adobeTransform = payload[11];
                        }
                        break;

                    case JpegMarker.APP1:
                        // EXIF/XMP never influence the decoded pixels, so a damaged block is not a reason to
                        // refuse the image. Validation is a conformance check.
                        if (_strict)
                            _sawExifApp1 = ValidateApp1(payload) || _sawExifApp1;
                        break;

                    case JpegMarker.DQT:
                        foreach (var table in QuantizationTable.ParseDqt(payload))
                        {
                            if (_strict && table.HasZeroCoefficient)
                                throw new InvalidDataException("DQT coefficients must be non-zero.");
                            if (_strict && !_sawScan && _definedQuantTables[table.Id])
                                throw new InvalidDataException($"JPEG quantization table {table.Id} is defined more than once before the first scan.");
                            _definedQuantTables[table.Id] = true;
                            _quantTables[table.Id] = table;
                        }
                        break;

                    case JpegMarker.DHT:
                        foreach (var table in HuffmanTable.ParseDht(payload, allowEmpty: !_strict))
                        {
                            bool[] defined = table.Class == 0 ? _definedDcTables : _definedAcTables;
                            if (_strict && !_sawScan && defined[table.Id])
                                throw new InvalidDataException($"JPEG Huffman table {table.Class}:{table.Id} is defined more than once before the first scan.");
                            defined[table.Id] = true;
                            if (table.Class == 0)
                                _dcTables[table.Id] = table;
                            else if (table.Class == 1)
                                _acTables[table.Id] = table;
                        }
                        break;

                    case JpegMarker.DRI:
                        if (payload.Length != 2)
                            throw new InvalidDataException("DRI segment must contain exactly two bytes.");
                        _restartInterval = (payload[0] << 8) | payload[1];
                        break;

                    case JpegMarker.SOF0:
                    case JpegMarker.SOF1:
                        if (_sawFrame || _sawScan)
                            throw new InvalidDataException("JPEG contains an unexpected second frame header.");
                        _frame = FrameHeader.Parse(marker, payload);
                        _sawFrame = true;
                        progressiveBuffer = null;
                        progressiveDecoder = null;
                        break;

                    case JpegMarker.SOF2:
                        if (_sawFrame || _sawScan)
                            throw new InvalidDataException("JPEG contains an unexpected second frame header.");
                        _frame = FrameHeader.Parse(marker, payload);
                        _sawFrame = true;
                        progressiveBuffer = new CoefficientBuffer(_frame);
                        _progression = new int[_frame.Components.Length][];
                        for (int c = 0; c < _progression.Length; c++)
                        {
                            _progression[c] = new int[64];
                            for (int k = 0; k < 64; k++)
                                _progression[c][k] = -1;
                        }
                        progressiveDecoder = new ProgressiveDecoder(_frame, _quantTables, _dcTables, _acTables);
                        break;

                    case JpegMarker.SOS:
                        if (_frame == null)
                            throw new InvalidDataException("SOS encountered before SOF marker.");
                        if (_frame.Type != SofType.Progressive && _sawScan)
                            throw new InvalidDataException("Sequential JPEG contains more than one scan.");

                        _sawScan = true;

                        if (_frame.Type == SofType.Progressive)
                        {
                            if (progressiveBuffer == null || progressiveDecoder == null)
                                throw new InvalidDataException("Progressive decoder state is not initialized.");

                            var scan = ScanHeader.Parse(payload);
                            ValidateScanReferences(scan);
                            ValidateProgressiveStructure(scan);
                            if (_strict)
                                ValidateProgression(scan);
                            try
                            {
                                byte[] entropyData = reader.ReadEntropyData(MaxEntropySegments(_frame), !_strict);
                                var entropyReader = new BitReader(entropyData);
                                progressiveDecoder.DecodeScan(scan, entropyReader, progressiveBuffer, _restartInterval);
                                // Padding bits and bytes after the final symbol are a conformance matter.
                                if (_strict)
                                    entropyReader.EnsureOnlyPaddingRemains();
                            }
                            catch (EndOfStreamException) when (!_strict)
                            {
                                // Compatibility mode preserves the historical behavior for
                                // marker-terminated partial progressive JPEGs. Corpus probes
                                // enable strict mode, so malformed corpus inputs cannot use
                                // this policy to produce a decoded image.
                            }
                            catch (InvalidDataException ex)
                                when (!_strict && ex.InnerException is EndOfStreamException)
                            {
                                // ProgressiveDecoder adds scan context around entropy EOF.
                            }
                            progressiveScans.Add(scan);
                            break;
                        }

                        return DecodeSequentialScan(reader, payload);

                    case JpegMarker.EOI:
                        if (_frame != null && _frame.Type == SofType.Progressive)
                        {
                            if (!_sawScan)
                                throw new InvalidDataException("JPEG EOI encountered before SOS.");
                            if (progressiveBuffer == null || progressiveDecoder == null)
                                throw new InvalidDataException("Progressive decoder state is not initialized.");

                            EntropyDecodeResult imageData = progressiveDecoder.BuildImageData(progressiveBuffer);
                            Bitmap progressiveBitmap = BuildBitmap(_frame, imageData);
                            progressiveBitmap._rawFormat = ImageFormat.Jpeg;
                            ApplyJfifDensity(progressiveBitmap, _jfif);
                            return progressiveBitmap;
                        }

                        throw new InvalidDataException("Invalid JPEG stream: no SOS marker found.");

                    case JpegMarker.COM:
                        break;

                    case JpegMarker.APP2:
                    case JpegMarker.APP3:
                    case JpegMarker.APP4:
                    case JpegMarker.APP5:
                    case JpegMarker.APP6:
                    case JpegMarker.APP7:
                    case JpegMarker.APP8:
                    case JpegMarker.APP9:
                    case JpegMarker.APP10:
                    case JpegMarker.APP11:
                    case JpegMarker.APP12:
                    case JpegMarker.APP13:
                    case JpegMarker.APP15:
                        break;

                    default:
                        // Unknown length-bearing markers (DNL, JPGn, ...) were already consumed with their payload.
                        if (_strict)
                            throw new InvalidDataException($"Unsupported or invalid JPEG marker 0x{(byte)marker:X2}.");
                        break;
                }
            }
        }

        private Bitmap DecodeSequentialScan(JpegReader reader, byte[] sosPayload)
        {
            if (_frame == null)
                throw new InvalidDataException("SOS encountered before SOF marker.");

            var scan = ScanHeader.Parse(sosPayload);

            if (scan.Ss != 0 || scan.Se != 63 || scan.Ah != 0 || scan.Al != 0)
                throw new InvalidDataException("Sequential JPEG SOS has invalid spectral or approximation parameters.");

            if (_frame.Components.Length > 1 && scan.Components.Length != _frame.Components.Length)
                throw new NotSupportedException("Non-interleaved sequential JPEG scans are not supported yet.");

            var entropyDecoder = new EntropyDecoder(
                _frame,
                scan,
                _quantTables,
                _dcTables,
                _acTables,
                _restartInterval);

            IReadOnlyList<byte[]> entropySegments = reader.ReadEntropySegments(MaxEntropySegments(_frame), !_strict);
            if (_restartInterval == 0 && entropySegments.Count != 1)
                throw new InvalidDataException("JPEG restart markers require a non-zero restart interval.");
            EntropyDecodeResult entropy = entropyDecoder.Decode(entropySegments);
            if (_strict)
            {
                var (endMarker, _) = reader.ReadMarker();
                if (endMarker != JpegMarker.EOI)
                    throw new InvalidDataException("Sequential JPEG scan was not terminated by EOI.");
            }
            Bitmap bitmap = BuildBitmap(_frame, entropy);
            bitmap._rawFormat = ImageFormat.Jpeg;
            ApplyJfifDensity(bitmap, _jfif);
            return bitmap;
        }

        private void ValidateScanReferences(ScanHeader scan)
        {
            if (_frame == null)
                throw new InvalidDataException("SOS encountered before SOF marker.");

            for (int i = 0; i < scan.Components.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < _frame.Components.Length; j++)
                {
                    if (_frame.Components[j].Id == scan.Components[i].ComponentId)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    throw new InvalidDataException($"SOS references unknown component id {scan.Components[i].ComponentId}.");
            }
        }

        /// <summary>
        /// Rules every progressive scan must satisfy for the decoder to interpret it at all (libjpeg treats a
        /// violation as fatal as well): a DC scan covers only coefficient 0, an AC scan never includes it and has
        /// exactly one component.
        /// </summary>
        private static void ValidateProgressiveStructure(ScanHeader scan)
        {
            if (scan.Ss == 0 && scan.Se != 0)
                throw new InvalidDataException("Progressive DC scans must have Se=0.");
            if (scan.Ss > 0 && scan.Se == 0)
                throw new InvalidDataException("Progressive AC scans must have Ss>0 and Se>0.");
            if (scan.Components.Length > 1 && scan.Ss != 0)
                throw new InvalidDataException("Progressive AC scans may contain only one component.");
        }

        /// <summary>
        /// Strict conformance only: successive approximation must be consistent. For every coefficient a first scan
        /// (Ah = 0) may appear once, and each refinement scan must continue exactly where the previous scan for that
        /// coefficient stopped (its Ah equals the Al that was last used). libjpeg merely warns about violations
        /// (JWRN_BOGUS_PROGRESSION) and decodes on, so lenient decoding does too.
        /// </summary>
        private void ValidateProgression(ScanHeader scan)
        {
            if (_frame == null || _progression == null)
                throw new InvalidDataException("Progressive decoder state is not initialized.");

            foreach (var scanComponent in scan.Components)
            {
                int frameIndex = Array.FindIndex(_frame.Components, c => c.Id == scanComponent.ComponentId);
                int[] state = _progression[frameIndex];
                for (int k = scan.Ss; k <= scan.Se; k++)
                {
                    if (scan.Ah == 0)
                    {
                        if (state[k] != -1)
                            throw new InvalidDataException($"Progressive JPEG codes coefficient {k} of component {scanComponent.ComponentId} more than once.");
                    }
                    else if (state[k] != scan.Ah)
                    {
                        throw new InvalidDataException(
                            $"Progressive JPEG refines coefficient {k} of component {scanComponent.ComponentId} from bit {scan.Ah}, "
                            + $"but it was last coded at bit {state[k]}.");
                    }

                    state[k] = scan.Al;
                }
            }
        }

        private void EnsureDefaultHuffmanTables()
        {
            if (!_allowDefaultHuffmanTables)
                return;

            _dcTables[0] ??= HuffmanTable.Create(0, 0, HuffmanEncoder.DcLuminanceCounts, HuffmanEncoder.DcLuminanceValues);
            _dcTables[1] ??= HuffmanTable.Create(0, 1, HuffmanEncoder.DcChrominanceCounts, HuffmanEncoder.DcChrominanceValues);
            _acTables[0] ??= HuffmanTable.Create(1, 0, HuffmanEncoder.AcLuminanceCounts, HuffmanEncoder.AcLuminanceValues);
            _acTables[1] ??= HuffmanTable.Create(1, 1, HuffmanEncoder.AcChrominanceCounts, HuffmanEncoder.AcChrominanceValues);
        }

        private static bool HasPrefix(byte[] data, string text)
        {
            if (data.Length < text.Length)
                return false;
            for (int i = 0; i < text.Length; i++)
            {
                if (data[i] != (byte)text[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Largest number of restart intervals a scan of this frame can have: an interval holds at least one
        /// MCU (or, for non-interleaved progressive scans, one block), and no component has more blocks than
        /// <c>MCUs x H x V</c>.
        /// </summary>
        private static int MaxEntropySegments(FrameHeader frame)
        {
            int maxH = 1, maxV = 1;
            foreach (var component in frame.Components)
            {
                maxH = Math.Max(maxH, component.HSampling);
                maxV = Math.Max(maxV, component.VSampling);
            }

            long mcusX = (frame.Width + 8L * maxH - 1) / (8L * maxH);
            long mcusY = (frame.Height + 8L * maxV - 1) / (8L * maxV);
            return (int)Math.Min(int.MaxValue, mcusX * mcusY * maxH * maxV);
        }

        private bool ValidateApp1(byte[] payload)
        {
            if (HasPrefix(payload, "Exif\0\0"))
            {
                if (payload.Length < 14)
                    throw new InvalidDataException("EXIF APP1 segment is truncated.");
                bool littleEndian = payload[6] == (byte)'I' && payload[7] == (byte)'I';
                bool bigEndian = payload[6] == (byte)'M' && payload[7] == (byte)'M';
                if (!littleEndian && !bigEndian)
                    throw new InvalidDataException("EXIF APP1 byte order is invalid.");
                int magic = littleEndian ? payload[8] | (payload[9] << 8) : (payload[8] << 8) | payload[9];
                if (magic != 42)
                    throw new InvalidDataException("EXIF APP1 TIFF magic is invalid.");
                return true;
            }

            const string xmpPrefix = "http://ns.adobe.com/xap/1.0/\0";
            if (!HasPrefix(payload, xmpPrefix))
                return false;
            if (!_sawExifApp1)
                throw new InvalidDataException("XMP APP1 metadata requires a preceding EXIF APP1 segment.");

            int xmlOffset = xmpPrefix.Length;
            string xmp = Encoding.UTF8.GetString(payload, xmlOffset, payload.Length - xmlOffset);
            int xmlStart = xmp.IndexOf("?>", StringComparison.Ordinal);
            int xmlEnd = xmp.LastIndexOf("<?xpacket end=", StringComparison.Ordinal);
            if (xmlStart < 0 || xmlEnd <= xmlStart)
                throw new InvalidDataException("XMP APP1 packet header is invalid.");

            string xml = xmp.Substring(xmlStart + 2, xmlEnd - xmlStart - 2).Trim();
            try
            {
                using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = 1_000_000,
                });
                while (reader.Read()) { }
            }
            catch (XmlException ex)
            {
                throw new InvalidDataException("XMP APP1 XML is malformed.", ex);
            }
            return false;
        }

        private static bool IsUnsupportedFrameMarker(JpegMarker marker) =>
            marker == JpegMarker.SOF3 || marker == JpegMarker.SOF5 || marker == JpegMarker.SOF6
            || marker == JpegMarker.SOF9 || marker == JpegMarker.SOF10 || marker == JpegMarker.SOF11;

        private Bitmap BuildBitmap(FrameHeader frame, EntropyDecodeResult entropy)
        {
            var bmp = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppArgb);

            int componentCount = frame.Components.Length;
            if (componentCount == 1)
            {
                var c = frame.Components[0];
                byte[] yPlane = UpsampleBuffer.UpsampleToFullResolution(
                    entropy.Planes[0],
                    entropy.PlaneWidths[0],
                    entropy.PlaneHeights[0],
                    frame.Width,
                    frame.Height,
                    c.HSampling,
                    c.VSampling,
                    entropy.MaxHorizontalSampling,
                    entropy.MaxVerticalSampling);

                for (int y = 0; y < frame.Height; y++)
                {
                    int srcRow = y * frame.Width;
                    int dstRow = y * bmp._stride;
                    for (int x = 0; x < frame.Width; x++)
                    {
                        ColorConverter.GrayscaleToRgb(yPlane[srcRow + x], out byte r, out byte g, out byte b);
                        int o = dstRow + x * 4;
                        bmp._pixels[o] = b;
                        bmp._pixels[o + 1] = g;
                        bmp._pixels[o + 2] = r;
                        bmp._pixels[o + 3] = 255;
                    }
                }

                return bmp;
            }

            if (componentCount == 4)
            {
                var planes = new byte[4][];
                for (var component = 0; component < 4; component++)
                {
                    var frameComponent = frame.Components[component];
                    planes[component] = UpsampleBuffer.UpsampleToFullResolution(
                        entropy.Planes[component],
                        entropy.PlaneWidths[component],
                        entropy.PlaneHeights[component],
                        frame.Width,
                        frame.Height,
                        frameComponent.HSampling,
                        frameComponent.VSampling,
                        entropy.MaxHorizontalSampling,
                        entropy.MaxVerticalSampling);
                }

                for (var y = 0; y < frame.Height; y++)
                {
                    var srcRow = y * frame.Width;
                    var dstRow = y * bmp._stride;
                    for (var x = 0; x < frame.Width; x++)
                    {
                        var src = srcRow + x;
                        var c = planes[0][src];
                        var m = planes[1][src];
                        var yy = planes[2][src];
                        var k = planes[3][src];
                        byte r;
                        byte g;
                        byte b;
                        if (_adobeTransform == 2)
                        {
                            ColorConverter.YcckToRgb(c, m, yy, k, out r, out g, out b);
                        }
                        else if (_adobeTransform >= 0)
                        {
                            // Adobe CMYK is stored inverted.
                            ColorConverter.AdobeCmykToRgb(c, m, yy, k, out r, out g, out b);
                        }
                        else
                        {
                            // Without an Adobe marker the four components are plain ink amounts.
                            r = ColorConverter.CmykChannelToRgb(c, k);
                            g = ColorConverter.CmykChannelToRgb(m, k);
                            b = ColorConverter.CmykChannelToRgb(yy, k);
                        }

                        var dst = dstRow + x * 4;
                        bmp._pixels[dst] = b;
                        bmp._pixels[dst + 1] = g;
                        bmp._pixels[dst + 2] = r;
                        bmp._pixels[dst + 3] = 255;
                    }
                }
                return bmp;
            }

            if (componentCount < 3)
                throw new NotSupportedException("JPEG with two components is not supported.");

            // Components are labelled Y, Cb, Cr with ids 1, 2, 3 in JFIF files; encoders that use other labels
            // (0-based, 'R','G','B', ...) still write them in Y, Cb, Cr order.
            bool hasStandardIds = HasComponentId(frame, 1) && HasComponentId(frame, 2) && HasComponentId(frame, 3);
            if (IsRgbColorSpace(frame))
            {
                byte[] red = Upsample(frame, entropy, 0, frame.Width, frame.Height);
                byte[] green = Upsample(frame, entropy, 1, frame.Width, frame.Height);
                byte[] blue = Upsample(frame, entropy, 2, frame.Width, frame.Height);
                for (int y = 0; y < frame.Height; y++)
                {
                    int srcRow = y * frame.Width;
                    int dstRow = y * bmp._stride;
                    for (int x = 0; x < frame.Width; x++)
                    {
                        int dst = dstRow + x * 4;
                        bmp._pixels[dst] = blue[srcRow + x];
                        bmp._pixels[dst + 1] = green[srcRow + x];
                        bmp._pixels[dst + 2] = red[srcRow + x];
                        bmp._pixels[dst + 3] = 255;
                    }
                }
                return bmp;
            }

            int yIndex = hasStandardIds ? ResolveComponentIndex(frame, 1, 0) : 0;
            int cbIndex = hasStandardIds ? ResolveComponentIndex(frame, 2, 1) : 1;
            int crIndex = hasStandardIds ? ResolveComponentIndex(frame, 3, 2) : 2;

            byte[] fullY = Upsample(frame, entropy, yIndex, frame.Width, frame.Height);
            byte[] fullCb = Upsample(frame, entropy, cbIndex, frame.Width, frame.Height);
            byte[] fullCr = Upsample(frame, entropy, crIndex, frame.Width, frame.Height);

            for (int y = 0; y < frame.Height; y++)
            {
                int srcRow = y * frame.Width;
                int dstRow = y * bmp._stride;
                for (int x = 0; x < frame.Width; x++)
                {
                    int src = srcRow + x;
                    ColorConverter.YCbCrToRgb(fullY[src], fullCb[src], fullCr[src], out byte r, out byte g, out byte b);

                    int dst = dstRow + x * 4;
                    bmp._pixels[dst] = b;
                    bmp._pixels[dst + 1] = g;
                    bmp._pixels[dst + 2] = r;
                    bmp._pixels[dst + 3] = 255;
                }
            }

            return bmp;
        }

        private static byte[] Upsample(FrameHeader frame, EntropyDecodeResult entropy, int componentIndex, int width, int height)
        {
            var component = frame.Components[componentIndex];
            return UpsampleBuffer.UpsampleToFullResolution(
                entropy.Planes[componentIndex],
                entropy.PlaneWidths[componentIndex],
                entropy.PlaneHeights[componentIndex],
                width,
                height,
                component.HSampling,
                component.VSampling,
                entropy.MaxHorizontalSampling,
                entropy.MaxVerticalSampling);
        }

        /// <summary>
        /// Three-component data is RGB (no colour transform) when an Adobe marker says transform 0, or, without any
        /// Adobe/JFIF marker, when the components are labelled 'R', 'G', 'B'. Everything else is YCbCr.
        /// </summary>
        private bool IsRgbColorSpace(FrameHeader frame)
        {
            if (frame.Components.Length != 3)
                return false;
            if (_adobeTransform == 0)
                return true;
            return _adobeTransform < 0 && _jfif == null
                && frame.Components[0].Id == (byte)'R' && frame.Components[1].Id == (byte)'G' && frame.Components[2].Id == (byte)'B';
        }

        private static bool HasComponentId(FrameHeader frame, byte componentId)
        {
            for (int i = 0; i < frame.Components.Length; i++)
            {
                if (frame.Components[i].Id == componentId)
                    return true;
            }
            return false;
        }

        private static int ResolveComponentIndex(FrameHeader frame, byte componentId, int fallbackIndex)
        {
            for (int i = 0; i < frame.Components.Length; i++)
            {
                if (frame.Components[i].Id == componentId)
                    return i;
            }

            return Math.Min(frame.Components.Length - 1, Math.Max(0, fallbackIndex));
        }

        private static void ApplyJfifDensity(Bitmap bmp, JfifHeader? jfif)
        {
            if (jfif == null)
                return;

            if (jfif.DensityUnits == 1)
            {
                if (jfif.XDensity > 0) bmp._dpiX = jfif.XDensity;
                if (jfif.YDensity > 0) bmp._dpiY = jfif.YDensity;
            }
            else if (jfif.DensityUnits == 2)
            {
                if (jfif.XDensity > 0) bmp._dpiX = (float)(jfif.XDensity * 2.54);
                if (jfif.YDensity > 0) bmp._dpiY = (float)(jfif.YDensity * 2.54);
            }
        }
    }
}
