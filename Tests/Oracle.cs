using System;
using System.Collections.Generic;
using System.Numerics;

namespace FPM.Tests
{
    // Exact reference arithmetic for the numerics tests, on BigInteger. Tests may use any .NET type; only FPM.Runtime
    // itself is held to the determinism rules.
    internal static class Oracle
    {
        public static readonly BigInteger One = BigInteger.One << FP.FractionalBits;
        private static readonly BigInteger TwoTo64 = BigInteger.One << 64;

        // n / d rounded to nearest, ties away from zero.
        public static BigInteger RoundDivide(BigInteger n, BigInteger d)
        {
            BigInteger magnitude = (BigInteger.Abs(n) + BigInteger.Abs(d) / 2) / BigInteger.Abs(d);
            return (n.Sign < 0) != (d.Sign < 0) ? -magnitude : magnitude;
        }

        // The value's low 64 bits as a long, like wrapping integer arithmetic.
        public static long Wrap(BigInteger value)
        {
            BigInteger low = value % TwoTo64;
            if (low < 0)
            {
                low += TwoTo64;
            }
            return low > long.MaxValue ? (long)(low - TwoTo64) : (long)low;
        }

        public static long Saturate(BigInteger value)
        {
            if (value > long.MaxValue)
            {
                return long.MaxValue;
            }
            if (value < long.MinValue)
            {
                return long.MinValue;
            }
            return (long)value;
        }

        public static BigInteger FromWide(WideInt value)
        {
            return ((BigInteger)value.High << 64) + value.Low;
        }

        public static double ToDouble(FP value)
        {
            return (double)value.RawValue / (double)(1L << FP.FractionalBits);
        }

        // Raw values that exercise every path: zero, units, the 2^31 fast-path boundary, format limits, and
        // random values of every bit length. The same seed always gives the same values.
        public static IEnumerable<long> RawValues(int randomCount, int seed)
        {
            long[] edges =
            {
                0, 1, -1, 2, -2, FP.OneRaw, -FP.OneRaw, FP.OneRaw / 2, -FP.OneRaw / 2,
                (1L << 31) - 1, 1L << 31, -(1L << 31), -(1L << 31) - 1,
                1L << 47, -(1L << 47), (1L << 62) + 12345, long.MaxValue, long.MinValue, long.MaxValue - 1, long.MinValue + 1,
            };
            foreach (long edge in edges)
            {
                yield return edge;
            }

            Random random = new Random(seed);
            byte[] bytes = new byte[8];
            for (int i = 0; i < randomCount; i++)
            {
                random.NextBytes(bytes);
                long value = BitConverter.ToInt64(bytes, 0);
                int bits = random.Next(1, 65);
                yield return bits == 64 ? value : value >> (64 - bits);
            }
        }
    }
}
