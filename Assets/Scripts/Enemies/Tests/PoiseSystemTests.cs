using NUnit.Framework;

namespace Game.Enemies.Tests
{
    public class PoiseSystemTests
    {
        static PoiseSystem Make(out FakeEnemyDefinition definition, StaggerTier tier = StaggerTier.Staggerable)
        {
            definition = new FakeEnemyDefinition { Tier = tier, PoiseMax = 30f, ArmorMax = 40f };
            return new PoiseSystem(definition);
        }

        // --- Tier 1: Staggerable ---

        [Test]
        public void StartsWithFullPoise()
        {
            PoiseSystem poise = Make(out FakeEnemyDefinition definition);
            Assert.That(poise.Poise, Is.EqualTo(definition.PoiseMax));
            Assert.That(poise.IsStaggered, Is.False);
        }

        [Test]
        public void PartialPoiseDamageIsAbsorbed()
        {
            PoiseSystem poise = Make(out _);
            Assert.That(poise.ApplyPoiseDamage(10f), Is.EqualTo(PoiseResult.Absorbed));
            Assert.That(poise.Poise, Is.EqualTo(20f));
        }

        [Test]
        public void PoiseBreaksAtZeroAndStaggers()
        {
            PoiseSystem poise = Make(out FakeEnemyDefinition definition);
            poise.ApplyPoiseDamage(10f);
            poise.ApplyPoiseDamage(10f);

            Assert.That(poise.ApplyPoiseDamage(10f), Is.EqualTo(PoiseResult.Broken));
            Assert.That(poise.IsStaggered, Is.True);
            Assert.That(poise.StaggerRemaining, Is.EqualTo(definition.StaggerDurationSeconds).Within(1e-4f));
        }

        [Test]
        public void OneKickBreaksThreePunchesWorthOfPoise()
        {
            // The kick is specced at roughly 3x poise damage; against a 30 poise enemy that
            // means the finisher alone opens the punish window.
            PoiseSystem poise = Make(out _);
            Assert.That(poise.ApplyPoiseDamage(30f), Is.EqualTo(PoiseResult.Broken));
        }

        [Test]
        public void BrokeEventCarriesTheStaggerDuration()
        {
            PoiseSystem poise = Make(out FakeEnemyDefinition definition);
            float seen = 0f;
            poise.Broke += d => seen = d;

            poise.ApplyPoiseDamage(999f);

            Assert.That(seen, Is.EqualTo(definition.StaggerDurationSeconds).Within(1e-4f));
        }

        [Test]
        public void FurtherHitsDuringAStaggerDoNotRestartIt()
        {
            PoiseSystem poise = Make(out _);
            poise.ApplyPoiseDamage(999f);
            poise.Tick(0.5f);
            float remaining = poise.StaggerRemaining;

            Assert.That(poise.ApplyPoiseDamage(999f), Is.EqualTo(PoiseResult.AlreadyStaggered));
            Assert.That(poise.StaggerRemaining, Is.EqualTo(remaining).Within(1e-4f), "stagger must not be extended by more hits");
        }

        [Test]
        public void StaggerExpiresAndRaisesRecovered()
        {
            PoiseSystem poise = Make(out FakeEnemyDefinition definition);
            int recovered = 0;
            poise.Recovered += () => recovered++;

            poise.ApplyPoiseDamage(999f);
            poise.Tick(definition.StaggerDurationSeconds + 0.01f);

            Assert.That(poise.IsStaggered, Is.False);
            Assert.That(recovered, Is.EqualTo(1));
        }

        [Test]
        public void PoiseRegeneratesOnlyAfterTheDelay()
        {
            PoiseSystem poise = Make(out FakeEnemyDefinition definition);
            definition.PoiseRegenDelay = 1f;
            definition.PoiseRegenRate = 10f;
            poise.ApplyPoiseDamage(10f); // 20 left

            poise.Tick(0.9f);
            Assert.That(poise.Poise, Is.EqualTo(20f).Within(1e-4f), "must not regenerate during the delay");

            poise.Tick(0.2f); // delay elapses
            poise.Tick(1f);
            Assert.That(poise.Poise, Is.EqualTo(30f).Within(0.5f));
        }

        [Test]
        public void RegenNeverExceedsTheMaximum()
        {
            PoiseSystem poise = Make(out FakeEnemyDefinition definition);
            poise.ApplyPoiseDamage(5f);
            poise.Tick(definition.PoiseRegenDelay + 100f);
            Assert.That(poise.Poise, Is.EqualTo(definition.PoiseMax));
        }

        [Test]
        public void TakingPoiseDamageRestartsTheRegenDelay()
        {
            PoiseSystem poise = Make(out FakeEnemyDefinition definition);
            definition.PoiseRegenDelay = 1f;
            poise.ApplyPoiseDamage(10f);
            poise.Tick(0.9f);
            poise.ApplyPoiseDamage(5f); // resets the clock

            poise.Tick(0.5f);
            Assert.That(poise.Poise, Is.EqualTo(15f).Within(1e-4f), "sustained pressure should keep poise suppressed");
        }

        // --- Tier 2: Armored ---

        [Test]
        public void ArmoredStartsWithArmourAndFullPoise()
        {
            PoiseSystem poise = Make(out FakeEnemyDefinition definition, StaggerTier.Armored);
            Assert.That(poise.Armor, Is.EqualTo(definition.ArmorMax));
            Assert.That(poise.IsArmorStripped, Is.False);
        }

        [Test]
        public void ArmourAbsorbsPoiseDamageUntilStripped()
        {
            PoiseSystem poise = Make(out _, StaggerTier.Armored);
            Assert.That(poise.ApplyPoiseDamage(30f), Is.EqualTo(PoiseResult.Absorbed));
            Assert.That(poise.Poise, Is.EqualTo(30f), "poise is untouched while armour holds");

            Assert.That(poise.ApplyPoiseDamage(20f), Is.EqualTo(PoiseResult.ArmorStripped));
            Assert.That(poise.IsArmorStripped, Is.True);
            Assert.That(poise.IsStaggered, Is.False, "stripping armour is its own beat, not a stagger");
        }

        [Test]
        public void ArmourOverkillDoesNotCarryIntoPoise()
        {
            PoiseSystem poise = Make(out FakeEnemyDefinition definition, StaggerTier.Armored);
            poise.ApplyPoiseDamage(999f);
            Assert.That(poise.Poise, Is.EqualTo(definition.PoiseMax));
        }

        [Test]
        public void OnceStrippedAnArmoredEnemyBehavesLikeTierOne()
        {
            PoiseSystem poise = Make(out FakeEnemyDefinition definition, StaggerTier.Armored);
            poise.ApplyPoiseDamage(definition.ArmorMax); // strip

            Assert.That(poise.ApplyPoiseDamage(30f), Is.EqualTo(PoiseResult.Broken));
            Assert.That(poise.IsStaggered, Is.True);
        }

        [Test]
        public void ArmourDoesNotRegenerate()
        {
            // Stripping armour is a one-off opening cost, not a wall that resets.
            PoiseSystem poise = Make(out FakeEnemyDefinition definition, StaggerTier.Armored);
            poise.ApplyPoiseDamage(definition.ArmorMax);
            poise.Tick(100f);

            Assert.That(poise.Armor, Is.EqualTo(0f));
            Assert.That(poise.IsArmorStripped, Is.True);
        }

        [Test]
        public void ArmorStrippedEventFiresOnce()
        {
            PoiseSystem poise = Make(out FakeEnemyDefinition definition, StaggerTier.Armored);
            int stripped = 0;
            poise.ArmorStripped += () => stripped++;

            poise.ApplyPoiseDamage(definition.ArmorMax);
            poise.ApplyPoiseDamage(10f);

            Assert.That(stripped, Is.EqualTo(1));
        }

        // --- Tier 3: Immune ---

        [Test]
        public void ImmuneEnemiesAreNeverStaggered()
        {
            PoiseSystem poise = Make(out _, StaggerTier.Immune);

            Assert.That(poise.ApplyPoiseDamage(9999f), Is.EqualTo(PoiseResult.Immune));
            Assert.That(poise.IsStaggered, Is.False);
            Assert.That(poise.Poise, Is.EqualTo(30f), "poise is not even consumed");
        }

        [Test]
        public void ImmuneEnemiesReportNoArmourToStrip()
        {
            PoiseSystem poise = Make(out _, StaggerTier.Immune);
            Assert.That(poise.Armor, Is.EqualTo(0f));
            Assert.That(poise.IsArmorStripped, Is.True);
        }

        [Test]
        public void ClearStaggerEndsItImmediately()
        {
            PoiseSystem poise = Make(out _);
            poise.ApplyPoiseDamage(999f);
            poise.ClearStagger();
            Assert.That(poise.IsStaggered, Is.False);
        }

        // --- Forced stagger (the vortex's arrival beat) ---

        [Test]
        public void ForcedStaggerBypassesAFullPoiseBar()
        {
            PoiseSystem poise = Make(out _);

            Assert.That(poise.ForceStagger(0.4f), Is.True);
            Assert.That(poise.IsStaggered, Is.True);
            Assert.That(poise.StaggerRemaining, Is.EqualTo(0.4f));
            Assert.That(poise.Poise, Is.EqualTo(30f), "the poise bar is untouched - this is not poise damage");
        }

        [Test]
        public void ForcedStaggerRaisesBrokeSoControllersInterrupt()
        {
            PoiseSystem poise = Make(out _);
            float notified = -1f;
            poise.Broke += duration => notified = duration;

            poise.ForceStagger(0.4f);

            Assert.That(notified, Is.EqualTo(0.4f));
        }

        [Test]
        public void ForcedStaggerExtendsButNeverShortensOne()
        {
            PoiseSystem poise = Make(out _);
            poise.ForceStagger(0.9f);

            Assert.That(poise.ForceStagger(0.4f), Is.False, "already down - this is not a fresh interrupt");
            Assert.That(poise.StaggerRemaining, Is.EqualTo(0.9f), "a second source must not cut a stagger short");
        }

        [Test]
        public void ForcedStaggerDoesNotRepeatTheInterruptWhileAlreadyDown()
        {
            PoiseSystem poise = Make(out _);
            int brokeCount = 0;
            poise.Broke += _ => brokeCount++;

            poise.ForceStagger(0.4f);
            poise.ForceStagger(0.4f);

            Assert.That(brokeCount, Is.EqualTo(1));
        }

        [Test]
        public void ImmuneEnemiesCannotBeForceStaggeredEither()
        {
            PoiseSystem poise = Make(out _, StaggerTier.Immune);

            Assert.That(poise.ForceStagger(0.4f), Is.False);
            Assert.That(poise.IsStaggered, Is.False);
        }

        [Test]
        public void AForcedStaggerStillExpiresOnItsOwnTimer()
        {
            PoiseSystem poise = Make(out _);
            poise.ForceStagger(0.4f);

            poise.Tick(0.5f);

            Assert.That(poise.IsStaggered, Is.False);
        }
    }
}
