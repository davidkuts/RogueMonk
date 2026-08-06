using System.Collections.Generic;

namespace Game.Core.Diagnostics
{
    /// <summary>
    /// Keeps the most recent N entries in memory so the in-game overlay can display them.
    /// Oldest entries fall off the front; the buffer never grows without bound during a run.
    /// </summary>
    public sealed class RingBufferSink : ILogSink
    {
        readonly Queue<LogEntry> entries;
        readonly List<LogEntry> snapshot = new List<LogEntry>();

        public RingBufferSink(int capacity = 200)
        {
            Capacity = capacity < 1 ? 1 : capacity;
            entries = new Queue<LogEntry>(Capacity);
        }

        public int Capacity { get; }

        public int Count => entries.Count;

        public void Write(in LogEntry entry)
        {
            entries.Enqueue(entry);
            while (entries.Count > Capacity)
                entries.Dequeue();
        }

        /// <summary>Oldest first. The returned list is reused — copy it if you need to keep it.</summary>
        public IReadOnlyList<LogEntry> Snapshot()
        {
            snapshot.Clear();
            foreach (LogEntry entry in entries)
                snapshot.Add(entry);

            return snapshot;
        }

        public void Clear()
        {
            entries.Clear();
            snapshot.Clear();
        }
    }
}
