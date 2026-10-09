using System.Linq;
using System.Numerics;
using NUnit.Framework;

namespace FPM.Tests
{
    public class WideIntTests
    {
        private static readonly BigInteger TwoTo128 = BigInteger.One << 128;

        [Test]
        public void Multiply_MatchesBigInteger()
        {
            long[] values = Oracle.RawValues(300, 1).ToArray();
            foreach (long a in values)
            {
                foreach (long b in values.Take(60))
                {
                    Assert.That(Oracle.FromWide(WideInt.Multiply(a, b)), Is.EqualTo((BigInteger)a * b), a + " * " + b);
                }
            }
        }

        [Test]
        public void AddSubtractNegate_WrapModulo2To128()
        {
            long[] values = Oracle.RawValues(200, 2).ToArray();
            for (int i = 0; i + 3 < values.Length; i += 4)
            {
                WideInt x = WideInt.Multiply(values[i], values[i + 1]);
                WideInt y = WideInt.Multiply(values[i + 2], values[i + 3]);
                BigInteger bx = Oracle.FromWide(x);
                BigInteger by = Oracle.FromWide(y);

                Assert.That(Oracle.FromWide(x + y), Is.EqualTo(ToSigned128(bx + by)));
                Assert.That(Oracle.FromWide(x - y), Is.EqualTo(ToSigned128(bx - by)));
                Assert.That(Oracle.FromWide(-x), Is.EqualTo(ToSigned128(-bx)));
                Assert.That(x.CompareTo(y), Is.EqualTo(bx.CompareTo(by)));
            }
        }

        [Test]
        public void ShiftRightRounded_RoundsTiesAwayFromZeroAndWraps()
        {
            long[] values = Oracle.RawValues(200, 3).ToArray();
            for (int i = 0; i + 1 < values.Length; i += 2)
            {
                WideInt product = WideInt.Multiply(values[i], values[i + 1]);
                foreach (int shift in new[] { 1, 16, 32, 63 })
                {
                    BigInteger expected = Oracle.RoundDivide(Oracle.FromWide(product), BigInteger.One << shift);
                    Assert.That(product.ShiftRightRounded(shift), Is.EqualTo(Oracle.Wrap(expected)), product + " >> " + shift);
                }
            }
        }

        [Test]
        public void DivideUnsigned_MatchesBigInteger()
        {
            long[] values = Oracle.RawValues(300, 4).ToArray();
            for (int i = 0; i + 2 < values.Length; i += 3)
            {
                ulong divisor = (ulong)values[i] | 1UL;
                ulong high = (ulong)values[i + 1] % divisor;
                ulong low = (ulong)values[i + 2];
                BigInteger dividend = ((BigInteger)high << 64) + low;

                Assert.That(WideInt.DivideUnsigned(high, low, divisor, out ulong quotient), Is.True);
                Assert.That((BigInteger)quotient, Is.EqualTo(dividend / divisor));
            }
        }

        // Divisors of every bit length (the two-digit division normalises them) and dividends of every size below them.
        [Test]
        public void DivideUnsigned_MatchesBigIntegerForEveryDivisorSize()
        {
            System.Random random = new System.Random(41);
            for (int bits = 1; bits <= 64; bits++)
            {
                for (int i = 0; i < 400; i++)
                {
                    ulong divisor = Next(random, bits) | (1UL << (bits - 1));
                    ulong high = i % 4 == 0 ? divisor - 1 : Next(random, 64) % divisor;
                    ulong low = i % 7 == 0 ? ulong.MaxValue : Next(random, 64);
                    if (i % 5 == 0)
                    {
                        high = 0;
                    }
                    BigInteger dividend = ((BigInteger)high << 64) + low;
                    Assert.That(WideInt.DivideUnsigned(high, low, divisor, out ulong quotient), Is.True);
                    Assert.That((BigInteger)quotient, Is.EqualTo(dividend / divisor), dividend + " / " + divisor);
                }
            }
        }

        // Squares of every size, one below and one above, and random values of every bit length.
        [Test]
        public void SqrtFloor_IsExactAroundSquaresOfEverySize()
        {
            System.Random random = new System.Random(43);
            for (int bits = 1; bits <= 64; bits++)
            {
                for (int i = 0; i < 200; i++)
                {
                    BigInteger root = Next(random, bits) | (1UL << (bits - 1));
                    foreach (BigInteger value in new[] { root * root - 1, root * root, root * root + 1, root * root + 2 * root })
                    {
                        if (value < 0 || value >= TwoTo128)
                        {
                            continue;
                        }
                        ulong high = (ulong)(value >> 64);
                        ulong low = (ulong)(value & ulong.MaxValue);
                        AssertIsFloorSqrt(WideInt.SqrtFloor(high, low), value);
                    }
                }
            }
            for (int bits = 65; bits <= 128; bits++)
            {
                for (int i = 0; i < 200; i++)
                {
                    ulong high = Next(random, bits - 64) | (1UL << (bits - 65));
                    ulong low = Next(random, 64);
                    AssertIsFloorSqrt(WideInt.SqrtFloor(high, low), ((BigInteger)high << 64) + low);
                }
            }
        }

        private static ulong Next(System.Random random, int bits)
        {
            ulong value = ((ulong)(uint)random.Next() << 33) ^ ((ulong)(uint)random.Next() << 11) ^ (uint)random.Next();
            return bits >= 64 ? value : value & ((1UL << bits) - 1);
        }

        [Test]
        public void DivideUnsigned_ReportsOverflow()
        {
            Assert.That(WideInt.DivideUnsigned(5, 0, 5, out ulong quotient), Is.False);
            Assert.That(quotient, Is.EqualTo(ulong.MaxValue));
        }

        [Test]
        public void SqrtFloor_IsExactFloorFor64And128Bits()
        {
            long[] values = Oracle.RawValues(400, 5).ToArray();
            for (int i = 0; i + 1 < values.Length; i += 2)
            {
                ulong high = (ulong)values[i];
                ulong low = (ulong)values[i + 1];
                AssertIsFloorSqrt(WideInt.SqrtFloor(high, low), ((BigInteger)high << 64) + low);
                AssertIsFloorSqrt(WideInt.SqrtFloor(low), low);
            }

            AssertIsFloorSqrt(WideInt.SqrtFloor(ulong.MaxValue, ulong.MaxValue), TwoTo128 - 1);
            AssertIsFloorSqrt(WideInt.SqrtFloor(ulong.MaxValue), ulong.MaxValue);
            AssertIsFloorSqrt(WideInt.SqrtFloor(0), 0);
        }

        [Test]
        public void LeadingZeroCount_CountsFromTheTopBit()
        {
            Assert.That(WideInt.LeadingZeroCount(0), Is.EqualTo(64));
            for (int bit = 0; bit < 64; bit++)
            {
                Assert.That(WideInt.LeadingZeroCount(1UL << bit), Is.EqualTo(63 - bit));
                Assert.That(WideInt.LeadingZeroCount((1UL << bit) | 1UL), Is.EqualTo(63 - bit));
            }
        }

        [Test]
        public void UnsignedAbs_IsExactForMinValue()
        {
            Assert.That(WideInt.UnsignedAbs(long.MinValue), Is.EqualTo(1UL << 63));
            Assert.That(WideInt.UnsignedAbs(-5), Is.EqualTo(5UL));
        }

        private static void AssertIsFloorSqrt(ulong root, BigInteger value)
        {
            BigInteger r = root;
            Assert.That(r * r <= value && (r + 1) * (r + 1) > value, Is.True, "sqrt(" + value + ") gave " + root);
        }

        private static BigInteger ToSigned128(BigInteger value)
        {
            BigInteger wrapped = value % TwoTo128;
            if (wrapped < 0)
            {
                wrapped += TwoTo128;
            }
            return wrapped >= TwoTo128 / 2 ? wrapped - TwoTo128 : wrapped;
        }
    }
}
