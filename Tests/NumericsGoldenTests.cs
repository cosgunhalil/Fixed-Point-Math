using NUnit.Framework;

namespace FPM.Tests
{
    // Pins the exact results of a long, mixed sequence of numerics operations. Every machine, platform and scripting
    // backend must produce these hashes. A failure means results changed: only update the pin when the change is
    // intended and agreed, because it changes what existing games compute.
    public class NumericsGoldenTests
    {
#if FPM_Q32_32
        private const ulong ExpectedHash = 0x5805273C9AB18EACUL;
#else
        private const ulong ExpectedHash = 0x461F01E4D178AC7FUL;
#endif

        [Test]
        public void MixedOperationSequence_MatchesPinnedHash()
        {
            Assert.That(ComputeHash().ToString("X16"), Is.EqualTo(ExpectedHash.ToString("X16")));
        }

        internal static ulong ComputeHash()
        {
            DeterministicRandom random = new DeterministicRandom(20260929, 1);
            ulong hash = 0xCBF29CE484222325UL;
            for (int i = 0; i < 20000; i++)
            {
                FP a = random.NextFP(FP.FromInt(-20000), FP.FromInt(20000));
                FP b = random.NextFP(FP.FromInt(-100), FP.FromInt(100));
                FP c = FP.FromRaw(((long)random.NextUInt() << 32) | random.NextUInt());
                FP3 u = new FP3(a, b, a - b);
                FP3 v = new FP3(b, c, a);

                hash = Mix(hash, (a * b).RawValue);
                hash = Mix(hash, (a / b).RawValue);
                hash = Mix(hash, (c * b).RawValue);
                hash = Mix(hash, (c / a).RawValue);
                hash = Mix(hash, FPMath.MulDiv(a, c, b).RawValue);
                hash = Mix(hash, FPMath.Sqrt(FPMath.Abs(c)).RawValue);
                hash = Mix(hash, FPMath.Sin(a).RawValue);
                hash = Mix(hash, FPMath.Cos(b).RawValue);
                hash = Mix(hash, FPMath.Atan2(b, a).RawValue);
                hash = Mix(hash, FP3.Dot(u, v).RawValue);
                hash = Mix(hash, FP3.Cross(u, v).Y.RawValue);
                hash = Mix(hash, u.Length.RawValue);
                hash = Mix(hash, u.Normalized.X.RawValue);
                hash = Mix(hash, FP2.Cross(u.XZ, v.XZ).RawValue);
                hash = Mix(hash, random.NextUInt(1000));
            }
            return hash;
        }

        private static ulong Mix(ulong hash, long value)
        {
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(value >> shift);
                hash *= 0x100000001B3UL;
            }
            return hash;
        }
    }
}
