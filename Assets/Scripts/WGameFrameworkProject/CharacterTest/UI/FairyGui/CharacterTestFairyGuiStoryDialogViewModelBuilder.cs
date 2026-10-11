using System;
using System.Collections.Generic;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using MxFramework.UI;

namespace MxFramework.Game.CharacterTest
{
    public static class CharacterTestFairyGuiStoryDialogViewModelBuilder
    {
        public static bool TryBuild(GameSlice slice, out CharacterTestFairyGuiStoryDialogViewModel viewModel)
        {
            return TryBuild(slice, null, out viewModel);
        }

        public static bool TryBuild(
            GameSlice slice,
            CharacterTestStoryContent content,
            out CharacterTestFairyGuiStoryDialogViewModel viewModel,
            IMxUiTextProvider textProvider = null)
        {
            if (slice == null)
            {
                viewModel = null;
                return false;
            }

            return TryBuild(
                slice.StorySnapshot,
                slice.StoryDirector,
                content,
                out viewModel,
                textProvider);
        }

        public static bool TryBuild(
            StoryDirectorSnapshot snapshot,
            IStoryChoiceSnapshotReader choiceReader,
            CharacterTestStoryContent content,
            out CharacterTestFairyGuiStoryDialogViewModel viewModel,
            IMxUiTextProvider textProvider = null)
        {
            viewModel = null;
            if (snapshot == null || snapshot.ActiveBeatInstances == null)
                return false;

            StoryBeatInstanceSnapshot candidate = null;
            bool waitingForPresentation = false;
            for (int i = 0; i < snapshot.ActiveBeatInstances.Count; i++)
            {
                StoryBeatInstanceSnapshot beat = snapshot.ActiveBeatInstances[i];
                if (beat == null)
                    continue;

                if (beat.PendingPresentationStepId > 0)
                {
                    candidate = beat;
                    waitingForPresentation = true;
                    break;
                }

                if (candidate == null && beat.AwaitingChoiceSetId > 0)
                    candidate = beat;
            }

            if (candidate == null)
                return false;

            if (waitingForPresentation)
            {
                string dialogue = ResolveStepText(content, candidate.PendingPresentationStepId);
                viewModel = new CharacterTestFairyGuiStoryDialogViewModel
                {
                    Title = L(textProvider, "ui.character_test.story.title", "Character Story"),
                    Phase = L(textProvider, "ui.character_test.story.phase.presentation_waiting", "Presentation waiting"),
                    DialogueText = dialogue,
                    ChoiceText = L(textProvider, "ui.character_test.story.continue_story", "Continue story."),
                    SignalText = FormatSignal(candidate),
                    EventLogText = FormatEvent(candidate),
                    Choices = Array.Empty<CharacterTestFairyGuiStoryChoiceViewModel>(),
                    Commands = new[]
                    {
                        new CharacterTestFairyGuiStoryCommandDescriptor(
                            CharacterTestFairyGuiStoryCommandIds.CompletePresentation,
                            L(textProvider, "ui.character_test.story.continue", "Continue"),
                            true,
                            new CharacterTestFairyGuiStoryCompletePresentationPayload(
                                candidate.GraphId,
                                candidate.BeatInstanceId,
                                candidate.PendingPresentationStepId,
                                "character-test.fairygui.presentation.complete")),
                        new CharacterTestFairyGuiStoryCommandDescriptor(
                            CharacterTestFairyGuiStoryCommandIds.SelectChoice,
                            L(textProvider, "ui.character_test.story.select", "Select"),
                            false,
                            null)
                    }
                };
                return true;
            }

            IReadOnlyList<CharacterTestFairyGuiStoryChoiceViewModel> choices =
                BuildChoices(candidate, choiceReader, content);
            CharacterTestFairyGuiStoryChoiceViewModel primaryChoice = ResolvePrimaryChoice(choices);
            string choiceText = primaryChoice.ChoiceId > 0
                ? primaryChoice.Text
                : L(textProvider, "ui.character_test.story.choose_response", "Choose a response.");
            viewModel = new CharacterTestFairyGuiStoryDialogViewModel
            {
                Title = L(textProvider, "ui.character_test.story.title", "Character Story"),
                Phase = L(textProvider, "ui.character_test.story.phase.choice_available", "Choice available"),
                DialogueText = L(textProvider, "ui.character_test.story.choice_waiting", "A story choice is waiting."),
                ChoiceText = choiceText,
                SignalText = FormatSignal(candidate),
                EventLogText = FormatEvent(candidate),
                Choices = choices,
                Commands = new[]
                {
                    new CharacterTestFairyGuiStoryCommandDescriptor(
                        CharacterTestFairyGuiStoryCommandIds.CompletePresentation,
                        L(textProvider, "ui.character_test.story.continue", "Continue"),
                        false,
                        null),
                    primaryChoice.ChoiceId > 0
                        ? primaryChoice.Command
                        : new CharacterTestFairyGuiStoryCommandDescriptor(
                            CharacterTestFairyGuiStoryCommandIds.SelectChoice,
                            L(textProvider, "ui.character_test.story.select", "Select"),
                            false,
                            null)
                }
            };
            return true;
        }

        private static IReadOnlyList<CharacterTestFairyGuiStoryChoiceViewModel> BuildChoices(
            StoryBeatInstanceSnapshot beat,
            IStoryChoiceSnapshotReader choiceReader,
            CharacterTestStoryContent content)
        {
            if (choiceReader == null || beat.AwaitingChoiceSetId <= 0)
                return Array.Empty<CharacterTestFairyGuiStoryChoiceViewModel>();

            var initial = new StoryChoiceView[4];
            int required = choiceReader.GetChoices(beat.BeatInstanceId, beat.AwaitingChoiceSetId, initial);
            if (required <= 0)
                return Array.Empty<CharacterTestFairyGuiStoryChoiceViewModel>();

            StoryChoiceView[] views = initial;
            if (required > initial.Length)
            {
                views = new StoryChoiceView[required];
                required = choiceReader.GetChoices(beat.BeatInstanceId, beat.AwaitingChoiceSetId, views);
            }

            int count = Math.Min(required, views.Length);
            var choices = new CharacterTestFairyGuiStoryChoiceViewModel[count];
            for (int i = 0; i < count; i++)
            {
                StoryChoiceView choice = views[i];
                string text = ResolveChoiceText(content, choice);
                var payload = new CharacterTestFairyGuiStorySelectChoicePayload(
                    beat.GraphId,
                    beat.BeatInstanceId,
                    choice.ChoiceId,
                    "character-test.fairygui.choice.select");
                var command = new CharacterTestFairyGuiStoryCommandDescriptor(
                    CharacterTestFairyGuiStoryCommandIds.SelectChoice,
                    text,
                    choice.Enabled,
                    payload);
                choices[i] = new CharacterTestFairyGuiStoryChoiceViewModel(
                    beat.GraphId,
                    beat.BeatInstanceId,
                    choice.ChoiceId,
                    text,
                    command);
            }

            return choices;
        }

        private static CharacterTestFairyGuiStoryChoiceViewModel ResolvePrimaryChoice(
            IReadOnlyList<CharacterTestFairyGuiStoryChoiceViewModel> choices)
        {
            if (choices == null || choices.Count == 0)
                return default;

            for (int i = 0; i < choices.Count; i++)
            {
                if (choices[i].Enabled)
                    return choices[i];
            }

            return choices[0];
        }

        private static string ResolveStepText(CharacterTestStoryContent content, int stepId)
        {
            if (content != null && content.TryResolveStepText(stepId, out string text))
                return text;

            return "Story step " + stepId + ".";
        }

        private static string ResolveChoiceText(CharacterTestStoryContent content, StoryChoiceView choice)
        {
            if (content != null && content.TryResolveText(choice.LabelTextKey, out string text))
                return text;

            return "Choice " + choice.ChoiceId;
        }

        private static string FormatSignal(StoryBeatInstanceSnapshot beat)
        {
            return "Beat " + beat.BeatId + " / step " + beat.CurrentStepIndex;
        }

        private static string FormatEvent(StoryBeatInstanceSnapshot beat)
        {
            return "graph=" + beat.GraphId + " beatInstance=" + beat.BeatInstanceId;
        }

        private static string L(IMxUiTextProvider provider, string key, string fallback)
        {
            return CharacterTestUiText.Resolve(provider, key, fallback);
        }
    }
}
