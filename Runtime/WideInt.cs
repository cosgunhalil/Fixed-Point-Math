using System;
using System.Runtime.CompilerServices;

namespace FPM
{
    // A signed 128-bit integer in two's complement, for exact intermediate results that don't fit in a long:
    // products, sums of products and squared lengths. Unity 2021.3 has no System.Int128, and every operation here is
    // plain 64-bit integer arithmetic, so results are the same under Burst, Mono, IL2CPP and .NET.
    public readonly struct WideInt : IEquatable<WideInt>, IComparable<WideInt>
    {
        public readonly long High;
        public readonly ulong Low;

        public WideInt(long high, ulong low)
        {
            High = high;
            Low = low;
        }

        public static WideInt Zero => new WideInt(0, 0);

        public bool IsNegative => High < 0;

        public int Sign => High < 0 ? -1 : (High == 0 && Low == 0 ? 0 : 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WideInt FromLong(long value)
        {
            return new WideInt(value >> 63, unchecked((ulong)value));
        }

        // The exact product of two longs.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WideInt Multiply(long a, long b)
        {
            MultiplyUnsigned(UnsignedAbs(a), UnsignedAbs(b), out ulong high, out ulong low);
            WideInt magnitude = new WideInt(unchecked((long)high), low);
            return (a ^ b) < 0 ? -magnitude : magnitude;
        }

        // The exact 128-bit product of two ulongs, from 32-bit halves.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MultiplyUnsigned(ulong a, ulong b, out ulong high, out ulong low)
        {
            ulong aLow = a & 0xFFFFFFFFUL;
            ulong aHigh = a >> 32;
            ulong bLow = b & 0xFFFFFFFFUL;
            ulong bHigh = b >> 32;

            ulong lowLow = aLow * bLow;
            ulong highLow = aHigh * bLow;
            ulong lowHigh = aLow * bHigh;
            ulong highHigh = aHigh * bHigh;

            ulong middle = (lowLow >> 32) + (highLow & 0xFFFFFFFFUL) + (lowHigh & 0xFFFFFFFFUL);
            low = (middle << 32) | (lowLow & 0xFFFFFFFFUL);
            high = highHigh + (highLow >> 32) + (lowHigh >> 32) + (middle >> 32);
        }

        // |value| as a ulong, which is exact even for long.MinValue (2^63).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong UnsignedAbs(long value)
        {
            return value < 0 ? unchecked((ulong)(-(value + 1)) + 1UL) : (ulong)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WideInt operator +(WideInt a, WideInt b)
        {
            ulong low = unchecked(a.Low + b.Low);
            long carry = low < a.Low ? 1 : 0;
            return new WideInt(unchecked(a.High + b.High + carry), low);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WideInt operator -(WideInt a, WideInt b)
        {
            ulong low = unchecked(a.Low - b.Low);
            long borrow = a.Low < b.Low ? 1 : 0;
            return new WideInt(unchecked(a.High - b.High - borrow), low);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static WideInt operator -(WideInt value)
        {
            ulong low = unchecked(~value.Low + 1UL);
            long carry = low == 0 ? 1 : 0;
            return new WideInt(unchecked(~value.High + carry), low);
        }

        // The magnitude as an unsigned 128-bit value. It is exact for every value, including -2^127.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetMagnitude(out ulong high, out ulong low)
        {
            if (High < 0)
            {
                WideInt negated = -this;
                high = unchecked((ulong)negated.High);
                low = negated.Low;
            }
            else
            {
                high = (ulong)High;
                low = Low;
            }
        }

        // this / 2^shift, rounded to nearest with ties away from zero, wrapped to 64 bits like integer arithmetic.
        // The rounding is symmetric, so negating the input negates the result. shift must be in [1, 63].
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public long ShiftRightRounded(int shift)
        {
            GetMagnitude(out ulong high, out ulong low);
            ulong half = 1UL << (shift - 1);
            ulong roundedLow = unchecked(low + half);
            if (roundedLow < low)
            {
                high = unchecked(high + 1UL);
            }

            ulong magnitude = (roundedLow >> shift) | (high << (64 - shift));
            return High < 0 ? unchecked((long)(0UL - magnitude)) : unchecked((long)magnitude);
        }

        // (high:low) / divisor for an unsigned 128-bit dividend, rounded down. Returns false, with quotient =
        // ulong.MaxValue, when the quotient doesn't fit in 64 bits (high >= divisor). divisor must not be 0.
        public static bool DivideUnsigned(ulong high, ulong low, ulong divisor, out ulong quotient)
        {
            if (high >= divisor)
            {
                quotient = ulong.MaxValue;
                return false;
            }
            quotient = high == 0 ? low / divisor : DivideTwoDigits(high, low, divisor);
            return true;
        }

        // Long division in base 2^32 with two quotient digits (Knuth's algorithm D, as in Hacker's Delight's divlu):
        // the divisor is shifted until its top bit is set, so each estimated digit is at most two too large. high must
        // be below divisor.
        private static ulong DivideTwoDigits(ulong high, ulong low, ulong divisor)
        {
            const ulong Base = 1UL << 32;
            int shift = LeadingZeroCount(divisor);
            divisor <<= shift;
            ulong divisorHigh = divisor >> 32;
            ulong divisorLow = divisor & 0xFFFFFFFFUL;
            ulong top = shift == 0 ? high : (high << shift) | (low >> (64 - shift));
            ulong rest = low << shift;
            ulong restHigh = rest >> 32;
            ulong restLow = rest & 0xFFFFFFFFUL;

            ulong digitHigh = top / divisorHigh;
            ulong remainder = top - digitHigh * divisorHigh;
            while (digitHigh >= Base || digitHigh * divisorLow > ((remainder << 32) | restHigh))
            {
                digitHigh--;
                remainder += divisorHigh;
                if (remainder >= Base)
                {
                    break;
                }
            }

            // The partial remainder fits in 64 bits; the products wrap, but the difference is exact.
            ulong middle = unchecked((top << 32) + restHigh - digitHigh * divisor);
            ulong digitLow = middle / divisorHigh;
            remainder = middle - digitLow * divisorHigh;
            while (digitLow >= Base || digitLow * divisorLow > ((remainder << 32) | restLow))
            {
                digitLow--;
                remainder += divisorHigh;
                if (remainder >= Base)
                {
                    break;
                }
            }
            return (digitHigh << 32) + digitLow;
        }

        // floor(sqrt(value)) for an unsigned 128-bit value. The result always fits in 64 bits. Newton's method from an
        // estimate that is never too small (the root of the top bits, plus one, scaled back) only ever decreases, and
        // stops at the floor of the root.
        public static ulong SqrtFloor(ulong high, ulong low)
        {
            if (high == 0)
            {
                return SqrtFloor(low);
            }

            // value >> (2 * half) keeps the top 63 or 64 bits; sqrt(value) < (sqrt(that) + 1) * 2^half.
            int bits = 128 - LeadingZeroCount(high);
            int half = (bits - 63) >> 1;
            ulong top = half >= 32 ? high >> (2 * half - 64) : (high << (64 - 2 * half)) | (low >> (2 * half));
            ulong rootTop = SqrtFloor(top) + 1;
            ulong estimate = rootTop >= 1UL << (64 - half) ? ulong.MaxValue : rootTop << half;
            while (true)
            {
                // A quotient beyond 64 bits means value / estimate > estimate, so the next estimate wouldn't be lower.
                if (!DivideUnsigned(high, low, estimate, out ulong quotient))
                {
                    return estimate;
                }
                ulong next = (estimate >> 1) + (quotient >> 1) + (estimate & quotient & 1UL);
                if (next >= estimate)
                {
                    return estimate;
                }
                estimate = next;
            }
        }

        // floor(sqrt(value)) for a ulong, by Newton's method from an estimate that is never too small.
        public static ulong SqrtFloor(ulong value)
        {
            if (value < 2)
            {
                return value;
            }

            int shift = (65 - LeadingZeroCount(value)) >> 1;
            ulong estimate = 1UL << shift;
            while (true)
            {
                ulong next = (estimate + value / estimate) >> 1;
                if (next >= estimate)
                {
                    return estimate;
                }
                estimate = next;
            }
        }

        public static int LeadingZeroCount(ulong value)
        {
            if (value == 0)
            {
                return 64;
            }

            int count = 0;
            if ((value & 0xFFFFFFFF00000000UL) == 0) { count += 32; value <<= 32; }
            if ((value & 0xFFFF000000000000UL) == 0) { count += 16; value <<= 16; }
            if ((value & 0xFF00000000000000UL) == 0) { count += 8; value <<= 8; }
            if ((value & 0xF000000000000000UL) == 0) { count += 4; value <<= 4; }
            if ((value & 0xC000000000000000UL) == 0) { count += 2; value <<= 2; }
            if ((value & 0x8000000000000000UL) == 0) { count += 1; }
            return count;
        }

        public static bool operator ==(WideInt a, WideInt b) => a.High == b.High && a.Low == b.Low;
        public static bool operator !=(WideInt a, WideInt b) => !(a == b);
        public static bool operator <(WideInt a, WideInt b) => a.CompareTo(b) < 0;
        public static bool operator >(WideInt a, WideInt b) => a.CompareTo(b) > 0;
        public static bool operator <=(WideInt a, WideInt b) => a.CompareTo(b) <= 0;
        public static bool operator >=(WideInt a, WideInt b) => a.CompareTo(b) >= 0;

        public int CompareTo(WideInt other)
        {
            if (High != other.High)
            {
                return High < other.High ? -1 : 1;
            }
            if (Low != other.Low)
            {
                return Low < other.Low ? -1 : 1;
            }
            return 0;
        }

        public bool Equals(WideInt other) => this == other;
        public override bool Equals(object obj) => obj is WideInt other && this == other;
        public override int GetHashCode() => unchecked((int)Low ^ (int)(Low >> 32) ^ (int)High ^ (int)(High >> 32));
        public override string ToString() => "0x" + High.ToString("X16") + Low.ToString("X16");
    }
}
