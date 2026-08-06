using System;
using NUnit.Framework;

namespace Game.Combat.Tests
{
    public class ComboTrackerTests
    {
        static ComboTracker Make(out FakeAttack[] attacks, float window = 0.40f)
        {
            attacks = new[]
            {
                new FakeAttack { Id = "punch1", ComboWindowSeconds = window },
                new FakeAttack { Id = "punch2", ComboWindowSeconds = window },
                new FakeAttack { Id = "kick", ComboWindowSeconds = window },
            };

            return new ComboTracker(attacks);
        }

        [Test]
        public void RejectsAnEmptySequence()
        {
            Assert.Throws<ArgumentException>(() => new ComboTracker(new IAttackDefinition[0]));
            Assert.Throws<ArgumentException>(() => new ComboTracker(null));
        }

        [Test]
        public void StartsAtTheFirstAttack()
        {
            ComboTracker combo = Make(out FakeAttack[] attacks);
            Assert.That(combo.Index, Is.EqualTo(0));
            Assert.That(combo.Next, Is.SameAs(attacks[0]));
        }

        [Test]
        public void ConsumeWalksTheSequenceInOrder()
        {
            ComboTracker combo = Make(out FakeAttack[] attacks);
            Assert.That(combo.Consume(), Is.SameAs(attacks[0]));
            Assert.That(combo.Consume(), Is.SameAs(attacks[1]));
            Assert.That(combo.Consume(), Is.SameAs(attacks[2]));
        }

        [Test]
        public void SequenceWrapsBackToTheStart()
        {
            ComboTracker combo = Make(out FakeAttack[] attacks);
            combo.Consume();
            combo.Consume();
            combo.Consume();
            Assert.That(combo.Index, Is.EqualTo(0));
            Assert.That(combo.Next, Is.SameAs(attacks[0]));
        }

        [Test]
        public void WindowDoesNotAgeWhileAnAttackIsRunning()
        {
            // A slow attack must not eat its own follow-up window.
            ComboTracker combo = Make(out _, window: 0.40f);
            combo.Consume();

            combo.Tick(2f, isAttacking: true);

            Assert.That(combo.Index, Is.EqualTo(1), "the combo should have survived a long attack");
        }

        [Test]
        public void ComboLapsesAfterTheWindow()
        {
            ComboTracker combo = Make(out _, window: 0.40f);
            combo.Consume();

            combo.Tick(0.41f, isAttacking: false);

            Assert.That(combo.Index, Is.EqualTo(0));
        }

        [Test]
        public void ComboSurvivesInsideTheWindow()
        {
            ComboTracker combo = Make(out FakeAttack[] attacks);
            combo.Consume();

            combo.Tick(0.39f, isAttacking: false);

            Assert.That(combo.Index, Is.EqualTo(1));
            Assert.That(combo.Next, Is.SameAs(attacks[1]));
        }

        [Test]
        public void WindowAgesAcrossManyFrames()
        {
            ComboTracker combo = Make(out _, window: 0.40f);
            combo.Consume();

            for (int i = 0; i < 30; i++)
                combo.Tick(1f / 60f, isAttacking: false); // 0.5 s

            Assert.That(combo.Index, Is.EqualTo(0));
        }

        [Test]
        public void EachAttackContributesItsOwnWindow()
        {
            var attacks = new[]
            {
                new FakeAttack { Id = "quick", ComboWindowSeconds = 0.1f },
                new FakeAttack { Id = "slow", ComboWindowSeconds = 1.0f },
            };
            var combo = new ComboTracker(attacks);

            combo.Consume(); // "quick" arms a 0.1 s window
            combo.Tick(0.15f, isAttacking: false);
            Assert.That(combo.Index, Is.EqualTo(0), "the short window should have lapsed");

            combo.Consume(); // "quick" again
            combo.Tick(0.05f, isAttacking: false);
            combo.Consume(); // "slow" arms a 1.0 s window
            combo.Tick(0.5f, isAttacking: false);
            Assert.That(combo.Index, Is.EqualTo(0), "wrapped to the start after the last attack");
        }

        [Test]
        public void WindowRemaining_CountsDown()
        {
            ComboTracker combo = Make(out _, window: 0.40f);
            Assert.That(combo.WindowRemaining, Is.EqualTo(0f), "nothing to expire before the first attack");

            combo.Consume();
            Assert.That(combo.WindowRemaining, Is.EqualTo(0.40f).Within(1e-4f));

            combo.Tick(0.10f, isAttacking: false);
            Assert.That(combo.WindowRemaining, Is.EqualTo(0.30f).Within(1e-4f));
        }

        [Test]
        public void Reset_ReturnsToTheFirstAttack()
        {
            ComboTracker combo = Make(out FakeAttack[] attacks);
            combo.Consume();
            combo.Reset();

            Assert.That(combo.Index, Is.EqualTo(0));
            Assert.That(combo.Next, Is.SameAs(attacks[0]));
        }

        [Test]
        public void SingleAttackSequence_AlwaysReturnsThatAttack()
        {
            var only = new FakeAttack { Id = "only" };
            var combo = new ComboTracker(new IAttackDefinition[] { only });

            Assert.That(combo.Consume(), Is.SameAs(only));
            Assert.That(combo.Next, Is.SameAs(only));
        }
    }
}
