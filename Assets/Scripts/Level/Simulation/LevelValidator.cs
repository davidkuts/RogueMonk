using System.Collections.Generic;

namespace Game.Level
{
    /// <summary>
    /// Checks that a generated level is actually playable end to end. This is what the seeded
    /// soak test asserts over thousands of seeds (DESIGN.md § RNG &amp; death flow), so the
    /// definition of "solvable" lives here rather than being restated in the test.
    ///
    /// Solvable means: the level has rooms; every room has at least one wave; every wave has
    /// at least one enemy (an empty wave clears itself and would skip the fight); every spawn
    /// names a real archetype; and no wave assigns two enemies to the same spawn point or a
    /// spawn point the template does not have.
    /// </summary>
    public static class LevelValidator
    {
        public static bool IsSolvable(LevelPlan plan, ILevelGenerationSettings settings, out string reason)
        {
            if (plan == null)
            {
                reason = "null plan";
                return false;
            }

            if (plan.RoomCount == 0)
            {
                reason = "level has no rooms";
                return false;
            }

            if (settings != null && (plan.RoomCount < settings.MinRooms || plan.RoomCount > settings.MaxRooms))
            {
                reason = $"room count {plan.RoomCount} outside the configured {settings.MinRooms}-{settings.MaxRooms}";
                return false;
            }

            var templatesById = BuildTemplateLookup(settings);
            var archetypeIds = BuildArchetypeLookup(settings);

            for (int r = 0; r < plan.Rooms.Count; r++)
            {
                RoomPlan room = plan.Rooms[r];

                if (room.Waves.Count == 0)
                {
                    reason = $"room {r} ({room.TemplateId}) has no waves";
                    return false;
                }

                IRoomTemplate template = null;
                if (templatesById != null && !templatesById.TryGetValue(room.TemplateId, out template))
                {
                    reason = $"room {r} references unknown template '{room.TemplateId}'";
                    return false;
                }

                if (template != null && r == plan.Rooms.Count - 1 && !template.CanBeFinalRoom)
                {
                    reason = $"final room uses template '{room.TemplateId}', which is not allowed to be final";
                    return false;
                }

                for (int w = 0; w < room.Waves.Count; w++)
                {
                    WavePlan wave = room.Waves[w];

                    if (wave.Count == 0)
                    {
                        reason = $"room {r} wave {w} is empty, so the room would clear itself";
                        return false;
                    }

                    var usedSpawnPoints = new HashSet<int>();
                    for (int s = 0; s < wave.Spawns.Count; s++)
                    {
                        SpawnAssignment spawn = wave.Spawns[s];

                        if (archetypeIds != null && !archetypeIds.Contains(spawn.ArchetypeId))
                        {
                            reason = $"room {r} wave {w} references unknown archetype '{spawn.ArchetypeId}'";
                            return false;
                        }

                        if (spawn.SpawnPointIndex < 0 ||
                            (template != null && spawn.SpawnPointIndex >= template.SpawnPointCount))
                        {
                            reason = $"room {r} wave {w} uses spawn point {spawn.SpawnPointIndex}, " +
                                     $"which template '{room.TemplateId}' does not have";
                            return false;
                        }

                        if (!usedSpawnPoints.Add(spawn.SpawnPointIndex))
                        {
                            reason = $"room {r} wave {w} spawns two enemies on spawn point {spawn.SpawnPointIndex}";
                            return false;
                        }
                    }
                }
            }

            reason = null;
            return true;
        }

        static Dictionary<string, IRoomTemplate> BuildTemplateLookup(ILevelGenerationSettings settings)
        {
            if (settings == null || settings.Templates == null)
                return null;

            var lookup = new Dictionary<string, IRoomTemplate>();
            for (int i = 0; i < settings.Templates.Count; i++)
            {
                IRoomTemplate template = settings.Templates[i];
                if (template != null)
                    lookup[template.Id] = template;
            }

            return lookup;
        }

        static HashSet<string> BuildArchetypeLookup(ILevelGenerationSettings settings)
        {
            if (settings == null || settings.Archetypes == null)
                return null;

            var lookup = new HashSet<string>();
            for (int i = 0; i < settings.Archetypes.Count; i++)
            {
                IEnemyArchetype archetype = settings.Archetypes[i];
                if (archetype != null)
                    lookup.Add(archetype.Id);
            }

            return lookup;
        }
    }
}
