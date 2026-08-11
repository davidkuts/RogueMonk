using NUnit.Framework;
using UnityEngine;

namespace Game.Combat.Tests
{
    /// <summary>
    /// The rolling past the Stored Rewind Stopgap reads. Ring-buffer arithmetic is exactly the
    /// kind of thing that works until the buffer wraps, so the wrap is what these pin.
    /// </summary>
    public class RewindHistoryTests
    {
        static RewindHistory Recorded(float windowSeconds, float interval, int steps, float stepSeconds)
        {
            var history = new RewindHistory(windowSeconds, interval);

            // Walks along +X one metre per step and loses one health per step, so a sampled
            // position and a sampled health both identify exactly which step they came from.
            for (int i = 0; i < steps; i++)
                history.Tick(stepSeconds, new Vector3(i, 0f, 0f), 100f - i);

            return history;
        }

        [Test]
        public void AnEmptyHistoryAnswersNothing()
        {
            var history = new RewindHistory(2f);

            Assert.That(history.TrySample(1f, out _), Is.False,
                "a rewind pressed before anything was recorded must not invent a destination");
        }

        [Test]
        public void ItSamplesTheStateFromTheRequestedTimeAgo()
        {
            // 20 samples at 0.1s each = 2 seconds of history, newest at t=2.0.
            RewindHistory history = Recorded(windowSeconds: 3f, interval: 0.1f, steps: 20, stepSeconds: 0.1f);

            Assert.That(history.TrySample(1f, out RewindHistory.Sample sample), Is.True);
            Assert.That(sample.Position.x, Is.EqualTo(10f).Within(1.01f),
                "one second back is roughly ten steps back");
            Assert.That(sample.Health, Is.EqualTo(90f).Within(1.01f));
        }

        [Test]
        public void ItClampsToTheOldestSampleRatherThanFailing()
        {
            // Only half a second of history exists; the player asks for two.
            RewindHistory history = Recorded(windowSeconds: 3f, interval: 0.1f, steps: 5, stepSeconds: 0.1f);

            Assert.That(history.TrySample(2f, out RewindHistory.Sample sample), Is.True,
                "a panic button used early must still fire — refusing AND consuming the item is the worst answer");
            Assert.That(sample.Position.x, Is.EqualTo(0f).Within(0.001f), "clamped to the oldest state held");
        }

        [Test]
        public void ItKeepsAnsweringCorrectlyAfterTheBufferWraps()
        {
            // Far more steps than the buffer can hold, so every slot has been overwritten.
            RewindHistory history = Recorded(windowSeconds: 1f, interval: 0.1f, steps: 200, stepSeconds: 0.1f);

            Assert.That(history.Count, Is.LessThanOrEqualTo(12), "the buffer is bounded");
            Assert.That(history.TrySample(0.5f, out RewindHistory.Sample sample), Is.True);

            // Newest sample is step 199 at x=199; half a second back is ~5 steps.
            Assert.That(sample.Position.x, Is.EqualTo(194f).Within(1.01f),
                "the wrap must not hand back a stale slot from a previous lap");
        }

        [Test]
        public void ZeroSecondsAgoIsTheMostRecentState()
        {
            RewindHistory history = Recorded(windowSeconds: 2f, interval: 0.1f, steps: 10, stepSeconds: 0.1f);

            Assert.That(history.TrySample(0f, out RewindHistory.Sample sample), Is.True);
            Assert.That(sample.Position.x, Is.EqualTo(9f).Within(0.001f));
        }

        [Test]
        public void ClearingDropsThePast()
        {
            RewindHistory history = Recorded(windowSeconds: 2f, interval: 0.1f, steps: 10, stepSeconds: 0.1f);
            history.Clear();

            Assert.That(history.Count, Is.Zero);
            Assert.That(history.TrySample(1f, out _), Is.False,
                "a new room is not somewhere the player can rewind to");
        }
    }
}
