using MxFramework.Animation;
using MxFramework.CharacterControl;
using MxFramework.CharacterControl.Animation;
using MxFramework.Combat.Core;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Resources;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterControl
{
    public sealed class CharacterControlMxAnimationAdapterTests
    {
        private static readonly ResourceKey Idle = Clip("test.animation.idle");
        private static readonly ResourceKey Reaction = Clip("test.animation.reaction.guard_break");

        [Test]
        public void LocomotionBlend1D_QuantizesSpeedRatio()
        {
            CharacterControlEntityRef entity = CreateEntity();
            var backend = new RecordingAnimationBackend();
            var adapter = new CharacterControlMxAnimationAdapter(new CharacterControlMxAnimationAdapterOptions
            {
                LocomotionMode = CharacterControlAnimationLocomotionMode.Blend1D
            });
            adapter.RegisterActor(entity, backend, CreateAnimationSet(includeBlend1D: true, includeBlend2D: false));

            CharacterControlAnimationAdapterResult result = adapter.ApplyLocomotion(new CharacterControlAnimationLocomotionSample(
                entity,
                new RuntimeFrame(12),
                Fix64.FromRatio(3, 2),
                Fix64.Half,
                -Fix64.Half,
                grounded: true));

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(CharacterControlAnimationRequestKind.LocomotionBlend1D, result.Request.Kind);
            Assert.AreEqual("locomotion.speed", result.Request.BlendId);
            Assert.AreEqual("locomotion.speed", result.Request.Parameter.ParameterId);
            Assert.AreEqual(1500, result.Request.Parameter.QuantizedValue);
            Assert.AreEqual(1, backend.SetBlend1DCalls);
            Assert.AreEqual(0, backend.SetBlend2DCalls);
        }

        [Test]
        public void LocomotionBlend2D_QuantizesDirectionAndUsesAirborneBlend()
        {
            CharacterControlEntityRef entity = CreateEntity();
            var backend = new RecordingAnimationBackend();
            var adapter = new CharacterControlMxAnimationAdapter();
            adapter.RegisterActor(entity, backend, CreateAnimationSet(includeBlend1D: true, includeBlend2D: true));

            CharacterControlAnimationAdapterResult result = adapter.ApplyLocomotion(new CharacterControlAnimationLocomotionSample(
                entity,
                new RuntimeFrame(20),
                Fix64.Half,
                Fix64.One,
                -Fix64.Half,
                grounded: false));

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(CharacterControlAnimationRequestKind.LocomotionBlend2D, result.Request.Kind);
            Assert.AreEqual("locomotion.air.direction", result.Request.BlendId);
            Assert.AreEqual("locomotion.x", result.Request.ParameterX.ParameterId);
            Assert.AreEqual(500, result.Request.ParameterX.QuantizedValue);
            Assert.AreEqual("locomotion.y", result.Request.ParameterY.ParameterId);
            Assert.AreEqual(-250, result.Request.ParameterY.QuantizedValue);
            Assert.AreEqual(1, backend.SetBlend2DCalls);
        }

        [Test]
        public void LocomotionBlend2D_DoesNotMatchAirborneBlendWhenGroundedBlendMissing()
        {
            CharacterControlEntityRef entity = CreateEntity();
            var backend = new RecordingAnimationBackend();
            var adapter = new CharacterControlMxAnimationAdapter(new CharacterControlMxAnimationAdapterOptions
            {
                FallbackToBlend1DWhenBlend2DMissing = false
            });
            adapter.RegisterActor(entity, backend, CreateAirborneOnly2DAnimationSet());

            CharacterControlAnimationAdapterResult result = adapter.ApplyLocomotion(new CharacterControlAnimationLocomotionSample(
                entity,
                new RuntimeFrame(24),
                Fix64.One,
                Fix64.One,
                Fix64.Zero,
                grounded: true));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CharacterControlAnimationMissingBindingKind.LocomotionBlend2D, result.MissingBindingKind);
            Assert.AreEqual("locomotion.direction", result.MissingBindingId);
            Assert.AreEqual(0, backend.SetBlend2DCalls);
        }

        [Test]
        public void LocomotionBlend2D_MissingDefinitionFallsBackToBlend1DWithDiagnostics()
        {
            CharacterControlEntityRef entity = CreateEntity();
            var backend = new RecordingAnimationBackend();
            var adapter = new CharacterControlMxAnimationAdapter();
            adapter.RegisterActor(entity, backend, CreateAnimationSet(includeBlend1D: true, includeBlend2D: false));

            CharacterControlAnimationAdapterResult result = adapter.ApplyLocomotion(new CharacterControlAnimationLocomotionSample(
                entity,
                new RuntimeFrame(30),
                Fix64.Half,
                Fix64.One,
                Fix64.Zero,
                grounded: true));
            CharacterControlAnimationDiagnosticSnapshot snapshot = adapter.CreateSnapshot();

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(CharacterControlAnimationRequestKind.LocomotionBlend1D, result.Request.Kind);
            Assert.AreEqual("2D locomotion blend missing; fallback to 1D speed blend.", result.FallbackReason);
            Assert.AreEqual(result.FallbackReason, snapshot.FallbackReason);
            Assert.AreEqual(1, backend.SetBlend1DCalls);
            Assert.AreEqual(0, backend.SetBlend2DCalls);
        }

        [Test]
        public void ActionEvent_DefaultAdapterDelegatesToCombatMxAnimationBridge()
        {
            CharacterControlEntityRef entity = CreateEntity();
            var backend = new RecordingAnimationBackend();
            var adapter = new CharacterControlMxAnimationAdapter();
            adapter.RegisterActor(entity, backend, CreateAnimationSet(includeBlend1D: true, includeBlend2D: true));
            var evt = new CharacterActionEvent(
                CharacterActionEventType.Started,
                CharacterActionRequest.CombatAction(RuntimeFrame.Zero, entity, CharacterActionKind.Attack, 1001),
                CharacterActionRejectedReason.None,
                actionInstanceId: 7,
                runtimeCommand: default,
                message: string.Empty);

            CharacterControlAnimationAdapterResult result = adapter.ApplyActionEvent(evt);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(CharacterControlAnimationRequestKind.ActionDelegated, result.Request.Kind);
            Assert.AreEqual("Action animation is delegated to CombatMxAnimationUnityBridge.", result.FallbackReason);
            Assert.AreEqual(0, backend.TotalRequests);
        }

        [Test]
        public void ReactionMissingBinding_RecordsDiagnosticsWithoutChangingAuthorityState()
        {
            CharacterControlEntityRef entity = CreateEntity();
            var machine = new CharacterControlStateMachine(entity);
            Assert.IsTrue(machine.BeginReaction(new RuntimeFrame(3)).Success);
            var backend = new RecordingAnimationBackend();
            var adapter = new CharacterControlMxAnimationAdapter();
            adapter.RegisterActor(entity, backend, CreateAnimationSet(includeBlend1D: true, includeBlend2D: true));

            CharacterControlAnimationAdapterResult result = adapter.ApplyReaction(new CharacterPressureReactionEvent(
                CharacterPressureReactionEventType.ReactionStarted,
                CharacterPressureReactionKind.GuardBreak,
                entity,
                entity.GameplayEntityId,
                new RuntimeFrame(3),
                new RuntimeFrame(9),
                PressureBand.Cracked,
                PressureBand.Broken,
                CharacterPressureReactionRejectedReason.None,
                CharacterControlLockMask.Action,
                "guard break"));
            CharacterControlAnimationDiagnosticSnapshot snapshot = adapter.CreateSnapshot();

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CharacterControlAnimationMissingBindingKind.Reaction, result.MissingBindingKind);
            Assert.AreEqual("reaction:GuardBreak", snapshot.MissingBindingId);
            Assert.AreEqual(CharacterControlState.Reaction, machine.CurrentState);
            Assert.AreEqual(0, backend.TotalRequests);
        }

        [Test]
        public void ReactionRequestFailureIsDiagnosticOnly()
        {
            CharacterControlEntityRef entity = CreateEntity();
            var machine = new CharacterControlStateMachine(entity);
            Assert.IsTrue(machine.BeginReaction(new RuntimeFrame(5)).Success);
            var backend = new RecordingAnimationBackend
            {
                UseNextCrossFadeResult = true,
                NextCrossFadeResult = MxAnimationBackendResult.Failed(
                    MxAnimationBackendResultCode.LoadFailed,
                    Reaction,
                    "missing reaction clip")
            };
            var adapter = new CharacterControlMxAnimationAdapter();
            adapter.RegisterActor(entity, backend, CreateAnimationSet(
                includeBlend1D: true,
                includeBlend2D: true,
                includeReaction: true));

            CharacterControlAnimationAdapterResult result = adapter.ApplyReaction(new CharacterPressureReactionEvent(
                CharacterPressureReactionEventType.ReactionStarted,
                CharacterPressureReactionKind.GuardBreak,
                entity,
                entity.GameplayEntityId,
                new RuntimeFrame(5),
                new RuntimeFrame(13),
                PressureBand.Cracked,
                PressureBand.Broken,
                CharacterPressureReactionRejectedReason.None,
                CharacterControlLockMask.Action,
                "guard break"));
            CharacterControlAnimationDiagnosticSnapshot snapshot = adapter.CreateSnapshot();

            Assert.IsFalse(result.Success);
            Assert.AreEqual(CharacterControlAnimationRequestKind.ReactionCrossFade, result.Request.Kind);
            Assert.AreEqual(MxAnimationBackendResultCode.LoadFailed, result.BackendResult.Code);
            Assert.AreEqual("reaction:GuardBreak", result.Request.ActionKey);
            Assert.AreEqual(MxAnimationBackendResultCode.LoadFailed, snapshot.LastBackendResult.Code);
            Assert.AreEqual("reaction:GuardBreak", snapshot.LastRequest.ActionKey);
            Assert.AreEqual(1, snapshot.RecentResults.Count);
            Assert.AreEqual(CharacterControlState.Reaction, machine.CurrentState);
            Assert.AreEqual(1, backend.CrossFadeCalls);
        }

        private static MxAnimationSetDefinition CreateAnimationSet(
            bool includeBlend1D,
            bool includeBlend2D,
            bool includeReaction = false)
        {
            MxAnimationBlend1DDefinition[] blend1D = includeBlend1D
                ? new[]
                {
                    new MxAnimationBlend1DDefinition(
                        "locomotion.speed",
                        "locomotion.speed",
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend1DPoint(0, Idle),
                            new MxAnimationBlend1DPoint(1000, Clip("test.animation.run"))
                        }),
                    new MxAnimationBlend1DDefinition(
                        "locomotion.air.speed",
                        "locomotion.speed",
                        MxAnimationLayerId.Base,
                        new[] { new MxAnimationBlend1DPoint(0, Clip("test.animation.air")) })
                }
                : null;

            MxAnimationBlend2DDefinition[] blend2D = includeBlend2D
                ? new[]
                {
                    new MxAnimationBlend2DDefinition(
                        "locomotion.direction",
                        "locomotion.x",
                        "locomotion.y",
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend2DPoint(0, 1000, Clip("test.animation.forward")),
                            new MxAnimationBlend2DPoint(1000, 0, Clip("test.animation.right")),
                            new MxAnimationBlend2DPoint(-1000, 0, Clip("test.animation.left"))
                        }),
                    new MxAnimationBlend2DDefinition(
                        "locomotion.air.direction",
                        "locomotion.x",
                        "locomotion.y",
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend2DPoint(0, 1000, Clip("test.animation.air_forward")),
                            new MxAnimationBlend2DPoint(1000, 0, Clip("test.animation.air_right")),
                            new MxAnimationBlend2DPoint(-1000, 0, Clip("test.animation.air_left"))
                        })
                }
                : null;

            MxAnimationActionBinding[] actions = includeReaction
                ? new[]
                {
                    new MxAnimationActionBinding(
                        "reaction.guard_break",
                        "reaction:GuardBreak",
                        Reaction,
                        MxAnimationLayerId.Base)
                }
                : null;

            return new MxAnimationSetDefinition(
                "test.character",
                version: 1,
                defaultClip: Idle,
                fallbackClip: Idle,
                actions: actions,
                blend1DDefinitions: blend1D,
                blend2DDefinitions: blend2D);
        }

        private static MxAnimationSetDefinition CreateAirborneOnly2DAnimationSet()
        {
            return new MxAnimationSetDefinition(
                "test.character",
                version: 1,
                defaultClip: Idle,
                fallbackClip: Idle,
                blend2DDefinitions: new[]
                {
                    new MxAnimationBlend2DDefinition(
                        "locomotion.air.direction",
                        "locomotion.x",
                        "locomotion.y",
                        MxAnimationLayerId.Base,
                        new[]
                        {
                            new MxAnimationBlend2DPoint(0, 1000, Clip("test.animation.air_forward")),
                            new MxAnimationBlend2DPoint(1000, 0, Clip("test.animation.air_right")),
                            new MxAnimationBlend2DPoint(-1000, 0, Clip("test.animation.air_left"))
                        })
                });
        }

        private static CharacterControlEntityRef CreateEntity()
        {
            return CharacterControlEntityRef.FromGameplayAndCombat(
                new GameplayEntityId(1, 1),
                new CombatEntityId(10),
                new CombatBodyId(10),
                stableId: 1);
        }

        private static ResourceKey Clip(string id)
        {
            return new ResourceKey(id, ResourceTypeIds.AnimationClip);
        }

        private sealed class RecordingAnimationBackend : IMxAnimationBackend
        {
            public bool UseNextCrossFadeResult { get; set; }

            public MxAnimationBackendResult NextCrossFadeResult { get; set; }

            public int SetBlend1DCalls { get; private set; }

            public int SetBlend2DCalls { get; private set; }

            public int CrossFadeCalls { get; private set; }

            public int PlayCalls { get; private set; }

            public int StopCalls { get; private set; }

            public int SetLayerWeightCalls { get; private set; }

            public int TotalRequests => PlayCalls + StopCalls + CrossFadeCalls + SetLayerWeightCalls + SetBlend1DCalls + SetBlend2DCalls;

            public string BackendName => "Recording";

            public MxAnimationBackendResult Play(MxAnimationPlayRequest request)
            {
                PlayCalls++;
                return MxAnimationBackendResult.Succeeded(request.ClipKey, "play");
            }

            public MxAnimationBackendResult Stop(MxAnimationStopRequest request)
            {
                StopCalls++;
                return MxAnimationBackendResult.Succeeded(default, "stop");
            }

            public MxAnimationBackendResult CrossFade(MxAnimationCrossFadeRequest request)
            {
                CrossFadeCalls++;
                return UseNextCrossFadeResult
                    ? NextCrossFadeResult
                    : MxAnimationBackendResult.Succeeded(request.ClipKey, "crossfade");
            }

            public MxAnimationBackendResult SetLayerWeight(MxAnimationLayerWeightRequest request)
            {
                SetLayerWeightCalls++;
                return MxAnimationBackendResult.Succeeded(default, "layer");
            }

            public MxAnimationBackendResult SetBlend1D(MxAnimationBlend1DRequest request)
            {
                SetBlend1DCalls++;
                return MxAnimationBackendResult.Succeeded(default, "blend1d");
            }

            public MxAnimationBackendResult SetBlend2D(MxAnimationBlend2DRequest request)
            {
                SetBlend2DCalls++;
                return MxAnimationBackendResult.Succeeded(default, "blend2d");
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
                    actorCount: 0,
                    graphIsValid: true,
                    isReleased: false,
                    defaultClip: null,
                    fallbackClip: null,
                    layerStates: null,
                    activeFades: null,
                    recentRequests: null,
                    recentResourceErrors: null);
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
