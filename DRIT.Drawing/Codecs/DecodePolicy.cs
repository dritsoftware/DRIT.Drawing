using System;

namespace DRIT.Drawing.Codecs
{
    /// <summary>
    /// Separates the two kinds of validation the native decoders perform.
    /// <para>
    /// <b>Safety</b> rules (size, count, range and allocation limits; never index out of bounds)
    /// are always enforced and are not affected by this policy.
    /// </para>
    /// <para>
    /// <b>Conformance</b> rules (checksums of ancillary data, trailing bytes, metadata validity,
    /// missing end markers, reserved flags, declared sizes, …) reject files that other decoders
    /// (libpng, libjpeg, giflib, browsers) still display. They are enforced only in
    /// <see cref="Strict"/> mode, which the corpus probe enables so that the conformance corpus keeps
    /// proving that invalid files are detected. Library users get the lenient default.
    /// </para>
    /// </summary>
    internal static class DecodePolicy
    {
        /// <summary>AppContext switch that turns on strict conformance checking for this process.</summary>
        internal const string StrictSwitchName = "DRIT.Drawing.StrictConformance";

        /// <summary>
        /// Legacy switch from the progressive-JPEG compatibility work; it now implies
        /// <see cref="Strict"/> so that existing probes keep their meaning.
        /// </summary>
        internal const string LegacyStrictProgressiveSwitchName = "DRIT.Drawing.Jpeg.StrictProgressive";

        /// <summary>True when conformance rules must reject non-conforming input.</summary>
        internal static bool Strict
        {
            get
            {
                if (AppContext.TryGetSwitch(StrictSwitchName, out bool strict) && strict)
                    return true;
                return AppContext.TryGetSwitch(LegacyStrictProgressiveSwitchName, out bool legacy) && legacy;
            }
        }
    }
}
