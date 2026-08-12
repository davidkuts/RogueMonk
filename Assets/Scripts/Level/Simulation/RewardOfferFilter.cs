using System.Collections.Generic;
using Game.Combat;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// What the player currently has, as the door-validity rules need to see it. Engine-free so the
    /// filter can be exercised without a scene.
    /// </summary>
    public readonly struct RewardOfferState
    {
        /// <summary>Grantable Stopgaps whose own D-pad direction is still free.</summary>
        public readonly int StopgapsTheyCouldStillTake;

        /// <summary>Health the player is currently down.</summary>
        public readonly float MissingHealth;

        /// <summary>What a Splice would restore right now, after Stray multipliers.</summary>
        public readonly float SpliceHealAmount;

        /// <summary>Givers with at least one boon this player could actually pick.</summary>
        public readonly IReadOnlyList<GiverId> OfferableGivers;

        public RewardOfferState(
            int stopgapsTheyCouldStillTake, float missingHealth, float spliceHealAmount,
            IReadOnlyList<GiverId> offerableGivers)
        {
            StopgapsTheyCouldStillTake = stopgapsTheyCouldStillTake;
            MissingHealth = missingHealth;
            SpliceHealAmount = spliceHealAmount;
            OfferableGivers = offerableGivers;
        }

        /// <summary>Everything permitted — what an unconfigured scene falls back to.</summary>
        public static RewardOfferState Unrestricted =>
            new RewardOfferState(int.MaxValue, float.MaxValue, 0f, null);
    }

    /// <summary>
    /// Stops a door offering something the player cannot use.
    ///
    /// <para><b>A dead offer is worse than a smaller fork.</b> A Stopgap door when every direction is
    /// full, a heal when you are at full health, a signal that has nothing left to give — each one
    /// reads as the game wasting your choice, and players resent it more than they would resent
    /// being offered less. So validity is checked per reward type and the invalid ones are removed.
    /// </para>
    ///
    /// <para>Structured as one predicate per type so a future reward plugs into the same filter
    /// instead of adding a special case at the call site.</para>
    ///
    /// <para><b>Consumes no randomness.</b> Substitutions are deterministic — first-valid rather
    /// than re-rolled — so filtering cannot shift a seed's later draws. A quoted seed still
    /// reproduces its level.</para>
    /// </summary>
    public static class RewardOfferFilter
    {
        /// <summary>
        /// The fallback when filtering leaves a fork with nothing.
        ///
        /// <para>Run currency, because it is the one reward that can never be dead: a wallet always
        /// has room. This reuses the degrade <see cref="RewardRoller"/> already applies to a band
        /// with nothing enabled, rather than inventing a second answer to the same question.</para>
        /// </summary>
        public static RewardChoice Fallback => new RewardChoice(RewardType.MinutesCache, RewardBand.Basic);

        /// <summary>
        /// Whether <paramref name="choice"/> would give the player anything.
        ///
        /// <para><paramref name="spliceThreshold"/> is the fraction of a Splice's heal that must be
        /// missing before offering it — at 0.5 a 25-point heal needs 13 points of damage taken. A
        /// heal that would be mostly wasted is a bad offer.</para>
        /// </summary>
        public static bool IsOfferValid(in RewardChoice choice, in RewardOfferState state, float spliceThreshold)
        {
            // The two structural doors carry no reward and are never filtered: the boss door is a
            // mark the player learns to read, and the level exit is the only way out of an arena.
            if (choice.IsBossDoor || choice.IsLevelExit)
                return true;

            switch (choice.Type)
            {
                case RewardType.Stopgap:
                    // "Max of all AVAILABLE stopgaps" — a disabled one must not count toward the
                    // ceiling, or switching Wound Spring off would have made this door permanently
                    // offerable against a cap it can never reach.
                    return state.StopgapsTheyCouldStillTake > 0;

                case RewardType.Splice:
                    return state.MissingHealth >= state.SpliceHealAmount * Mathf.Max(0f, spliceThreshold);

                case RewardType.Transmission:
                    if (state.OfferableGivers == null)
                        return true;

                    return choice.HasPinnedGiver
                        ? Contains(state.OfferableGivers, choice.PinnedGiver)
                        : state.OfferableGivers.Count > 0;

                default:
                    // Currency and Strays are always worth something — a wallet has room, and a
                    // second Stray is a swap decision rather than a wasted offer.
                    return true;
            }
        }

        /// <summary>
        /// Prunes <paramref name="choices"/> in place, re-pinning boon doors where it can and
        /// guaranteeing at least one door survives.
        ///
        /// <para>A boon door whose giver has run dry is <b>re-pinned</b> rather than dropped, to a
        /// giver not already used on this fork. That protects the human rule that a boon fork always
        /// offers at least two options — dropping the door instead would quietly turn a two-giver
        /// choice into a single take-it-or-leave-it.</para>
        /// </summary>
        public static void Filter(List<RewardChoice> choices, in RewardOfferState state, float spliceThreshold)
        {
            if (choices == null || choices.Count == 0)
                return;

            // Re-pin exhausted boon doors first, so a fork is only shrunk when it truly cannot be
            // repaired.
            for (int i = 0; i < choices.Count; i++)
            {
                RewardChoice choice = choices[i];
                if (choice.Type != RewardType.Transmission || !choice.HasPinnedGiver ||
                    state.OfferableGivers == null)
                    continue;

                if (Contains(state.OfferableGivers, choice.PinnedGiver))
                    continue;

                GiverId? replacement = FirstUnusedOfferableGiver(choices, state, i);
                if (replacement.HasValue)
                    choices[i] = new RewardChoice(choice.Type, choice.Band, replacement.Value);
            }

            for (int i = choices.Count - 1; i >= 0; i--)
            {
                if (!IsOfferValid(choices[i], state, spliceThreshold))
                    choices.RemoveAt(i);
            }

            // Filtering must never produce a doorless room.
            if (choices.Count == 0)
                choices.Add(Fallback);
        }

        static GiverId? FirstUnusedOfferableGiver(
            List<RewardChoice> choices, in RewardOfferState state, int ignoreIndex)
        {
            for (int g = 0; g < state.OfferableGivers.Count; g++)
            {
                GiverId candidate = state.OfferableGivers[g];

                bool alreadyOnFork = false;
                for (int i = 0; i < choices.Count && !alreadyOnFork; i++)
                {
                    if (i == ignoreIndex)
                        continue;

                    alreadyOnFork = choices[i].Type == RewardType.Transmission &&
                                    choices[i].HasPinnedGiver &&
                                    choices[i].PinnedGiver == candidate;
                }

                if (!alreadyOnFork)
                    return candidate;
            }

            return null;
        }

        static bool Contains(IReadOnlyList<GiverId> givers, GiverId giver)
        {
            for (int i = 0; i < givers.Count; i++)
            {
                if (givers[i] == giver)
                    return true;
            }

            return false;
        }
    }
}
