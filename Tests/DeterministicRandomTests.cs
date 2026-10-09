using NUnit.Framework;

namespace FPM.Tests
{
    public class DeterministicRandomTests
    {
        [Test]
        public void NextUInt_MatchesThePcg32ReferenceSequence()
        {
            // The first outputs of the reference pcg32-demo, seeded with pcg32_srandom_r(42, 54).
            uint[] expected = { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e };
            DeterministicRandom random = new DeterministicRandom(42, 54);

            foreach (uint value in expected)
            {
                Assert.That(random.NextUInt(), Is.EqualTo(value));
            }
        }

        [Test]
        public void CopiedState_ContinuesTheSameSequence()
        {
            DeterministicRandom random = new DeterministicRandom(7);
            random.NextUInt();
            DeterministicRandom copy = random;

            for (int i = 0; i < 100; i++)
            {
                Assert.That(copy.NextUInt(), Is.EqualTo(random.NextUInt()));
            }
        }

        [Test]
        public void Bounded_StaysInRangeAndCoversIt()
        {
            DeterministicRandom random = new DeterministicRandom(1);
            int[] counts = new int[7];
            for (int i = 0; i < 70000; i++)
            {
                uint value = random.NextUInt(7);
                Assert.That(value, Is.LessThan(7U));
                counts[value]++;
            }
            foreach (int count in counts)
            {
                Assert.That(count, Is.InRange(9500, 10500));
            }

            Assert.That(random.NextUInt(0), Is.EqualTo(0U));
            Assert.That(random.NextInt(5, 5), Is.EqualTo(5));
            Assert.That(random.NextInt(int.MinValue, int.MaxValue), Is.LessThan(int.MaxValue));
        }

        [Test]
        public void NextFP_StaysInItsHalfOpenRange()
        {
            DeterministicRandom random = new DeterministicRandom(2);
            FP min = FP.FromInt(-3);
            FP max = min + FP.FromRaw(3);
            for (int i = 0; i < 10000; i++)
            {
                FP unit = random.NextFP();
                Assert.That(unit >= FP.Zero && unit < FP.One, Is.True);

                FP value = random.NextFP(min, max);
                Assert.That(value >= min && value < max, Is.True);
            }
            Assert.That(random.NextFP(max, min), Is.EqualTo(max));
        }
    }
}
