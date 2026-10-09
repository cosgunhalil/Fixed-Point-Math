using System;
using UnityEngine;

namespace FPM.Unity
{
    // Conversions between FPM fixed point and Unity floats, for presentation and input only. Float to FP is exact on
    // the machine that does it but not guaranteed to be the same on another, so convert an input once where it
    // comes from (the local player, or the Editor when baking) and send or store the fixed-point value, never the float.
    public static class FPConversions
    {
        public static double ToDouble(this FP value)
        {
            return (double)value.RawValue / FP.OneRaw;
        }

        public static float ToFloat(this FP value)
        {
            return (float)value.ToDouble();
        }

        // Rounds to the nearest representable value, ties away from zero. NaN becomes zero, and values outside
        // the format's range saturate.
        public static FP ToFP(this double value)
        {
            if (double.IsNaN(value))
            {
                return FP.Zero;
            }

            double raw = Math.Round(value * FP.OneRaw, MidpointRounding.AwayFromZero);
            if (raw >= 9.2233720368547758e18)
            {
                return FP.MaxValue;
            }
            if (raw <= -9.2233720368547758e18)
            {
                return FP.MinValue;
            }
            return FP.FromRaw((long)raw);
        }

        public static FP ToFP(this float value)
        {
            return ((double)value).ToFP();
        }

        public static Vector2 ToVector2(this FP2 value)
        {
            return new Vector2(value.X.ToFloat(), value.Y.ToFloat());
        }

        public static Vector3 ToVector3(this FP3 value)
        {
            return new Vector3(value.X.ToFloat(), value.Y.ToFloat(), value.Z.ToFloat());
        }

        public static FP2 ToFP2(this Vector2 value)
        {
            return new FP2(value.x.ToFP(), value.y.ToFP());
        }

        public static FP3 ToFP3(this Vector3 value)
        {
            return new FP3(value.x.ToFP(), value.y.ToFP(), value.z.ToFP());
        }
    }
}
