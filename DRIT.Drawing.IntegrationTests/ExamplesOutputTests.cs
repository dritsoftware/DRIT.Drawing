using DRIT.Drawing.Examples;

namespace DRIT.Drawing.IntegrationTests
{
    /// <summary>
    /// Runs every documented example and checks its output. The documentation shows these images, so an
    /// example that silently draws nothing must fail here rather than in a published guide.
    /// </summary>
    [TestClass]
    public class ExamplesOutputTests
    {
        [TestMethod]
        public void AllExamples_ProduceNonBlankOutput()
        {
            string directory = Path.Combine(Path.GetTempPath(), "drit-examples-" + Guid.NewGuid().ToString("N"));
            try
            {
                int exitCode = Program.Main(new[] { "all", "--output", directory, "--verify" });
                var problems = OutputVerifier.Verify(directory);
                Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
                Assert.AreEqual(0, exitCode);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void Verifier_RejectsBlankAndFlatImages()
        {
            string directory = Path.Combine(Path.GetTempPath(), "drit-verifier-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                using (var blank = new DRIT.Drawing.Imaging.Bitmap(64, 64))
                using (var g = DRIT.Drawing.Graphics.FromImage(blank))
                {
                    g.Clear(DRIT.Drawing.Colors.Color.White);
                    blank.Save(Path.Combine(directory, "blank.png"), DRIT.Drawing.Imaging.ImageFormat.Png);
                }

                var problems = OutputVerifier.Verify(directory);
                Assert.AreEqual(1, problems.Count);
                StringAssert.Contains(problems[0], "blank.png");
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
