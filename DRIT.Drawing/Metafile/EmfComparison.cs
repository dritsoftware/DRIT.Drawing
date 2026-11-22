using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace DRIT.Drawing.Metafile
{
    /// <summary>Controls how EMF record streams are compared.</summary>
    public enum EmfComparisonMode
    {
        /// <summary>Compares record types, sizes, and payload bytes.</summary>
        Exact,

        /// <summary>Compares record types and payload lengths while ignoring payload values.</summary>
        Structural,

        /// <summary>Compares record structure and parsed geometry values.</summary>
        StructuralGeometry,

        /// <summary>Compares only parsed graphical operations, ignoring state and object records.</summary>
        GraphicalOperations,
    }

    /// <summary>Identifies the parsed operation family of an EMF record.</summary>
    public enum EmfOperationKind
    {
        /// <summary>The record is not a parsed geometry operation.</summary>
        Unknown,

        /// <summary>A 32-bit Bezier operation.</summary>
        PolyBezier,

        /// <summary>A 16-bit Bezier operation.</summary>
        PolyBezier16,

        /// <summary>A 32-bit polyline operation.</summary>
        Polyline,

        /// <summary>A 16-bit polyline operation.</summary>
        Polyline16,

        /// <summary>A 32-bit polygon operation.</summary>
        Polygon,

        /// <summary>A 16-bit polygon operation.</summary>
        Polygon16,

        /// <summary>A 32-bit poly-polygon operation.</summary>
        PolyPolygon,

        /// <summary>A 16-bit poly-polygon operation.</summary>
        PolyPolygon16,

        /// <summary>A rectangle operation.</summary>
        Rectangle,

        /// <summary>An ellipse operation.</summary>
        Ellipse,

        /// <summary>A move-to path operation.</summary>
        MoveTo,

        /// <summary>A line-to path operation.</summary>
        LineTo,

        /// <summary>Begins a path.</summary>
        BeginPath,

        /// <summary>Ends a path.</summary>
        EndPath,

        /// <summary>Closes the current path figure.</summary>
        CloseFigure,

        /// <summary>Fills the current path.</summary>
        FillPath,

        /// <summary>Strokes the current path.</summary>
        StrokePath,

        /// <summary>Strokes and fills the current path.</summary>
        StrokeAndFillPath,

        /// <summary>Outputs a raster image.</summary>
        RasterImage,

        /// <summary>Outputs UTF-16 text.</summary>
        ExtTextOutW,
    }

    /// <summary>Represents an EMF integer rectangle.</summary>
    public sealed class EmfRectangle
    {
        /// <summary>
        /// Initializes an empty <see cref="EmfRectangle"/> instance.
        /// </summary>
        public EmfRectangle()
        {
        }

        /// <summary>Gets the left coordinate.</summary>
        public int Left { get; internal set; }

        /// <summary>Gets the top coordinate.</summary>
        public int Top { get; internal set; }

        /// <summary>Gets the right coordinate.</summary>
        public int Right { get; internal set; }

        /// <summary>Gets the bottom coordinate.</summary>
        public int Bottom { get; internal set; }

        /// <summary>Returns the rectangle as a readable coordinate tuple.</summary>
        public override string ToString()
        {
            return $"({Left},{Top})-({Right},{Bottom})";
        }
    }

    /// <summary>Represents an EMF integer point.</summary>
    public sealed class EmfPoint
    {
        /// <summary>
        /// Initializes an empty <see cref="EmfPoint"/> instance.
        /// </summary>
        public EmfPoint()
        {
        }

        /// <summary>Gets the horizontal coordinate.</summary>
        public int X { get; internal set; }

        /// <summary>Gets the vertical coordinate.</summary>
        public int Y { get; internal set; }

        /// <summary>Returns the point as a readable coordinate tuple.</summary>
        public override string ToString()
        {
            return $"({X},{Y})";
        }
    }

    /// <summary>Contains parsed geometry metadata for an EMF record.</summary>
    public sealed class EmfOperation
    {
        /// <summary>
        /// Initializes an empty <see cref="EmfOperation"/> instance.
        /// </summary>
        public EmfOperation()
        {
        }

        /// <summary>Gets the parsed operation family.</summary>
        public EmfOperationKind Kind { get; internal set; }

        /// <summary>Gets the operation bounds, when the record contains bounds.</summary>
        public EmfRectangle Bounds { get; internal set; }

        /// <summary>Gets the number of points in the operation, when available.</summary>
        public int? PointCount { get; internal set; }

        /// <summary>Gets the number of polygons in a poly-polygon operation, when available.</summary>
        public int? PolygonCount { get; internal set; }

        /// <summary>Gets the parsed operation points, when available.</summary>
        public IReadOnlyList<EmfPoint> Points { get; internal set; } = Array.Empty<EmfPoint>();

        /// <summary>Gets the text payload, when the operation outputs text.</summary>
        public string Text { get; internal set; }
    }

    /// <summary>Options used by <see cref="EmfComparer"/>.</summary>
    public sealed class EmfComparisonOptions
    {
        /// <summary>
        /// Initializes an empty <see cref="EmfComparisonOptions"/> instance.
        /// </summary>
        public EmfComparisonOptions()
        {
        }

        /// <summary>Gets or sets the comparison mode.</summary>
        public EmfComparisonMode Mode { get; set; } = EmfComparisonMode.Exact;

        /// <summary>Gets or sets whether the EMR_HEADER record is excluded from comparison.</summary>
        public bool IgnoreHeader { get; set; }

        /// <summary>Gets or sets whether the EMR_EOF record is excluded from comparison.</summary>
        public bool IgnoreEndOfFile { get; set; }

        /// <summary>Gets or sets the maximum number of differences returned.</summary>
        public int MaximumDifferences { get; set; } = 100;
    }

    /// <summary>Describes one record in an EMF stream.</summary>
    public sealed class EmfRecordSignature
    {
        /// <summary>
        /// Initializes an empty <see cref="EmfRecordSignature"/> instance.
        /// </summary>
        public EmfRecordSignature()
        {
        }

        /// <summary>Gets the zero-based record index.</summary>
        public int Index { get; internal set; }

        /// <summary>Gets the byte offset of the record.</summary>
        public long Offset { get; internal set; }

        /// <summary>Gets the numeric EMF record type.</summary>
        public uint Type { get; internal set; }

        /// <summary>Gets the record size declared by the EMF stream.</summary>
        public uint Size { get; internal set; }

        /// <summary>Gets the payload length excluding the eight-byte record header.</summary>
        public int PayloadLength { get; internal set; }

        /// <summary>Gets the SHA-256 hash of the record payload.</summary>
        public string PayloadHash { get; internal set; } = string.Empty;

        /// <summary>Gets the parsed operation metadata, when the record format is recognized.</summary>
        public EmfOperation Operation { get; internal set; }

        internal byte[] Payload { get; set; }

        /// <summary>Returns a readable record signature.</summary>
        public override string ToString()
        {
            return $"#{Index} type=0x{Type:X8} size={Size} payload={PayloadLength}";
        }
    }

    /// <summary>Describes one difference between two EMF record streams.</summary>
    public sealed class EmfDifference
    {
        /// <summary>
        /// Initializes an empty <see cref="EmfDifference"/> instance.
        /// </summary>
        public EmfDifference()
        {
        }

        /// <summary>Gets the zero-based record index.</summary>
        public int Index { get; internal set; }

        /// <summary>Gets the left-side record, if present.</summary>
        public EmfRecordSignature Left { get; internal set; }

        /// <summary>Gets the right-side record, if present.</summary>
        public EmfRecordSignature Right { get; internal set; }

        /// <summary>Gets the reason for the difference.</summary>
        public string Reason { get; internal set; } = string.Empty;

        /// <summary>Returns a readable difference description.</summary>
        public override string ToString()
        {
            return $"record {Index}: {Reason}; left={Left?.ToString() ?? "<missing>"}; right={Right?.ToString() ?? "<missing>"}";
        }
    }

    /// <summary>Contains the result of comparing two EMF record streams.</summary>
    public sealed class EmfComparisonResult
    {
        /// <summary>
        /// Initializes an empty <see cref="EmfComparisonResult"/> instance.
        /// </summary>
        public EmfComparisonResult()
        {
        }

        /// <summary>Gets whether the compared streams are equivalent.</summary>
        public bool AreEqual { get; internal set; }

        /// <summary>Gets the left-side record signatures.</summary>
        public IReadOnlyList<EmfRecordSignature> LeftRecords { get; internal set; } = Array.Empty<EmfRecordSignature>();

        /// <summary>Gets the right-side record signatures.</summary>
        public IReadOnlyList<EmfRecordSignature> RightRecords { get; internal set; } = Array.Empty<EmfRecordSignature>();

        /// <summary>Gets the reported differences.</summary>
        public IReadOnlyList<EmfDifference> Differences { get; internal set; } = Array.Empty<EmfDifference>();
    }

    /// <summary>Compares EMF files or byte streams at record level.</summary>
    public sealed class EmfComparer
    {
        /// <summary>
        /// Initializes an empty <see cref="EmfComparer"/> instance.
        /// </summary>
        public EmfComparer()
        {
        }

        private const uint HeaderType = 1;
        private const uint EndOfFileType = 14;
        private const uint PolyBezierType = 2;
        private const uint PolylineType = 4;
        private const uint PolygonType = 3;
        private const uint PolyBezier16Type = 85;
        private const uint Polyline16Type = 87;
        private const uint Polygon16Type = 86;
        private const uint PolyPolygonType = 90;
        private const uint PolyPolygon16Type = 91;
        private const uint RectangleType = 43;
        private const uint EllipseType = 42;
        private const uint MoveToType = 27;
        private const uint LineToType = 54;
        private const uint BeginPathType = 59;
        private const uint EndPathType = 60;
        private const uint CloseFigureType = 61;
        private const uint FillPathType = 62;
        private const uint StrokeAndFillPathType = 63;
        private const uint StrokePathType = 64;
        private const uint StretchDibitsType = 81;
        private const uint ExtTextOutWType = 84;

        /// <summary>Compares two EMF files.</summary>
        public EmfComparisonResult Compare(string leftPath, string rightPath,
                                            EmfComparisonOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(leftPath))
                throw new ArgumentException("The left path cannot be null or whitespace.", nameof(leftPath));
            if (string.IsNullOrWhiteSpace(rightPath))
                throw new ArgumentException("The right path cannot be null or whitespace.", nameof(rightPath));

            return Compare(File.ReadAllBytes(leftPath), File.ReadAllBytes(rightPath), options);
        }

        /// <summary>Compares two EMF byte arrays.</summary>
        public EmfComparisonResult Compare(byte[] left, byte[] right,
                                            EmfComparisonOptions options = null)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            return Compare(new MemoryStream(left, false), new MemoryStream(right, false), options);
        }

        /// <summary>Compares two seekable EMF streams.</summary>
        public EmfComparisonResult Compare(Stream left, Stream right,
                                            EmfComparisonOptions options = null)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (!left.CanSeek) throw new InvalidOperationException("The left stream must support seeking.");
            if (!right.CanSeek) throw new InvalidOperationException("The right stream must support seeking.");

            options = options ?? new EmfComparisonOptions();
            var leftRecords = ReadSignatures(left, options);
            var rightRecords = ReadSignatures(right, options);
            if (options.Mode == EmfComparisonMode.GraphicalOperations)
                return CompareGraphicalOperations(leftRecords, rightRecords, options.MaximumDifferences);

            var differences = new List<EmfDifference>();
            int count = Math.Max(leftRecords.Count, rightRecords.Count);
            int limit = Math.Max(0, options.MaximumDifferences);

            for (int index = 0; index < count && differences.Count < limit; index++)
            {
                EmfRecordSignature leftRecord = index < leftRecords.Count ? leftRecords[index] : null;
                EmfRecordSignature rightRecord = index < rightRecords.Count ? rightRecords[index] : null;
                string reason = GetDifferenceReason(leftRecord, rightRecord, options.Mode);
                if (reason != null)
                {
                    differences.Add(new EmfDifference
                    {
                        Index = index,
                        Left = leftRecord,
                        Right = rightRecord,
                        Reason = reason,
                    });
                }
            }

            return new EmfComparisonResult
            {
                AreEqual = differences.Count == 0 && leftRecords.Count == rightRecords.Count,
                LeftRecords = leftRecords,
                RightRecords = rightRecords,
                Differences = differences,
            };
        }

        private static EmfComparisonResult CompareGraphicalOperations(
            IReadOnlyList<EmfRecordSignature> leftRecords,
            IReadOnlyList<EmfRecordSignature> rightRecords,
            int maximumDifferences)
        {
            var leftOperations = GetGraphicalRecords(leftRecords);
            var rightOperations = GetGraphicalRecords(rightRecords);
            var differences = new List<EmfDifference>();
            int count = Math.Max(leftOperations.Count, rightOperations.Count);
            int limit = Math.Max(0, maximumDifferences);

            for (var index = 0; index < count && differences.Count < limit; index++)
            {
                var left = index < leftOperations.Count ? leftOperations[index] : null;
                var right = index < rightOperations.Count ? rightOperations[index] : null;
                string reason = GetGraphicalDifferenceReason(left?.Operation, right?.Operation);
                if (reason != null)
                {
                    differences.Add(new EmfDifference
                    {
                        Index = index,
                        Left = left,
                        Right = right,
                        Reason = reason,
                    });
                }
            }

            return new EmfComparisonResult
            {
                AreEqual = differences.Count == 0 && leftOperations.Count == rightOperations.Count,
                LeftRecords = leftRecords,
                RightRecords = rightRecords,
                Differences = differences,
            };
        }

        private static List<EmfRecordSignature> GetGraphicalRecords(IReadOnlyList<EmfRecordSignature> records)
        {
            var result = new List<EmfRecordSignature>();
            foreach (var record in records)
            {
                if (record.Operation != null)
                    result.Add(record);
            }
            return result;
        }

        private static string GetGraphicalDifferenceReason(EmfOperation left, EmfOperation right)
        {
            if (left == null || right == null)
                return "graphical operation count differs";
            return AreOperationsEqual(left, right) ? null : "graphical operation differs";
        }

        /// <summary>Reads and parses the records in an EMF file.</summary>
        public IReadOnlyList<EmfRecordSignature> Read(string path,
                                                       EmfComparisonOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("The path cannot be null or whitespace.", nameof(path));
            using (var stream = File.OpenRead(path))
                return Read(stream, options ?? new EmfComparisonOptions());
        }

        /// <summary>Reads and parses the records in a seekable EMF stream.</summary>
        public IReadOnlyList<EmfRecordSignature> Read(Stream stream,
                                                       EmfComparisonOptions options = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanSeek) throw new InvalidOperationException("The stream must support seeking.");
            return ReadSignatures(stream, options ?? new EmfComparisonOptions());
        }

        private static string GetDifferenceReason(EmfRecordSignature left,
                                                   EmfRecordSignature right,
                                                   EmfComparisonMode mode)
        {
            if (left == null || right == null) return "record count differs";
            if (left.Type != right.Type) return "record type differs";
            if (left.Size != right.Size) return "record size differs";
            if (mode == EmfComparisonMode.Exact && left.PayloadHash != right.PayloadHash)
                return "record payload differs";
            if (mode == EmfComparisonMode.StructuralGeometry && !AreOperationsEqual(left.Operation, right.Operation))
                return "parsed operation geometry differs";
            return null;
        }

        private static List<EmfRecordSignature> ReadSignatures(Stream stream,
                                                                 EmfComparisonOptions options)
        {
            stream.Position = 0;
            var result = new List<EmfRecordSignature>();
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
            {
                while (stream.Position < stream.Length)
                {
                    long offset = stream.Position;
                    if (stream.Length - offset < 8) throw new InvalidDataException("EMF record header is truncated.");
                    uint type = reader.ReadUInt32();
                    uint size = reader.ReadUInt32();
                    if (size < 8 || size > stream.Length - offset)
                        throw new InvalidDataException("EMF record size is invalid.");

                    int payloadLength = checked((int)size - 8);
                    byte[] payload = reader.ReadBytes(payloadLength);
                    if (payload.Length != payloadLength)
                        throw new InvalidDataException("EMF record payload is truncated.");
                    if ((options.IgnoreHeader && type == HeaderType) ||
                        (options.IgnoreEndOfFile && type == EndOfFileType))
                        continue;

                    result.Add(new EmfRecordSignature
                    {
                        Index = result.Count,
                        Offset = offset,
                        Type = type,
                        Size = size,
                        PayloadLength = payloadLength,
                        PayloadHash = ComputeHash(payload),
                        Payload = payload,
                        Operation = ParseOperation(type, payload),
                    });
                }
            }
            return result;
        }

        private static bool AreOperationsEqual(EmfOperation left, EmfOperation right)
        {
            if (left == null || right == null) return left == right;
            return left.Kind == right.Kind &&
                   AreRectanglesEqual(left.Bounds, right.Bounds) &&
                   left.PointCount == right.PointCount &&
                   left.PolygonCount == right.PolygonCount &&
                     string.Equals(left.Text, right.Text, StringComparison.Ordinal) &&
                   ArePointsEqual(left.Points, right.Points);
        }

        private static bool ArePointsEqual(IReadOnlyList<EmfPoint> left, IReadOnlyList<EmfPoint> right)
        {
            if (left == null || right == null) return left == right;
            if (left.Count != right.Count) return false;
            for (int index = 0; index < left.Count; index++)
            {
                if (left[index].X != right[index].X || left[index].Y != right[index].Y)
                    return false;
            }
            return true;
        }

        private static bool AreRectanglesEqual(EmfRectangle left, EmfRectangle right)
        {
            if (left == null || right == null) return left == right;
            return left.Left == right.Left && left.Top == right.Top &&
                   left.Right == right.Right && left.Bottom == right.Bottom;
        }

        private static EmfOperation ParseOperation(uint type, byte[] payload)
        {
            EmfOperationKind kind;
            bool pointsAre16Bit;
            bool hasPointCount;
            bool hasPolygonCount;

            switch (type)
            {
                case PolyBezierType:
                    kind = EmfOperationKind.PolyBezier;
                    pointsAre16Bit = false;
                    hasPointCount = true;
                    hasPolygonCount = false;
                    break;
                case PolygonType:
                    kind = EmfOperationKind.Polygon;
                    pointsAre16Bit = false;
                    hasPointCount = true;
                    hasPolygonCount = false;
                    break;
                case PolyBezier16Type:
                    kind = EmfOperationKind.PolyBezier16;
                    pointsAre16Bit = true;
                    hasPointCount = true;
                    hasPolygonCount = false;
                    break;
                case PolylineType:
                    kind = EmfOperationKind.Polyline;
                    pointsAre16Bit = false;
                    hasPointCount = true;
                    hasPolygonCount = false;
                    break;
                case Polyline16Type:
                    kind = EmfOperationKind.Polyline16;
                    pointsAre16Bit = true;
                    hasPointCount = true;
                    hasPolygonCount = false;
                    break;
                case Polygon16Type:
                    kind = EmfOperationKind.Polygon16;
                    pointsAre16Bit = true;
                    hasPointCount = true;
                    hasPolygonCount = false;
                    break;
                case PolyPolygonType:
                    kind = EmfOperationKind.PolyPolygon;
                    pointsAre16Bit = false;
                    hasPointCount = false;
                    hasPolygonCount = true;
                    break;
                case PolyPolygon16Type:
                    kind = EmfOperationKind.PolyPolygon16;
                    pointsAre16Bit = true;
                    hasPointCount = false;
                    hasPolygonCount = true;
                    break;
                case RectangleType:
                    return new EmfOperation
                    {
                        Kind = EmfOperationKind.Rectangle,
                        Bounds = ReadRectangle(payload, 0),
                    };
                case EllipseType:
                    return new EmfOperation
                    {
                        Kind = EmfOperationKind.Ellipse,
                        Bounds = ReadRectangle(payload, 0),
                    };
                case MoveToType:
                    return new EmfOperation
                    {
                        Kind = EmfOperationKind.MoveTo,
                        Points = new[] { ReadPoint(payload, 0) },
                        PointCount = 1,
                    };
                case LineToType:
                    return new EmfOperation
                    {
                        Kind = EmfOperationKind.LineTo,
                        Points = new[] { ReadPoint(payload, 0) },
                        PointCount = 1,
                    };
                case BeginPathType:
                    return new EmfOperation { Kind = EmfOperationKind.BeginPath };
                case EndPathType:
                    return new EmfOperation { Kind = EmfOperationKind.EndPath };
                case CloseFigureType:
                    return new EmfOperation { Kind = EmfOperationKind.CloseFigure };
                case FillPathType:
                    return new EmfOperation
                    {
                        Kind = EmfOperationKind.FillPath,
                        Bounds = ReadRectangle(payload, 0),
                    };
                case StrokeAndFillPathType:
                    return new EmfOperation
                    {
                        Kind = EmfOperationKind.StrokeAndFillPath,
                        Bounds = ReadRectangle(payload, 0),
                    };
                case StrokePathType:
                    return new EmfOperation
                    {
                        Kind = EmfOperationKind.StrokePath,
                        Bounds = ReadRectangle(payload, 0),
                    };
                case StretchDibitsType:
                    return new EmfOperation
                    {
                        Kind = EmfOperationKind.RasterImage,
                        Bounds = ReadRectangle(payload, 0),
                    };
                case ExtTextOutWType:
                    return ReadExtTextOutW(payload);
                default:
                    return null;
            }

            if (payload.Length < 20) throw new InvalidDataException("EMF geometry record is truncated.");
            return new EmfOperation
            {
                Kind = kind,
                Bounds = ReadRectangle(payload, 0),
                PointCount = hasPointCount ? ReadInt32(payload, 16) : null,
                PolygonCount = hasPolygonCount ? ReadInt32(payload, 16) : null,
                Points = hasPointCount ? ReadPoints(payload, 20, ReadInt32(payload, 16), pointsAre16Bit) : Array.Empty<EmfPoint>(),
            };
        }

        private static IReadOnlyList<EmfPoint> ReadPoints(byte[] payload, int offset, int count, bool pointsAre16Bit)
        {
            if (count < 0) throw new InvalidDataException("EMF geometry point count is invalid.");
            int pointSize = pointsAre16Bit ? 4 : 8;
            int requiredLength = checked(offset + count * pointSize);
            if (payload.Length < requiredLength) throw new InvalidDataException("EMF geometry points are truncated.");

            var points = new List<EmfPoint>(count);
            for (int index = 0; index < count; index++)
            {
                int pointOffset = offset + index * pointSize;
                points.Add(new EmfPoint
                {
                    X = pointsAre16Bit ? BitConverter.ToInt16(payload, pointOffset) : ReadInt32(payload, pointOffset),
                    Y = pointsAre16Bit ? BitConverter.ToInt16(payload, pointOffset + 2) : ReadInt32(payload, pointOffset + 4),
                });
            }
            return points;
        }

        private static EmfRectangle ReadRectangle(byte[] payload, int offset)
        {
            if (payload.Length < offset + 16) throw new InvalidDataException("EMF rectangle is truncated.");
            return new EmfRectangle
            {
                Left = ReadInt32(payload, offset),
                Top = ReadInt32(payload, offset + 4),
                Right = ReadInt32(payload, offset + 8),
                Bottom = ReadInt32(payload, offset + 12),
            };
        }

        private static EmfPoint ReadPoint(byte[] payload, int offset)
        {
            if (payload.Length < offset + 8) throw new InvalidDataException("EMF point is truncated.");
            return new EmfPoint
            {
                X = ReadInt32(payload, offset),
                Y = ReadInt32(payload, offset + 4),
            };
        }

        private static EmfOperation ReadExtTextOutW(byte[] payload)
        {
            const int textObjectOffset = 28;
            const int referenceOffset = textObjectOffset;
            const int characterCountOffset = textObjectOffset + 8;
            const int stringOffsetOffset = textObjectOffset + 12;

            if (payload.Length < textObjectOffset + 40)
                throw new InvalidDataException("EMF ExtTextOutW record is truncated.");

            int characterCount = ReadInt32(payload, characterCountOffset);
            int declaredStringOffset = ReadInt32(payload, stringOffsetOffset);
            int stringOffset = declaredStringOffset < textObjectOffset + 40
                ? textObjectOffset + declaredStringOffset
                : declaredStringOffset - 8;
            if (characterCount < 0 || stringOffset < 0 || stringOffset + characterCount * 2 > payload.Length)
                throw new InvalidDataException("EMF ExtTextOutW string is invalid.");

            return new EmfOperation
            {
                Kind = EmfOperationKind.ExtTextOutW,
                Bounds = ReadRectangle(payload, 0),
                PointCount = characterCount,
                Points = new[] { ReadPoint(payload, referenceOffset) },
                Text = Encoding.Unicode.GetString(payload, stringOffset, characterCount * 2).TrimEnd('\0'),
            };
        }

        private static int ReadInt32(byte[] payload, int offset)
        {
            return BitConverter.ToInt32(payload, offset);
        }

        private static string ComputeHash(byte[] payload)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(payload);
                var builder = new System.Text.StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                    builder.Append(value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }
    }
}