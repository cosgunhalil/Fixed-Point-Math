using System;
using System.Runtime.CompilerServices;

namespace FPM
{
    // A 2D fixed-point vector. On the navmesh it is the XZ plane.
    // Dot, Cross and LengthSquared sum exact 128-bit products and round once. Length is floor(sqrt) of the exact sum
    // of squares, so it has no intermediate rounding; it saturates only beyond the format's range.
    public readonly struct FP2 : IEquatable<FP2>
    {
        public readonly FP X;
        public readonly FP Y;

        public FP2(FP x, FP y)
        {
            X = x;
            Y = y;
        }

        public static FP2 Zero => new FP2(FP.Zero, FP.Zero);
        public static FP2 One => new FP2(FP.One, FP.One);
        public static FP2 UnitX => new FP2(FP.One, FP.Zero);
        public static FP2 UnitY => new FP2(FP.Zero, FP.One);

        public static FP2 operator +(FP2 a, FP2 b) => new FP2(a.X + b.X, a.Y + b.Y);
        public static FP2 operator -(FP2 a, FP2 b) => new FP2(a.X - b.X, a.Y - b.Y);
        public static FP2 operator -(FP2 value) => new FP2(-value.X, -value.Y);
        public static FP2 operator *(FP2 value, FP scale) => new FP2(value.X * scale, value.Y * scale);
        public static FP2 operator *(FP scale, FP2 value) => new FP2(value.X * scale, value.Y * scale);
        public static FP2 operator /(FP2 value, FP divisor) => new FP2(value.X / divisor, value.Y / divisor);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Dot(FP2 a, FP2 b)
        {
            if (FP.FitsProductSum(a.X.RawValue) && FP.FitsProductSum(a.Y.RawValue) && FP.FitsProductSum(b.X.RawValue) && FP.FitsProductSum(b.Y.RawValue))
            {
                return FP.FromRaw(FP.RoundProductSum(a.X.RawValue * b.X.RawValue + a.Y.RawValue * b.Y.RawValue));
            }
            WideInt sum = WideInt.Multiply(a.X.RawValue, b.X.RawValue) + WideInt.Multiply(a.Y.RawValue, b.Y.RawValue);
            return FP.FromRaw(sum.ShiftRightRounded(FP.FractionalBits));
        }

        // The 2D cross product a.X * b.Y - a.Y * b.X: positive when b is counter-clockwise from a.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Cross(FP2 a, FP2 b)
        {
            if (FP.FitsProductSum(a.X.RawValue) && FP.FitsProductSum(a.Y.RawValue) && FP.FitsProductSum(b.X.RawValue) && FP.FitsProductSum(b.Y.RawValue))
            {
                return FP.FromRaw(FP.RoundProductSum(a.X.RawValue * b.Y.RawValue - a.Y.RawValue * b.X.RawValue));
            }
            WideInt difference = WideInt.Multiply(a.X.RawValue, b.Y.RawValue) - WideInt.Multiply(a.Y.RawValue, b.X.RawValue);
            return FP.FromRaw(difference.ShiftRightRounded(FP.FractionalBits));
        }

        public FP LengthSquared => Dot(this, this);

        public FP Length => FP.FromRaw(VectorMath.Length(X.RawValue, Y.RawValue, 0));

        // The vector scaled to length 1, or zero for the zero vector.
        public FP2 Normalized
        {
            get
            {
                long x = X.RawValue;
                long y = Y.RawValue;
                long z = 0;
                return VectorMath.Normalize(ref x, ref y, ref z) ? new FP2(FP.FromRaw(x), FP.FromRaw(y)) : Zero;
            }
        }

        // Rotated 90 degrees counter-clockwise.
        public FP2 Perpendicular => new FP2(-Y, X);

        public static FP Distance(FP2 a, FP2 b) => (a - b).Length;
        public static FP DistanceSquared(FP2 a, FP2 b) => (a - b).LengthSquared;
        public static FP2 Lerp(FP2 a, FP2 b, FP t) => new FP2(FPMath.Lerp(a.X, b.X, t), FPMath.Lerp(a.Y, b.Y, t));
        public static FP2 Min(FP2 a, FP2 b) => new FP2(FPMath.Min(a.X, b.X), FPMath.Min(a.Y, b.Y));
        public static FP2 Max(FP2 a, FP2 b) => new FP2(FPMath.Max(a.X, b.X), FPMath.Max(a.Y, b.Y));

        public static bool operator ==(FP2 a, FP2 b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(FP2 a, FP2 b) => !(a == b);
        public bool Equals(FP2 other) => this == other;
        public override bool Equals(object obj) => obj is FP2 other && this == other;
        public override int GetHashCode() => unchecked(X.GetHashCode() * 397 ^ Y.GetHashCode());
        public override string ToString() => "(" + X + ", " + Y + ")";
    }
}
