using System;
using Game.Core.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Level
{
    /// <summary>
    /// The physical reward waiting in a cleared room: a floating primitive matching the door
    /// icon's silhouette and tier tint, collected with a deliberate button press. No
    /// auto-pickup — the press is the design: rewards with consequences (a draft, a Stray
    /// swap) must never fire because someone walked through them mid-dodge.
    /// </summary>
    public sealed class RewardPickup : MonoBehaviour
    {
        RewardChoice choice;
        PlayerInputReader input;
        Transform player;
        float pickupRadius;

        Transform iconRoot;
        GameObject prompt;
        Camera view;
        float bobPhase;
        bool collected;

        /// <summary>Raised once, on the collecting press.</summary>
        public event Action<RewardChoice> Collected;

        public RewardChoice Choice => choice;

        /// <summary>True while the player stands close enough to collect.</summary>
        public bool PlayerInRange { get; private set; }

        public static RewardPickup Spawn(
            Vector3 position, RewardChoice choice, RewardDefinition definition, Color tierTint,
            float pickupRadius, Material material, PlayerInputReader input, Transform player)
        {
            var go = new GameObject($"RewardPickup_{choice}");
            go.transform.position = position;

            var pickup = go.AddComponent<RewardPickup>();
            pickup.choice = choice;
            pickup.input = input;
            pickup.player = player;
            pickup.pickupRadius = Mathf.Max(0.5f, pickupRadius);

            // If final art ever supplies a prefab it replaces the primitive icon wholesale;
            // the interaction logic above it stays identical.
            if (definition != null && definition.SpawnPrefab != null)
            {
                pickup.iconRoot = UnityEngine.Object.Instantiate(definition.SpawnPrefab, go.transform).transform;
                pickup.iconRoot.localPosition = new Vector3(0f, 1.2f, 0f);
            }
            else
            {
                pickup.iconRoot = new GameObject("Icon").transform;
                pickup.iconRoot.SetParent(go.transform, false);
                pickup.iconRoot.localPosition = new Vector3(0f, 1.2f, 0f);
                RewardIconBuilder.Build(
                    pickup.iconRoot,
                    definition != null ? definition.IconShape : RewardIconShape.Coin,
                    tierTint, material);
            }

            pickup.BuildPrompt();
            return pickup;
        }

        /// <summary>
        /// A placeholder button prompt above the icon. uGUI on a world-space canvas rather than
        /// a TextMesh: the legacy 3D text font material is exactly the kind of built-in shader
        /// URP strips from builds, while canvas UI ships its own path.
        /// </summary>
        void BuildPrompt()
        {
            prompt = new GameObject("Prompt");
            prompt.transform.SetParent(transform, false);
            prompt.transform.localPosition = new Vector3(0f, 2.3f, 0f);

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
        }

        void Update()
        {
            if (collected)
                return;

            // Bob and turn: unmistakably an object waiting to be taken, not scenery.
            bobPhase += Time.deltaTime;
            if (iconRoot != null)
                iconRoot.localPosition = new Vector3(0f, 1.2f + Mathf.Sin(bobPhase * 2.2f) * 0.18f, 0f);

            PlayerInRange = player != null &&
                PlanarDistance(player.position, transform.position) <= pickupRadius;

            if (prompt != null && prompt.activeSelf != PlayerInRange)
                prompt.SetActive(PlayerInRange);

            if (PlayerInRange && input != null && input.InteractPressedThisFrame)
            {
                collected = true;
                Collected?.Invoke(choice);
            }
        }

        void LateUpdate()
        {
            if (view == null)
                view = Camera.main;

            if (view == null)
                return;

            // Billboard the icon and the prompt to the fixed-yaw camera, spinning the icon
            // slowly around its own view axis' up so it still reads as alive.
            if (iconRoot != null)
                iconRoot.rotation = view.transform.rotation * Quaternion.Euler(0f, Mathf.Sin(bobPhase * 1.1f) * 18f, 0f);

            if (prompt != null && prompt.activeSelf)
                prompt.transform.rotation = view.transform.rotation;
        }

        static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
