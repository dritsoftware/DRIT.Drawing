using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DRIT.Drawing.Imaging;

namespace DRIT.Drawing.Examples;

/// <summary>
/// Checks that the examples produced something worth looking at. A drawing call that silently draws
/// nothing (an unsupported feature, a font that cannot be resolved) leaves a blank or flat image; the
/// documentation shows these images, so such output must fail the run.
/// </summary>
internal static class OutputVerifier
{
    /// <summary>Smallest share of pixels that must differ from the dominant colour.</summary>
    private const double MinimumInkShare = 0.002;

    /// <summary>Returns one message per problem found in <paramref name="directory"/>; empty when all is well.</summary>
    internal static IReadOnlyList<string> Verify(string directory)
    {
        var problems = new List<string>();
        foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            string name = Path.GetRelativePath(directory, file);
            string extension = Path.GetExtension(file).ToLowerInvariant();
            try
            {
                if (new FileInfo(file).Length == 0)
                    problems.Add($"{name}: file is empty");
                else if (extension == ".png")
                    VerifyImage(file, name, problems);
                else if (extension == ".svg" && !File.ReadAllText(file).Contains("<svg", StringComparison.Ordinal))
                    problems.Add($"{name}: not an SVG document");
            }
            catch (Exception ex)
            {
                problems.Add($"{name}: could not be read back ({ex.GetType().Name}: {ex.Message})");
            }
        }
        return problems;
    }

    private static void VerifyImage(string file, string name, List<string> problems)
    {
        using var image = Image.FromFile(file);
        using var bitmap = new Bitmap(image);
        var counts = new Dictionary<int, int>();
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                int argb = bitmap.GetPixel(x, y).ToArgb();
                counts[argb] = counts.TryGetValue(argb, out int n) ? n + 1 : 1;
            }
        }

        long total = (long)bitmap.Width * bitmap.Height;
        long dominant = counts.Values.Max();
        if (counts.Count < 2)
            problems.Add($"{name}: image is a single flat colour");
        else if ((total - dominant) / (double)total < MinimumInkShare)
            problems.Add($"{name}: almost nothing is drawn ({total - dominant} of {total} pixels differ from the background)");
    }
}
