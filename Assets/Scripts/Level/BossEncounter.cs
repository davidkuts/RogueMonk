using System.Collections.Generic;
using Game.Enemies;

namespace Game.Level
{
    /// <summary>
    /// Adapts a live boss to <see cref="IBossEncounter"/>. Lives in Game.Level, which already
    /// references Game.Enemies, so the UI can read a boss without ever seeing one.
    /// </summary>
    public sealed class BossEncounter : IBossEncounter
    {
        readonly EnemyActor actor;
        readonly BossController controller;
        readonly List<float> thresholds = new List<float>();

        public BossEncounter(EnemyActor actor, BossController controller)
        {
            this.actor = actor;
            this.controller = controller;

            IBossDefinition definition = controller != null ? controller.Definition : null;
            DisplayName = definition != null ? definition.DisplayName : "Boss";

            if (definition?.Phases != null)
            {
                for (int i = 0; i < definition.Phases.Count; i++)
                    thresholds.Add(definition.Phases[i].HealthFractionThreshold);
            }
        }

        public string DisplayName { get; }

        public IReadOnlyList<float> PhaseThresholds => thresholds;

        public int PhaseCount => controller?.Brain?.PhaseCount ?? 1;

        public int PhaseIndex => controller?.Brain?.PhaseIndex ?? 0;

        public float HealthFraction => controller != null ? controller.HealthFraction : 0f;

        // Deliberately not IsDying-aware: once the killing blow lands the bar should read empty
        // and get out of the way, even though the body is still playing its death beat.
        public bool IsAlive => actor != null && actor.IsAlive;
    }
}
