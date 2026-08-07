using System;
using System.Collections.Generic;
using Game.Core.Diagnostics;
using Game.Core.Rng;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The boons the player has picked up this run, and the offers put in front of them.
    ///
    /// Lives on the player rather than on the run director because the thing it ultimately does is
    /// register modifiers on the player's own <see cref="HitResolver"/> — and because a boon is a
    /// property of the character, not of the level they happen to be standing in.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerAttackController))]
    public sealed class PlayerBoons : MonoBehaviour
    {
        [SerializeField] PlayerAttackController attacks;
        [SerializeField, Tooltip("Every boon that can be offered. The pool, not the loadout.")]
        BoonDefinition[] pool = new BoonDefinition[0];
        [SerializeField, Tooltip("How many are offered at once.")]
        int offerCount = 3;

        readonly List<BoonDefinition> owned = new List<BoonDefinition>();
        readonly List<BoonDefinition> offerScratch = new List<BoonDefinition>();
        readonly List<BoonDefinition> currentOffer = new List<BoonDefinition>();
        readonly Dictionary<BoonDefinition, BoonModifier> registered = new Dictionary<BoonDefinition, BoonModifier>();

        public IReadOnlyList<BoonDefinition> Owned => owned;

        public IReadOnlyList<BoonDefinition> CurrentOffer => currentOffer;

        /// <summary>Raised whenever the loadout changes, for the HUD.</summary>
        public event Action BoonsChanged;

        void Awake()
        {
            if (attacks == null)
                attacks = GetComponent<PlayerAttackController>();
        }

        /// <summary>
        /// Draws an offer of distinct, not-yet-owned boons from the run's stream.
        ///
        /// Takes an explicit source so the caller decides which stream pays for it — offers must
        /// come from the run stream, since they are part of what a seed reproduces.
        /// </summary>
        public IReadOnlyList<BoonDefinition> RollOffer(IRandomSource random)
        {
            currentOffer.Clear();
            if (random == null)
                return currentOffer;

            offerScratch.Clear();
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] != null && !owned.Contains(pool[i]))
                    offerScratch.Add(pool[i]);
            }

            // Shuffling and taking the front is the honest way to draw distinct items: picking
            // repeatedly and rejecting duplicates would consume an unpredictable number of draws
            // and desynchronise the seed.
            random.Shuffle(offerScratch);

            int take = Mathf.Min(Mathf.Max(1, offerCount), offerScratch.Count);
            for (int i = 0; i < take; i++)
                currentOffer.Add(offerScratch[i]);

            return currentOffer;
        }

        /// <summary>True when there is anything left worth offering.</summary>
        public bool HasOfferableBoons()
        {
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i] != null && !owned.Contains(pool[i]))
                    return true;
            }

            return false;
        }

        public void Grant(BoonDefinition boon)
        {
            if (boon == null || owned.Contains(boon) || attacks == null)
                return;

            BoonModifier modifier = boon.CreateModifier();
            attacks.Resolver.AddModifier(modifier);
            registered[boon] = modifier;
            owned.Add(boon);

            GameLog.Info(LogCategory.Combat,
                $"BOON GAINED  {boon.DisplayName} ({boon.Kind})  -  {owned.Count} held");

            BoonsChanged?.Invoke();
        }

        /// <summary>
        /// Strips every boon. Called when a run ends: without it a restart would inherit the last
        /// run's loadout, which would make the very first room of run two trivial.
        /// </summary>
        public void ClearForNewRun()
        {
            if (attacks != null)
            {
                foreach (KeyValuePair<BoonDefinition, BoonModifier> entry in registered)
                    attacks.Resolver.RemoveModifier(entry.Value);
            }

            registered.Clear();
            owned.Clear();
            currentOffer.Clear();

            BoonsChanged?.Invoke();
        }
    }
}
