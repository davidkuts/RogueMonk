using System;
using Game.Core.Audio;
using Game.Core.Diagnostics;
using Game.Core.Feedback;
using Game.Core.Player;
using Game.Core.Timing;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// What a perfect dodge is worth: a moment of focus, and a counter-attack you have to spend.
    ///
    /// <para>The first version quietly empowered the player's next ordinary punch. It tested badly
    /// for a specific reason — nothing about it was visible. There was no button to press, no
    /// distinct sound, and the same hit spark, so the reward existed only in the damage numbers
    /// where nobody was looking. A buff the player cannot perceive is not a reward.</para>
    ///
    /// <para>The riposte replaces it with something you <em>do</em>. It is a move that does not
    /// exist until you earn it, sits on its own button, and is spent when used. That makes the whole
    /// loop legible: dodge perfectly, see the prompt, press the button, watch a room clear.</para>
    ///
    /// The charge does not expire. Earning it is the hard part, and putting a timer on it would
    /// punish the player for taking a beat to look at what they just unlocked.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PerfectDodgeReward : MonoBehaviour
    {
        [SerializeField] PlayerHealth health;
        [SerializeField] PlayerAttackController attacks;
        [SerializeField] PlayerInputReader input;

        [Header("Focus")]
        [SerializeField, Tooltip("How long the world stays slowed after a perfect dodge.")]
        float focusSeconds = 0.6f;
        [SerializeField, Range(0.05f, 1f), Tooltip("Time scale during focus. Low enough to be unmistakable, high enough that the player can still act.")]
        float focusTimeScale = 0.35f;

        [Header("Riposte")]
        [SerializeField, Tooltip("The counter-attack unlocked by a perfect dodge. A wide arc, so it answers a crowd as well as a duel.")]
        AttackDefinition riposte;
        [SerializeField, Tooltip("Charges that can be banked at once. More than one would let a good player hoard counters for the boss.")]
        int maxCharges = 1;

        [Header("Feedback")]
        [SerializeField] Vector2 focusRumble = new Vector2(0.2f, 0.8f);
        [SerializeField] Vector2 riposteRumble = new Vector2(1f, 1f);
        [SerializeField, Tooltip("Extra focus granted when the riposte connects, so a landed counter feels like it paid off.")]
        float riposteHitFocusSeconds = 0.25f;

        int charges;

        /// <summary>True while a counter is available. The HUD prompt reads this.</summary>
        public bool IsArmed => charges > 0;

        public int Charges => charges;

        public int MaxCharges => Mathf.Max(1, maxCharges);

        /// <summary>Raised whenever the charge count changes, for HUD and audio.</summary>
        public event Action<int> ChargesChanged;

        /// <summary>Raised when the riposte is actually thrown.</summary>
        public event Action RiposteThrown;

        void Awake()
        {
            if (health == null) health = GetComponent<PlayerHealth>();
            if (attacks == null) attacks = GetComponent<PlayerAttackController>();
            if (input == null) input = GetComponent<PlayerInputReader>();

            if (attacks == null || riposte == null)
            {
                Debug.LogError(
                    $"{nameof(PerfectDodgeReward)} on '{name}' needs a {nameof(PlayerAttackController)} and a riposte attack.", this);
                enabled = false;
                return;
            }

            health.PerfectDodged += OnPerfectDodge;
            attacks.Hit += OnHit;
        }

        void OnDestroy()
        {
            if (health != null) health.PerfectDodged -= OnPerfectDodge;
            if (attacks != null) attacks.Hit -= OnHit;
        }

        void Update()
        {
            if (!IsArmed || input == null || !input.RipostePressedThisFrame)
                return;

            if (!attacks.TryStartSpecial(riposte))
                return;

            SetCharges(charges - 1);
            RumbleDirector.Rumble(riposteRumble.x, riposteRumble.y);
            AudioDirector.PlaySound(GameSound.Riposte);
            RiposteThrown?.Invoke();

            GameLog.Info(LogCategory.Combat, $"RIPOSTE thrown  {charges} charge(s) left");
        }

        void OnPerfectDodge()
        {
            if (GameClock.Instance != null)
                GameClock.Instance.RequestSlowMotion(focusSeconds, focusTimeScale);

            RumbleDirector.Rumble(focusRumble.x, focusRumble.y);

            SetCharges(charges + 1);

            GameLog.Info(LogCategory.Combat,
                $"FOCUS  {focusSeconds:0.00}s at {focusTimeScale:0.00}x  -  RIPOSTE ready ({charges}/{MaxCharges})");
        }

        /// <summary>
        /// A landed riposte extends the focus a little. The dodge earned the counter; the counter
        /// connecting is what makes the whole exchange read as one deliberate beat rather than two
        /// unrelated events.
        /// </summary>
        void OnHit(HitContext context)
        {
            if (riposte == null || context.Attack == null || context.Attack.Id != riposte.Id)
                return;

            if (riposteHitFocusSeconds > 0f && GameClock.Instance != null)
                GameClock.Instance.RequestSlowMotion(riposteHitFocusSeconds, focusTimeScale);
        }

        void SetCharges(int value)
        {
            int clamped = Mathf.Clamp(value, 0, MaxCharges);
            if (clamped == charges)
                return;

            charges = clamped;
            ChargesChanged?.Invoke(charges);
        }
    }
}
