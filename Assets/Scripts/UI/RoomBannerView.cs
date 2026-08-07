using Game.Level;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Announces each room as the player arrives, and warns when the next door leads to the
    /// boss. Deliberately loud rather than a subtle badge — the boss room is the one room the
    /// player should walk into already braced.
    ///
    /// IMGUI placeholder, same as the other pre-M7 UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomBannerView : MonoBehaviour
    {
        [SerializeField] LevelDirector director;

        [Header("Timing")]
        [SerializeField] float bannerSeconds = 2.2f;
        [SerializeField] float fadeSeconds = 0.6f;

        [Header("Colours")]
        [SerializeField] Color standardColor = new Color(0.85f, 0.9f, 0.95f);
        [SerializeField] Color bossColor = new Color(1f, 0.35f, 0.35f);

        string bannerText = string.Empty;
        string subText = string.Empty;
        bool bannerIsBoss;
        float remaining;

        GUIStyle titleStyle;
        GUIStyle subStyle;

        void Awake()
        {
            if (director == null)
                director = FindAnyObjectByType<LevelDirector>();

            if (director != null)
            {
                director.RoomEntered += OnRoomEntered;
                director.LevelCompleted += OnLevelCompleted;
                director.BossEncounterStarted += OnBossEncounterStarted;
            }
        }

        void OnDestroy()
        {
            if (director == null)
                return;

            director.RoomEntered -= OnRoomEntered;
            director.LevelCompleted -= OnLevelCompleted;
            director.BossEncounterStarted -= OnBossEncounterStarted;
        }

        /// <summary>
        /// The boss's name only exists once it has actually spawned, which happens just after the
        /// room is announced. Filling it in here rather than guessing at room-entry time means a
        /// content set with no boss simply shows no name instead of a claim that is not true.
        /// </summary>
        void OnBossEncounterStarted(IBossEncounter encounter)
        {
            subText = encounter.DisplayName.ToUpperInvariant();
            remaining = bannerSeconds;
        }

        void OnRoomEntered(RoomPlan room)
        {
            bannerIsBoss = room.IsBossRoom;

            if (room.IsBossRoom)
            {
                bannerText = "BOSS ROOM";
                subText = string.Empty;
            }
            else
            {
                int total = director.Plan != null ? director.Plan.RoomCount : 0;
                bannerText = $"ROOM {room.Index + 1} / {total}";
                subText = director.BossRoomIsNext ? "the next door leads to the boss" : string.Empty;
            }

            remaining = bannerSeconds;
        }

        void OnLevelCompleted() => remaining = 0f;

        void Update()
        {
            if (remaining > 0f)
                remaining -= Time.unscaledDeltaTime;
        }

        void OnGUI()
        {
            EnsureStyles();
            DrawClearedHint();

            if (remaining <= 0f || string.IsNullOrEmpty(bannerText))
                return;


            float alpha = fadeSeconds > 0f ? Mathf.Clamp01(remaining / fadeSeconds) : 1f;
            Color previous = GUI.color;

            titleStyle.normal.textColor = bannerIsBoss ? bossColor : standardColor;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            var rect = new Rect(0f, Screen.height * 0.12f, Screen.width, 46f);
            GUI.Label(rect, bannerText, titleStyle);

            if (!string.IsNullOrEmpty(subText))
                GUI.Label(new Rect(0f, rect.y + 42f, Screen.width, 24f), subText, subStyle);

            GUI.color = previous;
        }

        /// <summary>
        /// Stays on screen for as long as a cleared room has not been left. Without it the only
        /// sign the door had opened was the blocker vanishing at the far end of the room, which
        /// is easy to miss and left the player believing they were stuck.
        /// </summary>
        void DrawClearedHint()
        {
            if (director == null || director.CurrentRoom == null || !director.CurrentRoom.IsCleared || director.IsComplete)
                return;

            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.75f + Mathf.PingPong(Time.unscaledTime * 0.6f, 0.25f));

            string message = director.BossRoomIsNext
                ? "ROOM CLEAR  -  the door ahead leads to the BOSS"
                : "ROOM CLEAR  -  head through the open door";

            GUI.Label(new Rect(0f, Screen.height - 64f, Screen.width, 26f), message, subStyle);
            GUI.color = previous;
        }

        void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };

            subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
            };
            subStyle.normal.textColor = new Color(0.8f, 0.82f, 0.86f);
        }
    }
}
