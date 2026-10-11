using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.Animation;
using MxFramework.Camera.URP;
using MxFramework.CharacterApplication;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.UI;
using UnityEngine;
using MxFramework.CharacterCloth;
using Fgui = global::FairyGUI;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestBonePoseEntry
    {
        public CharacterTestBonePoseEntry(
            string path,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            Path = path ?? string.Empty;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
        }

        public string Path { get; }
        public Vector3 LocalPosition { get; }
        public Quaternion LocalRotation { get; }
        public Vector3 LocalScale { get; }
    }

    internal sealed class CharacterTestBonePoseState
    {
        public CharacterTestBonePoseState(CharacterTestBonePoseEntry[] entries)
        {
            Entries = entries ?? Array.Empty<CharacterTestBonePoseEntry>();
        }

        public IReadOnlyList<CharacterTestBonePoseEntry> Entries { get; }
    }

    internal sealed class CharacterTestRuntimeCharacterPresentationState
    {
        public CharacterTestRuntimeCharacterPresentationState(
            int entityId,
            CharacterRuntimeAnimationPlaybackState animation,
            CharacterProceduralBonePlaybackState procedural,
            CharacterTestBonePoseState bonePose,
            double actionPlaybackTimeSeconds = -1d)
        {
            EntityId = entityId;
            Animation = animation;
            Procedural = procedural;
            BonePose = bonePose ?? new CharacterTestBonePoseState(Array.Empty<CharacterTestBonePoseEntry>());
            ActionPlaybackTimeSeconds = actionPlaybackTimeSeconds;
        }

        public int EntityId { get; }
        public CharacterRuntimeAnimationPlaybackState Animation { get; }
        public CharacterProceduralBonePlaybackState Procedural { get; }
        public CharacterTestBonePoseState BonePose { get; }
        public double ActionPlaybackTimeSeconds { get; }
    }

    internal enum CharacterTestSeverancePresentationStatus
    {
        None = 0,
        RuntimeSliceNoStableResource = 1,
        Released = 2
    }

    /// <summary>Typed result for the CharacterTest-only Severed presentation projection.</summary>
    internal readonly struct CharacterTestSeverancePresentationSyncResult
    {
        internal CharacterTestSeverancePresentationSyncResult(
            CharacterTestSeverancePresentationStatus status,
            int consumedAuthorityEventCount,
            int duplicateAuthorityEventCount,
            int currentStateProjectionCount)
        {
            Status = status;
            ConsumedAuthorityEventCount = consumedAuthorityEventCount;
            DuplicateAuthorityEventCount = duplicateAuthorityEventCount;
            CurrentStateProjectionCount = currentStateProjectionCount;
        }

        public CharacterTestSeverancePresentationStatus Status { get; }
        public int ConsumedAuthorityEventCount { get; }
        public int DuplicateAuthorityEventCount { get; }
        public int CurrentStateProjectionCount { get; }
    }

    /// <summary>Read-only diagnostics for one stable part-state presentation projection.</summary>
    internal readonly struct CharacterTestSeverancePresentationSnapshot
    {
        internal CharacterTestSeverancePresentationSnapshot(
            int entityId,
            int partStateId,
            long authorityRevision,
            bool authoritySevered,
            string presentationEventId,
            CharacterTestSeverancePresentationStatus status,
            int consumedAuthorityEventCount,
            int duplicateAuthorityEventCount,
            int currentStateProjectionCount)
        {
            EntityId = entityId;
            PartStateId = partStateId;
            AuthorityRevision = authorityRevision;
            AuthoritySevered = authoritySevered;
            PresentationEventId = presentationEventId ?? string.Empty;
            Status = status;
            ConsumedAuthorityEventCount = consumedAuthorityEventCount;
            DuplicateAuthorityEventCount = duplicateAuthorityEventCount;
            CurrentStateProjectionCount = currentStateProjectionCount;
        }

        public int EntityId { get; }
        public int PartStateId { get; }
        public long AuthorityRevision { get; }
        public bool AuthoritySevered { get; }
        public string PresentationEventId { get; }
        public CharacterTestSeverancePresentationStatus Status { get; }
        public int ConsumedAuthorityEventCount { get; }
        public int DuplicateAuthorityEventCount { get; }
        public int CurrentStateProjectionCount { get; }

        // The frozen Dragon14 resource set has no independent tail view. These counters
        // make the fail-soft boundary explicit instead of implying hidden side effects.
        public int SpawnedVfxCount => 0;
        public int PlayedSfxCount => 0;
        public int DetachedViewCount => 0;
    }

    public sealed partial class CharacterTestRuntimeCharacterView
    {
        private CharacterClothPresentationService _clothPresentation;
        private readonly List<ICharacterClothGroup> _clothGroups = new List<ICharacterClothGroup>(1);
        private CharacterTestPresentationLod _presentationLod;
        private PresentationLodHandle _presentationLodHandle;
        internal bool HasPresentationCloth => _clothGroups.Count > 0;
        internal bool HasPresentationHeadPad => _headPad != null;

        internal void AttachPresentationLod(CharacterTestPresentationLod service)
        {
            if (_presentationLod != null || Instance == null) return;
            _presentationLod = service;
            _presentationLodHandle = service.Register(this);
            _headPad?.BindPresentationLod(service, _presentationLodHandle, EntityId.Value);
        }

        internal void SetClothLodPaused(bool paused)
        {
            _clothLodPaused = paused;
            for (int i = 0; i < _clothGroups.Count; i++)
            {
                ICharacterClothGroup group = _clothGroups[i];
                if (group is UnityEngine.Object obj && obj == null) continue;
                _clothPresentation?.SetLodPaused(group, paused);
            }
        }

        /// <summary>Explicit notification for nested equipment/renderer changes that do not change the instance root.</summary>
        internal void InvalidatePresentationLodGeometry() =>
            _presentationLod?.InvalidateGeometry(_presentationLodHandle, EntityId.Value);

        internal void ReleaseDestroyedPresentationLod()
        {
            for (int i = 0; i < _clothGroups.Count; i++)
                _clothPresentation?.Unregister(_clothGroups[i], restoreLocalState: false);
            _clothGroups.Clear(); _clothPresentation = null;
            _presentationLod = null; _presentationLodHandle = default;
            ReleaseHeadPad();
        }

        internal void AttachClothPresentation(CharacterClothPresentationService service)
        {
            if (_clothPresentation != null || Instance == null) return;
            _clothPresentation = service;
            MonoBehaviour[] components = Instance.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is ICharacterClothGroup group && service.Register(group))
                    _clothGroups.Add(group);
            }
        }

        /// <summary>Resets cached cloth groups after an explicit presentation discontinuity.</summary>
        /// <remarks>LowFreqAlloc if a backing simulation is still building.</remarks>
        public void ResetClothAfterTeleport(bool keepPose = false)
        {
            _presentationLod?.Discontinuity(_presentationLodHandle);
            for (int i = 0; i < _clothGroups.Count; i++)
            {
                ICharacterClothGroup group = _clothGroups[i];
                if (group is UnityEngine.Object obj && obj == null) continue;
                group.ResetAll(keepPose);
            }
        }
        private const int NetworkPresentationSampleCapacity = 8;
        private const int AuthorityPosePresentationCapacity =
            CharacterAuthorityPoseRigRuntimeBinding.MaximumChannelCount;
        private const float NetworkPresentationSnapDistance = 2.5f;
        private const double NetworkPresentationCatchUpLagFrames = 4d;
        private const double NetworkPresentationCatchUpMultiplier = 4d;
        private const float ActiveActionHoldRefreshSeconds = 0.1f;
        private const float JumpActionLocomotionSuppressionSeconds = 0.25f;
        private const float AnimationTracePrecision = 100f;
        private const float TargetLockUiRendererBoundsHeightRatio = 0.62f;
        private const float TargetLockUiFallbackHeightMeters = 1.15f;
        internal const float ControlStatusHeadBoneOffsetMeters = 0.16f;
        internal const float ControlStatusRendererBoundsOffsetMeters = 0.08f;
        internal const float ControlStatusFallbackHeightMeters = 1.82f;
        private const int MaximumSeverancePresentationPartCount =
            CharacterTestBodyPartStateComponent.MaximumDefinitionCount;

        private readonly IResourceManager _resourceManager;
        private readonly CharacterRuntimeLocomotionBlendController _locomotionController;
        private readonly CharacterTestLocomotionTransitionPresentation _locomotionTransitions;
        private readonly CharacterBoneRuntimeSync _boneSync;
        private readonly CharacterAbstractBoneRuntimeMap _abstractBoneMap;
        private readonly CharacterProceduralBoneRig _proceduralBoneRig;
        private readonly Animator _animator;
        private readonly CharacterAuthorityPoseRigRuntimeBinding _authorityPoseRig;
        private readonly int[] _resolvedAuthorityPoseChannelIds =
            new int[AuthorityPosePresentationCapacity];
        private readonly Transform[] _resolvedAuthorityPoseBones =
            new Transform[AuthorityPosePresentationCapacity];
        private readonly int[] _resolvedAuthorityPoseBoneDepths =
            new int[AuthorityPosePresentationCapacity];
        private readonly Transform[] _resolvedAuthorityPoseRestoreBones =
            new Transform[AuthorityPosePresentationCapacity];
        private readonly Vector3[] _resolvedAuthorityPoseRestorePositions =
            new Vector3[AuthorityPosePresentationCapacity];
        private readonly Quaternion[] _resolvedAuthorityPoseRestoreRotations =
            new Quaternion[AuthorityPosePresentationCapacity];
        private readonly Dictionary<Transform, Vector3> _authorityPoseMirrorOffsets =
            new Dictionary<Transform, Vector3>();
        private readonly HashSet<string> _reportedAnimationErrors = new HashSet<string>();
        private readonly List<NetworkPresentationSample> _networkPresentationSamples =
            new List<NetworkPresentationSample>(NetworkPresentationSampleCapacity);
        private readonly List<CharacterAuthoritySecondaryPoseSample> _authorityPoseSamples =
            new List<CharacterAuthoritySecondaryPoseSample>();
        private readonly StringBuilder _animationTraceBuffer = new StringBuilder(160);
        private readonly Dictionary<int, BodyPartContactPointTargetCacheEntry> _bodyPartContactPointTargetCache =
            new Dictionary<int, BodyPartContactPointTargetCacheEntry>();
        private readonly Dictionary<int, Transform> _socketTargetCache =
            new Dictionary<int, Transform>(16);
        private readonly Dictionary<int, Transform> _authorityPoseChannelCache =
            new Dictionary<int, Transform>(16);
        private readonly SeverancePresentationEntry[] _severancePresentationEntries =
            new SeverancePresentationEntry[MaximumSeverancePresentationPartCount];
        private Dictionary<int, Transform> _socketAnchorCache;
        private Renderer[] _cachedRendererBoundsRenderers;
        private ResourceHandle<GameObject> _characterHandle;
        private CharacterTestFairyGuiHeadPad _headPad;
        private Transform _cachedHeadTransform;
        private double _networkPresentationFrame = double.NaN;
        private bool _networkPresentationActive;
        private bool _socketAnchorCacheResolved;
        private bool _headTransformResolved;
        private bool _rendererBoundsResolved;
        private bool _animationPaused;
        private double _actionPlaybackTimeSeconds = -1d;
        private bool _hasAnimationTraceState;
        private int _lastAnimationTraceBlendX;
        private int _lastAnimationTraceBlendY;
        private int _lastAnimationTraceSpeed;
        private bool _lastAnimationTraceSuppressed;
        private string _lastAnimationTraceActionKey = string.Empty;
        private long _lastActionActiveWithoutKeyTraceFrame = -1000L;
        private int _transformApplyCount;
        private int _socketAnchorResolveCount;
        private int _headPadAnchorResolveCount;
        private int _rendererBoundsResolveCount;
        private UnityCamera _cachedMainCamera;
        private Transform[] _playbackBoneTransforms;
        private string[] _playbackBonePaths;
        private int _severancePresentationEntryCount;
        private int _resolvedAuthorityPoseChannelCount;
        private int _resolvedAuthorityPoseRestoreCount;
        private CharacterTestResolvedAuthorityPoseFrame _resolvedAuthorityPoseFrame;
        private long _resolvedAuthorityPoseExpectedRuntimeFrame = -1L;
        private long _resolvedAuthorityPoseExpectedRevision;
        private int _resolvedAuthorityPoseExpectedBaseSourceRevision;
        private string _resolvedAuthorityPoseExpectedBaseSourceContentHash = string.Empty;

        public CharacterTestRuntimeCharacterView(
            CharacterTestCharacterEntityId entityId,
            GameObject instance,
            ResourceHandle<GameObject> characterHandle,
            IResourceManager resourceManager,
            CharacterRuntimeLocomotionBlendController locomotionController,
            CharacterBoneRuntimeSync boneSync = null,
            CharacterProceduralBoneRig proceduralBoneRig = null,
            CharacterAuthorityPoseRigRuntimeBinding authorityPoseRig = null)
        {
            EntityId = entityId;
            Instance = instance;
            _characterHandle = characterHandle;
            _resourceManager = resourceManager;
            _locomotionController = locomotionController;
            _locomotionTransitions = new CharacterTestLocomotionTransitionPresentation(locomotionController);
            _boneSync = boneSync;
            _abstractBoneMap = instance != null
                ? instance.GetComponent<CharacterAbstractBoneRuntimeMap>()
                : null;
            _proceduralBoneRig = proceduralBoneRig;
            _animator = instance != null
                ? instance.GetComponentInChildren<Animator>(includeInactive: true)
                : null;
            _authorityPoseRig = authorityPoseRig != null
                && authorityPoseRig.IsAuthored
                && authorityPoseRig.Channels.Length > 0
                && authorityPoseRig.Channels.Length <= AuthorityPosePresentationCapacity
                    ? authorityPoseRig
                    : null;
            ResolveAuthoredAuthorityPoseChannels();
            PrepareNumericPoseTargets(authorityPoseRig);
            _proceduralBoneRig?.ConfigureAuthorityPoseControlledBones(
                _resolvedAuthorityPoseBones,
                _resolvedAuthorityPoseChannelCount);
        }

        public CharacterTestCharacterEntityId EntityId { get; }
        public GameObject Instance { get; private set; }
        internal double BladeTrailActionTimeSeconds => _actionPlaybackTimeSeconds;
        internal bool TryEvaluateBladeTrailPose(double actionTimeSeconds)
        {
            if (_locomotionController == null || !_locomotionController.TryEvaluateActionPoseAtTime(actionTimeSeconds)) return false;
            _boneSync?.ApplyBindings();
            return true;
        }
        internal int TransformApplyCount => _transformApplyCount;
        internal int SocketAnchorResolveCount => _socketAnchorResolveCount;
        internal int HeadPadAnchorResolveCount => _headPadAnchorResolveCount;
        internal int RendererBoundsResolveCount => _rendererBoundsResolveCount;

        /// <summary>
        /// Consumes this frame's typed Severed transitions before projecting the current
        /// authority state. Current-state synchronization is deliberately silent, so a
        /// late view attach or restore cannot replay one-shot presentation.
        /// </summary>
        /// <remarks>NoAlloc after the fixed-capacity entries have been initialized.</remarks>
        internal CharacterTestSeverancePresentationSyncResult SyncSeverancePresentation(
            CharacterTestBodyPartStateComponent bodyPartState)
        {
            if (Instance == null)
            {
                return new CharacterTestSeverancePresentationSyncResult(
                    CharacterTestSeverancePresentationStatus.Released,
                    consumedAuthorityEventCount: 0,
                    duplicateAuthorityEventCount: 0,
                    currentStateProjectionCount: 0);
            }

            if (bodyPartState == null || bodyPartState.OwnerEntityId != EntityId.Value)
            {
                SynchronizeUnavailableSeveranceAuthority();
                return new CharacterTestSeverancePresentationSyncResult(
                    CharacterTestSeverancePresentationStatus.None,
                    consumedAuthorityEventCount: 0,
                    duplicateAuthorityEventCount: 0,
                    currentStateProjectionCount: 0);
            }

            int consumedEventCount = 0;
            int duplicateEventCount = 0;
            int currentStateProjectionCount = 0;
            for (int i = 0; i < bodyPartState.TransitionCount; i++)
            {
                CharacterTestBodyPartStateTransition transition = bodyPartState.GetTransitionAt(i);
                if (transition.Reason != CharacterTestBodyPartTransitionReason.Severed
                    || transition.TargetEntityId != EntityId.Value
                    || transition.After.StructureState != CharacterTestBodyPartStructureState.Severed
                    || transition.PartStateId == 0
                    || !bodyPartState.TryGetDefinition(
                        transition.DefinitionIndex,
                        out CharacterTestBodyPartStateDefinition definition)
                    || !definition.Severable)
                {
                    continue;
                }

                ref SeverancePresentationEntry presentation = ref GetOrCreateSeveranceEntry(
                    transition.PartStateId,
                    out bool available);
                if (!available)
                    continue;

                long authorityRevision = transition.After.Revision;
                if (presentation.HasConsumedAuthorityEventRevision
                    && presentation.ConsumedAuthorityEventRevision == authorityRevision)
                {
                    presentation.DuplicateAuthorityEventCount = SaturatingIncrement(
                        presentation.DuplicateAuthorityEventCount);
                    duplicateEventCount++;
                    continue;
                }

                presentation.HasConsumedAuthorityEventRevision = true;
                presentation.ConsumedAuthorityEventRevision = authorityRevision;
                presentation.ConsumedAuthorityEventCount = SaturatingIncrement(
                    presentation.ConsumedAuthorityEventCount);
                presentation.AuthorityRevision = authorityRevision;
                presentation.AuthoritySevered = true;
                presentation.PresentationEventId = definition.PresentationEventId;
                presentation.Status =
                    CharacterTestSeverancePresentationStatus.RuntimeSliceNoStableResource;
                consumedEventCount++;
            }

            bool anyAuthoritySevered = false;
            if (bodyPartState.InitializationStatus
                == CharacterTestBodyPartStateInitializationStatus.Ready)
            {
                for (int i = 0; i < bodyPartState.DefinitionCount; i++)
                {
                    if (!bodyPartState.TryGetDefinition(i, out CharacterTestBodyPartStateDefinition definition)
                        || !definition.Severable
                        || !bodyPartState.TryGetCurrentEntry(i, out CharacterTestBodyPartStateEntry state))
                    {
                        continue;
                    }

                    ref SeverancePresentationEntry presentation = ref GetOrCreateSeveranceEntry(
                        definition.PartStateId,
                        out bool available);
                    if (!available)
                        continue;

                    bool authoritySevered = state.StructureState
                        == CharacterTestBodyPartStructureState.Severed;
                    bool changed = presentation.AuthorityRevision != state.Revision
                        || presentation.AuthoritySevered != authoritySevered;
                    if (changed && authoritySevered)
                    {
                        presentation.CurrentStateProjectionCount = SaturatingIncrement(
                            presentation.CurrentStateProjectionCount);
                        currentStateProjectionCount++;
                    }

                    presentation.AuthorityRevision = state.Revision;
                    presentation.AuthoritySevered = authoritySevered;
                    presentation.PresentationEventId = definition.PresentationEventId;
                    presentation.Status = authoritySevered
                        ? CharacterTestSeverancePresentationStatus.RuntimeSliceNoStableResource
                        : CharacterTestSeverancePresentationStatus.None;
                    anyAuthoritySevered |= authoritySevered;
                }
            }
            else
            {
                SynchronizeUnavailableSeveranceAuthority();
            }

            return new CharacterTestSeverancePresentationSyncResult(
                anyAuthoritySevered
                    ? CharacterTestSeverancePresentationStatus.RuntimeSliceNoStableResource
                    : CharacterTestSeverancePresentationStatus.None,
                consumedEventCount,
                duplicateEventCount,
                currentStateProjectionCount);
        }

        internal bool TryGetSeverancePresentationSnapshot(
            int partStateId,
            out CharacterTestSeverancePresentationSnapshot snapshot)
        {
            for (int i = 0; i < _severancePresentationEntryCount; i++)
            {
                SeverancePresentationEntry entry = _severancePresentationEntries[i];
                if (!(entry.PartStateId == partStateId))
                    continue;

                snapshot = new CharacterTestSeverancePresentationSnapshot(
                    EntityId.Value,
                    entry.PartStateId,
                    entry.AuthorityRevision,
                    entry.AuthoritySevered,
                    entry.PresentationEventId,
                    entry.Status,
                    entry.ConsumedAuthorityEventCount,
                    entry.DuplicateAuthorityEventCount,
                    entry.CurrentStateProjectionCount);
                return true;
            }

            snapshot = default;
            return false;
        }

        internal CharacterTestRuntimeCharacterAnimationDiagnostics CreateAnimationDiagnostics()
        {
            int rendererCount = 0;
            int enabledRendererCount = 0;
            int skinnedRendererCount = 0;
            int enabledSkinnedRendererCount = 0;
            if (Instance != null)
            {
                Renderer[] renderers = Instance.GetComponentsInChildren<Renderer>(includeInactive: true);
                rendererCount = renderers.Length;
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null)
                        continue;

                    bool enabled = renderer.enabled && renderer.gameObject.activeInHierarchy;
                    if (enabled)
                        enabledRendererCount++;

                    if (renderer is SkinnedMeshRenderer)
                    {
                        skinnedRendererCount++;
                        if (enabled)
                            enabledSkinnedRendererCount++;
                    }
                }
            }

            MxAnimationBackendResult lastAnimationResult = _locomotionController != null
                ? _locomotionController.LastAnimationResult
                : default;
            MxAnimationBackendResult lastActionResult = _locomotionController != null
                ? _locomotionController.LastActionAnimationResult
                : default;

            return new CharacterTestRuntimeCharacterAnimationDiagnostics(
                EntityId.Value,
                Instance != null ? Instance.name : string.Empty,
                Instance != null,
                rendererCount,
                enabledRendererCount,
                skinnedRendererCount,
                enabledSkinnedRendererCount,
                _locomotionController != null,
                _locomotionController != null && _locomotionController.HasAnimationBackend,
                _locomotionController != null ? _locomotionController.ActiveBlendId : string.Empty,
                lastAnimationResult.Success,
                lastAnimationResult.Code.ToString(),
                lastAnimationResult.ClipKey.Id,
                _locomotionController != null ? _locomotionController.LastActionKey : string.Empty,
                lastActionResult.Success,
                lastActionResult.Code.ToString(),
                lastActionResult.ClipKey.Id);
        }

        public void SyncTransform(
            Vector3 position,
            Quaternion rotation,
            bool smoothNetworkSnapshot = false,
            RuntimeFrame frame = default)
        {
            if (Instance == null)
                return;

            if (smoothNetworkSnapshot)
            {
                RecordNetworkPresentationSample(frame, position, rotation);
                return;
            }

            Transform transform = Instance.transform;
            bool poseChanged = !HasSamePose(transform, position, rotation);
            bool wasNetworkPresentationActive = _networkPresentationActive;
            DisableNetworkPresentation();
            if (!poseChanged && !wasNetworkPresentationActive)
                return;

            bool teleported = (transform.position - position).sqrMagnitude
                > NetworkPresentationSnapDistance * NetworkPresentationSnapDistance;
            transform.SetPositionAndRotation(position, rotation);
            _presentationLod?.RefreshTransform(_presentationLodHandle, EntityId.Value);
            if (teleported) ResetClothAfterTeleport();
            _transformApplyCount++;
            _headPad?.SyncWorldAnchor(ResolveHeadPadWorldPosition());
        }

        public void TickNetworkPresentation(
            double deltaTime,
            int tickRate,
            int interpolationDelayFrames)
        {
            if (Instance == null || !_networkPresentationActive || _networkPresentationSamples.Count == 0)
                return;

            int safeTickRate = tickRate > 0 ? tickRate : 60;
            int safeDelayFrames = interpolationDelayFrames > 0 ? interpolationDelayFrames : 1;
            double maxStep = Math.Max(0d, deltaTime) * safeTickRate;
            double latestVisibleFrame =
                _networkPresentationSamples[_networkPresentationSamples.Count - 1].Frame - safeDelayFrames;
            if (double.IsNaN(_networkPresentationFrame))
                _networkPresentationFrame = _networkPresentationSamples[0].Frame;
            if (_networkPresentationFrame < _networkPresentationSamples[0].Frame)
                _networkPresentationFrame = _networkPresentationSamples[0].Frame;
            if (_networkPresentationFrame < latestVisibleFrame)
            {
                double lagFrames = latestVisibleFrame - _networkPresentationFrame;
                if (lagFrames > NetworkPresentationCatchUpLagFrames)
                {
                    _networkPresentationFrame = latestVisibleFrame;
                }
                else
                {
                    double step = lagFrames > 1d
                        ? maxStep * NetworkPresentationCatchUpMultiplier
                        : maxStep;
                    _networkPresentationFrame = Math.Min(latestVisibleFrame, _networkPresentationFrame + step);
                }
            }

            ApplyNetworkPresentation(_networkPresentationFrame);
        }

        public void TickPresentation(float deltaTime, RuntimeFrame frame = default)
        {
            TickPresentation(
                deltaTime,
                frame,
                CharacterTestMainCameraCache.Resolve(ref _cachedMainCamera));
        }

        internal void TickPresentation(float deltaTime, RuntimeFrame frame, UnityCamera camera)
        {
            TickPresentationWithPoseBudget(deltaTime, frame, camera);
        }

        internal void TickRuntimeAiLabReplayAnimationState(float deltaTime, RuntimeFrame frame)
        {
            if (Instance == null)
                return;

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            ClearAuthorityPoseMirrorOffsets();
            RestoreResolvedAuthorityPoseOverrides();
            if (!_animationPaused)
                TickAnimationAtActionTime(safeDeltaTime, frame);
            _boneSync?.ApplyBindings();
            bool movedBones = ApplyResolvedAuthorityPose(frame);
            movedBones |= ApplyAuthorityPoseBoneMirror();
            if (movedBones)
                _boneSync?.ApplyBindings();
            ApplyAuthorityPoseSocketMirror();
            if (_proceduralBoneRig != null && _proceduralBoneRig.HasActivePresentationWork)
                _proceduralBoneRig.Tick(safeDeltaTime);
        }

        public void SyncAuthorityPose(CharacterAuthorityPoseStateComponent authorityPose)
        {
            _authorityPoseSamples.Clear();
            authorityPose?.CopySecondaryPoseSamplesTo(_authorityPoseSamples);
            ClearResolvedAuthorityPoseFrame();
        }

        internal void SyncAuthorityPose(
            CharacterAuthorityPoseStateComponent authorityPose,
            CharacterTestTerrainIkStateComponent terrainIkState,
            CharacterTestResolvedAuthorityPoseFrame resolvedFrame,
            RuntimeFrame expectedRuntimeFrame)
        {
            _authorityPoseSamples.Clear();
            authorityPose?.CopySecondaryPoseSamplesTo(_authorityPoseSamples);
            ClearResolvedAuthorityPoseFrame();
            if (_authorityPoseRig == null
                || terrainIkState == null
                || terrainIkState.OwnerEntityId != EntityId.Value
                || !terrainIkState.IsConfigured
                || resolvedFrame == null
                || !ReferenceEquals(terrainIkState.ResolvedFrame, resolvedFrame)
                || !resolvedFrame.IsPublished
                || resolvedFrame.RuntimeFrame != expectedRuntimeFrame.Value
                || resolvedFrame.Revision <= 0L
                || resolvedFrame.Revision != terrainIkState.LastPublishedRevision
                || resolvedFrame.DefinitionRevision != terrainIkState.DefinitionRevision
                || resolvedFrame.DefinitionRevision != _authorityPoseRig.Revision
                || !string.Equals(
                    resolvedFrame.DefinitionContentHash,
                    terrainIkState.DefinitionContentHash,
                    StringComparison.Ordinal)
                || !string.Equals(
                    resolvedFrame.DefinitionContentHash,
                    _authorityPoseRig.ContentHash,
                    StringComparison.Ordinal)
                || resolvedFrame.BaseSourceRevision <= 0
                || resolvedFrame.BaseSourceRevision != terrainIkState.BaseSourceRevision
                || string.IsNullOrWhiteSpace(resolvedFrame.BaseSourceContentHash)
                || !string.Equals(
                    resolvedFrame.BaseSourceContentHash,
                    terrainIkState.BaseSourceContentHash,
                    StringComparison.Ordinal))
            {
                return;
            }

            _resolvedAuthorityPoseFrame = resolvedFrame;
            _resolvedAuthorityPoseExpectedRuntimeFrame = expectedRuntimeFrame.Value;
            _resolvedAuthorityPoseExpectedRevision = resolvedFrame.Revision;
            _resolvedAuthorityPoseExpectedBaseSourceRevision = resolvedFrame.BaseSourceRevision;
            _resolvedAuthorityPoseExpectedBaseSourceContentHash = resolvedFrame.BaseSourceContentHash;
        }

        public bool TryApplyProceduralBoneImpulse(CharacterProceduralBoneImpulseRequest request)
        {
            if (Instance == null || _proceduralBoneRig == null)
                return false;

            _proceduralBoneRig.ApplyImpulse(request);
            return true;
        }

        public bool TryApplyProceduralSecondaryBoneImpulse(CharacterProceduralBoneImpulseRequest request)
        {
            if (Instance == null || _proceduralBoneRig == null)
                return false;

            _proceduralBoneRig.ApplySecondaryBoneImpulse(request);
            return true;
        }

        public bool TryResolveNearestProceduralBodyRegion(Vector3 worldPosition, out int regionId)
        {
            regionId = 0;
            return Instance != null
                && _proceduralBoneRig != null
                && _proceduralBoneRig.TryResolveNearestBodyRegion(worldPosition, out regionId);
        }

        public CharacterProceduralBoneRigSnapshot CreateProceduralBoneRigSnapshot()
        {
            return Instance == null || _proceduralBoneRig == null
                ? CharacterProceduralBoneRigSnapshot.Empty("procedural bone rig is not available")
                : _proceduralBoneRig.CreateSnapshot();
        }

        internal CharacterTestRuntimeCharacterPresentationState CapturePresentationState()
        {
            EnsurePlaybackBoneCache();
            int count = _playbackBoneTransforms != null ? _playbackBoneTransforms.Length : 0;
            var entries = count > 0
                ? new CharacterTestBonePoseEntry[count]
                : Array.Empty<CharacterTestBonePoseEntry>();
            for (int i = 0; i < count; i++)
            {
                Transform bone = _playbackBoneTransforms[i];
                entries[i] = bone != null
                    ? new CharacterTestBonePoseEntry(
                        _playbackBonePaths[i],
                        bone.localPosition,
                        bone.localRotation,
                        bone.localScale)
                    : new CharacterTestBonePoseEntry(_playbackBonePaths[i], Vector3.zero, Quaternion.identity, Vector3.one);
            }

            return new CharacterTestRuntimeCharacterPresentationState(
                EntityId.Value,
                _locomotionController != null ? _locomotionController.CapturePlaybackState() : null,
                _proceduralBoneRig != null ? _proceduralBoneRig.CapturePlaybackState() : null,
                new CharacterTestBonePoseState(entries),
                _actionPlaybackTimeSeconds);
        }

        internal bool RestorePresentationState(
            CharacterTestRuntimeCharacterPresentationState state,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (state == null || state.EntityId != EntityId.Value)
            {
                diagnostic = "Character presentation state entity does not match.";
                return false;
            }

            ClearAuthorityPoseMirrorOffsets();
            RestoreResolvedAuthorityPoseOverrides();
            if (_locomotionController != null && state.Animation != null
                && !_locomotionController.RestorePlaybackState(state.Animation, out diagnostic))
            {
                return false;
            }

            _actionPlaybackTimeSeconds = state.ActionPlaybackTimeSeconds;
            _boneSync?.ApplyBindings();
            bool movedBones = ApplyAuthorityPoseBoneMirror();
            if (movedBones)
                _boneSync?.ApplyBindings();
            ApplyAuthorityPoseSocketMirror();
            if (_proceduralBoneRig != null && state.Procedural != null
                && !_proceduralBoneRig.RestorePlaybackState(state.Procedural, out diagnostic))
            {
                return false;
            }

            if (!RestoreBonePose(state.BonePose, out diagnostic))
                return false;
            _boneSync?.ApplyBindings();
            ResetClothAfterTeleport();
            _headPad?.SyncWorldAnchor(ResolveHeadPadWorldPosition());
            return true;
        }

        internal bool RestoreCanonicalAnimation(
            int bindingId,
            int localFrame,
            int tickRate,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (_locomotionController == null || bindingId <= 0)
            {
                diagnostic = "Canonical animation restore is missing a binding identity or animation controller.";
                return false;
            }

            float startOffset = Mathf.Max(0, localFrame) / (float)Mathf.Max(1, tickRate);
            bool accepted = _locomotionController.TryCrossFadeAction(
                bindingId,
                fadeDurationSeconds: 0f,
                targetStartOffsetSeconds: startOffset,
                playbackSpeed: 1f,
                correlationId: "character-test.animation-canonical-restore");
            if (!accepted)
            {
                diagnostic = "Canonical animation restore could not resolve the action clip.";
                return false;
            }

            _locomotionController.EvaluateCurrentAnimationPose();
            return true;
        }

        private void EnsurePlaybackBoneCache()
        {
            if (_playbackBoneTransforms != null && _playbackBonePaths != null)
                return;
            if (Instance == null)
            {
                _playbackBoneTransforms = Array.Empty<Transform>();
                _playbackBonePaths = Array.Empty<string>();
                return;
            }

            Transform root = _animator != null ? _animator.transform : Instance.transform;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
            var bindings = new List<PlaybackBoneBinding>(transforms.Length);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform != null)
                    bindings.Add(new PlaybackBoneBinding(BuildRelativePath(root, transform), transform));
            }

            bindings.Sort(PlaybackBoneBindingComparer.Instance);
            _playbackBoneTransforms = new Transform[bindings.Count];
            _playbackBonePaths = new string[bindings.Count];
            for (int i = 0; i < bindings.Count; i++)
            {
                _playbackBonePaths[i] = bindings[i].Path;
                _playbackBoneTransforms[i] = bindings[i].Transform;
            }
        }

        private bool RestoreBonePose(CharacterTestBonePoseState state, out string diagnostic)
        {
            diagnostic = string.Empty;
            EnsurePlaybackBoneCache();
            IReadOnlyList<CharacterTestBonePoseEntry> entries = state != null
                ? state.Entries
                : Array.Empty<CharacterTestBonePoseEntry>();
            if (entries.Count != _playbackBoneTransforms.Length)
            {
                diagnostic = "Character bone pose count does not match.";
                return false;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                CharacterTestBonePoseEntry entry = entries[i];
                if (!string.Equals(entry.Path, _playbackBonePaths[i], StringComparison.Ordinal))
                {
                    diagnostic = "Character bone pose path does not match at index " + i + ".";
                    return false;
                }

                Transform bone = _playbackBoneTransforms[i];
                if (bone == null)
                    continue;
                bone.localPosition = entry.LocalPosition;
                bone.localRotation = entry.LocalRotation;
                bone.localScale = entry.LocalScale;
            }

            return true;
        }

        private static string BuildRelativePath(Transform root, Transform target)
        {
            if (root == null || target == null || target == root)
                return string.Empty;
            var segments = new List<string>(8);
            Transform current = target;
            while (current != null && current != root)
            {
                segments.Add(current.name);
                current = current.parent;
            }
            segments.Reverse();
            return string.Join("/", segments);
        }

        private readonly struct PlaybackBoneBinding
        {
            public PlaybackBoneBinding(string path, Transform transform)
            {
                Path = path ?? string.Empty;
                Transform = transform;
            }

            public string Path { get; }
            public Transform Transform { get; }
        }

        private sealed class PlaybackBoneBindingComparer : IComparer<PlaybackBoneBinding>
        {
            public static readonly PlaybackBoneBindingComparer Instance = new PlaybackBoneBindingComparer();

            public int Compare(PlaybackBoneBinding x, PlaybackBoneBinding y)
            {
                return string.CompareOrdinal(x.Path, y.Path);
            }
        }

        public bool TryResolveVfxAnchor(int socketId, out Vector3 position, out Vector3 forward)
        {
            position = Vector3.zero;
            forward = Vector3.forward;
            if (Instance == null)
                return false;

            if (socketId > 0 && TryResolveSocketAnchor(socketId, out position, out forward))
                return true;

            Transform transform = Instance.transform;
            forward = ResolveInstanceForward();
            position = transform.position
                + forward * CharacterTestActionVfxAnchor.ForwardOffsetMeters
                + Vector3.up * CharacterTestActionVfxAnchor.HeightOffsetMeters;
            return true;
        }

        internal bool TryResolveAbstractBoneAnchor(
            string abstractBoneId,
            out Vector3 position,
            out Vector3 forward)
        {
            position = Vector3.zero;
            forward = Vector3.forward;
            if (_abstractBoneMap == null
                || !_abstractBoneMap.TryResolve(abstractBoneId, out Transform anchor)
                || anchor == null)
                return false;

            position = anchor.position;
            forward = ResolveInstanceForward();
            return true;
        }

        internal bool TryResolveControlStatusVfxAnchor(out Vector3 position, out Vector3 forward)
        {
            position = Vector3.zero;
            forward = Vector3.forward;
            if (Instance == null)
                return false;

            forward = ResolveInstanceForward();
            Transform head = ResolveHeadTransform();
            if (head != null)
            {
                position = head.position + Vector3.up * ControlStatusHeadBoneOffsetMeters;
                return true;
            }
            return false;
        }

        internal bool TryResolveTargetLockUiAnchor(out Vector3 position)
        {
            position = Vector3.zero;
            if (Instance == null)
                return false;

            Bounds bounds;
            if (TryResolveRendererBounds(out bounds))
            {
                position = new Vector3(
                    Instance.transform.position.x,
                    Mathf.Lerp(bounds.min.y, bounds.max.y, TargetLockUiRendererBoundsHeightRatio),
                    Instance.transform.position.z);
                return true;
            }

            position = Instance.transform.position + Vector3.up * TargetLockUiFallbackHeightMeters;
            return true;
        }

        internal bool TryResolveBodyPartContactPointWorldPosition(
            CharacterBodyPartContactPointConfig config,
            out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (Instance == null
                || config == null
                || config.MotionMode != CharacterBodyPartContactMotionMode.BakedSocket
                || (config.MotionSocketId == 0))
            {
                return false;
            }

            Transform anchor = ResolveSocketTarget(config.MotionSocketId);
            if (anchor == null)
                return false;

            if (TryResolveBodyPartContactPointTarget(config, anchor, out Transform contactPointTarget))
            {
                worldPosition = contactPointTarget.position;
                return true;
            }

            Vector3 offset = new Vector3(
                config.LocalOffsetXMillimeters / 1000f,
                config.LocalOffsetYMillimeters / 1000f,
                config.LocalOffsetZMillimeters / 1000f);
            worldPosition = anchor.TransformPoint(offset);
            return true;
        }

        private Vector3 ResolveInstanceForward()
        {
            if (Instance == null)
                return Vector3.forward;

            Vector3 forward = Instance.transform.forward;
            return forward.sqrMagnitude > 0.000001f
                ? forward.normalized
                : Vector3.forward;
        }

        private bool TryResolveSocketAnchor(int socketId, out Vector3 position, out Vector3 forward)
        {
            position = Vector3.zero;
            forward = Vector3.forward;
            Transform anchor = ResolveSocketAnchorTransform(socketId);
            if (anchor == null)
                return false;

            position = anchor.position;
            forward = anchor.forward.sqrMagnitude > 0.000001f
                ? anchor.forward.normalized
                : Vector3.forward;
            return true;
        }

        public void SyncCombatState(
            CharacterTestCharacterCombatStateComponent combatState,
            string displayName,
            int teamId,
            CharacterTestCharacterControlOwner controlOwner,
            bool hasLocalPlayerTeam,
            int localPlayerTeamId,
            bool headPadLockedVisible = false,
            CharacterTestHeadPadDisplayMode headPadDisplayMode =
                CharacterTestHeadPadDisplayMode.CombatReadout,
            IReadOnlyList<GameplayBuffPresentationItem> headPadBuffs = null,
            RuntimeFrame frame = default)
        {
            if (Instance == null)
                return;

            _presentationLod?.SetOwnerImportance(_presentationLodHandle, EntityId.Value,
                controlOwner == CharacterTestCharacterControlOwner.LocalPlayer
                || controlOwner == CharacterTestCharacterControlOwner.RemotePlayer);
            CharacterTestHeadPadDisplayMode safeDisplayMode =
                CharacterTestHeadPadDisplayModes.Normalize(headPadDisplayMode);
            if (ResolveHeadTransform() == null)
                safeDisplayMode = CharacterTestHeadPadDisplayMode.Hidden;
            Vector3 headPadWorldPosition = ResolveHeadPadWorldPosition();
            if (Application.isPlaying && safeDisplayMode != CharacterTestHeadPadDisplayMode.Hidden)
                EnsureHeadPad();
            else if (safeDisplayMode == CharacterTestHeadPadDisplayMode.Hidden)
                ReleaseHeadPad();

            _headPad?.Sync(
                EntityId.Value,
                displayName,
                teamId,
                controlOwner,
                ResolveAllegiance(teamId, controlOwner, hasLocalPlayerTeam, localPlayerTeamId),
                combatState,
                headPadLockedVisible,
                safeDisplayMode,
                headPadBuffs,
                headPadWorldPosition,
                frame.Value);
        }

        internal void SyncActionPlaybackTime(double timeSeconds)
        {
            _actionPlaybackTimeSeconds = timeSeconds;
        }

        /// <summary>Low-frequency Player acceptance sampling; does not write the authority pose.</summary>
        internal bool TryGetHumanoidPresentationHeights(out float rootY, out float hipsY, out float feetY)
        {
            rootY = hipsY = feetY = 0f;
            if (Instance == null || _animator == null || !_animator.isHuman) return false;
            Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform left = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform right = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (hips == null || left == null || right == null) return false;
            rootY = Instance.transform.position.y;
            hipsY = hips.position.y;
            feetY = Mathf.Min(left.position.y, right.position.y);
            return true;
        }

        private void TickAnimationAtActionTime(float deltaTime, RuntimeFrame frame)
        {
            if (_actionPlaybackTimeSeconds >= 0d)
                _locomotionController?.TrySynchronizeActionPlaybackTime(_actionPlaybackTimeSeconds);
            _locomotionController?.TickRuntimeAnimation(deltaTime, ToPresentationFrame(frame));
        }

        public void SyncAnimation(
            CharacterTestCharacterAnimationComponent animation,
            RuntimeFrame frame = default,
            CharacterTestTraceRecorder traceRecorder = null,
            bool presentationPaused = false,
            int turnInPlaceYawMilliDegrees = 0,
            bool requireExactActionClip = false)
        {
            bool wasAnimationPaused = _animationPaused;
            _animationPaused = presentationPaused;
            if (wasAnimationPaused != presentationPaused)
                _presentationLod?.Discontinuity(_presentationLodHandle);
            if (_animationPaused)
            {
                _locomotionTransitions.SynchronizeWithoutTransition(animation);
                if (!wasAnimationPaused)
                {
                    traceRecorder?.AttackView(
                        frame,
                        EntityId.Value,
                        BuildLastActionTraceMessage(
                            "stasis pause animation lastAction=",
                            _locomotionController != null ? _locomotionController.LastActionKey : string.Empty));
                }

                return;
            }

            if (_locomotionController != null)
            {
                _locomotionController.SetExternalMotion(
                    animation.LocomotionBlend,
                    animation.LocomotionSpeed01);
                bool canPlayLocomotionTransition = animation.Grounded
                    && !animation.Airborne
                    && !animation.ActionActive
                    && animation.RequestBindingId == 0;
                if (canPlayLocomotionTransition)
                {
                    _locomotionTransitions.TryApply(
                        animation,
                        turnInPlaceYawMilliDegrees,
                        frame,
                        EntityId.Value,
                        traceRecorder);
                }
                else
                {
                    _locomotionTransitions.SynchronizeWithoutTransition(animation);
                }
                bool locomotionSuppressed = _locomotionController.IsLocomotionBlendSuppressed;
                string lastActionKey = _locomotionController.LastActionKey;
                if (traceRecorder != null
                    && ShouldRecordLocomotionTrace(animation, locomotionSuppressed, lastActionKey))
                {
                    traceRecorder.Locomotion(
                        frame,
                        EntityId.Value,
                        BuildLocomotionTraceMessage(animation, locomotionSuppressed, lastActionKey));
                }

                if (animation.RequestBindingId > 0)
                {
                    float suppressionSeconds = ResolveLocomotionSuppressionSeconds(animation);
                    bool accepted = _locomotionController.TryCrossFadeAction(
                        animation.BindingId,
                        animation.ActionTransitionSeconds,
                        targetStartOffsetSeconds: 0f,
                        playbackSpeed: animation.ActionPlaybackSpeed,
                        suppressLocomotionBlendSeconds: suppressionSeconds,
                        correlationId: "character-test.action:" + EntityId.Value + ":" + animation.ActionKey,
                        allowFallback: !requireExactActionClip);
                    MxAnimationBackendResult result = _locomotionController.LastActionAnimationResult;
                    bool fallbackClip = accepted
                        && !string.IsNullOrEmpty(result.ClipKey.Id)
                        && !string.Equals(result.ClipKey.Id, animation.ActionKey, System.StringComparison.Ordinal);
                    bool error = !accepted || !result.Success;
                    bool warning = error || fallbackClip;
                    traceRecorder?.AttackView(
                        frame,
                        EntityId.Value,
                        "crossFade key=" + animation.ActionKey
                        + " accepted=" + accepted
                        + " backend=" + _locomotionController.HasAnimationBackend
                        + " result=" + result.Code
                        + " clip=" + EmptyAsNone(result.ClipKey.Id)
                        + " lastAction=" + EmptyAsNone(_locomotionController.LastActionKey)
                        + " suppressed=" + _locomotionController.IsLocomotionBlendSuppressed
                        + " hold=" + animation.ActionHoldSeconds.ToString("F2", CultureInfo.InvariantCulture)
                        + " suppression=" + suppressionSeconds.ToString("F2", CultureInfo.InvariantCulture)
                        + " message=" + EmptyAsNone(result.Message),
                        warning);
                    if (warning)
                        LogAnimationIssueOnce(animation.ActionKey, accepted, result, error);
                }
                else if (animation.ActionActive && !string.IsNullOrWhiteSpace(_locomotionController.LastActionKey))
                {
                    float refreshSeconds = ResolveActionActiveRefreshSuppressionSeconds(_locomotionController.LastActionKey);
                    bool lightAttackSuppressionExpired =
                        IsLightAttackLocomotionActionKey(_locomotionController.LastActionKey)
                        && !_locomotionController.IsLocomotionBlendSuppressed;
                    if (refreshSeconds > 0f && ShouldTraceActionActiveWithoutKey(frame))
                    {
                        traceRecorder?.AttackView(
                            frame,
                            EntityId.Value,
                            BuildLastActionTraceMessage(
                                lightAttackSuppressionExpired
                                    ? "light attack action active after locomotion hold expired; refreshing hold lastAction="
                                    : "action active without action key; refreshing hold lastAction=",
                                _locomotionController.LastActionKey),
                            lightAttackSuppressionExpired);
                    }

                    if (refreshSeconds > 0f)
                        _locomotionController.SuppressLocomotionBlendForSeconds(refreshSeconds);
                }
                else if (!animation.ActionActive
                    && !(canPlayLocomotionTransition && _locomotionTransitions.IsPlaying))
                {
                    bool hadActionHold = _locomotionController.IsLocomotionBlendSuppressed
                        || !string.IsNullOrWhiteSpace(_locomotionController.LastActionKey);
                    if (hadActionHold)
                    {
                        string resumedActionKey = _locomotionController.LastActionKey;
                        _locomotionController.ResumeLocomotionBlend();
                        traceRecorder?.AttackView(
                            frame,
                            EntityId.Value,
                            BuildLastActionTraceMessage("resume locomotion lastAction=", resumedActionKey));
                    }
                }
            }
            else
            {
                traceRecorder?.AttackView(frame, EntityId.Value, "missing locomotion controller", warning: true);
            }
        }

        internal static float ResolveLocomotionSuppressionSeconds(
            CharacterTestCharacterAnimationComponent animation)
        {
            float holdSeconds = animation.ActionHoldSeconds;
            if (holdSeconds <= 0f)
                return 0f;

            if (IsJumpLocomotionActionKey(animation.ActionKey))
                return Mathf.Min(holdSeconds, JumpActionLocomotionSuppressionSeconds);

            return holdSeconds;
        }

        private static float ResolveActionActiveRefreshSuppressionSeconds(string actionKey)
        {
            if (string.IsNullOrWhiteSpace(actionKey))
                return 0f;
            if (IsJumpLocomotionActionKey(actionKey))
                return 0f;

            return ActiveActionHoldRefreshSeconds;
        }

        private static bool IsJumpLocomotionActionKey(string actionKey)
        {
            return string.Equals(
                    actionKey,
                    CharacterTestCharacterActionConfigSource.JumpAnimationActionKey,
                    System.StringComparison.Ordinal)
                || string.Equals(
                    actionKey,
                    CharacterTestCharacterActionConfigSource.JumpAnimationClipResourceKey,
                    System.StringComparison.Ordinal);
        }

        private static bool IsLightAttackLocomotionActionKey(string actionKey)
        {
            if (string.IsNullOrWhiteSpace(actionKey))
                return false;

            switch (actionKey)
            {
                case CharacterTestCharacterActionConfigSource.LightAttackAnimationActionKey:
                case CharacterTestCharacterActionConfigSource.LightAttackAnimationClipResourceKey:
                case "art.character.skeleton.animation.katana_atk2":
                case "art.character.skeleton.animation.katana_atk3":
                    return true;
                default:
                    return false;
            }
        }

        private bool ShouldTraceActionActiveWithoutKey(RuntimeFrame frame)
        {
            long value = frame.Value;
            if (value < 0L)
                value = 0L;
            if (value - _lastActionActiveWithoutKeyTraceFrame < 30L)
                return false;

            _lastActionActiveWithoutKeyTraceFrame = value;
            return true;
        }

        public void Release()
        {
            for (int i = 0; i < _clothGroups.Count; i++)
            {
                ICharacterClothGroup group = _clothGroups[i];
                if (!(group is UnityEngine.Object obj) || obj != null)
                    group.SetPresentationOverride(CharacterClothSystemOverride.DisabledReset);
                _clothPresentation?.Unregister(group, restoreLocalState: false);
            }
            _clothGroups.Clear();
            _clothPresentation = null;
            _presentationLod?.Unregister(_presentationLodHandle, EntityId.Value);
            _presentationLod = null;
            _presentationLodHandle = default;
            ResetSeverancePresentation();
            _locomotionTransitions.Reset();
            if (Instance != null)
            {
                ClearAuthorityPoseMirrorOffsets();
                RestoreResolvedAuthorityPoseOverrides();
                ClearResolvedAuthorityPoseFrame();
                if (_locomotionController != null)
                    _locomotionController.ClearExternalMotion();

                ReleaseHeadPad();

                CharacterDefaultEquipmentRuntimeBinder binder =
                    Instance.GetComponent<CharacterDefaultEquipmentRuntimeBinder>();
                if (binder != null)
                    binder.ClearSpawnedWeapons();

                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(Instance);
                else
                    UnityEngine.Object.DestroyImmediate(Instance);
                Instance = null;
                _cachedMainCamera = null;
                _socketAnchorCache?.Clear();
                _socketAnchorCacheResolved = false;
                _bodyPartContactPointTargetCache.Clear();
                _socketTargetCache.Clear();
                _authorityPoseChannelCache.Clear();
                _cachedHeadTransform = null;
                _headTransformResolved = false;
                _cachedRendererBoundsRenderers = null;
                _rendererBoundsResolved = false;
            }

            if (_characterHandle != null && _resourceManager != null)
            {
                _resourceManager.Release(_characterHandle);
                _characterHandle = null;
            }
        }

        private ref SeverancePresentationEntry GetOrCreateSeveranceEntry(
            int partStateId,
            out bool available)
        {
            for (int i = 0; i < _severancePresentationEntryCount; i++)
            {
                if ((_severancePresentationEntries[i].PartStateId == partStateId))
                {
                    available = true;
                    return ref _severancePresentationEntries[i];
                }
            }

            if (_severancePresentationEntryCount >= _severancePresentationEntries.Length)
            {
                available = false;
                return ref _severancePresentationEntries[0];
            }

            int index = _severancePresentationEntryCount++;
            _severancePresentationEntries[index] = new SeverancePresentationEntry(partStateId);
            available = true;
            return ref _severancePresentationEntries[index];
        }

        private void SynchronizeUnavailableSeveranceAuthority()
        {
            for (int i = 0; i < _severancePresentationEntryCount; i++)
            {
                _severancePresentationEntries[i].AuthorityRevision = 0L;
                _severancePresentationEntries[i].AuthoritySevered = false;
                _severancePresentationEntries[i].Status =
                    CharacterTestSeverancePresentationStatus.None;
            }
        }

        private void ResetSeverancePresentation()
        {
            Array.Clear(
                _severancePresentationEntries,
                0,
                _severancePresentationEntries.Length);
            _severancePresentationEntryCount = 0;
        }

        private static int SaturatingIncrement(int value)
        {
            return value == int.MaxValue ? int.MaxValue : value + 1;
        }

        private struct SeverancePresentationEntry
        {
            public SeverancePresentationEntry(int partStateId)
            {
                PartStateId = partStateId;
                AuthorityRevision = 0L;
                AuthoritySevered = false;
                PresentationEventId = string.Empty;
                Status = CharacterTestSeverancePresentationStatus.None;
                HasConsumedAuthorityEventRevision = false;
                ConsumedAuthorityEventRevision = 0L;
                ConsumedAuthorityEventCount = 0;
                DuplicateAuthorityEventCount = 0;
                CurrentStateProjectionCount = 0;
            }

            public int PartStateId;
            public long AuthorityRevision;
            public bool AuthoritySevered;
            public string PresentationEventId;
            public CharacterTestSeverancePresentationStatus Status;
            public bool HasConsumedAuthorityEventRevision;
            public long ConsumedAuthorityEventRevision;
            public int ConsumedAuthorityEventCount;
            public int DuplicateAuthorityEventCount;
            public int CurrentStateProjectionCount;
        }

        private bool ApplyAuthorityPoseBoneMirror()
        {
            if (Instance == null || _authorityPoseSamples.Count == 0)
                return false;

            bool moved = false;
            for (int i = 0; i < _authorityPoseSamples.Count; i++)
            {
                CharacterAuthoritySecondaryPoseSample binding = _authorityPoseSamples[i];
                if ((binding.ChannelId == 0))
                    continue;
                if (IsAuthoredAuthorityPoseChannel(binding.ChannelId))
                    continue;

                Transform channel = ResolveAuthorityPoseChannel(binding.ChannelId);
                if (channel == null)
                    continue;

                if (ApplyAuthorityPoseOffset(channel, binding))
                    moved = true;
            }

            return moved;
        }

        private void ApplyAuthorityPoseSocketMirror()
        {
            if (Instance == null || _authorityPoseSamples.Count == 0)
                return;

            for (int i = 0; i < _authorityPoseSamples.Count; i++)
            {
                CharacterAuthoritySecondaryPoseSample binding = _authorityPoseSamples[i];
                if ((binding.SocketId == 0))
                    continue;
                if (IsAuthoredAuthorityPoseChannel(binding.ChannelId)
                    || IsAuthoredAuthorityPoseSocket(binding.SocketId))
                {
                    continue;
                }

                Transform target = ResolveSocketTarget(binding.SocketId);
                if (target != null)
                    ApplyAuthorityPoseOffset(target, binding);
            }
        }

        private bool ApplyAuthorityPoseOffset(Transform target, CharacterAuthoritySecondaryPoseSample binding)
        {
            Vector3 localOffset = ToUnityVector(binding.OffsetXRaw, binding.OffsetYRaw, binding.OffsetZRaw);
            if (!IsUsableOffset(localOffset))
                return false;

            Vector3 worldOffset = Instance != null
                ? Instance.transform.TransformDirection(localOffset)
                : localOffset;
            target.position += worldOffset;
            _authorityPoseMirrorOffsets[target] =
                _authorityPoseMirrorOffsets.TryGetValue(target, out Vector3 existing)
                    ? existing + worldOffset
                    : worldOffset;
            return true;
        }

        private void ClearAuthorityPoseMirrorOffsets()
        {
            if (_authorityPoseMirrorOffsets.Count == 0)
                return;

            foreach (KeyValuePair<Transform, Vector3> pair in _authorityPoseMirrorOffsets)
            {
                if (pair.Key != null)
                    pair.Key.position -= pair.Value;
            }

            _authorityPoseMirrorOffsets.Clear();
        }

        private void ResolveAuthoredAuthorityPoseChannels()
        {
            _resolvedAuthorityPoseChannelCount = 0;
            if (_authorityPoseRig == null || Instance == null)
                return;

            Transform authoredPathRoot = _animator != null
                ? _animator.transform
                : Instance.transform;
            CharacterAuthorityPoseChannelRuntimeBinding[] channels = _authorityPoseRig.Channels;
            for (int i = 0; i < channels.Length; i++)
            {
                CharacterAuthorityPoseChannelRuntimeBinding channel = channels[i];
                if (channel == null
                    || (channel.ChannelId == 0)
                    || string.IsNullOrWhiteSpace(channel.BonePath))
                {
                    continue;
                }

                int depth = CountBonePathDepth(channel.BonePath);
                int insert = _resolvedAuthorityPoseChannelCount;
                while (insert > 0 && _resolvedAuthorityPoseBoneDepths[insert - 1] > depth)
                {
                    _resolvedAuthorityPoseChannelIds[insert] =
                        _resolvedAuthorityPoseChannelIds[insert - 1];
                    _resolvedAuthorityPoseBones[insert] = _resolvedAuthorityPoseBones[insert - 1];
                    _resolvedAuthorityPoseBoneDepths[insert] =
                        _resolvedAuthorityPoseBoneDepths[insert - 1];
                    insert--;
                }

                _resolvedAuthorityPoseChannelIds[insert] = channel.ChannelId;
                _resolvedAuthorityPoseBones[insert] = FindAuthoredBoneByExactPath(
                    authoredPathRoot,
                    Instance.transform,
                    channel.BonePath);
                _resolvedAuthorityPoseBoneDepths[insert] = depth;
                _resolvedAuthorityPoseChannelCount++;
            }
        }

        private static Transform FindAuthoredBoneByExactPath(
            Transform searchRoot,
            Transform instanceRoot,
            string bonePath)
        {
            if (searchRoot == null
                || instanceRoot == null
                || string.IsNullOrWhiteSpace(bonePath))
            {
                return null;
            }

            Transform resolved = searchRoot.Find(bonePath);
            if (resolved != null)
                return resolved;

            int separator = bonePath.IndexOf('/');
            string firstSegment = separator >= 0
                ? bonePath.Substring(0, separator)
                : bonePath;
            Transform current = searchRoot;
            while (current != null)
            {
                if (string.Equals(current.name, firstSegment, StringComparison.Ordinal))
                {
                    return separator < 0
                        ? current
                        : current.Find(bonePath.Substring(separator + 1));
                }

                if (current == instanceRoot)
                    break;
                current = current.parent;
            }

            return null;
        }

        private bool ApplyResolvedAuthorityPose(RuntimeFrame presentationFrame)
        {
            CharacterTestResolvedAuthorityPoseFrame frame = _resolvedAuthorityPoseFrame;
            if (Instance == null
                || _authorityPoseRig == null
                || frame == null
                || !frame.IsPublished
                || presentationFrame.Value != _resolvedAuthorityPoseExpectedRuntimeFrame
                || frame.RuntimeFrame != _resolvedAuthorityPoseExpectedRuntimeFrame
                || frame.Revision != _resolvedAuthorityPoseExpectedRevision
                || frame.DefinitionRevision != _authorityPoseRig.Revision
                || !string.Equals(
                    frame.DefinitionContentHash,
                    _authorityPoseRig.ContentHash,
                    StringComparison.Ordinal)
                || frame.BaseSourceRevision != _resolvedAuthorityPoseExpectedBaseSourceRevision
                || !string.Equals(
                    frame.BaseSourceContentHash,
                    _resolvedAuthorityPoseExpectedBaseSourceContentHash,
                    StringComparison.Ordinal))
            {
                return false;
            }

            bool moved = false;
            Transform modelRoot = Instance.transform;
            for (int i = 0; i < _resolvedAuthorityPoseChannelCount; i++)
            {
                Transform bone = _resolvedAuthorityPoseBones[i];
                if (bone == null
                    || !frame.TryGetSample(
                        _resolvedAuthorityPoseChannelIds[i],
                        out CharacterTestResolvedAuthorityPoseSample sample)
                    || !sample.Valid
                    || !CharacterTestAuthorityPoseConsumerMath.TryNormalizeBasis(
                        sample.Basis,
                        out CharacterTestResolvedAuthorityPoseBasis basis))
                {
                    continue;
                }

                Vector3 localPosition = ToUnityVector(
                    sample.Position.X.RawValue,
                    sample.Position.Y.RawValue,
                    sample.Position.Z.RawValue);
                Vector3 localUp = ToUnityVector(
                    basis.Up.X.RawValue,
                    basis.Up.Y.RawValue,
                    basis.Up.Z.RawValue);
                Vector3 localForward = ToUnityVector(
                    basis.Forward.X.RawValue,
                    basis.Forward.Y.RawValue,
                    basis.Forward.Z.RawValue);
                if (!IsFinite(localPosition)
                    || !IsFinite(localUp)
                    || !IsFinite(localForward)
                    || localUp.sqrMagnitude <= 0.000001f
                    || localForward.sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                Quaternion localRotation = Quaternion.LookRotation(localForward, localUp);
                Quaternion worldRotation = modelRoot.rotation * localRotation;
                if (!IsFinite(worldRotation))
                    continue;

                CaptureResolvedAuthorityPoseRestore(bone);
                bone.SetPositionAndRotation(
                    modelRoot.position + modelRoot.rotation * localPosition,
                    worldRotation);
                moved = true;
            }

            return moved;
        }

        private void CaptureResolvedAuthorityPoseRestore(Transform bone)
        {
            for (int i = 0; i < _resolvedAuthorityPoseRestoreCount; i++)
            {
                if (_resolvedAuthorityPoseRestoreBones[i] == bone)
                    return;
            }

            if (bone == null
                || _resolvedAuthorityPoseRestoreCount >= _resolvedAuthorityPoseRestoreBones.Length)
            {
                return;
            }

            int index = _resolvedAuthorityPoseRestoreCount++;
            _resolvedAuthorityPoseRestoreBones[index] = bone;
            _resolvedAuthorityPoseRestorePositions[index] = bone.localPosition;
            _resolvedAuthorityPoseRestoreRotations[index] = bone.localRotation;
        }

        private void RestoreResolvedAuthorityPoseOverrides()
        {
            for (int i = 0; i < _resolvedAuthorityPoseRestoreCount; i++)
            {
                Transform bone = _resolvedAuthorityPoseRestoreBones[i];
                if (bone != null)
                {
                    bone.localPosition = _resolvedAuthorityPoseRestorePositions[i];
                    bone.localRotation = _resolvedAuthorityPoseRestoreRotations[i];
                }

                _resolvedAuthorityPoseRestoreBones[i] = null;
                _resolvedAuthorityPoseRestorePositions[i] = Vector3.zero;
                _resolvedAuthorityPoseRestoreRotations[i] = Quaternion.identity;
            }

            _resolvedAuthorityPoseRestoreCount = 0;
        }

        private void ClearResolvedAuthorityPoseFrame()
        {
            _resolvedAuthorityPoseFrame = null;
            _resolvedAuthorityPoseExpectedRuntimeFrame = -1L;
            _resolvedAuthorityPoseExpectedRevision = 0L;
            _resolvedAuthorityPoseExpectedBaseSourceRevision = 0;
            _resolvedAuthorityPoseExpectedBaseSourceContentHash = string.Empty;
        }

        private bool IsAuthoredAuthorityPoseChannel(int channelId)
        {
            if (channelId <= 0)
                return false;

            for (int i = 0; i < _resolvedAuthorityPoseChannelCount; i++)
            {
                if ((_resolvedAuthorityPoseChannelIds[i] == channelId))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsAuthoredAuthorityPoseSocket(int socketId)
        {
            if (_authorityPoseRig == null || socketId <= 0)
                return false;

            CharacterAuthorityPoseAttachmentRuntimeBinding[] attachments =
                _authorityPoseRig.Attachments;
            for (int i = 0; i < attachments.Length; i++)
            {
                CharacterAuthorityPoseAttachmentRuntimeBinding attachment = attachments[i];
                if (attachment != null
                    && attachment.Kind == CharacterAuthorityPoseRigAttachmentKind.Socket
                    && attachment.StableId == socketId)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountBonePathDepth(string bonePath)
        {
            int depth = 0;
            for (int i = 0; i < bonePath.Length; i++)
            {
                if (bonePath[i] == '/')
                    depth++;
            }
            return depth;
        }

        private void PrepareNumericPoseTargets(CharacterAuthorityPoseRigRuntimeBinding rig)
        {
            _socketTargetCache.Clear();
            _authorityPoseChannelCache.Clear();
            IReadOnlyList<CharacterBoneRuntimeSyncBinding> bindings = _boneSync?.Bindings;
            if (bindings != null)
                for (int i = 0; i < bindings.Count; i++)
                {
                    CharacterBoneRuntimeSyncBinding binding = bindings[i];
                    if (binding == null || binding.SocketId <= 0) continue;
                    Transform target = binding.Target != null ? binding.Target : binding.Bone;
                    if (_socketTargetCache.ContainsKey(binding.SocketId))
                        throw new InvalidOperationException("Duplicate numeric socket binding in character view.");
                    _socketTargetCache.Add(binding.SocketId, target);
                }
            if (rig == null || Instance == null) return;
            Transform searchRoot = _animator != null ? _animator.transform : Instance.transform;
            foreach (CharacterAuthorityPoseChannelRuntimeBinding channel in rig.Channels)
                if (channel != null && channel.ChannelId > 0)
                    _authorityPoseChannelCache.Add(channel.ChannelId,
                        FindAuthoredBoneByExactPath(searchRoot, Instance.transform, channel.BonePath));
        }

        private Transform ResolveAuthorityPoseChannel(int channelId) =>
            Instance != null && channelId > 0 && _authorityPoseChannelCache.TryGetValue(channelId, out Transform target)
                ? target : null;

        private Transform ResolveSocketTarget(int socketId) =>
            socketId > 0 && _socketTargetCache.TryGetValue(socketId, out Transform target)
                ? target : null;

        private bool TryResolveBodyPartContactPointTarget(
            CharacterBodyPartContactPointConfig config,
            Transform socketTarget,
            out Transform target)
        {
            target = null;
            if (Instance == null || config == null || config.ContactPointId <= 0)
                return false;

            int cacheKey = config.ContactPointId;
            if (_bodyPartContactPointTargetCache.TryGetValue(
                    cacheKey,
                    out BodyPartContactPointTargetCacheEntry cached))
            {
                target = cached.Target;
                return target != null;
            }

            string markerName = CreateContactPointMarkerName(config);
            Transform found = FindChildByExactName(socketTarget, markerName);
            if (found == null)
                found = FindChildByExactName(Instance.transform, markerName);

            _bodyPartContactPointTargetCache[cacheKey] =
                new BodyPartContactPointTargetCacheEntry(found);
            target = found;
            return target != null;
        }

        private static string CreateContactPointMarkerName(CharacterBodyPartContactPointConfig contactPoint)
        {
            string source = contactPoint == null ? string.Empty : contactPoint.StableId;
            if (string.IsNullOrWhiteSpace(source) && contactPoint != null && contactPoint.ContactPointId > 0)
                source = "id." + contactPoint.ContactPointId.ToString(CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(source))
                source = "unnamed";

            var builder = new StringBuilder(source.Length + 13);
            builder.Append("ContactPoint_");
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                builder.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' ? c : '_');
            }

            return builder.ToString();
        }

        private static Vector3 ToUnityVector(long xRaw, long yRaw, long zRaw)
        {
            return new Vector3(
                RawToFloat(xRaw),
                RawToFloat(yRaw),
                RawToFloat(zRaw));
        }

        private static float RawToFloat(long raw)
        {
            return (float)((double)raw / Fix64.Scale);
        }

        private static bool IsUsableOffset(Vector3 offset)
        {
            return IsFinite(offset)
                && offset.sqrMagnitude > 0.00000001f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.z)
                && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void RecordNetworkPresentationSample(
            RuntimeFrame frame,
            Vector3 position,
            Quaternion rotation)
        {
            long sampleFrame = frame.Value;
            if (_networkPresentationSamples.Count > 0)
            {
                NetworkPresentationSample latest =
                    _networkPresentationSamples[_networkPresentationSamples.Count - 1];
                if (sampleFrame < latest.Frame)
                    return;
                if (sampleFrame == latest.Frame)
                {
                    _networkPresentationSamples[_networkPresentationSamples.Count - 1] =
                        new NetworkPresentationSample(sampleFrame, position, rotation);
                    return;
                }
            }

            Transform transform = Instance.transform;
            bool snap = _networkPresentationSamples.Count == 0
                || (transform.position - position).sqrMagnitude
                > NetworkPresentationSnapDistance * NetworkPresentationSnapDistance;
            if (snap)
            {
                _networkPresentationSamples.Clear();
                _networkPresentationSamples.Add(new NetworkPresentationSample(sampleFrame, position, rotation));
                _networkPresentationFrame = sampleFrame;
                _networkPresentationActive = true;
                transform.SetPositionAndRotation(position, rotation);
                ResetClothAfterTeleport();
                _headPad?.SyncWorldAnchor(ResolveHeadPadWorldPosition());
                return;
            }

            _networkPresentationSamples.Add(new NetworkPresentationSample(sampleFrame, position, rotation));
            while (_networkPresentationSamples.Count > NetworkPresentationSampleCapacity)
                _networkPresentationSamples.RemoveAt(0);
            if (double.IsNaN(_networkPresentationFrame))
                _networkPresentationFrame = _networkPresentationSamples[0].Frame;
            _networkPresentationActive = true;
        }

        private void ApplyNetworkPresentation(double presentationFrame)
        {
            NetworkPresentationSample from = _networkPresentationSamples[0];
            NetworkPresentationSample to = from;
            for (int i = 1; i < _networkPresentationSamples.Count; i++)
            {
                to = _networkPresentationSamples[i];
                if (presentationFrame <= to.Frame)
                    break;
                from = to;
            }

            float t = to.Frame == from.Frame
                ? 1f
                : Mathf.Clamp01((float)((presentationFrame - from.Frame) / (to.Frame - from.Frame)));
            Instance.transform.SetPositionAndRotation(
                Vector3.Lerp(from.Position, to.Position, t),
                Quaternion.Slerp(from.Rotation, to.Rotation, t));
            _headPad?.SyncWorldAnchor(ResolveHeadPadWorldPosition());
        }

        private void DisableNetworkPresentation()
        {
            if (!_networkPresentationActive)
                return;

            _networkPresentationActive = false;
            _networkPresentationFrame = double.NaN;
            _networkPresentationSamples.Clear();
        }

        private void EnsureHeadPad()
        {
            if (_headPad != null)
                return;

            _headPad = CharacterTestFairyGuiHeadPad.Create();
            if (_presentationLod != null)
                _headPad.BindPresentationLod(_presentationLod, _presentationLodHandle, EntityId.Value);
        }

        private void ReleaseHeadPad()
        {
            _presentationLod?.SetImportance(_presentationLodHandle, EntityId.Value, false, 0);
            _headPad?.Dispose();
            _headPad = null;
        }

        private static bool HasSamePose(Transform transform, Vector3 position, Quaternion rotation)
        {
            return transform != null
                && transform.position == position
                && transform.rotation == rotation;
        }

        private readonly struct NetworkPresentationSample
        {
            public NetworkPresentationSample(long frame, Vector3 position, Quaternion rotation)
            {
                Frame = frame;
                Position = position;
                Rotation = rotation;
            }

            public long Frame { get; }
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
        }

        private Vector3 ResolveHeadPadWorldPosition()
        {
            Transform head = ResolveHeadTransform();
            if (head != null)
                return head.position + Vector3.up * 0.35f;
            return Vector3.zero;
        }

        private Transform ResolveSocketAnchorTransform(int socketId)
        {
            if (socketId <= 0)
                return null;

            if (!_socketAnchorCacheResolved)
                BuildSocketAnchorCache();

            return _socketAnchorCache != null
                && _socketAnchorCache.TryGetValue(socketId, out Transform anchor)
                && anchor != null
                    ? anchor
                    : null;
        }

        private void BuildSocketAnchorCache()
        {
            _socketAnchorCacheResolved = true;
            _socketAnchorResolveCount++;
            IReadOnlyList<CharacterBoneRuntimeSyncBinding> bindings = _boneSync?.Bindings;
            if (bindings == null || bindings.Count == 0)
            {
                _socketAnchorCache?.Clear();
                return;
            }

            if (_socketAnchorCache == null)
                _socketAnchorCache = new Dictionary<int, Transform>(bindings.Count);
            else
                _socketAnchorCache.Clear();

            for (int i = 0; i < bindings.Count; i++)
            {
                CharacterBoneRuntimeSyncBinding binding = bindings[i];
                if (binding == null)
                    continue;

                Transform anchor = binding.Target != null ? binding.Target : binding.Bone;
                if (anchor != null)
                {
                    if (binding.SocketId > 0 && !_socketAnchorCache.ContainsKey(binding.SocketId))
                        _socketAnchorCache.Add(binding.SocketId, anchor);

                    if (TryParseSocketBindingId(binding.BindingId, out int parsedSocketId)
                        && parsedSocketId > 0
                        && !_socketAnchorCache.ContainsKey(parsedSocketId))
                    {
                        _socketAnchorCache.Add(parsedSocketId, anchor);
                    }
                    int stableSocketId = StablePositiveHash(binding.BindingId);
                    if (stableSocketId > 0 && !_socketAnchorCache.ContainsKey(stableSocketId))
                        _socketAnchorCache.Add(stableSocketId, anchor);
                }
            }
        }

        private static int StablePositiveHash(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;
            unchecked
            {
                uint hash = 2166136261U;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619U;
                }

                int result = (int)(hash & 0x7FFFFFFF);
                return result == 0 ? 1 : result;
            }
        }

        private static bool TryParseSocketBindingId(string bindingId, out int socketId)
        {
            socketId = 0;
            if (string.IsNullOrWhiteSpace(bindingId))
                return false;

            string normalized = bindingId.Trim();
            const string SocketPrefix = "socket:";
            if (normalized.StartsWith(SocketPrefix, StringComparison.Ordinal))
                normalized = normalized.Substring(SocketPrefix.Length);

            return int.TryParse(
                normalized,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out socketId);
        }

        private Transform ResolveHeadTransform()
        {
            if (_headTransformResolved)
                return _cachedHeadTransform;

            _headTransformResolved = true;
            _headPadAnchorResolveCount++;
            if (_abstractBoneMap != null)
                _abstractBoneMap.TryResolve(CharacterAbstractBoneIds.Head, out _cachedHeadTransform);
            if (_cachedHeadTransform == null)
                Debug.LogWarning("MxFramework CharacterTest: required abstract bone is unresolved; head presentation is disabled. entity="
                    + EntityId.Value + " abstractBoneId=" + CharacterAbstractBoneIds.Head);
            return _cachedHeadTransform;
        }

        private bool TryResolveRendererBounds(out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = ResolveRendererBoundsRenderers();
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private Renderer[] ResolveRendererBoundsRenderers()
        {
            if (_rendererBoundsResolved)
                return _cachedRendererBoundsRenderers ?? Array.Empty<Renderer>();

            _rendererBoundsResolved = true;
            _rendererBoundsResolveCount++;
            _cachedRendererBoundsRenderers = Instance.GetComponentsInChildren<Renderer>();
            return _cachedRendererBoundsRenderers ?? Array.Empty<Renderer>();
        }

        private static Transform FindChildByName(Transform root, string token)
        {
            if (root == null || string.IsNullOrEmpty(token))
                return null;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildByName(root.GetChild(i), token);
                if (found != null)
                    return found;
            }

            if (root.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return root;

            return null;
        }

        private static Transform FindChildByExactName(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return null;

            if (string.Equals(root.name, name, StringComparison.Ordinal))
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildByExactName(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private readonly struct BodyPartContactPointTargetCacheEntry
        {
            public BodyPartContactPointTargetCacheEntry(Transform target)
            {
                Target = target;
            }

            public Transform Target { get; }
        }

        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrEmpty(value) ? "none" : value;
        }

        private bool ShouldRecordLocomotionTrace(
            CharacterTestCharacterAnimationComponent animation,
            bool suppressed,
            string lastActionKey)
        {
            int blendX = QuantizeAnimationTraceValue(animation.LocomotionBlend.x);
            int blendY = QuantizeAnimationTraceValue(animation.LocomotionBlend.y);
            int speed = QuantizeAnimationTraceValue(animation.LocomotionSpeed01);
            lastActionKey ??= string.Empty;
            if (_hasAnimationTraceState
                && _lastAnimationTraceBlendX == blendX
                && _lastAnimationTraceBlendY == blendY
                && _lastAnimationTraceSpeed == speed
                && _lastAnimationTraceSuppressed == suppressed
                && string.Equals(
                    _lastAnimationTraceActionKey,
                    lastActionKey,
                    StringComparison.Ordinal))
            {
                return false;
            }

            _hasAnimationTraceState = true;
            _lastAnimationTraceBlendX = blendX;
            _lastAnimationTraceBlendY = blendY;
            _lastAnimationTraceSpeed = speed;
            _lastAnimationTraceSuppressed = suppressed;
            _lastAnimationTraceActionKey = lastActionKey;
            return true;
        }

        private string BuildLocomotionTraceMessage(
            CharacterTestCharacterAnimationComponent animation,
            bool suppressed,
            string lastActionKey)
        {
            StringBuilder builder = _animationTraceBuffer;
            builder.Length = 0;
            builder.Append("blend=(");
            AppendFixed2(builder, animation.LocomotionBlend.x);
            builder.Append(", ");
            AppendFixed2(builder, animation.LocomotionBlend.y);
            builder.Append(") speed=");
            AppendFixed2(builder, animation.LocomotionSpeed01);
            builder.Append(" suppressed=");
            builder.Append(suppressed ? "True" : "False");
            builder.Append(" lastAction=");
            builder.Append(EmptyAsNone(lastActionKey));
            return builder.ToString();
        }

        private string BuildLastActionTraceMessage(string prefix, string lastActionKey)
        {
            StringBuilder builder = _animationTraceBuffer;
            builder.Length = 0;
            builder.Append(prefix);
            builder.Append(EmptyAsNone(lastActionKey));
            return builder.ToString();
        }

        private static int QuantizeAnimationTraceValue(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0;
            return Mathf.RoundToInt(value * AnimationTracePrecision);
        }

        private static void AppendFixed2(StringBuilder builder, float value)
        {
            AppendFixed2(builder, QuantizeAnimationTraceValue(value));
        }

        private static void AppendFixed2(StringBuilder builder, int hundredths)
        {
            if (hundredths < 0)
            {
                builder.Append('-');
                hundredths = -hundredths;
            }

            int whole = hundredths / 100;
            int fraction = hundredths - whole * 100;
            AppendNonNegativeInt(builder, whole);
            builder.Append('.');
            builder.Append((char)('0' + fraction / 10));
            builder.Append((char)('0' + fraction % 10));
        }

        private static void AppendNonNegativeInt(StringBuilder builder, int value)
        {
            if (value <= 0)
            {
                builder.Append('0');
                return;
            }

            int divisor = 1;
            while (value / divisor >= 10)
                divisor *= 10;

            while (divisor > 0)
            {
                int digit = value / divisor;
                builder.Append((char)('0' + digit));
                value -= digit * divisor;
                divisor /= 10;
            }
        }

        private static int ToPresentationFrame(RuntimeFrame frame)
        {
            if (frame.Value > int.MaxValue)
                return int.MaxValue;
            if (frame.Value < int.MinValue)
                return int.MinValue;
            return (int)frame.Value;
        }

        internal static CharacterTestHeadPadAllegiance ResolveAllegiance(
            int teamId,
            CharacterTestCharacterControlOwner controlOwner,
            bool hasLocalPlayerTeam,
            int localPlayerTeamId)
        {
            if (controlOwner == CharacterTestCharacterControlOwner.LocalPlayer)
                return CharacterTestHeadPadAllegiance.Player;

            if (!hasLocalPlayerTeam)
                return CharacterTestHeadPadAllegiance.Neutral;

            return teamId == localPlayerTeamId
                ? CharacterTestHeadPadAllegiance.Ally
                : CharacterTestHeadPadAllegiance.Enemy;
        }

        private void LogAnimationIssueOnce(
            string requestedKey,
            bool accepted,
            MxAnimationBackendResult result,
            bool error)
        {
            string actualClip = result.ClipKey.Id ?? string.Empty;
            string errorKey = requestedKey + "|" + accepted + "|" + result.Code + "|" + actualClip + "|" + error;
            if (!_reportedAnimationErrors.Add(errorKey))
                return;

            string message = "CharacterTest animation runtime resource "
                + (error ? "failed" : "used fallback")
                + ". entityId=" + EntityId.Value
                + " requestedKey=" + EmptyAsNone(requestedKey)
                + " accepted=" + accepted
                + " result=" + result.Code
                + " actualClip=" + EmptyAsNone(actualClip)
                + " message=" + EmptyAsNone(result.Message);
            if (error)
                Debug.LogError(message, Instance);
            else
                Debug.LogWarning(message, Instance);
        }
    }

    internal sealed class CharacterTestFairyGuiHeadPad : System.IDisposable
    {
        private const float Width = 148f;
        private const float Height = 54f;
        private const float MarkerX = 8f;
        private const float MarkerY = 9f;
        private const float MarkerWidth = 10f;
        private const float MarkerHeight = 25f;
        private const float BarX = 2f;
        private const float BarWidth = 144f;
        private const float HealthBarY = 2f;
        private const float HealthBarHeight = 12f;
        private const float HealthNumberWidth = 44f;
        private const float ScatterPressureBarY = 18f;
        private const float ScatterPressureBarHeight = 4f;
        private const float CohesionRecoveryFillHeight = 2f;
        private const float CohesionRecoverySparkWidth = 2f;
        private const int BuffBadgeCount = 4;
        private const float BuffBadgeWidth = 24f;
        private const float BuffBadgeHeight = 24f;
        private const float BuffBadgeGap = 2f;
        private const float BuffBadgeY = 27f;
        private const float BuffBadgeRemainingHeight = 2f;
        private const int HealthSegmentUnit = 25;
        private const int MajorHealthSegmentUnit = 100;
        private const int MaxVisibleHealthSegments = 24;
        private const int MaxHealthSegmentMarkerCount = MaxVisibleHealthSegments - 1;
        private const float HealthTrailSettleSpeed = 9f;
        private const float HealthFlashDuration = 0.22f;
        private const float HitPulseDuration = 0.18f;
        private const float CohesionBreakPulseDuration = 0.65f;
        private const float CohesionBreakFlashDuration = 0.65f;
        private const float CohesionBreakLabelWidth = 72f;
        private const float CohesionBreakLabelHeight = 20f;
        private const float CohesionBreakLabelBaseY = 14f;
        private const float DownPulseDuration = 0.24f;
        private const float AnimationSnapThreshold = 0.35f;
        private const float AutoHideDelaySeconds = 5f;
        internal const float DeathFeedbackDurationSeconds = 2f;
        private const float FadeInSpeed = 8f;
        private const float FadeOutSpeed = 3.8f;
        private const float VisibleAlphaThreshold = 0.01f;
        private const int MaxPooledPads = 8;
        internal const int DefaultPrewarmCount = 3;

        private static bool s_stageConfigured;
        private static readonly float[] s_frameRadii = { 5f, 5f, 5f, 5f };
        private static readonly float[] s_panelRadii = { 4f, 4f, 4f, 4f };
        private static readonly float[] s_markerRadii = { 2f, 2f, 2f, 2f };
        private static readonly int[] s_healthSegmentUnits = { HealthSegmentUnit, 50, 100, 250 };
        private static readonly List<CharacterTestFairyGuiHeadPad> s_activePads =
            new List<CharacterTestFairyGuiHeadPad>();
        private static readonly List<CharacterTestFairyGuiHeadPad> s_pooledPads =
            new List<CharacterTestFairyGuiHeadPad>(MaxPooledPads);
        private static UnityCamera s_cachedMainCamera;

        private sealed class BuffBadge
        {
            private readonly Fgui.GGraph _border;
            private readonly Fgui.GGraph _background;
            private readonly Fgui.GGraph _remaining;
            private readonly Fgui.GTextField _label;
            private readonly Fgui.GTextField _stack;
            private bool _lastVisible;
            private bool _hasStaticSignature;
            private int _lastBuffId;
            private string _lastDisplayName = string.Empty;
            private string _lastShortName = string.Empty;
            private string _lastIconKey = string.Empty;
            private int _lastStackCount;
            private bool _lastIsPermanent;
            private GameplayBuffPresentationPolarity _lastPolarity = (GameplayBuffPresentationPolarity)(-1);
            private bool _lastRemainingVisible;
            private float _lastRemainingWidth = -1f;

            public BuffBadge(
                Fgui.GComponent root,
                Fgui.GGraph border,
                Fgui.GGraph background,
                Fgui.GGraph remaining,
                Fgui.GTextField label,
                Fgui.GTextField stack)
            {
                Root = root;
                _border = border;
                _background = background;
                _remaining = remaining;
                _label = label;
                _stack = stack;
            }

            public Fgui.GComponent Root { get; }

            internal int DebugUpdateCount { get; private set; }

            public void SetVisible(bool visible)
            {
                if (_lastVisible == visible)
                    return;

                _lastVisible = visible;
                Root.visible = visible;
            }

            public void Bind(GameplayBuffPresentationItem item)
            {
                SetVisible(true);
                bool staticChanged = HasStaticSignatureChanged(item);
                if (staticChanged)
                {
                    _border.DrawRoundRect(
                        BuffBadgeWidth,
                        BuffBadgeHeight,
                        ResolveBuffBadgeBorderColor(item.Polarity),
                        s_markerRadii);
                    _background.DrawRoundRect(
                        BuffBadgeWidth - 2f,
                        BuffBadgeHeight - 2f,
                        ResolveBuffBadgeBackgroundColor(item.Polarity),
                        s_markerRadii);

                    _label.text = string.Empty;
                    _label.visible = false;
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardIconPresenter.Bind(Root, item.IconKey, 3f, 3f, 18f,
                        ResolveBuffBadgeBorderColor(item.Polarity), true);
                    if (item.StackCount > 1)
                    {
                        _label.SetSize(BuffBadgeWidth - 12f, BuffBadgeHeight);
                        _stack.text = item.StackCount.ToString(CultureInfo.InvariantCulture);
                        _stack.visible = false;
                    }
                    else
                    {
                        _label.SetSize(BuffBadgeWidth - 4f, BuffBadgeHeight);
                        _stack.text = string.Empty;
                        _stack.visible = false;
                    }

                    StoreStaticSignature(item);
                }

                bool remainingVisible = !item.IsPermanent && item.Remaining01 > 0f;
                float remainingWidth = remainingVisible
                    ? Mathf.Max(
                        1f,
                        Mathf.Round((BuffBadgeWidth - 4f) * Mathf.Clamp01(item.Remaining01)))
                    : 0f;
                bool remainingChanged = staticChanged
                    || _lastRemainingVisible != remainingVisible
                    || Mathf.Abs(remainingWidth - _lastRemainingWidth) > 0.01f;
                if (!remainingChanged)
                    return;

                if (!remainingVisible)
                {
                    _remaining.visible = false;
                    _lastRemainingVisible = false;
                    _lastRemainingWidth = 0f;
                    DebugUpdateCount++;
                    return;
                }

                _remaining.visible = true;
                _remaining.DrawRect(
                    remainingWidth,
                    BuffBadgeRemainingHeight,
                    0,
                    Color.clear,
                    ResolveBuffBadgeBorderColor(item.Polarity));
                _lastRemainingVisible = true;
                _lastRemainingWidth = remainingWidth;
                DebugUpdateCount++;
            }

            private bool HasStaticSignatureChanged(GameplayBuffPresentationItem item)
            {
                return !_hasStaticSignature
                    || _lastBuffId != item.BuffId
                    || !string.Equals(_lastDisplayName, item.DisplayName, StringComparison.Ordinal)
                    || !string.Equals(_lastShortName, item.ShortName, StringComparison.Ordinal)
                    || !string.Equals(_lastIconKey, item.IconKey, StringComparison.Ordinal)
                    || _lastStackCount != item.StackCount
                    || _lastIsPermanent != item.IsPermanent
                    || _lastPolarity != item.Polarity;
            }

            private void StoreStaticSignature(GameplayBuffPresentationItem item)
            {
                _hasStaticSignature = true;
                _lastBuffId = item.BuffId;
                _lastDisplayName = item.DisplayName;
                _lastShortName = item.ShortName;
                _lastIconKey = item.IconKey;
                _lastStackCount = item.StackCount;
                _lastIsPermanent = item.IsPermanent;
                _lastPolarity = item.Polarity;
            }

            public void ResetForReuse()
            {
                _lastVisible = false;
                _hasStaticSignature = false;
                _lastBuffId = 0;
                _lastDisplayName = string.Empty;
                _lastShortName = string.Empty;
                _lastIconKey = string.Empty;
                _lastStackCount = 0;
                _lastIsPermanent = false;
                _lastPolarity = (GameplayBuffPresentationPolarity)(-1);
                _lastRemainingVisible = false;
                _lastRemainingWidth = -1f;
                DebugUpdateCount = 0;
                Root.visible = false;
                _remaining.visible = false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            s_activePads.Clear();
            ClearPool();
            s_stageConfigured = false;
            s_cachedMainCamera = null;
        }

        private readonly Fgui.GComponent _root;
        private readonly Fgui.GGraph _frame;
        private readonly Fgui.GGraph _allegianceMarker;
        private readonly Fgui.GTextField _healthText;
        private readonly Fgui.GGraph _healthTrail;
        private readonly Fgui.GGraph _healthFill;
        private readonly Fgui.GGraph _scatterPressureBack;
        private readonly Fgui.GGraph _scatterPressureFill;
        private readonly Fgui.GGraph _cohesionBreakMarker;
        private readonly Fgui.GGraph _cohesionRecoveryFill;
        private readonly Fgui.GGraph _cohesionRecoverySpark;
        private readonly Fgui.GGraph _cohesionBreakFlash;
        private readonly Fgui.GTextField _cohesionBreakLabel;
        private readonly Fgui.GGraph[] _scatterPressureThresholds;
        private readonly Fgui.GGraph[] _healthSegmentMarkers;
        private readonly BuffBadge[] _buffBadges;
        private readonly bool _allowPooling;
        private Vector3 _worldAnchor;
        private float _lastHealthWidth = -1f;
        private float _targetHealthWidth = -1f;
        private float _healthTrailWidth = -1f;
        private float _lastHealthTrailDrawWidth = -1f;
        private float _lastScatterPressureWidth = -1f;
        private float _lastCohesionRecoveryWidth = -1f;
        private float _lastCohesionRecoverySparkX = -1f;
        private bool _lastCohesionRecoveryVisible;
        private float _framePulseTime;
        private float _framePulseDuration = 1f;
        private float _cohesionBreakFlashTime;
        private float _healthFlashTime;
        private int _lastHealthValue = -1;
        private int _lastMaxHealth = -1;
        private int _lastHealthSegmentUnit = -1;
        private CharacterTestHeadPadAllegiance _lastHealthAllegiance = (CharacterTestHeadPadAllegiance)(-1);
        private CharacterTestHeadPadAllegiance _lastMarkerAllegiance = (CharacterTestHeadPadAllegiance)(-1);
        private CharacterTestCharacterControlOwner _lastMarkerControlOwner = (CharacterTestCharacterControlOwner)(-1);
        private PressureBand _lastScatterPressureBand = (PressureBand)(-1);
        private CharacterTestHeadPadVisualState _lastVisualState = (CharacterTestHeadPadVisualState)(-1);
        private Color _healthFillColor;
        private Color _healthTextColor;
        private bool _lastHealthAlive;
        private bool _lastScatterPressureAlive;
        private bool _hasWorldAnchor;
        private CharacterTestHeadPadDisplayMode _displayMode =
            CharacterTestHeadPadDisplayMode.CombatReadout;
        private float _hideCountdownSeconds;
        private float _displayAlpha;
        private float _targetDisplayAlpha;
        private bool _lockedVisible;
        private bool _deadObserverSimplified;
        private float _deathFeedbackCountdownSeconds;
        private bool _screenVisible;
        private bool _hasVisibilitySnapshot;
        private bool _visibilityAlive;
        private int _visibilityHealth;
        private int _visibilityMaxHealth;
        private int _visibilityScatterPressure;
        private int _visibilityMaxScatterPressure;
        private PressureBand _visibilityScatterPressureBand = (PressureBand)(-1);
        private CharacterTestHeadPadAllegiance _visibilityAllegiance = (CharacterTestHeadPadAllegiance)(-1);
        private CharacterTestCharacterControlOwner _visibilityControlOwner = (CharacterTestCharacterControlOwner)(-1);
        private CharacterTestHeadPadVisualState _visibilityVisualState = (CharacterTestHeadPadVisualState)(-1);
        private CharacterTestHeadPadDisplayMode _visibilityDisplayMode =
            (CharacterTestHeadPadDisplayMode)(-1);
        private bool _inPool;
        private bool _disposed;

        private CharacterTestPresentationLod _presentationLod;
        private PresentationLodHandle _presentationLodHandle;
        private int _presentationLodEntityId;

        internal void BindPresentationLod(CharacterTestPresentationLod service, PresentationLodHandle handle, int entityId)
        {
            _presentationLod = service; _presentationLodHandle = handle; _presentationLodEntityId = entityId;
            service.Discontinuity(handle);
            if (_hasWorldAnchor) service.SetAnchor(handle, entityId, _worldAnchor);
        }

        private CharacterTestFairyGuiHeadPad(
            Fgui.GComponent root,
            Fgui.GGraph frame,
            Fgui.GGraph allegianceMarker,
            Fgui.GTextField healthText,
            Fgui.GGraph healthTrail,
            Fgui.GGraph healthFill,
            Fgui.GGraph scatterPressureBack,
            Fgui.GGraph scatterPressureFill,
            Fgui.GGraph cohesionBreakMarker,
            Fgui.GGraph cohesionRecoveryFill,
            Fgui.GGraph cohesionRecoverySpark,
            Fgui.GGraph cohesionBreakFlash,
            Fgui.GTextField cohesionBreakLabel,
            Fgui.GGraph[] scatterPressureThresholds,
            Fgui.GGraph[] healthSegmentMarkers,
            BuffBadge[] buffBadges,
            bool allowPooling)
        {
            _root = root;
            _frame = frame;
            _allegianceMarker = allegianceMarker;
            _healthText = healthText;
            _healthTrail = healthTrail;
            _healthFill = healthFill;
            _scatterPressureBack = scatterPressureBack;
            _scatterPressureFill = scatterPressureFill;
            _cohesionBreakMarker = cohesionBreakMarker;
            _cohesionRecoveryFill = cohesionRecoveryFill;
            _cohesionRecoverySpark = cohesionRecoverySpark;
            _cohesionBreakFlash = cohesionBreakFlash;
            _cohesionBreakLabel = cohesionBreakLabel;
            _scatterPressureThresholds = scatterPressureThresholds;
            _healthSegmentMarkers = healthSegmentMarkers;
            _buffBadges = buffBadges ?? Array.Empty<BuffBadge>();
            _allowPooling = allowPooling;
        }

        public static CharacterTestFairyGuiHeadPad Create()
        {
            ConfigureStage();
            CharacterTestFairyGuiHeadPadLateUpdateDriver.Ensure();

            CharacterTestFairyGuiHeadPad pad = RentOrCreate();

            CharacterTestFairyGuiLayerRoots.EnsureLayerRoots();
            if (CharacterTestFairyGuiLayerRoots.TryGetLayerRoot(MxUiLayer.Hud, out Fgui.GComponent hudRoot)
                && hudRoot != null)
            {
                hudRoot.AddChild(pad._root);
            }
            s_activePads.Add(pad);
            return pad;
        }

        internal static CharacterTestFairyGuiHeadPad CreateDetachedForTests(out Fgui.GComponent root)
        {
            CharacterTestFairyGuiHeadPad pad = CreateDetached(allowPooling: false);
            root = pad._root;
            return pad;
        }

        internal static void ClearPoolForTests()
        {
            ClearPool();
            s_cachedMainCamera = null;
        }

        internal static int PooledCountForTests => s_pooledPads.Count;

        internal static int ActiveCountForTests => s_activePads.Count;

        internal bool DeadObserverSimplifiedForTests => _deadObserverSimplified;

        internal float DeathFeedbackRemainingForTests => _deathFeedbackCountdownSeconds;

        internal float TargetDisplayAlphaForTests => _targetDisplayAlpha;

        internal float HealthFlashRemainingForTests => _healthFlashTime;

        internal float FramePulseRemainingForTests => _framePulseTime;

        internal float HealthTrailWidthForTests => _healthTrailWidth;

        internal float TargetHealthWidthForTests => _targetHealthWidth;

        internal Color BaseHealthFillColorForTests => _healthFillColor;

        internal static int PrewarmPool(int desiredCount)
        {
            if (desiredCount <= 0)
                return 0;

            ConfigureStage();
            PruneInvalidPooledPads();
            int targetCount = Mathf.Min(desiredCount, MaxPooledPads);
            int createdCount = 0;
            while (s_pooledPads.Count < targetCount)
            {
                CharacterTestFairyGuiHeadPad pad = CreateDetached(allowPooling: true);
                pad.ResetForReuse();
                pad._inPool = true;
                s_pooledPads.Add(pad);
                createdCount++;
            }

            return createdCount;
        }

        internal int DebugGetBuffBadgeUpdateCount(int slot)
        {
            int index = slot - 1;
            return index >= 0 && index < _buffBadges.Length && _buffBadges[index] != null
                ? _buffBadges[index].DebugUpdateCount
                : 0;
        }

        private static CharacterTestFairyGuiHeadPad RentOrCreate()
        {
            for (int i = s_pooledPads.Count - 1; i >= 0; i--)
            {
                CharacterTestFairyGuiHeadPad pad = s_pooledPads[i];
                s_pooledPads.RemoveAt(i);
                if (pad == null)
                    continue;

                if (!HasLiveDisplayObject(pad._root))
                {
                    pad.DestroyNow();
                    continue;
                }

                pad.ResetForReuse();
                pad._inPool = false;
                pad._disposed = false;
                return pad;
            }

            return CreateDetached(allowPooling: true);
        }

        private static void PruneInvalidPooledPads()
        {
            for (int i = s_pooledPads.Count - 1; i >= 0; i--)
            {
                CharacterTestFairyGuiHeadPad pad = s_pooledPads[i];
                if (pad != null && HasLiveDisplayObject(pad._root))
                    continue;

                s_pooledPads.RemoveAt(i);
                pad?.DestroyNow();
            }
        }

        private static CharacterTestFairyGuiHeadPad CreateDetached(bool allowPooling)
        {
            Fgui.GComponent root = new Fgui.GComponent
            {
                name = "CharacterTest.HeadPad",
                touchable = false,
                visible = false,
                alpha = 0f
            };
            root.SetSize(Width, Height);

            Fgui.GGraph frame = new Fgui.GGraph { name = "stateFrame", touchable = false };
            frame.DrawRoundRect(Width, Height, new Color(0.18f, 0.24f, 0.30f, 0.75f), s_frameRadii);
            frame.visible = false;
            root.AddChild(frame);

            Fgui.GGraph background = new Fgui.GGraph { name = "background", touchable = false };
            background.SetXY(2f, 2f);
            background.DrawRoundRect(Width - 4f, Height - 4f, new Color(0.025f, 0.04f, 0.05f, 0.90f), s_panelRadii);
            background.visible = false;
            root.AddChild(background);

            Fgui.GGraph allegianceMarker = new Fgui.GGraph { name = "allegiance", touchable = false };
            allegianceMarker.SetXY(MarkerX, MarkerY);
            allegianceMarker.DrawRoundRect(MarkerWidth, MarkerHeight, new Color(0.25f, 0.68f, 1f, 1f), s_markerRadii);
            allegianceMarker.visible = false;
            root.AddChild(allegianceMarker);

            Fgui.GGraph healthBack = new Fgui.GGraph { name = "hpBack", touchable = false };
            healthBack.SetXY(BarX - 1f, HealthBarY - 1f);
            healthBack.DrawRect(BarWidth + 2f, HealthBarHeight + 2f, 1,
                new Color(0.7f, 0.69f, 0.62f, 1f), new Color(0.05f, 0.07f, 0.08f, 0.98f));
            root.AddChild(healthBack);

            Fgui.GGraph healthTrail = new Fgui.GGraph { name = "hpTrail", touchable = false };
            healthTrail.SetXY(BarX, HealthBarY);
            healthTrail.visible = false;
            healthTrail.DrawRect(BarWidth, HealthBarHeight, 0, Color.clear, new Color(1f, 0.52f, 0.18f, 0.72f));
            root.AddChild(healthTrail);

            Fgui.GGraph healthFill = new Fgui.GGraph { name = "hpFill", touchable = false };
            healthFill.SetXY(BarX, HealthBarY);
            healthFill.DrawRect(BarWidth, HealthBarHeight, 0, Color.clear, new Color(0.12f, 0.85f, 0.38f, 0.95f));
            root.AddChild(healthFill);

            Fgui.GGraph[] healthSegmentMarkers = CreateHealthSegmentMarkers(root);

            Fgui.GTextField healthText = CreateText(
                "hpNumber",
                BarX + BarWidth - HealthNumberWidth - 4f,
                HealthBarY - 1f,
                HealthNumberWidth,
                HealthBarHeight + 2f,
                12,
                new Color(0.96f, 0.98f, 1.0f, 1f),
                Fgui.AlignType.Right);
            Fgui.TextFormat format = healthText.textFormat;
            format.bold = true;
            healthText.textFormat = format;
            healthText.visible = false;
            root.AddChild(healthText);

            Fgui.GGraph scatterPressureBack = new Fgui.GGraph { name = "scatterPressureBack", touchable = false };
            scatterPressureBack.SetXY(BarX, ScatterPressureBarY);
            scatterPressureBack.DrawRect(BarWidth, ScatterPressureBarHeight, 0, Color.clear, new Color(0.08f, 0.12f, 0.13f, 0.95f));
            root.AddChild(scatterPressureBack);

            Fgui.GGraph scatterPressureFill = new Fgui.GGraph { name = "scatterPressureFill", touchable = false };
            scatterPressureFill.SetXY(BarX, ScatterPressureBarY);
            scatterPressureFill.DrawRect(1f, ScatterPressureBarHeight, 0, Color.clear, new Color(0.47f, 0.74f, 0.64f, 0.9f));
            root.AddChild(scatterPressureFill);

            Fgui.GGraph[] scatterPressureThresholds =
            {
                AddScatterPressureThreshold(root, 0.35f, new Color(0.55f, 0.44f, 0.25f, 0.72f)),
                AddScatterPressureThreshold(root, 0.65f, new Color(0.72f, 0.48f, 0.22f, 0.8f)),
                AddScatterPressureThreshold(root, 0.85f, new Color(0.95f, 0.55f, 0.18f, 0.9f))
            };

            Fgui.GGraph cohesionBreakMarker = new Fgui.GGraph { name = "cohesionLimit", touchable = false };
            cohesionBreakMarker.SetXY(BarX + BarWidth - 2f, ScatterPressureBarY - 2f);
            cohesionBreakMarker.DrawRect(2f, ScatterPressureBarHeight + 4f, 0, Color.clear, new Color(1f, 0.78f, 0.35f, 1f));
            root.AddChild(cohesionBreakMarker);

            Fgui.GGraph cohesionRecoveryFill = new Fgui.GGraph
            {
                name = "cohesionRecoveryFill",
                touchable = false,
                visible = false
            };
            cohesionRecoveryFill.SetXY(BarX, ScatterPressureBarY + ScatterPressureBarHeight - CohesionRecoveryFillHeight);
            root.AddChild(cohesionRecoveryFill);

            Fgui.GGraph cohesionRecoverySpark = new Fgui.GGraph
            {
                name = "cohesionRecoverySpark",
                touchable = false,
                visible = false
            };
            cohesionRecoverySpark.SetXY(BarX, ScatterPressureBarY - 2f);
            root.AddChild(cohesionRecoverySpark);

            BuffBadge[] buffBadges = CreateBuffBadges(root);

            Fgui.GGraph cohesionBreakFlash = new Fgui.GGraph
            {
                name = "cohesionBreakFlash",
                touchable = false,
                visible = false,
                alpha = 0f
            };
            cohesionBreakFlash.SetXY(2f, 2f);
            cohesionBreakFlash.DrawRoundRect(
                Width - 4f,
                Height - 4f,
                new Color(1f, 0.5f, 0.08f, 0.42f),
                s_panelRadii);
            root.AddChild(cohesionBreakFlash);

            Fgui.GTextField cohesionBreakLabel = CreateText(
                "cohesionBreakLabel",
                CohesionBreakLabelX(),
                CohesionBreakLabelBaseY,
                CohesionBreakLabelWidth,
                CohesionBreakLabelHeight,
                15,
                new Color(1f, 0.9f, 0.35f, 1f),
                Fgui.AlignType.Center);
            cohesionBreakLabel.text = "破聚";
            cohesionBreakLabel.visible = false;
            cohesionBreakLabel.alpha = 0f;
            root.AddChild(cohesionBreakLabel);

            return new CharacterTestFairyGuiHeadPad(
                root,
                frame,
                allegianceMarker,
                healthText,
                healthTrail,
                healthFill,
                scatterPressureBack,
                scatterPressureFill,
                cohesionBreakMarker,
                cohesionRecoveryFill,
                cohesionRecoverySpark,
                cohesionBreakFlash,
                cohesionBreakLabel,
                scatterPressureThresholds,
                healthSegmentMarkers,
                buffBadges,
                allowPooling);
        }

        public static void UpdateAllScreenPositions()
        {
            TickAllVisibilityPresentations(0f);
        }

        internal static void TickAllVisibilityPresentations(float unscaledDeltaTime)
        {
            UnityCamera camera = CharacterTestMainCameraCache.Resolve(ref s_cachedMainCamera);
            for (int i = s_activePads.Count - 1; i >= 0; i--)
            {
                CharacterTestFairyGuiHeadPad pad = s_activePads[i];
                if (pad == null)
                {
                    s_activePads.RemoveAt(i);
                    continue;
                }

                pad.TickVisibilityPresentation(unscaledDeltaTime, camera);
            }
        }

        internal void TickVisibilityPresentation(float unscaledDeltaTime, UnityCamera camera)
        {
            if (!HasLiveDisplayObject(_root))
            {
                _root?.RemoveFromParent();
                s_activePads.Remove(this);
                return;
            }

            if (_displayMode == CharacterTestHeadPadDisplayMode.Hidden)
            {
                HideForDisplayMode();
                return;
            }

            UpdateScreenPosition(camera);
            UpdateAnimation(unscaledDeltaTime);
        }

        public void TickPresentation(float deltaTime, UnityCamera camera)
        {
            // Registered gameplay pads project once at the final late phase, including while gameplay is paused.
            if (_presentationLod != null) return;
            if (!HasLiveDisplayObject(_root))
            {
                _root?.RemoveFromParent();
                s_activePads.Remove(this);
                return;
            }

            if (_displayMode == CharacterTestHeadPadDisplayMode.Hidden)
            {
                HideForDisplayMode();
                return;
            }

            UpdateScreenPosition(camera);
        }

        public void Sync(
            int entityId,
            string displayName,
            int teamId,
            CharacterTestCharacterControlOwner controlOwner,
            CharacterTestHeadPadAllegiance allegiance,
            CharacterTestCharacterCombatStateComponent combatState,
            bool lockedVisible,
            CharacterTestHeadPadDisplayMode displayMode,
            IReadOnlyList<GameplayBuffPresentationItem> buffBadges,
            Vector3 worldPosition,
            long currentFrame = 0L)
        {
            if (!HasLiveDisplayObject(_root))
            {
                _root?.RemoveFromParent();
                s_activePads.Remove(this);
                return;
            }

            CharacterTestHeadPadDisplayMode safeDisplayMode =
                CharacterTestHeadPadDisplayModes.Normalize(displayMode);
            bool simplifyDeadObserver = !combatState.IsAlive
                && _deadObserverSimplified;
            CharacterTestHeadPadDisplayMode effectiveDisplayMode = simplifyDeadObserver
                && safeDisplayMode == CharacterTestHeadPadDisplayMode.CombatReadout
                    ? CharacterTestHeadPadDisplayMode.HealthOnly
                    : safeDisplayMode;
            UpdateDisplayMode(effectiveDisplayMode);
            if (safeDisplayMode == CharacterTestHeadPadDisplayMode.Hidden)
            {
                HideForDisplayMode();
                HideCohesionRecovery();
                StoreVisibilitySnapshot(controlOwner, allegiance, combatState, ResolveVisualState(combatState), safeDisplayMode);
                UpdateBuffBadges(null, safeDisplayMode);
                SyncWorldAnchor(worldPosition);
                return;
            }

            UpdateVisibilityPolicy(
                controlOwner,
                allegiance,
                combatState,
                lockedVisible,
                effectiveDisplayMode);
            UpdateFrame(combatState);
            UpdateAllegianceMarker(controlOwner, allegiance);
            UpdateHealth(combatState, allegiance);
            if (ShouldShowScatterPressure(effectiveDisplayMode))
            {
                UpdateCohesion(combatState);
                HideCohesionRecovery();
            }
            else
            {
                HideCohesionRecovery();
            }
            if (simplifyDeadObserver)
                HideCohesionBreakFlash();
            UpdateBuffBadges(
                simplifyDeadObserver ? null : buffBadges,
                effectiveDisplayMode);
            SyncWorldAnchor(worldPosition);
        }

        public void SyncWorldAnchor(Vector3 worldPosition)
        {
            _worldAnchor = worldPosition;
            _hasWorldAnchor = true;
            if (_presentationLod != null)
                _presentationLod.SetAnchor(_presentationLodHandle, _presentationLodEntityId, worldPosition);
            else
                UpdateScreenPosition(CharacterTestMainCameraCache.Resolve(ref s_cachedMainCamera));
        }

        private Fgui.GComponent _previewOriginalParent, _previewMount;

        internal static void RestorePreviewMounts(Fgui.GComponent layer)
        {
            for (int i = 0; i < s_activePads.Count; i++)
                if (ReferenceEquals(s_activePads[i]._previewMount, layer)) s_activePads[i].RestorePreviewMount();
        }

        private void RestorePreviewMount()
        {
            if (_previewMount == null) return;
            if (HasLiveDisplayObject(_root))
            {
                if (_previewOriginalParent != null && !_previewOriginalParent.isDisposed)
                    _previewOriginalParent.AddChild(_root);
                else _root.RemoveFromParent();
            }
            _previewOriginalParent = _previewMount = null;
        }

        private void UpdateScreenPosition(UnityCamera camera)
        {
            if (!_hasWorldAnchor || _root == null || _root.parent == null)
            {
                _screenVisible = false;
                ApplyRootVisibility();
                return;
            }

            var previewHost = CharacterTestRuntimeAiLabBehaviorCardPreviewHost.ActiveHost;
            if (previewHost != null && previewHost.IsActive && previewHost.PreviewCamera != null && previewHost.BoundLoader != null)
            {
                UnityCamera previewCam = previewHost.PreviewCamera;
                var loader = previewHost.BoundLoader;
                if (!loader.isDisposed && loader.onStage && loader.width > 10f && loader.height > 10f)
                {
                    var layer = previewHost.GetHeadPadLayer();
                    if (layer != null && !ReferenceEquals(_root.parent, layer))
                    {
                        RestorePreviewMount();
                        _previewOriginalParent = _root.parent;
                        _previewMount = layer;
                        layer.AddChild(_root);
                    }
                    Vector3 viewportPoint = previewCam.WorldToViewportPoint(_worldAnchor);
                    if (viewportPoint.z <= 0f || viewportPoint.x < 0f || viewportPoint.x > 1f || viewportPoint.y < 0f || viewportPoint.y > 1f)
                    {
                        _screenVisible = false;
                        ApplyRootVisibility();
                        return;
                    }

                    Vector2 loaderLocal = new Vector2(viewportPoint.x * loader.width, (1f - viewportPoint.y) * loader.height);
                    float minX = 8f;
                    float maxX = loader.width - Width - 8f;
                    float minY = 8f;
                    float maxY = loader.height - Height - 8f;

                    Vector2 clampedLoaderLocal = new Vector2(
                        Mathf.Clamp(loaderLocal.x - Width * 0.5f, minX, Mathf.Max(minX, maxX)),
                        Mathf.Clamp(loaderLocal.y - Height, minY, Mathf.Max(minY, maxY))
                    );

                    Vector2 finalPos = loader.TransformPoint(clampedLoaderLocal, _root.parent);

                    _screenVisible = true;
                    _root.SetXY(Mathf.Round(finalPos.x), Mathf.Round(finalPos.y));
                    ApplyRootVisibility();
                    return;
                }
            }

            RestorePreviewMount();
            if (camera == null) { _screenVisible = false; ApplyRootVisibility(); return; }
            Vector3 screen;
            if (_presentationLod != null)
            {
                if (!_presentationLod.TryProject(_presentationLodHandle, camera, out screen))
                { _screenVisible = false; ApplyRootVisibility(); return; }
            }
            else screen = camera.WorldToScreenPoint(_worldAnchor);
            bool visible = screen.z > 0f
                && screen.x >= -Width
                && screen.x <= Screen.width + Width
                && screen.y >= -Height
                && screen.y <= Screen.height + Height;
            _screenVisible = visible;
            if (!visible)
            {
                ApplyRootVisibility();
                return;
            }

            Vector2 screenPoint = new Vector2(screen.x, Screen.height - screen.y);
            Vector2 stagePoint = Application.isPlaying
                ? Fgui.GRoot.inst.GlobalToLocal(screenPoint)
                : screenPoint;
            _root.SetXY(
                Mathf.Round(stagePoint.x - Width * 0.5f),
                Mathf.Round(stagePoint.y - Height));
            ApplyRootVisibility();
        }

        public void Dispose()
        {
            _presentationLod = null;
            _presentationLodHandle = default;
            if (_disposed || _inPool)
                return;

            s_activePads.Remove(this);
            if (!HasLiveDisplayObject(_root))
            {
                _root?.RemoveFromParent();
                _disposed = true;
                return;
            }

            if (_allowPooling && s_pooledPads.Count < MaxPooledPads)
            {
                ReturnToPool();
                return;
            }

            DestroyNow();
        }

        private void ReturnToPool()
        {
            RestorePreviewMount();
            _root.RemoveFromParent();
            ResetForReuse();
            _inPool = true;
            s_pooledPads.Add(this);
        }

        private void DestroyNow()
        {
            RestorePreviewMount();
            s_activePads.Remove(this);
            _inPool = false;
            if (!HasLiveDisplayObject(_root))
            {
                _root?.RemoveFromParent();
                _disposed = true;
                return;
            }

            _root.RemoveFromParent();
            _root.Dispose();
            _disposed = true;
        }

        private void ResetForReuse()
        {
            _presentationLod = null;
            _presentationLodHandle = default;
            _presentationLodEntityId = 0;
            _worldAnchor = Vector3.zero;
            _lastHealthWidth = -1f;
            _targetHealthWidth = -1f;
            _healthTrailWidth = -1f;
            _lastHealthTrailDrawWidth = -1f;
            _lastScatterPressureWidth = -1f;
            _lastCohesionRecoveryWidth = -1f;
            _lastCohesionRecoverySparkX = -1f;
            _lastCohesionRecoveryVisible = false;
            _framePulseTime = 0f;
            _framePulseDuration = 1f;
            _cohesionBreakFlashTime = 0f;
            _healthFlashTime = 0f;
            _lastHealthValue = -1;
            _lastMaxHealth = -1;
            _lastHealthSegmentUnit = -1;
            _lastHealthAllegiance = (CharacterTestHeadPadAllegiance)(-1);
            _lastMarkerAllegiance = (CharacterTestHeadPadAllegiance)(-1);
            _lastMarkerControlOwner = (CharacterTestCharacterControlOwner)(-1);
            _lastScatterPressureBand = (PressureBand)(-1);
            _lastVisualState = (CharacterTestHeadPadVisualState)(-1);
            _healthFillColor = default;
            _healthTextColor = default;
            _lastHealthAlive = false;
            _lastScatterPressureAlive = false;
            _hasWorldAnchor = false;
            _displayMode = (CharacterTestHeadPadDisplayMode)(-1);
            _hideCountdownSeconds = 0f;
            _displayAlpha = 0f;
            _targetDisplayAlpha = 0f;
            _lockedVisible = false;
            _deadObserverSimplified = false;
            _deathFeedbackCountdownSeconds = 0f;
            _screenVisible = false;
            _hasVisibilitySnapshot = false;
            _visibilityAlive = false;
            _visibilityHealth = 0;
            _visibilityMaxHealth = 0;
            _visibilityScatterPressure = 0;
            _visibilityMaxScatterPressure = 0;
            _visibilityScatterPressureBand = (PressureBand)(-1);
            _visibilityAllegiance = (CharacterTestHeadPadAllegiance)(-1);
            _visibilityControlOwner = (CharacterTestCharacterControlOwner)(-1);
            _visibilityVisualState = (CharacterTestHeadPadVisualState)(-1);
            _visibilityDisplayMode = (CharacterTestHeadPadDisplayMode)(-1);

            if (_root != null)
            {
                _root.visible = false;
                _root.alpha = 0f;
            }

            _healthTrail.visible = false;
            HideCohesionRecovery();
            HideCohesionBreakFlash();
            for (int i = 0; i < _healthSegmentMarkers.Length; i++)
                _healthSegmentMarkers[i].visible = false;
            for (int i = 0; i < _buffBadges.Length; i++)
                _buffBadges[i]?.ResetForReuse();
        }

        private static void ClearPool()
        {
            for (int i = s_pooledPads.Count - 1; i >= 0; i--)
            {
                CharacterTestFairyGuiHeadPad pad = s_pooledPads[i];
                if (pad == null)
                    continue;

                pad._inPool = false;
                pad.DestroyNow();
            }

            s_pooledPads.Clear();
        }

        private static bool HasLiveDisplayObject(Fgui.GObject value)
        {
            if (value == null)
                return false;

            Fgui.DisplayObject displayObject = value.displayObject;
            return displayObject != null && !displayObject.isDisposed && displayObject.gameObject != null;
        }

        private static void ConfigureStage()
        {
            if (s_stageConfigured)
                return;

            if (!Application.isPlaying)
            {
                s_stageConfigured = true;
                return;
            }

            Fgui.StageCamera.CheckMainCamera();
            UnityCamera baseCamera = UnityCamera.main;
            UnityCamera overlayCamera = Fgui.StageCamera.main;
            if (baseCamera != null && overlayCamera != null)
            {
                overlayCamera.clearFlags = CameraClearFlags.Depth;
                overlayCamera.targetTexture = baseCamera.targetTexture;
                MxCameraUrpOverlayStackBinder.Bind(
                    baseCamera,
                    overlayCamera,
                    excludeOverlayLayersFromBaseCamera: true);
            }

            s_stageConfigured = true;
        }

        private static Fgui.GTextField CreateText(
            string name,
            float x,
            float y,
            float width,
            float height,
            int fontSize,
            Color color,
            Fgui.AlignType align)
        {
            var text = new Fgui.GTextField
            {
                name = name,
                touchable = false,
                autoSize = Fgui.AutoSizeType.None,
                singleLine = true,
                align = align,
                verticalAlign = Fgui.VertAlignType.Middle
            };
            Fgui.TextFormat format = text.textFormat;
            format.size = fontSize;
            format.color = color;
            text.textFormat = format;
            text.color = color;
            text.SetXY(x, y);
            text.SetSize(width, height);
            return text;
        }

        private static float CohesionBreakLabelX()
        {
            return Mathf.Round((Width - CohesionBreakLabelWidth) * 0.5f);
        }

        private static Fgui.GGraph AddScatterPressureThreshold(Fgui.GComponent root, float normalizedX, Color color)
        {
            Fgui.GGraph marker = new Fgui.GGraph { name = "scatterPressureThreshold", touchable = false };
            float x = BarX + Mathf.Round(BarWidth * Mathf.Clamp01(normalizedX));
            marker.SetXY(x, ScatterPressureBarY - 1f);
            marker.DrawRect(1f, ScatterPressureBarHeight + 2f, 0, Color.clear, color);
            root.AddChild(marker);
            return marker;
        }

        private void UpdateAnimation(float deltaTime)
        {
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            UpdateVisibilityAnimation(safeDeltaTime);
            UpdateHealthTrailAnimation(safeDeltaTime);
            UpdateHealthFlashAnimation(safeDeltaTime);
            UpdateFramePulseAnimation(safeDeltaTime);
            UpdateCohesionBreakFlashAnimation(safeDeltaTime);
        }

        private void UpdateVisibilityPolicy(
            CharacterTestCharacterControlOwner controlOwner,
            CharacterTestHeadPadAllegiance allegiance,
            CharacterTestCharacterCombatStateComponent combatState,
            bool lockedVisible,
            CharacterTestHeadPadDisplayMode displayMode)
        {
            CharacterTestHeadPadVisualState visualState = ResolveVisualState(combatState);
            bool justDied = _hasVisibilitySnapshot
                && _visibilityAlive
                && !combatState.IsAlive;
            bool scatterPressureVisible = ShouldShowScatterPressure(displayMode);
            bool changed = _hasVisibilitySnapshot
                && (_visibilityHealth != combatState.Health
                    || _visibilityMaxHealth != combatState.MaxHealth
                    || (scatterPressureVisible
                        && (_visibilityScatterPressure != combatState.ScatterPressure
                            || _visibilityMaxScatterPressure != combatState.MaxScatterPressure
                            || _visibilityScatterPressureBand != combatState.ScatterPressureBand))
                    || _visibilityAlive != combatState.IsAlive
                    || _visibilityAllegiance != allegiance
                    || _visibilityControlOwner != controlOwner
                    || _visibilityVisualState != visualState
                    || _visibilityDisplayMode != displayMode);
            bool wasLockedVisible = _lockedVisible;
            _lockedVisible = lockedVisible;
            _presentationLod?.SetImportance(_presentationLodHandle, _presentationLodEntityId, lockedVisible,
                changed || justDied ? _presentationLod.Now + (justDied ? DeathFeedbackDurationSeconds : AutoHideDelaySeconds) : 0);

            if (combatState.IsAlive)
            {
                _deathFeedbackCountdownSeconds = 0f;
                _deadObserverSimplified = false;
            }
            else if (justDied)
            {
                _deathFeedbackCountdownSeconds = DeathFeedbackDurationSeconds;
                _deadObserverSimplified = false;
            }
            else if (!_hasVisibilitySnapshot)
            {
                _deathFeedbackCountdownSeconds = 0f;
                _deadObserverSimplified = true;
            }

            if (lockedVisible)
            {
                ShowPersistent();
            }
            else if (justDied)
            {
                RevealTransient(DeathFeedbackDurationSeconds);
            }
            else if (!combatState.IsAlive)
            {
                if (_deathFeedbackCountdownSeconds > 0f)
                    RevealTransient(_deathFeedbackCountdownSeconds);
                else
                    HideAfterDeathFeedback();
            }
            else if (changed)
            {
                RevealTransient(AutoHideDelaySeconds);
            }
            else if (wasLockedVisible)
            {
                RevealTransient(AutoHideDelaySeconds);
            }

            StoreVisibilitySnapshot(controlOwner, allegiance, combatState, visualState, displayMode);
        }

        private void StoreVisibilitySnapshot(
            CharacterTestCharacterControlOwner controlOwner,
            CharacterTestHeadPadAllegiance allegiance,
            CharacterTestCharacterCombatStateComponent combatState,
            CharacterTestHeadPadVisualState visualState,
            CharacterTestHeadPadDisplayMode displayMode)
        {
            _hasVisibilitySnapshot = true;
            _visibilityHealth = combatState.Health;
            _visibilityMaxHealth = combatState.MaxHealth;
            _visibilityScatterPressure = combatState.ScatterPressure;
            _visibilityMaxScatterPressure = combatState.MaxScatterPressure;
            _visibilityScatterPressureBand = combatState.ScatterPressureBand;
            _visibilityAlive = combatState.IsAlive;
            _visibilityAllegiance = allegiance;
            _visibilityControlOwner = controlOwner;
            _visibilityVisualState = visualState;
            _visibilityDisplayMode = displayMode;
        }

        private void ShowPersistent()
        {
            _hideCountdownSeconds = 0f;
            _targetDisplayAlpha = 1f;
            _displayAlpha = 1f;
        }

        private void RevealTransient(float durationSeconds)
        {
            _hideCountdownSeconds = Mathf.Max(0f, durationSeconds);
            _targetDisplayAlpha = 1f;
        }

        private void HideAfterDeathFeedback()
        {
            _hideCountdownSeconds = 0f;
            _targetDisplayAlpha = 0f;
        }

        private void HideForDisplayMode()
        {
            _lockedVisible = false;
            _hideCountdownSeconds = 0f;
            _targetDisplayAlpha = 0f;
            HideCohesionBreakFlash();
            HideCohesionRecovery();
        }

        private void UpdateDisplayMode(CharacterTestHeadPadDisplayMode displayMode)
        {
            if (_displayMode == displayMode)
                return;

            _displayMode = displayMode;
            bool scatterPressureVisible = ShouldShowScatterPressure(displayMode);
            _scatterPressureBack.visible = scatterPressureVisible;
            _scatterPressureFill.visible = scatterPressureVisible;
            _cohesionBreakMarker.visible = scatterPressureVisible;
            for (int i = 0; i < _scatterPressureThresholds.Length; i++)
                _scatterPressureThresholds[i].visible = scatterPressureVisible;
            if (!scatterPressureVisible)
                HideCohesionRecovery();
            if (displayMode == CharacterTestHeadPadDisplayMode.Hidden)
                HideCohesionBreakFlash();
        }

        private void UpdateBuffBadges(
            IReadOnlyList<GameplayBuffPresentationItem> items,
            CharacterTestHeadPadDisplayMode displayMode)
        {
            bool visible = displayMode != CharacterTestHeadPadDisplayMode.Hidden;
            for (int i = 0; i < _buffBadges.Length; i++)
            {
                BuffBadge badge = _buffBadges[i];
                if (badge == null)
                    continue;

                bool show = visible && items != null && i < items.Count;
                badge.SetVisible(show);
                if (show)
                    badge.Bind(items[i]);
            }
        }

        private static bool ShouldShowScatterPressure(CharacterTestHeadPadDisplayMode displayMode)
        {
            return displayMode == CharacterTestHeadPadDisplayMode.CombatReadout;
        }

        private void UpdateVisibilityAnimation(float deltaTime)
        {
            if (_hasVisibilitySnapshot
                && !_visibilityAlive
                && _deathFeedbackCountdownSeconds > 0f)
            {
                _deathFeedbackCountdownSeconds = Mathf.Max(
                    0f,
                    _deathFeedbackCountdownSeconds - deltaTime);
                if (_deathFeedbackCountdownSeconds <= 0f)
                {
                    _deadObserverSimplified = true;
                    _targetDisplayAlpha = 0f;
                }
            }

            if (!_lockedVisible && _hideCountdownSeconds > 0f)
            {
                _hideCountdownSeconds = Mathf.Max(0f, _hideCountdownSeconds - deltaTime);
                if (_hideCountdownSeconds <= 0f)
                    _targetDisplayAlpha = 0f;
            }

            float speed = _targetDisplayAlpha > _displayAlpha ? FadeInSpeed : FadeOutSpeed;
            _displayAlpha = Mathf.MoveTowards(_displayAlpha, _targetDisplayAlpha, speed * deltaTime);
            ApplyRootVisibility();
        }

        private void ApplyRootVisibility()
        {
            if (_root == null)
                return;

            bool previewVisible = _previewMount != null && !_previewMount.isDisposed
                && _visibilityAlive && _displayMode != CharacterTestHeadPadDisplayMode.Hidden;
            float alpha = previewVisible ? 1f : _displayAlpha;
            _root.alpha = alpha;
            _root.visible = _screenVisible && alpha > VisibleAlphaThreshold;
        }

        private static Fgui.GGraph[] CreateHealthSegmentMarkers(Fgui.GComponent root)
        {
            var markers = new Fgui.GGraph[MaxHealthSegmentMarkerCount];
            for (int i = 0; i < markers.Length; i++)
            {
                Fgui.GGraph marker = new Fgui.GGraph { name = "hpSegment", touchable = false };
                marker.visible = false;
                root.AddChild(marker);
                markers[i] = marker;
            }

            return markers;
        }

        private static BuffBadge[] CreateBuffBadges(Fgui.GComponent root)
        {
            var badges = new BuffBadge[BuffBadgeCount];
            float totalWidth = (BuffBadgeCount * BuffBadgeWidth)
                + ((BuffBadgeCount - 1) * BuffBadgeGap);
            float startX = BarX;
            for (int i = 0; i < badges.Length; i++)
            {
                BuffBadge badge = CreateBuffBadge(i + 1);
                badge.Root.SetXY(
                    startX + (i * (BuffBadgeWidth + BuffBadgeGap)),
                    BuffBadgeY);
                root.AddChild(badge.Root);
                badges[i] = badge;
            }

            return badges;
        }

        private static BuffBadge CreateBuffBadge(int slot)
        {
            Fgui.GComponent root = new Fgui.GComponent
            {
                name = "buffBadge" + slot.ToString(CultureInfo.InvariantCulture),
                touchable = false,
                visible = false
            };
            root.SetSize(BuffBadgeWidth, BuffBadgeHeight);

            Fgui.GGraph border = new Fgui.GGraph
            {
                name = "buffBadgeBorder" + slot.ToString(CultureInfo.InvariantCulture),
                touchable = false
            };
            border.DrawRoundRect(
                BuffBadgeWidth,
                BuffBadgeHeight,
                ResolveBuffBadgeBorderColor(GameplayBuffPresentationPolarity.Neutral),
                s_markerRadii);
            root.AddChild(border);

            Fgui.GGraph background = new Fgui.GGraph
            {
                name = "buffBadgeBackground" + slot.ToString(CultureInfo.InvariantCulture),
                touchable = false
            };
            background.SetXY(1f, 1f);
            background.DrawRoundRect(
                BuffBadgeWidth - 2f,
                BuffBadgeHeight - 2f,
                ResolveBuffBadgeBackgroundColor(GameplayBuffPresentationPolarity.Neutral),
                s_markerRadii);
            root.AddChild(background);

            Fgui.GGraph remaining = new Fgui.GGraph
            {
                name = "buffBadgeRemaining" + slot.ToString(CultureInfo.InvariantCulture),
                touchable = false,
                visible = false
            };
            remaining.SetXY(2f, BuffBadgeHeight - BuffBadgeRemainingHeight - 1f);
            root.AddChild(remaining);

            Fgui.GTextField label = CreateText(
                "buffBadgeLabel" + slot.ToString(CultureInfo.InvariantCulture),
                2f,
                0f,
                BuffBadgeWidth - 4f,
                BuffBadgeHeight,
                8,
                new Color(0.96f, 0.95f, 0.86f, 1f),
                Fgui.AlignType.Center);
            root.AddChild(label);

            Fgui.GTextField stack = CreateText(
                "buffBadgeStack" + slot.ToString(CultureInfo.InvariantCulture),
                BuffBadgeWidth - 10f,
                -1f,
                8f,
                9f,
                7,
                new Color(1f, 0.94f, 0.72f, 1f),
                Fgui.AlignType.Right);
            stack.visible = false;
            root.AddChild(stack);

            return new BuffBadge(root, border, background, remaining, label, stack);
        }

        private void UpdateFrame(CharacterTestCharacterCombatStateComponent combatState)
        {
            CharacterTestHeadPadVisualState state = ResolveVisualState(combatState);
            if (_lastVisualState == state)
                return;

            _lastVisualState = state;
            switch (state)
            {
                case CharacterTestHeadPadVisualState.Hit:
                    StartFramePulse(HitPulseDuration);
                    break;
                case CharacterTestHeadPadVisualState.CohesionBreak:
                    StartFramePulse(CohesionBreakPulseDuration);
                    StartCohesionBreakFlash();
                    break;
                case CharacterTestHeadPadVisualState.Down:
                    StartFramePulse(DownPulseDuration);
                    break;
                default:
                    DrawFrame(ResolveFrameColor(state));
                    break;
            }
        }

        private void UpdateAllegianceMarker(
            CharacterTestCharacterControlOwner controlOwner,
            CharacterTestHeadPadAllegiance allegiance)
        {
            if (_lastMarkerAllegiance == allegiance && _lastMarkerControlOwner == controlOwner)
                return;

            Color markerColor = controlOwner == CharacterTestCharacterControlOwner.LocalPlayer
                ? new Color(0.84f, 1f, 0.48f, 1f)
                : ResolveMarkerColor(allegiance);
            _allegianceMarker.DrawRoundRect(MarkerWidth, MarkerHeight, markerColor, s_markerRadii);
            _lastMarkerAllegiance = allegiance;
            _lastMarkerControlOwner = controlOwner;
        }

        private void UpdateHealth(
            CharacterTestCharacterCombatStateComponent combatState,
            CharacterTestHeadPadAllegiance allegiance)
        {
            float normalized = combatState.MaxHealth <= 0
                ? 0f
                : Mathf.Clamp01((float)combatState.Health / combatState.MaxHealth);
            int segmentUnit = ResolveHealthSegmentUnit(combatState.MaxHealth);
            float width = normalized <= 0f ? 1f : Mathf.Max(1f, Mathf.Round(BarWidth * normalized));
            bool hasPreviousHealth = _lastHealthValue >= 0;
            bool healthDropped = hasPreviousHealth && combatState.Health < _lastHealthValue;
            bool healthChanged = hasPreviousHealth && combatState.Health != _lastHealthValue;
            if (Mathf.Abs(width - _lastHealthWidth) <= 0.01f
                && _lastHealthValue == combatState.Health
                && _lastMaxHealth == combatState.MaxHealth
                && _lastHealthSegmentUnit == segmentUnit
                && _lastHealthAllegiance == allegiance
                && _lastHealthAlive == combatState.IsAlive)
            {
                return;
            }

            _targetHealthWidth = width;
            _healthFillColor = combatState.IsAlive
                ? ResolveHealthColor(allegiance)
                : new Color(0.25f, 0.02f, 0.02f, 0.95f);
            _healthTextColor = ResolveHealthTextColor(combatState);
            if (healthDropped)
            {
                _healthTrailWidth = Mathf.Max(_healthTrailWidth, _lastHealthWidth, width);
                _lastHealthTrailDrawWidth = -1f;
                _healthFlashTime = HealthFlashDuration;
                StartFramePulse(HitPulseDuration);
            }
            else if (!hasPreviousHealth || healthChanged || combatState.MaxHealth != _lastMaxHealth)
            {
                _healthTrailWidth = width;
                _lastHealthTrailDrawWidth = -1f;
                _healthTrail.visible = false;
            }

            UpdateHealthSegments(combatState.MaxHealth, segmentUnit);
            _healthText.text = string.Empty;
            _healthText.visible = false;
            _healthText.color = ResolveAnimatedHealthTextColor();
            DrawHealthFill(width, ResolveAnimatedHealthFillColor());
            _lastHealthWidth = width;
            _lastHealthValue = combatState.Health;
            _lastMaxHealth = combatState.MaxHealth;
            _lastHealthSegmentUnit = segmentUnit;
            _lastHealthAllegiance = allegiance;
            _lastHealthAlive = combatState.IsAlive;
        }

        private void UpdateHealthSegments(int maxHealth, int segmentUnit)
        {
            int safeMaxHealth = Math.Max(1, maxHealth);
            int segmentCount = Mathf.Clamp(
                Mathf.CeilToInt((float)safeMaxHealth / Math.Max(1, segmentUnit)),
                1,
                MaxVisibleHealthSegments);
            int markerCount = Mathf.Min(_healthSegmentMarkers.Length, segmentCount - 1);

            for (int i = 0; i < _healthSegmentMarkers.Length; i++)
            {
                Fgui.GGraph marker = _healthSegmentMarkers[i];
                if (i >= markerCount)
                {
                    marker.visible = false;
                    continue;
                }

                int segmentIndex = i + 1;
                int healthAtMarker = segmentIndex * segmentUnit;
                float normalizedX = Mathf.Clamp01((float)healthAtMarker / safeMaxHealth);
                bool major = healthAtMarker % MajorHealthSegmentUnit == 0 || segmentUnit >= MajorHealthSegmentUnit;
                float markerWidth = major ? 2f : 1f;
                float markerHeight = major ? HealthBarHeight + 3f : HealthBarHeight + 1f;
                Color markerColor = major
                    ? new Color(1f, 1f, 1f, 0.55f)
                    : new Color(0.04f, 0.06f, 0.08f, 0.75f);

                marker.visible = true;
                marker.SetXY(BarX + Mathf.Round(BarWidth * normalizedX), HealthBarY - (major ? 2f : 1f));
                marker.DrawRect(markerWidth, markerHeight, 0, Color.clear, markerColor);
            }
        }

        private void UpdateCohesion(CharacterTestCharacterCombatStateComponent combatState)
        {
            var readout = TacticalCohesionReadout.FromScatterPressure(
                combatState.ScatterPressure, combatState.MaxScatterPressure,
                combatState.ScatterPressureBand == PressureBand.Broken);
            float width = Mathf.Round(BarWidth * readout.Remaining01);
            _scatterPressureFill.visible = readout.IsAvailable && width > 0f;
            _cohesionBreakMarker.visible = false;
            for (int i = 0; i < _scatterPressureThresholds.Length; i++)
                _scatterPressureThresholds[i].visible = false;
            if (Mathf.Abs(width - _lastScatterPressureWidth) > 0.01f
                || _lastScatterPressureBand != combatState.ScatterPressureBand
                || _lastScatterPressureAlive != combatState.IsAlive)
            {
                _scatterPressureFill.DrawRect(width, ScatterPressureBarHeight, 0, Color.clear, ResolveScatterPressureColor(combatState));
                _lastScatterPressureWidth = width;
                _lastScatterPressureBand = combatState.ScatterPressureBand;
                _lastScatterPressureAlive = combatState.IsAlive;
            }
        }

        private void UpdateCohesionRecovery(
            CharacterTestCharacterCombatStateComponent combatState,
            long currentFrame)
        {
            if (!combatState.TryGetCohesionBreakRecoveryProgress(currentFrame, out float progress))
            {
                HideCohesionRecovery();
                return;
            }

            float safeProgress = Mathf.Clamp01(progress);
            float width = safeProgress <= 0f
                ? 1f
                : Mathf.Max(1f, Mathf.Round(BarWidth * safeProgress));
            float progressX = Mathf.Round(BarWidth * safeProgress);
            float sparkX = BarX + Mathf.Clamp(
                progressX - (CohesionRecoverySparkWidth * 0.5f),
                0f,
                BarWidth - CohesionRecoverySparkWidth);

            bool changed = !_lastCohesionRecoveryVisible
                || Mathf.Abs(width - _lastCohesionRecoveryWidth) > 0.01f
                || Mathf.Abs(sparkX - _lastCohesionRecoverySparkX) > 0.01f;
            _cohesionRecoveryFill.visible = true;
            _cohesionRecoverySpark.visible = true;
            if (!changed)
                return;

            _cohesionRecoveryFill.SetXY(
                BarX,
                ScatterPressureBarY + ScatterPressureBarHeight - CohesionRecoveryFillHeight);
            _cohesionRecoveryFill.SetSize(width, CohesionRecoveryFillHeight);
            _cohesionRecoveryFill.DrawRect(
                width,
                CohesionRecoveryFillHeight,
                0,
                Color.clear,
                new Color(0.4f, 0.96f, 0.78f, 0.96f));
            _cohesionRecoverySpark.SetXY(sparkX, ScatterPressureBarY - 2f);
            _cohesionRecoverySpark.SetSize(CohesionRecoverySparkWidth, ScatterPressureBarHeight + 4f);
            _cohesionRecoverySpark.DrawRect(
                CohesionRecoverySparkWidth,
                ScatterPressureBarHeight + 4f,
                0,
                Color.clear,
                new Color(1f, 0.86f, 0.36f, 1f));
            _lastCohesionRecoveryWidth = width;
            _lastCohesionRecoverySparkX = sparkX;
            _lastCohesionRecoveryVisible = true;
        }

        private void HideCohesionRecovery()
        {
            _cohesionRecoveryFill.visible = false;
            _cohesionRecoverySpark.visible = false;
            _lastCohesionRecoveryWidth = -1f;
            _lastCohesionRecoverySparkX = -1f;
            _lastCohesionRecoveryVisible = false;
        }

        private static string FormatHealthNumber(CharacterTestCharacterCombatStateComponent combatState)
        {
            return Math.Max(0, combatState.Health).ToString(CultureInfo.InvariantCulture);
        }

        private static int ResolveHealthSegmentUnit(int maxHealth)
        {
            int safeMaxHealth = Math.Max(1, maxHealth);
            for (int i = 0; i < s_healthSegmentUnits.Length; i++)
            {
                if (Mathf.CeilToInt((float)safeMaxHealth / s_healthSegmentUnits[i]) <= MaxVisibleHealthSegments)
                    return s_healthSegmentUnits[i];
            }

            int minimumUnit = Mathf.CeilToInt((float)safeMaxHealth / MaxVisibleHealthSegments);
            return Mathf.CeilToInt(minimumUnit / 250f) * 250;
        }

        private static CharacterTestHeadPadVisualState ResolveVisualState(
            CharacterTestCharacterCombatStateComponent combatState)
        {
            if (!combatState.IsAlive)
                return CharacterTestHeadPadVisualState.Down;
            if (combatState.IsCohesionBreakReacting)
                return CharacterTestHeadPadVisualState.CohesionBreak;
            if (combatState.IsReacting)
                return CharacterTestHeadPadVisualState.Hit;

            return CharacterTestHeadPadVisualState.Normal;
        }

        private static Color ResolveFrameColor(CharacterTestHeadPadVisualState state)
        {
            switch (state)
            {
                case CharacterTestHeadPadVisualState.Down:
                    return new Color(0.48f, 0.08f, 0.08f, 0.94f);
                case CharacterTestHeadPadVisualState.CohesionBreak:
                    return new Color(1f, 0.48f, 0.16f, 0.98f);
                case CharacterTestHeadPadVisualState.Hit:
                    return new Color(1f, 0.78f, 0.28f, 0.96f);
                default:
                    return new Color(0.18f, 0.24f, 0.30f, 0.75f);
            }
        }

        private static Color ResolveMarkerColor(CharacterTestHeadPadAllegiance allegiance)
        {
            switch (allegiance)
            {
                case CharacterTestHeadPadAllegiance.Player:
                    return new Color(0.20f, 0.88f, 0.45f, 1f);
                case CharacterTestHeadPadAllegiance.Ally:
                    return new Color(0.25f, 0.68f, 1f, 1f);
                case CharacterTestHeadPadAllegiance.Enemy:
                    return new Color(0.95f, 0.25f, 0.25f, 1f);
                default:
                    return new Color(0.65f, 0.70f, 0.75f, 1f);
            }
        }

        internal static Color ResolveBuffBadgeBorderColor(GameplayBuffPresentationPolarity polarity)
        {
            switch (polarity)
            {
                case GameplayBuffPresentationPolarity.Buff:
                    return new Color(0.32f, 0.78f, 0.95f, 0.96f);
                case GameplayBuffPresentationPolarity.Debuff:
                    return new Color(0.9f, 0.18f, 0.24f, 0.96f);
                case GameplayBuffPresentationPolarity.Aura:
                    return new Color(0.84f, 0.68f, 0.2f, 0.98f);
                default:
                    return new Color(0.72f, 0.72f, 0.72f, 0.86f);
            }
        }

        internal static Color ResolveBuffBadgeBackgroundColor(GameplayBuffPresentationPolarity polarity)
        {
            switch (polarity)
            {
                case GameplayBuffPresentationPolarity.Buff:
                    return new Color(0.06f, 0.2f, 0.28f, 0.92f);
                case GameplayBuffPresentationPolarity.Debuff:
                    return new Color(0.28f, 0.04f, 0.07f, 0.92f);
                case GameplayBuffPresentationPolarity.Aura:
                    return new Color(0.28f, 0.2f, 0.05f, 0.94f);
                default:
                    return new Color(0.13f, 0.14f, 0.15f, 0.9f);
            }
        }

        private static string ResolveBuffBadgeShortName(GameplayBuffPresentationItem item)
        {
            string value = item.ShortName;
            if (string.IsNullOrWhiteSpace(value))
                value = item.DisplayName;
            if (string.IsNullOrWhiteSpace(value))
                value = "B" + item.BuffId.ToString(CultureInfo.InvariantCulture);

            value = value.Trim();
            return value.Length <= 5 ? value : value.Substring(0, 5);
        }

        private static Color ResolveHealthTextColor(CharacterTestCharacterCombatStateComponent combatState)
        {
            if (!combatState.IsAlive)
                return new Color(0.98f, 0.38f, 0.32f, 1f);

            return new Color(0.96f, 0.98f, 1.0f, 1f);
        }

        private static Color ResolveHealthColor(CharacterTestHeadPadAllegiance allegiance)
        {
            if (allegiance == CharacterTestHeadPadAllegiance.Enemy)
                return new Color(0.84f, 0.44f, 0.24f, 1f);
            if (allegiance == CharacterTestHeadPadAllegiance.Player || allegiance == CharacterTestHeadPadAllegiance.Ally)
                return new Color(0.38f, 0.68f, 0.47f, 1f);
            return new Color(0.75f, 0.75f, 0.55f, 0.95f);
        }

        private static Color ResolveScatterPressureColor(CharacterTestCharacterCombatStateComponent combatState)
        {
            return combatState.IsAlive ? new Color(0.36f, 0.66f, 0.80f, 1f) : new Color(0.12f, 0.17f, 0.20f, 0.65f);
        }

        private void UpdateHealthTrailAnimation(float deltaTime)
        {
            if (_targetHealthWidth < 0f || _healthTrailWidth <= _targetHealthWidth + AnimationSnapThreshold)
            {
                _healthTrailWidth = Mathf.Max(_targetHealthWidth, 0f);
                _healthTrail.visible = false;
                return;
            }

            float factor = 1f - Mathf.Exp(-HealthTrailSettleSpeed * deltaTime);
            _healthTrailWidth = Mathf.Lerp(_healthTrailWidth, _targetHealthWidth, factor);
            if (_healthTrailWidth <= _targetHealthWidth + AnimationSnapThreshold)
            {
                _healthTrailWidth = _targetHealthWidth;
                _healthTrail.visible = false;
                return;
            }

            if (Mathf.Abs(_healthTrailWidth - _lastHealthTrailDrawWidth) <= 0.1f)
                return;

            _healthTrail.visible = true;
            _healthTrail.DrawRect(_healthTrailWidth, HealthBarHeight, 0, Color.clear, new Color(1f, 0.52f, 0.18f, 0.72f));
            _lastHealthTrailDrawWidth = _healthTrailWidth;
        }

        private void UpdateHealthFlashAnimation(float deltaTime)
        {
            if (_healthFlashTime <= 0f)
                return;

            _healthFlashTime = Mathf.Max(0f, _healthFlashTime - deltaTime);
            DrawHealthFill(_targetHealthWidth, ResolveAnimatedHealthFillColor());
            _healthText.color = ResolveAnimatedHealthTextColor();
        }

        private void UpdateFramePulseAnimation(float deltaTime)
        {
            if (_framePulseTime <= 0f)
                return;

            _framePulseTime = Mathf.Max(0f, _framePulseTime - deltaTime);
            DrawFrame(ResolveAnimatedFrameColor());
        }

        private void UpdateCohesionBreakFlashAnimation(float deltaTime)
        {
            if (_cohesionBreakFlashTime <= 0f)
                return;

            _cohesionBreakFlashTime = Mathf.Max(0f, _cohesionBreakFlashTime - deltaTime);
            if (_cohesionBreakFlashTime <= 0f)
            {
                HideCohesionBreakFlash();
                return;
            }

            DrawCohesionBreakFlash(Mathf.Clamp01(_cohesionBreakFlashTime / CohesionBreakFlashDuration));
        }

        private void StartFramePulse(float duration)
        {
            _framePulseDuration = Mathf.Max(duration, 0.01f);
            _framePulseTime = _framePulseDuration;
            DrawFrame(ResolveAnimatedFrameColor());
        }

        private void StartCohesionBreakFlash()
        {
            _cohesionBreakFlashTime = CohesionBreakFlashDuration;
            DrawCohesionBreakFlash(1f);
        }

        private void DrawCohesionBreakFlash(float normalized)
        {
            if (_displayMode == CharacterTestHeadPadDisplayMode.Hidden)
            {
                HideCohesionBreakFlash();
                return;
            }

            float t = Mathf.Clamp01(normalized);
            _cohesionBreakFlash.visible = t > 0f;
            _cohesionBreakLabel.visible = t > 0f;
            if (t <= 0f)
                return;

            _cohesionBreakFlash.alpha = t;
            _cohesionBreakLabel.alpha = Mathf.Clamp01(t * 1.35f);
            float labelScale = Mathf.Lerp(1f, 1.16f, t);
            _cohesionBreakLabel.scaleX = labelScale;
            _cohesionBreakLabel.scaleY = labelScale;
            _cohesionBreakLabel.SetXY(
                CohesionBreakLabelX(),
                CohesionBreakLabelBaseY - Mathf.Round(3f * t));
        }

        private void HideCohesionBreakFlash()
        {
            _cohesionBreakFlashTime = 0f;
            _cohesionBreakFlash.visible = false;
            _cohesionBreakFlash.alpha = 0f;
            _cohesionBreakLabel.visible = false;
            _cohesionBreakLabel.alpha = 0f;
            _cohesionBreakLabel.scaleX = 1f;
            _cohesionBreakLabel.scaleY = 1f;
            _cohesionBreakLabel.SetXY(CohesionBreakLabelX(), CohesionBreakLabelBaseY);
        }

        private void DrawFrame(Color color)
        {
            _frame.DrawRoundRect(Width, HealthBarHeight + 4f, Color.clear, s_frameRadii);
            _frame.visible = false;
        }

        private void DrawHealthFill(float width, Color color)
        {
            _healthFill.DrawRect(Mathf.Max(1f, width), HealthBarHeight, 0, Color.clear, color);
        }

        private Color ResolveAnimatedFrameColor()
        {
            Color baseColor = ResolveFrameColor(_lastVisualState);
            if (_framePulseTime <= 0f)
                return baseColor;

            float amount = Mathf.Clamp01(_framePulseTime / _framePulseDuration);
            return Color.Lerp(baseColor, ResolveFramePulseColor(_lastVisualState), amount);
        }

        private Color ResolveAnimatedHealthFillColor()
        {
            if (_healthFlashTime <= 0f)
                return _healthFillColor;

            float amount = Mathf.Clamp01(_healthFlashTime / HealthFlashDuration);
            return Color.Lerp(_healthFillColor, new Color(1f, 0.78f, 0.34f, 1f), amount);
        }

        private Color ResolveAnimatedHealthTextColor()
        {
            if (_healthFlashTime <= 0f)
                return _healthTextColor;

            float amount = Mathf.Clamp01(_healthFlashTime / HealthFlashDuration);
            return Color.Lerp(_healthTextColor, Color.white, amount);
        }

        private static Color ResolveFramePulseColor(CharacterTestHeadPadVisualState state)
        {
            switch (state)
            {
                case CharacterTestHeadPadVisualState.Down:
                    return new Color(0.82f, 0.12f, 0.08f, 1f);
                case CharacterTestHeadPadVisualState.CohesionBreak:
                    return new Color(1f, 0.78f, 0.24f, 1f);
                case CharacterTestHeadPadVisualState.Hit:
                    return new Color(1f, 0.92f, 0.48f, 1f);
                default:
                    return new Color(0.82f, 0.66f, 0.36f, 1f);
            }
        }
    }

    internal enum CharacterTestHeadPadAllegiance
    {
        Neutral,
        Player,
        Ally,
        Enemy
    }

    public enum CharacterTestHeadPadDisplayMode
    {
        [InspectorName("战斗读数")]
        CombatReadout = 0,

        [InspectorName("仅生命值")]
        HealthOnly = 1,

        [InspectorName("隐藏")]
        Hidden = 2
    }

    internal static class CharacterTestHeadPadDisplayModes
    {
        public static CharacterTestHeadPadDisplayMode Normalize(CharacterTestHeadPadDisplayMode displayMode)
        {
            switch (displayMode)
            {
                case CharacterTestHeadPadDisplayMode.CombatReadout:
                case CharacterTestHeadPadDisplayMode.HealthOnly:
                case CharacterTestHeadPadDisplayMode.Hidden:
                    return displayMode;
                default:
                    return CharacterTestHeadPadDisplayMode.CombatReadout;
            }
        }
    }

    internal enum CharacterTestHeadPadVisualState
    {
        Normal,
        Hit,
        CohesionBreak,
        Down
    }

    [DefaultExecutionOrder(10000)]
    internal sealed class CharacterTestFairyGuiHeadPadLateUpdateDriver : MonoBehaviour
    {
        private static CharacterTestFairyGuiHeadPadLateUpdateDriver s_instance;

        public static void Ensure()
        {
            if (s_instance != null)
                return;

            CharacterTestFairyGuiHeadPadLateUpdateDriver instance =
                FindAnyObjectByType<CharacterTestFairyGuiHeadPadLateUpdateDriver>();
            if (instance == null)
            {
                GameObject host = new GameObject("CharacterTest.HeadPadLateUpdate");
                host.hideFlags = HideFlags.HideAndDontSave;
                instance = host.AddComponent<CharacterTestFairyGuiHeadPadLateUpdateDriver>();
                if (Application.isPlaying)
                    DontDestroyOnLoad(host);
            }

            s_instance = instance;
        }

        private void LateUpdate()
        {
            CharacterTestFairyGuiHeadPad.TickAllVisibilityPresentations(Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }
    }
}
