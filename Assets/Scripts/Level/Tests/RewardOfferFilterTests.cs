using System.Collections.Generic;
using Game.Combat;
using NUnit.Framework;

namespace Game.Level.Tests
{
    /// <summary>
    /// Door validity (M21E). The rule the whole thing exists for: <b>a door must never offer
    /// something this player gets nothing from</b>, and filtering must never leave a room with no
    /// door at all.
    /// </summary>
    public sealed class RewardOfferFilterTests
    {
        const float Threshold = 0.5f;

        static readonly GiverId[] AllGivers =
        {
            GiverId.Overclock, GiverId.Fray, GiverId.Stasis, GiverId.Echo, GiverId.Ward, GiverId.Flux,
        };

        static RewardOfferState State(
            int stopgapsTakeable = 4, float missing = 100f, float spliceHeal = 25f,
            IReadOnlyList<GiverId> givers = null) =>
            new RewardOfferState(stopgapsTakeable, missing, spliceHeal, givers ?? AllGivers);

        static RewardChoice Of(RewardType type, RewardBand band = RewardBand.Basic) =>
            new RewardChoice(type, band);

        // --- Stopgaps ---

        [Test]
        public void AStopgapDoorIsRefusedWhenEveryAvailableStopgapIsHeld()
        {
            Assert.IsFalse(
                RewardOfferFilter.IsOfferValid(Of(RewardType.Stopgap), State(stopgapsTakeable: 0), Threshold));
        }

        [Test]
        public void AStopgapDoorIsFineWhileOneCouldStillBeTaken()
        {
            Assert.IsTrue(
                RewardOfferFilter.IsOfferValid(Of(RewardType.Stopgap), State(stopgapsTakeable: 1), Threshold));
        }

        [Test]
        public void DisabledStopgapsMustNotCountTowardTheCeiling()
        {
            // The state is built from the GRANTABLE pool, so a disabled Stopgap can never raise the
            // count. This pins the contract the caller has to honour: with two grantable Stopgaps
            // both held, the count is 0 even though four directions exist.
            var state = State(stopgapsTakeable: 0);

            Assert.IsFalse(RewardOfferFilter.IsOfferValid(Of(RewardType.Stopgap), state, Threshold),
                "Wound Spring being switched off must not make this door permanently offerable.");
        }

        // --- Healing ---

        [Test]
        public void AHealIsRefusedAtFullHealth()
        {
            Assert.IsFalse(
                RewardOfferFilter.IsOfferValid(Of(RewardType.Splice), State(missing: 0f, spliceHeal: 25f), Threshold));
        }

        [Test]
        public void AHealIsRefusedWhenBarelyScratched()
        {
            // Missing 1 of a 25-point heal: 24 points would be thrown away.
            Assert.IsFalse(
                RewardOfferFilter.IsOfferValid(Of(RewardType.Splice), State(missing: 1f, spliceHeal: 25f), Threshold));
        }

        [Test]
        public void AHealIsOfferedOnceHalfOfItWouldLand()
        {
            // The brief's worked example: heal 25, threshold 0.5, so offer from 13 missing.
            Assert.IsFalse(
                RewardOfferFilter.IsOfferValid(Of(RewardType.Splice), State(missing: 12f, spliceHeal: 25f), Threshold));
            Assert.IsTrue(
                RewardOfferFilter.IsOfferValid(Of(RewardType.Splice), State(missing: 13f, spliceHeal: 25f), Threshold));
        }

        [Test]
        public void AThresholdOfZeroAlwaysOffersHealing()
        {
            Assert.IsTrue(
                RewardOfferFilter.IsOfferValid(Of(RewardType.Splice), State(missing: 0f, spliceHeal: 25f), 0f));
        }

        // --- Signals ---

        [Test]
        public void AnExhaustedSignalIsNeverOffered()
        {
            var onlyFray = new List<GiverId> { GiverId.Fray };
            var door = new RewardChoice(RewardType.Transmission, RewardBand.Boon, GiverId.Echo);

            Assert.IsFalse(RewardOfferFilter.IsOfferValid(door, State(givers: onlyFray), Threshold));
        }

        [Test]
        public void APinnedSignalWithSomethingLeftIsOffered()
        {
            var onlyFray = new List<GiverId> { GiverId.Fray };
            var door = new RewardChoice(RewardType.Transmission, RewardBand.Boon, GiverId.Fray);

            Assert.IsTrue(RewardOfferFilter.IsOfferValid(door, State(givers: onlyFray), Threshold));
        }

        [Test]
        public void AnUnpinnedTransmissionNeedsAtLeastOneLiveSignal()
        {
            var none = new List<GiverId>();

            Assert.IsFalse(
                RewardOfferFilter.IsOfferValid(Of(RewardType.Transmission, RewardBand.EliteBoon), State(givers: none), Threshold));
            Assert.IsTrue(
                RewardOfferFilter.IsOfferValid(Of(RewardType.Transmission, RewardBand.EliteBoon), State(), Threshold));
        }

        // --- Types that can never be dead ---

        [Test]
        public void CurrencyAndStraysAreAlwaysValid()
        {
            var starved = State(stopgapsTakeable: 0, missing: 0f, givers: new List<GiverId>());

            Assert.IsTrue(RewardOfferFilter.IsOfferValid(Of(RewardType.MinutesCache), starved, Threshold));
            Assert.IsTrue(RewardOfferFilter.IsOfferValid(Of(RewardType.HoursCache, RewardBand.Valuable), starved, Threshold));
            Assert.IsTrue(RewardOfferFilter.IsOfferValid(Of(RewardType.Stray, RewardBand.Valuable), starved, Threshold));
        }

        [Test]
        public void TheBossDoorAndLevelExitAreNeverFiltered()
        {
            var starved = State(stopgapsTakeable: 0, missing: 0f, givers: new List<GiverId>());

            Assert.IsTrue(RewardOfferFilter.IsOfferValid(RewardChoice.BossDoor, starved, Threshold));
            Assert.IsTrue(RewardOfferFilter.IsOfferValid(RewardChoice.LevelExit, starved, Threshold));
        }

        // --- Filtering a whole fork ---

        [Test]
        public void InvalidOffersAreDroppedAndValidOnesKept()
        {
            var fork = new List<RewardChoice>
            {
                Of(RewardType.Stopgap), Of(RewardType.Splice), Of(RewardType.MinutesCache),
            };

            RewardOfferFilter.Filter(fork, State(stopgapsTakeable: 0, missing: 0f), Threshold);

            Assert.AreEqual(1, fork.Count);
            Assert.AreEqual(RewardType.MinutesCache, fork[0].Type);
        }

        [Test]
        public void AForkFilteredToNothingStillProducesADoor()
        {
            // The hard guarantee: filtering can never make a doorless room.
            var fork = new List<RewardChoice> { Of(RewardType.Stopgap), Of(RewardType.Splice) };

            RewardOfferFilter.Filter(fork, State(stopgapsTakeable: 0, missing: 0f), Threshold);

            Assert.AreEqual(1, fork.Count);
            Assert.AreEqual(RewardOfferFilter.Fallback.Type, fork[0].Type);
        }

        [Test]
        public void AnExhaustedBoonDoorIsRePinnedRatherThanDropped()
        {
            // The human rule is that a boon fork always offers at least two givers, so an empty
            // giver must be replaced where possible rather than shrinking the choice.
            var fork = new List<RewardChoice>
            {
                new RewardChoice(RewardType.Transmission, RewardBand.Boon, GiverId.Echo),
                new RewardChoice(RewardType.Transmission, RewardBand.Boon, GiverId.Fray),
            };

            var live = new List<GiverId> { GiverId.Fray, GiverId.Ward };
            RewardOfferFilter.Filter(fork, State(givers: live), Threshold);

            Assert.AreEqual(2, fork.Count, "The two-giver boon fork must survive.");
            Assert.AreEqual(GiverId.Ward, fork[0].PinnedGiver, "Echo was dry, so the door re-pinned.");
            Assert.AreEqual(GiverId.Fray, fork[1].PinnedGiver, "The healthy door is untouched.");
        }

        [Test]
        public void RePinningNeverDuplicatesAGiverOnOneFork()
        {
            var fork = new List<RewardChoice>
            {
                new RewardChoice(RewardType.Transmission, RewardBand.Boon, GiverId.Echo),
                new RewardChoice(RewardType.Transmission, RewardBand.Boon, GiverId.Fray),
            };

            // Only Fray is left, and it is already on the fork — so the dead door cannot be
            // repaired and is dropped instead of becoming a second Fray door.
            RewardOfferFilter.Filter(fork, State(givers: new List<GiverId> { GiverId.Fray }), Threshold);

            Assert.AreEqual(1, fork.Count);
            Assert.AreEqual(GiverId.Fray, fork[0].PinnedGiver);
        }

        [Test]
        public void FilteringIsIdempotent()
        {
            // It runs once per room build; running it twice must not keep eating doors.
            var fork = new List<RewardChoice> { Of(RewardType.MinutesCache), Of(RewardType.Stopgap) };
            var state = State(stopgapsTakeable: 0);

            RewardOfferFilter.Filter(fork, state, Threshold);
            int afterFirst = fork.Count;
            RewardOfferFilter.Filter(fork, state, Threshold);

            Assert.AreEqual(afterFirst, fork.Count);
        }

        [Test]
        public void AnUnrestrictedStateFiltersNothing()
        {
            // The fallback for a scene with nothing wired: better to offer everything than to
            // silently collapse every fork to a minutes cache.
            var fork = new List<RewardChoice>
            {
                Of(RewardType.Stopgap), Of(RewardType.Splice), Of(RewardType.Transmission, RewardBand.Boon),
            };

            RewardOfferFilter.Filter(fork, RewardOfferState.Unrestricted, Threshold);

            Assert.AreEqual(3, fork.Count);
        }
    }
}
