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
    /// The capsule-phase transmission draft: gameplay pauses, up to three plain cards, pick
    /// one. Implements <see cref="ITransmissionDraftPresenter"/> and registers itself with the
    /// reward director — the director rolls WHAT is offered (each card carrying its own rolled
    /// rarity), this only shows it, which is the seam the real watch-face presentation will
    /// slot into.
    ///
    /// <para>Rarity reads as the card's border (human call 2026-08-11): Normal keeps the plain
    /// border, Rare is blue, Epic is purple. Selection therefore CANNOT be a border colour any
    /// more — the focused card brightens and grows instead.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TransmissionDraftView : MonoBehaviour, ITransmissionDraftPresenter
    {
        [SerializeField] RewardDirector rewards;

        [Header("Rarity borders")]
        [SerializeField] Color normalBorder = new Color(0.45f, 0.48f, 0.55f);
        [SerializeField] Color rareBorder = new Color(0.25f, 0.5f, 1f);
        [SerializeField] Color epicBorder = new Color(0.65f, 0.3f, 0.95f);

        [Header("Giver identity (indexed by GiverId; BOONS.md §8 materials, pushed loud)")]
        [SerializeField, Tooltip("Overclock/Mara: hot brass. Fray/Reeve: oxidizing patina. Stasis/Percy: frost. Echo/Denny: double-tick gold. Ward/Frank: heavy steel casing. Flux: the impossible signal. Saturated on purpose — the first pass was too subtle to tell apart.")]
        Color[] giverColors =
        {
            new Color(1.00f, 0.45f, 0.15f),  // Overclock — hot ember
            new Color(0.30f, 0.85f, 0.40f),  // Fray — verdigris green
            new Color(0.30f, 0.90f, 1.00f),  // Stasis — ice
            new Color(1.00f, 0.88f, 0.25f),  // Echo — tick gold
            new Color(0.65f, 0.72f, 0.82f),  // Ward — steel casing
            new Color(0.92f, 0.40f, 1.00f),  // Flux — off-spectrum
        };
        [SerializeField, Range(0f, 0.5f), Tooltip("How far each card's dark background leans toward its giver's colour.")]
        float giverBackgroundTint = 0.22f;

        [Header("Rarity scalars (for the stat line — must match RarityScalars.asset)")]
        [SerializeField, Tooltip("The card quotes the marked stat at the ACTUAL rolled value; this asset supplies the scalar. Null falls back to 1 / 1.5 / 2.")]
        RarityScalarSettings rarityScalars;

        [Header("Selection")]
        [SerializeField, Range(1f, 1.3f), Tooltip("Scale of the focused card.")]
        float selectedScale = 1.06f;
        [SerializeField, Range(0f, 1f), Tooltip("How far the focused card's border is pushed toward white.")]
        float selectedBrighten = 0.45f;

        [Header("Feel")]
        [SerializeField, Tooltip("Confirm is ignored this long after the panel opens, so the collecting press cannot double as a blind pick.")]
        float inputLockoutSeconds = 0.5f;

        GameObject root;
        Text title;
        readonly List<CardWidgets> cards = new List<CardWidgets>();
        readonly List<TransmissionOffer> offer = new List<TransmissionOffer>();

        MenuSelection selection;
        Action<TransmissionOffer> onChosen;
        float lockout;

        sealed class CardWidgets
        {
            public GameObject Root;
            public Image Frame;
            public Image Inner;
            public Image Header;
            public Text Name;
            public Text Stat;
            public Text Body;
        }

        float Scalar(RewardTier tier)
        {
            if (rarityScalars != null)
                return rarityScalars.Scalar(tier);

            switch (tier)
            {
                case RewardTier.Epic: return 2f;
                case RewardTier.Rare: return 1.5f;
                default: return 1f;
            }
        }

        Color GiverColor(GiverId giver)
        {
            int index = (int)giver;
            return giverColors != null && index >= 0 && index < giverColors.Length
                ? giverColors[index]
                : Color.white;
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

        public bool Present(IReadOnlyList<TransmissionOffer> draft, Action<TransmissionOffer> chosen)
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
            {
                // An elite draft is the one place two givers share the channel; name them both.
                bool twoGivers = offer.Count > 1 && offer[0].Definition.Giver != offer[offer.Count - 1].Definition.Giver;
                title.text = twoGivers
                    ? "TWO SIGNALS ON THE CHANNEL — CHOOSE"
                    : $"INCOMING TRANSMISSION — {offer[0].Definition.Giver.ToString().ToUpperInvariant()}";
                title.color = twoGivers ? new Color(0.3f, 0.9f, 1f) : GiverColor(offer[0].Definition.Giver);
            }

            BindCards();
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

            TransmissionOffer chosen = offer[Mathf.Clamp(selection.Index, 0, offer.Count - 1)];

            root.SetActive(false);
            IsShowing = false;
            if (GameClock.Instance != null)
                GameClock.Instance.SetPaused(false);

            AudioDirector.PlaySound(GameSound.PerfectDodge);

            Action<TransmissionOffer> callback = onChosen;
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
                card.Frame.color = normalBorder;
                var rect = (RectTransform)card.Root.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2((i - 1) * 440f, -40f);
                rect.sizeDelta = new Vector2(400f, 420f);

                card.Inner = new GameObject("Inner").AddComponent<Image>();
                card.Inner.transform.SetParent(card.Root.transform, false);
                card.Inner.color = new Color(0.07f, 0.09f, 0.13f, 1f);
                Stretch((RectTransform)card.Inner.transform, 6f);

                // A solid giver-coloured band across the top: the loudest possible "who is
                // calling" without touching the rarity border.
                card.Header = new GameObject("Header").AddComponent<Image>();
                card.Header.transform.SetParent(card.Root.transform, false);
                var headerRect = (RectTransform)card.Header.transform;
                headerRect.anchorMin = new Vector2(0f, 1f);
                headerRect.anchorMax = new Vector2(1f, 1f);
                headerRect.pivot = new Vector2(0.5f, 1f);
                headerRect.offsetMin = new Vector2(6f, 0f);
                headerRect.offsetMax = new Vector2(-6f, -6f);
                headerRect.sizeDelta = new Vector2(headerRect.sizeDelta.x, 62f);

                card.Name = MakeText(card.Header.transform, 28, new Color(0.05f, 0.06f, 0.09f), TextAnchor.MiddleCenter);
                Stretch((RectTransform)card.Name.transform, 4f);

                // The marked stat at its rolled value — the one line that changes with quality.
                card.Stat = MakeText(card.Root.transform, 26, Color.white, TextAnchor.MiddleCenter);
                var statRect = (RectTransform)card.Stat.transform;
                statRect.anchorMin = new Vector2(0f, 1f);
                statRect.anchorMax = new Vector2(1f, 1f);
                statRect.pivot = new Vector2(0.5f, 1f);
                statRect.offsetMin = new Vector2(12f, -128f);
                statRect.offsetMax = new Vector2(-12f, -76f);

                card.Body = MakeText(card.Root.transform, 22, new Color(0.8f, 0.83f, 0.9f), TextAnchor.MiddleCenter);
                var bodyRect = (RectTransform)card.Body.transform;
                Stretch(bodyRect, 24f);
                bodyRect.offsetMax = new Vector2(bodyRect.offsetMax.x, -130f);

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

        Color BorderFor(RewardTier rarity)
        {
            switch (rarity)
            {
                case RewardTier.Epic: return epicBorder;
                case RewardTier.Rare: return rareBorder;
                default: return normalBorder;
            }
        }

        void BindCards()
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

                TransmissionOffer o = offer[i];
                Color giver = GiverColor(o.Definition.Giver);

                // The giver reads at a glance: a solid colour band with their name's boon on
                // it, and the card body leaning the same way.
                if (card.Header != null)
                    card.Header.color = giver;
                card.Name.text = o.Definition.DisplayName.ToUpperInvariant();
                if (card.Inner != null)
                    card.Inner.color = Color.Lerp(new Color(0.07f, 0.09f, 0.13f, 1f), giver, giverBackgroundTint);

                // The marked stat at the value THIS rarity actually delivers, highlighted in
                // the rarity's colour — a Rare card must never quote the Normal number.
                float scalar = Scalar(o.Rarity);
                card.Stat.text = o.Definition.ScaledStatLabel(scalar);
                card.Stat.color = o.Rarity == RewardTier.Normal ? Color.white : BorderFor(o.Rarity);

                string rarityLine = o.Rarity == RewardTier.Normal
                    ? string.Empty
                    : "\n" + o.Rarity.ToString().ToUpperInvariant();
                card.Body.text = $"{o.Definition.Description}\n\n[{o.Definition.Giver} · {o.Definition.Ability}]{rarityLine}";
            }

            Highlight();
        }

        void Highlight()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (i >= offer.Count || cards[i].Frame == null)
                    continue;

                bool focused = i == selection.Index;
                Color border = BorderFor(offer[i].Rarity);
                cards[i].Frame.color = focused ? Color.Lerp(border, Color.white, selectedBrighten) : border;
                cards[i].Root.transform.localScale = Vector3.one * (focused ? selectedScale : 1f);
            }
        }
    }
}
