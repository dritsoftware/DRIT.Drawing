using DRIT.Drawing.Font.Glyphs;
using DRIT.Drawing.Font.Ttf.Tables;

namespace DRIT.Drawing.Font.Encoding
{
    /// <summary>
    /// Implements <see cref="IFontEncoding"/> by delegating to a CMap format subtable
    /// (<see cref="TtfCMapFormatBaseTable"/>) selected from a TrueType/OpenType font's
    /// "cmap" table.
    /// </summary>
    public sealed class TtfUnicodeEncoding : IFontEncoding
    {
        private readonly TtfCMapFormatBaseTable _cmapTable;
        private readonly bool _legacySymbolFont;

        /// <param name="cmapTable">
        /// The best Unicode-capable subtable as returned by
        /// <see cref="TtfCMapTable.FindUnicodeTable"/>.
        /// Must not be null.
        /// </param>
        /// <param name="legacySymbolFont">
        /// <c>true</c> when the font uses the Windows legacy symbol-font private-use mapping.
        /// </param>
        public TtfUnicodeEncoding(TtfCMapFormatBaseTable cmapTable, bool legacySymbolFont = false)
        {
            _cmapTable = cmapTable;
            _legacySymbolFont = legacySymbolFont;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Returns <see cref="GlyphUInt32Id.NotDefId"/> (glyph index 0) for code
        /// points not present in the font.
        /// </remarks>
        public GlyphId? DecodeToGid(uint codePoint)
        {
            uint glyphIndex = _cmapTable.GetGlyphIndex(codePoint);
            if (glyphIndex == 0 && _legacySymbolFont && codePoint <= 0xFF)
                glyphIndex = _cmapTable.GetGlyphIndex(0xF000u | codePoint);
            return new GlyphUInt32Id(glyphIndex);
        }

        /// <inheritdoc/>
        public GlyphId? DecodeToGid(char ch) => DecodeToGid((uint)ch);

        /// <inheritdoc/>
        public bool TryGetGlyph(uint codePoint, out uint glyphId)
        {
            glyphId = DecodeToGid(codePoint) is GlyphUInt32Id id ? id.Value : 0;
            return glyphId != 0;
        }

        /// <inheritdoc/>
        public bool HasGlyph(uint codePoint) => DecodeToGid(codePoint) is GlyphUInt32Id id && id.Value != 0;
    }
}
