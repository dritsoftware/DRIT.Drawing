using DRIT.Drawing.Text.Bidi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests.Text
{
    [TestClass]
    public class BidiAlgorithmTests
    {
        [TestMethod]
        public void Bidi_PureLTR_LevelsAllZero()
        {
            int baseLevel = BidiAlgorithm.GetLevels("hello world", BidiParagraphDirection.Auto, out int[] levels);
            Assert.AreEqual(0, baseLevel);
            foreach (var l in levels)
                Assert.AreEqual(0, l);
        }

        [TestMethod]
        public void Bidi_PureRTL_BaseLevel1()
        {
            int baseLevel = BidiAlgorithm.GetLevels("\u05E9\u05DC\u05D5\u05DD \u05E2\u05D5\u05DC\u05DD", BidiParagraphDirection.Auto, out int[] levels);
            Assert.AreEqual(1, baseLevel);
            foreach (var l in levels)
                Assert.IsTrue((l & 1) == 1, $"Level {l} should be odd (RTL)");
        }

        [TestMethod]
        public void Bidi_EmptyString()
        {
            int baseLevel = BidiAlgorithm.GetLevels("", BidiParagraphDirection.Auto, out int[] levels);
            Assert.AreEqual(0, baseLevel);
            Assert.AreEqual(0, levels.Length);
        }

        [TestMethod]
        public void Bidi_RTLParagraph_ForcedRTL()
        {
            int baseLevel = BidiAlgorithm.GetLevels("hello", BidiParagraphDirection.RTL, out int[] levels);
            Assert.AreEqual(1, baseLevel);
        }

        [TestMethod]
        public void Bidi_LTRParagraph_ForcedLTR()
        {
            int baseLevel = BidiAlgorithm.GetLevels("\u05E9\u05DC\u05D5\u05DD", BidiParagraphDirection.LTR, out int[] levels);
            Assert.AreEqual(0, baseLevel);
        }

        [TestMethod]
        public void Bidi_Mixed_LatinInRTL()
        {
            string text = "\u05D0\u05D1\u05D2 abc \u05D3\u05D4";
            int baseLevel = BidiAlgorithm.GetLevels(text, BidiParagraphDirection.Auto, out int[] levels);

            Assert.AreEqual(1, baseLevel);

            int abcStart = text.IndexOf("abc");
            for (int i = abcStart; i < abcStart + 3; i++)
            {
                Assert.IsTrue(levels[i] >= 2, $"Latin char at {i} should have level >= 2, got {levels[i]}");
                Assert.IsTrue((levels[i] & 1) == 0, $"Latin char at {i} should be even (LTR), got {levels[i]}");
            }
        }

        [TestMethod]
        public void Bidi_NumberInRTL_ResolvedEN()
        {
            string text = "\u05DE\u05D7\u05D9\u05E8 123 \u05E9\"\u05D7";
            int baseLevel = BidiAlgorithm.GetLevels(text, BidiParagraphDirection.Auto, out int[] levels);
            Assert.AreEqual(1, baseLevel);

            int numStart = text.IndexOf("123");
            for (int i = numStart; i < numStart + 3; i++)
            {
                Assert.IsTrue((levels[i] & 1) == 0 || levels[i] >= 2,
                    $"Number char at {i} should be resolved, got level {levels[i]}");
            }
        }

        [TestMethod]
        public void Bidi_TrailingWhitespace_L1()
        {
            string text = "abc   ";
            int baseLevel = BidiAlgorithm.GetLevels(text, BidiParagraphDirection.Auto, out int[] levels);
            Assert.AreEqual(0, baseLevel);
            for (int i = 3; i < text.Length; i++)
            {
                Assert.AreEqual(0, levels[i]);
            }
        }

        [TestMethod]
        public void Bidi_SurrogatePair_LevelShared()
        {
            string text = "A\U0001F600B";
            int baseLevel = BidiAlgorithm.GetLevels(text, BidiParagraphDirection.Auto, out int[] levels);
            Assert.AreEqual(0, baseLevel);
            Assert.AreEqual(text.Length, levels.Length);
            Assert.AreEqual(levels[1], levels[2]);
        }

        [TestMethod]
        public void Bidi_ExplicitEmbedding_LRE_RLE()
        {
            string text = "\u202Babc\u202C";
            int baseLevel = BidiAlgorithm.GetLevels(text, BidiParagraphDirection.LTR, out int[] levels);
            Assert.AreEqual(text.Length, levels.Length);
        }

        [TestMethod]
        public void Bidi_Mirror_ParenInRTL()
        {
            Assert.IsTrue(BidiMirroring.TryGetMirror('(', out char mirrored));
            Assert.AreEqual(')', mirrored);

            Assert.IsTrue(BidiMirroring.TryGetMirror(')', out mirrored));
            Assert.AreEqual('(', mirrored);

            Assert.IsTrue(BidiMirroring.TryGetMirror('[', out mirrored));
            Assert.AreEqual(']', mirrored);

            Assert.IsFalse(BidiMirroring.TryGetMirror('a', out _));
        }

        [TestMethod]
        public void Bidi_BracketPair_BidiBracketsTable()
        {
            Assert.AreEqual(BidiBracketType.Open, BidiBrackets.GetBracketType('('));
            Assert.AreEqual(BidiBracketType.Close, BidiBrackets.GetBracketType(')'));
            Assert.AreEqual(BidiBracketType.None, BidiBrackets.GetBracketType('a'));
            Assert.AreEqual(')', BidiBrackets.GetPairedBracket('('));
            Assert.AreEqual('(', BidiBrackets.GetPairedBracket(')'));
        }

        [TestMethod]
        public void Bidi_CharacterTable_DefaultIsL()
        {
            Assert.AreEqual(BidiCharacterType.L, BidiCharacterTable.GetType(0x0041));
            Assert.AreEqual(BidiCharacterType.L, BidiCharacterTable.GetType(0x0061));
        }

        [TestMethod]
        public void Bidi_CharacterTable_HebrewIsR()
        {
            Assert.AreEqual(BidiCharacterType.R, BidiCharacterTable.GetType(0x05D0));
            Assert.AreEqual(BidiCharacterType.R, BidiCharacterTable.GetType(0x05EA));
        }

        [TestMethod]
        public void Bidi_CharacterTable_ArabicIsAL()
        {
            Assert.AreEqual(BidiCharacterType.AL, BidiCharacterTable.GetType(0x0627));
            Assert.AreEqual(BidiCharacterType.AL, BidiCharacterTable.GetType(0x0640));
        }

        [TestMethod]
        public void Bidi_CharacterTable_DigitsAreEN()
        {
            Assert.AreEqual(BidiCharacterType.EN, BidiCharacterTable.GetType(0x0030));
            Assert.AreEqual(BidiCharacterType.EN, BidiCharacterTable.GetType(0x0039));
        }

        [TestMethod]
        public void Bidi_CharacterTable_ArabicDigitsAreAN()
        {
            Assert.AreEqual(BidiCharacterType.AN, BidiCharacterTable.GetType(0x0660));
            Assert.AreEqual(BidiCharacterType.AN, BidiCharacterTable.GetType(0x0669));
        }

        [TestMethod]
        public void Bidi_CharacterTable_SpaceIsWS()
        {
            Assert.AreEqual(BidiCharacterType.WS, BidiCharacterTable.GetType(0x0020));
        }
    }
}