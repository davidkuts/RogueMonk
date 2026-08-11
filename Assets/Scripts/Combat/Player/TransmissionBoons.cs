using System;
using System.Collections.Generic;
using Game.Core.Diagnostics;
using Game.Core.Economy;
using Game.Core.Player;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The transmission boons the player holds this run, granted by the drafts that
    /// Transmission rewards open. Sits beside <see cref="PlayerBoons"/> (the between-levels
    /// elemental picks) rather than replacing it — the two are different beats of the run and
    /// the full boon system will decide their final relationship.
    ///
    /// Hit-side effects register on the player's resolver as ability-scoped modifiers; the
    /// Ward lane patches the dash's i-frame fraction instead, because insurance is not a hit.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerAttackController))]
    public sealed class TransmissionBoons : MonoBehaviour
    {
        [SerializeField] PlayerAttackController attacks;
        [SerializeField, Tooltip("For the Ward lane's i-frame and grace patches.")]
        PlayerMotor motor;
        [SerializeField, Tooltip("For the Ward lane's shield procs (Guard High).")]
        PlayerHealth health;
        [SerializeField, Tooltip("Normal/Rare/Epic multipliers, shared with everything rarity touches.")]
        RarityScalarSettings rarityScalars;

        readonly List<OwnedBoon> owned = new List<OwnedBoon>();
        readonly List<TransmissionBoonDefinition> ownedDefinitions = new List<TransmissionBoonDefinition>();

        public readonly struct OwnedBoon
        {
            public readonly TransmissionBoonDefinition Definition;
            public readonly RewardTier Tier;

            public OwnedBoon(TransmissionBoonDefinition definition, RewardTier tier)
            {
                Definition = definition;
                Tier = tier;
            }
        }

        public IReadOnlyList<OwnedBoon> Owned => owned;

        /// <summary>The owned definitions alone, for draft dedup.</summary>
        public IReadOnlyList<TransmissionBoonDefinition> OwnedDefinitions => ownedDefinitions;

        /// <summary>Raised whenever the loadout changes, for HUD and the debug overlay.</summary>
        public event Action Changed;

        /// <summary>A boon may install several pipeline pieces (a stat mod AND a shield proc).</summary>
        readonly Dictionary<TransmissionBoonDefinition, List<IHitModifier>> registered =
            new Dictionary<TransmissionBoonDefinition, List<IHitModifier>>();

        void Awake()
        {
            if (attacks == null) attacks = GetComponent<PlayerAttackController>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (health == null) health = GetComponent<PlayerHealth>();
        }

        float Scalar(RewardTier tier) => rarityScalars != null ? rarityScalars.Scalar(tier) : 1f;

        public void Grant(TransmissionBoonDefinition boon, RewardTier tier)
        {
            if (boon == null || ownedDefinitions.Contains(boon) || attacks == null)
                return;

            float scalar = Scalar(tier);
            var installed = new List<IHitModifier>();

            AbilityScopedModifier modifier = boon.CreateModifier(scalar);
            if (modifier != null)
                installed.Add(modifier);

            // Guard High: rarity divides the hit count, so a Rare shields more often — the
            // number scales, the mechanic does not.
            if (boon.ShieldEveryNHits > 0 && health != null)
            {
                int hits = Mathf.Max(2, Mathf.RoundToInt(boon.ShieldEveryNHits / Mathf.Max(0.01f, scalar)));
                installed.Add(new ShieldProcModifier(boon.Ability, hits, health.GrantOneHitShield));
            }

            for (int i = 0; i < installed.Count; i++)
                attacks.Resolver.AddModifier(installed[i]);

            if (installed.Count > 0)
                registered[boon] = installed;

            owned.Add(new OwnedBoon(boon, tier));
            ownedDefinitions.Add(boon);
            ApplyDashPatches();

            GameLog.Info(LogCategory.Combat,
                $"TRANSMISSION INSTALLED  {boon.DisplayName} [{boon.Giver}/{boon.Ability}] at {tier}  -  {owned.Count} held");

            Changed?.Invoke();
        }

        /// <summary>Strips everything, for a new run. Mirrors <see cref="PlayerBoons.ClearForNewRun"/>.</summary>
        public void ClearForNewRun()
        {
            if (attacks != null)
            {
                foreach (KeyValuePair<TransmissionBoonDefinition, List<IHitModifier>> entry in registered)
                {
                    for (int i = 0; i < entry.Value.Count; i++)
                        attacks.Resolver.RemoveModifier(entry.Value[i]);
                }
            }

            registered.Clear();
            owned.Clear();
            ownedDefinitions.Clear();
            ApplyDashPatches();
            Changed?.Invoke();
        }

        /// <summary>
        /// Recomputes the dash's i-frame and grace multipliers from every owned Ward-lane
        /// boon. Set as one product each rather than incrementally, so clearing and
        /// re-granting can never drift the values.
        /// </summary>
        void ApplyDashPatches()
        {
            if (motor == null || motor.Dash == null)
                return;

            float iFrames = 1f;
            float grace = 1f;
            for (int i = 0; i < owned.Count; i++)
            {
                TransmissionBoonDefinition boon = owned[i].Definition;
                float scalar = Scalar(owned[i].Tier);
                if (boon.IFrameBonus > 0f)
                    iFrames *= 1f + boon.IFrameBonus * scalar;
                if (boon.DodgeGraceBonus > 0f)
                    grace *= 1f + boon.DodgeGraceBonus * scalar;
            }

            motor.Dash.IFrameFractionMultiplier = iFrames;
            motor.Dash.DodgeGraceMultiplier = grace;
        }
    }
}
