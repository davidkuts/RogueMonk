using Game.Core.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Level
{
    /// <summary>
    /// The "press this" marker shared by everything the Interact action touches — the reward
    /// pickup and the door choice.
    ///
    /// <para>uGUI on a world-space canvas rather than a TextMesh: the legacy 3D text font material
    /// is exactly the kind of built-in shader URP strips from builds.</para>
    ///
    /// <para>The label is read from the BINDING, not written here. The Interact path has been
    /// rebinding-safe since M16 — every caller references the action, never a physical button —
    /// which left the hardcoded "R1 / F" as the single component in the chain able to tell the
    /// player something untrue. It now names whatever key or button is actually bound, on whatever
    /// device the player last touched.</para>
    /// </summary>
    public static class InteractPrompt
    {
        public static GameObject Build(Transform parent, Vector3 localPosition)
        {
            var prompt = new GameObject("InteractPrompt");
            prompt.transform.SetParent(parent, false);
            prompt.transform.localPosition = localPosition;

            var canvas = prompt.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvas.transform;
            rect.sizeDelta = new Vector2(220f, 60f);
            prompt.transform.localScale = Vector3.one * 0.012f;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(prompt.transform, false);
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 34;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.98f, 0.96f, 0.88f);
            ((RectTransform)textGo.transform).sizeDelta = new Vector2(220f, 60f);

            // Keeps itself honest from here on: re-reads the binding whenever the player swaps
            // between pad and keyboard, so a prompt left on screen across a device change updates
            // rather than lying until it is rebuilt.
            prompt.AddComponent<InteractPromptLabel>().Bind(text);

            prompt.SetActive(false);
            return prompt;
        }
    }

    /// <summary>
    /// Keeps one prompt's text matching the live Interact binding.
    ///
    /// <para>Polls on a slow timer rather than every frame: the string only changes when someone
    /// rebinds or picks up a different controller, and a prompt is on screen for seconds at a
    /// time. It also re-reads on enable, so the common case — the prompt appearing after a device
    /// swap — is always correct on the first frame it is visible.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractPromptLabel : MonoBehaviour
    {
        [SerializeField, Tooltip("How often the binding is re-read while the prompt is on screen.")]
        float refreshIntervalSeconds = 0.25f;

        [SerializeField, Tooltip("Shown when no input reader has been bound yet — a prompt with no text at all reads as a broken pickup.")]
        string fallbackLabel = "INTERACT";

        Text label;
        PlayerInputReader reader;
        float nextRefreshUnscaled;
        string lastApplied;

        public void Bind(Text text) => label = text;

        /// <summary>Hands the prompt the reader that owns the Interact action.</summary>
        public void Bind(PlayerInputReader input)
        {
            reader = input;
            Refresh();
        }

        void OnEnable()
        {
            nextRefreshUnscaled = 0f;
            Refresh();
        }

        void Update()
        {
            // Unscaled: prompts are shown over a paused draft and while hitstop holds the clock.
            if (Time.unscaledTime < nextRefreshUnscaled)
                return;

            nextRefreshUnscaled = Time.unscaledTime + Mathf.Max(0.05f, refreshIntervalSeconds);
            Refresh();
        }

        void Refresh()
        {
            if (label == null)
                return;

            if (reader == null)
                reader = FindFirstObjectByType<PlayerInputReader>();

            string display = reader != null ? reader.InteractDisplayString : null;
            if (string.IsNullOrWhiteSpace(display))
                display = fallbackLabel;

            if (display == lastApplied)
                return;

            lastApplied = display;
            label.text = display;
        }
    }
}
