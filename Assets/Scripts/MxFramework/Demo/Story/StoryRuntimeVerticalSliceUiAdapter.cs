using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using MxFramework.Story.Runtime;
using MxFramework.UI;

namespace MxFramework.Demo.Story
{
    public static class StoryRuntimeVerticalSliceUiCommandIds
    {
        public const string CompletePresentation = "story.dialog.continue";
        public const string SelectChoice = "story.dialog.selectChoice";
    }

    public readonly struct StoryRuntimeVerticalSliceCompletePresentationPayload
    {
        public StoryRuntimeVerticalSliceCompletePresentationPayload(int graphId, int beatInstanceId, int stepId, string traceId = "")
        {
            GraphId = graphId;
            BeatInstanceId = beatInstanceId;
            StepId = stepId;
            TraceId = traceId ?? string.Empty;
        }

        public int GraphId { get; }
        public int BeatInstanceId { get; }
        public int StepId { get; }
        public string TraceId { get; }
    }

    public readonly struct StoryRuntimeVerticalSliceSelectChoicePayload
    {
        public StoryRuntimeVerticalSliceSelectChoicePayload(int graphId, int beatInstanceId, int choiceId, string traceId = "")
        {
            GraphId = graphId;
            BeatInstanceId = beatInstanceId;
            ChoiceId = choiceId;
            TraceId = traceId ?? string.Empty;
        }

        public int GraphId { get; }
        public int BeatInstanceId { get; }
        public int ChoiceId { get; }
        public string TraceId { get; }
    }

    public readonly struct StoryRuntimeVerticalSliceUiCommandDescriptor
    {
        public StoryRuntimeVerticalSliceUiCommandDescriptor(string commandId, string label, bool enabled, object payload)
        {
            CommandId = commandId ?? string.Empty;
            Label = label ?? string.Empty;
            Enabled = enabled;
            Payload = payload;
        }

        public string CommandId { get; }
        public string Label { get; }
        public bool Enabled { get; }
        public object Payload { get; }
    }

    public sealed class StoryRuntimeVerticalSliceFairyGuiViewModel
    {
        public string Title { get; set; }
        public MxUiLocalizedTextRequest TitleText { get; set; }
        public string Phase { get; set; }
        public string DialogueText { get; set; }
        public MxUiLocalizedTextRequest DialogueLocalizedText { get; set; }
        public string ChoiceText { get; set; }
        public MxUiLocalizedTextRequest ChoiceLocalizedText { get; set; }
        public string SignalText { get; set; }
        public string EventLogText { get; set; }
        public IReadOnlyList<StoryRuntimeVerticalSliceUiCommandDescriptor> Commands { get; set; } =
            Array.Empty<StoryRuntimeVerticalSliceUiCommandDescriptor>();
    }

    public static class StoryRuntimeVerticalSliceFairyGuiViewModelBuilder
    {
        public static StoryRuntimeVerticalSliceFairyGuiViewModel Build(
            StoryRuntimeVerticalSliceSnapshot snapshot,
            int graphId = StoryRuntimeVerticalSliceDemo.GraphId)
        {
            string dialogue = NonEmpty(snapshot.DialogueText, "Story is waiting for a trigger.");
            string choice = NonEmpty(snapshot.ChoiceText, "No choice available.");

            return new StoryRuntimeVerticalSliceFairyGuiViewModel
            {
                Title = "Story",
                TitleText = new MxUiLocalizedTextRequest(new MxUiTextKey("ui.story.dialog.title"), "Story"),
                Phase = ResolvePhase(snapshot),
                DialogueText = dialogue,
                DialogueLocalizedText = new MxUiLocalizedTextRequest(
                    new MxUiTextKey("story.text." + (snapshot.WaitingStepId > 0 ? snapshot.WaitingStepId.ToString() : "dialogue")),
                    dialogue),
                ChoiceText = choice,
                ChoiceLocalizedText = new MxUiLocalizedTextRequest(
                    new MxUiTextKey("story.choice." + (snapshot.ChoiceId > 0 ? snapshot.ChoiceId.ToString() : "none")),
                    choice),
                SignalText = "Signal " + snapshot.SignalValue + " / commands " + snapshot.GameplayCommandCount,
                EventLogText = FormatEventLog(snapshot.EventLog),
                Commands = BuildCommands(snapshot, graphId)
            };
        }

        private static IReadOnlyList<StoryRuntimeVerticalSliceUiCommandDescriptor> BuildCommands(
            StoryRuntimeVerticalSliceSnapshot snapshot,
            int graphId)
        {
            return new[]
            {
                new StoryRuntimeVerticalSliceUiCommandDescriptor(
                    StoryRuntimeVerticalSliceUiCommandIds.CompletePresentation,
                    "Continue",
                    snapshot.IsWaitingForPresentation,
                    new StoryRuntimeVerticalSliceCompletePresentationPayload(
                        graphId,
                        snapshot.WaitingBeatInstanceId,
                        snapshot.WaitingStepId,
                        "story.fairygui.presentation.complete")),
                new StoryRuntimeVerticalSliceUiCommandDescriptor(
                    StoryRuntimeVerticalSliceUiCommandIds.SelectChoice,
                    NonEmpty(snapshot.ChoiceText, "Select"),
                    snapshot.HasChoice,
                    new StoryRuntimeVerticalSliceSelectChoicePayload(
                        graphId,
                        snapshot.ChoiceBeatInstanceId,
                        snapshot.ChoiceId,
                        "story.fairygui.choice.select"))
            };
        }

        private static string ResolvePhase(StoryRuntimeVerticalSliceSnapshot snapshot)
        {
            if (snapshot.GraphStatus == MxFramework.Story.StoryGraphRuntimeStatus.Completed)
                return "Completed";
            if (snapshot.HasChoice)
                return "Choice available";
            if (snapshot.IsWaitingForPresentation)
                return "Presentation waiting";
            return "Ready";
        }

        private static string FormatEventLog(IReadOnlyList<string> eventLog)
        {
            if (eventLog == null || eventLog.Count == 0)
                return "No story events.";

            int start = Math.Max(0, eventLog.Count - 2);
            if (start == eventLog.Count - 1)
                return eventLog[start] ?? string.Empty;

            return (eventLog[start] ?? string.Empty) + "\n" + (eventLog[start + 1] ?? string.Empty);
        }

        private static string NonEmpty(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }

    public interface IStoryRuntimeVerticalSliceUiCommandTarget
    {
        RuntimeFrame CurrentCommandFrame { get; }
        RuntimeCommandValidationResult EnqueueStoryCommand(RuntimeCommand command);
    }

    public sealed class StoryRuntimeVerticalSliceDemoUiCommandTarget : IStoryRuntimeVerticalSliceUiCommandTarget
    {
        private readonly StoryRuntimeVerticalSliceDemo _demo;

        public StoryRuntimeVerticalSliceDemoUiCommandTarget(StoryRuntimeVerticalSliceDemo demo)
        {
            _demo = demo ?? throw new ArgumentNullException(nameof(demo));
        }

        public RuntimeFrame CurrentCommandFrame => _demo.CurrentCommandFrame;

        public RuntimeCommandValidationResult EnqueueStoryCommand(RuntimeCommand command)
        {
            return _demo.StoryModule.CommandBuffer.Enqueue(command);
        }
    }

    public sealed class StoryRuntimeVerticalSliceUiCommandSink : IMxUiCommandSink
    {
        private readonly IStoryRuntimeVerticalSliceUiCommandTarget _target;

        public StoryRuntimeVerticalSliceUiCommandSink(IStoryRuntimeVerticalSliceUiCommandTarget target)
        {
            _target = target;
            LastResult = Failed("Story UI command sink has not received a command.");
        }

        public MxUiCommand LastCommand { get; private set; }
        public RuntimeCommand LastRuntimeCommand { get; private set; }
        public RuntimeCommandValidationResult LastResult { get; private set; }
        public int AcceptedCount { get; private set; }
        public int RejectedCount { get; private set; }

        public void Enqueue(MxUiCommand command)
        {
            LastCommand = command;

            if (_target == null)
            {
                Reject("Story UI command target is not available.");
                return;
            }

            RuntimeCommand runtimeCommand;
            if (!TryMap(command, _target.CurrentCommandFrame, out runtimeCommand, out string error))
            {
                Reject(error);
                return;
            }

            LastRuntimeCommand = runtimeCommand;
            LastResult = _target.EnqueueStoryCommand(runtimeCommand);
            if (LastResult.Success)
                AcceptedCount++;
            else
                RejectedCount++;
        }

        public static bool TryMap(MxUiCommand command, RuntimeFrame frame, out RuntimeCommand runtimeCommand, out string error)
        {
            switch (command.CommandId)
            {
                case StoryRuntimeVerticalSliceUiCommandIds.CompletePresentation:
                    if (command.Payload is StoryRuntimeVerticalSliceCompletePresentationPayload complete)
                    {
                        runtimeCommand = StoryRuntimeCommandFactory.CompletePresentation(
                            frame,
                            StoryRuntimeCommandSources.PresentationAdapter,
                            complete.BeatInstanceId,
                            complete.StepId,
                            complete.GraphId,
                            complete.TraceId);
                        error = string.Empty;
                        return true;
                    }

                    return FailMap("Story continue command requires a complete-presentation payload.", out runtimeCommand, out error);
                case StoryRuntimeVerticalSliceUiCommandIds.SelectChoice:
                    if (command.Payload is StoryRuntimeVerticalSliceSelectChoicePayload choice)
                    {
                        runtimeCommand = StoryRuntimeCommandFactory.SelectChoice(
                            frame,
                            StoryRuntimeCommandSources.UiAdapter,
                            choice.BeatInstanceId,
                            choice.ChoiceId,
                            choice.GraphId,
                            choice.TraceId);
                        error = string.Empty;
                        return true;
                    }

                    return FailMap("Story choice command requires a select-choice payload.", out runtimeCommand, out error);
                default:
                    return FailMap("Story UI command is not mapped: " + command.CommandId, out runtimeCommand, out error);
            }
        }

        private void Reject(string message)
        {
            LastResult = Failed(message);
            RejectedCount++;
        }

        private static bool FailMap(string message, out RuntimeCommand runtimeCommand, out string error)
        {
            runtimeCommand = default;
            error = message;
            return false;
        }

        private static RuntimeCommandValidationResult Failed(string message)
        {
            return RuntimeCommandValidationResult.Failed(new RuntimeCommandError(
                RuntimeCommandErrorCode.InvalidPayload,
                default(RuntimeCommand),
                RuntimeFrame.Zero,
                message));
        }
    }
}
