using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DRIT.Drawing.Metafile.EmfPlus;
using DRIT.Drawing.Metafile.EmfPlus.Records;

namespace DRIT.Drawing.Metafile
{
    /// <summary>Identifies the rendered unit reconstructed from an EMF record sequence.</summary>
    public enum EmfSemanticUnitKind
    {
        /// <summary>Text output with its effective font and text color.</summary>
        Text,

        /// <summary>A direct vector geometry operation such as a polygon or ellipse.</summary>
        Shape,

        /// <summary>A path assembled from path construction records and a terminal path operation.</summary>
        Path,

        /// <summary>A raster image operation embedded in the EMF stream.</summary>
        RasterImage,
    }

    /// <summary>Represents an RGB color reconstructed from an EMF COLORREF.</summary>
    public sealed class EmfColor
    {
        /// <summary>
        /// Initializes an empty <see cref="EmfColor"/> instance.
        /// </summary>
        public EmfColor()
        {
        }

        /// <summary>Gets the red channel.</summary>
        public byte R { get; internal set; }

        /// <summary>Gets the green channel.</summary>
        public byte G { get; internal set; }

        /// <summary>Gets the blue channel.</summary>
        public byte B { get; internal set; }

        /// <summary>Returns the color as a hexadecimal RGB value.</summary>
        public override string ToString()
        {
            return $"#{R:X2}{G:X2}{B:X2}";
        }
    }

    /// <summary>Represents an effective EMF world transform.</summary>
    public sealed class EmfTransform
    {
        /// <summary>
        /// Initializes an empty <see cref="EmfTransform"/> instance.
        /// </summary>
        public EmfTransform()
        {
        }

        /// <summary>Gets the first horizontal matrix component.</summary>
        public float M11 { get; internal set; } = 1f;

        /// <summary>Gets the first vertical matrix component.</summary>
        public float M12 { get; internal set; }

        /// <summary>Gets the second horizontal matrix component.</summary>
        public float M21 { get; internal set; }

        /// <summary>Gets the second vertical matrix component.</summary>
        public float M22 { get; internal set; } = 1f;

        /// <summary>Gets the horizontal translation.</summary>
        public float Dx { get; internal set; }

        /// <summary>Gets the vertical translation.</summary>
        public float Dy { get; internal set; }

        /// <summary>Returns the affine matrix in readable form.</summary>
        public override string ToString()
        {
            return $"[{M11},{M12};{M21},{M22};{Dx},{Dy}]";
        }
    }

    /// <summary>Represents a normalized point in the EMF canvas.</summary>
    public sealed class EmfNormalizedPoint
    {
        /// <summary>
        /// Initializes an empty <see cref="EmfNormalizedPoint"/> instance.
        /// </summary>
        public EmfNormalizedPoint()
        {
        }

        /// <summary>Gets the horizontal coordinate normalized to the canvas width.</summary>
        public double X { get; internal set; }

        /// <summary>Gets the vertical coordinate normalized to the canvas height.</summary>
        public double Y { get; internal set; }

        /// <summary>Returns the normalized coordinate pair.</summary>
        public override string ToString()
        {
            return $"({X.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},{Y.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)})";
        }
    }

    /// <summary>Represents a normalized rectangle in the EMF canvas.</summary>
    public sealed class EmfNormalizedRectangle
    {
        /// <summary>
        /// Initializes an empty <see cref="EmfNormalizedRectangle"/> instance.
        /// </summary>
        public EmfNormalizedRectangle()
        {
        }

        /// <summary>Gets the normalized left coordinate.</summary>
        public double Left { get; internal set; }

        /// <summary>Gets the normalized top coordinate.</summary>
        public double Top { get; internal set; }

        /// <summary>Gets the normalized right coordinate.</summary>
        public double Right { get; internal set; }

        /// <summary>Gets the normalized bottom coordinate.</summary>
        public double Bottom { get; internal set; }

        /// <summary>Returns the normalized rectangle.</summary>
        public override string ToString()
        {
            return $"({Left.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},{Top.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)})-({Right.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)},{Bottom.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)})";
        }
    }

    /// <summary>Contains the effective LOGFONTW values selected for an EMF text unit.</summary>
    public sealed class EmfFontInfo
    {
        /// <summary>
        /// Initializes an empty <see cref="EmfFontInfo"/> instance.
        /// </summary>
        public EmfFontInfo()
        {
        }

        /// <summary>Gets the EMF object handle.</summary>
        public uint ObjectHandle { get; internal set; }

        /// <summary>Gets the logical font height in EMF units.</summary>
        public int Height { get; internal set; }

        /// <summary>Gets the logical font width in EMF units.</summary>
        public int Width { get; internal set; }

        /// <summary>Gets the character escapement angle in tenths of a degree.</summary>
        public int Escapement { get; internal set; }

        /// <summary>Gets the character orientation angle in tenths of a degree.</summary>
        public int Orientation { get; internal set; }

        /// <summary>Gets the font weight.</summary>
        public int Weight { get; internal set; }

        /// <summary>Gets whether the font is italic.</summary>
        public bool Italic { get; internal set; }

        /// <summary>Gets whether the font is underlined.</summary>
        public bool Underline { get; internal set; }

        /// <summary>Gets whether the font is struck out.</summary>
        public bool StrikeOut { get; internal set; }

        /// <summary>Gets the character set identifier.</summary>
        public byte CharSet { get; internal set; }

        /// <summary>Gets the face name stored in the LOGFONTW structure.</summary>
        public string FaceName { get; internal set; } = string.Empty;

        /// <summary>Returns the key font identity and metrics.</summary>
        public override string ToString()
        {
            return $"{FaceName} height={Height} width={Width} weight={Weight} italic={Italic}";
        }
    }

    /// <summary>Represents one effective drawing unit reconstructed from an EMF stream.</summary>
    public sealed class EmfSemanticUnit
    {
        /// <summary>
        /// Initializes an empty <see cref="EmfSemanticUnit"/> instance.
        /// </summary>
        public EmfSemanticUnit()
        {
        }

        /// <summary>Gets the source EMF record index.</summary>
        public int RecordIndex { get; internal set; }

        /// <summary>Gets the semantic unit category.</summary>
        public EmfSemanticUnitKind Kind { get; internal set; }

        /// <summary>Gets the terminal EMF operation that produced the unit.</summary>
        public EmfOperationKind OperationKind { get; internal set; }

        /// <summary>Gets the raw logical bounds from the terminal EMF operation.</summary>
        public EmfRectangle Bounds { get; internal set; }

        /// <summary>Gets the path or geometry points carried by the unit.</summary>
        public IReadOnlyList<EmfPoint> Points { get; internal set; } = Array.Empty<EmfPoint>();

        /// <summary>Gets the points transformed and normalized to the EMF canvas.</summary>
        public IReadOnlyList<EmfNormalizedPoint> NormalizedPoints { get; internal set; } = Array.Empty<EmfNormalizedPoint>();

        /// <summary>Gets the raw bounds normalized to the EMF canvas.</summary>
        public EmfNormalizedRectangle NormalizedBounds { get; internal set; }

        /// <summary>Gets the number of polygons when the operation carries that value.</summary>
        public int? PolygonCount { get; internal set; }

        /// <summary>Gets the text when the unit is a text operation.</summary>
        public string Text { get; internal set; }

        /// <summary>Gets the effective selected font for a text unit.</summary>
        public EmfFontInfo Font { get; internal set; }

        /// <summary>Gets the effective text color for a text unit.</summary>
        public EmfColor TextColor { get; internal set; }

        /// <summary>Gets the effective selected pen color, when known.</summary>
        public EmfColor StrokeColor { get; internal set; }

        /// <summary>Gets the effective selected pen width in logical EMF units.</summary>
        public int StrokeWidth { get; internal set; }

        /// <summary>Gets the effective selected brush color, when known.</summary>
        public EmfColor FillColor { get; internal set; }

        /// <summary>Gets the effective world transform at the output record.</summary>
        public EmfTransform Transform { get; internal set; }

        /// <summary>Gets whether the unit fills its geometry.</summary>
        public bool IsFilled { get; internal set; }

        /// <summary>Gets whether the unit strokes its geometry.</summary>
        public bool IsStroked { get; internal set; }

        /// <summary>Returns a concise semantic description.</summary>
        public override string ToString()
        {
            return $"#{RecordIndex} {Kind} {OperationKind} text={Text ?? "<none>"} bounds={Bounds?.ToString() ?? "<none>"}";
        }
    }

    /// <summary>Replays EMF state records and reconstructs effective drawing units.</summary>
    public sealed class EmfSemanticReader
    {
        /// <summary>
        /// Initializes an empty <see cref="EmfSemanticReader"/> instance.
        /// </summary>
        public EmfSemanticReader()
        {
        }

        private const uint CreateFontIndirectWType = 82;
        private const uint SelectObjectType = 37;
        private const uint CreatePenType = 38;
        private const uint ExtCreatePenType = 95;
        private const uint CreateBrushIndirectType = 39;
        private const uint SaveDcType = 33;
        private const uint RestoreDcType = 34;
        private const uint SetWorldTransformType = 35;
        private const uint ModifyWorldTransformType = 36;
        private const uint SetTextColorType = 24;
        private const uint SetDCPenColorType = 21;
        private const uint SetDCBrushColorType = 22;
        private const uint BeginPathType = 59;
        private const uint MoveToType = 27;
        private const uint LineToType = 54;
        private const uint EndPathType = 60;
        private const uint CloseFigureType = 61;
        private const uint StrokeAndFillPathType = 63;
        private const uint StrokePathType = 64;
        private const uint AbortPathType = 68;
        private const uint StretchDibitsType = 81;

        private const uint HeaderType = 1;

        /// <summary>Reads semantic units from an EMF file.</summary>
        public IReadOnlyList<EmfSemanticUnit> Read(string path, EmfComparisonOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("The path cannot be null or whitespace.", nameof(path));
            using (var stream = File.OpenRead(path))
                return Read(stream, options);
        }

        /// <summary>Reads semantic units from EMF bytes.</summary>
        public IReadOnlyList<EmfSemanticUnit> Read(byte[] bytes, EmfComparisonOptions options = null)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using (var stream = new MemoryStream(bytes, false))
                return Read(stream, options);
        }

        /// <summary>Reads semantic units from a seekable EMF stream.</summary>
        public IReadOnlyList<EmfSemanticUnit> Read(Stream stream, EmfComparisonOptions options = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanSeek) throw new InvalidOperationException("The stream must support seeking.");

            var records = new EmfComparer().Read(stream, options ?? new EmfComparisonOptions());
            EmfRectangle canvasBounds = ReadCanvasBounds(records);
            var state = new ReplayState();
            var units = new List<EmfSemanticUnit>();
            foreach (var record in records)
            {
                ApplyState(record, state);
                AddUnit(record, state, canvasBounds, units);
            }
            AddEmfPlusUnits(records, canvasBounds, units);
            return units;
        }

        private static void AddEmfPlusUnits(IReadOnlyList<EmfRecordSignature> records,
            EmfRectangle canvasBounds, List<EmfSemanticUnit> units)
        {
            foreach (EmfRecordSignature record in records.Where(item => item.Type == 70))
            {
                if (record.Payload == null || record.Payload.Length < 8)
                    continue;

                int commentLength = ReadInt32(record.Payload, 0);
                if (commentLength < 4 || commentLength > record.Payload.Length - 4 ||
                    record.Payload[4] != (byte)'E' || record.Payload[5] != (byte)'M' ||
                    record.Payload[6] != (byte)'F' || record.Payload[7] != (byte)'+')
                    continue;

                var emfPlusObjects = new Dictionary<int, EmfPlusObjectData>();
                var transform = new EmfTransform();
                using (var stream = new MemoryStream(record.Payload, 4, commentLength, false))
                {
                    var reader = new EmfPlusReader();
                    reader.Read(stream);
                    foreach (Record plusRecord in reader.Records)
                    {
                        int objectId = plusRecord.Header.Flags & 0xFF;
                        if (plusRecord is EmfPlusSetWorldTransformRecord setWorldTransform)
                        {
                            transform = new EmfTransform
                            {
                                M11 = setWorldTransform.M11,
                                M12 = setWorldTransform.M12,
                                M21 = setWorldTransform.M21,
                                M22 = setWorldTransform.M22,
                                Dx = setWorldTransform.Dx,
                                Dy = setWorldTransform.Dy,
                            };
                        }
                        else if (plusRecord is EmfPlusObjectRecord objectRecord)
                        {
                            EmfPlusObjectData objectData = ParseEmfPlusObject(objectRecord);
                            if (objectData != null)
                                emfPlusObjects[objectId] = objectData;
                        }
                        else if (plusRecord is EmfPlusDrawPathRecord drawPath &&
                            emfPlusObjects.TryGetValue(drawPath.PathId, out EmfPlusObjectData path))
                        {
                            int penId = (int)drawPath.PenId;
                            if (!emfPlusObjects.TryGetValue(penId, out EmfPlusObjectData pen) ||
                                path.Points == null || path.Points.Count < 2)
                                continue;

                            EmfPoint[] points = TransformPoints(path.Points, transform);
                            EmfRectangle bounds = GetBounds(points);
                            units.Add(new EmfSemanticUnit
                            {
                                RecordIndex = record.Index,
                                Kind = EmfSemanticUnitKind.Path,
                                OperationKind = EmfOperationKind.StrokePath,
                                Bounds = bounds,
                                Points = points,
                                NormalizedPoints = NormalizePoints(points, new EmfTransform(), canvasBounds),
                                NormalizedBounds = NormalizeBounds(bounds, canvasBounds),
                                StrokeColor = pen.Color,
                                StrokeWidth = pen.Width,
                                Transform = transform.Clone(),
                                IsStroked = true,
                            });
                        }
                    }
                }
            }
        }

        private static EmfPlusObjectData ParseEmfPlusObject(EmfPlusObjectRecord objectRecord)
        {
            byte[] data = objectRecord.Data;
            if (data == null || data.Length < 12)
                return null;

            uint signature = ReadUInt32(data, 0);
            if (signature != 0xDBC01002)
                return null;

            if (((objectRecord.Header.Flags >> 8) & 0x7F) == 2)
                return new EmfPlusObjectData
                {
                    Color = ReadArgbColor(data, data.Length - 4),
                    Width = ReadPenWidth(data),
                };

            if (((objectRecord.Header.Flags >> 8) & 0x7F) != 3 || data.Length < 16)
                return null;

            int pointCount = ReadInt32(data, 4);
            if (pointCount < 2 || pointCount > 10000)
                return null;
            uint flags = ReadUInt32(data, 8);
            int pointOffset = 12;
            var points = new List<EmfPoint>(pointCount);
            if ((flags & 0x4000) != 0)
            {
                if (pointOffset + pointCount * 4 > data.Length)
                    return null;
                for (int index = 0; index < pointCount; index++)
                    points.Add(new EmfPoint
                    {
                        X = ReadInt16(data, pointOffset + index * 4),
                        Y = ReadInt16(data, pointOffset + index * 4 + 2),
                    });
            }
            else
            {
                if (pointOffset + pointCount * 8 > data.Length)
                    return null;
                for (int index = 0; index < pointCount; index++)
                    points.Add(new EmfPoint
                    {
                        X = (int)Math.Round(ReadSingle(data, pointOffset + index * 8)),
                        Y = (int)Math.Round(ReadSingle(data, pointOffset + index * 8 + 4)),
                    });
            }
            return new EmfPlusObjectData { Points = points };
        }

        private static int ReadPenWidth(byte[] data)
        {
            if (data.Length < 24)
                return 0;
            return (int)Math.Round(ReadSingle(data, 20));
        }

        private static EmfColor ReadArgbColor(byte[] data, int offset)
        {
            uint argb = ReadUInt32(data, offset);
            return new EmfColor
            {
                R = (byte)((argb >> 16) & 0xFF),
                G = (byte)((argb >> 8) & 0xFF),
                B = (byte)(argb & 0xFF),
            };
        }

        private static EmfRectangle GetBounds(IReadOnlyList<EmfPoint> points)
        {
            return new EmfRectangle
            {
                Left = points.Min(point => point.X),
                Top = points.Min(point => point.Y),
                Right = points.Max(point => point.X),
                Bottom = points.Max(point => point.Y),
            };
        }

        private static EmfPoint[] TransformPoints(IReadOnlyList<EmfPoint> points, EmfTransform transform)
        {
            return points.Select(point => new EmfPoint
            {
                X = (int)Math.Round(point.X * transform.M11 + point.Y * transform.M21 + transform.Dx),
                Y = (int)Math.Round(point.X * transform.M12 + point.Y * transform.M22 + transform.Dy),
            }).ToArray();
        }

        private static void ApplyState(EmfRecordSignature record, ReplayState state)
        {
            byte[] payload = record.Payload;
            switch (record.Type)
            {
                case CreateFontIndirectWType:
                    if (payload.Length >= 32)
                        state.Fonts[ReadUInt32(payload, 0)] = ReadFont(payload);
                    break;
                case SelectObjectType:
                    if (payload.Length >= 4)
                    {
                        uint handle = ReadUInt32(payload, 0);
                        state.SelectedObject = handle;
                        if (state.Fonts.TryGetValue(handle, out var font))
                            state.Font = font;
                        if (state.Paints.TryGetValue(handle, out var paint))
                            state.SelectedPaint = paint;
                    }
                    break;
                case CreatePenType:
                    if (payload.Length >= 20)
                        state.Paints[ReadUInt32(payload, 0)] = new PaintInfo
                        {
                            Color = ReadColor(payload, 16),
                            Width = ReadInt16(payload, 8),
                            IsBrush = false,
                        };
                    break;
                case ExtCreatePenType:
                    if (payload.Length >= 44)
                        state.Paints[ReadUInt32(payload, 0)] = new PaintInfo
                        {
                            Color = ReadColor(payload, 32),
                            Width = (int)ReadUInt32(payload, 24),
                            IsBrush = false,
                        };
                    break;
                case CreateBrushIndirectType:
                    if (payload.Length >= 12)
                        state.Paints[ReadUInt32(payload, 0)] = new PaintInfo
                        {
                            Color = ReadColor(payload, 8),
                            IsBrush = true,
                        };
                    break;
                case SetTextColorType:
                    if (payload.Length >= 4)
                        state.TextColor = ReadColor(payload, 0);
                    break;
                case SetDCPenColorType:
                    if (payload.Length >= 4)
                        state.StrokeColor = ReadColor(payload, 0);
                    break;
                case SetDCBrushColorType:
                    if (payload.Length >= 4)
                        state.FillColor = ReadColor(payload, 0);
                    break;
                case SetWorldTransformType:
                    if (payload.Length >= 24)
                        state.Transform = ReadTransform(payload, 0);
                    break;
                case ModifyWorldTransformType:
                    if (payload.Length >= 28)
                        ModifyTransform(state, ReadTransform(payload, 0), ReadUInt32(payload, 24));
                    break;
                case SaveDcType:
                    state.SavedStates.Push(state.Clone());
                    break;
                case RestoreDcType:
                    if (state.SavedStates.Count > 0)
                    {
                        ReplayState restored = state.SavedStates.Pop();
                        state.RestoreFrom(restored);
                    }
                    break;
            }

            if (state.SelectedPaint != null)
            {
                if (state.SelectedPaint.IsBrush)
                    state.FillColor = state.SelectedPaint.Color;
                else
                    state.StrokeColor = state.SelectedPaint.Color;
            }
        }

        private static void AddUnit(EmfRecordSignature record, ReplayState state,
            EmfRectangle canvasBounds, List<EmfSemanticUnit> units)
        {
            EmfOperation operation = record.Operation;
            if (operation == null)
                return;

            switch (record.Type)
            {
                case BeginPathType:
                    state.PathPoints = new List<EmfPoint>();
                    state.InPath = true;
                    return;
                case MoveToType:
                case LineToType:
                    if (state.InPath && operation.Points.Count > 0)
                        state.PathPoints.Add(operation.Points[0]);
                    return;
                case EndPathType:
                case CloseFigureType:
                    return;
                case AbortPathType:
                    state.PathPoints.Clear();
                    state.InPath = false;
                    return;
                case StrokePathType:
                case StrokeAndFillPathType:
                case 62:
                    units.Add(CreateUnit(record, state, EmfSemanticUnitKind.Path,
                        canvasBounds, state.PathPoints, record.Type == StrokePathType || record.Type == StrokeAndFillPathType,
                        record.Type == StrokeAndFillPathType || record.Type == 62));
                    state.PathPoints.Clear();
                    state.InPath = false;
                    return;
                case StretchDibitsType:
                    units.Add(CreateUnit(record, state, EmfSemanticUnitKind.RasterImage,
                        canvasBounds, operation.Points, false, false));
                    return;
            }

            EmfSemanticUnitKind kind = record.Type == 84
                ? EmfSemanticUnitKind.Text
                : EmfSemanticUnitKind.Shape;
            bool filled = record.Type == 3 || record.Type == 86 || record.Type == 90 ||
                record.Type == 91 || record.Type == 42 || record.Type == 43;
            bool stroked = record.Type == 2 || record.Type == 3 || record.Type == 42 ||
                record.Type == 4 || record.Type == 43 || record.Type == 85 ||
                record.Type == 86 || record.Type == 87 ||
                record.Type == 90 || record.Type == 91;
            units.Add(CreateUnit(record, state, kind, canvasBounds, operation.Points, stroked, filled));
        }

        private static EmfSemanticUnit CreateUnit(EmfRecordSignature record, ReplayState state,
            EmfSemanticUnitKind kind, EmfRectangle canvasBounds,
            IReadOnlyList<EmfPoint> points, bool stroked, bool filled)
        {
            EmfOperation operation = record.Operation;
            IReadOnlyList<EmfNormalizedPoint> normalizedPoints = NormalizePoints(points, state.Transform, canvasBounds);
            return new EmfSemanticUnit
            {
                RecordIndex = record.Index,
                Kind = kind,
                OperationKind = operation.Kind,
                Bounds = operation.Bounds,
                Points = points == null ? Array.Empty<EmfPoint>() : points.ToArray(),
                NormalizedPoints = normalizedPoints,
                NormalizedBounds = NormalizeBounds(operation.Bounds, canvasBounds),
                PolygonCount = operation.PolygonCount,
                Text = operation.Text,
                Font = state.Font,
                TextColor = kind == EmfSemanticUnitKind.Text ? state.TextColor : null,
                StrokeColor = state.StrokeColor,
                StrokeWidth = state.SelectedPaint == null ? 0 : state.SelectedPaint.Width,
                FillColor = state.FillColor,
                Transform = state.Transform.Clone(),
                IsStroked = stroked,
                IsFilled = filled,
            };
        }

        private static EmfRectangle ReadCanvasBounds(IReadOnlyList<EmfRecordSignature> records)
        {
            EmfRecordSignature header = records.FirstOrDefault(record => record.Type == HeaderType);
            if (header != null && header.Payload != null && header.Payload.Length >= 16)
                return ReadRectangle(header.Payload, 0);

            return new EmfRectangle { Left = 0, Top = 0, Right = 1, Bottom = 1 };
        }

        private static IReadOnlyList<EmfNormalizedPoint> NormalizePoints(
            IReadOnlyList<EmfPoint> points, EmfTransform transform, EmfRectangle canvasBounds)
        {
            if (points == null || points.Count == 0)
                return Array.Empty<EmfNormalizedPoint>();

            var result = new List<EmfNormalizedPoint>(points.Count);
            foreach (EmfPoint point in points)
            {
                float transformedX = point.X * transform.M11 + point.Y * transform.M21 + transform.Dx;
                float transformedY = point.X * transform.M12 + point.Y * transform.M22 + transform.Dy;
                result.Add(new EmfNormalizedPoint
                {
                    X = Normalize(transformedX, canvasBounds.Left, canvasBounds.Right),
                    Y = Normalize(transformedY, canvasBounds.Top, canvasBounds.Bottom),
                });
            }
            return result;
        }

        private static EmfNormalizedRectangle NormalizeBounds(EmfRectangle bounds, EmfRectangle canvasBounds)
        {
            if (bounds == null)
                return null;
            return new EmfNormalizedRectangle
            {
                Left = Normalize(bounds.Left, canvasBounds.Left, canvasBounds.Right),
                Top = Normalize(bounds.Top, canvasBounds.Top, canvasBounds.Bottom),
                Right = Normalize(bounds.Right, canvasBounds.Left, canvasBounds.Right),
                Bottom = Normalize(bounds.Bottom, canvasBounds.Top, canvasBounds.Bottom),
            };
        }

        private static double Normalize(float value, int minimum, int maximum)
        {
            return (value - minimum) / (double)Math.Max(1, maximum - minimum);
        }

        private static EmfFontInfo ReadFont(byte[] payload)
        {
            if (payload.Length < 100)
            {
                return new EmfFontInfo
                {
                    ObjectHandle = ReadUInt32(payload, 0),
                    Height = ReadInt16(payload, 4),
                    Width = ReadInt16(payload, 6),
                    Escapement = ReadInt16(payload, 8),
                    Orientation = ReadInt16(payload, 10),
                    Weight = ReadInt16(payload, 12),
                    Italic = payload.Length > 14 && payload[14] != 0,
                    Underline = payload.Length > 15 && payload[15] != 0,
                    StrikeOut = payload.Length > 16 && payload[16] != 0,
                    CharSet = payload.Length > 17 ? payload[17] : (byte)0,
                    FaceName = ReadAsciiString(payload, 21, 32),
                };
            }

            return new EmfFontInfo
            {
                ObjectHandle = ReadUInt32(payload, 0),
                Height = ReadInt32(payload, 4),
                Width = ReadInt32(payload, 8),
                Escapement = ReadInt32(payload, 12),
                Orientation = ReadInt32(payload, 16),
                Weight = ReadInt32(payload, 20),
                Italic = payload[24] != 0,
                Underline = payload[25] != 0,
                StrikeOut = payload[26] != 0,
                CharSet = payload[27],
                FaceName = ReadUnicodeString(payload, 32, 64),
            };
        }

        private static string ReadUnicodeString(byte[] payload, int offset, int byteCount)
        {
            if (offset >= payload.Length)
                return string.Empty;
            int length = Math.Min(byteCount, payload.Length - offset);
            if ((length & 1) != 0)
                length--;
            return Encoding.Unicode.GetString(payload, offset, length).TrimEnd('\0');
        }

        private static string ReadAsciiString(byte[] payload, int offset, int byteCount)
        {
            if (offset >= payload.Length)
                return string.Empty;
            int length = Math.Min(byteCount, payload.Length - offset);
            return Encoding.ASCII.GetString(payload, offset, length).TrimEnd('\0');
        }

        private static void ModifyTransform(ReplayState state, EmfTransform transform, uint mode)
        {
            if (mode == 1)
                state.Transform = new EmfTransform();
            else if (mode == 2)
                state.Transform = Multiply(transform, state.Transform);
            else if (mode == 3)
                state.Transform = Multiply(state.Transform, transform);
        }

        private static EmfTransform Multiply(EmfTransform left, EmfTransform right)
        {
            return new EmfTransform
            {
                M11 = left.M11 * right.M11 + left.M12 * right.M21,
                M12 = left.M11 * right.M12 + left.M12 * right.M22,
                M21 = left.M21 * right.M11 + left.M22 * right.M21,
                M22 = left.M21 * right.M12 + left.M22 * right.M22,
                Dx = left.Dx * right.M11 + left.Dy * right.M21 + right.Dx,
                Dy = left.Dx * right.M12 + left.Dy * right.M22 + right.Dy,
            };
        }

        private static EmfTransform ReadTransform(byte[] payload, int offset)
        {
            return new EmfTransform
            {
                M11 = ReadSingle(payload, offset),
                M12 = ReadSingle(payload, offset + 4),
                M21 = ReadSingle(payload, offset + 8),
                M22 = ReadSingle(payload, offset + 12),
                Dx = ReadSingle(payload, offset + 16),
                Dy = ReadSingle(payload, offset + 20),
            };
        }

        private static EmfColor ReadColor(byte[] payload, int offset)
        {
            uint value = ReadUInt32(payload, offset);
            return new EmfColor
            {
                R = (byte)(value & 0xFF),
                G = (byte)((value >> 8) & 0xFF),
                B = (byte)((value >> 16) & 0xFF),
            };
        }

        private static EmfRectangle ReadRectangle(byte[] payload, int offset)
        {
            return new EmfRectangle
            {
                Left = ReadInt32(payload, offset),
                Top = ReadInt32(payload, offset + 4),
                Right = ReadInt32(payload, offset + 8),
                Bottom = ReadInt32(payload, offset + 12),
            };
        }

        private static int ReadInt32(byte[] payload, int offset)
        {
            return BitConverter.ToInt32(payload, offset);
        }

        private static short ReadInt16(byte[] payload, int offset)
        {
            return BitConverter.ToInt16(payload, offset);
        }

        private static uint ReadUInt32(byte[] payload, int offset)
        {
            return BitConverter.ToUInt32(payload, offset);
        }

        private static float ReadSingle(byte[] payload, int offset)
        {
            return BitConverter.ToSingle(payload, offset);
        }

        private sealed class EmfPlusObjectData
        {
            public EmfColor Color { get; set; }
            public int Width { get; set; }
            public IReadOnlyList<EmfPoint> Points { get; set; }
        }

        private sealed class PaintInfo
        {
            public EmfColor Color { get; set; }
            public int Width { get; set; }
            public bool IsBrush { get; set; }
        }

        private sealed class ReplayState
        {
            public Dictionary<uint, EmfFontInfo> Fonts { get; } = new Dictionary<uint, EmfFontInfo>();
            public Dictionary<uint, PaintInfo> Paints { get; } = new Dictionary<uint, PaintInfo>();
            public Stack<ReplayState> SavedStates { get; } = new Stack<ReplayState>();
            public uint SelectedObject { get; set; }
            public EmfFontInfo Font { get; set; }
            public PaintInfo SelectedPaint { get; set; }
            public EmfColor TextColor { get; set; } = new EmfColor();
            public EmfColor StrokeColor { get; set; } = new EmfColor();
            public EmfColor FillColor { get; set; } = new EmfColor();
            public EmfTransform Transform { get; set; } = new EmfTransform();
            public bool InPath { get; set; }
            public List<EmfPoint> PathPoints { get; set; } = new List<EmfPoint>();

            public ReplayState Clone()
            {
                return new ReplayState
                {
                    SelectedObject = SelectedObject,
                    Font = Font,
                    SelectedPaint = SelectedPaint,
                    TextColor = CloneColor(TextColor),
                    StrokeColor = CloneColor(StrokeColor),
                    FillColor = CloneColor(FillColor),
                    Transform = Transform.Clone(),
                    InPath = InPath,
                    PathPoints = new List<EmfPoint>(PathPoints),
                };
            }

            public void RestoreFrom(ReplayState other)
            {
                SelectedObject = other.SelectedObject;
                Font = other.Font;
                SelectedPaint = other.SelectedPaint;
                TextColor = other.TextColor;
                StrokeColor = other.StrokeColor;
                FillColor = other.FillColor;
                Transform = other.Transform;
                InPath = other.InPath;
                PathPoints = other.PathPoints;
            }

            private static EmfColor CloneColor(EmfColor color)
            {
                return color == null ? null : new EmfColor { R = color.R, G = color.G, B = color.B };
            }
        }
    }

    internal static class EmfSemanticUnitExtensions
    {
        public static EmfTransform Clone(this EmfTransform transform)
        {
            return new EmfTransform
            {
                M11 = transform.M11,
                M12 = transform.M12,
                M21 = transform.M21,
                M22 = transform.M22,
                Dx = transform.Dx,
                Dy = transform.Dy,
            };
        }
    }
}
