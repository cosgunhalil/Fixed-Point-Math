using System.Linq;
using System.Numerics;
using NUnit.Framework;

namespace FPM.Tests
{
    public class FPMathTests
    {
        // The tables' interpolation error is below 2^-21. The rest is the format's rounding.
        private static readonly double TrigTolerance = 1.0 / (1 << 21) + 1.0 / FP.OneRaw;

        [Test]
        public void Sqrt_IsExactFloor()
        {
            foreach (long raw in Oracle.RawValues(600, 20).Where(v => v > 0))
            {
                BigInteger root = FPMath.Sqrt(FP.FromRaw(raw)).RawValue;
                BigInteger scaled = (BigInteger)raw << FP.FractionalBits;
                Assert.That(root * root <= scaled && (root + 1) * (root + 1) > scaled, Is.True, "sqrt of raw " + raw + " gave raw " + root);
            }
        }

        [Test]
        public void Sqrt_OfZeroAndNegativeIsZero()
        {
            Assert.That(FPMath.Sqrt(FP.Zero), Is.EqualTo(FP.Zero));
            Assert.That(FPMath.Sqrt(-FP.One), Is.EqualTo(FP.Zero));
            Assert.That(FPMath.Sqrt(FP.MinValue), Is.EqualTo(FP.Zero));
            Assert.That(FPMath.Sqrt(FP.FromInt(4)), Is.EqualTo(FP.Two));
        }

        [Test]
        public void SinAndCos_AreWithinTolerance()
        {
            System.Random random = new System.Random(21);
            for (int i = 0; i < 20000; i++)
            {
                FP angle = FP.FromRaw((long)((random.NextDouble() * 200 - 100) * FP.OneRaw));
                double exact = Oracle.ToDouble(angle);

                FPMath.SinCos(angle, out FP sin, out FP cos);
                Assert.That(Oracle.ToDouble(FPMath.Sin(angle)), Is.EqualTo(System.Math.Sin(exact)).Within(TrigTolerance), "sin " + angle);
                Assert.That(Oracle.ToDouble(FPMath.Cos(angle)), Is.EqualTo(System.Math.Cos(exact)).Within(TrigTolerance), "cos " + angle);
                Assert.That(sin, Is.EqualTo(FPMath.Sin(angle)));
                Assert.That(cos, Is.EqualTo(FPMath.Cos(angle)));
            }
        }

        [Test]
        public void SinAndCos_HitTheirExtremes()
        {
            Assert.That(FPMath.Sin(FP.Zero), Is.EqualTo(FP.Zero));
            Assert.That(FPMath.Cos(FP.Zero), Is.EqualTo(FP.One));
            Assert.That(Oracle.ToDouble(FPMath.Sin(FP.HalfPi)), Is.EqualTo(1).Within(TrigTolerance));
            Assert.That(Oracle.ToDouble(FPMath.Sin(-FP.HalfPi)), Is.EqualTo(-1).Within(TrigTolerance));
            Assert.That(FPMath.Sin(FP.HalfPi), Is.LessThanOrEqualTo(FP.One));
            Assert.That(FPMath.Sin(FP.MaxValue).RawValue, Is.InRange(-FP.OneRaw, FP.OneRaw));
            Assert.That(FPMath.Cos(FP.MinValue).RawValue, Is.InRange(-FP.OneRaw, FP.OneRaw));
        }

        [Test]
        public void Atan2_IsWithinToleranceInEveryQuadrant()
        {
            System.Random random = new System.Random(22);
            for (int i = 0; i < 20000; i++)
            {
                double scale = System.Math.Pow(10, random.Next(-3, 5));
                FP y = FP.FromRaw((long)((random.NextDouble() * 2 - 1) * scale * FP.OneRaw));
                FP x = FP.FromRaw((long)((random.NextDouble() * 2 - 1) * scale * FP.OneRaw));
                if (x == FP.Zero && y == FP.Zero)
                {
                    continue;
                }

                double expected = System.Math.Atan2(Oracle.ToDouble(y), Oracle.ToDouble(x));
                Assert.That(Oracle.ToDouble(FPMath.Atan2(y, x)), Is.EqualTo(expected).Within(TrigTolerance), "atan2(" + y + ", " + x + ")");
            }
        }

        [Test]
        public void Atan2_HandlesAxesAndLimits()
        {
            Assert.That(FPMath.Atan2(FP.Zero, FP.Zero), Is.EqualTo(FP.Zero));
            Assert.That(FPMath.Atan2(FP.Zero, FP.One), Is.EqualTo(FP.Zero));
            Assert.That(FPMath.Atan2(FP.One, FP.Zero), Is.EqualTo(FP.HalfPi));
            Assert.That(FPMath.Atan2(-FP.One, FP.Zero), Is.EqualTo(-FP.HalfPi));
            Assert.That(FPMath.Atan2(FP.Zero, -FP.One), Is.EqualTo(FP.Pi));
            Assert.That(Oracle.ToDouble(FPMath.Atan2(FP.MaxValue, FP.MinValue)), Is.EqualTo(3 * System.Math.PI / 4).Within(TrigTolerance));
            Assert.That(Oracle.ToDouble(FPMath.Atan2(FP.Epsilon, FP.MaxValue)), Is.EqualTo(0).Within(TrigTolerance));
        }

        [Test]
        public void Lerp_ClampAndFrac_BehaveAsDocumented()
        {
            Assert.That(FPMath.Lerp(FP.Zero, FP.FromInt(10), FP.Half), Is.EqualTo(FP.FromInt(5)));
            Assert.That(FPMath.Lerp(FP.Zero, FP.FromInt(10), FP.Two), Is.EqualTo(FP.FromInt(20)));
            Assert.That(FPMath.Clamp(FP.FromInt(5), FP.Zero, FP.One), Is.EqualTo(FP.One));
            Assert.That(FPMath.Clamp01(-FP.One), Is.EqualTo(FP.Zero));
            Assert.That(FPMath.Frac(FP.FromRatio(-5, 4)), Is.EqualTo(FP.FromRatio(3, 4)));
            Assert.That(FPMath.Abs(FP.FromInt(-3)), Is.EqualTo(FP.FromInt(3)));
            Assert.That(FPMath.Sign(FP.FromInt(-3)), Is.EqualTo(-1));
        }
    }
}
