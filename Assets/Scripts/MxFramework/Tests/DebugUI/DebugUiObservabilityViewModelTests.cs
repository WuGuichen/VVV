using MxFramework.DebugUI;
using NUnit.Framework;

namespace MxFramework.Tests.DebugUI
{
    public sealed class DebugUiObservabilityViewModelTests
    {
        [Test]
        public void Timeline_FiltersSortsAndKeepsRecentEntries()
        {
            var entries = new[]
            {
                new DebugUiTimelineEntryViewModel(3, "Combat", "Hit", "2", "c", "late"),
                new DebugUiTimelineEntryViewModel(1, "Gameplay", "Pressure", "1:1", "a", "early"),
                new DebugUiTimelineEntryViewModel(2, "Gameplay", "Pressure", "1:1", "b", "middle")
            };

            DebugUiTimelineViewModel timeline = DebugUiTimelineViewModel.From(
                entries,
                new DebugUiTimelineFilter("Gameplay", "1:1", "Pressure"),
                maxEntries: 1);

            Assert.AreEqual(1, timeline.Count);
            Assert.AreEqual(2, timeline.Entries[0].Frame);
            Assert.That(DebugUiObservabilityFormatter.FormatTimeline(timeline), Does.Contain("middle"));
        }

        [Test]
        public void EntityWatch_FiltersByEntity()
        {
            var entries = new[]
            {
                new DebugUiEntityWatchEntryViewModel("1:1", "player", "alive", "1=80/100", "Stable", "Pressed", "10/20", ""),
                new DebugUiEntityWatchEntryViewModel("2:1", "enemy", "alive", "1=0/100", "Broken", "Broken", "0/10 broken", "")
            };

            DebugUiEntityWatchViewModel watch = DebugUiEntityWatchViewModel.From(entries, "2:1");

            Assert.AreEqual(1, watch.Count);
            Assert.AreEqual("enemy", watch.Entities[0].DisplayName);
            Assert.That(DebugUiObservabilityFormatter.FormatEntityWatch(watch), Does.Contain("armor=0/10 broken"));
        }
    }
}
