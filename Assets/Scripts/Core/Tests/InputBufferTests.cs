using Game.Core.Input;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class InputBufferTests
    {
        const float Window = 0.15f;

        [Test]
        public void StartsEmpty()
        {
            var buffer = new InputBuffer();
            Assert.That(buffer.HasInput, Is.False);
            Assert.That(buffer.TryConsume(), Is.False);
        }

        [Test]
        public void PressIsConsumableImmediately()
        {
            var buffer = new InputBuffer();
            buffer.Press();
            Assert.That(buffer.TryConsume(), Is.True);
        }

        [Test]
        public void ConsumingClearsTheBuffer()
        {
            var buffer = new InputBuffer();
            buffer.Press();
            buffer.TryConsume();
            Assert.That(buffer.TryConsume(), Is.False, "a press must only fire once");
        }

        [Test]
        public void PressSurvivesInsideTheWindow()
        {
            var buffer = new InputBuffer();
            buffer.Press();
            buffer.Tick(Window - 0.01f, Window);
            Assert.That(buffer.HasInput, Is.True);
            Assert.That(buffer.TryConsume(), Is.True);
        }

        [Test]
        public void PressExpiresPastTheWindow()
        {
            var buffer = new InputBuffer();
            buffer.Press();
            buffer.Tick(Window + 0.01f, Window);
            Assert.That(buffer.HasInput, Is.False);
            Assert.That(buffer.TryConsume(), Is.False);
        }

        [Test]
        public void ExpiryAccumulatesAcrossFrames()
        {
            var buffer = new InputBuffer();
            buffer.Press();
            for (int i = 0; i < 10; i++)
                buffer.Tick(0.02f, Window); // 0.2 s total

            Assert.That(buffer.HasInput, Is.False);
        }

        [Test]
        public void RepressingResetsTheAge()
        {
            var buffer = new InputBuffer();
            buffer.Press();
            buffer.Tick(0.14f, Window);
            buffer.Press();
            buffer.Tick(0.14f, Window);
            Assert.That(buffer.HasInput, Is.True);
        }

        [Test]
        public void TickWithoutAPress_IsANoOp()
        {
            var buffer = new InputBuffer();
            buffer.Tick(1f, Window);
            Assert.That(buffer.HasInput, Is.False);
        }

        [Test]
        public void ZeroWindow_ExpiresOnTheNextTick()
        {
            var buffer = new InputBuffer();
            buffer.Press();
            Assert.That(buffer.HasInput, Is.True, "still consumable on the press frame");
            buffer.Tick(0.001f, 0f);
            Assert.That(buffer.HasInput, Is.False);
        }

        [Test]
        public void Clear_DropsAQueuedPress()
        {
            var buffer = new InputBuffer();
            buffer.Press();
            buffer.Clear();
            Assert.That(buffer.HasInput, Is.False);
        }
    }
}
