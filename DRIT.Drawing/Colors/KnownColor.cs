namespace DRIT.Drawing.Colors
{
    /// <summary>
    /// Specifies known system and web colors, matching GDI+ KnownColor values exactly.
    /// Values 1-26 are system colors; 27-167 are web/named colors; 168-174 are additional system colors.
    /// </summary>
    public enum KnownColor
    {
        // System colors (1-26)
        /// <summary>
        /// The border of the active window.
        /// </summary>
        ActiveBorder         = 1,
        /// <summary>
        /// The background of the active window title bar.
        /// </summary>
        ActiveCaption        = 2,
        /// <summary>
        /// The text in the active window title bar.
        /// </summary>
        ActiveCaptionText    = 3,
        /// <summary>
        /// The background of the multiple-document interface workspace.
        /// </summary>
        AppWorkspace         = 4,
        /// <summary>
        /// The face of a three-dimensional control.
        /// </summary>
        Control              = 5,
        /// <summary>
        /// The shadow of a three-dimensional control.
        /// </summary>
        ControlDark          = 6,
        /// <summary>
        /// The dark shadow of a three-dimensional control.
        /// </summary>
        ControlDarkDark      = 7,
        /// <summary>
        /// The light edge of a three-dimensional control.
        /// </summary>
        ControlLight         = 8,
        /// <summary>
        /// The highlight edge of a three-dimensional control.
        /// </summary>
        ControlLightLight    = 9,
        /// <summary>
        /// The text displayed in a three-dimensional control.
        /// </summary>
        ControlText          = 10,
        /// <summary>
        /// The desktop background.
        /// </summary>
        Desktop              = 11,
        /// <summary>
        /// Text displayed for a disabled control.
        /// </summary>
        GrayText             = 12,
        /// <summary>
        /// The background of a selected item.
        /// </summary>
        Highlight            = 13,
        /// <summary>
        /// The text of a selected item.
        /// </summary>
        HighlightText        = 14,
        /// <summary>
        /// The color used to indicate a hot-tracked item, such as a hyperlink.
        /// </summary>
        HotTrack             = 15,
        /// <summary>
        /// The border of an inactive window.
        /// </summary>
        InactiveBorder       = 16,
        /// <summary>
        /// The background of an inactive window title bar.
        /// </summary>
        InactiveCaption      = 17,
        /// <summary>
        /// The text in an inactive window title bar.
        /// </summary>
        InactiveCaptionText  = 18,
        /// <summary>
        /// The background of an informational tooltip.
        /// </summary>
        Info                 = 19,
        /// <summary>
        /// The text in an informational tooltip.
        /// </summary>
        InfoText             = 20,
        /// <summary>
        /// The background of a menu.
        /// </summary>
        Menu                 = 21,
        /// <summary>
        /// The text in a menu.
        /// </summary>
        MenuText             = 22,
        /// <summary>
        /// The background of a scroll bar.
        /// </summary>
        ScrollBar            = 23,
        /// <summary>
        /// The background of a window client area.
        /// </summary>
        Window               = 24,
        /// <summary>
        /// The frame of a window.
        /// </summary>
        WindowFrame          = 25,
        /// <summary>
        /// The text in a window client area.
        /// </summary>
        WindowText           = 26,

        // Web / named colors (27-167)
        /// <summary>
        /// A fully transparent color.
        /// </summary>
        Transparent          = 27,
        /// <summary>
        /// The named color AliceBlue (RGB 240, 248, 255).
        /// </summary>
        AliceBlue            = 28,
        /// <summary>
        /// The named color AntiqueWhite (RGB 250, 235, 215).
        /// </summary>
        AntiqueWhite         = 29,
        /// <summary>
        /// The named color Aqua (RGB 0, 255, 255).
        /// </summary>
        Aqua                 = 30,
        /// <summary>
        /// The named color Aquamarine (RGB 127, 255, 212).
        /// </summary>
        Aquamarine           = 31,
        /// <summary>
        /// The named color Azure (RGB 240, 255, 255).
        /// </summary>
        Azure                = 32,
        /// <summary>
        /// The named color Beige (RGB 245, 245, 220).
        /// </summary>
        Beige                = 33,
        /// <summary>
        /// The named color Bisque (RGB 255, 228, 196).
        /// </summary>
        Bisque               = 34,
        /// <summary>
        /// The named color Black (RGB 0, 0, 0).
        /// </summary>
        Black                = 35,
        /// <summary>
        /// The named color BlanchedAlmond (RGB 255, 235, 205).
        /// </summary>
        BlanchedAlmond       = 36,
        /// <summary>
        /// The named color Blue (RGB 0, 0, 255).
        /// </summary>
        Blue                 = 37,
        /// <summary>
        /// The named color BlueViolet (RGB 138, 43, 226).
        /// </summary>
        BlueViolet           = 38,
        /// <summary>
        /// The named color Brown (RGB 165, 42, 42).
        /// </summary>
        Brown                = 39,
        /// <summary>
        /// The named color BurlyWood (RGB 222, 184, 135).
        /// </summary>
        BurlyWood            = 40,
        /// <summary>
        /// The named color CadetBlue (RGB 95, 158, 160).
        /// </summary>
        CadetBlue            = 41,
        /// <summary>
        /// The named color Chartreuse (RGB 127, 255, 0).
        /// </summary>
        Chartreuse           = 42,
        /// <summary>
        /// The named color Chocolate (RGB 210, 105, 30).
        /// </summary>
        Chocolate            = 43,
        /// <summary>
        /// The named color Coral (RGB 255, 127, 80).
        /// </summary>
        Coral                = 44,
        /// <summary>
        /// The named color CornflowerBlue (RGB 100, 149, 237).
        /// </summary>
        CornflowerBlue       = 45,
        /// <summary>
        /// The named color Cornsilk (RGB 255, 248, 220).
        /// </summary>
        Cornsilk             = 46,
        /// <summary>
        /// The named color Crimson (RGB 220, 20, 60).
        /// </summary>
        Crimson              = 47,
        /// <summary>
        /// The named color Cyan (RGB 0, 255, 255).
        /// </summary>
        Cyan                 = 48,
        /// <summary>
        /// The named color DarkBlue (RGB 0, 0, 139).
        /// </summary>
        DarkBlue             = 49,
        /// <summary>
        /// The named color DarkCyan (RGB 0, 139, 139).
        /// </summary>
        DarkCyan             = 50,
        /// <summary>
        /// The named color DarkGoldenrod (RGB 184, 134, 11).
        /// </summary>
        DarkGoldenrod        = 51,
        /// <summary>
        /// The named color DarkGray (RGB 169, 169, 169).
        /// </summary>
        DarkGray             = 52,
        /// <summary>
        /// The named color DarkGreen (RGB 0, 100, 0).
        /// </summary>
        DarkGreen            = 53,
        /// <summary>
        /// The named color DarkKhaki (RGB 189, 183, 107).
        /// </summary>
        DarkKhaki            = 54,
        /// <summary>
        /// The named color DarkMagenta (RGB 139, 0, 139).
        /// </summary>
        DarkMagenta          = 55,
        /// <summary>
        /// The named color DarkOliveGreen (RGB 85, 107, 47).
        /// </summary>
        DarkOliveGreen       = 56,
        /// <summary>
        /// The named color DarkOrange (RGB 255, 140, 0).
        /// </summary>
        DarkOrange           = 57,
        /// <summary>
        /// The named color DarkOrchid (RGB 153, 50, 204).
        /// </summary>
        DarkOrchid           = 58,
        /// <summary>
        /// The named color DarkRed (RGB 139, 0, 0).
        /// </summary>
        DarkRed              = 59,
        /// <summary>
        /// The named color DarkSalmon (RGB 233, 150, 122).
        /// </summary>
        DarkSalmon           = 60,
        /// <summary>
        /// The named color DarkSeaGreen (RGB 143, 188, 143).
        /// </summary>
        DarkSeaGreen         = 61,
        /// <summary>
        /// The named color DarkSlateBlue (RGB 72, 61, 139).
        /// </summary>
        DarkSlateBlue        = 62,
        /// <summary>
        /// The named color DarkSlateGray (RGB 47, 79, 79).
        /// </summary>
        DarkSlateGray        = 63,
        /// <summary>
        /// The named color DarkTurquoise (RGB 0, 206, 209).
        /// </summary>
        DarkTurquoise        = 64,
        /// <summary>
        /// The named color DarkViolet (RGB 148, 0, 211).
        /// </summary>
        DarkViolet           = 65,
        /// <summary>
        /// The named color DeepPink (RGB 255, 20, 147).
        /// </summary>
        DeepPink             = 66,
        /// <summary>
        /// The named color DeepSkyBlue (RGB 0, 191, 255).
        /// </summary>
        DeepSkyBlue          = 67,
        /// <summary>
        /// The named color DimGray (RGB 105, 105, 105).
        /// </summary>
        DimGray              = 68,
        /// <summary>
        /// The named color DodgerBlue (RGB 30, 144, 255).
        /// </summary>
        DodgerBlue           = 69,
        /// <summary>
        /// The named color Firebrick (RGB 178, 34, 34).
        /// </summary>
        Firebrick            = 70,
        /// <summary>
        /// The named color FloralWhite (RGB 255, 250, 240).
        /// </summary>
        FloralWhite          = 71,
        /// <summary>
        /// The named color ForestGreen (RGB 34, 139, 34).
        /// </summary>
        ForestGreen          = 72,
        /// <summary>
        /// The named color Fuchsia (RGB 255, 0, 255).
        /// </summary>
        Fuchsia              = 73,
        /// <summary>
        /// The named color Gainsboro (RGB 220, 220, 220).
        /// </summary>
        Gainsboro            = 74,
        /// <summary>
        /// The named color GhostWhite (RGB 248, 248, 255).
        /// </summary>
        GhostWhite           = 75,
        /// <summary>
        /// The named color Gold (RGB 255, 215, 0).
        /// </summary>
        Gold                 = 76,
        /// <summary>
        /// The named color Goldenrod (RGB 218, 165, 32).
        /// </summary>
        Goldenrod            = 77,
        /// <summary>
        /// The named color Gray (RGB 128, 128, 128).
        /// </summary>
        Gray                 = 78,
        /// <summary>
        /// The named color Green (RGB 0, 128, 0).
        /// </summary>
        Green                = 79,
        /// <summary>
        /// The named color GreenYellow (RGB 173, 255, 47).
        /// </summary>
        GreenYellow          = 80,
        /// <summary>
        /// The named color Honeydew (RGB 240, 255, 240).
        /// </summary>
        Honeydew             = 81,
        /// <summary>
        /// The named color HotPink (RGB 255, 105, 180).
        /// </summary>
        HotPink              = 82,
        /// <summary>
        /// The named color IndianRed (RGB 205, 92, 92).
        /// </summary>
        IndianRed            = 83,
        /// <summary>
        /// The named color Indigo (RGB 75, 0, 130).
        /// </summary>
        Indigo               = 84,
        /// <summary>
        /// The named color Ivory (RGB 255, 255, 240).
        /// </summary>
        Ivory                = 85,
        /// <summary>
        /// The named color Khaki (RGB 240, 230, 140).
        /// </summary>
        Khaki                = 86,
        /// <summary>
        /// The named color Lavender (RGB 230, 230, 250).
        /// </summary>
        Lavender             = 87,
        /// <summary>
        /// The named color LavenderBlush (RGB 255, 240, 245).
        /// </summary>
        LavenderBlush        = 88,
        /// <summary>
        /// The named color LawnGreen (RGB 124, 252, 0).
        /// </summary>
        LawnGreen            = 89,
        /// <summary>
        /// The named color LemonChiffon (RGB 255, 250, 205).
        /// </summary>
        LemonChiffon         = 90,
        /// <summary>
        /// The named color LightBlue (RGB 173, 216, 230).
        /// </summary>
        LightBlue            = 91,
        /// <summary>
        /// The named color LightCoral (RGB 240, 128, 128).
        /// </summary>
        LightCoral           = 92,
        /// <summary>
        /// The named color LightCyan (RGB 224, 255, 255).
        /// </summary>
        LightCyan            = 93,
        /// <summary>
        /// The named color LightGoldenrodYellow (RGB 250, 250, 210).
        /// </summary>
        LightGoldenrodYellow = 94,
        /// <summary>
        /// The named color LightGray (RGB 211, 211, 211).
        /// </summary>
        LightGray            = 95,
        /// <summary>
        /// The named color LightGreen (RGB 144, 238, 144).
        /// </summary>
        LightGreen           = 96,
        /// <summary>
        /// The named color LightPink (RGB 255, 182, 193).
        /// </summary>
        LightPink            = 97,
        /// <summary>
        /// The named color LightSalmon (RGB 255, 160, 122).
        /// </summary>
        LightSalmon          = 98,
        /// <summary>
        /// The named color LightSeaGreen (RGB 32, 178, 170).
        /// </summary>
        LightSeaGreen        = 99,
        /// <summary>
        /// The named color LightSkyBlue (RGB 135, 206, 250).
        /// </summary>
        LightSkyBlue         = 100,
        /// <summary>
        /// The named color LightSlateGray (RGB 119, 136, 153).
        /// </summary>
        LightSlateGray       = 101,
        /// <summary>
        /// The named color LightSteelBlue (RGB 176, 196, 222).
        /// </summary>
        LightSteelBlue       = 102,
        /// <summary>
        /// The named color LightYellow (RGB 255, 255, 224).
        /// </summary>
        LightYellow          = 103,
        /// <summary>
        /// The named color Lime (RGB 0, 255, 0).
        /// </summary>
        Lime                 = 104,
        /// <summary>
        /// The named color LimeGreen (RGB 50, 205, 50).
        /// </summary>
        LimeGreen            = 105,
        /// <summary>
        /// The named color Linen (RGB 250, 240, 230).
        /// </summary>
        Linen                = 106,
        /// <summary>
        /// The named color Magenta (RGB 255, 0, 255).
        /// </summary>
        Magenta              = 107,
        /// <summary>
        /// The named color Maroon (RGB 128, 0, 0).
        /// </summary>
        Maroon               = 108,
        /// <summary>
        /// The named color MediumAquamarine (RGB 102, 205, 170).
        /// </summary>
        MediumAquamarine     = 109,
        /// <summary>
        /// The named color MediumBlue (RGB 0, 0, 205).
        /// </summary>
        MediumBlue           = 110,
        /// <summary>
        /// The named color MediumOrchid (RGB 186, 85, 211).
        /// </summary>
        MediumOrchid         = 111,
        /// <summary>
        /// The named color MediumPurple (RGB 147, 112, 219).
        /// </summary>
        MediumPurple         = 112,
        /// <summary>
        /// The named color MediumSeaGreen (RGB 60, 179, 113).
        /// </summary>
        MediumSeaGreen       = 113,
        /// <summary>
        /// The named color MediumSlateBlue (RGB 123, 104, 238).
        /// </summary>
        MediumSlateBlue      = 114,
        /// <summary>
        /// The named color MediumSpringGreen (RGB 0, 250, 154).
        /// </summary>
        MediumSpringGreen    = 115,
        /// <summary>
        /// The named color MediumTurquoise (RGB 72, 209, 204).
        /// </summary>
        MediumTurquoise      = 116,
        /// <summary>
        /// The named color MediumVioletRed (RGB 199, 21, 133).
        /// </summary>
        MediumVioletRed      = 117,
        /// <summary>
        /// The named color MidnightBlue (RGB 25, 25, 112).
        /// </summary>
        MidnightBlue         = 118,
        /// <summary>
        /// The named color MintCream (RGB 245, 255, 250).
        /// </summary>
        MintCream            = 119,
        /// <summary>
        /// The named color MistyRose (RGB 255, 228, 225).
        /// </summary>
        MistyRose            = 120,
        /// <summary>
        /// The named color Moccasin (RGB 255, 228, 181).
        /// </summary>
        Moccasin             = 121,
        /// <summary>
        /// The named color NavajoWhite (RGB 255, 222, 173).
        /// </summary>
        NavajoWhite          = 122,
        /// <summary>
        /// The named color Navy (RGB 0, 0, 128).
        /// </summary>
        Navy                 = 123,
        /// <summary>
        /// The named color OldLace (RGB 253, 245, 230).
        /// </summary>
        OldLace              = 124,
        /// <summary>
        /// The named color Olive (RGB 128, 128, 0).
        /// </summary>
        Olive                = 125,
        /// <summary>
        /// The named color OliveDrab (RGB 107, 142, 35).
        /// </summary>
        OliveDrab            = 126,
        /// <summary>
        /// The named color Orange (RGB 255, 165, 0).
        /// </summary>
        Orange               = 127,
        /// <summary>
        /// The named color OrangeRed (RGB 255, 69, 0).
        /// </summary>
        OrangeRed            = 128,
        /// <summary>
        /// The named color Orchid (RGB 218, 112, 214).
        /// </summary>
        Orchid               = 129,
        /// <summary>
        /// The named color PaleGoldenrod (RGB 238, 232, 170).
        /// </summary>
        PaleGoldenrod        = 130,
        /// <summary>
        /// The named color PaleGreen (RGB 152, 251, 152).
        /// </summary>
        PaleGreen            = 131,
        /// <summary>
        /// The named color PaleTurquoise (RGB 175, 238, 238).
        /// </summary>
        PaleTurquoise        = 132,
        /// <summary>
        /// The named color PaleVioletRed (RGB 219, 112, 147).
        /// </summary>
        PaleVioletRed        = 133,
        /// <summary>
        /// The named color PapayaWhip (RGB 255, 239, 213).
        /// </summary>
        PapayaWhip           = 134,
        /// <summary>
        /// The named color PeachPuff (RGB 255, 218, 185).
        /// </summary>
        PeachPuff            = 135,
        /// <summary>
        /// The named color Peru (RGB 205, 133, 63).
        /// </summary>
        Peru                 = 136,
        /// <summary>
        /// The named color Pink (RGB 255, 192, 203).
        /// </summary>
        Pink                 = 137,
        /// <summary>
        /// The named color Plum (RGB 221, 160, 221).
        /// </summary>
        Plum                 = 138,
        /// <summary>
        /// The named color PowderBlue (RGB 176, 224, 230).
        /// </summary>
        PowderBlue           = 139,
        /// <summary>
        /// The named color Purple (RGB 128, 0, 128).
        /// </summary>
        Purple               = 140,
        /// <summary>
        /// The named color Red (RGB 255, 0, 0).
        /// </summary>
        Red                  = 141,
        /// <summary>
        /// The named color RosyBrown (RGB 188, 143, 143).
        /// </summary>
        RosyBrown            = 142,
        /// <summary>
        /// The named color RoyalBlue (RGB 65, 105, 225).
        /// </summary>
        RoyalBlue            = 143,
        /// <summary>
        /// The named color SaddleBrown (RGB 139, 69, 19).
        /// </summary>
        SaddleBrown          = 144,
        /// <summary>
        /// The named color Salmon (RGB 250, 128, 114).
        /// </summary>
        Salmon               = 145,
        /// <summary>
        /// The named color SandyBrown (RGB 244, 164, 96).
        /// </summary>
        SandyBrown           = 146,
        /// <summary>
        /// The named color SeaGreen (RGB 46, 139, 87).
        /// </summary>
        SeaGreen             = 147,
        /// <summary>
        /// The named color SeaShell (RGB 255, 245, 238).
        /// </summary>
        SeaShell             = 148,
        /// <summary>
        /// The named color Sienna (RGB 160, 82, 45).
        /// </summary>
        Sienna               = 149,
        /// <summary>
        /// The named color Silver (RGB 192, 192, 192).
        /// </summary>
        Silver               = 150,
        /// <summary>
        /// The named color SkyBlue (RGB 135, 206, 235).
        /// </summary>
        SkyBlue              = 151,
        /// <summary>
        /// The named color SlateBlue (RGB 106, 90, 205).
        /// </summary>
        SlateBlue            = 152,
        /// <summary>
        /// The named color SlateGray (RGB 112, 128, 144).
        /// </summary>
        SlateGray            = 153,
        /// <summary>
        /// The named color Snow (RGB 255, 250, 250).
        /// </summary>
        Snow                 = 154,
        /// <summary>
        /// The named color SpringGreen (RGB 0, 255, 127).
        /// </summary>
        SpringGreen          = 155,
        /// <summary>
        /// The named color SteelBlue (RGB 70, 130, 180).
        /// </summary>
        SteelBlue            = 156,
        /// <summary>
        /// The named color Tan (RGB 210, 180, 140).
        /// </summary>
        Tan                  = 157,
        /// <summary>
        /// The named color Teal (RGB 0, 128, 128).
        /// </summary>
        Teal                 = 158,
        /// <summary>
        /// The named color Thistle (RGB 216, 191, 216).
        /// </summary>
        Thistle              = 159,
        /// <summary>
        /// The named color Tomato (RGB 255, 99, 71).
        /// </summary>
        Tomato               = 160,
        /// <summary>
        /// The named color Turquoise (RGB 64, 224, 208).
        /// </summary>
        Turquoise            = 161,
        /// <summary>
        /// The named color Violet (RGB 238, 130, 238).
        /// </summary>
        Violet               = 162,
        /// <summary>
        /// The named color Wheat (RGB 245, 222, 179).
        /// </summary>
        Wheat                = 163,
        /// <summary>
        /// The named color White (RGB 255, 255, 255).
        /// </summary>
        White                = 164,
        /// <summary>
        /// The named color WhiteSmoke (RGB 245, 245, 245).
        /// </summary>
        WhiteSmoke           = 165,
        /// <summary>
        /// The named color Yellow (RGB 255, 255, 0).
        /// </summary>
        Yellow               = 166,
        /// <summary>
        /// The named color YellowGreen (RGB 154, 205, 50).
        /// </summary>
        YellowGreen          = 167,

        // Additional system colors (168-174)
        /// <summary>
        /// The face of a three-dimensional button.
        /// </summary>
        ButtonFace               = 168,
        /// <summary>
        /// The highlight edge of a three-dimensional button.
        /// </summary>
        ButtonHighlight          = 169,
        /// <summary>
        /// The shadow of a three-dimensional button.
        /// </summary>
        ButtonShadow             = 170,
        /// <summary>
        /// The lighter color in the gradient of an active title bar.
        /// </summary>
        GradientActiveCaption    = 171,
        /// <summary>
        /// The lighter color in the gradient of an inactive title bar.
        /// </summary>
        GradientInactiveCaption  = 172,
        /// <summary>
        /// The background of a menu bar.
        /// </summary>
        MenuBar                  = 173,
        /// <summary>
        /// The background of a highlighted menu item.
        /// </summary>
        MenuHighlight            = 174,
    }
}
