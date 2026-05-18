using System;
using System.Collections.Generic;
using MxFramework.Animation;
using MxFramework.CharacterControl;
using MxFramework.CharacterControl.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterControl
{
    public sealed class CharacterAnimationPresentationTests
    {
        [Test]
        public void LocomotionBlend2D_QuantizesDirectionAndSpeedDiagnostics()
        {
            var backend = new RecordingAnimationBackend();
            var controller = new CharacterAnimationPresentationController(
                backend,
                new CharacterAnimationPresentationOptions
                {
                    TargetActorId = "actor-1",
                    LocomotionBlendMode = CharacterAnimationLocomotionBlendMode.Blend2D,
                    LocomotionBlend2DId = "locomotion.direction"
                });

            CharacterAnimationPresentationResult result = controller.ApplyLocomotion(
                CreateMotionResult(Forward(), Fix64.FromRatio(3, 2)));

            Assert.IsTrue(result.Success, result.Diagnostic.Message);
            Assert.IsTrue(result.BackendResult.Success, result.BackendResult.Message);
            Assert.AreEqual(1, backend.Blend2DRequests.Count);
            Assert.AreEqual("actor-1", backend.Blend2DRequests[0].TargetActorId);
            Assert.AreEqual("locomotion.direction", backend.Blend2DRequests[0].BlendId);
            Assert.AreEqual(0, backend.Blend2DRequests[0].ParameterX.QuantizedValue);
            Assert.AreEqual(1000, backend.Blend2DRequests[0].ParameterY.QuantizedValue);

            CharacterAnimationPresentationDiagnosticSnapshot snapshot = controller.CreateSnapshot();
            Assert.AreEqual(CharacterAnimationPresentationEventKind.LocomotionBlend2D, snapshot.LastEntry.EventKind);
            Assert.AreEqual(1500, snapshot.LastEntry.SpeedParameter);
            Assert.AreEqual(0, snapshot.LastEntry.DirectionXParameter);
            Assert.AreEqual(1000, snapshot.LastEntry.DirectionYParameter);
        }

        [Test]
        public void LocomotionBlend1D_QuantizesMoveSpeedScale()
        {
            var backend = new RecordingAnimationBackend();
            var controller = new CharacterAnimationPresentationController(
                backend,
                new CharacterAnimationPresentationOptions
                {
                    LocomotionBlendMode = CharacterAnimationLocomotionBlendMode.Blend1D,
                    LocomotionBlend1DId = "locomotion.speed"
                });

            CharacterAnimationPresentationResult result = controller.ApplyLocomotion(
                CreateMotionResult(Forward(), Fix64.Half));

            Assert.IsTrue(result.Success, result.Diagnostic.Message);
            Assert.IsTrue(result.BackendResult.Success, result.BackendResult.Message);
            Assert.AreEqual(1, backend.Blend1DRequests.Count);
            Assert.AreEqual("locomotion.speed", backend.Blend1DRequests[0].BlendId);
            Assert.AreEqual("locomotion.speed", backend.Blend1DRequests[0].Parameter.ParameterId);
            Assert.AreEqual(500, backend.Blend1DRequests[0].Parameter.QuantizedValue);
        }

        [Test]
        public void LocomotionBlend1D_UsesAnalogMoveMagnitude()
        {
            var backend = new RecordingAnimationBackend();
            var controller = new CharacterAnimationPresentationController(
                backend,
                new CharacterAnimationPresentationOptions
                {
                    LocomotionBlendMode = CharacterAnimationLocomotionBlendMode.Blend1D,
                    LocomotionBlend1DId = "locomotion.speed"
                });

            CharacterAnimationPresentationResult result = controller.ApplyLocomotion(
                CreateMotionResult(new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.Half), Fix64.One));

            Assert.IsTrue(result.Success, result.Diagnostic.Message);
            Assert.AreEqual(500, backend.Blend1DRequests[0].Parameter.QuantizedValue);
            Assert.AreEqual(500, controller.CreateSnapshot().LastEntry.SpeedParameter);
        }

        [Test]
        public void LocomotionBlend2D_AirborneZerosParametersByDefault()
        {
            var backend = new RecordingAnimationBackend();
            var controller = new CharacterAnimationPresentationController(
                backend,
                new CharacterAnimationPresentationOptions
                {
                    LocomotionBlendMode = CharacterAnimationLocomotionBlendMode.Blend2D
                });

            CharacterAnimationPresentationResult result = controller.ApplyLocomotion(
                CreateMotionResult(Forward(), Fix64.One, grounded: false));

            Assert.IsTrue(result.Success, result.Diagnostic.Message);
            Assert.AreEqual(1, backend.Blend2DRequests.Count);
            Assert.AreEqual(0, backend.Blend2DRequests[0].ParameterX.QuantizedValue);
            Assert.AreEqual(0, backend.Blend2DRequests[0].ParameterY.QuantizedValue);
            Assert.AreEqual(0, controller.CreateSnapshot().LastEntry.SpeedParameter);
        }

        [Test]
        public void ReactionState_CrossFadesConfiguredBinding()
        {
            var backend = new RecordingAnimationBackend();
            ResourceKey clip = ClipKey("clip.reaction.pressure");
            var controller = new CharacterAnimationPresentationController(
                backend,
                new CharacterAnimationPresentationOptions
                {
                    TargetActorId = "actor-2",
                    ReactionBindings =
                    {
                        new CharacterAnimationReactionBinding
                        {
                            Reason = CharacterControlTransitionReason.PressureBreak,
                            RequestKind = CharacterAnimationReactionRequestKind.CrossFade,
                            BindingId = "reaction.pressure",
                            ClipKey = clip,
                            FadeDurationSeconds = 0.2f
                        }
                    }
                });

            CharacterAnimationPresentationResult result = controller.ApplyStateChanged(new CharacterStateChangedEvent(
                CreateEntity(),
                CharacterControlState.Locomotion,
                CharacterControlState.Reaction,
                CharacterControlTransitionReason.PressureBreak,
                new RuntimeFrame(8),
                RuntimeFrame.Zero,
                version: 1,
                CharacterControlLockMask.None,
                CharacterControlLockMask.Action,
                "pressure"));

            Assert.IsTrue(result.Success, result.Diagnostic.Message);
            Assert.IsTrue(result.BackendResult.Success, result.BackendResult.Message);
            Assert.AreEqual(1, backend.CrossFades.Count);
            Assert.AreEqual("actor-2", backend.CrossFades[0].TargetActorId);
            Assert.AreEqual("reaction.pressure", backend.CrossFades[0].BindingId);
            Assert.AreEqual(clip, backend.CrossFades[0].ClipKey);
            Assert.AreEqual(0.2f, backend.CrossFades[0].FadeDurationSeconds);
            Assert.AreEqual(CharacterAnimationPresentationEventKind.ReactionCrossFade, result.EventKind);
        }

        [Test]
        public void ReactionState_PlayBindingUsesBackendPlay()
        {
            var backend = new RecordingAnimationBackend();
            ResourceKey clip = ClipKey("clip.reaction.guard");
            var controller = new CharacterAnimationPresentationController(
                backend,
                new CharacterAnimationPresentationOptions
                {
                    ReactionBindings =
                    {
                        new CharacterAnimationReactionBinding
                        {
                            Reason = CharacterControlTransitionReason.GuardBreak,
                            RequestKind = CharacterAnimationReactionRequestKind.Play,
                            BindingId = "reaction.guard",
                            ClipKey = clip
                        }
                    }
                });

            CharacterAnimationPresentationResult result = controller.ApplyStateChanged(CreateReactionEvent(
                CharacterControlTransitionReason.GuardBreak));

            Assert.IsTrue(result.Success, result.Diagnostic.Message);
            Assert.AreEqual(CharacterAnimationPresentationEventKind.ReactionPlay, result.EventKind);
            Assert.AreEqual(1, backend.Plays.Count);
            Assert.AreEqual(0, backend.CrossFades.Count);
            Assert.AreEqual(clip, backend.Plays[0].ClipKey);
        }

        [Test]
        public void ReactionState_FallbackBindingRecordsFallbackReason()
        {
            var backend = new RecordingAnimationBackend();
            var controller = new CharacterAnimationPresentationController(
                backend,
                new CharacterAnimationPresentationOptions
                {
                    ReactionBindings =
                    {
                        new CharacterAnimationReactionBinding
                        {
                            Reason = CharacterControlTransitionReason.None,
                            BindingId = "reaction.fallback",
                            ClipKey = ClipKey("clip.reaction.fallback")
                        }
                    }
                });

            CharacterAnimationPresentationResult result = controller.ApplyStateChanged(CreateReactionEvent(
                CharacterControlTransitionReason.ArmorBreak));

            Assert.IsTrue(result.Success, result.Diagnostic.Message);
            Assert.AreEqual("reaction.fallback", backend.CrossFades[0].BindingId);
            StringAssert.Contains("Using fallback reaction animation binding", result.Diagnostic.Message);
        }

        [Test]
        public void ReactionState_BackendFailurePreservesResourceDiagnostics()
        {
            ResourceKey clip = ClipKey("clip.reaction.missing");
            var error = new ResourceError(
                ResourceErrorCode.NotFound,
                clip,
                providerId: "memory",
                message: "clip missing");
            var backend = new RecordingAnimationBackend
            {
                NextCrossFadeResult = MxAnimationBackendResult.Failed(
                    MxAnimationBackendResultCode.LoadFailed,
                    clip,
                    error,
                    "load failed")
            };
            var controller = new CharacterAnimationPresentationController(
                backend,
                new CharacterAnimationPresentationOptions
                {
                    ReactionBindings =
                    {
                        new CharacterAnimationReactionBinding
                        {
                            Reason = CharacterControlTransitionReason.PressureBreak,
                            BindingId = "reaction.pressure",
                            ClipKey = clip
                        }
                    }
                });

            CharacterAnimationPresentationResult result = controller.ApplyStateChanged(CreateReactionEvent(
                CharacterControlTransitionReason.PressureBreak));
            CharacterAnimationPresentationDiagnosticEntry entry = controller.CreateSnapshot().LastEntry;

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CharacterAnimationPresentationEventKind.BackendRejected, result.EventKind);
            Assert.AreEqual(clip, entry.BackendClipKey);
            Assert.AreEqual(ResourceErrorCode.NotFound, entry.BackendResourceError.Code);
            Assert.AreEqual("memory", entry.BackendResourceError.ProviderId);
        }

        [Test]
        public void ReactionState_MissingBindingRecordsDiagnosticWithoutBackendRequest()
        {
            var backend = new RecordingAnimationBackend();
            var controller = new CharacterAnimationPresentationController(backend);

            CharacterAnimationPresentationResult result = controller.ApplyStateChanged(new CharacterStateChangedEvent(
                CreateEntity(),
                CharacterControlState.Locomotion,
                CharacterControlState.Reaction,
                CharacterControlTransitionReason.GuardBreak,
                new RuntimeFrame(3),
                RuntimeFrame.Zero,
                version: 1,
                CharacterControlLockMask.None,
                CharacterControlLockMask.Action,
                "guard"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CharacterAnimationPresentationEventKind.MissingReactionBinding, result.EventKind);
            Assert.AreEqual(0, backend.CrossFades.Count);
            Assert.AreEqual(0, backend.Plays.Count);
            Assert.AreEqual("Missing reaction animation binding.", controller.CreateSnapshot().LastEntry.Message);
        }

        [Test]
        public void ActionEvent_IsRecordedAsExternalBridgeWithoutBackendRequest()
        {
            var backend = new RecordingAnimationBackend();
            var controller = new CharacterAnimationPresentationController(backend);

            CharacterAnimationPresentationResult result = controller.RecordActionEvent(new CharacterActionEvent(
                CharacterActionEventType.Started,
                CharacterActionRequest.CombatAction(
                    new RuntimeFrame(5),
                    CreateEntity(),
                    CharacterActionKind.Attack,
                    combatActionId: 1001),
                CharacterActionRejectedReason.None,
                actionInstanceId: 1,
                default,
                string.Empty));

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Skipped);
            Assert.AreEqual(CharacterAnimationPresentationEventKind.ActionHandledByCombatBridge, result.EventKind);
            Assert.AreEqual(0, backend.CrossFades.Count);
            Assert.AreEqual(0, backend.Plays.Count);
            Assert.AreEqual(0, backend.Stops.Count);
        }

        [Test]
        public void ActionEvent_RejectedIsSkippedWithoutClaimingCombatBridgeOwnership()
        {
            var backend = new RecordingAnimationBackend();
            var controller = new CharacterAnimationPresentationController(backend);

            CharacterAnimationPresentationResult result = controller.RecordActionEvent(new CharacterActionEvent(
                CharacterActionEventType.Rejected,
                CharacterActionRequest.CombatAction(
                    new RuntimeFrame(5),
                    CreateEntity(),
                    CharacterActionKind.Attack,
                    combatActionId: 1001),
                CharacterActionRejectedReason.Busy,
                actionInstanceId: 0,
                default,
                "busy"));

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Skipped);
            Assert.AreEqual(CharacterAnimationPresentationEventKind.Skipped, result.EventKind);
            StringAssert.Contains("not owned by the Combat to MxAnimation bridge", result.Diagnostic.Message);
            Assert.AreEqual(0, backend.CrossFades.Count);
        }

        [Test]
        public void MissingBackendRecordsRequestKind()
        {
            var controller = new CharacterAnimationPresentationController(
                null,
                new CharacterAnimationPresentationOptions
                {
                    LocomotionBlendMode = CharacterAnimationLocomotionBlendMode.Blend2D
                });

            CharacterAnimationPresentationResult result = controller.ApplyLocomotion(
                CreateMotionResult(Forward(), Fix64.One));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CharacterAnimationPresentationEventKind.MissingBackend, result.EventKind);
            Assert.AreEqual(MxAnimationRequestKind.SetBlend2D, result.Diagnostic.RequestKind);
        }

        private static CharacterMotionResult CreateMotionResult(
            FixVector3 moveDirection,
            Fix64 moveSpeedScale,
            bool grounded = true)
        {
            CharacterControlEntityRef entity = CreateEntity();
            var command = new CharacterCommand(
                new RuntimeFrame(12),
                sourceId: 1,
                entity,
                moveDirection,
                CharacterFacingBasis.Identity,
                jumpPressed: false,
                sprintHeld: false,
                CharacterActionButtons.None,
                default,
                traceId: "locomotion");
            var input = new CombatMotionInput(moveDirection, jumpPressed: false, moveSpeedScale);
            var state = new CombatMotionState(
                new CombatFrame(12),
                FixVector3.Zero,
                FixVector3.Zero,
                grounded: grounded,
                lastCollisionNormal: FixVector3.Zero,
                grounded ? CombatMotionCollisionFlags.Grounded : CombatMotionCollisionFlags.None);
            var step = new CombatMotionStepResult(
                state,
                FixVector3.Zero,
                FixVector3.Zero,
                jumpStarted: false,
                grounded ? CombatMotionCollisionFlags.Grounded : CombatMotionCollisionFlags.None,
                Array.Empty<CombatMotionCollision>());

            return new CharacterMotionResult(
                command,
                CharacterControlState.Locomotion,
                CharacterControlLockMask.None,
                input,
                step,
                default,
                worldSynced: false,
                worldRevision: 0);
        }

        private static CharacterControlEntityRef CreateEntity()
        {
            return CharacterControlEntityRef.FromGameplayAndCombat(
                new GameplayEntityId(1, 1),
                new CombatEntityId(10),
                new CombatBodyId(10),
                stableId: 1);
        }

        private static ResourceKey ClipKey(string id)
        {
            return new ResourceKey(id, ResourceTypeIds.AnimationClip);
        }

        private static CharacterStateChangedEvent CreateReactionEvent(CharacterControlTransitionReason reason)
        {
            return new CharacterStateChangedEvent(
                CreateEntity(),
                CharacterControlState.Locomotion,
                CharacterControlState.Reaction,
                reason,
                new RuntimeFrame(8),
                RuntimeFrame.Zero,
                version: 1,
                CharacterControlLockMask.None,
                CharacterControlLockMask.Action,
                "reaction");
        }

        private static FixVector3 Forward()
        {
            return new FixVector3(Fix64.Zero, Fix64.Zero, Fix64.One);
        }

        private sealed class RecordingAnimationBackend : IMxAnimationBackend
        {
            public readonly List<MxAnimationPlayRequest> Plays = new List<MxAnimationPlayRequest>();
            public readonly List<MxAnimationStopRequest> Stops = new List<MxAnimationStopRequest>();
            public readonly List<MxAnimationCrossFadeRequest> CrossFades = new List<MxAnimationCrossFadeRequest>();
            public readonly List<MxAnimationBlend1DRequest> Blend1DRequests = new List<MxAnimationBlend1DRequest>();
            public readonly List<MxAnimationBlend2DRequest> Blend2DRequests = new List<MxAnimationBlend2DRequest>();

            public MxAnimationBackendResult NextPlayResult { get; set; }

            public MxAnimationBackendResult NextCrossFadeResult { get; set; }

            public string BackendName => "Recording";

            public MxAnimationBackendResult Play(MxAnimationPlayRequest request)
            {
                Plays.Add(request);
                if (NextPlayResult.Success
                    || NextPlayResult.Code != MxAnimationBackendResultCode.Success
                    || !string.IsNullOrEmpty(NextPlayResult.Message))
                {
                    MxAnimationBackendResult result = NextPlayResult;
                    NextPlayResult = default;
                    return result;
                }

                return MxAnimationBackendResult.Succeeded(request != null ? request.ClipKey : default, "Recorded play.");
            }

            public MxAnimationBackendResult Stop(MxAnimationStopRequest request)
            {
                Stops.Add(request);
                return MxAnimationBackendResult.Succeeded(default, "Recorded stop.");
            }

            public MxAnimationBackendResult CrossFade(MxAnimationCrossFadeRequest request)
            {
                CrossFades.Add(request);
                if (NextCrossFadeResult.Success
                    || NextCrossFadeResult.Code != MxAnimationBackendResultCode.Success
                    || !string.IsNullOrEmpty(NextCrossFadeResult.Message))
                {
                    MxAnimationBackendResult result = NextCrossFadeResult;
                    NextCrossFadeResult = default;
                    return result;
                }

                return MxAnimationBackendResult.Succeeded(request != null ? request.ClipKey : default, "Recorded crossfade.");
            }

            public MxAnimationBackendResult SetLayerWeight(MxAnimationLayerWeightRequest request)
            {
                return MxAnimationBackendResult.Succeeded(default, "Recorded layer weight.");
            }

            public MxAnimationBackendResult SetBlend1D(MxAnimationBlend1DRequest request)
            {
                Blend1DRequests.Add(request);
                return MxAnimationBackendResult.Succeeded(default, "Recorded 1D blend.");
            }

            public MxAnimationBackendResult SetBlend2D(MxAnimationBlend2DRequest request)
            {
                Blend2DRequests.Add(request);
                return MxAnimationBackendResult.Succeeded(default, "Recorded 2D blend.");
            }

            public void Tick(float deltaTime)
            {
            }

            public MxAnimationDiagnosticSnapshot CreateSnapshot()
            {
                return new MxAnimationDiagnosticSnapshot(
                    BackendName,
                    string.Empty,
                    string.Empty,
                    1,
                    graphIsValid: false,
                    isReleased: false,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
            }

            public void Release()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
