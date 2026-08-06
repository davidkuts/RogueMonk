using System;
using System.Collections.Generic;

namespace Game.Core.Diagnostics
{
    /// <summary>
    /// Central log hub. Everything the game reports goes through here so it can be filtered
    /// by category, mirrored to any number of sinks, and read back in-game by the debug
    /// overlay. Deliberately sink-agnostic: the Unity console is just one destination, which
    /// is what makes this testable without touching the editor console.
    /// </summary>
    public static class GameLog
    {
        static readonly List<ILogSink> sinks = new List<ILogSink>();

        /// <summary>Null means everything passes.</summary>
        public static ILogFilter Filter { get; set; }

        /// <summary>Swapped out in tests so entries get deterministic timestamps.</summary>
        public static Func<float> TimeProvider { get; set; }

        /// <summary>Swapped out in tests so entries get deterministic frame numbers.</summary>
        public static Func<int> FrameProvider { get; set; }

        /// <summary>Raised for every entry that passes the filter, after all sinks have run.</summary>
        public static event Action<LogEntry> Written;

        public static IReadOnlyList<ILogSink> Sinks => sinks;

        public static void AddSink(ILogSink sink)
        {
            if (sink != null && !sinks.Contains(sink))
                sinks.Add(sink);
        }

        public static bool RemoveSink(ILogSink sink) => sinks.Remove(sink);

        public static void ClearSinks() => sinks.Clear();

        /// <summary>Drops sinks, filter, providers and subscribers. Used between tests.</summary>
        public static void Reset()
        {
            sinks.Clear();
            Filter = null;
            TimeProvider = null;
            FrameProvider = null;
            Written = null;
        }

        public static bool IsEnabled(LogCategory category, LogLevel level) =>
            Filter == null || Filter.ShouldLog(category, level);

        public static void Write(LogCategory category, LogLevel level, string message)
        {
            if (!IsEnabled(category, level))
                return;

            var entry = new LogEntry(
                category,
                level,
                message ?? string.Empty,
                TimeProvider != null ? TimeProvider() : 0f,
                FrameProvider != null ? FrameProvider() : 0);

            for (int i = 0; i < sinks.Count; i++)
            {
                // One misbehaving sink must not stop the others or break the caller.
                try
                {
                    sinks[i].Write(in entry);
                }
                catch (Exception)
                {
                    // Swallowed on purpose: logging must never throw into gameplay.
                }
            }

            Written?.Invoke(entry);
        }

        public static void Debug(LogCategory category, string message) => Write(category, LogLevel.Debug, message);

        public static void Info(LogCategory category, string message) => Write(category, LogLevel.Info, message);

        public static void Warn(LogCategory category, string message) => Write(category, LogLevel.Warning, message);

        public static void Error(LogCategory category, string message) => Write(category, LogLevel.Error, message);
    }
}
