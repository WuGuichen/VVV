using System;

namespace MxFramework.Editor.ProjectileAuthoring
{
    [Serializable]
    public sealed class ProjectileAuthoringWorkspaceView
    {
        public string schemaVersion;
        public string packageId;
        public string sourceRoot;
        public string workspaceHash;
        public int sourceCount;
        public int definitionCount;
        public int requestCount;
        public int presentationBindingCount;
        public ProjectileAuthoringSourceView[] sources = Array.Empty<ProjectileAuthoringSourceView>();
        public ProjectileDefinitionListItem[] definitions = Array.Empty<ProjectileDefinitionListItem>();
        public ProjectileRequestVariantListItem[] requests = Array.Empty<ProjectileRequestVariantListItem>();
        public ProjectilePresentationBindingListItem[] bindings = Array.Empty<ProjectilePresentationBindingListItem>();
        public ProjectileGeneratedOnlyListItem[] generatedOnly = Array.Empty<ProjectileGeneratedOnlyListItem>();
        public ProjectileAuthoringProblemView[] diagnostics = Array.Empty<ProjectileAuthoringProblemView>();
        public string[] knownSocketIds = Array.Empty<string>();
    }

    [Serializable] public sealed class ProjectileAuthoringSourceView { public string stableId; public string relativePath; public string expectedSha256; public string state; }
    [Serializable] public sealed class ProjectileDefinitionListItem { public string stableId; public int definitionId; public string motion; public string impact; public string[] requestIds = Array.Empty<string>(); public string[] sourcePaths = Array.Empty<string>(); public string[] presentationSourcePaths = Array.Empty<string>(); public bool shared; public bool hasPresentation; public ProjectileDefinitionDetailView detail; public string rawJson; }
    [Serializable] public sealed class ProjectileRequestVariantListItem { public string stableId; public string definitionStableId; public string sourcePath; public ProjectileRequestDetailView detail; public string rawJson; }
    [Serializable] public sealed class ProjectilePresentationBindingListItem { public int projectileDefinitionId; public string definitionStableId; public string sourcePath; public bool legacyV1; public ProjectilePresentationBindingView detail; public string rawJson; }
    [Serializable] public sealed class ProjectilePresentationBindingView { public int projectileDefinitionId; public string viewNamePrefix; public ProjectileResourceKeyView prefabResource; public ProjectilePresentationSlotsView slots; public ProjectilePresentationExitPolicyView exitPolicy; }
    [Serializable] public sealed class ProjectilePresentationSlotsView { public ProjectilePresentationSlotView spawn; public ProjectilePresentationSlotView flightBody; public ProjectilePresentationSlotView trail; public ProjectilePresentationSlotView directImpact; public ProjectilePresentationSlotView areaImpact; public ProjectilePresentationSlotView blockedImpact; public ProjectilePresentationSlotView expire; }
    [Serializable] public sealed class ProjectilePresentationSlotView { public ProjectileResourceKeyView resource; public string orientation; public ProjectileVector3View localOffset; public int lifetimeFramesOverride; public int vfxDefinitionId; }
    [Serializable] public sealed class ProjectilePresentationExitPolicyView { public string acceptedImpact; public string blockedImpact; public string expiration; public string externalRemoval; public string runtimeReset; }
    [Serializable] public sealed class ProjectileResourceKeyView { public string id; public string typeId; public string variant; public string packageId; }
    [Serializable] public sealed class ProjectileGeneratedOnlyListItem { public string stableId; public int definitionId; public string status; public bool complete; public string motion; public string impact; public string[] diagnostics = Array.Empty<string>(); }
    [Serializable] public sealed class ProjectileAuthoringProblemView { public string code; public string severity; public string message; public string sourcePath; public string objectPath; public string fieldPath; public string[] relatedStableIds = Array.Empty<string>(); public string suggestedFix; }
    [Serializable] public sealed class ProjectileAuthoringReferenceImpactView { public string stableId; public string[] requestStableIds = Array.Empty<string>(); public string[] consumerPaths = Array.Empty<string>(); public string[] authorizationRequiredPaths = Array.Empty<string>(); public string[] fieldPaths = Array.Empty<string>(); public string[] blockingReasons = Array.Empty<string>(); public bool blocking; public string summary; public string confirmationToken; public string workspaceToken; }
    [Serializable] public sealed class ProjectileAuthoringDirtySourceView { public string relativePath; public string operation; }
    [Serializable] public sealed class ProjectileAuthoringMutationRequestView { public string schemaVersion = "1"; public string kind; public string sourceStableId; public string stableId; public string definitionStableId; public string generatedOnlyStableId; public int definitionId; public bool createRequest = true; public bool copyPresentation = true; public bool confirmDestructive; public bool planOnly; public bool confirmed; public string confirmationToken; public string workspaceToken; public string[] authorizedConsumerPaths = Array.Empty<string>(); public ProjectileAuthoringFieldEditView[] fieldEdits = Array.Empty<ProjectileAuthoringFieldEditView>(); public string presentationSlotKind; public ProjectileResourceKeyView presentationResource; public string presentationOrientation; public ProjectileVector3View presentationLocalOffset; public int presentationLifetimeFramesOverride; public bool synchronizeVfxContractLifetime; public string presentationExitKind; public string presentationExitMode; }
    [Serializable] public sealed class ProjectileAuthoringMutationSequenceView { public string schemaVersion = "1"; public ProjectileAuthoringMutationRequestView[] commands = Array.Empty<ProjectileAuthoringMutationRequestView>(); }
    [Serializable] public sealed class ProjectileAuthoringMutationResultView { public string schemaVersion; public string status; public ProjectileAuthoringReferenceImpactView impact; public ProjectileAuthoringDirtySourceView[] dirtySources = Array.Empty<ProjectileAuthoringDirtySourceView>(); public ProjectileAuthoringProblemView[] diagnostics = Array.Empty<ProjectileAuthoringProblemView>(); public ProjectileAuthoringWorkspaceView projectedWorkspace; }
    [Serializable] public sealed class ProjectileAuthoringApplyResult { public string schemaVersion; public string status; public string[] diagnosticCodes = Array.Empty<string>(); public string playerMirrorFreshness; }

    [Serializable]
    public sealed class ProjectileAuthoringFieldEditView
    {
        public string fieldPath;
        public string valueKind;
        public bool boolValue;
        public int intValue;
        public float floatValue;
        public string stringValue;
        public string[] stringValues = Array.Empty<string>();
        public int[] intValues = Array.Empty<int>();
        public ProjectileVector3View vectorValue;
        public string jsonValue;
    }

    [Serializable] public sealed class ProjectileVector3View { public float x; public float y; public float z; }
    [Serializable] public sealed class ProjectileDefinitionDetailView { public int definitionId; public string stableId; public bool physicalEventsEnabled; public ProjectileMotionView motion; public ProjectileCollisionView collision; public ProjectileImpactView impact; }
    [Serializable] public sealed class ProjectileMotionView { public string kind; public float speedPerFrame; public int lifetimeFrames; public float arcHeight; public float gravityPerFrameSquared; public float maxLaunchAngleDegrees; public float maxLaunchAngleTangent; public float lateralAmplitude; public int lateralCycles; public ProjectileVector3View bezierControlOffsetA; public ProjectileVector3View bezierControlOffsetB; public ProjectileSpeedProfileView speedProfile; public ProjectileHomingView homing; }
    [Serializable] public sealed class ProjectileSpeedProfileView { public string kind; }
    [Serializable] public sealed class ProjectileHomingView { public float turnRatePerFrame; }
    [Serializable] public sealed class ProjectileCollisionView { public float radius; public int layerMask; public bool stopOnFirstAcceptedImpact; public bool stopOnBlockedImpact; public int maxAcceptedImpacts; public string hitSameTargetPolicy; }
    [Serializable] public sealed class ProjectileImpactView { public string presentationProfileId; public string stableEventId; public string policyKind; public float areaRadius; public int areaLayerMask; public bool triggerOnExpire; public string effectTimelineId; public CombatHitEventView hitEvent; public CombatHitEventView areaHitEvent; }
    [Serializable] public sealed class CombatHitEventView { public string[] targetTypes = Array.Empty<string>(); public string environmentHitPolicy; public string stableEventId; public int maxAcceptedHits; public string hitSameTargetPolicy; public CombatHitDamageView damage; public CombatHitBuffView[] onHitBuffs = Array.Empty<CombatHitBuffView>(); }
    [Serializable] public sealed class CombatHitBuffView { public int buffId; }
    [Serializable] public sealed class CombatHitDamageView { public string damageTypeId; public int baseDamage; public int impactPower; public int launchPower; public string[] tags = Array.Empty<string>(); public string analysisCategory; }
    [Serializable] public sealed class ProjectileRequestDetailView { public string stableId; public string projectileStableId; public ProjectileSpawnOriginView origin; public ProjectileAimView aim; }
    [Serializable] public sealed class ProjectileSpawnOriginView { public string kind; public string socketId; public ProjectileVector3View localOffset; public ProjectileVector3View fallbackRootOffset; }
    [Serializable] public sealed class ProjectileAimView { public string kind; public float targetHeightOffset; public string fallbackKind; public float maxRange; }

    public interface IProjectileAuthoringService : IDisposable
    {
        bool IsBusy { get; }
        void LoadWorkspace(string packagePath, Action<ProjectileAuthoringWorkspaceView, string> completed);
        void Validate(string packagePath, Action<ProjectileAuthoringProblemView[], string> completed);
        void Mutate(string packagePath, ProjectileAuthoringMutationRequestView request, Action<ProjectileAuthoringMutationResultView, string, string> completed);
        void MutateSequence(string packagePath, ProjectileAuthoringMutationRequestView[] commands, Action<ProjectileAuthoringMutationResultView, string, string> completed);
        void Apply(string packagePath, string draftPath, Action<ProjectileAuthoringApplyResult, string> completed);
        void Cancel();
    }
}
