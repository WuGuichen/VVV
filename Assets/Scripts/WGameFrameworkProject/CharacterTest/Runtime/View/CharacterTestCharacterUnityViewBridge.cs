using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.CharacterApplication;
using MxFramework.CharacterCloth;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using MxFramework.Runtime;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestAnimationInputPresentationState
    {
        public CharacterTestAnimationInputPresentationState(
            int entityId,
            CharacterTestCharacterAnimationComponent animation)
        {
            EntityId = entityId;
            Animation = animation;
        }

        public int EntityId { get; }
        public CharacterTestCharacterAnimationComponent Animation { get; }
    }

    internal sealed class CharacterTestAnimationPresentationCheckpoint
    {
        public CharacterTestAnimationPresentationCheckpoint(
            RuntimeFrame frame,
            CharacterTestRuntimeCharacterPresentationState[] characters,
            CharacterTestAnimationInputPresentationState[] animationInputs,
            bool frameAligned,
            string diagnostic)
        {
            Frame = frame;
            Characters = characters ?? Array.Empty<CharacterTestRuntimeCharacterPresentationState>();
            AnimationInputs = animationInputs ?? Array.Empty<CharacterTestAnimationInputPresentationState>();
            FrameAligned = frameAligned;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RuntimeFrame Frame { get; }
        public IReadOnlyList<CharacterTestRuntimeCharacterPresentationState> Characters { get; }
        public IReadOnlyList<CharacterTestAnimationInputPresentationState> AnimationInputs { get; }
        public bool FrameAligned { get; }
        public string Diagnostic { get; }
    }

    public sealed class CharacterTestCharacterUnityViewBridge
    {
        private readonly GlobalResourceRuntimeServices _resources;
        private readonly IRuntimeLogger _logger;
        private readonly CharacterTestRuntimeCharacterLoader _loader;
        private readonly Dictionary<int, CharacterTestRuntimeCharacterView> _views =
            new Dictionary<int, CharacterTestRuntimeCharacterView>();
        /// <summary>Session controls for spawned gameplay cloth; independent previews are not registered.</summary>
        public CharacterClothPresentationService ClothPresentation { get; } = new CharacterClothPresentationService();
        internal CharacterTestPresentationLod PresentationLod { get; } = new CharacterTestPresentationLod();
        private CharacterTestTraceRecorder _traceRecorder;
        private int _lastSyncRequestCount;
        private int _lastSyncAppliedCount;
        private int _lastSyncMissCount;
        private int _lastTransformApplyCount;
        private int _totalSyncRequestCount;
        private int _totalSyncAppliedCount;
        private int _totalSyncMissCount;
        private int _totalTransformApplyCount;
        private UnityCamera _cachedMainCamera;
        private RuntimeFrame _lastPresentationFrame;
        private bool _hasPresentationFrame;
        private RuntimeFrame _runtimeAiLabAnimationFrame;
        private bool _hasRuntimeAiLabAnimationFrame;

        public CharacterTestCharacterUnityViewBridge(
            GlobalResourceRuntimeServices resources,
            IRuntimeLogger logger,
            bool installDefaultProceduralHitFeedbackChain = true)
        {
            _resources = resources;
            _logger = logger ?? NullRuntimeLogger.Instance;
            _loader = new CharacterTestRuntimeCharacterLoader(
                _resources?.ResourceManager,
                _logger,
                installDefaultProceduralHitFeedbackChain);
        }

        public void SetTraceRecorder(CharacterTestTraceRecorder traceRecorder)
        {
            _traceRecorder = traceRecorder;
        }

        public bool TryCreateView(CharacterTestCharacterEntity entity)
        {
            if (entity == null)
            {
                _logger.Warning("CharacterSpawn", "Cannot create character view for null entity.");
                return false;
            }

            if (_resources == null || _resources.ResourceManager == null)
            {
                _logger.Warning("CharacterSpawn", "Resource runtime is not available for character view.");
                return false;
            }

            ResourceLoadResult<ResourceHandle<GameObject>> result =
                _resources.ResourceManager.Load<GameObject>(entity.Identity.PrefabKey);

            if (!result.Success || result.Value == null || result.Value.Value == null)
            {
                _logger.Warning(
                    "CharacterSpawn",
                    "Character prefab load failed. entityId=" + entity.Id
                    + " characterId=" + entity.Identity.CharacterId
                    + " key=" + entity.Identity.PrefabKey
                    + " error=" + result.Error.Message);
                return false;
            }

            Vector3 position = new Vector3(
                entity.Transform.X,
                entity.Transform.Y,
                entity.Transform.Z);
            string runtimeName = result.Value.Value.name + "_Entity" + entity.Id.Value;
            if (!_loader.Instantiate(
                entity.Id,
                result.Value,
                position,
                runtimeName,
                entity.Identity.AnimationBinding,
                entity.Identity.GeneratedPackageRoot,
                entity.Control.Owner,
                out CharacterTestRuntimeCharacterView view))
            {
                _resources.ResourceManager.Release(result.Value);
                return false;
            }

            view.AttachClothPresentation(ClothPresentation);
            view.AttachPresentationLod(PresentationLod);
            _views[entity.Id.Value] = view;
            _logger.Info(
                "CharacterSpawn",
                "Character view created. entityId=" + entity.Id
                + " characterId=" + entity.Identity.CharacterId
                + " controlOwner=" + entity.Control.Owner
                + " position=" + position
                + " instance=" + view.Instance.name);
            return true;
        }

        internal int ActiveViewCount => _views.Count;

        internal int LastSyncRequestCount => _lastSyncRequestCount;

        internal int LastSyncAppliedCount => _lastSyncAppliedCount;

        internal int LastSyncMissCount => _lastSyncMissCount;

        internal int LastTransformApplyCount => _lastTransformApplyCount;

        internal int TotalSyncRequestCount => _totalSyncRequestCount;

        internal int TotalSyncAppliedCount => _totalSyncAppliedCount;

        internal int TotalSyncMissCount => _totalSyncMissCount;

        internal int TotalTransformApplyCount => _totalTransformApplyCount;

        internal CharacterTestAnimationViewDiagnostics CreateAnimationDiagnostics()
        {
            var diagnostics = new CharacterTestAnimationViewDiagnostics(_views.Count);
            foreach (CharacterTestRuntimeCharacterView view in _views.Values)
                diagnostics.Add(view.CreateAnimationDiagnostics());
            return diagnostics;
        }

        internal void BeginViewSyncFrame()
        {
            _lastSyncRequestCount = 0;
            _lastSyncAppliedCount = 0;
            _lastSyncMissCount = 0;
            _lastTransformApplyCount = 0;
        }

        public bool ReleaseView(CharacterTestCharacterEntityId entityId)
        {
            if (!_views.TryGetValue(entityId.Value, out CharacterTestRuntimeCharacterView view))
                return false;

            view.Release();
            _views.Remove(entityId.Value);
            return true;
        }

        public bool SyncView(
            CharacterTestCharacterEntity entity,
            bool hasLocalPlayerTeam,
            int localPlayerTeamId,
            bool smoothNetworkSnapshot = false,
            RuntimeFrame frame = default,
            bool headPadLockedVisible = false,
            CharacterTestHeadPadDisplayMode headPadDisplayMode =
                CharacterTestHeadPadDisplayMode.CombatReadout,
            IReadOnlyList<GameplayBuffPresentationItem> headPadBuffs = null)
        {
            _lastSyncRequestCount++;
            _totalSyncRequestCount++;
            if (entity == null || !_views.TryGetValue(entity.Id.Value, out CharacterTestRuntimeCharacterView view))
            {
                _lastSyncMissCount++;
                _totalSyncMissCount++;
                return false;
            }

            int transformApplyCountBefore = view.TransformApplyCount;
            view.SyncTransform(
                new Vector3(
                    entity.Transform.X,
                    entity.Transform.Y,
                    entity.Transform.Z),
                Quaternion.Euler(0f, entity.Transform.YawDegrees, 0f),
                smoothNetworkSnapshot,
                frame);
            view.SyncSeverancePresentation(entity.BodyPartState);
            int appliedTransforms = view.TransformApplyCount - transformApplyCountBefore;
            if (appliedTransforms > 0)
            {
                _lastTransformApplyCount += appliedTransforms;
                _totalTransformApplyCount += appliedTransforms;
            }

            view.SyncCombatState(
                entity.CombatState,
                entity.Identity.DisplayName,
                entity.Team.TeamId,
                entity.Control.Owner,
                hasLocalPlayerTeam,
                localPlayerTeamId,
                headPadLockedVisible,
                headPadDisplayMode,
                headPadBuffs,
                frame);
            _lastSyncAppliedCount++;
            _totalSyncAppliedCount++;
            return true;
        }

        public void TickNetworkPresentation(
            double deltaTime,
            int tickRate,
            int interpolationDelayFrames)
        {
            foreach (CharacterTestRuntimeCharacterView view in _views.Values)
                view.TickNetworkPresentation(deltaTime, tickRate, interpolationDelayFrames);
        }

        public void TickPresentation(double deltaTime, RuntimeFrame frame = default)
        {
            float safeDeltaTime = deltaTime > 0d ? (float)deltaTime : 0f;
            if (_hasRuntimeAiLabAnimationFrame && _runtimeAiLabAnimationFrame == frame)
                safeDeltaTime = 0f;
            UnityCamera mainCamera = CharacterTestMainCameraCache.Resolve(ref _cachedMainCamera);
            foreach (CharacterTestRuntimeCharacterView view in _views.Values)
                view.TickPresentation(safeDeltaTime, frame, mainCamera);
            _lastPresentationFrame = frame;
            _hasPresentationFrame = true;
        }

        internal void TickRuntimeAiLabReplayAnimationState(
            double deltaTime,
            RuntimeFrame frame)
        {
            float safeDeltaTime = deltaTime > 0d ? (float)deltaTime : 0f;
            foreach (CharacterTestRuntimeCharacterView view in _views.Values)
                view.TickRuntimeAiLabReplayAnimationState(safeDeltaTime, frame);
            _runtimeAiLabAnimationFrame = frame;
            _hasRuntimeAiLabAnimationFrame = true;
            _lastPresentationFrame = frame;
            _hasPresentationFrame = true;
        }

        internal CharacterTestAnimationPresentationCheckpoint CaptureAnimationPresentationCheckpoint(
            RuntimeFrame frame,
            IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            var characters = new CharacterTestRuntimeCharacterPresentationState[_views.Count];
            int characterIndex = 0;
            foreach (CharacterTestRuntimeCharacterView view in _views.Values)
                characters[characterIndex++] = view.CapturePresentationState();
            Array.Sort(characters, CharacterPresentationStateComparer.Instance);

            int entityCount = entities != null ? entities.Count : 0;
            var animationInputs = new CharacterTestAnimationInputPresentationState[entityCount];
            for (int i = 0; i < entityCount; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                animationInputs[i] = new CharacterTestAnimationInputPresentationState(
                    entity.Id.Value,
                    entity.Animation);
            }
            Array.Sort(animationInputs, AnimationInputPresentationStateComparer.Instance);

            bool aligned = _hasPresentationFrame && _lastPresentationFrame == frame;
            return new CharacterTestAnimationPresentationCheckpoint(
                frame,
                characters,
                animationInputs,
                aligned,
                aligned ? string.Empty : "Presentation has not reached the requested RuntimeFrame.");
        }

        internal bool TryCaptureCharacterPresentationState(
            CharacterTestCharacterEntityId entityId,
            out CharacterTestRuntimeCharacterPresentationState state)
        {
            if (_views.TryGetValue(entityId.Value, out CharacterTestRuntimeCharacterView view))
            {
                state = view.CapturePresentationState();
                return true;
            }

            state = null;
            return false;
        }

        internal bool RestoreAnimationPresentationCheckpoint(
            CharacterTestAnimationPresentationCheckpoint checkpoint,
            RuntimeFrame expectedFrame,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (checkpoint == null || checkpoint.Frame != expectedFrame || !checkpoint.FrameAligned)
            {
                diagnostic = checkpoint?.Diagnostic ?? "Animation presentation checkpoint is missing.";
                return false;
            }

            IReadOnlyList<CharacterTestRuntimeCharacterPresentationState> characters = checkpoint.Characters;
            for (int i = 0; i < characters.Count; i++)
            {
                CharacterTestRuntimeCharacterPresentationState state = characters[i];
                if (state == null || !_views.TryGetValue(state.EntityId, out CharacterTestRuntimeCharacterView view))
                {
                    diagnostic = "Animation presentation checkpoint references a missing character view.";
                    return false;
                }
                if (!view.RestorePresentationState(state, out diagnostic))
                    return false;
            }

            _lastPresentationFrame = expectedFrame;
            _hasPresentationFrame = true;
            return true;
        }

        internal bool RestoreCanonicalAnimation(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            int tickRate,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            if (entities == null)
                return true;
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null || !_views.TryGetValue(entity.Id.Value, out CharacterTestRuntimeCharacterView view))
                    continue;
                int key = entity.Action.LastAnimationBindingId > 0
                    ? entity.Action.LastAnimationBindingId : entity.Animation.BindingId;
                if (key <= 0)
                    continue;
                int localFrame = entity.Action.LocalFrame >= 0
                    ? entity.Action.LocalFrame
                    : 0;
                if (!view.RestoreCanonicalAnimation(key, localFrame, tickRate, out diagnostic))
                    return false;
            }

            return true;
        }

        public void SyncAuthorityPose(
            IReadOnlyList<CharacterTestCharacterEntity> entities,
            RuntimeFrame expectedRuntimeFrame)
        {
            if (entities == null || entities.Count == 0)
                return;

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                if (entity == null || !_views.TryGetValue(entity.Id.Value, out CharacterTestRuntimeCharacterView view))
                    continue;

                view.SyncAuthorityPose(
                    entity.AuthorityPose,
                    entity.TerrainIkState,
                    entity.ResolvedAuthorityPoseFrame,
                    expectedRuntimeFrame);
            }
        }

        internal bool TryGetBladeTrailView(int entityId, out CharacterTestRuntimeCharacterView view) =>
            _views.TryGetValue(entityId, out view) && view.Instance != null;

        public bool TryResolveVfxAnchor(
            int entityId,
            int socketId,
            out Vector3 position,
            out Vector3 forward)
        {
            position = Vector3.zero;
            forward = Vector3.forward;
            return _views.TryGetValue(entityId, out CharacterTestRuntimeCharacterView view)
                && view.TryResolveVfxAnchor(socketId, out position, out forward);
        }

        internal bool TryResolveAbstractBoneAnchor(
            int entityId,
            string abstractBoneId,
            out Vector3 position,
            out Vector3 forward)
        {
            position = Vector3.zero;
            forward = Vector3.forward;
            return _views.TryGetValue(entityId, out CharacterTestRuntimeCharacterView view)
                && view.TryResolveAbstractBoneAnchor(abstractBoneId, out position, out forward);
        }

        internal bool TryResolveBodyPartContactPointEntityLocalOffset(
            CharacterTestCharacterEntity entity,
            CharacterBodyPartContactPointConfig config,
            out FixVector3 localOffset)
        {
            localOffset = FixVector3.Zero;
            if (entity == null
                || !_views.TryGetValue(entity.Id.Value, out CharacterTestRuntimeCharacterView view)
                || !view.TryResolveBodyPartContactPointWorldPosition(config, out Vector3 worldPosition))
            {
                return false;
            }

            CharacterTestCharacterTransformComponent transform = entity.Transform;
            Vector3 bodyPosition = new Vector3(transform.X, transform.Y, transform.Z);
            Vector3 delta = worldPosition - bodyPosition;
            float yawRadians = transform.YawDegrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(yawRadians);
            float cos = Mathf.Cos(yawRadians);
            Vector3 right = new Vector3(cos, 0f, -sin);
            Vector3 forward = new Vector3(sin, 0f, cos);
            localOffset = new FixVector3(
                ToFix64(Vector3.Dot(delta, right)),
                ToFix64(delta.y),
                ToFix64(Vector3.Dot(delta, forward)));
            return true;
        }

        private static Fix64 ToFix64(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return Fix64.Zero;

            return Fix64.FromRaw((long)System.Math.Round(value * Fix64.Scale));
        }

        internal bool TryResolveControlStatusVfxAnchor(
            int entityId,
            out Vector3 position,
            out Vector3 forward)
        {
            position = Vector3.zero;
            forward = Vector3.forward;
            return _views.TryGetValue(entityId, out CharacterTestRuntimeCharacterView view)
                && view.TryResolveControlStatusVfxAnchor(out position, out forward);
        }

        internal bool TryResolveTargetLockUiAnchor(int entityId, out Vector3 position)
        {
            position = Vector3.zero;
            return _views.TryGetValue(entityId, out CharacterTestRuntimeCharacterView view)
                && view.TryResolveTargetLockUiAnchor(out position);
        }

        public bool HasView(CharacterTestCharacterEntityId entityId)
        {
            return _views.ContainsKey(entityId.Value);
        }

        internal bool TryGetSeverancePresentationSnapshot(
            CharacterTestCharacterEntityId entityId,
            int partStateId,
            out CharacterTestSeverancePresentationSnapshot snapshot)
        {
            if (_views.TryGetValue(entityId.Value, out CharacterTestRuntimeCharacterView view))
                return view.TryGetSeverancePresentationSnapshot(partStateId, out snapshot);

            snapshot = default;
            return false;
        }

        internal bool TryGetViewPose(
            CharacterTestCharacterEntityId entityId,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (!_views.TryGetValue(entityId.Value, out CharacterTestRuntimeCharacterView view)
                || view.Instance == null)
            {
                return false;
            }

            Transform transform = view.Instance.transform;
            position = transform.position;
            rotation = transform.rotation;
            return true;
        }

        internal bool TryGetHumanoidPresentationHeights(CharacterTestCharacterEntityId entityId,
            out float rootY, out float hipsY, out float feetY)
        {
            rootY = hipsY = feetY = 0f;
            return _views.TryGetValue(entityId.Value, out CharacterTestRuntimeCharacterView view)
                && view.TryGetHumanoidPresentationHeights(out rootY, out hipsY, out feetY);
        }

        internal bool TryRestoreRuntimeAiLabViewPose(
            CharacterTestCharacterEntityId entityId,
            Vector3 position,
            Quaternion rotation)
        {
            if (!_views.TryGetValue(entityId.Value, out CharacterTestRuntimeCharacterView view)
                || view.Instance == null)
            {
                return false;
            }

            Transform transform = view.Instance.transform;
            transform.SetPositionAndRotation(position, rotation);
            view.ResetClothAfterTeleport();
            return true;
        }

        /// <summary>Notifies cloth of an explicit teleport, including short translations or rotation-only teleports.</summary>
        /// <remarks>LowFreqAlloc if cloth is still building. Call after applying the new character pose.</remarks>
        public bool ResetClothAfterTeleport(CharacterTestCharacterEntityId entityId, bool keepPose = false)
        {
            if (!_views.TryGetValue(entityId.Value, out CharacterTestRuntimeCharacterView view)) return false;
            view.ResetClothAfterTeleport(keepPose);
            return true;
        }

        public bool TryApplyProceduralBoneImpulse(
            CharacterTestCharacterEntityId entityId,
            CharacterProceduralBoneImpulseRequest request)
        {
            return _views.TryGetValue(entityId.Value, out CharacterTestRuntimeCharacterView view)
                && view.TryApplyProceduralBoneImpulse(request);
        }

        public bool TryApplyProceduralSecondaryBoneImpulse(
            CharacterTestCharacterEntityId entityId,
            CharacterProceduralBoneImpulseRequest request)
        {
            return _views.TryGetValue(entityId.Value, out CharacterTestRuntimeCharacterView view)
                && view.TryApplyProceduralSecondaryBoneImpulse(request);
        }

        public bool TryResolveNearestProceduralBodyRegion(
            CharacterTestCharacterEntityId entityId,
            Vector3 worldPosition,
            out int regionId)
        {
            regionId = 0;
            return _views.TryGetValue(entityId.Value, out CharacterTestRuntimeCharacterView view)
                && view.TryResolveNearestProceduralBodyRegion(worldPosition, out regionId);
        }

        public bool TryGetProceduralBoneRigSnapshot(
            CharacterTestCharacterEntityId entityId,
            out CharacterProceduralBoneRigSnapshot snapshot)
        {
            if (_views.TryGetValue(entityId.Value, out CharacterTestRuntimeCharacterView view))
            {
                snapshot = view.CreateProceduralBoneRigSnapshot();
                return true;
            }

            snapshot = CharacterProceduralBoneRigSnapshot.Empty("character view is not available");
            return false;
        }

        public bool SyncAnimation(
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame = default,
            bool presentationPaused = false)
        {
            return entity != null
                && SyncAnimation(entity, entity.Animation, frame, presentationPaused);
        }

        public bool SyncAnimation(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterAnimationComponent animation,
            RuntimeFrame frame = default,
            bool presentationPaused = false)
        {
            if (entity == null || !_views.TryGetValue(entity.Id.Value, out CharacterTestRuntimeCharacterView view))
                return false;

            view.SyncAnimation(
                animation,
                frame,
                _traceRecorder,
                presentationPaused,
                entity.Input.TurnInPlaceYawMilliDegrees,
                requireExactActionClip: entity.BattleState.LifeState == CharacterTestCharacterLifeState.Dying
                    || entity.BattleState.LifeState == CharacterTestCharacterLifeState.Dead);
            return true;
        }

        internal void SyncActionPlaybackTime(CharacterTestCharacterEntityId entityId, double timeSeconds)
        {
            if (_views.TryGetValue(entityId.Value, out CharacterTestRuntimeCharacterView view))
                view.SyncActionPlaybackTime(timeSeconds);
        }

        public void ReleaseAll()
        {
            foreach (CharacterTestRuntimeCharacterView view in _views.Values)
                view.Release();
            _views.Clear();
            PresentationLod.Clear();
            BeginViewSyncFrame();
            _totalSyncRequestCount = 0;
            _totalSyncAppliedCount = 0;
            _totalSyncMissCount = 0;
            _totalTransformApplyCount = 0;
            _cachedMainCamera = null;
        }

        private sealed class CharacterPresentationStateComparer :
            IComparer<CharacterTestRuntimeCharacterPresentationState>
        {
            public static readonly CharacterPresentationStateComparer Instance =
                new CharacterPresentationStateComparer();

            public int Compare(
                CharacterTestRuntimeCharacterPresentationState x,
                CharacterTestRuntimeCharacterPresentationState y)
            {
                int left = x != null ? x.EntityId : int.MinValue;
                int right = y != null ? y.EntityId : int.MinValue;
                return left.CompareTo(right);
            }
        }

        private sealed class AnimationInputPresentationStateComparer :
            IComparer<CharacterTestAnimationInputPresentationState>
        {
            public static readonly AnimationInputPresentationStateComparer Instance =
                new AnimationInputPresentationStateComparer();

            public int Compare(
                CharacterTestAnimationInputPresentationState x,
                CharacterTestAnimationInputPresentationState y)
            {
                return x.EntityId.CompareTo(y.EntityId);
            }
        }

    }

    internal static class CharacterTestMainCameraCache
    {
        public static UnityCamera Resolve(ref UnityCamera cachedMainCamera)
        {
            if (IsUsableMainCamera(cachedMainCamera))
                return cachedMainCamera;

            cachedMainCamera = UnityCamera.main;
            return cachedMainCamera;
        }

        internal static bool IsUsableMainCamera(UnityCamera camera)
        {
            return camera != null
                && camera.isActiveAndEnabled
                && camera.CompareTag("MainCamera");
        }
    }

    internal readonly struct CharacterTestRuntimeCharacterAnimationDiagnostics
    {
        public CharacterTestRuntimeCharacterAnimationDiagnostics(
            int entityId,
            string instanceName,
            bool hasInstance,
            int rendererCount,
            int enabledRendererCount,
            int skinnedRendererCount,
            int enabledSkinnedRendererCount,
            bool hasLocomotionController,
            bool hasAnimationBackend,
            string activeBlendId,
            bool lastAnimationResultSuccess,
            string lastAnimationResultCode,
            string lastAnimationClipKey,
            string lastActionKey,
            bool lastActionResultSuccess,
            string lastActionResultCode,
            string lastActionClipKey)
        {
            EntityId = entityId;
            InstanceName = instanceName ?? string.Empty;
            HasInstance = hasInstance;
            RendererCount = rendererCount;
            EnabledRendererCount = enabledRendererCount;
            SkinnedRendererCount = skinnedRendererCount;
            EnabledSkinnedRendererCount = enabledSkinnedRendererCount;
            HasLocomotionController = hasLocomotionController;
            HasAnimationBackend = hasAnimationBackend;
            ActiveBlendId = activeBlendId ?? string.Empty;
            LastAnimationResultSuccess = lastAnimationResultSuccess;
            LastAnimationResultCode = lastAnimationResultCode ?? string.Empty;
            LastAnimationClipKey = lastAnimationClipKey ?? string.Empty;
            LastActionKey = lastActionKey ?? string.Empty;
            LastActionResultSuccess = lastActionResultSuccess;
            LastActionResultCode = lastActionResultCode ?? string.Empty;
            LastActionClipKey = lastActionClipKey ?? string.Empty;
        }

        public int EntityId { get; }
        public string InstanceName { get; }
        public bool HasInstance { get; }
        public int RendererCount { get; }
        public int EnabledRendererCount { get; }
        public int SkinnedRendererCount { get; }
        public int EnabledSkinnedRendererCount { get; }
        public bool HasLocomotionController { get; }
        public bool HasAnimationBackend { get; }
        public string ActiveBlendId { get; }
        public bool LastAnimationResultSuccess { get; }
        public string LastAnimationResultCode { get; }
        public string LastAnimationClipKey { get; }
        public string LastActionKey { get; }
        public bool LastActionResultSuccess { get; }
        public string LastActionResultCode { get; }
        public string LastActionClipKey { get; }

        public bool Ready => HasInstance
            && EnabledRendererCount > 0
            && HasLocomotionController
            && HasAnimationBackend
            && LastAnimationResultSuccess
            && (string.IsNullOrEmpty(LastActionKey) || LastActionResultSuccess);

        public string Issue
        {
            get
            {
                if (!HasInstance)
                    return "character view instance is missing";
                if (EnabledRendererCount <= 0)
                    return "character view has no enabled renderer";
                if (!HasLocomotionController)
                    return "locomotion controller is missing";
                if (!HasAnimationBackend)
                    return "animation backend is missing";
                if (!LastAnimationResultSuccess)
                    return "locomotion animation failed: " + LastAnimationResultCode + " " + LastAnimationClipKey;
                if (!string.IsNullOrEmpty(LastActionKey) && !LastActionResultSuccess)
                    return "action animation failed: " + LastActionResultCode + " " + LastActionClipKey;
                return string.Empty;
            }
        }
    }

    internal sealed class CharacterTestAnimationViewDiagnostics
    {
        private readonly List<CharacterTestRuntimeCharacterAnimationDiagnostics> _entries;

        public CharacterTestAnimationViewDiagnostics(int capacity)
        {
            _entries = new List<CharacterTestRuntimeCharacterAnimationDiagnostics>(
                capacity > 0 ? capacity : 0);
        }

        public IReadOnlyList<CharacterTestRuntimeCharacterAnimationDiagnostics> Entries => _entries;
        public int ActiveViewCount { get; private set; }
        public int EnabledRendererCount { get; private set; }
        public int EnabledSkinnedRendererCount { get; private set; }
        public int LocomotionControllerCount { get; private set; }
        public int AnimationBackendReadyCount { get; private set; }

        public bool Ready
        {
            get
            {
                if (ActiveViewCount == 0) return false;
                for (int i = 0; i < _entries.Count; i++)
                    if (!_entries[i].Ready) return false;
                return true;
            }
        }

        public string FirstIssue
        {
            get
            {
                if (ActiveViewCount == 0)
                    return "no character views are active";

                for (int i = 0; i < _entries.Count; i++)
                {
                    string issue = _entries[i].Issue;
                    if (!string.IsNullOrEmpty(issue))
                        return issue;
                }

                return string.Empty;
            }
        }

        public void Add(CharacterTestRuntimeCharacterAnimationDiagnostics entry)
        {
            _entries.Add(entry);
            ActiveViewCount++;
            EnabledRendererCount += entry.EnabledRendererCount;
            EnabledSkinnedRendererCount += entry.EnabledSkinnedRendererCount;
            if (entry.HasLocomotionController)
                LocomotionControllerCount++;
            if (entry.HasAnimationBackend)
                AnimationBackendReadyCount++;
        }

        /// <remarks>AllocByDesign: Player/package diagnostics report generation.</remarks>
        public string CreateReportText(string scenarioName)
        {
            var builder = new StringBuilder(2048);
            builder.Append("scenario: ").AppendLine(string.IsNullOrWhiteSpace(scenarioName)
                ? "character-test.player-animation-smoke"
                : scenarioName);
            builder.Append("status: ").AppendLine(Ready ? "ready" : "failed");
            builder.Append("activeViewCount: ").AppendLine(ActiveViewCount.ToString(CultureInfo.InvariantCulture));
            builder.Append("enabledRendererCount: ")
                .AppendLine(EnabledRendererCount.ToString(CultureInfo.InvariantCulture));
            builder.Append("enabledSkinnedRendererCount: ")
                .AppendLine(EnabledSkinnedRendererCount.ToString(CultureInfo.InvariantCulture));
            builder.Append("locomotionControllerCount: ")
                .AppendLine(LocomotionControllerCount.ToString(CultureInfo.InvariantCulture));
            builder.Append("animationBackendReadyCount: ")
                .AppendLine(AnimationBackendReadyCount.ToString(CultureInfo.InvariantCulture));
            if (!Ready)
                builder.Append("issue: ").AppendLine(FirstIssue);
            builder.AppendLine("views:");

            for (int i = 0; i < _entries.Count; i++)
                AppendEntry(builder, _entries[i]);

            return builder.ToString();
        }

        private static void AppendEntry(
            StringBuilder builder,
            CharacterTestRuntimeCharacterAnimationDiagnostics entry)
        {
            builder.Append("- entityId: ").AppendLine(entry.EntityId.ToString(CultureInfo.InvariantCulture));
            builder.Append("  instance: ").AppendLine(EmptyAsNone(entry.InstanceName));
            builder.Append("  ready: ").AppendLine(entry.Ready ? "true" : "false");
            builder.Append("  hasInstance: ").AppendLine(entry.HasInstance ? "true" : "false");
            builder.Append("  rendererCount: ").AppendLine(entry.RendererCount.ToString(CultureInfo.InvariantCulture));
            builder.Append("  enabledRendererCount: ")
                .AppendLine(entry.EnabledRendererCount.ToString(CultureInfo.InvariantCulture));
            builder.Append("  skinnedRendererCount: ")
                .AppendLine(entry.SkinnedRendererCount.ToString(CultureInfo.InvariantCulture));
            builder.Append("  enabledSkinnedRendererCount: ")
                .AppendLine(entry.EnabledSkinnedRendererCount.ToString(CultureInfo.InvariantCulture));
            builder.Append("  hasLocomotionController: ")
                .AppendLine(entry.HasLocomotionController ? "true" : "false");
            builder.Append("  hasAnimationBackend: ")
                .AppendLine(entry.HasAnimationBackend ? "true" : "false");
            builder.Append("  activeBlendId: ").AppendLine(EmptyAsNone(entry.ActiveBlendId));
            builder.Append("  lastAnimationResult: ")
                .Append(entry.LastAnimationResultSuccess ? "success" : "not-success")
                .Append(" code=")
                .Append(EmptyAsNone(entry.LastAnimationResultCode))
                .Append(" clip=")
                .AppendLine(EmptyAsNone(entry.LastAnimationClipKey));
            builder.Append("  lastActionKey: ").AppendLine(EmptyAsNone(entry.LastActionKey));
            builder.Append("  lastActionResult: ")
                .Append(entry.LastActionResultSuccess ? "success" : "not-success")
                .Append(" code=")
                .Append(EmptyAsNone(entry.LastActionResultCode))
                .Append(" clip=")
                .AppendLine(EmptyAsNone(entry.LastActionClipKey));
            if (!entry.Ready)
                builder.Append("  issue: ").AppendLine(entry.Issue);
        }

        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<none>" : value;
        }
    }
}
