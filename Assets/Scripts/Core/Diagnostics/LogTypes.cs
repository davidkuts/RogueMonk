using System.Globalization;

namespace Game.Core.Diagnostics
{
    /// <summary>Subsystem a log entry came from. Filterable independently.</summary>
    public enum LogCategory
    {
        Core = 0,
        Input = 1,
        Camera = 2,
        Combat = 3,
        Enemy = 4,
        Level = 5,
        UI = 6,
    }

    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    /// <summary>One immutable log record.</summary>
    public readonly struct LogEntry
    {
        public readonly LogCategory Category;
        public readonly LogLevel Level;
        public readonly string Message;

        /// <summary>Unscaled game time, so hitstop does not distort the timeline.</summary>
        public readonly float TimeSeconds;

        public readonly int Frame;

        public LogEntry(LogCategory category, LogLevel level, string message, float timeSeconds, int frame)
        {
            Category = category;
            Level = level;
            Message = message;
            TimeSeconds = timeSeconds;
            Frame = frame;
        }

        /// <summary>Single-line form: "[  12.345 f#742] Combat/Info  message".</summary>
        public string Format() =>
            string.Format(
                CultureInfo.InvariantCulture,
                "[{0,8:F3} f#{1}] {2}/{3}  {4}",
                TimeSeconds, Frame, Category, Level, Message);

        public override string ToString() => Format();
    }

    /// <summary>Destination for log entries that pass the filter.</summary>
    public interface ILogSink
    {
        void Write(in LogEntry entry);
    }

    /// <summary>Decides what gets through. Implemented by the LogSettings asset.</summary>
    public interface ILogFilter
    {
        bool ShouldLog(LogCategory category, LogLevel level);
    }
}
