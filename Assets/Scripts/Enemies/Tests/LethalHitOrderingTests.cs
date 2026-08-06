using NUnit.Framework;

namespace Game.Enemies.Tests
{
    /// <summary>
    /// Regression cover for a bug found by reading a real playtest log: the log showed
    /// "DEATH" immediately followed by "POISE BREAK ... punish window open" on the same
    /// frame, because health and poise were both applied unconditionally. A dead enemy must
    /// not stagger, since that also fired the interrupt event and put a status on a corpse.
    /// </summary>
    public class LethalHitOrderingTests
    {
        [Test]
        public void PoiseIsNotAppliedOnceHealthReachesZero()
        {
            var definition = new FakeEnemyDefinition { MaxHealth = 20f, PoiseMax = 30f };
            var health = new Health(definition.MaxHealth);
            var poise = new PoiseSystem(definition);
            int breaks = 0;
            poise.Broke += _ => breaks++;

            health.TakeDamage(20f); // lethal

            // The guard the actor applies: poise only matters to something still standing.
            if (health.IsAlive)
                poise.ApplyPoiseDamage(30f);

            Assert.That(health.IsAlive, Is.False);
            Assert.That(breaks, Is.EqualTo(0), "a corpse must not open a punish window");
            Assert.That(poise.IsStaggered, Is.False);
        }

        [Test]
        public void ANonLethalHitStillAppliesPoiseNormally()
        {
            var definition = new FakeEnemyDefinition { MaxHealth = 60f, PoiseMax = 30f };
            var health = new Health(definition.MaxHealth);
            var poise = new PoiseSystem(definition);

            health.TakeDamage(20f);
            PoiseResult result = health.IsAlive ? poise.ApplyPoiseDamage(30f) : PoiseResult.Absorbed;

            Assert.That(health.IsAlive, Is.True);
            Assert.That(result, Is.EqualTo(PoiseResult.Broken));
        }
    }
}
