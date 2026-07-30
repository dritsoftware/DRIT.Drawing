using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Geometry;
using DRIT.Drawing.Colors;
using DRIT.Drawing.Text;
using DRIT.Drawing.Pens;

namespace DRIT.Drawing.Tests
{
    [TestClass]
    public class GraphicsPathTests
    {
        private const float Tol = 1e-3f;

        // -----------------------------------------------------------------------
        // PathPointType enum
        // -----------------------------------------------------------------------

        [TestMethod]
        public void PathPointType_Values()
        {
            Assert.AreEqual(0,    (byte)PathPointType.Start);
            Assert.AreEqual(1,    (byte)PathPointType.Line);
            Assert.AreEqual(3,    (byte)PathPointType.Bezier);
            Assert.AreEqual(0x80, (byte)PathPointType.CloseSubpath);
            Assert.AreEqual(0x20, (byte)PathPointType.PathMarker);
        }

        // -----------------------------------------------------------------------
        // PathData
        // -----------------------------------------------------------------------

        [TestMethod]
        public void PathData_DefaultEmpty()
        {
            var pd = new PathData();
            Assert.AreEqual(0, pd.Points.Length);
            Assert.AreEqual(0, pd.Types.Length);
        }

        // -----------------------------------------------------------------------
        // Empty path
        // -----------------------------------------------------------------------

        [TestMethod]
        public void GraphicsPath_Empty_PointCount0()
        {
            var p = new GraphicsPath();
            Assert.AreEqual(0, p.PointCount);
        }

        [TestMethod]
        public void GraphicsPath_Empty_BoundsEmpty()
        {
            var p = new GraphicsPath();
            Assert.AreEqual(RectangleF.Empty, p.GetBounds());
        }

        // -----------------------------------------------------------------------
        // AddLine
        // -----------------------------------------------------------------------

        [TestMethod]
        public void GraphicsPath_AddLine_TwoPoints()
        {
            var p = new GraphicsPath();
            p.AddLine(new PointF(0, 0), new PointF(100, 0));
            Assert.AreEqual(2, p.PointCount);
            Assert.AreEqual((byte)PathPointType.Start, p.PathTypes[0]);
            Assert.AreEqual((byte)PathPointType.Line,  p.PathTypes[1]);
        }

        [TestMethod]
        public void GraphicsPath_AddLine_Continued_NoNewFigure()
        {
            var p = new GraphicsPath();
            p.AddLine(0, 0, 100, 0);
            p.AddLine(100, 0, 100, 100);
            // Second AddLine: pt1 gets Line, pt2 gets Line → 4 points total
            Assert.AreEqual(4, p.PointCount);
            Assert.AreEqual((byte)PathPointType.Start, p.PathTypes[0]);
            Assert.AreEqual((byte)PathPointType.Line,  p.PathTypes[1]);
            Assert.AreEqual((byte)PathPointType.Line,  p.PathTypes[2]);
            Assert.AreEqual((byte)PathPointType.Line,  p.PathTypes[3]);
        }

        [TestMethod]
        public void GraphicsPath_AddLines_FirstIsStart()
        {
            var p = new GraphicsPath();
            p.AddLines(new[] { new PointF(0,0), new PointF(10,0), new PointF(10,10) });
            Assert.AreEqual(3, p.PointCount);
            Assert.AreEqual((byte)PathPointType.Start, p.PathTypes[0]);
            Assert.AreEqual((byte)PathPointType.Line,  p.PathTypes[1]);
            Assert.AreEqual((byte)PathPointType.Line,  p.PathTypes[2]);
        }

        // -----------------------------------------------------------------------
        // StartFigure / CloseFigure
        // -----------------------------------------------------------------------

        [TestMethod]
        public void GraphicsPath_CloseFigure_SetsCloseSubpathFlag()
        {
            var p = new GraphicsPath();
            p.AddLine(0, 0, 100, 0);
            p.CloseFigure();
            Assert.AreNotEqual(0, p.PathTypes[p.PointCount - 1] & (byte)PathPointType.CloseSubpath);
        }

        [TestMethod]
        public void GraphicsPath_StartFigure_NewSubpath()
        {
            var p = new GraphicsPath();
            p.AddLine(0, 0, 100, 0);
            p.StartFigure();
            p.AddLine(200, 200, 300, 200);
            // After StartFigure, the new line's first point is Start
            Assert.AreEqual((byte)PathPointType.Start, p.PathTypes[2]);
        }

        [TestMethod]
        public void GraphicsPath_CloseAllFigures()
        {
            var p = new GraphicsPath();
            p.AddLine(0, 0, 100, 0);
            p.StartFigure();
            p.AddLine(200, 0, 300, 0);
            p.CloseAllFigures();
            // Both subpaths must end with CloseSubpath
            // Check last type is closed
            Assert.AreNotEqual(0, p.PathTypes[p.PointCount - 1] & (byte)PathPointType.CloseSubpath);
        }

        // -----------------------------------------------------------------------
        // AddRectangle
        // -----------------------------------------------------------------------

        [TestMethod]
        public void GraphicsPath_AddRectangle_FourPoints()
        {
            var p = new GraphicsPath();
            p.AddRectangle(new RectangleF(10, 20, 100, 50));
            Assert.AreEqual(4, p.PointCount);
            Assert.AreEqual((byte)PathPointType.Start, p.PathTypes[0]);
            Assert.AreEqual((byte)PathPointType.Line,  p.PathTypes[1]);
            Assert.AreEqual((byte)PathPointType.Line,  p.PathTypes[2]);
            // Last point has CloseSubpath flag
            Assert.AreNotEqual(0, p.PathTypes[3] & (byte)PathPointType.CloseSubpath);
        }

        [TestMethod]
        public void GraphicsPath_AddRectangle_CorrectCoords()
        {
            var p = new GraphicsPath();
            p.AddRectangle(new RectangleF(10, 20, 100, 50));
            var pts = p.PathPoints;
            Assert.AreEqual(10f, pts[0].X, Tol);  Assert.AreEqual(20f,  pts[0].Y, Tol);
            Assert.AreEqual(110f, pts[1].X, Tol); Assert.AreEqual(20f,  pts[1].Y, Tol);
            Assert.AreEqual(110f, pts[2].X, Tol); Assert.AreEqual(70f,  pts[2].Y, Tol);
            Assert.AreEqual(10f,  pts[3].X, Tol); Assert.AreEqual(70f,  pts[3].Y, Tol);
        }

        [TestMethod]
        public void GraphicsPath_AddRectangle_GetBounds()
        {
            var p = new GraphicsPath();
            p.AddRectangle(new RectangleF(10, 20, 100, 50));
            var b = p.GetBounds();
            Assert.AreEqual(10f,  b.X,      Tol);
            Assert.AreEqual(20f,  b.Y,      Tol);
            Assert.AreEqual(100f, b.Width,  Tol);
            Assert.AreEqual(50f,  b.Height, Tol);
        }

        // -----------------------------------------------------------------------
        // AddEllipse
        // -----------------------------------------------------------------------

        [TestMethod]
        public void GraphicsPath_AddEllipse_13Points()
        {
            var p = new GraphicsPath();
            p.AddEllipse(0f, 0f, 100f, 100f);
            Assert.AreEqual(13, p.PointCount);
        }

        [TestMethod]
        public void GraphicsPath_AddEllipse_FirstIsStart()
        {
            var p = new GraphicsPath();
            p.AddEllipse(0f, 0f, 100f, 100f);
            Assert.AreEqual((byte)PathPointType.Start, p.PathTypes[0]);
        }

        [TestMethod]
        public void GraphicsPath_AddEllipse_LastIsClosedBezier()
        {
            var p = new GraphicsPath();
            p.AddEllipse(0f, 0f, 100f, 100f);
            byte last = p.PathTypes[12];
            Assert.AreNotEqual(0, last & (byte)PathPointType.CloseSubpath);
            Assert.AreEqual((byte)PathPointType.Bezier, (byte)(last & (byte)PathPointType.PathTypeMask));
        }

        [TestMethod]
        public void GraphicsPath_AddEllipse_StartPointIsRightEdge()
        {
            // For ellipse (0,0,100,100): cx=50,cy=50,rx=50 → start=(100,50)
            var p = new GraphicsPath();
            p.AddEllipse(0f, 0f, 100f, 100f);
            var pts = p.PathPoints;
            Assert.AreEqual(100f, pts[0].X, Tol);
            Assert.AreEqual(50f,  pts[0].Y, Tol);
        }

        // -----------------------------------------------------------------------
        // AddArc
        // -----------------------------------------------------------------------

        [TestMethod]
        public void GraphicsPath_AddArc_90Deg_FourPoints()
        {
            // 90° arc → 1 Start + 3 Bezier = 4 points
            var p = new GraphicsPath();
            p.AddArc(0f, 0f, 100f, 100f, 0f, 90f);
            Assert.AreEqual(4, p.PointCount);
            Assert.AreEqual((byte)PathPointType.Start,  p.PathTypes[0]);
            Assert.AreEqual((byte)PathPointType.Bezier, p.PathTypes[1]);
            Assert.AreEqual((byte)PathPointType.Bezier, p.PathTypes[2]);
            Assert.AreEqual((byte)PathPointType.Bezier, p.PathTypes[3]);
        }

        [TestMethod]
        public void GraphicsPath_AddArc_180Deg_SevenPoints()
        {
            // 180° arc = 2 × 90° → 1 Start + 6 Bezier = 7 points
            var p = new GraphicsPath();
            p.AddArc(0f, 0f, 100f, 100f, 0f, 180f);
            Assert.AreEqual(7, p.PointCount);
        }

        // -----------------------------------------------------------------------
        // AddBezier
        // -----------------------------------------------------------------------

        [TestMethod]
        public void GraphicsPath_AddBezier_FourPoints()
        {
            var p = new GraphicsPath();
            p.AddBezier(new PointF(0,0), new PointF(10,20), new PointF(20,20), new PointF(30,0));
            Assert.AreEqual(4, p.PointCount);
            Assert.AreEqual((byte)PathPointType.Start,  p.PathTypes[0]);
            Assert.AreEqual((byte)PathPointType.Bezier, p.PathTypes[1]);
            Assert.AreEqual((byte)PathPointType.Bezier, p.PathTypes[2]);
            Assert.AreEqual((byte)PathPointType.Bezier, p.PathTypes[3]);
        }

        // -----------------------------------------------------------------------
        // AddPolygon
        // -----------------------------------------------------------------------

        [TestMethod]
        public void GraphicsPath_AddPolygon_Closed()
        {
            var pts = new[] { new PointF(0,0), new PointF(50,0), new PointF(25,50) };
            var p = new GraphicsPath();
            p.AddPolygon(pts);
            Assert.AreEqual(3, p.PointCount);
            Assert.AreNotEqual(0, p.PathTypes[2] & (byte)PathPointType.CloseSubpath);
        }

        // -----------------------------------------------------------------------
        // AddCurve
        // -----------------------------------------------------------------------

        [TestMethod]
        public void GraphicsPath_AddCurve_TwoPoints_FourTotal()
        {
            // 2 points = 1 segment → 1 Start + 3 Bezier = 4 points
            var pts = new[] { new PointF(0,0), new PointF(100,100) };
            var p = new GraphicsPath();
            p.AddCurve(pts);
            Assert.AreEqual(4, p.PointCount);
        }

        [TestMethod]
        public void GraphicsPath_AddCurve_ThreePoints_SevenTotal()
        {
            // 3 points = 2 segments → 1 Start + 6 Bezier = 7 points
            var pts = new[] { new PointF(0,0), new PointF(50,50), new PointF(100,0) };
            var p = new GraphicsPath();
            p.AddCurve(pts);
            Assert.AreEqual(7, p.PointCount);
        }

        // -----------------------------------------------------------------------
        // AddClosedCurve
        // -----------------------------------------------------------------------

        [TestMethod]
        public void GraphicsPath_AddClosedCurve_Closed()
        {
            var pts = new[] { new PointF(0,0), new PointF(100,0), new PointF(50,80) };
            var p = new GraphicsPath();
            p.AddClosedCurve(pts);
            // n=3 segments → 1 Start + 9 Bezier = 10 points, last is closed
            Assert.AreEqual(10, p.PointCount);
            Assert.AreNotEqual(0, p.PathTypes[p.PointCount - 1] & (byte)PathPointType.CloseSubpath);
        }

        // -----------------------------------------------------------------------
        // AddPath
        // -----------------------------------------------------------------------

        [TestMethod]
        public void GraphicsPath_AddPath_NoConnect()
        {
            var p1 = new GraphicsPath();
            p1.AddLine(0, 0, 100, 0);
            var p2 = new GraphicsPath();
            p2.AddLine(200, 200, 300, 200);
            p1.AddPath(p2, connect: false);
            Assert.AreEqual(4, p1.PointCount);
            // The first point of p2's contribution should still be Start
            Assert.AreEqual((byte)PathPointType.Start, p1.PathTypes[2]);
        }

        [TestMethod]
        public void GraphicsPath_AddPath_Connect()
        {
            var p1 = new GraphicsPath();
            p1.AddLine(0, 0, 100, 0);
            var p2 = new GraphicsPath();
            p2.AddLine(200, 200, 300, 200);
            p1.AddPath(p2, connect: true);
            // The first point of p2 should be Line, not Start
            Assert.AreEqual((byte)PathPointType.Line, (byte)(p1.PathTypes[2] & 0x07));
        }

        // -----------------------------------------------------------------------
        // Transform
        // -----------------------------------------------------------------------

        [TestMethod]
        public void GraphicsPath_Transform_Translate()
        {
            var p = new GraphicsPath();
            p.AddLine(new PointF(0, 0), new PointF(100, 0));
            var m = new Matrix();
            m.Translate(10, 20);
            p.Transform(m);
            var pts = p.PathPoints;
            Assert.AreEqual(10f,  pts[0].X, Tol);
            Assert.AreEqual(20f,  pts[0].Y, Tol);
            Assert.AreEqual(110f, pts[1].X, Tol);
            Assert.AreEqual(20f,  pts[1].Y, Tol);
        }

        // -----------------------------------------------------------------------
        // Flatten
        // -----------------------------------------------------------------------

        [TestMethod]
        public void GraphicsPath_Flatten_EllipseBecomesLines()
        {
            var p = new GraphicsPath();
            p.AddEllipse(0f, 0f, 100f, 100f);
            p.Flatten();
            foreach (var t in p.PathTypes)
            {
                byte pt = (byte)(t & (byte)PathPointType.PathTypeMask);
                Assert.IsTrue(pt == (byte)PathPointType.Start || pt == (byte)PathPointType.Line,
                    $"Expected only Start or Line types after Flatten, got {pt}.");
            }
        }

        [TestMethod]
        public void GraphicsPath_Flatten_LineUnchanged()
        {
            var p = new GraphicsPath();
            p.AddLine(0, 0, 100, 0);
            int before = p.PointCount;
            p.Flatten();
            Assert.AreEqual(before, p.PointCount);
        }

        // -----------------------------------------------------------------------
        // GetBounds with Matrix
        // -----------------------------------------------------------------------

        [TestMethod]
        public void GraphicsPath_GetBounds_WithTranslate()
        {
            var p = new GraphicsPath();
            p.AddRectangle(new RectangleF(0, 0, 100, 50));
            var m = new Matrix();
            m.Translate(20, 10);
            var b = p.GetBounds(m);
            Assert.AreEqual(20f,  b.X,      Tol);
            Assert.AreEqual(10f,  b.Y,      Tol);
            Assert.AreEqual(100f, b.Width,  Tol);
            Assert.AreEqual(50f,  b.Height, Tol);
        }

        // -----------------------------------------------------------------------
        // IsVisible
        // -----------------------------------------------------------------------

        [TestMethod]
        public void GraphicsPath_IsVisible_InsideRectangle()
        {
            var p = new GraphicsPath();
            p.AddRectangle(new RectangleF(0, 0, 100, 100));
            Assert.IsTrue(p.IsVisible(new PointF(50, 50)));
        }

        [TestMethod]
        public void GraphicsPath_IsVisible_OutsideRectangle()
        {
            var p = new GraphicsPath();
            p.AddRectangle(new RectangleF(0, 0, 100, 100));
            Assert.IsFalse(p.IsVisible(new PointF(150, 50)));
        }

        // -----------------------------------------------------------------------
        // Reset / Clone
        // -----------------------------------------------------------------------

        [TestMethod]
        public void GraphicsPath_Reset_EmptiesPath()
        {
            var p = new GraphicsPath();
            p.AddLine(0, 0, 100, 0);
            p.Reset();
            Assert.AreEqual(0, p.PointCount);
        }

        [TestMethod]
        public void GraphicsPath_Clone_IsDeepCopy()
        {
            var p = new GraphicsPath();
            p.AddRectangle(new RectangleF(0, 0, 100, 50));
            var c = p.Clone();
            Assert.AreNotSame(p, c);
            Assert.AreEqual(p.PointCount, c.PointCount);
            c.Reset();
            Assert.AreEqual(4, p.PointCount); // original unchanged
        }

        // -----------------------------------------------------------------------
        // PathData
        // -----------------------------------------------------------------------

        [TestMethod]
        public void GraphicsPath_PathData_RoundTrip()
        {
            var p = new GraphicsPath();
            p.AddLine(new PointF(5, 10), new PointF(50, 60));
            var pd = p.PathData;
            Assert.AreEqual(2, pd.Points.Length);
            Assert.AreEqual(2, pd.Types.Length);
            Assert.AreEqual(5f,  pd.Points[0].X, Tol);
            Assert.AreEqual(10f, pd.Points[0].Y, Tol);
        }

        // -----------------------------------------------------------------------
        // Constructor from arrays
        // -----------------------------------------------------------------------

        [TestMethod]
        public void GraphicsPath_CtorFromArrays()
        {
            var pts   = new[] { new PointF(0,0), new PointF(100,0), new PointF(100,100) };
            var types = new byte[] { (byte)PathPointType.Start, (byte)PathPointType.Line, (byte)PathPointType.Line };
            var p = new GraphicsPath(pts, types);
            Assert.AreEqual(3, p.PointCount);
            Assert.AreEqual(FillMode.Alternate, p.FillMode);
        }

        [TestMethod]
        public void GraphicsPath_EllipseFlattenAndWiden_WorkAsExpected()
        {
            using var ellipse = new GraphicsPath();
            ellipse.AddEllipse(new RectangleF(10f, 20f, 80f, 60f));
            ellipse.Flatten(null, 0.2f);

            PointF[] pts = ellipse.PathPoints;
            byte[] types = ellipse.PathTypes;
            Assert.IsTrue(pts.Length > 16, "Flattened ellipse should contain many line points.");

            for (int i = 0; i < types.Length; i++)
            {
                byte raw = (byte)(types[i] & (byte)PathPointType.PathTypeMask);
                Assert.AreNotEqual((byte)PathPointType.Bezier, raw, "Flatten should not keep bezier points.");
            }

            float cx = 50f;
            float cy = 50f;
            float rx = 40f;
            float ry = 30f;
            foreach (PointF p in pts)
            {
                float eq = ((p.X - cx) * (p.X - cx)) / (rx * rx)
                         + ((p.Y - cy) * (p.Y - cy)) / (ry * ry);
                Assert.IsTrue(Math.Abs(eq - 1f) <= 0.18f, $"Flattened ellipse deviates too much: eq={eq:0.000}");
            }

            using var line = new GraphicsPath();
            line.AddLine(new PointF(0f, 0f), new PointF(40f, 0f));
            using var pen = new Pen(Color.Black, 6f);
            line.Widen(pen);

            bool hasClose = false;
            foreach (byte t in line.PathTypes)
            {
                if ((t & (byte)PathPointType.CloseSubpath) != 0)
                {
                    hasClose = true;
                    break;
                }
            }

            Assert.IsTrue(hasClose, "Widened path should contain closed outlines.");
        }

        

        

        // -----------------------------------------------------------------------
        // Pen.CustomStartCap / CustomEndCap
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Pen_CustomStartCap_RoundTrip()
        {
            using var pen = new Pen(Color.Red);
            var cap = new AdjustableArrowCap(5f, 10f);
            pen.CustomStartCap = cap;
            Assert.AreSame(cap, pen.CustomStartCap);
        }

        // -----------------------------------------------------------------------
        // FontFamily / StringFormat stubs
        // -----------------------------------------------------------------------

        [TestMethod]
        public void FontFamily_Name()
        {
            var ff = new FontFamily("Arial");
            Assert.AreEqual("Arial", ff.Name);
        }

        [TestMethod]
        public void StringFormat_Default()
        {
            using var sf = new StringFormat();
            Assert.AreEqual(StringAlignment.Near, sf.Alignment);
        }
    }
}
