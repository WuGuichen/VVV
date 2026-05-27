using System;
using System.Text;
using MxFramework.Diagnostics;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.Story;

namespace MxFramework.Demo.CharacterTest
{
    /// <summary>
    /// CharacterTest slice session summary for Debug UI (frame clock, pause gate, resources bootstrap).
    /// </summary>
    public sealed class CharacterTestSliceDebugSource : IFrameworkDebugSource
    {
        private readonly GameSlice _slice;
        private readonly Func<bool> _isPausedProvider;

        public CharacterTestSliceDebugSource(
            GameSlice slice,
            Func<bool> isPausedProvider = null,
            string name = "CharacterTest")
        {
            _slice = slice;
            _isPausedProvider = isPausedProvider;
            Name = string.IsNullOrWhiteSpace(name) ? "CharacterTest" : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => _slice != null;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            if (!IsAvailable)
            {
                return new FrameworkDebugSnapshot(
                    Name,
                    Mode,
                    new[] { new FrameworkDebugSection("Status", "unavailable") });
            }

            RuntimeHostDiagnostics host = _slice.CaptureDiagnostics();
            ResourceDebugSnapshot resources = TryCreateResourceSnapshot();

            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection("Summary", CreateSummary(host, resources)),
                    new FrameworkDebugSection("Story Session", CreateStorySessionSection()),
                    new FrameworkDebugSection("Resources", CreateResourcesSection(resources))
                });
        }

        private string CreateSummary(RuntimeHostDiagnostics host, ResourceDebugSnapshot resources)
        {
            bool paused = _isPausedProvider != null && _isPausedProvider();
            var builder = new StringBuilder();
            builder.Append("frame=").Append(_slice.CurrentFrame.Value).Append('\n');
            builder.Append("elapsedSeconds=").Append(_slice.ElapsedSeconds.ToString("F3")).Append('\n');
            builder.Append("paused=").Append(paused ? "true" : "false").Append('\n');
            builder.Append("hostState=").Append(host.State).Append('\n');
            builder.Append("tickCount=").Append(host.TickCount).Append('\n');
            builder.Append("moduleCount=").Append(host.Modules.Count).Append('\n');
            builder.Append("hostErrors=").Append(host.Errors.Count);
            if (resources != null)
            {
                builder.Append('\n');
                builder.Append("resourceMode=").Append(_slice.Resources.Mode);
                builder.Append('\n');
                builder.Append("catalogs=").Append(resources.CatalogCount);
                builder.Append('\n');
                builder.Append("loadedEntries=").Append(resources.LoadedCount);
            }

            return builder.ToString();
        }

        private string CreateStorySessionSection()
        {
            StoryDirectorSnapshot story = _slice.StorySnapshot;
            return "graphCount=" + story.Graphs.Count
                + "\nactiveBeats=" + story.ActiveBeatInstances.Count
                + "\nblackboardFacts=" + story.Facts.Count
                + "\nrecentEvents=" + _slice.StoryModule.RecentEvents.Count
                + "\npendingCommandErrors=" + _slice.StoryModule.LastCommandErrors.Count;
        }

        private static string CreateResourcesSection(ResourceDebugSnapshot resources)
        {
            if (resources == null)
                return "unavailable";

            return "catalogs=" + resources.CatalogCount
                + "\nentries=" + resources.EntryCount
                + "\nloaded=" + resources.LoadedCount
                + "\nloading=" + resources.LoadingCount
                + "\nfailed=" + resources.FailedCount
                + "\nrecentErrors=" + resources.RecentErrors.Count;
        }

        private ResourceDebugSnapshot TryCreateResourceSnapshot()
        {
            try
            {
                return _slice.Resources?.ResourceManager?.CreateDebugSnapshot();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
