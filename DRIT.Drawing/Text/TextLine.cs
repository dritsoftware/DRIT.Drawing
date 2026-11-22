using System.Collections.Generic;

namespace DRIT.Drawing.Text
{
    /// <summary>
    /// One visual line of text made up of one or more <see cref="TextRun"/> objects.
    /// </summary>
    internal sealed class TextLine
    {
        public List<TextRun> Runs       { get; } = new List<TextRun>();

        public float         Baseline   { get; set; }
        public float         Width      { get; set; }
        public float         LineHeight { get; set; }

        internal bool         IsLastLineOfParagraph { get; set; } = true;
        internal bool         TokenizeNativeText { get; set; }
        internal float        ExtraCharacterSpacing { get; set; }
        internal float        ExtraSpaceSpacing { get; set; }

        /// <summary>Maximum ascent across all runs (for mixed-size lines).</summary>
        public float         Ascent     { get; set; }

        /// <summary>Maximum descent across all runs (for mixed-size lines).</summary>
        public float         Descent    { get; set; }
    }
}