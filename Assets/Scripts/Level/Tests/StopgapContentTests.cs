using System.Collections.Generic;
using System.Linq;
using Game.Combat;
using Game.Core.Player;
using NUnit.Framework;
using UnityEditor;

namespace Game.Level.Tests
{
    /// <summary>
    /// Runs against the REAL Stopgap assets, on the same principle as the Biome 1 content tests:
    /// a test that builds its own inputs can pass while the shipped data is broken.
    ///
    /// <para>The invariant that matters is the slot rule. Stopgaps are now held one per D-pad
    /// direction, so two of them sharing a direction would make one permanently unreachable — and
    /// nothing at runtime would say so, because the second would just silently refuse to be picked
    /// up forever.</para>
    /// </summary>
    public class StopgapContentTests
    {
        static List<StopgapDefinition> AllStopgaps() =>
            AssetDatabase.FindAssets("t:StopgapDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<StopgapDefinition>)
                .Where(s => s != null)
                .ToList();

        static StopgapSettings LoadSettings()
        {
            string[] guids = AssetDatabase.FindAssets("t:StopgapSettings");
            Assert.That(guids.Length, Is.GreaterThan(0), "no StopgapSettings asset in the project");
            return AssetDatabase.LoadAssetAtPath<StopgapSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        [Test]
        public void NoTwoGrantableStopgapsShareADpadDirection()
        {
            var bySlot = new Dictionary<StopgapSlot, string>();

            foreach (StopgapDefinition stopgap in AllStopgaps())
            {
                // A disabled Stopgap may park on a taken direction — it can never be granted, so
                // it cannot collide with anything. Wound Spring does exactly this.
                if (!stopgap.Enabled)
                    continue;

                Assert.That(bySlot.ContainsKey(stopgap.Slot), Is.False,
                    $"'{stopgap.Id}' and '{(bySlot.TryGetValue(stopgap.Slot, out string other) ? other : "?")}' " +
                    $"both sit on D-pad {stopgap.Slot} — one of them could never be carried");

                bySlot[stopgap.Slot] = stopgap.Id;
            }

            Assert.That(bySlot.Count, Is.GreaterThan(0), "every Stopgap is disabled — nothing can ever be granted");
        }

        [Test]
        public void AStopgapNeverOffersMoreThanTheDpadHasDirections()
        {
            int grantable = AllStopgaps().Count(s => s.Enabled);
            int directions = StopgapInventory.AllSlots.Length;

            Assert.That(grantable, Is.LessThanOrEqualTo(directions),
                $"{grantable} grantable Stopgaps for {directions} d-pad directions — one has nowhere to live");
        }

        [Test]
        public void WoundSpringIsSwitchedOffWhileTheVortexHasNoCooldown()
        {
            // Human call 2026-08-12. The vortex cooldown is deliberately 0 — the Undertow is
            // spammable, governed by the pull-immunity window — so Wound Spring's "instant Vortex
            // recharge" has nothing to refund and would be a dead pickup. The asset and its logic
            // stay; only the switch is off.
            StopgapDefinition spring = AllStopgaps().FirstOrDefault(s => s.Kind == StopgapKind.WoundSpring);
            Assert.That(spring, Is.Not.Null, "the Wound Spring asset should still exist, just disabled");
            Assert.That(spring.Enabled, Is.False,
                "Wound Spring must stay off until the vortex has a cooldown for it to refund");
        }

        [Test]
        public void TheRewardPoolHandsOutExactlyTheEnabledStopgaps()
        {
            StopgapSettings settings = LoadSettings();
            List<string> grantable = settings.Grantable.Select(s => s.Id).ToList();

            Assert.That(grantable, Does.Not.Contain("Stopgap_WoundSpring"),
                "a disabled Stopgap must never reach the reward pool");

            foreach (StopgapDefinition stopgap in settings.Pool)
            {
                if (stopgap != null && stopgap.Enabled)
                    Assert.That(grantable, Contains.Item(stopgap.Id), $"'{stopgap.Id}' is enabled but ungrantable");
            }

            Assert.That(grantable.Count, Is.GreaterThan(0), "the Stopgap reward would always come up empty");
        }

        [Test]
        public void EveryGrantableStopgapHasAShortHudLabel()
        {
            // The label sits beside a d-pad button, not in a menu. A long one would run into the
            // screen edge or across the cross itself.
            foreach (StopgapDefinition stopgap in AllStopgaps().Where(s => s.Enabled))
            {
                Assert.That(string.IsNullOrWhiteSpace(stopgap.HudLabel), Is.False, $"'{stopgap.Id}' has no HUD label");
                Assert.That(stopgap.HudLabel.Length, Is.LessThanOrEqualTo(14),
                    $"'{stopgap.Id}' HUD label '{stopgap.HudLabel}' is too long to sit beside a d-pad button");
            }
        }
    }
}
