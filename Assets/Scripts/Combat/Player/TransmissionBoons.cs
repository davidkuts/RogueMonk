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
        [SerializeField, Tooltip("For the Ward lane's i-frame patch.")]
        PlayerMotor motor;
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

        readonly Dictionary<TransmissionBoonDefinition, AbilityScopedModifier> registered =
            new Dictionary<TransmissionBoonDefinition, AbilityScopedModifier>();

        void Awake()
        {
            if (attacks == null) attacks = GetComponent<PlayerAttackController>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
        }

        float Scalar(RewardTier tier) => rarityScalars != null ? rarityScalars.Scalar(tier) : 1f;

        public void Grant(TransmissionBoonDefinition boon, RewardTier tier)
        {
            if (boon == null || ownedDefinitions.Contains(boon) || attacks == null)
                return;

            float scalar = Scalar(tier);

            AbilityScopedModifier modifier = boon.CreateModifier(scalar);
            if (modifier != null)
            {
                attacks.Resolver.AddModifier(modifier);
                registered[boon] = modifier;
            }

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
                foreach (KeyValuePair<TransmissionBoonDefinition, AbilityScopedModifier> entry in registered)
                    attacks.Resolver.RemoveModifier(entry.Value);
            }

            registered.Clear();
            owned.Clear();
            ownedDefinitions.Clear();
            ApplyDashPatches();
            Changed?.Invoke();
        }

        /// <summary>
        /// Recomputes the dash i-frame multiplier from every owned Ward-lane boon. Set as one
        /// product on the dash rather than incrementally, so clearing and re-granting can never
        /// drift the value.
        /// </summary>
        void ApplyDashPatches()
        {
            if (motor == null || motor.Dash == null)
                return;

            float multiplier = 1f;
            for (int i = 0; i < owned.Count; i++)
            {
                TransmissionBoonDefinition boon = owned[i].Definition;
                if (boon.IFrameBonus > 0f)
                    multiplier *= 1f + boon.IFrameBonus * Scalar(owned[i].Tier);
            }

            motor.Dash.IFrameFractionMultiplier = multiplier;
        }
    }
}
