using System;
using System.Collections.Generic;
using DRIT.Drawing.Enums;

namespace DRIT.Drawing.Text
{
    /// <summary>
    /// Maps the generic font families (<see cref="GenericFontFamilies"/>) to a concrete
    /// installed <see cref="FontFamily"/> so that they have real glyph outlines and metrics.
    /// </summary>
    /// <remarks>
    /// Each generic family is resolved at most once per process by walking a per-platform
    /// list of well-known family names and taking the first one that is installed and
    /// exposes a regular face. The result is <c>null</c> when none is installed (for example
    /// when <see cref="FontSettings.AutoLoadSystemFonts"/> is disabled); callers then keep the
    /// generic family's built-in metrics and no outlines.
    /// </remarks>
    internal static class GenericFontResolver
    {
        private static readonly Lazy<FontFamily?> Serif =
            new Lazy<FontFamily?>(() => Find(GenericFontFamilies.Serif), true);

        private static readonly Lazy<FontFamily?> SansSerif =
            new Lazy<FontFamily?>(() => Find(GenericFontFamilies.SansSerif), true);

        private static readonly Lazy<FontFamily?> Monospace =
            new Lazy<FontFamily?>(() => Find(GenericFontFamilies.Monospace), true);

        /// <summary>Returns the installed family that stands in for <paramref name="generic"/>, or null.</summary>
        internal static FontFamily? Resolve(GenericFontFamilies generic)
        {
            switch (generic)
            {
                case GenericFontFamilies.Serif: return Serif.Value;
                case GenericFontFamilies.Monospace: return Monospace.Value;
                default: return SansSerif.Value;
            }
        }

        /// <summary>The ordered candidate names for a generic family on the current platform.</summary>
        internal static IReadOnlyList<string> GetCandidates(GenericFontFamilies generic)
        {
            PlatformID platform = Environment.OSVersion.Platform;
            if (platform == PlatformID.Win32NT)
            {
                switch (generic)
                {
                    case GenericFontFamilies.Serif:
                        return new[] { "Times New Roman", "Georgia", "Cambria", "Palatino Linotype" };
                    case GenericFontFamilies.Monospace:
                        return new[] { "Courier New", "Consolas", "Lucida Console" };
                    default:
                        return new[] { "Arial", "Segoe UI", "Tahoma", "Microsoft Sans Serif", "Verdana" };
                }
            }

            if (platform == PlatformID.MacOSX)
            {
                switch (generic)
                {
                    case GenericFontFamilies.Serif:
                        return new[] { "Times", "Times New Roman", "Georgia" };
                    case GenericFontFamilies.Monospace:
                        return new[] { "Menlo", "Monaco", "Courier New", "Courier" };
                    default:
                        return new[] { "Helvetica Neue", "Helvetica", "Arial" };
                }
            }

            switch (generic)
            {
                case GenericFontFamilies.Serif:
                    return new[] { "DejaVu Serif", "Liberation Serif", "Noto Serif", "Times New Roman" };
                case GenericFontFamilies.Monospace:
                    return new[] { "DejaVu Sans Mono", "Liberation Mono", "Noto Sans Mono", "Courier New" };
                default:
                    return new[] { "DejaVu Sans", "Liberation Sans", "Noto Sans", "Arial" };
            }
        }

        private static FontFamily? Find(GenericFontFamilies generic)
        {
            InstalledFontCollection installed = InstalledFontCollection.Default;
            foreach (string name in GetCandidates(generic))
            {
                FontFamily? family = installed.FindFamily(name);
                if (family != null && family.GetFontFace(FontStyle.Regular) != null)
                    return family;
            }
            return null;
        }
    }
}
