using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The one source of each giver's identity colour (BOONS.md §8 materials, pushed loud).
    /// Shared by the draft cards, the door previews and the reward pickups, so "who is
    /// calling" is the same hue everywhere it appears — two lists would drift, and a door that
    /// disagreed with the card it opens would be worse than no colour at all.
    /// </summary>
    public static class GiverPalette
    {
        public static Color ColorOf(GiverId giver)
        {
            switch (giver)
            {
                case GiverId.Overclock: return new Color(1.00f, 0.45f, 0.15f); // hot ember
                case GiverId.Fray: return new Color(0.30f, 0.85f, 0.40f);      // verdigris green
                case GiverId.Stasis: return new Color(0.30f, 0.90f, 1.00f);    // ice
                case GiverId.Echo: return new Color(1.00f, 0.88f, 0.25f);      // tick gold
                case GiverId.Ward: return new Color(0.65f, 0.72f, 0.82f);      // steel casing
                case GiverId.Flux: return new Color(0.92f, 0.40f, 1.00f);      // off-spectrum
                default: return Color.white;
            }
        }
    }
}
