using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace DRIT.Drawing.IntegrationTests
{
    /// <summary>
    /// Base class for integration tests that produce visual output.
    /// Provides helper methods for saving images.
    /// </summary>
    public abstract class IntegrationTestBase
    {
        protected static readonly string OutPath = @"C:\Munka\File Formats\File Formats\DRIT.Drawing\Out";

        /// <summary>
        /// Saves a bitmap to the Out folder with the specified format.
        /// </summary>
        protected static void SaveImage(Bitmap bmp, string testName, ImageFormat format)
        {
            string extension = GetExtensionForFormat(format);
            string filename = $"{testName}{extension}";
            string fullPath = Path.Combine(OutPath, filename);
            
            // Ensure directory exists
            Directory.CreateDirectory(OutPath);
            
            bmp.Save(fullPath, format);
        }

        private static string GetExtensionForFormat(ImageFormat format)
        {
            if (format == ImageFormat.Png) return ".png";
            if (format == ImageFormat.Jpeg) return ".jpg";
            if (format == ImageFormat.Gif) return ".gif";
            if (format == ImageFormat.Tiff) return ".tif";
            if (format == ImageFormat.Bmp) return ".bmp";
            return ".png";
        }

        /// <summary>
        /// Saves a bitmap to the Out folder as PNG.
        /// </summary>
        protected static void SaveImage(Bitmap bmp, string testName)
        {
            SaveImage(bmp, testName, ImageFormat.Png);
        }
    }
}