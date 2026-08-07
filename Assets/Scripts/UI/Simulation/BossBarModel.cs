using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Decides what a boss bar shows: the fill, the trailing "chip" that makes each hit read as an
    /// amount lost, and the one-frame edge when a phase breaks.
    ///
    /// Split out from the view because scene-authored uGUI cannot be tested, but this can. The view
    /// keeps only the parts that genuinely need a Canvas.
    /// </summary>
    public sealed class BossBarModel
    {
        readonly float chipDrainPerSecond;
        readonly float chipDelaySeconds;
        readonly List<float> thresholds = new List<float>();

        float chipHold;
        int lastPhaseIndex;

        public BossBarModel(float chipDrainPerSecond, float chipDelaySeconds)
        {
            this.chipDrainPerSecond = Mathf.Max(0f, chipDrainPerSecond);
            this.chipDelaySeconds = Mathf.Max(0f, chipDelaySeconds);
        }

        /// <summary>Current health, 0..1.</summary>
        public float Fill { get; private set; } = 1f;

        /// <summary>The trailing bar. Never below <see cref="Fill"/>.</summary>
        public float Chip { get; private set; } = 1f;

        public int PhaseIndex { get; private set; }

        public int PhaseCount { get; private set; } = 1;

        /// <summary>Health fractions where each later phase begins, for the divider ticks.</summary>
        public IReadOnlyList<float> PhaseThresholds => thresholds;

        /// <summary>
        /// True for exactly one <see cref="Tick"/> after a phase boundary is crossed, so the view
        /// can fire a one-shot flash without having to remember the previous phase itself.
        /// </summary>
        public bool PhaseJustBroke { get; private set; }

        public void Bind(int phaseCount, IReadOnlyList<float> phaseThresholds)
        {
            PhaseCount = Mathf.Max(1, phaseCount);

            thresholds.Clear();
            if (phaseThresholds != null)
            {
                for (int i = 0; i < phaseThresholds.Count; i++)
                    thresholds.Add(Mathf.Clamp01(phaseThresholds[i]));
            }

            Fill = 1f;
            Chip = 1f;
            chipHold = 0f;
            PhaseIndex = 0;
            lastPhaseIndex = 0;
            PhaseJustBroke = false;
        }

        public void Tick(float unscaledDeltaTime, float healthFraction, int phaseIndex)
        {
            float fraction = Mathf.Clamp01(healthFraction);

            PhaseIndex = Mathf.Clamp(phaseIndex, 0, Mathf.Max(0, PhaseCount - 1));
            PhaseJustBroke = PhaseIndex > lastPhaseIndex;
            lastPhaseIndex = PhaseIndex;

            if (unscaledDeltaTime <= 0f)
            {
                // Still track the value, but do not age the chip — a paused frame is not a hit.
                Fill = fraction;
                Chip = Mathf.Max(Chip, Fill);
                return;
            }

            if (fraction < Fill - 0.0001f && chipHold <= 0f)
                chipHold = chipDelaySeconds;

            Fill = fraction;

            if (chipHold > 0f)
                chipHold -= unscaledDeltaTime;
            else
                Chip = Mathf.MoveTowards(Chip, Fill, chipDrainPerSecond * unscaledDeltaTime);

            Chip = Mathf.Max(Chip, Fill);
        }

        public void Clear()
        {
            Fill = 1f;
            Chip = 1f;
            chipHold = 0f;
            PhaseIndex = 0;
            lastPhaseIndex = 0;
            PhaseCount = 1;
            PhaseJustBroke = false;
            thresholds.Clear();
        }
    }
}
