using Game.Combat;
using Game.Core.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Tells the player they have a counter, and that it is on a button.
    ///
    /// This is the part the previous version was missing entirely. The reward existed, but there
    /// was nothing on screen that said so — the playtest verdict was "I never really noticed I have
    /// it". An earned ability that is not announced may as well not exist, so this arrives loudly:
    /// it pops in, pulses while held, and names the button.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RiposteReadyView : MonoBehaviour
    {
        [SerializeField] PerfectDodgeReward reward;
        [SerializeField, Tooltip("Root shown only while a counter is banked.")]
        GameObject root;
        [SerializeField] Image badge;
        [SerializeField] Text label;

        [Header("Look")]
        [SerializeField] Color readyColor = new Color(1f, 0.85f, 0.35f);
        [SerializeField, Tooltip("Pulse rate while armed. The prompt has to keep asking to be used.")]
        float pulseHz = 2.2f;
        [SerializeField, Range(0f, 1f)] float pulseDepth = 0.35f;
        [SerializeField, Tooltip("How long the badge stays scaled up after being earned.")]
        float popSeconds = 0.35f;
        [SerializeField] float popScale = 1.6f;

        float popRemaining;

        void Awake()
        {
            if (reward == null)
                reward = FindAnyObjectByType<PerfectDodgeReward>();

            if (reward == null)
            {
                Debug.LogError($"{nameof(RiposteReadyView)} on '{name}' found no {nameof(PerfectDodgeReward)}.", this);
                enabled = false;
                return;
            }

            reward.ChargesChanged += OnChargesChanged;
            Show(false);
        }

        void OnDestroy()
        {
            if (reward != null)
                reward.ChargesChanged -= OnChargesChanged;
        }

        void OnChargesChanged(int charges)
        {
            bool armed = charges > 0;
            Show(armed);

            if (armed)
            {
                popRemaining = popSeconds;
                AudioDirector.PlaySound(GameSound.PerfectDodge);
            }
        }

        void LateUpdate()
        {
            if (reward == null || !reward.IsArmed)
                return;

            // Unscaled so the prompt keeps pulsing through the focus window it appears in — that
            // slow-motion moment is exactly when the player is looking for what they just earned.
            float deltaTime = Time.unscaledDeltaTime;
            if (popRemaining > 0f)
                popRemaining -= deltaTime;

            float pop = popSeconds > 0f ? Mathf.Clamp01(popRemaining / popSeconds) : 0f;
            float scale = Mathf.Lerp(1f, popScale, pop);

            if (badge != null)
            {
                badge.rectTransform.localScale = Vector3.one * scale;

                float pulse = 1f - pulseDepth * Mathf.PingPong(Time.unscaledTime * pulseHz, 1f);
                badge.color = new Color(readyColor.r, readyColor.g, readyColor.b, pulse);
            }

            if (label != null)
                label.color = readyColor;
        }

        void Show(bool visible)
        {
            if (root != null)
                root.SetActive(visible);
        }
    }
}
