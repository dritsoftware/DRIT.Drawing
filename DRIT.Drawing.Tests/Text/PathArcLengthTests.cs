using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DRIT.Drawing.Tests.Text
{
    [TestClass]
    public class PathArcLengthTests
    {
        private const float Tol = 1e-3f;

        // -----------------------------------------------------------------------
        // Empty / degenerate paths
        // -----------------------------------------------------------------------

        [TestMethod]
        public void PathArcLength_EmptyPath_TotalLengthZero()
        {
            var arc = PathArcLength.Build(new GraphicsPath());
            Assert.AreEqual(0f, arc.TotalLength);
            Assert.IsFalse(arc.GetPointAt(0f, out _, out _));
        }

        [TestMethod]
        public void PathArcLength_SinglePoint_TotalLengthZero()
        {
            var p = new GraphicsPath();
            p.AddLine(10f, 10f, 10f, 10f); // zero-length segment
            var arc = PathArcLength.Build(p);
            Assert.AreEqual(0f, arc.TotalLength, Tol);
        }

        // -----------------------------------------------------------------------
        // Horizontal line
        // -----------------------------------------------------------------------

        [TestMethod]
        public void PathArcLength_HorizontalLine_TotalLength()
        {
            var p = new GraphicsPath();
            p.AddLine(0f, 0f, 100f, 0f);
            var arc = PathArcLength.Build(p);
            Assert.AreEqual(100f, arc.TotalLength, Tol);
        }

        [TestMethod]
        public void PathArcLength_HorizontalLine_PointAt0()
        {
            var p = new GraphicsPath();
            p.AddLine(0f, 0f, 100f, 0f);
            var arc = PathArcLength.Build(p);
            Assert.IsTrue(arc.GetPointAt(0f, out PointF pt, out float angle));
            Assert.AreEqual(0f, pt.X, Tol);
            Assert.AreEqual(0f, pt.Y, Tol);
            Assert.AreEqual(0f, angle, Tol);
        }

        [TestMethod]
        public void PathArcLength_HorizontalLine_PointAt50()
        {
            var p = new GraphicsPath();
            p.AddLine(0f, 0f, 100f, 0f);
            var arc = PathArcLength.Build(p);
            Assert.IsTrue(arc.GetPointAt(50f, out PointF pt, out float angle));
            Assert.AreEqual(50f, pt.X, Tol);
            Assert.AreEqual(0f, pt.Y, Tol);
            Assert.AreEqual(0f, angle, Tol);
        }

        [TestMethod]
        public void PathArcLength_HorizontalLine_PointAt100()
        {
            var p = new GraphicsPath();
            p.AddLine(0f, 0f, 100f, 0f);
            var arc = PathArcLength.Build(p);
            Assert.IsTrue(arc.GetPointAt(100f, out PointF pt, out _));
            Assert.AreEqual(100f, pt.X, Tol);
            Assert.AreEqual(0f, pt.Y, Tol);
        }

        // -----------------------------------------------------------------------
        // Vertical / diagonal lines
        // -----------------------------------------------------------------------

        [TestMethod]
        public void PathArcLength_VerticalLine_Angle90()
        {
            var p = new GraphicsPath();
            p.AddLine(0f, 0f, 0f, 100f);
            var arc = PathArcLength.Build(p);
            Assert.IsTrue(arc.GetPointAt(50f, out PointF pt, out float angle));
            Assert.AreEqual(0f, pt.X, Tol);
            Assert.AreEqual(50f, pt.Y, Tol);
            Assert.AreEqual(90f, angle, Tol);
        }

        [TestMethod]
        public void PathArcLength_DiagonalLine_LengthAndAngle()
        {
            var p = new GraphicsPath();
            p.AddLine(0f, 0f, 30f, 40f);
            var arc = PathArcLength.Build(p);
            Assert.AreEqual(50f, arc.TotalLength, Tol);
            Assert.IsTrue(arc.GetPointAt(25f, out PointF pt, out float angle));
            Assert.AreEqual(15f, pt.X, Tol);
            Assert.AreEqual(20f, pt.Y, Tol);
            Assert.AreEqual(53.130f, angle, 0.01f);
        }

        // -----------------------------------------------------------------------
        // Closed shapes
        // -----------------------------------------------------------------------

        [TestMethod]
        public void PathArcLength_ClosedRectangle_Perimeter()
        {
            var p = new GraphicsPath();
            p.AddRectangle(new RectangleF(0f, 0f, 100f, 50f));
            var arc = PathArcLength.Build(p);
            // Perimeter = 2*(100+50) = 300
            Assert.AreEqual(300f, arc.TotalLength, Tol);
        }

        [TestMethod]
        public void PathArcLength_ClosedRectangle_WrapsAround()
        {
            var p = new GraphicsPath();
            p.AddRectangle(new RectangleF(0f, 0f, 100f, 50f));
            var arc = PathArcLength.Build(p);

            // The rectangle starts at top-left (0,0). AddRectangle adds 4 lines:
            // top edge (0,0)->(100,0), right edge (100,0)->(100,50),
            // bottom edge (100,50)->(0,50), left edge (0,50)->(0,0).
            // At distance 150 (top=100 + right=50) we are at corner (100,50).
            Assert.IsTrue(arc.GetPointAt(150f, out PointF pt, out _));
            Assert.AreEqual(100f, pt.X, Tol);
            Assert.AreEqual(50f, pt.Y, Tol);
        }

        [TestMethod]
        public void PathArcLength_ClosedCircle_TotalLengthApprox()
        {
            var p = new GraphicsPath();
            p.AddEllipse(new RectangleF(0f, 0f, 100f, 100f));
            var arc = PathArcLength.Build(p);
            // Circumference ≈ π * 100 ≈ 314.16
            Assert.AreEqual(314.16f, arc.TotalLength, 2f);
        }

        [TestMethod]
        public void PathArcLength_ClosedPath_WrapsModuloLength()
        {
            var p = new GraphicsPath();
            p.AddRectangle(new RectangleF(0f, 0f, 100f, 50f));
            var arc = PathArcLength.Build(p);
            float len = arc.TotalLength; // 300

            // Distance == length wraps to 0 (start point) for a closed subpath.
            Assert.IsTrue(arc.GetPointAt(len, out PointF pt, out _));
            Assert.AreEqual(0f, pt.X, Tol);
            Assert.AreEqual(0f, pt.Y, Tol);
        }

        // -----------------------------------------------------------------------
        // Multiple subpaths
        // -----------------------------------------------------------------------

        [TestMethod]
        public void PathArcLength_TwoSubpaths_Concatenated()
        {
            var p = new GraphicsPath();
            p.AddLine(0f, 0f, 50f, 0f);   // subpath 0, length 50
            p.StartFigure();
            p.AddLine(100f, 0f, 150f, 0f); // subpath 1, length 50
            var arc = PathArcLength.Build(p);

            Assert.AreEqual(2, arc.SubpathCount);
            Assert.AreEqual(100f, arc.TotalLength, Tol);

            // Distance 75 is in subpath 1 (offset 50), local 25 → (125, 0).
            Assert.IsTrue(arc.GetPointAt(75f, out PointF pt, out _));
            Assert.AreEqual(125f, pt.X, Tol);
            Assert.AreEqual(0f, pt.Y, Tol);
        }

        // -----------------------------------------------------------------------
        // Bézier (flattened)
        // -----------------------------------------------------------------------

        [TestMethod]
        public void PathArcLength_Bezier_Flattened()
        {
            var p = new GraphicsPath();
            p.AddBezier(0f, 0f, 0f, 100f, 100f, 100f, 100f, 0f);
            var arc = PathArcLength.Build(p);

            // Chord distance from (0,0) to (100,0) is 100; the curve is longer.
            Assert.IsTrue(arc.TotalLength > 100f);

            // Start point.
            Assert.IsTrue(arc.GetPointAt(0f, out PointF start, out _));
            Assert.AreEqual(0f, start.X, Tol);
            Assert.AreEqual(0f, start.Y, Tol);

            // End point.
            Assert.IsTrue(arc.GetPointAt(arc.TotalLength, out PointF end, out _));
            Assert.AreEqual(100f, end.X, Tol);
            Assert.AreEqual(0f, end.Y, Tol);
        }

        // -----------------------------------------------------------------------
        // Clamping
        // -----------------------------------------------------------------------

        [TestMethod]
        public void PathArcLength_ClampBeyondEnd()
        {
            var p = new GraphicsPath();
            p.AddLine(0f, 0f, 100f, 0f);
            var arc = PathArcLength.Build(p);
            Assert.IsTrue(arc.GetPointAt(1000f, out PointF pt, out _));
            Assert.AreEqual(100f, pt.X, Tol);
            Assert.AreEqual(0f, pt.Y, Tol);
        }

        [TestMethod]
        public void PathArcLength_ClampBeforeZero()
        {
            var p = new GraphicsPath();
            p.AddLine(0f, 0f, 100f, 0f);
            var arc = PathArcLength.Build(p);
            Assert.IsTrue(arc.GetPointAt(-50f, out PointF pt, out _));
            Assert.AreEqual(0f, pt.X, Tol);
            Assert.AreEqual(0f, pt.Y, Tol);
        }
    }
}