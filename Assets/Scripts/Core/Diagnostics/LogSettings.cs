using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Diagnostics
{
    /// <summary>
    /// What gets logged, per category. Data rather than code so verbosity can be dialled in
    /// during a playtest without a rebuild.
    /// </summary>
    [CreateAssetMenu(menuName = "Monk/Log Settings", fileName = "LogSettings")]
    public sealed class LogSettings : ScriptableObject, ILogFilter
    {
        [Serializable]
        public struct CategoryRule
        {
            public LogCategory Category;

            [Tooltip("Entries below this level are dropped for this category.")]
            public LogLevel MinimumLevel;

            [Tooltip("Uncheck to silence this category entirely.")]
            public bool Enabled;
        }

        [SerializeField, Tooltip("Global floor. A category can be stricter but never looser than this.")]
        LogLevel globalMinimumLevel = LogLevel.Debug;

        [SerializeField, Tooltip("Per-category overrides. Categories not listed fall back to the global minimum.")]
        CategoryRule[] rules =
        {
            new CategoryRule { Category = LogCategory.Core, MinimumLevel = LogLevel.Info, Enabled = true },
            new CategoryRule { Category = LogCategory.Input, MinimumLevel = LogLevel.Warning, Enabled = true },
            new CategoryRule { Category = LogCategory.Camera, MinimumLevel = LogLevel.Warning, Enabled = true },
            new CategoryRule { Category = LogCategory.Combat, MinimumLevel = LogLevel.Debug, Enabled = true },
            new CategoryRule { Category = LogCategory.Enemy, MinimumLevel = LogLevel.Debug, Enabled = true },
            new CategoryRule { Category = LogCategory.Level, MinimumLevel = LogLevel.Info, Enabled = true },
            new CategoryRule { Category = LogCategory.UI, MinimumLevel = LogLevel.Warning, Enabled = true },
        };

        [Header("Sinks")]
        [SerializeField, Tooltip("Mirror to the Unity console — which also puts entries in Player.log for standalone playtests.")]
        bool writeToUnityConsole = true;

        [SerializeField, Tooltip("How many recent entries the in-game overlay can show.")]
        int overlayBufferSize = 240;

        [SerializeField, Tooltip("Route Unity's own warnings, errors and uncaught exceptions into this log too.")]
        bool captureUnityMessages = true;

        public bool WriteToUnityConsole => writeToUnityConsole;
        public int OverlayBufferSize => Mathf.Max(1, overlayBufferSize);
        public bool CaptureUnityMessages => captureUnityMessages;

        public bool ShouldLog(LogCategory category, LogLevel level)
        {
            if (level < globalMinimumLevel)
                return false;

            if (rules != null)
            {
                for (int i = 0; i < rules.Length; i++)
                {
                    if (rules[i].Category != category)
                        continue;

                    return rules[i].Enabled && level >= rules[i].MinimumLevel;
                }
            }

            return true;
        }
    }
}
