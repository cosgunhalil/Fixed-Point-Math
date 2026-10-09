using System.Runtime.CompilerServices;

namespace FPM
{
    // Exact length and normalization on raw components, shared by FP2 (with z = 0) and FP3.
    internal static class VectorMath
    {
        // Normalize works on components whose largest has exactly this many bits.
        private const int NormalizedBits = 62;

        // floor(sqrt(x^2 + y^2 + z^2)) in raw units, from the exact sum of squares. Lengths beyond the format's range
        // (up to sqrt(3) * 2^63) saturate to long.MaxValue.
        public static long Length(long x, long y, long z)
        {
            // Components below 2^31 square to at most 2^62 each, and three of those sum within 64 bits.
            if (FP.FitsProductSum(x >> 1) && FP.FitsProductSum(y >> 1) && FP.FitsProductSum(z >> 1))
            {
                return (long)WideInt.SqrtFloor((ulong)(x * x) + (ulong)(y * y) + (ulong)(z * z));
            }
            ulong high = 0;
            ulong low = 0;
            AddSquare(x, ref high, ref low);
            AddSquare(y, ref high, ref low);
            AddSquare(z, ref high, ref low);
            ulong root = WideInt.SqrtFloor(high, low);
            return root > long.MaxValue ? long.MaxValue : (long)root;
        }

        // Scales (x, y, z) to length one, each component rounded once. Returns false, leaving the components alone,
        // for the zero vector. Direction doesn't depend on scale, so the vector is first shifted until its largest
        // component is in [2^61, 2^62): short vectors shift up exactly, and the length then has over 60 significant
        // bits, so the result is as accurate for a vector of a few raw units as for a long one.
        public static bool Normalize(ref long x, ref long y, ref long z)
        {
            ulong largest = WideInt.UnsignedAbs(x);
            ulong absY = WideInt.UnsignedAbs(y);
            ulong absZ = WideInt.UnsignedAbs(z);
            if (absY > largest)
            {
                largest = absY;
            }
            if (absZ > largest)
            {
                largest = absZ;
            }
            if (largest == 0)
            {
                return false;
            }

            int shift = NormalizedBits - (64 - WideInt.LeadingZeroCount(largest));
            if (shift > 0)
            {
                x <<= shift;
                y <<= shift;
                z <<= shift;
            }
            else if (shift < 0)
            {
                x >>= -shift;
                y >>= -shift;
                z >>= -shift;
            }

            // Three squares below 2^124 each sum to below 2^126, so the length is in [2^61, 2^63).
            ulong high = 0;
            ulong low = 0;
            AddSquare(x, ref high, ref low);
            AddSquare(y, ref high, ref low);
            AddSquare(z, ref high, ref low);
            long length = (long)WideInt.SqrtFloor(high, low);

            x = FP.MulDivRaw(x, FP.OneRaw, length);
            y = FP.MulDivRaw(y, FP.OneRaw, length);
            z = FP.MulDivRaw(z, FP.OneRaw, length);
            return true;
        }

        // Adds raw^2 to an unsigned 128-bit sum.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddSquare(long raw, ref ulong high, ref ulong low)
        {
            ulong magnitude = WideInt.UnsignedAbs(raw);
            WideInt.MultiplyUnsigned(magnitude, magnitude, out ulong squareHigh, out ulong squareLow);
            ulong sumLow = unchecked(low + squareLow);
            high = unchecked(high + squareHigh + (sumLow < low ? 1UL : 0UL));
            low = sumLow;
        }
    }
}
