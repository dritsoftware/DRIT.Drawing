using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using DRIT.Drawing.Brushes;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Pens;
using DRIT.Drawing.Text;
using DRIT.Drawing.Vector.Svg;
using DrawingFont = DRIT.Drawing.Text.Font;
using DrawingMetafile = DRIT.Drawing.Imaging.Metafile;

namespace DRIT.Drawing.Examples;

/// <summary>
/// Contains runnable examples for raster formats, image transformations, pixel access, encoding options, metadata, limits, TIFF, and ICO files.
/// </summary>
internal static class ImageExamples
{
    /// <summary>
    /// Creates one source image and exports it through the supported raster encoders.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region RasterFormats
    internal static void ImagesSupportedRasterFormats(ExampleContext context)
    {
        using var bitmap = ExampleSupport.CreatePhotoLike();
        var formats = new (string Name, ImageFormat Format)[]
        {
            ("png", ImageFormat.Png), ("jpg", ImageFormat.Jpeg), ("bmp", ImageFormat.Bmp),
            ("gif", ImageFormat.Gif), ("tiff", ImageFormat.Tiff),
        };
        var sizes = new System.Text.StringBuilder();
        foreach (var format in formats)
        {
            string path = context.Path($"RasterFormats-supported-formats.{format.Name}");
            bitmap.Save(path, format.Format);
            sizes.AppendLine($"{format.Name,-5} {new FileInfo(path).Length,9:N0} bytes");
        }
        File.WriteAllText(context.Path("RasterFormats-sizes.txt"), sizes.ToString());
        Console.WriteLine($"  wrote {formats.Length} raster formats");
    }
    #endregion

    /// <summary>
    /// Draws a source rectangle into destination rectangles to demonstrate scaling and cropping.
    /// </summary>
    /// <param name="context">The output context that determines where generated files are written.</param>
    #region ScaleAndCrop
    internal static void ImagesScaleAndCrop(ExampleContext context)
    {
        using var source = ExampleSupport.CreatePhotoLike();
        using var result = ExampleSupport.NewCanvas(720, 420, Color.White);
        using var graphics = Graphics.FromImage(result);
        graphics.DrawImage(source, new RectangleF(40, 40, 300, 340),
            new RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
        graphics.DrawImage(source, new RectangleF(390, 80, 260, 240),
            new RectangleF(100, 30, 220, 170), GraphicsUnit.Pixel);
        ExampleSupport.Save(result, context, "ScaleAndCrop-scale-crop.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Clones an image, applies rotation and flipping to the clone, and preserves the original image.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region RotateFlipClone
    internal static void RotateFlipAndClone(ExampleContext c)
    {
        using var source = ExampleSupport.CreatePhotoLike();
        using var clone = new Bitmap(source);
        clone.RotateFlip(RotateFlipType.Rotate90FlipX);
        ExampleSupport.Save(source, c, "RotateFlipClone-original.png", ImageFormat.Png);
        ExampleSupport.Save(clone, c, "RotateFlipClone-rotated-clone.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Reads and writes individual pixels to demonstrate simple pixel-level editing.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region PixelAccess
    internal static void ReadWritePixels(ExampleContext c)
    {
        using var bitmap = new Bitmap(160, 100, PixelFormat.Format32bppArgb);
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                bitmap.SetPixel(x, y, Color.FromArgb(255, x * 255 / bitmap.Width, y * 255 / bitmap.Height, 120));
        Console.WriteLine($"  center pixel: {bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2)}");
        ExampleSupport.Save(bitmap, c, "PixelAccess-pixels.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Locks bitmap data, edits pixels using the stride, and unlocks the bitmap safely.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region LockBits
    internal static void DirectPixelAccess(ExampleContext c)
    {
        using var bitmap = ExampleSupport.NewCanvas(180, 120, Color.White);
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            // Copy the locked pixels out, edit them in a byte array, and copy them back.
            // Rows are Stride bytes apart (not Width * 4), and each pixel is stored as B, G, R, A.
            var pixels = new byte[Math.Abs(data.Stride) * data.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            for (int y = 20; y < 100; y++)
            {
                for (int x = 30; x < 150; x++)
                {
                    int offset = y * data.Stride + x * 4;
                    pixels[offset] = 160;                                   // blue
                    pixels[offset + 1] = (byte)((y - 20) * 255 / 79);       // green grows downwards
                    pixels[offset + 2] = (byte)((x - 30) * 255 / 119);      // red grows to the right
                    pixels[offset + 3] = 255;                               // alpha
                }
            }
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        ExampleSupport.Save(bitmap, c, "LockBits-lockbits.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Applies supported image attribute operations to show how colour transforms affect rendering.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region ImageAttributes
    internal static void ImageAttributes(ExampleContext c)
    {
        using var source = ExampleSupport.CreatePhotoLike(360, 220);
        using var output = ExampleSupport.NewCanvas(720, 300, Color.White);
        using var g = Graphics.FromImage(output);
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix(new[]
        {
            new[] { 0.35f, 0f, 0f, 0f, 0f }, new[] { 0f, 0.35f, 0f, 0f, 0f },
            new[] { 0f, 0f, 0.35f, 0f, 0f }, new[] { 0f, 0f, 0f, 1f, 0f }, new[] { 0.65f, 0.65f, 0.65f, 0f, 1f },
        }));
        g.DrawImage(source, new RectangleF(30, 40, 300, 190), new RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel, attributes);
        g.DrawImage(source, new RectangleF(390, 40, 300, 190), new RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
        ExampleSupport.Save(output, c, "ImageAttributes-image-attributes.png", ImageFormat.Png);
    }
    #endregion

    /// <summary>
    /// Encodes the same image with different JPEG quality settings for comparison.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region JpegQuality
    internal static void JpegQuality(ExampleContext c)
    {
        using var source = ExampleSupport.CreatePhotoLike(480, 300);
        foreach (long quality in new[] { 30L, 90L })
            using (var parameters = new EncoderParameters())
            using (var parameter = new EncoderParameter(Encoder.Quality, quality))
            using (var output = File.Create(c.Path($"JpegQuality-jpeg-quality-{quality}.jpg")))
            {
                parameters.Param = new[] { parameter };
                source.Save(output, ImageFormat.Jpeg, parameters);
            }
        source.Save(c.Path("JpegQuality-jpeg-quality-default.jpg"), ImageFormat.Jpeg);

        File.WriteAllText(c.Path("JpegQuality-sizes.txt"),
            $"quality 30:  {new FileInfo(c.Path("JpegQuality-jpeg-quality-30.jpg")).Length,6:N0} bytes\n" +
            $"quality 90:  {new FileInfo(c.Path("JpegQuality-jpeg-quality-90.jpg")).Length,6:N0} bytes\n" +
            $"unspecified: {new FileInfo(c.Path("JpegQuality-jpeg-quality-default.jpg")).Length,6:N0} bytes\n");
    }
    #endregion

    /// <summary>
    /// Reports image resolution and the metadata properties exposed by the current image implementation.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region Resolution
    internal static void ResolutionAndMetadata(ExampleContext c)
    {
        using var bitmap = ExampleSupport.CreatePhotoLike();
        File.WriteAllText(c.Path("Resolution-resolution.txt"), $"HorizontalResolution={bitmap.HorizontalResolution}\nVerticalResolution={bitmap.VerticalResolution}\nRawFormat={bitmap.RawFormat.Name}\n");
    }
    #endregion

    /// <summary>
    /// Applies a pixel limit to upload-like input and records safe rejection of an oversized decode.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region DecodeLimitsForUploads
    internal static void DecodeLimitsForUploads(ExampleContext c)
    {
        // Limits for an upload endpoint: at most 1 megapixel and 2000 pixels along either side.
        var limits = new ImageDecodeLimits(maxPixels: 1_000_000, maxDimension: 2_000);
        var uploads = new (string Name, byte[] Bytes)[]
        {
            ("thumbnail 320x200", EncodePng(320, 200)),
            ("poster 1500x1100", EncodePng(1500, 1100)),   // 1.65 megapixels
            ("banner 2400x100", EncodePng(2400, 100)),     // only 0.24 megapixels, but too wide
            ("not an image", System.Text.Encoding.ASCII.GetBytes("this is not an image")),
        };

        var report = new System.Text.StringBuilder();
        foreach (var (name, bytes) in uploads)
        {
            using var stream = new MemoryStream(bytes);
            try
            {
                using var image = Image.FromStream(stream, limits);
                report.AppendLine($"{name}: accepted as {image.Width}x{image.Height}");
            }
            catch (Exception ex) when (ex is InvalidDataException || ex is ArgumentException || ex is NotSupportedException)
            {
                report.AppendLine($"{name}: rejected ({ex.GetType().Name}) {ex.Message}");
            }
        }
        File.WriteAllText(c.Path("DecodeLimitsForUploads-report.txt"), report.ToString());

        static byte[] EncodePng(int width, int height)
        {
            using var source = ExampleSupport.CreatePhotoLike(width, height);
            using var stream = new MemoryStream();
            source.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
    }
    #endregion

    /// <summary>
    /// Loads a configured TIFF fixture, or a generated fallback, and reports image properties.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region MultiStripTiff
    internal static void MultiStripTiff(ExampleContext c)
    {
        string? input = Environment.GetEnvironmentVariable("DRIT_EXAMPLE_TIFF_INPUT");
        if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
        {
            using var generated = ExampleSupport.CreatePhotoLike(240, 160);
            input = c.Path("MultiStripTiff-generated-tiff.tiff");
            generated.Save(input, ImageFormat.Tiff);
        }
        using var image = Image.FromFile(input);
        image.Save(c.Path("MultiStripTiff-tiff-copy.tiff"), ImageFormat.Tiff);
        File.WriteAllText(c.Path("MultiStripTiff-tiff-info.txt"), $"Input={System.IO.Path.GetFileName(input)}\nDimensions={image.Width}x{image.Height}\nFormat={image.RawFormat.Name}\n");
    }
    #endregion

    /// <summary>
    /// Generates an ICO file containing a PNG image, then loads and renders it.
    /// </summary>
    /// <param name="c">The output context that determines where generated files are written.</param>
    #region Icon
    internal static void WorkWithIcon(ExampleContext c)
    {
        using var source = ExampleSupport.CreatePhotoLike(64, 64);
        using var png = new MemoryStream();
        source.Save(png, ImageFormat.Png);
        byte[] pngBytes = png.ToArray();
        string iconPath = c.Path("Icon-input.ico");
        using (var output = File.Create(iconPath))
        using (var writer = new BinaryWriter(output))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)1);
            writer.Write((byte)64);
            writer.Write((byte)64);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write((uint)pngBytes.Length);
            writer.Write((uint)22);
            writer.Write(pngBytes);
        }
        using var icon = new Icon(iconPath);
        using var bitmap = icon.ToBitmap();
        ExampleSupport.Save(bitmap, c, "Icon-icon.png", ImageFormat.Png);
        Console.WriteLine($"  icon size: {icon.Width}x{icon.Height}");
    }
    #endregion
}




