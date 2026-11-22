using System;
using System.Collections.Generic;
using DRIT.Drawing.Brushes;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Font.Core;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Metafile;
using DRIT.Drawing.Metafile.Emf.Records;
using DRIT.Drawing.Metafile.Emf.Records.DrawingRecordTypes;
using DRIT.Drawing.Metafile.Emf.Records.StateRecordTypes;
using DRIT.Drawing.Pens;
using DRIT.Drawing.Rendering;
using DRIT.Drawing.Text;
using DRITFont = DRIT.Drawing.Text.Font;
using DRegion = DRIT.Drawing.Drawing2D.Region;
using DMatrix = DRIT.Drawing.Drawing2D.Matrix;
using DGraphicsPath = DRIT.Drawing.Drawing2D.GraphicsPath;
using EmfRecord = DRIT.Drawing.Metafile.Emf.Records.Record;

namespace DRIT.Drawing.Vector.Emf
{
    /// <summary>
    /// An <see cref="IDrawingContext"/> implementation that accumulates EMF
    /// records into a <see cref="MetafileDocument"/>. This is the VDM->EMF
    /// bridge: replay a <see cref="VectorScene"/> onto this context to produce
    /// EMF output. Manages the EMF object table (pen/brush/font handles),
    /// world transforms, clip regions, state stack, and path records.
    /// </summary>
    internal sealed class EmfDrawingContext : IDrawingContext
    {
        private readonly int _width;
        private readonly int _height;
        private readonly float _dpiX;
        private readonly float _dpiY;
        private readonly EmfRecordMapper _mapper;
        private readonly List<EmfRecord> _allRecords = new List<EmfRecord>();

        private DMatrix _transform = new DMatrix();
        private DRegion _clip = new DRegion();
        private SmoothingMode _smoothingMode = SmoothingMode.None;
        private InterpolationMode _interpolationMode = InterpolationMode.NearestNeighbor;
        private CompositingMode _compositingMode = CompositingMode.SourceOver;
        private CompositingQuality _compositingQuality = CompositingQuality.Default;
        private PixelOffsetMode _pixelOffsetMode = PixelOffsetMode.Default;
        private TextRenderingHint _textRenderingHint = TextRenderingHint.SystemDefault;
        private GraphicsUnit _pageUnit = GraphicsUnit.Pixel;
        private float _pageScale = 1f;
        private Point _renderingOrigin = new Point(0, 0);

        private readonly Stack<StateFrame> _stateStack = new Stack<StateFrame>();
        private bool _disposed;

        public EmfDrawingContext(int width, int height, float dpiX = 96f, float dpiY = 96f,
            float logicalCoordinateScale = 1f)
            : this(width, height, dpiX, dpiY, logicalCoordinateScale,
                logicalCoordinateScale != 1f ? EmfOutputMode.EmfPlusDual : EmfOutputMode.Classic)
        {
        }

        internal EmfDrawingContext(int width, int height, float dpiX, float dpiY,
            float logicalCoordinateScale, EmfOutputMode outputMode)
        {
            _mapper = new EmfRecordMapper(outputMode);
            _width = width;
            _height = height;
            _dpiX = dpiX > 0 ? dpiX : 96f;
            _dpiY = dpiY > 0 ? dpiY : 96f;
            _mapper.SetLogicalCoordinateScale(
                logicalCoordinateScale,
                _dpiX,
                _dpiY,
                _smoothingMode,
                _interpolationMode,
                _compositingMode,
                _compositingQuality,
                _pixelOffsetMode,
                _textRenderingHint,
                _pageUnit,
                _pageScale,
                _renderingOrigin);
            _clip.MakeInfinite();
        }

        public int Width => _width;
        public int Height => _height;
        public float DpiX => _dpiX;
        public float DpiY => _dpiY;

        public DMatrix Transform
        {
            get => _transform;
            set
            {
                _transform = value ?? new DMatrix();
                _mapper.SetTransform(_transform);
            }
        }

        public DRegion Clip
        {
            get => _clip;
            set => _clip = value ?? new DRegion();
        }

        public SmoothingMode SmoothingMode
        {
            get => _smoothingMode;
            set { _smoothingMode = value; _mapper.SetSmoothingMode(value); }
        }

        public InterpolationMode InterpolationMode
        {
            get => _interpolationMode;
            set { _interpolationMode = value; _mapper.SetInterpolationMode(value); }
        }

        public CompositingMode CompositingMode
        {
            get => _compositingMode;
            set { _compositingMode = value; _mapper.SetCompositingMode(value); }
        }

        public CompositingQuality CompositingQuality
        {
            get => _compositingQuality;
            set { _compositingQuality = value; _mapper.SetCompositingQuality(value); }
        }

        public PixelOffsetMode PixelOffsetMode
        {
            get => _pixelOffsetMode;
            set { _pixelOffsetMode = value; _mapper.SetPixelOffsetMode(value); }
        }

        public TextRenderingHint TextRenderingHint
        {
            get => _textRenderingHint;
            set { _textRenderingHint = value; _mapper.SetTextRenderingHint(value); }
        }

        public GraphicsUnit PageUnit
        {
            get => _pageUnit;
            set { _pageUnit = value; _mapper.SetPageTransform(value, _pageScale); }
        }

        public float PageScale
        {
            get => _pageScale;
            set { _pageScale = value; _mapper.SetPageTransform(_pageUnit, value); }
        }

        public Point RenderingOrigin
        {
            get => _renderingOrigin;
            set { _renderingOrigin = value; _mapper.SetRenderingOrigin(value); }
        }

        public void ResetTransform()
        {
            _transform.Reset();
            _mapper.SetTransform(_transform);
        }

        public void MultiplyTransform(DMatrix m, MatrixOrder order = MatrixOrder.Prepend)
        {
            _transform.Multiply(m, order);
            _mapper.SetTransform(_transform);
        }

        public void RotateTransform(float angle, MatrixOrder order = MatrixOrder.Prepend)
        {
            _transform.Rotate(angle, order);
            _mapper.SetTransform(_transform);
        }

        public void ScaleTransform(float sx, float sy, MatrixOrder order = MatrixOrder.Prepend)
        {
            _transform.Scale(sx, sy, order);
            _mapper.SetTransform(_transform);
        }

        public void TranslateTransform(float dx, float dy, MatrixOrder order = MatrixOrder.Prepend)
        {
            _transform.Translate(dx, dy, order);
            _mapper.SetTransform(_transform);
        }

        public void SetClip(RectangleF rect, CombineMode mode = CombineMode.Replace)
        {
            _clip = new DRegion(rect);
            _mapper.SetClipRect(rect, mode);
        }

        public void SetClip(DGraphicsPath path, CombineMode mode = CombineMode.Replace)
        {
            _clip = new DRegion(path);
            _mapper.SetClipPath(path, mode);
        }

public void SetClip(DRegion region, CombineMode mode = CombineMode.Replace)
        {
            _clip = region.Clone();
            if (_mapper.SetClipRegion(region, mode))
                return;

            using var path = region.GetSinglePath();
            if (path != null)
            {
                _mapper.SetClipPath(path, mode);
                return;
            }

            var rect = region.GetBounds();
            if (rect.Width > 0 && rect.Height > 0 && !float.IsInfinity(rect.Width) && !float.IsInfinity(rect.Height))
                _mapper.SetClipRect(rect, mode);
        }

        public void IntersectClip(RectangleF rect)
        {
            _clip.Intersect(rect);
            _mapper.IntersectClipRect(rect);
        }

        public void IntersectClip(DRegion region)
        {
            _clip.Intersect(region);
        }

        public void ExcludeClip(Rectangle rect) => _clip.Exclude(rect);
        public void ExcludeClip(DRegion region) => _clip.Exclude(region);

        public void ResetClip()
        {
            _clip = new DRegion();
            _clip.MakeInfinite();
            _mapper.ResetClip();
        }

        public void TranslateClip(float dx, float dy)
        {
            _clip.Translate(dx, dy);
            _mapper.OffsetClip(dx, dy);
        }

        private readonly Dictionary<GraphicsState, int> _stateDepths = new Dictionary<GraphicsState, int>();
        private readonly Dictionary<GraphicsContainer, int> _containerDepths = new Dictionary<GraphicsContainer, int>();

        public GraphicsState Save()
        {
            _stateStack.Push(CaptureState());
            _mapper.SaveDC();
            var token = new GraphicsState(_transform.Clone(), _clip.Clone(), _pageUnit, _pageScale,
                _smoothingMode, _interpolationMode, _compositingMode, _compositingQuality,
                _pixelOffsetMode, _textRenderingHint, _renderingOrigin);
            _stateDepths[token] = _stateStack.Count;
            return token;
        }

        public void Restore(GraphicsState state)
        {
            if (state == null) return;
            if (!_stateDepths.TryGetValue(state, out int depth)) return;
            while (_stateStack.Count >= depth && _stateStack.Count > 0)
            {
                _mapper.RestoreDC();
                RestoreState(_stateStack.Pop());
            }
        }

        public GraphicsContainer BeginContainer()
        {
            _stateStack.Push(CaptureState());
            _mapper.BeginContainer();
            var gs = new GraphicsState(_transform.Clone(), _clip.Clone(), _pageUnit, _pageScale,
                _smoothingMode, _interpolationMode, _compositingMode, _compositingQuality,
                _pixelOffsetMode, _textRenderingHint, _renderingOrigin);
            var token = new GraphicsContainer(gs);
            _containerDepths[token] = _stateStack.Count;
            return token;
        }

        public GraphicsContainer BeginContainer(RectangleF dst, RectangleF src, GraphicsUnit unit)
        {
            _stateStack.Push(CaptureState());
            _mapper.BeginContainer(dst, src, unit);
            var gs = new GraphicsState(_transform.Clone(), _clip.Clone(), _pageUnit, _pageScale,
                _smoothingMode, _interpolationMode, _compositingMode, _compositingQuality,
                _pixelOffsetMode, _textRenderingHint, _renderingOrigin);
            var token = new GraphicsContainer(gs);
            _containerDepths[token] = _stateStack.Count;
            return token;
        }

        public void EndContainer(GraphicsContainer container)
        {
            if (container == null) return;
            if (!_containerDepths.TryGetValue(container, out int depth)) return;
            while (_stateStack.Count >= depth && _stateStack.Count > 0)
            {
                _mapper.EndContainer();
                RestoreState(_stateStack.Pop());
            }
        }

        public void Clear(Color color)
        {
            _mapper.EmitEmfPlusClear(color);
            var rect = new RectangleF(0, 0, _width, _height);
            var path = new DGraphicsPath();
            path.AddRectangle(rect);
            var brush = new SolidBrush(color);
            _mapper.SelectBrush(brush);
            _mapper.BuildPath(path);
            _mapper.FillPath(rect);
            brush.Dispose();
        }

        public void DrawPath(Pen pen, DGraphicsPath path)
        {
            if (pen == null || path == null || path.PointCount == 0) return;

            if (path.PreferPolyPolygon16 && pen.StartCap == LineCap.Flat &&
                pen.EndCap == LineCap.Flat)
            {
                float coordinateScale = _mapper.UsesLogicalCoordinateScale
                    ? _mapper.GetPolylineCoordinateScale(path)
                    : 1f;
                _mapper.EmitEmfPlusDrawPath(pen, path);
                _mapper.BeginLogicalPath(coordinateScale);
                if (_mapper.StrokePolyPolyline16(path, pen, coordinateScale))
                {
                    _mapper.EndLogicalPath(coordinateScale);
                    return;
                }
                _mapper.EndLogicalPath(coordinateScale);
            }

            if (pen.StartCap != LineCap.Flat || pen.EndCap != LineCap.Flat ||
                (_mapper.UsesLogicalCoordinateScale && IsOpenPolyline(path)))
            {
                if (_mapper.UsesLogicalCoordinateScale && IsOpenPolyline(path))
                {
                    _mapper.EmitEmfPlusDrawPath(pen, path);
                    float coordinateScale = _mapper.GetPolylineCoordinateScale(path);
                    _mapper.BeginLogicalPath(coordinateScale);
                    if (_mapper.StrokePolyline16(path, pen, coordinateScale))
                    {
                        _mapper.EndLogicalPath(coordinateScale);
                        return;
                    }
                    _mapper.EndLogicalPath(coordinateScale);
                }
                _mapper.EmitEmfPlusDrawPath(pen, path);
                var expanded = StrokeExpander.Expand(path, pen);
                _mapper.BeginLogicalPath();
                if (!_mapper.FillPolygons16(expanded, pen.Brush))
                {
                    _mapper.SelectBrush(pen.Brush, GetPathBounds(expanded));
                    _mapper.SetPolyFillMode(FillMode.Winding);
                    _mapper.BuildPath(expanded);
                    _mapper.FillPath(GetPathBounds(expanded));
                }
                _mapper.EndLogicalPath();
                return;
            }

            if (path.PreferPolygon16 && _mapper.StrokePolygon16(path, pen))
            {
                _mapper.EmitEmfPlusDrawPath(pen, path);
                return;
            }

            var bounds = GetPathBounds(path, pen.Width);
            _mapper.EmitEmfPlusDrawPath(pen, path);
            _mapper.SelectPen(pen);
            _mapper.BeginLogicalPath();
            _mapper.BuildPath(path);
            _mapper.StrokePath(bounds);
            _mapper.EndLogicalPath();
        }

        private static bool IsOpenPolyline(DGraphicsPath path)
        {
            if (path.PointCount < 2) return false;
            var types = path.PathTypes;
            if (types.Length != path.PointCount)
                return false;

            if ((types[0] & (byte)PathPointType.PathTypeMask) != (byte)PathPointType.Start)
                return false;

            for (int i = 1; i < types.Length; i++)
            {
                if ((types[i] & (byte)PathPointType.PathTypeMask) != (byte)PathPointType.Line ||
                    (types[i] & (byte)PathPointType.CloseSubpath) != 0)
                    return false;
            }

            return (types[0] & (byte)PathPointType.CloseSubpath) == 0;
        }

        public void FillPath(Brush brush, DGraphicsPath path)
            => FillPath(brush, path, path?.FillMode ?? FillMode.Alternate);

        public void FillPath(Brush brush, DGraphicsPath path, FillMode fillMode)
        {
            if (brush == null || path == null || path.PointCount == 0) return;

            _mapper.EmitEmfPlusFillPath(brush, path, fillMode);

            if (brush is HatchBrush hatchBrush && TryGetRectangle(path, out var hatchRect))
            {
                _mapper.FillHatchRectangle(hatchRect, hatchBrush);
                return;
            }

            if (path.PreferPolyPolygon16)
            {
                _mapper.BeginLogicalPath();
                bool emitted = _mapper.FillPolygons16(path, brush);
                _mapper.EndLogicalPath();
                if (emitted) return;
            }

            if (path.PreferPolygon16)
            {
                _mapper.BeginLogicalPath();
                bool emitted = _mapper.FillPolygon16(path, brush);
                _mapper.EndLogicalPath();
                if (emitted) return;
            }

            _mapper.SetPolyFillMode(fillMode);
            var bounds = GetPathBounds(path);
            _mapper.SelectBrush(brush, bounds);
            _mapper.BeginLogicalPath();
            _mapper.BuildPath(path, allowOpenBezierRecord: true);
            _mapper.FillPath(bounds);
            _mapper.EndLogicalPath();
        }

        public void DrawImage(Image image, RectangleF destRect, RectangleF srcRect,
                               GraphicsUnit srcUnit, ImageAttributes? attr)
        {
            if (image == null) return;
            if (image is Bitmap bmp && attr == null)
            {
                _mapper.EmitEmfPlusDrawImage(bmp, destRect, srcRect, srcUnit, null);
                _mapper.EmitEmfPlusGetDc();
                _mapper.StretchDIBits(destRect, bmp);
                return;
            }

            using var rasterized = PrepareBitmap(image, attr);
            if (rasterized != null)
            {
                _mapper.EmitEmfPlusDrawImage(rasterized, destRect, srcRect, srcUnit, null);
                _mapper.EmitEmfPlusGetDc();
                _mapper.StretchDIBits(destRect, rasterized);
            }
        }

        public void DrawImage(Image image, PointF[] destPoints, RectangleF srcRect,
                               GraphicsUnit srcUnit, ImageAttributes? attr)
        {
            if (image == null || destPoints == null || destPoints.Length < 3) return;
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in destPoints)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            var destRect = new RectangleF(minX, minY, maxX - minX, maxY - minY);
            if (image is Bitmap bitmap && attr == null)
            {
                _mapper.EmitEmfPlusDrawImagePoints(bitmap, destPoints, srcRect, srcUnit, null);
                _mapper.EmitEmfPlusGetDc();
                _mapper.StretchDIBits(destRect, bitmap);
                return;
            }

            using var rasterized = PrepareBitmap(image, attr);
            if (rasterized != null)
            {
                _mapper.EmitEmfPlusDrawImagePoints(rasterized, destPoints, srcRect, srcUnit, null);
                _mapper.EmitEmfPlusGetDc();
                _mapper.StretchDIBits(destRect, rasterized);
            }
        }

        private static Bitmap? PrepareBitmap(Image image, ImageAttributes? attr)
        {
            var bitmap = image as Bitmap ?? image.Clone() as Bitmap;
            if (bitmap == null || attr == null)
                return bitmap;

            var adjusted = new Bitmap(bitmap.Width, bitmap.Height);
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                    adjusted.SetPixel(x, y, attr.ApplyToColor(bitmap.GetPixel(x, y)));

            if (!ReferenceEquals(bitmap, image))
                bitmap.Dispose();
            return adjusted;
        }

        public void DrawString(string s, DRITFont font, Brush brush,
                                RectangleF layoutRect, StringFormat? format)
        {
            if (string.IsNullOrEmpty(s) || font == null || brush == null) return;

            var richOptions = new RichTextOptions(font.FontFamily, font.Size, font.Style)
            {
                Unit = font.Unit,
                Fallback = FontFallbackChain.CreateDefault(),
            };
            var stringFormat = format ?? StringFormat.GenericDefault;
            _mapper.EmitEmfPlusDrawString(s, font, brush, layoutRect, format);
            var runs = RunSegmenter.Segment(s, font, richOptions, stringFormat,
                                             richOptions.Fallback);
            var lines = TextLayoutEngine.Layout(runs, layoutRect,
                                                 stringFormat, _dpiX, _dpiY);
            if (lines.Count == 0) return;

            foreach (var line in lines)
            {
                foreach (var run in line.Runs)
                {
                    var runBrush = run.Brush ?? brush;
                    var color = runBrush is SolidBrush solidBrush
                        ? solidBrush.Color : Color.Black;
                    float lineTop = line.Baseline - line.Ascent;
                    EmitTextRun(line, run, color, lineTop);
                }
            }
        }

        private void EmitTextRun(TextLine line, TextRun run, Color color, float lineTop)
        {
            if (!line.TokenizeNativeText && line.ExtraCharacterSpacing == 0f &&
                line.ExtraSpaceSpacing == 0f)
            {
                _mapper.ExtTextOutW(run.Text, run.Font, color,
                    new RectangleF(run.X, lineTop, run.Width, line.LineHeight));
                return;
            }

            var metrics = run.Font.FontFamily.GetMetricsData(run.Font.Style);
            float emPixels = TextLayoutEngine.FontSizeToPixels(
                run.Font.Size, run.Font.Unit, _dpiY);
            float emScale = metrics.UnitsPerEm > 0
                ? (float)(emPixels / metrics.UnitsPerEm) : 1f;
            var fontFace = run.Font.FontFamily.GetFontFace(run.Font.Style);
            float fallbackWidth = emPixels * TextLayoutEngine.DefaultCharWidthFactor;
            float currentX = run.X;
            int tokenStart = 0;
            for (int i = 0; i <= run.Text.Length; i++)
            {
                bool atEnd = i == run.Text.Length;
                bool isSpace = !atEnd && char.IsWhiteSpace(run.Text[i]);
                bool isPunctuation = !atEnd && (run.Text[i] == '.' || run.Text[i] == ',' ||
                                                run.Text[i] == ';' || run.Text[i] == ':' ||
                                                run.Text[i] == '!' || run.Text[i] == '?');
                bool endsToken = atEnd || isSpace || isPunctuation ||
                    (i > tokenStart && isPunctuation);

                if (endsToken && i > tokenStart)
                {
                    EmitToken(line, run, color, lineTop, tokenStart, i - tokenStart,
                        currentX, fontFace, emScale, fallbackWidth, emPixels,
                        i < run.Text.Length);
                    currentX += MeasureExpandedRange(line, run, tokenStart, i,
                        fontFace, emScale, fallbackWidth, emPixels);
                    tokenStart = i;
                }

                if (isPunctuation)
                {
                    EmitToken(line, run, color, lineTop, i, 1, currentX,
                        fontFace, emScale, fallbackWidth, emPixels,
                        i + 1 < run.Text.Length);
                    currentX += MeasureExpandedRange(line, run, i, i + 1,
                        fontFace, emScale, fallbackWidth, emPixels);
                    tokenStart = i + 1;
                }

                if (isSpace)
                {
                    currentX += TextLayoutEngine.GetCharAdvance(run.Text[i],
                        currentX - run.X, fontFace, emScale, fallbackWidth,
                        0f, Array.Empty<float>(), emPixels) + line.ExtraSpaceSpacing;
                    if (i < run.Text.Length - 1)
                        currentX += line.ExtraCharacterSpacing;
                    tokenStart = i + 1;
                }
            }
        }

        private void EmitToken(TextLine line, TextRun run, Color color, float lineTop,
                               int start, int length, float x, IFont? fontFace,
                               float emScale, float fallbackWidth, float emPixels,
                               bool hasFollowingCharacter)
        {
            string text = run.Text.Substring(start, length);
            int[]? advances = BuildExpandedAdvances(line, text, fontFace,
                emScale, fallbackWidth, emPixels, hasFollowingCharacter);
            float width = MeasureExpandedRange(line, run, start, start + length,
                fontFace, emScale, fallbackWidth, emPixels);
            _mapper.ExtTextOutW(text, run.Font, color,
                new RectangleF(x, lineTop, width, line.LineHeight), advances);
        }

        private static int[]? BuildExpandedAdvances(TextLine line, string text,
                                                     IFont? fontFace, float emScale,
                                                     float fallbackWidth, float emPixels,
                                                     bool hasFollowingCharacter)
        {
            if (line.ExtraCharacterSpacing == 0f && line.ExtraSpaceSpacing == 0f)
                return null;

            var advances = new int[text.Length];
            float position = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                float advance = TextLayoutEngine.GetCharAdvance(c, position,
                    fontFace, emScale, fallbackWidth, 0f,
                    Array.Empty<float>(), emPixels);
                position += advance;
                if (c == ' ')
                    advance += line.ExtraSpaceSpacing;
                if (i < text.Length - 1 || hasFollowingCharacter)
                    advance += line.ExtraCharacterSpacing;
                advances[i] = Math.Max(0, (int)Math.Round(advance));
            }
            return advances;
        }

        private static float MeasureExpandedRange(TextLine line, TextRun run,
                                                   int start, int end, IFont? fontFace,
                                                   float emScale, float fallbackWidth,
                                                   float emPixels)
        {
            float width = 0f;
            for (int i = start; i < end; i++)
            {
                float advance = TextLayoutEngine.GetCharAdvance(run.Text[i], width,
                    fontFace, emScale, fallbackWidth, 0f,
                    Array.Empty<float>(), emPixels);
                width += advance;
                if (run.Text[i] == ' ')
                    width += line.ExtraSpaceSpacing;
                if (i < run.Text.Length - 1)
                    width += line.ExtraCharacterSpacing;
            }
            return width;
        }

        /// <summary>
        /// Builds a complete <see cref="MetafileDocument"/> from the accumulated
        /// records, including EMR_HEADER and EMR_EOF.
        /// </summary>
        public MetafileDocument ToDocument()
        {
            _mapper.CompleteEmfPlus();
            var records = new List<EmfRecord>();
            var header = CreateHeader();
            records.Add(header);
            foreach (var r in _mapper.Records)
                records.Add(r);
            records.Add(new EMR_EOF());
            header.HeaderObject.Records = (uint)records.Count;
            header.HeaderObject.Bytes = 0;
            var hdr = header.Header;
            hdr.Size = 0;
            header.Header = hdr;
            return new MetafileDocument
            {
                Format = MetafileFormat.Emf,
                EmfRecords = records,
            };
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }

        private EMR_HEADER CreateHeader()
        {
            return new EMR_HEADER
            {
                HeaderObject = new Metafile.Emf.Objects.Header
                {
                    Bounds = new Metafile.Wmf.Objects.RectL
                    {
                        Left = 0, Top = 0, Right = _width, Bottom = _height,
                    },
                    Frame = new Metafile.Wmf.Objects.RectL
                    {
                        Left = 0,
                        Top = 0,
                        Right = (int)Math.Max(1, Math.Round(_width * 25.4 / _dpiX * 100)),
                        Bottom = (int)Math.Max(1, Math.Round(_height * 25.4 / _dpiY * 100)),
                    },
                    RecordSignature = 0x464D4520,
                    Version = 0x00010000,
                    Bytes = 0,
                    Records = 0,
                    Handles = _mapper.HandleCount,
                    nDescription = 0,
                    offDescription = 0,
                    nPalEntries = 0,
                    Device = new Metafile.Wmf.Objects.SizeL
                    {
                        cx = (uint)_width, cy = (uint)_height,
                    },
                    Millimeters = new Metafile.Wmf.Objects.SizeL
                    {
                        cx = (uint)Math.Max(1, Math.Round(_width * 25.4 / _dpiX)),
                        cy = (uint)Math.Max(1, Math.Round(_height * 25.4 / _dpiY)),
                    },
                },
            };
        }

        private static RectangleF GetPathBounds(DGraphicsPath path, float strokeWidth = 0f)
        {
            var pts = path.PathPoints;
            if (pts.Length == 0) return RectangleF.Empty;
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in pts)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            float expansion = Math.Max(0f, strokeWidth) + (strokeWidth > 0f ? 1f : 0f);
            return new RectangleF(minX - expansion, minY - expansion,
                maxX - minX + expansion * 2f, maxY - minY + expansion * 2f);
        }

        private static bool TryGetRectangle(DGraphicsPath path, out RectangleF rect)
        {
            rect = RectangleF.Empty;
            if (path.PointCount != 4) return false;

            var points = path.PathPoints;
            var types = path.PathTypes;
            if (types.Length != 4
                || (types[0] & (byte)PathPointType.PathTypeMask) != (byte)PathPointType.Start
                || (types[1] & (byte)PathPointType.PathTypeMask) != (byte)PathPointType.Line
                || (types[2] & (byte)PathPointType.PathTypeMask) != (byte)PathPointType.Line
                || (types[3] & (byte)PathPointType.PathTypeMask) != (byte)PathPointType.Line
                || (types[3] & (byte)PathPointType.CloseSubpath) == 0)
                return false;

            float minX = Math.Min(points[0].X, points[2].X);
            float maxX = Math.Max(points[0].X, points[2].X);
            float minY = Math.Min(points[0].Y, points[2].Y);
            float maxY = Math.Max(points[0].Y, points[2].Y);
            bool corners = points[0].Y == points[1].Y && points[1].X == points[2].X
                && points[2].Y == points[3].Y && points[3].X == points[0].X;
            if (!corners) return false;

            rect = new RectangleF(minX, minY, maxX - minX, maxY - minY);
            return rect.Width > 0 && rect.Height > 0;
        }

        private StateFrame CaptureState() => new StateFrame
        {
            Transform = _transform.Clone(),
            Clip = _clip.Clone(),
            SmoothingMode = _smoothingMode,
            InterpolationMode = _interpolationMode,
            CompositingMode = _compositingMode,
            CompositingQuality = _compositingQuality,
            PixelOffsetMode = _pixelOffsetMode,
            TextRenderingHint = _textRenderingHint,
            PageUnit = _pageUnit,
            PageScale = _pageScale,
            RenderingOrigin = _renderingOrigin,
        };

        private void RestoreState(StateFrame frame)
        {
            _transform = frame.Transform;
            _clip = frame.Clip;
            _smoothingMode = frame.SmoothingMode;
            _interpolationMode = frame.InterpolationMode;
            _compositingMode = frame.CompositingMode;
            _compositingQuality = frame.CompositingQuality;
            _pixelOffsetMode = frame.PixelOffsetMode;
            _textRenderingHint = frame.TextRenderingHint;
            _pageUnit = frame.PageUnit;
            _pageScale = frame.PageScale;
            _renderingOrigin = frame.RenderingOrigin;
            _mapper.SetTransform(_transform);
            _mapper.SetSmoothingMode(_smoothingMode);
            _mapper.SetInterpolationMode(_interpolationMode);
            _mapper.SetCompositingMode(_compositingMode);
            _mapper.SetCompositingQuality(_compositingQuality);
            _mapper.SetPixelOffsetMode(_pixelOffsetMode);
            _mapper.SetTextRenderingHint(_textRenderingHint);
            _mapper.SetPageTransform(_pageUnit, _pageScale);
            _mapper.SetRenderingOrigin(_renderingOrigin);
        }

        private sealed class StateFrame
        {
            public DMatrix Transform = null!;
            public DRegion Clip = null!;
            public SmoothingMode SmoothingMode;
            public InterpolationMode InterpolationMode;
            public CompositingMode CompositingMode;
            public CompositingQuality CompositingQuality;
            public PixelOffsetMode PixelOffsetMode;
            public TextRenderingHint TextRenderingHint;
            public GraphicsUnit PageUnit;
            public float PageScale;
            public Point RenderingOrigin;
        }
    }
}