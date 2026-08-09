using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The one asset holding what each <see cref="TelegraphChannel"/> looks like.
    ///
    /// <para>Exists so the grammar has a single enforcement point. Retuning "incoming projectile"
    /// is one field here rather than a hunt through every attack asset that happens to fire one,
    /// and a new enemy physically cannot invent a hue that already means something else — it picks
    /// a channel or it declares itself Custom, which is visible in review.</para>
    ///
    /// <para>Defaults are the values locked in DESIGN.md § Telegraph grammar as amended
    /// 2026-08-09.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Telegraph Palette", fileName = "TelegraphPalette")]
    public sealed class TelegraphPalette : ScriptableObject
    {
        [SerializeField, Tooltip("Melee arc or centred burst. The oldest and most common tell.")]
        Color meleeArc = new Color(1f, 0.25f, 0.20f, 1f);

        [SerializeField, Tooltip("Incoming projectile. Moved off amber 2026-08-09 when ENEMIES_BIOME1.md reserved amber for hardened time.")]
        Color projectile = new Color(1f, 0.18f, 0.78f, 1f);

        [SerializeField, Tooltip("Gap-closer: 'it is coming to you'. Cerashorn's charge, Twice-Struck's horn rush, the boss Slam.")]
        Color gapCloser = new Color(0.65f, 0.35f, 1f, 1f);

        [SerializeField, Tooltip("Delayed static floor hazard. The boss Eruption, the Tyrant's junk-rain impact circles.")]
        Color groundHazard = new Color(0.80f, 0.95f, 0.20f, 1f);

        [SerializeField, Tooltip("Solidified time: armour plates, stall zones, boss armour patches. Never anything else.")]
        Color hardenedTime = new Color(1f, 0.67f, 0.13f, 1f);

        [SerializeField, Tooltip("Temporal echo. Paler than the locked dash blue on purpose, so the player's own colour stays theirs.")]
        Color echo = new Color(0.78f, 0.95f, 0.98f, 1f);

        /// <summary>
        /// The colour for <paramref name="channel"/>, or <paramref name="authored"/> when the
        /// attack declares itself Custom.
        /// </summary>
        public Color Resolve(TelegraphChannel channel, Color authored)
        {
            switch (channel)
            {
                case TelegraphChannel.MeleeArc: return meleeArc;
                case TelegraphChannel.Projectile: return projectile;
                case TelegraphChannel.GapCloser: return gapCloser;
                case TelegraphChannel.GroundHazard: return groundHazard;
                case TelegraphChannel.HardenedTime: return hardenedTime;
                case TelegraphChannel.Echo: return echo;
                default: return authored;
            }
        }

        /// <summary>
        /// Resolves through <paramref name="palette"/> when there is one, and falls back to the
        /// authored colour when there is not. Lets a prefab work with the palette left unassigned
        /// instead of drawing every telegraph black.
        /// </summary>
        public static Color Resolve(TelegraphPalette palette, TelegraphChannel channel, Color authored) =>
            palette != null ? palette.Resolve(channel, authored) : authored;
    }
}
