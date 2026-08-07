using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Core.Feedback
{
    /// <summary>
    /// Gamepad rumble, driven by the same events as hitstop (DESIGN.md § Input).
    ///
    /// Overlapping requests take the strongest rather than summing, for the same reason
    /// hitstop does: a flurry of hits should not build into a permanent buzz. Runs on the
    /// unscaled clock because it has to keep decaying while hitstop holds the game at zero —
    /// otherwise a hit would freeze the motors on at full power for the whole freeze.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RumbleDirector : MonoBehaviour
    {
        public static RumbleDirector Instance { get; private set; }

        [SerializeField, Range(0f, 1f), Tooltip("Global scale. 0 disables rumble entirely.")]
        float intensityScale = 1f;
        [SerializeField, Tooltip("How fast a pulse decays, in intensity per second.")]
        float decayPerSecond = 4f;

        float lowFrequency;
        float highFrequency;
        bool motorsRunning;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            StopMotors();
            if (Instance == this)
                Instance = null;
        }

        void OnApplicationFocus(bool hasFocus)
        {
            // Losing focus with the motors running would leave the pad buzzing on the desktop.
            if (!hasFocus)
                StopMotors();
        }

        /// <summary>Requests a pulse. Stronger requests win; weaker ones are ignored.</summary>
        public void Pulse(float low, float high)
        {
            lowFrequency = Mathf.Max(lowFrequency, Mathf.Clamp01(low));
            highFrequency = Mathf.Max(highFrequency, Mathf.Clamp01(high));
        }

        public static void Rumble(float low, float high)
        {
            if (Instance != null)
                Instance.Pulse(low, high);
        }

        void Update()
        {
            Gamepad pad = Gamepad.current;
            if (pad == null)
                return;

            float deltaTime = Time.unscaledDeltaTime;
            lowFrequency = Mathf.Max(0f, lowFrequency - decayPerSecond * deltaTime);
            highFrequency = Mathf.Max(0f, highFrequency - decayPerSecond * deltaTime);

            if (lowFrequency <= 0.001f && highFrequency <= 0.001f)
            {
                if (motorsRunning)
                    StopMotors();

                return;
            }

            pad.SetMotorSpeeds(lowFrequency * intensityScale, highFrequency * intensityScale);
            motorsRunning = true;
        }

        void StopMotors()
        {
            lowFrequency = 0f;
            highFrequency = 0f;
            motorsRunning = false;

            Gamepad pad = Gamepad.current;
            if (pad != null)
                pad.SetMotorSpeeds(0f, 0f);
        }
    }
}
