using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace DRIT.Drawing.IntegrationTests
{
    /// <summary>
    /// Assembly initializer to clean up the Out folder once before all tests.
    /// </summary>
    [TestClass]
    public static class AssemblyInitializer
    {
        private static readonly string OutPath = @"C:\Munka\File Formats\File Formats\DRIT.Drawing\Out";

        [AssemblyInitialize]
        public static void Init(TestContext context)
        {
            // Clean up Out folder
            Directory.CreateDirectory(OutPath);
            if (Directory.Exists(OutPath))
            {
                foreach (var file in Directory.GetFiles(OutPath, "*.*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        // Skip README.md
                        if (!file.EndsWith("README.md", StringComparison.OrdinalIgnoreCase))
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
        }
    }
}