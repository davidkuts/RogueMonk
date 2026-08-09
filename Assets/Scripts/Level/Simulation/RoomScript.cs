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
}
