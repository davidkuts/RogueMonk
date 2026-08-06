using NUnit.Framework;

namespace Game.Enemies.Tests
{
    public class HealthTests
    {
        [Test]
        public void StartsFull()
        {
            var health = new Health(60f);
            Assert.That(health.Current, Is.EqualTo(60f));
            Assert.That(health.Max, Is.EqualTo(60f));
            Assert.That(health.IsAlive, Is.True);
            Assert.That(health.Fraction, Is.EqualTo(1f));
        }

        [Test]
        public void TakeDamageReducesCurrentAndReturnsWhatWasApplied()
        {
            var health = new Health(60f);
            Assert.That(health.TakeDamage(20f), Is.EqualTo(20f));
            Assert.That(health.Current, Is.EqualTo(40f));
        }

        [Test]
        public void DamageIsClampedByWhatIsLeft_NoNegativeHealth()
        {
            var health = new Health(10f);
            Assert.That(health.TakeDamage(999f), Is.EqualTo(10f), "only 10 was available to take");
            Assert.That(health.Current, Is.EqualTo(0f));
        }

        [Test]
        public void DiesExactlyOnce()
        {
            var health = new Health(10f);
            int deaths = 0;
            health.Died += () => deaths++;

            health.TakeDamage(10f);
            health.TakeDamage(10f);
            health.TakeDamage(10f);

            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(health.IsAlive, Is.False);
        }

        [Test]
        public void DamagedFiresWithTheAppliedAmount()
        {
            var health = new Health(10f);
            float seen = 0f;
            health.Damaged += a => seen = a;

            health.TakeDamage(4f);

            Assert.That(seen, Is.EqualTo(4f));
        }

        [Test]
        public void ZeroAndNegativeDamageDoNothing()
        {
            var health = new Health(10f);
            int damagedEvents = 0;
            health.Damaged += _ => damagedEvents++;

            Assert.That(health.TakeDamage(0f), Is.EqualTo(0f));
            Assert.That(health.TakeDamage(-5f), Is.EqualTo(0f));
            Assert.That(health.Current, Is.EqualTo(10f));
            Assert.That(damagedEvents, Is.EqualTo(0), "a non-damaging hit must not raise Damaged");
        }

        [Test]
        public void DeadThingsTakeNoFurtherDamage()
        {
            var health = new Health(10f);
            health.TakeDamage(10f);
            Assert.That(health.TakeDamage(5f), Is.EqualTo(0f));
        }

        [Test]
        public void FractionTracksTheBar()
        {
            var health = new Health(50f);
            health.TakeDamage(20f);
            Assert.That(health.Fraction, Is.EqualTo(0.6f).Within(1e-4f));
        }
    }
}
