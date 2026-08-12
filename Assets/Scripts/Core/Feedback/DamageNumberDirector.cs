using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Feedback
{
    /// <summary>
    /// Pools and hands out the floating damage numbers.
    ///
    /// <para>A scene singleton reached through a static helper, exactly like
    /// <see cref="RumbleDirector"/> and the audio director: the systems that raise damage should
    /// not each own a pool, and a swarm room would otherwise carry six of them.</para>
    ///
    /// <para>Deliberately colour-agnostic. <c>DamageType</c> lives in Game.Combat and Game.Core
    /// does not reference it, so callers resolve their own hue and hand it in — which also keeps
    /// the one damage-type palette in the assembly that owns the enum.</para>
    ///
    /// <para>Numbers are dropped rather than queued when the pool is exhausted. A flurry that
    /// outruns the pool is already unreadable, and allocating mid-flurry is the one thing that
    /// would actually be felt.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageNumberDirector : MonoBehaviour
    {
        public static DamageNumberDirector Instance { get; private set; }

        [SerializeField, Tooltip("Material the digit segments carry. Must be a URP material from the project — CreatePrimitive's default draws magenta in a build.")]
        Material digitMaterial;

        [SerializeField, Tooltip("Numbers alive at once. Beyond this, further hits simply do not pop one.")]
        int poolSize = 24;

        [SerializeField, Tooltip("World height of a digit. Small: the number is a read, not a headline.")]
        float scale = 0.34f;

        [SerializeField, Tooltip("How long a number lives. It holds full strength for the first half and fades over the second.")]
        float lifetimeSeconds = 0.75f;

        [SerializeField, Tooltip("Initial upward speed. The number lofts and settles rather than sliding straight up.")]
        float riseSpeed = 2.4f;

        [SerializeField, Tooltip("Sideways spread, so numbers landing on the same body do not stack into an unreadable pile.")]
        float spread = 0.9f;

        [SerializeField, Tooltip("Vertical offset above the hit point, so the number clears the body it describes.")]
        float heightOffset = 0.35f;

        [SerializeField, Tooltip("Downward pull on the arc. The number lofts and settles rather than sliding away in a straight line; 0 makes it drift flat.")]
        float arcGravity = 2.2f;

        readonly List<DamageNumberLabel> pool = new List<DamageNumberLabel>();

        /// <summary>
        /// Alternates the sideways drift left and right. A counter rather than a random draw:
        /// cosmetics never touch the run RNG, or simply landing more hits would change the level.
        /// </summary>
        int spreadStep;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            for (int i = 0; i < Mathf.Max(1, poolSize); i++)
            {
                var go = new GameObject($"DamageNumber_{i}");
                go.transform.SetParent(transform, false);
                var label = go.AddComponent<DamageNumberLabel>();
                label.Build(digitMaterial);
                pool.Add(label);
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Pops <paramref name="amount"/> at <paramref name="worldPosition"/>. Amounts are rounded
        /// to whole numbers — a "7.3" tells the player nothing a "7" does not, and the fractions
        /// only ever arrive from modifier maths.
        /// </summary>
        public void Pop(
            Vector3 worldPosition, float amount, Color color, float scaleMultiplier = 1f,
            bool allowZero = false, bool punch = true)
        {
            int value = Mathf.RoundToInt(amount);

            // Zero is normally noise — a rounded-down modifier remainder, nothing the player did.
            // The exception is a hit an amber guard refused, where the zero IS the information.
            if (value == 0 && !allowZero)
                return;

            DamageNumberLabel label = Take();
            if (label == null)
                return;

            // Fan alternately left and right of the hit, widening every other pair, so a combo
            // landing on one body reads as several numbers rather than one flickering one.
            float side = (spreadStep % 2 == 0 ? 1f : -1f) * (0.4f + 0.3f * (spreadStep % 4));
            spreadStep = (spreadStep + 1) % 8;

            Vector3 drift = new Vector3(side * spread, riseSpeed, 0f);

            label.Show(
                worldPosition + Vector3.up * heightOffset,
                value, color, scale * scaleMultiplier, lifetimeSeconds, drift, arcGravity, punch);
        }

        DamageNumberLabel Take()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && !pool[i].gameObject.activeSelf)
                    return pool[i];
            }

            return null;
        }

        /// <summary>Convenience for callers that may run before the director exists.</summary>
        public static void Show(
            Vector3 worldPosition, float amount, Color color, float scaleMultiplier = 1f,
            bool allowZero = false, bool punch = true)
        {
            if (Instance != null)
                Instance.Pop(worldPosition, amount, color, scaleMultiplier, allowZero, punch);
        }
    }
}
