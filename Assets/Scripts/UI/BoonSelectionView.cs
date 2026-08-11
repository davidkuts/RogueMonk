using System.Collections.Generic;
using Game.Combat;
using Game.Core.Audio;
using Game.Core.Diagnostics;
using Game.Core.Timing;
using Game.Level;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// The between-levels boon choice — the beat the run structure exists to create.
    ///
    /// Holds the run open until the player answers rather than running on a timer: this is the one
    /// moment in a run that is a decision instead of an execution, and rushing it would waste it.
    /// The clock is paused through <see cref="GameClock"/> for the same reason pause is — nothing
    /// else may own <c>Time.timeScale</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoonSelectionView : MonoBehaviour
    {
        [SerializeField] LevelDirector director;
        [SerializeField] PlayerBoons boons;
        [SerializeField, Tooltip("Root shown only while choosing.")]
        GameObject root;
        [SerializeField] Text title;
        [SerializeField] Text subtitle;
        [SerializeField, Tooltip("One card per offered boon, in order. Extra cards are hidden.")]
        BoonCard[] cards = new BoonCard[0];

        [Header("Look")]
        [SerializeField] Color selectedColor = new Color(1f, 0.95f, 0.8f);
        [SerializeField] Color unselectedColor = new Color(0.55f, 0.58f, 0.65f);

        [Header("Feel")]
        [SerializeField, Tooltip("Confirm is ignored for this long after the screen opens. The screen arrives the instant the boss dies, so without it a dash press mid-fight picks a boon the player never read.")]
        float inputLockoutSeconds = 0.5f;

        MenuSelection selection;
        float lockout;
        readonly List<BoonDefinition> offer = new List<BoonDefinition>();

        /// <summary>One offer card. A plain container so the view can stay about behaviour.</summary>
        [System.Serializable]
        public sealed class BoonCard
        {
            public GameObject Root;
            public Image Frame;
            public Text Name;
            public Text Description;
        }

        public bool IsShowing { get; private set; }

        void Awake()
        {
            if (director == null) director = FindAnyObjectByType<LevelDirector>();
            if (boons == null) boons = FindAnyObjectByType<PlayerBoons>();

            if (director == null || boons == null)
            {
                Debug.LogError($"{nameof(BoonSelectionView)} on '{name}' needs a director and a {nameof(PlayerBoons)}.", this);
                enabled = false;
                return;
            }

            // RETIRED 2026-08-11: the level-exit door leads straight into the next era and
            // transmissions are the boon system, so nothing triggers this screen any more.
            // The class is kept until the elemental boon system's fate is decided; the
            // component stays inert in the scene.
            Show(false);
        }

        void OnLevelCleared(int levelNumber)
        {
            offer.Clear();
            offer.AddRange(boons.RollOffer(director.Run != null ? director.Run.Random : null));

            // Nothing left to offer — every boon is already held. Skipping straight on beats
            // showing an empty screen the player cannot dismiss.
            if (offer.Count == 0)
            {
                GameLog.Info(LogCategory.Level, "no boons left to offer - continuing straight to the next level");
                director.ContinueToNextLevel();
                return;
            }

            selection = new MenuSelection(offer.Count, axis: MenuAxis.Horizontal);
            selection.Reset();
            lockout = inputLockoutSeconds;

            if (title != null)
                title.text = $"LEVEL {levelNumber} CLEARED";
            if (subtitle != null)
                subtitle.text = "CHOOSE A BOON";

            BindCards();
            Show(true);

            AudioDirector.PlaySound(GameSound.RoomClear);
            if (GameClock.Instance != null)
                GameClock.Instance.SetPaused(true);
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

            // Browsing is allowed immediately; only committing waits. Locking movement too would
            // feel unresponsive for no benefit.
            if (lockout > 0f)
            {
                lockout -= Time.unscaledDeltaTime;
                return;
            }

            if (!MenuSelection.ConfirmPressed())
                return;

            BoonDefinition chosen = offer[Mathf.Clamp(selection.Index, 0, offer.Count - 1)];
            boons.Grant(chosen);

            Show(false);
            if (GameClock.Instance != null)
                GameClock.Instance.SetPaused(false);

            AudioDirector.PlaySound(GameSound.PerfectDodge);
            director.ContinueToNextLevel();
        }

        void BindCards()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                BoonCard card = cards[i];
                if (card == null || card.Root == null)
                    continue;

                bool used = i < offer.Count;
                card.Root.SetActive(used);
                if (!used)
                    continue;

                BoonDefinition boon = offer[i];
                if (card.Name != null)
                {
                    card.Name.text = boon.DisplayName.ToUpperInvariant();
                    card.Name.color = boon.Tint;
                }

                if (card.Description != null)
                    card.Description.text = boon.Description;
            }

            Highlight();
        }

        void Highlight()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                BoonCard card = cards[i];
                if (card == null || card.Frame == null || i >= offer.Count)
                    continue;

                card.Frame.color = i == selection.Index ? selectedColor : unselectedColor;
            }
        }

        void Show(bool visible)
        {
            IsShowing = visible;
            if (root != null)
                root.SetActive(visible);
        }
    }
}
