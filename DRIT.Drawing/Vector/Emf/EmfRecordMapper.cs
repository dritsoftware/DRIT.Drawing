using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Metafile.Emf.Enumerations;
using DRIT.Drawing.Metafile.Emf.Objects;
using DRIT.Drawing.Metafile.Emf.Records;
using DRIT.Drawing.Metafile.Emf.Records.DrawingRecordTypes;
using DRIT.Drawing.Metafile.Emf.Records.ObjectRecordTypes;
using DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes;
using DRIT.Drawing.Metafile.Wmf.Flags;
using DRIT.Drawing.Metafile.Wmf.Enumerations;
using DRIT.Drawing.Metafile.Wmf.Objects;
using DRIT.Drawing.Pens;
using DRIT.Drawing.Rendering;
using DRIT.Drawing.Text;
using Pen = DRIT.Drawing.Pens.Pen;
using Brush = DRIT.Drawing.Brushes.Brush;
using SolidBrush = DRIT.Drawing.Brushes.SolidBrush;
using TextureBrush = DRIT.Drawing.Brushes.TextureBrush;
using LinearGradientBrush = DRIT.Drawing.Drawing2D.LinearGradientBrush;
using PathGradientBrush = DRIT.Drawing.Drawing2D.PathGradientBrush;
using Color = DRIT.Drawing.Colors.Color;
using DashStyle = DRIT.Drawing.Drawing2D.DashStyle;
using EmfRecord = DRIT.Drawing.Metafile.Emf.Records.Record;
using EmfRasterOperation = DRIT.Drawing.Metafile.Emf.Enumerations.EmfTernaryRasterOperation;
using WmfHatchStyle = DRIT.Drawing.Metafile.Wmf.Enumerations.HatchStyle;

namespace DRIT.Drawing.Vector.Emf
{
    /// <summary>
    /// Maps <see cref="DrawingCommand"/> state to EMF records. Manages the EMF
    /// object table (pen/brush/font handle allocation and selection), world
    /// transforms, clip regions, state stack, and path-to-records conversion.
    /// Used by <see cref="EmfDrawingContext"/>.
    /// </summary>
    internal sealed class EmfRecordMapper
    {
        private const ushort EmfPlusGetDc = 0x4004;
        private const ushort EmfPlusClear = 0x4009;
        private const ushort EmfPlusEndOfFile = 0x4002;
        private const ushort EmfPlusSetAntiAliasMode = 0x401E;
        private const ushort EmfPlusSetTextRenderingHint = 0x401F;
        private const ushort EmfPlusSetInterpolationMode = 0x4021;
        private const ushort EmfPlusSetPixelOffsetMode = 0x4022;
        private const ushort EmfPlusSetCompositingMode = 0x4023;
        private const ushort EmfPlusSetCompositingQuality = 0x4024;
        private const ushort EmfPlusObject = 0x4008;
        private const ushort EmfPlusFillPath = 0x4014;
        private const ushort EmfPlusDrawPath = 0x4015;
        private const ushort EmfPlusDrawImage = 0x401A;
        private const ushort EmfPlusDrawImagePoints = 0x401B;
        private const ushort EmfPlusDrawString = 0x401C;
        private const ushort EmfPlusSave = 0x4025;
        private const ushort EmfPlusRestore = 0x4026;
        private const ushort EmfPlusBeginContainer = 0x4027;
        private const ushort EmfPlusBeginContainerNoParams = 0x4028;
        private const ushort EmfPlusEndContainer = 0x4029;
        private const ushort EmfPlusSetPageTransform = 0x4030;
        private const ushort EmfPlusResetClip = 0x4031;
        private const ushort EmfPlusSetClipRect = 0x4032;
        private const ushort EmfPlusSetClipPath = 0x4033;
        private const ushort EmfPlusSetClipRegion = 0x4034;
        private const ushort EmfPlusOffsetClip = 0x4035;
        private const ushort EmfPlusObjectTypeBrush = 0x0001;
        private const ushort EmfPlusObjectTypePen = 0x0002;
        private const ushort EmfPlusObjectTypePath = 0x0003;
        private const ushort EmfPlusObjectTypeImage = 0x0005;
        private const ushort EmfPlusObjectTypeFont = 0x0006;
        private const ushort EmfPlusObjectTypeStringFormat = 0x0007;
        private const ushort EmfPlusObjectTypeRegion = 0x0004;
        private const int EmfPlusObjectTableCapacity = 64;

        private readonly List<EmfRecord> _records = new List<EmfRecord>();
        private readonly Dictionary<object, uint> _objectTable = new Dictionary<object, uint>();
        private readonly Dictionary<string, byte> _emfPlusObjectTable = new Dictionary<string, byte>();
        private readonly Stack<uint> _emfPlusStateStack = new Stack<uint>();
        private uint _nextHandle = 1;
        private int _nextEmfPlusObjectId;
        private uint _nextEmfPlusStackIndex = 1;
        private uint _currentPenHandle;
        private uint _currentBrushHandle;
        private uint _currentFontHandle;
        private BackgroundMode? _currentBackgroundMode;
        private float _logicalCoordinateScale = 1f;
        private float _dpiY = 96f;
        private float _emfPlusUnitsPerPixel = 1f;
        private bool _emfPlusCompleted;
        private readonly EmfOutputMode _outputMode;

        internal EmfRecordMapper(EmfOutputMode outputMode)
        {
            _outputMode = outputMode;
        }

        public IReadOnlyList<EmfRecord> Records => _records;
        public ushort HandleCount => (ushort)_nextHandle;
        public bool UsesLogicalCoordinateScale => _logicalCoordinateScale != 1f;

        public void CompleteEmfPlus()
        {
            if (!CanEmitEmfPlus || _emfPlusCompleted) return;

            WriteEmfPlusRecord(EmfPlusEndOfFile, 0);
            _emfPlusCompleted = true;
        }

        public void SetLogicalCoordinateScale(
            float scale,
            float dpiX,
            float dpiY,
            SmoothingMode smoothingMode,
            InterpolationMode interpolationMode,
            CompositingMode compositingMode,
            CompositingQuality compositingQuality,
            PixelOffsetMode pixelOffsetMode,
            TextRenderingHint textRenderingHint,
            GraphicsUnit pageUnit,
            float pageScale,
            Point renderingOrigin)
        {
            _logicalCoordinateScale = scale > 0 ? scale : 1f;
            _dpiY = dpiY > 0 ? dpiY : 96f;
            _emfPlusUnitsPerPixel = dpiX > 0 ? 914400f / dpiX : 9525f;
            if (CanEmitEmfPlus)
            {
                _records.Add(new EMR_COMMENT
                {
                    CommentData = EmfPlusCommentWriter.CreateHeaderComment(dpiX, dpiY),
                });
                _records.Add(new EMR_COMMENT
                {
                    CommentData = CreateInitialStateCommentData(
                        smoothingMode, interpolationMode, compositingMode,
                        compositingQuality, pixelOffsetMode, textRenderingHint,
                        pageUnit, pageScale, renderingOrigin),
                });
            }
        }

        private static byte[] CreateInitialStateCommentData(
            SmoothingMode smoothingMode,
            InterpolationMode interpolationMode,
            CompositingMode compositingMode,
            CompositingQuality compositingQuality,
            PixelOffsetMode pixelOffsetMode,
            TextRenderingHint textRenderingHint,
            GraphicsUnit pageUnit,
            float pageScale,
            Point renderingOrigin)
        {
            return EmfPlusCommentWriter.CreateComment(writer =>
            {
                WriteEmfPlusStateRecord(writer, EmfPlusSetPixelOffsetMode,
                    ToEmfPlusPixelOffsetMode(pixelOffsetMode));
                WriteEmfPlusStateRecord(writer, EmfPlusSetAntiAliasMode,
                    ToEmfPlusSmoothingMode(smoothingMode));
                WriteEmfPlusStateRecord(writer, EmfPlusSetCompositingMode,
                    ToEmfPlusCompositingMode(compositingMode));
                WriteEmfPlusStateRecord(writer, EmfPlusSetCompositingQuality,
                    ToEmfPlusCompositingQuality(compositingQuality));
                WriteEmfPlusStateRecord(writer, EmfPlusSetPageTransform,
                    ToEmfPlusUnit(pageUnit), BitConverter.GetBytes(pageScale));
                WriteEmfPlusStateRecord(writer, EmfPlusSetInterpolationMode,
                    ToEmfPlusInterpolationMode(interpolationMode));
                WriteEmfPlusStateRecord(writer, EmfPlusSetTextRenderingHint,
                    ToEmfPlusTextRenderingHint(textRenderingHint));
                WriteEmfPlusStateRecord(writer, 0x401D, 0, CreatePointData(renderingOrigin));
                WriteEmfPlusStateRecord(writer, EmfPlusGetDc, 0x0000);
            });
        }

        private static void WriteEmfPlusStateRecord(
            System.IO.BinaryWriter writer, ushort type, ushort flags, byte[] data = null)
        {
            EmfPlusCommentWriter.WriteRecord(writer, type, flags, data);
        }

        private void WriteEmfPlusStateRecord(ushort type, ushort flags, byte[] data = null)
        {
            if (!CanEmitEmfPlus || _logicalCoordinateScale == 1f) return;

            _records.Add(new EMR_COMMENT
            {
                CommentData = EmfPlusCommentWriter.CreateComment(
                    writer => EmfPlusCommentWriter.WriteRecord(writer, type, flags, data)),
            });
        }

        public void SetSmoothingMode(SmoothingMode value)
            => WriteEmfPlusStateRecord(EmfPlusSetAntiAliasMode, ToEmfPlusSmoothingMode(value));

        public void SetInterpolationMode(InterpolationMode value)
            => WriteEmfPlusStateRecord(EmfPlusSetInterpolationMode, ToEmfPlusInterpolationMode(value));

        public void SetPixelOffsetMode(PixelOffsetMode value)
            => WriteEmfPlusStateRecord(EmfPlusSetPixelOffsetMode, ToEmfPlusPixelOffsetMode(value));

        public void SetCompositingMode(CompositingMode value)
            => WriteEmfPlusStateRecord(EmfPlusSetCompositingMode, ToEmfPlusCompositingMode(value));

        public void SetCompositingQuality(CompositingQuality value)
            => WriteEmfPlusStateRecord(EmfPlusSetCompositingQuality, ToEmfPlusCompositingQuality(value));

        public void SetTextRenderingHint(TextRenderingHint value)
            => WriteEmfPlusStateRecord(EmfPlusSetTextRenderingHint, ToEmfPlusTextRenderingHint(value));

        public void SetPageTransform(GraphicsUnit pageUnit, float pageScale)
        {
            if (_logicalCoordinateScale != 1f && pageUnit == GraphicsUnit.Point && pageScale == 1f)
                return;

            WriteEmfPlusStateRecord(EmfPlusSetPageTransform, ToEmfPlusUnit(pageUnit),
                BitConverter.GetBytes(pageScale));
        }

        public void SetRenderingOrigin(Point origin)
            => WriteEmfPlusStateRecord(0x401D, 0, CreatePointData(origin));

        public bool EmitEmfPlusDrawPath(Pen pen, GraphicsPath path)
        {
            if (!CanEmitEmfPlus || !SupportsEmfPlusPen(pen) || path == null || path.PointCount == 0)
                return false;

            PrepareEmfPlusObjectTable(2);

            if (_logicalCoordinateScale != 1f)
            {
                PointF origin = path.PathPoints[0];
                using var stream = new System.IO.MemoryStream();
                using var writer = new System.IO.BinaryWriter(stream);
                WriteEmfPlusWorldTransform(writer,
                    1f / _emfPlusUnitsPerPixel, 0f, 0f,
                    -1f / _emfPlusUnitsPerPixel, origin.X, origin.Y);
                byte batchPenId = GetOrCreateEmfPlusPen(pen, writer);
                byte batchPathId = GetOrCreateEmfPlusDrawPath(path, writer);
                WriteEmfPlusRecord(writer, EmfPlusDrawPath, batchPathId,
                    BitConverter.GetBytes((uint)batchPenId));
                WriteEmfPlusWorldTransform(writer, 1f, 0f, 0f, 1f, 0f, 0f);
                _records.Add(new EMR_COMMENT
                {
                    CommentData = EmfPlusCommentWriter.CreateComment(
                        commentWriter => commentWriter.Write(stream.ToArray())),
                });
                WriteEmfPlusRecord(EmfPlusGetDc, 0);
                return true;
            }

            byte pathId = GetOrCreateEmfPlusPath(path, null, "draw");
            byte penId = GetOrCreateEmfPlusPen(pen);
            WriteEmfPlusRecord(EmfPlusDrawPath, pathId, BitConverter.GetBytes((uint)penId));
            WriteEmfPlusRecord(EmfPlusGetDc, 0);
            return true;
        }

        public bool EmitEmfPlusFillPath(Brush brush, GraphicsPath path, FillMode fillMode)
        {
            if (!CanEmitEmfPlus || !SupportsEmfPlusBrush(brush) || path == null || path.PointCount == 0)
                return false;

            PrepareEmfPlusObjectTable(2);

            if (_logicalCoordinateScale != 1f)
            {
                PointF origin = path.PathPoints[0];
                using var stream = new System.IO.MemoryStream();
                using var writer = new System.IO.BinaryWriter(stream);
                WriteEmfPlusWorldTransform(writer,
                    1f / _emfPlusUnitsPerPixel, 0f, 0f,
                    -1f / _emfPlusUnitsPerPixel, origin.X, origin.Y);
                byte batchBrushId = GetOrCreateEmfPlusBrush(brush, writer);
                byte batchPathId = GetOrCreateEmfPlusPath(path, fillMode, "fill", writer);
                WriteEmfPlusRecord(writer, EmfPlusFillPath, batchBrushId,
                    BitConverter.GetBytes((uint)batchPathId));
                WriteEmfPlusWorldTransform(writer, 1f, 0f, 0f, 1f, 0f, 0f);
                _records.Add(new EMR_COMMENT
                {
                    CommentData = EmfPlusCommentWriter.CreateComment(
                        commentWriter => commentWriter.Write(stream.ToArray())),
                });
                WriteEmfPlusRecord(EmfPlusGetDc, 0);
                return true;
            }

            byte fallbackBrushId = GetOrCreateEmfPlusBrush(brush);
            byte fallbackPathId = GetOrCreateEmfPlusPath(path, fillMode);
            WriteEmfPlusRecord(EmfPlusFillPath, fallbackBrushId,
                BitConverter.GetBytes((uint)fallbackPathId));
            WriteEmfPlusRecord(EmfPlusGetDc, 0);
            return true;
        }

        public bool EmitEmfPlusClear(Color color)
        {
            if (!CanEmitEmfPlus) return false;

            WriteEmfPlusRecord(EmfPlusClear, 0, BitConverter.GetBytes(ToArgb(color)));
            WriteEmfPlusRecord(EmfPlusGetDc, 0);
            return true;
        }

        public bool EmitEmfPlusDrawImage(Image image, RectangleF destRect,
            RectangleF srcRect, GraphicsUnit srcUnit, ImageAttributes attr)
        {
            return EmitEmfPlusDrawImagePoints(image,
                new[]
                {
                    new PointF(destRect.Left, destRect.Top),
                    new PointF(destRect.Right, destRect.Top),
                    new PointF(destRect.Left, destRect.Bottom),
                }, srcRect, srcUnit, attr);
        }

        public void EmitEmfPlusGetDc()
        {
            if (!CanEmitEmfPlus) return;
            WriteEmfPlusRecord(EmfPlusGetDc, 0);
        }

        public bool EmitEmfPlusDrawImagePoints(Image image, PointF[] destPoints,
            RectangleF srcRect, GraphicsUnit srcUnit, ImageAttributes attr)
        {
            if (!CanEmitEmfPlus || !(image is Bitmap bitmap) || attr != null ||
                destPoints == null || destPoints.Length < 3)
                return false;

            PrepareEmfPlusObjectTable(1);

            byte imageId = GetOrCreateEmfPlusImage(bitmap);
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            writer.Write(uint.MaxValue);
            writer.Write((uint)ToEmfPlusUnit(srcUnit));
            WriteRectangle(writer, srcRect);
            writer.Write((uint)destPoints.Length);
            bool compact = destPoints.All(point =>
                point.X >= short.MinValue && point.X <= short.MaxValue &&
                point.Y >= short.MinValue && point.Y <= short.MaxValue &&
                point.X == (float)Math.Round(point.X) &&
                point.Y == (float)Math.Round(point.Y));
            if (compact)
            {
                foreach (PointF point in destPoints)
                {
                    writer.Write((short)Math.Round(point.X));
                    writer.Write((short)Math.Round(point.Y));
                }
            }
            else
            {
                foreach (PointF point in destPoints)
                {
                    writer.Write(point.X);
                    writer.Write(point.Y);
                }
            }
            WriteEmfPlusRecord(EmfPlusDrawImagePoints,
                (ushort)(imageId | (compact ? 0x4000 : 0)), stream.ToArray());
            return true;
        }

        public bool EmitEmfPlusDrawString(string text, DRIT.Drawing.Text.Font font, Brush brush,
            RectangleF layoutRect, StringFormat format)
        {
            if (!CanEmitEmfPlus || string.IsNullOrEmpty(text) || font == null ||
                !(brush is SolidBrush))
                return false;

            PrepareEmfPlusObjectTable(3);

            byte brushId = GetOrCreateEmfPlusBrush(brush);
            byte fontId = GetOrCreateEmfPlusFont(font);
            uint formatId = format == null ? uint.MaxValue : GetOrCreateEmfPlusStringFormat(format);
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            ushort flags = 0;
            if (brush is SolidBrush solidBrush)
            {
                flags = 0x8000;
                writer.Write(ToArgb(solidBrush.Color));
            }
            else
            {
                writer.Write((uint)brushId);
            }
            writer.Write((uint)formatId);
            writer.Write((uint)text.Length);
            WriteRectangle(writer, layoutRect);
            writer.Write(System.Text.Encoding.Unicode.GetBytes(text + "\0"));
            WriteEmfPlusRecord(EmfPlusDrawString, (ushort)(fontId | flags), stream.ToArray());
            WriteEmfPlusRecord(EmfPlusGetDc, 0);
            return true;
        }

        public void SetClipRect(RectangleF rect, CombineMode combineMode)
        {
            _records.Add(CreateClassicClipRect(rect, combineMode));
            if (!CanEmitEmfPlus) return;

            WriteEmfPlusRecord(EmfPlusSetClipRect, ToEmfPlusCombineMode(combineMode),
                CreateRectangleData(rect));
        }

        public void SetClipPath(GraphicsPath path, CombineMode combineMode)
        {
            if (path == null || path.PointCount == 0) return;

            BuildPath(path);
            _records.Add(new EMR_SELECTCLIPPATH { RegionMode = ToClassicRegionMode(combineMode) });
            if (!CanEmitEmfPlus) return;

            PrepareEmfPlusObjectTable(1);
            byte pathId = GetOrCreateEmfPlusPath(path);
            WriteEmfPlusRecord(EmfPlusSetClipPath, ToEmfPlusCombineMode(combineMode),
                BitConverter.GetBytes((uint)pathId));
        }

        public bool SetClipRegion(DRIT.Drawing.Drawing2D.Region region, CombineMode combineMode)
        {
            if (!CanEmitEmfPlus || region == null || !SupportsEmfPlusRegion(region.Root))
                return false;

            _records.Add(CreateClassicClipRect(region.GetBounds(), combineMode));
            PrepareEmfPlusObjectTable(1);
            byte regionId = GetOrCreateEmfPlusRegion(region);
            WriteEmfPlusRecord(EmfPlusSetClipRegion, ToEmfPlusCombineMode(combineMode),
                BitConverter.GetBytes((uint)regionId));
            return true;
        }

        private EmfRecord CreateClassicClipRect(RectangleF rect, CombineMode combineMode)
        {
            return new EMR_EXTSELECTCLIPRGN
            {
                RegionMode = (uint)ToClassicRegionMode(combineMode),
                RegionData = CreateClassicRectRegionData(rect),
            };
        }

        private static ushort ToEmfPlusCombineMode(CombineMode combineMode)
            => (ushort)((ushort)combineMode << 8);

        private static RegionMode ToClassicRegionMode(CombineMode combineMode)
            => combineMode switch
            {
                CombineMode.Intersect => RegionMode.RGN_AND,
                CombineMode.Union => RegionMode.RGN_OR,
                CombineMode.Xor => RegionMode.RGN_XOR,
                CombineMode.Exclude => RegionMode.RGN_DIFF,
                CombineMode.Complement => RegionMode.RGN_DIFF,
                _ => RegionMode.RGN_COPY,
            };

        private static byte[] CreateClassicRectRegionData(RectangleF rect)
        {
            int left = (int)Math.Floor(rect.Left);
            int top = (int)Math.Floor(rect.Top);
            int right = (int)Math.Ceiling(rect.Right);
            int bottom = (int)Math.Ceiling(rect.Bottom);
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            writer.Write(32u);
            writer.Write(1u);
            writer.Write(1u);
            writer.Write(16u);
            writer.Write(left);
            writer.Write(top);
            writer.Write(right);
            writer.Write(bottom);
            writer.Write(left);
            writer.Write(top);
            writer.Write(right);
            writer.Write(bottom);
            return stream.ToArray();
        }

        public void ResetClip()
        {
            if (!CanEmitEmfPlus) return;
            WriteEmfPlusRecord(EmfPlusResetClip, 0);
        }

        public void OffsetClip(float dx, float dy)
        {
            if (!CanEmitEmfPlus) return;

            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            writer.Write(dx);
            writer.Write(dy);
            WriteEmfPlusRecord(EmfPlusOffsetClip, 0, stream.ToArray());
        }

        public void SetWorldTransform(Matrix matrix)
        {
            if (_logicalCoordinateScale == 1f) return;

            var elements = matrix.Elements;
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            for (int i = 0; i < 6; i++)
                writer.Write(elements[i]);

            WriteEmfPlusStateRecord(0x402A, 0, stream.ToArray());
        }

        private void WriteEmfPlusWorldTransform(
            float m11, float m12, float m21, float m22, float dx, float dy)
        {
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            WriteEmfPlusWorldTransform(writer, m11, m12, m21, m22, dx, dy);
            WriteEmfPlusStateRecord(0x402A, 0, stream.ToArray());
        }

        private static void WriteEmfPlusWorldTransform(
            System.IO.BinaryWriter writer,
            float m11, float m12, float m21, float m22, float dx, float dy)
        {
            using var stream = new System.IO.MemoryStream();
            using var payloadWriter = new System.IO.BinaryWriter(stream);
            payloadWriter.Write(m11);
            payloadWriter.Write(m12);
            payloadWriter.Write(m21);
            payloadWriter.Write(m22);
            payloadWriter.Write(dx);
            payloadWriter.Write(dy);
            WriteEmfPlusRecord(writer, 0x402A, 0, stream.ToArray());
        }

        private static byte[] CreatePointData(Point point)
        {
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            writer.Write(point.X);
            writer.Write(point.Y);
            return stream.ToArray();
        }

        private static byte[] CreateRectangleData(RectangleF rect)
        {
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            writer.Write(rect.X);
            writer.Write(rect.Y);
            writer.Write(rect.Width);
            writer.Write(rect.Height);
            return stream.ToArray();
        }

        private static void WriteRectangle(System.IO.BinaryWriter writer, RectangleF rect)
        {
            writer.Write(rect.X);
            writer.Write(rect.Y);
            writer.Write(rect.Width);
            writer.Write(rect.Height);
        }

        private static ushort ToEmfPlusSmoothingMode(SmoothingMode value)
            => value switch
            {
                SmoothingMode.Default => 1,
                SmoothingMode.HighSpeed => 2,
                SmoothingMode.HighQuality => 3,
                SmoothingMode.None => 4,
                SmoothingMode.AntiAlias => 5,
                _ => 1,
            };

        private static ushort ToEmfPlusInterpolationMode(InterpolationMode value)
            => value switch
            {
                InterpolationMode.Default => 1,
                InterpolationMode.Low => 2,
                InterpolationMode.High => 3,
                InterpolationMode.Bilinear => 4,
                InterpolationMode.Bicubic => 5,
                InterpolationMode.NearestNeighbor => 6,
                InterpolationMode.HighQualityBilinear => 7,
                InterpolationMode.HighQualityBicubic => 8,
                _ => 1,
            };

        private static ushort ToEmfPlusPixelOffsetMode(PixelOffsetMode value)
            => value switch
            {
                PixelOffsetMode.Default => 1,
                PixelOffsetMode.HighSpeed => 2,
                PixelOffsetMode.HighQuality => 3,
                PixelOffsetMode.None => 4,
                PixelOffsetMode.Half => 5,
                _ => 1,
            };

        private static ushort ToEmfPlusCompositingMode(CompositingMode value)
            => value == CompositingMode.SourceCopy ? (ushort)1 : (ushort)0;

        private static ushort ToEmfPlusCompositingQuality(CompositingQuality value)
            => value switch
            {
                CompositingQuality.Default => 1,
                CompositingQuality.HighSpeed => 2,
                CompositingQuality.HighQuality => 3,
                CompositingQuality.GammaCorrected => 4,
                CompositingQuality.AssumeLinear => 5,
                _ => 1,
            };

        private static ushort ToEmfPlusTextRenderingHint(TextRenderingHint value)
            => value switch
            {
                TextRenderingHint.SystemDefault => 0,
                TextRenderingHint.SingleBitPerPixelGridFit => 1,
                TextRenderingHint.SingleBitPerPixel => 2,
                TextRenderingHint.AntiAliasGridFit => 3,
                TextRenderingHint.AntiAlias => 4,
                TextRenderingHint.ClearTypeGridFit => 5,
                _ => 0,
            };

        private static ushort ToEmfPlusUnit(GraphicsUnit value)
            => value switch
            {
                GraphicsUnit.World => 0,
                GraphicsUnit.Display => 1,
                GraphicsUnit.Pixel => 2,
                GraphicsUnit.Point => 3,
                GraphicsUnit.Inch => 4,
                GraphicsUnit.Document => 5,
                GraphicsUnit.Millimeter => 6,
                _ => 2,
            };

        private bool CanEmitEmfPlus => _outputMode == EmfOutputMode.EmfPlusDual;

        private byte GetOrCreateEmfPlusPen(Pen pen, System.IO.BinaryWriter writer = null)
        {
            string key = CreatePenKey(pen);
            if (_emfPlusObjectTable.TryGetValue(key, out byte id))
                return id;

            id = AllocateEmfPlusObjectId();
            byte[] payload = CreatePenPayload(pen, _logicalCoordinateScale != 1f);
            if (writer == null)
                WriteEmfPlusObject(id, EmfPlusObjectTypePen, payload);
            else
                WriteEmfPlusObject(writer, id, EmfPlusObjectTypePen, payload);
            _emfPlusObjectTable[key] = id;
            return id;
        }

        private byte GetOrCreateEmfPlusDrawPath(GraphicsPath path,
            System.IO.BinaryWriter writer = null, string role = "draw-path")
        {
            PointF[] sourcePoints = path.PathPoints;
            PointF origin = sourcePoints[0];
            PointF[] points = sourcePoints.Select(point => new PointF(
                (point.X - origin.X) * _emfPlusUnitsPerPixel,
                (origin.Y - point.Y) * _emfPlusUnitsPerPixel)).ToArray();
            byte[] types = path.PathTypes;
            string key = string.Format(CultureInfo.InvariantCulture,
                "{0}:{1}:{2}:{3}", role, path.FillMode,
                string.Join(";", points.Select(point => string.Format(
                    CultureInfo.InvariantCulture, "{0:R},{1:R}", point.X, point.Y))),
                Convert.ToBase64String(types));
            if (_emfPlusObjectTable.TryGetValue(key, out byte id))
                return id;

            id = AllocateEmfPlusObjectId();
            byte[] payload = CreatePathPayload(points, types);
            if (writer == null)
                WriteEmfPlusObject(id, EmfPlusObjectTypePath, payload);
            else
                WriteEmfPlusObject(writer, id, EmfPlusObjectTypePath, payload);
            _emfPlusObjectTable[key] = id;
            return id;
        }

        private byte GetOrCreateEmfPlusBrush(Brush brush, System.IO.BinaryWriter writer = null)
        {
            var solidBrush = brush as SolidBrush;
            var hatchBrush = brush as HatchBrush;
            string key = solidBrush != null
                ? CreateColorKey("brush", solidBrush.Color)
                : brush is LinearGradientBrush linear
                ? CreateLinearGradientKey(linear)
                : brush is PathGradientBrush path
                ? CreatePathGradientKey(path)
                : brush is TextureBrush texture
                ? CreateTextureKey(texture)
                : string.Format(CultureInfo.InvariantCulture, "hatch:{0}:{1}:{2}",
                    hatchBrush.HatchStyle,
                    CreateColorKey("fore", hatchBrush.ForegroundColor),
                    CreateColorKey("back", hatchBrush.BackgroundColor));
            if (_emfPlusObjectTable.TryGetValue(key, out byte id))
                return id;

            id = AllocateEmfPlusObjectId();
            byte[] payload = solidBrush != null
                ? CreateBrushPayload(solidBrush.Color)
                : brush is LinearGradientBrush linearBrush
                ? CreateLinearGradientBrushPayload(linearBrush)
                : brush is PathGradientBrush pathBrush
                ? CreatePathGradientBrushPayload(pathBrush)
                : CreateHatchBrushPayload(hatchBrush);
            if (writer == null)
                WriteEmfPlusObject(id, EmfPlusObjectTypeBrush, payload);
            else
                WriteEmfPlusObject(writer, id, EmfPlusObjectTypeBrush, payload);
            _emfPlusObjectTable[key] = id;
            return id;
        }

        private byte GetOrCreateEmfPlusPath(GraphicsPath path, FillMode? fillMode = null,
            string role = "path", System.IO.BinaryWriter writer = null)
        {
            string key = role + ":" + CreatePathKey(path, fillMode ?? path.FillMode);
            if (_emfPlusObjectTable.TryGetValue(key, out byte id))
                return id;

            id = AllocateEmfPlusObjectId();
            byte[] payload = CreatePathPayload(path, fillMode ?? path.FillMode);
            if (writer == null)
                WriteEmfPlusObject(id, EmfPlusObjectTypePath, payload);
            else
                WriteEmfPlusObject(writer, id, EmfPlusObjectTypePath, payload);
            _emfPlusObjectTable[key] = id;
            return id;
        }

        private byte GetOrCreateEmfPlusRegion(DRIT.Drawing.Drawing2D.Region region)
        {
            string key = "region:" + Convert.ToBase64String(region.GetRegionData().Data);
            if (_emfPlusObjectTable.TryGetValue(key, out byte id))
                return id;

            id = AllocateEmfPlusObjectId();
            WriteEmfPlusObject(id, EmfPlusObjectTypeRegion, CreateRegionPayload(region.Root));
            _emfPlusObjectTable[key] = id;
            return id;
        }

        private byte GetOrCreateEmfPlusImage(Bitmap bitmap)
        {
            if (!SupportsEmfPlusBitmap(bitmap))
                throw new NotSupportedException("The bitmap pixel format is not supported by EMF+ output.");

            string key = CreateImageKey(bitmap);
            if (_emfPlusObjectTable.TryGetValue(key, out byte id))
                return id;

            id = AllocateEmfPlusObjectId();
            WriteEmfPlusObject(id, EmfPlusObjectTypeImage, CreateImagePayload(bitmap));
            _emfPlusObjectTable[key] = id;
            return id;
        }

        private byte GetOrCreateEmfPlusFont(DRIT.Drawing.Text.Font font)
        {
            string key = string.Format(CultureInfo.InvariantCulture,
                "font:{0}:{1:R}:{2}:{3}:{4}", font.Name, font.Size, font.Style,
                font.Unit, font.GdiCharSet);
            if (_emfPlusObjectTable.TryGetValue(key, out byte id))
                return id;

            id = AllocateEmfPlusObjectId();
            WriteEmfPlusObject(id, EmfPlusObjectTypeFont, CreateFontPayload(font));
            _emfPlusObjectTable[key] = id;
            return id;
        }

        private byte GetOrCreateEmfPlusStringFormat(StringFormat format)
        {
            format ??= StringFormat.GenericDefault;
            float firstTabOffset;
            float[] tabStops = format.GetTabStops(out firstTabOffset);
            string key = string.Format(CultureInfo.InvariantCulture,
                "format:{0}:{1}:{2}:{3}:{4}:{5}:{6}", format.Alignment,
                format.LineAlignment, format.Trimming, format.FormatFlags,
                format.HotkeyPrefix, firstTabOffset,
                string.Join(",", tabStops.Select(value => value.ToString("R", CultureInfo.InvariantCulture))));
            if (_emfPlusObjectTable.TryGetValue(key, out byte id))
                return id;

            id = AllocateEmfPlusObjectId();
            WriteEmfPlusObject(id, EmfPlusObjectTypeStringFormat,
                CreateStringFormatPayload(format, firstTabOffset, tabStops));
            _emfPlusObjectTable[key] = id;
            return id;
        }

        private byte AllocateEmfPlusObjectId()
        {
            if (_nextEmfPlusObjectId >= EmfPlusObjectTableCapacity)
                throw new InvalidOperationException("The EMF+ object table cannot contain more than 256 objects.");

            return (byte)_nextEmfPlusObjectId++;
        }

        private void PrepareEmfPlusObjectTable(int maximumNewObjects)
        {
            if (_nextEmfPlusObjectId + maximumNewObjects <= EmfPlusObjectTableCapacity)
                return;

            _emfPlusObjectTable.Clear();
            _nextEmfPlusObjectId = 0;
        }

        private void WriteEmfPlusObject(byte id, ushort objectType, byte[] payload)
        {
            WriteEmfPlusRecord(EmfPlusObject,
                (ushort)(id | (objectType << 8)), payload);
        }

        private static void WriteEmfPlusObject(
            System.IO.BinaryWriter writer, byte id, ushort objectType, byte[] payload)
        {
            WriteEmfPlusRecord(writer, EmfPlusObject,
                (ushort)(id | (objectType << 8)), payload);
        }

        private static void WriteEmfPlusRecord(
            System.IO.BinaryWriter writer, ushort type, ushort flags, byte[] data = null)
        {
            EmfPlusCommentWriter.WriteRecord(writer, type, flags, data);
        }

        private void WriteEmfPlusRecord(ushort type, ushort flags, byte[] data = null)
        {
            if (!CanEmitEmfPlus) return;

            _records.Add(new EMR_COMMENT
            {
                CommentData = EmfPlusCommentWriter.CreateComment(
                    writer => EmfPlusCommentWriter.WriteRecord(writer, type, flags, data)),
            });
        }

        private static bool SupportsEmfPlusPen(Pen pen)
            => pen != null && pen.PenType == PenType.SolidColor
            && pen.StartCap != LineCap.Custom && pen.EndCap != LineCap.Custom
            && pen.LineJoin != (LineJoin)255
            && pen.Alignment == PenAlignment.Center && pen.CompoundArray == null
            && pen.CustomStartCap == null && pen.CustomEndCap == null
            && (pen.DashStyle != DashStyle.Custom ||
                (pen.DashPattern != null && pen.DashPattern.Length > 0))
            && pen.Transform != null && pen.Transform.IsIdentity;

        private static bool SupportsEmfPlusBrush(Brush brush)
            => brush is SolidBrush || brush is HatchBrush ||
               brush is LinearGradientBrush linear && IsSimpleLinearGradient(linear) ||
               brush is PathGradientBrush path && path.Transform.IsIdentity &&
                   path.Blend == null && path.InterpolationColors == null;

        private static bool IsSimpleLinearGradient(LinearGradientBrush brush)
        {
            if (!brush.Transform.IsIdentity || brush.GammaCorrection || brush.Blend != null)
                return false;

            Color[] colors = brush.LinearColors;
            ColorBlend blend = brush.InterpolationColors;
            return blend != null && blend.Colors != null && blend.Positions != null &&
                blend.Colors.Length == 2 && blend.Positions.Length == 2 &&
                blend.Positions[0] == 0f && blend.Positions[1] == 1f &&
                blend.Colors[0].ToArgb() == colors[0].ToArgb() &&
                blend.Colors[1].ToArgb() == colors[1].ToArgb();
        }

        private static byte[] CreatePenPayload(Pen pen, bool excelCoordinates = false)
        {
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            float[] dashPattern = pen.DashPattern;
            uint flags = 0x88;
            if (pen.StartCap != LineCap.Flat) flags |= 0x02;
            if (pen.EndCap != LineCap.Flat) flags |= 0x04;
            if (pen.LineJoin != LineJoin.Miter) flags |= 0x08;
            if (pen.MiterLimit != 10f) flags |= 0x10;
            if (pen.DashStyle != DashStyle.Solid) flags |= 0x20;
            if (pen.DashCap != DashCap.Flat) flags |= 0x40;
            if (pen.DashStyle == DashStyle.Custom &&
                dashPattern != null && dashPattern.Length > 0)
                flags |= 0x100;

            writer.Write(0xDBC01002u);
            writer.Write((uint)0);
            writer.Write(flags);
            writer.Write((uint)0);
            writer.Write(excelCoordinates ? pen.Width * 12700f : pen.Width);
            if ((flags & 0x02) != 0) writer.Write((uint)pen.StartCap);
            if ((flags & 0x04) != 0) writer.Write((uint)pen.EndCap);
            if ((flags & 0x08) != 0) writer.Write((uint)pen.LineJoin);
            if ((flags & 0x10) != 0) writer.Write(pen.MiterLimit);
            if ((flags & 0x20) != 0) writer.Write((uint)pen.DashStyle);
            if ((flags & 0x40) != 0) writer.Write((uint)pen.DashCap);
            if ((flags & 0x80) != 0) writer.Write(pen.DashOffset);
            if ((flags & 0x100) != 0)
            {
                writer.Write((uint)dashPattern.Length);
                foreach (float value in dashPattern)
                    writer.Write(value);
            }
            writer.Write(0xDBC01002u);
            writer.Write((uint)0);
            writer.Write(ToArgb(pen.Color));
            return stream.ToArray();
        }

        private static byte[] CreateBrushPayload(Color color)
        {
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write(ToArgb(color));
            return stream.ToArray();
        }

        private static byte[] CreateHatchBrushPayload(HatchBrush brush)
        {
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            writer.Write(0xDBC01002u);
            writer.Write((uint)1);
            writer.Write((uint)brush.HatchStyle);
            writer.Write(ToArgb(brush.ForegroundColor));
            writer.Write(ToArgb(brush.BackgroundColor));
            return stream.ToArray();
        }

        private static byte[] CreateLinearGradientBrushPayload(LinearGradientBrush brush)
        {
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            writer.Write(0xDBC01002u);
            writer.Write((uint)4);
            writer.Write((uint)0);
            writer.Write((uint)brush.WrapMode);
            writer.Write(brush.Point1.X);
            writer.Write(brush.Point1.Y);
            writer.Write(brush.Point2.X);
            writer.Write(brush.Point2.Y);
            Color[] colors = brush.LinearColors;
            writer.Write(ToArgb(colors[0]));
            writer.Write(ToArgb(colors[1]));
            writer.Write(ToArgb(colors[0]));
            writer.Write(ToArgb(colors[1]));
            return stream.ToArray();
        }

        private static byte[] CreatePathGradientBrushPayload(PathGradientBrush brush)
        {
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            writer.Write(0xDBC01002u);
            writer.Write((uint)3);
            writer.Write((uint)0);
            Color[] colors = brush.SurroundColors;
            writer.Write((uint)colors.Length);
            writer.Write(ToArgb(brush.CenterColor));
            writer.Write(brush.CenterPoint.X);
            writer.Write(brush.CenterPoint.Y);
            writer.Write((uint)colors.Length);
            foreach (Color color in colors)
                writer.Write(ToArgb(color));
            PointF[] points = GetPathGradientPoints(brush);
            writer.Write((uint)points.Length);
            foreach (PointF point in points)
            {
                writer.Write(point.X);
                writer.Write(point.Y);
            }
            return stream.ToArray();
        }

        private static PointF[] GetPathGradientPoints(PathGradientBrush brush)
            => brush.PathPoints;

        private static string FormatBounds(RectangleF bounds)
            => string.Format(CultureInfo.InvariantCulture, "{0:R},{1:R},{2:R},{3:R}",
                bounds.X, bounds.Y, bounds.Width, bounds.Height);

        private static byte[] CreatePathPayload(GraphicsPath path, FillMode fillMode)
            => CreatePathPayload(path.PathPoints, path.PathTypes);

        private static byte[] CreatePathPayload(PointF[] points, byte[] types)
        {
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
                writer.Write(0xDBC01002u);
                writer.Write((uint)points.Length);
                bool compact = points.All(point =>
                    point.X >= short.MinValue && point.X <= short.MaxValue &&
                    point.Y >= short.MinValue && point.Y <= short.MaxValue &&
                    point.X == (float)Math.Round(point.X) &&
                    point.Y == (float)Math.Round(point.Y));
                writer.Write(compact ? 0x4000u : 0u);
                if (compact)
                    foreach (PointF point in points)
                    {
                        writer.Write((short)Math.Round(point.X));
                        writer.Write((short)Math.Round(point.Y));
                    }
                else
                    foreach (PointF point in points)
                    {
                        writer.Write(point.X);
                        writer.Write(point.Y);
                    }
            writer.Write(types);
                while ((stream.Length & 3) != 0)
                    writer.Write((byte)0);
            return stream.ToArray();
        }

        private static bool SupportsEmfPlusRegion(RegionNode node)
            => node is EmptyRegionNode || node is InfiniteRegionNode || node is LeafRectNode ||
                node is LeafPathNode ||
                (node is OpRegionNode operation && SupportsEmfPlusRegion(operation.Left) &&
                 SupportsEmfPlusRegion(operation.Right));

        private static byte[] CreateRegionPayload(RegionNode node)
        {
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            writer.Write(0xDBC01002u);
            writer.Write(node is OpRegionNode ? 2u : 0u);
            WriteRegionNode(writer, node);
            return stream.ToArray();
        }

        private static void WriteRegionNode(System.IO.BinaryWriter writer, RegionNode node)
        {
            if (node is EmptyRegionNode)
            {
                writer.Write(0x10000002u);
                return;
            }

            if (node is InfiniteRegionNode)
            {
                writer.Write(0x10000003u);
                return;
            }

            if (node is LeafRectNode rect)
            {
                writer.Write(0x10000000u);
                writer.Write(rect.Rect.X);
                writer.Write(rect.Rect.Y);
                writer.Write(rect.Rect.Width);
                writer.Write(rect.Rect.Height);
                return;
            }

            if (node is LeafPathNode path)
            {
                writer.Write(0x10000001u);
                writer.Write(CreatePathPayload(path.Path, path.Path.FillMode));
                return;
            }

            var operation = (OpRegionNode)node;
            writer.Write(operation.Op switch
            {
                SetOp.Intersect => 0x00000001u,
                SetOp.Union => 0x00000002u,
                SetOp.Xor => 0x00000003u,
                SetOp.Exclude => 0x00000004u,
                SetOp.Complement => 0x00000005u,
                _ => throw new InvalidOperationException("Unknown region operation."),
            });
            WriteRegionNode(writer, operation.Left);
            WriteRegionNode(writer, operation.Right);
        }

        private static byte[] CreateImagePayload(Bitmap bitmap)
        {
            using var imageStream = new System.IO.MemoryStream();
            bitmap.Save(imageStream, ImageFormat.Png);

            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            writer.Write(0xDBC01002u);
            writer.Write((uint)1);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write((uint)1);
            writer.Write(imageStream.ToArray());
            return stream.ToArray();
        }

        private static byte[] CreateFontPayload(DRIT.Drawing.Text.Font font)
        {
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            writer.Write(0xDBC01001u);
            writer.Write(font.Size);
            writer.Write((uint)ToEmfPlusUnit(font.Unit));
            writer.Write((uint)font.Style);
            writer.Write((uint)font.GdiCharSet);
            writer.Write((uint)font.Name.Length);
            writer.Write(System.Text.Encoding.Unicode.GetBytes(font.Name + "\0"));
            return stream.ToArray();
        }

        private static byte[] CreateStringFormatPayload(StringFormat format,
            float firstTabOffset, float[] tabStops)
        {
            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            writer.Write(0xDBC01003u);
            writer.Write((uint)format.FormatFlags);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write((uint)0);
            writer.Write(firstTabOffset);
            writer.Write((uint)tabStops.Length);
            foreach (float tabStop in tabStops)
                writer.Write(tabStop);
            writer.Write((uint)format.Alignment);
            writer.Write((uint)format.LineAlignment);
            writer.Write((uint)format.Trimming);
            writer.Write((uint)format.HotkeyPrefix);
            return stream.ToArray();
        }

        private static uint ToArgb(Color color)
            => ((uint)color.A << 24) | ((uint)color.R << 16)
            | ((uint)color.G << 8) | color.B;

        private static string CreatePenKey(Pen pen)
        {
            var elements = pen.Transform.Elements;
            return string.Format(CultureInfo.InvariantCulture,
                "pen:{0}:{1:R}:{2}:{3}:{4}:{5}:{6}:{7}:{8}:{9}:{10}:{11}:{12}",
                CreateColorKey("", pen.Color), pen.Width, pen.StartCap, pen.EndCap,
                pen.LineJoin, pen.MiterLimit, pen.DashStyle, pen.DashOffset,
                pen.DashCap, pen.Alignment, string.Join(",", elements.Select(
                    element => element.ToString("R", CultureInfo.InvariantCulture))),
                pen.PenType, pen.DashPattern == null ? string.Empty : string.Join(",",
                    pen.DashPattern.Select(element =>
                        element.ToString("R", CultureInfo.InvariantCulture))));
        }

        private static string CreatePathKey(GraphicsPath path, FillMode fillMode)
        {
            PointF[] points = path.PathPoints;
            byte[] types = path.PathTypes;
            return string.Format(CultureInfo.InvariantCulture, "path:{0}:{1}:{2}",
                fillMode,
                string.Join(";", points.Select(point => string.Format(
                    CultureInfo.InvariantCulture, "{0:R},{1:R}", point.X, point.Y))),
                Convert.ToBase64String(types));
        }

        private static string CreateColorKey(string prefix, Color color)
            => string.Format(CultureInfo.InvariantCulture, "{0}:{1:X2}{2:X2}{3:X2}{4:X2}",
                prefix, color.A, color.R, color.G, color.B);

        private static string CreateLinearGradientKey(LinearGradientBrush brush)
        {
            Color[] colors = brush.LinearColors;
            return string.Format(CultureInfo.InvariantCulture,
                "linear-gradient:{0:R},{1:R}:{2:R},{3:R}:{4}:{5}:{6}",
                brush.Point1.X, brush.Point1.Y, brush.Point2.X, brush.Point2.Y,
                brush.WrapMode,
                CreateColorKey("start", colors[0]),
                CreateColorKey("end", colors[1]));
        }

        private static string CreatePathGradientKey(PathGradientBrush brush)
        {
            Color[] colors = brush.SurroundColors;
            PointF[] points = GetPathGradientPoints(brush);
            return string.Format(CultureInfo.InvariantCulture,
                "path-gradient:{0}:{1:R},{2:R}:{3}:{4}:{5}",
                CreateColorKey("center", brush.CenterColor),
                brush.CenterPoint.X, brush.CenterPoint.Y, brush.WrapMode,
                string.Join(",", colors.Select(color => CreateColorKey("", color))),
                string.Join(";", points.Select(point => string.Format(
                    CultureInfo.InvariantCulture, "{0:R},{1:R}",
                    point.X, point.Y))));
        }

        private static string CreateTextureKey(TextureBrush brush)
        {
            var bitmap = (Bitmap)brush.Image;
            return string.Format(CultureInfo.InvariantCulture,
                "texture:{0}:{1}:{2}", brush.WrapMode, bitmap.Width, bitmap.Height) +
                ":" + Convert.ToBase64String(bitmap._pixels);
        }

        private static bool SupportsEmfPlusBitmap(Bitmap bitmap)
            => bitmap != null && (bitmap.PixelFormat == PixelFormat.Format24bppRgb ||
                bitmap.PixelFormat == PixelFormat.Format32bppRgb ||
                bitmap.PixelFormat == PixelFormat.Format32bppArgb ||
                bitmap.PixelFormat == PixelFormat.Format32bppPArgb);

        private static string CreateImageKey(Bitmap bitmap)
            => string.Format(CultureInfo.InvariantCulture, "image:{0}:{1}:{2}:{3}",
                bitmap.Width, bitmap.Height, bitmap.PixelFormat,
                Convert.ToBase64String(bitmap._pixels));

        public void BeginLogicalPath()
        {
            BeginLogicalPath(_logicalCoordinateScale);
        }

        internal void BeginLogicalPath(float coordinateScale)
        {
            if (coordinateScale == 1f) return;

            _records.Add(new EMR_MODIFYWORLDTRANSFORM
            {
                M11 = 1f / coordinateScale,
                M22 = 1f / coordinateScale,
                ModifyWorldTransformMode = ModifyWorldTransformMode.MWT_LEFTMULTIPLY,
            });
        }

        public void EndLogicalPath()
        {
            EndLogicalPath(_logicalCoordinateScale);
        }

        internal void EndLogicalPath(float coordinateScale)
        {
            if (coordinateScale == 1f) return;

            _records.Add(new EMR_MODIFYWORLDTRANSFORM
            {
                M11 = coordinateScale,
                M22 = coordinateScale,
                ModifyWorldTransformMode = ModifyWorldTransformMode.MWT_LEFTMULTIPLY,
            });
        }

        internal float GetPolylineCoordinateScale(GraphicsPath path)
        {
            float coordinateScale = _logicalCoordinateScale;
            while (coordinateScale > 1f && !FitsPolyline16(path, coordinateScale))
                coordinateScale /= 2f;
            return coordinateScale;
        }

        // ── Object table management ────────────────────────────────────────────

        private uint GetOrCreatePen(Pen pen)
            => GetOrCreatePen(pen, _logicalCoordinateScale);

        private uint GetOrCreatePen(Pen pen, float coordinateScale)
        {
            if (pen == null) return 0;
            var key = (pen.Color, pen.Width, pen.DashStyle, pen.DashOffset,
                pen.DashCap, pen.StartCap, pen.EndCap, pen.LineJoin,
                coordinateScale,
                string.Join(",", pen.DashPattern.Select(value =>
                    value.ToString("R", CultureInfo.InvariantCulture))));
            if (_objectTable.TryGetValue(key, out uint handle))
                return handle;

            handle = _nextHandle++;
            float[] dashPattern = GetDashPattern(pen);
            bool dashed = dashPattern.Length > 0;
            var penStyle = pen.DashStyle == DashStyle.Solid
                ? PenStyle.PS_SOLID
                : PenStyle.PS_USERSTYLE;
            uint width = (uint)Math.Max(1,
                Math.Round(pen.Width * (coordinateScale != 1f
                    ? coordinateScale * 600.0 / 72.0
                    : 1.0)));
            if (coordinateScale != 1f || dashed)
            {
                _records.Add(new EMR_EXTCREATEPEN
                {
                    IhPen = handle,
                    PenStyle = ToClassicPenStyle(pen, (uint)penStyle),
                    Width = width,
                    BrushStyle = (uint)BrushStyle.BS_SOLID,
                    ColorRef = (uint)(pen.Color.R | (pen.Color.G << 8) | (pen.Color.B << 16)),
                    StyleEntries = dashed
                        ? dashPattern.Select(value => (uint)Math.Max(1,
                            Math.Round(value * width))).ToArray()
                        : Array.Empty<uint>(),
                });
            }
            else
            {
                _records.Add(new EMR_CREATEPEN
                {
                    IhPen = handle,
                    PenStyle = penStyle,
                    WidthX = (int)Math.Max(1, pen.Width),
                    WidthY = 0,
                    ColorRef = ToColorRef(pen.Color),
                });
            }
            _objectTable[key] = handle;
            return handle;
        }

        private static uint ToClassicPenStyle(Pen pen, uint baseStyle)
        {
            uint style = baseStyle | 0x00010000;
            if (pen.StartCap == LineCap.Square && pen.EndCap == LineCap.Square)
                style |= 0x00000100;
            else if (pen.StartCap != LineCap.Round || pen.EndCap != LineCap.Round)
                style |= 0x00000200;

            switch (pen.LineJoin)
            {
                case LineJoin.Bevel:
                    style |= 0x00001000;
                    break;
                case LineJoin.Miter:
                case LineJoin.MiterClipped:
                    style |= 0x00002000;
                    break;
            }

            return style;
        }

        private static float[] GetDashPattern(Pen pen)
        {
            if (pen.DashStyle == DashStyle.Custom)
                return pen.DashPattern;

            switch (pen.DashStyle)
            {
                case DashStyle.Dash:
                    return new[] { 3f, 1f };
                case DashStyle.Dot:
                    return new[] { 1f, 1f };
                case DashStyle.DashDot:
                    return new[] { 3f, 1f, 1f, 1f };
                case DashStyle.DashDotDot:
                    return new[] { 3f, 1f, 1f, 1f, 1f, 1f };
                default:
                    return Array.Empty<float>();
            }
        }

        private uint GetOrCreateBrush(Brushes.Brush brush, RectangleF? fallbackBounds = null)
        {
            if (brush == null) return 0;
            var hatchBrush = brush as HatchBrush;
            Color color = brush is Brushes.SolidBrush sb
                ? sb.Color
                : hatchBrush?.ForegroundColor ?? Color.Black;
            bool sampledFallback = fallbackBounds.HasValue &&
                (brush is LinearGradientBrush || brush is PathGradientBrush ||
                 brush is TextureBrush textureValue &&
                 (textureValue.WrapMode == WrapMode.Clamp || !textureValue.Transform.IsIdentity));
            var key = hatchBrush == null
                ? brush is TextureBrush texture && texture.Image is Bitmap textureBitmap
                    ? (object)("texture", texture.WrapMode, textureBitmap.Width, textureBitmap.Height,
                        Convert.ToBase64String(textureBitmap._pixels),
                        texture.Transform.Elements.Length == 0 ? string.Empty : string.Join(",",
                            texture.Transform.Elements.Select(value => value.ToString("R", CultureInfo.InvariantCulture))),
                        sampledFallback && fallbackBounds.HasValue ? FormatBounds(fallbackBounds.Value) : string.Empty)
                    : brush is LinearGradientBrush linearGradient
                    ? (object)("linear-gradient", CreateLinearGradientKey(linearGradient),
                        sampledFallback && fallbackBounds.HasValue ? FormatBounds(fallbackBounds.Value) : string.Empty)
                    : brush is PathGradientBrush pathGradient
                    ? (object)("path-gradient", CreatePathGradientKey(pathGradient),
                        sampledFallback && fallbackBounds.HasValue ? FormatBounds(fallbackBounds.Value) : string.Empty)
                    : (object)("brush", color)
                : ("hatch", color, hatchBrush.HatchStyle, hatchBrush.BackgroundColor);
            if (_objectTable.TryGetValue(key, out uint handle))
                return handle;

            handle = _nextHandle++;
            if (brush is TextureBrush textureBrush && textureBrush.Image is Bitmap)
            {
                var (bmi, bits) = sampledFallback && fallbackBounds.HasValue
                    ? CreateSampledBrushDib(textureBrush, fallbackBounds.Value)
                    : CreateTextureDib(textureBrush);
                _records.Add(new EMR_CREATEDIBPATTERNBRUSHPT
                {
                    IhBrush = handle,
                    Usage = DIBColors.DibRgbColors,
                    Bmi = bmi,
                    Bits = bits,
                });
                _objectTable[key] = handle;
                return handle;
            }

            if (sampledFallback && fallbackBounds.HasValue)
            {
                var (bmi, bits) = CreateSampledBrushDib(brush, fallbackBounds.Value);
                _records.Add(new EMR_CREATEDIBPATTERNBRUSHPT
                {
                    IhBrush = handle,
                    Usage = DIBColors.DibRgbColors,
                    Bmi = bmi,
                    Bits = bits,
                });
                _objectTable[key] = handle;
                return handle;
            }

            var createBrush = new EMR_CREATEBRUSHINDIRECT
            {
                IhBrush = handle,
                LogBrush = new LogBrush
                {
                    BrushStyle = hatchBrush == null ? BrushStyle.BS_SOLID : BrushStyle.BS_HATCHED,
                    ColorRef = ToColorRef(color),
                    BrushHatch = hatchBrush == null
                        ? WmfHatchStyle.HS_HORIZONTAL
                        : ToWmfHatchStyle(hatchBrush.HatchStyle),
                },
            };
            _records.Add(createBrush);
            _objectTable[key] = handle;
            return handle;
        }

        private uint GetOrCreateFont(Text.Font font)
        {
            if (font == null) return 0;
            var key = ("font", font.Name, font.Size, font.Style);
            if (_objectTable.TryGetValue(key, out uint handle))
                return handle;

            handle = _nextHandle++;
            short weight = font.Bold ? (short)700 : (short)400;
            var createFont = new EMR_EXTCREATEFONTINDIRECTW
            {
                IhFont = handle,
                Font = new Metafile.Wmf.Objects.Font
                {
                    Height = (short)-Math.Max(1, Math.Round(font.Size * _dpiY / 72f)),
                    Width = 0,
                    Escapement = 0,
                    Orientation = 0,
                    Weight = weight,
                    CharSet = CharacterSet.EASTEUROPE_CHARSET,
                    OutPrecision = OutPrecision.OUT_TT_PRECIS,
                    PitchAndFamily = 34,
                    Italic = (byte)(font.Italic ? 1 : 0),
                    Underline = (byte)(font.Underline ? 1 : 0),
                    StrikeOut = (byte)(font.Strikeout ? 1 : 0),
                },
            };
            createFont.Font.Facename = font.Name;
            _records.Add(createFont);
            _objectTable[key] = handle;
            return handle;
        }

        public void SelectPen(Pen pen)
        {
            uint handle = GetOrCreatePen(pen);
            SelectPenHandle(handle);
        }

        internal void SelectPen(Pen pen, float coordinateScale)
        {
            uint handle = GetOrCreatePen(pen, coordinateScale);
            SelectPenHandle(handle);
        }

        private void SelectPenHandle(uint handle)
        {
            if (handle == 0 || handle == _currentPenHandle) return;
            _currentPenHandle = handle;
            _records.Add(new EMR_SELECTOBJECT { ObjectIndex = handle });
        }

        public void SelectBrush(Brushes.Brush brush, RectangleF? fallbackBounds = null)
        {
            uint handle = GetOrCreateBrush(brush, fallbackBounds);
            if (handle == 0 || handle == _currentBrushHandle) return;
            _currentBrushHandle = handle;
            if (brush is HatchBrush hatchBrush)
            {
                BackgroundMode hatchBackgroundMode = hatchBrush.BackgroundColor.A == 0
                    ? BackgroundMode.TRANSPARENT : BackgroundMode.OPAQUE;
                _currentBackgroundMode = hatchBackgroundMode;
                _records.Add(new EMR_SETBKMODE
                {
                    BackgroundMode = hatchBackgroundMode,
                });
                if (hatchBrush.BackgroundColor.A != 0)
                {
                    _records.Add(new EMR_SETBKCOLOR
                    {
                        Color = ToColorRef(hatchBrush.BackgroundColor),
                    });
                }
            }
            if (fallbackBounds.HasValue &&
                (brush is LinearGradientBrush || brush is PathGradientBrush ||
                 brush is TextureBrush))
            {
                RectangleF bounds = fallbackBounds.Value;
                _records.Add(new EMR_SETBRUSHORGEX
                {
                    X = (int)Math.Floor(bounds.X),
                    Y = (int)Math.Floor(bounds.Y),
                });
            }
            _records.Add(new EMR_SELECTOBJECT { ObjectIndex = handle });
        }

        public void SelectFont(Text.Font font)
        {
            uint handle = GetOrCreateFont(font);
            if (handle == 0 || handle == _currentFontHandle) return;
            _currentFontHandle = handle;
            _records.Add(new EMR_SELECTOBJECT { ObjectIndex = handle });
        }

        // ── State records ─────────────────────────────────────────────────────

        public void SetTransform(Matrix matrix)
        {
            var e = matrix.Elements;
            _records.Add(new EMR_SETWORLDTRANSFORM
            {
                M11 = e[0], M12 = e[1],
                M21 = e[2], M22 = e[3],
                Dx = e[4], Dy = e[5],
            });
            SetWorldTransform(matrix);
        }

        public void SaveDC()
        {
            _records.Add(new EMR_SAVEDC());
            if (!CanEmitEmfPlus) return;

            uint stackIndex = _nextEmfPlusStackIndex++;
            _emfPlusStateStack.Push(stackIndex);
            WriteEmfPlusRecord(EmfPlusSave, 0, BitConverter.GetBytes(stackIndex));
        }

        public void RestoreDC()
        {
            _records.Add(new EMR_RESTOREDC { SavedDC = -1 });
            if (!CanEmitEmfPlus || _emfPlusStateStack.Count == 0) return;

            uint stackIndex = _emfPlusStateStack.Pop();
            WriteEmfPlusRecord(EmfPlusRestore, 0, BitConverter.GetBytes(stackIndex));
        }

        public void BeginContainer()
        {
            _records.Add(new EMR_SAVEDC());
            if (CanEmitEmfPlus)
                WriteEmfPlusRecord(EmfPlusBeginContainerNoParams, 0,
                    BitConverter.GetBytes(0u));
        }

        public void BeginContainer(RectangleF destination, RectangleF source, GraphicsUnit unit)
        {
            _records.Add(new EMR_SAVEDC());
            if (!CanEmitEmfPlus) return;

            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);
            WriteRectangle(writer, destination);
            WriteRectangle(writer, source);
            writer.Write((uint)ToEmfPlusUnit(unit));
            WriteEmfPlusRecord(EmfPlusBeginContainer, ToEmfPlusUnit(unit), stream.ToArray());
        }

        public void EndContainer()
        {
            _records.Add(new EMR_RESTOREDC { SavedDC = -1 });
            if (CanEmitEmfPlus)
                WriteEmfPlusRecord(EmfPlusEndContainer, 0, BitConverter.GetBytes(0u));
        }

        public void SetPolyFillMode(FillMode fillMode)
        {
            _records.Add(new EMR_SETPOLYFILLMODE
            {
                PolyFillMode = fillMode == FillMode.Alternate
                    ? PolyFillMode.ALTERNATE
                    : PolyFillMode.WINDING,
            });
        }

        public void SetTextColor(Color color)
        {
            _records.Add(new EMR_SETTEXTCOLOR
            {
                Color = new ColorRef { Red = color.R, Green = color.G, Blue = color.B, Reserved = 0 },
            });
        }

        public void IntersectClipRect(RectangleF rect)
            => SetClipRect(rect, CombineMode.Intersect);

        // ── Path records ──────────────────────────────────────────────────────

        public void BuildPath(GraphicsPath path, bool allowOpenBezierRecord = false)
        {
            _records.Add(new EMR_BEGINPATH());

            var points = path.PathPoints;
            var types = path.PathTypes;

                if (_logicalCoordinateScale != 1f &&
                    (IsClosedBezierPath(points, types) ||
                     (allowOpenBezierRecord && IsOpenBezierPath(points, types))))
                {
                    _records.Add(new EMR_POLYBEZIER16
                    {
                        Bounds = new RectL { Left = 0, Top = 0, Right = -1, Bottom = -1 },
                        Count = (uint)points.Length,
                        Points = points.Select(point => ToPointS(ToPointL(point))).ToArray(),
                    });
                    _records.Add(new EMR_ENDPATH());
                    return;
                }

            for (int i = 0; i < points.Length; i++)
            {
                byte type = types[i];
                PathPointType ptType = (PathPointType)(type & (byte)PathPointType.PathTypeMask);
                bool closeSubpath = (type & (byte)PathPointType.CloseSubpath) != 0;

                if (ptType == PathPointType.Start)
                {
                    _records.Add(new EMR_MOVETOEX { Offset = ToPointL(points[i]) });
                }
                else if (ptType == PathPointType.Line)
                {
                    var linePoints = new List<PointL>();
                    bool lineClosesSubpath = false;
                    while (i < points.Length &&
                           (PathPointType)(types[i] & (byte)PathPointType.PathTypeMask) == PathPointType.Line)
                    {
                        PointL linePoint = ToPointL(points[i]);
                        linePoints.Add(linePoint);
                        lineClosesSubpath |=
                            (types[i] & (byte)PathPointType.CloseSubpath) != 0;
                        i++;
                    }
                    i--;

                    if (path.PreferPolyline16 && _logicalCoordinateScale != 1f &&
                        linePoints.All(p => p.x >= short.MinValue && p.x <= short.MaxValue
                                         && p.y >= short.MinValue && p.y <= short.MaxValue))
                    {
                        _records.Add(new EMR_POLYLINETO16
                        {
                            Bounds = new RectL { Left = 0, Top = 0, Right = -1, Bottom = -1 },
                            Count = (uint)linePoints.Count,
                            Points = linePoints.Select(ToPointS).ToArray(),
                        });
                    }
                    else
                    {
                        foreach (PointL linePoint in linePoints)
                            _records.Add(new EMR_LINETO { Point = linePoint });
                    }

                    if (lineClosesSubpath)
                        _records.Add(new EMR_CLOSEFIGURE());
                }
                else if (ptType == PathPointType.Bezier)
                {
                    var bezierPoints = new List<PointL>();
                    bool bezierClosesSubpath = false;
                    while (i < points.Length &&
                           (PathPointType)(types[i] & (byte)PathPointType.PathTypeMask) == PathPointType.Bezier)
                    {
                        bezierPoints.Add(ToPointL(points[i]));
                        bezierClosesSubpath =
                            (types[i] & (byte)PathPointType.CloseSubpath) != 0;
                        i++;
                    }
                    i--;

                    if (bezierPoints.Count >= 3)
                    {
                        if (_logicalCoordinateScale != 1f &&
                            bezierPoints.All(p => p.x >= short.MinValue && p.x <= short.MaxValue
                                               && p.y >= short.MinValue && p.y <= short.MaxValue))
                        {
                            _records.Add(new EMR_POLYBEZIERTO16
                            {
                                Bounds = new RectL { Left = 0, Top = 0, Right = -1, Bottom = -1 },
                                Count = (uint)bezierPoints.Count,
                                Points = bezierPoints.Select(ToPointS).ToArray(),
                            });
                        }
                        else
                        {
                            _records.Add(new EMR_POLYBEZIERTO
                            {
                                Bounds = ToRectL(GetBounds(path)),
                                Count = (uint)bezierPoints.Count,
                                Points = bezierPoints.ToArray(),
                            });
                        }
                    }

                    if (bezierClosesSubpath)
                        _records.Add(new EMR_CLOSEFIGURE());
                }

                if (closeSubpath)
                    _records.Add(new EMR_CLOSEFIGURE());
            }

            _records.Add(new EMR_ENDPATH());
        }

        public void StrokePath(RectangleF bounds)
            => _records.Add(new EMR_STROKEPATH { Bounds = ToRectL(bounds) });

        public void FillPath(RectangleF bounds)
            => _records.Add(new EMR_FILLPATH { Bounds = ToRectL(bounds) });

        public void FillHatchRectangle(RectangleF rect, HatchBrush brush)
        {
            if (brush == null) return;

            var key = ("dib-hatch", brush.HatchStyle, brush.ForegroundColor, brush.BackgroundColor);
            if (!_objectTable.TryGetValue(key, out uint handle))
            {
                handle = _nextHandle++;
                var (bmi, bits) = CreateHatchDib(brush);
                _records.Add(new EMR_CREATEDIBPATTERNBRUSHPT
                {
                    IhBrush = handle,
                    Usage = DIBColors.DibRgbColors,
                    Bmi = bmi,
                    Bits = bits,
                });
                _objectTable[key] = handle;
            }

            if (handle != _currentBrushHandle)
            {
                _currentBrushHandle = handle;
                _records.Add(new EMR_SELECTOBJECT { ObjectIndex = handle });
            }

            RectL bounds = ToRectL(rect);
            _records.Add(new EMR_BITBLT
            {
                Bounds = bounds,
                XDest = bounds.Left,
                YDest = bounds.Top,
                CxDest = Math.Max(0, bounds.Right - bounds.Left),
                CyDest = Math.Max(0, bounds.Bottom - bounds.Top),
                BitBltRasterOperation = EmfRasterOperation.PATCOPY,
                XSrc = 0,
                YSrc = 0,
                XFormSrc = new XForm { M11 = 1f, M22 = 1f },
                BkColorSrc = ToColorRef(brush.BackgroundColor),
                UsageSrc = DIBColors.DibRgbColors,
            });
        }

        public bool FillPolygons16(GraphicsPath path, Brushes.Brush brush)
        {
            if (path == null || path.PointCount == 0 || brush == null) return false;

            var polygons = new List<PointS[]>();
            var current = new List<PointS>();
            var points = path.PathPoints;
            var types = path.PathTypes;

            for (int i = 0; i < points.Length; i++)
            {
                if ((types[i] & (byte)PathPointType.PathTypeMask) == (byte)PathPointType.Start)
                {
                    AddPolygon16(polygons, current);
                    current = new List<PointS>();
                }

                var point = ToPointL(points[i]);
                if (point.x < short.MinValue || point.x > short.MaxValue ||
                    point.y < short.MinValue || point.y > short.MaxValue)
                {
                    return false;
                }

                current.Add(ToPointS(point));

                if ((types[i] & (byte)PathPointType.CloseSubpath) != 0)
                {
                    AddPolygon16(polygons, current);
                    current = new List<PointS>();
                }
            }

            AddPolygon16(polygons, current);

            if (polygons.Count == 0) return false;

            SelectBrush(brush, GetBounds(path));
            _records.Add(new EMR_POLYPOLYGON16
            {
                Bounds = ToRectL(GetBounds(path)),
                NumberOfPolys = (uint)polygons.Count,
                Count = (uint)polygons.Sum(p => p.Length),
                PolyCounts = polygons.Select(p => (uint)p.Length).ToArray(),
                Points = polygons.SelectMany(p => p).ToArray(),
            });
            return true;
        }

        public bool FillPolygon16(GraphicsPath path, Brush brush)
        {
            if (brush == null || !TryGetSinglePolygon16(path, out var points)) return false;

            SelectBrush(brush, GetBounds(path));
            if (path.FillPolygon16WithoutOutline)
                SelectNullPen();
            _records.Add(new EMR_POLYGON16
            {
                Bounds = ToRectL(GetBounds(path)),
                Count = (uint)points.Length,
                Points = points,
            });
            return true;
        }

        // A polygon record also outlines with the selected pen; the stock null pen keeps a
        // fill from picking up a stale outline.
        private void SelectNullPen()
        {
            const uint StockNullPen = 0x80000008;
            if (_currentPenHandle == StockNullPen) return;
            _currentPenHandle = StockNullPen;
            _records.Add(new EMR_SELECTOBJECT { ObjectIndex = StockNullPen });
        }

        public bool StrokePolygon16(GraphicsPath path, Pen pen)
        {
            if (pen == null || !TryGetSinglePolygon16(path, out var points)) return false;

            SelectPen(pen);
            _records.Add(new EMR_POLYGON16
            {
                Bounds = ToRectL(GetBounds(path)),
                Count = (uint)points.Length,
                Points = points,
            });
            return true;
        }

        public bool StrokePolyline16(GraphicsPath path, Pen pen)
            => StrokePolyline16(path, pen, _logicalCoordinateScale);

        internal bool StrokePolyline16(GraphicsPath path, Pen pen, float coordinateScale)
        {
            if (path != null && path.PreferPolyPolygon16)
                return StrokePolyPolyline16(path, pen, coordinateScale);

            if (pen == null || path == null || path.PointCount < 2)
                return false;

            var sourcePoints = path.PathPoints;
            var sourceTypes = path.PathTypes;
            if (sourceTypes.Length != sourcePoints.Length ||
                (sourceTypes[0] & (byte)PathPointType.PathTypeMask) != (byte)PathPointType.Start)
                return false;

            var points = new PointS[sourcePoints.Length];
            for (int i = 0; i < sourcePoints.Length; i++)
            {
                if (i > 0 && (sourceTypes[i] & (byte)PathPointType.PathTypeMask) !=
                    (byte)PathPointType.Line)
                    return false;

                var point = ToPointL(sourcePoints[i], coordinateScale);
                if (point.x < short.MinValue || point.x > short.MaxValue ||
                    point.y < short.MinValue || point.y > short.MaxValue)
                    return false;
                points[i] = ToPointS(point);
            }

            SelectPen(pen, coordinateScale);
            _records.Add(new EMR_POLYLINE16
            {
                Bounds = ToRectL(GetBounds(path)),
                Count = (uint)points.Length,
                Points = points,
            });
            return true;
        }

        internal bool StrokePolyPolyline16(GraphicsPath path, Pen pen, float coordinateScale)
        {
            if (pen == null || path == null || path.PointCount < 2)
                return false;

            PointF[] sourcePoints = path.PathPoints;
            byte[] sourceTypes = path.PathTypes;
            if (sourceTypes.Length != sourcePoints.Length ||
                (sourceTypes[0] & (byte)PathPointType.PathTypeMask) !=
                    (byte)PathPointType.Start)
                return false;

            var polylines = new List<PointS[]>();
            var current = new List<PointS>();
            for (int index = 0; index < sourcePoints.Length; index++)
            {
                byte type = (byte)(sourceTypes[index] & (byte)PathPointType.PathTypeMask);
                if (type == (byte)PathPointType.Start)
                {
                    if (current.Count >= 2)
                        polylines.Add(current.ToArray());
                    current = new List<PointS>();
                }
                else if (type != (byte)PathPointType.Line)
                {
                    return false;
                }

                PointL point = ToPointL(sourcePoints[index], coordinateScale);
                if (point.x < short.MinValue || point.x > short.MaxValue ||
                    point.y < short.MinValue || point.y > short.MaxValue)
                    return false;
                current.Add(ToPointS(point));
            }

            if (current.Count >= 2)
                polylines.Add(current.ToArray());
            if (polylines.Count == 0)
                return false;

            SelectPen(pen, coordinateScale);
            _records.Add(new EMR_POLYPOLYLINE16
            {
                Bounds = ToRectL(GetBounds(path)),
                NumberOfPolys = (uint)polylines.Count,
                Count = (uint)polylines.Sum(polyline => polyline.Length),
                PolyCounts = polylines.Select(polyline => (uint)polyline.Length).ToArray(),
                Points = polylines.SelectMany(polyline => polyline).ToArray(),
            });
            return true;
        }

        private bool TryGetSinglePolygon16(GraphicsPath path, out PointS[] polygon)
        {
            polygon = Array.Empty<PointS>();
            if (path == null || path.PointCount < 3) return false;

            var sourcePoints = path.PathPoints;
            var sourceTypes = path.PathTypes;
            if (sourceTypes.Length != sourcePoints.Length ||
                (sourceTypes[0] & (byte)PathPointType.PathTypeMask) != (byte)PathPointType.Start ||
                (sourceTypes[sourceTypes.Length - 1] & (byte)PathPointType.CloseSubpath) == 0)
                return false;

            polygon = new PointS[sourcePoints.Length];
            for (int i = 0; i < sourcePoints.Length; i++)
            {
                byte type = (byte)(sourceTypes[i] & (byte)PathPointType.PathTypeMask);
                if (i > 0 && type != (byte)PathPointType.Line)
                    return false;

                var point = ToPointL(sourcePoints[i]);
                if (point.x < short.MinValue || point.x > short.MaxValue ||
                    point.y < short.MinValue || point.y > short.MaxValue)
                {
                    polygon = Array.Empty<PointS>();
                    return false;
                }
                polygon[i] = ToPointS(point);
            }
            return true;
        }

        private static void AddPolygon16(List<PointS[]> polygons, List<PointS> points)
        {
            if (points.Count >= 3)
                polygons.Add(points.ToArray());
        }

            private static bool IsClosedBezierPath(PointF[] points, byte[] types)
            {
                if (points.Length < 4 || types.Length != points.Length ||
                    (types[0] & (byte)PathPointType.PathTypeMask) != (byte)PathPointType.Start ||
                    points[0].X != points[points.Length - 1].X ||
                    points[0].Y != points[points.Length - 1].Y)
                    return false;

                for (int i = 1; i < types.Length; i++)
                    if ((types[i] & (byte)PathPointType.PathTypeMask) != (byte)PathPointType.Bezier)
                        return false;
                return true;
            }

            private static bool IsOpenBezierPath(PointF[] points, byte[] types)
            {
                if (points.Length < 4 || types.Length != points.Length ||
                    (types[0] & (byte)PathPointType.PathTypeMask) != (byte)PathPointType.Start ||
                    points[0].X == points[points.Length - 1].X &&
                    points[0].Y == points[points.Length - 1].Y)
                    return false;

                for (int i = 1; i < types.Length; i++)
                    if ((types[i] & (byte)PathPointType.PathTypeMask) != (byte)PathPointType.Bezier)
                        return false;
                return true;
            }

        private static RectangleF GetBounds(GraphicsPath path)
        {
            var points = path.PathPoints;
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            foreach (var point in points)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        public void StrokeAndFillPath(RectangleF bounds)
            => _records.Add(new EMR_STROKEANDFILLPATH { Bounds = ToRectL(bounds) });

        // ── Drawing records ───────────────────────────────────────────────────

        public void StretchDIBits(RectangleF destRect, Bitmap bitmap)
        {
            var (bmi, bits) = BitmapToDib(bitmap);
            _records.Add(new EMR_STRETCHDIBITS
            {
                Bounds = ToRectL(destRect),
                XDest = (int)Math.Floor(destRect.X),
                YDest = (int)Math.Floor(destRect.Y),
                XSrc = 0,
                YSrc = 0,
                CxSrc = bitmap.Width,
                CySrc = bitmap.Height,
                OffBmiSrc = 0,
                CbBmiSrc = (uint)bmi.Length,
                OffBitsSrc = 0,
                CbBitsSrc = (uint)bits.Length,
                UsageSrc = DIBColors.DibRgbColors,
                BitBltRasterOperation = EmfRasterOperation.SRCCOPY,
                CxDest = (int)destRect.Width,
                CyDest = (int)destRect.Height,
                BmiSrc = bmi,
                BitsSrc = bits,
            });
        }

        private void SetBackgroundMode(BackgroundMode mode)
        {
            if (_currentBackgroundMode == mode)
                return;
            _currentBackgroundMode = mode;
            _records.Add(new EMR_SETBKMODE { BackgroundMode = mode });
        }

        public void ExtTextOutW(string text, Text.Font font, Color color, RectangleF layoutRect)
        {
            ExtTextOutW(text, font, color, layoutRect, null);
        }

        public void ExtTextOutW(string text, Text.Font font, Color color,
                                RectangleF layoutRect, int[]? advances)
        {
            SetBackgroundMode(BackgroundMode.TRANSPARENT);
            SelectFont(font);
            SetTextColor(color);

            var outputString = System.Text.Encoding.Unicode.GetBytes(text);
            var emrText = new EmrText
            {
                Reference = new PointL { x = (int)layoutRect.X, y = (int)layoutRect.Y },
                Chars = (uint)text.Length,
                offString = 0,
                // Match Excel's transparent text output: no opaque/clipping option and
                // the unused rectangle set to the sentinel values used by Interop EMFs.
                Options = 0,
                Rectangle = new RectL
                {
                    Left = -1,
                    Top = -1,
                    Right = -1,
                    Bottom = -1,
                },
                offDx = 0,
                OutputString = outputString,
            };

            if (advances != null && advances.Length == text.Length)
            {
                using var dxStream = new System.IO.MemoryStream();
                using var dxWriter = new System.IO.BinaryWriter(dxStream);
                foreach (int advance in advances)
                    dxWriter.Write(advance);
                emrText.OutputDx = dxStream.ToArray();
            }

            _records.Add(new EMR_EXTTEXTOUTW
            {
                Bounds = ToRectL(layoutRect),
                IGraphicsMode = GraphicsMode.GM_COMPATIBLE,
                ExScale = 1f,
                EyScale = 1f,
                EmrText = emrText,
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private PointL ToPointL(PointF p)
            => ToPointL(p, _logicalCoordinateScale);

        private static PointL ToPointL(PointF p, float coordinateScale)
            => new PointL
            {
                x = (int)Math.Round(p.X * coordinateScale),
                y = (int)Math.Round(p.Y * coordinateScale),
            };

        private static bool FitsPolyline16(GraphicsPath path, float coordinateScale)
        {
            if (path == null || path.PointCount < 2)
                return false;

            foreach (PointF point in path.PathPoints)
            {
                PointL scaled = ToPointL(point, coordinateScale);
                if (scaled.x < short.MinValue || scaled.x > short.MaxValue ||
                    scaled.y < short.MinValue || scaled.y > short.MaxValue)
                    return false;
            }

            return true;
        }

        private static PointS ToPointS(PointL p)
            => new PointS { x = p.x, y = p.y };

        private static ColorRef ToColorRef(Color color)
        {
            // Classic GDI brushes have no alpha channel. The image canvas is
            // cleared to white, so preserve translucent fill appearance by
            // compositing against that background before writing ColorRef.
            if (color.A < 255)
            {
                int alpha = color.A;
                return new ColorRef
                {
                    Red = (byte)((color.R * alpha + 255 * (255 - alpha) + 127) / 255),
                    Green = (byte)((color.G * alpha + 255 * (255 - alpha) + 127) / 255),
                    Blue = (byte)((color.B * alpha + 255 * (255 - alpha) + 127) / 255),
                    Reserved = 0,
                };
            }

            return new ColorRef { Red = color.R, Green = color.G, Blue = color.B, Reserved = 0 };
        }

        private static RectL ToRectL(RectangleF r)
            => new RectL
            {
                Left = (int)Math.Round(r.Left),
                Top = (int)Math.Round(r.Top),
                Right = (int)Math.Round(r.Right),
                Bottom = (int)Math.Round(r.Bottom),
            };

        private static WmfHatchStyle ToWmfHatchStyle(Drawing2D.HatchStyle hatchStyle)
            => hatchStyle switch
            {
                Drawing2D.HatchStyle.Vertical => WmfHatchStyle.HS_VERTICAL,
                Drawing2D.HatchStyle.ForwardDiagonal => WmfHatchStyle.HS_FDIAGONAL,
                Drawing2D.HatchStyle.BackwardDiagonal => WmfHatchStyle.HS_BDIAGONAL,
                Drawing2D.HatchStyle.Cross => WmfHatchStyle.HS_CROSS,
                Drawing2D.HatchStyle.DiagonalCross => WmfHatchStyle.HS_DIAGCROSS,
                _ => WmfHatchStyle.HS_HORIZONTAL,
            };

        private static (byte[] bmi, byte[] bits) BitmapToDib(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            PixelFormat format = bitmap.PixelFormat;
            int bpp = format == PixelFormat.Format24bppRgb ? 24 : 32;

            int stride = ((width * bpp + 31) / 32) * 4;
            int headerSize = 40;
            int pixelDataSize = stride * height;

            var bmi = new byte[headerSize];
            BitConverter.GetBytes((uint)headerSize).CopyTo(bmi, 0);
            BitConverter.GetBytes(width).CopyTo(bmi, 4);
            BitConverter.GetBytes(height).CopyTo(bmi, 8);
            BitConverter.GetBytes((ushort)1).CopyTo(bmi, 12);
            BitConverter.GetBytes((ushort)bpp).CopyTo(bmi, 14);
            BitConverter.GetBytes((uint)0).CopyTo(bmi, 16);
            BitConverter.GetBytes((uint)pixelDataSize).CopyTo(bmi, 20);
            for (int i = 24; i < headerSize; i++)
                bmi[i] = 0;

            var bits = new byte[pixelDataSize];
            int srcStride = Bitmap.CalcStride(width, format);
            int copyStride = Math.Min(srcStride, stride);

            for (int y = 0; y < height; y++)
            {
                int srcOffset = y * srcStride;
                int dstOffset = (height - 1 - y) * stride;
                if (bpp == 24)
                {
                    Buffer.BlockCopy(bitmap._pixels, srcOffset, bits, dstOffset, copyStride);
                    continue;
                }

                for (int x = 0; x < width; x++)
                {
                    int source = srcOffset + x * 4;
                    int target = dstOffset + x * 4;
                    int alpha = bitmap._pixels[source + 3];
                    bits[target] = (byte)((bitmap._pixels[source] * alpha + 255 * (255 - alpha) + 127) / 255);
                    bits[target + 1] = (byte)((bitmap._pixels[source + 1] * alpha + 255 * (255 - alpha) + 127) / 255);
                    bits[target + 2] = (byte)((bitmap._pixels[source + 2] * alpha + 255 * (255 - alpha) + 127) / 255);
                    bits[target + 3] = 255;
                }
            }

            return (bmi, bits);
        }

        private static (byte[] bmi, byte[] bits) CreateHatchDib(HatchBrush brush)
        {
            const int width = 8;
            const int height = 8;
            const int headerSize = 52;
            const int stride = width * 4;

            var bmi = new byte[headerSize];
            BitConverter.GetBytes((uint)40).CopyTo(bmi, 0);
            BitConverter.GetBytes(width).CopyTo(bmi, 4);
            BitConverter.GetBytes(height).CopyTo(bmi, 8);
            BitConverter.GetBytes((ushort)1).CopyTo(bmi, 12);
            BitConverter.GetBytes((ushort)32).CopyTo(bmi, 14);
            BitConverter.GetBytes((uint)3).CopyTo(bmi, 16);
            BitConverter.GetBytes((uint)(stride * height)).CopyTo(bmi, 20);
            BitConverter.GetBytes(0x00FF0000u).CopyTo(bmi, 40);
            BitConverter.GetBytes(0x0000FF00u).CopyTo(bmi, 44);
            BitConverter.GetBytes(0x000000FFu).CopyTo(bmi, 48);

            var bits = new byte[stride * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var color = HatchRenderer.Sample(brush, x, y);
                int offset = y * stride + x * 4;
                bits[offset] = color.B;
                bits[offset + 1] = color.G;
                bits[offset + 2] = color.R;
            }

            return (bmi, bits);
        }

        private static (byte[] bmi, byte[] bits) CreateTextureDib(TextureBrush brush)
        {
            var source = (Bitmap)brush.Image;
            int tileWidth = brush.WrapMode == WrapMode.TileFlipX ||
                brush.WrapMode == WrapMode.TileFlipXY ? source.Width * 2 : source.Width;
            int tileHeight = brush.WrapMode == WrapMode.TileFlipY ||
                brush.WrapMode == WrapMode.TileFlipXY ? source.Height * 2 : source.Height;
            using var tile = new Bitmap(tileWidth, tileHeight, PixelFormat.Format32bppArgb);

            for (int y = 0; y < tileHeight; y++)
            for (int x = 0; x < tileWidth; x++)
            {
                int tileX = x / source.Width;
                int tileY = y / source.Height;
                int sourceX = x % source.Width;
                int sourceY = y % source.Height;
                if ((tileX & 1) != 0 &&
                    (brush.WrapMode == WrapMode.TileFlipX || brush.WrapMode == WrapMode.TileFlipXY))
                    sourceX = source.Width - sourceX - 1;
                if ((tileY & 1) != 0 &&
                    (brush.WrapMode == WrapMode.TileFlipY || brush.WrapMode == WrapMode.TileFlipXY))
                    sourceY = source.Height - sourceY - 1;
                tile.SetPixel(x, y, source.GetPixel(sourceX, sourceY));
            }

            return BitmapToDib(tile);
        }

        private static (byte[] bmi, byte[] bits) CreateSampledBrushDib(Brush brush, RectangleF bounds)
        {
            int width = Math.Max(1, (int)Math.Ceiling(Math.Abs(bounds.Width)));
            int height = Math.Max(1, (int)Math.Ceiling(Math.Abs(bounds.Height)));
            using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                bitmap.SetPixel(x, y, BrushSampler.Sample(brush,
                    bounds.X + x + 0.5f, bounds.Y + y + 0.5f,
                    InterpolationMode.NearestNeighbor));
            return BitmapToDib(bitmap);
        }
    }
}