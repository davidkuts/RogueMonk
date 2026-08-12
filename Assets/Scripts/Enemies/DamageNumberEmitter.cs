using System.Collections.Generic;
using Game.Combat;
using Game.Core.Feedback;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// Pops a floating number for every hit this body takes, coloured by what landed.
    ///
    /// <para>A component on the body rather than a hook in the resolver, so it is removable per
    /// enemy and so nothing in the simulation learns about presentation — the same shape as
    /// <see cref="AmberSheen"/> and <see cref="DeathPop"/>. The labels themselves are pooled once
    /// by <see cref="DamageNumberDirector"/>; this only decides the colour and asks.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageNumberEmitter : MonoBehaviour
    {
        [SerializeField, Tooltip("The body whose hits this reports. Found on this GameObject when left empty.")]
        EnemyActor actor;

        [SerializeField, Tooltip("Where damage-type colours come from. ONE asset, shared with the hit sparks, so a spark and its number can never disagree about what just landed.")]
        DamageTypePalette palette;

        [SerializeField, Tooltip("Hitstop at or above this counts as a heavy hit, which reads in the Physical colour and at a larger size.")]
        float heavyHitstopThreshold = 0.08f;

        [SerializeField, Tooltip("How much bigger a heavy hit's number is. The kick and the Riposte should land visibly harder than a jab.")]
        float heavyScale = 1.45f;

        [SerializeField, Tooltip("Show a 0 in the amber hue when a guard refuses a hit. This is the 'that did nothing' read in numbers rather than in colour.")]
        bool showRefusedHits = true;

        [SerializeField, Tooltip("Flush interval and size for damage-over-time numbers. Left empty, DoT ticks fall back to one number a second at 70% size.")]
        DotSettings dotSettings;

        /// <summary>
        /// Pools this body's DoT damage so a stack of burns prints one figure a second rather than
        /// a stream of ones. Per body, because it lives on the body.
        /// </summary>
        readonly DotNumberAccumulator dotNumbers = new DotNumberAccumulator();

        const float FallbackFlushSeconds = 1f;
        const float FallbackDotScale = 0.7f;

        void Awake()
        {
            if (actor == null)
                actor = GetComponent<EnemyActor>();

            if (actor != null)
                actor.DamageResolved += OnDamageResolved;
        }

        void OnDestroy()
        {
            if (actor != null)
                actor.DamageResolved -= OnDamageResolved;
        }

        /// <summary>
        /// Lets the pooled DoT damage out on its interval.
        ///
        /// <para>The figure is drawn at the body's CURRENT position rather than where the tick
        /// landed: up to a second has passed, and a number left behind at a stale point would
        /// describe a body that has walked away from it.</para>
        /// </summary>
        void Update()
        {
            float interval = dotSettings != null ? dotSettings.NumberFlushIntervalSeconds : FallbackFlushSeconds;
            float dotScale = dotSettings != null ? dotSettings.NumberScale : FallbackDotScale;

            IReadOnlyList<DotNumberAccumulator.Flush> due = dotNumbers.Tick(Time.deltaTime, interval);
            if (due.Count == 0)
                return;

            Vector3 point = transform.position + Vector3.up;
            for (int i = 0; i < due.Count; i++)
            {
                // Smaller, in the DoT's own colour, and deliberately without the arrival punch:
                // a tick is not a hit, and it must never compete with the number that says the
                // player landed something.
                DamageNumberDirector.Show(
                    point, due[i].Amount, due[i].Definition.NumberColor, dotScale,
                    allowZero: false, punch: false);
            }
        }

        void OnDamageResolved(DamageReport report)
        {
            // A DoT tick is pooled rather than shown, which is the whole answer to an enemy under
            // nine stacks throwing a number every few frames.
            if (report.Dot != null)
            {
                dotNumbers.Add(report.Dot, Mathf.RoundToInt(report.Amount));
                return;
            }

            if (report.Refused)
            {
                // A refused hit reports zero, which Pop would otherwise discard — and a hit that
                // silently prints nothing is exactly the confusion the amber sheen exists to fix.
                if (showRefusedHits)
                {
                    Color amber = palette != null ? palette.Refused : new Color(1f, 0.67f, 0.13f, 1f);
                    DamageNumberDirector.Show(report.Point, 0f, amber, 1f, allowZero: true);
                }

                return;
            }

            if (report.Amount <= 0f)
                return;

            bool heavy = report.HitstopSeconds >= heavyHitstopThreshold;
            Color color = DamageTypePalette.Resolve(
                palette, report.Type, heavy, new Color(1f, 0.85f, 0.45f, 0.9f));

            DamageNumberDirector.Show(report.Point, report.Amount, color, heavy ? heavyScale : 1f);
        }
    }
}
