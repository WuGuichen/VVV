using Unity.Profiling;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Separates synchronous session startup preparation from simulation and UI binding.</summary>
    internal static class CharacterTestStartupProfiler
    {
        internal static readonly ProfilerMarker TestCurrentChanges = new ProfilerMarker("CharacterTest.Startup.TestCurrentChanges");
        internal static readonly ProfilerMarker StartSession = new ProfilerMarker("CharacterTest.Startup.StartSession");
        internal static readonly ProfilerMarker ValidateSession = new ProfilerMarker("CharacterTest.Startup.ValidateSession");
        internal static readonly ProfilerMarker SaveDraft = new ProfilerMarker("CharacterTest.Startup.SaveDraft");
        internal static readonly ProfilerMarker BuildCandidate = new ProfilerMarker("CharacterTest.Startup.BuildCandidate");
        internal static readonly ProfilerMarker CompileCard = new ProfilerMarker("CharacterTest.Startup.CompileCard");
        internal static readonly ProfilerMarker PrepareInputs = new ProfilerMarker("CharacterTest.Startup.PrepareInputs");
        internal static readonly ProfilerMarker CaptureReplayBaseline = new ProfilerMarker("CharacterTest.Startup.CaptureReplayBaseline");
        internal static readonly ProfilerMarker RebuildSession = new ProfilerMarker("CharacterTest.Startup.RebuildSession");
        internal static readonly ProfilerMarker Resources = new ProfilerMarker("CharacterTest.Startup.Resources");
        internal static readonly ProfilerMarker Physics = new ProfilerMarker("CharacterTest.Startup.Physics");
        internal static readonly ProfilerMarker ConfigureUi = new ProfilerMarker("CharacterTest.Startup.ConfigureUi");
        internal static readonly ProfilerMarker CreateSlice = new ProfilerMarker("CharacterTest.Startup.CreateSlice");
        internal static readonly ProfilerMarker CreateNetworkSession = new ProfilerMarker("CharacterTest.Startup.CreateNetworkSession");
        internal static readonly ProfilerMarker UiConfigure = new ProfilerMarker("CharacterTest.Startup.UiConfigure");
        internal static readonly ProfilerMarker CaptureRecovery = new ProfilerMarker("CharacterTest.Startup.CaptureRecovery");
        internal static readonly ProfilerMarker PrepareTimeline = new ProfilerMarker("CharacterTest.Startup.PrepareTimeline");
        internal static readonly ProfilerMarker PrepareFrameStorage = new ProfilerMarker("CharacterTest.Startup.PrepareFrameStorage");
        internal static readonly ProfilerMarker PrepareNumericIds = new ProfilerMarker("CharacterTest.Startup.PrepareNumericIds");
        internal static readonly ProfilerMarker LoadDraftDirectory = new ProfilerMarker("CharacterTest.Startup.LoadDraftDirectory");
        internal static readonly ProfilerMarker LoadStory = new ProfilerMarker("CharacterTest.Startup.LoadStory");
        internal static readonly ProfilerMarker LoadAudio = new ProfilerMarker("CharacterTest.Startup.LoadAudio");
        internal static readonly ProfilerMarker PlayerDeck = new ProfilerMarker("CharacterTest.Startup.PlayerDeck");
        internal static readonly ProfilerMarker SessionAuthority = new ProfilerMarker("CharacterTest.Startup.SessionAuthority");
        internal static readonly ProfilerMarker ArenaLoadouts = new ProfilerMarker("CharacterTest.Startup.ArenaLoadouts");
        internal static readonly ProfilerMarker DisposeSlice = new ProfilerMarker("CharacterTest.Startup.DisposeSlice");
        internal static readonly ProfilerMarker CharacterRuntime = new ProfilerMarker("CharacterTest.Startup.CharacterRuntime");
        internal static readonly ProfilerMarker LoadActions = new ProfilerMarker("CharacterTest.Startup.LoadActions");
    }
}
