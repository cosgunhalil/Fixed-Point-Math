using FPM.Unity;
using NUnit.Framework;
using UnityEngine;

namespace FPM.Tests.Editor
{
    public class FPConversionsTests
    {
        [Test]
        public void FPConversions_RoundTripAndSaturate()
        {
            Assert.That(1.5f.ToFP(), Is.EqualTo(FP.One + FP.Half));
            Assert.That((-0.25f).ToFP().ToFloat(), Is.EqualTo(-0.25f));
            Assert.That(float.NaN.ToFP(), Is.EqualTo(FP.Zero));
            Assert.That(1e30.ToFP(), Is.EqualTo(FP.MaxValue));
            Assert.That((-1e30).ToFP(), Is.EqualTo(FP.MinValue));
            Assert.That(new Vector3(1f, 2f, -3f).ToFP3().ToVector3(), Is.EqualTo(new Vector3(1f, 2f, -3f)));
        }
    }
}
