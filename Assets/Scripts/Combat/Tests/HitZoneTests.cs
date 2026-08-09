using NUnit.Framework;
using UnityEngine;

namespace Game.Combat.Tests
{
    /// <summary>
    /// Per-collider armour. The property that matters most is the boring one: a hit that never
    /// looked for a zone must behave exactly as it did before zones existed, because every enemy
    /// attack in the game takes that path.
    /// </summary>
    public class HitZoneTests
    {
        [Test]
        public void TheDefaultZoneIsOrdinaryFlesh()
        {
            HitZone zone = default;

            // Expressed as a reduction rather than a multiplier for exactly this reason. A
            // multiplier field would default to 0 and silently zero every unzoned hit in the game.
            Assert.AreEqual(1f, zone.DamageMultiplier, 1e-6f);
            Assert.IsFalse(zone.BlocksStagger);
            Assert.IsFalse(zone.IsArmored);
            Assert.IsTrue(zone.IsNeutral);
        }

        [Test]
        public void APlateReducesDamageByItsStatedFraction()
        {
            var zone = new HitZone { Id = "dome", DamageReduction = 0.7f, BlocksStagger = true, IsArmored = true };

            Assert.AreEqual(0.3f, zone.DamageMultiplier, 1e-6f);
            Assert.IsFalse(zone.IsNeutral);
        }

        [Test]
        public void ReductionIsClampedSoAPlateCanNeverHealOrAmplify()
        {
            Assert.AreEqual(0f, new HitZone { DamageReduction = 1.4f }.DamageMultiplier, 1e-6f);
            Assert.AreEqual(1f, new HitZone { DamageReduction = -3f }.DamageMultiplier, 1e-6f);
        }

        [Test]
        public void AContextBuiltWithoutAZoneCarriesTheNeutralOne()
        {
            var attack = new FakeAttack();
            HitContext context = HitContext.FromAttack(attack, new FakeDamageable(), Vector3.forward, Vector3.zero);

            Assert.IsTrue(context.Zone.IsNeutral);
            Assert.AreEqual(1f, context.Zone.DamageMultiplier, 1e-6f);
        }

        [Test]
        public void AContextCarriesTheZoneItWasGiven()
        {
            var attack = new FakeAttack();
            var zone = new HitZone { Id = "flank", DamageReduction = 0.7f, BlocksStagger = true, IsArmored = true };

            HitContext context = HitContext.FromAttack(
                attack, new FakeDamageable(), Vector3.forward, Vector3.zero, zone);

            Assert.AreEqual("flank", context.Zone.Id);
            Assert.IsTrue(context.Zone.BlocksStagger);
        }

        [Test]
        public void ResolvingANullColliderIsNeutralRatherThanAnError()
        {
            Assert.IsTrue(HitZones.Resolve(null).IsNeutral);
        }
    }
}
