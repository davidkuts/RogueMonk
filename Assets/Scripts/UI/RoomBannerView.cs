using Game.Level;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Announces each room as the player arrives, and warns when the next door leads to the
    /// boss. Until boss mechanics exist this warning plus the room's tint are the entire
    /// signal, so it is deliberately loud and unmissable rather than a subtle badge.
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
            }
        }

        void OnDestroy()
        {
            if (director == null)
                return;

            director.RoomEntered -= OnRoomEntered;
            director.LevelCompleted -= OnLevelCompleted;
        }

        void OnRoomEntered(RoomPlan room)
        {
            bannerIsBoss = room.IsBossRoom;

            if (room.IsBossRoom)
            {
                bannerText = "BOSS ROOM";
                subText = "placeholder encounter - no boss mechanics yet";
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
            if (remaining <= 0f || string.IsNullOrEmpty(bannerText))
                return;

            EnsureStyles();

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
