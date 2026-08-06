using Game.Core.Locomotion;
using Game.Core.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Reads dash charge state and drives one filled Image per pip. Pure view: the fill
    /// levels — including which pip is the one currently recharging — come from
    /// <see cref="DashCharges.GetChargeFill"/>, so the display can't disagree with the
    /// simulation. Saturated pip colours are gameplay information, per DESIGN.md.
    ///
    /// Presentation values are serialized fields rather than a ScriptableObject: they are
    /// view dressing, not gameplay tuning, and this whole element is slated for a redesign.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DashPipsView : MonoBehaviour
    {
        [SerializeField] PlayerMotor motor;
        [SerializeField] Image[] pipFills;

        [Header("Colours")]
        [SerializeField, Tooltip("A charge that is ready to spend.")]
        Color readyColor = new Color(0.29f, 0.85f, 0.92f, 1f);
        [SerializeField, Tooltip("The charge currently refilling. Dimmer so a ready pip reads first.")]
        Color rechargingColor = new Color(0.22f, 0.62f, 0.67f, 0.55f);

        [Header("Pulse")]
        [SerializeField, Tooltip("Pulse rate in cycles per second. All pips pulse in sync.")]
        float pulseSpeedHz = 0.9f;
        [SerializeField, Range(0f, 1f), Tooltip("How deeply a ready pip dips in brightness. 0 = steady.")]
        float readyPulseDepth = 0.35f;
        [SerializeField, Range(0f, 1f), Tooltip("How deeply a refilling pip pulses. Kept subtle so it reads as 'not ready yet'.")]
        float rechargingPulseDepth = 0.15f;

        void Awake()
        {
            if (motor == null)
                motor = FindAnyObjectByType<PlayerMotor>();

            if (motor == null || pipFills == null || pipFills.Length == 0)
            {
                Debug.LogError($"{nameof(DashPipsView)} on '{name}' is missing a {nameof(PlayerMotor)} or pip images.", this);
                enabled = false;
            }
        }

        void LateUpdate()
        {
            DashCharges charges = motor.Dash?.Charges;
            if (charges == null)
                return;

            // Unscaled so the HUD keeps breathing through hitstop and pause.
            float phase = (Mathf.Sin(Time.unscaledTime * pulseSpeedHz * 2f * Mathf.PI) + 1f) * 0.5f;

            for (int i = 0; i < pipFills.Length; i++)
            {
                Image fill = pipFills[i];
                if (fill == null)
                    continue;

                float amount = charges.GetChargeFill(i);
                fill.fillAmount = amount;

                bool ready = amount >= 1f;
                Color baseColor = ready ? readyColor : rechargingColor;
                float depth = ready ? readyPulseDepth : rechargingPulseDepth;
                float brightness = Mathf.Lerp(1f - depth, 1f, phase);

                fill.color = new Color(
                    baseColor.r * brightness,
                    baseColor.g * brightness,
                    baseColor.b * brightness,
                    baseColor.a);
            }
        }
    }
}
