using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Camera.Unity;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Gameplay;
using MxFramework.Input;
using MxFramework.Resources;
using MxFramework.Runtime;
using MxFramework.Story;
using MxFramework.Story.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    public sealed class CharacterTestDeveloperDebugSnapshotBuilder
    {
        public CharacterTestDeveloperDebugSnapshot Build(
            GameSlice slice,
            Func<bool> isPausedProvider,
            IInputProvider inputProvider,
            CharacterTestThirdPersonCameraRigBridge cameraBridge,
            bool refreshPaused,
            IRuntimeLogger logger)
        {
            return Build(
                slice?.Services,
                isPausedProvider,
                inputProvider,
                cameraBridge,
                refreshPaused,
                logger);
        }

        internal CharacterTestDeveloperDebugSnapshot Build(
            CharacterTestSliceServices services,
            Func<bool> isPausedProvider,
            IInputProvider inputProvider,
            CharacterTestThirdPersonCameraRigBridge cameraBridge,
            bool refreshPaused,
            IRuntimeLogger logger)
        {
            if (services == null)
                return CharacterTestDeveloperDebugSnapshot.Unavailable(refreshPaused);

            RuntimeHostDiagnostics host = services.CaptureDiagnostics();
            ResourceDebugSnapshot resources = TryCreateResourceSnapshot(services, logger);
            int issueCount = CountIssues(services, host, resources);
            CharacterTestCharacterEntity localPlayer = FindLocalPlayer(services.CharacterEntities);
            var tabs = new[]
            {
                CreateStatusTab(services, isPausedProvider, host, resources, cameraBridge),
                CreateCharactersTab(services),
                CreateMovablesTab(services),
                CreateCameraInputTab(services, inputProvider, cameraBridge),
                CreateCommandsTab(services),
                CreateStoryTab(services),
                CreateResourcesTab(services, resources)
            };

            return new CharacterTestDeveloperDebugSnapshot(
                true,
                services.CurrentFrame.Value,
                refreshPaused,
                issueCount,
                localPlayer != null ? "#" + localPlayer.Id.Value : "missing",
                FormatCollapsedAction(localPlayer),
                FormatTargetLock(services),
                cameraBridge != null,
                cameraBridge != null && cameraBridge.CursorCaptured,
                tabs);
        }

        private static CharacterTestDeveloperDebugTabSnapshot CreateStatusTab(
            CharacterTestSliceServices services,
            Func<bool> isPausedProvider,
            RuntimeHostDiagnostics host,
            ResourceDebugSnapshot resources,
            CharacterTestThirdPersonCameraRigBridge cameraBridge)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = services.CharacterEntities;
            CharacterTestCharacterEntity localPlayer = FindLocalPlayer(entities);
            StoryDirectorSnapshot story = services.StorySnapshot;
            CharacterTestCharacterActionRuntime actionRuntime = services.ActionRuntime;
            CharacterTestCharacterActionReloadResult? actionReload = services.LastActionReloadResult;
            CharacterTestInteractionDebugSnapshot interactions = services.InteractionDebugSnapshot;
            CharacterTestInteractionPhysicsSummary interactionPhysics = services.InteractionPhysicsSummary;
            CharacterTestInteractionVfxSummary interactionVfx =
                services.InteractionVfxSummary;
            CharacterTestInteractionAudioSummary interactionAudio =
                services.InteractionAudioSummary;
            CharacterTestPhysicalAudioSummary physicalAudio =
                services.PhysicalAudioSummary;
            CharacterTestAudioIntentDrainSummary audioIntent =
                services.AudioIntentDrainSummary;
            CharacterTestInteractionRenderingSummary interactionRendering =
                services.InteractionRenderingSummary;
            CharacterTestInteractionConsumerDispatchSummary interactionConsumers =
                services.InteractionConsumerSummary;

            bool paused = isPausedProvider != null && isPausedProvider();
            var cards = new List<CharacterTestDeveloperDebugCard>
            {
                CharacterTestDeveloperDebugCard.MetricGrid(
                    "Runtime",
                    new[]
                    {
                        Metric("Frame", services.CurrentFrame.Value.ToString()),
                        Metric("Elapsed", services.ElapsedSeconds.ToString("F1") + "s"),
                        Metric("Host Ticks", host.TickCount.ToString()),
                        StatusChip("Paused", paused ? "YES" : "NO", paused),
                        StatusChip("Host", host.State.ToString(), host.State != RuntimeLifecycleState.Started),
                        Text("Modules: " + FormatModules(host))
                    }),
                CharacterTestDeveloperDebugCard.StatusGrid(
                    "Health",
                    new[]
                    {
                        StatusChip("Characters", CountOrZero(entities).ToString(), CountOrZero(entities) == 0),
                        StatusChip("Local Player", localPlayer != null ? "#" + localPlayer.Id.Value : "missing", localPlayer == null),
                        StatusChip("Action", localPlayer != null ? EmptyAsNone(localPlayer.Action.ActiveActionId) : "n/a", localPlayer == null),
                        StatusChip("Action Reject", localPlayer != null ? EmptyAsNone(localPlayer.Action.LastRejectReason) : "n/a", localPlayer != null && !string.IsNullOrEmpty(localPlayer.Action.LastRejectReason)),
                        StatusChip("Camera", services.CameraMode.ToString()),
                        StatusChip("Action Config", FormatActionConfigSource(actionRuntime), actionRuntime == null || !actionRuntime.IsReady),
                        StatusChip("Movement Profile", FormatMovementProfile(services.MovementProfile), services.MovementProfile.UsedFallback),
                        StatusChip("Last Reload", FormatActionReload(actionReload), actionReload.HasValue && !actionReload.Value.Success),
                        StatusChip("Target Lock", FormatTargetLock(services)),
                        StatusChip(
                            "Effect Preview",
                            FormatProceduralEffectPreview(services),
                            IsProceduralEffectPreviewWarning(services)),
                        StatusChip(
                            "Hit Feedback",
                            FormatProceduralHitFeedback(services),
                            IsProceduralHitFeedbackWarning(services)),
                        StatusChip(
                            "Interactions",
                            interactions.LastFrameCount + "/" + interactions.Count),
                        StatusChip(
                            "Interaction Field",
                            interactionPhysics.AppliedForceCount + "/" + interactionPhysics.ProcessedFieldCount),
                        StatusChip(
                            "Interaction VFX",
                            interactionVfx.VfxSpawnedCount
                            + "+"
                            + interactionAudio.AudioPlayedCount
                            + "/"
                            + Math.Max(
                                interactionVfx.ProcessedInteractionCount,
                                interactionAudio.ProcessedInteractionCount)),
                        StatusChip(
                            "Interaction Render",
                            interactionRendering.PublishedFieldImpulseCount
                            + "/"
                            + interactionRendering.ProcessedFieldCount),
                        StatusChip(
                            "Interaction Consumers",
                            interactionConsumers.ExecutedConsumerCount
                            + "/"
                            + interactionConsumers.RegisteredConsumerCount,
                            interactionConsumers.FailedConsumerCount > 0),
                        StatusChip("Story Beats", story.ActiveBeatInstances.Count.ToString()),
                        StatusChip("Cmd Errors", services.RuntimeCommands.LastCommandErrors.Count.ToString(), services.RuntimeCommands.LastCommandErrors.Count > 0),
                        StatusChip("Story Errors", services.StoryModule.LastCommandErrors.Count.ToString(), services.StoryModule.LastCommandErrors.Count > 0),
                        StatusChip("Resources", resources != null ? resources.FailedCount + " failed" : "n/a", resources == null || resources.FailedCount > 0),
                        StatusChip("Host Errors", host.Errors.Count.ToString(), host.Errors.Count > 0)
                    }),
                CharacterTestDeveloperDebugCard.FlowPipeline(
                    "Flow Check",
                    new[]
                    {
                        FlowStep("Spawn", CountOrZero(entities) > 0 ? CountOrZero(entities) + " entity" : "waiting", CountOrZero(entities) > 0),
                        FlowStep("Input", localPlayer != null ? Vec2(localPlayer.Input.Move) : "no player", localPlayer != null),
                        FlowStep("Motion", localPlayer != null ? FormatMotionShort(localPlayer) : "no player", localPlayer != null && localPlayer.IsGrounded),
                        FlowStep("Action", localPlayer != null ? FormatActionShort(localPlayer) : "no player", localPlayer != null),
                        FlowStep("Animation", localPlayer != null ? FormatAnimationShort(localPlayer) : "no player", localPlayer != null),
                        FlowStep("Camera", cameraBridge != null ? "bridge ok" : "missing", cameraBridge != null)
                    })
            };
            cards.Add(CharacterTestDeveloperDebugCard.Log(
                "Action Config Source",
                FormatActionConfigDiagnostics(actionRuntime, actionReload)));
            cards.Add(CharacterTestDeveloperDebugCard.Log(
                "Input Trace",
                FormatTraceEvents(services.TraceRecorder)));
            cards.Add(CharacterTestDeveloperDebugCard.Log(
                "Interactions",
                FormatInteractions(
                    interactions,
                    interactionPhysics,
                    interactionVfx,
                    interactionAudio,
                    physicalAudio,
                    audioIntent,
                    interactionRendering)));

            return new CharacterTestDeveloperDebugTabSnapshot(CharacterTestDeveloperDebugSnapshot.TabNames[0], cards);
        }

        private static CharacterTestDeveloperDebugTabSnapshot CreateCharactersTab(CharacterTestSliceServices services)
        {
            IReadOnlyList<CharacterTestCharacterEntity> entities = services.CharacterEntities;
            var cards = new List<CharacterTestDeveloperDebugCard>();
            CharacterTestDeveloperDebugCard presenceContact =
                CharacterTestLargeCharacterPresenceDebug.CreateContactDiagnosticsCard(
                    services.CharacterRuntime);
            if (presenceContact != null)
                cards.Add(presenceContact);
            if (entities == null || entities.Count == 0)
            {
                cards.Add(CharacterTestDeveloperDebugCard.StatusGrid(
                    "Characters",
                    new[] { StatusChip("entities", "none", true) }));
                return new CharacterTestDeveloperDebugTabSnapshot(CharacterTestDeveloperDebugSnapshot.TabNames[1], cards);
            }

            for (int i = 0; i < entities.Count; i++)
            {
                CharacterTestCharacterEntity entity = entities[i];
                bool isLocal = entity.Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer;
                CharacterTestCharacterMotionDiagnostic motionDiagnostic = default;
                bool hasMotionDiagnostic = services.CharacterRuntime != null
                    && services.CharacterRuntime.TryGetCharacterMotionDiagnostic(
                        entity.Id.Value,
                        out motionDiagnostic);
                cards.Add(CharacterTestDeveloperDebugCard.Entity(
                    (isLocal ? "★ " : string.Empty) + "Entity #" + entity.Id.Value,
                    new[]
                    {
                        StatusChip("Character", entity.Identity.CharacterId.ToString()),
                        StatusChip("Team", entity.Team.TeamId.ToString()),
                        StatusChip("Owner", entity.Control.Owner.ToString()),
                        StatusChip("HP", entity.CombatState.Health.ToString() + "/" + entity.CombatState.MaxHealth.ToString(), !entity.CombatState.IsAlive),
                        StatusChip(
                            "ScatterPressure",
                            entity.CombatState.ScatterPressure.ToString()
                                + "/"
                                + entity.CombatState.MaxScatterPressure.ToString()
                                + " "
                                + entity.CombatState.ScatterPressureBand,
                            entity.CombatState.ScatterPressureBand != PressureBand.Stable),
                        StatusChip("Hit", entity.CombatState.IsReacting ? entity.CombatState.HitStunFramesRemaining.ToString() + "f" : "ready", entity.CombatState.IsReacting),
                        Metric("Yaw", entity.Transform.YawDegrees.ToString("F0") + "°"),
                        Metric("Vy", entity.VerticalVelocity.ToString("F2")),
                        Metric("Speed", entity.HorizontalVelocity.magnitude.ToString("F2")),
                        StatusChip("Grounded", entity.IsGrounded ? "yes" : "no", !entity.IsGrounded),
                        StatusChip("Move Mode", entity.MovementMode.ToString(), entity.MovementLocked),
                        Metric("Control Scale", entity.MovementControlScale.ToString("F2") + " / " + entity.RotationControlScale.ToString("F2")),
                        StatusChip("Move Profile", EmptyAsNone(entity.MovementProfileId), entity.MovementProfileFallback),
                        StatusChip("Physics", entity.PhysicsEnabled ? entity.CollisionFlags.ToString() : "planar", !entity.PhysicsEnabled),
                        StatusChip("Surface", FormatSurfaceMaterial(entity)),
                        StatusChip(
                            "Slope Diagnostic",
                            FormatMotionDiagnosticState(hasMotionDiagnostic, motionDiagnostic)),
                        Metric(
                            "Slope Target",
                            FormatMotionDiagnosticTarget(hasMotionDiagnostic, motionDiagnostic)),
                        Metric(
                            "Slope Normal Y",
                            FormatMotionDiagnosticNormal(hasMotionDiagnostic, motionDiagnostic)),
                        Metric(
                            "Slope Class / Snap",
                            FormatMotionDiagnosticClassification(hasMotionDiagnostic, motionDiagnostic)),
                        Text(
                            "Slope flags/material "
                            + FormatMotionDiagnosticFlagsAndMaterial(hasMotionDiagnostic, motionDiagnostic)),
                        Text(
                            "Slope requested/applied/resolved "
                            + FormatMotionDiagnosticDeltas(hasMotionDiagnostic, motionDiagnostic)),
                        StatusChip(
                            "Living Natural",
                            entity.LivingNaturalAttributes.LifecycleState.ToString()
                                + " "
                                + entity.LivingNaturalAttributes.MaterialReading,
                            entity.LivingNaturalAttributes.ReturnedToMaterial),
                        Metric("Surface Friction", entity.SurfaceFriction.ToString("F2")),
                        Metric("Surface Elasticity", entity.SurfaceElasticity.ToString("F2")),
                        Metric("Surface Bounce Height", entity.SurfaceFixedBounceHeight.ToString("F2")),
                        Metric(
                            "Body",
                            "r=" + entity.Physics.EffectiveRadiusMeters.ToString("F2")
                                + " h=" + entity.Physics.EffectiveHeightMeters.ToString("F2")
                                + " m=" + entity.Physics.EffectiveMassKg.ToString("F0")),
                        Metric(
                            "Push",
                            entity.Physics.EffectivePushScale.ToString("F2")
                                + " contact=" + entity.Physics.ContactCount.ToString()),
                        Vector2("Contact Vel", entity.Physics.ContactVelocity.x, entity.Physics.ContactVelocity.y),
                        Vector2("Contact Move", entity.Physics.ContactDisplacement.x, entity.Physics.ContactDisplacement.y),
                        Metric(
                            "Living Scatter",
                            entity.LivingNaturalAttributes.Scatter.ToString()
                                + "/"
                                + entity.LivingNaturalAttributes.CohesionLimit.ToString()),
                        Vector2("Input", entity.Input.Move.x, entity.Input.Move.y),
                        Vector2("Last Move", entity.LastMoveInput.x, entity.LastMoveInput.y),
                        Vector2("Velocity", entity.HorizontalVelocity.x, entity.HorizontalVelocity.y),
                        Vector2("Desired", entity.DesiredVelocity.x, entity.DesiredVelocity.y),
                        Text("Intent buffer=" + entity.PendingGameplayIntentCount.ToString()
                            + " jump="
                            + FormatIntentSeconds(entity, services.CurrentFrame.Value)
                            + " coyote="
                            + entity.CoyoteTimeSecondsRemaining.ToString("F2")),
                        Text(FormatCombatState(entity), entity.CombatState.IsReacting || !entity.CombatState.IsAlive),
                        Text(FormatAction(entity), !string.IsNullOrEmpty(entity.Action.LastRejectReason)),
                        Text("Pos (" + entity.Transform.X.ToString("F1") + ", " + entity.Transform.Y.ToString("F1") + ", " + entity.Transform.Z.ToString("F1") + ")"),
                        Text(FormatAnimation(entity))
                    }));

                if (CharacterTestLargeCharacterPresenceDebug.TryCapture(
                        services.CharacterRuntime,
                        entity,
                        out CharacterTestLargeCharacterPresenceDebugSnapshot presence))
                {
                    cards.Add(CharacterTestLargeCharacterPresenceDebug.CreateCard(presence));
                }

                if (CharacterTestSeverancePresentationDebug.TryCapture(
                        services.CharacterRuntime,
                        entity,
                        out CharacterTestSeverancePresentationDebugSnapshot severance))
                {
                    cards.Add(CharacterTestSeverancePresentationDebug.CreateCard(severance));
                }
            }

            return new CharacterTestDeveloperDebugTabSnapshot(CharacterTestDeveloperDebugSnapshot.TabNames[1], cards);
        }

        private static string FormatMotionDiagnosticState(
            bool hasDiagnostic,
            CharacterTestCharacterMotionDiagnostic diagnostic)
        {
            if (!hasDiagnostic)
                return "none";
            if (!diagnostic.HasPhysics)
                return "planar";
            return diagnostic.HasCollision ? "contact" : "no collision";
        }

        private static string FormatMotionDiagnosticTarget(
            bool hasDiagnostic,
            CharacterTestCharacterMotionDiagnostic diagnostic)
        {
            if (!hasDiagnostic || !diagnostic.HasCollision)
                return "none";

            return diagnostic.TargetShapeKind
                + " "
                + diagnostic.TargetBodyId.Value
                + "."
                + diagnostic.TargetColliderId.Value;
        }

        private static string FormatMotionDiagnosticNormal(
            bool hasDiagnostic,
            CharacterTestCharacterMotionDiagnostic diagnostic)
        {
            if (!hasDiagnostic)
                return "none";

            return diagnostic.Normal.Y
                + " / threshold "
                + diagnostic.GroundMinNormalY;
        }

        private static string FormatMotionDiagnosticClassification(
            bool hasDiagnostic,
            CharacterTestCharacterMotionDiagnostic diagnostic)
        {
            if (!hasDiagnostic)
                return "none";

            return diagnostic.SurfaceClassification
                + " snap="
                + diagnostic.GroundSnapAppliedDistance;
        }

        private static string FormatMotionDiagnosticFlagsAndMaterial(
            bool hasDiagnostic,
            CharacterTestCharacterMotionDiagnostic diagnostic)
        {
            if (!hasDiagnostic)
                return "none";

            return diagnostic.CollisionFlags
                + " "
                + diagnostic.SurfaceMaterial.Kind
                + "/"
                + EmptyAsNone(diagnostic.SurfaceMaterial.StableId);
        }

        private static string FormatMotionDiagnosticDeltas(
            bool hasDiagnostic,
            CharacterTestCharacterMotionDiagnostic diagnostic)
        {
            if (!hasDiagnostic)
                return "none";

            return "requested="
                + diagnostic.RequestedDelta
                + " applied="
                + diagnostic.AppliedDelta
                + " resolved="
                + diagnostic.ResolvedVelocity;
        }

        private static CharacterTestDeveloperDebugTabSnapshot CreateMovablesTab(
            CharacterTestSliceServices services)
        {
            IReadOnlyList<CharacterTestMovableNaturalObjectState> objects = services.MovableNaturalObjects;
            var cards = new List<CharacterTestDeveloperDebugCard>
            {
                CharacterTestDeveloperDebugCard.MetricGrid(
                    "Movable Natural Objects",
                    new[]
                    {
                        Metric("Count", CountOrZero(objects).ToString()),
                        StatusChip(
                            "State",
                            CountOrZero(objects) > 0 ? "runtime-authority" : "none",
                            CountOrZero(objects) == 0),
                        Text(services.MovableNaturalObjectSummary.ToSummaryLine())
                    })
            };
            if (objects == null || objects.Count == 0)
                return new CharacterTestDeveloperDebugTabSnapshot(CharacterTestDeveloperDebugSnapshot.TabNames[2], cards);

            for (int i = 0; i < objects.Count; i++)
            {
                CharacterTestMovableNaturalObjectState obj = objects[i];
                cards.Add(CharacterTestDeveloperDebugCard.Entity(
                    "Object #" + obj.NumericId,
                    new[]
                    {
                        StatusChip("Stable", EmptyAsNone(obj.StableId)),
                        StatusChip("Category", EmptyAsNone(obj.Category)),
                        StatusChip("Shape", obj.ShapeKind.ToString()),
                        StatusChip("Material", obj.Material.Kind + "/" + obj.Material.StableId),
                        StatusChip("Potency Result", obj.LatestPotencyApplication.Result.ToString()),
                        Metric("Mass", obj.MassKg.ToString("F1") + "kg"),
                        Metric("Base DPS", obj.BasePotency.ToTupleString()),
                        Metric("Current DPS", obj.CurrentPotency.ToTupleString()),
                        Metric(
                            "Position",
                            "("
                                + obj.X.ToString("F2")
                                + ", "
                                + obj.Y.ToString("F2")
                                + ", "
                                + obj.Z.ToString("F2")
                                + ")"),
                        Metric(
                            "Half Extents",
                            "("
                                + obj.HalfExtents.x.ToString("F2")
                                + ", "
                                + obj.HalfExtents.y.ToString("F2")
                                + ", "
                                + obj.HalfExtents.z.ToString("F2")
                                + ")"),
                        Vector2("Velocity", obj.HorizontalVelocity.x, obj.HorizontalVelocity.y),
                        Vector2("Contact Vel", obj.ContactVelocity.x, obj.ContactVelocity.y),
                        Vector2("Contact Move", obj.ContactDisplacement.x, obj.ContactDisplacement.y),
                        Metric("Pressure", obj.ContactPressure.ToString()),
                        Metric("Contact Count", obj.ContactCount.ToString()),
                        StatusChip("Behavior", EmptyAsNone(obj.BehaviorState)),
                        Text("contactPair=" + EmptyAsNone(obj.ContactPair)),
                        Text("potencyProfile=" + obj.PotencyProfile.ToSummaryLine()),
                        Text("latestPotency=" + obj.LatestPotencyApplication.ToSummaryLine())
                    }));
            }

            return new CharacterTestDeveloperDebugTabSnapshot(CharacterTestDeveloperDebugSnapshot.TabNames[2], cards);
        }

        private static CharacterTestDeveloperDebugTabSnapshot CreateCameraInputTab(
            CharacterTestSliceServices services,
            IInputProvider inputProvider,
            CharacterTestThirdPersonCameraRigBridge cameraBridge)
        {
            var cards = new List<CharacterTestDeveloperDebugCard>();
            var cameraRows = new List<CharacterTestDeveloperDebugLine>
            {
                Row("runtime mode", services.CameraMode.ToString()),
                Row("target lock", FormatTargetLock(services)),
                Row("bridge", cameraBridge != null ? "active" : "missing", cameraBridge == null)
            };

            if (cameraBridge != null)
            {
                CharacterTestCharacterMovementBasis basis = cameraBridge.CurrentMovementBasis;
                cameraRows.Add(Row("cursor captured", Bool(cameraBridge.CursorCaptured)));
                cameraRows.Add(Row("basis forward", Vec2(basis.Forward)));
                cameraRows.Add(Row("basis right", Vec2(basis.Right)));
                cameraRows.Add(Row("basis locked target", basis.HasLockedTarget ? Vec2(basis.LockedTargetPosition) : "none"));
                MxCameraUnityShakeResult shake = cameraBridge.LastShakeResult;
                cameraRows.Add(Row("shake mode", cameraBridge.ShakeMode.ToString()));
                cameraRows.Add(Row(
                    "shake",
                    shake.IsActive
                        ? "active i=" + shake.Intensity.ToString("F2") + " pos=" + Vec3(shake.PositionOffset) + " rot=" + Vec3(shake.RotationEulerOffset)
                        : "idle"));
                MxCameraUnityObstructionResult obstruction = cameraBridge.LastObstructionResult;
                cameraRows.Add(Row(
                    "obstruction",
                    obstruction.IsObstructed
                        ? "hit d=" + obstruction.ResolvedDistance.ToString("F2") + "/" + obstruction.IdealDistance.ToString("F2")
                        : cameraBridge.ObstructionMode.ToString()));
            }

            cards.Add(CharacterTestDeveloperDebugCard.StatusGrid("Camera", ToStatusLines(cameraRows)));

            if (inputProvider == null)
            {
                cards.Add(CharacterTestDeveloperDebugCard.StatusGrid(
                    "Input Provider",
                    new[] { StatusChip("provider", "unavailable", true) }));
                return new CharacterTestDeveloperDebugTabSnapshot(CharacterTestDeveloperDebugSnapshot.TabNames[3], cards);
            }

            InputSnapshot snapshot = inputProvider.Snapshot;
            cards.Add(CharacterTestDeveloperDebugCard.InputPanel(
                "Input Provider",
                new CharacterTestDeveloperDebugLine[]
                {
                    StatusChip("Context", inputProvider.CurrentContext.ToString()),
                    Bool("Gameplay", inputProvider.IsContextEnabled(InputContext.Gameplay)),
                    Bool("Debug UI", inputProvider.IsContextEnabled(InputContext.Debug)),
                    Bool("Jump Held", snapshot.JumpHeld),
                    Bool("Attack", snapshot.AttackPrimaryPressed),
                    Bool("Fireball", snapshot.AttackSecondaryPressed),
                    Bool("Preview F5", snapshot.DebugPrimaryPressed),
                    Bool("Preview F6", snapshot.DebugSecondaryPressed),
                    Bool("Preview F7", snapshot.AudioPrimaryPressed),
                    Bool("Preview F8", snapshot.AudioSecondaryPressed),
                    Text("Skill Slots " + snapshot.SkillSlotPressedMask.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    Bool("RMB Held", snapshot.RightClickHeld),
                    Bool("Sprint", snapshot.SprintHeld),
                    Bool("Debug Toggle", snapshot.DebugTogglePressed),
                    Vector2("Move", snapshot.Move.x, snapshot.Move.y),
                    Vector2("Look", snapshot.Look.x, snapshot.Look.y),
                    Vector2("Point", snapshot.Point.x, snapshot.Point.y)
                }));

            return new CharacterTestDeveloperDebugTabSnapshot(CharacterTestDeveloperDebugSnapshot.TabNames[3], cards);
        }

        private static CharacterTestDeveloperDebugTabSnapshot CreateCommandsTab(CharacterTestSliceServices services)
        {
            RuntimeEventQueueSnapshot queue = services.RuntimeEventQueueSnapshot;
            CharacterTestDamageZoneLockStateSnapshot[] zoneLocks =
                services.RuntimeAiPlannerCommands?.CaptureDamageZoneLocksForDiagnostics()
                ?? Array.Empty<CharacterTestDamageZoneLockStateSnapshot>();
            CharacterTestEngagementDiagnosticSnapshot[] engagement =
                services.RuntimeAiPlannerCommands?.CaptureEngagementDiagnosticsForDiagnostics()
                ?? Array.Empty<CharacterTestEngagementDiagnosticSnapshot>();
            CharacterTestRuntimeAiJointDiagnosticSnapshot[] jointSelection =
                services.RuntimeAiPlannerCommands?.CaptureJointSelectionDiagnosticsForDiagnostics()
                ?? Array.Empty<CharacterTestRuntimeAiJointDiagnosticSnapshot>();
            var cards = new List<CharacterTestDeveloperDebugCard>
            {
                CharacterTestDeveloperDebugCard.MetricGrid(
                    "Command Queue",
                    new[]
                    {
                        Metric("Registered", services.RuntimeCommands.CommandRegistry.Count.ToString()),
                        Metric("Pending", services.PendingRuntimeCommandCount.ToString(), services.PendingRuntimeCommandCount > 0),
                        Metric("Drained", services.RuntimeCommands.LastDrainedCommands.Count.ToString()),
                        Metric("Errors", services.RuntimeCommands.LastCommandErrors.Count.ToString(), services.RuntimeCommands.LastCommandErrors.Count > 0),
                        Metric("Events", queue.PendingCount.ToString(), queue.PendingCount > 0)
                    }),
                CreateDamageZoneDiagnosticsCard(zoneLocks),
                CreateEngagementAnchorDiagnosticsCard(engagement),
                CreateJointSelectionDiagnosticsCard(jointSelection),
                CharacterTestDeveloperDebugCard.Log("Last Drained Commands", FormatCommands(services.RuntimeCommands.LastDrainedCommands)),
                CharacterTestDeveloperDebugCard.Log("Command Errors", FormatCommandErrors(services.RuntimeCommands.LastCommandErrors)),
                CharacterTestDeveloperDebugCard.Log("Runtime Events", FormatRuntimeEvents(services.RecentRuntimeEvents))
            };

            return new CharacterTestDeveloperDebugTabSnapshot(CharacterTestDeveloperDebugSnapshot.TabNames[4], cards);
        }

        internal static CharacterTestDeveloperDebugCard CreateDamageZoneDiagnosticsCard(
            IReadOnlyList<CharacterTestDamageZoneLockStateSnapshot> locks)
        {
            if (locks == null || locks.Count == 0)
            {
                return CharacterTestDeveloperDebugCard.StatusGrid(
                    "DamageZone Selection",
                    new[]
                    {
                        StatusChip("Lock", "none", true),
                        Text("No authored DamageZone lock is active. Legacy Hurtbox evaluation may still be in use.")
                    });
            }

            var lines = new List<CharacterTestDeveloperDebugLine>(locks.Count * 9);
            for (int i = 0; i < locks.Count; i++)
            {
                CharacterTestDamageZoneLockStateSnapshot value = locks[i];
                if (i > 0)
                    lines.Add(Text("---"));
                lines.Add(StatusChip("Source", "#" + value.SourceEntityId));
                lines.Add(Row(
                    "Target",
                    "gameplay=" + value.TargetGameplayEntityIndex
                        + ":" + value.TargetGameplayEntityGeneration
                        + " combat=" + value.TargetCombatEntityId));
                lines.Add(Row("DamageZone", EmptyAsNone(value.ZoneId), value.ZoneId == 0));
                lines.Add(Row("HitZone", EmptyAsNone(value.HitZoneId), value.HitZoneId == 0));
                lines.Add(Row("Hurtbox", EmptyAsNone(value.LogicalHurtboxId), value.LogicalHurtboxId == 0));
                lines.Add(Row(
                    "Opportunity",
                    ((CharacterTestAttackOpportunityLevel)value.OpportunityLevel)
                        + " / earliest tick " + value.EarliestHitTick));
                lines.Add(Row(
                    "Decision",
                    ((CharacterTestDamageZoneSwitchReason)value.SwitchReason)
                        + " @ frame " + value.SelectedFrame));
                lines.Add(Row("Action Revision", value.ResolvedActionRevision.ToString()));
                lines.Add(Row(
                    "Revisions",
                    "binding=" + value.BindingRevision
                        + " zone=" + value.ZoneRevision
                        + " hurtbox=" + value.HurtboxRevision
                        + " world=" + value.WorldRevision));
            }

            return CharacterTestDeveloperDebugCard.Entity("DamageZone Selection", lines);
        }

        internal static CharacterTestDeveloperDebugCard CreateEngagementAnchorDiagnosticsCard(
            IReadOnlyList<CharacterTestEngagementDiagnosticSnapshot> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
            {
                return CharacterTestDeveloperDebugCard.StatusGrid(
                    "Engagement Anchor",
                    new[]
                    {
                        StatusChip("Selection", "none", true),
                        Text("No translated authored-anchor evaluation is active.")
                    });
            }

            var lines = new List<CharacterTestDeveloperDebugLine>(snapshots.Count * 10);
            for (int i = 0; i < snapshots.Count; i++)
            {
                CharacterTestEngagementDiagnosticSnapshot value = snapshots[i];
                if (i > 0)
                    lines.Add(Text("---"));
                lines.Add(StatusChip("Source", "#" + value.SourceEntityId));
                lines.Add(Row(
                    "Authority",
                    "target=#" + value.TargetEntityId
                        + " actionRev=" + value.ResolvedActionRevision
                        + " frame=" + value.PlannerFrame));
                lines.Add(Row(
                    "Result",
                    value.Status.ToString(),
                    value.Status != CharacterTestEngagementCandidateStatus.Success));
                if (value.Status != CharacterTestEngagementCandidateStatus.Success)
                    continue;
                lines.Add(Row("Anchor", EmptyAsNone(value.AnchorId)));
                lines.Add(Row("DamageZone", EmptyAsNone(value.ZoneId)));
                lines.Add(Row(
                    "CandidatePose",
                    "(" + value.CandidateXMillimeters
                        + ", " + value.CandidateYMillimeters
                        + ", " + value.CandidateZMillimeters
                        + ") mm yaw=" + value.CandidateYawMilliDegrees + " mdeg"));
                lines.Add(Row("Path Cost", value.PathCostMillimeters + " mm"));
                lines.Add(Row(
                    "Next Waypoint",
                    value.HasNextWaypoint
                        ? "(" + value.NextWaypointXMillimeters
                            + ", " + value.NextWaypointYMillimeters
                            + ", " + value.NextWaypointZMillimeters + ") mm"
                        : "candidate pose"));
                lines.Add(Row(
                    "Reservation",
                    value.HasReservation
                        ? value.ReservationCount + "/" + value.Capacity
                            + " slot=" + value.CapacitySlot
                        : value.ReservationCount + "/" + value.Capacity + " not held",
                    !value.HasReservation));
            }
            return CharacterTestDeveloperDebugCard.Entity("Engagement Anchor", lines);
        }

        internal static CharacterTestDeveloperDebugCard CreateJointSelectionDiagnosticsCard(
            IReadOnlyList<CharacterTestRuntimeAiJointDiagnosticSnapshot> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
            {
                return CharacterTestDeveloperDebugCard.StatusGrid(
                    "Runtime AI Joint Selection",
                    new[]
                    {
                        StatusChip("Selection", "none", true),
                        Text("No joint (Target, Action, DamageZone, Anchor) decision was recorded this tick.")
                    });
            }

            var lines = new List<CharacterTestDeveloperDebugLine>(snapshots.Count * 12);
            for (int i = 0; i < snapshots.Count; i++)
            {
                CharacterTestRuntimeAiJointDiagnosticSnapshot value = snapshots[i];
                if (i > 0)
                    lines.Add(Text("---"));
                lines.Add(StatusChip("Source", "#" + value.SourceEntityId));
                lines.Add(Row("Frame", value.PlannerFrame.ToString()));
                lines.Add(Row(
                    "Selection",
                    value.HasSelection
                        ? "target=#" + value.TargetEntityId
                            + " plannerAction=" + value.PlannerActionId
                        : "none",
                    !value.HasSelection));
                if (!value.HasSelection)
                    lines.Add(Row("Failure", value.FailureReason.ToString(), true));
                if (value.HasSelection)
                {
                    lines.Add(Row("Resolved Action", EmptyAsNone(value.ResolvedActionId)));
                    lines.Add(Row("DamageZone", EmptyAsNone(value.ZoneId)));
                    lines.Add(Row("Anchor", EmptyAsNone(value.AnchorId)));
                    lines.Add(Row(
                        "Opportunity",
                        value.IsCompatibility
                            ? "CompatibilityLegacyRange"
                            : value.CurrentPose ? "CurrentPose"
                            : value.TurnInPlace ? "TurnInPlace"
                            : value.ApproachPose ? "ApproachPose"
                            : value.Status.ToString()));
                    lines.Add(Row(
                        "Cost",
                        "utilityMilli=" + value.ActionUtilityMilli
                            + " path=" + FormatBounded(value.PathCostMillimeters)
                            + " mm earliestHitTick=" + FormatBounded(value.EarliestHitTick)));
                    lines.Add(Row("Zone Priority", value.ZonePriority.ToString()));
                }
                CharacterTestRuntimeAiJointGenerationDiagnostics generation = value.Generation;
                lines.Add(Row(
                    "Candidates",
                    "actors=" + generation.RetainedActorCount + "/" + generation.ValidActorCount
                        + " actions=" + generation.ReadyActionCount
                        + " broadphase=" + generation.ActionZoneBroadphaseCount
                        + " joints=" + generation.CandidateCount));
                lines.Add(Row(
                    "Evaluation Cache",
                    "evaluated=" + value.EvaluationCount
                        + " candidatePose=" + value.CandidatePosePreviewCount
                        + " pathQueries=" + value.PathQueryCount
                        + " traceBuilds=" + value.TracePlanBuildCount
                        + " resultHits=" + value.ResultCacheHitCount));
                lines.Add(Row(
                    "Truncation",
                    value.TruncationPhase == CharacterTestRuntimeAiJointTruncationPhase.None
                        ? "none"
                        : value.TruncationPhase
                            + " observed=" + value.TruncationObservedCount
                            + " accepted=" + value.TruncationAcceptedCount
                            + " limit=" + value.TruncationLimit,
                    value.TruncationPhase != CharacterTestRuntimeAiJointTruncationPhase.None));
            }
            return CharacterTestDeveloperDebugCard.Entity("Runtime AI Joint Selection", lines);
        }

        private static string FormatBounded(int value)
        {
            return value == int.MaxValue ? "n/a" : value.ToString();
        }

        private static CharacterTestDeveloperDebugTabSnapshot CreateStoryTab(CharacterTestSliceServices services)
        {
            StoryDirectorSnapshot story = services.StorySnapshot;
            var cards = new List<CharacterTestDeveloperDebugCard>
            {
                CharacterTestDeveloperDebugCard.MetricGrid(
                    "Story Summary",
                    new[]
                    {
                        Metric("Graphs", story.Graphs.Count.ToString()),
                        Metric("Active Beats", story.ActiveBeatInstances.Count.ToString()),
                        Metric("Facts", story.Facts.Count.ToString()),
                        Metric("Events", services.StoryModule.RecentEvents.Count.ToString()),
                        Metric("Errors", services.StoryModule.LastCommandErrors.Count.ToString(), services.StoryModule.LastCommandErrors.Count > 0)
                    }),
                CharacterTestDeveloperDebugCard.Log("Active Beats", FormatBeats(story.ActiveBeatInstances)),
                CharacterTestDeveloperDebugCard.Log("Recent Story Events", FormatStoryEvents(services.StoryModule.RecentEvents))
            };

            return new CharacterTestDeveloperDebugTabSnapshot(CharacterTestDeveloperDebugSnapshot.TabNames[5], cards);
        }

        private static CharacterTestDeveloperDebugTabSnapshot CreateResourcesTab(CharacterTestSliceServices services, ResourceDebugSnapshot resources)
        {
            if (resources == null)
            {
                return new CharacterTestDeveloperDebugTabSnapshot(
                    CharacterTestDeveloperDebugSnapshot.TabNames[6],
                    new[]
                    {
                        CharacterTestDeveloperDebugCard.StatusGrid(
                            "Resources",
                            new[] { StatusChip("snapshot", "unavailable", true) })
                    });
            }

            return new CharacterTestDeveloperDebugTabSnapshot(
                CharacterTestDeveloperDebugSnapshot.TabNames[6],
                new[]
                {
                    CharacterTestDeveloperDebugCard.MetricGrid(
                        "Resources",
                        new[]
                        {
                            StatusChip("Mode", services.Resources.Mode.ToString()),
                            Metric("Catalogs", resources.CatalogCount.ToString()),
                            Metric("Entries", resources.EntryCount.ToString()),
                            Metric("Loaded", resources.LoadedCount.ToString()),
                            Metric("Loading", resources.LoadingCount.ToString(), resources.LoadingCount > 0),
                            Metric("Failed", resources.FailedCount.ToString(), resources.FailedCount > 0),
                            Progress("Load Progress", resources.LoadedCount, Math.Max(1, resources.EntryCount), resources.FailedCount > 0),
                            StatusChip("Recent Errors", resources.RecentErrors.Count.ToString(), resources.RecentErrors.Count > 0)
                        })
                });
        }

        private static ResourceDebugSnapshot TryCreateResourceSnapshot(CharacterTestSliceServices services, IRuntimeLogger logger)
        {
            try
            {
                return services.Resources?.ResourceManager?.CreateDebugSnapshot();
            }
            catch (Exception exception)
            {
                logger?.Warning("CharacterTest.Debug", "Resource debug snapshot failed: " + exception.Message);
                return null;
            }
        }

        private static int CountIssues(CharacterTestSliceServices services, RuntimeHostDiagnostics host, ResourceDebugSnapshot resources)
        {
            int count = 0;
            if (host.Errors.Count > 0)
                count += host.Errors.Count;
            if (services.RuntimeCommands.LastCommandErrors.Count > 0)
                count += services.RuntimeCommands.LastCommandErrors.Count;
            if (services.StoryModule.LastCommandErrors.Count > 0)
                count += services.StoryModule.LastCommandErrors.Count;
            if (resources == null || resources.FailedCount > 0 || resources.RecentErrors.Count > 0)
                count++;
            if (CountOrZero(services.CharacterEntities) == 0)
                count++;
            if (FindLocalPlayer(services.CharacterEntities) == null)
                count++;
            if (services.ActionRuntime == null || !services.ActionRuntime.IsReady)
                count++;
            if (services.ActionRuntime != null)
                count += CountActionConfigErrors(services.ActionRuntime);
            if (services.LastActionReloadResult.HasValue && !services.LastActionReloadResult.Value.Success)
                count++;
            if (IsProceduralEffectPreviewWarning(services))
                count++;

            return count;
        }

        private static int CountActionConfigErrors(CharacterTestCharacterActionRuntime runtime)
        {
            int count = 0;
            IReadOnlyList<CharacterTestCharacterActionConfigSourceDiagnostic> sourceDiagnostics =
                runtime.ConfigSourceDiagnostics;
            for (int i = 0; i < sourceDiagnostics.Count; i++)
            {
                if (sourceDiagnostics[i].Severity == CharacterActionDiagnosticSeverity.Error)
                    count++;
            }

            CharacterActionJsonDiagnostic[] importDiagnostics = runtime.ImportResult.Diagnostics;
            for (int i = 0; i < importDiagnostics.Length; i++)
            {
                if (importDiagnostics[i].Severity == CharacterActionDiagnosticSeverity.Error)
                    count++;
            }

            CharacterActionValidationDiagnostic[] validationDiagnostics = runtime.ValidationReport.Diagnostics;
            for (int i = 0; i < validationDiagnostics.Length; i++)
            {
                if (validationDiagnostics[i].Severity == CharacterActionDiagnosticSeverity.Error)
                    count++;
            }

            return count;
        }

        private static CharacterTestCharacterEntity FindLocalPlayer(IReadOnlyList<CharacterTestCharacterEntity> entities)
        {
            if (entities == null)
                return null;

            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Control.Owner == CharacterTestCharacterControlOwner.LocalPlayer)
                    return entities[i];
            }

            return null;
        }

        private static IReadOnlyList<CharacterTestDeveloperDebugLine> FormatCommands(IReadOnlyList<RuntimeCommand> commands)
        {
            if (commands == null || commands.Count == 0)
                return OneText("none");

            var lines = new List<CharacterTestDeveloperDebugLine>(commands.Count);
            for (int i = 0; i < commands.Count; i++)
            {
                RuntimeCommand command = commands[i];
                lines.Add(LogEntry(
                    "f" + command.Frame
                    + " · " + command.CommandId
                    + " · src=" + command.SourceId
                    + " · tgt=" + command.TargetId));
            }

            return lines;
        }

        private static IReadOnlyList<CharacterTestDeveloperDebugLine> FormatCommandErrors(IReadOnlyList<RuntimeCommandError> errors)
        {
            if (errors == null || errors.Count == 0)
                return OneText("none");

            var lines = new List<CharacterTestDeveloperDebugLine>(errors.Count);
            for (int i = 0; i < errors.Count; i++)
            {
                RuntimeCommandError error = errors[i];
                lines.Add(LogEntry(error.Code + " · f" + error.CommandFrame + " · " + error.Message, true));
            }

            return lines;
        }

        private static IReadOnlyList<CharacterTestDeveloperDebugLine> FormatRuntimeEvents(IReadOnlyList<CharacterTestRuntimeEvent> events)
        {
            if (events == null || events.Count == 0)
                return OneText("none");

            int start = Math.Max(0, events.Count - 20);
            var lines = new List<CharacterTestDeveloperDebugLine>(events.Count - start);
            for (int i = start; i < events.Count; i++)
                lines.Add(LogEntry(events[i].ToString()));
            return lines;
        }

        private static IReadOnlyList<CharacterTestDeveloperDebugLine> FormatTraceEvents(CharacterTestTraceRecorder traceRecorder)
        {
            if (traceRecorder == null)
                return OneText("trace unavailable", true);

            IReadOnlyList<CharacterTestTraceEvent> events = traceRecorder.RecentEvents;
            var lines = new List<CharacterTestDeveloperDebugLine>();
            lines.Add(Text(traceRecorder.WritesFile ? "file: " + traceRecorder.FilePath : "file: disabled"));

            if (events == null || events.Count == 0)
            {
                lines.Add(Text("none"));
                return lines;
            }

            int start = Math.Max(0, events.Count - 12);
            for (int i = start; i < events.Count; i++)
            {
                CharacterTestTraceEvent evt = events[i];
                lines.Add(LogEntry(evt.ToDisplayString(), evt.Warning));
            }

            return lines;
        }

        private static IReadOnlyList<CharacterTestDeveloperDebugLine> FormatInteractions(
            CharacterTestInteractionDebugSnapshot snapshot,
            CharacterTestInteractionPhysicsSummary physicsSummary,
            CharacterTestInteractionVfxSummary vfxSummary,
            CharacterTestInteractionAudioSummary audioSummary,
            CharacterTestPhysicalAudioSummary physicalAudioSummary,
            CharacterTestAudioIntentDrainSummary audioIntentSummary,
            CharacterTestInteractionRenderingSummary renderingSummary)
        {
            snapshot = snapshot ?? CharacterTestInteractionDebugSnapshot.Empty;
            var lines = new List<CharacterTestDeveloperDebugLine>
            {
                Text("count=" + snapshot.Count + " lastFrame=" + snapshot.LastFrameCount),
                Text("stableHash=" + snapshot.StableHash),
                Text("physics=" + physicsSummary.ToSummaryLine()),
                Text("vfx=" + vfxSummary.ToSummaryLine()),
                Text("audio=" + audioSummary.ToSummaryLine()),
                Text("physicalAudio=" + physicalAudioSummary.ToSummaryLine()),
                Text("audioIntent=" + audioIntentSummary.ToSummaryLine()),
                Text("rendering=" + renderingSummary.ToSummaryLine())
            };
            if (!snapshot.TryGetLastEvent(out CharacterTestInteractionEvent last))
            {
                lines.Add(Text("last=none"));
                return lines;
            }

            lines.Add(LogEntry(last.ToSummaryLine()));
            return lines;
        }

        private static IReadOnlyList<CharacterTestDeveloperDebugLine> FormatBeats(IReadOnlyList<StoryBeatInstanceSnapshot> beats)
        {
            if (beats == null || beats.Count == 0)
                return OneText("none", true);

            var lines = new List<CharacterTestDeveloperDebugLine>(beats.Count);
            for (int i = 0; i < beats.Count; i++)
            {
                StoryBeatInstanceSnapshot beat = beats[i];
                lines.Add(LogEntry(
                    beat.BeatId
                    + " · graph=" + beat.GraphId
                    + " · step=" + beat.CurrentStepIndex
                    + " · inst=" + beat.BeatInstanceId));
            }

            return lines;
        }

        private static IReadOnlyList<CharacterTestDeveloperDebugLine> FormatStoryEvents(IReadOnlyList<StoryRuntimeEvent> events)
        {
            if (events == null || events.Count == 0)
                return OneText("none");

            int start = Math.Max(0, events.Count - 20);
            var lines = new List<CharacterTestDeveloperDebugLine>(events.Count - start);
            for (int i = start; i < events.Count; i++)
            {
                StoryRuntimeEvent evt = events[i];
                lines.Add(LogEntry(
                    "f" + evt.Frame
                    + " · " + evt.Kind
                    + " · " + evt.BeatId
                    + " · graph=" + evt.GraphId));
            }

            return lines;
        }

        private static string FormatTargetLock(CharacterTestSliceServices services)
        {
            return services.TargetLockEnabled
                ? "locked entity=" + services.LockedTargetEntityId
                    + " hurtbox="
                    + (services.LockedCombatTarget.HasTarget
                        ? services.LockedCombatTarget.LogicalHurtboxId
                        : "none")
                : "off";
        }

        private static string FormatProceduralEffectPreview(CharacterTestSliceServices services)
        {
            CharacterTestProceduralEffectPreviewResult result = services.LastProceduralEffectPreviewResult;
            if (!result.HasResult)
                return "none";

            return EmptyAsNone(result.TriggerName)
                + " "
                + result.EffectId
                + " target="
                + result.TargetEntityId
                + " "
                + (result.Accepted ? "ok" : "rejected")
                + " activeSecondary="
                + result.Snapshot.ActiveSecondaryCount
                + " regions="
                + result.Snapshot.KnownRegionCount
                + " lastRegion="
                + EmptyAsNone(result.Snapshot.LastResolvedRegionId)
                + " source="
                + EmptyAsNone(result.Snapshot.LastMatchSource)
                + (result.Snapshot.LastIssue != CharacterAuthorityPoseIssue.None
                    ? " issue=" + result.Snapshot.LastIssue
                    : string.Empty)
                + " "
                + EmptyAsNone(result.Message);
        }

        private static bool IsProceduralEffectPreviewWarning(CharacterTestSliceServices services)
        {
            CharacterTestProceduralEffectPreviewResult result = services.LastProceduralEffectPreviewResult;
            return result.HasResult && !result.Accepted;
        }

        private static string FormatProceduralHitFeedback(CharacterTestSliceServices services)
        {
            CharacterTestProceduralHitFeedbackResult result = services.LastProceduralHitFeedbackResult;
            if (!result.HasResult)
                return services.ProceduralHitFeedbackEnabled ? "waiting" : "off";

            return "f"
                + result.Frame.Value
                + " target="
                + result.TargetEntityId
                + " "
                + (result.Accepted ? "ok" : "rejected")
                + " region="
                + EmptyAsNone(result.RegionId)
                + " source="
                + EmptyAsNone(result.MatchSource)
                + " activeSecondary="
                + result.Snapshot.ActiveSecondaryCount
                + " regions="
                + result.Snapshot.KnownRegionCount
                + (!string.IsNullOrEmpty(result.FallbackIssue)
                    ? " issue=" + result.FallbackIssue
                    : string.Empty);
        }

        private static bool IsProceduralHitFeedbackWarning(CharacterTestSliceServices services)
        {
            CharacterTestProceduralHitFeedbackResult result = services.LastProceduralHitFeedbackResult;
            return result.HasResult
                && (!result.Accepted || !string.IsNullOrEmpty(result.FallbackIssue));
        }

        private static string FormatActionConfigSource(CharacterTestCharacterActionRuntime runtime)
        {
            if (runtime == null)
                return "missing";

            return runtime.ConfigSourceKind + (runtime.IsReady ? " ready" : " blocked");
        }

        private static string FormatMovementProfile(CharacterTestCharacterMovementProfileRuntime runtime)
        {
            return EmptyAsNone(runtime.StableId)
                + (runtime.UsedFallback ? " fallback" : string.Empty);
        }

        private static string FormatMotionShort(CharacterTestCharacterEntity entity)
        {
            return (entity.IsGrounded ? "grounded" : "airborne")
                + " mode=" + entity.MovementMode
                + " scale=" + entity.MovementControlScale.ToString("F2") + "/" + entity.RotationControlScale.ToString("F2")
                + " v=" + entity.HorizontalVelocity.magnitude.ToString("F1")
                + " vy=" + entity.VerticalVelocity.ToString("F1")
                + " phys=" + (entity.PhysicsEnabled ? entity.CollisionFlags.ToString() : "planar")
                + " surface=" + entity.SurfaceMaterialStableId
                + " fric=" + entity.SurfaceFriction.ToString("F1")
                + " elas=" + entity.SurfaceElasticity.ToString("F1")
                + " bounceH=" + entity.SurfaceFixedBounceHeight.ToString("F1")
                + " mass=" + entity.Physics.EffectiveMassKg.ToString("F0")
                + " r=" + entity.Physics.EffectiveRadiusMeters.ToString("F2")
                + " contact=" + entity.Physics.ContactCount
                + " intents=" + entity.PendingGameplayIntentCount
                + " coy=" + entity.CoyoteTimeSecondsRemaining.ToString("F2");
        }

        private static string FormatIntentSeconds(CharacterTestCharacterEntity entity, long frame)
        {
            if (entity == null)
                return "0.00";

            return CharacterTestDeterministicUnits.FramesToSeconds(
                entity.GameplayIntentBuffer.GetRemainingFrames(
                    CharacterTestGameplayIntentKind.Jump,
                    frame)).ToString("F2");
        }

        private static string FormatSurfaceMaterial(CharacterTestCharacterEntity entity)
        {
            return entity.SurfaceMaterialStableId
                + " kind="
                + entity.SurfaceMaterialKind
                + " e="
                + entity.SurfaceElasticity.ToString("F2")
                + " bounceH="
                + entity.SurfaceFixedBounceHeight.ToString("F2");
        }

        private static IReadOnlyList<CharacterTestDeveloperDebugLine> FormatActionConfigDiagnostics(
            CharacterTestCharacterActionRuntime runtime,
            CharacterTestCharacterActionReloadResult? reloadResult)
        {
            if (runtime == null)
                return OneText("Action runtime missing.", true);

            var lines = new List<CharacterTestDeveloperDebugLine>
            {
                LogEntry("source=" + runtime.ConfigSourceKind + " · " + runtime.ConfigSourceDescription, !runtime.IsReady),
                LogEntry("actionSets=" + runtime.ActionSetsSourcePath),
                LogEntry("actions=" + runtime.ActionsSourcePath),
                LogEntry("importSuccess=" + runtime.ImportResult.Success + " validationSuccess=" + runtime.ValidationReport.Success, !runtime.IsReady),
                LogEntry("lastReload=" + FormatActionReloadDetail(reloadResult), reloadResult.HasValue && !reloadResult.Value.Success)
            };

            IReadOnlyList<CharacterTestCharacterActionConfigSourceDiagnostic> sourceDiagnostics =
                runtime.ConfigSourceDiagnostics;
            for (int i = 0; i < sourceDiagnostics.Count; i++)
            {
                CharacterTestCharacterActionConfigSourceDiagnostic diagnostic = sourceDiagnostics[i];
                lines.Add(LogEntry("source " + diagnostic, diagnostic.Severity == CharacterActionDiagnosticSeverity.Error));
            }

            CharacterActionJsonDiagnostic[] importDiagnostics = runtime.ImportResult.Diagnostics;
            for (int i = 0; i < importDiagnostics.Length; i++)
                lines.Add(LogEntry("import " + importDiagnostics[i], importDiagnostics[i].Severity == CharacterActionDiagnosticSeverity.Error));

            CharacterActionValidationDiagnostic[] validationDiagnostics = runtime.ValidationReport.Diagnostics;
            for (int i = 0; i < validationDiagnostics.Length; i++)
                lines.Add(LogEntry("validation " + validationDiagnostics[i], validationDiagnostics[i].Severity == CharacterActionDiagnosticSeverity.Error));

            return lines;
        }

        private static string FormatActionReload(CharacterTestCharacterActionReloadResult? result)
        {
            if (!result.HasValue)
                return "not requested";

            CharacterTestCharacterActionReloadResult value = result.Value;
            return (value.Success ? "ok" : "failed") + " f" + value.Frame.Value;
        }

        private static string FormatActionReloadDetail(CharacterTestCharacterActionReloadResult? result)
        {
            if (!result.HasValue)
                return "not requested";

            CharacterTestCharacterActionReloadResult value = result.Value;
            return (value.Success ? "success" : "failed")
                + " frame=" + value.Frame.Value
                + " imports=" + value.ImportDiagnosticCount
                + " validations=" + value.ValidationDiagnosticCount
                + " source=" + EmptyAsNone(value.SourceDescription)
                + " message=" + EmptyAsNone(value.Message);
        }

        private static string FormatModules(RuntimeHostDiagnostics host)
        {
            if (host.Modules == null || host.Modules.Count == 0)
                return "none";

            var builder = new StringBuilder();
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

            return builder.ToString();
        }

        private static string FormatAnimation(CharacterTestCharacterEntity entity)
        {
            return "Anim blend " + Vec2(entity.Animation.LocomotionBlend)
                + " · speed " + entity.Animation.LocomotionSpeed01.ToString("F2")
                + " · airborne " + Bool(entity.Animation.Airborne)
                + " · actionKey " + EmptyAsNone(entity.Animation.ActionKey);
        }

        private static string FormatAnimationShort(CharacterTestCharacterEntity entity)
        {
            return "spd " + entity.Animation.LocomotionSpeed01.ToString("F2")
                + (entity.Animation.Airborne ? " · air" : string.Empty)
                + (!string.IsNullOrEmpty(entity.Animation.ActionKey) ? " · " + entity.Animation.ActionKey : string.Empty);
        }

        private static string FormatAction(CharacterTestCharacterEntity entity)
        {
            return "Action " + EmptyAsNone(entity.Action.ActiveActionId)
                + " · state " + CharacterTestCharacterActionComponent.ResolveStateText(entity.Action.State)
                + " · phase " + CharacterTestCharacterActionComponent.ResolvePhaseText(entity.Action.Phase)
                + " · frame " + entity.Action.LocalFrame
                + " · reject " + EmptyAsNone(entity.Action.LastRejectReason)
                + " · animKey " + EmptyAsNone(entity.Action.LastAnimationActionKey);
        }

        private static string FormatCombatState(CharacterTestCharacterEntity entity)
        {
            CharacterTestCharacterCombatStateComponent combat = entity.CombatState;
            return "Combat hp " + combat.Health + "/" + combat.MaxHealth
                + " · scatterPressure " + combat.ScatterPressure + "/" + combat.MaxScatterPressure
                + " " + combat.ScatterPressureBand
                + " · cohesionBreak f" + combat.LastCohesionBreakFrame
                + " · stagger " + combat.HitStunFramesRemaining
                + " · lastHit f" + combat.LastHitFrame
                + " dmg=" + combat.LastDamage
                + " src=" + combat.LastHitSourceEntityId
                + " trace=" + combat.LastHitTraceId;
        }

        private static string FormatActionShort(CharacterTestCharacterEntity entity)
        {
            return EmptyAsNone(entity.Action.ActiveActionId)
                + " " + CharacterTestCharacterActionComponent.ResolvePhaseText(entity.Action.Phase)
                + " f" + entity.Action.LocalFrame
                + " reject=" + EmptyAsNone(entity.Action.LastRejectReason);
        }

        private static string FormatCollapsedAction(CharacterTestCharacterEntity entity)
        {
            if (entity == null)
                return "n/a";

            string action = EmptyAsNone(entity.Action.ActiveActionId);
            if (!string.IsNullOrEmpty(entity.Action.LastRejectReason))
                action += " reject=" + entity.Action.LastRejectReason;
            return action;
        }

        private static string EmptyAsNone(string value)
        {
            return string.IsNullOrEmpty(value) ? "none" : value;
        }

        private static string EmptyAsNone(CharacterActionId value)
        {
            return value.IsValid ? value.ToString() : "none";
        }

        private static string EmptyAsNone(int value)
        {
            return value <= 0 ? "none" : value.ToString();
        }

        private static string EmptyAsNone(CharacterAuthorityPoseMatchSource value)
        {
            return value == CharacterAuthorityPoseMatchSource.None ? "none" : value.ToString();
        }

        private static CharacterTestDeveloperDebugLine[] ToStatusLines(List<CharacterTestDeveloperDebugLine> rows)
        {
            var chips = new CharacterTestDeveloperDebugLine[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                CharacterTestDeveloperDebugLine row = rows[i];
                chips[i] = StatusChip(row.Name, row.Value, row.Warning);
            }

            return chips;
        }

        private static IReadOnlyList<CharacterTestDeveloperDebugLine> OneText(string value, bool warning = false)
        {
            return new[] { LogEntry(value, warning) };
        }

        private static CharacterTestDeveloperDebugLine Row(string name, string value, bool warning = false)
        {
            return CharacterTestDeveloperDebugLine.Row(name, value, warning);
        }

        private static CharacterTestDeveloperDebugLine Text(string value, bool warning = false)
        {
            return CharacterTestDeveloperDebugLine.Text(value, warning);
        }

        private static CharacterTestDeveloperDebugLine Metric(string label, string value, bool warning = false)
        {
            return CharacterTestDeveloperDebugLine.Metric(label, value, warning);
        }

        private static CharacterTestDeveloperDebugLine StatusChip(string label, string value, bool warning = false)
        {
            return CharacterTestDeveloperDebugLine.StatusChip(label, value, warning);
        }

        private static CharacterTestDeveloperDebugLine Bool(string label, bool on)
        {
            return CharacterTestDeveloperDebugLine.Bool(label, on);
        }

        private static CharacterTestDeveloperDebugLine Vector2(string label, float x, float y, bool warning = false)
        {
            return CharacterTestDeveloperDebugLine.Vector2(label, x, y, warning);
        }

        private static CharacterTestDeveloperDebugLine Progress(string label, float current, float max, bool warning = false)
        {
            return CharacterTestDeveloperDebugLine.Progress(label, current, max, warning);
        }

        private static CharacterTestDeveloperDebugLine LogEntry(string message, bool warning = false)
        {
            return CharacterTestDeveloperDebugLine.LogEntry(message, warning);
        }

        private static CharacterTestDeveloperDebugLine FlowStep(string step, string detail, bool ok)
        {
            return CharacterTestDeveloperDebugLine.FlowStep(step, detail, ok);
        }

        private static string Button(bool pressed, bool held, bool released)
        {
            return "pressed=" + Bool(pressed) + " held=" + Bool(held) + " released=" + Bool(released);
        }

        private static string Vec2(Vector2 value)
        {
            return "(" + value.x.ToString("F2") + ", " + value.y.ToString("F2") + ")";
        }

        private static string Vec3(Vector3 value)
        {
            return "(" + value.x.ToString("F2") + ", " + value.y.ToString("F2") + ", " + value.z.ToString("F2") + ")";
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        private static int CountOrZero<T>(IReadOnlyList<T> values)
        {
            return values != null ? values.Count : 0;
        }
    }
}
