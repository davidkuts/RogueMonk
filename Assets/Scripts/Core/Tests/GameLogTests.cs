using System.Collections.Generic;
using Game.Core.Diagnostics;
using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>Collects entries so tests never touch the Unity console.</summary>
    internal sealed class RecordingSink : ILogSink
    {
        public List<LogEntry> Entries { get; } = new List<LogEntry>();

        public void Write(in LogEntry entry) => Entries.Add(entry);
    }

    internal sealed class ThrowingSink : ILogSink
    {
        public void Write(in LogEntry entry) => throw new System.InvalidOperationException("sink failure");
    }

    internal sealed class FakeFilter : ILogFilter
    {
        public LogCategory AllowedCategory { get; set; } = LogCategory.Combat;
        public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

        public bool ShouldLog(LogCategory category, LogLevel level) =>
            category == AllowedCategory && level >= MinimumLevel;
    }

    public class GameLogTests
    {
        RecordingSink sink;

        [SetUp]
        public void SetUp()
        {
            GameLog.Reset();
            sink = new RecordingSink();
            GameLog.AddSink(sink);
            GameLog.TimeProvider = () => 1.5f;
            GameLog.FrameProvider = () => 42;
        }

        [TearDown]
        public void TearDown() => GameLog.Reset();

        [Test]
        public void WritesReachEverySink()
        {
            var second = new RecordingSink();
            GameLog.AddSink(second);

            GameLog.Info(LogCategory.Combat, "hello");

            Assert.That(sink.Entries, Has.Count.EqualTo(1));
            Assert.That(second.Entries, Has.Count.EqualTo(1));
        }

        [Test]
        public void EntryCarriesCategoryLevelMessageTimeAndFrame()
        {
            GameLog.Warn(LogCategory.Enemy, "careful");

            LogEntry entry = sink.Entries[0];
            Assert.That(entry.Category, Is.EqualTo(LogCategory.Enemy));
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(entry.Message, Is.EqualTo("careful"));
            Assert.That(entry.TimeSeconds, Is.EqualTo(1.5f));
            Assert.That(entry.Frame, Is.EqualTo(42));
        }

        [Test]
        public void FormatIncludesTheEssentials()
        {
            GameLog.Info(LogCategory.Combat, "punch landed");
            string line = sink.Entries[0].Format();

            Assert.That(line, Does.Contain("Combat"));
            Assert.That(line, Does.Contain("Info"));
            Assert.That(line, Does.Contain("punch landed"));
            Assert.That(line, Does.Contain("f#42"));
        }

        [Test]
        public void FilterSuppressesOtherCategories()
        {
            GameLog.Filter = new FakeFilter { AllowedCategory = LogCategory.Combat };

            GameLog.Info(LogCategory.Combat, "kept");
            GameLog.Info(LogCategory.Level, "dropped");

            Assert.That(sink.Entries, Has.Count.EqualTo(1));
            Assert.That(sink.Entries[0].Message, Is.EqualTo("kept"));
        }

        [Test]
        public void FilterSuppressesLevelsBelowTheMinimum()
        {
            GameLog.Filter = new FakeFilter { AllowedCategory = LogCategory.Combat, MinimumLevel = LogLevel.Warning };

            GameLog.Debug(LogCategory.Combat, "noise");
            GameLog.Info(LogCategory.Combat, "noise");
            GameLog.Error(LogCategory.Combat, "signal");

            Assert.That(sink.Entries, Has.Count.EqualTo(1));
            Assert.That(sink.Entries[0].Message, Is.EqualTo("signal"));
        }

        [Test]
        public void IsEnabledMatchesTheFilter_SoCallersCanSkipExpensiveFormatting()
        {
            GameLog.Filter = new FakeFilter { AllowedCategory = LogCategory.Combat, MinimumLevel = LogLevel.Warning };

            Assert.That(GameLog.IsEnabled(LogCategory.Combat, LogLevel.Error), Is.True);
            Assert.That(GameLog.IsEnabled(LogCategory.Combat, LogLevel.Debug), Is.False);
            Assert.That(GameLog.IsEnabled(LogCategory.Level, LogLevel.Error), Is.False);
        }

        [Test]
        public void NoFilterMeansEverythingPasses()
        {
            GameLog.Filter = null;
            GameLog.Debug(LogCategory.UI, "anything");
            Assert.That(sink.Entries, Has.Count.EqualTo(1));
        }

        [Test]
        public void AThrowingSinkNeverBreaksTheCallerOrTheOtherSinks()
        {
            // Logging is called from gameplay code; it must never be able to take it down.
            GameLog.AddSink(new ThrowingSink());
            var third = new RecordingSink();
            GameLog.AddSink(third);

            Assert.DoesNotThrow(() => GameLog.Error(LogCategory.Core, "still fine"));
            Assert.That(third.Entries, Has.Count.EqualTo(1));
        }

        [Test]
        public void WrittenEventFiresForPassingEntriesOnly()
        {
            GameLog.Filter = new FakeFilter { AllowedCategory = LogCategory.Combat };
            int raised = 0;
            GameLog.Written += _ => raised++;

            GameLog.Info(LogCategory.Combat, "kept");
            GameLog.Info(LogCategory.UI, "dropped");

            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void NullMessageBecomesEmptyRatherThanThrowing()
        {
            Assert.DoesNotThrow(() => GameLog.Info(LogCategory.Core, null));
            Assert.That(sink.Entries[0].Message, Is.EqualTo(string.Empty));
        }

        [Test]
        public void RemoveSinkStopsDelivery()
        {
            GameLog.RemoveSink(sink);
            GameLog.Info(LogCategory.Core, "gone");
            Assert.That(sink.Entries, Is.Empty);
        }
    }

    public class RingBufferSinkTests
    {
        static LogEntry Entry(string message) =>
            new LogEntry(LogCategory.Combat, LogLevel.Info, message, 0f, 0);

        [Test]
        public void KeepsEntriesInOrder()
        {
            var buffer = new RingBufferSink(10);
            buffer.Write(Entry("a"));
            buffer.Write(Entry("b"));

            IReadOnlyList<LogEntry> snapshot = buffer.Snapshot();
            Assert.That(snapshot[0].Message, Is.EqualTo("a"));
            Assert.That(snapshot[1].Message, Is.EqualTo("b"));
        }

        [Test]
        public void DropsOldestPastCapacity()
        {
            var buffer = new RingBufferSink(3);
            for (int i = 0; i < 5; i++)
                buffer.Write(Entry(i.ToString()));

            IReadOnlyList<LogEntry> snapshot = buffer.Snapshot();
            Assert.That(buffer.Count, Is.EqualTo(3));
            Assert.That(snapshot[0].Message, Is.EqualTo("2"));
            Assert.That(snapshot[2].Message, Is.EqualTo("4"));
        }

        [Test]
        public void CapacityIsAtLeastOne()
        {
            var buffer = new RingBufferSink(0);
            buffer.Write(Entry("a"));
            Assert.That(buffer.Capacity, Is.EqualTo(1));
            Assert.That(buffer.Count, Is.EqualTo(1));
        }

        [Test]
        public void ClearEmptiesTheBuffer()
        {
            var buffer = new RingBufferSink(5);
            buffer.Write(Entry("a"));
            buffer.Clear();
            Assert.That(buffer.Count, Is.EqualTo(0));
            Assert.That(buffer.Snapshot(), Is.Empty);
        }
    }
}
