using UnityEngine;
using UnityEngine.UI;

namespace Game.Level
{
    /// <summary>
    /// The placeholder "press this" marker shared by everything the Interact action touches —
    /// the reward pickup and the door choice. uGUI on a world-space canvas rather than a
    /// TextMesh: the legacy 3D text font material is exactly the kind of built-in shader URP
    /// strips from builds. Still a hardcoded label; the glyph-per-scheme pass replaces it.
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
            text.text = "R1 / F";
            ((RectTransform)textGo.transform).sizeDelta = new Vector2(220f, 60f);

            prompt.SetActive(false);
            return prompt;
        }
    }
}
