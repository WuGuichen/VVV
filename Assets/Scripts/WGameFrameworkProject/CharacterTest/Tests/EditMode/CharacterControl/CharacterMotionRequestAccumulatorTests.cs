using MxFramework.CharacterControl;
using MxFramework.CharacterAction;
using MxFramework.Combat.Core;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using NUnit.Framework;

namespace MxFramework.Tests.CharacterControl
{
    public sealed class CharacterMotionRequestAccumulatorTests
    {
        private const int IdleMode = 0;
        private const int RunMode = 2;
        private const int RootMotionMode = 8;
        private const int ControlLockedMode = 10;

        [Test]
        public void MovementModeRequest_AppliesModeWithoutVelocity()
        {
            var accumulator = new CharacterMotionRequestAccumulator();
            accumulator.Reset();

            CharacterMotionRequest request = CreateRequest(
                CharacterMotionRequestKind.MovementMode,
                0,
                0,
                0,
                CharacterMotionRequestSpace.World,
                RunMode,
                CharacterTestActionIds.Action_action_mode,
                CharacterTestBulkIds36.Id790010);
            accumulator.Accumulate(in request, 0, null);

            CharacterMotionRequestFrame frame = accumulator.ToFrame();

            Assert.AreEqual(RunMode, frame.MovementModeId);
            Assert.IsFalse(frame.HasImpulse);
            Assert.IsFalse(frame.HasVelocityOverride);
            Assert.IsFalse(frame.HasExternalAcceleration);
        }

        [Test]
        public void Accumulate_CombinesRequestsAndKeepsFirstSources()
        {
            var accumulator = new CharacterMotionRequestAccumulator();
            accumulator.Reset();

            CharacterMotionRequest impulse = CreateRequest(
                CharacterMotionRequestKind.Impulse,
                100,
                200,
                300,
                CharacterMotionRequestSpace.World,
                RunMode,
                CharacterTestActionIds.Action_action_impulse,
                CharacterTestBulkIds36.Id790010);
            accumulator.Accumulate(in impulse, 0, null);

            CharacterMotionRequest rootMotion = CreateRequest(
                CharacterMotionRequestKind.RootMotionDelta,
                10,
                20,
                30,
                CharacterMotionRequestSpace.Local,
                RootMotionMode,
                CharacterTestActionIds.Action_action_root,
                CharacterTestBulkIds36.Id790010);
            accumulator.Accumulate(in rootMotion, 90000, DoubleLocalResolver.Instance);

            CharacterMotionRequest controlScale = new CharacterMotionRequest(
                CharacterMotionRequestKind.ControlScale,
                0,
                0,
                0,
                CharacterMotionRequestSpace.World,
                IdleMode,
                lockMovement: false,
                movementControlScaleMilli: 500,
                rotationControlScaleMilli: 250,
                hasMovementControlScale: true,
                hasRotationControlScale: true,
                sourceActionId: CharacterTestActionIds.Action_action_scale,
                stableEventId: CharacterTestBulkIds36.Id790010);
            accumulator.Accumulate(in controlScale, 0, null);

            CharacterMotionRequestFrame frame = accumulator.ToFrame();

            Assert.IsTrue(frame.HasImpulse);
            Assert.AreEqual(100, frame.ImpulseVelocityX);
            Assert.AreEqual(200, frame.ImpulseVelocityY);
            Assert.AreEqual(300, frame.ImpulseVelocityZ);
            Assert.AreEqual(CharacterTestActionIds.Action_action_impulse, frame.ImpulseSource.SourceActionId);
            Assert.AreEqual(CharacterTestBulkIds36.Id790010, frame.ImpulseSource.StableEventId);
            Assert.IsTrue(frame.HasRootMotion);
            Assert.AreEqual(20, frame.RootMotionDeltaX);
            Assert.AreEqual(40, frame.RootMotionDeltaY);
            Assert.AreEqual(60, frame.RootMotionDeltaZ);
            Assert.AreEqual(RootMotionMode, frame.MovementModeId);
            Assert.AreEqual(500, frame.MovementControlScaleMilli);
            Assert.AreEqual(250, frame.RotationControlScaleMilli);
            Assert.AreEqual(CharacterTestActionIds.Action_action_scale, frame.ControlScaleSource.SourceActionId);
        }

        [Test]
        public void LockMovement_WinsMovementModeAndSetsControlScale()
        {
            var accumulator = new CharacterMotionRequestAccumulator();
            accumulator.Reset();

            CharacterMotionRequest locked = new CharacterMotionRequest(
                CharacterMotionRequestKind.LockMovement,
                0,
                0,
                0,
                CharacterMotionRequestSpace.World,
                ControlLockedMode,
                lockMovement: true,
                movementControlScaleMilli: 1000,
                rotationControlScaleMilli: 1000,
                hasMovementControlScale: true,
                hasRotationControlScale: true,
                sourceActionId: CharacterTestActionIds.Action_lock,
                stableEventId: CharacterTestBulkIds36.Id790009);
            accumulator.Accumulate(in locked, 0, null);

            CharacterMotionRequest laterMode = CreateRequest(
                CharacterMotionRequestKind.Impulse,
                1,
                2,
                3,
                CharacterMotionRequestSpace.World,
                RunMode,
                CharacterTestActionIds.Action_run,
                CharacterTestBulkIds36.Id790010);
            accumulator.Accumulate(in laterMode, 0, null);

            CharacterMotionRequestFrame frame = accumulator.ToFrame();

            Assert.IsTrue(frame.LockMovement);
            Assert.AreEqual(ControlLockedMode, frame.MovementModeId);
            Assert.AreEqual(0, frame.MovementControlScaleMilli);
            Assert.AreEqual(1000, frame.RotationControlScaleMilli);
            Assert.AreEqual(0, CharacterMotionRequestAccumulator.ResolveControlScaleMilli(frame, 1000));
            Assert.AreEqual(1000, CharacterMotionRequestAccumulator.ResolveRotationScaleMilli(frame, 750));
        }

        [Test]
        public void RootMotionPersistentVelocity_DoesNotKeepRootMotionOnlyVelocity()
        {
            CharacterMotionRequestAccumulator.ResolvePersistentHorizontalVelocityAfterRootMotion(
                baseVelocityX: 0,
                baseVelocityY: 0,
                resolvedVelocityX: 120,
                resolvedVelocityY: 0,
                rootMotionVelocityX: 120,
                rootMotionVelocityY: 0,
                epsilon: 1,
                out int persistentX,
                out int persistentY);

            Assert.AreEqual(0, persistentX);
            Assert.AreEqual(0, persistentY);

            int persistentVertical = CharacterMotionRequestAccumulator.ResolvePersistentVerticalVelocityAfterRootMotion(
                baseVelocity: 0,
                resolvedVelocity: 90,
                rootMotionVelocity: 90,
                epsilon: 1);

            Assert.AreEqual(0, persistentVertical);
        }

        [Test]
        public void CharacterActionMotionRequestAccumulatorSink_TranslatesMotionEventsToFrame()
        {
            var sink = new CharacterActionMotionRequestAccumulatorSink(
                yawMilliDegrees: 90000,
                localSpaceResolver: DoubleLocalResolver.Instance);

            CharacterActionAdapterSinkResult modeResult = sink.SubmitMotionRequest(CreateActionMotionRequest(
                CharacterActionTrackEventKind.SetMovementMode,
                CharacterMovementMode.Run,
                stableEventId: CharacterTestNumericIds.Field_motion_run));
            CharacterActionAdapterSinkResult impulseResult = sink.SubmitMotionRequest(CreateActionMotionRequest(
                CharacterActionTrackEventKind.ApplyImpulse,
                CharacterMovementMode.Idle,
                x: 0.25f,
                y: 0.5f,
                z: 3f,
                stableEventId: CharacterTestNumericIds.Field_motion_impulse,
                motionVectorSpace: CharacterActionMotionVectorSpace.Local));
            CharacterActionAdapterSinkResult scaleResult = sink.SubmitMotionRequest(CreateActionMotionRequest(
                CharacterActionTrackEventKind.SetControlScale,
                CharacterMovementMode.Airborne,
                movementControlScale: 0.25f,
                rotationControlScale: 0.75f,
                stableEventId: CharacterTestNumericIds.Field_motion_scale));

            CharacterMotionRequestFrame frame = sink.ToFrame();

            Assert.IsTrue(modeResult.Accepted);
            Assert.IsTrue(impulseResult.Accepted);
            Assert.IsTrue(scaleResult.Accepted);
            Assert.AreEqual(RunMode, frame.MovementModeId);
            Assert.IsTrue(frame.HasImpulse);
            Assert.AreEqual(500, frame.ImpulseVelocityX);
            Assert.AreEqual(1000, frame.ImpulseVelocityY);
            Assert.AreEqual(6000, frame.ImpulseVelocityZ);
            Assert.AreEqual(CharacterTestActionIds.Action_action_dash, frame.ImpulseSource.SourceActionId);
            Assert.AreEqual(CharacterTestBulkIds36.Id790010, frame.ImpulseSource.StableEventId);
            Assert.IsTrue(frame.HasMovementControlScale);
            Assert.IsTrue(frame.HasRotationControlScale);
            Assert.AreEqual(250, frame.MovementControlScaleMilli);
            Assert.AreEqual(750, frame.RotationControlScaleMilli);
            Assert.AreEqual(CharacterTestBulkIds36.Id790010, frame.ControlScaleSource.StableEventId);
        }

        [Test]
        public void CharacterActionMotionRequestAccumulatorSink_LockAndExplicitDefaultScaleUseExpectedControlScales()
        {
            var sink = new CharacterActionMotionRequestAccumulatorSink();

            CharacterActionAdapterSinkResult lockResult = sink.SubmitMotionRequest(CreateActionMotionRequest(
                CharacterActionTrackEventKind.LockMovement,
                CharacterMovementMode.Idle,
                stableEventId: CharacterTestNumericIds.Field_motion_lock));

            CharacterMotionRequestFrame lockedFrame = sink.ToFrame();

            Assert.IsTrue(lockResult.Accepted);
            Assert.IsTrue(lockedFrame.LockMovement);
            Assert.AreEqual(ControlLockedMode, lockedFrame.MovementModeId);
            Assert.AreEqual(0, lockedFrame.MovementControlScaleMilli);
            Assert.AreEqual(1000, lockedFrame.RotationControlScaleMilli);

            sink.Reset();
            CharacterActionAdapterSinkResult resetResult = sink.SubmitMotionRequest(CreateActionMotionRequest(
                CharacterActionTrackEventKind.SetControlScale,
                CharacterMovementMode.Airborne,
                stableEventId: CharacterTestNumericIds.Field_motion_reset));

            CharacterMotionRequestFrame resetFrame = sink.ToFrame();

            Assert.IsTrue(resetResult.Accepted);
            Assert.AreEqual(IdleMode, resetFrame.MovementModeId);
            Assert.AreEqual(1000, resetFrame.MovementControlScaleMilli);
            Assert.AreEqual(1000, resetFrame.RotationControlScaleMilli);
        }

        [Test]
        public void CharacterActionMotionRequestAccumulatorSink_LocalImpulseRequiresResolver()
        {
            var sink = new CharacterActionMotionRequestAccumulatorSink();

            CharacterActionAdapterSinkResult result = sink.SubmitMotionRequest(CreateActionMotionRequest(
                CharacterActionTrackEventKind.ApplyImpulse,
                CharacterMovementMode.Idle,
                z: 1f,
                stableEventId: CharacterTestNumericIds.Field_motion_local,
                motionVectorSpace: CharacterActionMotionVectorSpace.Local));

            CharacterMotionRequestFrame frame = sink.ToFrame();

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(1, result.Diagnostics.Length);
            Assert.AreEqual(CharacterActionDiagnosticCodes.AdapterPayloadMissing, result.Diagnostics[0].Code);
            Assert.IsFalse(frame.HasImpulse);
        }

        [Test]
        public void CharacterActionMotionRequestAccumulatorSink_TranslatesRootMotionRequestToFrame()
        {
            var sink = new CharacterActionMotionRequestAccumulatorSink(
                yawMilliDegrees: 90000,
                localSpaceResolver: DoubleLocalResolver.Instance);

            CharacterActionAdapterSinkResult result = sink.SubmitRootMotionRequest(CreateRootMotionRequest(
                x: 0.25f,
                y: 0.5f,
                z: 3f,
                stableEventId: CharacterTestNumericIds.Field_anim_root,
                space: CharacterActionRootMotionSpace.Local,
                obstaclePolicy: CharacterActionRootMotionObstaclePolicy.StaticOnly));

            CharacterMotionRequestFrame frame = sink.ToFrame();

            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(frame.HasRootMotion);
            Assert.AreEqual(500, frame.RootMotionDeltaX);
            Assert.AreEqual(1000, frame.RootMotionDeltaY);
            Assert.AreEqual(6000, frame.RootMotionDeltaZ);
            Assert.AreEqual(RootMotionMode, frame.MovementModeId);
            Assert.AreEqual(CharacterMotionObstaclePolicy.StaticOnly, frame.RootMotionObstaclePolicy);
            Assert.AreEqual(CharacterTestActionIds.Action_action_dash, frame.RootMotionSource.SourceActionId);
            Assert.AreEqual(CharacterTestBulkIds36.Id790010, frame.RootMotionSource.StableEventId);
        }

        [Test]
        public void CharacterActionMotionRequestAccumulatorSink_LocalRootMotionRequiresResolver()
        {
            var sink = new CharacterActionMotionRequestAccumulatorSink();

            CharacterActionAdapterSinkResult result = sink.SubmitRootMotionRequest(CreateRootMotionRequest(
                z: 1f,
                stableEventId: CharacterTestNumericIds.Field_anim_local,
                space: CharacterActionRootMotionSpace.Local));

            CharacterMotionRequestFrame frame = sink.ToFrame();

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(1, result.Diagnostics.Length);
            Assert.AreEqual(CharacterActionDiagnosticCodes.AdapterPayloadMissing, result.Diagnostics[0].Code);
            Assert.IsFalse(frame.HasRootMotion);
        }

        private static CharacterMotionRequest CreateRequest(
            CharacterMotionRequestKind kind,
            int x,
            int y,
            int z,
            CharacterMotionRequestSpace space,
            int movementMode,
            CharacterActionId sourceActionId,
            int stableEventId)
        {
            return new CharacterMotionRequest(
                kind,
                x,
                y,
                z,
                space,
                movementMode,
                lockMovement: false,
                movementControlScaleMilli: 1000,
                rotationControlScaleMilli: 1000,
                hasMovementControlScale: false,
                hasRotationControlScale: false,
                sourceActionId: sourceActionId,
                stableEventId: stableEventId);
        }

        private static CharacterActionMotionRequest CreateActionMotionRequest(
            CharacterActionTrackEventKind kind,
            CharacterMovementMode movementMode,
            float x = 0f,
            float y = 0f,
            float z = 0f,
            int stableEventId = 0,
            float movementControlScale = 1f,
            float rotationControlScale = 1f,
            CharacterActionMotionVectorSpace motionVectorSpace = CharacterActionMotionVectorSpace.World,
            bool hasMovementControlScale = true,
            bool hasRotationControlScale = true)
        {
            return new CharacterActionMotionRequest(
                new CharacterActionAdapterDispatchMetadata(
                    new RuntimeFrame(12),
                    instanceId: 1,
                    planId: 2,
                    actionId: CharacterTestActionIds.Action_action_dash,
                    localFrame: 3,
                    stableEventId: stableEventId,
                    sourceId: 4,
                    traceId: CharacterTestRuntimeTraceIds.TraceDash),
                new GameplayEntityId(7, 1),
                new CombatBodyId(13),
                kind,
                movementMode,
                x,
                y,
                z,
                movementControlScale,
                rotationControlScale,
                motionVectorSpace,
                hasMovementControlScale,
                hasRotationControlScale);
        }

        private static CharacterActionRootMotionRequest CreateRootMotionRequest(
            float x = 0f,
            float y = 0f,
            float z = 0f,
            int stableEventId = 0,
            CharacterActionRootMotionSpace space = CharacterActionRootMotionSpace.World,
            CharacterActionRootMotionObstaclePolicy obstaclePolicy = CharacterActionRootMotionObstaclePolicy.Default)
        {
            return new CharacterActionRootMotionRequest(
                new CharacterActionAdapterDispatchMetadata(
                    new RuntimeFrame(12),
                    instanceId: 1,
                    planId: 2,
                    actionId: CharacterTestActionIds.Action_action_dash,
                    localFrame: 3,
                    stableEventId: stableEventId,
                    sourceId: 4,
                    traceId: CharacterTestRuntimeTraceIds.TraceDash),
                x,
                y,
                z,
                space,
                obstaclePolicy);
        }

        private sealed class DoubleLocalResolver : ICharacterMotionLocalSpaceResolver
        {
            public static readonly DoubleLocalResolver Instance = new DoubleLocalResolver();

            public void ResolveLocalToWorld(
                int yawMilliDegrees,
                int localX,
                int localY,
                int localZ,
                out int worldX,
                out int worldY,
                out int worldZ)
            {
                worldX = localX * 2;
                worldY = localY * 2;
                worldZ = localZ * 2;
            }
        }
    }
}
