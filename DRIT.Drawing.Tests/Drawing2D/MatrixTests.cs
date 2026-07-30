using Microsoft.VisualStudio.TestTools.UnitTesting;
using DRIT.Drawing.Drawing2D;
using DRIT.Drawing.Geometry;

namespace DRIT.Drawing.Tests.Drawing2D
{
    [TestClass]
    public class MatrixTests
    {
        private const float Tolerance = 1e-4f;

        // -----------------------------------------------------------------------
        // Identity / basic properties
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Matrix_Default_IsIdentity()
        {
            var m = new Matrix();
            Assert.IsTrue(m.IsIdentity);
            Assert.IsTrue(m.IsInvertible);
            Assert.AreEqual(0f, m.OffsetX);
            Assert.AreEqual(0f, m.OffsetY);
        }

        [TestMethod]
        public void Matrix_Elements_Returns6()
        {
            var m = new Matrix(1, 2, 3, 4, 5, 6);
            var e = m.Elements;
            Assert.AreEqual(6, e.Length);
            Assert.AreEqual(1f, e[0]);
            Assert.AreEqual(2f, e[1]);
            Assert.AreEqual(3f, e[2]);
            Assert.AreEqual(4f, e[3]);
            Assert.AreEqual(5f, e[4]);
            Assert.AreEqual(6f, e[5]);
        }

        [TestMethod]
        public void Matrix_Elements_IsCopy()
        {
            var m = new Matrix();
            var e = m.Elements;
            e[0] = 99f;                  // mutate the copy
            Assert.IsTrue(m.IsIdentity); // original unchanged
        }

        [TestMethod]
        public void Matrix_OffsetXY()
        {
            var m = new Matrix(1, 0, 0, 1, 7f, -3f);
            Assert.AreEqual(7f,  m.OffsetX);
            Assert.AreEqual(-3f, m.OffsetY);
        }

        // -----------------------------------------------------------------------
        // Plan acceptance test 1 — Rotate(90) on (1,0) → (0,1)
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Matrix_Rotate90_TransformPoint()
        {
            var m = new Matrix();
            m.Rotate(90);
            var pts = new[] { new PointF(1, 0) };
            m.TransformPoints(pts);
            Assert.AreEqual(0f, pts[0].X, Tolerance);
            Assert.AreEqual(1f, pts[0].Y, Tolerance);
        }

        // -----------------------------------------------------------------------
        // Plan acceptance test 2 — Invert translation, transform (15,25) → (5,5)
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Matrix_Invert_TranslationMatrix()
        {
            var m2 = new Matrix(1, 0, 0, 1, 10, 20);
            m2.Invert();
            var p = new[] { new PointF(15, 25) };
            m2.TransformPoints(p);
            Assert.AreEqual(5f, p[0].X, Tolerance);
            Assert.AreEqual(5f, p[0].Y, Tolerance);
        }

        // -----------------------------------------------------------------------
        // Translate
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Matrix_Translate_Prepend()
        {
            var m = new Matrix();
            m.Translate(10, 20);
            var pts = new[] { new PointF(5, 5) };
            m.TransformPoints(pts);
            Assert.AreEqual(15f, pts[0].X, Tolerance);
            Assert.AreEqual(25f, pts[0].Y, Tolerance);
        }

        [TestMethod]
        public void Matrix_Translate_Append()
        {
            var m = new Matrix();
            m.Rotate(90);             // first rotate (1,0)→(0,1)
            m.Translate(10, 0, MatrixOrder.Append); // then shift +10 in X
            var pts = new[] { new PointF(1, 0) };
            m.TransformPoints(pts);
            // Rotate (1,0)→(0,1), then translate: (0+10,1)=(10,1)
            Assert.AreEqual(10f, pts[0].X, Tolerance);
            Assert.AreEqual(1f,  pts[0].Y, Tolerance);
        }

        // -----------------------------------------------------------------------
        // Scale
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Matrix_Scale()
        {
            var m = new Matrix();
            m.Scale(2, 3);
            var pts = new[] { new PointF(4, 5) };
            m.TransformPoints(pts);
            Assert.AreEqual(8f,  pts[0].X, Tolerance);
            Assert.AreEqual(15f, pts[0].Y, Tolerance);
        }

        // -----------------------------------------------------------------------
        // RotateAt
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Matrix_RotateAt_AroundPoint()
        {
            // Rotate 90° around (1,1)
            // (2,1) is 1 unit to the right of center → after 90° CW → (1,2)
            var m = new Matrix();
            m.RotateAt(90, new PointF(1, 1));
            var pts = new[] { new PointF(2, 1) };
            m.TransformPoints(pts);
            Assert.AreEqual(1f, pts[0].X, Tolerance);
            Assert.AreEqual(2f, pts[0].Y, Tolerance);
        }

        // -----------------------------------------------------------------------
        // TransformVectors (no translation)
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Matrix_TransformVectors_IgnoresTranslation()
        {
            var m = new Matrix(1, 0, 0, 1, 100, 200); // pure translation
            var pts = new[] { new PointF(3, 4) };
            m.TransformVectors(pts);
            // Translation must NOT apply to vectors
            Assert.AreEqual(3f, pts[0].X, Tolerance);
            Assert.AreEqual(4f, pts[0].Y, Tolerance);
        }

        [TestMethod]
        public void Matrix_TransformVectors_AppliesLinear()
        {
            var m = new Matrix();
            m.Scale(2, 3);
            var pts = new[] { new PointF(1, 1) };
            m.TransformVectors(pts);
            Assert.AreEqual(2f, pts[0].X, Tolerance);
            Assert.AreEqual(3f, pts[0].Y, Tolerance);
        }

        // -----------------------------------------------------------------------
        // TransformPoints (Point overload)
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Matrix_TransformPoints_IntArray()
        {
            var m = new Matrix(1, 0, 0, 1, 5, 10); // translate
            var pts = new[] { new Point(3, 7) };
            m.TransformPoints(pts);
            Assert.AreEqual(8,  pts[0].X);
            Assert.AreEqual(17, pts[0].Y);
        }

        // -----------------------------------------------------------------------
        // Multiply — Prepend vs Append
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Matrix_Multiply_Prepend()
        {
            // A = Translate(10,0), B = Rotate(90).  Prepend B onto A → B*A.
            // For point (1,0): first B(rotate)→(0,1), then A(translate +10,0)→(10,1).
            var a = new Matrix(1, 0, 0, 1, 10, 0); // T(10,0)
            var b = new Matrix();
            b.Rotate(90);                           // R(90)
            a.Multiply(b, MatrixOrder.Prepend);     // a = b * a
            var pts = new[] { new PointF(1, 0) };
            a.TransformPoints(pts);
            Assert.AreEqual(10f, pts[0].X, Tolerance);
            Assert.AreEqual(1f,  pts[0].Y, Tolerance);
        }

        [TestMethod]
        public void Matrix_Multiply_Append()
        {
            // A = Translate(10,0), B = Rotate(90).  Append B onto A → A*B.
            // For point (1,0): first A(translate +10,0)→(11,0), then B(rotate)→(0,11).
            var a = new Matrix(1, 0, 0, 1, 10, 0);
            var b = new Matrix();
            b.Rotate(90);
            a.Multiply(b, MatrixOrder.Append);      // a = a * b
            var pts = new[] { new PointF(1, 0) };
            a.TransformPoints(pts);
            Assert.AreEqual(0f,  pts[0].X, Tolerance);
            Assert.AreEqual(11f, pts[0].Y, Tolerance);
        }

        // -----------------------------------------------------------------------
        // IsInvertible
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Matrix_IsInvertible_SingularMatrix()
        {
            var m = new Matrix(1, 2, 2, 4, 0, 0); // det = 1*4 - 2*2 = 0
            Assert.IsFalse(m.IsInvertible);
        }

        // -----------------------------------------------------------------------
        // Reset
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Matrix_Reset_RestoresIdentity()
        {
            var m = new Matrix();
            m.Rotate(45);
            m.Translate(10, 20);
            m.Reset();
            Assert.IsTrue(m.IsIdentity);
        }

        // -----------------------------------------------------------------------
        // Clone
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Matrix_Clone_IsDeepCopy()
        {
            var m = new Matrix(1, 0, 0, 1, 10, 20);
            var c = m.Clone();
            Assert.AreNotSame(m, c);
            Assert.AreEqual(10f, c.OffsetX);
            c.Translate(5, 0);
            Assert.AreEqual(10f, m.OffsetX); // original unchanged
        }

        // -----------------------------------------------------------------------
        // RectangleF + PointF[] constructor
        // -----------------------------------------------------------------------

        [TestMethod]
        public void Matrix_ParallelogramCtor_MapsRectToTriangle()
        {
            // Map rect (0,0,100,100) to parallelogram: TL=(0,0), TR=(100,0), BL=(0,100)
            // That's just the identity transform.
            var rect = new RectangleF(0, 0, 100, 100);
            var plg  = new[] { new PointF(0,0), new PointF(100,0), new PointF(0,100) };
            var m    = new Matrix(rect, plg);
            Assert.IsTrue(m.IsIdentity);
        }

        // -----------------------------------------------------------------------
        // PointTransform internal helper
        // -----------------------------------------------------------------------

        [TestMethod]
        public void PointTransform_Transform_SinglePoint()
        {
            var m   = new Matrix(1, 0, 0, 1, 5, 10);
            var res = PointTransform.Transform(m, new PointF(3, 4));
            Assert.AreEqual(8f,  res.X, Tolerance);
            Assert.AreEqual(14f, res.Y, Tolerance);
        }

        [TestMethod]
        public void PointTransform_TransformAll_ReturnsCopy()
        {
            var m    = new Matrix(1, 0, 0, 1, 1, 1);
            var orig = new[] { new PointF(0, 0), new PointF(2, 3) };
            var res  = PointTransform.TransformAll(m, orig);
            Assert.AreNotSame(orig, res);
            Assert.AreEqual(1f, res[0].X, Tolerance);
            Assert.AreEqual(1f, res[0].Y, Tolerance);
            Assert.AreEqual(3f, res[1].X, Tolerance);
            Assert.AreEqual(4f, res[1].Y, Tolerance);
            // Original unchanged
            Assert.AreEqual(0f, orig[0].X);
        }

        [TestMethod]
        public void PointTransform_TransformInPlace_ModifiesArray()
        {
            var m    = new Matrix(1, 0, 0, 1, 1, 1);
            var pts  = new[] { new PointF(2, 3) };
            PointTransform.TransformInPlace(m, pts);
            Assert.AreEqual(3f, pts[0].X, Tolerance);
            Assert.AreEqual(4f, pts[0].Y, Tolerance);
        }

        [TestMethod]
        public void Matrix_CompositeInvert_RestoresPoints()
        {
            using var m = new Matrix();
            m.Rotate(23f, MatrixOrder.Append);
            m.Scale(1.5f, 0.8f, MatrixOrder.Append);
            m.Translate(11f, -7f, MatrixOrder.Append);

            var points = new[]
            {
                new PointF(1f, 2f),
                new PointF(-4f, 3f),
                new PointF(10f, -8f),
            };

            var transformed = (PointF[])points.Clone();
            m.TransformPoints(transformed);

            using var inverse = m.Clone();
            Assert.IsTrue(inverse.IsInvertible);
            inverse.Invert();
            inverse.TransformPoints(transformed);

            for (int i = 0; i < points.Length; i++)
            {
                Assert.AreEqual(points[i].X, transformed[i].X, 1e-3f, $"X mismatch at index {i}.");
                Assert.AreEqual(points[i].Y, transformed[i].Y, 1e-3f, $"Y mismatch at index {i}.");
            }
        }
    }
}
