using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Audio.Diagnostics;
using MxFramework.Audio.Physical;
using MxFramework.CharacterAction;
using MxFramework.Combat.Projectiles;
using MxFramework.Diagnostics;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.Story;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// CharacterTest slice session summary for Debug UI (frame clock, pause gate, resources bootstrap).
    /// </summary>
    public sealed class CharacterTestSliceDebugSource : IFrameworkDebugSource
    {
        private readonly CharacterTestSliceServices _services;
        private readonly Func<bool> _isPausedProvider;

        internal CharacterTestSliceDebugSource(
            CharacterTestSliceServices services,
            Func<bool> isPausedProvider = null,
            string name = "CharacterTest")
        {
            _services = services;
            _isPausedProvider = isPausedProvider;
            Name = string.IsNullOrWhiteSpace(name) ? "CharacterTest" : name;
        }

        internal CharacterTestSliceDebugSource(
            GameSlice slice,
            Func<bool> isPausedProvider = null,
            string name = "CharacterTest")
            : this(slice?.Services, isPausedProvider, name)
        {
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => _services != null;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            if (!IsAvailable)
            {
                return new FrameworkDebugSnapshot(
                    Name,
                    Mode,
                    new[] { new FrameworkDebugSection("Status", "unavailable") });
            }

            RuntimeHostDiagnostics host = _services.CaptureDiagnostics();
            ResourceDebugSnapshot resources = TryCreateResourceSnapshot();

            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection("Summary", CreateSummary(host, resources)),
                    new FrameworkDebugSection("Runtime Inspector", CreateRuntimeInspectorSection(host)),
                    new FrameworkDebugSection("Story Session", CreateStorySessionSection()),
                    new FrameworkDebugSection("Runtime Commands", CreateRuntimeCommandsSection()),
                    new FrameworkDebugSection("Network Session", CreateNetworkLoopbackSection()),
                    new FrameworkDebugSection("Runtime Event Log", CreateRuntimeEventLogSection()),
                    new FrameworkDebugSection("Scene Flow", CreateSceneFlowSection()),
                    new FrameworkDebugSection("Characters", CreateCharactersSection()),
                    new FrameworkDebugSection("Movable Natural Objects", CreateMovableNaturalObjectsSection()),
                    new FrameworkDebugSection("Genesis Movable Objects", CreateGenesisMovableObjectsSection()),
                    new FrameworkDebugSection("Genesis Potency Mechanics", CreateGenesisPotencyMechanicsSection()),
                    new FrameworkDebugSection("Interactions", CreateInteractionsSection()),
                    new FrameworkDebugSection("Audio Runtime", CreateAudioRuntimeSection()),
                    new FrameworkDebugSection("Projectiles", CreateProjectilesSection()),
                    new FrameworkDebugSection("Action Config Source", CreateActionConfigSourceSection()),
                    new FrameworkDebugSection("Resources", CreateResourcesSection(resources))
                });
        }

        private string CreateSummary(RuntimeHostDiagnostics host, ResourceDebugSnapshot resources)
        {
            bool paused = _isPausedProvider != null && _isPausedProvider();
            var builder = new StringBuilder();
            builder.Append("frame=").Append(_services.CurrentFrame.Value).Append('\n');
            builder.Append("elapsedSeconds=").Append(_services.ElapsedSeconds.ToString("F3")).Append('\n');
            builder.Append("paused=").Append(paused ? "true" : "false").Append('\n');
            builder.Append("hostState=").Append(host.State).Append('\n');
            builder.Append("tickCount=").Append(host.TickCount).Append('\n');
            builder.Append("moduleCount=").Append(host.Modules.Count).Append('\n');
            builder.Append("hostErrors=").Append(host.Errors.Count);
            CharacterTestCharacterActionRuntime actionRuntime = _services.ActionRuntime;
            builder.Append('\n');
            builder.Append("actionConfigSource=").Append(actionRuntime != null ? actionRuntime.ConfigSourceKind.ToString() : "missing");
            builder.Append('\n');
            builder.Append("actionConfigReady=").Append(actionRuntime != null && actionRuntime.IsReady ? "true" : "false");
            builder.Append('\n');
            builder.Append("actionConfigLastReload=").Append(FormatActionReload(_services.LastActionReloadResult));
            builder.Append('\n');
            builder.Append("sceneFlow=").Append(_services.HasSceneFlow ? "available" : "unavailable");
            if (resources != null)
            {
                builder.Append('\n');
                builder.Append("resourceMode=").Append(_services.Resources.Mode);
                builder.Append('\n');
                builder.Append("catalogs=").Append(resources.CatalogCount);
                builder.Append('\n');
                builder.Append("loadedEntries=").Append(resources.LoadedCount);
            }

            return builder.ToString();
        }

        private string CreateSceneFlowSection()
        {
            SceneFlowSnapshot snapshot = _services.CaptureSceneFlowSnapshot();
            if (snapshot == null)
                return "unavailable";

            return "activeScene=" + EmptyAsNone(snapshot.ActiveSceneKey)
                + "\nbusy=" + (snapshot.IsBusy ? "true" : "false")
                + "\noperation=" + snapshot.CurrentOperationType
                + "\noperationScene=" + EmptyAsNone(snapshot.CurrentSceneKey)
                + "\nprogress=" + snapshot.Progress.ToString("F2")
                + "\nlastStatus=" + snapshot.LastResult.Status
                + "\nlastOperation=" + snapshot.LastResult.OperationType
                + "\nlastScene=" + EmptyAsNone(snapshot.LastResult.SceneKey)
                + "\nlastError=" + (snapshot.LastResult.Error.IsNone
                    ? "none"
                    : snapshot.LastResult.Error.Code + ": " + snapshot.LastResult.Error.Message);
        }

        private string CreateActionConfigSourceSection()
        {
            CharacterTestCharacterActionRuntime runtime = _services.ActionRuntime;
            if (runtime == null)
                return "missing";

            var builder = new StringBuilder();
            builder.Append("sourceKind=").Append(runtime.ConfigSourceKind).Append('\n');
            builder.Append("description=").Append(runtime.ConfigSourceDescription).Append('\n');
            builder.Append("isReady=").Append(runtime.IsReady ? "true" : "false").Append('\n');
            builder.Append("actionSetsPath=").Append(runtime.ActionSetsSourcePath).Append('\n');
            builder.Append("actionsPath=").Append(runtime.ActionsSourcePath).Append('\n');
            builder.Append("lastReload=").Append(FormatActionReload(_services.LastActionReloadResult)).Append('\n');
            builder.Append("sourceDiagnostics=").Append(runtime.ConfigSourceDiagnostics.Count).Append('\n');
            for (int i = 0; i < runtime.ConfigSourceDiagnostics.Count; i++)
                builder.Append("source.").Append(i).Append('=').Append(runtime.ConfigSourceDiagnostics[i]).Append('\n');

            builder.Append("importDiagnostics=").Append(runtime.ImportResult.Diagnostics.Length).Append('\n');
            for (int i = 0; i < runtime.ImportResult.Diagnostics.Length; i++)
                builder.Append("import.").Append(i).Append('=').Append(runtime.ImportResult.Diagnostics[i]).Append('\n');

            builder.Append("validationDiagnostics=").Append(runtime.ValidationReport.Diagnostics.Length);
            for (int i = 0; i < runtime.ValidationReport.Diagnostics.Length; i++)
                builder.Append('\n').Append("validation.").Append(i).Append('=').Append(runtime.ValidationReport.Diagnostics[i]);

            return builder.ToString();
        }

        private string CreateRuntimeInspectorSection(RuntimeHostDiagnostics host)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = _services.CharacterEntities;
            int entityCount = entities != null ? entities.Count : 0;
            int localPlayers = 0;
            int scripted = 0;
            int aiControlled = 0;
            int uncontrolled = 0;
            CharacterTestCharacterEntity localPlayer = null;

            if (entities != null)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    CharacterTestCharacterEntity entity = entities[i];
                    switch (entity.Control.Owner)
                    {
                        case CharacterTestCharacterControlOwner.LocalPlayer:
                            localPlayers++;
                            if (localPlayer == null)
                                localPlayer = entity;
                            break;
                        case CharacterTestCharacterControlOwner.Scripted:
                            scripted++;
                            break;
                        case CharacterTestCharacterControlOwner.AiControlled:
                            aiControlled++;
                            break;
                        default:
                            uncontrolled++;
                            break;
                    }
                }
            }

            var builder = new StringBuilder();
            builder.Append("world.entities=").Append(entityCount).Append('\n');
            builder.Append("world.localPlayers=").Append(localPlayers).Append('\n');
            builder.Append("world.scripted=").Append(scripted).Append('\n');
            builder.Append("world.aiControlled=").Append(aiControlled).Append('\n');
            builder.Append("world.uncontrolled=").Append(uncontrolled).Append('\n');
            builder.Append("camera.mode=").Append(_services.CameraMode).Append('\n');
            builder.Append("camera.targetLock=")
                .Append(_services.TargetLockEnabled ? "true" : "false")
                .Append(" entity=")
                .Append(_services.LockedTargetEntityId)
                .Append(" hurtbox=")
                .Append(_services.LockedCombatTarget.HasTarget
                    ? _services.LockedCombatTarget.LogicalHurtboxId
                    : "none")
                .Append('\n');
            CharacterTestCharacterMovementProfileRuntime movement = _services.MovementProfile;
            builder.Append("movement.profile=")
                .Append(EmptyAsNone(movement.StableId))
                .Append(" fallback=")
                .Append(movement.UsedFallback ? "true" : "false")
                .Append(" source=")
                .Append(EmptyAsNone(movement.SourceDescription))
                .Append('\n');
            CharacterTestCharacterPhysicsRuntime physics = _services.PhysicsRuntime;
            builder.Append("physics.source=")
                .Append(physics != null ? EmptyAsNone(physics.SourceDescription) : "planar")
                .Append(" staticColliders=")
                .Append(physics != null ? physics.StaticColliderCount : 0)
                .Append(" staticMask=")
                .Append(physics != null ? physics.StaticObstacleLayerMask.Value : 0)
                .Append(" motionMask=")
                .Append(physics != null ? physics.MotionObstacleLayerMask.Value : 0)
                .Append('\n');
            builder.Append("movableNaturalObjects.count=")
                .Append(_services.MovableNaturalObjects != null ? _services.MovableNaturalObjects.Count : 0)
                .Append('\n');
            AppendLocalPlayerInput(builder, localPlayer);
            AppendRuntimeModules(builder, host);
            return builder.ToString();
        }

        private string CreateStorySessionSection()
        {
            StoryDirectorSnapshot story = _services.StorySnapshot;
            return "graphCount=" + story.Graphs.Count
                + "\nactiveBeats=" + story.ActiveBeatInstances.Count
                + "\nblackboardFacts=" + story.Facts.Count
                + "\nrecentEvents=" + _services.StoryModule.RecentEvents.Count
                + "\npendingCommandErrors=" + _services.StoryModule.LastCommandErrors.Count;
        }

        private string CreateRuntimeCommandsSection()
        {
            RuntimeEventQueueSnapshot queue = _services.RuntimeEventQueueSnapshot;
            var builder = new StringBuilder();
            builder.Append("registeredCommands=").Append(_services.RuntimeCommands.CommandRegistry.Count).Append('\n');
            builder.Append("pendingCommands=").Append(_services.PendingRuntimeCommandCount).Append('\n');
            builder.Append("lastDrainedCommands=").Append(_services.RuntimeCommands.LastDrainedCommands.Count).Append('\n');
            builder.Append("lastCommandErrors=").Append(_services.RuntimeCommands.LastCommandErrors.Count).Append('\n');
            builder.Append("eventQueuePending=").Append(queue.PendingCount).Append('\n');
            builder.Append("cameraMode=").Append(_services.CameraMode).Append('\n');
            builder.Append("targetLock=")
                .Append(_services.TargetLockEnabled ? "true" : "false")
                .Append(" entity=")
                .Append(_services.LockedTargetEntityId)
                .Append(" hurtbox=")
                .Append(_services.LockedCombatTarget.HasTarget
                    ? _services.LockedCombatTarget.LogicalHurtboxId
                    : "none");

            return builder.ToString();
        }

        private string CreateNetworkLoopbackSection()
        {
            CharacterTestNetworkLoopbackDiagnostics diagnostics = _services.CaptureNetworkLoopbackDiagnostics();
            if (!diagnostics.Available)
                return "disabled";

            var builder = new StringBuilder();
            builder.Append("transportMode=").Append(diagnostics.TransportMode).Append('\n');
            builder.Append("transportId=").Append(diagnostics.TransportId).Append('\n');
            builder.Append("roomStatus=").Append(diagnostics.RoomStatus).Append('\n');
            builder.Append("roomId=").Append(diagnostics.RoomId).Append('\n');
            builder.Append("roomReady=").Append(diagnostics.RoomReadyCount)
                .Append('/').Append(diagnostics.RoomConnectedCount).Append('\n');
            builder.Append("roomVersion=").Append(diagnostics.RoomStateVersion).Append('\n');
            builder.Append("roomMembers=").Append(diagnostics.RoomMembers).Append('\n');
            if (!string.IsNullOrEmpty(diagnostics.RoomReason))
                builder.Append("roomReason=").Append(diagnostics.RoomReason).Append('\n');
            builder.Append("localPeer=").Append(diagnostics.LocalPeerId).Append('\n');
            builder.Append("ownedEntity=").Append(diagnostics.OwnedEntityId).Append('\n');
            builder.Append("connectedPeers=").Append(diagnostics.ConnectedPeerCount).Append('\n');
            builder.Append("ownership=").Append(diagnostics.OwnershipSummary).Append('\n');
            builder.Append("serverStatus=").Append(diagnostics.ServerStatus).Append('\n');
            builder.Append("clientStatus=").Append(diagnostics.ClientStatus).Append('\n');
            builder.Append("serverFrame=").Append(diagnostics.ServerFrame.Value).Append('\n');
            builder.Append("clientFrame=").Append(diagnostics.ClientLastServerFrame.Value).Append('\n');
            builder.Append("rttEstimateFrames=").Append(diagnostics.RttEstimateFrames).Append('\n');
            builder.Append("avgRttFrames=").Append(diagnostics.AverageRttFrames).Append('\n');
            builder.Append("maxRttFrames=").Append(diagnostics.MaxRttFrames).Append('\n');
            builder.Append("predictionLeadFrames=").Append(diagnostics.PredictionLeadFrames).Append('\n');
            builder.Append("predictionHistory=").Append(diagnostics.PredictionHistoryCount).Append('\n');
            builder.Append("snapshotIntervalAvg=").Append(diagnostics.AverageSnapshotIntervalFrames).Append('\n');
            builder.Append("snapshotIntervalMax=").Append(diagnostics.MaxSnapshotIntervalFrames).Append('\n');
            builder.Append("frameDrift=").Append(diagnostics.FrameDriftFrames).Append('\n');
            builder.Append("frameDriftAvg=").Append(diagnostics.AverageFrameDriftFrames).Append('\n');
            builder.Append("frameDriftMaxAbs=").Append(diagnostics.MaxFrameDriftFrames).Append('\n');
            builder.Append("pendingCommands=").Append(diagnostics.PendingRuntimeCommands).Append('\n');
            builder.Append("pendingMessages=").Append(diagnostics.PendingTransportMessages).Append('\n');
            builder.Append("ackedInputFrame=").Append(diagnostics.LastAckedInputFrame.Value).Append('\n');
            builder.Append("lastSnapshotHash=").Append(diagnostics.LastSnapshotHash).Append('\n');
            builder.Append("resyncCount=").Append(diagnostics.ResyncCount).Append('\n');
            builder.Append("snapshotApplyFailures=").Append(diagnostics.SnapshotApplyFailureCount).Append('\n');
            builder.Append("snapshotMismatches=").Append(diagnostics.SnapshotMismatchCount).Append('\n');
            builder.Append("suppressedSnapshotMismatches=").Append(diagnostics.SuppressedSnapshotMismatchCount).Append('\n');
            builder.Append("snapshotHashComparable=").Append(diagnostics.ComparableSnapshotHashCount).Append('\n');
            builder.Append("snapshotHashMissingHistory=").Append(diagnostics.MissingSnapshotHashCount).Append('\n');
            builder.Append("snapshotRequests=").Append(diagnostics.SnapshotRequestCount).Append('\n');
            builder.Append("authCommandsSent=").Append(diagnostics.AuthoritativeCommandSentCount).Append('\n');
            builder.Append("authCommandsReceived=").Append(diagnostics.AuthoritativeCommandReceivedCount).Append('\n');
            builder.Append("authCommandsEcho=").Append(diagnostics.AuthoritativeCommandEchoCount).Append('\n');
            builder.Append("authCommandsEnqueued=").Append(diagnostics.AuthoritativeCommandEnqueuedCount).Append('\n');
            builder.Append("authCommandsLate=").Append(diagnostics.AuthoritativeCommandLateCount).Append('\n');
            builder.Append("authCommandsDropped=").Append(diagnostics.AuthoritativeCommandDroppedCount).Append('\n');
            builder.Append("fullSnapshotsSent=").Append(diagnostics.FullSnapshotSentCount).Append('\n');
            builder.Append("digestSnapshotsSent=").Append(diagnostics.DigestSnapshotSentCount).Append('\n');
            builder.Append("fullSnapshotsReceived=").Append(diagnostics.FullSnapshotReceivedCount).Append('\n');
            builder.Append("digestSnapshotsReceived=").Append(diagnostics.DigestSnapshotReceivedCount).Append('\n');
            builder.Append("sentBytes=").Append(diagnostics.SentBytes).Append('\n');
            builder.Append("deliveredBytes=").Append(diagnostics.DeliveredBytes).Append('\n');
            builder.Append("lastPayloadBytes=").Append(diagnostics.LastSentPayloadBytes).Append('\n');
            builder.Append("maxPayloadBytes=").Append(diagnostics.MaxSentPayloadBytes).Append('\n');
            builder.Append("commandBytes=").Append(diagnostics.RuntimeCommandSentBytes).Append('\n');
            builder.Append("snapshotBytes=").Append(diagnostics.SnapshotSentBytes).Append('\n');
            builder.Append("snapshotRequestBytes=").Append(diagnostics.SnapshotRequestSentBytes).Append('\n');
            builder.Append("heartbeatBytes=").Append(diagnostics.HeartbeatSentBytes).Append('\n');
            builder.Append("roomBytes=").Append(diagnostics.RoomSentBytes).Append('\n');
            builder.Append("duplicateMessages=").Append(diagnostics.DuplicateMessageCount).Append('\n');
            builder.Append("outOfOrderMessages=").Append(diagnostics.OutOfOrderMessageCount).Append('\n');
            builder.Append("codecErrors=").Append(diagnostics.CodecErrorCount).Append('\n');
            builder.Append("disconnectReason=").Append(diagnostics.DisconnectReason);
            return builder.ToString();
        }

        private string CreateRuntimeEventLogSection()
        {
            IReadOnlyList<CharacterTestRuntimeEvent> events = _services.RecentRuntimeEvents;
            if (events == null || events.Count == 0)
                return "none";

            var builder = new StringBuilder();
            int start = Math.Max(0, events.Count - 16);
            builder.Append("showing=").Append(events.Count - start).Append('/').Append(events.Count);
            for (int i = start; i < events.Count; i++)
                builder.Append('\n').Append(events[i].ToString());

            return builder.ToString();
        }

        private string CreateCharactersSection()
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = _services.CharacterEntities;
            if (entities == null || entities.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                builder.Append("entity=")
                    .Append(entity.Id.Value)
                    .Append(" character=")
                    .Append(entity.Identity.CharacterId)
                    .Append(" team=")
                    .Append(entity.Team.TeamId)
                    .Append(" owner=")
                    .Append(entity.Control.Owner)
                    .Append(" pos=(")
                    .Append(entity.Transform.X.ToString("F2"))
                    .Append(',')
                    .Append(entity.Transform.Y.ToString("F2"))
                    .Append(',')
                    .Append(entity.Transform.Z.ToString("F2"))
                    .Append(") yaw=")
                    .Append(entity.Transform.YawDegrees.ToString("F1"))
                    .Append(" input=(")
                    .Append(entity.Input.Move.x.ToString("F2"))
                    .Append(',')
                    .Append(entity.Input.Move.y.ToString("F2"))
                    .Append(") grounded=")
                    .Append(entity.IsGrounded ? "true" : "false")
                    .Append(" mode=")
                    .Append(entity.MovementMode)
                    .Append(" locked=")
                    .Append(entity.MovementLocked ? "true" : "false")
                    .Append(" controlScale=")
                    .Append(entity.MovementControlScale.ToString("F2"))
                    .Append('/')
                    .Append(entity.RotationControlScale.ToString("F2"))
                    .Append(" hp=")
                    .Append(entity.CombatState.Health)
                    .Append('/')
                    .Append(entity.CombatState.MaxHealth)
                    .Append(" scatterPressure=")
                    .Append(entity.CombatState.ScatterPressure)
                    .Append('/')
                    .Append(entity.CombatState.MaxScatterPressure)
                    .Append('(')
                    .Append(entity.CombatState.ScatterPressureBand)
                    .Append(')')
                    .Append(" stagger=")
                    .Append(entity.CombatState.HitStunFramesRemaining)
                    .Append(" lastHit=(")
                    .Append("frame=")
                    .Append(entity.CombatState.LastHitFrame)
                    .Append(" dmg=")
                    .Append(entity.CombatState.LastDamage)
                    .Append(" src=")
                    .Append(entity.CombatState.LastHitSourceEntityId)
                    .Append(" trace=")
                    .Append(entity.CombatState.LastHitTraceId)
                    .Append(')')
                    .Append(" profile=")
                    .Append(EmptyAsNone(entity.MovementProfileId))
                    .Append(" physics=")
                    .Append(entity.PhysicsEnabled ? entity.CollisionFlags.ToString() : "planar")
                    .Append(" surface=")
                    .Append(entity.SurfaceMaterialStableId)
                    .Append(" surfaceKind=")
                    .Append(entity.SurfaceMaterialKind)
                    .Append(" surfaceFriction=")
                    .Append(entity.SurfaceFriction.ToString("F2"))
                    .Append(" surfaceElasticity=")
                    .Append(entity.SurfaceElasticity.ToString("F2"))
                    .Append(" surfaceFixedBounceHeight=")
                    .Append(entity.SurfaceFixedBounceHeight.ToString("F2"))
                    .Append(" livingNaturalAttributes=(")
                    .Append(entity.LivingNaturalAttributes.LifecycleState)
                    .Append(" op=")
                    .Append(entity.LivingNaturalAttributes.LastOperation)
                    .Append(" scatter=")
                    .Append(entity.LivingNaturalAttributes.Scatter)
                    .Append(" cohesion=")
                    .Append(entity.LivingNaturalAttributes.SelfCohesion)
                    .Append('/')
                    .Append(entity.LivingNaturalAttributes.CohesionLimit)
                    .Append(" material=")
                    .Append(entity.LivingNaturalAttributes.MaterialReading)
                    .Append(" returnedToMaterial=")
                    .Append(entity.LivingNaturalAttributes.ReturnedToMaterial ? "true" : "false")
                    .Append(')')
                    .Append(" vel=(")
                    .Append(entity.HorizontalVelocity.x.ToString("F2"))
                    .Append(',')
                    .Append(entity.HorizontalVelocity.y.ToString("F2"))
                    .Append(") desired=(")
                    .Append(entity.DesiredVelocity.x.ToString("F2"))
                    .Append(',')
                    .Append(entity.DesiredVelocity.y.ToString("F2"))
                    .Append(") intentBuffer=")
                    .Append(entity.PendingGameplayIntentCount)
                    .Append(" jumpIntent=")
                    .Append(CharacterTestDeterministicUnits.FramesToSeconds(
                        entity.GameplayIntentBuffer.GetRemainingFrames(
                            CharacterTestGameplayIntentKind.Jump,
                            _services.CurrentFrame.Value)).ToString("F2"))
                    .Append(" coyote=")
                    .Append(entity.CoyoteTimeSecondsRemaining.ToString("F2"))
                    .Append(" vy=")
                    .Append(entity.VerticalVelocity.ToString("F2"))
                    .Append(" anim=(")
                    .Append(entity.Animation.LocomotionBlend.x.ToString("F2"))
                    .Append(',')
                    .Append(entity.Animation.LocomotionBlend.y.ToString("F2"))
                    .Append(" speed=")
                    .Append(entity.Animation.LocomotionSpeed01.ToString("F2"))
                    .Append(" airborne=")
                    .Append(entity.Animation.Airborne ? "true" : "false")
                    .Append(" actionKey=")
                    .Append(EmptyAsNone(entity.Animation.ActionKey))
                    .Append(") action=(")
                    .Append(EmptyAsNone(entity.Action.ActiveActionId))
                    .Append(" state=")
                    .Append(CharacterTestCharacterActionComponent.ResolveStateText(entity.Action.State))
                    .Append(" phase=")
                    .Append(CharacterTestCharacterActionComponent.ResolvePhaseText(entity.Action.Phase))
                    .Append(" localFrame=")
                    .Append(entity.Action.LocalFrame)
                    .Append(" reject=")
                    .Append(EmptyAsNone(entity.Action.LastRejectReason))
                    .Append(')');
                AppendMotionDiagnostic(builder, entity.Id.Value);
                AppendLineIfNeeded(builder, i, entities.Count);
            }

            return builder.ToString();
        }

        private string CreateProjectilesSection()
        {
            CombatProjectileDebugSnapshot snapshot = _services.CombatTraceSystem?.ProjectileDebugSnapshot;
            if (snapshot == null)
                return "unavailable";

            var builder = new StringBuilder();
            builder.Append("module=").Append(snapshot.ModuleId).Append('\n');
            builder.Append("definitionHash=").Append(snapshot.DefinitionHash).Append('\n');
            builder.Append("active=").Append(snapshot.ActiveCount).Append('\n');
            builder.Append("flightPresenter.active=")
                .Append(_services.ProjectilePresenter != null ? _services.ProjectilePresenter.ActiveCount : 0)
                .Append('\n');
            builder.Append("last.spawns=").Append(snapshot.LastFrameEvents.Spawns.Count).Append('\n');
            builder.Append("last.impacts=").Append(snapshot.LastFrameEvents.Impacts.Count).Append('\n');
            builder.Append("last.expirations=").Append(snapshot.LastFrameEvents.Expirations.Count).Append('\n');
            builder.Append("last.rejections=").Append(snapshot.LastFrameEvents.Rejections.Count);
            IReadOnlyList<CombatProjectileDebugEntry> entries = snapshot.ActiveEntries;
            for (int i = 0; i < entries.Count; i++)
            {
                CombatProjectileDebugEntry entry = entries[i];
                CombatProjectileSnapshot projectile = entry.Projectile;
                builder.Append('\n')
                    .Append("projectile.")
                    .Append(i)
                    .Append(" id=")
                    .Append(projectile.ProjectileId)
                    .Append(" stable=")
                    .Append(EmptyAsNone(projectile.DefinitionStableId))
                    .Append(" source=")
                    .Append(projectile.SourceEntityId.Value)
                    .Append(" pos=(")
                    .Append(FormatFix(projectile.Position.X))
                    .Append(',')
                    .Append(FormatFix(projectile.Position.Y))
                    .Append(',')
                    .Append(FormatFix(projectile.Position.Z))
                    .Append(") dir=(")
                    .Append(FormatFix(projectile.Direction.X))
                    .Append(',')
                    .Append(FormatFix(projectile.Direction.Y))
                    .Append(',')
                    .Append(FormatFix(projectile.Direction.Z))
                    .Append(") age=")
                    .Append(projectile.AgeFrames)
                    .Append(" life=")
                    .Append(projectile.EffectiveLifetimeFrames > 0
                        ? projectile.EffectiveLifetimeFrames.ToString()
                        : "definition")
                    .Append(" remaining=")
                    .Append(entry.RemainingFrames)
                    .Append(" acceptedHits=")
                    .Append(entry.AcceptedImpactCount)
                    .Append(" remainingPierce=")
                    .Append(entry.RemainingAcceptedImpacts);
                if (projectile.HasTargetPoint)
                {
                    builder.Append(" target=(")
                        .Append(FormatFix(projectile.TargetPoint.X))
                        .Append(',')
                        .Append(FormatFix(projectile.TargetPoint.Y))
                        .Append(',')
                        .Append(FormatFix(projectile.TargetPoint.Z))
                        .Append(')');
                }

                if (projectile.HasTrackingTarget)
                {
                    builder.Append(" trackingTarget=")
                        .Append(projectile.TrackingTargetEntityId.Value);
                }
            }

            return builder.ToString();
        }

        private string CreateGenesisMovableObjectsSection()
        {
            string summary = _services.LastGenesisMovableObjectSummary;
            if (string.IsNullOrWhiteSpace(summary))
                return "stateModel=harness-only\nlastProbe=none";

            return summary;
        }

        private string CreateMovableNaturalObjectsSection()
        {
            return _services.MovableNaturalObjectSummary.ToSummaryLine();
        }

        private string CreateGenesisPotencyMechanicsSection()
        {
            string summary = _services.LastGenesisPotencyMechanicsSummary;
            if (string.IsNullOrWhiteSpace(summary))
                return "stateModel=harness-only\nlastProbe=none";

            return summary;
        }

        private string CreateInteractionsSection()
        {
            CharacterTestInteractionDebugSnapshot snapshot = _services.InteractionDebugSnapshot
                ?? CharacterTestInteractionDebugSnapshot.Empty;
            CharacterTestInteractionPhysicsSummary physicsSummary = _services.InteractionPhysicsSummary;
            CharacterTestInteractionVfxSummary vfxSummary = _services.InteractionVfxSummary;
            CharacterTestInteractionAudioSummary audioSummary = _services.InteractionAudioSummary;
            CharacterTestPhysicalAudioSummary physicalAudioSummary = _services.PhysicalAudioSummary;
            ContinuousPhysicalAudioSnapshot continuousAudio = _services.ContinuousPhysicalAudioSnapshot;
            CharacterTestAudioIntentDrainSummary audioIntentSummary = _services.AudioIntentDrainSummary;
            CharacterTestInteractionRenderingSummary renderingSummary = _services.InteractionRenderingSummary;
            CharacterTestInteractionConsumerDispatchSummary consumerSummary = _services.InteractionConsumerSummary;
            var builder = new StringBuilder();
            builder.Append("count=").Append(snapshot.Count).Append('\n');
            builder.Append("lastFrame=").Append(snapshot.LastFrameCount).Append('\n');
            builder.Append("stableHash=").Append(snapshot.StableHash).Append('\n');
            builder.Append("dispatcher=").Append(consumerSummary.ToSummaryLine()).Append('\n');
            builder.Append("physics=").Append(physicsSummary.ToSummaryLine()).Append('\n');
            builder.Append("vfx=").Append(vfxSummary.ToSummaryLine()).Append('\n');
            builder.Append("audio=").Append(audioSummary.ToSummaryLine()).Append('\n');
            builder.Append("physicalAudio=").Append(physicalAudioSummary.ToSummaryLine()).Append('\n');
            builder.Append("continuousAudio=active=").Append(continuousAudio.Active)
                .Append(" started=").Append(continuousAudio.Started)
                .Append(" updated=").Append(continuousAudio.Updated)
                .Append(" stopped=").Append(continuousAudio.Stopped)
                .Append(" rejected=").Append(continuousAudio.Rejected)
                .Append(" failed=").Append(continuousAudio.Failed).Append('\n');
            builder.Append("audioIntent=").Append(audioIntentSummary.ToSummaryLine()).Append('\n');
            builder.Append("rendering=").Append(renderingSummary.ToSummaryLine());
            if (snapshot.TryGetLastEvent(out CharacterTestInteractionEvent last))
            {
                builder.Append('\n')
                    .Append("last=")
                    .Append(last.ToSummaryLine());
            }
            else
            {
                builder.Append('\n').Append("last=none");
            }

            IReadOnlyList<CharacterTestInteractionEvent> lastFrameEvents = snapshot.LastFrameEvents;
            for (int i = 0; i < lastFrameEvents.Count; i++)
            {
                builder.Append('\n')
                    .Append("lastFrame.")
                    .Append(i)
                    .Append('=')
                    .Append(lastFrameEvents[i].ToSummaryLine());
            }

            return builder.ToString();
        }

        private string CreateAudioRuntimeSection()
        {
            if (_services.AudioService == null)
                return "unavailable";

            CharacterTestAudioCatalogStatus catalog = _services.AudioCatalogStatus;
            FrameworkDebugSnapshot snapshot = new AudioDebugSource(
                _services.AudioService,
                _services.AudioPresentationDrain).CreateSnapshot();
            var builder = new StringBuilder();
            builder.Append("[Catalog]\nmode=").Append(catalog.Mode)
                .Append("\naudioManifestHash=").Append(catalog.AudioManifestHash)
                .Append("\nphysicalManifestHash=").Append(catalog.PhysicalManifestHash)
                .Append("\nlinkedAudioCatalogHash=").Append(catalog.LinkedAudioCatalogHash)
                .Append("\nmessage=").Append(catalog.Message);
            for (int i = 0; i < snapshot.Sections.Count; i++)
            {
                FrameworkDebugSection section = snapshot.Sections[i];
                builder.Append('\n');
                builder.Append('[').Append(section.Title).Append("]\n").Append(section.Body);
            }
            return builder.ToString();
        }

        private static string CreateResourcesSection(ResourceDebugSnapshot resources)
        {
            if (resources == null)
                return "unavailable";

            return "catalogs=" + resources.CatalogCount
                + "\nentries=" + resources.EntryCount
                + "\nloaded=" + resources.LoadedCount
                + "\nloading=" + resources.LoadingCount
                + "\nfailed=" + resources.FailedCount
                + "\nrecentErrors=" + resources.RecentErrors.Count;
        }

        private ResourceDebugSnapshot TryCreateResourceSnapshot()
        {
            try
            {
                return _services.Resources?.ResourceManager?.CreateDebugSnapshot();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void AppendLineIfNeeded(StringBuilder builder, int index, int count)
        {
            if (index + 1 < count)
                builder.Append('\n');
        }

        private static string FormatFix(MxFramework.Core.Math.Fix64 value)
        {
            return ((double)value.RawValue / MxFramework.Core.Math.Fix64.Scale).ToString("F2");
        }

        private static void AppendLocalPlayerInput(StringBuilder builder, CharacterTestCharacterEntity localPlayer)
        {
            builder.Append("input.localPlayer=");
            if (localPlayer == null)
            {
                builder.Append("none").Append('\n');
                return;
            }

            builder.Append(localPlayer.Id.Value)
                .Append(" move=(")
                .Append(localPlayer.Input.Move.x.ToString("F2"))
                .Append(',')
                .Append(localPlayer.Input.Move.y.ToString("F2"))
                .Append(") jump=")
                .Append(localPlayer.Input.JumpPressed ? "true" : "false")
                .Append(" held=")
                .Append(localPlayer.Input.JumpHeld ? "true" : "false")
                .Append(" released=")
                .Append(localPlayer.Input.JumpReleased ? "true" : "false")
                .Append(" attack=")
                .Append(localPlayer.Input.AttackPrimaryPressed ? "true" : "false")
                .Append(" fireball=")
                .Append(localPlayer.Input.AttackSecondaryPressed ? "true" : "false")
                .Append(" fireballBurst=")
                .Append(localPlayer.Input.DebugPrimaryPressed ? "true" : "false")
                .Append(" flameLance=")
                .Append(localPlayer.Input.DebugSecondaryPressed ? "true" : "false")
                .Append(" grounded=")
                .Append(localPlayer.IsGrounded ? "true" : "false")
                .Append(" lastMove=(")
                .Append(localPlayer.LastMoveInput.x.ToString("F2"))
                .Append(',')
                .Append(localPlayer.LastMoveInput.y.ToString("F2"))
                .Append(")")
                .Append('\n')
                .Append("action.active=")
                .Append(EmptyAsNone(localPlayer.Action.ActiveActionId))
                .Append(" state=")
                .Append(CharacterTestCharacterActionComponent.ResolveStateText(localPlayer.Action.State))
                .Append(" phase=")
                .Append(CharacterTestCharacterActionComponent.ResolvePhaseText(localPlayer.Action.Phase))
                .Append(" localFrame=")
                .Append(localPlayer.Action.LocalFrame)
                .Append(" lastReject=")
                .Append(EmptyAsNone(localPlayer.Action.LastRejectReason))
                .Append(" animationActionKey=")
                .Append(EmptyAsNone(localPlayer.Action.LastAnimationActionKey))
                .Append('\n');
        }

        private void AppendMotionDiagnostic(StringBuilder builder, int entityId)
        {
            builder.Append(" motionDiagnostic=");
            if (_services.CharacterRuntime == null
                || !_services.CharacterRuntime.TryGetCharacterMotionDiagnostic(
                    entityId,
                    out CharacterTestCharacterMotionDiagnostic diagnostic))
            {
                builder.Append("none");
                return;
            }

            builder.Append('(')
                .Append("physics=")
                .Append(diagnostic.HasPhysics ? "true" : "false")
                .Append(" collision=")
                .Append(diagnostic.HasCollision ? "true" : "false")
                .Append(" shape=")
                .Append(diagnostic.TargetShapeKind)
                .Append(" target=")
                .Append(diagnostic.TargetBodyId.Value)
                .Append('.')
                .Append(diagnostic.TargetColliderId.Value)
                .Append(" normal=")
                .Append(diagnostic.Normal)
                .Append(" normalY=")
                .Append(diagnostic.Normal.Y)
                .Append(" threshold=")
                .Append(diagnostic.GroundMinNormalY)
                .Append(" classification=")
                .Append(diagnostic.SurfaceClassification)
                .Append(" snapDistance=")
                .Append(diagnostic.GroundSnapAppliedDistance)
                .Append(" flags=")
                .Append(diagnostic.CollisionFlags)
                .Append(" material=")
                .Append(diagnostic.SurfaceMaterial.Kind)
                .Append('/')
                .Append(EmptyAsNone(diagnostic.SurfaceMaterial.StableId))
                .Append(" requested=")
                .Append(diagnostic.RequestedDelta)
                .Append(" applied=")
                .Append(diagnostic.AppliedDelta)
                .Append(" resolved=")
                .Append(diagnostic.ResolvedVelocity)
                .Append(')');
        }

        private static string EmptyAsNone(int value) => value == 0 ? "none" : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrEmpty(value) ? "none" : value;
        }

        private static string EmptyAsNone(CharacterActionId value)
        {
            return value.IsValid ? value.ToString() : "none";
        }

        private static string FormatActionReload(CharacterTestCharacterActionReloadResult? result)
        {
            if (!result.HasValue)
                return "not requested";

            CharacterTestCharacterActionReloadResult value = result.Value;
            return (value.Success ? "success" : "failed")
                + " frame=" + value.Frame.Value
                + " imports=" + value.ImportDiagnosticCount
                + " validations=" + value.ValidationDiagnosticCount
                + " message=" + EmptyAsNone(value.Message);
        }

        private static void AppendRuntimeModules(StringBuilder builder, RuntimeHostDiagnostics host)
        {
            builder.Append("modules=");
            if (host.Modules == null || host.Modules.Count == 0)
            {
                builder.Append("none");
                return;
            }

            for (int i = 0; i < host.Modules.Count; i++)
            {
                RuntimeModuleDiagnostics module = host.Modules[i];
                if (i > 0)
                    builder.Append(" -> ");

                builder.Append(module.ModuleId)
                    .Append('[')
                    .Append(module.TickStage)
                    .Append(':')
                    .Append(module.Priority)
                    .Append(']');
            }
        }
    }

    public sealed class CharacterTestAbilityDebugSource : IFrameworkDebugSource
    {
        private readonly CharacterTestSliceServices _services;

        internal CharacterTestAbilityDebugSource(CharacterTestSliceServices services, string name = "Ability")
        {
            _services = services;
            Name = string.IsNullOrWhiteSpace(name) ? "Ability" : name;
        }

        internal CharacterTestAbilityDebugSource(GameSlice slice, string name = "Ability")
            : this(slice?.Services, name)
        {
        }

        public string Name { get; }
        public FrameworkDebugMode Mode => FrameworkDebugMode.Runtime;
        public bool IsAvailable => _services?.AbilityRuntime != null;

        public FrameworkDebugSnapshot CreateSnapshot()
        {
            if (!IsAvailable)
            {
                return new FrameworkDebugSnapshot(
                    Name,
                    Mode,
                    new[] { new FrameworkDebugSection("Status", "unavailable") });
            }

            AbilityRuntimeDebugSnapshot snapshot = _services.AbilityRuntime.CreateDebugSnapshot();
            return new FrameworkDebugSnapshot(
                Name,
                Mode,
                new[]
                {
                    new FrameworkDebugSection("Summary", CreateSummary(snapshot)),
                    new FrameworkDebugSection("Cooldowns", CreateCooldownSection(snapshot)),
                    new FrameworkDebugSection("Characters", CreateCharactersSection())
                });
        }

        private string CreateSummary(AbilityRuntimeDebugSnapshot snapshot)
        {
            return "moduleId=" + snapshot.ModuleId
                + "\nframe=" + _services.CurrentFrame.Value
                + "\ndefinitionHash=" + snapshot.DefinitionHash
                + "\nactiveCooldowns=" + snapshot.ActiveCooldownCount;
        }

        private string CreateCooldownSection(AbilityRuntimeDebugSnapshot snapshot)
        {
            if (snapshot.Cooldowns.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < snapshot.Cooldowns.Count; i++)
            {
                AbilityRuntimeCooldownEntry entry = snapshot.Cooldowns[i];
                string stableId = ResolveAbilityStableId(entry.AbilityId);
                long remaining = Math.Max(0L, entry.EndFrame - _services.CurrentFrame.Value);
                builder.Append("caster=").Append(entry.CasterEntityId)
                    .Append(" ability=").Append(entry.AbilityId)
                    .Append(" stableId=").Append(EmptyAsNone(stableId))
                    .Append(" endFrame=").Append(entry.EndFrame)
                    .Append(" remaining=").Append(remaining);
                if (i + 1 < snapshot.Cooldowns.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }

        private string CreateCharactersSection()
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = _services.CharacterEntities;
            if (entities == null || entities.Count == 0)
                return "none";

            _services.AbilityRuntime.Definitions.TryGet(
                CharacterTestAbilityIds.FireballAbilityId,
                out AbilityDefinitionConfig fireball);

            var builder = new StringBuilder();
            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                long fireballRemaining = _services.AbilityRuntime.GetRemainingCooldownFrames(
                    entity.Id.Value,
                    fireball,
                    _services.CurrentFrame);
                builder.Append("entity=").Append(entity.Id.Value)
                    .Append(" owner=").Append(entity.Control.Owner)
                    .Append(" fireballRemaining=").Append(fireballRemaining)
                    .Append(" lastReject=").Append(EmptyAsNone(entity.Action.LastRejectReason));
                if (i + 1 < entities.Count)
                    builder.Append('\n');
            }

            return builder.ToString();
        }

        private string ResolveAbilityStableId(int abilityId)
        {
            return _services.AbilityRuntime.Definitions.TryGet(abilityId, out AbilityDefinitionConfig definition)
                ? definition.AbilityStableId
                : string.Empty;
        }

        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrEmpty(value) ? "none" : value;
        }

        private static string EmptyAsNone(CharacterActionId value)
        {
            return value.IsValid ? value.ToString() : "none";
        }
    }
}
