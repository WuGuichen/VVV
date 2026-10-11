using System;
using System.Collections.Generic;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    public readonly struct CharacterTestFairyGuiStoryCompletePresentationPayload
    {
        public CharacterTestFairyGuiStoryCompletePresentationPayload(int graphId, int beatInstanceId, int stepId, string traceId = "")
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

    public readonly struct CharacterTestFairyGuiStorySelectChoicePayload
    {
        public CharacterTestFairyGuiStorySelectChoicePayload(int graphId, int beatInstanceId, int choiceId, string traceId = "")
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

    public readonly struct CharacterTestFairyGuiStoryCommandDescriptor
    {
        public CharacterTestFairyGuiStoryCommandDescriptor(string commandId, string label, bool enabled, object payload)
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

    public readonly struct CharacterTestFairyGuiStoryChoiceViewModel
    {
        public CharacterTestFairyGuiStoryChoiceViewModel(
            int graphId,
            int beatInstanceId,
            int choiceId,
            string text,
            CharacterTestFairyGuiStoryCommandDescriptor command)
        {
            GraphId = graphId;
            BeatInstanceId = beatInstanceId;
            ChoiceId = choiceId;
            Text = text ?? string.Empty;
            Command = command;
        }

        public int GraphId { get; }
        public int BeatInstanceId { get; }
        public int ChoiceId { get; }
        public string Text { get; }
        public CharacterTestFairyGuiStoryCommandDescriptor Command { get; }
        public bool Enabled => Command.Enabled;
    }

    public sealed class CharacterTestFairyGuiStoryDialogViewModel
    {
        public string Title { get; set; } = "Character Story";
        public string Phase { get; set; } = string.Empty;
        public string DialogueText { get; set; } = string.Empty;
        public string ChoiceText { get; set; } = string.Empty;
        public string SignalText { get; set; } = string.Empty;
        public string EventLogText { get; set; } = string.Empty;
        public IReadOnlyList<CharacterTestFairyGuiStoryChoiceViewModel> Choices { get; set; } =
            Array.Empty<CharacterTestFairyGuiStoryChoiceViewModel>();
        public IReadOnlyList<CharacterTestFairyGuiStoryCommandDescriptor> Commands { get; set; } =
            Array.Empty<CharacterTestFairyGuiStoryCommandDescriptor>();
    }

    internal static class CharacterTestFairyGuiStoryDialogViewModelSignature
    {
        public static string Create(CharacterTestFairyGuiStoryDialogViewModel viewModel)
        {
            if (viewModel == null)
                return string.Empty;

            var builder = new StringBuilder(256);
            AppendToken(builder, viewModel.Title);
            AppendToken(builder, viewModel.Phase);
            AppendToken(builder, viewModel.DialogueText);
            AppendToken(builder, viewModel.ChoiceText);
            AppendToken(builder, viewModel.SignalText);
            AppendToken(builder, viewModel.EventLogText);

            IReadOnlyList<CharacterTestFairyGuiStoryCommandDescriptor> commands =
                viewModel.Commands ?? Array.Empty<CharacterTestFairyGuiStoryCommandDescriptor>();
            builder.Append("commands=").Append(commands.Count).Append('|');
            for (int i = 0; i < commands.Count; i++)
            {
                CharacterTestFairyGuiStoryCommandDescriptor command = commands[i];
                AppendToken(builder, command.CommandId);
                AppendToken(builder, command.Label);
                builder.Append(command.Enabled ? '1' : '0').Append('|');
                AppendPayload(builder, command.Payload);
            }

            IReadOnlyList<CharacterTestFairyGuiStoryChoiceViewModel> choices =
                viewModel.Choices ?? Array.Empty<CharacterTestFairyGuiStoryChoiceViewModel>();
            builder.Append("choices=").Append(choices.Count).Append('|');
            for (int i = 0; i < choices.Count; i++)
            {
                CharacterTestFairyGuiStoryChoiceViewModel choice = choices[i];
                builder.Append(choice.GraphId).Append('|')
                    .Append(choice.BeatInstanceId).Append('|')
                    .Append(choice.ChoiceId).Append('|')
                    .Append(choice.Enabled ? '1' : '0').Append('|');
                AppendToken(builder, choice.Text);
                AppendPayload(builder, choice.Command.Payload);
            }

            return builder.ToString();
        }

        private static void AppendPayload(StringBuilder builder, object payload)
        {
            if (payload is CharacterTestFairyGuiStoryCompletePresentationPayload complete)
            {
                builder.Append("complete|")
                    .Append(complete.GraphId).Append('|')
                    .Append(complete.BeatInstanceId).Append('|')
                    .Append(complete.StepId).Append('|');
                AppendToken(builder, complete.TraceId);
                return;
            }

            if (payload is CharacterTestFairyGuiStorySelectChoicePayload choice)
            {
                builder.Append("choice|")
                    .Append(choice.GraphId).Append('|')
                    .Append(choice.BeatInstanceId).Append('|')
                    .Append(choice.ChoiceId).Append('|');
                AppendToken(builder, choice.TraceId);
                return;
            }

            builder.Append("payload|");
            AppendToken(builder, payload != null ? payload.ToString() : string.Empty);
        }

        private static void AppendToken(StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            builder.Append(value.Length).Append(':').Append(value).Append('|');
        }
    }
}
