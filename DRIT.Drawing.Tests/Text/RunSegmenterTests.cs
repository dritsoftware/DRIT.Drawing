using DRIT.Drawing.Enums;
using DRIT.Drawing.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests.Text
{
    [TestClass]
    public class RunSegmenterTests
    {
        private static DRIT.Drawing.Text.Font DefaultFont => new DRIT.Drawing.Text.Font(DRIT.Drawing.Text.FontFamily.GenericSansSerif, 12f);

        [TestMethod]
        public void Segment_NoRich_OneRun()
        {
            var sf = new StringFormat();
            var runs = RunSegmenter.Segment("Hello", DefaultFont, null, sf, null);
            Assert.AreEqual(1, runs.Count);
            Assert.AreEqual("Hello", runs[0].Text);
        }

        [TestMethod]
        public void Segment_RichTwoRuns_TwoSegments()
        {
            var sf = new StringFormat();
            var rich = new RichTextOptions(DRIT.Drawing.Text.FontFamily.GenericSansSerif, 12f);
            rich.Runs.Add(new RichTextRun(0, 5));
            rich.Runs.Add(new RichTextRun(5, 5) { FontStyle = FontStyle.Bold });

            var runs = RunSegmenter.Segment("HelloWorld", DefaultFont, rich, sf, null);
            Assert.IsTrue(runs.Count >= 2);
        }

        [TestMethod]
        public void Segment_RichGap_DefaultRun()
        {
            var sf = new StringFormat();
            var rich = new RichTextOptions(DRIT.Drawing.Text.FontFamily.GenericSansSerif, 12f);
            rich.Runs.Add(new RichTextRun(0, 3));
            rich.Runs.Add(new RichTextRun(5, 3));

            var runs = RunSegmenter.Segment("ABCDEFGH", DefaultFont, rich, sf, null);
            Assert.IsTrue(runs.Count >= 3);
        }

        [TestMethod]
        public void Segment_BidiSplitsRun()
        {
            var sf = new StringFormat();
            string text = "\u05D0\u05D1\u05D2 abc";
            var runs = RunSegmenter.Segment(text, DefaultFont, null, sf, null);
            Assert.IsTrue(runs.Count >= 2);
        }

        [TestMethod]
        public void Segment_NoFontFallbackFlag_NoSplit()
        {
            var sf = new StringFormat(StringFormatFlags.NoFontFallback);
            var runs = RunSegmenter.Segment("Hello", DefaultFont, null, sf, null);
            Assert.AreEqual(1, runs.Count);
        }

        [TestMethod]
        public void Segment_BidiLevel_PreservedInResolvedRun()
        {
            var sf = new StringFormat(StringFormatFlags.DirectionRightToLeft);
            string text = "\u05D0\u05D1\u05D2";
            var runs = RunSegmenter.Segment(text, DefaultFont, null, sf, null);
            foreach (var r in runs)
                Assert.IsTrue((r.BidiLevel & 1) == 1, "RTL runs should have odd bidi level");
        }

        [TestMethod]
        public void Segment_EmptyString()
        {
            var sf = new StringFormat();
            var runs = RunSegmenter.Segment("", DefaultFont, null, sf, null);
            Assert.AreEqual(0, runs.Count);
        }

        [TestMethod]
        public void Segment_CodePointAt_ASCII()
        {
            Assert.AreEqual('A', RunSegmenter.CodePointAt("ABC", 0));
            Assert.AreEqual('B', RunSegmenter.CodePointAt("ABC", 1));
        }

        [TestMethod]
        public void Segment_CodePointAt_SurrogatePair()
        {
            string text = "A\U0001F600";
            Assert.AreEqual('A', RunSegmenter.CodePointAt(text, 0));
            Assert.AreEqual(0x1F600u, RunSegmenter.CodePointAt(text, 1));
        }

        [TestMethod]
        public void Segment_GetBaseLevel_LTR()
        {
            var sf = new StringFormat();
            Assert.AreEqual(0, RunSegmenter.GetBaseLevel("Hello", sf));
        }

        [TestMethod]
        public void Segment_GetBaseLevel_RTL()
        {
            var sf = new StringFormat();
            Assert.AreEqual(1, RunSegmenter.GetBaseLevel("\u05D0\u05D1\u05D2", sf));
        }

        [TestMethod]
        public void Segment_GetBaseLevel_ForcedRTL()
        {
            var sf = new StringFormat(StringFormatFlags.DirectionRightToLeft);
            Assert.AreEqual(1, RunSegmenter.GetBaseLevel("Hello", sf));
        }
    }
}