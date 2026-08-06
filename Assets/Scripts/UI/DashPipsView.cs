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
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DashPipsView : MonoBehaviour
    {
        [SerializeField] PlayerMotor motor;
        [SerializeField] Image[] pipFills;

        [Header("Colours")]
        [SerializeField] Color readyColor = new Color(0.29f, 0.85f, 0.92f, 1f);
        [SerializeField] Color rechargingColor = new Color(0.29f, 0.85f, 0.92f, 0.45f);

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

            for (int i = 0; i < pipFills.Length; i++)
            {
                Image fill = pipFills[i];
                if (fill == null)
                    continue;

                float amount = charges.GetChargeFill(i);
                fill.fillAmount = amount;
                fill.color = amount >= 1f ? readyColor : rechargingColor;
            }
        }
    }
}
