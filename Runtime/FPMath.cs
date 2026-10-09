using System.Runtime.CompilerServices;

namespace FPM
{
    // Functions on FP. Everything is integer arithmetic, so results are bit-identical everywhere.
    // - Sqrt is exact: floor(sqrt(x)) of the true value. Rounding down keeps distance heuristics admissible.
    // - Sin, Cos and Atan2 interpolate 1025-entry Q2.30 tables (FPTables). Their error is below 2^-21 plus the
    //   format's resolution, and their results are the same everywhere, which is what matters.
    public static class FPMath
    {
        // A full circle maps to 2^28 positions: 4 quadrants of Steps (2^10) table intervals of 2^16 sub-steps.
        private const int CirclePositionBits = 28;
        private const int QuadrantPositionBits = 26;
        private const long QuadrantPositions = 1L << QuadrantPositionBits;
        private const int SubStepBits = 16;
        private const long SubStepMask = (1L << SubStepBits) - 1;

        // round(2^65 / 2pi), for turning radians into a position on the circle.
        private const long InverseTwoPi = 5871781006564002453L;

        // pi and pi / 2 in Q4.60, for Atan2's quadrants. Rounded to either format they give exactly FP.Pi and FP.HalfPi.
        private const long PiQ60 = 3622009729038561421L;
        private const long HalfPiQ60 = 1811004864519280711L;

        // Atan2 scales its operands below 2^37 so that the tangent position (y << 26) / x fits in 64 bits.
        private const int AtanOperandBits = 37;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Abs(FP value)
        {
            return value.RawValue < 0 ? -value : value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Sign(FP value)
        {
            return value.RawValue > 0 ? 1 : (value.RawValue < 0 ? -1 : 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Min(FP a, FP b)
        {
            return a.RawValue <= b.RawValue ? a : b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Max(FP a, FP b)
        {
            return a.RawValue >= b.RawValue ? a : b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Clamp(FP value, FP min, FP max)
        {
            return value.RawValue < min.RawValue ? min : (value.RawValue > max.RawValue ? max : value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Clamp01(FP value)
        {
            return Clamp(value, FP.Zero, FP.One);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Floor(FP value)
        {
            return FP.FromRaw(value.RawValue & ~FP.FractionMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Ceil(FP value)
        {
            long floor = value.RawValue & ~FP.FractionMask;
            return FP.FromRaw(floor == value.RawValue ? floor : unchecked(floor + FP.OneRaw));
        }

        // Rounds to the nearest integer, ties away from zero.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Round(FP value)
        {
            return FP.FromInt(value.RoundToLong());
        }

        // value - Floor(value), always in [0, 1).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Frac(FP value)
        {
            return FP.FromRaw(value.RawValue & FP.FractionMask);
        }

        // a + (b - a) * t, without clamping t.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Lerp(FP a, FP b, FP t)
        {
            return a + (b - a) * t;
        }

        // a * b / c with an exact intermediate, rounded once. Saturates like FP division.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP MulDiv(FP a, FP b, FP c)
        {
            return FP.FromRaw(FP.MulDivRaw(a.RawValue, b.RawValue, c.RawValue));
        }

        // floor(sqrt(value)), exact. Zero for zero and negative values.
        public static FP Sqrt(FP value)
        {
            long raw = value.RawValue;
            if (raw <= 0)
            {
                return FP.Zero;
            }

            // sqrt(raw / 2^F) * 2^F = sqrt(raw * 2^F).
            if (raw < 1L << (63 - FP.FractionalBits))
            {
                return FP.FromRaw((long)WideInt.SqrtFloor((ulong)raw << FP.FractionalBits));
            }
            ulong high = (ulong)raw >> (64 - FP.FractionalBits);
            ulong low = (ulong)raw << FP.FractionalBits;
            return FP.FromRaw((long)WideInt.SqrtFloor(high, low));
        }

        public static FP Sin(FP radians)
        {
            return FP.FromRaw(Q30ToRaw(SinAtPosition(CirclePosition(radians.RawValue))));
        }

        public static FP Cos(FP radians)
        {
            long position = (CirclePosition(radians.RawValue) + QuadrantPositions) & ((1L << CirclePositionBits) - 1);
            return FP.FromRaw(Q30ToRaw(SinAtPosition(position)));
        }

        public static void SinCos(FP radians, out FP sin, out FP cos)
        {
            long position = CirclePosition(radians.RawValue);
            sin = FP.FromRaw(Q30ToRaw(SinAtPosition(position)));
            long cosPosition = (position + QuadrantPositions) & ((1L << CirclePositionBits) - 1);
            cos = FP.FromRaw(Q30ToRaw(SinAtPosition(cosPosition)));
        }

        // The angle of (x, y) in radians, in (-pi, pi]. Atan2(0, 0) is 0.
        public static FP Atan2(FP y, FP x)
        {
            if (x.RawValue == 0 && y.RawValue == 0)
            {
                return FP.Zero;
            }

            ulong absX = WideInt.UnsignedAbs(x.RawValue);
            ulong absY = WideInt.UnsignedAbs(y.RawValue);
            bool steep = absY > absX;
            ulong adjacent = steep ? absY : absX;
            ulong opposite = steep ? absX : absY;

            int excess = 64 - WideInt.LeadingZeroCount(adjacent) - AtanOperandBits;
            if (excess > 0)
            {
                adjacent >>= excess;
                opposite >>= excess;
            }

            // The octant angle in [0, pi / 4], moved to its quadrant in Q4.60 and rounded once, so the result is as
            // accurate as one rounding allows and the axes give exactly 0, pi / 2 and pi in both formats.
            long angle = InterpolateAtan((long)((opposite << QuadrantPositionBits) / adjacent)) << (60 - FPTables.ValueBits);
            if (steep)
            {
                angle = HalfPiQ60 - angle;
            }
            if (x.RawValue < 0)
            {
                angle = PiQ60 - angle;
            }
            if (y.RawValue < 0)
            {
                angle = -angle;
            }
            return FP.FromRaw(RoundShift(angle, 60 - FP.FractionalBits));
        }

        // The angle's position on the circle in [0, 2^28): the fraction of a turn, radians / 2pi, in 28 bits. It is the
        // bits [37 + F, 65 + F) of radiansRaw * round(2^65 / 2pi), so the whole turns fall off the top and negative
        // angles wrap correctly through two's complement. Unlike reducing by a rounded 2pi, the error doesn't grow
        // with each turn: it stays below one position (2^-28 of a turn) for any angle below 2^38 radians.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long CirclePosition(long radiansRaw)
        {
            WideInt turns = WideInt.Multiply(radiansRaw, InverseTwoPi);
            const int shift = 37 + FP.FractionalBits;
            ulong bits = shift >= 64
                ? unchecked((ulong)turns.High) >> (shift - 64)
                : (turns.Low >> shift) | (unchecked((ulong)turns.High) << (64 - shift));
            return (long)(bits & ((1UL << CirclePositionBits) - 1));
        }

        // sin in Q2.30 at a circle position, from the quarter-wave table.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long SinAtPosition(long position)
        {
            int quadrant = (int)(position >> QuadrantPositionBits);
            long inQuadrant = position & (QuadrantPositions - 1);
            if ((quadrant & 1) != 0)
            {
                inQuadrant = QuadrantPositions - inQuadrant;
            }

            long value = InterpolateSin(inQuadrant);
            return quadrant >= 2 ? -value : value;
        }

        // Linear interpolation of the sin and atan tables at a position in [0, Steps * 2^16]. Burst can read static
        // readonly tables but not take a managed array as a parameter, so each table has its own accessor.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long InterpolateSin(long position)
        {
            int index = (int)(position >> SubStepBits);
            if (index >= FPTables.Steps)
            {
                return FPTables.Sin[FPTables.Steps];
            }
            return Lerp(FPTables.Sin[index], FPTables.Sin[index + 1], position & SubStepMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long InterpolateAtan(long position)
        {
            int index = (int)(position >> SubStepBits);
            if (index >= FPTables.Steps)
            {
                return FPTables.Atan[FPTables.Steps];
            }
            return Lerp(FPTables.Atan[index], FPTables.Atan[index + 1], position & SubStepMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long Lerp(long start, long end, long subStep)
        {
            return start + (((end - start) * subStep) >> SubStepBits);
        }

        // A Q2.30 value in the FP format, rounded to nearest with ties away from zero.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long Q30ToRaw(long q30)
        {
#if FPM_Q32_32
            return q30 << (FP.FractionalBits - FPTables.ValueBits);
#else
            return RoundShift(q30, FPTables.ValueBits - FP.FractionalBits);
#endif
        }

        // value / 2^shift, rounded to nearest with ties away from zero. value must not be long.MinValue.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long RoundShift(long value, int shift)
        {
            long magnitude = value < 0 ? -value : value;
            long rounded = (magnitude + (1L << (shift - 1))) >> shift;
            return value < 0 ? -rounded : rounded;
        }
    }
}
