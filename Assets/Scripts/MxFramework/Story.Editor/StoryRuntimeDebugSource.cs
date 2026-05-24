using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Diagnostics;
using MxFramework.Runtime;
using MxFramework.Story.Runtime;

namespace MxFramework.Story.Editor
{
    public sealed class StoryRuntimeDebugSource : IFrameworkDebugSource
    {
        public const string DefaultSourceName = "StoryRuntime";

        private readonly IStoryDirector _director;
        private readonly RuntimeEventQueue<StoryRuntimeEvent> _events;
        private readonly Func<IReadOnlyList<RuntimeCommand>> _lastCommandsProvider;
        private readonly Func<IReadOnlyList<RuntimeCommandError>> _lastErrorsProvider;
        private readonly Func<IReadOnlyList<StoryRuntimeEvent>> _recentEventsProvider;

        public StoryRuntimeDebugSource(
            StoryRuntimeModule module,
            string name = DefaultSourceName,
            Func<IReadOnlyList<StoryRuntimeEvent>> recentEventsProvider = null)
            : this(
                module != null ? module.Director : null,
                module != null ? module.Events : null,
                module != null ? (Func<IReadOnlyList<RuntimeCommand>>)(() => module.LastDrainedCommands) : null,
                module != null ? (Func<IReadOnlyList<RuntimeCommandError>>)(() => module.LastCommandErrors) : null,
                recentEventsProvider ?? (module != null ? (Func<IReadOnlyList<StoryRuntimeEvent>>)(() => module.RecentEvents) : null),
                name)
        {
        }

        public StoryRuntimeDebugSource(
            IStoryDirector director,
            RuntimeEventQueue<StoryRuntimeEvent> events = null,
            Func<IReadOnlyList<RuntimeCommand>> lastCommandsProvider = null,
            Func<IReadOnlyList<RuntimeCommandError>> lastErrorsProvider = null,
            Func<IReadOnlyList<StoryRuntimeEvent>> recentEventsProvider = null,
            string name = DefaultSourceName)
        {
            _director = director;
            _events = events;
            _lastCommandsProvider = lastCommandsProvider;
            _lastErrorsProvider = lastErrorsProvider;
            _recentEventsProvider = recentEventsProvider;
            Name = string.IsNullOrWhiteSpace(name) ? DefaultSourceName : name;
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => _director != null;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            if (_director == null)
            {
                return new FrameworkDebugSnapshot(
                    Name,
                    Mode,
                    new[] { new FrameworkDebugSection("状态", "unavailable") });
            }

            StoryDirectorSnapshot snapshot = _director.CreateSnapshot();
            RuntimeEventQueueSnapshot? eventQueue = _events != null ? _events.CreateSnapshot() : (RuntimeEventQueueSnapshot?)null;

            var sections = new List<FrameworkDebugSection>
            {
                new FrameworkDebugSection("摘要", CreateSummary(snapshot, eventQueue)),
                new FrameworkDebugSection("Graphs", CreateGraphs(snapshot)),
                new FrameworkDebugSection("Beats", CreateBeats(snapshot)),
                new FrameworkDebugSection("Blackboard", CreateBlackboard(snapshot)),
                new FrameworkDebugSection("事件队列", CreateEventQueue(eventQueue))
            };

            IReadOnlyList<StoryRuntimeEvent> recentEvents = _recentEventsProvider != null ? _recentEventsProvider() : null;
            if (recentEvents != null)
            {
                sections.Add(new FrameworkDebugSection("最近事件", CreateRecentEvents(recentEvents)));
            }

            IReadOnlyList<RuntimeCommand> commands = _lastCommandsProvider != null ? _lastCommandsProvider() : null;
            if (commands != null)
            {
                sections.Add(new FrameworkDebugSection("最近命令", CreateCommands(commands)));
            }

            IReadOnlyList<RuntimeCommandError> errors = _lastErrorsProvider != null ? _lastErrorsProvider() : null;
            if (errors != null)
            {
                sections.Add(new FrameworkDebugSection("命令错误", CreateErrors(errors)));
            }

            return new FrameworkDebugSnapshot(Name, Mode, sections);
        }

        private static string CreateSummary(StoryDirectorSnapshot snapshot, RuntimeEventQueueSnapshot? eventQueue)
        {
            int pendingEvents = eventQueue.HasValue ? eventQueue.Value.PendingCount : 0;
            return "schema=" + snapshot.SchemaVersion
                + "\nnextBeatInstanceId=" + snapshot.NextBeatInstanceId
                + "\ngraphs=" + snapshot.Graphs.Count
                + "\nactiveBeats=" + snapshot.ActiveBeatInstances.Count
                + "\nblackboardFacts=" + snapshot.Facts.Count
                + "\npendingEvents=" + pendingEvents;
        }

        private static string CreateGraphs(StoryDirectorSnapshot snapshot)
        {
            if (snapshot.Graphs.Count == 0)
            {
                return "none";
            }

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.Graphs.Count; i++)
            {
                StoryGraphRuntimeSnapshot graph = snapshot.Graphs[i];
                builder.Append("graph=")
                    .Append(graph.GraphId)
                    .Append(" version=")
                    .Append(graph.Version)
                    .Append(" status=")
                    .Append(graph.Status);
                AppendLineIfNeeded(builder, i, snapshot.Graphs.Count);
            }

            return builder.ToString();
        }

        private static string CreateBeats(StoryDirectorSnapshot snapshot)
        {
            if (snapshot.ActiveBeatInstances.Count == 0)
            {
                return "none";
            }

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.ActiveBeatInstances.Count; i++)
            {
                StoryBeatInstanceSnapshot beat = snapshot.ActiveBeatInstances[i];
                builder.Append("instance=")
                    .Append(beat.BeatInstanceId)
                    .Append(" graph=")
                    .Append(beat.GraphId)
                    .Append(" beat=")
                    .Append(beat.BeatId)
                    .Append(" stepIndex=")
                    .Append(beat.CurrentStepIndex)
                    .Append(" pendingStep=")
                    .Append(beat.PendingPresentationStepId)
                    .Append(" wait=")
                    .Append(beat.PendingPresentationPolicy)
                    .Append(" choiceSet=")
                    .Append(beat.AwaitingChoiceSetId);
                AppendLineIfNeeded(builder, i, snapshot.ActiveBeatInstances.Count);
            }

            return builder.ToString();
        }

        private static string CreateBlackboard(StoryDirectorSnapshot snapshot)
        {
            if (snapshot.Facts.Count == 0)
            {
                return "none";
            }

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.Facts.Count; i++)
            {
                StoryFactEntry fact = snapshot.Facts[i];
                builder.Append(fact.Key)
                    .Append('=')
                    .Append(FormatValue(fact.Value));
                AppendLineIfNeeded(builder, i, snapshot.Facts.Count);
            }

            return builder.ToString();
        }

        private static string CreateEventQueue(RuntimeEventQueueSnapshot? snapshot)
        {
            if (!snapshot.HasValue)
            {
                return "not bound";
            }

            RuntimeEventQueueSnapshot value = snapshot.Value;
            return "type=" + value.EventTypeName
                + "\npending=" + value.PendingCount
                + "\noldestFrame=" + value.OldestFrame
                + "\nnewestFrame=" + value.NewestFrame
                + "\nnextSequence=" + value.NextSequence;
        }

        private static string CreateRecentEvents(IReadOnlyList<StoryRuntimeEvent> events)
        {
            if (events.Count == 0)
            {
                return "none";
            }

            var builder = new StringBuilder();
            for (int i = 0; i < events.Count; i++)
            {
                StoryRuntimeEvent evt = events[i];
                builder.Append("frame=")
                    .Append(evt.Frame)
                    .Append(" kind=")
                    .Append(evt.Kind)
                    .Append(" graph=")
                    .Append(evt.GraphId)
                    .Append(" beat=")
                    .Append(evt.BeatId)
                    .Append(" instance=")
                    .Append(evt.BeatInstanceId)
                    .Append(" step=")
                    .Append(evt.StepId)
                    .Append(" choiceSet=")
                    .Append(evt.ChoiceSetId)
                    .Append(" aux=")
                    .Append(evt.AuxId);
                AppendLineIfNeeded(builder, i, events.Count);
            }

            return builder.ToString();
        }

        private static string CreateCommands(IReadOnlyList<RuntimeCommand> commands)
        {
            if (commands.Count == 0)
            {
                return "none";
            }

            var builder = new StringBuilder();
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                builder.Append("frame=")
                    .Append(command.Frame)
                    .Append(" source=")
                    .Append(command.SourceId)
                    .Append(" command=")
                    .Append(command.CommandId)
                    .Append(" target=")
                    .Append(command.TargetId)
                    .Append(" payloads=")
                    .Append(command.Payload0)
                    .Append(',')
                    .Append(command.Payload1)
                    .Append(',')
                    .Append(command.Payload2);
                AppendLineIfNeeded(builder, i, commands.Count);
            }

            return builder.ToString();
        }

        private static string CreateErrors(IReadOnlyList<RuntimeCommandError> errors)
        {
            if (errors.Count == 0)
            {
                return "none";
            }

            var builder = new StringBuilder();
            for (int i = 0; i < errors.Count; i++)
            {
                RuntimeCommandError error = errors[i];
                builder.Append(error.Code)
                    .Append(" frame=")
                    .Append(error.CommandFrame)
                    .Append(" message=")
                    .Append(error.Message);
                AppendLineIfNeeded(builder, i, errors.Count);
            }

            return builder.ToString();
        }

        private static string FormatValue(StoryValue value)
        {
            switch (value.Kind)
            {
                case StoryValueKind.Bool:
                    return value.Raw != 0L ? "Bool(true)" : "Bool(false)";
                case StoryValueKind.Int32:
                    return "Int32(" + (int)value.Raw + ")";
                case StoryValueKind.Int64:
                    return "Int64(" + value.Raw + ")";
                case StoryValueKind.Fix64:
                    return "Fix64Raw(" + value.Raw + ")";
                case StoryValueKind.StringRef:
                    return "StringRef(" + value.Raw + ")";
                default:
                    return "None";
            }
        }

        private static void AppendLineIfNeeded(StringBuilder builder, int index, int count)
        {
            if (index + 1 < count)
            {
                builder.Append('\n');
            }
        }
    }
}
