using System;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind
    {
        Open,
        Execute,
        Close,
        PreviewPlay,
        PreviewPause,
        PreviewStep,
        PreviewScrub,
        PreviewStop,
        Capture,
    }

    internal sealed class CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest
    {
        internal CharacterTestRuntimeAiLabCharacterActionWorkspaceRequestKind Kind;
        internal CharacterTestRuntimeAiLabCharacterActionAuthoringRequest OpenRequest;
        internal CharacterTestRuntimeAiLabCharacterActionWorkspaceCommand Command;
        internal int PreviewFrame;
    }

    internal enum CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind
    {
        SelectAction,
        CreateAction,
        DuplicateAction,
        DeleteAction,
        SelectTimelineItem,
        AddTimelineItem,
        MoveResizeTimelineItem,
        DeleteTimelineItem,
        SetField,
        BeginGesture,
        EndGesture,
        Undo,
        Redo,
        Validate,
        SaveAll,
        AddArrayElement,
        DeleteArrayElement,
    }

    internal sealed class CharacterTestRuntimeAiLabCharacterActionWorkspaceCommand
    {
        internal CharacterTestRuntimeAiLabCharacterActionWorkspaceCommandKind Kind;
        internal long ExpectedRevision;
        internal string Text = string.Empty;
        internal string SecondaryText = string.Empty;
        internal int IntValue;
        internal int SecondaryIntValue;
    }

    internal sealed class CharacterTestRuntimeAiLabCharacterActionWorkspaceActionRow
    {
        public int ActionId;
        public string DisplayName = string.Empty;
        public string LibraryId = string.Empty;
        public string Category = string.Empty;
        public int DurationFrames;
        public bool Selected;
        public bool Dirty;
        public int ProblemCount;
    }

    internal sealed class CharacterTestRuntimeAiLabCharacterActionWorkspaceTimelineItem
    {
        public string ItemId = string.Empty;
        public string TrackKind = string.Empty;
        public string Title = string.Empty;
        public int StartFrame;
        public int EndFrame;
        public bool CanEdit;
        public bool CanResize;
        public bool Selected;
        public uint ColorRgba;
    }

    internal sealed class CharacterTestRuntimeAiLabCharacterActionWorkspaceTrack
    {
        public string Kind = string.Empty;
        public string Label = string.Empty;
        public CharacterTestRuntimeAiLabCharacterActionWorkspaceTimelineItem[] Items =
            Array.Empty<CharacterTestRuntimeAiLabCharacterActionWorkspaceTimelineItem>();
    }

    internal sealed class CharacterTestRuntimeAiLabCharacterActionWorkspaceInspectorField
    {
        public string Path = string.Empty;
        public string Label = string.Empty;
        public string Value = string.Empty;
        public string Kind = string.Empty;
        public bool Editable;
        public string[] Options = Array.Empty<string>();
        public string[] OptionLabels = Array.Empty<string>();
    }

    internal sealed class CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot
    {
        public bool Available;
        public string SessionId = string.Empty;
        public long Revision;
        public string ProfileId = string.Empty;
        public string ActionSetId = string.Empty;
        public string LibraryId = string.Empty;
        public int SelectedActionId;
        public string SelectedTimelineItemId = string.Empty;
        public int DurationFrames;
        public bool ReadOnly;
        public bool Dirty;
        public bool CanUndo;
        public bool CanRedo;
        public bool HasErrors;
        public bool PreviewActive;
        public bool PreviewPlaying;
        public int PreviewFrame;
        public string PreviewSourceDirectory = string.Empty;
        public string Status = string.Empty;
        public int ReturnSelectedAgentIndex;
        public string ReturnStableCardId = string.Empty;
        public string ReturnTimelineId = string.Empty;
        public string ReturnEventId = string.Empty;
        public CharacterTestRuntimeAiLabTimelineMode ReturnTimelineMode;
        public CharacterTestRuntimeAiLabCharacterActionWorkspaceActionRow[] Actions =
            Array.Empty<CharacterTestRuntimeAiLabCharacterActionWorkspaceActionRow>();
        public CharacterTestRuntimeAiLabCharacterActionWorkspaceTrack[] Tracks =
            Array.Empty<CharacterTestRuntimeAiLabCharacterActionWorkspaceTrack>();
        public CharacterTestRuntimeAiLabCharacterActionWorkspaceInspectorField[] InspectorFields =
            Array.Empty<CharacterTestRuntimeAiLabCharacterActionWorkspaceInspectorField>();
        public string[] Problems = Array.Empty<string>();
        public string[] DirtyPaths = Array.Empty<string>();
    }

    internal static class CharacterTestRuntimeAiLabCharacterActionWorkspaceBridge
    {
        internal static Func<CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest,
            CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot> Handler { get; set; }

        internal static bool IsRegistered => Handler != null;

        internal static CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot Execute(
            CharacterTestRuntimeAiLabCharacterActionWorkspaceRequest request)
        {
            return Handler?.Invoke(request)
                ?? new CharacterTestRuntimeAiLabCharacterActionWorkspaceSnapshot
                {
                    Status = "完整 CharacterAction 编辑器只在 Unity Editor 中可用。",
                };
        }
    }
}
