using System.Collections.Generic;
using System.Text;
using Game.Combat;
using Game.Core.Diagnostics;
using Game.Core.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// F1 toggles a live log + combat-state readout, F2 clears it. Drawn with IMGUI on
    /// purpose: a debug overlay must work in any build without depending on font assets or
    /// canvas setup, and it must not be mistaken for shipping UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebugOverlayView : MonoBehaviour
    {
        [SerializeField] GameLogBootstrap log;
        [SerializeField] PlayerAttackController attacks;
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerHealth health;
        [SerializeField] Game.Core.Economy.PlayerWallet wallet;
        [SerializeField] TransmissionBoons transmissions;
        [SerializeField] StrayInventory strays;
        [SerializeField] StopgapInventory stopgaps;

        [Header("Display")]
        [SerializeField, Tooltip("Start with the overlay already visible.")]
        bool visibleOnStart;
        [SerializeField, Range(8, 40)] int visibleLines = 18;
        [SerializeField] Key toggleKey = Key.F1;
        [SerializeField] Key clearKey = Key.F2;

        readonly StringBuilder builder = new StringBuilder(512);
        GUIStyle lineStyle;
        GUIStyle panelStyle;
        Texture2D panelTexture;
        bool visible;

        void Awake()
        {
            visible = visibleOnStart;
            if (log == null) log = FindAnyObjectByType<GameLogBootstrap>();
            if (attacks == null) attacks = FindAnyObjectByType<PlayerAttackController>();
            if (motor == null) motor = FindAnyObjectByType<PlayerMotor>();
            if (health == null) health = FindAnyObjectByType<PlayerHealth>();
            if (wallet == null) wallet = FindAnyObjectByType<Game.Core.Economy.PlayerWallet>();
            if (transmissions == null) transmissions = FindAnyObjectByType<TransmissionBoons>();
            if (strays == null) strays = FindAnyObjectByType<StrayInventory>();
            if (stopgaps == null) stopgaps = FindAnyObjectByType<StopgapInventory>();
        }

        void OnDestroy()
        {
            if (panelTexture != null)
                Destroy(panelTexture);
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard[toggleKey].wasPressedThisFrame)
                visible = !visible;

            if (keyboard[clearKey].wasPressedThisFrame && log != null && log.Buffer != null)
                log.Buffer.Clear();
        }

        void OnGUI()
        {
            if (!visible)
                return;

            EnsureStyles();

            float width = Mathf.Min(Screen.width - 24f, 900f);
            float height = (visibleLines + 6) * lineStyle.lineHeight + 24f;
            var rect = new Rect(12f, 12f, width, height);

            GUI.Box(rect, GUIContent.none, panelStyle);
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f));

            DrawState();
            GUILayout.Space(6f);
            DrawLog();

            GUILayout.EndArea();
        }

        void DrawState()
        {
            builder.Clear();
            builder.Append("F1 hide · F2 clear      ");
            builder.AppendFormat("fps {0:000}   ", 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime));
            builder.AppendFormat("timeScale {0:0.00}", Time.timeScale);
            Line(builder.ToString(), Color.white);

            if (health != null)
            {
                builder.Clear();
                builder.AppendFormat("hp    {0:0}/{1:0}", health.CurrentHealth, health.MaxHealth);
                builder.AppendFormat("   invulnerable {0}", health.IsInvulnerable);
                Line(builder.ToString(), health.HealthFraction > 0.35f
                    ? new Color(0.6f, 0.95f, 0.65f)
                    : new Color(1f, 0.45f, 0.4f));
            }

            if (motor != null && motor.Dash != null)
            {
                builder.Clear();
                builder.AppendFormat("dash  charges {0}/{1}  next {2:P0}   ",
                    motor.Dash.Charges.Available, motor.Dash.Charges.Max, motor.Dash.Charges.NextChargeProgress);
                builder.AppendFormat("dashing {0}  i-frames {1}  grace {2:0.000}s",
                    motor.Dash.IsDashing, motor.Dash.IsInvulnerable, motor.Dash.DodgeGraceRemaining);
                Line(builder.ToString(), new Color(0.55f, 0.85f, 0.95f));
            }

            if (attacks != null)
            {
                builder.Clear();
                builder.AppendFormat("attack  {0} {1}   ",
                    attacks.Attacks.Current != null ? attacks.Attacks.Current.Id : "-",
                    attacks.Attacks.Phase);
                builder.AppendFormat("hitstop {0:0.000}s   ", attacks.Hitstop.Remaining);
                builder.AppendFormat("moveMult {0:0.00}  canDash {1}", attacks.MoveSpeedMultiplier, attacks.AllowsDash);
                Line(builder.ToString(), new Color(1f, 0.75f, 0.4f));

                builder.Clear();
                builder.Append("combo  ");
                IReadOnlyList<PlayerAttackController.ComboStepState> steps = attacks.ComboSteps;
                for (int i = 0; steps != null && i < steps.Count; i++)
                {
                    switch (steps[i])
                    {
                        case PlayerAttackController.ComboStepState.Connected: builder.Append("[HIT]"); break;
                        case PlayerAttackController.ComboStepState.Whiffed: builder.Append("[miss]"); break;
                        default: builder.Append("[ - ]"); break;
                    }
                }

                if (attacks.Combo != null)
                    builder.AppendFormat("   window {0:0.00}s", attacks.Combo.WindowRemaining);

                Line(builder.ToString(), new Color(1f, 0.72f, 0.25f));
            }

            // The economy: the only place Hours and Amber are visible until the hub exists.
            if (wallet != null)
            {
                builder.Clear();
                builder.AppendFormat("wallet  Seconds {0}  Minutes {1}  Hours {2}  Amber {3}",
                    wallet.Wallet.Get(Game.Core.Economy.CurrencyType.Seconds),
                    wallet.Wallet.Get(Game.Core.Economy.CurrencyType.Minutes),
                    wallet.Wallet.Get(Game.Core.Economy.CurrencyType.Hours),
                    wallet.Wallet.Get(Game.Core.Economy.CurrencyType.Amber));
                Line(builder.ToString(), new Color(0.95f, 0.85f, 0.5f));
            }

            if (strays != null || stopgaps != null)
            {
                builder.Clear();
                builder.Append("carry   ");
                builder.AppendFormat("stray {0}", strays != null && strays.Equipped != null ? strays.Equipped.DisplayName : "-");
                if (health != null)
                    builder.AppendFormat("   shield {0}", health.HasOneHitShield);
                if (stopgaps != null)
                {
                    builder.AppendFormat("   stopgaps {0}/{1}", stopgaps.Count, stopgaps.CarryCap);
                    for (int i = 0; i < stopgaps.Carried.Count; i++)
                        builder.AppendFormat(" [{0}]", stopgaps.Carried[i].DisplayName);
                }

                Line(builder.ToString(), new Color(0.75f, 0.9f, 0.8f));
            }

            if (transmissions != null && transmissions.Owned.Count > 0)
            {
                builder.Clear();
                builder.Append("boons   ");
                for (int i = 0; i < transmissions.Owned.Count; i++)
                {
                    var owned = transmissions.Owned[i];
                    builder.AppendFormat("{0}{1} [{2}/{3}]", i > 0 ? ", " : string.Empty,
                        owned.Definition.DisplayName, owned.Definition.Ability, owned.Tier);
                }

                Line(builder.ToString(), new Color(0.55f, 0.85f, 0.95f));
            }
        }

        void DrawLog()
        {
            if (log == null || log.Buffer == null)
            {
                Line("(no GameLogBootstrap in scene)", Color.gray);
                return;
            }

            IReadOnlyList<LogEntry> entries = log.Buffer.Snapshot();
            int first = Mathf.Max(0, entries.Count - visibleLines);
            for (int i = first; i < entries.Count; i++)
                Line(entries[i].Format(), ColorFor(entries[i].Level));
        }

        static Color ColorFor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Error: return new Color(1f, 0.45f, 0.4f);
                case LogLevel.Warning: return new Color(1f, 0.85f, 0.4f);
                case LogLevel.Info: return new Color(0.85f, 0.9f, 0.95f);
                default: return new Color(0.6f, 0.65f, 0.7f);
            }
        }

        void Line(string text, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUILayout.Label(text, lineStyle);
            GUI.color = previous;
        }

        void EnsureStyles()
        {
            if (lineStyle != null)
                return;

            lineStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                richText = false,
                wordWrap = false,
            };
            lineStyle.padding = new RectOffset(0, 0, 0, 0);
            lineStyle.margin = new RectOffset(0, 0, 0, 0);

            panelTexture = new Texture2D(1, 1);
            panelTexture.SetPixel(0, 0, new Color(0.02f, 0.03f, 0.05f, 0.82f));
            panelTexture.Apply();

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTexture;
        }
    }
}
