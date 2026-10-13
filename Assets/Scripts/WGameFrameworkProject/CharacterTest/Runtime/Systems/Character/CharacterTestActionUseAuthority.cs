using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterAction;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public enum CharacterTestActionUseStatus
    {
        Available = 0, ActionMissing = 1, InvalidRequirement = 2, UnsupportedRequirement = 3, NotGrounded = 4, NotAirborne = 5,
        AttributeMissing = 6, ResourceUnavailable = 7, AbilityOwnerMissing = 8, AbilityOwnerMismatch = 9, AbilityUnavailable = 10, TargetUnavailable = 11, WeaponTypeUnavailable = 12, WeaponTypeUnclassified = 13
    }

    internal readonly struct CharacterTestActionUseResult
    {
        internal CharacterTestActionUseResult(CharacterTestActionUseStatus status, int requirement = -1, int attribute = 0, int ability = 0)
        { Status = status; RequirementIndex = requirement; AttributeId = attribute; AbilityId = ability; }
        internal CharacterTestActionUseStatus Status { get; }
        internal int RequirementIndex { get; }
        internal int AttributeId { get; }
        internal int AbilityId { get; }
        internal bool Available => Status == CharacterTestActionUseStatus.Available;
    }

    internal readonly struct CharacterTestActionUseCommitResult
    {
        internal CharacterTestActionUseCommitResult(int entity, int action, int ability, long frame, bool configured, bool committed,
            CharacterTestActionUseFailure failure = default)
        { EntityId = entity; ActionId = action; AbilityId = ability; Frame = frame; Configured = configured; Committed = committed; Failure = failure; }
        internal int EntityId { get; }
        internal int ActionId { get; }
        internal int AbilityId { get; }
        internal long Frame { get; }
        internal bool Configured { get; }
        internal bool Committed { get; }
        internal string Reason => Failure.Reason;
        internal CharacterTestActionUseFailure Failure { get; }
    }

    /// <summary>Project adapter over existing action declarations, attributes and Ability state. Owns no cooldown or resource state.</summary>
    internal sealed partial class CharacterTestActionUseAuthority
    {
        private const int ActionUseCommitTraceKind = 91002;
        private readonly CharacterTestCharacterRuntimeModule _runtime;
        private readonly AbilityRuntimeModule _abilities;
        private readonly Dictionary<int, AbilityDefinitionConfig> _cooldownOwners = new Dictionary<int, AbilityDefinitionConfig>();
        private int _indexedAbilityCount = -1;
        internal CharacterTestActionUseAuthority(CharacterTestCharacterRuntimeModule runtime, AbilityRuntimeModule abilities)
        { _runtime = runtime; _abilities = abilities; RefreshCooldownOwners(); }
        // Observation only; excluded from gameplay hash and SaveState.
        internal long QueryCount { get; private set; }
        internal long CommitCount { get; private set; }
        internal long CommitQueryCount { get; private set; }
        internal CharacterTestActionUseCommitResult LastCommit { get; private set; }

        internal CharacterTestActionUseResult EvaluateWeaponTypes(int entityId, CharacterActionConfig action, out string issue)
        {
            return CharacterTestWeaponTypeAdmission.Evaluate(_runtime, _runtime?.FindEntityById(entityId), action,
                out int requirementIndex, out issue)
                ? new CharacterTestActionUseResult(CharacterTestActionUseStatus.Available)
                : new CharacterTestActionUseResult(issue.StartsWith("WeaponTypeClassificationPending:", StringComparison.Ordinal)
                    ? CharacterTestActionUseStatus.WeaponTypeUnclassified : CharacterTestActionUseStatus.WeaponTypeUnavailable, requirementIndex);
        }

        /// <summary>Read-only card admission query over current weapon types and the existing Ability cooldown owner.</summary>
        /// <remarks>NoAlloc for a stable catalog. New registry definitions refresh a cold lookup; no cooldown state is cached.</remarks>
        internal CharacterTestActionUseResult EvaluateCooldown(int entityId, CharacterActionConfig action,
            RuntimeFrame frame, out long remainingFrames)
        {
            remainingFrames = 0;
            if (entityId <= 0 || action == null)
                return new CharacterTestActionUseResult(CharacterTestActionUseStatus.ActionMissing);
            var weaponTypes = EvaluateWeaponTypes(entityId, action, out _);
            if (!weaponTypes.Available) return weaponTypes;
            var resolution = ResolveCooldownOwner(entityId, action, null, out var owner);
            if (!resolution.Available) return resolution;
            if (owner != null) remainingFrames = _abilities.GetRemainingCooldownFrames(entityId, owner, frame);
            return new CharacterTestActionUseResult(remainingFrames > 0
                ? CharacterTestActionUseStatus.AbilityUnavailable : CharacterTestActionUseStatus.Available,
                ability: owner?.AbilityId ?? 0);
        }

        // Read-only combined gate. The card fee is additional only before its own reservation exists.
        internal CharacterTestActionUseResult EvaluateCardCosts(int entityId,CharacterActionConfig action,int additionalCardMotion,out int actionMotion,int reservationCredit=0)
        {
            actionMotion=0;var weaponTypes=EvaluateWeaponTypes(entityId,action,out _);if(!weaponTypes.Available)return weaponTypes;
            var resolution=ResolveCooldownOwner(entityId,action,null,out var owner);if(!resolution.Available)return resolution;
            var entity=_runtime?.FindEntityById(entityId);if(entity==null)return new CharacterTestActionUseResult(CharacterTestActionUseStatus.ActionMissing);
            _runtime.World.TryGetTacticalMotion(entityId,out var motion);
            int missing=CharacterTestCharacterAttributeIds.Motion;
            if(!CharacterTestAbilityCosts.TryGetMotionCost(owner?.Costs,out actionMotion) ||
                !CharacterTestAbilityCosts.CanAfford(_runtime.World.GameplayComponentWorld,entity.GameplayEntityId,motion,owner?.Costs,additionalCardMotion,out missing,reservationCredit))
                return new CharacterTestActionUseResult(CharacterTestActionUseStatus.ResourceUnavailable,attribute:missing,ability:owner?.AbilityId??0);
            return new CharacterTestActionUseResult(CharacterTestActionUseStatus.Available,ability:owner?.AbilityId??0);
        }

        // One resolution rule for card queries, action qualification and actual commit.
        private CharacterTestActionUseResult ResolveCooldownOwner(int entityId, CharacterActionConfig action,
            AbilityDefinitionConfig requested, out AbilityDefinitionConfig owner)
        {
            owner = null;
            if (action == null) return new CharacterTestActionUseResult(CharacterTestActionUseStatus.ActionMissing);
            if (requested != null)
            {
                if (_abilities == null || !_abilities.Definitions.TryGet(requested.AbilityId, out var registered)
                    || !ReferenceEquals(registered, requested) || !AbilityOwnsAction(entityId, requested, action.ActionId)
                    || requested.ActionBinding.ActionIntentId <= 0)
                    return new CharacterTestActionUseResult(CharacterTestActionUseStatus.AbilityOwnerMismatch, ability: requested.AbilityId);
                owner = requested;
            }
            if (action.ActionId.Value == CharacterTestCombatEvadeRuntime.ForwardStepActionId)
            {
                if (_abilities == null || !_abilities.Definitions.TryGet(CharacterTestCombatEvadeRuntime.StepAbilityId, out var step))
                    return new CharacterTestActionUseResult(CharacterTestActionUseStatus.AbilityOwnerMissing);
                if (owner != null && owner.AbilityId != step.AbilityId)
                    return new CharacterTestActionUseResult(CharacterTestActionUseStatus.AbilityOwnerMismatch, ability: owner.AbilityId);
                owner = step;
            }
            for (int i = 0; i < action.Requirements.Length; i++)
            {
                var requirement = action.Requirements[i];
                if (requirement?.Kind != CharacterActionRequirementKind.CooldownReady) continue;
                var result = ResolveAbilityOwner(entityId, action.ActionId.Value, requirement, owner, i, out var found);
                if (!result.Available) return result;
                owner = found;
            }
            if (owner == null)
            {
                RefreshCooldownOwners();
                if (_cooldownOwners.TryGetValue(action.ActionId.Value, out owner)
                    && (owner == null || owner.ActionBinding.ActionIntentId <= 0
                        || !AbilityOwnsAction(entityId, owner, action.ActionId)))
                    return new CharacterTestActionUseResult(CharacterTestActionUseStatus.AbilityOwnerMismatch);
            }
            return new CharacterTestActionUseResult(CharacterTestActionUseStatus.Available, ability: owner?.AbilityId ?? 0);
        }

        private bool AbilityOwnsAction(int entityId, AbilityDefinitionConfig ability, CharacterActionId actionId)
        {
            return _runtime?.ActionRuntime != null
                && _runtime.ActionRuntime.ResolveAbilityActionId(
                    new CharacterTestCharacterEntityId(entityId), ability) == actionId;
        }

        // Definitions are append-only. Count detects registration after construction without allocating per frame.
        private void RefreshCooldownOwners()
        {
            int count = _abilities?.Definitions.Count ?? 0;
            if (_indexedAbilityCount == count) return;
            _cooldownOwners.Clear();
            if (_abilities != null)
            {
                var definitions = _abilities.Definitions.CreateSnapshot();
                for (int i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    int actionId = definition.ActionBinding.ActionId.Value;
                    if (actionId <= 0) continue;
                    // An explicit CooldownReady declaration may disambiguate; never guess between two owners.
                    _cooldownOwners[actionId] = _cooldownOwners.ContainsKey(actionId) ? null : definition;
                }
            }
            _indexedAbilityCount = count;
        }

        /// <summary>Read-only whole-card gate. Resource requirements and payment keep their original boundaries.</summary>
        internal CharacterTestActionUseResult EvaluateCooldowns(int entityId, IReadOnlyList<int> actionIds,
            CharacterActionConfigRegistry registry, RuntimeFrame frame)
        {
            for (int i = 0; i < actionIds.Count; i++)
            {
                if (registry == null || !registry.TryGetAction(new CharacterActionId(actionIds[i]), out var action))
                    return new CharacterTestActionUseResult(CharacterTestActionUseStatus.ActionMissing);
                var result = EvaluateCooldown(entityId, action, frame, out _);
                if (!result.Available) return result;
            }
            return new CharacterTestActionUseResult(CharacterTestActionUseStatus.Available);
        }

        internal CharacterTestActionUseResult Evaluate(CharacterTestCharacterEntity entity, int actionId, RuntimeFrame frame,
            CharacterTestRuntimeAiPlannerFactState facts, AbilityDefinitionConfig requestedAbility = null)
        {
            QueryCount++;
            if (entity == null || actionId <= 0 || _runtime?.ActionRuntime?.Registry == null
                || !_runtime.ActionRuntime.Registry.TryGetAction(new CharacterActionId(actionId), out var action))
                return new CharacterTestActionUseResult(CharacterTestActionUseStatus.ActionMissing);
            var weaponTypes = EvaluateWeaponTypes(entity.Id.Value, action, out _);
            if (!weaponTypes.Available) return weaponTypes;
            var resolution = ResolveCooldownOwner(entity.Id.Value, action, requestedAbility, out var owner);
            if (!resolution.Available) return resolution;
            if (actionId == CharacterTestCombatEvadeRuntime.ForwardStepActionId && !_runtime.CanUseAuthoredForwardStep(entity))
                return new CharacterTestActionUseResult(CharacterTestActionUseStatus.AbilityUnavailable,
                    ability: CharacterTestCombatEvadeRuntime.StepAbilityId);
            for (int i = 0; i < action.Requirements.Length; i++)
            {
                var requirement = action.Requirements[i];
                if (requirement == null) return new CharacterTestActionUseResult(CharacterTestActionUseStatus.InvalidRequirement, i);
                switch (requirement.Kind)
                {
                    case CharacterActionRequirementKind.None:
                    case CharacterActionRequirementKind.Grounded:
                    case CharacterActionRequirementKind.Airborne:
                    case CharacterActionRequirementKind.TargetValid:
                        if (!string.IsNullOrEmpty(requirement.StableId) || requirement.Value != 0)
                            return new CharacterTestActionUseResult(CharacterTestActionUseStatus.InvalidRequirement, i);
                        if (requirement.Kind == CharacterActionRequirementKind.Grounded && !entity.IsGrounded)
                            return new CharacterTestActionUseResult(CharacterTestActionUseStatus.NotGrounded, i);
                        if (requirement.Kind == CharacterActionRequirementKind.Airborne && entity.IsGrounded)
                            return new CharacterTestActionUseResult(CharacterTestActionUseStatus.NotAirborne, i);
                        if (requirement.Kind == CharacterActionRequirementKind.TargetValid && (facts == null || !facts.TargetExists || facts.TargetDead))
                            return new CharacterTestActionUseResult(CharacterTestActionUseStatus.TargetUnavailable, i);
                        break;
                    case CharacterActionRequirementKind.ResourceAvailable:
                        if (requirement.Value < 0 || requirement.ReferenceId <= 0 || requirement.StableId.Length > 0)
                            return new CharacterTestActionUseResult(CharacterTestActionUseStatus.InvalidRequirement, i);
                        if (requirement.ReferenceId == CharacterTestCharacterAttributeIds.Motion)
                        {
                            if (!_runtime.World.TryGetTacticalMotion(entity.Id.Value, out var motion))
                                return new CharacterTestActionUseResult(CharacterTestActionUseStatus.AttributeMissing, i, requirement.ReferenceId);
                            if ((long)motion.Spendable + _runtime.FixedActionReservationCredit(entity, actionId) < requirement.Value)
                                return new CharacterTestActionUseResult(CharacterTestActionUseStatus.ResourceUnavailable, i, requirement.ReferenceId);
                            break;
                        }
                        if (!_runtime.World.GameplayComponentWorld.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store)
                            || !store.TryGet(entity.GameplayEntityId, out var attributes) || !attributes.TryGet(requirement.ReferenceId, out _)
                            || !GameplayComponentAttributeQuery.TryGetCurrentValue(_runtime.World.GameplayComponentWorld, entity.GameplayEntityId, requirement.ReferenceId, out int value, includeModifiers: false))
                            return new CharacterTestActionUseResult(CharacterTestActionUseStatus.AttributeMissing, i, requirement.ReferenceId);
                        if (value < requirement.Value) return new CharacterTestActionUseResult(CharacterTestActionUseStatus.ResourceUnavailable, i, requirement.ReferenceId);
                        break;
                    case CharacterActionRequirementKind.WeaponTypeIndependent:
                    case CharacterActionRequirementKind.WeaponTypeRequired:
                        // Current equipment was checked before cooldown and resource admission.
                        break;
                    case CharacterActionRequirementKind.CooldownReady:
                        // Resolved once above; the existing Ability checks cooldown and costs below.
                        break;
                    default:
                        return new CharacterTestActionUseResult(CharacterTestActionUseStatus.UnsupportedRequirement, i);
                }
            }
            if (owner != null && !_abilities.TryEvaluate(entity.Id.Value, owner, frame, out _))
                return new CharacterTestActionUseResult(CharacterTestActionUseStatus.AbilityUnavailable, ability: owner.AbilityId);
            return new CharacterTestActionUseResult(CharacterTestActionUseStatus.Available);
        }

        private CharacterTestActionUseResult ResolveAbilityOwner(int entityId, int action, CharacterActionRequirement requirement,
            AbilityDefinitionConfig previous, int index, out AbilityDefinitionConfig owner)
        {
            owner = null;
            if (requirement.Value != 0 || requirement.ReferenceId <= 0 || requirement.StableId.Length > 0)
                return new CharacterTestActionUseResult(CharacterTestActionUseStatus.InvalidRequirement, index);
            if (_abilities == null || !_abilities.Definitions.TryGet(requirement.ReferenceId, out owner))
                return new CharacterTestActionUseResult(CharacterTestActionUseStatus.AbilityOwnerMissing, index);
            if (!AbilityOwnsAction(entityId, owner, new CharacterActionId(action)) || owner.ActionBinding.ActionIntentId <= 0
                || (previous != null && previous.AbilityId != owner.AbilityId))
                return new CharacterTestActionUseResult(CharacterTestActionUseStatus.AbilityOwnerMismatch, index, ability: owner.AbilityId);
            return new CharacterTestActionUseResult(CharacterTestActionUseStatus.Available);
        }

        internal CharacterTestActionUseCommitResult TryCommit(CharacterTestCharacterEntity entity, CharacterActionConfig action,
            RuntimeFrame frame, CharacterTestRuntimeAiPlannerFactState facts, AbilityDefinitionConfig requestedAbility = null)
        {
            CommitCount++;
            var resolution = ResolveCooldownOwner(entity.Id.Value, action, requestedAbility, out var owner);
            if (!resolution.Available)
                return Failed(entity, action, frame, owner, new CharacterTestActionUseFailure(
                    CharacterTestActionCommitFailureKind.AbilityOwnerResolution, resolution.Status,
                    resolution.RequirementIndex, resolution.AttributeId, resolution.AbilityId));
            bool forwardStep = action.ActionId.Value == CharacterTestCombatEvadeRuntime.ForwardStepActionId;
            CommitQueryCount++;
            var qualification = Evaluate(entity, action.ActionId.Value, frame, facts, requestedAbility);
            if (!qualification.Available) return Failed(entity, action, frame, owner,
                new CharacterTestActionUseFailure(CharacterTestActionCommitFailureKind.Qualification,
                    qualification.Status, qualification.RequirementIndex, qualification.AttributeId, qualification.AbilityId));
            if (!_runtime.ActionRuntime.TryStageOrchestration(entity.Id, action.ActionId,
                    new RuntimeTraceIdentity(ActionUseCommitTraceKind, entity.Id.Value, frame.Value), out var stage, out CharacterTestActionUseFailure stageFailure))
                return Failed(entity, action, frame, owner, stageFailure);
            bool published = false;
            try
            {
                // The gateway buffer remains private until the existing cost/cooldown owner commits.
                // No world tick, projection, callback or competing selection occurs inside this boundary.
                if (owner != null && !_abilities.TryCommitActivation(entity.Id.Value, owner, frame, out AbilityActivationFailure activationFailure))
                    return Failed(entity, action, frame, owner,
                        new CharacterTestActionUseFailure(CharacterTestActionCommitFailureKind.Activation,
                            attributeId: activationFailure.AttributeId, abilityId: owner.AbilityId,
                            activationReason: activationFailure.ReasonCode));
                _runtime.ActionRuntime.CommitOrchestration(stage, frame); published = true;
                if (forwardStep) _runtime.RecordAuthoredForwardStep(entity.Id.Value, frame);
                return LastCommit = new CharacterTestActionUseCommitResult(entity.Id.Value, action.ActionId.Value,
                    owner?.AbilityId ?? 0, frame.Value, owner != null, true);
            }
            finally { if (!published) _runtime.ActionRuntime.AbortOrchestration(stage); }
        }

        private CharacterTestActionUseCommitResult Failed(CharacterTestCharacterEntity entity, CharacterActionConfig action,
            RuntimeFrame frame, AbilityDefinitionConfig owner, CharacterTestActionUseFailure failure)
            => LastCommit = new CharacterTestActionUseCommitResult(entity.Id.Value, action.ActionId.Value, owner?.AbilityId ?? 0,
                frame.Value, owner != null, false, failure);
    }
}
