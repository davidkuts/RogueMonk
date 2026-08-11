using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Combat.Tests
{
    /// <summary>
    /// The marked-stat rarity rule (human call 2026-08-11): each boon marks exactly ONE stat;
    /// the quality upgrade multiplies that stat by the rarity scalar (×1 / ×1.5 / ×2 of base)
    /// and touches nothing else, whatever the boon is.
    /// </summary>
    public class ScaledStatTests
    {
        static TransmissionBoonDefinition Boon(
            ScaledStat marked, float damageBonus = 0f, float statusSeconds = 0f, int shieldEveryN = 0)
        {
            var boon = ScriptableObject.CreateInstance<TransmissionBoonDefinition>();
            var so = new SerializedObject(boon);
            so.FindProperty("scaledStat").intValue = (int)marked;
            so.FindProperty("damageBonus").floatValue = damageBonus;
            so.FindProperty("statusSeconds").floatValue = statusSeconds;
            so.FindProperty("status").intValue = (int)StatusEffect.Chilled;
            so.FindProperty("shieldEveryNHits").intValue = shieldEveryN;
            so.FindProperty("ability").intValue = (int)AbilityId.ATK;
            so.ApplyModifiedPropertiesWithoutUndo();
            return boon;
        }

        [Test]
        public void OnlyTheMarkedStatScalesWithRarity()
        {
            // Marked: damage. The status rider must stay at base whatever the quality.
            TransmissionBoonDefinition boon = Boon(ScaledStat.DamageBonus, damageBonus: 0.2f, statusSeconds: 2f);

            var resolver = new HitResolver();
            resolver.AddModifier(boon.CreateModifier(2f)); // Epic: double the base

            var target = new FakeDamageable();
            var context = HitContext.FromAttack(
                new FakeAttack { Ability = AbilityId.ATK, Damage = 100f }, target, Vector3.forward, Vector3.zero);
            resolver.Resolve(ref context);

            Assert.That(context.Damage, Is.EqualTo(140f).Within(0.001f),
                "20% base doubles to 40% at Epic - the human's worked example");
            Assert.That(target.Statuses.Remaining(StatusEffect.Chilled), Is.EqualTo(2f).Within(0.001f),
                "the unmarked rider stays at base");

            Object.DestroyImmediate(boon);
        }

        [Test]
        public void TheSameBoonMarkedOnTheRiderScalesTheRiderInstead()
        {
            TransmissionBoonDefinition boon = Boon(ScaledStat.StatusSeconds, damageBonus: 0.2f, statusSeconds: 2f);

            var resolver = new HitResolver();
            resolver.AddModifier(boon.CreateModifier(1.5f)); // Rare: +50% of base

            var target = new FakeDamageable();
            var context = HitContext.FromAttack(
                new FakeAttack { Ability = AbilityId.ATK, Damage = 100f }, target, Vector3.forward, Vector3.zero);
            resolver.Resolve(ref context);

            Assert.That(context.Damage, Is.EqualTo(120f).Within(0.001f), "the unmarked damage stays at base");
            Assert.That(target.Statuses.Remaining(StatusEffect.Chilled), Is.EqualTo(3f).Within(0.001f),
                "2s base becomes 3s at Rare");

            Object.DestroyImmediate(boon);
        }

        [Test]
        public void CadenceStatsDivideSoBetterCardsFireMoreOften()
        {
            TransmissionBoonDefinition marked = Boon(ScaledStat.ShieldProcRate, shieldEveryN: 10);
            TransmissionBoonDefinition unmarked = Boon(ScaledStat.DamageBonus, shieldEveryN: 10);

            Assert.That(marked.ScaledShieldEveryNHits(2f), Is.EqualTo(5), "every 10th hit becomes every 5th at Epic");
            Assert.That(unmarked.ScaledShieldEveryNHits(2f), Is.EqualTo(10), "unmarked cadence never moves");

            Object.DestroyImmediate(marked);
            Object.DestroyImmediate(unmarked);
        }
    }
}
