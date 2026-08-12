using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Every playtest knob for the Undertow's visual layers, kept apart from
    /// <see cref="VortexDefinition"/> on purpose: that asset is what the ability <em>does</em>, this
    /// is what it <em>looks like</em>. Mixing them means a designer retuning a glow has to open the
    /// file that owns damage and radius.
    ///
    /// <para>⚠️ Nothing here may change gameplay. Radius, damage and the foot-traced range smear are
    /// final; these layers draw on top of them and read the pull radius rather than setting it.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Vortex VFX Settings", fileName = "VortexVfxSettings")]
    public sealed class VortexVfxSettings : ScriptableObject
    {
        [Header("Shared")]
        [SerializeField, Tooltip("The reserved time-blue. Sampled from the Blink's afterimages so the whole Second Hand kit reads as one colour family — do not invent a new blue here.")]
        Color timeBlue = new Color(0.29f, 0.85f, 0.92f, 1f);

        [Header("Inner disc")]
        [SerializeField, Range(0f, 1f), Tooltip("How far across the ACTUAL pull radius the inner disc reaches. Deliberately well inside the foot-smear's reach so the two never compete for the same read — the smear says 'this is the range', the disc says 'this is the drain'.")]
        float discRadiusFraction = 0.55f;
        [SerializeField, Range(0f, 1f), Tooltip("Base alpha at rest, before any hit pulse. Low: at rest this should be a suggestion, not a light source.")]
        float spiralBaseAlpha = 0.22f;
        [SerializeField, Tooltip("Turns per second the spiral scrolls inward. Negative reverses it — keep the sign matching the body's spin, or the effect fights the animation.")]
        float spiralScrollSpeed = 1.35f;
        [SerializeField, Range(1, 6), Tooltip("How many arms the spiral has.")]
        int spiralArms = 3;
        [SerializeField, Range(0f, 6f), Tooltip("How tightly the arms wind. Higher curls them harder toward the centre.")]
        float spiralTightness = 1.6f;
        [SerializeField, Tooltip("Height above the floor. Just enough to beat z-fighting on the ground plane.")]
        float discGroundOffset = 0.02f;

        [Header("Hit pulse — the priority feature")]
        [SerializeField, Range(0f, 1f), Tooltip("How much brightness ONE enemy damaged by ONE tick adds. Nine of these can land in a single spin (3 enemies x 3 ticks), and they accumulate, so a crowd should visibly outshine a duel.")]
        float hitPulseIntensity = 0.28f;
        [SerializeField, Tooltip("Seconds a full pulse takes to fall back to rest. Short — this is a flare, not a state.")]
        float hitPulseDurationSeconds = 0.35f;
        [SerializeField, Range(0f, 4f), Tooltip("Extra spiral scroll speed at full pulse, as a multiplier on top of the base. 0 leaves the spin rate alone; around 1 makes a fed vortex visibly whip up.")]
        float hitPulseSpinKick = 1.1f;

        [Header("Inward streaks")]
        [SerializeField, Range(0, 40), Tooltip("Hard ceiling on live streaks. The pool is allocated once at this size and never grows, so this is a real budget rather than a hope.")]
        int streakBudget = 32;
        [SerializeField, Tooltip("Streaks spawned per second while the spin runs.")]
        float streakSpawnRate = 46f;
        [SerializeField, Tooltip("Seconds a streak takes to travel from the rim to the centre.")]
        float streakTravelSeconds = 0.42f;
        [SerializeField, Range(0f, 720f), Tooltip("Degrees a streak sweeps around the player over its life. This is what makes the path a spiral rather than a straight line inward.")]
        float streakSwirlDegrees = 190f;
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of the disc radius at which a streak dies. Above 0 so they do not all converge on a single pixel and pile up.")]
        float streakDeathRadiusFraction = 0.12f;
        [SerializeField, Tooltip("Length and width of one streak, in metres.")]
        Vector2 streakSize = new Vector2(0.45f, 0.05f);
        [SerializeField, Range(0f, 1f), Tooltip("Peak alpha of a streak.")]
        float streakAlpha = 0.55f;

        [Header("Character trails (Layer 1)")]
        [SerializeField, Tooltip("Bones that trail during the spin. Left empty, both hands are found by name — the arms, deliberately: the FEET already carry the range smear and doubling up there would just thicken one ring.")]
        string[] trailBoneNameSuffixes = { "LeftHand", "RightHand" };
        [SerializeField, Tooltip("How long a point on an arm trail survives.")]
        float trailSeconds = 0.22f;
        [SerializeField, Tooltip("Width at the hand.")]
        float trailStartWidth = 0.13f;
        [SerializeField, Tooltip("Width at the tail.")]
        float trailEndWidth = 0.01f;
        [SerializeField, Range(0f, 1f), Tooltip("Trail alpha. HARD REQUIREMENT: Cole's silhouette must stay readable. If the trails start hiding the body, this is the number to pull down.")]
        float trailAlpha = 0.38f;

        public Color TimeBlue => timeBlue;

        public float DiscRadiusFraction => Mathf.Clamp01(discRadiusFraction);
        public float SpiralBaseAlpha => Mathf.Clamp01(spiralBaseAlpha);
        public float SpiralScrollSpeed => spiralScrollSpeed;
        public int SpiralArms => Mathf.Clamp(spiralArms, 1, 6);
        public float SpiralTightness => Mathf.Max(0f, spiralTightness);
        public float DiscGroundOffset => discGroundOffset;

        public float HitPulseIntensity => Mathf.Clamp01(hitPulseIntensity);
        public float HitPulseDurationSeconds => Mathf.Max(0f, hitPulseDurationSeconds);
        public float HitPulseSpinKick => Mathf.Max(0f, hitPulseSpinKick);

        public int StreakBudget => Mathf.Clamp(streakBudget, 0, 40);
        public float StreakSpawnRate => Mathf.Max(0f, streakSpawnRate);
        public float StreakTravelSeconds => Mathf.Max(0.01f, streakTravelSeconds);
        public float StreakSwirlDegrees => streakSwirlDegrees;
        public float StreakDeathRadiusFraction => Mathf.Clamp01(streakDeathRadiusFraction);
        public Vector2 StreakSize => streakSize;
        public float StreakAlpha => Mathf.Clamp01(streakAlpha);

        public string[] TrailBoneNameSuffixes => trailBoneNameSuffixes;
        public float TrailSeconds => Mathf.Max(0.01f, trailSeconds);
        public float TrailStartWidth => Mathf.Max(0f, trailStartWidth);
        public float TrailEndWidth => Mathf.Max(0f, trailEndWidth);
        public float TrailAlpha => Mathf.Clamp01(trailAlpha);
    }
}
