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
    /// spawn point the template does not have. Swarm archetypes that declare
    /// <see cref="IEnemyArchetype.AllowsSharedSpawnPoints"/> are exempt from the unique-point
    /// rule only — a swarm's size is authored, not capped by the room's point count.
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

            // Exactly one boss room, and it must be last — otherwise the level either has no
            // climax or ends on an ordinary fight.
            int bossCount = 0;
            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                if (plan.Rooms[i].IsBossRoom)
                    bossCount++;
            }

            if (bossCount != 1)
            {
                reason = $"level has {bossCount} boss rooms, expected exactly 1";
                return false;
            }

            if (!plan.FinalRoom.IsBossRoom)
            {
                reason = "the boss room is not the last room";
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

                if (template != null && (template.SupportedRoles & room.Role) == 0)
                {
                    reason = $"room {r} is {room.Role} but template '{room.TemplateId}' only supports {template.SupportedRoles}";
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

                        IEnemyArchetype archetype = null;
                        if (archetypeIds != null && !archetypeIds.TryGetValue(spawn.ArchetypeId, out archetype))
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

                        bool sharedAllowed = archetype != null && archetype.AllowsSharedSpawnPoints;
                        if (!usedSpawnPoints.Add(spawn.SpawnPointIndex) && !sharedAllowed)
                        {
                            reason = $"room {r} wave {w} spawns two enemies on spawn point {spawn.SpawnPointIndex}";
                            return false;
                        }
                    }
                }
            }

            // Ordered last on purpose. Several existing tests hand-build a boss room out of
            // ordinary spawns to exercise the per-wave rules above and assert on the reason they
            // produce; a boss check that fired first would fail all of them for the wrong reason.
            if (settings != null && !string.IsNullOrEmpty(settings.BossArchetypeId))
            {
                WavePlan bossWave = plan.FinalRoom.Waves[0];
                int bossSpawns = 0;
                for (int s = 0; s < bossWave.Spawns.Count; s++)
                {
                    if (bossWave.Spawns[s].ArchetypeId == settings.BossArchetypeId)
                        bossSpawns++;
                }

                if (bossSpawns != 1)
                {
                    reason = $"the boss room's first wave holds {bossSpawns} of '{settings.BossArchetypeId}', expected exactly 1";
                    return false;
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

        static Dictionary<string, IEnemyArchetype> BuildArchetypeLookup(ILevelGenerationSettings settings)
        {
            if (settings == null || settings.Archetypes == null)
                return null;

            var lookup = new Dictionary<string, IEnemyArchetype>();
            for (int i = 0; i < settings.Archetypes.Count; i++)
            {
                IEnemyArchetype archetype = settings.Archetypes[i];
                if (archetype != null)
                    lookup[archetype.Id] = archetype;
            }

            return lookup;
        }
    }
}
