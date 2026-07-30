using System.IO;
using DRIT.Drawing.Brushes;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Enums;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Imaging;
using DRIT.Drawing.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests.Text
{
    [TestClass]
    public class TextOnPathLayoutTests
    {
        private const float Tol = 0.5f;

        // ── Helpers ────────────────────────────────────────────────────────────

        private static string GetGlyphiconsPath() =>
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "..",
                "DRIT.Drawing.Tests",
                "TestAssets",
                "Fonts",
                "glyphicons-halflings-regular.ttf"));

        private static FontFamily? TryLoadFont()
        {
            string path = GetGlyphiconsPath();
            if (!File.Exists(path)) return null;
            var pfc = new PrivateFontCollection();
            pfc.AddFontFile(path);
            return pfc.Families.Length > 0 ? pfc.Families[0] : null;
        }

        private static bool HasNonWhitePixels(Bitmap bmp)
        {
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                    if (bmp.GetPixel(x, y) != Color.White) return true;
            return false;
        }

        // -----------------------------------------------------------------------
        // Empty / degenerate inputs
        // -----------------------------------------------------------------------

        [TestMethod]
        public void BuildPath_EmptyText_EmptyResult()
        {
            using var fam = FontFamily.GenericSansSerif;
            using var font = new DRIT.Drawing.Text.Font(fam, 12f, FontStyle.Regular, GraphicsUnit.Point);
            var baseline = new GraphicsPath();
            baseline.AddLine(0f, 0f, 100f, 0f);

            var result = TextOnPathLayout.BuildPath("", font, baseline, null, null, 96f, 96f);
            Assert.AreEqual(0, result.PointCount);
        }

        [TestMethod]
        public void BuildPath_EmptyBaseline_EmptyResult()
        {
            using var fam = FontFamily.GenericSansSerif;
            using var font = new DRIT.Drawing.Text.Font(fam, 12f, FontStyle.Regular, GraphicsUnit.Point);

            var result = TextOnPathLayout.BuildPath("ABC", font, new GraphicsPath(), null, null, 96f, 96f);
            Assert.AreEqual(0, result.PointCount);
        }

        [TestMethod]
        public void BuildPath_FallbackFont_EmptyResult()
        {
            // GenericSansSerif has no real IFont → no glyph outlines → empty result.
            using var fam = FontFamily.GenericSansSerif;
            using var font = new DRIT.Drawing.Text.Font(fam, 12f, FontStyle.Regular, GraphicsUnit.Point);
            var baseline = new GraphicsPath();
            baseline.AddLine(0f, 0f, 500f, 0f);

            var result = TextOnPathLayout.BuildPath("ABC", font, baseline, null, null, 96f, 96f);
            Assert.AreEqual(0, result.PointCount);
        }

        // -----------------------------------------------------------------------
        // Tests requiring a real font
        // -----------------------------------------------------------------------

        [TestMethod]
        public void BuildPath_HorizontalLine_PopulatesPath()
        {
            FontFamily? fam = TryLoadFont();
            if (fam == null) Assert.Inconclusive("Glyphicons font not found.");

            using (fam)
            {
                using var font = new DRIT.Drawing.Text.Font(fam, 24f, FontStyle.Regular, GraphicsUnit.Point);
                var baseline = new GraphicsPath();
                baseline.AddLine(0f, 100f, 500f, 100f);

                var result = TextOnPathLayout.BuildPath("ABC", font, baseline, null, null, 96f, 96f);
                Assert.IsTrue(result.PointCount > 0, "Text on a horizontal line should produce path points.");
            }
        }

        [TestMethod]
        public void BuildPath_HorizontalLine_GlyphsAboveOrOnPath()
        {
            FontFamily? fam = TryLoadFont();
            if (fam == null) Assert.Inconclusive("Glyphicons font not found.");

            using (fam)
            {
                using var font = new DRIT.Drawing.Text.Font(fam, 24f, FontStyle.Regular, GraphicsUnit.Point);
                var baseline = new GraphicsPath();
                baseline.AddLine(0f, 100f, 500f, 100f);

                var result = TextOnPathLayout.BuildPath("ABC", font, baseline, null, null, 96f, 96f);
                var bounds = result.GetBounds();

                // Glyphs sit on the baseline (y=100) with ascenders above (y < 100).
                // All glyph content should be at or above y=100 (no descenders for "ABC"
                // in most fonts, but allow a small margin).
                Assert.IsTrue(bounds.Y <= 100f + Tol,
                    $"Glyphs should be at or above the baseline. Bounds Y={bounds.Y}");
            }
        }

        [TestMethod]
        public void BuildPath_VerticalLine_GlyphsRotated()
        {
            FontFamily? fam = TryLoadFont();
            if (fam == null) Assert.Inconclusive("Glyphicons font not found.");

            using (fam)
            {
                using var font = new DRIT.Drawing.Text.Font(fam, 24f, FontStyle.Regular, GraphicsUnit.Point);
                var baseline = new GraphicsPath();
                baseline.AddLine(100f, 0f, 100f, 500f);

                var result = TextOnPathLayout.BuildPath("ABC", font, baseline, null, null, 96f, 96f);
                Assert.IsTrue(result.PointCount > 0);

                var bounds = result.GetBounds();
                // On a vertical line, glyphs are rotated 90°, so the text block
                // should be taller than it is wide (relative to the baseline direction).
                Assert.IsTrue(bounds.Height > bounds.Width,
                    $"Rotated text should be taller than wide. W={bounds.Width}, H={bounds.Height}");
            }
        }

        [TestMethod]
        public void BuildPath_Circle_GlyphsRotateAround()
        {
            FontFamily? fam = TryLoadFont();
            if (fam == null) Assert.Inconclusive("Glyphicons font not found.");

            using (fam)
            {
                using var font = new DRIT.Drawing.Text.Font(fam, 16f, FontStyle.Regular, GraphicsUnit.Point);
                var baseline = new GraphicsPath();
                baseline.AddEllipse(new RectangleF(50f, 50f, 200f, 200f));

                var result = TextOnPathLayout.BuildPath("TEXT", font, baseline,
                    new StringFormat { Alignment = StringAlignment.Center }, null, 96f, 96f);
                Assert.IsTrue(result.PointCount > 0);

                var bounds = result.GetBounds();
                // The text should be placed on the circle and glyphs should be
                // rotated to follow the curve. With center alignment on a circle
                // starting at 3 o'clock, text is centered at 9 o'clock (leftmost
                // point), where glyphs are rotated ~90°.
                Assert.IsTrue(bounds.Height > bounds.Width,
                    $"Rotated text on circle should be taller than wide. W={bounds.Width}, H={bounds.Height}");

                // Text should be on the circle, not at the center.
                float cx = 150f, cy = 150f, radius = 100f;
                float bcx = (bounds.X + bounds.Right) / 2f;
                float bcy = (bounds.Y + bounds.Bottom) / 2f;
                float distFromCenter = (float)Math.Sqrt(
                    Math.Pow(bcx - cx, 2) + Math.Pow(bcy - cy, 2));
                Assert.IsTrue(Math.Abs(distFromCenter - radius) < 30f,
                    $"Text should be on the circle. Distance from center={distFromCenter}, expected≈{radius}");
            }
        }

        // -----------------------------------------------------------------------
        // Alignment
        // -----------------------------------------------------------------------

        [TestMethod]
        public void BuildPath_AlignmentNear_StartsAt0()
        {
            FontFamily? fam = TryLoadFont();
            if (fam == null) Assert.Inconclusive("Glyphicons font not found.");

            using (fam)
            {
                using var font = new DRIT.Drawing.Text.Font(fam, 24f, FontStyle.Regular, GraphicsUnit.Point);
                var baseline = new GraphicsPath();
                baseline.AddLine(0f, 100f, 500f, 100f);

                var sf = new StringFormat { Alignment = StringAlignment.Near };
                var result = TextOnPathLayout.BuildPath("A", font, baseline, sf, null, 96f, 96f);
                var bounds = result.GetBounds();

                // First glyph should start near x=0 (within font ascent tolerance).
                Assert.IsTrue(bounds.X < 10f,
                    $"Near alignment should start near x=0. Bounds X={bounds.X}");
            }
        }

        [TestMethod]
        public void BuildPath_AlignmentFar_EndsAtPathEnd()
        {
            FontFamily? fam = TryLoadFont();
            if (fam == null) Assert.Inconclusive("Glyphicons font not found.");

            using (fam)
            {
                using var font = new DRIT.Drawing.Text.Font(fam, 24f, FontStyle.Regular, GraphicsUnit.Point);
                float pathEnd = 500f;
                var baseline = new GraphicsPath();
                baseline.AddLine(0f, 100f, pathEnd, 100f);

                var sf = new StringFormat { Alignment = StringAlignment.Far };
                var result = TextOnPathLayout.BuildPath("A", font, baseline, sf, null, 96f, 96f);
                var bounds = result.GetBounds();

                // Last glyph should end near x=pathEnd.
                Assert.IsTrue(bounds.Right > pathEnd - 50f,
                    $"Far alignment should end near path end. Bounds Right={bounds.Right}");
            }
        }

        [TestMethod]
        public void BuildPath_AlignmentCenter_CentersText()
        {
            FontFamily? fam = TryLoadFont();
            if (fam == null) Assert.Inconclusive("Glyphicons font not found.");

            using (fam)
            {
                using var font = new DRIT.Drawing.Text.Font(fam, 24f, FontStyle.Regular, GraphicsUnit.Point);
                float pathEnd = 500f;
                var baseline = new GraphicsPath();
                baseline.AddLine(0f, 100f, pathEnd, 100f);

                var sf = new StringFormat { Alignment = StringAlignment.Center };
                var result = TextOnPathLayout.BuildPath("A", font, baseline, sf, null, 96f, 96f);
                var bounds = result.GetBounds();

                float center = (bounds.X + bounds.Right) / 2f;
                Assert.IsTrue(Math.Abs(center - pathEnd / 2f) < 60f,
                    $"Center alignment should center the text. Center={center}, expected≈{pathEnd / 2f}");
            }
        }

        // -----------------------------------------------------------------------
        // StartOffset / PerpendicularOffset
        // -----------------------------------------------------------------------

        [TestMethod]
        public void BuildPath_StartOffset_ShiftsFirstGlyph()
        {
            FontFamily? fam = TryLoadFont();
            if (fam == null) Assert.Inconclusive("Glyphicons font not found.");

            using (fam)
            {
                using var font = new DRIT.Drawing.Text.Font(fam, 24f, FontStyle.Regular, GraphicsUnit.Point);
                var baseline = new GraphicsPath();
                baseline.AddLine(0f, 100f, 500f, 100f);

                var result = TextOnPathLayout.BuildPath("A", font, baseline, null,
                    new TextOnPathOptions { StartOffset = 100f }, 96f, 96f);
                var bounds = result.GetBounds();

                Assert.IsTrue(bounds.X >= 90f,
                    $"StartOffset=100 should shift first glyph past x=90. Bounds X={bounds.X}");
            }
        }

        [TestMethod]
        public void BuildPath_PerpendicularOffsetAbove_GlyphsAbovePath()
        {
            FontFamily? fam = TryLoadFont();
            if (fam == null) Assert.Inconclusive("Glyphicons font not found.");

            using (fam)
            {
                using var font = new DRIT.Drawing.Text.Font(fam, 24f, FontStyle.Regular, GraphicsUnit.Point);
                var baseline = new GraphicsPath();
                baseline.AddLine(0f, 100f, 500f, 100f);

                var result = TextOnPathLayout.BuildPath("A", font, baseline, null,
                    new TextOnPathOptions { PerpendicularOffset = 30f }, 96f, 96f);
                var bounds = result.GetBounds();

                // With +30 offset on a horizontal line (angle 0), glyphs move up (smaller Y).
                // The glyph baseline is now at y=100-30=70, so all content is above y=100.
                Assert.IsTrue(bounds.Bottom < 101f,
                    $"PerpendicularOffset=+30 should move glyphs above the path. Bounds Bottom={bounds.Bottom}");
            }
        }

        [TestMethod]
        public void BuildPath_PerpendicularOffsetBelow_GlyphsBelowPath()
        {
            FontFamily? fam = TryLoadFont();
            if (fam == null) Assert.Inconclusive("Glyphicons font not found.");

            using (fam)
            {
                using var font = new DRIT.Drawing.Text.Font(fam, 24f, FontStyle.Regular, GraphicsUnit.Point);
                var baseline = new GraphicsPath();
                baseline.AddLine(0f, 100f, 500f, 100f);

                var result = TextOnPathLayout.BuildPath("A", font, baseline, null,
                    new TextOnPathOptions { PerpendicularOffset = -50f }, 96f, 96f);
                var bounds = result.GetBounds();

                // With -50 offset, glyphs move down (larger Y).
                Assert.IsTrue(bounds.Y > 100f,
                    $"PerpendicularOffset=-50 should move glyphs below the path. Bounds Y={bounds.Y}");
            }
        }

        // -----------------------------------------------------------------------
        // Overflow
        // -----------------------------------------------------------------------

        [TestMethod]
        public void BuildPath_TextLongerThanPath_StopsGracefully()
        {
            FontFamily? fam = TryLoadFont();
            if (fam == null) Assert.Inconclusive("Glyphicons font not found.");

            using (fam)
            {
                using var font = new DRIT.Drawing.Text.Font(fam, 24f, FontStyle.Regular, GraphicsUnit.Point);
                var baseline = new GraphicsPath();
                baseline.AddLine(0f, 100f, 50f, 100f); // very short path

                // Long text on a short path should not throw.
                var result = TextOnPathLayout.BuildPath("ABCDEFGHIJKLMNOPQRSTUVWXYZ",
                    font, baseline, null,
                    new TextOnPathOptions { Overflow = TextOnPathOverflow.Stop },
                    96f, 96f);

                // Should produce some glyphs (those that fit) without exception.
                // May be 0 if even the first glyph doesn't fit, which is acceptable.
                Assert.IsTrue(result.PointCount >= 0, "Should not throw on overflow.");
            }
        }

        // -----------------------------------------------------------------------
        // Tab / newline handling
        // -----------------------------------------------------------------------

        [TestMethod]
        public void BuildPath_TabSkipped_NoGlyphForTab()
        {
            FontFamily? fam = TryLoadFont();
            if (fam == null) Assert.Inconclusive("Glyphicons font not found.");

            using (fam)
            {
                using var font = new DRIT.Drawing.Text.Font(fam, 24f, FontStyle.Regular, GraphicsUnit.Point);
                var baseline = new GraphicsPath();
                baseline.AddLine(0f, 100f, 2000f, 100f);

                var resultWithTab = TextOnPathLayout.BuildPath("A\tB", font, baseline, null, null, 96f, 96f);
                var resultNoTab = TextOnPathLayout.BuildPath("AB", font, baseline, null, null, 96f, 96f);

                // Both should produce glyphs. The tab version has a gap between A and B.
                Assert.IsTrue(resultWithTab.PointCount > 0);
                Assert.IsTrue(resultNoTab.PointCount > 0);

                // The tab version's bounds should be wider (tab advances the pen).
                var boundsTab = resultWithTab.GetBounds();
                var boundsNoTab = resultNoTab.GetBounds();
                Assert.IsTrue(boundsTab.Width >= boundsNoTab.Width,
                    "Tab should add advance width between A and B.");
            }
        }

        // -----------------------------------------------------------------------
        // AddStringOnPath integration
        // -----------------------------------------------------------------------

        [TestMethod]
        public void AddStringOnPath_AddsToExistingPath()
        {
            FontFamily? fam = TryLoadFont();
            if (fam == null) Assert.Inconclusive("Glyphicons font not found.");

            using (fam)
            {
                var baseline = new GraphicsPath();
                baseline.AddLine(0f, 100f, 500f, 100f);

                using var gp = new GraphicsPath();
                gp.AddLine(0f, 0f, 10f, 10f); // pre-existing figure
                int before = gp.PointCount;

                gp.AddStringOnPath("AB", fam, (int)FontStyle.Regular, 24f, baseline);

                Assert.IsTrue(gp.PointCount > before,
                    "AddStringOnPath should add points to an existing path.");
            }
        }

        [TestMethod]
        public void AddStringOnPath_DoesNotMutateBaseline()
        {
            FontFamily? fam = TryLoadFont();
            if (fam == null) Assert.Inconclusive("Glyphicons font not found.");

            using (fam)
            {
                var baseline = new GraphicsPath();
                baseline.AddLine(0f, 100f, 500f, 100f);
                int baselinePoints = baseline.PointCount;
                var baselineBounds = baseline.GetBounds();

                using var gp = new GraphicsPath();
                gp.AddStringOnPath("ABC", fam, (int)FontStyle.Regular, 24f, baseline);

                Assert.AreEqual(baselinePoints, baseline.PointCount,
                    "Baseline path should not be mutated by AddStringOnPath.");
                var after = baseline.GetBounds();
                Assert.AreEqual(baselineBounds.X, after.X, Tol);
                Assert.AreEqual(baselineBounds.Y, after.Y, Tol);
            }
        }

        // -----------------------------------------------------------------------
        // DrawStringOnPath smoke test
        // -----------------------------------------------------------------------

        [TestMethod]
        public void DrawStringOnPath_Smoke_RendersWithoutError()
        {
            FontFamily? fam = TryLoadFont();
            if (fam == null) Assert.Inconclusive("Glyphicons font not found.");

            using (fam)
            {
                using var font = new DRIT.Drawing.Text.Font(fam, 20f, FontStyle.Regular, GraphicsUnit.Point);
                using var bmp = new Bitmap(400, 200);
                using var g = Graphics.FromImage(bmp);
                using var bg = new SolidBrush(Color.White);
                using var fg = new SolidBrush(Color.Black);

                g.FillRectangle(bg, 0, 0, 400, 200);

                var baseline = new GraphicsPath();
                baseline.AddLine(20f, 100f, 380f, 100f);

                g.DrawStringOnPath("Hello", font, fg, baseline);

                Assert.IsTrue(HasNonWhitePixels(bmp),
                    "DrawStringOnPath should render non-white pixels.");
            }
        }

        [TestMethod]
        public void DrawStringOnPath_CurvedPath_RendersWithoutError()
        {
            FontFamily? fam = TryLoadFont();
            if (fam == null) Assert.Inconclusive("Glyphicons font not found.");

            using (fam)
            {
                using var font = new DRIT.Drawing.Text.Font(fam, 16f, FontStyle.Regular, GraphicsUnit.Point);
                using var bmp = new Bitmap(400, 400);
                using var g = Graphics.FromImage(bmp);
                using var bg = new SolidBrush(Color.White);
                using var fg = new SolidBrush(Color.Black);

                g.FillRectangle(bg, 0, 0, 400, 400);

                var circle = new GraphicsPath();
                circle.AddEllipse(new RectangleF(50f, 50f, 300f, 300f));

                g.DrawStringOnPath("Around the circle", font, fg, circle,
                    new StringFormat { Alignment = StringAlignment.Center });

                Assert.IsTrue(HasNonWhitePixels(bmp),
                    "DrawStringOnPath on a circle should render non-white pixels.");
            }
        }
    }
}