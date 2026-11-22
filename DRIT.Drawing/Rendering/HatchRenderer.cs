using DRIT.Drawing.Colors;
using DRIT.Drawing.Drawing2D;

namespace DRIT.Drawing.Rendering
{
    internal static class HatchRenderer
    {
        public static Color Sample(HatchBrush brush, int x, int y)
        {
            bool fg = IsForeground(brush.HatchStyle, x, y);
            return fg ? brush.ForegroundColor : brush.BackgroundColor;
        }

        private static bool IsForeground(HatchStyle style, int x, int y)
        {
            string[] rows = GetPattern(style);
            return rows[y & 7][x & 7] == 'X';
        }

        private static string[] GetPattern(HatchStyle style)
        {
            int index = (int)style;
            if (index >= 0 && index < s_patternCache.Length)
            {
                string[] cached = s_patternCache[index];
                if (cached != null) return cached;

                string[] created = CreatePattern(style);
                string[] prior = System.Threading.Interlocked.CompareExchange(
                    ref s_patternCache[index], created, null);
                return prior ?? created;
            }
            return CreatePattern(style);
        }

        private static readonly string[][] s_patternCache = new string[53][];

        private static string[] CreatePattern(HatchStyle style)
        {
            switch (style)
            {
                case HatchStyle.Horizontal:
                    return new[] { "XXXXXXXX", "........", "........", "........", "........", "........", "........", "........" };
                case HatchStyle.Vertical:
                    return new[] { "X.......", "X.......", "X.......", "X.......", "X.......", "X.......", "X.......", "X......." };
                case HatchStyle.ForwardDiagonal:
                    return new[] { "X.......", ".X......", "..X.....", "...X....", "....X...", ".....X..", "......X.", ".......X" };
                case HatchStyle.BackwardDiagonal:
                    return new[] { ".......X", "......X.", ".....X..", "....X...", "...X....", "..X.....", ".X......", "X......." };
                case HatchStyle.Cross:
                    return new[] { "XXXXXXXX", "X.......", "X.......", "X.......", "X.......", "X.......", "X.......", "X......." };
                case HatchStyle.DiagonalCross:
                    return new[] { "X......X", ".X....X.", "..X..X..", "...XX...", "...XX...", "..X..X..", ".X....X.", "X......X" };
                case HatchStyle.Percent05:
                    return new[] { "X.......", "........", "........", "........", "....X...", "........", "........", "........" };
                case HatchStyle.Percent10:
                    return new[] { "X.......", "........", "....X...", "........", "X.......", "........", "....X...", "........" };
                case HatchStyle.Percent20:
                    return new[] { "X...X...", "........", "..X...X.", "........", "X...X...", "........", "..X...X.", "........" };
                case HatchStyle.Percent25:
                    return new[] { "X...X...", "..X...X.", "X...X...", "..X...X.", "X...X...", "..X...X.", "X...X...", "..X...X." };
                case HatchStyle.Percent30:
                    return new[] { "X.X.X.X.", ".X...X..", "X.X.X.X.", "...X...X", "X.X.X.X.", ".X...X..", "X.X.X.X.", "...X...X" };
                case HatchStyle.Percent40:
                    return new[] { "X.X.X.X.", ".X.X.X.X", "X.X.X.X.", ".X.X...X", "X.X.X.X.", ".X.X.X.X", "X.X.X.X.", "...X.X.X" };
                case HatchStyle.Percent50:
                    return new[] { "X.X.X.X.", ".X.X.X.X", "X.X.X.X.", ".X.X.X.X", "X.X.X.X.", ".X.X.X.X", "X.X.X.X.", ".X.X.X.X" };
                case HatchStyle.Percent60:
                    return new[] { "XXX.XXX.", ".X.X.X.X", "X.XXX.XX", ".X.X.X.X", "XXX.XXX.", ".X.X.X.X", "X.XXX.XX", ".X.X.X.X" };
                case HatchStyle.Percent70:
                    return new[] { ".XXX.XXX", "XX.XXX.X", ".XXX.XXX", "XX.XXX.X", ".XXX.XXX", "XX.XXX.X", ".XXX.XXX", "XX.XXX.X" };
                case HatchStyle.Percent75:
                    return new[] { ".XXX.XXX", "XXXXXXXX", "XX.XXX.X", "XXXXXXXX", ".XXX.XXX", "XXXXXXXX", "XX.XXX.X", "XXXXXXXX" };
                case HatchStyle.Percent80:
                    return new[] { "XXX.XXXX", "XXXXXXXX", "XXXXXXX.", "XXXXXXXX", "XXX.XXXX", "XXXXXXXX", "XXXXXXX.", "XXXXXXXX" };
                case HatchStyle.Percent90:
                    return new[] { "XXXXXXXX", "XXXXXXXX", "XXXXXXXX", "XXXX.XXX", "XXXXXXXX", "XXXXXXXX", "XXXXXXXX", ".XXXXXXX" };
                case HatchStyle.LightDownwardDiagonal:
                    return new[] { "X...X...", ".X...X..", "..X...X.", "...X...X", "X...X...", ".X...X..", "..X...X.", "...X...X" };
                case HatchStyle.LightUpwardDiagonal:
                    return new[] { "...X...X", "..X...X.", ".X...X..", "X...X...", "...X...X", "..X...X.", ".X...X..", "X...X..." };
                case HatchStyle.DarkDownwardDiagonal:
                    return new[] { "XX..XX..", ".XX..XX.", "..XX..XX", "X..XX..X", "XX..XX..", ".XX..XX.", "..XX..XX", "X..XX..X" };
                case HatchStyle.DarkUpwardDiagonal:
                    return new[] { "..XX..XX", ".XX..XX.", "XX..XX..", "X..XX..X", "..XX..XX", ".XX..XX.", "XX..XX..", "X..XX..X" };
                case HatchStyle.WideDownwardDiagonal:
                    return new[] { "XX.....X", "XXX.....", ".XXX....", "..XXX...", "...XXX..", "....XXX.", ".....XXX", "X.....XX" };
                case HatchStyle.WideUpwardDiagonal:
                    return new[] { "X.....XX", ".....XXX", "....XXX.", "...XXX..", "..XXX...", ".XXX....", "XXX.....", "XX.....X" };
                case HatchStyle.NarrowVertical:
                    return new[] { ".X.X.X.X", ".X.X.X.X", ".X.X.X.X", ".X.X.X.X", ".X.X.X.X", ".X.X.X.X", ".X.X.X.X", ".X.X.X.X" };
                case HatchStyle.NarrowHorizontal:
                    return new[] { "XXXXXXXX", "........", "XXXXXXXX", "........", "XXXXXXXX", "........", "XXXXXXXX", "........" };
                case HatchStyle.DarkVertical:
                    return new[] { "XX..XX..", "XX..XX..", "XX..XX..", "XX..XX..", "XX..XX..", "XX..XX..", "XX..XX..", "XX..XX.." };
                case HatchStyle.DarkHorizontal:
                    return new[] { "XXXXXXXX", "XXXXXXXX", "........", "........", "XXXXXXXX", "XXXXXXXX", "........", "........" };
                case HatchStyle.LightVertical:
                    return new[] { "X...X...", "X...X...", "X...X...", "X...X...", "X...X...", "X...X...", "X...X...", "X...X..." };
                case HatchStyle.LightHorizontal:
                    return new[] { "XXXXXXXX", "........", "........", "........", "XXXXXXXX", "........", "........", "........" };
                case HatchStyle.DashedDownwardDiagonal:
                    return new[] { "........", "........", "X...X...", ".X...X..", "..X...X.", "...X...X", "........", "........" };
                case HatchStyle.DashedUpwardDiagonal:
                    return new[] { "........", "........", "...X...X", "..X...X.", ".X...X..", "X...X...", "........", "........" };
                case HatchStyle.DashedHorizontal:
                    return new[] { "XXXX....", "........", "........", "........", "....XXXX", "........", "........", "........" };
                case HatchStyle.DashedVertical:
                    return new[] { "X.......", "X.......", "X.......", "X.......", "....X...", "....X...", "....X...", "....X..." };
                case HatchStyle.SmallConfetti:
                    return new[] { "X.......", "....X...", ".X......", "......X.", "...X....", ".......X", "..X.....", ".....X.." };
                case HatchStyle.LargeConfetti:
                    return new[] { "X.XX...X", "..XX....", "......XX", "...XX.XX", "XX.XX...", "XX......", "....XX..", "X...XX.X" };
                case HatchStyle.ZigZag:
                    return new[] { "X......X", ".X....X.", "..X..X..", "...XX...", "X......X", ".X....X.", "..X..X..", "...XX..." };
                case HatchStyle.Wave:
                    return new[] { "........", "...XX...", "..X..X.X", "XX......", "........", "...XX...", "..X..X.X", "XX......" };
                case HatchStyle.DiagonalBrick:
                    return new[] { ".......X", "......X.", ".....X..", "....X...", "...XX...", "..X..X..", ".X....X.", "X......X" };
                case HatchStyle.HorizontalBrick:
                    return new[] { "XXXXXXXX", "X.......", "X.......", "X.......", "XXXXXXXX", "....X...", "....X...", "....X..." };
                case HatchStyle.Weave:
                    return new[] { "X...X...", ".X.X.X..", "..X...X.", ".X...X.X", "X...X...", "...X.X..", "..X...X.", ".X.X.X.." };
                case HatchStyle.Plaid:
                    return new[] { "X.X.X.X.", ".X.X.X.X", "X.X.X.X.", ".X.X.X.X", "XXXX....", "XXXX....", "XXXX....", "XXXX...." };
                case HatchStyle.Divot:
                    return new[] { "........", "...X....", "....X...", "...X....", "........", "X.......", ".......X", "X......." };
                case HatchStyle.DottedGrid:
                    return new[] { "X.X.X.X.", "........", "X.......", "........", "X.......", "........", "X.......", "........" };
                case HatchStyle.DottedDiamond:
                    return new[] { "X.......", "........", "..X...X.", "........", "....X...", "........", "..X...X.", "........" };
                case HatchStyle.Shingle:
                    return new[] { "......XX", "X....X..", ".X..X...", "..XX....", "....XX..", "......X.", ".......X", ".......X" };
                case HatchStyle.Trellis:
                    return new[] { "XXXXXXXX", ".XX..XX.", "XXXXXXXX", "X..XX..X", "XXXXXXXX", ".XX..XX.", "XXXXXXXX", "X..XX..X" };
                case HatchStyle.Sphere:
                    return new[] { ".XXX.XXX", "X...X..X", "X...XXXX", "X...XXXX", ".XXX.XXX", "X..XX...", "XXXXX...", "XXXXX..." };
                case HatchStyle.SmallGrid:
                    return new[] { "XXXXXXXX", "X...X...", "X...X...", "X...X...", "XXXXXXXX", "X...X...", "X...X...", "X...X..." };
                case HatchStyle.SmallCheckerBoard:
                    return new[] { "X..XX..X", ".XX..XX.", ".XX..XX.", "X..XX..X", "X..XX..X", ".XX..XX.", ".XX..XX.", "X..XX..X" };
                case HatchStyle.LargeCheckerBoard:
                    return new[] { "XXXX....", "XXXX....", "XXXX....", "XXXX....", "....XXXX", "....XXXX", "....XXXX", "....XXXX" };
                case HatchStyle.OutlinedDiamond:
                    return new[] { "X.....X.", ".X...X..", "..X.X...", "...X....", "..X.X...", ".X...X..", "X.....X.", ".......X" };
                case HatchStyle.SolidDiamond:
                    return new[] { "...X....", "..XXX...", ".XXXXX..", "XXXXXXX.", ".XXXXX..", "..XXX...", "...X....", "........" };
                default:
                    return new[] { "........", "........", "........", "........", "........", "........", "........", "........" };
            }
        }
    }
}
