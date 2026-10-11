using System.Collections.Generic;
using MxFramework.Audio;
using MxFramework.Input;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestSliceOptions
    {
        // Internal candidate construction: no bootstrap, Host start, views or global time writes.
        internal bool DetachedTacticalRestore;
        internal System.Func<MxFramework.Gameplay.AbilityRuntimeModule> FreshAbilityRuntime;
        internal System.Func<CharacterTestCharacterActionRuntime> FreshActionRuntime;
        public IRuntimeLogger Logger { get; set; }
        public CharacterTestStoryContent StoryContent { get; set; }
        public bool EnableStoryBootstrap { get; set; } = true;
        public GlobalResourceRuntimeServices Resources { get; set; }
        // Called only after composition has registered cleanup for incoming resources/audio.
        internal System.Action IncomingOwnershipAccepted { get; set; }
        public string BaseResourcePreloadGroupId { get; set; } = string.Empty;
        public CharacterTestCameraMode InitialCameraMode { get; set; } =
            CharacterTestCameraMode.ThirdPersonControlled;
        public CharacterTestCharacterActionConfigSourceOptions ActionConfigOptions { get; set; }
        public CharacterTestCharacterActionCatalog ActionCatalog { get; set; }
        public CharacterTestCharacterPhysicsRuntime PhysicsRuntime { get; set; }

        internal CharacterTestNavigationSessionBuildInput? NavigationBuildInput { get; set; }
        internal CharacterTestNavigationSceneInputResult NavigationInputResult { get; set; }
        public bool CreateCharacterViews { get; set; } = true;
        public IInputProvider InputProvider { get; set; }
        public bool EnableLocalInputCommands { get; set; } = true;
        public bool SmoothNetworkSnapshotViews { get; set; }
        public bool EnableCharacterSimulation { get; set; } = true;
        public bool EnableTraceFileWrite { get; set; }
        public IAudioService AudioService { get; set; }
        public ICharacterTestAudioCueCatalog AudioCueCatalog { get; set; }
        public bool DisposeAudioService { get; set; }
        public int SimulationTickRate { get; set; } = CharacterTestDeterministicUnits.DefaultTickRate;
        public CharacterTestProceduralHitFeedbackOptions ProceduralHitFeedbackOptions { get; set; }
        public CharacterTestAiBehaviorProfile AiBehaviorProfile { get; set; }
        public CharacterTestRuntimeAiPlannerProfile RuntimeAiPlannerProfile { get; set; }
        public CharacterTestRuntimeAiPlannerValidationReport RuntimeAiPlannerProfileReport { get; set; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerProfileBinding> RuntimeAiPlannerProfileBindings { get; set; }
        public CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime RuntimeAiBehaviorLoadouts { get; set; }
        public IReadOnlyList<CharacterTestMovableNaturalObjectSpawnSpec> MovableNaturalObjectSpawnSpecs { get; set; }
        public IReadOnlyList<CharacterTestCharacterSpawnSpec> StartupCharacterSpawnSpecs { get; set; }
        public bool SuppressMissingStartupSpawnWarning { get; set; }
        public int PresentationAllyTeamId { get; set; }
        public CharacterTestGameplayTriggerSceneCache GameplayTriggerSceneCache { get; set; }
        internal TacticalCard.CharacterTestTacticalStageAuthoringDocument TacticalStage { get; set; }
        internal TacticalCard.CharacterTestTacticalStageAuthoringDocument MixedPlayerTacticalStage { get; set; }
        internal TacticalCard.TacticalDeployment TacticalDeployment { get; set; }
        internal ulong TacticalStageRunId { get; set; } = 1;
        // Runs after modules exist but before GameSlice can publish its first Story event.
        public System.Func<GameSlice, System.IDisposable> SubscribeBeforeStoryBootstrap { get; set; }

        public static CharacterTestSliceOptions FromLegacyArguments(
            IRuntimeLogger logger,
            CharacterTestStoryContent storyContent,
            GlobalResourceRuntimeServices resources,
            string baseResourcePreloadGroupId,
            CharacterTestCameraMode initialCameraMode,
            CharacterTestCharacterActionConfigSourceOptions actionConfigOptions,
            CharacterTestCharacterPhysicsRuntime physicsRuntime,
            bool createCharacterViews,
            IInputProvider inputProvider,
            bool enableLocalInputCommands,
            bool smoothNetworkSnapshotViews,
            bool enableCharacterSimulation,
            IAudioService audioService,
            ICharacterTestAudioCueCatalog audioCueCatalog,
            bool disposeAudioService,
            int simulationTickRate,
            CharacterTestProceduralHitFeedbackOptions proceduralHitFeedbackOptions,
            CharacterTestAiBehaviorProfile aiBehaviorProfile,
            IReadOnlyList<CharacterTestMovableNaturalObjectSpawnSpec> movableNaturalObjectSpawnSpecs = null,
            IReadOnlyList<CharacterTestCharacterSpawnSpec> startupCharacterSpawnSpecs = null,
            CharacterTestRuntimeAiPlannerProfile runtimeAiPlannerProfile = null,
            CharacterTestRuntimeAiPlannerValidationReport runtimeAiPlannerProfileReport = null)
        {
            return new CharacterTestSliceOptions
            {
                Logger = logger,
                StoryContent = storyContent,
                Resources = resources,
                BaseResourcePreloadGroupId = baseResourcePreloadGroupId ?? string.Empty,
                InitialCameraMode = initialCameraMode,
                ActionConfigOptions = actionConfigOptions,
                PhysicsRuntime = physicsRuntime,
                CreateCharacterViews = createCharacterViews,
                InputProvider = inputProvider,
                EnableLocalInputCommands = enableLocalInputCommands,
                SmoothNetworkSnapshotViews = smoothNetworkSnapshotViews,
                EnableCharacterSimulation = enableCharacterSimulation,
                AudioService = audioService,
                AudioCueCatalog = audioCueCatalog,
                DisposeAudioService = disposeAudioService,
                SimulationTickRate = simulationTickRate,
                ProceduralHitFeedbackOptions = proceduralHitFeedbackOptions,
                AiBehaviorProfile = aiBehaviorProfile,
                RuntimeAiPlannerProfile = runtimeAiPlannerProfile,
                RuntimeAiPlannerProfileReport = runtimeAiPlannerProfileReport,
                RuntimeAiPlannerProfileBindings = null,
                MovableNaturalObjectSpawnSpecs = movableNaturalObjectSpawnSpecs,
                StartupCharacterSpawnSpecs = startupCharacterSpawnSpecs,
                PresentationAllyTeamId = 0,
                GameplayTriggerSceneCache = null
            };
        }
    }
}
