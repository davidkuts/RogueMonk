using System.Collections.Generic;

namespace Game.Level
{
    /// <summary>
    /// One authored spawn line inside a scripted wave: an archetype and how many of it.
    /// The generator resolves these to concrete spawn points at generation time.
    /// </summary>
    public readonly struct ScriptedSpawn
    {
        public readonly string ArchetypeId;
        public readonly int Count;

        public ScriptedSpawn(string archetypeId, int count)
        {
            ArchetypeId = archetypeId;
            Count = count < 1 ? 1 : count;
        }

        public override string ToString() => $"{ArchetypeId}x{Count}";
    }

    /// <summary>
    /// One authored fight a room slot can host: an ordered list of waves, each an ordered
    /// list of spawn lines. A room script offers one or more of these and the generator
    /// picks one per run with a seeded draw, so runs vary without the composition rules
    /// (ENEMIES_BIOME1.md §5) ever being left to a budget roll.
    /// </summary>
    public interface IRoomScriptVariant
    {
        IReadOnlyList<IReadOnlyList<ScriptedSpawn>> Waves { get; }

        /// <summary>
        /// True when this variant is the slot's elite duel.
        ///
        /// <para>Authored rather than inferred. The generator could look for an archetype with no
        /// budget weight and guess, but "is this the elite fight" is a composition decision the
        /// author already made when they wrote the variant — and a guess would silently change
        /// meaning the first time some other archetype was given weight 0 for an unrelated
        /// reason.</para>
        /// </summary>
        bool IsElite { get; }
    }

    /// <summary>
    /// The authored composition for one standard-room slot, positional: RoomScripts[2] is
    /// the third ordinary room of a level. A slot with no variants (or beyond the end of
    /// the list) falls back to the budget-weighted generator, which is what keeps an
    /// escalated level with more rooms than scripts still playable.
    /// </summary>
    public interface IRoomScript
    {
        IReadOnlyList<IRoomScriptVariant> Variants { get; }
    }

    /// <summary>
    /// What the level still owes in elites when a slot is drawn.
    ///
    /// <para>Exists because two independent 50/50 slots gave a distribution nobody asked for:
    /// 25% of runs met no elite at all and 25% met two. The elite duel is a set piece — the one
    /// fight in a biome that is a duel rather than a crowd — so a run should reliably contain
    /// exactly one, with the seed deciding WHICH rather than whether.</para>
    /// </summary>
    public enum EliteRequirement
    {
        /// <summary>Either variant is allowed; the draw is free.</summary>
        Free = 0,

        /// <summary>The level has not placed its elite and this is the last slot that can.</summary>
        Required = 1,

        /// <summary>The level already has its elite; this slot must draw an ordinary fight.</summary>
        Forbidden = 2,
    }
}
