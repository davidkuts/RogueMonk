using Game.Core.Diagnostics;
using Game.Core.Audio;
using Game.Core.Feedback;
using Game.Core.Timing;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// What a perfect dodge is worth.
    ///
    /// The refunded dash charge on its own was a correct reward that did not <em>feel</em> like one:
    /// it pays out on the HUD, a second later, in a resource the player usually had anyway. This
    /// adds the two halves it was missing — an immediate sensory payoff, and a way to convert the
    /// dodge into damage.
    ///
    /// <para><b>Focus.</b> Time drops for a moment. It reads instantly and it is also tactical: the
    /// slow is what gives the player room to walk into the punish they just earned.</para>
    ///
    /// <para><b>Empowered strike.</b> The next hit inside a short window lands much harder, through
    /// the <see cref="HitResolver"/> pipeline rather than a special case in the attack code.</para>
    ///
    /// The charge is deliberately consumed by the <em>first</em> hit and expires if unused, so the
    /// reward is "dodge, then punish" rather than "dodge, then bank it".
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PerfectDodgeReward : MonoBehaviour
    {
        [SerializeField] PlayerHealth health;
        [SerializeField] PlayerAttackController attacks;

        [Header("Focus")]
        [SerializeField, Tooltip("How long the world stays slowed after a perfect dodge.")]
        float focusSeconds = 0.6f;
        [SerializeField, Range(0.05f, 1f), Tooltip("Time scale during focus. Low enough to be unmistakable, high enough that the player can still act.")]
        float focusTimeScale = 0.35f;

        [Header("Empowered strike")]
        [SerializeField, Tooltip("How long the charged hit stays available. Short, so the reward is a punish rather than something to bank.")]
        float empowerWindowSeconds = 2.5f;
        [SerializeField, Tooltip("Damage multiplier on the charged hit.")]
        float damageMultiplier = 2.5f;
        [SerializeField, Tooltip("Extra hitstop on the charged hit, so it lands heavier than anything else the player can throw.")]
        float hitstopBonus = 0.09f;
        [SerializeField, Tooltip("Knockback multiplier on the charged hit.")]
        float knockbackMultiplier = 2f;

        [Header("Feedback")]
        [SerializeField] Vector2 focusRumble = new Vector2(0.2f, 0.8f);
        [SerializeField] Vector2 empoweredRumble = new Vector2(0.9f, 0.9f);

        EmpoweredStrikeModifier empowered;

        /// <summary>True while the charged hit is available — the HUD and VFX read this.</summary>
        public bool IsEmpowered => empowered != null && empowered.IsArmed;

        /// <summary>0..1 of the empower window remaining, for a meter.</summary>
        public float EmpowerFraction =>
            empowered != null && empowerWindowSeconds > 0f
                ? Mathf.Clamp01(empowered.Remaining / empowerWindowSeconds)
                : 0f;

        void Awake()
        {
            if (health == null) health = GetComponent<PlayerHealth>();
            if (attacks == null) attacks = GetComponent<PlayerAttackController>();

            if (attacks == null)
            {
                Debug.LogError($"{nameof(PerfectDodgeReward)} on '{name}' needs a {nameof(PlayerAttackController)}.", this);
                enabled = false;
                return;
            }

            empowered = new EmpoweredStrikeModifier(damageMultiplier, hitstopBonus, knockbackMultiplier);
            empowered.Resolved += OnEmpowerResolved;

            // Registered once and left in place. It is inert until armed, so there is no cost to
            // it sitting in the pipeline, and nothing has to add or remove modifiers mid-fight.
            attacks.Resolver.AddModifier(empowered);

            health.PerfectDodged += OnPerfectDodge;
        }

        void OnDestroy()
        {
            if (health != null)
                health.PerfectDodged -= OnPerfectDodge;
            if (empowered != null)
                empowered.Resolved -= OnEmpowerResolved;
        }

        void Update()
        {
            // Unscaled on purpose: the focus window itself slows the game, and a charge measured on
            // the scaled clock would stretch to nearly three times its authored length.
            empowered?.Tick(Time.unscaledDeltaTime);
        }

        void OnPerfectDodge()
        {
            if (GameClock.Instance != null)
                GameClock.Instance.RequestSlowMotion(focusSeconds, focusTimeScale);

            empowered.Arm(empowerWindowSeconds);

            RumbleDirector.Rumble(focusRumble.x, focusRumble.y);
            AudioDirector.PlaySound(GameSound.PerfectDodge);

            GameLog.Info(LogCategory.Combat,
                $"FOCUS  {focusSeconds:0.00}s at {focusTimeScale:0.00}x  -  next hit within " +
                $"{empowerWindowSeconds:0.0}s deals x{damageMultiplier:0.0}");
        }

        void OnEmpowerResolved(bool spent)
        {
            if (spent)
            {
                RumbleDirector.Rumble(empoweredRumble.x, empoweredRumble.y);
                AudioDirector.PlaySound(GameSound.HitHeavy);
                GameLog.Info(LogCategory.Combat, $"EMPOWERED STRIKE landed  x{damageMultiplier:0.0} damage");
            }
            else
            {
                GameLog.Debug(LogCategory.Combat, "empowered strike expired unused");
            }
        }
    }
}
