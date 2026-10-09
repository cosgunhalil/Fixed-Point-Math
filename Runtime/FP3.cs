using System;
using System.Runtime.CompilerServices;

namespace FPM
{
    // A 3D fixed-point vector, with Y up as in Unity. Products and lengths are exact like FP2's.
    public readonly struct FP3 : IEquatable<FP3>
    {
        public readonly FP X;
        public readonly FP Y;
        public readonly FP Z;

        public FP3(FP x, FP y, FP z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        // A point from its position on the navmesh plane and its height.
        public static FP3 FromXZ(FP2 xz, FP y) => new FP3(xz.X, y, xz.Y);

        public static FP3 Zero => new FP3(FP.Zero, FP.Zero, FP.Zero);
        public static FP3 One => new FP3(FP.One, FP.One, FP.One);
        public static FP3 Up => new FP3(FP.Zero, FP.One, FP.Zero);

        // The projection onto the navmesh plane: (X, Z).
        public FP2 XZ => new FP2(X, Z);

        public static FP3 operator +(FP3 a, FP3 b) => new FP3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static FP3 operator -(FP3 a, FP3 b) => new FP3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static FP3 operator -(FP3 value) => new FP3(-value.X, -value.Y, -value.Z);
        public static FP3 operator *(FP3 value, FP scale) => new FP3(value.X * scale, value.Y * scale, value.Z * scale);
        public static FP3 operator *(FP scale, FP3 value) => new FP3(value.X * scale, value.Y * scale, value.Z * scale);
        public static FP3 operator /(FP3 value, FP divisor) => new FP3(value.X / divisor, value.Y / divisor, value.Z / divisor);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FP Dot(FP3 a, FP3 b)
        {
            if (FP.FitsProductSum(a.X.RawValue) && FP.FitsProductSum(a.Y.RawValue) && FP.FitsProductSum(a.Z.RawValue)
                && FP.FitsProductSum(b.X.RawValue) && FP.FitsProductSum(b.Y.RawValue) && FP.FitsProductSum(b.Z.RawValue))
            {
                return FP.FromRaw(FP.RoundProductSum(a.X.RawValue * b.X.RawValue + a.Y.RawValue * b.Y.RawValue + a.Z.RawValue * b.Z.RawValue));
            }
            WideInt sum = WideInt.Multiply(a.X.RawValue, b.X.RawValue)
                + WideInt.Multiply(a.Y.RawValue, b.Y.RawValue)
                + WideInt.Multiply(a.Z.RawValue, b.Z.RawValue);
            return FP.FromRaw(sum.ShiftRightRounded(FP.FractionalBits));
        }

        public static FP3 Cross(FP3 a, FP3 b)
        {
            return new FP3(
                CrossComponent(a.Y.RawValue, b.Z.RawValue, a.Z.RawValue, b.Y.RawValue),
                CrossComponent(a.Z.RawValue, b.X.RawValue, a.X.RawValue, b.Z.RawValue),
                CrossComponent(a.X.RawValue, b.Y.RawValue, a.Y.RawValue, b.X.RawValue));
        }

        public FP LengthSquared => Dot(this, this);

        public FP Length => FP.FromRaw(VectorMath.Length(X.RawValue, Y.RawValue, Z.RawValue));

        // The vector scaled to length 1, or zero for the zero vector.
        public FP3 Normalized
        {
            get
            {
                long x = X.RawValue;
                long y = Y.RawValue;
                long z = Z.RawValue;
                return VectorMath.Normalize(ref x, ref y, ref z) ? new FP3(FP.FromRaw(x), FP.FromRaw(y), FP.FromRaw(z)) : Zero;
            }
        }

        public static FP Distance(FP3 a, FP3 b) => (a - b).Length;
        public static FP DistanceSquared(FP3 a, FP3 b) => (a - b).LengthSquared;
        public static FP3 Lerp(FP3 a, FP3 b, FP t) => new FP3(FPMath.Lerp(a.X, b.X, t), FPMath.Lerp(a.Y, b.Y, t), FPMath.Lerp(a.Z, b.Z, t));
        public static FP3 Min(FP3 a, FP3 b) => new FP3(FPMath.Min(a.X, b.X), FPMath.Min(a.Y, b.Y), FPMath.Min(a.Z, b.Z));
        public static FP3 Max(FP3 a, FP3 b) => new FP3(FPMath.Max(a.X, b.X), FPMath.Max(a.Y, b.Y), FPMath.Max(a.Z, b.Z));

        // a * b - c * d, from exact products, rounded once.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static FP CrossComponent(long a, long b, long c, long d)
        {
            if (FP.FitsProductSum(a) && FP.FitsProductSum(b) && FP.FitsProductSum(c) && FP.FitsProductSum(d))
            {
                return FP.FromRaw(FP.RoundProductSum(a * b - c * d));
            }
            WideInt difference = WideInt.Multiply(a, b) - WideInt.Multiply(c, d);
            return FP.FromRaw(difference.ShiftRightRounded(FP.FractionalBits));
        }

        public static bool operator ==(FP3 a, FP3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        public static bool operator !=(FP3 a, FP3 b) => !(a == b);
        public bool Equals(FP3 other) => this == other;
        public override bool Equals(object obj) => obj is FP3 other && this == other;
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode());
        public override string ToString() => "(" + X + ", " + Y + ", " + Z + ")";
    }
}
