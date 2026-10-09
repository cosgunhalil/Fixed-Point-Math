using System;
using System.Runtime.CompilerServices;

namespace FPM
{
    // A fixed-point number: RawValue / 2^FractionalBits, stored in a long. Q48.16 by default; the scripting define
    // FPM_Q32_32 selects Q32.32 for the whole project. The API is the same in both formats.
    //
    // Arithmetic rules, identical on every platform:
    // - + and - wrap on overflow, like integer arithmetic.
    // - * and / are exact before rounding once, to nearest with ties away from zero, so they are symmetric:
    //   (-a) * b == -(a * b). * wraps on overflow.
    // - / never throws: x / 0 is MaxValue, MinValue or 0 by the sign of x, and quotients that don't fit saturate.
    public readonly struct FP : IEquatable<FP>, IComparable<FP>
    {
#if FPM_Q32_32
        public const int FractionalBits = 32;
        internal const long PiRaw = 13493037705L;
        internal const long TwoPiRaw = 26986075409L;
        internal const long HalfPiRaw = 6746518852L;
        private const int DecimalDigits = 10;
#else
        public const int FractionalBits = 16;
        internal const long PiRaw = 205887L;
        internal const long TwoPiRaw = 411775L;
        internal const long HalfPiRaw = 102944L;
        private const int DecimalDigits = 5;
#endif

        public const long OneRaw = 1L << FractionalBits;
        internal const long FractionMask = OneRaw - 1;

        // Operands below 2^31 in raw value multiply exactly in 64 bits.
        private const long SmallOperandLimit = 1L << 31;

        // Below 2^30, the products of up to three pairs sum exactly in 64 bits (dot and cross products).
        private const long ProductSumLimit = 1L << 30;

        public readonly long RawValue;

        private FP(long rawValue)
        {
            RawValue = rawValue;
        }

        public static FP Zero => new FP(0);
        public static FP One => new FP(OneRaw);
        public static FP MinusOne => new FP(-OneRaw);
        public static FP Two => new FP(OneRaw << 1);
        public static FP Half => new FP(OneRaw >> 1);
        public static FP Pi => new FP(PiRaw);
        public static FP TwoPi => new FP(TwoPiRaw);
        public static FP HalfPi => new FP(HalfPiRaw);
        public static FP Epsilon => new FP(1);
        public static FP MaxValue => new FP(long.MaxValue);
        public static FP MinValue => new FP(long.MinValue);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FromRaw(long rawValue)
        {
            return new FP(rawValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP FromInt(long value)
        {
            return new FP(unchecked(value << FractionalBits));
        }

        // numerator / denominator, rounded to nearest: the exact way to write constants such as FromRatio(3, 10).
        public static FP FromRatio(long numerator, long denominator)
        {
            return new FP(MulDivRaw(numerator, OneRaw, denominator));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator FP(int value)
        {
            return FromInt(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator +(FP a, FP b)
        {
            return new FP(unchecked(a.RawValue + b.RawValue));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator -(FP a, FP b)
        {
            return new FP(unchecked(a.RawValue - b.RawValue));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator -(FP value)
        {
            return new FP(unchecked(-value.RawValue));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator *(FP a, FP b)
        {
            return new FP(MultiplyRaw(a.RawValue, b.RawValue));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator /(FP a, FP b)
        {
            return new FP(DivideRaw(a.RawValue, b.RawValue));
        }

        // The remainder of a / b with the sign of a, like the integer %. x % 0 is 0.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP operator %(FP a, FP b)
        {
            if (b.RawValue == 0 || b.RawValue == -1)
            {
                return Zero;
            }
            return new FP(a.RawValue % b.RawValue);
        }

        // round(a * b / 2^F) for raw values.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long MultiplyRaw(long a, long b)
        {
            if (unchecked((ulong)(a + SmallOperandLimit)) < (ulong)(2 * SmallOperandLimit)
                && unchecked((ulong)(b + SmallOperandLimit)) < (ulong)(2 * SmallOperandLimit))
            {
                long product = a * b;
                long magnitude = product < 0 ? -product : product;
                long rounded = (magnitude + (1L << (FractionalBits - 1))) >> FractionalBits;
                return product < 0 ? -rounded : rounded;
            }
            return WideInt.Multiply(a, b).ShiftRightRounded(FractionalBits);
        }

        // Whether raw is small enough for SumOfProducts' fast path.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool FitsProductSum(long raw)
        {
            return unchecked((ulong)(raw + ProductSumLimit)) < (ulong)(2 * ProductSumLimit);
        }

        // An exact sum of products of operands that pass FitsProductSum, divided by 2^F and rounded as
        // WideInt.ShiftRightRounded does (to nearest, ties away from zero), which it then equals exactly.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long RoundProductSum(long sum)
        {
            long magnitude = sum < 0 ? -sum : sum;
            long rounded = (magnitude + (1L << (FractionalBits - 1))) >> FractionalBits;
            return sum < 0 ? -rounded : rounded;
        }

        // round(a * 2^F / b) for raw values, saturating instead of overflowing or dividing by zero.
        internal static long DivideRaw(long a, long b)
        {
            if (b == 0)
            {
                return a > 0 ? long.MaxValue : (a < 0 ? long.MinValue : 0);
            }

            bool negative = (a ^ b) < 0;
            ulong dividend = WideInt.UnsignedAbs(a);
            ulong divisor = WideInt.UnsignedAbs(b);
            ulong quotient;
            if (dividend < 1UL << (63 - FractionalBits))
            {
                quotient = ((dividend << FractionalBits) + (divisor >> 1)) / divisor;
            }
            else
            {
                ulong high = dividend >> (64 - FractionalBits);
                ulong low = dividend << FractionalBits;
                ulong roundedLow = unchecked(low + (divisor >> 1));
                if (roundedLow < low)
                {
                    high++;
                }
                WideInt.DivideUnsigned(high, roundedLow, divisor, out quotient);
            }
            return SaturateSigned(quotient, negative);
        }

        // round(a * b / c) with an exact 128-bit intermediate, saturating like DivideRaw.
        internal static long MulDivRaw(long a, long b, long c)
        {
            WideInt product = WideInt.Multiply(a, b);
            if (c == 0)
            {
                int sign = product.Sign;
                return sign > 0 ? long.MaxValue : (sign < 0 ? long.MinValue : 0);
            }

            bool negative = product.IsNegative != (c < 0);
            product.GetMagnitude(out ulong high, out ulong low);
            ulong divisor = WideInt.UnsignedAbs(c);
            ulong roundedLow = unchecked(low + (divisor >> 1));
            if (roundedLow < low)
            {
                high = unchecked(high + 1UL);
            }
            WideInt.DivideUnsigned(high, roundedLow, divisor, out ulong quotient);
            return SaturateSigned(quotient, negative);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long SaturateSigned(ulong magnitude, bool negative)
        {
            if (negative)
            {
                return magnitude >= 1UL << 63 ? long.MinValue : -(long)magnitude;
            }
            return magnitude > long.MaxValue ? long.MaxValue : (long)magnitude;
        }

        // Integer conversions. Floor rounds towards negative infinity, Round rounds ties away from zero.
        public long FloorToLong() => RawValue >> FractionalBits;
        public long CeilToLong() => (RawValue >> FractionalBits) + ((RawValue & FractionMask) != 0 ? 1 : 0);

        public long RoundToLong()
        {
            long rounded = (long)((WideInt.UnsignedAbs(RawValue) + (ulong)(OneRaw >> 1)) >> FractionalBits);
            return RawValue < 0 ? -rounded : rounded;
        }

        public int FloorToInt() => (int)FloorToLong();
        public int CeilToInt() => (int)CeilToLong();
        public int RoundToInt() => (int)RoundToLong();

        public static bool operator ==(FP a, FP b) => a.RawValue == b.RawValue;
        public static bool operator !=(FP a, FP b) => a.RawValue != b.RawValue;
        public static bool operator <(FP a, FP b) => a.RawValue < b.RawValue;
        public static bool operator >(FP a, FP b) => a.RawValue > b.RawValue;
        public static bool operator <=(FP a, FP b) => a.RawValue <= b.RawValue;
        public static bool operator >=(FP a, FP b) => a.RawValue >= b.RawValue;

        public int CompareTo(FP other) => RawValue < other.RawValue ? -1 : (RawValue > other.RawValue ? 1 : 0);
        public bool Equals(FP other) => RawValue == other.RawValue;
        public override bool Equals(object obj) => obj is FP other && RawValue == other.RawValue;
        public override int GetHashCode() => unchecked((int)RawValue ^ (int)(RawValue >> 32));

        // The exact value rounded to DecimalDigits places, with trailing zeros removed, and no culture formatting.
        public override string ToString()
        {
            ulong magnitude = WideInt.UnsignedAbs(RawValue);
            ulong integer = magnitude >> FractionalBits;
            ulong fraction = magnitude & (ulong)FractionMask;

            ulong scale = 1;
            for (int i = 0; i < DecimalDigits; i++)
            {
                scale *= 10;
            }

            // fraction * 10^digits / 2^F, rounded to nearest, from an exact 128-bit product.
            WideInt.MultiplyUnsigned(fraction, scale, out ulong high, out ulong low);
            ulong roundedLow = unchecked(low + (1UL << (FractionalBits - 1)));
            if (roundedLow < low)
            {
                high++;
            }
            ulong digits = (roundedLow >> FractionalBits) | (high << (64 - FractionalBits));
            if (digits >= scale)
            {
                digits -= scale;
                integer++;
            }

            string text = (RawValue < 0 ? "-" : "") + integer.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (digits == 0)
            {
                return text;
            }

            string fractionText = digits.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(DecimalDigits, '0').TrimEnd('0');
            return text + "." + fractionText;
        }
    }
}
