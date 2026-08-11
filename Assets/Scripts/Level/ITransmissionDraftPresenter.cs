using System;
using System.Collections.Generic;
using Game.Combat;

namespace Game.Level
{
    /// <summary>
    /// Presents a rolled transmission draft and reports the pick. The seam that keeps the
    /// draft's look replaceable: the capsule phase ships a plain panel, the real thing is the
    /// watch-face complication tuner (BOONS.md §8), and the reward flow cannot tell them
    /// apart. The reward director rolls WHAT is offered (each card carrying its own rarity);
    /// the presenter only shows it.
    /// </summary>
    public interface ITransmissionDraftPresenter
    {
        /// <summary>
        /// Shows the offer and calls <paramref name="onChosen"/> with the player's pick.
        /// Returns false when the presenter cannot show right now, in which case the caller
        /// falls back to an automatic pick rather than deadlocking the run.
        /// </summary>
        bool Present(IReadOnlyList<TransmissionOffer> offer, Action<TransmissionOffer> onChosen);
    }
}
