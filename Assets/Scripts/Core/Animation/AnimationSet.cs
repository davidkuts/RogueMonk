using UnityEngine;

namespace Game.Core.Animation
{
    /// <summary>
    /// The clips one character type uses. Clips are referenced as data rather than wired into
    /// an Animator Controller graph — CLAUDE.md forbids controller state machines, because the
    /// combat simulation already owns the state machine and two of them would drift apart.
    ///
    /// Every field may be left empty. Nothing here is required for the game to run, which is
    /// what lets the gray-box capsules keep working until Mixamo clips are imported.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Animation Set", fileName = "AnimationSet")]
    public sealed class AnimationSet : ScriptableObject
    {
        [Header("Locomotion")]
        [SerializeField] AnimationClip idle;
        [SerializeField] AnimationClip run;
        [SerializeField] AnimationClip dash;

        [Header("Combat")]
        [SerializeField, Tooltip("One per combo step, in order. Trim each clip to match the attack's frame data.")]
        AnimationClip[] attacks = new AnimationClip[0];
        [SerializeField] AnimationClip hitReaction;
        [SerializeField] AnimationClip death;

        [Header("Blending")]
        [SerializeField, Tooltip("Cross-fade used between locomotion states.")]
        float locomotionFadeSeconds = 0.12f;
        [SerializeField, Tooltip("Cross-fade into an attack. Short, so the wind-up stays readable as a tell.")]
        float actionFadeSeconds = 0.04f;

        public AnimationClip Idle => idle;
        public AnimationClip Run => run;
        public AnimationClip Dash => dash;
        public AnimationClip HitReaction => hitReaction;
        public AnimationClip Death => death;

        public float LocomotionFadeSeconds => locomotionFadeSeconds;
        public float ActionFadeSeconds => actionFadeSeconds;

        public int AttackCount => attacks != null ? attacks.Length : 0;

        public AnimationClip GetAttack(int index) =>
            attacks != null && index >= 0 && index < attacks.Length ? attacks[index] : null;

        /// <summary>True when at least one clip is assigned — used to skip the graph entirely.</summary>
        public bool HasAnyClip
        {
            get
            {
                if (idle != null || run != null || dash != null || hitReaction != null || death != null)
                    return true;

                for (int i = 0; i < AttackCount; i++)
                {
                    if (attacks[i] != null)
                        return true;
                }

                return false;
            }
        }
    }
}
