using UnityEngine;

namespace Game.Core.Diagnostics
{
    /// <summary>
    /// Installs the log sinks and, crucially, funnels Unity's <em>own</em> warnings, errors
    /// and uncaught exceptions into the same stream — so the overlay shows real failures and
    /// not just the messages we remembered to write ourselves.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class GameLogBootstrap : MonoBehaviour
    {
        [SerializeField] LogSettings settings;

        RingBufferSink ringBuffer;
        UnityConsoleSink consoleSink;
        bool capturing;

        /// <summary>Recent entries, for the debug overlay.</summary>
        public RingBufferSink Buffer => ringBuffer;

        public LogSettings Settings => settings;

        void Awake()
        {
            GameLog.Reset();
            GameLog.TimeProvider = () => Time.unscaledTime;
            GameLog.FrameProvider = () => Time.frameCount;
            GameLog.Filter = settings;

            ringBuffer = new RingBufferSink(settings != null ? settings.OverlayBufferSize : 240);
            GameLog.AddSink(ringBuffer);

            if (settings == null || settings.WriteToUnityConsole)
            {
                consoleSink = new UnityConsoleSink();
                GameLog.AddSink(consoleSink);
            }

            if (settings == null || settings.CaptureUnityMessages)
            {
                Application.logMessageReceived += OnUnityLog;
                capturing = true;
            }

            GameLog.Info(LogCategory.Core, $"Log started. Unity capture={capturing}, buffer={ringBuffer.Capacity}.");
        }

        void OnDestroy()
        {
            if (capturing)
                Application.logMessageReceived -= OnUnityLog;

            GameLog.Reset();
        }

        void OnUnityLog(string condition, string stackTrace, LogType type)
        {
            // Our own console sink writes through Debug.Log, which comes straight back here.
            // Without this guard every entry would be logged twice and recurse.
            if (UnityConsoleSink.IsEmitting)
                return;

            LogLevel level;
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    level = LogLevel.Error;
                    break;
                case LogType.Warning:
                    level = LogLevel.Warning;
                    break;
                default:
                    return; // plain Debug.Log from third-party code is noise, not signal
            }

            string message = level == LogLevel.Error && !string.IsNullOrEmpty(stackTrace)
                ? $"{condition}\n{stackTrace}"
                : condition;

            // Bypass the console sink for these — Unity already printed them.
            LogEntry entry = new LogEntry(
                LogCategory.Core, level, $"[unity] {message}", Time.unscaledTime, Time.frameCount);
            ringBuffer.Write(in entry);
        }
    }
}
