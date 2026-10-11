using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Core;
using MxFramework.Core.Math;
using MxFramework.Runtime;
using MxFramework.Vfx;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestActionVfxBridgeModule : RuntimeModule
    {
        private const string LogCategory = "CharacterTest.ActionVfx";

        private readonly Func<IReadOnlyList<CharacterTestCharacterEntity>> _entitiesProvider;
        private readonly Func<int, long, FixVector3?> _channelBeamAimDirectionProvider;
        private readonly VfxRuntimeModule _vfx;
        private readonly IRuntimeLogger _logger;
        private readonly Dictionary<string, VfxDefinitionId> _bladeTrailDefinitions =
            new Dictionary<string, VfxDefinitionId>(StringComparer.Ordinal);
        private readonly HashSet<(int EntityId, long InstanceId, int EventId)> _bladeTrailStartsThisFrame =
            new HashSet<(int, long, int)>();
        private readonly List<VfxInstanceSnapshot> _activeInstances =
            new List<VfxInstanceSnapshot>(32);

        public CharacterTestActionVfxBridgeModule(
            Func<IReadOnlyList<CharacterTestCharacterEntity>> entitiesProvider,
            VfxRuntimeModule vfx,
            IRuntimeLogger logger = null,
            Func<int, long, FixVector3?> channelBeamAimDirectionProvider = null)
            : base("CharacterTest.ActionVfxBridge", RuntimeTickStage.PostSimulation, priority: 140)
        {
            _entitiesProvider = entitiesProvider ?? throw new ArgumentNullException(nameof(entitiesProvider));
            _channelBeamAimDirectionProvider = channelBeamAimDirectionProvider;
            _vfx = vfx ?? throw new ArgumentNullException(nameof(vfx));
            foreach (VfxDefinition definition in _vfx.Definitions.CreateSnapshot())
                _bladeTrailDefinitions[definition.PrefabResource.ToString()] = definition.DefinitionId;
            _logger = logger ?? NullRuntimeLogger.Instance;
        }

        public int LastFrameRequestCount { get; private set; }
        public int LastFrameSpawnedCount { get; private set; }
        public int LastFrameFailureCount { get; private set; }

        /// <remarks>NoAlloc on the steady-state path after registration/initialization; invalid-call and exception paths may allocate diagnostics.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            LastFrameRequestCount = 0;
            _bladeTrailStartsThisFrame.Clear();
            LastFrameSpawnedCount = 0;
            LastFrameFailureCount = 0;

            IReadOnlyList<CharacterTestCharacterEntity> entities = _entitiesProvider();
            if (entities == null || entities.Count == 0)
                return;

            var frame = new RuntimeFrame(context.FrameIndex);
            for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++)
                TickEntity(entities[entityIndex], frame, entityIndex);
        }

        private void TickEntity(CharacterTestCharacterEntity entity, RuntimeFrame frame, int entityIndex)
        {
            if (entity == null || entity.PendingVfxRequestCount == 0)
                return;

            IReadOnlyList<CharacterActionVfxRequest> requests = entity.VfxRequests;
            try
            {
                for (int requestIndex = 0; requestIndex < requests.Count; requestIndex++)
                {
                    CharacterActionVfxRequest request = requests[requestIndex];
                    if (request.Metadata.LifecyclePhase == CharacterActionTrackLifecyclePhase.Active)
                        continue;
                    if (request.Metadata.LifecyclePhase == CharacterActionTrackLifecyclePhase.Exit)
                    {
                        Stop(entity, request, frame);
                        continue;
                    }
                    if (request.Metadata.LifecyclePhase == CharacterActionTrackLifecyclePhase.Enter
                        && HasForcedExitLater(requests, requestIndex, request))
                    {
                        continue;
                    }
                    Spawn(entity, request, frame, entityIndex, requestIndex);
                }
            }
            finally
            {
                entity.ClearVfxRequests();
            }
        }

        private void Spawn(
            CharacterTestCharacterEntity entity,
            CharacterActionVfxRequest request,
            RuntimeFrame frame,
            int entityIndex,
            int requestIndex)
        {
            LastFrameRequestCount++;
            var definitionId = new VfxDefinitionId(request.VfxDefinitionId);
            int residueMilliseconds = request.VfxConfig.Parameter3;
            if (request.VfxConfig.UseWeaponBladeTrailStyle)
            {
                if (!entity.Equipment.UseDefaultLoadout
                    || !CharacterWeaponBladeTrailStyleResolver.TryResolve(CharacterTestGeneratedPackageSource.LoadPackageForEntity(entity),
                        request.VfxConfig.Parameter0, request.VfxConfig.Parameter1, out string style, out residueMilliseconds))
                    return;
                if (!_bladeTrailDefinitions.TryGetValue(style, out definitionId))
                {
                    LastFrameFailureCount++;
                    _logger.Warning(LogCategory, "Weapon blade trail style has no formal VFX definition: " + style);
                    return;
                }
                var identity = (entity.Id.Value, request.Metadata.InstanceId, request.Metadata.StableEventId);
                if (!_bladeTrailStartsThisFrame.Add(identity)) return;
                _activeInstances.Clear();
                _vfx.CopyActiveInstances(_activeInstances);
                bool exists = false;
                foreach (VfxInstanceSnapshot active in _activeInstances)
                    exists |= active.SourceDomain == VfxSourceDomains.ActionPresentation && active.SourceId == identity.Item1
                        && active.SourceFrame == identity.Item2 && active.TriggerId == identity.Item3;
                _activeInstances.Clear();
                if (exists) return;
            }
            if (!definitionId.IsValid || !_vfx.Definitions.TryGet(definitionId, out _))
            {
                LastFrameFailureCount++;
                _logger.Warning(
                    LogCategory,
                    "Action VFX request skipped: resource key is not mapped to a VFX definition. resourceKey="
                    + (request.ResourceKey ?? string.Empty)
                    + " action="
                    + request.Metadata.ActionId);
                return;
            }

            int stableEventId = request.Metadata.StableEventId;
            if (stableEventId <= 0) throw new InvalidOperationException("Action VFX requires a prepared stable event identity.");
            int triggerId = stableEventId;
            int sourceOrder = Math.Max(0, entityIndex) * 1000 + Math.Max(0, requestIndex);
            VfxSpawnRequest spawned = _vfx.EnqueueSpawnRequest(new VfxSpawnRequest(
                frame,
                VfxSourceDomains.ActionPresentation,
                request.Metadata.HasLifecycle ? request.Metadata.InstanceId : frame.Value,
                request.Metadata.LocalFrame,
                sourceOrder,
                triggerId,
                entity.Id.Value,
                targetId: 0,
                definitionId,
                CreateAnchor(entity, request),
                new VfxParameterBlock(
                    request.VfxConfig.Parameter0,
                    request.VfxConfig.Parameter1,
                    request.VfxConfig.Parameter2,
                    residueMilliseconds),
                ResolveLifetimeFrames(request),
                request.Metadata.TraceId,
                expireStopMode: ResolveStopMode(request.VfxConfig.StopPolicy),
                playbackRatePermille: ToPlaybackRatePermille(request.VfxConfig.PlaybackRate),
                sourceStartOffsetFrames: request.VfxConfig.StartOffsetFrames,
                sourceEndOffsetFrames: request.VfxConfig.EndOffsetFrames));

            if (spawned.DefinitionId.IsValid)
                LastFrameSpawnedCount++;
        }

        private void Stop(
            CharacterTestCharacterEntity entity,
            CharacterActionVfxRequest request,
            RuntimeFrame frame)
        {
            LastFrameRequestCount++;
            int triggerId = request.Metadata.StableEventId;
            if (triggerId <= 0) throw new InvalidOperationException("Action VFX requires a prepared stable event identity.");
            _activeInstances.Clear();
            _vfx.CopyActiveInstances(_activeInstances);
            for (int i = 0; i < _activeInstances.Count; i++)
            {
                VfxInstanceSnapshot instance = _activeInstances[i];
                if (instance.SourceDomain != VfxSourceDomains.ActionPresentation
                    || instance.SourceId != entity.Id.Value
                    || instance.SourceFrame != request.Metadata.InstanceId
                    || instance.TriggerId != triggerId)
                {
                    continue;
                }

                _vfx.EnqueueStopRequest(new VfxStopRequest(
                    frame,
                    instance.InstanceId,
                    ResolveStopMode(request.VfxConfig.StopPolicy),
                    "character_action.lifecycle_exit",
                    request.Metadata.TraceId));
            }
            _activeInstances.Clear();
        }

        private static bool HasForcedExitLater(
            IReadOnlyList<CharacterActionVfxRequest> requests,
            int enterIndex,
            CharacterActionVfxRequest enter)
        {
            for (int i = enterIndex + 1; i < requests.Count; i++)
            {
                CharacterActionVfxRequest candidate = requests[i];
                if (candidate.Metadata.LifecyclePhase == CharacterActionTrackLifecyclePhase.Exit
                    && candidate.Metadata.LifecycleBoundary == CharacterActionTrackLifecycleBoundary.BeforeFrame
                    && candidate.Metadata.InstanceId == enter.Metadata.InstanceId
                    && (candidate.Metadata.StableEventId == enter.Metadata.StableEventId))
                {
                    return true;
                }
            }
            return false;
        }

        private VfxAnchor CreateAnchor(CharacterTestCharacterEntity entity, CharacterActionVfxRequest request)
        {
            VfxPose pose;
            if ((request.VfxDefinitionId == CharacterTestVfxIds.ChannelBeamBeamDefinitionId)
                && _channelBeamAimDirectionProvider != null)
            {
                FixVector3? direction = _channelBeamAimDirectionProvider(
                    entity.Id.Value,
                    request.Metadata.InstanceId);
                if (direction.HasValue)
                {
                    pose = CreateCasterPose(entity, request, direction.Value);
                    return CreateConfiguredAnchor(entity, request, pose);
                }
            }

            pose = CreateCasterPose(entity, request, null);
            return CreateConfiguredAnchor(entity, request, pose);
        }

        private static VfxPose CreateCasterPose(
            CharacterTestCharacterEntity entity,
            CharacterActionVfxRequest request,
            FixVector3? overrideDirection)
        {
            CharacterTestCharacterTransformComponent transform = entity.Transform;
            float forwardX;
            float forwardZ;
            if (overrideDirection.HasValue)
            {
                forwardX = ToFloat(overrideDirection.Value.X);
                forwardZ = ToFloat(overrideDirection.Value.Z);
            }
            else
            {
                double yawRadians = transform.YawDegrees * Math.PI / 180.0;
                forwardX = (float)Math.Sin(yawRadians);
                forwardZ = (float)Math.Cos(yawRadians);
            }

            if (forwardX * forwardX + forwardZ * forwardZ <= 0.000001f)
            {
                forwardX = 0f;
                forwardZ = 1f;
            }

            CharacterActionVfxEventConfig config = request.VfxConfig;
            MxAttachmentTransform attachment = config.Attachment;
            MxTransformOffset offset = attachment.Transform;
            float baseForwardOffset = (request.VfxDefinitionId == CharacterTestVfxIds.GuardAuraDefinitionId)
                ? 0f
                : CharacterTestActionVfxAnchor.ForwardOffsetMeters;
            float baseHeightOffset = baseForwardOffset > 0f
                ? CharacterTestActionVfxAnchor.HeightOffsetMeters
                : 0f;
            float rightX = forwardZ;
            float rightZ = -forwardX;
            float offsetX = attachment.OffsetSpace == MxTransformSpace.Local
                ? rightX * offset.PositionX + forwardX * (offset.PositionZ + baseForwardOffset)
                : offset.PositionX + forwardX * baseForwardOffset;
            float offsetZ = attachment.OffsetSpace == MxTransformSpace.Local
                ? rightZ * offset.PositionX + forwardZ * (offset.PositionZ + baseForwardOffset)
                : offset.PositionZ + forwardZ * baseForwardOffset;

            return new VfxPose(
                ToVfxVector(
                    transform.X + offsetX,
                    transform.Y + baseHeightOffset + offset.PositionY,
                    transform.Z + offsetZ),
                ToVfxVector(forwardX, 0f, forwardZ),
                ToVfxVector(offset.RotationEulerX, offset.RotationEulerY, offset.RotationEulerZ),
                ToVfxVector(
                    offset.ScaleX * config.ScaleMultiplier,
                    offset.ScaleY * config.ScaleMultiplier,
                    offset.ScaleZ * config.ScaleMultiplier));
        }

        private static VfxAnchor CreateConfiguredAnchor(
            CharacterTestCharacterEntity entity,
            CharacterActionVfxRequest request,
            VfxPose worldPose)
        {
            CharacterActionVfxEventConfig config = request.VfxConfig;
            MxAttachmentTransform attachment = config.Attachment;
            if (attachment.Mode == MxAttachmentMode.FixedWorld)
                return VfxAnchor.World(worldPose);

            MxTransformOffset offset = attachment.Transform;
            int socketId = attachment.Mode == MxAttachmentMode.FollowSocket
                ? request.NumericSocketId
                : 0;
            VfxPose relativePose = new VfxPose(
                ToVfxVector(offset.PositionX, offset.PositionY, offset.PositionZ),
                VfxVector3.Forward,
                ToVfxVector(offset.RotationEulerX, offset.RotationEulerY, offset.RotationEulerZ),
                ToVfxVector(
                    offset.ScaleX * config.ScaleMultiplier,
                    offset.ScaleY * config.ScaleMultiplier,
                    offset.ScaleZ * config.ScaleMultiplier));
            VfxAnchorKind kind = attachment.OffsetSpace == MxTransformSpace.Local
                ? VfxAnchorKind.Attached
                : VfxAnchorKind.SourceEntity;
            return new VfxAnchor(kind, entity.Id.Value, 0, socketId, relativePose);
        }

        private static int ResolveLifetimeFrames(CharacterActionVfxRequest request)
        {
            return request.EffectiveLifetimeFrames;
        }

        private static int ToPlaybackRatePermille(float playbackRate)
        {
            return Math.Max(1, (int)Math.Round(playbackRate * 1000f));
        }

        private static VfxStopMode ResolveStopMode(CharacterActionVfxStopPolicy policy)
        {
            switch (policy)
            {
                case CharacterActionVfxStopPolicy.Dissipate:
                    return VfxStopMode.Dissipate;
                case CharacterActionVfxStopPolicy.Immediate:
                    return VfxStopMode.Immediate;
                default:
                    return VfxStopMode.Complete;
            }
        }

        private static VfxVector3 ToVfxVector(float x, float y, float z)
        {
            return VfxVector3.FromRaw(ToRaw(x), ToRaw(y), ToRaw(z));
        }

        private static long ToRaw(float value)
        {
            return (long)Math.Round(value * VfxVector3.Scale);
        }

        private static float ToFloat(Fix64 value)
        {
            return (float)value.RawValue / Fix64.Scale;
        }


    }
}
