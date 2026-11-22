# DRIT.Drawing 26.11 Release Notes

Release date: To be announced

DRIT.Drawing 26.11 focuses on image decoding and encoding fidelity, safer handling of image inputs, and expanded regression coverage. It also improves EMF polyline playback, rasterization performance, and API documentation.

## Highlights

- Improved progressive JPEG decoding, colour conversion, and performance, including the image reported in GitHub issue #1.
- Improved PNG, APNG, GIF, BMP, TIFF, and ICO handling, including animation, transparency, colour, and common image-layout cases.
- Added configurable image decoding limits and strengthened validation of malformed or oversized inputs.
- Improved EMF polyline handling and solid-brush path filling.
- Extended EMF+ recording for supported drawing, text, image, and clipping operations.
- Expanded image regression coverage for codecs and rendering.
- Expanded XML API documentation and added runnable examples.

## Image and rendering improvements

- JPEG decoding handles progressive scan geometry and refinement, restart markers, and RGB, CMYK, and YCCK colour interpretation more accurately. The decoder also has faster image processing for larger files.
- PNG and APNG improvements cover interlacing, transparency, animation composition, frame disposal, and decompression validation. GIF improvements include interlaced frames, colour quantization, and saving animation frames and timing.
- BMP, TIFF, and ICO decoding handles more layout and alpha cases safely. Supported uncompressed RGB/RGBA TIFFs can be read from multiple strips, while unsupported layouts are reported explicitly.
- Image export handles common pixel formats and alpha representations more consistently. Bitmap cloning and pixel-locking behavior have also been improved.
- EMF polyline and grouped-polyline records are handled more accurately, and solid-brush path filling has been optimized.

## Compatibility

The package remains a pure-managed library targeting `netstandard2.0` and continues to use the MIT license.

Image decoding now applies resource limits for dimensions, pixels, source and decoded data, and animation frames. Applications can configure defaults or provide limits per decode using `ImageDecodeLimits`. Files that exceed these limits, or use unsupported image features, may now fail explicitly instead of producing partial or incorrect output. Safety checks apply by default; stricter format-conformance checks are available separately.

Some valid format variants remain unsupported, including arithmetic-coded and 12-bit JPEG, indexed or bitfield BMP variants, and compressed or non-RGB TIFF layouts. Applications that depend on these variants should verify their inputs before upgrading.

## Validation

The source includes unit and integration tests for image codecs, animation, pixel formats, rendering, and EMF playback. The project also has an internal test suite for an external codec corpus; it is not currently part of the CI pipeline or the public source release.

## Upgrade notes

1. Review representative images and animations after upgrading, particularly progressive and CMYK JPEGs, transparent images, GIF/APNG animations, and TIFF files.
2. Configure `ImageDecodeLimits` for applications processing unusually large trusted images or untrusted uploads.
3. Review code that handles malformed or unsupported images: decoding can now reject inputs that previously produced partial or incorrect results.
4. Review TIFF images written by earlier versions of the library when they contain transparency; corrected alpha interpretation can change their appearance.
