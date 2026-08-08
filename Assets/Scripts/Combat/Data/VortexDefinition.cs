using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// The Undertow — the default anti-swarm ability (DESIGN.md § The Vortex).
    ///
    /// <para>Implements <see cref="IAttackDefinition"/> directly rather than pointing at a separate
    /// attack asset. The ability *is* an attack with a pull bolted on: it runs through the same
    /// <see cref="AttackStateMachine"/>, obeys the same wind-up/active/recovery grammar and the same
    /// dash-cancel rule as a punch. Splitting it across two assets would put its frame data in one
    /// inspector and its radius in another, which is exactly how the two drift apart.</para>
    ///
    /// <para>Its job is space and setup, not damage: the ticks total less than one punch, and the
    /// only thing that scales them is a boon, through the ordinary hit-modifier pipeline.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Vortex Definition", fileName = "VortexDefinition")]
    public sealed class VortexDefinition : ScriptableObject, IAttackDefinition
    {
        [Header("Frame data (seconds)")]
        [SerializeField, Tooltip("Committed wind-up — the tell that a spin is coming.")]
        float windupSeconds = 0.12f;
        [SerializeField, Tooltip("The spin itself: pull and damage ticks both run across this window.")]
        float activeSeconds = 0.45f;
        [SerializeField, Tooltip("Trailing recovery. Dash-cancellable from its first frame, which forfeits any remaining ticks.")]
        float recoverySeconds = 0.25f;

        [Header("Pull")]
        [SerializeField, Tooltip("How far out the spin reaches. Too big trivialises a room; too small whiffs on a spread swarm.")]
        float radiusMeters = 4f;
        [SerializeField, Tooltip("Where the pull delivers them: inside kick range, outside body-block jank.")]
        float innerRingMeters = 1.5f;
        [SerializeField, Tooltip("Inward impulse per metre of overshoot beyond the inner ring. Applied as negative knockback through the existing impulse system, so a target already on the ring is left alone.")]
        float pullImpulsePerMeter = 8f;
        [SerializeField, Tooltip("Ceiling on that impulse, so a target caught at the rim is not slingshotted through the player.")]
        float maxPullImpulse = 24f;
        [SerializeField, Tooltip("How long a target is immune to being pulled again after arriving. Prevents vortex-into-vortex juggle loops once a boon cuts the cooldown.")]
        float pullImmunitySeconds = 1f;

        [Header("Ticks")]
        [SerializeField, Tooltip("Damage ticks spread across the active window. Each is a separate resolved hit, so each takes its own boon modifiers and its own spark.")]
        int tickCount = 3;
        [SerializeField, Tooltip("Damage per tick. All ticks together should total less than one punch — the vortex gathers, the follow-up spends.")]
        float tickDamage = 3f;
        [SerializeField, Tooltip("Poise damage per tick, around punch level.")]
        float tickPoiseDamage = 8f;
        [SerializeField] DamageType damageType = DamageType.Physical;
        [SerializeField, Tooltip("Freeze per tick. Deliberately tiny: three ticks across a crowd at punch-level hitstop would stutter the whole spin.")]
        float tickHitstopSeconds = 0.02f;

        [Header("Arrival")]
        [SerializeField, Tooltip("Stagger applied to everything the pull actually moved. Non-negotiable: the ability drags threats into hug range, so they must not arrive mid-wind-up.")]
        float arrivalStaggerSeconds = 0.4f;

        [Header("Recharge")]
        [SerializeField, Tooltip("Cooldown from a standing start. Tune this for the passive player — it is their whole experience of the ability.")]
        float cooldownSeconds = 10f;
        [SerializeField, Tooltip("Shaved off the cooldown by every landed player hit. Whiffs pay nothing, so uptime rides aggression rather than button-mashing.")]
        float perHitRefundSeconds = 0.4f;

        [Header("Movement")]
        [SerializeField, Range(0f, 1f), Tooltip("Walk speed during the spin. 0 roots the player: the vortex moves enemies, not the monk.")]
        float moveSpeedMultiplier;

        [Header("Telegraph")]
        [SerializeField, Tooltip("The reserved dash hue. The Undertow is the Second Hand's doing, so it reads as part of the same time-kit as the Blink.")]
        Color telegraphColor = new Color(0.3f, 0.9f, 1f, 1f);

        public string Id => name;
        public float WindupSeconds => windupSeconds;
        public float ActiveSeconds => activeSeconds;
        public float RecoverySeconds => recoverySeconds;

        /// <summary>Always true — the cancel rule is universal, and cancelling forfeits the rest of the spin.</summary>
        public bool CancellableOnRecovery => true;

        /// <summary>Zero: the vortex is outside the chain and must not extend a combo window.</summary>
        public float ComboWindowSeconds => 0f;

        /// <summary>
        /// Deliberately empty. <see cref="PlayerVortex"/> owns every hit this ability makes, because
        /// one target has to be struck once per tick rather than once per attack — which is the one
        /// thing the shared hitbox pass is built never to do.
        /// </summary>
        public HitboxShape Hitbox => HitboxShape.DefaultSphere;

        public float Damage => tickDamage;
        public DamageType DamageType => damageType;
        public float PoiseDamage => tickPoiseDamage;

        /// <summary>Negative: this is a pull. Same impulse system as knockback, reversed sign.</summary>
        public float Knockback => -pullImpulsePerMeter;

        public float HitstopSeconds => tickHitstopSeconds;

        /// <summary>A radial move has no direction to assist, so auto-aim is switched off outright.</summary>
        public float AutoAimConeDegrees => 0f;

        public float AutoAimRangeMeters => 0f;
        public float AimSnapSpeedDegPerSec => 0f;
        public float MoveSpeedMultiplier => moveSpeedMultiplier;

        public float RadiusMeters => Mathf.Max(0f, radiusMeters);
        public float InnerRingMeters => Mathf.Max(0f, innerRingMeters);
        public float PullImpulsePerMeter => Mathf.Max(0f, pullImpulsePerMeter);
        public float MaxPullImpulse => Mathf.Max(0f, maxPullImpulse);
        public float PullImmunitySeconds => Mathf.Max(0f, pullImmunitySeconds);
        public int TickCount => Mathf.Max(1, tickCount);
        public float ArrivalStaggerSeconds => Mathf.Max(0f, arrivalStaggerSeconds);
        public float CooldownSeconds => Mathf.Max(0f, cooldownSeconds);
        public float PerHitRefundSeconds => Mathf.Max(0f, perHitRefundSeconds);
        public Color TelegraphColor => telegraphColor;

        public float TotalSeconds => windupSeconds + activeSeconds + recoverySeconds;
    }
}
