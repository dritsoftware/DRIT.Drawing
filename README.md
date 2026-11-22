# DRIT.Drawing

A pure-managed .NET 2D graphics library with the same API surface as `System.Drawing.Common`, using an integrated font engine for all text measurement and glyph rendering — with no native dependencies and no third-party NuGet packages.

DRIT.Drawing is built around a **vector domain model**: any drawing performed through a `Graphics` surface can be captured as true vector output (SVG, EMF/WMF), not just rasterized to a bitmap. The same `Graphics` API produces raster, vector, or metafile output unchanged.

[![.NET](https://img.shields.io/badge/.NET-netstandard2.0-blue.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

---

## Table of Contents

- [What is DRIT.Drawing?](#what-is-dritdrawing)
- [Features](#features)
- [Supported File Formats](#supported-file-formats)
- [Platform Independence](#platform-independence)
- [Get Started](#get-started)
  - [Basic Drawing](#basic-drawing)
  - [Transformations](#transformations)
  - [Gradients](#gradients)
  - [Text Layout](#text-layout)
  - [Draw to Vector SVG](#draw-to-vector-svg)
  - [Convert SVG, EMF, and Bitmap](#convert-svg-emf-and-bitmap)
  - [Animated PNG (APNG)](#animated-png-apng)
  - [Text on a Path](#text-on-a-path)
  - [Rich Text, Bidi, and Font Fallback](#rich-text-bidi-and-font-fallback)
- [Architecture](#architecture)
- [The Vector Domain Model](#the-vector-domain-model)
- [Integration](#integration)
- [Building](#building)
- [License](#license)

---

## What is DRIT.Drawing?

DRIT.Drawing is a **pure-managed .NET 2D graphics library**. It does not depend on `System.Drawing.Common`, on libgdiplus, or on any third-party NuGet package. The entire stack — rasterizer, image codecs, font engine, metafile parser, SVG reader/writer, and the Unicode bidirectional algorithm — is implemented in source.

Key positioning:

- **`System.Drawing.Common` API surface, mirrored in the `DRIT.Drawing` namespace.** `Graphics`, `Bitmap`, `Pen`, `Brush`, `Font`, `Color`, `Matrix`, `GraphicsPath`, `Region`, `Image`/`Metafile` — consumers can alias with `using SD = DRIT.Drawing`.
- **Vector-first architecture.** A format-agnostic `VectorScene` sits between every producer (`Graphics` recording, EMF/WMF playback, SVG parsing) and every consumer (rasterizer, SVG writer, EMF writer). Any `Graphics` drawing can be emitted as true vector SVG or recorded as EMF/WMF.
- **Deep format fidelity.** Full in-house WMF/EMF/EMF+ metafile engine; managed PNG/BMP/GIF/TIFF/ICO codecs; a hand-written JPEG codec (baseline, progressive, encoder) with no external JPEG dependency.
- **First-class text.** Integrated TrueType/OpenType/CFF/Type1/WOFF/WOFF2 font engine, full Unicode Bidirectional Algorithm (UAX #9), automatic font fallback, rich text runs, and text-on-path.
- **Zero external dependencies.** No NuGet packages, no native libraries, no `System.Drawing.Common` installation required.

## Features

### Core Graphics Types

| Type | Description |
|------|-------------|
| **Color** | ARGB packed `uint`, 174 known colors, HSV conversion |
| **Point/PointF** | Integer/float coordinate structs with arithmetic |
| **Size/SizeF** | Dimension structs with arithmetic |
| **Rectangle/RectangleF** | Bounding box structs with intersection/union |
| **Matrix** | Affine transformation (rotate, scale, translate, shear, invert) |

### Drawing Primitives

| Type | Description |
|------|-------------|
| **Pen** | Stroke with color, width, line caps, joins, dash patterns |
| **Brush** | Fill styles: `SolidBrush`, `HatchBrush`, `LinearGradientBrush`, `PathGradientBrush`, `TextureBrush` |
| **GraphicsPath** | Vector paths with lines, Bézier curves, arcs, ellipses |
| **Region** | Constructive solid geometry (CSG) for clipping |

### Image Support

| Format | Status |
|--------|--------|
| **BMP** | ✅ Supported uncompressed RGB layouts; indexed and bitfield variants are not supported |
| **PNG** | ✅ Managed codec with zlib compression |
| **APNG** | ✅ Animated PNG — read/write, multi-frame, dispose/blend ops |
| **GIF** | ✅ LZW compression, animation support |
| **TIFF** | ✅ Basic uncompressed RGB/RGBA read/write |
| **ICO** | ✅ Multi-size icon support |
| **JPEG** | ✅ Native decoder/encoder (SOF0/SOF1/SOF2) — no external JPEG dependency |
| **SVG** | ✅ Read/write — true vector, not embedded raster |
| **WMF/EMF/EMF+** | ✅ Read/write — playback and recording |

### Text Rendering

| Feature | Description |
|---------|-------------|
| **Font/FontFamily** | Integration with the built-in font engine for metrics and glyph access |
| **StringFormat** | Alignment, trimming, tab stops, hotkey prefix |
| **DrawString** | Full text layout with word breaking, alignment |
| **MeasureString** | Text measurement for layout |
| **Bidi (UAX #9)** | Full Unicode Bidirectional Algorithm — RTL scripts and mixed-direction text |
| **Font Fallback** | Automatic glyph substitution from a fallback font chain |
| **Rich Text Runs** | Mixed fonts, sizes, styles, and brushes in a single layout |
| **Text on Path** | Render text along an arbitrary vector baseline |

### Vector Graphics

| Feature | Description |
|---------|-------------|
| **VectorScene** | Format-agnostic domain model of recorded drawing commands |
| **RecordingGraphics** | A `Graphics` surface that captures vector commands instead of rasterizing |
| **SvgImage** | Vector `Image` subclass — load/save SVG, rasterize, or convert to EMF |
| **SVG Writer** | `VectorScene` → SVG (path/gradient/pattern/clip/text/image elements) |
| **SVG Reader** | SVG → `VectorScene` (streaming `XmlReader`-based, tolerant) |
| **EMF/WMF Bridge** | Bidirectional transcoding between `VectorScene` and metafile records |
| **EMF/WMF Recording** | Capture any `Graphics` drawing as EMF/WMF (previously playback-only) |

### Advanced Features

| Feature | Description |
|---------|-------------|
| **Metafile** | EMF/WMF/EMF+ playback and recording using the in-house metafile engine |
| **Printing** | `PrintDocument`, `PageSettings`, `PrinterSettings` stubs |
| **Icon/Cursor** | Icon extraction and cursor support |
| **Antialiasing** | 4× MSAA for smooth rendering |
| **ImageAttributes** | Color matrix, gamma, color key, remap table, threshold |

## Supported File Formats

| Format | Read | Write | Specification |
|---|:---:|:---:|---|
| **BMP** | ✅ | ✅ | Uncompressed RGB layouts; indexed and bitfield variants are not supported |
| **PNG** | ✅ | ✅ | Portable Network Graphics (managed zlib) |
| **APNG** | ✅ | ✅ | Animated PNG (Mozilla APNG 1.0) |
| **JPEG** | ✅ | ✅ | Baseline (SOF0/SOF1) + Progressive (SOF2) |
| **GIF** | ✅ | ✅ | GIF87a/GIF89a (managed LZW, animation) |
| **TIFF** | ✅ | ✅ | Uncompressed RGB/RGBA; compressed and non-RGB layouts are not supported |
| **ICO** | ✅ | ✅ | Multi-size icon/cursor |
| **SVG** | ✅ | ✅ | SVG 1.1 (write) / SVG 1.1 + selective SVG 2 (read) |
| **WMF** | ✅ | ✅ | Windows Metafile ([MS-WMF]) |
| **EMF** | ✅ | ✅ | Enhanced Metafile ([MS-EMF]) |
| **EMF+** | ✅ | ✅ | Enhanced Metafile Plus |

## Platform Independence

DRIT.Drawing is implemented in pure managed C# and targets **`netstandard2.0`**, so it can run on compatible versions of:

- .NET Framework (via netstandard2.0)
- .NET Core / .NET 5+ (via netstandard2.0)
- Windows, Linux, and macOS

It has **no external dependencies** — no NuGet packages, no native libraries, and no `System.Drawing.Common` installation required.

---

## Get Started

### Basic Drawing

```csharp
using var bitmap = new Bitmap(800, 600);
using var graphics = Graphics.FromImage(bitmap);

// Draw shapes
using var pen = new Pen(Color.Blue, 2);
graphics.DrawLine(pen, 0, 0, 800, 600);
graphics.DrawEllipse(pen, 100, 100, 200, 200);

// Fill shapes
using var brush = new SolidBrush(Color.Red);
graphics.FillRectangle(brush, 50, 50, 100, 100);

// Draw text
using var font = new Font("Arial", 16, FontStyle.Bold);
graphics.DrawString("Hello, World!", font, brush, new PointF(200, 200));

bitmap.Save("output.png", ImageFormat.Png);
```

### Transformations

```csharp
var matrix = new Matrix();
matrix.Rotate(45);
matrix.Translate(100, 100);
graphics.Transform = matrix;
```

### Gradients

```csharp
var brush = new LinearGradientBrush(
    new PointF(0, 0),
    new PointF(100, 100),
    Color.Red,
    Color.Blue);
graphics.FillRectangle(brush, 0, 0, 100, 100);
```

### Text Layout

```csharp
var format = new StringFormat
{
    Alignment = StringAlignment.Center,
    LineAlignment = StringAlignment.Center
};
graphics.DrawString("Centered text", font, brush, layoutRect, format);
```

### Draw to Vector SVG

Any `Graphics` drawing can be captured as true vector SVG — no rasterization occurs until `RenderToBitmap()` is explicitly called.

```csharp
using var svg = new SvgImage(800, 600);          // vector canvas
using var g = svg.CreateGraphics();               // RecordingGraphics, same API as Graphics
g.Clear(Color.White);
g.DrawEllipse(new Pen(Color.Blue, 2), 100, 100, 200, 200);
using var grad = new LinearGradientBrush(
    new Point(0, 0), new Point(100, 100), Color.Red, Color.Blue);
g.FillRectangle(grad, 50, 50, 100, 100);
g.DrawString("Hello", new Font("Arial", 16), Brushes.Black, 200, 200);
svg.Save("drawing.svg", ImageFormat.Svg);         // true vector SVG
```

### Convert SVG, EMF, and Bitmap

The `VectorScene` is the single pivot for all vector conversions — no pairwise converters are written.

```csharp
// Load SVG, rasterize, or convert to EMF
using var svg = new SvgImage("input.svg");
svg.RenderToBitmap().Save("output.png", ImageFormat.Png);
svg.Save("output.emf", ImageFormat.Emf);          // SVG → EMF

// Convert EMF to SVG
using var emf = new Metafile("chart.emf");
emf.Save("chart.svg", ImageFormat.Svg);

// Convert any Bitmap to (embedded-image) SVG
using var bmp = new Bitmap("photo.png");
bmp.Save("photo.svg", ImageFormat.Svg);

// Decode SVG through the codec registry (rasterized)
using var raster = new Bitmap("drawing.svg");
```

### Animated PNG (APNG)

APNG is exposed through the `System.Drawing`-style multi-frame API on `Image`/`Bitmap` (`GetFrameCount`/`SelectActiveFrame` with `FrameDimension.Time`) — the same surface `System.Drawing` uses for animated GIF. A non-APNG-aware caller still sees the default image; animation frames are reachable via `FrameDimension.Time`.

```csharp
using var bmp = new Bitmap("animated.apng");

int frames = bmp.GetFrameCount(FrameDimension.Time);
for (int i = 0; i < frames; i++)
{
    bmp.SelectActiveFrame(FrameDimension.Time, i);
    bmp.Save($"frame{i}.png", ImageFormat.Png);
}
```

Encoding a multi-frame `Bitmap` as PNG automatically emits APNG (`acTL`/`fcTL`/`fdAT`); a single-frame `Bitmap` produces a plain PNG, byte-identical to before.

### Text on a Path

Render text along an arbitrary vector baseline, with each glyph positioned and rotated tangent to the path.

```csharp
using var circle = new GraphicsPath();
circle.AddEllipse(100, 50, 400, 300);
g.DrawStringOnPath("Around the circle we go",
    new Font("Arial", 20f), brush, circle,
    new StringFormat { Alignment = StringAlignment.Center });

// Above the path, with a start offset
using var curve = new GraphicsPath();
curve.AddBezier(20, 300, 150, 100, 450, 500, 580, 300);
g.DrawStringOnPath("Above the curve",
    new Font("Arial", 18f), brush, curve, null,
    new TextOnPathOptions { StartOffset = 30f, PerpendicularOffset = 20f });

// Produce a fillable path (metafile-recordable)
using var textPath = new GraphicsPath();
textPath.AddStringOnPath("Fillable", new FontFamily("Arial"), 0, 24f, curve);
g.FillPath(new SolidBrush(Color.Red), textPath);
```

### Rich Text, Bidi, and Font Fallback

Existing `DrawString` now performs bidi reordering (UAX #9) and font fallback by default. Rich text runs (mixed fonts/styles/colors) are available via `DrawRichText`.

```csharp
// RTL Hebrew — renders right-to-left automatically
g.DrawString("שלום עולם", new Font("Arial", 24f), brush,
    new RectangleF(0, 20, 600, 40),
    new StringFormat(StringFormatFlags.DirectionRightToLeft));

// Fallback — CJK chars render from a fallback font even though Arial lacks them
g.DrawString("Hello 世界", new Font("Arial", 24f), brush, 20f, 80f);

// Rich text — mixed fonts/styles/colors
var rich = new RichTextOptions(new FontFamily("Arial"), 18f)
{
    Runs =
    {
        new RichTextRun(0, 6),                                   // "Hello " — default
        new RichTextRun(6, 5) { FontStyle = FontStyle.Bold },    // "World" — bold
        new RichTextRun(11, 4) { EmSize = 28f },                 // "Big!" — larger
        new RichTextRun(15, 5) { Brush = new SolidBrush(Color.Red) }, // "Red!" — red
    }
};
g.DrawRichText("Hello WorldBig!Red!", rich, brush, new RectangleF(20, 160, 560, 60));

// Custom fallback chain
g.FontFallback = new FontFallbackChain
{
    new FontFamily("Segoe UI Symbol"),
    new FontFamily("Segoe UI Emoji"),
};
```

## Architecture

```
DRIT.Drawing/
├── Colors/         # Color, KnownColor, SystemColors
├── Geometry/       # Point, PointF, Size, SizeF, Rectangle, RectangleF
├── Drawing2D/      # Matrix, GraphicsPath, Region, brushes, pens
├── Pens/           # Pen, Pens, CustomLineCap, AdjustableArrowCap
├── Brushes/        # Brush, SolidBrush, TextureBrush, Brushes
├── Text/           # Font, FontFamily, StringFormat, text layout
│   └── Bidi/       # UAX #9 Bidirectional Algorithm + character tables
├── Imaging/        # Image, Bitmap, Icon, Cursor, Metafile, SvgImage
├── Graphics/       # Graphics, RecordingGraphics, IDrawingContext
├── Rendering/      # Scanline rasterizer, image renderer, EmfPlayer/WmfPlayer
├── Codecs/         # BMP, PNG/APNG, GIF, TIFF, JPEG, ICO, SvgCodec
│   ├── Png/        # APNG chunk parsing + frame builder
│   └── Jpeg/       # Native marker parser, baseline/progressive decoder, encoder
├── Vector/         # VectorScene + DrawingCommand hierarchy (vector domain model)
│   ├── Commands/   # DrawPath/FillPath/DrawImage/DrawString/transform/clip/state
│   ├── Svg/        # SvgReader, SvgWriter, brush/pen/path/transform/clip/text/image
│   └── Emf/        # EmfDrawingContext, WmfDrawingContext, EmfRecordMapper
├── Metafile/       # In-house WMF/EMF/EMF+ parser + writer
├── Font/           # Integrated font engine (TTF/OTF/CFF/Type1/WOFF/WOFF2)
└── Printing/       # PrintDocument, PageSettings, PrinterSettings
```

## The Vector Domain Model

The `Vector/` directory is a format-agnostic recording of drawing operations that sits between every producer and every consumer. This turns the conversion matrix from O(N²) pairwise converters into O(2N) readers + writers against the common model:

```
                ┌─────────────────┐
   Graphics ──► │                 │ ──► Rasterizer (Bitmap)
   EMF/WMF ──►  │   VectorScene   │ ──► SvgWriter  (.svg)
   SVG ──────►  │  (domain model) │ ──► EmfWriter  (.emf/.wmf)
   (future) ──► │                 │ ──► (future: Pdf, Xps)
                └─────────────────┘
```

- **`RecordingGraphics : Graphics`** overrides the core draw methods to append commands to a `VectorScene` instead of rasterizing. Existing drawing code produces vector output unchanged.
- **`VectorScenePlayer`** replays a scene onto any `IDrawingContext` — a raster `Graphics`, an `SvgDrawingContext`, or an `EmfDrawingContext`. One replay engine, three outputs.
- **EMF/WMF recording** — previously `EmfCodec.Encode` could only play back metafiles, never record them. The `RecordingGraphics` → `VectorScene` → `EmfDrawingContext` path emits any `Graphics` drawing as EMF/WMF.

## Integration

### Font Engine

Text rendering uses the integrated font engine (source-level, no external dependency) for:
- Font loading from files or byte arrays in TTF, OTF, CFF, Type1, WOFF, and WOFF2 formats
- Font metrics (`IFontMetrics`) — ascender, descender, line gap, units per EM
- Glyph outlines (`GlyphOutlineRenderer`, `IGlyphOutlinePainter`)
- Unicode to glyph mapping (`IFontEncoding`) with `HasGlyph`/`TryGetGlyph` for fallback detection
- Font collections (TTC) and font subsetting

### Metafile Engine

EMF/WMF/EMF+ playback and recording uses the in-house metafile engine for:
- Record parsing (`MetafileReader`) and writing (`MetafileWriter`)
- GDI command translation to `Graphics`/`IDrawingContext` calls (`EmfPlayer`, `WmfPlayer`)
- Bidirectional bridging to the `VectorScene` (`EmfDrawingContext`, `WmfDrawingContext`)

## Building

```bash
dotnet build DRIT.Drawing/DRIT.Drawing.csproj
dotnet test DRIT.Drawing.Tests/DRIT.Drawing.Tests.csproj
```

Requires the .NET 8.0 SDK or later for tests. The library targets `netstandard2.0`.

## License

DRIT.Drawing is licensed under the [MIT License](LICENSE).
