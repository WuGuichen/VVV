using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Story;

namespace MxFramework.Demo.GameplayTriggers
{
    /// <summary>Demo-owned Simulation seam; Combat motion and topology projection attach here.</summary>
    internal sealed class GameplayTriggerPlayableSimulationModule : RuntimeModule
    {
        internal const string StableModuleId =
            "mxframework.demo.gameplay-trigger-playable";

        private readonly StoryDirector _storyDirector;
        private readonly GameplayComponentWorld _componentWorld;
        private readonly GameplayEntityId _playerEntity;
        private readonly RuntimeCommandBuffer _commands;
        private readonly CombatKinematicMotor _motor;
        private readonly CombatKinematicMotorCandidateCache _candidateCache;
        private readonly CombatMotionStepBuffer _stepBuffer;
        private readonly List<RuntimeCommand> _drainedCommands =
            new List<RuntimeCommand>(2);
        private CombatMotionState _motionState;
        private int _moveX;
        private int _moveZ;
        private bool _doorTopologyPresent = true;
        private long _lastConsumedCommandSequence = -1L;

        /// <remarks>LowFreqAlloc during composition.</remarks>
        internal GameplayTriggerPlayableSimulationModule(
            StoryDirector storyDirector,
            GameplayComponentWorld componentWorld,
            GameplayEntityId playerEntity,
            RuntimeCommandBuffer commands,
            CombatPhysicsWorld physicsWorld)
            : base(StableModuleId, RuntimeTickStage.Simulation, priority: 0)
        {
            _storyDirector = storyDirector ??
                throw new ArgumentNullException(nameof(storyDirector));
            _componentWorld = componentWorld ??
                throw new ArgumentNullException(nameof(componentWorld));
            if (!playerEntity.IsValid)
                throw new ArgumentOutOfRangeException(nameof(playerEntity));
            _playerEntity = playerEntity;
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            PhysicsWorld = physicsWorld ??
                throw new ArgumentNullException(nameof(physicsWorld));

            CombatPhysicsLayerMask obstacleMask = new CombatPhysicsLayerMask(
                (1 << GameplayTriggerPlayableCombatLayers.World) |
                (1 << GameplayTriggerPlayableCombatLayers.Door));
            _motor = new CombatKinematicMotor(new CombatMotionConfig(
                ticksPerSecond: 60,
                moveSpeed: Fix64.FromInt(6),
                gravityPerSecond: Fix64.Zero,
                jumpSpeed: Fix64.Zero,
                maxFallSpeed: Fix64.Zero,
                skinWidth: Fix64.FromRatio(1, 100),
                groundMinNormalY: Fix64.Zero,
                ceilingMinNormalY: Fix64.Zero,
                maxCollisionIterations: 3,
                characterHalfExtents: new FixVector3(
                    Fix64.FromRatio(3, 10),
                    Fix64.Half,
                    Fix64.FromRatio(3, 10)),
                obstacleLayerMask: obstacleMask));
            _candidateCache = new CombatKinematicMotorCandidateCache();
            _stepBuffer = new CombatMotionStepBuffer(collisionCapacity: 3);
            _motionState = new CombatMotionState(
                CombatFrame.Zero,
                new FixVector3(Fix64.FromInt(-4), Fix64.Zero, Fix64.Zero),
                FixVector3.Zero,
                grounded: false,
                lastCollisionNormal: FixVector3.Zero,
                collisionFlags: CombatMotionCollisionFlags.None);
            _candidateCache.Prepare(
                PhysicsWorld,
                obstacleMask,
                CombatPhysicsLayerMask.FromLayer(
                    GameplayTriggerPlayableCombatLayers.Actor));
        }

        internal CombatPhysicsWorld PhysicsWorld { get; }
        internal CombatMotionState MotionState => _motionState;
        internal bool DoorTopologyPresent => _doorTopologyPresent;
        internal int LastControlCommandId { get; private set; }
        internal long LastConsumedCommandSequence => _lastConsumedCommandSequence;

        /// <remarks>NoAlloc after construction and warm-up.</remarks>
        public override void Tick(RuntimeTickContext context)
        {
            DrainCommands(new RuntimeFrame(context.FrameIndex));
            ProjectDoorTopology();

            int moveSpeed = GameplayComponentAttributeQuery.GetCurrentValueOrDefault(
                _componentWorld,
                _playerEntity,
                GameplayTriggerPlayableIds.MoveSpeedAttribute);
            Fix64 speedScale = moveSpeed <= 0
                ? Fix64.Zero
                : Fix64.FromRatio(moveSpeed, 6);
            var input = new CombatMotionInput(
                new FixVector3(
                    Fix64.FromRatio(_moveX, 1000),
                    Fix64.Zero,
                    Fix64.FromRatio(_moveZ, 1000)),
                jumpPressed: false,
                speedScale,
                applyGroundSurfaceFriction: false);
            _motionState = _motor.Step(
                PhysicsWorld,
                new CombatBodyId(GameplayTriggerPlayableIds.PlayerBody),
                _motionState,
                input,
                impactVelocityOverride: null,
                _stepBuffer,
                _candidateCache).State;
        }

        private void DrainCommands(RuntimeFrame frame)
        {
            LastControlCommandId = 0;
            _drainedCommands.Clear();
            _commands.DrainForFrame(frame, _drainedCommands);
            for (int i = 0; i < _drainedCommands.Count; i++)
            {
                RuntimeCommand command = _drainedCommands[i];
                if (command.Sequence > _lastConsumedCommandSequence)
                    _lastConsumedCommandSequence = command.Sequence;
                if (command.CommandId == GameplayTriggerPlayableIds.MoveCommand)
                {
                    _moveX = command.Payload0;
                    _moveZ = command.Payload1;
                }
                else
                {
                    LastControlCommandId = command.CommandId;
                }
            }
            _drainedCommands.Clear();
        }

        internal void RestoreAuthority(
            in CombatMotionState motionState,
            long lastConsumedCommandSequence)
        {
            _motionState = motionState;
            _moveX = 0;
            _moveZ = 0;
            LastControlCommandId = 0;
            _lastConsumedCommandSequence = lastConsumedCommandSequence;
            PhysicsWorld.UpsertBody(new CombatPhysicsBody(
                new CombatEntityId(GameplayTriggerPlayableIds.PlayerCombatEntity),
                new CombatBodyId(GameplayTriggerPlayableIds.PlayerBody),
                motionState.Position));
            RebuildDoorTopologyFromStory();
        }

        private void ProjectDoorTopology()
        {
            if (!_doorTopologyPresent || !IsDoorOpen())
                return;

            CombatBodyId bodyId = new CombatBodyId(
                GameplayTriggerPlayableIds.DoorBody);
            PhysicsWorld.RemoveCollider(
                bodyId,
                new CombatColliderId(GameplayTriggerPlayableIds.DoorCollider));
            PhysicsWorld.RemoveBody(bodyId);
            _doorTopologyPresent = false;
            _candidateCache.Invalidate();
            _candidateCache.Prepare(
                PhysicsWorld,
                new CombatPhysicsLayerMask(
                    (1 << GameplayTriggerPlayableCombatLayers.World) |
                    (1 << GameplayTriggerPlayableCombatLayers.Door)),
                CombatPhysicsLayerMask.FromLayer(
                    GameplayTriggerPlayableCombatLayers.Actor));
        }

        private void RebuildDoorTopologyFromStory()
        {
            CombatBodyId doorBody = new CombatBodyId(
                GameplayTriggerPlayableIds.DoorBody);
            bool doorOpen = IsDoorOpen();
            if (doorOpen)
            {
                PhysicsWorld.RemoveCollider(
                    doorBody,
                    new CombatColliderId(GameplayTriggerPlayableIds.DoorCollider));
                PhysicsWorld.RemoveBody(doorBody);
                _doorTopologyPresent = false;
            }
            else
            {
                PhysicsWorld.UpsertBody(new CombatPhysicsBody(
                    new CombatEntityId(GameplayTriggerPlayableIds.DoorCombatEntity),
                    doorBody,
                    FixVector3.Zero));
                PhysicsWorld.UpsertAabbCollider(new CombatPhysicsAabbCollider(
                    doorBody,
                    new CombatColliderId(GameplayTriggerPlayableIds.DoorCollider),
                    GameplayTriggerPlayableCombatLayers.Door,
                    new FixVector3(
                        Fix64.FromRatio(-2, 10),
                        -Fix64.One,
                        Fix64.FromInt(-3)),
                    new FixVector3(
                        Fix64.FromRatio(2, 10),
                        Fix64.One,
                        Fix64.FromInt(3))));
                _doorTopologyPresent = true;
            }

            _candidateCache.Invalidate();
            _candidateCache.Prepare(
                PhysicsWorld,
                new CombatPhysicsLayerMask(
                    (1 << GameplayTriggerPlayableCombatLayers.World) |
                    (1 << GameplayTriggerPlayableCombatLayers.Door)),
                CombatPhysicsLayerMask.FromLayer(
                    GameplayTriggerPlayableCombatLayers.Actor));
            if (_doorTopologyPresent == doorOpen)
            {
                throw new InvalidOperationException(
                    "Gameplay Trigger playable derived door topology does not match Story authority.");
            }
        }

        private bool IsDoorOpen()
        {
            var key = new StoryFactKey(
                GameplayTriggerPlayableIds.StoryFactNamespace,
                GameplayTriggerPlayableIds.StoryDoorOpenFact);
            return _storyDirector.Blackboard.TryGet(key, out StoryValue value) &&
                   value.Kind == StoryValueKind.Bool &&
                   value.AsBool();
        }
    }

    internal static class GameplayTriggerPlayableCombatLayers
    {
        internal const int Actor = 1;
        internal const int World = 2;
        internal const int Door = 3;
    }

    internal sealed class GameplayTriggerPlayableCommandValidator :
        IRuntimeCommandValidator
    {
        /// <remarks>NoAlloc.</remarks>
        public RuntimeCommandValidationResult Validate(RuntimeCommand command)
        {
            bool validEnvelope = command.Envelope.TypeId ==
                                     GameplayTriggerPlayableIds.CommandEnvelopeType &&
                                 command.Envelope.SchemaVersion ==
                                     GameplayTriggerPlayableIds.CommandEnvelopeSchema &&
                                 command.Envelope.Provenance ==
                                     RuntimeCommandProvenanceKind.Exogenous;
            bool validPayload;
            switch (command.CommandId)
            {
                case GameplayTriggerPlayableIds.MoveCommand:
                    validPayload = command.TargetId ==
                                       GameplayTriggerPlayableIds.PlayerCombatEntity &&
                                   command.Payload0 >= -1000 && command.Payload0 <= 1000 &&
                                   command.Payload1 >= -1000 && command.Payload1 <= 1000 &&
                                   command.Payload2 == 0;
                    break;
                case GameplayTriggerPlayableIds.CaptureCommand:
                case GameplayTriggerPlayableIds.RestoreCommand:
                case GameplayTriggerPlayableIds.ResetCommand:
                    validPayload = command.TargetId == 0 &&
                                   command.Payload0 == 0 &&
                                   command.Payload1 == 0 &&
                                   command.Payload2 == 0;
                    break;
                default:
                    validPayload = false;
                    break;
            }

            if (validEnvelope && validPayload)
                return RuntimeCommandValidationResult.Accepted(command);
            return RuntimeCommandValidationResult.Failed(
                new RuntimeCommandError(
                    RuntimeCommandErrorCode.InvalidPayload,
                    command,
                    RuntimeFrame.Zero,
                    "Gameplay Trigger playable command payload is invalid."));
        }
    }
}
