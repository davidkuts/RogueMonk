using UnityEngine;

namespace Game.Core.Diagnostics
{
    /// <summary>
    /// Mirrors entries into Unity's console — which also means they land in Player.log in a
    /// standalone build, so a playtest session can be read back afterwards without any extra
    /// file plumbing.
    /// </summary>
    public sealed class UnityConsoleSink : ILogSink
    {
        /// <summary>Set while this sink is writing, so capture of Unity's own log stream can ignore the echo.</summary>
        public static bool IsEmitting { get; private set; }

        public void Write(in LogEntry entry)
        {
            string line = entry.Format();
            IsEmitting = true;
            try
            {
                switch (entry.Level)
                {
                    case LogLevel.Error:
                        UnityEngine.Debug.LogError(line);
                        break;
                    case LogLevel.Warning:
                        UnityEngine.Debug.LogWarning(line);
                        break;
                    default:
                        UnityEngine.Debug.Log(line);
                        break;
                }
            }
            finally
            {
                IsEmitting = false;
            }
        }
    }
}
