using System.Linq;
using System.Numerics;
using NUnit.Framework;

namespace FPM.Tests
{
    public class FPVectorTests
    {
        [Test]
        public void DotAndCross_AreExactSumsRoundedOnce()
        {
            // Components below 2^62 in raw value, so sums of two or three products stay inside 128 bits.
            long[] values = Oracle.RawValues(600, 30).Select(v => v >> 2).ToArray();
            for (int i = 0; i + 5 < values.Length; i += 6)
            {
                FP3 a = new FP3(FP.FromRaw(values[i]), FP.FromRaw(values[i + 1]), FP.FromRaw(values[i + 2]));
                FP3 b = new FP3(FP.FromRaw(values[i + 3]), FP.FromRaw(values[i + 4]), FP.FromRaw(values[i + 5]));

                Assert.That(FP3.Dot(a, b).RawValue, Is.EqualTo(Rounded(P(a.X, b.X) + P(a.Y, b.Y) + P(a.Z, b.Z))));
                Assert.That(FP2.Dot(a.XZ, b.XZ).RawValue, Is.EqualTo(Rounded(P(a.X, b.X) + P(a.Z, b.Z))));
                Assert.That(FP2.Cross(a.XZ, b.XZ).RawValue, Is.EqualTo(Rounded(P(a.X, b.Z) - P(a.Z, b.X))));

                FP3 cross = FP3.Cross(a, b);
                Assert.That(cross.X.RawValue, Is.EqualTo(Rounded(P(a.Y, b.Z) - P(a.Z, b.Y))));
                Assert.That(cross.Y.RawValue, Is.EqualTo(Rounded(P(a.Z, b.X) - P(a.X, b.Z))));
                Assert.That(cross.Z.RawValue, Is.EqualTo(Rounded(P(a.X, b.Y) - P(a.Y, b.X))));
            }
        }

        // Products and lengths take a 64-bit path for small components (below 2^30, and 2^31 for lengths): values on
        // both sides of those limits, and small random ones, must match the exact sums all the same.
        [Test]
        public void DotCrossAndLength_AreExactAroundTheFastPathLimits()
        {
            System.Collections.Generic.List<long> edges = new System.Collections.Generic.List<long>();
            foreach (long limit in new[] { 1L << 30, 1L << 31 })
            {
                foreach (long offset in new long[] { -2, -1, 0, 1 })
                {
                    edges.Add(limit + offset);
                    edges.Add(-(limit + offset));
                }
            }
            System.Random random = new System.Random(32);
            for (int i = 0; i < 64; i++)
            {
                edges.Add(random.Next(int.MinValue, int.MaxValue) >> (i % 8));
            }
            long[] values = edges.ToArray();
            for (int i = 0; i < values.Length; i++)
            {
                for (int j = 0; j < values.Length; j += 3)
                {
                    FP3 a = new FP3(FP.FromRaw(values[i]), FP.FromRaw(values[(i + j) % values.Length]), FP.FromRaw(values[(i + 2 * j + 1) % values.Length]));
                    FP3 b = new FP3(FP.FromRaw(values[j]), FP.FromRaw(values[(j + 5) % values.Length]), FP.FromRaw(values[(i + j + 7) % values.Length]));
                    Assert.That(FP3.Dot(a, b).RawValue, Is.EqualTo(Rounded(P(a.X, b.X) + P(a.Y, b.Y) + P(a.Z, b.Z))));
                    Assert.That(FP2.Dot(a.XZ, b.XZ).RawValue, Is.EqualTo(Rounded(P(a.X, b.X) + P(a.Z, b.Z))));
                    Assert.That(FP2.Cross(a.XZ, b.XZ).RawValue, Is.EqualTo(Rounded(P(a.X, b.Z) - P(a.Z, b.X))));
                    FP3 cross = FP3.Cross(a, b);
                    Assert.That(cross.X.RawValue, Is.EqualTo(Rounded(P(a.Y, b.Z) - P(a.Z, b.Y))));
                    Assert.That(cross.Z.RawValue, Is.EqualTo(Rounded(P(a.X, b.Y) - P(a.Y, b.X))));
                    AssertIsFloorSqrt(a.Length.RawValue, P(a.X, a.X) + P(a.Y, a.Y) + P(a.Z, a.Z));
                    AssertIsFloorSqrt(a.XZ.Length.RawValue, P(a.X, a.X) + P(a.Z, a.Z));
                }
            }
        }

        [Test]
        public void Length_IsExactFloorOfTheTrueLength()
        {
            long[] values = Oracle.RawValues(600, 31).ToArray();
            for (int i = 0; i + 2 < values.Length; i += 3)
            {
                FP3 v = new FP3(FP.FromRaw(values[i]), FP.FromRaw(values[i + 1]), FP.FromRaw(values[i + 2]));

                AssertIsFloorSqrt(v.Length.RawValue, P(v.X, v.X) + P(v.Y, v.Y) + P(v.Z, v.Z));
                AssertIsFloorSqrt(v.XZ.Length.RawValue, P(v.X, v.X) + P(v.Z, v.Z));
            }
        }

        [Test]
        public void Normalized_HasUnitLength()
        {
            long[] values = Oracle.RawValues(600, 32).ToArray();
            for (int i = 0; i + 2 < values.Length; i += 3)
            {
                FP3 v = new FP3(FP.FromRaw(values[i]), FP.FromRaw(values[i + 1]), FP.FromRaw(values[i + 2]));

                // Each component is rounded once, so even short vectors normalize to within two raw units of one.
                Assert.That(v == FP3.Zero || System.Math.Abs(v.Normalized.Length.RawValue - FP.OneRaw) <= 2, Is.True, v.ToString());
                Assert.That(v.XZ == FP2.Zero || System.Math.Abs(v.XZ.Normalized.Length.RawValue - FP.OneRaw) <= 2, Is.True, v.ToString());
            }
            Assert.That(FP3.Zero.Normalized, Is.EqualTo(FP3.Zero));
            Assert.That(FP2.Zero.Normalized, Is.EqualTo(FP2.Zero));
        }

        [Test]
        public void Normalized_IsExactForAxesAndTinyVectors()
        {
            Assert.That(new FP3(FP.FromRaw(1), FP.Zero, FP.Zero).Normalized, Is.EqualTo(new FP3(FP.One, FP.Zero, FP.Zero)));
            Assert.That(new FP2(FP.Zero, FP.FromRaw(-3)).Normalized, Is.EqualTo(new FP2(FP.Zero, FP.MinusOne)));
            Assert.That(Oracle.ToDouble(new FP3(FP.MinValue, FP.MinValue, FP.MinValue).Normalized.X), Is.EqualTo(-1 / System.Math.Sqrt(3)).Within(1.0 / FP.OneRaw));
            Assert.That(Oracle.ToDouble(new FP2(FP.FromRaw(1), FP.FromRaw(-1)).Normalized.Y), Is.EqualTo(-1 / System.Math.Sqrt(2)).Within(1.0 / FP.OneRaw));
        }

        [Test]
        public void XZ_ProjectsOntoTheNavMeshPlane()
        {
            FP3 point = new FP3(FP.FromInt(1), FP.FromInt(2), FP.FromInt(3));

            Assert.That(point.XZ, Is.EqualTo(new FP2(FP.FromInt(1), FP.FromInt(3))));
            Assert.That(FP3.FromXZ(point.XZ, point.Y), Is.EqualTo(point));
            Assert.That(FP2.UnitX.Perpendicular, Is.EqualTo(FP2.UnitY));
            Assert.That(FP2.Cross(FP2.UnitX, FP2.UnitY), Is.EqualTo(FP.One));
        }

        private static BigInteger P(FP a, FP b)
        {
            return (BigInteger)a.RawValue * b.RawValue;
        }

        private static long Rounded(BigInteger value)
        {
            return Oracle.Wrap(Oracle.RoundDivide(value, Oracle.One));
        }

        // floor(sqrt(value)), or long.MaxValue when that doesn't fit.
        private static void AssertIsFloorSqrt(long root, BigInteger value)
        {
            BigInteger limit = (BigInteger)long.MaxValue + 1;
            if (value >= limit * limit)
            {
                Assert.That(root, Is.EqualTo(long.MaxValue));
                return;
            }
            BigInteger r = root;
            Assert.That(r * r <= value && (r + 1) * (r + 1) > value, Is.True, "sqrt(" + value + ") gave " + root);
        }
    }
}
