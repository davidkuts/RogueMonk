using System;
using System.Collections.Generic;
using Game.Combat;
using Game.Core.Audio;
using Game.Core.Economy;
using Game.Core.Timing;
using Game.Level;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The capsule-phase transmission draft: gameplay pauses, three plain cards from one
    /// giver, pick one. Implements <see cref="ITransmissionDraftPresenter"/> and registers
    /// itself with the reward director — the director rolls WHAT is offered, this only shows
    /// it, which is the seam the real watch-face presentation will slot into.
    ///
    /// Builds its own uGUI at runtime, like the wallet HUD: scaffolding should not leave scene
    /// litter for the real UI pass to dig out.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TransmissionDraftView : MonoBehaviour, ITransmissionDraftPresenter
    {
        [SerializeField] RewardDirector rewards;

        [Header("Look")]
        [SerializeField] Color selectedColor = new Color(1f, 0.95f, 0.8f);
        [SerializeField] Color unselectedColor = new Color(0.45f, 0.48f, 0.55f);

        [Header("Feel")]
        [SerializeField, Tooltip("Confirm is ignored this long after the panel opens, so the collecting press cannot double as a blind pick.")]
        float inputLockoutSeconds = 0.5f;

        GameObject root;
        Text title;
        readonly List<CardWidgets> cards = new List<CardWidgets>();
        readonly List<TransmissionBoonDefinition> offer = new List<TransmissionBoonDefinition>();

        MenuSelection selection;
        Action<TransmissionBoonDefinition> onChosen;
        float lockout;

        sealed class CardWidgets
        {
            public GameObject Root;
            public Image Frame;
            public Text Name;
            public Text Body;
        }

        public bool IsShowing { get; private set; }

        void Start()
        {
            if (rewards == null)
                rewards = FindAnyObjectByType<RewardDirector>();

            if (rewards != null)
                rewards.DraftPresenter = this;
        }

        void OnDestroy()
        {
            if (rewards != null && ReferenceEquals(rewards.DraftPresenter, this))
                rewards.DraftPresenter = null;
        }

        public bool Present(IReadOnlyList<TransmissionBoonDefinition> draft, RewardTier tier,
            Action<TransmissionBoonDefinition> chosen)
        {
            if (IsShowing || draft == null || draft.Count == 0)
                return false;

            if (root == null)
                BuildPanel();

            offer.Clear();
            for (int i = 0; i < draft.Count; i++)
                offer.Add(draft[i]);

            onChosen = chosen;
            selection = new MenuSelection(offer.Count, axis: MenuAxis.Horizontal);
            selection.Reset();
            lockout = inputLockoutSeconds;

            if (title != null)
                title.text = $"INCOMING TRANSMISSION — {offer[0].Giver.ToString().ToUpperInvariant()}  [{tier.ToString().ToUpperInvariant()}]";

            BindCards(tier);
            root.SetActive(true);
            IsShowing = true;

            AudioDirector.PlaySound(GameSound.RoomClear);
            if (GameClock.Instance != null)
                GameClock.Instance.SetPaused(true);

            return true;
        }

        void Update()
        {
            if (!IsShowing || selection == null)
                return;

            if (selection.Tick(Time.unscaledDeltaTime))
            {
                Highlight();
                AudioDirector.PlaySound(GameSound.Whiff);
            }

            if (lockout > 0f)
            {
                lockout -= Time.unscaledDeltaTime;
                return;
            }

            if (!MenuSelection.ConfirmPressed())
                return;

            TransmissionBoonDefinition chosen = offer[Mathf.Clamp(selection.Index, 0, offer.Count - 1)];

            root.SetActive(false);
            IsShowing = false;
            if (GameClock.Instance != null)
                GameClock.Instance.SetPaused(false);

            AudioDirector.PlaySound(GameSound.PerfectDodge);

            Action<TransmissionBoonDefinition> callback = onChosen;
            onChosen = null;
            callback?.Invoke(chosen);
        }

        void BuildPanel()
        {
            root = new GameObject("TransmissionDraftCanvas");
            root.transform.SetParent(transform, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var dim = new GameObject("Dim").AddComponent<Image>();
            dim.transform.SetParent(root.transform, false);
            dim.color = new Color(0.02f, 0.03f, 0.06f, 0.88f);
            Stretch((RectTransform)dim.transform);

            title = MakeText(root.transform, 40, new Color(0.3f, 0.9f, 1f), TextAnchor.MiddleCenter);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = titleRect.anchorMax = titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -120f);
            titleRect.sizeDelta = new Vector2(1400f, 60f);

            for (int i = 0; i < 3; i++)
            {
                var card = new CardWidgets();
                card.Root = new GameObject($"Card_{i}");
                card.Root.transform.SetParent(root.transform, false);

                card.Frame = card.Root.AddComponent<Image>();
                card.Frame.color = unselectedColor;
                var rect = (RectTransform)card.Root.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2((i - 1) * 440f, -40f);
                rect.sizeDelta = new Vector2(400f, 420f);

                var inner = new GameObject("Inner").AddComponent<Image>();
                inner.transform.SetParent(card.Root.transform, false);
                inner.color = new Color(0.07f, 0.09f, 0.13f, 1f);
                Stretch((RectTransform)inner.transform, 6f);

                card.Name = MakeText(card.Root.transform, 30, Color.white, TextAnchor.UpperCenter);
                var nameRect = (RectTransform)card.Name.transform;
                Stretch(nameRect, 20f);
                nameRect.offsetMax = new Vector2(nameRect.offsetMax.x, -30f);

                card.Body = MakeText(card.Root.transform, 22, new Color(0.8f, 0.83f, 0.9f), TextAnchor.MiddleCenter);
                var bodyRect = (RectTransform)card.Body.transform;
                Stretch(bodyRect, 24f);

                cards.Add(card);
            }

            var hint = MakeText(root.transform, 22, new Color(0.6f, 0.65f, 0.72f), TextAnchor.MiddleCenter);
            hint.text = "◄ ► CHOOSE      ✕ / ENTER INSTALL";
            var hintRect = (RectTransform)hint.transform;
            hintRect.anchorMin = hintRect.anchorMax = hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 90f);
            hintRect.sizeDelta = new Vector2(900f, 40f);

            root.SetActive(false);
        }

        static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        static Text MakeText(Transform parent, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = anchor;
            text.color = color;
            return text;
        }

        void BindCards(RewardTier tier)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                CardWidgets card = cards[i];
                bool used = i < offer.Count;
                card.Root.SetActive(used);
                if (!used)
                    continue;

                // Centre the row on however many cards this draft actually has — a two-card
                // offer parked left-of-centre read as a broken three-card one.
                var rect = (RectTransform)card.Root.transform;
                rect.anchoredPosition = new Vector2((i - (offer.Count - 1) * 0.5f) * 440f, -40f);

                TransmissionBoonDefinition boon = offer[i];
                card.Name.text = boon.DisplayName.ToUpperInvariant();
                card.Body.text = $"{boon.Description}\n\n[{boon.Ability}]";
            }

            Highlight();
        }

        void Highlight()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (i < offer.Count && cards[i].Frame != null)
                    cards[i].Frame.color = i == selection.Index ? selectedColor : unselectedColor;
            }
        }
    }
}
