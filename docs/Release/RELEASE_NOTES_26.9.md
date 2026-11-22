# DRIT.Drawing 26.9 Release Notes

Release date: 2026-09-15

DRIT.Drawing 26.9 builds on the capabilities documented in the current release README. The release focuses on metafile parsing and export fidelity, JPEG restart-marker decoding, rendering and text-layout corrections, and more configurable SVG output.

The current README is the release baseline. APNG, text-on-path, rich text, bidirectional text, font fallback, `VectorScene`, `RecordingGraphics`, SVG read/write, and EMF/WMF recording are therefore treated as existing capabilities, not as 26.9 additions.

## Highlights

- Improved EMF and WMF parsing for headers, text records, raster operations, and enhanced records.
- Improved EMF+ export compatibility for paths, images, text, clipping, and object/state records.
- Improved JPEG restart-marker decoding and image, text, clipping, hatch, path, line-cap, and font export behavior.
- Added parameterized SVG saving through `SvgEncoderParameters` and validation for externally embedded images.
- Expanded EMF comparison and diagnostic regression coverage for output fidelity.

## Baseline capabilities

The release baseline already documents APNG frame decoding and encoding, text-on-path layout, rich text runs, Unicode bidirectional processing, font fallback, vector scenes, recording graphics, SVG reading and writing, and EMF/WMF/EMF+ playback, recording, and conversion. These capabilities remain supported in 26.9 but are not claimed as newly introduced by this release.

## Image and rendering improvements

- JPEG restart-marker decoding is more tolerant of restart intervals and malformed or incomplete restart state.
- Rendering and export behavior was improved for hatch brushes, path gradients, clipping regions, graphics paths, and line caps.
- SVG and metafile image placement now handles more image and transform combinations consistently.
- Font record and encoding handling improve glyph resolution during vector and metafile export.

## Compatibility

The package remains a pure-managed library with no native or third-party NuGet dependencies. The current package assembly targets `netstandard2.0`, providing compatibility with .NET Framework applications and modern .NET applications that support .NET Standard 2.0.

No public API removals are intended by this release. The main compatibility consideration is that vector recording and format conversion now exercise more of the public drawing surface than raster-only workflows. Applications that serialize SVG, EMF, WMF, or EMF+ output should review representative output files after upgrading.

The package continues to use the MIT license.

## Validation

The release changes include focused unit and integration coverage for:

- EMF and WMF parsing, EMF+ export compatibility, and metafile comparisons.
- Text layout, font records and encoding, hatch rendering, clipping, paths, images, and line caps.
- JPEG restart decoding and image codec behavior.
- SVG parameter handling and external image embedding validation.

Before publishing 26.9, run the complete CI, signing, package inspection, and NuGet publishing checklist used by the DRIT.Drawing pipelines. Verify that the package contains the signed `netstandard2.0` assembly and the current README.

## Upgrade notes

1. Existing drawing, imaging, APNG, text, vector, and recording APIs continue to be supported.
2. Review representative SVG and EMF/WMF/EMF+ output in consuming applications because export fidelity has changed in this release.
3. Review JPEG inputs that use restart intervals when upgrading.
