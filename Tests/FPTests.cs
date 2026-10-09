using System.Globalization;
using System.Linq;
using System.Numerics;
using NUnit.Framework;

namespace FPM.Tests
{
    public class FPTests
    {
        [Test]
        public void Multiply_IsExactProductRoundedOnce()
        {
            long[] values = Oracle.RawValues(400, 10).ToArray();
            foreach (long a in values)
            {
                foreach (long b in values.Take(80))
                {
                    long expected = Oracle.Wrap(Oracle.RoundDivide((BigInteger)a * b, Oracle.One));
                    Assert.That((FP.FromRaw(a) * FP.FromRaw(b)).RawValue, Is.EqualTo(expected), a + " * " + b);
                }
            }
        }

        [Test]
        public void Multiply_IsSymmetricUnderNegation()
        {
            long[] values = Oracle.RawValues(200, 11).Where(v => v != long.MinValue).ToArray();
            for (int i = 0; i + 1 < values.Length; i += 2)
            {
                FP a = FP.FromRaw(values[i]);
                FP b = FP.FromRaw(values[i + 1]);
                Assert.That((-a * b).RawValue, Is.EqualTo((-(a * b)).RawValue));
            }
        }

        [Test]
        public void Divide_IsExactQuotientRoundedOnceAndSaturates()
        {
            long[] values = Oracle.RawValues(400, 12).ToArray();
            foreach (long a in values)
            {
                foreach (long b in values.Take(80))
                {
                    long expected = b == 0
                        ? (a > 0 ? long.MaxValue : (a < 0 ? long.MinValue : 0))
                        : Oracle.Saturate(Oracle.RoundDivide((BigInteger)a * Oracle.One, b));
                    Assert.That((FP.FromRaw(a) / FP.FromRaw(b)).RawValue, Is.EqualTo(expected), a + " / " + b);
                }
            }
        }

        [Test]
        public void MulDiv_IsExactAndRoundedOnce()
        {
            long[] values = Oracle.RawValues(150, 13).ToArray();
            for (int i = 0; i + 2 < values.Length; i++)
            {
                long a = values[i];
                long b = values[i + 1];
                long c = values[i + 2];
                BigInteger product = (BigInteger)a * b;
                long expected = c == 0
                    ? (product.Sign > 0 ? long.MaxValue : (product.Sign < 0 ? long.MinValue : 0))
                    : Oracle.Saturate(Oracle.RoundDivide(product, c));
                Assert.That(FPMath.MulDiv(FP.FromRaw(a), FP.FromRaw(b), FP.FromRaw(c)).RawValue, Is.EqualTo(expected), a + " * " + b + " / " + c);
            }
        }

        [Test]
        public void FromRatio_RoundsToNearest()
        {
            Assert.That(FP.FromRatio(1, 3).RawValue, Is.EqualTo((long)Oracle.RoundDivide(Oracle.One, 3)));
            Assert.That(FP.FromRatio(-2, 3).RawValue, Is.EqualTo((long)Oracle.RoundDivide(-2 * Oracle.One, 3)));
            Assert.That(FP.FromRatio(3, 2), Is.EqualTo(FP.One + FP.Half));
            Assert.That(FP.FromRatio(5, 0), Is.EqualTo(FP.MaxValue));
        }

        [Test]
        public void Remainder_NeverThrows()
        {
            Assert.That(FP.MinValue % FP.FromRaw(-1), Is.EqualTo(FP.Zero));
            Assert.That(FP.One % FP.Zero, Is.EqualTo(FP.Zero));
            Assert.That(FP.FromInt(7) % FP.FromInt(3), Is.EqualTo(FP.One));
            Assert.That(FP.FromInt(-7) % FP.FromInt(3), Is.EqualTo(FP.MinusOne));
        }

        [TestCase(5, 2, 2, 3, 3)]
        [TestCase(-5, 2, -3, -2, -3)]
        [TestCase(9, 4, 2, 3, 2)]
        [TestCase(-9, 4, -3, -2, -2)]
        [TestCase(4, 1, 4, 4, 4)]
        public void IntegerConversions_RoundInTheirDirection(int numerator, int denominator, int floor, int ceil, int round)
        {
            FP value = FP.FromRatio(numerator, denominator);

            Assert.That(value.FloorToInt(), Is.EqualTo(floor));
            Assert.That(value.CeilToInt(), Is.EqualTo(ceil));
            Assert.That(value.RoundToInt(), Is.EqualTo(round));
            Assert.That(FPMath.Floor(value), Is.EqualTo(FP.FromInt(floor)));
            Assert.That(FPMath.Ceil(value), Is.EqualTo(FP.FromInt(ceil)));
            Assert.That(FPMath.Round(value), Is.EqualTo(FP.FromInt(round)));
        }

        [Test]
        public void IntegerConversions_HandleFormatLimits()
        {
            Assert.That(FP.MaxValue.CeilToLong(), Is.EqualTo((long.MaxValue >> FP.FractionalBits) + 1));
            Assert.That(FP.MinValue.FloorToLong(), Is.EqualTo(long.MinValue >> FP.FractionalBits));
            Assert.That(FP.MinValue.RoundToLong(), Is.EqualTo(long.MinValue >> FP.FractionalBits));
        }

        [TestCase(3, 2, "1.5")]
        [TestCase(-1, 4, "-0.25")]
        [TestCase(3, 1, "3")]
        [TestCase(0, 1, "0")]
        [TestCase(-7, 2, "-3.5")]
        public void ToString_WritesExactShortValues(int numerator, int denominator, string expected)
        {
            Assert.That(FP.FromRatio(numerator, denominator).ToString(), Is.EqualTo(expected));
        }

        [Test]
        public void ToString_IsTheValueRoundedToItsDigits()
        {
            foreach (long raw in Oracle.RawValues(300, 14).Where(v => v != long.MinValue))
            {
                FP value = FP.FromRaw(raw);
                decimal written = decimal.Parse(value.ToString(), CultureInfo.InvariantCulture);
                decimal exact = (decimal)raw / (decimal)Oracle.One;
                int digits = FP.FractionalBits == 16 ? 5 : 10;
                decimal tolerance = 0.5m / (decimal)BigInteger.Pow(10, digits) + 0.0000000000001m;
                Assert.That(System.Math.Abs(written - exact), Is.LessThanOrEqualTo(tolerance), raw + " was written as " + value);
            }
        }

        [Test]
        public void Constants_AreTheirValuesRounded()
        {
            Assert.That(Oracle.ToDouble(FP.Pi), Is.EqualTo(System.Math.PI).Within(1.0 / FP.OneRaw));
            Assert.That(FP.TwoPi.RawValue - 2 * FP.Pi.RawValue, Is.InRange(-1, 1));
            Assert.That(2 * FP.HalfPi.RawValue - FP.Pi.RawValue, Is.InRange(-1, 1));
            Assert.That(FP.One.RawValue, Is.EqualTo(1L << FP.FractionalBits));
        }
    }
}
