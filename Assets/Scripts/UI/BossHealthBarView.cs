using Game.Level;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The boss bar. Hidden entirely until a boss is in play.
    ///
    /// Against a 600 HP Immune-tier enemy that can never be staggered, this bar <em>is</em> the hit
    /// reaction: with no healing in a run, the size of each loss is the only progress the player
    /// gets. The decisions live in <see cref="BossBarModel"/>; this only draws them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossHealthBarView : MonoBehaviour
    {
        [SerializeField] LevelDirector director;
        [SerializeField, Tooltip("Root enabled only while a boss is alive.")]
        GameObject root;
        [SerializeField] Image fill;
        [SerializeField, Tooltip("Trails the real value so a hit reads as an amount lost.")]
        Image chip;
        [SerializeField] Text nameLabel;
        [SerializeField, Tooltip("Optional. One divider per later phase, positioned at its threshold.")]
        RectTransform dividerContainer;
        [SerializeField] RectTransform dividerPrefab;

        [Header("Colours")]
        [SerializeField] Color barColor = new Color(0.85f, 0.3f, 0.32f);
        [SerializeField] Color chipColor = new Color(1f, 0.75f, 0.4f, 0.7f);
        [SerializeField, Tooltip("Flashed for a moment when a phase threshold is crossed — the punish window the player just earned.")]
        Color phaseBreakColor = new Color(0.55f, 0.85f, 1f);

        [Header("Feel")]
        [SerializeField] float chipDrainPerSecond = 0.25f;
        [SerializeField] float chipDelaySeconds = 0.4f;
        [SerializeField] float phaseFlashSeconds = 0.6f;

        BossBarModel model;
        IBossEncounter encounter;
        EnemyLabSpawner lab;
        float flashRemaining;

        void Awake()
        {
            model = new BossBarModel(chipDrainPerSecond, chipDelaySeconds);

            if (director == null)
                director = FindAnyObjectByType<LevelDirector>();

            // A missing director is not an error any more: the enemy lab has no level generation at
            // all, and a boss spawned there drives this bar through ShowFor instead. Disabling the
            // component here would have made the Tyrant the only boss in the game without a bar.
            if (director != null)
            {
                // Bound to the director, not the room runner: the director outlives every room, so
                // the bar survives the teardown between rooms without re-subscribing.
                director.BossEncounterStarted += OnBossStarted;
                director.BossEncounterEnded += OnBossEnded;
            }

            // The enemy lab has no level generation at all, so a boss spawned there announces
            // itself instead. Subscribing to both means the bar behaves identically either way.
            lab = FindAnyObjectByType<EnemyLabSpawner>(FindObjectsInactive.Include);
            if (lab != null)
            {
                lab.BossSpawned += OnBossStarted;
                lab.BossCleared += OnBossEnded;
            }

            Show(false);
        }

        void OnDestroy()
        {
            if (director != null)
            {
                director.BossEncounterStarted -= OnBossStarted;
                director.BossEncounterEnded -= OnBossEnded;
            }

            if (lab != null)
            {
                lab.BossSpawned -= OnBossStarted;
                lab.BossCleared -= OnBossEnded;
            }
        }

        /// <summary>
        /// Puts the bar up for an encounter that did not come from level generation — a boss
        /// spawned straight into the enemy lab. Same path the director drives, so the bar behaves
        /// identically either way.
        /// </summary>
        public void ShowFor(IBossEncounter started)
        {
            if (started != null)
                OnBossStarted(started);
        }

        /// <summary>Takes the bar down again, for a lab arena being cleared.</summary>
        public void Hide() => OnBossEnded();

        void OnBossStarted(IBossEncounter started)
        {
            encounter = started;
            model.Bind(started.PhaseCount, started.PhaseThresholds);
            flashRemaining = 0f;

            if (nameLabel != null)
                nameLabel.text = started.DisplayName;

            if (chip != null)
                chip.color = chipColor;

            BuildDividers(started);
            Show(true);
        }

        void OnBossEnded()
        {
            encounter = null;
            model.Clear();
            Show(false);
        }

        void LateUpdate()
        {
            if (encounter == null)
                return;

            // Unscaled: the chip must keep draining through the hitstop of the very blow that
            // caused it, which is precisely when the player is looking at the bar.
            float deltaTime = Time.unscaledDeltaTime;
            model.Tick(deltaTime, encounter.HealthFraction, encounter.PhaseIndex);

            if (model.PhaseJustBroke)
                flashRemaining = phaseFlashSeconds;

            if (flashRemaining > 0f)
                flashRemaining -= deltaTime;

            if (fill != null)
            {
                fill.fillAmount = model.Fill;

                float flash = phaseFlashSeconds > 0f ? Mathf.Clamp01(flashRemaining / phaseFlashSeconds) : 0f;
                fill.color = flash > 0f ? Color.Lerp(barColor, phaseBreakColor, flash) : barColor;
            }

            if (chip != null)
                chip.fillAmount = model.Chip;
        }

        /// <summary>
        /// Places one divider per later phase at its health threshold, so the player can see how
        /// far the next phase break is rather than being surprised by it.
        /// </summary>
        void BuildDividers(IBossEncounter started)
        {
            if (dividerContainer == null || dividerPrefab == null)
                return;

            for (int i = dividerContainer.childCount - 1; i >= 0; i--)
                Destroy(dividerContainer.GetChild(i).gameObject);

            for (int i = 0; i < started.PhaseThresholds.Count; i++)
            {
                float threshold = Mathf.Clamp01(started.PhaseThresholds[i]);
                RectTransform divider = Instantiate(dividerPrefab, dividerContainer);
                divider.gameObject.SetActive(true);
                divider.anchorMin = new Vector2(threshold, 0f);
                divider.anchorMax = new Vector2(threshold, 1f);
                divider.anchoredPosition = Vector2.zero;
            }
        }

        void Show(bool visible)
        {
            if (root != null)
                root.SetActive(visible);
            else
                gameObject.SetActive(visible);
        }
    }
}
