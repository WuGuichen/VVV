using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Motion;
using MxFramework.Combat.Physics;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestComponentWorld
    {
        private IReadOnlyDictionary<int, CharacterTestPreparedAnimationBindings> _preparedAnimations;

        internal void SetPreparedAnimationProfiles(IReadOnlyDictionary<int, CharacterTestPreparedAnimationBindings> profiles) =>
            _preparedAnimations = profiles;

        internal bool TryGetPreparedAnimation(CharacterTestCharacterEntityId entityId, CharacterActionId actionId, int eventId,
            out CharacterTestPreparedAnimationBinding binding)
        {
            binding = default;
            return _preparedAnimations != null
                && _preparedAnimations.TryGetValue(GetIdentity(entityId).CharacterId, out CharacterTestPreparedAnimationBindings profile)
                && profile.TryGet(actionId, eventId, out binding);
        }

        internal bool TryGetPreparedAnimationById(CharacterTestCharacterEntityId entityId, int bindingId,
            out MxFramework.Animation.MxAnimationActionBinding binding)
        {
            binding = null;
            return _preparedAnimations != null
                && _preparedAnimations.TryGetValue(GetIdentity(entityId).CharacterId, out CharacterTestPreparedAnimationBindings profile)
                && profile.TryGetById(bindingId, out binding);
        }

        private readonly List<CharacterTestCharacterEntityId> _entityIds =
            new List<CharacterTestCharacterEntityId>();

        private readonly CharacterTestComponentStore<CharacterTestCharacterIdentityComponent> _identity =
            new CharacterTestComponentStore<CharacterTestCharacterIdentityComponent>();
        private readonly CharacterTestComponentStore<CharacterTestCharacterTeamComponent> _team =
            new CharacterTestComponentStore<CharacterTestCharacterTeamComponent>();
        private readonly CharacterTestComponentStore<CharacterTestCharacterTransformComponent> _transform =
            new CharacterTestComponentStore<CharacterTestCharacterTransformComponent>();
        private readonly CharacterTestComponentStore<CharacterTestCharacterSourceRevisionComponent> _sourceRevisions =
            new CharacterTestComponentStore<CharacterTestCharacterSourceRevisionComponent>();
        private readonly CharacterTestComponentStore<CharacterTestCharacterEquipmentComponent> _equipment =
            new CharacterTestComponentStore<CharacterTestCharacterEquipmentComponent>();
        private readonly CharacterTestComponentStore<CharacterTestCharacterControlComponent> _control =
            new CharacterTestComponentStore<CharacterTestCharacterControlComponent>();
        private readonly CharacterTestComponentStore<GameplayEntityId> _gameplayEntity =
            new CharacterTestComponentStore<GameplayEntityId>();
        private readonly CharacterTestComponentStore<CharacterTestCharacterPhysicsComponent> _physics =
            new CharacterTestComponentStore<CharacterTestCharacterPhysicsComponent>();
        private readonly CharacterTestComponentStore<CharacterTestColliderPostureComponent> _colliderPosture =
            new CharacterTestComponentStore<CharacterTestColliderPostureComponent>();
        private readonly CharacterTestComponentStore<CharacterTestCharacterCombatStateComponent> _combatState =
            new CharacterTestComponentStore<CharacterTestCharacterCombatStateComponent>();
        private readonly CharacterTestComponentStore<CharacterTestBodyPartStateComponent> _bodyPartState =
            new CharacterTestComponentStore<CharacterTestBodyPartStateComponent>();
        private readonly CharacterTestComponentStore<CharacterTestCharacterLifecycleComponent> _lifecycle =
            new CharacterTestComponentStore<CharacterTestCharacterLifecycleComponent>();
        private readonly CharacterTestComponentStore<CharacterTestEntityPresenceComponent> _presence =
            new CharacterTestComponentStore<CharacterTestEntityPresenceComponent>();
        private readonly CharacterTestComponentStore<CharacterTestBattleStateComponent> _battleState =
            new CharacterTestComponentStore<CharacterTestBattleStateComponent>();
        private readonly CharacterTestComponentStore<CharacterTestSpecialHitStateComponent> _specialHitState =
            new CharacterTestComponentStore<CharacterTestSpecialHitStateComponent>();
        private readonly CharacterTestComponentStore<CharacterTestSpecialHitSusceptibilityComponent> _specialHitSusceptibility =
            new CharacterTestComponentStore<CharacterTestSpecialHitSusceptibilityComponent>();
        private readonly CharacterTestComponentStore<CharacterTestCharacterInputComponent> _input =
            new CharacterTestComponentStore<CharacterTestCharacterInputComponent>();
        private readonly CharacterTestComponentStore<CharacterTestCharacterAnimationComponent> _animation =
            new CharacterTestComponentStore<CharacterTestCharacterAnimationComponent>();
        private readonly CharacterTestComponentStore<CharacterTestCharacterAnimationRequestComponent> _animationRequest =
            new CharacterTestComponentStore<CharacterTestCharacterAnimationRequestComponent>();
        private readonly CharacterTestComponentStore<CharacterTestCharacterActionComponent> _action =
            new CharacterTestComponentStore<CharacterTestCharacterActionComponent>();
        private readonly CharacterTestComponentStore<CharacterAuthorityPoseStateComponent> _authorityPose =
            new CharacterTestComponentStore<CharacterAuthorityPoseStateComponent>();
        private readonly CharacterTestComponentStore<CharacterTestTerrainIkStateComponent> _terrainIk =
            new CharacterTestComponentStore<CharacterTestTerrainIkStateComponent>();
        private readonly CharacterTestComponentStore<CharacterTestLivingNaturalAttributesRuntimeState> _livingNaturalAttributes =
            new CharacterTestComponentStore<CharacterTestLivingNaturalAttributesRuntimeState>();
        private readonly CharacterTestComponentStore<CharacterTestCharacterMovementStateComponent> _movement =
            new CharacterTestComponentStore<CharacterTestCharacterMovementStateComponent>();
        private readonly CharacterTestComponentStore<CharacterTestCharacterRequestBufferComponent> _requestBuffers =
            new CharacterTestComponentStore<CharacterTestCharacterRequestBufferComponent>();

        public IReadOnlyList<CharacterTestCharacterEntityId> EntityIds => _entityIds;

        public bool Contains(CharacterTestCharacterEntityId id)
        {
            return _identity.Contains(id);
        }

        public void RegisterCharacter(
            CharacterTestCharacterEntityId id,
            CharacterTestCharacterIdentityComponent identity,
            CharacterTestCharacterTeamComponent team,
            CharacterTestCharacterTransformComponent transform,
            CharacterTestCharacterEquipmentComponent equipment,
            CharacterTestCharacterControlComponent control,
            GameplayEntityId gameplayEntityId,
            CharacterTestCharacterPhysicsComponent physics)
        {
            if (!id.IsValid)
                throw new ArgumentException("CharacterTest component world requires a valid entity id.", nameof(id));

            if (!_identity.Contains(id))
                InsertEntityId(id);

            _identity.Set(id, identity);
            _team.Set(id, team);
            _transform.Set(id, transform);
            _sourceRevisions.Set(
                id,
                new CharacterTestCharacterSourceRevisionComponent(
                    CharacterTestCharacterSourceRevisionComponent.InitialRevision,
                    CharacterTestCharacterSourceRevisionComponent.InitialRevision));
            _equipment.Set(id, equipment);
            _control.Set(id, control);
            _gameplayEntity.Set(id, gameplayEntityId);
            _physics.Set(id, physics.IsConfigured ? physics : CharacterTestCharacterPhysicsComponent.Default);
            _colliderPosture.Set(
                id,
                CharacterTestColliderPostureComponent.FromPhysics(
                    _physics.TryGet(id, out CharacterTestCharacterPhysicsComponent storedPhysics)
                        ? storedPhysics
                        : CharacterTestCharacterPhysicsComponent.Default,
                    frame: 0L));
            _combatState.Set(id, CharacterTestCharacterCombatStateComponent.CreateDefault());
            _bodyPartState.Set(id, new CharacterTestBodyPartStateComponent(id.Value));
            _lifecycle.Set(id, CharacterTestCharacterLifecycleComponent.Alive);
            _presence.Set(id, CharacterTestEntityPresenceComponent.Active);
            _battleState.Set(id, CharacterTestBattleStateComponent.Alive);
            _specialHitState.Set(id, CharacterTestSpecialHitStateComponent.None);
            _specialHitSusceptibility.Set(id, CharacterTestSpecialHitSusceptibilityComponent.HumanoidDefault);
            _input.Set(id, default);
            _animation.Set(id, default);
            _animationRequest.Set(id, CharacterTestCharacterAnimationRequestComponent.None);
            _action.Set(id, CharacterTestCharacterActionComponent.None);
            _authorityPose.Set(
                id,
                new CharacterAuthorityPoseStateComponent(
                    CharacterTestAuthorityPoseDefinitionSource.CreateDefaultDefinition()));
            _terrainIk.Set(id, new CharacterTestTerrainIkStateComponent(id.Value));
            _livingNaturalAttributes.Set(
                id,
                CharacterTestGenesisConfigDefaults.CreateDefaultLivingNaturalAttributesState());
            _movement.Set(id, CharacterTestCharacterMovementStateComponent.Default);
            _groundPointTraction.Set(id, default);
            _positionRecall.Set(id, default);
            _interactionEffects.Set(id, new CharacterTestInteractionEffectsComponent(true));
            _requestBuffers.Set(id, new CharacterTestCharacterRequestBufferComponent());
        }

        public bool Remove(CharacterTestCharacterEntityId id)
        {
            if (!id.IsValid || !_identity.Remove(id))
                return false;

            _team.Remove(id);
            _transform.Remove(id);
            _sourceRevisions.Remove(id);
            _equipment.Remove(id);
            _control.Remove(id);
            _gameplayEntity.Remove(id);
            _physics.Remove(id);
            _colliderPosture.Remove(id);
            _combatState.Remove(id);
            _bodyPartState.Remove(id);
            _lifecycle.Remove(id);
            _presence.Remove(id);
            _battleState.Remove(id);
            _specialHitState.Remove(id);
            _specialHitSusceptibility.Remove(id);
            _input.Remove(id);
            _animation.Remove(id);
            _animationRequest.Remove(id);
            _action.Remove(id);
            _authorityPose.Remove(id);
            _terrainIk.Remove(id);
            _livingNaturalAttributes.Remove(id);
            _movement.Remove(id);
            _groundPointTraction.Remove(id);
            _positionRecall.Remove(id);
            GetInteractionEffects(id)?.Clear(); _interactionEffects.Remove(id);
            _requestBuffers.Remove(id);
            RemoveEntityId(id);
            return true;
        }

        public void Clear()
        {
            _entityIds.Clear();
            _identity.Clear();
            _team.Clear();
            _transform.Clear();
            _sourceRevisions.Clear();
            _equipment.Clear();
            _control.Clear();
            _gameplayEntity.Clear();
            _physics.Clear();
            _colliderPosture.Clear();
            _combatState.Clear();
            _bodyPartState.Clear();
            _lifecycle.Clear();
            _presence.Clear();
            _battleState.Clear();
            _specialHitState.Clear();
            _specialHitSusceptibility.Clear();
            _input.Clear();
            _animation.Clear();
            _animationRequest.Clear();
            _action.Clear();
            _authorityPose.Clear();
            _terrainIk.Clear();
            _livingNaturalAttributes.Clear();
            _movement.Clear();
            _groundPointTraction.Clear();
            _positionRecall.Clear();
            _interactionEffects.Clear();
            _requestBuffers.Clear();
        }

        public bool TryGetComponent<T>(CharacterTestCharacterEntityId id, out T component)
        {
            if (typeof(T) == typeof(CharacterTestCharacterIdentityComponent)
                && _identity.TryGet(id, out CharacterTestCharacterIdentityComponent identity))
                return Return(identity, out component);
            if (typeof(T) == typeof(CharacterTestCharacterTeamComponent)
                && _team.TryGet(id, out CharacterTestCharacterTeamComponent team))
                return Return(team, out component);
            if (typeof(T) == typeof(CharacterTestCharacterTransformComponent)
                && _transform.TryGet(id, out CharacterTestCharacterTransformComponent transform))
                return Return(transform, out component);
            if (typeof(T) == typeof(CharacterTestCharacterEquipmentComponent)
                && _equipment.TryGet(id, out CharacterTestCharacterEquipmentComponent equipment))
                return Return(equipment, out component);
            if (typeof(T) == typeof(CharacterTestCharacterControlComponent)
                && _control.TryGet(id, out CharacterTestCharacterControlComponent control))
                return Return(control, out component);
            if (typeof(T) == typeof(GameplayEntityId)
                && _gameplayEntity.TryGet(id, out GameplayEntityId gameplayEntityId))
                return Return(gameplayEntityId, out component);
            if (typeof(T) == typeof(CharacterTestCharacterPhysicsComponent)
                && _physics.TryGet(id, out CharacterTestCharacterPhysicsComponent physics))
                return Return(physics, out component);
            if (typeof(T) == typeof(CharacterTestColliderPostureComponent)
                && _colliderPosture.TryGet(id, out CharacterTestColliderPostureComponent colliderPosture))
                return Return(colliderPosture, out component);
            if (typeof(T) == typeof(CharacterTestCharacterCombatStateComponent)
                && _combatState.TryGet(id, out CharacterTestCharacterCombatStateComponent combatState))
                return Return(ApplyLifecycle(id, combatState), out component);
            if (typeof(T) == typeof(CharacterTestCharacterLifecycleComponent)
                && _lifecycle.TryGet(id, out CharacterTestCharacterLifecycleComponent lifecycle))
                return Return(lifecycle, out component);
            if (typeof(T) == typeof(CharacterTestEntityPresenceComponent)
                && _presence.TryGet(id, out CharacterTestEntityPresenceComponent presence))
                return Return(presence, out component);
            if (typeof(T) == typeof(CharacterTestBattleStateComponent)
                && _battleState.TryGet(id, out CharacterTestBattleStateComponent battleState))
                return Return(battleState, out component);
            if (typeof(T) == typeof(CharacterTestSpecialHitStateComponent)
                && _specialHitState.TryGet(id, out CharacterTestSpecialHitStateComponent specialHitState))
                return Return(specialHitState.IsValid ? specialHitState : CharacterTestSpecialHitStateComponent.None, out component);
            if (typeof(T) == typeof(CharacterTestSpecialHitSusceptibilityComponent)
                && _specialHitSusceptibility.TryGet(id, out CharacterTestSpecialHitSusceptibilityComponent susceptibility))
                return Return(susceptibility.IsValid ? susceptibility : CharacterTestSpecialHitSusceptibilityComponent.Disabled, out component);
            if (typeof(T) == typeof(CharacterTestCharacterInputComponent)
                && _input.TryGet(id, out CharacterTestCharacterInputComponent input))
                return Return(input, out component);
            if (typeof(T) == typeof(CharacterTestCharacterAnimationComponent)
                && _animation.TryGet(id, out CharacterTestCharacterAnimationComponent animation))
                return Return(animation, out component);
            if (typeof(T) == typeof(CharacterTestCharacterAnimationRequestComponent)
                && _animationRequest.TryGet(id, out CharacterTestCharacterAnimationRequestComponent animationRequest))
                return Return(
                    animationRequest.IsValid
                        ? animationRequest
                        : CharacterTestCharacterAnimationRequestComponent.None,
                    out component);
            if (typeof(T) == typeof(CharacterTestCharacterActionComponent)
                && _action.TryGet(id, out CharacterTestCharacterActionComponent action))
                return Return(action, out component);
            if (typeof(T) == typeof(CharacterAuthorityPoseStateComponent)
                && _authorityPose.TryGet(id, out CharacterAuthorityPoseStateComponent authorityPose))
                return Return(authorityPose, out component);
            if (typeof(T) == typeof(CharacterTestTerrainIkStateComponent)
                && _terrainIk.TryGet(id, out CharacterTestTerrainIkStateComponent terrainIk))
                return Return(terrainIk, out component);
            if (typeof(T) == typeof(CharacterTestLivingNaturalAttributesRuntimeState)
                && _livingNaturalAttributes.TryGet(id, out CharacterTestLivingNaturalAttributesRuntimeState natural))
                return Return(natural, out component);
            if (typeof(T) == typeof(CharacterTestCharacterMovementStateComponent)
                && _movement.TryGet(id, out CharacterTestCharacterMovementStateComponent movement))
                return Return(movement, out component);

            if (typeof(T) == typeof(CharacterTestPositionRecallState) && _positionRecall.TryGet(id, out CharacterTestPositionRecallState recall))
            { component = (T)(object)recall; return true; }
            if (typeof(T) == typeof(CharacterTestGroundPointTractionState)
                && _groundPointTraction.TryGet(id, out CharacterTestGroundPointTractionState traction))
                return Return(traction, out component);

            component = default;
            return false;
        }

        public bool SetComponent<T>(CharacterTestCharacterEntityId id, T component)
        {
            if (!_identity.Contains(id))
                return false;

            if (typeof(T) == typeof(CharacterTestCharacterIdentityComponent))
            {
                _identity.Set(id, (CharacterTestCharacterIdentityComponent)(object)component);
                return true;
            }
            if (typeof(T) == typeof(CharacterTestCharacterEquipmentComponent))
            {
                _equipment.Set(id, (CharacterTestCharacterEquipmentComponent)(object)component);
                return true;
            }
            if (typeof(T) == typeof(CharacterTestCharacterTransformComponent))
            {
                SetTransform(id, (CharacterTestCharacterTransformComponent)(object)component);
                return true;
            }
            if (typeof(T) == typeof(CharacterTestCharacterControlComponent))
            {
                _control.Set(id, (CharacterTestCharacterControlComponent)(object)component);
                return true;
            }
            if (typeof(T) == typeof(CharacterTestCharacterPhysicsComponent))
            {
                CharacterTestCharacterPhysicsComponent physics =
                    (CharacterTestCharacterPhysicsComponent)(object)component;
                SetPhysics(id, physics);
                return true;
            }
            if (typeof(T) == typeof(CharacterTestColliderPostureComponent))
            {
                _colliderPosture.Set(id, (CharacterTestColliderPostureComponent)(object)component);
                return true;
            }
            if (typeof(T) == typeof(CharacterTestCharacterCombatStateComponent))
            {
                SetCombatState(id, (CharacterTestCharacterCombatStateComponent)(object)component);
                return true;
            }
            if (typeof(T) == typeof(CharacterTestCharacterLifecycleComponent))
            {
                _lifecycle.Set(id, (CharacterTestCharacterLifecycleComponent)(object)component);
                return true;
            }
            if (typeof(T) == typeof(CharacterTestEntityPresenceComponent))
            {
                _presence.Set(id, (CharacterTestEntityPresenceComponent)(object)component);
                return true;
            }
            if (typeof(T) == typeof(CharacterTestBattleStateComponent))
            {
                _battleState.Set(id, (CharacterTestBattleStateComponent)(object)component);
                return true;
            }
            if (typeof(T) == typeof(CharacterTestSpecialHitStateComponent))
            {
                _specialHitState.Set(id, (CharacterTestSpecialHitStateComponent)(object)component);
                return true;
            }
            if (typeof(T) == typeof(CharacterTestSpecialHitSusceptibilityComponent))
            {
                _specialHitSusceptibility.Set(
                    id,
                    (CharacterTestSpecialHitSusceptibilityComponent)(object)component);
                return true;
            }
            if (typeof(T) == typeof(CharacterTestCharacterInputComponent))
            {
                _input.Set(id, (CharacterTestCharacterInputComponent)(object)component);
                return true;
            }
            if (typeof(T) == typeof(CharacterTestCharacterAnimationComponent))
            {
                _animation.Set(id, (CharacterTestCharacterAnimationComponent)(object)component);
                return true;
            }
            if (typeof(T) == typeof(CharacterTestCharacterAnimationRequestComponent))
            {
                _animationRequest.Set(
                    id,
                    ((CharacterTestCharacterAnimationRequestComponent)(object)component).IsValid
                        ? (CharacterTestCharacterAnimationRequestComponent)(object)component
                        : CharacterTestCharacterAnimationRequestComponent.None);
                return true;
            }
            if (typeof(T) == typeof(CharacterTestCharacterActionComponent))
            {
                _action.Set(id, (CharacterTestCharacterActionComponent)(object)component);
                return true;
            }
            if (typeof(T) == typeof(CharacterAuthorityPoseStateComponent))
            {
                var authorityPose = (CharacterAuthorityPoseStateComponent)(object)component;
                _authorityPose.Set(
                    id,
                    authorityPose ?? new CharacterAuthorityPoseStateComponent(
                        CharacterTestAuthorityPoseDefinitionSource.CreateDefaultDefinition()));
                return true;
            }
            if (typeof(T) == typeof(CharacterTestTerrainIkStateComponent))
            {
                CharacterTestTerrainIkStateComponent terrainIk =
                    (CharacterTestTerrainIkStateComponent)(object)component;
                _terrainIk.Set(id, terrainIk ?? new CharacterTestTerrainIkStateComponent(id.Value));
                return true;
            }
            if (typeof(T) == typeof(CharacterTestLivingNaturalAttributesRuntimeState))
            {
                _livingNaturalAttributes.Set(
                    id,
                    (CharacterTestLivingNaturalAttributesRuntimeState)(object)component);
                return true;
            }
            if (typeof(T) == typeof(CharacterTestCharacterMovementStateComponent))
            {
                _movement.Set(id, (CharacterTestCharacterMovementStateComponent)(object)component);
                return true;
            }

            if (typeof(T) == typeof(CharacterTestPositionRecallState))
            { _positionRecall.Set(id, (CharacterTestPositionRecallState)(object)component); return true; }
            if (typeof(T) == typeof(CharacterTestGroundPointTractionState))
            {
                _groundPointTraction.Set(id, (CharacterTestGroundPointTractionState)(object)component);
                return true;
            }
            return false;
        }

        public CharacterTestCharacterRequestBufferComponent GetMutableRequestBuffer(
            CharacterTestCharacterEntityId id)
        {
            if (_requestBuffers.TryGet(id, out CharacterTestCharacterRequestBufferComponent buffers))
                return buffers;

            throw new InvalidOperationException(
                "CharacterTest request buffers are only created during character spawn.");
        }

        public CharacterTestGameplayIntentBuffer GetGameplayIntentBuffer(
            CharacterTestCharacterEntityId id)
        {
            return GetMutableRequestBuffer(id).GameplayIntentBuffer;
        }

        public void EnqueueMotionRequest(
            CharacterTestCharacterEntityId id,
            CharacterTestCharacterMotionRequest request)
        {
            if (request.HasValue)
                GetMutableRequestBuffer(id).MotionRequests.Add(request);
        }

        public void EnqueueCombatRequest(
            CharacterTestCharacterEntityId id,
            CharacterActionCombatRequest request)
        {
            if (request.EventKind != CharacterActionTrackEventKind.None)
                GetMutableRequestBuffer(id).CombatRequests.Add(request);
        }

        public void EnqueueGameplayRequest(
            CharacterTestCharacterEntityId id,
            CharacterActionGameplayRequest request)
        {
            if (request.EventKind != CharacterActionTrackEventKind.None)
                GetMutableRequestBuffer(id).GameplayRequests.Add(request);
        }

        public void EnqueueAudioCueRequest(
            CharacterTestCharacterEntityId id,
            CharacterActionAudioCueRequest request)
        {
            if (request.CueId > 0)
            {
                CharacterTestCharacterRequestBufferComponent buffer = GetMutableRequestBuffer(id);
                buffer.AudioCueRequests.Add(request);
                buffer.AudioCuePresentationKinds.Add(CharacterTestAudioCueRequestClassifier.Classify(request));
            }
        }

        public void EnqueueVfxRequest(
            CharacterTestCharacterEntityId id,
            CharacterActionVfxRequest request)
        {
            if (!string.IsNullOrEmpty(request.ResourceKey))
                GetMutableRequestBuffer(id).VfxRequests.Add(request);
        }

        public void ClearPresentationRequests(CharacterTestCharacterEntityId id)
        {
            GetMutableRequestBuffer(id).ClearPresentationRequests();
        }

        public void ClearGameplayIntentBuffer(CharacterTestCharacterEntityId id)
        {
            GetMutableRequestBuffer(id).GameplayIntentBuffer.Clear();
        }

        public void ConsumeJumpBuffer(CharacterTestCharacterEntityId id)
        {
            SetMovement(id, GetMovement(id).WithoutJumpBuffer());
        }

        public CharacterTestCharacterIdentityComponent GetIdentity(CharacterTestCharacterEntityId id)
        {
            _identity.TryGet(id, out CharacterTestCharacterIdentityComponent component);
            return component;
        }

        public CharacterTestCharacterTeamComponent GetTeam(CharacterTestCharacterEntityId id)
        {
            _team.TryGet(id, out CharacterTestCharacterTeamComponent component);
            return component;
        }

        public CharacterTestCharacterTransformComponent GetTransform(CharacterTestCharacterEntityId id)
        {
            _transform.TryGet(id, out CharacterTestCharacterTransformComponent component);
            return component;
        }

        public CharacterTestCharacterSourceRevisionComponent GetSourceRevisions(
            CharacterTestCharacterEntityId id)
        {
            if (_sourceRevisions.TryGet(
                    id,
                    out CharacterTestCharacterSourceRevisionComponent component))
            {
                return component;
            }

            return new CharacterTestCharacterSourceRevisionComponent(
                CharacterTestCharacterSourceRevisionComponent.InitialRevision,
                CharacterTestCharacterSourceRevisionComponent.InitialRevision);
        }

        public CharacterTestCharacterEquipmentComponent GetEquipment(CharacterTestCharacterEntityId id)
        {
            _equipment.TryGet(id, out CharacterTestCharacterEquipmentComponent component);
            return component;
        }

        public CharacterTestCharacterControlComponent GetControl(CharacterTestCharacterEntityId id)
        {
            _control.TryGet(id, out CharacterTestCharacterControlComponent component);
            return component;
        }

        public GameplayEntityId GetGameplayEntityId(CharacterTestCharacterEntityId id)
        {
            _gameplayEntity.TryGet(id, out GameplayEntityId component);
            return component;
        }

        public CharacterTestCharacterPhysicsComponent GetPhysics(CharacterTestCharacterEntityId id)
        {
            _physics.TryGet(id, out CharacterTestCharacterPhysicsComponent component);
            return component;
        }

        public CharacterTestColliderPostureComponent GetColliderPosture(CharacterTestCharacterEntityId id)
        {
            if (_colliderPosture.TryGet(id, out CharacterTestColliderPostureComponent component)
                && component.IsValid)
            {
                return component;
            }

            return CharacterTestColliderPostureComponent.FromPhysics(GetPhysics(id), frame: 0L);
        }

        public CharacterTestCharacterCombatStateComponent GetCombatState(CharacterTestCharacterEntityId id)
        {
            _combatState.TryGet(id, out CharacterTestCharacterCombatStateComponent component);
            return ApplyLifecycle(id, component);
        }

        internal CharacterTestBodyPartStateComponent GetBodyPartState(
            CharacterTestCharacterEntityId id)
        {
            _bodyPartState.TryGet(id, out CharacterTestBodyPartStateComponent component);
            return component;
        }

        public CharacterTestCharacterLifecycleComponent GetLifecycle(CharacterTestCharacterEntityId id)
        {
            _lifecycle.TryGet(id, out CharacterTestCharacterLifecycleComponent component);
            return component;
        }

        public CharacterTestEntityPresenceComponent GetPresence(CharacterTestCharacterEntityId id)
        {
            _presence.TryGet(id, out CharacterTestEntityPresenceComponent component);
            return component.IsValid ? component : CharacterTestEntityPresenceComponent.Active;
        }

        public CharacterTestBattleStateComponent GetBattleState(CharacterTestCharacterEntityId id)
        {
            _battleState.TryGet(id, out CharacterTestBattleStateComponent component);
            return component.IsValid ? component : CharacterTestBattleStateComponent.Alive;
        }

        public CharacterTestSpecialHitStateComponent GetSpecialHitState(CharacterTestCharacterEntityId id)
        {
            _specialHitState.TryGet(id, out CharacterTestSpecialHitStateComponent component);
            return component.IsValid ? component : CharacterTestSpecialHitStateComponent.None;
        }

        public CharacterTestSpecialHitSusceptibilityComponent GetSpecialHitSusceptibility(
            CharacterTestCharacterEntityId id)
        {
            _specialHitSusceptibility.TryGet(id, out CharacterTestSpecialHitSusceptibilityComponent component);
            return component.IsValid ? component : CharacterTestSpecialHitSusceptibilityComponent.Disabled;
        }

        public CharacterTestCharacterInputComponent GetInput(CharacterTestCharacterEntityId id)
        {
            _input.TryGet(id, out CharacterTestCharacterInputComponent component);
            return component;
        }

        public CharacterTestCharacterAnimationComponent GetAnimation(CharacterTestCharacterEntityId id)
        {
            _animation.TryGet(id, out CharacterTestCharacterAnimationComponent component);
            return component;
        }

        public CharacterTestCharacterAnimationRequestComponent GetAnimationRequest(
            CharacterTestCharacterEntityId id)
        {
            _animationRequest.TryGet(id, out CharacterTestCharacterAnimationRequestComponent component);
            return component.IsValid ? component : CharacterTestCharacterAnimationRequestComponent.None;
        }

        public CharacterTestCharacterActionComponent GetAction(CharacterTestCharacterEntityId id)
        {
            _action.TryGet(id, out CharacterTestCharacterActionComponent component);
            return component;
        }

        public CharacterAuthorityPoseStateComponent GetAuthorityPose(CharacterTestCharacterEntityId id)
        {
            if (_authorityPose.TryGet(id, out CharacterAuthorityPoseStateComponent component))
                return component;

            return null;
        }

        internal CharacterTestTerrainIkStateComponent GetTerrainIk(
            CharacterTestCharacterEntityId id)
        {
            if (_terrainIk.TryGet(id, out CharacterTestTerrainIkStateComponent component))
                return component;
            return null;
        }

        public CharacterTestLivingNaturalAttributesRuntimeState GetLivingNaturalAttributes(
            CharacterTestCharacterEntityId id)
        {
            _livingNaturalAttributes.TryGet(id, out CharacterTestLivingNaturalAttributesRuntimeState component);
            return component;
        }

        public CharacterTestCharacterMovementStateComponent GetMovement(
            CharacterTestCharacterEntityId id)
        {
            _movement.TryGet(id, out CharacterTestCharacterMovementStateComponent component);
            return component;
        }

        public void SetTransform(
            CharacterTestCharacterEntityId id,
            CharacterTestCharacterTransformComponent component)
        {
            CharacterTestCharacterTransformComponent previous = GetTransform(id);
            CharacterTestCharacterSourceRevisionComponent revisions = GetSourceRevisions(id);
            _transform.Set(id, component);
            _sourceRevisions.Set(id, revisions.TouchTransform(previous, component));
        }

        public void SetTeam(CharacterTestCharacterEntityId id, CharacterTestCharacterTeamComponent component) =>
            _team.Set(id, component);

        public void TouchAuthorityPoseRevision(CharacterTestCharacterEntityId id)
        {
            CharacterTestCharacterSourceRevisionComponent revisions = GetSourceRevisions(id);
            _sourceRevisions.Set(id, revisions.TouchAuthorityPose());
        }

        public void RestoreSourceRevisions(
            CharacterTestCharacterEntityId id,
            int poseRevision,
            int facingRevision)
        {
            CharacterTestCharacterSourceRevisionComponent revisions = GetSourceRevisions(id);
            _sourceRevisions.Set(id, revisions.Restore(poseRevision, facingRevision));
        }
        public void SetControl(CharacterTestCharacterEntityId id, CharacterTestCharacterControlComponent component) =>
            _control.Set(id, component);
        public void SetPhysics(CharacterTestCharacterEntityId id, CharacterTestCharacterPhysicsComponent component)
        {
            CharacterTestCharacterPhysicsComponent physics = component.IsConfigured
                ? component
                : CharacterTestCharacterPhysicsComponent.Default;
            _physics.Set(id, physics);
            CharacterTestColliderPostureComponent previous = GetColliderPosture(id);
            _colliderPosture.Set(
                id,
                CharacterTestColliderPostureComponent.FromPhysics(physics, previous.SinceFrame)
                    .WithActionOverride(
                        previous.ActionProfileId,
                        previous.ActionRestorePolicy,
                        previous.ActionTransitionFrames,
                        previous.ActionSourceActionId,
                        previous.ActionStableEventId));
        }
        public void SetColliderPosture(
            CharacterTestCharacterEntityId id,
            CharacterTestColliderPostureComponent component) =>
            _colliderPosture.Set(
                id,
                component.IsValid
                    ? component
                    : CharacterTestColliderPostureComponent.FromPhysics(GetPhysics(id), frame: 0L));
        public void SetCombatState(CharacterTestCharacterEntityId id, CharacterTestCharacterCombatStateComponent component)
        {
            _combatState.Set(id, component);
            _lifecycle.Set(id, CharacterTestCharacterLifecycleComponent.FromCombatState(component));
        }
        public void SetLifecycle(CharacterTestCharacterEntityId id, CharacterTestCharacterLifecycleComponent component) =>
            _lifecycle.Set(id, component);
        public void SetPresence(CharacterTestCharacterEntityId id, CharacterTestEntityPresenceComponent component) =>
            _presence.Set(id, component.IsValid ? component : CharacterTestEntityPresenceComponent.Active);
        public void SetBattleState(CharacterTestCharacterEntityId id, CharacterTestBattleStateComponent component) =>
            _battleState.Set(id, component.IsValid ? component : CharacterTestBattleStateComponent.Alive);
        public void SetSpecialHitState(
            CharacterTestCharacterEntityId id,
            CharacterTestSpecialHitStateComponent component) =>
            _specialHitState.Set(id, component.IsValid ? component : CharacterTestSpecialHitStateComponent.None);
        public void SetSpecialHitSusceptibility(
            CharacterTestCharacterEntityId id,
            CharacterTestSpecialHitSusceptibilityComponent component) =>
            _specialHitSusceptibility.Set(
                id,
                component.IsValid ? component : CharacterTestSpecialHitSusceptibilityComponent.Disabled);
        public void SetInput(CharacterTestCharacterEntityId id, CharacterTestCharacterInputComponent component) =>
            _input.Set(id, component);
        public void SetAnimation(CharacterTestCharacterEntityId id, CharacterTestCharacterAnimationComponent component) =>
            _animation.Set(id, component);
        public void SetAnimationRequest(
            CharacterTestCharacterEntityId id,
            CharacterTestCharacterAnimationRequestComponent component) =>
            _animationRequest.Set(
                id,
                component.IsValid ? component : CharacterTestCharacterAnimationRequestComponent.None);
        public void MarkAnimationRequestEmitted(CharacterTestCharacterEntityId id, long frame)
        {
            CharacterTestCharacterAnimationRequestComponent request = GetAnimationRequest(id);
            if (!request.IsPending)
                return;

            _animationRequest.Set(id, request.MarkEmitted(frame));
        }
        public void ClearAnimationRequest(CharacterTestCharacterEntityId id) =>
            _animationRequest.Set(id, CharacterTestCharacterAnimationRequestComponent.None);
        public void ClearAnimationRequestsBefore(long frame) =>
            ClearAnimationRequests(frame, includeFrame: false);
        public void ClearAnimationRequestsAtOrBefore(long frame) =>
            ClearAnimationRequests(frame, includeFrame: true);
        public void SetAction(CharacterTestCharacterEntityId id, CharacterTestCharacterActionComponent component) =>
            _action.Set(id, component);
        public void SetLivingNaturalAttributes(
            CharacterTestCharacterEntityId id,
            CharacterTestLivingNaturalAttributesRuntimeState component) =>
            _livingNaturalAttributes.Set(id, component);
        public void SetAuthorityPose(
            CharacterTestCharacterEntityId id,
            CharacterAuthorityPoseStateComponent component) =>
            _authorityPose.Set(
                id,
                component ?? new CharacterAuthorityPoseStateComponent(
                    CharacterTestAuthorityPoseDefinitionSource.CreateDefaultDefinition()));
        internal void SetTerrainIk(
            CharacterTestCharacterEntityId id,
            CharacterTestTerrainIkStateComponent component) =>
            _terrainIk.Set(id, component ?? new CharacterTestTerrainIkStateComponent(id.Value));
        public void SetMovement(
            CharacterTestCharacterEntityId id,
            CharacterTestCharacterMovementStateComponent component) =>
            _movement.Set(id, component);

        private static bool Return<TComponent, TResult>(TComponent value, out TResult result)
        {
            result = (TResult)(object)value;
            return true;
        }

        private void ClearAnimationRequests(long frame, bool includeFrame)
        {
            for (int i = 0; i < _entityIds.Count; i++)
            {
                CharacterTestCharacterEntityId id = _entityIds[i];
                CharacterTestCharacterAnimationRequestComponent request = GetAnimationRequest(id);
                if (!request.IsValid)
                    continue;

                if (request.IsPending)
                {
                    bool shouldExpire = includeFrame
                        ? request.ExpiresFrame <= frame
                        : request.ExpiresFrame < frame;
                    if (shouldExpire)
                        _animationRequest.Set(id, request.MarkExpired(frame));
                    continue;
                }

                long terminalFrame = request.TerminalFrame;
                bool shouldClearTerminal = includeFrame
                    ? terminalFrame <= frame
                    : terminalFrame < frame;
                if (shouldClearTerminal)
                    ClearAnimationRequest(id);
            }
        }

        private CharacterTestCharacterCombatStateComponent ApplyLifecycle(
            CharacterTestCharacterEntityId id,
            CharacterTestCharacterCombatStateComponent combatState)
        {
            return _lifecycle.TryGet(id, out CharacterTestCharacterLifecycleComponent lifecycle)
                ? combatState.WithLifecycle(lifecycle)
                : combatState;
        }

        private void InsertEntityId(CharacterTestCharacterEntityId id)
        {
            int index = _entityIds.Count;
            for (int i = 0; i < _entityIds.Count; i++)
            {
                if (_entityIds[i].Value > id.Value)
                {
                    index = i;
                    break;
                }
            }

            _entityIds.Insert(index, id);
        }

        private void RemoveEntityId(CharacterTestCharacterEntityId id)
        {
            for (int i = 0; i < _entityIds.Count; i++)
            {
                if (_entityIds[i].Value != id.Value)
                    continue;

                _entityIds.RemoveAt(i);
                return;
            }
        }
    }

    internal sealed class CharacterTestComponentStore<T>
    {
        private readonly Dictionary<int, T> _components = new Dictionary<int, T>();

        public int Count => _components.Count;

        public bool Contains(CharacterTestCharacterEntityId id)
        {
            return id.IsValid && _components.ContainsKey(id.Value);
        }

        public bool TryGet(CharacterTestCharacterEntityId id, out T component)
        {
            if (!id.IsValid)
            {
                component = default;
                return false;
            }

            return _components.TryGetValue(id.Value, out component);
        }

        public void Set(CharacterTestCharacterEntityId id, T component)
        {
            if (!id.IsValid)
                throw new ArgumentException("CharacterTest component store requires a valid entity id.", nameof(id));

            _components[id.Value] = component;
        }

        public bool Remove(CharacterTestCharacterEntityId id)
        {
            return id.IsValid && _components.Remove(id.Value);
        }

        public void Clear()
        {
            _components.Clear();
        }
    }

    internal sealed class CharacterTestCharacterRequestBufferComponent
    {
        public CharacterTestCharacterRequestBufferComponent()
        {
            MotionRequests = new List<CharacterTestCharacterMotionRequest>();
            CombatRequests = new List<CharacterActionCombatRequest>();
            GameplayRequests = new List<CharacterActionGameplayRequest>();
            AudioCueRequests = new List<CharacterActionAudioCueRequest>();
            AudioCuePresentationKinds = new List<CharacterTestAudioPresentationKind>();
            VfxRequests = new List<CharacterActionVfxRequest>();
            LastCombatHitResults = new List<HitResolveResult>();
            GameplayIntentBuffer = new CharacterTestGameplayIntentBuffer();
        }

        public List<CharacterTestCharacterMotionRequest> MotionRequests { get; }
        public List<CharacterActionCombatRequest> CombatRequests { get; }
        public List<CharacterActionGameplayRequest> GameplayRequests { get; }
        public List<CharacterActionAudioCueRequest> AudioCueRequests { get; }
        public List<CharacterTestAudioPresentationKind> AudioCuePresentationKinds { get; }
        public List<CharacterActionVfxRequest> VfxRequests { get; }
        public List<HitResolveResult> LastCombatHitResults { get; }
        public CharacterTestGameplayIntentBuffer GameplayIntentBuffer { get; }

        public void ClearPresentationRequests()
        {
            MotionRequests.Clear();
            CombatRequests.Clear();
            GameplayRequests.Clear();
            AudioCueRequests.Clear();
            AudioCuePresentationKinds.Clear();
            VfxRequests.Clear();
            LastCombatHitResults.Clear();
        }
    }

    internal enum CharacterTestEntityPresenceState
    {
        Active = 0,
        PendingDestroy = 1,
        Destroyed = 2
    }

    internal readonly struct CharacterTestEntityPresenceComponent
    {
        public static CharacterTestEntityPresenceComponent Active =>
            new CharacterTestEntityPresenceComponent(
                CharacterTestEntityPresenceState.Active,
                requestedFrame: -1L,
                destroyedFrame: -1L,
                sourceEntityId: 0,
                traceId: RuntimeTraceIdentity.None,
                isValid: true);

        public CharacterTestEntityPresenceComponent(
            CharacterTestEntityPresenceState state,
            long requestedFrame,
            long destroyedFrame,
            int sourceEntityId,
            RuntimeTraceIdentity traceId)
            : this(state, requestedFrame, destroyedFrame, sourceEntityId, traceId, isValid: true)
        {
        }

        private CharacterTestEntityPresenceComponent(
            CharacterTestEntityPresenceState state,
            long requestedFrame,
            long destroyedFrame,
            int sourceEntityId,
            RuntimeTraceIdentity traceId,
            bool isValid)
        {
            State = NormalizeState(state);
            RequestedFrame = requestedFrame >= 0L ? requestedFrame : -1L;
            DestroyedFrame = destroyedFrame >= 0L ? destroyedFrame : -1L;
            SourceEntityId = sourceEntityId > 0 ? sourceEntityId : 0;
            TraceId = traceId;
            IsValid = isValid;
        }

        public CharacterTestEntityPresenceState State { get; }
        public long RequestedFrame { get; }
        public long DestroyedFrame { get; }
        public int SourceEntityId { get; }
        public RuntimeTraceIdentity TraceId { get; }
        public bool IsValid { get; }
        public bool IsActive => State == CharacterTestEntityPresenceState.Active;
        public bool IsPendingDestroy => State == CharacterTestEntityPresenceState.PendingDestroy;
        public bool IsDestroyed => State == CharacterTestEntityPresenceState.Destroyed;

        public CharacterTestEntityPresenceComponent WithPendingDestroy(
            long frame,
            int sourceEntityId,
            RuntimeTraceIdentity traceId)
        {
            return new CharacterTestEntityPresenceComponent(
                CharacterTestEntityPresenceState.PendingDestroy,
                requestedFrame: frame,
                destroyedFrame: -1L,
                sourceEntityId: sourceEntityId,
                traceId: traceId);
        }

        public CharacterTestEntityPresenceComponent WithDestroyed(long frame)
        {
            return new CharacterTestEntityPresenceComponent(
                CharacterTestEntityPresenceState.Destroyed,
                requestedFrame: RequestedFrame,
                destroyedFrame: frame,
                sourceEntityId: SourceEntityId,
                traceId: TraceId);
        }

        private static CharacterTestEntityPresenceState NormalizeState(
            CharacterTestEntityPresenceState state)
        {
            int value = (int)state;
            return value < (int)CharacterTestEntityPresenceState.Active
                || value > (int)CharacterTestEntityPresenceState.Destroyed
                ? CharacterTestEntityPresenceState.Active
                : state;
        }
    }

    internal enum CharacterTestSpecialHitStateKind
    {
        None = 0,
        Launched = 1
    }

    internal enum CharacterTestSpecialHitPhase
    {
        None = 0,
        Enter = 1,
        Airborne = 2,
        LandingRecovery = 3
    }

    internal readonly struct CharacterTestSpecialHitStateComponent
    {
        public static CharacterTestSpecialHitStateComponent None =>
            new CharacterTestSpecialHitStateComponent(
                CharacterTestSpecialHitStateKind.None,
                sinceFrame: -1L,
                sourceEntityId: 0,
                traceId: 0,
                launchVelocityYMillimetersPerSecond: 0,
                horizontalImpulseMillimetersPerSecond: 0,
                CharacterTestSpecialHitPhase.None,
                isValid: true);

        public CharacterTestSpecialHitStateComponent(
            CharacterTestSpecialHitStateKind state,
            long sinceFrame,
            int sourceEntityId,
            int traceId,
            int launchVelocityYMillimetersPerSecond,
            int horizontalImpulseMillimetersPerSecond,
            CharacterTestSpecialHitPhase phase)
            : this(
                state,
                sinceFrame,
                sourceEntityId,
                traceId,
                launchVelocityYMillimetersPerSecond,
                horizontalImpulseMillimetersPerSecond,
                phase,
                isValid: true)
        {
        }

        private CharacterTestSpecialHitStateComponent(
            CharacterTestSpecialHitStateKind state,
            long sinceFrame,
            int sourceEntityId,
            int traceId,
            int launchVelocityYMillimetersPerSecond,
            int horizontalImpulseMillimetersPerSecond,
            CharacterTestSpecialHitPhase phase,
            bool isValid)
        {
            State = NormalizeState(state);
            SinceFrame = sinceFrame >= 0L ? sinceFrame : -1L;
            SourceEntityId = sourceEntityId > 0 ? sourceEntityId : 0;
            TraceId = traceId > 0 ? traceId : 0;
            LaunchVelocityYMillimetersPerSecond = launchVelocityYMillimetersPerSecond > 0
                ? launchVelocityYMillimetersPerSecond
                : 0;
            HorizontalImpulseMillimetersPerSecond = horizontalImpulseMillimetersPerSecond > 0
                ? horizontalImpulseMillimetersPerSecond
                : 0;
            Phase = NormalizePhase(phase);
            IsValid = isValid;
        }

        public CharacterTestSpecialHitStateKind State { get; }
        public long SinceFrame { get; }
        public int SourceEntityId { get; }
        public int TraceId { get; }
        public int LaunchVelocityYMillimetersPerSecond { get; }
        public int HorizontalImpulseMillimetersPerSecond { get; }
        public CharacterTestSpecialHitPhase Phase { get; }
        public bool IsValid { get; }
        public bool IsLaunched => State == CharacterTestSpecialHitStateKind.Launched;

        public static CharacterTestSpecialHitStateComponent Launched(
            long frame,
            int sourceEntityId,
            int traceId,
            int launchVelocityYMillimetersPerSecond,
            int horizontalImpulseMillimetersPerSecond)
        {
            return new CharacterTestSpecialHitStateComponent(
                CharacterTestSpecialHitStateKind.Launched,
                frame,
                sourceEntityId,
                traceId,
                launchVelocityYMillimetersPerSecond,
                horizontalImpulseMillimetersPerSecond,
                CharacterTestSpecialHitPhase.Enter);
        }

        public CharacterTestSpecialHitStateComponent WithPhase(CharacterTestSpecialHitPhase phase)
        {
            return new CharacterTestSpecialHitStateComponent(
                State,
                SinceFrame,
                SourceEntityId,
                TraceId,
                LaunchVelocityYMillimetersPerSecond,
                HorizontalImpulseMillimetersPerSecond,
                phase);
        }

        public CharacterTestSpecialHitStateComponent WithPhase(CharacterTestSpecialHitPhase phase, long frame)
        {
            return new CharacterTestSpecialHitStateComponent(
                State,
                frame,
                SourceEntityId,
                TraceId,
                LaunchVelocityYMillimetersPerSecond,
                HorizontalImpulseMillimetersPerSecond,
                phase);
        }

        private static CharacterTestSpecialHitStateKind NormalizeState(CharacterTestSpecialHitStateKind state)
        {
            int value = (int)state;
            return value < (int)CharacterTestSpecialHitStateKind.None
                || value > (int)CharacterTestSpecialHitStateKind.Launched
                ? CharacterTestSpecialHitStateKind.None
                : state;
        }

        private static CharacterTestSpecialHitPhase NormalizePhase(CharacterTestSpecialHitPhase phase)
        {
            int value = (int)phase;
            return value < (int)CharacterTestSpecialHitPhase.None
                || value > (int)CharacterTestSpecialHitPhase.LandingRecovery
                ? CharacterTestSpecialHitPhase.None
                : phase;
        }
    }

    internal readonly struct CharacterTestSpecialHitSusceptibilityComponent
    {
        public static CharacterTestSpecialHitSusceptibilityComponent HumanoidDefault =>
            new CharacterTestSpecialHitSusceptibilityComponent(
                canBeLaunched: true,
                requiresBrokenForLaunch: true,
                isValid: true);

        public static CharacterTestSpecialHitSusceptibilityComponent Disabled =>
            new CharacterTestSpecialHitSusceptibilityComponent(
                canBeLaunched: false,
                requiresBrokenForLaunch: true,
                isValid: true);

        public CharacterTestSpecialHitSusceptibilityComponent(
            bool canBeLaunched,
            bool requiresBrokenForLaunch)
            : this(canBeLaunched, requiresBrokenForLaunch, isValid: true)
        {
        }

        private CharacterTestSpecialHitSusceptibilityComponent(
            bool canBeLaunched,
            bool requiresBrokenForLaunch,
            bool isValid)
        {
            CanBeLaunched = canBeLaunched;
            RequiresBrokenForLaunch = requiresBrokenForLaunch;
            IsValid = isValid;
        }

        public bool CanBeLaunched { get; }
        public bool RequiresBrokenForLaunch { get; }
        public bool IsValid { get; }
    }

    internal enum CharacterTestBattleStateDominantReason
    {
        None = 0,
        Death = 1,
        Revive = 2,
        CohesionBreakReaction = 3,
        BrokenHitReaction = 4,
        Stasis = 5,
        ControlLock = 6,
        Launch = 7,
        Destroy = 8
    }

    [Flags]
    internal enum CharacterTestBattleGateFlags
    {
        None = 0,
        CanBeTargeted = 1 << 0,
        CanReceiveHpDamage = 1 << 1,
        CanReceiveScatter = 1 << 2,
        CanStartNormalAction = 1 << 3,
        CanMoveByInput = 1 << 4,
        CanRunRuntimeAiPlanner = 1 << 5,
        KeepsTerminalPose = 1 << 6
    }

    internal readonly struct CharacterTestBattleStateComponent
    {
        private const CharacterTestBattleGateFlags AliveGateFlags =
            CharacterTestBattleGateFlags.CanBeTargeted
            | CharacterTestBattleGateFlags.CanReceiveHpDamage
            | CharacterTestBattleGateFlags.CanReceiveScatter
            | CharacterTestBattleGateFlags.CanStartNormalAction
            | CharacterTestBattleGateFlags.CanMoveByInput
            | CharacterTestBattleGateFlags.CanRunRuntimeAiPlanner;

        public static CharacterTestBattleStateComponent Alive =>
            new CharacterTestBattleStateComponent(
                CharacterTestCharacterLifeState.Alive,
                CharacterTestBattleStateDominantReason.None,
                AliveGateFlags,
                sinceFrame: 0L,
                lastTransitionFrame: 0L,
                sourceActionId: CharacterActionId.None,
                sourceTraceId: 0,
                isValid: true);

        public CharacterTestBattleStateComponent(
            CharacterTestCharacterLifeState lifeState,
            CharacterTestBattleStateDominantReason dominantReason,
            CharacterTestBattleGateFlags gateFlags,
            long sinceFrame,
            long lastTransitionFrame,
            CharacterActionId sourceActionId,
            int sourceTraceId)
            : this(
                lifeState,
                dominantReason,
                gateFlags,
                sinceFrame,
                lastTransitionFrame,
                sourceActionId,
                sourceTraceId,
                isValid: true)
        {
        }

        private CharacterTestBattleStateComponent(
            CharacterTestCharacterLifeState lifeState,
            CharacterTestBattleStateDominantReason dominantReason,
            CharacterTestBattleGateFlags gateFlags,
            long sinceFrame,
            long lastTransitionFrame,
            CharacterActionId sourceActionId,
            int sourceTraceId,
            bool isValid)
        {
            LifeState = NormalizeLifeState(lifeState);
            DominantReason = NormalizeReason(dominantReason);
            GateFlags = gateFlags;
            SinceFrame = sinceFrame >= 0L ? sinceFrame : 0L;
            LastTransitionFrame = lastTransitionFrame >= 0L ? lastTransitionFrame : SinceFrame;
            SourceActionId = sourceActionId;
            SourceTraceId = sourceTraceId > 0 ? sourceTraceId : 0;
            IsValid = isValid;
        }

        public CharacterTestCharacterLifeState LifeState { get; }
        public CharacterTestBattleStateDominantReason DominantReason { get; }
        public CharacterTestBattleGateFlags GateFlags { get; }
        public long SinceFrame { get; }
        public long LastTransitionFrame { get; }
        public CharacterActionId SourceActionId { get; }
        public int SourceTraceId { get; }
        public bool IsValid { get; }
        public bool CanBeTargeted => HasGate(CharacterTestBattleGateFlags.CanBeTargeted);
        public bool CanReceiveHpDamage => HasGate(CharacterTestBattleGateFlags.CanReceiveHpDamage);
        public bool CanReceiveScatter => HasGate(CharacterTestBattleGateFlags.CanReceiveScatter);
        public bool CanStartNormalAction => HasGate(CharacterTestBattleGateFlags.CanStartNormalAction);
        public bool CanMoveByInput => HasGate(CharacterTestBattleGateFlags.CanMoveByInput);
        public bool CanRunRuntimeAiPlanner => HasGate(CharacterTestBattleGateFlags.CanRunRuntimeAiPlanner);
        public bool KeepsTerminalPose => HasGate(CharacterTestBattleGateFlags.KeepsTerminalPose);
        public bool CountsAsAlive => LifeState == CharacterTestCharacterLifeState.Alive && CanBeTargeted;

        internal CharacterTestBattleStateComponent WithStableTransition(
            CharacterTestBattleStateComponent previous,
            long frame)
        {
            if (previous.IsValid
                && previous.LifeState == LifeState
                && previous.DominantReason == DominantReason
                && previous.GateFlags == GateFlags
                && previous.SourceActionId == SourceActionId
                && previous.SourceTraceId == SourceTraceId)
            {
                return new CharacterTestBattleStateComponent(
                    LifeState,
                    DominantReason,
                    GateFlags,
                    previous.SinceFrame,
                    previous.LastTransitionFrame,
                    SourceActionId,
                    SourceTraceId);
            }

            return new CharacterTestBattleStateComponent(
                LifeState,
                DominantReason,
                GateFlags,
                frame,
                frame,
                SourceActionId,
                SourceTraceId);
        }

        private bool HasGate(CharacterTestBattleGateFlags gate)
        {
            return (GateFlags & gate) == gate;
        }

        private static CharacterTestCharacterLifeState NormalizeLifeState(
            CharacterTestCharacterLifeState lifeState)
        {
            int value = (int)lifeState;
            return value < (int)CharacterTestCharacterLifeState.Alive
                || value > (int)CharacterTestCharacterLifeState.Reviving
                ? CharacterTestCharacterLifeState.Alive
                : lifeState;
        }

        private static CharacterTestBattleStateDominantReason NormalizeReason(
            CharacterTestBattleStateDominantReason reason)
        {
            int value = (int)reason;
            return value < (int)CharacterTestBattleStateDominantReason.None
                || value > (int)CharacterTestBattleStateDominantReason.Destroy
                ? CharacterTestBattleStateDominantReason.None
                : reason;
        }
    }

    internal sealed class CharacterTestBattleStateSystem
    {
        /// <summary>
        /// Refreshes the cached battle gates for every CharacterTest entity.
        /// </summary>
        /// <remarks>NoAlloc after initialization; iterates existing runtime stores without allocating collections.</remarks>
        public void Tick(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame,
            ICharacterTestStatusControlProvider statusControl)
        {
            RefreshWorld(world, frame, statusControl);
        }

        public static void RefreshWorld(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame,
            ICharacterTestStatusControlProvider statusControl)
        {
            if (world == null)
                return;

            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
                RefreshEntity(world, entities[i], frame, statusControl);
        }

        public static bool RefreshEntity(
            CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame,
            ICharacterTestStatusControlProvider statusControl)
        {
            if (world == null || entity == null)
                return false;

            CharacterTestBattleStateComponent previous = entity.BattleState;
            CharacterTestBattleStateComponent next = Resolve(entity, frame, statusControl)
                .WithStableTransition(previous, frame.Value);
            world.SetBattleState(entity.Id, next);
            return !AreSame(previous, next);
        }

        internal static bool RefreshEntity(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            RuntimeFrame frame)
        {
            if (components == null || !components.Contains(entityId))
                return false;

            CharacterTestBattleStateComponent previous = components.GetBattleState(entityId);
            CharacterTestBattleStateComponent next = Resolve(components, entityId, frame, null, null)
                .WithStableTransition(previous, frame.Value);
            components.SetBattleState(entityId, next);
            return !AreSame(previous, next);
        }

        internal static CharacterTestBattleStateComponent Resolve(
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame,
            ICharacterTestStatusControlProvider statusControl)
        {
            return Resolve(entity.ComponentWorld, entity.Id, frame, statusControl, entity);
        }

        private static CharacterTestBattleStateComponent Resolve(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            RuntimeFrame frame,
            ICharacterTestStatusControlProvider statusControl,
            CharacterTestCharacterEntity entity)
        {
            CharacterTestCharacterCombatStateComponent combatState = components.GetCombatState(entityId);
            CharacterTestCharacterActionComponent action = components.GetAction(entityId);
            CharacterTestCharacterMovementStateComponent movement = components.GetMovement(entityId);
            CharacterTestEntityPresenceComponent presence = components.GetPresence(entityId);
            CharacterTestSpecialHitStateComponent specialHitState = components.GetSpecialHitState(entityId);
            CharacterTestCharacterLifeState lifeState = combatState.LifeState;
            CharacterTestBattleStateDominantReason reason = ResolveLifecycleReason(lifeState);
            CharacterTestBattleGateFlags gates = CharacterTestBattleGateFlags.None;
            CharacterActionId sourceActionId = action.ActiveActionId;
            int sourceTraceId = combatState.LastDeathTraceId > 0
                ? combatState.LastDeathTraceId
                : combatState.LastHitTraceId;

            if (!presence.IsActive)
            {
                reason = CharacterTestBattleStateDominantReason.Destroy;
                sourceTraceId = 0;
            }
            else if (lifeState == CharacterTestCharacterLifeState.Alive)
            {
                bool actionFrozen = entity != null
                    && statusControl != null
                    && statusControl.IsActionFrozen(entity);
                bool movementLocked = IsMovementLocked(action, movement);
                bool reactionAction = TryResolveActionReason(action, out reason);
                bool broken = combatState.ScatterPressureBand == PressureBand.Broken;

                gates |= CharacterTestBattleGateFlags.CanBeTargeted;
                gates |= CharacterTestBattleGateFlags.CanReceiveHpDamage;
                if (!broken && !specialHitState.IsLaunched)
                    gates |= CharacterTestBattleGateFlags.CanReceiveScatter;

                if (specialHitState.IsLaunched)
                {
                    reason = CharacterTestBattleStateDominantReason.Launch;
                    sourceTraceId = specialHitState.TraceId > 0
                        ? specialHitState.TraceId
                        : sourceTraceId;
                }
                else if (actionFrozen)
                {
                    reason = CharacterTestBattleStateDominantReason.Stasis;
                }
                else if (!reactionAction)
                {
                    if (movementLocked)
                        reason = CharacterTestBattleStateDominantReason.ControlLock;
                    gates |= CharacterTestBattleGateFlags.CanStartNormalAction;
                    gates |= CharacterTestBattleGateFlags.CanRunRuntimeAiPlanner;
                    if (!movementLocked)
                        gates |= CharacterTestBattleGateFlags.CanMoveByInput;
                }
            }
            else if (lifeState == CharacterTestCharacterLifeState.Dying)
            {
                gates |= CharacterTestBattleGateFlags.KeepsTerminalPose;
            }
            else if (lifeState == CharacterTestCharacterLifeState.Dead)
            {
                gates |= CharacterTestBattleGateFlags.KeepsTerminalPose;
            }

            return new CharacterTestBattleStateComponent(
                lifeState,
                reason,
                gates,
                frame.Value,
                frame.Value,
                sourceActionId,
                sourceTraceId);
        }

        private static CharacterTestBattleStateDominantReason ResolveLifecycleReason(
            CharacterTestCharacterLifeState lifeState)
        {
            switch (lifeState)
            {
                case CharacterTestCharacterLifeState.Dying:
                case CharacterTestCharacterLifeState.Dead:
                    return CharacterTestBattleStateDominantReason.Death;
                case CharacterTestCharacterLifeState.Reviving:
                    return CharacterTestBattleStateDominantReason.Revive;
                default:
                    return CharacterTestBattleStateDominantReason.None;
            }
        }

        internal static bool TryResolveActionReason(
            CharacterTestCharacterActionComponent action,
            out CharacterTestBattleStateDominantReason reason)
        {
            reason = CharacterTestBattleStateDominantReason.None;
            CharacterActionId actionId = action.ActiveActionId;
            if (!actionId.IsValid)
                return false;

            if (actionId.Value is 344 or 361 or 407 or 930001 or 930102 or 950004)
            {
                reason = CharacterTestBattleStateDominantReason.Death;
                return true;
            }

            if (actionId.Value == 362)
            {
                reason = CharacterTestBattleStateDominantReason.Revive;
                return true;
            }

            if (actionId.Value is 330 or 337 or 400 or 950005)
            {
                reason = CharacterTestBattleStateDominantReason.CohesionBreakReaction;
                return true;
            }

            if (actionId.Value == 363)
            {
                reason = CharacterTestBattleStateDominantReason.Launch;
                return true;
            }

            if ((actionId.Value >= 331 && actionId.Value <= 343)
                || (actionId.Value >= 401 && actionId.Value <= 406)
                || actionId.Value is 950006 or 950007)
            {
                reason = CharacterTestBattleStateDominantReason.BrokenHitReaction;
                return true;
            }

            return false;
        }

        private static bool IsMovementLocked(
            CharacterTestCharacterActionComponent action,
            CharacterTestCharacterMovementStateComponent movement)
        {
            return action.MovementLocked
                || movement.MovementLocked
                || action.MovementMode == CharacterMovementMode.ControlLocked
                || movement.MovementMode == CharacterMovementMode.ControlLocked;
        }

        private static bool ContainsOrdinal(string value, string pattern)
        {
            return value.IndexOf(pattern, StringComparison.Ordinal) >= 0;
        }

        private static bool AreSame(
            CharacterTestBattleStateComponent lhs,
            CharacterTestBattleStateComponent rhs)
        {
            return lhs.IsValid == rhs.IsValid
                && lhs.LifeState == rhs.LifeState
                && lhs.DominantReason == rhs.DominantReason
                && lhs.GateFlags == rhs.GateFlags
                && lhs.SinceFrame == rhs.SinceFrame
                && lhs.LastTransitionFrame == rhs.LastTransitionFrame
                && lhs.SourceActionId == rhs.SourceActionId
                && lhs.SourceTraceId == rhs.SourceTraceId;
        }
    }

    internal readonly struct CharacterTestCharacterLifecycleComponent
    {
        public static CharacterTestCharacterLifecycleComponent Alive =>
            new CharacterTestCharacterLifecycleComponent(
                CharacterTestCharacterLifeState.Alive,
                deathFrame: -1L,
                reviveFrame: -1L,
                lastDeathSourceEntityId: 0,
                lastDeathTraceId: 0);

        public CharacterTestCharacterLifecycleComponent(
            CharacterTestCharacterLifeState lifeState,
            long deathFrame,
            long reviveFrame,
            int lastDeathSourceEntityId,
            int lastDeathTraceId)
        {
            LifeState = NormalizeLifeState(lifeState);
            DeathFrame = deathFrame >= 0L ? deathFrame : -1L;
            ReviveFrame = reviveFrame >= 0L ? reviveFrame : -1L;
            LastDeathSourceEntityId = lastDeathSourceEntityId > 0 ? lastDeathSourceEntityId : 0;
            LastDeathTraceId = lastDeathTraceId > 0 ? lastDeathTraceId : 0;
        }

        public CharacterTestCharacterLifeState LifeState { get; }
        public long DeathFrame { get; }
        public long ReviveFrame { get; }
        public int LastDeathSourceEntityId { get; }
        public int LastDeathTraceId { get; }
        public bool IsAlive => LifeState == CharacterTestCharacterLifeState.Alive;

        public static CharacterTestCharacterLifecycleComponent FromCombatState(
            CharacterTestCharacterCombatStateComponent combatState)
        {
            return new CharacterTestCharacterLifecycleComponent(
                combatState.LifeState,
                combatState.DeathFrame,
                combatState.ReviveFrame,
                combatState.LastDeathSourceEntityId,
                combatState.LastDeathTraceId);
        }

        private static CharacterTestCharacterLifeState NormalizeLifeState(
            CharacterTestCharacterLifeState lifeState)
        {
            int value = (int)lifeState;
            return value < (int)CharacterTestCharacterLifeState.Alive
                || value > (int)CharacterTestCharacterLifeState.Reviving
                ? CharacterTestCharacterLifeState.Alive
                : lifeState;
        }
    }

    internal readonly struct CharacterTestCharacterMovementStateComponent
    {
        public static CharacterTestCharacterMovementStateComponent Default =>
            new CharacterTestCharacterMovementStateComponent(
                verticalVelocityMillimetersPerSecond: 0,
                horizontalVelocityXMillimetersPerSecond: 0,
                horizontalVelocityYMillimetersPerSecond: 0,
                desiredVelocityXMillimetersPerSecond: 0,
                desiredVelocityYMillimetersPerSecond: 0,
                lastMoveInputXMilli: 0,
                lastMoveInputYMilli: 0,
                isGrounded: false,
                jumpBufferFramesRemaining: 0,
                coyoteTimeFramesRemaining: 0,
                movementMode: CharacterMovementMode.Idle,
                movementLocked: false,
                movementControlScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                rotationControlScaleMilli: CharacterTestDeterministicUnits.ScaleFactorScale,
                movementProfileId: 0,
                movementProfileFallback: false,
                physicsEnabled: false,
                collisionFlags: CombatMotionCollisionFlags.None,
                surfaceFrictionMilli: 0,
                surfaceMaterial: CombatSurfaceMaterial.Default,
                surfaceBounceSpeedCapYMillimetersPerSecond: 0);

        public CharacterTestCharacterMovementStateComponent(
            int verticalVelocityMillimetersPerSecond,
            int horizontalVelocityXMillimetersPerSecond,
            int horizontalVelocityYMillimetersPerSecond,
            int desiredVelocityXMillimetersPerSecond,
            int desiredVelocityYMillimetersPerSecond,
            short lastMoveInputXMilli,
            short lastMoveInputYMilli,
            bool isGrounded,
            int jumpBufferFramesRemaining,
            int coyoteTimeFramesRemaining,
            CharacterMovementMode movementMode,
            bool movementLocked,
            int movementControlScaleMilli,
            int rotationControlScaleMilli,
            int movementProfileId,
            bool movementProfileFallback,
            bool physicsEnabled,
            CombatMotionCollisionFlags collisionFlags,
            int surfaceFrictionMilli,
            CombatSurfaceMaterial surfaceMaterial,
            int surfaceBounceSpeedCapYMillimetersPerSecond)
        {
            VerticalVelocityMillimetersPerSecond = verticalVelocityMillimetersPerSecond;
            HorizontalVelocityXMillimetersPerSecond = horizontalVelocityXMillimetersPerSecond;
            HorizontalVelocityYMillimetersPerSecond = horizontalVelocityYMillimetersPerSecond;
            DesiredVelocityXMillimetersPerSecond = desiredVelocityXMillimetersPerSecond;
            DesiredVelocityYMillimetersPerSecond = desiredVelocityYMillimetersPerSecond;
            LastMoveInputXMilli = lastMoveInputXMilli;
            LastMoveInputYMilli = lastMoveInputYMilli;
            IsGrounded = isGrounded;
            JumpBufferFramesRemaining = jumpBufferFramesRemaining < 0 ? 0 : jumpBufferFramesRemaining;
            CoyoteTimeFramesRemaining = coyoteTimeFramesRemaining < 0 ? 0 : coyoteTimeFramesRemaining;
            MovementMode = movementMode;
            MovementLocked = movementLocked;
            MovementControlScaleMilli = CharacterTestDeterministicMath.ClampControlScaleMilli(movementControlScaleMilli);
            RotationControlScaleMilli = CharacterTestDeterministicMath.ClampControlScaleMilli(rotationControlScaleMilli);
            MovementProfileId = movementProfileId;
            MovementProfileFallback = movementProfileFallback;
            PhysicsEnabled = physicsEnabled;
            CollisionFlags = collisionFlags;
            SurfaceFrictionMilli = surfaceFrictionMilli < 0 ? 0 : surfaceFrictionMilli;
            SurfaceMaterial = surfaceMaterial;
            SurfaceBounceSpeedCapYMillimetersPerSecond =
                surfaceBounceSpeedCapYMillimetersPerSecond > 0
                    ? surfaceBounceSpeedCapYMillimetersPerSecond
                    : 0;
        }

        public int VerticalVelocityMillimetersPerSecond { get; }
        public int HorizontalVelocityXMillimetersPerSecond { get; }
        public int HorizontalVelocityYMillimetersPerSecond { get; }
        public int DesiredVelocityXMillimetersPerSecond { get; }
        public int DesiredVelocityYMillimetersPerSecond { get; }
        public short LastMoveInputXMilli { get; }
        public short LastMoveInputYMilli { get; }
        public bool IsGrounded { get; }
        public int JumpBufferFramesRemaining { get; }
        public int CoyoteTimeFramesRemaining { get; }
        public CharacterMovementMode MovementMode { get; }
        public bool MovementLocked { get; }
        public int MovementControlScaleMilli { get; }
        public int RotationControlScaleMilli { get; }
        public int MovementProfileId { get; }
        public bool MovementProfileFallback { get; }
        public bool PhysicsEnabled { get; }
        public CombatMotionCollisionFlags CollisionFlags { get; }
        public int SurfaceFrictionMilli { get; }
        public CombatSurfaceMaterial SurfaceMaterial { get; }
        public int SurfaceBounceSpeedCapYMillimetersPerSecond { get; }

        public CharacterTestCharacterMovementStateComponent WithJumpBufferFrames(int frames)
        {
            int nextFrames = frames > JumpBufferFramesRemaining ? frames : JumpBufferFramesRemaining;
            return WithBuffers(nextFrames, CoyoteTimeFramesRemaining);
        }

        public CharacterTestCharacterMovementStateComponent WithoutJumpBuffer()
        {
            return WithBuffers(0, CoyoteTimeFramesRemaining);
        }

        private CharacterTestCharacterMovementStateComponent WithBuffers(
            int jumpBufferFramesRemaining,
            int coyoteTimeFramesRemaining)
        {
            return new CharacterTestCharacterMovementStateComponent(
                VerticalVelocityMillimetersPerSecond,
                HorizontalVelocityXMillimetersPerSecond,
                HorizontalVelocityYMillimetersPerSecond,
                DesiredVelocityXMillimetersPerSecond,
                DesiredVelocityYMillimetersPerSecond,
                LastMoveInputXMilli,
                LastMoveInputYMilli,
                IsGrounded,
                jumpBufferFramesRemaining,
                coyoteTimeFramesRemaining,
                MovementMode,
                MovementLocked,
                MovementControlScaleMilli,
                RotationControlScaleMilli,
                MovementProfileId,
                MovementProfileFallback,
                PhysicsEnabled,
                CollisionFlags,
                SurfaceFrictionMilli,
                SurfaceMaterial,
                SurfaceBounceSpeedCapYMillimetersPerSecond);
        }
    }
}
