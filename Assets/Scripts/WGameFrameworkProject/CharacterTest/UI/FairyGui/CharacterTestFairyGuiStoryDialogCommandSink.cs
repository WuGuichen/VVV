using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Runtime;
using MxFramework.Story.Runtime;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestFairyGuiStoryCommandSink : IMxUiCommandSink
    {
        private GameSlice _slice;
        private readonly Dictionary<string, RuntimeCommand> _acceptedUiCommands =
            new Dictionary<string, RuntimeCommand>(StringComparer.Ordinal);
        private string _activeViewModelSignature = string.Empty;

        public CharacterTestFairyGuiStoryCommandSink(GameSlice slice)
        {
            _slice = slice;
            LastResult = Failed("CharacterTest Story UI command sink has not received a command.");
        }

        public void UpdateSlice(GameSlice slice)
        {
            _slice = slice;
            _acceptedUiCommands.Clear();
        }

        public MxUiCommand LastCommand { get; private set; }
        public RuntimeCommand LastRuntimeCommand { get; private set; }
        public RuntimeCommandValidationResult LastResult { get; private set; }
        public int AcceptedCount { get; private set; }
        public int RejectedCount { get; private set; }
        public int DuplicateIgnoredCount { get; private set; }

        public void SetActiveViewModelSignature(string signature)
        {
            signature = signature ?? string.Empty;
            if (string.Equals(_activeViewModelSignature, signature, StringComparison.Ordinal))
                return;

            _activeViewModelSignature = signature;
            _acceptedUiCommands.Clear();
        }

        public void Enqueue(MxUiCommand command)
        {
            LastCommand = command;

            if (_slice == null)
            {
                Reject("CharacterTest GameSlice is not available.");
                return;
            }

            RuntimeCommand runtimeCommand;
            if (!TryMap(command, _slice.CurrentFrame, out runtimeCommand, out string error))
            {
                Reject(error);
                return;
            }

            string commandKey = CreateCommandKey(command);
            if (_acceptedUiCommands.TryGetValue(commandKey, out RuntimeCommand acceptedCommand))
            {
                LastRuntimeCommand = acceptedCommand;
                LastResult = RuntimeCommandValidationResult.Accepted(acceptedCommand);
                DuplicateIgnoredCount++;
                return;
            }

            LastResult = _slice.StoryModule.CommandBuffer.Enqueue(runtimeCommand);
            if (LastResult.Success)
            {
                LastRuntimeCommand = LastResult.Command;
                AcceptedCount++;
                _acceptedUiCommands[commandKey] = LastResult.Command;
            }
            else
            {
                LastRuntimeCommand = runtimeCommand;
                RejectedCount++;
            }
        }

        public static bool TryMap(
            MxUiCommand command,
            RuntimeFrame frame,
            out RuntimeCommand runtimeCommand,
            out string error)
        {
            switch (command.CommandId)
            {
                case CharacterTestFairyGuiStoryCommandIds.CompletePresentation:
                    if (command.Payload is CharacterTestFairyGuiStoryCompletePresentationPayload complete)
                    {
                        runtimeCommand = StoryRuntimeCommandFactory.CompletePresentation(
                            frame,
                            StoryRuntimeCommandSources.PresentationAdapter,
                            complete.BeatInstanceId,
                            complete.StepId,
                            complete.GraphId,
                            RuntimeTraceIdentity.None);
                        error = string.Empty;
                        return true;
                    }

                    return FailMap("CharacterTest Story continue command requires a complete-presentation payload.", out runtimeCommand, out error);
                case CharacterTestFairyGuiStoryCommandIds.SelectChoice:
                    if (command.Payload is CharacterTestFairyGuiStorySelectChoicePayload choice)
                    {
                        runtimeCommand = StoryRuntimeCommandFactory.SelectChoice(
                            frame,
                            StoryRuntimeCommandSources.UiAdapter,
                            choice.BeatInstanceId,
                            choice.ChoiceId,
                            choice.GraphId,
                            RuntimeTraceIdentity.None);
                        error = string.Empty;
                        return true;
                    }

                    return FailMap("CharacterTest Story choice command requires a select-choice payload.", out runtimeCommand, out error);
                default:
                    return FailMap("CharacterTest Story UI command is not mapped: " + command.CommandId, out runtimeCommand, out error);
            }
        }

        private void Reject(string message)
        {
            LastResult = Failed(message);
            RejectedCount++;
        }

        private string CreateCommandKey(MxUiCommand command)
        {
            var builder = new StringBuilder(128);
            AppendCommandKeyToken(builder, _activeViewModelSignature);
            AppendCommandKeyToken(builder, command.SourceViewId.Value);
            AppendCommandKeyToken(builder, command.CommandId);
            AppendCommandKeyPayload(builder, command.Payload);
            return builder.ToString();
        }

        private static void AppendCommandKeyPayload(StringBuilder builder, object payload)
        {
            if (payload is CharacterTestFairyGuiStoryCompletePresentationPayload complete)
            {
                builder.Append("complete|")
                    .Append(complete.GraphId).Append('|')
                    .Append(complete.BeatInstanceId).Append('|')
                    .Append(complete.StepId).Append('|');
                AppendCommandKeyToken(builder, complete.TraceId);
                return;
            }

            if (payload is CharacterTestFairyGuiStorySelectChoicePayload choice)
            {
                builder.Append("choice|")
                    .Append(choice.GraphId).Append('|')
                    .Append(choice.BeatInstanceId).Append('|')
                    .Append(choice.ChoiceId).Append('|');
                AppendCommandKeyToken(builder, choice.TraceId);
                return;
            }

            builder.Append("payload|");
            AppendCommandKeyToken(builder, payload != null ? payload.ToString() : string.Empty);
        }

        private static void AppendCommandKeyToken(StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            builder.Append(value.Length).Append(':').Append(value).Append('|');
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
                default,
                RuntimeFrame.Zero,
                message));
        }
    }
}
