using Game.Combat;
using Game.Core.Diagnostics;
using Game.Core.Player;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// A patch of hardened time on the floor. Whatever stands in it moves slowly; whatever it is
    /// doing, it keeps doing at full speed.
    ///
    /// <para>That distinction is the whole design. ENEMIES_BIOME1.md § 2.3 is explicit that a stall
    /// zone "slows movement only (not attacks)" and "never feels like a stun" — it exists to shape
    /// the arena, to make some ground expensive, so that a Swiftjaw pack working the flanks becomes
    /// a harder problem. A zone that also slowed attacks would be a soft stun-lock, and the player
    /// would have no answer at all while two more things closed in.</para>
    ///
    /// <para>Amber, per § 1's semantic channel: this is solidified time, and the player's learned
    /// rule for amber is "that blocks stagger or it blocks movement". Here it is movement.</para>
    ///
    /// <para><b>Zones do not stack.</b> Overlapping puddles resolve to the slowest, not the
    /// product — enforced by <see cref="PlayerMotor.ApplyExternalSpeedMultiplier"/> taking a
    /// minimum, so it holds however many Sailspits are in the room.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StallZone : MonoBehaviour
    {
        [SerializeField, Tooltip("Authored radius of the slowed ground, before the multiplier below.")]
        float radius = 1.5f;

        [SerializeField, Tooltip("Scales the authored radius. Kept as a separate number rather than folded into it so the rework's +50% is visible as a decision instead of hiding inside a retuned value.")]
        float radiusMultiplier = 1.5f;

        [SerializeField, Tooltip("How long the amber holds before it thins out.")]
        float durationSeconds = 2f;

        [SerializeField, Range(0.05f, 1f), Tooltip("Movement speed of anything standing in it. Overlapping zones take the slowest, never the product.")]
        float speedMultiplier = 0.45f;

        [SerializeField, Tooltip("Flat quad lying on the floor, scaled to the radius. Uses the telegraph material so the amber matches every other piece of hardened time.")]
        MeshRenderer decal;

        [SerializeField, Tooltip("Height above the floor. Enough to beat z-fighting, little enough to still read as painted on.")]
        float groundOffset = 0.02f;

        [SerializeField, Tooltip("The hue table, so the venom is the one venom rather than this component's idea of it.")]
        TelegraphPalette palette;

        [Header("Venom")]
        [SerializeField, Tooltip("Damage per tick to anything standing in it. The goo is area denial with a price for ignoring it, not a damage source to be farmed.")]
        float dotDamagePerTick = 1f;

        [SerializeField, Tooltip("Seconds of CONTINUOUS standing per tick. Nothing is dealt on entry: the first second inside is free, so walking through costs nothing and only loitering does.")]
        float dotIntervalSeconds = 1f;

        [SerializeField, Range(0f, 0.4f), Tooltip("How much the landed pool breathes. Motion is most of what separates 'arrived and dangerous' from the bright filling ring that warned about it.")]
        float pulseAmplitude = 0.08f;

        [SerializeField, Tooltip("Breaths per second of the landed pool.")]
        float pulseHz = 0.7f;

        [SerializeField, Range(0f, 1f), Tooltip("Base alpha once landed. Deliberately below the incoming telegraph's, so the warning is always the louder of the two.")]
        float landedAlpha = 0.5f;

        readonly DwellDamageClock dwell = new DwellDamageClock();
        PlayerHealth playerHealth;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int FillId = Shader.PropertyToID("_Fill");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int RectId = Shader.PropertyToID("_Rect");
        static readonly int ArcId = Shader.PropertyToID("_ArcDegrees");

        MaterialPropertyBlock block;
        PlayerMotor player;
        float remaining;

        /// <summary>The authored radius, before the multiplier.</summary>
        public float Radius => radius;

        /// <summary>
        /// The radius the puddle actually has. Everything — the overlap test, the decal, and the
        /// landing warning drawn before it exists — reads this one number, so the warning cannot
        /// promise a different footprint from the thing it warns about.
        /// </summary>
        public float EffectiveRadius => Mathf.Max(0.1f, radius * Mathf.Max(0f, radiusMultiplier));

        public float SpeedMultiplier => speedMultiplier;

        public float Remaining => remaining;

        /// <summary>Overrides the authored tuning, for a projectile that carries its own splash size.</summary>
        public void Configure(float zoneRadius, float seconds, float slow)
        {
            radius = Mathf.Max(0.1f, zoneRadius);
            durationSeconds = Mathf.Max(0.1f, seconds);
            speedMultiplier = Mathf.Clamp01(slow);
            remaining = durationSeconds;
            ApplyDecal();
        }

        void Awake()
        {
            block = new MaterialPropertyBlock();
            remaining = durationSeconds;

            // Found once rather than searched per frame; a room has one player and it does not
            // change while a two-second puddle is alive.
            GameObject found = GameObject.FindWithTag("Player");
            if (found != null)
            {
                player = found.GetComponent<PlayerMotor>();
                playerHealth = found.GetComponent<PlayerHealth>();
            }

            ApplyDecal();
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            remaining -= deltaTime;
            if (remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (player == null)
                return;

            // Planar: a puddle is a patch of ground, so height must not decide whether you are in
            // it. Without flattening, a player standing on anything at all would step out of it.
            Vector3 delta = player.transform.position - transform.position;
            delta.y = 0f;

            float effective = EffectiveRadius;
            bool inside = delta.sqrMagnitude <= effective * effective;
            if (inside)
                player.ApplyExternalSpeedMultiplier(speedMultiplier);

            // The slow is what the goo IS; the damage is the price of ignoring it. One tick per
            // whole second of unbroken standing, nothing on entry — see DwellDamageClock for why
            // the first second is deliberately free.
            int ticks = dwell.Tick(deltaTime, inside, dotIntervalSeconds);
            if (ticks > 0 && playerHealth != null && dotDamagePerTick > 0f)
                playerHealth.ApplyDamageOverTime(dotDamagePerTick * ticks, "venom");

            FadeDecal();
        }

        void ApplyDecal()
        {
            if (decal == null)
                return;

            decal.transform.localScale = new Vector3(EffectiveRadius * 2f, EffectiveRadius * 2f, 1f);

            Vector3 position = decal.transform.position;
            decal.transform.position = new Vector3(position.x, transform.position.y + groundOffset, position.z);

            if (block == null)
                block = new MaterialPropertyBlock();

            decal.GetPropertyBlock(block);

            // Venom green, not amber. The puddle was amber while it only slowed — amber's learned
            // rule is "it blocks stagger or it blocks movement" and that was honest. Now that it
            // also damages, it is a floor that hurts, and it must not look like the armour plates
            // and stall patches the player has learned are merely expensive to walk on.
            block.SetColor(ColorId, TelegraphPalette.Resolve(palette, TelegraphChannel.Venom, new Color(0.20f, 0.62f, 0.30f, 1f)));
            block.SetFloat(FillId, 1f);      // already resolved, so it is filled rather than filling
            block.SetFloat(AlphaId, landedAlpha);
            block.SetFloat(RectId, 0f);
            block.SetFloat(ArcId, 360f);
            decal.SetPropertyBlock(block);
        }

        /// <summary>
        /// Thins the amber out across the last of its life, so the ground stops being expensive at
        /// a moment the player can see coming rather than at a moment they have to have counted.
        /// </summary>
        void FadeDecal()
        {
            if (decal == null || durationSeconds <= 0f)
                return;

            float fade = Mathf.Clamp01(remaining / Mathf.Min(0.6f, durationSeconds));

            // A slow breath, so a landed pool reads as alive and static where the incoming warning
            // read as a boundary being drawn. Motion is the clearest separator the two have — the
            // ring grows once and stops; this never stops.
            float pulse = 1f + Mathf.Sin(Time.time * pulseHz * Mathf.PI * 2f) * pulseAmplitude;

            decal.GetPropertyBlock(block);
            block.SetFloat(AlphaId, landedAlpha * fade * pulse);
            decal.SetPropertyBlock(block);
        }

        void OnDestroy() => GameLog.Debug(LogCategory.Enemy, "stall zone expired");
    }
}
