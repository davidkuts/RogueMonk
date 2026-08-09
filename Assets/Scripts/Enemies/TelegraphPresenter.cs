using System.Collections.Generic;
using Game.Combat;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// The one component that draws "something is about to happen here".
    ///
    /// <para>Two channels, because one is not enough. The body flash says <em>when</em> and, via
    /// the palette, <em>what class of threat</em>; the ground decal says <em>where</em> and
    /// <em>how far</em>. On a capsule with no animation the flash alone can only be answered by
    /// frame-perfect timing, which is precisely the thing DESIGN.md's telegraph grammar exists to
    /// avoid — a wind-up should be answerable by standing somewhere else.</para>
    ///
    /// <para>Decals are pooled rather than pre-placed because a fan needs several at once and a
    /// bite needs one. The pool grows to whatever the widest attack on this enemy asks for and
    /// then stops.</para>
    ///
    /// <para>It never invents a colour. Every hue comes from <see cref="TelegraphPalette"/> via
    /// the attack's declared <see cref="TelegraphChannel"/>, so an enemy physically cannot ship a
    /// tell in a hue that already means something else.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TelegraphPresenter : MonoBehaviour
    {
        [SerializeField, Tooltip("Body whose primitives flash. Left empty, taken from this GameObject.")]
        EnemyActor actor;

        [SerializeField, Tooltip("The locked hue table. Without one, attacks fall back to their authored colour.")]
        TelegraphPalette palette;

        [SerializeField, Tooltip("Prefab holding a TelegraphDecal. Instantiated on demand — one per lane for a fan, one for everything else.")]
        TelegraphDecal decalPrefab;

        [SerializeField, Tooltip("Ceiling on pooled decals. A fan of five spines is the widest thing in Biome 1.")]
        int maxDecals = 6;

        [SerializeField, Tooltip("Height the hitbox is measured from, matching the attacker's own query origin.")]
        float hitboxHeightOffset = 0.9f;

        readonly List<TelegraphDecal> pool = new List<TelegraphDecal>();

        int shownThisFrame;

        void Awake()
        {
            if (actor == null)
                actor = GetComponent<EnemyActor>();
        }

        void OnDisable() => Hide();

        /// <summary>The colour this attack should flash, resolved through the palette.</summary>
        public Color ColorFor(AttackDefinition attack) =>
            attack != null ? attack.ResolveTelegraphColor(palette) : Color.white;

        /// <summary>Direct palette access, for effects that are not attached to an attack asset.</summary>
        public Color ColorFor(TelegraphChannel channel, Color fallback) =>
            TelegraphPalette.Resolve(palette, channel, fallback);

        /// <summary>
        /// Draws a single-footprint telegraph: a bite, an arc sweep, a stomp ring, or a charge lane
        /// (which is an ordinary box hitbox — long, narrow, offset forward).
        /// </summary>
        /// <param name="progress">0..1 through the wind-up. The fill reaches the outline exactly as the attack goes active.</param>
        public void Show(AttackDefinition attack, Vector3 origin, Vector3 forward, float progress, float groundY)
        {
            if (attack == null)
                return;

            BeginFrame();
            FlashBody(ColorFor(attack), progress);
            DrawOne(attack.Hitbox, origin, forward, ColorFor(attack), progress, groundY);
            EndFrame();
        }

        /// <summary>
        /// Draws one footprint per lane, spread evenly across <paramref name="totalSpreadDegrees"/>
        /// and centred on facing.
        ///
        /// <para>The gaps between lanes are the point: Sailspit's spine fan is a projectile-reading
        /// exercise whose answer is a dash <em>between</em> the spines, so the telegraph has to draw
        /// the spines rather than the cone that contains them. A single wide wedge would say "there
        /// is no way through", which would be a lie.</para>
        /// </summary>
        public void ShowFan(
            AttackDefinition attack,
            Vector3 origin,
            Vector3 forward,
            float totalSpreadDegrees,
            int laneCount,
            float progress,
            float groundY)
        {
            if (attack == null || laneCount <= 0)
                return;

            BeginFrame();

            Color color = ColorFor(attack);
            FlashBody(color, progress);

            for (int i = 0; i < laneCount && i < maxDecals; i++)
            {
                // Single lane fires straight ahead; anything more spreads evenly end to end.
                float t = laneCount == 1 ? 0.5f : i / (float)(laneCount - 1);
                float angle = Mathf.Lerp(-totalSpreadDegrees * 0.5f, totalSpreadDegrees * 0.5f, t);
                Vector3 laneForward = Quaternion.AngleAxis(angle, Vector3.up) * forward;

                DrawOne(attack.Hitbox, origin, laneForward, color, progress, groundY);
            }

            EndFrame();
        }

        /// <summary>Clears the flash and every decal. Safe to call every frame.</summary>
        public void Hide()
        {
            if (actor != null)
            {
                actor.TelegraphOverride = null;
                actor.TelegraphProgress = 0f;
            }

            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null)
                    pool[i].Hide();
            }

            shownThisFrame = 0;
        }

        /// <summary>The origin an attack's hitbox is measured from — matched to the attacker's own query.</summary>
        public Vector3 HitboxOrigin => transform.position + Vector3.up * hitboxHeightOffset;

        void BeginFrame() => shownThisFrame = 0;

        /// <summary>
        /// Hides whatever the previous frame drew and this one did not. Without it, a fan of five
        /// followed by a bite would leave four decals painted on the floor with nothing behind them.
        /// </summary>
        void EndFrame()
        {
            for (int i = shownThisFrame; i < pool.Count; i++)
            {
                if (pool[i] != null)
                    pool[i].Hide();
            }
        }

        void FlashBody(Color color, float progress)
        {
            if (actor == null)
                return;

            actor.TelegraphOverride = color;
            actor.TelegraphProgress = Mathf.Clamp01(progress);
        }

        void DrawOne(in HitboxShape shape, Vector3 origin, Vector3 forward, Color color, float progress, float groundY)
        {
            TelegraphDecal decal = Rent();
            if (decal == null)
                return;

            decal.Show(shape, origin, forward, color, Mathf.Clamp01(progress), groundY);
        }

        TelegraphDecal Rent()
        {
            if (shownThisFrame < pool.Count)
                return pool[shownThisFrame++];

            if (decalPrefab == null || pool.Count >= maxDecals)
                return null;

            TelegraphDecal instance = Instantiate(decalPrefab, transform.position, Quaternion.identity, transform);
            pool.Add(instance);
            shownThisFrame++;
            return instance;
        }
    }
}
