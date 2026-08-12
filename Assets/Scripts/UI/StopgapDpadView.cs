using System.Collections.Generic;
using Game.Combat;
using Game.Core.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The D-pad, drawn on screen, showing which Stopgaps Cole is carrying.
    ///
    /// <para>Replaces the old "■□" pip line, which said how MANY Stopgaps were held and never
    /// which ones — so a player who picked one up two rooms ago had no way to answer "do I still
    /// have the rewind?" short of pressing the button and finding out. Here the widget is a
    /// picture of the thing you would press: every direction is always drawn, and a direction you
    /// are holding something on is filled in.</para>
    ///
    /// <para>Lit, not flashing. A HUD element that blinks reads as a warning; this is a
    /// fact about your inventory, and it should sit still.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StopgapDpadView : MonoBehaviour
    {
        [SerializeField] StopgapInventory inventory;

        [Header("Placement")]
        [SerializeField, Tooltip("Distance from the LEFT screen edge to the centre of the d-pad, at 1920x1080.")]
        float leftMargin = 110f;
        [SerializeField, Tooltip("Vertical offset from screen centre. Negative sits it below the middle.")]
        float verticalOffset = -180f;

        [Header("Buttons")]
        [SerializeField, Tooltip("Edge length of one direction button.")]
        float buttonSize = 34f;
        [SerializeField, Tooltip("Centre-to-centre distance from the middle of the cross to a button.")]
        float buttonSpread = 38f;

        [Header("Colours")]
        [SerializeField, Tooltip("A direction holding a Stopgap. Filled in, so 'I have this' reads without reading the label.")]
        Color heldColor = Color.white;
        [SerializeField, Tooltip("An empty direction. Present but plainly unfilled — the shape has to stay legible as a d-pad.")]
        Color emptyColor = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField, Tooltip("Label colour beside a held direction.")]
        Color labelColor = new Color(0.96f, 0.96f, 0.92f);

        [Header("Labels")]
        [SerializeField, Tooltip("Gap between a button's edge and its label, so text and icon never touch.")]
        float labelPadding = 10f;
        [SerializeField] int labelFontSize = 16;

        sealed class SlotView
        {
            public Image Button;
            public Text Label;
        }

        readonly Dictionary<StopgapSlot, SlotView> views = new Dictionary<StopgapSlot, SlotView>();

        void Awake()
        {
            if (inventory == null)
                inventory = FindAnyObjectByType<StopgapInventory>();

            Build();

            if (inventory != null)
                inventory.Changed += Refresh;

            Refresh();
        }

        void OnDestroy()
        {
            if (inventory != null)
                inventory.Changed -= Refresh;
        }

        void Build()
        {
            // Attach to the HUD's EXISTING canvas rather than making one.
            //
            // A Canvas nested inside another Canvas is not driven to the screen rect — it keeps
            // whatever RectTransform it happens to have, which for a fresh GameObject is 100x100
            // centred on the parent. Anchoring "to the left edge" of that puts the widget near the
            // middle of the screen, which is exactly where the first version of this landed.
            Canvas host = GetComponentInParent<Canvas>();
            Transform parent;

            if (host != null)
            {
                parent = host.transform;
            }
            else
            {
                var canvasGo = new GameObject("StopgapDpadCanvas");
                canvasGo.transform.SetParent(transform, false);
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                parent = canvasGo.transform;
            }

            // Anchored to the left edge at mid-height, so the cross keeps its distance from the
            // edge at any aspect ratio rather than drifting with the resolution.
            var root = new GameObject("Dpad").AddComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 0.5f);
            root.anchoredPosition = new Vector2(leftMargin, verticalOffset);
            root.sizeDelta = Vector2.zero;

            foreach (StopgapSlot slot in StopgapInventory.AllSlots)
                views[slot] = BuildSlot(root, slot);
        }

        SlotView BuildSlot(RectTransform root, StopgapSlot slot)
        {
            Vector2 dir = Direction(slot);

            var buttonGo = new GameObject($"Button_{slot}");
            var button = buttonGo.AddComponent<Image>();
            var buttonRect = (RectTransform)buttonGo.transform;
            buttonRect.SetParent(root, false);
            buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.sizeDelta = new Vector2(buttonSize, buttonSize);
            buttonRect.anchoredPosition = dir * buttonSpread;

            var labelGo = new GameObject($"Label_{slot}");
            var label = labelGo.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = labelFontSize;
            label.fontStyle = FontStyle.Bold;
            label.color = labelColor;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            var labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(root, false);
            labelRect.sizeDelta = new Vector2(200f, 22f);

            // The label sits on the button's OWN side and is anchored so it grows away from the
            // cross — a right-slot label runs right, a left-slot label runs left. That is what
            // keeps text off the icon however long a Stopgap's name turns out to be.
            float step = buttonSpread + buttonSize * 0.5f + labelPadding;
            switch (slot)
            {
                case StopgapSlot.Up:
                    label.alignment = TextAnchor.LowerCenter;
                    labelRect.pivot = new Vector2(0.5f, 0f);
                    labelRect.anchoredPosition = new Vector2(0f, step);
                    break;
                case StopgapSlot.Down:
                    label.alignment = TextAnchor.UpperCenter;
                    labelRect.pivot = new Vector2(0.5f, 1f);
                    labelRect.anchoredPosition = new Vector2(0f, -step);
                    break;
                case StopgapSlot.Left:
                    label.alignment = TextAnchor.MiddleRight;
                    labelRect.pivot = new Vector2(1f, 0.5f);
                    labelRect.anchoredPosition = new Vector2(-step, 0f);
                    break;
                default:
                    label.alignment = TextAnchor.MiddleLeft;
                    labelRect.pivot = new Vector2(0f, 0.5f);
                    labelRect.anchoredPosition = new Vector2(step, 0f);
                    break;
            }

            return new SlotView { Button = button, Label = label };
        }

        static Vector2 Direction(StopgapSlot slot)
        {
            switch (slot)
            {
                case StopgapSlot.Up: return Vector2.up;
                case StopgapSlot.Down: return Vector2.down;
                case StopgapSlot.Left: return Vector2.left;
                default: return Vector2.right;
            }
        }

        /// <summary>
        /// Every direction is drawn every frame it exists; only the FILL changes. An empty slot
        /// that vanished would make the widget change shape as the player picked things up, and
        /// the shape is what makes it recognisable as a d-pad.
        /// </summary>
        void Refresh()
        {
            foreach (KeyValuePair<StopgapSlot, SlotView> entry in views)
            {
                StopgapDefinition held = inventory != null ? inventory.Get(entry.Key) : null;
                SlotView view = entry.Value;

                if (view.Button != null)
                    view.Button.color = held != null ? heldColor : emptyColor;

                // An empty direction carries no name: a label for something you are not holding
                // reads as a promise rather than as inventory.
                if (view.Label != null)
                    view.Label.text = held != null ? held.HudLabel : string.Empty;
            }
        }
    }
}
