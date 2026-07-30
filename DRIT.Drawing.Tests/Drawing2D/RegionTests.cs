using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Geometry;

namespace DRIT.Drawing.Tests.Drawing2D
{
    [TestClass]
    public class RegionTests
    {
        // -----------------------------------------------------------------------
        // Region — construction
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Region_DefaultCtor_IsInfinite()
        {
            using var r = new Region();
            Assert.IsTrue(r.IsInfinite());
            Assert.IsFalse(r.IsEmpty());
        }

        [TestMethod]
        public void Region_RectFCtor_ContainsInteriorPoint()
        {
            using var r = new Region(new RectangleF(10, 20, 100, 50));
            Assert.IsTrue(r.IsVisible(50f, 40f));
            Assert.IsFalse(r.IsVisible(200f, 200f));
        }

        [TestMethod]
        public void Region_IntRectCtor_ContainsInteriorPoint()
        {
            using var r = new Region(new Rectangle(0, 0, 100, 100));
            Assert.IsTrue(r.IsVisible(50f, 50f));
            Assert.IsFalse(r.IsVisible(150f, 50f));
        }

        [TestMethod]
        public void Region_PathCtor_ContainsInteriorPoint()
        {
            using var path = new GraphicsPath();
            path.AddRectangle(new RectangleF(0, 0, 100, 100));
            using var r = new Region(path);
            Assert.IsTrue(r.IsVisible(50f, 50f));
            Assert.IsFalse(r.IsVisible(200f, 200f));
        }

        // -----------------------------------------------------------------------
        // MakeEmpty / MakeInfinite
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Region_MakeEmpty_IsEmpty()
        {
            using var r = new Region(new RectangleF(0, 0, 100, 100));
            r.MakeEmpty();
            Assert.IsTrue(r.IsEmpty());
            Assert.IsFalse(r.IsInfinite());
        }

        [TestMethod]
        public void Region_MakeInfinite_IsInfinite()
        {
            using var r = new Region(new RectangleF(0, 0, 100, 100));
            r.MakeInfinite();
            Assert.IsTrue(r.IsInfinite());
            Assert.IsFalse(r.IsEmpty());
        }

        // -----------------------------------------------------------------------
        // Union
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Region_Union_CoversBothAreas()
        {
            using var r = new Region(new RectangleF(0, 0, 50, 50));
            r.Union(new RectangleF(60, 60, 50, 50));
            Assert.IsTrue(r.IsVisible(25f, 25f));   // first rect
            Assert.IsTrue(r.IsVisible(85f, 85f));   // second rect
            Assert.IsFalse(r.IsVisible(55f, 55f));  // gap
        }

        [TestMethod]
        public void Region_Union_RectOverlap_CoversBoth()
        {
            using var r = new Region(new RectangleF(0, 0, 60, 60));
            using var r2 = new Region(new RectangleF(40, 40, 60, 60));
            r.Union(r2);
            Assert.IsTrue(r.IsVisible(10f, 10f));
            Assert.IsTrue(r.IsVisible(90f, 90f));
            Assert.IsTrue(r.IsVisible(50f, 50f)); // overlap
        }

        // -----------------------------------------------------------------------
        // Intersect
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Region_Intersect_OnlyCommonArea()
        {
            using var r = new Region(new RectangleF(0, 0, 60, 60));
            r.Intersect(new RectangleF(40, 40, 60, 60));
            Assert.IsFalse(r.IsVisible(10f, 10f));  // only in first
            Assert.IsFalse(r.IsVisible(90f, 90f));  // only in second
            Assert.IsTrue(r.IsVisible(50f, 50f));   // overlap
        }

        [TestMethod]
        public void Region_Intersect_NoOverlap_IsEmpty()
        {
            using var r = new Region(new RectangleF(0, 0, 40, 40));
            r.Intersect(new RectangleF(50, 50, 40, 40));
            Assert.IsTrue(r.IsEmpty());
        }

        // -----------------------------------------------------------------------
        // Exclude
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Region_Exclude_RemovesOverlapArea()
        {
            using var r = new Region(new RectangleF(0, 0, 100, 100));
            r.Exclude(new RectangleF(50, 0, 100, 100));
            Assert.IsTrue(r.IsVisible(25f, 50f));   // left side remains
            Assert.IsFalse(r.IsVisible(75f, 50f));  // excluded area
        }

        // -----------------------------------------------------------------------
        // Complement
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Region_Complement_FlipsRegions()
        {
            // Complement: result = arg \ this  (points in arg but NOT in this)
            using var r = new Region(new RectangleF(50, 0, 50, 100));
            r.Complement(new RectangleF(0, 0, 100, 100));
            // Result: left half only
            Assert.IsTrue(r.IsVisible(25f, 50f));   // in arg, not in original
            Assert.IsFalse(r.IsVisible(75f, 50f));  // in both original and arg
        }

        // -----------------------------------------------------------------------
        // Xor
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Region_Xor_SymmetricDifference()
        {
            using var r = new Region(new RectangleF(0, 0, 60, 60));
            r.Xor(new RectangleF(40, 40, 60, 60));
            Assert.IsTrue(r.IsVisible(10f, 10f));   // only in first
            Assert.IsTrue(r.IsVisible(90f, 90f));   // only in second
            Assert.IsFalse(r.IsVisible(50f, 50f));  // in both → excluded
        }

        // -----------------------------------------------------------------------
        // Translate
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Region_Translate_ShiftsRegion()
        {
            using var r = new Region(new RectangleF(0, 0, 50, 50));
            r.Translate(100f, 100f);
            Assert.IsFalse(r.IsVisible(25f, 25f));    // original position
            Assert.IsTrue(r.IsVisible(125f, 125f));   // new position
        }

        [TestMethod]
        public void Region_Translate_Int_ShiftsRegion()
        {
            using var r = new Region(new RectangleF(0, 0, 50, 50));
            r.Translate(50, 50);
            Assert.IsTrue(r.IsVisible(75f, 75f));
        }

        // -----------------------------------------------------------------------
        // Transform
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Region_Transform_ScaleDoubles()
        {
            using var r = new Region(new RectangleF(0, 0, 50, 50));
            using var m = new Matrix();
            m.Scale(2f, 2f);
            r.Transform(m);
            Assert.IsTrue(r.IsVisible(75f, 75f));   // within scaled region
            Assert.IsFalse(r.IsVisible(150f, 150f));// outside
        }

        // -----------------------------------------------------------------------
        // GetBounds
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Region_GetBounds_RectRegion()
        {
            using var r = new Region(new RectangleF(10, 20, 80, 60));
            var b = r.GetBounds();
            Assert.AreEqual(10f, b.X, 1e-4f);
            Assert.AreEqual(20f, b.Y, 1e-4f);
            Assert.AreEqual(80f, b.Width,  1e-4f);
            Assert.AreEqual(60f, b.Height, 1e-4f);
        }

        [TestMethod]
        public void Region_GetBounds_UnionRegion_EnclosedByMax()
        {
            using var r = new Region(new RectangleF(0, 0, 50, 50));
            r.Union(new RectangleF(60, 60, 50, 50));
            var b = r.GetBounds();
            Assert.IsTrue(b.X <= 0f);
            Assert.IsTrue(b.Y <= 0f);
            Assert.IsTrue(b.Right  >= 110f);
            Assert.IsTrue(b.Bottom >= 110f);
        }

        // -----------------------------------------------------------------------
        // IsVisible(RectangleF)
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Region_IsVisible_Rect_OverlapReturnsTrue()
        {
            using var r = new Region(new RectangleF(0, 0, 100, 100));
            Assert.IsTrue(r.IsVisible(new RectangleF(50, 50, 100, 100)));
        }

        [TestMethod]
        public void Region_IsVisible_Rect_NoOverlapReturnsFalse()
        {
            using var r = new Region(new RectangleF(0, 0, 100, 100));
            Assert.IsFalse(r.IsVisible(new RectangleF(200, 200, 50, 50)));
        }

        // -----------------------------------------------------------------------
        // Clone
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Region_Clone_IsIndependent()
        {
            using var r = new Region(new RectangleF(0, 0, 100, 100));
            using var c = r.Clone();
            r.MakeEmpty();
            Assert.IsTrue(c.IsVisible(50f, 50f)); // clone unaffected
        }

        // -----------------------------------------------------------------------
        // GetRegionData / round-trip
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Region_GetRegionData_RoundTrip_Empty()
        {
            using var r = new Region();
            r.MakeEmpty();
            var data = r.GetRegionData();
            using var r2 = new Region(data);
            Assert.IsTrue(r2.IsEmpty());
        }

        [TestMethod]
        public void Region_GetRegionData_RoundTrip_Rect()
        {
            using var r = new Region(new RectangleF(5, 10, 80, 40));
            var data = r.GetRegionData();
            using var r2 = new Region(data);
            Assert.IsTrue(r2.IsVisible(40f, 30f));
            Assert.IsFalse(r2.IsVisible(200f, 200f));
        }

        [TestMethod]
        public void Region_GetRegionData_RoundTrip_Path()
        {
            using var path = new GraphicsPath();
            path.AddRectangle(new RectangleF(10, 10, 80, 80));
            using var r = new Region(path);
            var data = r.GetRegionData();
            using var r2 = new Region(data);
            Assert.IsTrue(r2.IsVisible(50f, 50f));
        }

        [TestMethod]
        public void Region_GetRegionData_RoundTrip_Op()
        {
            using var r = new Region(new RectangleF(0, 0, 60, 60));
            r.Union(new RectangleF(40, 40, 60, 60));
            var data = r.GetRegionData();
            using var r2 = new Region(data);
            Assert.IsTrue(r2.IsVisible(10f, 10f));
            Assert.IsTrue(r2.IsVisible(90f, 90f));
        }

        // -----------------------------------------------------------------------
        // GetRegionScans
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Region_GetRegionScans_SingleRect_ReturnsSingleEntry()
        {
            using var r = new Region(new RectangleF(10, 20, 80, 40));
            using var m = new Matrix();
            var scans = r.GetRegionScans(m);
            Assert.AreEqual(1, scans.Length);
            Assert.AreEqual(10f, scans[0].X,      1e-4f);
            Assert.AreEqual(20f, scans[0].Y,      1e-4f);
            Assert.AreEqual(80f, scans[0].Width,  1e-4f);
            Assert.AreEqual(40f, scans[0].Height, 1e-4f);
        }

        [TestMethod]
        public void Region_GetRegionScans_Empty_ReturnsEmptyArray()
        {
            using var r = new Region();
            r.MakeEmpty();
            using var m = new Matrix();
            var scans = r.GetRegionScans(m);
            Assert.AreEqual(0, scans.Length);
        }

        [TestMethod]
        public void Region_GetRegionScans_ScaledMatrix_AppliesTransform()
        {
            using var r = new Region(new RectangleF(0, 0, 100, 100));
            using var m = new Matrix();
            m.Scale(2f, 2f);
            var scans = r.GetRegionScans(m);
            Assert.IsTrue(scans.Length >= 1);
            // All scans should be within the scaled area (0,0 to 200,200)
            foreach (var s in scans)
            {
                Assert.IsTrue(s.X >= -0.01f);
                Assert.IsTrue(s.Right <= 200.01f);
            }
        }

        // -----------------------------------------------------------------------
        // Equals
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Region_Equals_SameRect_ReturnsTrue()
        {
            using var r1 = new Region(new RectangleF(10, 10, 80, 80));
            using var r2 = new Region(new RectangleF(10, 10, 80, 80));
            Assert.IsTrue(r1.Equals(r2));
        }

        [TestMethod]
        public void Region_Equals_DifferentRects_ReturnsFalse()
        {
            using var r1 = new Region(new RectangleF(0, 0, 50, 50));
            using var r2 = new Region(new RectangleF(60, 60, 50, 50));
            Assert.IsFalse(r1.Equals(r2));
        }

        [TestMethod]
        public void Region_Equals_BothEmpty_ReturnsTrue()
        {
            using var r1 = new Region();
            r1.MakeEmpty();
            using var r2 = new Region();
            r2.MakeEmpty();
            Assert.IsTrue(r1.Equals(r2));
        }

        [TestMethod]
        public void Region_Equals_BothInfinite_ReturnsTrue()
        {
            using var r1 = new Region();
            using var r2 = new Region();
            Assert.IsTrue(r1.Equals(r2));
        }

        [TestMethod]
        public void Region_IsVisible_NullGraphics_Works()
        {
            using var r = new Region(new RectangleF(0, 0, 100, 100));
            Assert.IsTrue(r.IsVisible(new PointF(50f, 50f), null));
            Assert.IsFalse(r.IsVisible(new PointF(200f, 200f), null));
        }

        [TestMethod]
        public void Region_IsEmpty_NullGraphics_Works()
        {
            using var r = new Region();
            r.MakeEmpty();
            Assert.IsTrue(r.IsEmpty(null));
        }

        [TestMethod]
        public void Region_GetBounds_NullGraphics_Works()
        {
            using var r = new Region(new RectangleF(5, 5, 90, 90));
            var b = r.GetBounds(null);
            Assert.AreEqual(5f, b.X, 1e-4f);
        }
    }
}
