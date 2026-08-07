using Game.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Player HP bar. Two layers: a fast fill showing current health and a slower "chip" bar
    /// trailing behind it, so a hit reads as an amount lost rather than just a new length.
    /// With no healing in a run (DESIGN.md), the bar only ever goes down — which makes the
    /// size of each loss the information that matters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HealthBarView : MonoBehaviour
    {
        [SerializeField] PlayerHealth health;
        [SerializeField] Image fill;
        [SerializeField, Tooltip("Trails the real value to show how much the last hit cost.")]
        Image chip;
        [SerializeField] Text label;

        [Header("Colours")]
        [SerializeField] Color healthyColor = new Color(0.55f, 0.85f, 0.5f);
        [SerializeField] Color hurtColor = new Color(0.95f, 0.75f, 0.25f);
        [SerializeField] Color criticalColor = new Color(0.95f, 0.3f, 0.28f);
        [SerializeField] Color chipColor = new Color(0.9f, 0.35f, 0.3f, 0.65f);

        [Header("Feel")]
        [SerializeField, Range(0.05f, 0.6f)] float criticalThreshold = 0.3f;
        [SerializeField] float chipDrainPerSecond = 0.45f;
        [SerializeField] float chipDelaySeconds = 0.35f;

        float chipValue = 1f;
        float chipHold;

        void Awake()
        {
            if (health == null)
                health = FindAnyObjectByType<PlayerHealth>();

            if (health == null || fill == null)
            {
                Debug.LogError($"{nameof(HealthBarView)} on '{name}' is missing its health or fill image.", this);
                enabled = false;
                return;
            }

            chipValue = health.HealthFraction;
            if (chip != null)
                chip.color = chipColor;
        }

        void LateUpdate()
        {
            float fraction = health.HealthFraction;

            fill.fillAmount = fraction;
            fill.color = fraction <= criticalThreshold
                ? criticalColor
                : Color.Lerp(hurtColor, healthyColor, Mathf.InverseLerp(criticalThreshold, 1f, fraction));

            if (chip != null)
            {
                // Unscaled so the chip keeps draining through hitstop, which is exactly when
                // the player is looking at the bar.
                float deltaTime = Time.unscaledDeltaTime;

                if (fraction < chipValue - 0.0001f && chipHold <= 0f)
                    chipHold = chipDelaySeconds;

                if (chipHold > 0f)
                    chipHold -= deltaTime;
                else
                    chipValue = Mathf.MoveTowards(chipValue, fraction, chipDrainPerSecond * deltaTime);

                chipValue = Mathf.Max(chipValue, fraction);
                chip.fillAmount = chipValue;
            }

            if (label != null)
                label.text = $"{Mathf.CeilToInt(health.CurrentHealth)} / {Mathf.RoundToInt(health.MaxHealth)}";
        }
    }
}
