using Game.Core.Animation;
using Game.Core.Player;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// Chooses which clip should be playing from the simulation's current state.
    ///
    /// One-directional on purpose: animation reads gameplay and never drives it. Root motion is
    /// off everywhere (CLAUDE.md hard rule 3), so a missing or mis-trimmed clip can change how
    /// the game looks but never how it plays — which is what keeps frame data honest.
    ///
    /// Does nothing until an <see cref="AnimationSet"/> with real clips is assigned, so the
    /// gray-box capsules are unaffected.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class PlayerAnimationDriver : MonoBehaviour
    {
        [SerializeField] AnimationSet animations;
        [SerializeField] ClipPlayer player;
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerAttackController attacks;
        [SerializeField] PlayerHealth health;

        [SerializeField, Range(0.01f, 0.5f), Tooltip("Speed fraction above which the run clip is used.")]
        float runThreshold = 0.1f;

        AnimationClip lastAttackClip;

        void Awake()
        {
            if (player == null) player = GetComponent<ClipPlayer>();
            if (motor == null) motor = GetComponent<PlayerMotor>();
            if (attacks == null) attacks = GetComponent<PlayerAttackController>();
            if (health == null) health = GetComponent<PlayerHealth>();

            enabled = animations != null && animations.HasAnyClip && player != null && player.IsReady;
        }

        void Update()
        {
            if (health != null && !health.IsAlive)
            {
                player.Play(animations.Death, animations.ActionFadeSeconds, loop: false);
                return;
            }

            // Attacks outrank everything: the wind-up is a tell the player reads, so it must
            // never be replaced by a locomotion clip mid-swing.
            if (attacks != null && attacks.Attacks.IsAttacking)
            {
                AnimationClip clip = ResolveAttackClip();
                if (clip != null)
                {
                    bool isNewSwing = clip != lastAttackClip || player.CurrentClip != clip;
                    player.Play(clip, animations.ActionFadeSeconds, loop: false, restart: isNewSwing);
                    lastAttackClip = clip;
                    return;
                }
            }
            else
            {
                lastAttackClip = null;
            }

            if (motor == null)
                return;

            // The dash normally has no clip — it is sold with afterimages instead (DESIGN.md).
            // Only take over the body if one was actually assigned; otherwise fall through so
            // locomotion keeps playing rather than freezing on whatever frame was up.
            if (motor.Dash != null && motor.Dash.IsDashing && animations.Dash != null)
            {
                player.Play(animations.Dash, animations.ActionFadeSeconds, loop: false);
                return;
            }

            bool moving = motor.Locomotion != null && motor.Locomotion.NormalizedSpeed > runThreshold;
            player.Play(moving ? animations.Run : animations.Idle, animations.LocomotionFadeSeconds);
        }

        /// <summary>Maps the combo step to its clip, falling back to the first attack clip.</summary>
        AnimationClip ResolveAttackClip()
        {
            if (animations.AttackCount == 0)
                return null;

            int step = 0;
            if (attacks.Combo != null)
            {
                // Combo.Index is the *next* attack, so the one in flight is the previous step.
                int length = Mathf.Max(1, attacks.ComboSteps.Count);
                step = (attacks.Combo.Index - 1 + length) % length;
            }

            return animations.GetAttack(step) ?? animations.GetAttack(0);
        }
    }
}
