using Game.Combat;
using Game.Core.Economy;
using Game.Level;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Minimal placeholder counters for the run currencies (Seconds, Minutes), the Stopgap
    /// pips and the equipped Stray, plus a float-up feedback line for reward payouts. Builds
    /// its own corner of uGUI at runtime so no scene canvas surgery is needed; the real HUD
    /// pass replaces all of it. Hours and Amber deliberately do NOT appear here — meta
    /// currency lives in the debug overlay until the hub exists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WalletHudView : MonoBehaviour
    {
        [SerializeField] PlayerWallet wallet;
        [SerializeField] StopgapInventory stopgaps;
        [SerializeField] StrayInventory strays;
        [SerializeField] RewardDirector rewards;

        [Header("Feel")]
        [SerializeField, Tooltip("How long the float-up payout line lives.")]
        float feedbackSeconds = 2f;
        [SerializeField, Tooltip("How far the payout line rises across its life, in pixels.")]
        float feedbackRise = 40f;

        Text secondsText;
        Text minutesText;
        Text carriedText;
        Text feedbackText;
        float feedbackRemaining;
        Vector2 feedbackHome;

        void Awake()
        {
            if (wallet == null) wallet = FindAnyObjectByType<PlayerWallet>();
            if (stopgaps == null) stopgaps = FindAnyObjectByType<StopgapInventory>();
            if (strays == null) strays = FindAnyObjectByType<StrayInventory>();
            if (rewards == null) rewards = FindAnyObjectByType<RewardDirector>();

            BuildHud();

            if (wallet != null)
                wallet.Wallet.Changed += OnWalletChanged;
            if (stopgaps != null)
                stopgaps.Changed += RefreshCarried;
            if (strays != null)
                strays.Changed += RefreshCarried;
            if (rewards != null)
                rewards.RewardFeedback += OnRewardFeedback;

            RefreshAll();
        }

        void OnDestroy()
        {
            if (wallet != null)
                wallet.Wallet.Changed -= OnWalletChanged;
            if (stopgaps != null)
                stopgaps.Changed -= RefreshCarried;
            if (strays != null)
                strays.Changed -= RefreshCarried;
            if (rewards != null)
                rewards.RewardFeedback -= OnRewardFeedback;
        }

        void BuildHud()
        {
            // Use the HUD's existing canvas when there is one. A Canvas nested inside another is
            // NOT driven to the screen rect — it keeps its own 100x100 default centred on the
            // parent, so text anchored "to the top-right corner" landed in the middle of the
            // screen instead. That is where these counters have quietly been sitting.
            Canvas host = GetComponentInParent<Canvas>();
            Transform parent;

            if (host != null)
            {
                parent = host.transform;
            }
            else
            {
                var canvasGo = new GameObject("WalletHudCanvas");
                canvasGo.transform.SetParent(transform, false);
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                parent = canvasGo.transform;
            }

            secondsText = MakeLine(parent, new Vector2(-24f, -24f), 26, new Color(0.3f, 0.9f, 1f));
            minutesText = MakeLine(parent, new Vector2(-24f, -58f), 26, new Color(0.95f, 0.85f, 0.5f));
            carriedText = MakeLine(parent, new Vector2(-24f, -92f), 20, new Color(0.8f, 0.82f, 0.88f));

            feedbackText = MakeLine(parent, new Vector2(-24f, -140f), 30, Color.white);
            feedbackHome = ((RectTransform)feedbackText.transform).anchoredPosition;
            feedbackText.text = string.Empty;
        }

        static Text MakeLine(Transform parent, Vector2 anchored, int size, Color color)
        {
            var go = new GameObject("Line");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.UpperRight;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.color = color;

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = anchored;
            rect.sizeDelta = new Vector2(420f, 34f);
            return text;
        }

        void OnWalletChanged(CurrencyType currency, int balance)
        {
            if (currency == CurrencyType.Seconds || currency == CurrencyType.Minutes)
                RefreshAll();
        }

        void RefreshAll()
        {
            if (wallet != null)
            {
                if (secondsText != null)
                    secondsText.text = $"SECONDS  {wallet.Wallet.Get(CurrencyType.Seconds)}";
                if (minutesText != null)
                    minutesText.text = $"MINUTES  {wallet.Wallet.Get(CurrencyType.Minutes)}";
            }

            RefreshCarried();
        }

        void RefreshCarried()
        {
            if (carriedText == null)
                return;

            // Stopgaps used to draw pips here. They moved to StopgapDpadView, which shows WHICH
            // ones are held rather than how many — a count never answered the only question the
            // player actually has in a fight.
            carriedText.text = strays != null && strays.Equipped != null
                ? $"STRAY: {strays.Equipped.DisplayName}"
                : string.Empty;
        }

        void OnRewardFeedback(string message)
        {
            if (feedbackText == null)
                return;

            feedbackText.text = message;
            feedbackRemaining = feedbackSeconds;
        }

        void Update()
        {
            if (feedbackText == null || feedbackRemaining <= 0f)
                return;

            // Unscaled: payouts often land while a draft has the clock paused.
            feedbackRemaining -= Time.unscaledDeltaTime;
            float t = 1f - Mathf.Clamp01(feedbackRemaining / Mathf.Max(0.01f, feedbackSeconds));

            var rect = (RectTransform)feedbackText.transform;
            rect.anchoredPosition = feedbackHome + new Vector2(0f, feedbackRise * t);

            Color color = feedbackText.color;
            color.a = 1f - t;
            feedbackText.color = color;

            if (feedbackRemaining <= 0f)
            {
                feedbackText.text = string.Empty;
                rect.anchoredPosition = feedbackHome;
                color.a = 1f;
                feedbackText.color = color;
            }
        }
    }
}
