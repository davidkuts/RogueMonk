using NUnit.Framework;

namespace Game.Combat.Tests
{
    /// <summary>
    /// The vortex's hit-pulse curve (M21B). The acceptance criterion is a shape, not a value:
    /// <b>subtle at rest, visibly fiercer during a dense hit sequence, settling back down on its
    /// own.</b> That is the sort of thing normally checked by squinting at a build, which is exactly
    /// why the curve lives in plain C# instead.
    /// </summary>
    public sealed class VortexPulseEnvelopeTests
    {
        const float Intensity = 0.28f;
        const float Duration = 0.35f;

        [Test]
        public void ItRestsAtZero()
        {
            var pulse = new VortexPulseEnvelope();

            Assert.AreEqual(0f, pulse.Level, 0.0001f, "A spin catching nothing must add no brightness.");
            Assert.IsTrue(pulse.IsQuiet);
        }

        [Test]
        public void HitsAccumulateRatherThanRetrigger()
        {
            var single = new VortexPulseEnvelope();
            var crowd = new VortexPulseEnvelope();

            single.Add(Intensity);

            // Three enemies caught by one tick.
            crowd.Add(Intensity);
            crowd.Add(Intensity);
            crowd.Add(Intensity);

            Assert.Greater(
                crowd.Level, single.Level,
                "A crowd must outshine a duel — a retriggering flash would make them identical.");
            Assert.AreEqual(Intensity * 3f, crowd.Level, 0.0001f);
        }

        [Test]
        public void ItClampsSoAWideSpinCannotBlowOut()
        {
            var pulse = new VortexPulseEnvelope();

            // Nine damage events is a full spin against three enemies.
            for (int i = 0; i < 9; i++)
                pulse.Add(Intensity);

            Assert.AreEqual(1f, pulse.Level, 0.0001f, "Past a point, more hits must stop meaning brighter.");
        }

        [Test]
        public void AFullPulseDecaysToRestInExactlyTheDuration()
        {
            var pulse = new VortexPulseEnvelope();
            for (int i = 0; i < 9; i++)
                pulse.Add(Intensity);

            // One frame short.
            pulse.Tick(Duration - 0.01f, Duration);
            Assert.Greater(pulse.Level, 0f, "It must not have settled early.");

            pulse.Tick(0.02f, Duration);
            Assert.AreEqual(0f, pulse.Level, 0.0001f, "Nor late — the duration is the whole promise of the knob.");
            Assert.IsTrue(pulse.IsQuiet);
        }

        [Test]
        public void ItSettlesBackDownAfterASequence()
        {
            var pulse = new VortexPulseEnvelope();

            // A dense sequence: hits arriving faster than the decay.
            for (int i = 0; i < 9; i++)
            {
                pulse.Add(Intensity);
                pulse.Tick(0.02f, Duration);
            }

            float peak = pulse.Level;
            Assert.Greater(peak, 0.5f, "A dense sequence must visibly intensify.");

            // Then the room is empty.
            for (int i = 0; i < 40; i++)
                pulse.Tick(0.02f, Duration);

            Assert.AreEqual(0f, pulse.Level, 0.0001f, "And it must return to subtle on its own.");
        }

        [Test]
        public void ADurationOfZeroSnapsOffRatherThanDividingByZero()
        {
            var pulse = new VortexPulseEnvelope();
            pulse.Add(1f);

            pulse.Tick(0.016f, 0f);

            Assert.AreEqual(0f, pulse.Level, 0.0001f);
        }

        [Test]
        public void NegativeAndZeroInputsAreIgnored()
        {
            var pulse = new VortexPulseEnvelope();

            pulse.Add(0f);
            pulse.Add(-1f);
            Assert.AreEqual(0f, pulse.Level, 0.0001f);

            pulse.Add(0.5f);
            pulse.Tick(0f, Duration);
            pulse.Tick(-1f, Duration);
            Assert.AreEqual(0.5f, pulse.Level, 0.0001f, "A frozen frame must not advance the decay.");
        }

        [Test]
        public void ResetIsImmediate()
        {
            var pulse = new VortexPulseEnvelope();
            pulse.Add(1f);

            pulse.Reset();

            Assert.AreEqual(0f, pulse.Level, 0.0001f, "An interrupt cannot leave the disc lit.");
        }
    }
}
