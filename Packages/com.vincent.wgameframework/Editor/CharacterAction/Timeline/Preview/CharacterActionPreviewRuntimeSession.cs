using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Projectiles;
using MxFramework.Combat.Projectiles.Unity;
using MxFramework.Core.Math;
using MxFramework.Editor;
using MxFramework.Editor.CharacterAction;
using MxFramework.Editor.CharacterAction.Timeline;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using MxFramework.Vfx;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MxFramework.Editor.CharacterAction.Timeline.Preview
{
    internal sealed partial class CharacterActionPreviewRuntimeSession :
        IDisposable,
        ICharacterActionMotionRequestSink,
        ICharacterActionCombatRequestSink,
        ICharacterActionGameplayRequestSink,
        ICharacterActionAnimationRequestSink,
        ICharacterActionAudioCueRequestSink,
        ICharacterActionVfxRequestSink,
        ICharacterActionCameraRequestSink,
        ICharacterActionUiFeedbackRequestSink
    {
        private const string GeneratedPackagesRoot = "Assets/MxFrameworkGenerated/CharacterPackages";
        private const string PreviewCatalogIdPrefix = "character-action-preview.";
        private const int DefaultPreviewDurationFrames = 8;
        private const float PreviewGravity = -30f;
        private static readonly int PreviewUpwardGravityScaleMilli =
            CharacterActionVerticalRootMotionGravity.ResolveUpwardScaleMilli(PreviewGravity);
        private const float PreviewGroundY = 0f;
        private const float PreviewMaxFallSpeed = -50f;
        private const string GeneratedVfxDefinitionsPath = "Assets/MxFrameworkGenerated/Vfx/vfx_definitions.json";
        private const string GeneratedProjectileDefinitionsPath =
            "Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_definitions.json";
        private const string GeneratedProjectileSpawnProfilesPath =
            "Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_spawn_profiles.json";
        private const string GeneratedProjectilePresentationBindingsPath =
            "Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_presentation_bindings.json";
        private const string SampleResourceCatalogPath =
            "Assets/Config/MxFramework/ResourceCatalogs/mxframework_samples_resource_catalog.json";
        private const string PreviewCharacterNamePrefix = "[CharacterAction Preview] ";
        private const string LegacyPreviewObjectNamePrefix = "[CharacterAction Preview]";
        private const string LegacyPreviewVfxNamePrefix = "[CharacterAction Preview VFX] ";
        private const string LegacyPreviewProjectileNamePrefix = "[CharacterAction Preview Projectile] ";
        private const string PreviewVfxNamePrefix = "[角色动作预览 特效] ";
        private const string PreviewProjectileNamePrefix = "[角色动作预览 投射物] ";
        private const float PreviewCasterBodyRadius = 0.34f;
        private const float PreviewCasterBodyHeight = 1.86f;
        private const float PreviewCasterBodyCenterY = PreviewCasterBodyHeight * 0.5f;
        private const int PreviewTrackTraceKind = 90801;
        private const int PreviewPresentationTraceKind = 90802;
        private const int PreviewRebuildTraceKind = 90803;
        private const int PreviewDirectTraceKind = 90804;
        private const int PreviewIntentTraceKind = 90805;
        private readonly Dictionary<int, PreparedPreviewAnimation> _preparedPreviewAnimations =
            new Dictionary<int, PreparedPreviewAnimation>(InitialAnimationClipCapacity);
        private const int InitialFrameRequestCapacity = 8;
        private const int InitialPreviewItemCapacity = 16;
        private const int InitialAnimationClipCapacity = 32;
        private const int InitialDiagnosticCacheCapacity = 128;
        private const int ProjectilePathSegmentCount = 16;
        private const HideFlags PreviewHideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        private static readonly Vector3[] RadiusHandleDirections =
        {
            Vector3.right,
            Vector3.left,
            Vector3.forward,
            Vector3.back,
        };
        private static readonly string[] PreviewObjectNamePrefixes =
        {
            PreviewCharacterNamePrefix,
            PreviewVfxNamePrefix,
            PreviewProjectileNamePrefix,
            LegacyPreviewVfxNamePrefix,
            LegacyPreviewProjectileNamePrefix,
            LegacyPreviewObjectNamePrefix,
        };

        private readonly PreviewRuntimeLogger _logger = new PreviewRuntimeLogger();
        private readonly ICharacterActionPreviewObjectHost _objectHost;
        private readonly CharacterActionPreviewSurfaceMode _surfaceMode;
        private readonly bool _sceneViewHandlesRegistered;
        private readonly bool _captureDebugSnapshots;
        private readonly CharacterActionTrackAdapter _trackAdapter;
        private readonly CharacterActionPresentationTrackAdapter _presentationAdapter;
        private readonly CharacterActionDispatchBuffer _trackDispatchBuffer;
        private readonly CharacterActionDispatchBuffer _presentationDispatchBuffer;
        private readonly List<CharacterActionMotionRequest> _lastMotionRequests =
            new List<CharacterActionMotionRequest>(InitialFrameRequestCapacity);
        private readonly List<CharacterActionCombatRequest> _lastCombatRequests =
            new List<CharacterActionCombatRequest>(InitialFrameRequestCapacity);
        private readonly List<CharacterActionGameplayRequest> _lastGameplayRequests =
            new List<CharacterActionGameplayRequest>(InitialFrameRequestCapacity);
        private readonly List<CharacterActionAnimationRequest> _lastAnimationRequests =
            new List<CharacterActionAnimationRequest>(InitialFrameRequestCapacity);
        private readonly List<CharacterActionAudioCueRequest> _lastAudioCueRequests =
            new List<CharacterActionAudioCueRequest>(InitialFrameRequestCapacity);
        private readonly List<CharacterActionVfxRequest> _lastVfxRequests =
            new List<CharacterActionVfxRequest>(InitialFrameRequestCapacity);
        private readonly List<CharacterActionCameraRequest> _lastCameraRequests =
            new List<CharacterActionCameraRequest>(InitialFrameRequestCapacity);
        private readonly List<CharacterActionUiFeedbackRequest> _lastUiFeedbackRequests =
            new List<CharacterActionUiFeedbackRequest>(InitialFrameRequestCapacity);
        private readonly List<CharacterActionTrackDispatchEvent> _lastDebugDispatches =
            new List<CharacterActionTrackDispatchEvent>(InitialFrameRequestCapacity);
        private readonly Dictionary<int, Transform> _previewSockets = new Dictionary<int, Transform>();
        private readonly List<PreviewActiveHitTrace> _activeHitTraces =
            new List<PreviewActiveHitTrace>(InitialPreviewItemCapacity);
        private readonly List<CharacterActionPreviewWeaponTraceSnapshot> _weaponTraceSnapshots =
            new List<CharacterActionPreviewWeaponTraceSnapshot>(InitialPreviewItemCapacity);
        private readonly List<CharacterActionPreviewHitVolumeSnapshot> _hitVolumeSnapshots =
            new List<CharacterActionPreviewHitVolumeSnapshot>(InitialPreviewItemCapacity);
        private readonly List<CharacterActionPreviewWeaponLocatorPoseSnapshot> _weaponLocatorPoseSnapshots =
            new List<CharacterActionPreviewWeaponLocatorPoseSnapshot>(InitialPreviewItemCapacity);
        private readonly List<PreviewActiveVfx> _activeVfxPreviews =
            new List<PreviewActiveVfx>(InitialPreviewItemCapacity);
        private readonly List<PreviewActiveProjectile> _activeProjectilePreviews =
            new List<PreviewActiveProjectile>(InitialPreviewItemCapacity);
        private readonly List<CharacterActionPreviewVfxSnapshot> _vfxPreviewSnapshots =
            new List<CharacterActionPreviewVfxSnapshot>(InitialPreviewItemCapacity);
        private readonly List<CharacterActionPreviewProjectileSnapshot> _projectilePreviewSnapshots =
            new List<CharacterActionPreviewProjectileSnapshot>(InitialPreviewItemCapacity);
        private readonly List<int> _removePreviewIndices = new List<int>(InitialPreviewItemCapacity);
        private readonly List<WeaponTraceSegment> _sceneWeaponTraceSegments =
            new List<WeaponTraceSegment>(InitialFrameRequestCapacity);
        private readonly Vector3[] _sceneLinePoints = new Vector3[2];
        private readonly Vector3[] _projectilePathPoints = new Vector3[ProjectilePathSegmentCount + 1];
        private readonly Dictionary<int, CachedWeaponLocatorPose> _weaponLocatorPoseCache =
            new Dictionary<int, CachedWeaponLocatorPose>(InitialPreviewItemCapacity);
        private readonly Dictionary<PreviewDispatchTraceKey, string> _dispatchTraceTextCache =
            new Dictionary<PreviewDispatchTraceKey, string>(InitialDiagnosticCacheCapacity);
        private readonly Dictionary<PreviewDiagnosticLineKey, string> _diagnosticLineCache =
            new Dictionary<PreviewDiagnosticLineKey, string>(InitialDiagnosticCacheCapacity);
        private readonly Dictionary<PreviewHitTraceDiagnosticKey, string> _hitTraceDiagnosticLineCache =
            new Dictionary<PreviewHitTraceDiagnosticKey, string>(InitialDiagnosticCacheCapacity);
        private Dictionary<string, VfxDefinition> _vfxDefinitionsByStableName =
            new Dictionary<string, VfxDefinition>(StringComparer.Ordinal);
        private ResourceManager _resourceManager;
        private MemoryResourceProvider _memoryProvider;
        private readonly List<AnimationClip> _previewAnimationClips =
            new List<AnimationClip>(InitialAnimationClipCapacity);
        private readonly Dictionary<string, AnimationClip> _previewAnimationClipsByKey =
            new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
        private ResourceHandle<GameObject> _characterHandle;
        private VfxDefinitionRegistry _previewVfxDefinitions = new VfxDefinitionRegistry();
        private CombatProjectileDefinitionRegistry _previewProjectileDefinitions = new CombatProjectileDefinitionRegistry();
        private CombatProjectileSpawnProfileRegistry _previewProjectileSpawnProfiles =
            new CombatProjectileSpawnProfileRegistry();
        private CombatProjectileUnityPresentationRegistry _previewProjectilePresentation =
            new CombatProjectileUnityPresentationRegistry();
        private bool _hasProjectileCatalogOverride;
        private GameObject _previewInstance;
        private Animator _previewAnimator;
        private GameObject _previewAnimationSamplingRoot;
        private CharacterDefaultEquipmentRuntimeBinder _defaultEquipmentBinder;
        private CharacterRuntimeLocomotionBlendController _locomotion;
        private CharacterBoneRuntimeSync _boneSync;
        private PlayableGraph _fallbackAnimationGraph;
        private AnimationClipPlayable _fallbackAnimationPlayable;
        private AnimationClip _fallbackAnimationClip;
        private bool _fallbackAnimatorStateCaptured;
        private bool _fallbackAnimatorWasEnabled;
        private CharacterActionExecutionGateway _gateway;
        private readonly CharacterActionRunnerEventBuffer _executionBuffer =
            new CharacterActionRunnerEventBuffer(24);
        private CharacterActionConfig _previewAction;
        private PreviewBinding _currentPreviewBinding;
        private CharacterWeaponTraceBakeCatalog _previewTraceBakeCatalog;
        private CharacterActionDebugSnapshot _lastDebugSnapshot;
        private string _previewBindingKey = string.Empty;
        private CharacterActionId _activeActionId = CharacterActionId.None;
        private string _activeActionRevision = string.Empty;
        private long _activeActionRevisionToken;
        private bool _usesActionRevisionToken;
        private long _validatedRevisionToken;
        private CharacterActionId _validatedSelectedActionId = CharacterActionId.None;
        private CharacterActionSetConfig _validatedActionSet;
        private CharacterActionConfig _validatedAction;
        private CharacterActionConfigRegistry _validatedRegistry;
        private CharacterActionSetJsonDto _validatedActionSetDto;
        private CharacterActionJsonDto _validatedSelectedActionDto;
        private CharacterActionAuthoringExportResult _validatedExternalContext;
        private bool _hasValidatedRevisionContext;
        private int _contextValidationCount;
        private CharacterActionSetConfig _previewBoundActionSet;
        private int _previewAssetRevision;
        private int _previewBoundAssetRevision = -1;
        private CharacterActionConfigRegistry _previewBoundRegistry;
        private int _previewHierarchyCacheBuildCount;
        private int _weaponLocatorCacheBuildCount;
        private int _dispatchTraceTextBuildCount;
        private int _diagnosticLineBuildCount;
        private int _hitTraceDiagnosticLineBuildCount;
        private int _hitTraceDiagnosticLogCount;
        private Vector3 _previewVelocity;
        private Vector3 _pendingImpulse;
        private CharacterMovementMode _previewMovementMode = CharacterMovementMode.Idle;
        private bool _previewMovementLocked;
        private float _previewMovementControlScale = 1f;
        private float _previewRotationControlScale = 1f;
        private bool _previewGrounded = true;
        private bool _previewAuthoringResourcesReady;
        private int _lastFrame = -1;
        private long _nextPlanId = 1L;
        private long _nextPreviewVisualId = 1L;
        private bool _disposed;

        public CharacterActionPreviewRuntimeSession()
            : this(null, captureDebugSnapshots: true)
        {
        }

        /// <summary>
        /// Creates an editor preview session on the supplied object host.
        /// </summary>
        /// <remarks>
        /// Allocation category: AllocByDesign. The host is used only for preview object lifetime and repaint
        /// routing. Passing null preserves the legacy SceneView preview behavior.
        /// </remarks>
        public CharacterActionPreviewRuntimeSession(ICharacterActionPreviewObjectHost objectHost)
            : this(objectHost, captureDebugSnapshots: false)
        {
        }

        public CharacterActionPreviewRuntimeSession(
            ICharacterActionPreviewObjectHost objectHost,
            bool captureDebugSnapshots)
        {
            _objectHost = objectHost;
            _surfaceMode = objectHost?.SurfaceMode ?? CharacterActionPreviewSurfaceMode.SceneView;
            _captureDebugSnapshots = captureDebugSnapshots;
            _trackAdapter = new CharacterActionTrackAdapter(this, this, this);
            _presentationAdapter = new CharacterActionPresentationTrackAdapter(this, this, this, this, this);
            _trackDispatchBuffer = new CharacterActionDispatchBuffer(8);
            _presentationDispatchBuffer = new CharacterActionDispatchBuffer(8);
            EditorApplication.projectChanged += OnPreviewAssetsChanged;
            if (_surfaceMode == CharacterActionPreviewSurfaceMode.SceneView)
            {
                SceneView.duringSceneGui += OnSceneViewDuringSceneGui;
                _sceneViewHandlesRegistered = true;
            }
        }

        public string DiagnosticsText { get; private set; } = string.Empty;
        public CharacterActionDebugSnapshot LastDebugSnapshot => _lastDebugSnapshot;
        public GameObject PreviewInstance => _previewInstance;
        public int LastFrameMotionRequestCount => _lastMotionRequests.Count;
        public int LastFrameCombatRequestCount => _lastCombatRequests.Count;
        public int LastFrameGameplayRequestCount => _lastGameplayRequests.Count;
        public int LastFrameAnimationRequestCount => _lastAnimationRequests.Count;
        public int LastFrameAudioCueRequestCount => _lastAudioCueRequests.Count;
        public int LastFrameVfxRequestCount => _lastVfxRequests.Count;
        public int LastFrameCameraRequestCount => _lastCameraRequests.Count;
        public int LastFrameUiFeedbackRequestCount => _lastUiFeedbackRequests.Count;
        public int LastFrameDebugDispatchCount => _lastDebugDispatches.Count;
        public int ActiveVfxPreviewCount => _vfxPreviewSnapshots.Count;
        public int ActiveProjectilePreviewCount => _projectilePreviewSnapshots.Count;
        public IReadOnlyList<CharacterActionPreviewVfxSnapshot> VfxPreviewSnapshots => _vfxPreviewSnapshots;
        public IReadOnlyList<CharacterActionPreviewProjectileSnapshot> ProjectilePreviewSnapshots => _projectilePreviewSnapshots;
        public int ActiveHitWindowCount => _activeHitTraces.Count;
        public int ActiveHitTraceCount => _activeHitTraces.Count;
        public int DrawnWeaponTraceCount => _weaponTraceSnapshots.Count;
        public int DrawnHitVolumeCount => _hitVolumeSnapshots.Count;
        public IReadOnlyList<CharacterActionPreviewWeaponTraceSnapshot> WeaponTraceSnapshots => _weaponTraceSnapshots;
        public IReadOnlyList<CharacterActionPreviewHitVolumeSnapshot> HitVolumeSnapshots => _hitVolumeSnapshots;
        public IReadOnlyList<CharacterActionPreviewWeaponLocatorPoseSnapshot> WeaponLocatorPoseSnapshots => _weaponLocatorPoseSnapshots;
        public Vector3 PreviewVelocity => _previewVelocity;
        public CharacterMovementMode PreviewMovementMode => _previewMovementMode;
        public bool PreviewMovementLocked => _previewMovementLocked;
        public float PreviewMovementControlScale => _previewMovementControlScale;
        public float PreviewRotationControlScale => _previewRotationControlScale;
        public bool PreviewGrounded => _previewGrounded;
        public CharacterActionPreviewSurfaceMode SurfaceMode => _surfaceMode;
        public bool UsesSceneViewHandles => _sceneViewHandlesRegistered;
        public int ContextValidationCount => _contextValidationCount;
        public int PreviewHierarchyCacheBuildCount => _previewHierarchyCacheBuildCount;
        public int WeaponLocatorCacheBuildCount => _weaponLocatorCacheBuildCount;
        public int CachedWeaponLocatorCount => _weaponLocatorPoseCache.Count;
        public int DispatchTraceTextBuildCount => _dispatchTraceTextBuildCount;
        public int DiagnosticLineBuildCount => _diagnosticLineBuildCount;
        public int HitTraceDiagnosticLineBuildCount => _hitTraceDiagnosticLineBuildCount;
        public int HitTraceDiagnosticLogCount => _hitTraceDiagnosticLogCount;
        public int CachedHitTraceDiagnosticCount => _hitTraceDiagnosticLineCache.Count;
        public Animator CachedPreviewAnimator => _previewAnimator;
        public CharacterDefaultEquipmentRuntimeBinder CachedDefaultEquipmentBinder => _defaultEquipmentBinder;
        internal Vector3[] SceneLinePointBufferForTests => _sceneLinePoints;
        internal Vector3[] ProjectilePathPointBufferForTests => _projectilePathPoints;

        internal void SetWeaponLocatorPoseSnapshotForTests(
            CharacterActionPreviewWeaponLocatorPoseSnapshot snapshot)
        {
            _weaponLocatorPoseSnapshots.Clear();
            _weaponLocatorPoseSnapshots.Add(snapshot);
            RebuildWeaponLocatorPoseCache();
        }

        internal void ClearPreviewTraceBakeCatalogForTests()
        {
            _previewTraceBakeCatalog = null;
        }

        public static int CleanupStalePreviewObjects()
        {
            int destroyed = 0;
            GameObject[] objects = UnityEngine.Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (!IsStalePreviewObject(candidate))
                    continue;

                DestroyPreviewGameObject(candidate);
                destroyed++;
            }

            if (destroyed > 0)
                SceneView.RepaintAll();

            return destroyed;
        }

        public void SetProjectileCatalogOverride(
            CombatProjectileDefinitionRegistry definitions,
            CombatProjectileSpawnProfileRegistry spawnProfiles,
            CombatProjectileUnityPresentationRegistry presentation)
        {
            _previewProjectileDefinitions = definitions ?? new CombatProjectileDefinitionRegistry();
            _previewProjectileSpawnProfiles = spawnProfiles ?? new CombatProjectileSpawnProfileRegistry();
            _previewProjectilePresentation = presentation ?? new CombatProjectileUnityPresentationRegistry();
            _hasProjectileCatalogOverride = true;
        }

        public void ClearProjectileCatalogOverride()
        {
            if (!_hasProjectileCatalogOverride)
                return;

            _hasProjectileCatalogOverride = false;
            _previewAuthoringResourcesReady = false;
            _previewProjectileDefinitions = new CombatProjectileDefinitionRegistry();
            _previewProjectileSpawnProfiles = new CombatProjectileSpawnProfileRegistry();
            _previewProjectilePresentation = new CombatProjectileUnityPresentationRegistry();
        }

        public void PreviewFrame(CharacterActionAuthoringModel model, int targetFrame)
        {
            PreviewFrameInternal(
                model,
                targetFrame,
                usesRevisionToken: false,
                revisionToken: 0L,
                externalContext: null);
        }

        /// <summary>
        /// Previews a frame using a caller-owned document revision token instead of serializing the action
        /// model to detect changes on every editor frame.
        /// </summary>
        /// <remarks>
        /// Allocation category: NoAlloc in the added editor orchestration after preview initialization,
        /// excluding replay work triggered by a changed token and allocations owned by Unity animation sampling.
        /// </remarks>
        public void PreviewFrame(CharacterActionAuthoringModel model, int targetFrame, long revisionToken)
        {
            PreviewFrameInternal(
                model,
                targetFrame,
                usesRevisionToken: true,
                revisionToken,
                externalContext: null);
        }

        /// <summary>
        /// Previews a frame against a caller-cached validation result for the complete authoring document graph.
        /// </summary>
        /// <remarks>
        /// Allocation category: NoAlloc while the revision and external context are unchanged. The caller owns
        /// rebuilding <paramref name="externalContext"/> when its document revision changes.
        /// </remarks>
        internal void PreviewFrame(
            CharacterActionAuthoringModel model,
            int targetFrame,
            long revisionToken,
            CharacterActionAuthoringExportResult externalContext)
        {
            PreviewFrameInternal(
                model,
                targetFrame,
                usesRevisionToken: true,
                revisionToken,
                externalContext);
        }

        private void PreviewFrameInternal(
            CharacterActionAuthoringModel model,
            int targetFrame,
            bool usesRevisionToken,
            long revisionToken,
            CharacterActionAuthoringExportResult externalContext)
        {
            if (_disposed)
                return;

            // Caller-owned revisions make repeated UI refreshes of a paused frame
            // idempotent. Frame zero remains a replay boundary for explicit Restart.
            if (!EditorApplication.isPlayingOrWillChangePlaymode
                && usesRevisionToken && _usesActionRevisionToken
                && _hasValidatedRevisionContext && model?.SelectedAction != null
                && _gateway != null && _previewInstance != null
                && _previewBoundAssetRevision == _previewAssetRevision
                && targetFrame > 0 && targetFrame == _lastFrame
                && _activeActionRevisionToken == revisionToken
                && _validatedRevisionToken == revisionToken
                && _activeActionId == new CharacterActionId(model.SelectedAction.ActionId)
                && ReferenceEquals(_validatedActionSetDto, model.ActionSet)
                && ReferenceEquals(_validatedSelectedActionDto, model.SelectedAction)
                && ReferenceEquals(_validatedExternalContext, externalContext))
            {
                return;
            }

            _logger.Clear();
            DiagnosticsText = string.Empty;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                DisposePreviewInstance();
                SetDiagnostics("运行时预览在进入或运行 Play Mode 时会被禁用。");
                return;
            }

            if (!TryBuildActionContext(
                    model,
                    usesRevisionToken,
                    revisionToken,
                    externalContext,
                    out CharacterActionSetConfig actionSet,
                    out CharacterActionConfig action,
                    out CharacterActionConfigRegistry registry))
            {
                DisposeRunner();
                return;
            }

            if (!EnsurePreviewInstance(actionSet, registry))
            {
                DisposeRunner();
                return;
            }

            string revision = usesRevisionToken ? string.Empty : CreateActionRevision(model);
            bool actionChanged = _activeActionId != action.ActionId
                || usesRevisionToken != _usesActionRevisionToken
                || (usesRevisionToken
                    ? _activeActionRevisionToken != revisionToken
                    : !string.Equals(_activeActionRevision, revision, StringComparison.Ordinal));
            if (actionChanged)
            {
                ResetRevisionCaches();
                PrepareHotBufferCapacity(action);
            }

            int clampedFrame = Mathf.Clamp(targetFrame, 0, Math.Max(0, ResolveDurationFrames(action) - 1));
            bool canStep = !actionChanged
                && _gateway != null
                && _lastFrame >= 0
                && clampedFrame == _lastFrame + 1;

            if (canStep)
            {
                StepRunner(clampedFrame);
            }
            else
            {
                StartAndReplay(
                    actionSet,
                    action,
                    registry,
                    revision,
                    clampedFrame,
                    actionChanged
                        ? CharacterActionLifecycleReason.ReplayRebuild
                        : CharacterActionLifecycleReason.PreviewSeek);
                _usesActionRevisionToken = usesRevisionToken;
                _activeActionRevisionToken = revisionToken;
            }

            FlushDiagnostics();
            RepaintPreviewViews();
        }

        public void RegeneratePreview(bool refreshAssetDatabase)
        {
            if (_disposed)
                return;

            if (refreshAssetDatabase)
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            DisposeRunner();
            DisposePreviewInstance();
            InvalidateValidatedContext();
            RepaintPreviewViews();
        }

        /// <summary>
        /// Drops action presentation instances and cached action data while preserving the
        /// loaded character, preview render scene and camera. Editor-only, LowFreqAlloc.
        /// </summary>
        public void InvalidatePresentationBindings()
        {
            if (_disposed)
                return;

            DisposeRunner();
            InvalidateValidatedContext();
            RepaintPreviewViews();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            EditorApplication.projectChanged -= OnPreviewAssetsChanged;
            if (_sceneViewHandlesRegistered)
                SceneView.duringSceneGui -= OnSceneViewDuringSceneGui;
            DisposeRunner();
            DisposePreviewInstance();
        }

        private bool TryBuildActionContext(
            CharacterActionAuthoringModel model,
            bool usesRevisionToken,
            long revisionToken,
            CharacterActionAuthoringExportResult externalContext,
            out CharacterActionSetConfig actionSet,
            out CharacterActionConfig action,
            out CharacterActionConfigRegistry registry)
        {
            actionSet = null;
            action = null;
            registry = null;

            if (model == null || model.SelectedAction == null)
            {
                SetDiagnostics("运行时预览需要先选择一个动作。");
                return false;
            }

            CharacterActionId selectedActionId = new CharacterActionId(model.SelectedAction.ActionId);
            if (usesRevisionToken
                && _hasValidatedRevisionContext
                && _validatedRevisionToken == revisionToken
                && ReferenceEquals(_validatedActionSetDto, model.ActionSet)
                && ReferenceEquals(_validatedSelectedActionDto, model.SelectedAction)
                && ReferenceEquals(_validatedExternalContext, externalContext)
                && _validatedSelectedActionId == selectedActionId)
            {
                actionSet = _validatedActionSet;
                action = _validatedAction;
                registry = _validatedRegistry;
                return actionSet != null && action != null && registry != null;
            }

            _contextValidationCount++;
            CharacterActionAuthoringExportResult export = externalContext
                ?? CharacterActionAuthoringJsonService.Validate(
                    model,
                    model.ReferenceContext,
                    buildWorkstationSnapshots: false);

            if (export?.ImportResult == null || !export.ImportResult.Success || export.Registry == null)
            {
                SetDiagnostics("运行时预览导入动作失败。\n" + (export?.DiagnosticsText ?? string.Empty));
                return false;
            }

            registry = export.Registry;
            string selectedActionSetId = model.ActionSet?.StableId ?? string.Empty;
            if (!string.IsNullOrEmpty(selectedActionSetId))
                registry.TryGetActionSet(selectedActionSetId, out actionSet);
            else
                actionSet = registry.ActionSets.Length > 0 ? registry.ActionSets[0] : null;
            if (actionSet == null)
            {
                SetDiagnostics("运行时预览在导入后的注册表中找不到当前 ActionSet：" + selectedActionSetId);
                return false;
            }

            if (!registry.TryGetAction(selectedActionId, out action) || action == null)
            {
                SetDiagnostics("运行时预览找不到当前选中动作：" + selectedActionId);
                return false;
            }

            if (usesRevisionToken)
            {
                _validatedRevisionToken = revisionToken;
                _validatedSelectedActionId = selectedActionId;
                _validatedActionSet = actionSet;
                _validatedAction = action;
                _validatedRegistry = registry;
                _validatedActionSetDto = model.ActionSet;
                _validatedSelectedActionDto = model.SelectedAction;
                _validatedExternalContext = externalContext;
                _hasValidatedRevisionContext = true;
            }

            return true;
        }

        private void OnPreviewAssetsChanged()
        {
            unchecked { _previewAssetRevision++; }
        }

        private bool EnsurePreviewInstance(
            CharacterActionSetConfig actionSet,
            CharacterActionConfigRegistry registry)
        {
            if (_previewInstance != null && _previewBoundActionSet != null
                && _previewBoundAssetRevision == _previewAssetRevision
                && string.Equals(_previewBoundActionSet.CharacterStableId, actionSet.CharacterStableId, StringComparison.Ordinal)
                && string.Equals(_previewBoundActionSet.EquipmentStateStableId, actionSet.EquipmentStateStableId, StringComparison.Ordinal))
            {
                if (!ReferenceEquals(_previewBoundRegistry, registry))
                {
                    _previewTraceBakeCatalog = BuildPreviewTraceBakeCatalog(_currentPreviewBinding.Package, registry);
                    _previewBoundRegistry = registry;
                }
                _previewBoundActionSet = actionSet;
                return true;
            }

            if (!TryResolvePreviewBinding(actionSet, out PreviewBinding binding))
                return false;

            string key = BuildPreviewBindingKey(binding);
            if (_previewInstance != null && string.Equals(_previewBindingKey, key, StringComparison.Ordinal))
            {
                _currentPreviewBinding = binding;
                _previewTraceBakeCatalog = BuildPreviewTraceBakeCatalog(binding.Package, registry);
                UpdateWeaponLocatorPoseSnapshots(binding);
                RebuildWeaponLocatorPoseCache();
                EnsurePreviewAuthoringResources();
                _previewBoundActionSet = actionSet;
                _previewBoundAssetRevision = _previewAssetRevision;
                _previewBoundRegistry = registry;
                return true;
            }

            DisposePreviewInstance();
            _currentPreviewBinding = binding;
            _previewTraceBakeCatalog = BuildPreviewTraceBakeCatalog(binding.Package, registry);
            UpdateWeaponLocatorPoseSnapshots(binding);
            _previewBindingKey = key;

            _memoryProvider = new MemoryResourceProvider();
            _previewAnimationClips.Clear();
            _preparedPreviewAnimations.Clear();
            _previewAnimationClipsByKey.Clear();
            _resourceManager = new ResourceManager();
            _resourceManager.RegisterProvider(new ResourcesProvider());
            _resourceManager.RegisterProvider(new EditorAssetDatabaseResourceProvider());
            _resourceManager.RegisterProvider(_memoryProvider);

            var entries = new List<ResourceCatalogEntry>();
            AddPrefabCatalogEntries(binding.Package, entries);
            AddRuntimePublicEquipmentCatalogEntries(binding.Package, entries);
            AddAnimationCatalogEntries(binding.Package, entries);
            _resourceManager.AddCatalog(new ResourceCatalog(
                PreviewCatalogIdPrefix + binding.Package.PackageId,
                binding.Package.PackageId,
                entries));
            AddGlobalResourceEditorCatalog();
            EnsurePreviewAuthoringResources();

            ResourceKey characterKey = new ResourceKey(
                GetCharacterPrefabResourceId(binding.Package),
                ResourceTypeIds.GameObject,
                "default",
                binding.Package.PackageId);
            ResourceLoadResult<ResourceHandle<GameObject>> loaded = _resourceManager.Load<GameObject>(characterKey);
            if (!loaded.Success || loaded.Value == null || loaded.Value.Value == null)
            {
                SetDiagnostics("运行时预览加载角色 Prefab 失败。key="
                    + characterKey
                    + " error="
                    + loaded.Error.Message);
                return false;
            }

            _characterHandle = loaded.Value;
            _previewInstance = InstantiatePreviewObject(_characterHandle.Value, Vector3.zero, Quaternion.identity);
            _previewInstance.name = PreviewCharacterNamePrefix + binding.Character.StableId;
            _previewInstance.hideFlags = PreviewHideFlags;

            CharacterRuntimeSpawnResult spawnResult = CharacterRuntimeSpawnResolver.Resolve(binding.Package, default);
            if (spawnResult == null || !spawnResult.IsSuccess)
            {
                SetDiagnostics("运行时预览装配失败：Spawn resolve failed. " + FormatSpawnIssues(spawnResult));
                DisposePreviewInstance();
                return false;
            }

            CharacterRuntimePrefabCompositionResult composition = CharacterRuntimePrefabComposer.Compose(
                _previewInstance,
                new CharacterRuntimePrefabCompositionOptions(binding.Package, spawnResult)
                {
                    ResourceManager = _resourceManager,
                    InstantiateDefaultWeapons = false,
                    InstallInputMotionController = false,
                    EnableInputMotion = false,
                    UseExternalRuntimeTick = true,
                    AnimationClips = _previewAnimationClips.ToArray()
                });
            if (!composition.Success)
            {
                SetDiagnostics("运行时预览装配失败：" + string.Join("; ", composition.Issues));
                DisposePreviewInstance();
                return false;
            }

            CachePreviewHierarchy(_previewInstance);
            PreparePreviewSockets(binding.Package, _previewInstance.transform);
            ConfigureDefaultEquipment(_previewInstance, binding);
            ConfigureControlOwnership(_previewInstance);
            ConfigureLocomotion(_previewInstance, binding.Package);
            _boneSync = composition.BoneSync;
            ApplyRuntimeBoneBindings();
            RebuildWeaponLocatorPoseCache();
            _previewBoundActionSet = actionSet;
            _previewBoundAssetRevision = _previewAssetRevision;
            _previewBoundRegistry = registry;
            _logger.Info(
                "CharacterActionPreview",
                "预览已加载。角色=" + binding.Character.StableId
                + "，装配=" + (binding.Loadout != null ? binding.Loadout.StableId : string.Empty)
                + "，状态=" + (binding.ActiveState != null ? binding.ActiveState.StableId : string.Empty));
            return true;
        }

        private static string BuildPreviewBindingKey(PreviewBinding binding)
        {
            CharacterUnityImportRuntimeReport report = binding.Package != null
                ? binding.Package.ImportReport
                : null;
            return (binding.Package != null ? binding.Package.PackageId : string.Empty)
                + "|"
                + (binding.Character != null ? binding.Character.StableId : string.Empty)
                + "|"
                + (binding.Loadout != null ? binding.Loadout.StableId : string.Empty)
                + "|source="
                + (report != null ? report.SourcePackageHash : string.Empty)
                + "|generated="
                + (report != null ? report.GeneratedConfigHash : string.Empty)
                + "|geometry="
                + (report != null ? report.GeometryBindingHash : string.Empty)
                + "|mapping="
                + (report != null ? report.ResourceMappingHash : string.Empty)
                + "|writePlan="
                + (report != null ? report.WritePlanHash : string.Empty);
        }

        private void AddPrefabCatalogEntries(CharacterImportedPackage package, List<ResourceCatalogEntry> entries)
        {
            string root = NormalizeProjectPath(package.RootPath);
            var ids = new HashSet<string>(StringComparer.Ordinal)
            {
                GetCharacterPrefabResourceId(package)
            };
            entries.Add(CharacterRuntimePrefabResourceCatalog.CreateCharacterPrefabEntry(
                package,
                root,
                CharacterRuntimePrefabResourceCatalog.GetGeneratedCharacterPrefabPath(root, package.PackageId),
                new[] { "CharacterActionPreview", "CharacterSpawn" }));
            CharacterRuntimePrefabResourceCatalog.AddWeaponPrefabEntries(
                package,
                root,
                entries,
                ids,
                new[] { "CharacterActionPreview", "CharacterEquipment" });
        }

        private void AddRuntimePublicEquipmentCatalogEntries(CharacterImportedPackage package, List<ResourceCatalogEntry> entries)
        {
            ResourceCatalog runtimeCatalog = package?.RuntimeResourceCatalog;
            if (runtimeCatalog == null || entries == null)
                return;

            for (int i = 0; i < runtimeCatalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = runtimeCatalog.Entries[i];
                if (!IsPreviewPublicEquipmentResource(entry))
                    continue;

                string assetPath = ResolveEditorAssetPath(entry);
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    _logger.Warning(
                        "CharacterActionPreview.Resources",
                        "Public weapon preview asset is missing: " + entry.Id + " path=" + assetPath);
                    continue;
                }

                assetPath = NormalizeProjectPath(assetPath);
                Dictionary<string, string> providerData = entry.ProviderData != null
                    ? new Dictionary<string, string>(entry.ProviderData, StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal);
                providerData[EditorAssetDatabaseResourceProvider.AssetPathProviderDataKey] = assetPath;
                entries.Add(new ResourceCatalogEntry(
                    entry.Id,
                    ResourceTypeIds.GameObject,
                    EditorAssetDatabaseResourceProvider.Id,
                    assetPath,
                    GetDefaultEquipmentResourceVariant(entry),
                    string.IsNullOrWhiteSpace(entry.PackageId) ? "common" : entry.PackageId,
                    entry.Dependencies,
                    entry.Labels,
                    entry.Hash,
                    entry.Size,
                    entry.AllowOverride,
                    providerData));
            }
        }

        private void AddAnimationCatalogEntries(CharacterImportedPackage package, List<ResourceCatalogEntry> entries)
        {
            if (!CharacterRuntimeAnimationResources.TryCreateBinding(
                    package,
                    out CharacterRuntimeAnimationBinding binding,
                    out _))
            {
                return;
            }
            var emitted = new HashSet<string>(StringComparer.Ordinal);

            foreach (ResourceCatalogEntry entry in CharacterRuntimeAnimationResources.CreateCatalogEntries(
                         binding,
                         _memoryProvider.ProviderId,
                         useRuntimeResourceKeyAsAddress: true,
                         labels: new[] { "CharacterActionPreview", "CharacterAnimation" }))
            {
                if (!emitted.Add(entry.Id))
                    continue;
                if (!TryRegisterAnimationClip(entry))
                    continue;
                entries.Add(entry);
            }

            ResourceCatalogEntry[] runtimeEntries =
                CharacterActionRuntimeAnimationResourceDiscovery.DiscoverRuntimeAnimationClipEntries(
                    _memoryProvider.ProviderId,
                    new[] { "CharacterActionPreview", "CharacterAnimation", "ResourceManagerRuntime" });
            for (int i = 0; i < runtimeEntries.Length; i++)
            {
                ResourceCatalogEntry entry = runtimeEntries[i];
                if (entry == null || !emitted.Add(entry.Id))
                    continue;
                if (!TryRegisterAnimationClip(entry))
                    continue;
                entries.Add(entry);
            }
        }

        private void EnsurePreviewAuthoringResources()
        {
            if (_previewAuthoringResourcesReady || _resourceManager == null)
                return;

            _previewAuthoringResourcesReady = true;
            LoadPreviewVfxDefinitions();
            if (!_hasProjectileCatalogOverride)
            {
                LoadPreviewProjectileDefinitions();
                LoadPreviewProjectileSpawnProfiles();
                LoadPreviewProjectilePresentation();
            }
            AddPreviewResourceCatalog(SampleResourceCatalogPath);
        }


        private void LoadPreviewProjectileDefinitions()
        {
            _previewProjectileDefinitions = new CombatProjectileDefinitionRegistry();
            if (!File.Exists(GeneratedProjectileDefinitionsPath))
            {
                _logger.Warning(
                    "CharacterActionPreview.Resources",
                    "未找到生成的投射物定义：" + GeneratedProjectileDefinitionsPath);
                return;
            }

            try
            {
                _previewProjectileDefinitions =
                    CombatProjectileConfigJsonImporter.ImportFile(GeneratedProjectileDefinitionsPath);
            }
            catch (Exception ex)
            {
                _logger.Warning(
                    "CharacterActionPreview.Resources",
                    "生成的投射物定义加载失败：" + ex.Message);
            }
        }

        private void LoadPreviewProjectileSpawnProfiles()
        {
            _previewProjectileSpawnProfiles = new CombatProjectileSpawnProfileRegistry();
            if (!File.Exists(GeneratedProjectileSpawnProfilesPath))
            {
                _logger.Warning(
                    "CharacterActionPreview.Resources",
                    "未找到生成的投射物发射配置：" + GeneratedProjectileSpawnProfilesPath);
                return;
            }

            try
            {
                _previewProjectileSpawnProfiles =
                    CombatProjectileSpawnProfileConfigJsonImporter.ImportFile(GeneratedProjectileSpawnProfilesPath);
            }
            catch (Exception ex)
            {
                _logger.Warning(
                    "CharacterActionPreview.Resources",
                    "生成的投射物发射配置加载失败：" + ex.Message);
            }
        }

        private void LoadPreviewProjectilePresentation()
        {
            _previewProjectilePresentation = new CombatProjectileUnityPresentationRegistry();
            if (!File.Exists(GeneratedProjectilePresentationBindingsPath))
            {
                _logger.Warning(
                    "CharacterActionPreview.Resources",
                    "未找到生成的投射物表现绑定："
                    + GeneratedProjectilePresentationBindingsPath);
                return;
            }

            try
            {
                _previewProjectilePresentation =
                    CombatProjectileUnityPresentationConfigJsonImporter.ImportFile(GeneratedProjectilePresentationBindingsPath);
            }
            catch (Exception ex)
            {
                _logger.Warning(
                    "CharacterActionPreview.Resources",
                    "生成的投射物表现绑定加载失败：" + ex.Message);
            }
        }

        private void AddPreviewResourceCatalog(string path)
        {
            ResourceCatalog catalog = TryLoadEditorResourceCatalog(path);
            if (catalog == null || _resourceManager == null)
                return;

            var entries = new List<ResourceCatalogEntry>();
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (IsPreviewPresentationResource(entry))
                    entries.Add(entry);
            }

            if (entries.Count == 0)
                return;

            try
            {
                _resourceManager.AddCatalog(new ResourceCatalog(
                    catalog.CatalogId + ".characterActionPreview",
                    catalog.PackageId,
                    entries));
            }
            catch (Exception ex)
            {
                _logger.Warning(
                    "CharacterActionPreview.Resources",
                    "预览资源目录添加失败：" + path + " " + ex.Message);
            }
        }

        private void AddGlobalResourceEditorCatalog()
        {
            if (_resourceManager == null)
                return;

            try
            {
                GlobalPlayerResourceBuildProfileBuilder.GlobalResourceBuildPlan plan =
                    GlobalPlayerResourceBuildProfileBuilder.CreateBuildPlan(GlobalPlayerResourceBuildProfileBuilder.ProfilePath);
                if (plan == null)
                {
                    _logger.Warning(
                        "CharacterActionPreview.Resources",
                        "Global resource profile could not be loaded; public default weapons may not preview.");
                    return;
                }
                if (plan.HasErrors)
                {
                    _logger.Warning(
                        "CharacterActionPreview.Resources",
                        "Global resource profile has errors; character action preview will still try usable public weapon entries.");
                }

                ResourceCatalog catalog = GlobalPlayerResourceBuildProfileBuilder.CreateEditorAssetDatabaseCatalog(plan);
                if (catalog == null || catalog.Entries.Count == 0)
                    return;

                var entries = new List<ResourceCatalogEntry>();
                for (int i = 0; i < catalog.Entries.Count; i++)
                {
                    ResourceCatalogEntry entry = catalog.Entries[i];
                    if (IsPreviewPublicEquipmentResource(entry) || IsPreviewPresentationResource(entry))
                    {
                        ResourceCatalogEntry previewEntry = IsPreviewPresentationResource(entry)
                            ? entry
                            : CreatePreviewPublicEquipmentEntry(entry);
                        if (!_resourceManager.Contains(previewEntry.CreateKey(catalog.PackageId)))
                            entries.Add(previewEntry);
                    }
                }

                if (entries.Count == 0)
                    return;

                _resourceManager.AddCatalog(new ResourceCatalog(
                    catalog.CatalogId + ".characterActionPreview",
                    catalog.PackageId,
                    entries));
            }
            catch (Exception ex)
            {
                _logger.Warning(
                    "CharacterActionPreview.Resources",
                    "Global resource profile could not be added to character action preview: " + ex.Message);
            }
        }

        private static bool IsPreviewPresentationResource(ResourceCatalogEntry entry)
        {
            return entry != null
                && string.Equals(entry.TypeId, ResourceTypeIds.GameObject, StringComparison.Ordinal)
                && entry.Id.StartsWith("vfx.", StringComparison.Ordinal);
        }

        private static bool IsPreviewPublicEquipmentResource(ResourceCatalogEntry entry)
        {
            if (entry == null)
                return false;

            if (entry.Id.StartsWith("weapon.", StringComparison.Ordinal))
                return true;

            IReadOnlyList<string> labels = entry.Labels;
            for (int i = 0; i < labels.Count; i++)
            {
                string label = labels[i];
                if (string.Equals(label, "domain.weapon", StringComparison.Ordinal)
                    || string.Equals(label, "usage.weaponmodel", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolveEditorAssetPath(ResourceCatalogEntry entry)
        {
            if (entry == null)
                return string.Empty;

            if (TryGetProviderData(entry, "generatedAssetPath", out string generatedAssetPath))
                return generatedAssetPath.Replace('\\', '/');
            if (TryGetProviderData(entry, "unityAssetPath", out string unityAssetPath))
                return unityAssetPath.Replace('\\', '/');
            if (TryGetProviderData(entry, EditorAssetDatabaseResourceProvider.AssetPathProviderDataKey, out string assetPath))
                return assetPath.Replace('\\', '/');

            return entry.Address.Replace('\\', '/');
        }

        private static bool TryGetProviderData(ResourceCatalogEntry entry, string key, out string value)
        {
            value = string.Empty;
            if (entry == null || entry.ProviderData == null || string.IsNullOrWhiteSpace(key))
                return false;

            if (!entry.ProviderData.TryGetValue(key, out value) || string.IsNullOrWhiteSpace(value))
            {
                value = string.Empty;
                return false;
            }

            return true;
        }

        private static ResourceCatalogEntry CreatePreviewPublicEquipmentEntry(ResourceCatalogEntry entry)
        {
            string packageId = entry.Id.StartsWith("weapon.", StringComparison.Ordinal)
                ? "common"
                : entry.PackageId;
            return new ResourceCatalogEntry(
                entry.Id,
                ResourceTypeIds.GameObject,
                entry.ProviderId,
                entry.Address,
                GetDefaultEquipmentResourceVariant(entry),
                packageId,
                entry.Dependencies,
                entry.Labels,
                entry.Hash,
                entry.Size,
                entry.AllowOverride,
                entry.ProviderData);
        }

        private static string GetDefaultEquipmentResourceVariant(ResourceCatalogEntry entry)
        {
            if (entry != null && entry.Id.StartsWith("weapon.", StringComparison.Ordinal))
                return entry.Variant ?? string.Empty;

            return entry != null && !string.IsNullOrWhiteSpace(entry.Variant)
                ? entry.Variant
                : "default";
        }

        private static ResourceCatalog TryLoadEditorResourceCatalog(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            ResourceCatalogJsonDto dto = JsonConvert.DeserializeObject<ResourceCatalogJsonDto>(File.ReadAllText(path));
            if (dto?.entries == null)
                return null;

            var entries = new List<ResourceCatalogEntry>(dto.entries.Length);
            for (int i = 0; i < dto.entries.Length; i++)
            {
                ResourceCatalogEntryJsonDto entry = dto.entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.id))
                    continue;

                string provider = entry.provider ?? string.Empty;
                Dictionary<string, string> providerData = entry.providerData != null
                    ? new Dictionary<string, string>(entry.providerData, StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal);
                if (providerData.ContainsKey(EditorAssetDatabaseResourceProvider.AssetPathProviderDataKey))
                    provider = EditorAssetDatabaseResourceProvider.Id;

                entries.Add(new ResourceCatalogEntry(
                    entry.id,
                    entry.ResolveTypeId(),
                    provider,
                    entry.address,
                    entry.variant,
                    entry.packageId,
                    ConvertDependencies(entry.dependencies),
                    entry.labels,
                    entry.hash,
                    entry.size,
                    entry.allowOverride,
                    providerData));
            }

            return new ResourceCatalog(dto.catalogId, dto.packageId, entries);
        }

        private static ResourceKey[] ConvertDependencies(ResourceKeyJsonDto[] dependencies)
        {
            dependencies = dependencies ?? Array.Empty<ResourceKeyJsonDto>();
            var result = new List<ResourceKey>(dependencies.Length);
            for (int i = 0; i < dependencies.Length; i++)
            {
                ResourceKeyJsonDto dependency = dependencies[i];
                if (dependency == null || string.IsNullOrWhiteSpace(dependency.id))
                    continue;

                result.Add(new ResourceKey(
                    dependency.id,
                    dependency.ResolveTypeId(),
                    dependency.variant,
                    dependency.packageId));
            }

            return result.ToArray();
        }

        private bool TryRegisterAnimationClip(ResourceCatalogEntry entry)
        {
            if (!CharacterRuntimeAnimationResources.TryGetCatalogEntryUnityAssetPath(entry, out string assetPath))
                return false;

            CharacterRuntimeAnimationResources.TryGetCatalogEntrySourceClipName(entry, out string sourceClipName);
            AnimationClip clip = LoadAnimationClipAsset(assetPath, sourceClipName);
            if (clip == null)
            {
                _logger.Warning(
                    "CharacterActionPreview",
                    "找不到动画片段资源。resource=" + entry.Id
                    + "，sourceClip=" + sourceClipName
                    + "，assetPath=" + assetPath);
                return false;
            }

            _memoryProvider.Register(entry.Address, clip);
            if (!_previewAnimationClips.Contains(clip))
                _previewAnimationClips.Add(clip);
            RegisterPreviewAnimationClipKey(entry.Id, clip);
            RegisterPreviewAnimationClipKey(entry.Address, clip);
            return true;
        }

        private void RegisterPreviewAnimationClipKey(string key, AnimationClip clip)
        {
            if (string.IsNullOrWhiteSpace(key) || clip == null)
                return;

            _previewAnimationClipsByKey[key] = clip;
        }

        private void CachePreviewHierarchy(GameObject instance)
        {
            _previewHierarchyCacheBuildCount++;
            _defaultEquipmentBinder = instance != null
                ? instance.GetComponent<CharacterDefaultEquipmentRuntimeBinder>()
                : null;
            _previewAnimator = instance != null
                ? instance.GetComponentInChildren<Animator>(includeInactive: true)
                : null;
            _previewAnimationSamplingRoot = _previewAnimator != null
                ? _previewAnimator.gameObject
                : instance;
        }

        private void ConfigureDefaultEquipment(GameObject instance, PreviewBinding binding)
        {
            CharacterDefaultEquipmentRuntimeBinder binder = _defaultEquipmentBinder;
            if (binder == null)
                return;

            ConfigureDefaultWeaponBindings(binder, instance, binding);
            binder.ConfigureResourceManager(_resourceManager, instantiateDefaultWeapons: true);
            EnsureSpawnedWeaponTraceLocators(binding.Package, binder);
        }

        private void ConfigureDefaultWeaponBindings(
            CharacterDefaultEquipmentRuntimeBinder binder,
            GameObject instance,
            PreviewBinding binding)
        {
            if (binder == null || instance == null || binding.Package == null || binding.Loadout == null)
                return;

            CharacterWeaponAttachmentRuntimeBinding[] attachments =
                ResolveMountedWeaponAttachments(binding.Package, binding.EquippedWeapons);
            Transform socketsRoot = instance.transform.Find("Sockets");
            var weapons = new CharacterDefaultWeaponPrefabBinding[attachments.Length];
            for (int i = 0; i < attachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = attachments[i];
                var weapon = new CharacterDefaultWeaponPrefabBinding();
                ComposePreviewWeaponPose(
                    attachment,
                    out Vector3 localPosition,
                    out Quaternion localRotation,
                    out Vector3 localScale);
                weapon.Configure(
                    attachment.WeaponId,
                    attachment.EquipSlot,
                    attachment.AttachSocketId,
                    attachment.TraceId,
                    GetDefaultWeaponResourceId(binding.Package, attachment),
                    ResourceTypeIds.GameObject,
                    GetDefaultWeaponResourceVariant(attachment),
                    GetDefaultWeaponResourcePackageId(binding.Package, attachment),
                    FindSocket(instance.transform, attachment.AttachSocketId),
                    prefab: null,
                    localPosition,
                    localRotation,
                    localScale, socketLabel: attachment.AttachSocketLabel, weaponLabel: attachment.WeaponLabel, equipmentSlotLabel: attachment.EquipmentSlotLabel, traceLabel: attachment.TraceLabel);
                weapons[i] = weapon;
            }

            binder.ConfigureDefaultEquipment(
                binding.Package.PackageId,
                binding.Character != null ? binding.Character.CharacterId.Value.ToString() : string.Empty,
                binding.Loadout.LoadoutId.Value.ToString(),
                socketsRoot,
                _previewAnimator,
                weapons,
                _previewAnimationClips.ToArray(),
                instantiateDefaultWeaponsOnAwake: false,
                playFirstAnimationOnStart: false);
        }

        private static void ComposePreviewWeaponPose(
            CharacterWeaponAttachmentRuntimeBinding attachment,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 scale)
        {
            Vector3 slotPosition = ToVector3(attachment.SlotPose.Position, Vector3.zero);
            Quaternion slotRotation = ToQuaternion(attachment.SlotPose.Rotation);
            Vector3 slotScale = ToVector3(attachment.SlotPose.Scale, Vector3.one);
            Vector3 weaponPosition = ToVector3(attachment.WeaponModelPose.Position, Vector3.zero);
            Quaternion weaponRotation = ToQuaternion(attachment.WeaponModelPose.Rotation);
            Vector3 weaponScale = ToVector3(attachment.WeaponModelPose.Scale, Vector3.one);
            position = slotPosition + slotRotation * Vector3.Scale(slotScale, weaponPosition);
            rotation = slotRotation * weaponRotation;
            scale = Vector3.Scale(slotScale, weaponScale);
        }

        private static void EnsureSpawnedWeaponTraceLocators(
            CharacterImportedPackage package,
            CharacterDefaultEquipmentRuntimeBinder binder)
        {
            if (package == null || package.Geometry == null || binder == null)
                return;

            IReadOnlyList<CharacterDefaultWeaponPrefabBinding> bindings = binder.DefaultWeapons;
            IReadOnlyList<GameObject> spawned = binder.SpawnedWeapons;
            CharacterWeaponTraceRuntimeBinding[] traces = package.Geometry.WeaponTraces;
            int count = Math.Min(bindings.Count, spawned.Count);
            for (int i = 0; i < count; i++)
            {
                CharacterDefaultWeaponPrefabBinding binding = bindings[i];
                GameObject weapon = spawned[i];
                if (binding == null || weapon == null)
                    continue;

                CharacterWeaponAttachmentRuntimeBinding attachment = FindAttachmentForDefaultWeaponBinding(
                    package.Geometry.WeaponAttachments,
                    binding);
                if (attachment == null)
                    continue;

                for (int traceIndex = 0; traces != null && traceIndex < traces.Length; traceIndex++)
                {
                    CharacterWeaponTraceRuntimeBinding trace = traces[traceIndex];
                    if (!TraceBelongsToAttachment(trace, attachment))
                        continue;

                    EnsureSpawnedWeaponTraceLocator(
                        weapon.transform,
                        trace.StartLocatorPath,
                        binding,
                        trace.StartPose);
                    EnsureSpawnedWeaponTraceLocator(
                        weapon.transform,
                        trace.EndLocatorPath,
                        binding,
                        trace.EndPose);
                }
            }
        }

        private static CharacterWeaponAttachmentRuntimeBinding FindAttachmentForDefaultWeaponBinding(
            CharacterWeaponAttachmentRuntimeBinding[] attachments,
            CharacterDefaultWeaponPrefabBinding binding)
        {
            if (attachments == null || binding == null)
                return null;

            for (int i = 0; i < attachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = attachments[i];
                if (attachment == null)
                    continue;
                if (!MatchesNonEmpty(binding.WeaponId, attachment.WeaponId))
                    continue;
                if (!MatchesNonEmpty(binding.EquipSlot, attachment.EquipSlot))
                    continue;
                if ((binding.SocketId > 0 && binding.SocketId != attachment.AttachSocketId))
                    continue;
                if (!MatchesNonEmpty(binding.TraceId, attachment.TraceId))
                    continue;

                return attachment;
            }

            return null;
        }

        private static bool TraceBelongsToAttachment(
            CharacterWeaponTraceRuntimeBinding trace,
            CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            if (trace == null || attachment == null)
                return false;
            if ((attachment.TraceId > 0)
                && trace.TraceId != attachment.TraceId)
            {
                return false;
            }
            if ((trace.WeaponId > 0)
                && trace.WeaponId != attachment.WeaponId)
            {
                return false;
            }
            return (trace.EquipSlot <= 0)
                || trace.EquipSlot == attachment.EquipSlot;
        }

        private static bool MatchesNonEmpty(int left, int right) => left <= 0 || right <= 0 || left == right;

        private static bool MatchesNonEmpty(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return true;
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private static void EnsureSpawnedWeaponTraceLocator(
            Transform weaponRoot,
            string locatorPath,
            CharacterDefaultWeaponPrefabBinding binding,
            CharacterRuntimePose tracePose)
        {
            if (weaponRoot == null || string.IsNullOrWhiteSpace(locatorPath))
                return;
            if (FindLocatorTransform(weaponRoot, locatorPath) != null)
                return;

            Transform locator = EnsureNestedTransform(weaponRoot, locatorPath);
            if (locator == null)
                return;

            ResolveSpawnedWeaponTraceLocatorPose(
                weaponRoot,
                binding,
                tracePose,
                out Vector3 localPosition,
                out Quaternion localRotation,
                out Vector3 localScale);
            locator.localPosition = localPosition;
            locator.localRotation = localRotation;
            locator.localScale = localScale;
        }

        private static Transform EnsureNestedTransform(Transform root, string path)
        {
            if (root == null || string.IsNullOrWhiteSpace(path))
                return null;

            string normalized = NormalizeProjectPath(path).Trim('/');
            if (string.IsNullOrEmpty(normalized))
                return null;

            Transform current = root;
            string[] segments = normalized.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                if (string.IsNullOrWhiteSpace(segment))
                    continue;

                Transform child = current.Find(segment);
                if (child == null)
                {
                    child = new GameObject(segment).transform;
                    child.SetParent(current, false);
                }

                current = child;
            }

            return current == root ? null : current;
        }

        private static void ResolveSpawnedWeaponTraceLocatorPose(
            Transform weaponRoot,
            CharacterDefaultWeaponPrefabBinding binding,
            CharacterRuntimePose tracePose,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Vector3 localScale)
        {
            localPosition = ToVector3(tracePose.Position, Vector3.zero);
            localRotation = ToQuaternion(tracePose.Rotation);
            localScale = ToVector3(tracePose.Scale, Vector3.one);
            if (weaponRoot == null || binding == null || !IsDefaultWeaponSocketPose(binding, tracePose))
                return;

            Transform socket = weaponRoot.parent;
            if (socket != null)
            {
                Vector3 worldPosition = socket.TransformPoint(localPosition);
                Quaternion worldRotation = socket.rotation * localRotation;
                localPosition = weaponRoot.InverseTransformPoint(worldPosition);
                localRotation = Quaternion.Inverse(weaponRoot.rotation) * worldRotation;
                localScale = DivideScale(localScale, weaponRoot.lossyScale);
                return;
            }

            Vector3 bindingScale = binding.LocalScale == Vector3.zero ? Vector3.one : binding.LocalScale;
            Matrix4x4 bindingMatrix = Matrix4x4.TRS(binding.LocalPosition, binding.LocalRotation, bindingScale);
            Matrix4x4 inverseBinding = bindingMatrix.inverse;
            localPosition = inverseBinding.MultiplyPoint3x4(localPosition);
            localRotation = Quaternion.Inverse(binding.LocalRotation) * localRotation;
            localScale = DivideScale(localScale, bindingScale);
        }

        private static bool IsDefaultWeaponSocketPose(
            CharacterDefaultWeaponPrefabBinding binding,
            CharacterRuntimePose tracePose)
        {
            if (!string.Equals(tracePose.ParentKind, "Socket", StringComparison.Ordinal)
                && !string.Equals(tracePose.ParentKind, "Bone", StringComparison.Ordinal))
            {
                return false;
            }

            string parent = tracePose.ParentPath ?? string.Empty;
            return string.Equals(parent, binding.SocketLabel, StringComparison.Ordinal)
                || string.Equals(parent, binding.EquipmentSlotLabel, StringComparison.Ordinal);
        }

        private static Vector3 DivideScale(Vector3 value, Vector3 divisor)
        {
            return new Vector3(
                Math.Abs(divisor.x) > 0.00001f ? value.x / divisor.x : value.x,
                Math.Abs(divisor.y) > 0.00001f ? value.y / divisor.y : value.y,
                Math.Abs(divisor.z) > 0.00001f ? value.z / divisor.z : value.z);
        }

        private static void ConfigureControlOwnership(GameObject instance)
        {
            CharacterRuntimeInputMotionController motion =
                instance.GetComponent<CharacterRuntimeInputMotionController>();
            if (motion != null)
                motion.EnableInputMotion = false;
        }

        private void ConfigureLocomotion(GameObject instance, CharacterImportedPackage package)
        {
            _locomotion = instance.GetComponent<CharacterRuntimeLocomotionBlendController>();
            if (_locomotion == null)
                return;

            _locomotion.SetExternalMotion(Vector2.zero, 0f);
            if (!CharacterRuntimeAnimationResources.TryCreateBinding(
                    package,
                    out CharacterRuntimeAnimationBinding binding,
                    out _))
            {
                return;
            }

            CharacterRuntimeAnimationResources.TryConfigureLocomotion(
                _locomotion,
                _resourceManager,
                binding,
                instance.name,
                _logger);
        }

        private void StartAndReplay(
            CharacterActionSetConfig actionSet,
            CharacterActionConfig action,
            CharacterActionConfigRegistry registry,
            string actionRevision,
            int targetFrame,
            CharacterActionLifecycleReason exitReason)
        {
            ExitRunnerForRebuild(exitReason);
            DisposeRunner();
            _gateway = new CharacterActionExecutionGateway();
            _previewAction = action;
            _activeActionId = action.ActionId;
            _activeActionRevision = actionRevision;
            _lastFrame = -1;
            ResetPreviewSimulationState();
            PreparePreviewAnimationBindings(action);

            CharacterActionResolveResult resolve = CreateDirectResolveResult(action);
            CharacterActionExecutionRequest request = CharacterActionExecutionRequest.StartIntent(
                resolve,
                CharacterActionRunnerActionDefinition.FromConfig(action),
                CharacterActionSourceKind.Debug,
                action.Priority,
                resolve.TraceId);
            CharacterActionExecutionResult start = _gateway.Execute(in request, _executionBuffer);
            ApplyOperation(start, 0, afterFrame: false);
            _lastFrame = 0;
            _lastDebugSnapshot = _captureDebugSnapshots ? _gateway.CreateDebugSnapshot() : null;
            SamplePreviewFrame(0);
            ApplyOperation(start, 0, afterFrame: true);
            AppendIntentDiagnostics(actionSet, registry, action);

            for (int frame = 1; frame <= targetFrame; frame++)
                StepRunner(frame);
        }

        // Rebuilt only when the selected authored action or character profile changes.
        private void PreparePreviewAnimationBindings(CharacterActionConfig action)
        {
            _preparedPreviewAnimations.Clear();
            if (_locomotion == null || action?.AnimationTrack == null) return;
            AnimationTrackEvent[] events = action.AnimationTrack.Events;
            for (int i = 0; i < events.Length; i++)
            {
                AnimationTrackEvent entry = events[i];
                string key = entry.EffectiveAnimationKey;
                if (string.IsNullOrEmpty(key)) continue;
                int bindingId = 0;
                ResourceKey clipKey = default;
                if (_locomotion.AnimationSet != null
                    && _locomotion.AnimationSet.TryResolveBindingForPreparation(key, out MxAnimationActionBinding binding))
                    bindingId = binding.BindingId;
                else if (!_locomotion.TryPrepareClipKeyForPlayback(key, out clipKey))
                    continue;
                if (_preparedPreviewAnimations.ContainsKey(entry.StableEventId))
                    throw new InvalidOperationException("Preview animation events require distinct declared identities.");
                _preparedPreviewAnimations.Add(entry.StableEventId,
                    new PreparedPreviewAnimation(bindingId, clipKey, "character-action-preview:" + key));
            }
        }

        private readonly struct PreparedPreviewAnimation
        {
            internal PreparedPreviewAnimation(int bindingId, ResourceKey clipKey, string defaultCorrelation)
            {
                BindingId = bindingId; ClipKey = clipKey; DefaultCorrelation = defaultCorrelation;
            }
            internal int BindingId { get; }
            internal ResourceKey ClipKey { get; }
            internal string DefaultCorrelation { get; }
        }

        private void ExitRunnerForRebuild(CharacterActionLifecycleReason exitReason)
        {
            if (_gateway == null || _lastFrame < 0)
                return;

            CharacterActionExecutionRequest terminate =
                CharacterActionExecutionRequest.TerminateForLifecycle(
                    exitReason,
                    new RuntimeTraceIdentity(PreviewRebuildTraceKind));
            CharacterActionExecutionResult result = _gateway.Execute(in terminate, _executionBuffer);
            ApplyOperation(result, _lastFrame, afterFrame: false);
        }

        private void StepRunner(int frame)
        {
            if (_gateway == null)
                return;

            AdvancePreviewInterval(frame);
            CharacterActionExecutionResult tick = _gateway.Tick(_executionBuffer);
            ApplyOperation(tick, frame, afterFrame: false);
            _lastFrame = frame;
            _lastDebugSnapshot = _captureDebugSnapshots ? _gateway.CreateDebugSnapshot() : null;
            SamplePreviewFrame(frame);
            ApplyOperation(tick, frame, afterFrame: true);
        }

        private CharacterActionResolveResult CreateDirectResolveResult(CharacterActionConfig action)
        {
            CharacterActionPlanDurationResult duration = CharacterActionPlanDurationResolver.Resolve(
                action,
                policy: new CharacterActionDurationPolicy(DefaultPreviewDurationFrames));
            if (!duration.Resolved)
            {
                _logger.Warning(
                    "CharacterActionPreview",
                    "无法解析动作时长，使用预览默认时长。action=" + action.ActionId);
            }

            int durationFrames = duration.Resolved
                ? duration.DurationFrames
                : DefaultPreviewDurationFrames;
            var plan = new CharacterActionPlan(
                _nextPlanId++,
                action.ActionId,
                action.Category,
                action.Priority,
                durationFrames,
                action.Phases,
                CharacterActionTrackPlan.FromConfig(action),
                new RuntimeTraceIdentity(PreviewDirectTraceKind, action.ActionId.Value));
            return CharacterActionResolveResult.Success(plan, duration.Diagnostics);
        }

        private void ApplyOperation(CharacterActionExecutionResult operation, int frame, bool afterFrame)
        {
            if (!afterFrame)
            {
                BeginFrameDispatch();
                CharacterActionDiagnostic[] diagnostics = operation.Diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
                for (int i = 0; i < diagnostics.Length; i++)
                {
                    LogCachedDiagnosticLine(
                        RuntimeLogLevel.Warning,
                        "CharacterActionPreview",
                        diagnostics[i].Code,
                        diagnostics[i].Message);
                }
            }

            CharacterActionRunnerEventBuffer events = operation.Events;
            int eventCount = events == null ? 0 : events.Count;
            for (int i = 0; i < eventCount; i++)
            {
                CharacterActionRunnerEvent evt = events[i];
                bool isAfterFrameLifecycle = evt.TrackLifecycle.HasEvent
                    && evt.TrackLifecycle.Boundary == CharacterActionTrackLifecycleBoundary.AfterFrame;
                if (isAfterFrameLifecycle != afterFrame)
                    continue;

                if (!afterFrame && !string.IsNullOrWhiteSpace(evt.DiagnosticCode))
                {
                    LogCachedDiagnosticLine(
                        RuntimeLogLevel.Warning,
                        "CharacterActionPreview",
                        evt.DiagnosticCode,
                        evt.Reason);
                }

                if (evt.TrackDispatch.HasEvent)
                    DispatchTrackEvent(evt, frame);
            }
        }

        private void DispatchTrackEvent(CharacterActionRunnerEvent runnerEvent, int frame)
        {
            CharacterActionTrackDispatchEvent dispatch = runnerEvent.TrackDispatch;
            LogDispatchInfo(dispatch, frame);

            CharacterActionAdapterResult trackResult = _trackAdapter.Adapt(
                runnerEvent,
                new CharacterActionAdapterContext(
                    new RuntimeFrame(frame),
                    new GameplayEntityId(1, 1),
                    new CombatEntityId(1),
                    new CombatBodyId(1),
                    sourceId: 0,
                    traceId: new RuntimeTraceIdentity(PreviewTrackTraceKind, sequence: frame)),
                _trackDispatchBuffer);
            LogAdapterDiagnostics(trackResult.Diagnostics);

            CharacterActionPresentationAdapterResult presentationResult = _presentationAdapter.Adapt(
                runnerEvent,
                new CharacterActionPresentationAdapterContext(
                    new RuntimeFrame(frame),
                    _previewInstance != null ? _previewInstance.name : "character-action-preview",
                    sourceId: 0,
                    traceId: new RuntimeTraceIdentity(PreviewPresentationTraceKind, sequence: frame)),
                _presentationDispatchBuffer);
            LogAdapterDiagnostics(presentationResult.Diagnostics);

            if (dispatch.TrackKind == CharacterActionTrackKind.Debug)
                _lastDebugDispatches.Add(dispatch);
        }

        private void AdvancePreviewInterval(int frame)
        {
            float deltaTime = 1f / CharacterActionTimelineLayout.EditorTimelineFps;
            ApplyPreviewMotion(frame, deltaTime);
            TickAnimation(frame, deltaTime);
            ApplyRuntimeBoneBindings();
            UpdatePreviewPresentation(frame);
        }

        private void SamplePreviewFrame(int frame)
        {
            TickAnimation(frame, 0f);
            ApplyRuntimeBoneBindings();
            UpdateHitTraceSnapshots(frame);
            UpdatePreviewPresentation(frame, sampleBladeTrails: true);
        }

        private void TickAnimation(int frame, float deltaTime)
        {
            if (_locomotion != null)
            {
                _locomotion.TickRuntimeAnimation(
                    deltaTime,
                    frame);

                // The runtime Playables backend already evaluates the humanoid pose with root motion
                // extracted from the skeleton. Keep the editor fallback disconnected when it succeeds.
                if (_locomotion.HasAnimationBackend && _locomotion.LastActionAnimationResult.Success)
                {
                    DisposeFallbackAnimationSampler();
                    return;
                }
            }

            SamplePreviewAnimationClip(frame);
        }

        private void SamplePreviewAnimationClip(int frame)
        {
            if (_previewInstance == null)
                return;
            if (!TryResolvePreviewAnimationClipEvent(frame, out AnimationTrackEvent animationEvent))
                return;
            if (!TryGetPreviewAnimationClip(animationEvent.EffectiveAnimationKey, out AnimationClip clip))
                return;

            GameObject samplingRoot = _previewAnimationSamplingRoot;
            if (samplingRoot == null)
                return;

            float sampleTime = ResolvePreviewAnimationSampleTime(animationEvent, frame);
            if (_previewAnimator == null)
            {
                clip.SampleAnimation(samplingRoot, sampleTime);
                return;
            }

            if (!EnsureFallbackAnimationSampler(clip))
                return;

            _fallbackAnimationPlayable.SetTime(sampleTime);
            _fallbackAnimationGraph.Evaluate(0f);
        }

        private bool EnsureFallbackAnimationSampler(AnimationClip clip)
        {
            if (clip == null || _previewAnimator == null)
                return false;
            if (_fallbackAnimationGraph.IsValid()
                && _fallbackAnimationPlayable.IsValid()
                && ReferenceEquals(_fallbackAnimationClip, clip))
            {
                return true;
            }

            DisposeFallbackAnimationSampler();

            _fallbackAnimatorWasEnabled = _previewAnimator.enabled;
            _fallbackAnimatorStateCaptured = true;
            _previewAnimator.enabled = true;

            _fallbackAnimationGraph = PlayableGraph.Create("CharacterActionPreviewFallback");
            _fallbackAnimationGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(
                _fallbackAnimationGraph,
                "CharacterActionPreviewAnimation",
                _previewAnimator);
            _fallbackAnimationPlayable = AnimationClipPlayable.Create(_fallbackAnimationGraph, clip);
            _fallbackAnimationPlayable.SetApplyFootIK(false);
            output.SetSourcePlayable(_fallbackAnimationPlayable);
            _fallbackAnimationGraph.Play();
            _fallbackAnimationClip = clip;
            return true;
        }

        private void DisposeFallbackAnimationSampler()
        {
            if (_fallbackAnimationGraph.IsValid())
                _fallbackAnimationGraph.Destroy();

            _fallbackAnimationGraph = default;
            _fallbackAnimationPlayable = default;
            _fallbackAnimationClip = null;

            if (_fallbackAnimatorStateCaptured && _previewAnimator != null)
                _previewAnimator.enabled = _fallbackAnimatorWasEnabled;
            _fallbackAnimatorStateCaptured = false;
            _fallbackAnimatorWasEnabled = false;
        }

        private bool TryGetPreviewAnimationClip(string clipKey, out AnimationClip clip)
        {
            clip = null;
            if (string.IsNullOrWhiteSpace(clipKey))
                return false;

            return _previewAnimationClipsByKey.TryGetValue(clipKey, out clip) && clip != null;
        }

        private static float ResolvePreviewAnimationSampleTime(AnimationTrackEvent animationEvent, int frame)
        {
            int localFrame = Math.Max(0, frame - animationEvent.Frame);
            float speed = animationEvent.PlaybackSpeed <= 0.0001f ? 1f : animationEvent.PlaybackSpeed;
            float sourceFrame = Math.Max(0, animationEvent.AnimationStartOffsetFrames) + localFrame * speed;
            return sourceFrame / CharacterActionTimelineLayout.EditorTimelineFps;
        }

        private void RepaintPreviewViews()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            if (_objectHost != null)
                _objectHost.RequestRepaint();
            else
                SceneView.RepaintAll();
        }

        private void UpdateHitTraceSnapshots(int frame)
        {
            _weaponTraceSnapshots.Clear();
            _hitVolumeSnapshots.Clear();
            if (_previewInstance == null || _activeHitTraces.Count == 0)
                return;

            for (int i = _activeHitTraces.Count - 1; i >= 0; i--)
            {
                PreviewActiveHitTrace active = _activeHitTraces[i];
                if (active.ActionId != (_previewAction?.ActionId ?? CharacterActionId.None))
                {
                    _activeHitTraces.RemoveAt(i);
                    continue;
                }

                if (active.HitVolume.HasVolume)
                {
                    if (active.HitVolume.ShapeKind != CharacterActionHitVolumeShapeKind.Capsule)
                        continue;

                    if (active.HitVolume.SourceKind == CharacterActionHitVolumeSourceKind.CasterBody)
                    {
                        _hitVolumeSnapshots.Add(CreateCasterBodyHitVolumeSnapshot(active, frame));
                        continue;
                    }

                    if (active.HitVolume.SourceKind == CharacterActionHitVolumeSourceKind.BakedSocketCapsule)
                    {
                        Transform startSocket = FindSocket(_previewInstance.transform, active.HitVolume.StartSocketId);
                        Transform endSocket = FindSocket(_previewInstance.transform, active.HitVolume.EndSocketId);
                        if (startSocket == null || endSocket == null)
                        {
                            LogHitTraceDiagnostic(
                                PreviewHitTraceDiagnosticKind.MissingSocket,
                                active,
                                frame,
                                string.Empty,
                                startSocketId: active.HitVolume.StartSocketId,
                                endSocketId: active.HitVolume.EndSocketId);
                            continue;
                        }

                        _hitVolumeSnapshots.Add(new CharacterActionPreviewHitVolumeSnapshot(
                            active.ActionId,
                            active.StableEventId,
                            frame,
                            startSocket.position,
                            endSocket.position,
                            active.HitVolume.Radius,
                            active.HitVolume.ShapeKind,
                            active.HitVolume.SourceKind));
                        continue;
                    }

                    Transform root = _previewInstance.transform;
                    _hitVolumeSnapshots.Add(new CharacterActionPreviewHitVolumeSnapshot(
                        active.ActionId,
                        active.StableEventId,
                        frame,
                        root.TransformPoint(new Vector3(
                            active.HitVolume.LocalPointAX,
                            active.HitVolume.LocalPointAY,
                            active.HitVolume.LocalPointAZ)),
                        root.TransformPoint(new Vector3(
                            active.HitVolume.LocalPointBX,
                            active.HitVolume.LocalPointBY,
                            active.HitVolume.LocalPointBZ)),
                        active.HitVolume.Radius,
                        active.HitVolume.ShapeKind,
                        active.HitVolume.SourceKind));
                    continue;
                }

                if (!TryResolvePreviewAnimationClipKey(frame, out string clipKey))
                {
                    LogHitTraceDiagnostic(
                        PreviewHitTraceDiagnosticKind.MissingAnimationClip,
                        active,
                        frame);
                    continue;
                }

                if (!TryResolvePreviewWeaponTraceProfile(
                        active.EquipmentSlotId,
                        active.TraceProfileId,
                        out PreviewWeaponTraceProfile profile))
                {
                    LogHitTraceDiagnostic(
                        PreviewHitTraceDiagnosticKind.MissingWeaponTraceProfile,
                        active,
                        frame);
                    continue;
                }

                if (TryResolveWeaponLocatorPose(
                    active.TraceProfileId,
                    out CharacterActionPreviewWeaponLocatorWorldPose locatorPose))
                {
                    var locatorTraceFrame = new WeaponTraceFrame(
                        active.TraceProfileId,
                        ToFixVector3(locatorPose.Root),
                        ToFixVector3(locatorPose.Tip),
                        ToFixVector3(locatorPose.Root),
                        ToFixVector3(locatorPose.Tip),
                        ToFix64(profile.Radius),
                        CombatPhysicsLayerMask.All,
                        ToFix64(profile.ProximalExtension),
                        ToFix64(profile.ProximalRadius));
                    _weaponTraceSnapshots.Add(new CharacterActionPreviewWeaponTraceSnapshot(
                        active.ActionId,
                        active.TraceProfileId,
                        frame,
                        locatorTraceFrame));
                    continue;
                }

                if (TryGetWeaponLocatorPoseSnapshot(
                        active.TraceProfileId,
                        out CharacterActionPreviewWeaponLocatorPoseSnapshot locatorSnapshot)
                    && HasLocatorPath(locatorSnapshot))
                {
                    LogHitTraceDiagnostic(
                        PreviewHitTraceDiagnosticKind.UnresolvedWeaponLocator,
                        active,
                        frame,
                        string.Empty,
                        locatorSnapshot.StartLocatorPath,
                        locatorSnapshot.EndLocatorPath);
                    continue;
                }

                if (!TryGetPreviewBakedTraceFrame(clipKey, active.TraceProfileId, frame, out PreviewBakedTraceFrame baked))
                {
                    LogHitTraceDiagnostic(
                        PreviewHitTraceDiagnosticKind.MissingBakedTraceFrame,
                        active,
                        frame,
                        clipKey);
                    continue;
                }

                Vector3 rootPrev = _previewInstance.transform.TransformPoint(baked.RootPrev);
                Vector3 tipPrev = _previewInstance.transform.TransformPoint(baked.TipPrev);
                Vector3 rootNow = _previewInstance.transform.TransformPoint(baked.RootNow);
                Vector3 tipNow = _previewInstance.transform.TransformPoint(baked.TipNow);
                var traceFrame = new WeaponTraceFrame(
                    active.TraceProfileId,
                    ToFixVector3(rootPrev),
                    ToFixVector3(tipPrev),
                    ToFixVector3(rootNow),
                    ToFixVector3(tipNow),
                    ToFix64(profile.Radius),
                    CombatPhysicsLayerMask.All,
                    ToFix64(profile.ProximalExtension),
                    ToFix64(profile.ProximalRadius));
                _weaponTraceSnapshots.Add(new CharacterActionPreviewWeaponTraceSnapshot(
                    active.ActionId,
                    active.TraceProfileId,
                    frame,
                    traceFrame));
            }
        }

        private void OnSceneViewDuringSceneGui(SceneView sceneView)
        {
            if (_disposed || _previewInstance == null)
                return;
            if (_weaponLocatorPoseSnapshots.Count == 0
                && _weaponTraceSnapshots.Count == 0
                && _hitVolumeSnapshots.Count == 0
                && _vfxPreviewSnapshots.Count == 0
                && _projectilePreviewSnapshots.Count == 0)
            {
                return;
            }

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Color previousColor = Handles.color;
            for (int i = 0; i < _weaponLocatorPoseSnapshots.Count; i++)
                DrawWeaponLocatorPoseSnapshot(_weaponLocatorPoseSnapshots[i]);
            for (int i = 0; i < _weaponTraceSnapshots.Count; i++)
                DrawWeaponTraceSnapshot(_weaponTraceSnapshots[i]);
            for (int i = 0; i < _hitVolumeSnapshots.Count; i++)
                DrawHitVolumeSnapshot(_hitVolumeSnapshots[i]);
            for (int i = 0; i < _vfxPreviewSnapshots.Count; i++)
                DrawVfxPreviewSnapshot(_vfxPreviewSnapshots[i]);
            for (int i = 0; i < _projectilePreviewSnapshots.Count; i++)
                DrawProjectilePreviewSnapshot(_projectilePreviewSnapshots[i]);

            Handles.color = previousColor;
        }

        private void DrawWeaponLocatorPoseSnapshot(CharacterActionPreviewWeaponLocatorPoseSnapshot snapshot)
        {
            if (!TryResolveWeaponLocatorPose(snapshot, out CharacterActionPreviewWeaponLocatorWorldPose pose))
                return;

            Vector3 root = pose.Root;
            Vector3 tip = pose.Tip;
            Quaternion rootRotation = pose.RootRotation;
            Quaternion tipRotation = pose.TipRotation;
            float radius = Mathf.Max(0.001f, snapshot.Radius);
            Vector3 axis = tip - root;
            Vector3 normal = axis.sqrMagnitude > 0.000001f ? axis.normalized : Vector3.up;

            Handles.color = new Color(0.1f, 0.95f, 0.35f, 0.95f);
            DrawSceneLine(3f, root, tip);
            Handles.DrawWireDisc(root, normal, radius);
            Handles.color = new Color(1f, 0.15f, 0.65f, 0.95f);
            Handles.DrawWireDisc(tip, normal, radius);
            Handles.SphereHandleCap(0, root, Quaternion.identity, radius * 0.45f, EventType.Repaint);
            Handles.SphereHandleCap(0, tip, Quaternion.identity, radius * 0.55f, EventType.Repaint);

            DrawPoseAxes(root, rootRotation, radius * 1.8f);
            DrawPoseAxes(tip, tipRotation, radius * 1.8f);

            Handles.color = new Color(0.75f, 1f, 0.75f, 1f);
            Handles.Label(
                root + Vector3.up * (radius + 0.03f),
                "刀刃根部 " + EmptyAsDash(snapshot.TraceProfileId) + "\n"
                + EmptyAsDash(snapshot.StartLocatorPath) + "\n来源=" + EmptyAsDash(pose.Source));
            Handles.color = new Color(1f, 0.75f, 0.95f, 1f);
            Handles.Label(
                tip + Vector3.up * (radius + 0.03f),
                "刀刃尖端 " + EmptyAsDash(snapshot.TraceProfileId) + "\n"
                + EmptyAsDash(snapshot.EndLocatorPath) + "\n来源=" + EmptyAsDash(pose.Source));
        }

        private void DrawPoseAxes(Vector3 origin, Quaternion rotation, float size)
        {
            float axisSize = Mathf.Max(0.04f, size);
            Handles.color = new Color(1f, 0.15f, 0.15f, 1f);
            DrawSceneLine(2f, origin, origin + rotation * Vector3.right * axisSize);
            Handles.color = new Color(0.25f, 1f, 0.25f, 1f);
            DrawSceneLine(2f, origin, origin + rotation * Vector3.up * axisSize);
            Handles.color = new Color(0.25f, 0.55f, 1f, 1f);
            DrawSceneLine(2f, origin, origin + rotation * Vector3.forward * axisSize);
        }

        private void DrawSceneLine(float width, Vector3 from, Vector3 to)
        {
            _sceneLinePoints[0] = from;
            _sceneLinePoints[1] = to;
            Handles.DrawAAPolyLine(width, _sceneLinePoints);
        }

        private void RebuildWeaponLocatorPoseCache()
        {
            _weaponLocatorPoseCache.Clear();
            _weaponLocatorCacheBuildCount++;
            if (_previewInstance == null)
                return;

            for (int i = 0; i < _weaponLocatorPoseSnapshots.Count; i++)
            {
                CharacterActionPreviewWeaponLocatorPoseSnapshot snapshot = _weaponLocatorPoseSnapshots[i];
                if (snapshot.TraceProfileId <= 0)
                    continue;

                _weaponLocatorPoseCache[snapshot.TraceProfileId] = BuildCachedWeaponLocatorPose(snapshot);
            }
        }

        private CachedWeaponLocatorPose BuildCachedWeaponLocatorPose(
            CharacterActionPreviewWeaponLocatorPoseSnapshot snapshot)
        {
            if (TryResolveSpawnedWeaponTransform(snapshot, out Transform weaponRoot))
            {
                Transform startLocator = FindLocatorTransform(weaponRoot, snapshot.StartLocatorPath);
                Transform endLocator = FindLocatorTransform(weaponRoot, snapshot.EndLocatorPath);
                if (startLocator != null && endLocator != null)
                {
                    return CachedWeaponLocatorPose.FromSpawnedWeapon(
                        snapshot,
                        startLocator,
                        endLocator);
                }

                if (HasLocatorPath(snapshot))
                    return CachedWeaponLocatorPose.Unresolved(snapshot);
            }

            // Weapon-bound traces must follow an actual preview weapon. Falling back to the
            // character root makes an empty equipment state look like a valid weapon trace.
            if (RequiresSpawnedWeapon(snapshot))
                return CachedWeaponLocatorPose.Unresolved(snapshot);

            Transform previewRoot = _previewInstance.transform;
            Transform startParent = ResolveLocatorParent(
                previewRoot,
                snapshot.StartParentKind,
                snapshot.StartParentPath);
            Transform endParent = ResolveLocatorParent(
                previewRoot,
                snapshot.EndParentKind,
                snapshot.EndParentPath);
            return startParent != null && endParent != null
                ? CachedWeaponLocatorPose.FromConfiguredParents(snapshot, startParent, endParent)
                : CachedWeaponLocatorPose.Unresolved(snapshot);
        }

        public bool TryResolveWeaponLocatorPose(
            CharacterActionPreviewWeaponLocatorPoseSnapshot snapshot,
            out CharacterActionPreviewWeaponLocatorWorldPose pose)
        {
            pose = default;
            if (_previewInstance == null)
                return false;

            return _weaponLocatorPoseCache.TryGetValue(snapshot.TraceProfileId, out CachedWeaponLocatorPose cached)
                && cached.Matches(snapshot)
                && cached.TryResolve(out pose);
        }

        private static bool HasLocatorPath(CharacterActionPreviewWeaponLocatorPoseSnapshot snapshot)
        {
            return !string.IsNullOrWhiteSpace(snapshot.StartLocatorPath)
                || !string.IsNullOrWhiteSpace(snapshot.EndLocatorPath);
        }

        private static bool RequiresSpawnedWeapon(CharacterActionPreviewWeaponLocatorPoseSnapshot snapshot)
        {
            return snapshot.WeaponId > 0
                || snapshot.EquipSlot > 0;
        }

        private bool TryResolveWeaponLocatorPose(
            int traceProfileId,
            out CharacterActionPreviewWeaponLocatorWorldPose pose)
        {
            pose = default;
            if (!TryGetWeaponLocatorPoseSnapshot(
                    traceProfileId,
                    out CharacterActionPreviewWeaponLocatorPoseSnapshot snapshot))
            {
                return false;
            }

            return TryResolveWeaponLocatorPose(snapshot, out pose);
        }

        private bool TryGetWeaponLocatorPoseSnapshot(
            int traceProfileId,
            out CharacterActionPreviewWeaponLocatorPoseSnapshot snapshot)
        {
            snapshot = default;
            if ((traceProfileId <= 0))
                return false;

            for (int i = 0; i < _weaponLocatorPoseSnapshots.Count; i++)
            {
                CharacterActionPreviewWeaponLocatorPoseSnapshot candidate = _weaponLocatorPoseSnapshots[i];
                if (candidate.TraceProfileId == traceProfileId)
                {
                    snapshot = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveSpawnedWeaponTransform(
            CharacterActionPreviewWeaponLocatorPoseSnapshot snapshot,
            out Transform weaponRoot)
        {
            weaponRoot = null;
            CharacterDefaultEquipmentRuntimeBinder binder = _defaultEquipmentBinder;
            if (binder == null)
                return false;

            IReadOnlyList<CharacterDefaultWeaponPrefabBinding> bindings = binder.DefaultWeapons;
            IReadOnlyList<GameObject> spawned = binder.SpawnedWeapons;
            int count = Math.Min(bindings.Count, spawned.Count);
            for (int i = 0; i < count; i++)
            {
                CharacterDefaultWeaponPrefabBinding binding = bindings[i];
                GameObject instance = spawned[i];
                if (binding == null || instance == null)
                    continue;
                if (!MatchesWeaponBinding(binding, snapshot))
                    continue;

                weaponRoot = instance.transform;
                return true;
            }

            for (int i = 0; i < spawned.Count; i++)
            {
                GameObject instance = spawned[i];
                if (instance == null)
                    continue;
                if (!SpawnedWeaponNameMatches(instance.name, snapshot))
                    continue;

                weaponRoot = instance.transform;
                return true;
            }

            return false;
        }

        private static bool MatchesWeaponBinding(
            CharacterDefaultWeaponPrefabBinding binding,
            CharacterActionPreviewWeaponLocatorPoseSnapshot snapshot)
        {
            if (snapshot.TraceProfileId > 0
                && binding.TraceId == snapshot.TraceProfileId)
            {
                return true;
            }

            if (snapshot.WeaponId > 0
                && binding.WeaponId != snapshot.WeaponId)
            {
                return false;
            }

            if (snapshot.EquipSlot > 0
                && binding.EquipSlot != snapshot.EquipSlot)
            {
                return false;
            }

            return snapshot.WeaponId > 0
                || snapshot.EquipSlot > 0;
        }

        private static bool SpawnedWeaponNameMatches(
            string spawnedName,
            CharacterActionPreviewWeaponLocatorPoseSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(spawnedName))
                return false;

            if (snapshot.WeaponId > 0
                && spawnedName.IndexOf(SafePreviewNameFragment(snapshot.WeaponLabel), StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            if (snapshot.EquipSlot > 0
                && spawnedName.IndexOf(SafePreviewNameFragment(snapshot.EquipmentSlotLabel), StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return snapshot.WeaponId > 0
                || snapshot.EquipSlot > 0;
        }

        private static string SafePreviewNameFragment(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static string SafePreviewNameFragment(string value)
        {
            return (value ?? string.Empty).Replace(' ', '_').Replace('/', '_');
        }

        private static Transform FindLocatorTransform(Transform weaponRoot, string locatorPath)
        {
            if (weaponRoot == null || string.IsNullOrWhiteSpace(locatorPath))
                return null;

            string normalized = NormalizeProjectPath(locatorPath).Trim('/');
            Transform byPath = weaponRoot.Find(normalized);
            if (byPath != null)
                return byPath;

            int slash = normalized.LastIndexOf('/');
            string locatorName = slash >= 0 ? normalized.Substring(slash + 1) : normalized;
            return FindChild(weaponRoot, locatorName);
        }

        private static Transform ResolveWeaponVisualPoseBasis(Transform weaponRoot)
        {
            if (weaponRoot == null)
                return null;

            Transform model = weaponRoot.Find("Model");
            return model != null ? model : weaponRoot;
        }

        private static Vector3 TransformMetricPoint(Transform basis, Vector3 localPosition)
        {
            return basis == null ? localPosition : basis.TransformPoint(localPosition);
        }

        private static Transform ResolveLocatorParent(Transform previewRoot, string parentKind, string parentPath)
        {
            if (previewRoot == null)
                return null;

            if (string.Equals(parentKind, "Socket", StringComparison.OrdinalIgnoreCase))
            {
                Transform socket = FindSocket(previewRoot, parentPath);
                if (socket != null)
                    return socket;
            }

            if (!string.IsNullOrWhiteSpace(parentPath))
            {
                Transform byPath = previewRoot.Find(parentPath);
                if (byPath != null)
                    return byPath;

                Transform byName = FindChild(previewRoot, parentPath);
                if (byName != null)
                    return byName;
            }

            return previewRoot;
        }

        private void DrawWeaponTraceSnapshot(CharacterActionPreviewWeaponTraceSnapshot snapshot)
        {
            WeaponTraceFrame trace = snapshot.TraceFrame;
            Vector3 root = ToVector3(trace.RootNow);
            Vector3 tip = ToVector3(trace.TipNow);
            float radius = Mathf.Max(0.001f, ToFloat(trace.Radius));
            Vector3 axis = tip - root;
            Vector3 normal = axis.sqrMagnitude > 0.000001f ? axis.normalized : Vector3.up;
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.000001f)
                tangent = Vector3.Cross(normal, Vector3.right);
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

            Handles.color = new Color(1f, 0.55f, 0.05f, 0.95f);
            DrawSceneLine(4f, root, tip);
            Handles.DrawWireDisc(root, normal, radius);
            Handles.DrawWireDisc(tip, normal, radius);
            Handles.DrawLine(root + tangent * radius, tip + tangent * radius);
            Handles.DrawLine(root - tangent * radius, tip - tangent * radius);
            Handles.DrawLine(root + bitangent * radius, tip + bitangent * radius);
            Handles.DrawLine(root - bitangent * radius, tip - bitangent * radius);

            if (trace.HasProximalRange && axis.sqrMagnitude > 0.000001f)
            {
                Vector3 proximalStart = root - normal * ToFloat(trace.ProximalExtension);
                float proximalRadius = Mathf.Max(0.001f, ToFloat(trace.ProximalRadius));
                Handles.color = new Color(0.15f, 0.95f, 1f, 0.85f);
                DrawSceneLine(3f, proximalStart, root);
                Handles.DrawWireDisc(proximalStart, normal, proximalRadius);
                Handles.DrawWireDisc(root, normal, proximalRadius);
            }

            _sceneWeaponTraceSegments.Clear();
            WeaponTraceQueryBuilder.BuildTipSweepSegments(
                trace,
                Fix64.FromRatio(20, 100),
                8,
                _sceneWeaponTraceSegments);
            Handles.color = new Color(0.2f, 0.75f, 1f, 0.85f);
            for (int i = 0; i < _sceneWeaponTraceSegments.Count; i++)
            {
                Vector3 from = ToVector3(_sceneWeaponTraceSegments[i].PointA);
                Vector3 to = ToVector3(_sceneWeaponTraceSegments[i].PointB);
                DrawSceneLine(3f, from, to);
                Handles.DrawWireDisc(to, normal, radius * 0.65f);
            }

            Handles.color = new Color(1f, 0.8f, 0.2f, 1f);
            Handles.SphereHandleCap(0, root, Quaternion.identity, radius * 0.35f, EventType.Repaint);
            Handles.SphereHandleCap(0, tip, Quaternion.identity, radius * 0.45f, EventType.Repaint);
            Handles.Label(tip + Vector3.up * (radius + 0.04f), "命中判定 " + EmptyAsDash(snapshot.TraceProfileId));
        }

        private CharacterActionPreviewHitVolumeSnapshot CreateCasterBodyHitVolumeSnapshot(
            PreviewActiveHitTrace active,
            int frame)
        {
            CharacterActionHitVolumeConfig hitVolume = active.HitVolume;
            Vector3 localOffset = new Vector3(
                hitVolume.BodyOffsetX,
                hitVolume.BodyOffsetY,
                hitVolume.BodyOffsetZ);
            Vector3 center = _previewInstance.transform.position
                + Vector3.up * PreviewCasterBodyCenterY
                + _previewInstance.transform.rotation * localOffset;
            float radius = Mathf.Max(0f, PreviewCasterBodyRadius * hitVolume.BodyRadiusScale + hitVolume.BodyRadiusAdd);
            float height = Mathf.Max(0f, PreviewCasterBodyHeight * hitVolume.BodyHeightScale + hitVolume.BodyHeightAdd);
            float segmentHalf = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 offset = Vector3.up * segmentHalf;
            return new CharacterActionPreviewHitVolumeSnapshot(
                active.ActionId,
                active.StableEventId,
                frame,
                center - offset,
                center + offset,
                radius,
                CharacterActionHitVolumeShapeKind.Capsule,
                CharacterActionHitVolumeSourceKind.CasterBody);
        }

        private void DrawHitVolumeSnapshot(CharacterActionPreviewHitVolumeSnapshot snapshot)
        {
            Vector3 pointA = snapshot.PointA;
            Vector3 pointB = snapshot.PointB;
            float radius = Mathf.Max(0.001f, snapshot.Radius);
            Vector3 axis = pointB - pointA;
            Vector3 normal = axis.sqrMagnitude > 0.000001f ? axis.normalized : Vector3.up;
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 0.000001f)
                tangent = Vector3.Cross(normal, Vector3.right);
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

            Handles.color = new Color(0.25f, 0.95f, 1f, 0.95f);
            DrawSceneLine(4f, pointA, pointB);
            Handles.DrawWireDisc(pointA, normal, radius);
            Handles.DrawWireDisc(pointB, normal, radius);
            Handles.DrawLine(pointA + tangent * radius, pointB + tangent * radius);
            Handles.DrawLine(pointA - tangent * radius, pointB - tangent * radius);
            Handles.DrawLine(pointA + bitangent * radius, pointB + bitangent * radius);
            Handles.DrawLine(pointA - bitangent * radius, pointB - bitangent * radius);

            Handles.color = new Color(0.45f, 1f, 1f, 1f);
            Handles.SphereHandleCap(0, pointA, Quaternion.identity, radius * 0.35f, EventType.Repaint);
            Handles.SphereHandleCap(0, pointB, Quaternion.identity, radius * 0.45f, EventType.Repaint);
            Handles.Label(
                pointB + Vector3.up * (radius + 0.04f),
                (snapshot.SourceKind == CharacterActionHitVolumeSourceKind.CasterBody
                    ? "身体撞击判定 "
                    : "碰撞框判定 ")
                + EmptyAsDash(snapshot.StableEventId));
        }


        private void StartPreviewProjectile(CharacterActionGameplayRequest request)
        {
            if ((request.RequestId <= 0))
                return;

            EnsurePreviewAuthoringResources();
            if (!_previewProjectileSpawnProfiles.TryGet(
                    request.RequestId,
                    out CombatProjectileSpawnProfile spawnProfile))
            {
                // Ordinary gameplay requests are not projectile preview requests.
                return;
            }
            if (!_previewProjectileDefinitions.TryGet(spawnProfile.ProjectileDefinitionId, out CombatProjectileDefinition definition)
                || definition == null)
            {
                _logger.Warning(
                    "CharacterActionPreview.Projectile",
                    "投射物请求找不到对应定义：" + request.RequestId);
                return;
            }

            ResourceKey prefabKey = default;
            if (_previewProjectilePresentation.TryGetV2(
                    definition.DefinitionId,
                    out CombatProjectileUnityPresentationBindingV2 binding))
            {
                prefabKey = binding.FlightBodyResource;
            }
            else
            {
                _logger.Warning(
                    "CharacterActionPreview.Projectile",
                    "投射物定义找不到对应表现绑定："
                    + definition.DefinitionId);
            }

            PreviewCastAnchor anchor = ResolvePreviewProjectileAnchor(request, spawnProfile, definition);
            string originLabel = FormatPreviewProjectileOriginLabel(spawnProfile);
            float range = ResolvePreviewProjectileAimRange(spawnProfile, definition);
            Vector3 targetPoint = anchor.Position + anchor.Forward * range;
            Vector3 direction = SafeDirection(targetPoint - anchor.Position);
            int effectiveLifetimeFrames = ResolvePreviewProjectileEffectiveLifetimeFrames(
                definition,
                anchor.Position,
                targetPoint);
            if (definition.Motion.Kind == CombatProjectileMotionKind.BallisticArcToTarget
                && TrySolvePreviewLaunchArc(
                    anchor.Position,
                    targetPoint,
                    ToFloat(definition.Motion.SpeedPerFrame),
                    ToFloat(definition.Motion.GravityPerFrameSquared),
                    ToFloat(definition.Motion.MaxLaunchAngleTangent),
                    definition.Motion.LifetimeFrames,
                    out Vector3 previewInitialVelocity,
                    out float launchSpeedPerFrame,
                    out int ballisticLifetimeFrames))
            {
                direction = SafeDirection(previewInitialVelocity);
                effectiveLifetimeFrames = ballisticLifetimeFrames;
            }
            else
            {
                launchSpeedPerFrame = 0f;
            }

            if (spawnProfile != null
                && (spawnProfile.Aim.Kind == CombatProjectileAimKind.LockedTargetCenter
                    || spawnProfile.Aim.Kind == CombatProjectileAimKind.LockedTargetOrCasterForwardRange))
            {
                _logger.Info(
                    "CharacterActionPreview.Projectile",
                    "锁定目标瞄准需要运行时目标锁定状态；编辑预览使用正前方回退路径。request="
                    + request.RequestId);
            }
            GameObject view = TryCreatePreviewView(
                prefabKey,
                PreviewProjectileNamePrefix + SafePreviewNameFragment(request.RequestId),
                out ResourceHandle<GameObject> handle,
                "投射物 " + EmptyAsDash(request.RequestId));
            if (view != null)
                ApplyPreviewViewPose(view, anchor.Position, direction);

            _activeProjectilePreviews.Add(new PreviewActiveProjectile(
                _nextPreviewVisualId++,
                request.RequestId,
                definition.DefinitionId,
                prefabKey,
                request.Metadata.LocalFrame,
                effectiveLifetimeFrames,
                ToFloat(definition.Motion.SpeedPerFrame),
                launchSpeedPerFrame,
                definition.Motion.Kind,
                definition.Motion.SpeedProfileKind,
                ToFloat(definition.Motion.HomingTurnRatePerFrame),
                ToFloat(definition.Motion.ArcHeight),
                ToFloat(definition.Motion.GravityPerFrameSquared),
                ToFloat(definition.Motion.LateralAmplitude),
                definition.Motion.LateralCycles,
                ToVector3(definition.Motion.BezierControlOffsetA),
                ToVector3(definition.Motion.BezierControlOffsetB),
                ToFloat(definition.Collision.Radius),
                definition.Impact.PolicyKind == CombatProjectileImpactPolicyKind.AreaPulseOnImpact,
                ToFloat(definition.Impact.AreaRadius),
                anchor.Position,
                direction,
                targetPoint,
                originLabel,
                view,
                handle,
                view != null));
        }

        private static float ResolvePreviewProjectileAimRange(
            CombatProjectileSpawnProfile spawnProfile,
            CombatProjectileDefinition definition)
        {
            if (definition != null && definition.Motion.Kind == CombatProjectileMotionKind.BallisticArcToTarget)
            {
                float ballisticRange = ResolvePreviewBallisticMaxRange(
                    ToFloat(definition.Motion.SpeedPerFrame),
                    ToFloat(definition.Motion.GravityPerFrameSquared),
                    ToFloat(definition.Motion.MaxLaunchAngleTangent));
                return ballisticRange;
            }

            if (spawnProfile != null && spawnProfile.Aim.MaxRange > Fix64.Zero)
                return ToFloat(spawnProfile.Aim.MaxRange);
            return ToFloat(definition.Motion.SpeedPerFrame) * definition.Motion.LifetimeFrames;
        }

        private static int ResolvePreviewProjectileEffectiveLifetimeFrames(
            CombatProjectileDefinition definition,
            Vector3 origin,
            Vector3 targetPoint)
        {
            if (definition == null)
                return 1;

            if (definition.Motion.Kind == CombatProjectileMotionKind.BallisticArcToTarget
                && TrySolvePreviewLaunchArc(
                    origin,
                    targetPoint,
                    ToFloat(definition.Motion.SpeedPerFrame),
                    ToFloat(definition.Motion.GravityPerFrameSquared),
                    ToFloat(definition.Motion.MaxLaunchAngleTangent),
                    definition.Motion.LifetimeFrames,
                    out _,
                    out _,
                    out int ballisticLifetimeFrames))
            {
                return ballisticLifetimeFrames;
            }

            float speed = Mathf.Max(0.0001f, ToFloat(definition.Motion.SpeedPerFrame));
            float distance = Vector3.Distance(origin, targetPoint);
            return Mathf.Max(1, Mathf.CeilToInt(distance / speed));
        }

        private static Vector3 ResolvePreviewProjectilePosition(
            PreviewActiveProjectile active,
            int ageFrames)
        {
            if (active == null)
                return Vector3.zero;

            if (active.MotionKind == CombatProjectileMotionKind.ArcToTarget)
                return ResolvePreviewArcPosition(
                    active.Origin,
                    active.TargetPoint,
                    active.ArcHeight,
                    ageFrames,
                    active.LifetimeFrames,
                    active.SpeedProfileKind);

            if (active.MotionKind == CombatProjectileMotionKind.BallisticArcToTarget)
                return ResolvePreviewBallisticArcPosition(
                    active.Origin,
                    active.Direction,
                    active.TargetPoint,
                    active.LaunchSpeedPerFrame > 0f ? active.LaunchSpeedPerFrame : active.SpeedPerFrame,
                    active.GravityPerFrameSquared,
                    ageFrames,
                    active.LifetimeFrames);

            if (active.MotionKind == CombatProjectileMotionKind.BoomerangReturn)
                return ResolvePreviewBoomerangPosition(
                    active.Origin,
                    active.TargetPoint,
                    ageFrames,
                    active.LifetimeFrames,
                    active.SpeedProfileKind);

            if (active.MotionKind == CombatProjectileMotionKind.SwayForward)
                return ResolvePreviewSwayPosition(
                    active.Origin,
                    active.TargetPoint,
                    active.Direction,
                    active.LateralAmplitude,
                    active.LateralCycles,
                    ageFrames,
                    active.LifetimeFrames,
                    active.SpeedProfileKind);

            if (active.MotionKind == CombatProjectileMotionKind.HomingToTarget)
                return ResolvePreviewHomingPosition(
                    active.Origin,
                    active.TargetPoint,
                    active.Direction,
                    active.SpeedPerFrame,
                    ageFrames,
                    active.LifetimeFrames,
                    active.SpeedProfileKind,
                    active.HomingTurnRatePerFrame);

            if (active.MotionKind == CombatProjectileMotionKind.BezierToTarget)
                return ResolvePreviewBezierPosition(
                    active.Origin,
                    active.TargetPoint,
                    active.Direction,
                    active.BezierControlOffsetA,
                    active.BezierControlOffsetB,
                    ageFrames,
                    active.LifetimeFrames,
                    active.SpeedProfileKind);

            if (active.SpeedProfileKind == CombatProjectileSpeedProfileKind.Constant)
                return active.Origin + active.Direction * active.SpeedPerFrame * Math.Max(0, ageFrames);

            float progress = EvaluatePreviewProjectileProgress(
                active.SpeedProfileKind,
                ageFrames,
                active.LifetimeFrames);
            return active.Origin + active.Direction * active.SpeedPerFrame * active.LifetimeFrames * progress;
        }

        private static Vector3 ResolvePreviewHomingPosition(
            Vector3 origin,
            Vector3 targetPoint,
            Vector3 fallbackDirection,
            float speedPerFrame,
            int ageFrames,
            int lifetimeFrames,
            CombatProjectileSpeedProfileKind speedProfileKind,
            float homingTurnRatePerFrame)
        {
            int life = Mathf.Max(1, lifetimeFrames);
            int clampedAge = Mathf.Clamp(ageFrames, 0, life);
            Vector3 position = origin;
            Vector3 direction = SafeDirection(fallbackDirection);
            float turnRate = Mathf.Clamp01(homingTurnRatePerFrame <= 0f ? 1f : homingTurnRatePerFrame);
            for (int frame = 1; frame <= clampedAge; frame++)
            {
                Vector3 delta = targetPoint - position;
                if (delta.sqrMagnitude > 0.0001f)
                    direction = SteerPreviewDirection(direction, delta.normalized, turnRate);

                float oldProgress = EvaluatePreviewProjectileProgress(speedProfileKind, frame - 1, life);
                float newProgress = EvaluatePreviewProjectileProgress(speedProfileKind, frame, life);
                float stepDistance = Mathf.Max(0f, speedPerFrame) * life * Mathf.Max(0f, newProgress - oldProgress);
                position += direction * stepDistance;
            }

            return position;
        }

        private static Vector3 SteerPreviewDirection(Vector3 currentDirection, Vector3 desiredDirection, float turnRatePerFrame)
        {
            Vector3 current = SafeDirection(currentDirection);
            Vector3 desired = SafeDirection(desiredDirection);
            float rate = Mathf.Clamp01(turnRatePerFrame);
            if (rate >= 1f)
                return desired;
            if (rate <= 0f)
                return current;

            Vector3 blended = Vector3.LerpUnclamped(current, desired, rate);
            if (blended.sqrMagnitude > 0.0001f)
                return blended.normalized;

            Vector3 fallback = new Vector3(current.z, 0f, -current.x);
            if (fallback.sqrMagnitude <= 0.0001f)
                fallback = Vector3.right;
            return Vector3.LerpUnclamped(current, fallback.normalized, rate).normalized;
        }

        private static Vector3 ResolvePreviewBoomerangPosition(
            Vector3 origin,
            Vector3 targetPoint,
            int ageFrames,
            int lifetimeFrames,
            CombatProjectileSpeedProfileKind speedProfileKind)
        {
            float progress = EvaluatePreviewProjectileProgress(speedProfileKind, ageFrames, lifetimeFrames);
            float legProgress = progress <= 0.5f ? progress * 2f : (1f - progress) * 2f;
            return Vector3.LerpUnclamped(origin, targetPoint, legProgress);
        }

        private static Vector3 ResolvePreviewSwayPosition(
            Vector3 origin,
            Vector3 targetPoint,
            Vector3 direction,
            float lateralAmplitude,
            int lateralCycles,
            int ageFrames,
            int lifetimeFrames,
            CombatProjectileSpeedProfileKind speedProfileKind)
        {
            float progress = EvaluatePreviewProjectileProgress(speedProfileKind, ageFrames, lifetimeFrames);
            Vector3 center = Vector3.LerpUnclamped(origin, targetPoint, progress);
            Vector3 right = ResolvePreviewPlanarRight(direction);
            float sway = EvaluatePreviewSignedTriangleWave(progress, lateralCycles) * Mathf.Max(0f, lateralAmplitude);
            return center + right * sway;
        }

        private static Vector3 ResolvePreviewArcPosition(
            Vector3 origin,
            Vector3 targetPoint,
            float arcHeight,
            int ageFrames,
            int lifetimeFrames,
            CombatProjectileSpeedProfileKind speedProfileKind)
        {
            int life = Math.Max(1, lifetimeFrames);
            float t = EvaluatePreviewProjectileProgress(speedProfileKind, ageFrames, life);
            Vector3 linear = Vector3.LerpUnclamped(origin, targetPoint, t);
            float verticalOffset = 4f * Mathf.Max(0f, arcHeight) * t * (1f - t);
            return linear + Vector3.up * verticalOffset;
        }

        private static Vector3 ResolvePreviewBezierPosition(
            Vector3 origin,
            Vector3 targetPoint,
            Vector3 direction,
            Vector3 controlOffsetA,
            Vector3 controlOffsetB,
            int ageFrames,
            int lifetimeFrames,
            CombatProjectileSpeedProfileKind speedProfileKind)
        {
            float t = EvaluatePreviewProjectileProgress(speedProfileKind, ageFrames, lifetimeFrames);
            float inverse = 1f - t;
            Vector3 p0 = origin;
            Vector3 p1 = origin + ResolvePreviewProjectileLocalOffset(direction, controlOffsetA);
            Vector3 p2 = targetPoint + ResolvePreviewProjectileLocalOffset(direction, controlOffsetB);
            Vector3 p3 = targetPoint;
            return p0 * (inverse * inverse * inverse)
                + p1 * (3f * inverse * inverse * t)
                + p2 * (3f * inverse * t * t)
                + p3 * (t * t * t);
        }

        private static Vector3 ResolvePreviewBallisticArcPosition(
            Vector3 origin,
            Vector3 direction,
            Vector3 targetPoint,
            float speedPerFrame,
            float gravityPerFrameSquared,
            int ageFrames,
            int lifetimeFrames)
        {
            int life = Math.Max(1, lifetimeFrames);
            int clampedAge = Mathf.Clamp(ageFrames, 0, life);
            if (clampedAge >= life)
                return targetPoint;

            float time = clampedAge;
            Vector3 gravity = new Vector3(0f, -Mathf.Max(0f, gravityPerFrameSquared), 0f);
            Vector3 initialVelocity = SafeDirection(direction) * Mathf.Max(0f, speedPerFrame);
            return origin + initialVelocity * time + gravity * (0.5f * time * time);
        }

        private static float ResolvePreviewBallisticMaxRange(
            float speedPerFrame,
            float gravityPerFrameSquared,
            float maxLaunchAngleTangent)
        {
            if (speedPerFrame <= 0f || gravityPerFrameSquared <= 0f)
                return 0f;
            float unconstrained = speedPerFrame * speedPerFrame / gravityPerFrameSquared;
            if (maxLaunchAngleTangent <= 0f || maxLaunchAngleTangent >= 1f)
                return unconstrained;

            float tangentSquared = maxLaunchAngleTangent * maxLaunchAngleTangent;
            float factor = 2f * maxLaunchAngleTangent / (1f + tangentSquared);
            return unconstrained * factor;
        }

        private static bool TrySolvePreviewLaunchArc(
            Vector3 origin,
            Vector3 targetPoint,
            float maxSpeedPerFrame,
            float gravityPerFrameSquared,
            float maxLaunchAngleTangent,
            int maxLifetimeFrames,
            out Vector3 initialVelocity,
            out float launchSpeedPerFrame,
            out int effectiveLifetimeFrames)
        {
            initialVelocity = Vector3.zero;
            launchSpeedPerFrame = 0f;
            effectiveLifetimeFrames = 0;
            if (maxSpeedPerFrame <= 0f || gravityPerFrameSquared <= 0f || maxLifetimeFrames <= 0)
                return false;

            Vector3 displacement = targetPoint - origin;
            Vector3 horizontal = new Vector3(displacement.x, 0f, displacement.z);
            float horizontalDistance = horizontal.magnitude;
            if (horizontalDistance <= 0.0001f)
                return false;

            float maxSpeedSquared = maxSpeedPerFrame * maxSpeedPerFrame;
            float heightReach = maxSpeedSquared - 2f * gravityPerFrameSquared * displacement.y;
            if (heightReach <= 0.000001f)
                return false;

            float rangeOptimalTangent = maxSpeedPerFrame / Mathf.Sqrt(heightReach);
            float tanTheta = maxLaunchAngleTangent > 0f && maxLaunchAngleTangent < rangeOptimalTangent
                ? maxLaunchAngleTangent
                : rangeOptimalTangent;
            if (tanTheta <= 0.0001f)
                return false;

            float denominator = 2f * (horizontalDistance * tanTheta - displacement.y);
            if (denominator <= 0.0001f)
                return false;

            float tangentSquared = tanTheta * tanTheta;
            float requiredSpeedSquared =
                gravityPerFrameSquared
                * horizontalDistance
                * horizontalDistance
                * (1f + tangentSquared)
                / denominator;
            if (requiredSpeedSquared <= 0f || requiredSpeedSquared > maxSpeedSquared + 0.000001f)
                return false;

            launchSpeedPerFrame = Mathf.Sqrt(requiredSpeedSquared);
            if (launchSpeedPerFrame <= 0.0001f || launchSpeedPerFrame > maxSpeedPerFrame + 0.0001f)
                return false;

            float horizontalSpeed = launchSpeedPerFrame / Mathf.Sqrt(1f + tangentSquared);
            if (horizontalSpeed <= 0.0001f)
                return false;

            initialVelocity = horizontal.normalized * horizontalSpeed + Vector3.up * (horizontalSpeed * tanTheta);
            effectiveLifetimeFrames = Mathf.CeilToInt(horizontalDistance / horizontalSpeed);
            return effectiveLifetimeFrames > 0 && effectiveLifetimeFrames <= maxLifetimeFrames;
        }

        private static Vector3 ResolvePreviewProjectilePosition(
            CharacterActionPreviewProjectileSnapshot snapshot,
            int ageFrames)
        {
            if (snapshot.MotionKind == CombatProjectileMotionKind.ArcToTarget)
            {
                return ResolvePreviewArcPosition(
                    snapshot.Origin,
                    snapshot.TargetPoint,
                    snapshot.ArcHeight,
                    ageFrames,
                    Math.Max(1, snapshot.EndFrame - snapshot.StartFrame + 1),
                    snapshot.SpeedProfileKind);
            }

            int lifetimeFrames = Math.Max(1, snapshot.EndFrame - snapshot.StartFrame + 1);
            if (snapshot.MotionKind == CombatProjectileMotionKind.BallisticArcToTarget)
            {
                return ResolvePreviewBallisticArcPosition(
                    snapshot.Origin,
                    snapshot.Direction,
                    snapshot.TargetPoint,
                    snapshot.LaunchSpeedPerFrame > 0f ? snapshot.LaunchSpeedPerFrame : snapshot.SpeedPerFrame,
                    snapshot.GravityPerFrameSquared,
                    ageFrames,
                    lifetimeFrames);
            }

            if (snapshot.MotionKind == CombatProjectileMotionKind.BoomerangReturn)
            {
                return ResolvePreviewBoomerangPosition(
                    snapshot.Origin,
                    snapshot.TargetPoint,
                    ageFrames,
                    lifetimeFrames,
                    snapshot.SpeedProfileKind);
            }

            if (snapshot.MotionKind == CombatProjectileMotionKind.SwayForward)
            {
                return ResolvePreviewSwayPosition(
                    snapshot.Origin,
                    snapshot.TargetPoint,
                    snapshot.Direction,
                    snapshot.LateralAmplitude,
                    snapshot.LateralCycles,
                    ageFrames,
                    lifetimeFrames,
                    snapshot.SpeedProfileKind);
            }

            if (snapshot.MotionKind == CombatProjectileMotionKind.HomingToTarget)
            {
                return ResolvePreviewHomingPosition(
                    snapshot.Origin,
                    snapshot.TargetPoint,
                    snapshot.Direction,
                    snapshot.SpeedPerFrame,
                    ageFrames,
                    lifetimeFrames,
                    snapshot.SpeedProfileKind,
                    snapshot.HomingTurnRatePerFrame);
            }

            if (snapshot.MotionKind == CombatProjectileMotionKind.BezierToTarget)
            {
                return ResolvePreviewBezierPosition(
                    snapshot.Origin,
                    snapshot.TargetPoint,
                    snapshot.Direction,
                    snapshot.BezierControlOffsetA,
                    snapshot.BezierControlOffsetB,
                    ageFrames,
                    lifetimeFrames,
                    snapshot.SpeedProfileKind);
            }

            if (snapshot.SpeedProfileKind == CombatProjectileSpeedProfileKind.Constant)
                return snapshot.Origin + snapshot.Direction * snapshot.SpeedPerFrame * Math.Max(0, ageFrames);

            float progress = EvaluatePreviewProjectileProgress(
                snapshot.SpeedProfileKind,
                ageFrames,
                lifetimeFrames);
            return snapshot.Origin + snapshot.Direction * snapshot.SpeedPerFrame * lifetimeFrames * progress;
        }

        private static float EvaluatePreviewProjectileProgress(
            CombatProjectileSpeedProfileKind kind,
            int ageFrames,
            int lifetimeFrames)
        {
            float t = Mathf.Clamp01(Mathf.Max(0, ageFrames) / (float)Math.Max(1, lifetimeFrames));
            switch (kind)
            {
                case CombatProjectileSpeedProfileKind.EaseInQuadratic:
                    return t * t;
                case CombatProjectileSpeedProfileKind.EaseOutQuadratic:
                {
                    float inverse = 1f - t;
                    return 1f - inverse * inverse;
                }
                case CombatProjectileSpeedProfileKind.EaseInOutSmoothstep:
                    return t * t * (3f - 2f * t);
                case CombatProjectileSpeedProfileKind.Constant:
                default:
                    return t;
            }
        }

        private static Vector3 ResolvePreviewPlanarRight(Vector3 direction)
        {
            Vector3 right = new Vector3(direction.z, 0f, -direction.x);
            return right.sqrMagnitude > 0.000001f ? right.normalized : Vector3.right;
        }

        private static Vector3 ResolvePreviewProjectileLocalOffset(Vector3 direction, Vector3 localOffset)
        {
            Vector3 forward = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.forward;
            return ResolvePreviewPlanarRight(forward) * localOffset.x
                + Vector3.up * localOffset.y
                + forward * localOffset.z;
        }

        private static float EvaluatePreviewSignedTriangleWave(float progress, int cycles)
        {
            if (cycles <= 0)
                return 0f;

            float phase = Mathf.Repeat(Mathf.Clamp01(progress) * cycles, 1f);
            if (phase <= 0.25f)
                return phase * 4f;
            if (phase <= 0.75f)
                return 2f - phase * 4f;
            return phase * 4f - 4f;
        }

        private void UpdatePreviewPresentation(int frame, bool sampleBladeTrails = false)
        {
            UpdatePreviewVfx(frame, sampleBladeTrails);
            UpdatePreviewProjectiles(frame);
        }


        private void UpdatePreviewProjectiles(int frame)
        {
            _removePreviewIndices.Clear();
            _projectilePreviewSnapshots.Clear();
            for (int i = 0; i < _activeProjectilePreviews.Count; i++)
            {
                PreviewActiveProjectile active = _activeProjectilePreviews[i];
                int endFrame = active.StartFrame + Math.Max(1, active.LifetimeFrames) - 1;
                if (frame < active.StartFrame || frame > endFrame)
                {
                    _removePreviewIndices.Add(i);
                    continue;
                }

                int ageFrames = Math.Max(0, frame - active.StartFrame);
                Vector3 position = ResolvePreviewProjectilePosition(active, ageFrames);
                if (active.View != null)
                    ApplyPreviewViewPose(active.View, position, active.Direction);
                _projectilePreviewSnapshots.Add(new CharacterActionPreviewProjectileSnapshot(
                    active.VisualId,
                    active.RequestId,
                    active.DefinitionId,
                    active.PrefabResource,
                    active.StartFrame,
                    endFrame,
                    active.Origin,
                    active.TargetPoint,
                    position,
                    active.Direction,
                    active.SpeedPerFrame,
                    active.LaunchSpeedPerFrame,
                    active.MotionKind,
                    active.SpeedProfileKind,
                    active.HomingTurnRatePerFrame,
                    active.ArcHeight,
                    active.GravityPerFrameSquared,
                    active.LateralAmplitude,
                    active.LateralCycles,
                    active.BezierControlOffsetA,
                    active.BezierControlOffsetB,
                    active.Radius,
                    active.IsAreaPolicy,
                    active.AreaRadius,
                    active.OriginLabel,
                    active.HasPrefabView));
            }

            RemovePreviewProjectilesByCollectedIndices();
        }


        private void RemovePreviewProjectilesByCollectedIndices()
        {
            for (int i = _removePreviewIndices.Count - 1; i >= 0; i--)
            {
                int index = _removePreviewIndices[i];
                if (index < 0 || index >= _activeProjectilePreviews.Count)
                    continue;
                ReleasePreviewObject(_activeProjectilePreviews[index].View, _activeProjectilePreviews[index].Handle);
                _activeProjectilePreviews.RemoveAt(index);
            }

            _removePreviewIndices.Clear();
        }

        private void ClearPreviewPresentation()
        {
            for (int i = 0; i < _activeVfxPreviews.Count; i++)
                ReleasePreviewObject(_activeVfxPreviews[i].View, _activeVfxPreviews[i].Handle);
            for (int i = 0; i < _activeProjectilePreviews.Count; i++)
                ReleasePreviewObject(_activeProjectilePreviews[i].View, _activeProjectilePreviews[i].Handle);

            _activeVfxPreviews.Clear();
            _activeProjectilePreviews.Clear();
            _vfxPreviewSnapshots.Clear();
            _projectilePreviewSnapshots.Clear();
            _removePreviewIndices.Clear();
        }


        private GameObject TryCreatePreviewView(
            ResourceKey prefabKey,
            string viewName,
            out ResourceHandle<GameObject> handle,
            string diagnosticContext)
        {
            handle = null;
            IVfxUnityPrefabBindingSource editorBindings = VfxUnityPrefabBindingSourceRegistry.Current;
            if (editorBindings != null
                && editorBindings.TryResolve(prefabKey, out VfxUnityPrefabBinding editorBinding)
                && editorBinding?.Prefab != null)
            {
                GameObject editorView = InstantiatePreviewObject(
                    editorBinding.Prefab,
                    Vector3.zero,
                    Quaternion.identity);
                editorView.name = string.IsNullOrWhiteSpace(viewName)
                    ? LegacyPreviewObjectNamePrefix
                    : viewName;
                editorView.hideFlags = PreviewHideFlags;
                editorView.SetActive(true);
                PlayParticlesWithoutLifecycle(editorView);
                return editorView;
            }

            if (!prefabKey.IsValid || _resourceManager == null)
            {
                _logger.Warning(
                    "CharacterActionPreview.Resources",
                    diagnosticContext + " 没有可用 Prefab 资源，改用辅助图形预览。");
                return null;
            }

            ResourceLoadResult<ResourceHandle<GameObject>> loaded = _resourceManager.Load<GameObject>(prefabKey);
            if (!loaded.Success || loaded.Value == null || loaded.Value.Value == null)
            {
                _logger.Warning(
                    "CharacterActionPreview.Resources",
                    diagnosticContext + " Prefab 加载失败：" + loaded.Error);
                return null;
            }

            handle = loaded.Value;
            GameObject view = InstantiatePreviewObject(handle.Value, Vector3.zero, Quaternion.identity);
            view.name = string.IsNullOrWhiteSpace(viewName) ? LegacyPreviewObjectNamePrefix : viewName;
            view.hideFlags = PreviewHideFlags;
            view.SetActive(true);
            PlayParticlesWithoutLifecycle(view);
            return view;
        }

        private void ReleasePreviewObject(GameObject view, ResourceHandle<GameObject> handle)
        {
            if (view != null)
            {
                // Non-ExecuteAlways behaviours need explicit teardown in the isolated Edit Mode host.
                view.GetComponent<UnityBladeTrailBackend>()?.Dispose();
                DestroyOwnedPreviewGameObject(view);
            }

            if (handle != null && _resourceManager != null)
                _resourceManager.Release(handle);
        }

        private static void ApplyPreviewViewPose(GameObject view, Vector3 position, Vector3 forward)
        {
            if (view == null)
                return;

            Quaternion rotation = Quaternion.LookRotation(SafeDirection(forward), Vector3.up);
            view.transform.SetPositionAndRotation(position, rotation);
        }

        private PreviewCastAnchor ResolvePreviewCastAnchor()
        {
            if (_previewInstance == null)
                return new PreviewCastAnchor(Vector3.up * 1.15f + Vector3.forward * 0.65f, Vector3.forward);

            Transform root = _previewInstance.transform;
            Vector3 forward = SafeDirection(root.forward);
            return new PreviewCastAnchor(
                root.position + forward * 0.65f + Vector3.up * 1.15f,
                forward);
        }

        private PreviewCastAnchor ResolvePreviewProjectileAnchor(
            CharacterActionGameplayRequest request,
            CombatProjectileSpawnProfile spawnProfile,
            CombatProjectileDefinition definition)
        {
            if (spawnProfile == null)
                return ResolvePreviewCastAnchor();

            Vector3 local = ToVector3(spawnProfile.Origin.FallbackRootOffset + spawnProfile.Origin.LocalOffset);
            if (_previewInstance == null)
                return new PreviewCastAnchor(local, Vector3.forward);

            Transform root = _previewInstance.transform;
            Vector3 forward = SafeDirection(root.forward);
            return new PreviewCastAnchor(root.position + root.rotation * local, forward);
        }

        private static string FormatPreviewProjectileOriginLabel(CombatProjectileSpawnProfile spawnProfile)
        {
            if (spawnProfile == null)
                return "发射起点：缺少 Spawn Profile，预览使用默认施法点";

            string socketId = spawnProfile.Origin.SocketLabel;
            string socketName = FormatPreviewSocketDisplayName(socketId);
            return "发射起点："
                + socketName
                + " / "
                + EmptyAsDash(socketId)
                + " / 预览使用 fallback root offset";
        }

        private static string FormatPreviewSocketDisplayName(string socketId)
        {
            if (string.Equals(socketId, "mainHand", StringComparison.Ordinal))
                return "主手";
            if (string.Equals(socketId, "offHand", StringComparison.Ordinal))
                return "副手";
            if (string.Equals(socketId, "back", StringComparison.Ordinal))
                return "背部";
            if (string.Equals(socketId, "headVfx", StringComparison.Ordinal))
                return "头部特效";
            return string.IsNullOrWhiteSpace(socketId) ? "未知 Socket" : socketId;
        }


        private void DrawProjectilePreviewSnapshot(CharacterActionPreviewProjectileSnapshot snapshot)
        {
            Vector3 end = snapshot.TargetPoint;
            Handles.color = snapshot.HasPrefabView
                ? new Color(1f, 0.45f, 0.12f, 0.9f)
                : new Color(1f, 0.45f, 0.12f, 0.45f);
            FillProjectilePathPoints(snapshot);
            Handles.DrawAAPolyLine(3f, _projectilePathPoints);
            DrawProjectileSpeedProfileTicks(snapshot);
            Handles.SphereHandleCap(0, snapshot.Position, Quaternion.identity, Mathf.Max(0.08f, snapshot.Radius), EventType.Repaint);
            Handles.DrawWireDisc(snapshot.Position, Vector3.up, Mathf.Max(0.02f, snapshot.Radius));
            if (snapshot.IsAreaPolicy)
            {
                Handles.color = new Color(1f, 0.75f, 0.2f, 0.45f);
                Handles.DrawWireDisc(end, Vector3.up, Mathf.Max(0.02f, snapshot.AreaRadius));
            }

            Handles.Label(
                snapshot.Origin + Vector3.up * 0.18f,
                EmptyAsDash(snapshot.OriginLabel));
            Handles.Label(
                snapshot.Position + Vector3.up * (Mathf.Max(0.12f, snapshot.Radius) + 0.08f),
                "投射物预览：" + EmptyAsDash(snapshot.RequestId)
                + "\n定义=" + snapshot.DefinitionId
                + "\n运动=" + CharacterActionProjectileInspectorViewModel.FormatMotionKind(snapshot.MotionKind.ToString())
                + "\n速度曲线=" + CharacterActionProjectileInspectorViewModel.FormatSpeedProfileKind(snapshot.SpeedProfileKind.ToString())
                + (snapshot.HasPrefabView ? "\nPrefab 显示" : "\nGizmo 显示")
                + "\n只读；请在 Projectile Authoring 编辑"
                + (snapshot.MotionKind == CombatProjectileMotionKind.BezierToTarget ? "\nBezier 控制点使用局部偏移：X右 / Y上 / Z前" : string.Empty));
        }

        private void FillProjectilePathPoints(CharacterActionPreviewProjectileSnapshot snapshot)
        {
            int lifetimeFrames = Math.Max(1, snapshot.EndFrame - snapshot.StartFrame + 1);
            for (int i = 0; i <= ProjectilePathSegmentCount; i++)
            {
                int age = (int)Math.Round(lifetimeFrames * (i / (float)ProjectilePathSegmentCount));
                _projectilePathPoints[i] = ResolvePreviewProjectilePosition(snapshot, age);
            }
        }

        private static void DrawProjectileSpeedProfileTicks(
            CharacterActionPreviewProjectileSnapshot snapshot)
        {
            int lifetimeFrames = Math.Max(1, snapshot.EndFrame - snapshot.StartFrame + 1);
            Handles.color = new Color(1f, 0.9f, 0.35f, 0.55f);
            for (int i = 1; i <= 4; i++)
            {
                int age = Mathf.Clamp(
                    Mathf.RoundToInt(lifetimeFrames * (i / 5f)),
                    0,
                    lifetimeFrames);
                Vector3 position = ResolvePreviewProjectilePosition(snapshot, age);
                float size = Mathf.Max(0.035f, HandleUtility.GetHandleSize(position) * 0.035f);
                Handles.SphereHandleCap(0, position, Quaternion.identity, size, EventType.Repaint);
            }
        }

        private static void PlayParticlesWithoutLifecycle(GameObject view)
        {
            if (view == null)
                return;

            if (view.GetComponent<UnityVfxLifecycleController>() != null)
                return;

            ParticleSystem[] systems = view.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            for (int i = 0; i < systems.Length; i++)
                systems[i].Play(withChildren: false);
        }

        private static void StopParticles(GameObject view)
        {
            if (view == null)
                return;

            ParticleSystem[] systems = view.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            for (int i = 0; i < systems.Length; i++)
                systems[i].Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void BeginFrameDispatch()
        {
            _lastMotionRequests.Clear();
            _lastCombatRequests.Clear();
            _lastGameplayRequests.Clear();
            _lastAnimationRequests.Clear();
            _lastAudioCueRequests.Clear();
            _lastVfxRequests.Clear();
            _lastCameraRequests.Clear();
            _lastUiFeedbackRequests.Clear();
            _lastDebugDispatches.Clear();
            _pendingImpulse = Vector3.zero;
        }

        private void ApplyPreviewMotion(int frame, float deltaTime)
        {
            if (_previewInstance == null)
                return;

            bool ignoreGravity = false;
            bool hasVerticalVelocity = false;
            float verticalVelocity = 0f;
            float impulseY = 0f;
            MotionTrackEvent[] motionEvents = _previewAction?.MotionTrack?.Events;
            if (motionEvents != null)
            {
                for (int i = 0; i < motionEvents.Length; i++)
                {
                    MotionTrackEvent evt = motionEvents[i];
                    if (evt.Kind == CharacterActionTrackEventKind.ApplyImpulse && evt.Frame == frame)
                        impulseY += evt.Y;
                    if (evt.Frame > frame || evt.EndFrame < frame)
                        continue;
                    if (evt.Kind == CharacterActionTrackEventKind.SetVerticalVelocity)
                    {
                        hasVerticalVelocity = true;
                        verticalVelocity = evt.Y;
                    }
                    else if (evt.Kind == CharacterActionTrackEventKind.IgnoreGravity)
                    {
                        ignoreGravity = true;
                    }
                }
            }
            if (hasVerticalVelocity)
                _previewVelocity.y = verticalVelocity;
            if (impulseY > 0f)
            {
                if (_previewGrounded)
                    _previewVelocity.y = impulseY;
            }
            else
                _previewVelocity.y += impulseY;

            if (_pendingImpulse != Vector3.zero)
            {
                _previewVelocity.x += _pendingImpulse.x;
                _previewVelocity.z += _pendingImpulse.z;

            }
            _pendingImpulse = Vector3.zero;

            Vector3 rootMotionDelta = ResolvePreviewRootMotionDelta(frame);
            if (rootMotionDelta.y > 0f)
            {
                _previewVelocity.y = 0f;
                int scaleMilli = ignoreGravity
                    ? CharacterActionVerticalRootMotionGravity.ScaleMilli
                    : PreviewUpwardGravityScaleMilli;
                rootMotionDelta.y *= scaleMilli
                    / (float)CharacterActionVerticalRootMotionGravity.ScaleMilli;
            }
            else if (!ignoreGravity)
            {
                _previewVelocity.y = Mathf.Max(PreviewMaxFallSpeed, _previewVelocity.y + PreviewGravity * deltaTime);
            }
            Vector3 position = _previewInstance.transform.position + _previewVelocity * deltaTime;
            position += rootMotionDelta;
            if (position.y <= PreviewGroundY)
            {
                position.y = PreviewGroundY;
                _previewVelocity.y = 0f;
                _previewGrounded = true;
            }
            else
            {
                _previewGrounded = false;
            }

            _previewInstance.transform.position = position;

            if (_locomotion != null)
            {
                Vector2 planar = _previewMovementLocked
                    ? Vector2.zero
                    : new Vector2(_previewVelocity.x, _previewVelocity.z) * _previewMovementControlScale;
                _locomotion.SetExternalMotion(
                    Vector2.ClampMagnitude(planar, 1f),
                    Mathf.Clamp01(planar.magnitude));
            }
        }

        private Vector3 ResolvePreviewRootMotionDelta(int frame)
        {
            MotionTrackConfig motionTrack = _previewAction?.MotionTrack;
            if (motionTrack == null)
                return Vector3.zero;
            Vector3 delta = Vector3.zero;
            CharacterActionRootMotionConfig rootMotion = motionTrack.RootMotion;
            if (motionTrack.UsesRootMotion && rootMotion != null && rootMotion.HasFrames
                && rootMotion.TryGetDelta(frame, out float x, out _, out float z))
            {
                delta = new Vector3(x, 0f, z);
                if (rootMotion.Space == CharacterActionRootMotionSpace.Local && _previewInstance != null)
                    delta = _previewInstance.transform.TransformVector(delta);
            }
            if (motionTrack.TryGetVerticalRootMotionDelta(frame, out float y))
                delta.y += y;
            return delta;
        }

        private void ApplyRuntimeBoneBindings()
        {
            if (_boneSync != null)
                _boneSync.ApplyBindings();
        }

        private void ResetPreviewSimulationState()
        {
            BeginFrameDispatch();
            ClearPreviewPresentation();
            _activeHitTraces.Clear();
            _weaponTraceSnapshots.Clear();
            _hitVolumeSnapshots.Clear();
            _previewVelocity = Vector3.zero;
            _pendingImpulse = Vector3.zero;
            _previewMovementMode = CharacterMovementMode.Idle;
            _previewMovementLocked = false;
            _previewMovementControlScale = 1f;
            _previewRotationControlScale = 1f;
            _previewGrounded = true;
            if (_previewInstance != null)
                _previewInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (_locomotion != null)
            {
                _locomotion.ResumeLocomotionBlend();
                _locomotion.SetExternalMotion(Vector2.zero, 0f);
                _locomotion.TickRuntimeAnimation(0f, 0);
            }
            ApplyRuntimeBoneBindings();
        }

        private void LogDispatchInfo(CharacterActionTrackDispatchEvent dispatch, int frame)
        {
            var cacheKey = new PreviewDispatchTraceKey(dispatch, frame);
            if (_dispatchTraceTextCache.TryGetValue(cacheKey, out string cachedLine))
            {
                _logger.AddLine(cachedLine);
                return;
            }

            _logger.Info(
                "CharacterActionPreview.Event",
                "帧=" + frame.ToString()
                + "，轨道=" + CharacterActionAuthoringText.EnumDisplayLabel(typeof(CharacterActionTrackKind), dispatch.TrackKind.ToString())
                + "，事件=" + CharacterActionAuthoringText.EnumDisplayLabel(typeof(CharacterActionTrackEventKind), dispatch.EventKind.ToString())
                + FormatDispatchPayload(dispatch));
            _dispatchTraceTextBuildCount++;
            _dispatchTraceTextCache[cacheKey] = _logger.LastLine;
        }

        private void LogAdapterDiagnostics(CharacterActionDiagnostic[] diagnostics)
        {
            diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
            for (int i = 0; i < diagnostics.Length; i++)
            {
                CharacterActionDiagnostic diagnostic = diagnostics[i];
                LogCachedDiagnosticLine(
                    diagnostic.Severity == CharacterActionDiagnosticSeverity.Info
                        ? RuntimeLogLevel.Info
                        : RuntimeLogLevel.Warning,
                    "CharacterActionPreview.Adapter",
                    diagnostic.Code,
                    diagnostic.Message);
            }
        }

        private void LogCachedDiagnosticLine(
            RuntimeLogLevel level,
            string category,
            string code,
            string message)
        {
            var cacheKey = new PreviewDiagnosticLineKey(level, category, code, message);
            if (!_diagnosticLineCache.TryGetValue(cacheKey, out string line))
            {
                string normalizedCategory = category ?? string.Empty;
                string normalizedCode = code ?? string.Empty;
                string normalizedMessage = message ?? string.Empty;
                string separator = normalizedCode.Length > 0 && normalizedMessage.Length > 0 ? " " : string.Empty;
                line = "[" + level + "] "
                    + normalizedCategory
                    + ": "
                    + normalizedCode
                    + separator
                    + normalizedMessage;
                _diagnosticLineCache.Add(cacheKey, line);
                _diagnosticLineBuildCount++;
            }

            _logger.AddLine(line);
        }

        private void LogHitTraceDiagnostic(
            PreviewHitTraceDiagnosticKind kind,
            PreviewActiveHitTrace active,
            int firstMissingFrame,
            string clipKey = "",
            string startLocatorPath = "",
            string endLocatorPath = "",
            int startSocketId = 0,
            int endSocketId = 0)
        {
            var cacheKey = new PreviewHitTraceDiagnosticKey(
                kind,
                active.ActionId,
                active.StableEventId,
                active.EquipmentSlotId,
                active.TraceProfileId,
                clipKey,
                startLocatorPath,
                endLocatorPath, startSocketId, endSocketId);
            if (_hitTraceDiagnosticLineCache.ContainsKey(cacheKey))
                return;

            string line;
            switch (kind)
            {
                case PreviewHitTraceDiagnosticKind.MissingAnimationClip:
                    line = "[Warning] CharacterActionPreview.HitTrace: "
                        + "命中判定预览无法解析当前动画片段。action=" + EmptyAsDash(active.ActionId)
                        + "，frame=" + firstMissingFrame;
                    break;
                case PreviewHitTraceDiagnosticKind.MissingWeaponTraceProfile:
                    line = "[Warning] CharacterActionPreview.HitTrace: "
                        + "命中判定预览缺少武器绑定。trace=" + EmptyAsDash(active.TraceProfileId);
                    break;
                case PreviewHitTraceDiagnosticKind.UnresolvedWeaponLocator:
                    line = "[Warning] CharacterActionPreview.HitTrace: "
                        + "Weapon trace locator is configured but could not be resolved from the spawned weapon. "
                        + "Skipping baked fallback. trace=" + EmptyAsDash(active.TraceProfileId)
                        + " startLocator=" + EmptyAsDash(startLocatorPath)
                        + " endLocator=" + EmptyAsDash(endLocatorPath);
                    break;
                case PreviewHitTraceDiagnosticKind.MissingSocket:
                    line = "[Warning] CharacterActionPreview.HitTrace: "
                        + "烘焙挂点 Capsule 的预览挂点无法解析，已跳过该命中体。"
                        + " action=" + EmptyAsDash(active.ActionId)
                        + " startSocket=" + EmptyAsDash(startSocketId)
                        + " endSocket=" + EmptyAsDash(endSocketId)
                        + " frame=" + firstMissingFrame;
                    break;
                default:
                    line = "[Warning] CharacterActionPreview.HitTrace: "
                        + "命中判定预览缺少烘焙帧。trace=" + EmptyAsDash(active.TraceProfileId)
                        + "，clip=" + EmptyAsDash(clipKey)
                        + "，frame=" + firstMissingFrame;
                    break;
            }

            _hitTraceDiagnosticLineCache.Add(cacheKey, line);
            _hitTraceDiagnosticLineBuildCount++;
            _logger.AddPersistentLine(line);
            _hitTraceDiagnosticLogCount++;
        }

        private static string FormatDispatchPayload(CharacterActionTrackDispatchEvent dispatch)
        {
            switch (dispatch.TrackKind)
            {
                case CharacterActionTrackKind.Motion:
                    return "，模式=" + CharacterActionAuthoringText.LocalizeEnumValue(
                            typeof(CharacterMovementMode),
                            dispatch.MovementMode.ToString())
                        + "，向量=(" + dispatch.X + "," + dispatch.Y + "," + dispatch.Z + ")";
                case CharacterActionTrackKind.Combat:
                    return "，战斗动作=" + EmptyAsDash(dispatch.CombatActionId)
                        + "，装备槽位=" + EmptyAsDash(dispatch.EquipmentSlotId)
                        + "，判定=" + EmptyAsDash(dispatch.TraceProfileId);
                case CharacterActionTrackKind.Gameplay:
                    return "，请求=" + EmptyAsDash(dispatch.GameplayRequestId)
                        + "，能力=" + EmptyAsDash(dispatch.AbilityStableId);
                case CharacterActionTrackKind.Animation:
                    return "，动画=" + EmptyAsDash(dispatch.AnimationActionKey)
                        + "，片段=" + EmptyAsDash(dispatch.AnimationClipResourceKey)
                        + "，起始偏移=" + dispatch.AnimationStartOffsetFrames
                        + "，结束偏移=" + dispatch.AnimationEndOffsetFrames
                        + "，过渡=" + dispatch.TransitionSeconds;
                case CharacterActionTrackKind.Presentation:
                    return "，表现=" + EmptyAsDash(dispatch.PresentationCueId)
                        + "，资源=" + EmptyAsDash(dispatch.ResourceKey);
                case CharacterActionTrackKind.Debug:
                    return "，标记=" + EmptyAsDash(dispatch.DebugMarkerId);
                default:
                    return string.Empty;
            }
        }

        private static string EmptyAsDash(int value) => value > 0 ? value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-";

        private static string EmptyAsDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static string EmptyAsDash(CharacterActionId value)
        {
            return value.IsValid ? value.ToString() : "-";
        }

        private bool TryResolvePreviewAnimationClipKey(int frame, out string clipKey)
        {
            clipKey = string.Empty;
            if (!TryResolvePreviewAnimationClipEvent(frame, out AnimationTrackEvent animationEvent))
                return false;

            clipKey = animationEvent.EffectiveAnimationKey;
            return true;
        }

        private bool TryResolvePreviewAnimationClipEvent(int frame, out AnimationTrackEvent animationEvent)
        {
            animationEvent = default;
            AnimationTrackEvent[] events = _previewAction?.AnimationTrack?.Events ?? Array.Empty<AnimationTrackEvent>();
            for (int i = 0; i < events.Length; i++)
            {
                AnimationTrackEvent candidate = events[i];
                if (frame < candidate.Frame || frame > candidate.EndFrame)
                    continue;
                if (string.IsNullOrWhiteSpace(candidate.EffectiveAnimationKey))
                    continue;

                animationEvent = candidate;
                return true;
            }

            return false;
        }

        private bool TryResolvePreviewWeaponTraceProfile(
            int equipmentSlotId,
            int traceProfileId,
            out PreviewWeaponTraceProfile profile)
        {
            if (TryResolvePreviewWeaponTraceBinding(
                    equipmentSlotId,
                    traceProfileId,
                    out CharacterResolvedWeaponTraceBinding binding))
            {
                profile = new PreviewWeaponTraceProfile(
                    binding.Length,
                    binding.Radius,
                    binding.ProximalExtension,
                    binding.ProximalRadius);
                return true;
            }

            profile = default;
            return false;
        }

        private bool TryResolvePreviewWeaponTraceProfileIdBySlot(int equipmentSlotId, out int traceProfileId)
        {
            traceProfileId = 0;
            if ((equipmentSlotId <= 0))
                return false;

            if (!TryResolvePreviewWeaponTraceBinding(
                    equipmentSlotId,
                    explicitTraceProfileId: 0,
                    out CharacterResolvedWeaponTraceBinding binding))
            {
                return false;
            }

            traceProfileId = binding.TraceId;
            return true;
        }

        private bool TryResolvePreviewWeaponTraceBinding(
            int equipmentSlotId,
            int explicitTraceProfileId,
            out CharacterResolvedWeaponTraceBinding binding)
        {
            return CharacterWeaponTraceBindingResolver.TryResolve(
                _currentPreviewBinding.Package,
                equipmentSlotId,
                explicitTraceProfileId,
                out binding,
                out _);
        }

        private bool TryGetPreviewBakedTraceFrame(
            string clipKey,
            int traceProfileId,
            int frame,
            out PreviewBakedTraceFrame bakedFrame)
        {
            bakedFrame = default;
            if (_previewTraceBakeCatalog == null
                || !_previewTraceBakeCatalog.TryGetArtifact(clipKey, traceProfileId, out MxAnimationBakeArtifact artifact)
                || !_previewTraceBakeCatalog.TryGetFrame(clipKey, traceProfileId, frame, out MxAnimationBakedWeaponTraceFrame frameData))
            {
                return false;
            }

            bakedFrame = new PreviewBakedTraceFrame(
                ToVector3(frameData.RootPrev, artifact.Profile.QuantizationScale),
                ToVector3(frameData.TipPrev, artifact.Profile.QuantizationScale),
                ToVector3(frameData.RootNow, artifact.Profile.QuantizationScale),
                ToVector3(frameData.TipNow, artifact.Profile.QuantizationScale));
            return true;
        }

        private static CharacterWeaponTraceBakeCatalog BuildPreviewTraceBakeCatalog(
            CharacterImportedPackage package,
            CharacterActionConfigRegistry registry)
        {
            CharacterWeaponTraceBakeRequest[] requests = CreatePreviewTraceBakeRequests(package, registry);
            return CharacterWeaponTraceBakeCatalogBuilder.Build(
                package,
                requests,
                out _);
        }

        private static CharacterWeaponTraceBakeRequest[] CreatePreviewTraceBakeRequests(
            CharacterImportedPackage package,
            CharacterActionConfigRegistry registry)
        {
            CharacterActionConfig[] actions = registry?.Actions ?? Array.Empty<CharacterActionConfig>();
            if (actions.Length == 0)
                return Array.Empty<CharacterWeaponTraceBakeRequest>();

            var requests = new List<CharacterWeaponTraceBakeRequest>();
            for (int actionIndex = 0; actionIndex < actions.Length; actionIndex++)
            {
                CharacterActionConfig action = actions[actionIndex];
                if (action == null)
                    continue;

                CombatTrackEvent[] combatEvents = action.CombatTrack?.Events
                    ?? Array.Empty<CombatTrackEvent>();
                for (int eventIndex = 0; eventIndex < combatEvents.Length; eventIndex++)
                {
                    CombatTrackEvent start = combatEvents[eventIndex];
                    if (start.Kind != CharacterActionTrackEventKind.HitTraceWindow
                        || start.HitVolume.HasVolume)
                    {
                        continue;
                    }

                    int endFrame = start.EndFrame;
                    string clipKey = ResolvePreviewActiveClipKey(action, start.Frame);
                    int traceId = CharacterWeaponTraceBakeCatalogBuilder.ResolveTraceIdForEquipmentSlot(
                        package,
                        start.EquipmentSlotId,
                        start.TraceProfileId);
                    requests.Add(new CharacterWeaponTraceBakeRequest(
                        action.ActionId,
                        clipKey,
                        traceId,
                        Math.Max(0, start.Frame),
                        Math.Max(start.Frame, endFrame),
                        ResolvePreviewMaxFrame(action, endFrame),
                        "character-action-preview.action="
                        + action.ActionId
                        + "|clip="
                        + (clipKey ?? string.Empty)
                        + "|trace="
                        + (traceId.ToString(System.Globalization.CultureInfo.InvariantCulture))));
                }
            }

            return requests.ToArray();
        }

        private static string ResolvePreviewActiveClipKey(CharacterActionConfig action, int frame)
        {
            AnimationTrackEvent[] events = action.AnimationTrack?.Events
                ?? Array.Empty<AnimationTrackEvent>();
            string fallback = string.Empty;
            for (int i = 0; i < events.Length; i++)
            {
                AnimationTrackEvent candidate = events[i];
                string key = candidate.EffectiveAnimationKey;
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                if (string.IsNullOrWhiteSpace(fallback))
                    fallback = key;
                if (frame >= candidate.Frame && frame <= candidate.EndFrame)
                    return key;
            }

            return fallback;
        }

        private static int ResolvePreviewMaxFrame(CharacterActionConfig action, int endFrame)
        {
            int maxFrame = Math.Max(1, endFrame);
            if (action.DurationFrames.HasValue)
                maxFrame = Math.Max(maxFrame, action.DurationFrames.Value);

            AnimationTrackEvent[] events = action.AnimationTrack?.Events
                ?? Array.Empty<AnimationTrackEvent>();
            for (int i = 0; i < events.Length; i++)
                maxFrame = Math.Max(maxFrame, events[i].EndFrame);
            return maxFrame;
        }

        private void UpdateWeaponLocatorPoseSnapshots(PreviewBinding binding)
        {
            _weaponLocatorPoseSnapshots.Clear();
            CharacterWeaponTraceRuntimeBinding[] traces = binding.Package?.Geometry?.WeaponTraces
                ?? Array.Empty<CharacterWeaponTraceRuntimeBinding>();
            if (traces.Length == 0)
                return;

            HashSet<int> equippedTraceIds = CreateEquippedTraceIdSet(binding.Package, binding.EquippedWeapons);
            for (int i = 0; i < traces.Length; i++)
            {
                CharacterWeaponTraceRuntimeBinding trace = traces[i];
                if (trace == null || (trace.TraceId <= 0))
                    continue;
                if (equippedTraceIds.Count > 0 && !equippedTraceIds.Contains(trace.TraceId))
                    continue;

                _weaponLocatorPoseSnapshots.Add(new CharacterActionPreviewWeaponLocatorPoseSnapshot(
                    trace.TraceId,
                    trace.WeaponId,
                    trace.EquipSlot,
                    trace.StartLocatorPath,
                    trace.EndLocatorPath,
                    trace.StartPose.ParentKind,
                    trace.StartPose.ParentPath,
                    ToVector3(trace.StartPose.Position, Vector3.zero),
                    ToQuaternion(trace.StartPose.Rotation),
                    trace.EndPose.ParentKind,
                    trace.EndPose.ParentPath,
                    ToVector3(trace.EndPose.Position, Vector3.zero),
                    ToQuaternion(trace.EndPose.Rotation),
                    trace.Radius,
                    trace.SampleRule, trace.WeaponLabel, trace.EquipmentSlotLabel));
            }
        }

        // Authoring loadout labels are resolved only while preparing a new preview binding.
        private static int ResolvePreparedAuthoringId(int domainId, string label) => string.IsNullOrEmpty(label)
            ? 0 : CharacterContentIdentityAuthoringCatalog.GetExisting(domainId, label);

        private static HashSet<int> CreateEquippedTraceIdSet(
            CharacterImportedPackage package,
            EquippedWeaponSlot[] equippedWeapons)
        {
            var ids = new HashSet<int>();
            if (equippedWeapons == null)
                return ids;

            for (int i = 0; i < equippedWeapons.Length; i++)
            {
                EquippedWeaponSlot equipped = equippedWeapons[i];
                if (TryAddResolvedTraceId(package, equipped.SlotId, ids))
                    continue;
                if (TryAddResolvedTraceId(package, equipped.MountedSlotId, ids))
                    continue;

                CharacterTraceBindingEntry[] legacyBindings = equipped.Weapon?.TraceBindings
                    ?? Array.Empty<CharacterTraceBindingEntry>();
                for (int j = 0; j < legacyBindings.Length; j++)
                {
                    int traceId = ResolvePreparedAuthoringId(403, legacyBindings[j].TraceProfileId);
                    if (traceId > 0)
                        ids.Add(traceId);
                }
            }

            return ids;
        }

        private static bool TryAddResolvedTraceId(
            CharacterImportedPackage package,
            int equipmentSlotId,
            HashSet<int> ids)
        {
            if ((equipmentSlotId <= 0)
                || !CharacterWeaponTraceBindingResolver.TryResolve(
                    package,
                    equipmentSlotId,
                    explicitTraceProfileId: 0,
                    out CharacterResolvedWeaponTraceBinding binding,
                    out _)
                || (binding.TraceId <= 0))
            {
                return false;
            }

            ids.Add(binding.TraceId);
            return true;
        }

        private static Vector3 ToVector3(MxAnimationBakedVector3 value, int quantizationScale)
        {
            float scale = quantizationScale <= 0 ? 1000000f : quantizationScale;
            return new Vector3(value.X / scale, value.Y / scale, value.Z / scale);
        }

        private static Vector3 SafeDirection(Vector3 value)
        {
            return value.sqrMagnitude <= 0.000001f ? Vector3.forward : value.normalized;
        }

        private void StartPreviewHitTrace(CharacterActionCombatRequest request)
        {
            if (request.HitVolume.HasVolume)
            {
                var stop = new PreviewHitTraceStop(request.Metadata.InstanceId, 0, 0);
                RemovePreviewHitTrace(stop);
                _activeHitTraces.Add(new PreviewActiveHitTrace(
                    request.Metadata.ActionId,
                    request.Metadata.InstanceId,
                    0,
                    0,
                    request.Metadata.LocalFrame,
                    request.Metadata.StableEventId,
                    request.HitVolume));
                return;
            }

            int traceProfileId = ResolvePreviewHitTraceProfileId(request);
            if ((traceProfileId <= 0))
                return;

            RemovePreviewHitTrace(request, traceProfileId);
            _activeHitTraces.Add(new PreviewActiveHitTrace(
                request.Metadata.ActionId,
                request.Metadata.InstanceId,
                request.EquipmentSlotId,
                traceProfileId,
                request.Metadata.LocalFrame,
                request.Metadata.StableEventId,
                default));
        }

        private void StopPreviewHitTrace(CharacterActionCombatRequest request)
        {
            StopPreviewHitTrace(request, ResolvePreviewHitTraceProfileId(request));
        }

        private void StopPreviewHitTrace(CharacterActionCombatRequest request, int traceProfileId)
        {
            RemovePreviewHitTrace(request, traceProfileId);
        }

        private void RemovePreviewHitTrace(CharacterActionCombatRequest request, int traceProfileId)
        {
            RemovePreviewHitTrace(new PreviewHitTraceStop(
                request.Metadata.InstanceId,
                request.EquipmentSlotId,
                traceProfileId));
        }

        private void RemovePreviewHitTrace(PreviewHitTraceStop stop)
        {
            for (int i = _activeHitTraces.Count - 1; i >= 0; i--)
            {
                PreviewActiveHitTrace active = _activeHitTraces[i];
                if (stop.Matches(active))
                {
                    _activeHitTraces.RemoveAt(i);
                }
            }
        }

        private int ResolvePreviewHitTraceProfileId(CharacterActionCombatRequest request)
        {
            if ((request.TraceProfileId > 0))
                return request.TraceProfileId;
            return TryResolvePreviewWeaponTraceProfileIdBySlot(request.EquipmentSlotId, out int traceProfileId)
                ? traceProfileId
                : 0;
        }

        private static FixVector3 ToFixVector3(Vector3 value)
        {
            return new FixVector3(ToFix64(value.x), ToFix64(value.y), ToFix64(value.z));
        }

        private static Fix64 ToFix64(float value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }

        private static Vector3 ToVector3(FixVector3 value)
        {
            return new Vector3(ToFloat(value.X), ToFloat(value.Y), ToFloat(value.Z));
        }

        private static float ToFloat(Fix64 value)
        {
            return value.RawValue / (float)Fix64.Scale;
        }

        CharacterActionAdapterSinkResult ICharacterActionMotionRequestSink.SubmitMotionRequest(CharacterActionMotionRequest request)
        {
            _lastMotionRequests.Add(request);
            switch (request.EventKind)
            {
                case CharacterActionTrackEventKind.SetMovementMode:
                    _previewMovementMode = request.MovementMode;
                    break;
                case CharacterActionTrackEventKind.ApplyImpulse:
                    _pendingImpulse += new Vector3(request.X, 0f, request.Z);
                    if (request.MovementMode != CharacterMovementMode.Idle)
                        _previewMovementMode = request.MovementMode;
                    break;
                case CharacterActionTrackEventKind.LockMovement:
                    _previewMovementLocked = true;
                    _previewMovementControlScale = 0f;
                    _previewRotationControlScale = 1f;
                    break;
                case CharacterActionTrackEventKind.SetControlScale:
                    if (request.HasMovementControlScale)
                    {
                        _previewMovementControlScale = request.MovementControlScale;
                        _previewMovementLocked = request.MovementControlScale <= 0f;
                    }

                    if (request.HasRotationControlScale)
                        _previewRotationControlScale = request.RotationControlScale;
                    break;
            }

            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        CharacterActionAdapterSinkResult ICharacterActionCombatRequestSink.SubmitCombatRequest(CharacterActionCombatRequest request)
        {
            _lastCombatRequests.Add(request);
            switch (request.EventKind)
            {
                case CharacterActionTrackEventKind.HitTraceWindow:
                    if (request.Metadata.LifecyclePhase == CharacterActionTrackLifecyclePhase.Enter)
                        StartPreviewHitTrace(request);
                    else if (request.Metadata.LifecyclePhase == CharacterActionTrackLifecyclePhase.Exit)
                        StopPreviewHitTrace(request);
                    break;
            }

            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        CharacterActionAdapterSinkResult ICharacterActionGameplayRequestSink.SubmitGameplayRequest(CharacterActionGameplayRequest request)
        {
            _lastGameplayRequests.Add(request);
            StartPreviewProjectile(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        CharacterActionAdapterSinkResult ICharacterActionAnimationRequestSink.SubmitAnimationRequest(CharacterActionAnimationRequest request)
        {
            _lastAnimationRequests.Add(request);
            if (_locomotion == null)
            {
                return CharacterActionAdapterSinkResult.Rejected(CharacterActionDiagnostic.Warning(
                    CharacterActionDiagnosticCode.AnimationActionMissing,
                    "预览缺少 locomotion controller，动画请求已记录但不会应用。"));
            }

            string animationKey = request.EffectiveAnimationKey;
            float holdSeconds = Mathf.Max(
                request.DurationSeconds,
                request.TransitionSeconds + (1f / CharacterActionTimelineLayout.EditorTimelineFps));
            if (!_preparedPreviewAnimations.TryGetValue(request.Metadata.StableEventId, out PreparedPreviewAnimation prepared))
                return CharacterActionAdapterSinkResult.Rejected(CharacterActionDiagnostic.Warning(
                    CharacterActionDiagnosticCode.AnimationActionMissing,
                    "Preview animation was not present in the prepared action definition."));
            string correlation = request.Metadata.TraceId.IsNone
                ? prepared.DefaultCorrelation : request.Metadata.TraceId.ToString();
            bool accepted = prepared.BindingId > 0
                ? _locomotion.TryCrossFadeAction(prepared.BindingId, request.TransitionSeconds,
                    request.StartOffsetSeconds, request.PlaybackSpeed, holdSeconds, correlation)
                : _locomotion.TryCrossFadeClip(prepared.ClipKey, request.TransitionSeconds,
                    request.StartOffsetSeconds, request.PlaybackSpeed, holdSeconds, correlation);
            if (!accepted)
            {
                return CharacterActionAdapterSinkResult.Rejected(CharacterActionDiagnostic.Warning(
                    CharacterActionDiagnosticCode.AnimationActionMissing,
                    "预览 locomotion 后端拒绝动画 key：" + animationKey));
            }

            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        CharacterActionAdapterSinkResult ICharacterActionAudioCueRequestSink.SubmitAudioCueRequest(CharacterActionAudioCueRequest request)
        {
            _lastAudioCueRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }


        CharacterActionAdapterSinkResult ICharacterActionCameraRequestSink.SubmitCameraRequest(CharacterActionCameraRequest request)
        {
            _lastCameraRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        CharacterActionAdapterSinkResult ICharacterActionUiFeedbackRequestSink.SubmitUiFeedbackRequest(CharacterActionUiFeedbackRequest request)
        {
            _lastUiFeedbackRequests.Add(request);
            return CharacterActionAdapterSinkResult.AcceptedResult();
        }

        private void AppendIntentDiagnostics(
            CharacterActionSetConfig actionSet,
            CharacterActionConfigRegistry registry,
            CharacterActionConfig action)
        {
            if (actionSet == null || registry == null || action == null)
                return;

            CharacterActionBinding[] bindings = actionSet.CommandBindings ?? Array.Empty<CharacterActionBinding>();
            for (int i = 0; i < bindings.Length; i++)
            {
                CharacterActionBinding binding = bindings[i];
                if (binding == null || binding.ActionId != action.ActionId)
                {
                    continue;
                }

                var resolver = new CharacterActionResolver();
                var request = new CharacterActionIntentRequest(
                    new GameplayEntityId(1, 1),
                    binding.IntentId,
                    abilityId: null,
                    abilityStableId: 0,
                    requestedActionId: CharacterActionId.None,
                    CharacterActionSourceKind.LocalInput,
                    binding.Priority,
                    RuntimeFrame.Zero,
                    new RuntimeTraceIdentity(PreviewIntentTraceKind, binding.IntentId));
                CharacterActionResolveResult result = resolver.ResolveCommand(
                    new CharacterActionResolverContext(
                        actionSet,
                        registry.Actions,
                        durationPolicy: new CharacterActionDurationPolicy(DefaultPreviewDurationFrames)),
                    request);
                if (result.IsSuccess && result.Plan.ActionId == action.ActionId)
                {
                    _logger.Info(
                        "CharacterActionPreview",
                        "当前动作可由输入意图触发：" + binding.IntentId);
                }
            }
        }

        private int ResolveDurationFrames(CharacterActionConfig action)
        {
            if (action == null)
                return 1;

            CharacterActionPlanDurationResult duration = CharacterActionPlanDurationResolver.Resolve(
                action,
                policy: new CharacterActionDurationPolicy(DefaultPreviewDurationFrames));
            return duration.Resolved ? Math.Max(1, duration.DurationFrames) : DefaultPreviewDurationFrames;
        }

        private bool TryResolvePreviewBinding(CharacterActionSetConfig actionSet, out PreviewBinding binding)
        {
            binding = default;
            if (actionSet == null)
            {
                SetDiagnostics("运行时预览需要 ActionSet。");
                return false;
            }

            CharacterImportedPackage[] packages = LoadGeneratedPackages();
            if (packages.Length == 0)
            {
                SetDiagnostics("运行时预览找不到生成的角色包目录：" + GeneratedPackagesRoot + "。");
                return false;
            }

            CharacterImportedPackage package = null;
            CharacterConfig character = null;
            if (!string.IsNullOrWhiteSpace(actionSet.CharacterStableId))
            {
                for (int i = 0; i < packages.Length && character == null; i++)
                {
                    character = FindCharacterByStableId(packages[i].Configs, actionSet.CharacterStableId);
                    if (character != null)
                        package = packages[i];
                }
            }
            else if (packages.Length == 1 && packages[0].Configs.Characters.Length == 1)
            {
                package = packages[0];
                character = packages[0].Configs.Characters[0];
                _logger.Warning(
                    "CharacterActionPreview",
                    "ActionSet.CharacterStableId 为空，使用唯一生成角色：" + character.StableId + "。");
            }

            if (package == null || character == null)
            {
                SetDiagnostics("运行时预览无法解析角色 StableId：" + actionSet.CharacterStableId);
                return false;
            }

            EquipmentLoadoutConfig loadout = ResolveLoadout(
                package.Configs,
                character,
                actionSet.EquipmentStateStableId,
                out EquipmentStateConfig state,
                out EquippedWeaponSlot[] equippedWeapons);
            if (loadout == null)
            {
                SetDiagnostics("运行时预览无法解析装备状态对应的装配：" + actionSet.EquipmentStateStableId);
                return false;
            }

            binding = new PreviewBinding(package, character, loadout, state, equippedWeapons);
            return true;
        }

        private static CharacterImportedPackage[] LoadGeneratedPackages()
        {
            // Share one read with toolbar discovery during a profile switch.
            return CharacterActionPreviewBindingDiscovery.LoadGeneratedPackages();
        }

        private static CharacterConfig FindCharacterByStableId(CharacterImportedConfigSet configs, string stableId)
        {
            CharacterConfig[] characters = configs?.Characters ?? Array.Empty<CharacterConfig>();
            for (int i = 0; i < characters.Length; i++)
            {
                CharacterConfig character = characters[i];
                if (character != null && string.Equals(character.StableId, stableId, StringComparison.Ordinal))
                    return character;
            }

            return null;
        }

        private static EquipmentLoadoutConfig ResolveLoadout(
            CharacterImportedConfigSet configs,
            CharacterConfig character,
            string equipmentStateStableId,
            out EquipmentStateConfig activeState,
            out EquippedWeaponSlot[] equippedWeapons)
        {
            activeState = null;
            equippedWeapons = Array.Empty<EquippedWeaponSlot>();
            if (configs == null || character == null)
                return null;

            if (string.IsNullOrWhiteSpace(equipmentStateStableId))
            {
                EquipmentLoadoutConfig defaultLoadout = configs.FindLoadout(character.DefaultLoadoutId);
                if (defaultLoadout != null)
                    activeState = ResolveState(configs, character, defaultLoadout, out equippedWeapons);
                return defaultLoadout;
            }

            EquipmentSchemaConfig schema = configs.FindEquipmentSchema(character.EquipmentSchemaId);
            if (schema == null)
            {
                EquipmentLoadoutConfig defaultLoadout = configs.FindLoadout(character.DefaultLoadoutId);
                if (defaultLoadout != null)
                    activeState = ResolveState(configs, character, defaultLoadout, out equippedWeapons);
                return defaultLoadout;
            }

            for (int i = 0; i < configs.Loadouts.Length; i++)
            {
                EquipmentLoadoutConfig loadout = configs.Loadouts[i];
                if (loadout == null || !loadout.EquipmentSchemaId.Equals(character.EquipmentSchemaId))
                    continue;

                EquipmentStateResolveResult result = EquipmentStateResolver.Resolve(
                    schema,
                    loadout,
                    configs.Weapons,
                    configs.EquipmentStates);
                if (!result.IsSuccess || result.ActiveState == null)
                    continue;

                if (string.Equals(result.ActiveState.StableId, equipmentStateStableId, StringComparison.Ordinal))
                {
                    activeState = result.ActiveState;
                    equippedWeapons = result.EquippedWeapons ?? Array.Empty<EquippedWeaponSlot>();
                    return loadout;
                }
            }

            return null;
        }

        private static EquipmentStateConfig ResolveState(
            CharacterImportedConfigSet configs,
            CharacterConfig character,
            EquipmentLoadoutConfig loadout,
            out EquippedWeaponSlot[] equippedWeapons)
        {
            equippedWeapons = Array.Empty<EquippedWeaponSlot>();
            EquipmentSchemaConfig schema = configs.FindEquipmentSchema(character.EquipmentSchemaId);
            if (schema == null || loadout == null)
                return null;

            EquipmentStateResolveResult result = EquipmentStateResolver.Resolve(
                schema,
                loadout,
                configs.Weapons,
                configs.EquipmentStates);
            if (result.IsSuccess)
                equippedWeapons = result.EquippedWeapons ?? Array.Empty<EquippedWeaponSlot>();
            return result.IsSuccess ? result.ActiveState : null;
        }

        private static CharacterWeaponAttachmentRuntimeBinding[] ResolveMountedWeaponAttachments(
            CharacterImportedPackage package,
            EquippedWeaponSlot[] equippedWeapons)
        {
            CharacterWeaponAttachmentRuntimeBinding[] attachments = package?.Geometry?.WeaponAttachments
                ?? Array.Empty<CharacterWeaponAttachmentRuntimeBinding>();
            if (attachments.Length == 0 || equippedWeapons == null || equippedWeapons.Length == 0)
                return Array.Empty<CharacterWeaponAttachmentRuntimeBinding>();

            var result = new List<CharacterWeaponAttachmentRuntimeBinding>();
            var emitted = new HashSet<(int Slot, int Weapon)>();
            for (int i = 0; i < equippedWeapons.Length; i++)
            {
                EquippedWeaponSlot equipped = equippedWeapons[i];
                if (equipped.Weapon == null)
                    continue;

                int mountedSlot = equipped.MountedSlotId <= 0
                    ? equipped.SlotId
                    : equipped.MountedSlotId;
                var key = (mountedSlot, equipped.Weapon.WeaponId.Value);
                if (!emitted.Add(key))
                    continue;

                CharacterWeaponAttachmentRuntimeBinding attachment =
                    FindAttachmentForWeapon(attachments, equipped.Weapon, mountedSlot);
                if (attachment != null)
                    result.Add(attachment);
            }

            return result.ToArray();
        }

        private static CharacterWeaponAttachmentRuntimeBinding FindAttachmentForWeapon(
            CharacterWeaponAttachmentRuntimeBinding[] attachments,
            WeaponConfig weapon,
            int mountedSlot)
        {
            for (int i = 0; i < attachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = attachments[i];
                if (attachment == null)
                    continue;
                if (attachment.EquipSlot != mountedSlot)
                    continue;
                if (AttachmentMatchesWeapon(attachment, weapon))
                    return attachment;
            }

            return null;
        }

        private static bool AttachmentMatchesWeapon(
            CharacterWeaponAttachmentRuntimeBinding attachment,
            WeaponConfig weapon)
        {
            CharacterResourceKeyEntry[] resourceKeys = weapon?.ResourceKeys ?? Array.Empty<CharacterResourceKeyEntry>();
            for (int i = 0; i < resourceKeys.Length; i++)
            {
                if (string.Equals(resourceKeys[i].Id, GetAttachmentRuntimeResourceKey(attachment), StringComparison.Ordinal))
                    return true;
            }

            return string.Equals(attachment.WeaponLabel, weapon?.StableId, StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(weapon?.StableId)
                    && weapon.StableId.EndsWith(attachment.WeaponLabel, StringComparison.Ordinal));
        }

        private static string GetAttachmentRuntimeResourceKey(CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            if (attachment == null)
                return string.Empty;

            return !string.IsNullOrWhiteSpace(attachment.RuntimeResourceKey)
                ? attachment.RuntimeResourceKey
                : attachment.PreviewResourceKey;
        }

        private void PreparePreviewSockets(CharacterImportedPackage package, Transform root)
        {
            _previewSockets.Clear();
            var sockets = package?.Geometry?.Sockets ?? Array.Empty<CharacterSocketRuntimeBinding>();
            for (int i = 0; i < sockets.Length; i++)
            {
                var socket = sockets[i];
                if (socket == null || socket.SocketId <= 0) continue;
                Transform target = FindSocket(root, socket.SocketLabel)
                    ?? FindChild(root, "Socket_" + socket.SocketId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (target != null) _previewSockets.Add(socket.SocketId, target);
            }
        }

        private Transform FindSocket(Transform root, int socketId) => root != null && _previewSockets.TryGetValue(socketId, out Transform value) ? value : null;

        private static Transform FindSocket(Transform root, string socketId)
        {
            if (root == null || string.IsNullOrWhiteSpace(socketId))
                return null;

            return FindChild(root, "Socket_" + socketId) ?? FindChild(root, socketId);
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
                return null;
            if (string.Equals(root.name, name, StringComparison.OrdinalIgnoreCase))
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindChild(root.GetChild(i), name);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static Vector3 ToVector3(CharacterRuntimeVector3 value, Vector3 fallback)
        {
            Vector3 vector = new Vector3(value.X, value.Y, value.Z);
            return vector == Vector3.zero && fallback != Vector3.zero ? fallback : vector;
        }

        private static Quaternion ToQuaternion(CharacterRuntimeQuaternion value)
        {
            Quaternion rotation = new Quaternion(value.X, value.Y, value.Z, value.W);
            return rotation == default ? Quaternion.identity : rotation;
        }

        private void DisposeRunner()
        {
            ClearPreviewPresentation();
            _gateway = null;
            _previewAction = null;
            _lastDebugSnapshot = null;
            _activeActionId = CharacterActionId.None;
            _activeActionRevision = string.Empty;
            _activeActionRevisionToken = 0L;
            _usesActionRevisionToken = false;
            _lastFrame = -1;
        }

        private void InvalidateValidatedContext()
        {
            _validatedRevisionToken = 0L;
            _validatedSelectedActionId = CharacterActionId.None;
            _validatedActionSet = null;
            _validatedAction = null;
            _validatedRegistry = null;
            _validatedActionSetDto = null;
            _validatedSelectedActionDto = null;
            _validatedExternalContext = null;
            _hasValidatedRevisionContext = false;
        }

        private void ResetRevisionCaches()
        {
            _dispatchTraceTextCache.Clear();
            _diagnosticLineCache.Clear();
            _hitTraceDiagnosticLineCache.Clear();


            _logger.ClearPersistentLines();
        }

        private void PrepareHotBufferCapacity(CharacterActionConfig action)
        {
            if (action == null)
                return;

            int eventCount = action.MotionTrack.Events.Length
                + action.CombatTrack.Events.Length
                + action.GameplayTrack.Events.Length
                + action.AnimationTrack.Events.Length
                + action.PresentationTrack.Events.Length
                + action.DebugTrack.Events.Length;
            int capacity = Math.Max(InitialFrameRequestCapacity, eventCount);
            EnsureListCapacity(_lastMotionRequests, capacity);
            EnsureListCapacity(_lastCombatRequests, capacity);
            EnsureListCapacity(_lastGameplayRequests, capacity);
            EnsureListCapacity(_lastAnimationRequests, capacity);
            EnsureListCapacity(_lastAudioCueRequests, capacity);
            EnsureListCapacity(_lastVfxRequests, capacity);
            EnsureListCapacity(_lastCameraRequests, capacity);
            EnsureListCapacity(_lastUiFeedbackRequests, capacity);
            EnsureListCapacity(_lastDebugDispatches, capacity);
            EnsureListCapacity(_activeHitTraces, capacity);
            EnsureListCapacity(_weaponTraceSnapshots, capacity);
            EnsureListCapacity(_hitVolumeSnapshots, capacity);
            EnsureListCapacity(_activeVfxPreviews, capacity);
            EnsureListCapacity(_activeProjectilePreviews, capacity);
            EnsureListCapacity(_vfxPreviewSnapshots, capacity);
            EnsureListCapacity(_projectilePreviewSnapshots, capacity);
            EnsureListCapacity(_removePreviewIndices, capacity);
        }

        private static void EnsureListCapacity<T>(List<T> list, int capacity)
        {
            if (list.Capacity < capacity)
                list.Capacity = capacity;
        }

        private void DisposePreviewInstance()
        {
            ClearPreviewPresentation();
            DisposeFallbackAnimationSampler();
            if (_previewInstance != null)
            {
                DestroyOwnedPreviewGameObject(_previewInstance);
                _previewInstance = null;
            }

            if (_characterHandle != null && _resourceManager != null)
            {
                _resourceManager.Release(_characterHandle);
                _characterHandle = null;
            }

            _locomotion = null;
            _boneSync = null;
            _previewAnimator = null;
            _previewAnimationSamplingRoot = null;
            _defaultEquipmentBinder = null;
            _resourceManager = null;
            _memoryProvider = null;
            _previewAnimationClips.Clear();
            _preparedPreviewAnimations.Clear();
            _previewAnimationClipsByKey.Clear();
            _previewVfxDefinitions = new VfxDefinitionRegistry();
            _vfxDefinitionsByStableName = new Dictionary<string, VfxDefinition>(StringComparer.Ordinal);
            _previewProjectileDefinitions = new CombatProjectileDefinitionRegistry();
            _previewProjectileSpawnProfiles = new CombatProjectileSpawnProfileRegistry();
            _previewProjectilePresentation = new CombatProjectileUnityPresentationRegistry();
            _hasProjectileCatalogOverride = false;
            _previewAuthoringResourcesReady = false;
            _currentPreviewBinding = default;
            _previewTraceBakeCatalog = null;
            _previewBindingKey = string.Empty;
            _previewBoundActionSet = null;
            _previewBoundRegistry = null;
            _previewSockets.Clear();
            _weaponLocatorPoseCache.Clear();
            _sceneWeaponTraceSegments.Clear();
            ResetRevisionCaches();
            _previewVelocity = Vector3.zero;
            _pendingImpulse = Vector3.zero;
            _previewMovementMode = CharacterMovementMode.Idle;
            _previewMovementLocked = false;
            _previewMovementControlScale = 1f;
            _previewRotationControlScale = 1f;
            _previewGrounded = true;
            _activeHitTraces.Clear();
            _weaponTraceSnapshots.Clear();
            _weaponLocatorPoseSnapshots.Clear();
            BeginFrameDispatch();
        }

        private void SetDiagnostics(string text)
        {
            DiagnosticsText = text ?? string.Empty;
        }

        private static bool IsStalePreviewObject(GameObject candidate)
        {
            if (candidate == null || EditorUtility.IsPersistent(candidate))
                return false;
            if (!candidate.scene.IsValid())
                return false;
            if (HasPreviewObjectAncestor(candidate.transform))
                return false;

            string name = candidate.name ?? string.Empty;
            for (int i = 0; i < PreviewObjectNamePrefixes.Length; i++)
            {
                if (name.StartsWith(PreviewObjectNamePrefixes[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool HasPreviewObjectAncestor(Transform transform)
        {
            if (transform == null)
                return false;

            Transform parent = transform.parent;
            while (parent != null)
            {
                string parentName = parent.gameObject.name ?? string.Empty;
                for (int i = 0; i < PreviewObjectNamePrefixes.Length; i++)
                {
                    if (parentName.StartsWith(PreviewObjectNamePrefixes[i], StringComparison.Ordinal))
                        return true;
                }

                parent = parent.parent;
            }

            return false;
        }

        private static void DestroyPreviewGameObject(GameObject gameObject)
        {
            if (gameObject == null)
                return;

            CharacterDefaultEquipmentRuntimeBinder binder =
                gameObject.GetComponent<CharacterDefaultEquipmentRuntimeBinder>();
            if (binder != null)
                binder.ClearSpawnedWeapons();

            StopParticles(gameObject);
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        private GameObject InstantiatePreviewObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return _objectHost != null
                ? _objectHost.Instantiate(prefab, position, rotation)
                : UnityEngine.Object.Instantiate(prefab, position, rotation);
        }

        private void DestroyOwnedPreviewGameObject(GameObject gameObject)
        {
            if (gameObject == null)
                return;

            CharacterDefaultEquipmentRuntimeBinder binder =
                gameObject.GetComponent<CharacterDefaultEquipmentRuntimeBinder>();
            if (binder != null)
                binder.ClearSpawnedWeapons();

            StopParticles(gameObject);
            if (_objectHost != null && !_objectHost.IsDisposed)
                _objectHost.Destroy(gameObject);
            else
                UnityEngine.Object.DestroyImmediate(gameObject);
        }

        private void FlushDiagnostics()
        {
            string log = _logger.Format();
            if (string.IsNullOrWhiteSpace(log))
                return;

            DiagnosticsText = string.IsNullOrWhiteSpace(DiagnosticsText)
                ? log
                : DiagnosticsText + "\n" + log;
        }

        private static string CreateActionRevision(CharacterActionAuthoringModel model)
        {
            if (model == null)
                return string.Empty;

            return JsonConvert.SerializeObject(
                new ActionRevision(model.ActionSet, model.SelectedAction),
                CharacterActionAuthoringJsonService.JsonSettings);
        }

        private static string GetCharacterPrefabResourceId(CharacterImportedPackage package)
        {
            return CharacterRuntimePrefabResourceCatalog.GetCharacterPrefabResourceId(package?.PackageId);
        }

        private static string GetWeaponPrefabResourceId(
            CharacterImportedPackage package,
            CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            return CharacterRuntimePrefabResourceCatalog.GetWeaponPrefabResourceId(
                package?.PackageId,
                attachment?.EquipmentSlotLabel,
                attachment?.WeaponLabel);
        }

        private static string GetDefaultWeaponResourceId(
            CharacterImportedPackage package,
            CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            return CharacterRuntimePrefabResourceCatalog.GetDefaultWeaponResourceId(package, attachment);
        }

        private static string GetDefaultWeaponResourceVariant(CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            return CharacterRuntimePrefabResourceCatalog.GetDefaultWeaponResourceVariant(attachment);
        }

        private static string GetDefaultWeaponResourcePackageId(
            CharacterImportedPackage package,
            CharacterWeaponAttachmentRuntimeBinding attachment)
        {
            return CharacterRuntimePrefabResourceCatalog.GetDefaultWeaponResourcePackageId(package, attachment);
        }

        private static string FormatSpawnIssues(CharacterRuntimeSpawnResult result)
        {
            if (result == null || result.Issues.Length == 0)
                return "unknown error";

            var parts = new List<string>(result.Issues.Length);
            for (int i = 0; i < result.Issues.Length; i++)
                parts.Add(result.Issues[i].Code + ":" + result.Issues[i].Message);
            return string.Join("; ", parts);
        }

        private static AnimationClip LoadAnimationClipAsset(string assetPath, string sourceClipName)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;

            AnimationClip direct = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (direct != null && (string.IsNullOrWhiteSpace(sourceClipName) || string.Equals(direct.name, sourceClipName, StringComparison.Ordinal)))
                return direct;
            if (direct != null && !assetPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) && !assetPath.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
                return direct;

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            AnimationClip first = null;
            for (int i = 0; i < assets.Length; i++)
            {
                if (!(assets[i] is AnimationClip clip))
                    continue;
                if (first == null)
                    first = clip;
                if (!string.IsNullOrWhiteSpace(sourceClipName) && string.Equals(clip.name, sourceClipName, StringComparison.Ordinal))
                    return clip;
            }

            return first;
        }

        private static string NormalizeProjectPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private static bool TryParseResourceKey(string text, string fallbackTypeId, out ResourceKey key)
        {
            key = default;
            string value = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string packageId = string.Empty;
            int packageIndex = value.LastIndexOf('@');
            if (packageIndex >= 0)
            {
                packageId = value.Substring(packageIndex + 1);
                value = value.Substring(0, packageIndex);
            }

            string variant = string.Empty;
            int variantIndex = value.LastIndexOf('#');
            if (variantIndex >= 0)
            {
                variant = value.Substring(variantIndex + 1);
                value = value.Substring(0, variantIndex);
            }

            string id = value;
            string typeId = fallbackTypeId ?? string.Empty;
            int typeIndex = value.LastIndexOf(':');
            if (typeIndex >= 0)
            {
                id = value.Substring(0, typeIndex);
                typeId = value.Substring(typeIndex + 1);
            }

            key = new ResourceKey(id, typeId, variant, packageId);
            return key.IsValid;
        }

        private readonly struct PreviewActiveHitTrace
        {
            public PreviewActiveHitTrace(
                CharacterActionId actionId,
                long instanceId,
                int equipmentSlotId,
                int traceProfileId,
                int startFrame,
                int stableEventId,
                CharacterActionHitVolumeConfig hitVolume)
            {
                ActionId = actionId;
                InstanceId = instanceId;
                EquipmentSlotId = equipmentSlotId;
                TraceProfileId = traceProfileId;
                StartFrame = startFrame < 0 ? 0 : startFrame;
                StableEventId = stableEventId;
                HitVolume = hitVolume;
            }

            public CharacterActionId ActionId { get; }
            public long InstanceId { get; }
            public int EquipmentSlotId { get; }
            public int TraceProfileId { get; }
            public int StartFrame { get; }
            public int StableEventId { get; }
            public CharacterActionHitVolumeConfig HitVolume { get; }
        }

        private readonly struct PreviewHitTraceStop
        {
            public PreviewHitTraceStop(long instanceId, int equipmentSlotId, int traceProfileId)
            {
                InstanceId = instanceId;
                EquipmentSlotId = equipmentSlotId;
                TraceProfileId = traceProfileId;
            }

            public long InstanceId { get; }
            public int EquipmentSlotId { get; }
            public int TraceProfileId { get; }

            public bool Matches(PreviewActiveHitTrace active)
            {
                if (active.InstanceId != InstanceId)
                    return false;
                if ((TraceProfileId > 0))
                    return active.TraceProfileId == TraceProfileId;
                if ((EquipmentSlotId > 0))
                    return active.EquipmentSlotId == EquipmentSlotId;
                return (active.TraceProfileId <= 0)
                    && (active.EquipmentSlotId <= 0);
            }

            public bool Matches(PreviewHitTraceStop other)
            {
                return InstanceId == other.InstanceId
                    && EquipmentSlotId == other.EquipmentSlotId
                    && TraceProfileId == other.TraceProfileId;
            }
        }

        private readonly struct PreviewWeaponTraceProfile
        {
            public PreviewWeaponTraceProfile(
                float length,
                float radius,
                float proximalExtension,
                float proximalRadius)
            {
                Length = length < 0f ? 0f : length;
                Radius = radius < 0f ? 0f : radius;
                ProximalExtension = proximalExtension < 0f ? 0f : proximalExtension;
                ProximalRadius = proximalRadius < 0f ? 0f : proximalRadius;
            }

            public float Length { get; }
            public float Radius { get; }
            public float ProximalExtension { get; }
            public float ProximalRadius { get; }
        }

        private readonly struct PreviewBakedTraceFrame
        {
            public PreviewBakedTraceFrame(Vector3 rootPrev, Vector3 tipPrev, Vector3 rootNow, Vector3 tipNow)
            {
                RootPrev = rootPrev;
                TipPrev = tipPrev;
                RootNow = rootNow;
                TipNow = tipNow;
            }

            public Vector3 RootPrev { get; }
            public Vector3 TipPrev { get; }
            public Vector3 RootNow { get; }
            public Vector3 TipNow { get; }
        }

        public readonly struct CharacterActionPreviewWeaponTraceSnapshot
        {
            public CharacterActionPreviewWeaponTraceSnapshot(
                CharacterActionId actionId,
                int traceProfileId,
                int frame,
                WeaponTraceFrame traceFrame)
            {
                ActionId = actionId;
                TraceProfileId = traceProfileId;
                Frame = frame < 0 ? 0 : frame;
                TraceFrame = traceFrame;
            }

            public CharacterActionId ActionId { get; }
            public int TraceProfileId { get; }
            public int Frame { get; }
            public WeaponTraceFrame TraceFrame { get; }
        }

        public readonly struct CharacterActionPreviewHitVolumeSnapshot
        {
            public CharacterActionPreviewHitVolumeSnapshot(
                CharacterActionId actionId,
                int stableEventId,
                int frame,
                Vector3 pointA,
                Vector3 pointB,
                float radius,
                CharacterActionHitVolumeShapeKind shapeKind,
                CharacterActionHitVolumeSourceKind sourceKind)
            {
                ActionId = actionId;
                StableEventId = stableEventId;
                Frame = frame < 0 ? 0 : frame;
                PointA = pointA;
                PointB = pointB;
                Radius = radius < 0f ? 0f : radius;
                ShapeKind = shapeKind;
                SourceKind = sourceKind;
            }

            public CharacterActionId ActionId { get; }
            public int StableEventId { get; }
            public int Frame { get; }
            public Vector3 PointA { get; }
            public Vector3 PointB { get; }
            public float Radius { get; }
            public CharacterActionHitVolumeShapeKind ShapeKind { get; }
            public CharacterActionHitVolumeSourceKind SourceKind { get; }
        }

        public readonly struct CharacterActionPreviewWeaponLocatorPoseSnapshot
        {
            public CharacterActionPreviewWeaponLocatorPoseSnapshot(
                int traceProfileId,
                int weaponId,
                int equipSlot,
                string startLocatorPath,
                string endLocatorPath,
                string startParentKind,
                string startParentPath,
                Vector3 startLocalPosition,
                Quaternion startLocalRotation,
                string endParentKind,
                string endParentPath,
                Vector3 endLocalPosition,
                Quaternion endLocalRotation,
                float radius,
                string sampleRule, string weaponLabel = "", string equipmentSlotLabel = "")
            {
                TraceProfileId = traceProfileId;
                WeaponId = weaponId; WeaponLabel = weaponLabel ?? string.Empty;
                EquipSlot = equipSlot; EquipmentSlotLabel = equipmentSlotLabel ?? string.Empty;
                StartLocatorPath = startLocatorPath ?? string.Empty;
                EndLocatorPath = endLocatorPath ?? string.Empty;
                StartParentKind = startParentKind ?? string.Empty;
                StartParentPath = startParentPath ?? string.Empty;
                StartLocalPosition = startLocalPosition;
                StartLocalRotation = startLocalRotation == default ? Quaternion.identity : startLocalRotation;
                EndParentKind = endParentKind ?? string.Empty;
                EndParentPath = endParentPath ?? string.Empty;
                EndLocalPosition = endLocalPosition;
                EndLocalRotation = endLocalRotation == default ? Quaternion.identity : endLocalRotation;
                Radius = radius < 0f ? 0f : radius;
                SampleRule = sampleRule ?? string.Empty;
            }

            public int TraceProfileId { get; }
            public int WeaponId { get; }
            public string WeaponLabel { get; }
            public int EquipSlot { get; }
            public string EquipmentSlotLabel { get; }
            public string StartLocatorPath { get; }
            public string EndLocatorPath { get; }
            public string StartParentKind { get; }
            public string StartParentPath { get; }
            public Vector3 StartLocalPosition { get; }
            public Quaternion StartLocalRotation { get; }
            public string EndParentKind { get; }
            public string EndParentPath { get; }
            public Vector3 EndLocalPosition { get; }
            public Quaternion EndLocalRotation { get; }
            public float Radius { get; }
            public string SampleRule { get; }
        }

        public readonly struct CharacterActionPreviewWeaponLocatorWorldPose
        {
            public CharacterActionPreviewWeaponLocatorWorldPose(
                Vector3 root,
                Vector3 tip,
                Quaternion rootRotation,
                Quaternion tipRotation,
                string source,
                bool isUsingSpawnedWeapon)
            {
                Root = root;
                Tip = tip;
                RootRotation = rootRotation == default ? Quaternion.identity : rootRotation;
                TipRotation = tipRotation == default ? Quaternion.identity : tipRotation;
                Source = source ?? string.Empty;
                IsUsingSpawnedWeapon = isUsingSpawnedWeapon;
            }

            public Vector3 Root { get; }
            public Vector3 Tip { get; }
            public Quaternion RootRotation { get; }
            public Quaternion TipRotation { get; }
            public string Source { get; }
            public bool IsUsingSpawnedWeapon { get; }
        }


        public readonly struct CharacterActionPreviewProjectileSnapshot
        {
            public CharacterActionPreviewProjectileSnapshot(
                long visualId,
                int requestId,
                int definitionId,
                ResourceKey prefabResource,
                int startFrame,
                int endFrame,
                Vector3 origin,
                Vector3 targetPoint,
                Vector3 position,
                Vector3 direction,
                float speedPerFrame,
                float launchSpeedPerFrame,
                CombatProjectileMotionKind motionKind,
                CombatProjectileSpeedProfileKind speedProfileKind,
                float homingTurnRatePerFrame,
                float arcHeight,
                float gravityPerFrameSquared,
                float lateralAmplitude,
                int lateralCycles,
                Vector3 bezierControlOffsetA,
                Vector3 bezierControlOffsetB,
                float radius,
                bool isAreaPolicy,
                float areaRadius,
                string originLabel,
                bool hasPrefabView)
            {
                VisualId = visualId;
                RequestId = requestId;
                DefinitionId = definitionId;
                PrefabResource = prefabResource;
                StartFrame = startFrame < 0 ? 0 : startFrame;
                EndFrame = Math.Max(StartFrame, endFrame);
                Origin = origin;
                TargetPoint = targetPoint;
                Position = position;
                Direction = SafeDirection(direction);
                SpeedPerFrame = speedPerFrame < 0f ? 0f : speedPerFrame;
                LaunchSpeedPerFrame = launchSpeedPerFrame < 0f ? 0f : launchSpeedPerFrame;
                MotionKind = motionKind;
                SpeedProfileKind = speedProfileKind;
                HomingTurnRatePerFrame = Mathf.Clamp01(homingTurnRatePerFrame <= 0f ? 1f : homingTurnRatePerFrame);
                ArcHeight = arcHeight < 0f ? 0f : arcHeight;
                GravityPerFrameSquared = gravityPerFrameSquared < 0f ? 0f : gravityPerFrameSquared;
                LateralAmplitude = lateralAmplitude < 0f ? 0f : lateralAmplitude;
                LateralCycles = lateralCycles < 0 ? 0 : lateralCycles;
                BezierControlOffsetA = bezierControlOffsetA;
                BezierControlOffsetB = bezierControlOffsetB;
                Radius = radius < 0f ? 0f : radius;
                IsAreaPolicy = isAreaPolicy;
                AreaRadius = areaRadius < 0f ? 0f : areaRadius;
                OriginLabel = originLabel ?? string.Empty;
                HasPrefabView = hasPrefabView;
            }

            public long VisualId { get; }
            public int RequestId { get; }
            public int DefinitionId { get; }
            public ResourceKey PrefabResource { get; }
            public int StartFrame { get; }
            public int EndFrame { get; }
            public Vector3 Origin { get; }
            public Vector3 TargetPoint { get; }
            public Vector3 Position { get; }
            public Vector3 Direction { get; }
            public float SpeedPerFrame { get; }
            public float LaunchSpeedPerFrame { get; }
            public CombatProjectileMotionKind MotionKind { get; }
            public CombatProjectileSpeedProfileKind SpeedProfileKind { get; }
            public float HomingTurnRatePerFrame { get; }
            public float ArcHeight { get; }
            public float GravityPerFrameSquared { get; }
            public float LateralAmplitude { get; }
            public int LateralCycles { get; }
            public Vector3 BezierControlOffsetA { get; }
            public Vector3 BezierControlOffsetB { get; }
            public float Radius { get; }
            public bool IsAreaPolicy { get; }
            public float AreaRadius { get; }
            public string OriginLabel { get; }
            public bool HasPrefabView { get; }
        }

        private readonly struct PreviewCastAnchor
        {
            public PreviewCastAnchor(Vector3 position, Vector3 forward)
            {
                Position = position;
                Forward = SafeDirection(forward);
            }

            public Vector3 Position { get; }
            public Vector3 Forward { get; }
        }


        private sealed class PreviewActiveProjectile
        {
            public PreviewActiveProjectile(
                long visualId,
                int requestId,
                int definitionId,
                ResourceKey prefabResource,
                int startFrame,
                int lifetimeFrames,
                float speedPerFrame,
                float launchSpeedPerFrame,
                CombatProjectileMotionKind motionKind,
                CombatProjectileSpeedProfileKind speedProfileKind,
                float homingTurnRatePerFrame,
                float arcHeight,
                float gravityPerFrameSquared,
                float lateralAmplitude,
                int lateralCycles,
                Vector3 bezierControlOffsetA,
                Vector3 bezierControlOffsetB,
                float radius,
                bool isAreaPolicy,
                float areaRadius,
                Vector3 origin,
                Vector3 direction,
                Vector3 targetPoint,
                string originLabel,
                GameObject view,
                ResourceHandle<GameObject> handle,
                bool hasPrefabView)
            {
                VisualId = visualId;
                RequestId = requestId;
                DefinitionId = definitionId;
                PrefabResource = prefabResource;
                StartFrame = startFrame < 0 ? 0 : startFrame;
                LifetimeFrames = Math.Max(1, lifetimeFrames);
                SpeedPerFrame = speedPerFrame < 0f ? 0f : speedPerFrame;
                LaunchSpeedPerFrame = launchSpeedPerFrame < 0f ? 0f : launchSpeedPerFrame;
                MotionKind = motionKind;
                SpeedProfileKind = speedProfileKind;
                HomingTurnRatePerFrame = Mathf.Clamp01(homingTurnRatePerFrame <= 0f ? 1f : homingTurnRatePerFrame);
                ArcHeight = arcHeight < 0f ? 0f : arcHeight;
                GravityPerFrameSquared = gravityPerFrameSquared < 0f ? 0f : gravityPerFrameSquared;
                LateralAmplitude = lateralAmplitude < 0f ? 0f : lateralAmplitude;
                LateralCycles = lateralCycles < 0 ? 0 : lateralCycles;
                BezierControlOffsetA = bezierControlOffsetA;
                BezierControlOffsetB = bezierControlOffsetB;
                Radius = radius < 0f ? 0f : radius;
                IsAreaPolicy = isAreaPolicy;
                AreaRadius = areaRadius < 0f ? 0f : areaRadius;
                Origin = origin;
                Direction = SafeDirection(direction);
                TargetPoint = targetPoint;
                OriginLabel = originLabel ?? string.Empty;
                View = view;
                Handle = handle;
                HasPrefabView = hasPrefabView;
            }

            public long VisualId { get; }
            public int RequestId { get; }
            public int DefinitionId { get; }
            public ResourceKey PrefabResource { get; }
            public int StartFrame { get; }
            public int LifetimeFrames { get; }
            public float SpeedPerFrame { get; }
            public float LaunchSpeedPerFrame { get; }
            public CombatProjectileMotionKind MotionKind { get; }
            public CombatProjectileSpeedProfileKind SpeedProfileKind { get; }
            public float HomingTurnRatePerFrame { get; }
            public float ArcHeight { get; }
            public float GravityPerFrameSquared { get; }
            public float LateralAmplitude { get; }
            public int LateralCycles { get; }
            public Vector3 BezierControlOffsetA { get; }
            public Vector3 BezierControlOffsetB { get; }
            public float Radius { get; }
            public bool IsAreaPolicy { get; }
            public float AreaRadius { get; }
            public Vector3 Origin { get; }
            public Vector3 Direction { get; }
            public Vector3 TargetPoint { get; }
            public string OriginLabel { get; }
            public GameObject View { get; }
            public ResourceHandle<GameObject> Handle { get; }
            public bool HasPrefabView { get; }
        }

        private readonly struct PreviewDispatchTraceKey : IEquatable<PreviewDispatchTraceKey>
        {
            public PreviewDispatchTraceKey(CharacterActionTrackDispatchEvent dispatch, int frame)
            {
                Dispatch = dispatch;
                Frame = frame;
            }

            private CharacterActionTrackDispatchEvent Dispatch { get; }
            private int Frame { get; }

            public bool Equals(PreviewDispatchTraceKey other)
            {
                return Frame == other.Frame && Dispatch.Equals(other.Dispatch);
            }

            public override bool Equals(object obj)
            {
                return obj is PreviewDispatchTraceKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Dispatch.GetHashCode() * 397) ^ Frame;
                }
            }
        }

        private readonly struct PreviewDiagnosticLineKey : IEquatable<PreviewDiagnosticLineKey>
        {
            public PreviewDiagnosticLineKey(
                RuntimeLogLevel level,
                string category,
                string code,
                string message)
            {
                Level = level;
                Category = category ?? string.Empty;
                Code = code ?? string.Empty;
                Message = message ?? string.Empty;
            }

            private RuntimeLogLevel Level { get; }
            private string Category { get; }
            private string Code { get; }
            private string Message { get; }

            public bool Equals(PreviewDiagnosticLineKey other)
            {
                return Level == other.Level
                    && string.Equals(Category, other.Category, StringComparison.Ordinal)
                    && string.Equals(Code, other.Code, StringComparison.Ordinal)
                    && string.Equals(Message, other.Message, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is PreviewDiagnosticLineKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)Level;
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Category);
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Code);
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Message);
                    return hash;
                }
            }
        }

        private enum PreviewHitTraceDiagnosticKind
        {
            MissingAnimationClip = 0,
            MissingWeaponTraceProfile = 1,
            UnresolvedWeaponLocator = 2,
            MissingBakedTraceFrame = 3,
            MissingSocket = 4,
        }

        private readonly struct PreviewHitTraceDiagnosticKey : IEquatable<PreviewHitTraceDiagnosticKey>
        {
            public PreviewHitTraceDiagnosticKey(
                PreviewHitTraceDiagnosticKind kind,
                CharacterActionId actionId,
                int stableEventId,
                int equipmentSlotId,
                int traceProfileId,
                string clipKey,
                string startLocatorPath,
                string endLocatorPath, int startSocketId, int endSocketId)
            {
                Kind = kind; StartSocketId = startSocketId; EndSocketId = endSocketId;
                ActionId = actionId;
                StableEventId = stableEventId;
                EquipmentSlotId = equipmentSlotId;
                TraceProfileId = traceProfileId;
                ClipKey = clipKey ?? string.Empty;
                StartLocatorPath = startLocatorPath ?? string.Empty;
                EndLocatorPath = endLocatorPath ?? string.Empty;
            }

            private int StartSocketId { get; }
            private int EndSocketId { get; }
            private PreviewHitTraceDiagnosticKind Kind { get; }
            private CharacterActionId ActionId { get; }
            private int StableEventId { get; }
            private int EquipmentSlotId { get; }
            private int TraceProfileId { get; }
            private string ClipKey { get; }
            private string StartLocatorPath { get; }
            private string EndLocatorPath { get; }

            public bool Equals(PreviewHitTraceDiagnosticKey other)
            {
                return StartSocketId == other.StartSocketId && EndSocketId == other.EndSocketId && Kind == other.Kind
                    && ActionId == other.ActionId
                    && StableEventId == other.StableEventId
                    && EquipmentSlotId == other.EquipmentSlotId
                    && TraceProfileId == other.TraceProfileId
                    && string.Equals(ClipKey, other.ClipKey, StringComparison.Ordinal)
                    && string.Equals(StartLocatorPath, other.StartLocatorPath, StringComparison.Ordinal)
                    && string.Equals(EndLocatorPath, other.EndLocatorPath, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is PreviewHitTraceDiagnosticKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = ((int)Kind * 397 ^ StartSocketId) * 397 ^ EndSocketId;
                    hash = (hash * 397) ^ ActionId.GetHashCode();
                    hash = (hash * 397) ^ StableEventId;
                    hash = (hash * 397) ^ EquipmentSlotId;
                    hash = (hash * 397) ^ TraceProfileId;
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(ClipKey);
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(StartLocatorPath);
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(EndLocatorPath);
                    return hash;
                }
            }
        }

        private readonly struct CachedWeaponLocatorPose
        {
            private CachedWeaponLocatorPose(
                CharacterActionPreviewWeaponLocatorPoseSnapshot snapshot,
                Transform startTransform,
                Transform endTransform,
                bool resolved,
                bool usesSpawnedWeapon)
            {
                Snapshot = snapshot;
                StartTransform = startTransform;
                EndTransform = endTransform;
                Resolved = resolved;
                UsesSpawnedWeapon = usesSpawnedWeapon;
            }

            private CharacterActionPreviewWeaponLocatorPoseSnapshot Snapshot { get; }
            private Transform StartTransform { get; }
            private Transform EndTransform { get; }
            private bool Resolved { get; }
            private bool UsesSpawnedWeapon { get; }

            public static CachedWeaponLocatorPose FromSpawnedWeapon(
                CharacterActionPreviewWeaponLocatorPoseSnapshot snapshot,
                Transform startLocator,
                Transform endLocator)
            {
                return new CachedWeaponLocatorPose(
                    snapshot,
                    startLocator,
                    endLocator,
                    resolved: true,
                    usesSpawnedWeapon: true);
            }

            public static CachedWeaponLocatorPose FromConfiguredParents(
                CharacterActionPreviewWeaponLocatorPoseSnapshot snapshot,
                Transform startParent,
                Transform endParent)
            {
                return new CachedWeaponLocatorPose(
                    snapshot,
                    startParent,
                    endParent,
                    resolved: true,
                    usesSpawnedWeapon: false);
            }

            public static CachedWeaponLocatorPose Unresolved(
                CharacterActionPreviewWeaponLocatorPoseSnapshot snapshot)
            {
                return new CachedWeaponLocatorPose(
                    snapshot,
                    null,
                    null,
                    resolved: false,
                    usesSpawnedWeapon: false);
            }

            public bool Matches(CharacterActionPreviewWeaponLocatorPoseSnapshot snapshot)
            {
                return Snapshot.TraceProfileId == snapshot.TraceProfileId
                    && Snapshot.WeaponId == snapshot.WeaponId
                    && Snapshot.EquipSlot == snapshot.EquipSlot
                    && string.Equals(Snapshot.StartLocatorPath, snapshot.StartLocatorPath, StringComparison.Ordinal)
                    && string.Equals(Snapshot.EndLocatorPath, snapshot.EndLocatorPath, StringComparison.Ordinal)
                    && string.Equals(Snapshot.StartParentKind, snapshot.StartParentKind, StringComparison.Ordinal)
                    && string.Equals(Snapshot.StartParentPath, snapshot.StartParentPath, StringComparison.Ordinal)
                    && Snapshot.StartLocalPosition == snapshot.StartLocalPosition
                    && Snapshot.StartLocalRotation == snapshot.StartLocalRotation
                    && string.Equals(Snapshot.EndParentKind, snapshot.EndParentKind, StringComparison.Ordinal)
                    && string.Equals(Snapshot.EndParentPath, snapshot.EndParentPath, StringComparison.Ordinal)
                    && Snapshot.EndLocalPosition == snapshot.EndLocalPosition
                    && Snapshot.EndLocalRotation == snapshot.EndLocalRotation;
            }

            public bool TryResolve(out CharacterActionPreviewWeaponLocatorWorldPose pose)
            {
                pose = default;
                if (!Resolved || StartTransform == null || EndTransform == null)
                    return false;

                if (UsesSpawnedWeapon)
                {
                    pose = new CharacterActionPreviewWeaponLocatorWorldPose(
                        StartTransform.position,
                        EndTransform.position,
                        StartTransform.rotation,
                        EndTransform.rotation,
                        "weaponLocator",
                        isUsingSpawnedWeapon: true);
                    return true;
                }

                pose = new CharacterActionPreviewWeaponLocatorWorldPose(
                    TransformMetricPoint(StartTransform, Snapshot.StartLocalPosition),
                    TransformMetricPoint(EndTransform, Snapshot.EndLocalPosition),
                    StartTransform.rotation * Snapshot.StartLocalRotation,
                    EndTransform.rotation * Snapshot.EndLocalRotation,
                    "configuredParent",
                    isUsingSpawnedWeapon: false);
                return true;
            }
        }

        private readonly struct PreviewBinding
        {
            public PreviewBinding(
                CharacterImportedPackage package,
                CharacterConfig character,
                EquipmentLoadoutConfig loadout,
                EquipmentStateConfig activeState,
                EquippedWeaponSlot[] equippedWeapons)
            {
                Package = package;
                Character = character;
                Loadout = loadout;
                ActiveState = activeState;
                EquippedWeapons = equippedWeapons ?? Array.Empty<EquippedWeaponSlot>();
            }

            public CharacterImportedPackage Package { get; }
            public CharacterConfig Character { get; }
            public EquipmentLoadoutConfig Loadout { get; }
            public EquipmentStateConfig ActiveState { get; }
            public EquippedWeaponSlot[] EquippedWeapons { get; }
        }

        private readonly struct ActionRevision
        {
            public ActionRevision(CharacterActionSetJsonDto actionSet, CharacterActionJsonDto action)
            {
                ActionSet = actionSet;
                Action = action;
            }

            public CharacterActionSetJsonDto ActionSet { get; }
            public CharacterActionJsonDto Action { get; }
        }

        private sealed class ResourceCatalogJsonDto
        {
            public string catalogId;
            public string packageId;
            public ResourceCatalogEntryJsonDto[] entries;
        }

        private sealed class ResourceCatalogEntryJsonDto
        {
            public string id;
            public string type;
            public string typeId;
            public string variant;
            public string packageId;
            public string provider;
            public string address;
            public string[] labels;
            public ResourceKeyJsonDto[] dependencies;
            public string hash;
            public long size;
            public bool allowOverride;
            public Dictionary<string, string> providerData;

            public string ResolveTypeId()
            {
                return string.IsNullOrWhiteSpace(typeId) ? type : typeId;
            }
        }

        private sealed class ResourceKeyJsonDto
        {
            public string id;
            public string type;
            public string typeId;
            public string variant;
            public string packageId;

            public string ResolveTypeId()
            {
                return string.IsNullOrWhiteSpace(typeId) ? type : typeId;
            }
        }

        private sealed class PreviewRuntimeLogger : IRuntimeLogger
        {
            private readonly List<string> _lines = new List<string>(InitialDiagnosticCacheCapacity);
            private readonly List<string> _persistentLines = new List<string>(InitialDiagnosticCacheCapacity);
            private readonly List<string> _lastFormattedLines = new List<string>(InitialDiagnosticCacheCapacity);
            private string _lastFormattedText = string.Empty;

            public string LastLine => _lines.Count == 0 ? string.Empty : _lines[_lines.Count - 1];

            public void Log(RuntimeLogLevel level, string category, string message)
            {
                if (level == RuntimeLogLevel.Info)
                    return;

                AddLine("[" + level + "] " + category + ": " + (message ?? string.Empty));
            }

            public void Info(string category, string message)
            {
                AddLine("[Info] " + category + ": " + (message ?? string.Empty));
            }

            public void Warning(string category, string message)
            {
                AddLine("[Warning] " + category + ": " + (message ?? string.Empty));
            }

            public void AddLine(string line)
            {
                if (!string.IsNullOrEmpty(line))
                    _lines.Add(line);
            }

            public void AddPersistentLine(string line)
            {
                if (!string.IsNullOrEmpty(line))
                    _persistentLines.Add(line);
            }

            public void Clear()
            {
                _lines.Clear();
            }

            public void ClearPersistentLines()
            {
                _persistentLines.Clear();
            }

            public string Format()
            {
                int totalLineCount = _persistentLines.Count + _lines.Count;
                if (totalLineCount == 0)
                    return string.Empty;

                bool unchanged = _lastFormattedLines.Count == totalLineCount;
                for (int i = 0; unchanged && i < _persistentLines.Count; i++)
                {
                    unchanged = ReferenceEquals(_lastFormattedLines[i], _persistentLines[i])
                        || string.Equals(_lastFormattedLines[i], _persistentLines[i], StringComparison.Ordinal);
                }

                for (int i = 0; unchanged && i < _lines.Count; i++)
                {
                    int formattedIndex = _persistentLines.Count + i;
                    unchanged = ReferenceEquals(_lastFormattedLines[formattedIndex], _lines[i])
                        || string.Equals(_lastFormattedLines[formattedIndex], _lines[i], StringComparison.Ordinal);
                }

                if (unchanged)
                    return _lastFormattedText;

                _lastFormattedLines.Clear();
                for (int i = 0; i < _persistentLines.Count; i++)
                    _lastFormattedLines.Add(_persistentLines[i]);
                for (int i = 0; i < _lines.Count; i++)
                    _lastFormattedLines.Add(_lines[i]);
                _lastFormattedText = string.Join("\n", _lastFormattedLines);
                return _lastFormattedText;
            }
        }
    }
}
