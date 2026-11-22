namespace DRIT.Drawing.Text
{
    /// <summary>
    /// Specifies the alignment of a text string relative to a layout rectangle.
    /// </summary>
    public enum StringAlignment
    {
        /// <summary>
        /// Align text near (left for horizontal, top for vertical).
        /// </summary>
        Near = 0,

        /// <summary>
        /// Center-align the text.
        /// </summary>
        Center = 1,

        /// <summary>
        /// Align text far (right for horizontal, bottom for vertical).
        /// </summary>
        Far = 2,

        /// <summary>
        /// Expand spaces on non-final wrapped lines to fill the layout width.
        /// </summary>
        Justify = 3,

        /// <summary>
        /// Distribute additional space between all characters to fill the layout width.
        /// </summary>
        Distributed = 4
    }
}