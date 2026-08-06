using Game.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Answers "did I land the combo?" without needing animation or audio. One chevron per
    /// chain step: dark = not thrown, dim = thrown and missed, bright = connected. A drain
    /// bar underneath shows how long the chain stays alive, and the whole strip flashes when
    /// the final step connects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComboMeterView : MonoBehaviour
    {
        [SerializeField] PlayerAttackController attacks;
        [SerializeField] Image[] stepFills;
        [SerializeField] Image windowDrain;
        [SerializeField] CanvasGroup group;

        [Header("Colours")]
        [SerializeField] Color pendingColor = new Color(0.18f, 0.20f, 0.24f, 0.55f);
        [SerializeField, Tooltip("Thrown but missed — visible, but clearly not a hit.")]
        Color whiffedColor = new Color(0.55f, 0.52f, 0.45f, 0.7f);
        [SerializeField, Tooltip("Connected. Saturated, because this is gameplay information.")]
        Color connectedColor = new Color(1f, 0.72f, 0.25f, 1f);
        [SerializeField] Color flashColor = Color.white;

        [Header("Feel")]
        [SerializeField, Tooltip("How long the whole strip flashes when the final step connects.")]
        float landedFlashSeconds = 0.35f;
        [SerializeField, Tooltip("Fade-out once the chain has lapsed and nothing is happening.")]
        float idleFadeSeconds = 0.5f;

        float flashRemaining;
        float visibleRemaining;

        void Awake()
        {
            if (attacks == null)
                attacks = FindAnyObjectByType<PlayerAttackController>();

            if (attacks == null || stepFills == null || stepFills.Length == 0)
            {
                Debug.LogError($"{nameof(ComboMeterView)} on '{name}' is missing its controller or step images.", this);
                enabled = false;
                return;
            }

            attacks.ComboLanded += OnComboLanded;
        }

        void OnDestroy()
        {
            if (attacks != null)
                attacks.ComboLanded -= OnComboLanded;
        }

        void OnComboLanded(int connectedSteps) => flashRemaining = landedFlashSeconds;

        void LateUpdate()
        {
            float deltaTime = Time.unscaledDeltaTime;
            if (flashRemaining > 0f)
                flashRemaining -= deltaTime;

            ComboTracker combo = attacks.Combo;
            bool chainActive = combo != null && combo.Index > 0;
            if (chainActive || flashRemaining > 0f)
                visibleRemaining = idleFadeSeconds;
            else if (visibleRemaining > 0f)
                visibleRemaining -= deltaTime;

            if (group != null)
                group.alpha = Mathf.Clamp01(visibleRemaining / Mathf.Max(0.0001f, idleFadeSeconds));

            float flash = landedFlashSeconds > 0f ? Mathf.Clamp01(flashRemaining / landedFlashSeconds) : 0f;

            var states = attacks.ComboSteps;
            for (int i = 0; i < stepFills.Length; i++)
            {
                Image fill = stepFills[i];
                if (fill == null)
                    continue;

                Color target = pendingColor;
                if (states != null && i < states.Count)
                {
                    switch (states[i])
                    {
                        case PlayerAttackController.ComboStepState.Connected:
                            target = connectedColor;
                            break;
                        case PlayerAttackController.ComboStepState.Whiffed:
                            target = whiffedColor;
                            break;
                    }
                }

                fill.color = Color.Lerp(target, flashColor, flash);
            }

            if (windowDrain != null)
            {
                float duration = combo != null ? combo.WindowDuration : 0f;
                windowDrain.fillAmount = chainActive && duration > 0f
                    ? Mathf.Clamp01(combo.WindowRemaining / duration)
                    : 0f;
            }
        }
    }
}
