using Game.Core.Rng;
using NUnit.Framework;

namespace Game.Combat.Tests
{
    /// <summary>
    /// The unknown waveform's lane. BOONS.md §5 asks for "EV parity with other givers;
    /// distribution is the identity", so what these pin is the distribution and — more
    /// importantly — that the rolls are reproducible from a seed.
    /// </summary>
    public class ChanceCritTests
    {
        sealed class FakeTarget : IDamageable
        {
            public bool IsAlive => true;
            public StatusEffectContainer Statuses { get; } = new StatusEffectContainer();
            public DotContainer Dots { get; } = new DotContainer();
            public void ApplyHit(in HitContext context) { }
            public void ApplyStagger(float seconds) { }
        }

        static HitContext Hit(AbilityId ability, float damage = 10f) => new HitContext
        {
            Attack = new FakeAttack { Ability = ability },
            Target = new FakeTarget(),
            Damage = damage,
            HitstopSeconds = 0.06f,
        };

        static int RollMany(ChanceCritModifier modifier, int count, AbilityId ability = AbilityId.ATK)
        {
            int crits = 0;
            for (int i = 0; i < count; i++)
            {
                HitContext context = Hit(ability);
                modifier.Modify(ref context);
                if (context.Damage > 10f)
                    crits++;
            }

            return crits;
        }

        [Test]
        public void ACertainRollAlwaysMultiplies()
        {
            var modifier = new ChanceCritModifier(
                AbilityId.ATK, chance: 1f, multiplier: 3f, hitstopBonus: 0.05f, new XorShiftRandom(1u));

            HitContext context = Hit(AbilityId.ATK);
            modifier.Modify(ref context);

            Assert.That(context.Damage, Is.EqualTo(30f).Within(0.001f));
            Assert.That(context.HitstopSeconds, Is.EqualTo(0.11f).Within(0.001f),
                "a crit lands like a heavy hit rather than printing a big number with a jab's feel");
        }

        [Test]
        public void AnImpossibleRollNeverMultiplies()
        {
            var modifier = new ChanceCritModifier(
                AbilityId.ATK, chance: 0f, multiplier: 3f, hitstopBonus: 0.05f, new XorShiftRandom(1u));

            Assert.That(RollMany(modifier, 200), Is.Zero);
            Assert.That(modifier.Procs, Is.Zero);
        }

        [Test]
        public void ItNeverStampsADamageType()
        {
            // M12's rule: a boon that is not itself elemental must never claim the hit, or it
            // blanks the element another giver's boon already put on it.
            var modifier = new ChanceCritModifier(
                AbilityId.ATK, chance: 1f, multiplier: 3f, hitstopBonus: 0f, new XorShiftRandom(1u));

            HitContext context = Hit(AbilityId.ATK);
            context.DamageType = DamageType.Fire;
            modifier.Modify(ref context);

            Assert.That(context.DamageType, Is.EqualTo(DamageType.Fire),
                "Flux is Wind, but a crit must not overwrite Overclock's fire");
        }

        [Test]
        public void ItOnlyRollsForItsOwnSlot()
        {
            var modifier = new ChanceCritModifier(
                AbilityId.SPLIT, chance: 1f, multiplier: 2f, hitstopBonus: 0f, new XorShiftRandom(1u));

            Assert.That(RollMany(modifier, 50, AbilityId.ATK), Is.Zero,
                "a Riposte boon must not crit a punch");
            Assert.That(RollMany(modifier, 5, AbilityId.SPLIT), Is.EqualTo(5));
        }

        [Test]
        public void TheDistributionLandsNearTheStatedChance()
        {
            // The card TELLS the player 15%; if the roll drifted far from it the card would be
            // lying, which for a variance giver is the whole product.
            var modifier = new ChanceCritModifier(
                AbilityId.ATK, chance: 0.15f, multiplier: 3f, hitstopBonus: 0f, new XorShiftRandom(20260811u));

            int crits = RollMany(modifier, 4000);

            Assert.That(crits / 4000f, Is.EqualTo(0.15f).Within(0.03f));
        }

        static string RollSequence(uint seed, int count)
        {
            var modifier = new ChanceCritModifier(AbilityId.ATK, 0.5f, 2f, 0f, new XorShiftRandom(seed));
            var sequence = new System.Text.StringBuilder(count);

            for (int i = 0; i < count; i++)
            {
                HitContext context = Hit(AbilityId.ATK);
                modifier.Modify(ref context);
                sequence.Append(context.Damage > 10f ? '1' : '0');
            }

            return sequence.ToString();
        }

        [Test]
        public void TheSameSeedRollsTheSameFight()
        {
            // The reason Flux draws from a DERIVED stream rather than the run stream: the rolls
            // must be reproducible from a seed, while never consuming run draws whose COUNT
            // depends on how the player chose to fight.
            Assert.That(RollSequence(777u, 500), Is.EqualTo(RollSequence(777u, 500)));
            Assert.That(RollSequence(778u, 500), Is.Not.EqualTo(RollSequence(777u, 500)),
                "and a different seed is a different fight");
        }
    }
}
