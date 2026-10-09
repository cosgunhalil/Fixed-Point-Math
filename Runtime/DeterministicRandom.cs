namespace FPM
{
    // PCG32 (XSH RR, O'Neill 2014): a small, fast generator whose output is fixed by its specification. It has 16 bytes
    // of state and nothing hidden, so it snapshots, restores and hashes with the rest of the world.
    // Ranges use Lemire's multiply-and-reject method, so they are unbiased and still identical everywhere.
    public struct DeterministicRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;

        public ulong State;
        public ulong Increment;

        // Seeds like the reference pcg32_srandom_r: stream selects one of 2^63 independent sequences.
        public DeterministicRandom(ulong seed, ulong stream = 0)
        {
            State = 0;
            Increment = (stream << 1) | 1UL;
            NextUInt();
            State = unchecked(State + seed);
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = State;
            State = unchecked(old * Multiplier + Increment);
            uint shifted = (uint)(((old >> 18) ^ old) >> 27);
            int rotation = (int)(old >> 59);
            return (shifted >> rotation) | (shifted << (-rotation & 31));
        }

        // A value in [0, bound). Returns 0 when bound is 0.
        public uint NextUInt(uint bound)
        {
            if (bound == 0)
            {
                return 0;
            }

            ulong product = (ulong)NextUInt() * bound;
            uint low = (uint)product;
            if (low < bound)
            {
                uint threshold = unchecked(0U - bound) % bound;
                while (low < threshold)
                {
                    product = (ulong)NextUInt() * bound;
                    low = (uint)product;
                }
            }
            return (uint)(product >> 32);
        }

        public ulong NextULong()
        {
            ulong high = NextUInt();
            return (high << 32) | NextUInt();
        }

        // A value in [0, bound), unbiased: a draw from an incomplete last bucket of the 64-bit range is rejected.
        // Returns 0 when bound is 0 or less.
        public long NextLong(long bound)
        {
            if (bound <= 0)
            {
                return 0;
            }
            ulong range = (ulong)bound;
            while (true)
            {
                ulong value = NextULong();
                ulong remainder = value % range;
                if (value - remainder <= unchecked(0UL - range))
                {
                    return (long)remainder;
                }
            }
        }

        // A value in [minInclusive, maxExclusive). Returns minInclusive when the range is empty.
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }
            uint range = unchecked((uint)(maxExclusive - minInclusive));
            return unchecked(minInclusive + (int)NextUInt(range));
        }

        // A value in [0, 1) with every representable step equally likely.
        public FP NextFP()
        {
#if FPM_Q32_32
            return FP.FromRaw(NextUInt());
#else
            return FP.FromRaw(NextUInt() >> (32 - FP.FractionalBits));
#endif
        }

        // A value in [min, max). The offset rounds down, so the result never reaches max. Returns min when max <= min.
        public FP NextFP(FP min, FP max)
        {
            if (max.RawValue <= min.RawValue)
            {
                return min;
            }

            ulong range = unchecked((ulong)(max.RawValue - min.RawValue));
            WideInt.MultiplyUnsigned(range, (ulong)NextFP().RawValue, out ulong high, out ulong low);
            ulong offset = (low >> FP.FractionalBits) | (high << (64 - FP.FractionalBits));
            return FP.FromRaw(unchecked(min.RawValue + (long)offset));
        }
    }
}
