using NUnit.Framework;

namespace FPM.Tests
{
    public class FPTablesTests
    {
        // Pinned here as well as in the generated file, so an edit or a regeneration that changes a table fails.
        private const ulong SinChecksum = 0x60A5F48F63756D6AUL;
        private const ulong AtanChecksum = 0x145670E5D69B9910UL;

        [Test]
        public void Tables_MatchTheirPinnedChecksums()
        {
            Assert.That(Checksum(FPTables.Sin), Is.EqualTo(SinChecksum).And.EqualTo(FPTables.SinChecksum));
            Assert.That(Checksum(FPTables.Atan), Is.EqualTo(AtanChecksum).And.EqualTo(FPTables.AtanChecksum));
        }

        [Test]
        public void Tables_HaveExactEndpoints()
        {
            Assert.That(FPTables.Sin.Length, Is.EqualTo(FPTables.Steps + 1));
            Assert.That(FPTables.Atan.Length, Is.EqualTo(FPTables.Steps + 1));
            Assert.That(FPTables.Sin[0], Is.EqualTo(0));
            Assert.That(FPTables.Sin[FPTables.Steps], Is.EqualTo(1 << 30));
            Assert.That(FPTables.Atan[0], Is.EqualTo(0));
            Assert.That(FPTables.Atan[FPTables.Steps], Is.EqualTo(843314857));
        }

        [Test]
        public void Tables_AreIncreasing()
        {
            for (int i = 0; i < FPTables.Steps; i++)
            {
                Assert.That(FPTables.Sin[i + 1], Is.GreaterThan(FPTables.Sin[i]));
                Assert.That(FPTables.Atan[i + 1], Is.GreaterThan(FPTables.Atan[i]));
            }
        }

        // FNV-1a 64 over the values as little-endian int32s, like Tools~/generate-trig-tables.py.
        private static ulong Checksum(int[] values)
        {
            ulong hash = 0xCBF29CE484222325UL;
            foreach (int value in values)
            {
                for (int shift = 0; shift < 32; shift += 8)
                {
                    hash ^= (byte)(value >> shift);
                    hash *= 0x100000001B3UL;
                }
            }
            return hash;
        }
    }
}
