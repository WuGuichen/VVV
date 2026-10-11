using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Gameplay;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestActionPartStateEligibilityStatus
    {
        Eligible = 0,
        ActionUnavailable = 1,
        StaleActionBinding = 2,
        EventUnavailable = 3,
        InvalidDependency = 4,
        UnknownPartState = 5,
        InvalidPartStateBinding = 6,
        PartStateSevered = 7,
        PartStateDisallowed = 8
    }

    internal readonly struct CharacterTestActionPartStateEligibility
    {
        internal CharacterTestActionPartStateEligibility(
            CharacterTestActionPartStateEligibilityStatus status,
            int requiredPartStateId,
            CharacterActionPartStructureStateMask allowedStates,
            CharacterActionPartStructureStateMask observedState,
            long bodyPartStateRevision)
        {
            Status = status;
            RequiredPartStateId = requiredPartStateId;
            AllowedStates = allowedStates;
            ObservedState = observedState;
            BodyPartStateRevision = bodyPartStateRevision < 0L ? 0L : bodyPartStateRevision;
        }

        public CharacterTestActionPartStateEligibilityStatus Status { get; }
        public int RequiredPartStateId { get; }
        public CharacterActionPartStructureStateMask AllowedStates { get; }
        public CharacterActionPartStructureStateMask ObservedState { get; }
        public long BodyPartStateRevision { get; }
        public bool IsEligible => Status == CharacterTestActionPartStateEligibilityStatus.Eligible;

        public string RejectCode => CharacterActionDiagnosticCodeText.Get(RejectCodeId);
        public CharacterActionDiagnosticCode RejectCodeId
        {
            get
            {
                switch (Status)
                {
                    case CharacterTestActionPartStateEligibilityStatus.ActionUnavailable:
                        return CharacterActionDiagnosticCode.MissingActionConfig;
                    case CharacterTestActionPartStateEligibilityStatus.StaleActionBinding:
                        return CharacterActionDiagnosticCode.MissingActionBinding;
                    case CharacterTestActionPartStateEligibilityStatus.EventUnavailable:
                        return CharacterActionDiagnosticCode.PartStateDependencyEventUnavailable;
                    case CharacterTestActionPartStateEligibilityStatus.InvalidDependency:
                        return CharacterActionDiagnosticCode.InvalidPartStateDependency;
                    case CharacterTestActionPartStateEligibilityStatus.UnknownPartState:
                        return CharacterActionDiagnosticCode.PartStateDependencyUnknown;
                    case CharacterTestActionPartStateEligibilityStatus.InvalidPartStateBinding:
                        return CharacterActionDiagnosticCode.PartStateDependencyBindingInvalid;
                    case CharacterTestActionPartStateEligibilityStatus.PartStateSevered:
                        return CharacterActionDiagnosticCode.PartStateDependencySevered;
                    case CharacterTestActionPartStateEligibilityStatus.PartStateDisallowed:
                        return CharacterActionDiagnosticCode.PartStateDependencyDisallowed;
                    default:
                        return CharacterActionDiagnosticCode.None;
                }
            }
        }

        internal static CharacterTestActionPartStateEligibility Eligible(long revision = 0L)
        {
            return new CharacterTestActionPartStateEligibility(
                CharacterTestActionPartStateEligibilityStatus.Eligible,
                0,
                CharacterActionPartStructureStateMask.None,
                CharacterActionPartStructureStateMask.None,
                revision);
        }
    }

    public readonly struct CharacterTestCharacterActionReloadResult
    {
        public CharacterTestCharacterActionReloadResult(
            bool success,
            string message,
            string sourceDescription,
            int importDiagnosticCount,
            int validationDiagnosticCount,
            RuntimeFrame frame)
        {
            Success = success;
            Message = message ?? string.Empty;
            SourceDescription = sourceDescription ?? string.Empty;
            ImportDiagnosticCount = importDiagnosticCount < 0 ? 0 : importDiagnosticCount;
            ValidationDiagnosticCount = validationDiagnosticCount < 0 ? 0 : validationDiagnosticCount;
            Frame = frame;
        }

        public bool Success { get; }
        public string Message { get; }
        public string SourceDescription { get; }
        public int ImportDiagnosticCount { get; }
        public int ValidationDiagnosticCount { get; }
        public RuntimeFrame Frame { get; }

        public static CharacterTestCharacterActionReloadResult Accepted(
            CharacterTestCharacterActionRuntime runtime,
            RuntimeFrame frame)
        {
            return new CharacterTestCharacterActionReloadResult(
                true,
                "Action config reloaded.",
                runtime != null ? runtime.ConfigSourceDescription : string.Empty,
                runtime != null ? runtime.ImportResult.Diagnostics.Length : 0,
                runtime != null ? runtime.ValidationReport.Diagnostics.Length : 0,
                frame);
        }

        public static CharacterTestCharacterActionReloadResult Rejected(
            CharacterTestCharacterActionRuntime runtime,
            RuntimeFrame frame,
            string message)
        {
            return new CharacterTestCharacterActionReloadResult(
                false,
                message,
                runtime != null ? runtime.ConfigSourceDescription : string.Empty,
                runtime != null ? runtime.ImportResult.Diagnostics.Length : 0,
                runtime != null ? runtime.ValidationReport.Diagnostics.Length : 0,
                frame);
        }
    }

    public sealed partial class CharacterTestCharacterActionRuntime
    {
        private readonly CharacterTestCharacterActionCatalog _catalog;
        private readonly CharacterTestCharacterActionExecutionStore _executions;
        private readonly CharacterTestCharacterActionProjectionWriter _projection;
        private readonly CharacterTestCharacterActionSnapshotAdapter _snapshots;

        public CharacterTestCharacterActionRuntime(
            CharacterActionJsonImportResult importResult,
            CharacterActionConfigRegistry registry,
            CharacterActionValidationReport validationReport,
            CharacterActionSetConfig actionSet,
            CharacterTestCharacterActionConfigSourceKind configSourceKind,
            string configSourceDescription,
            string actionSetsSourcePath,
            string actionsSourcePath,
            CharacterTestCharacterActionConfigSourceDiagnostic[] configSourceDiagnostics)
            : this(new CharacterTestCharacterActionCatalog(
                importResult,
                registry,
                validationReport,
                actionSet,
                configSourceKind,
                configSourceDescription,
                actionSetsSourcePath,
                actionsSourcePath,
                configSourceDiagnostics))
        {
        }

        public CharacterTestCharacterActionRuntime(CharacterTestCharacterActionCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _executions = new CharacterTestCharacterActionExecutionStore();
            _projection = new CharacterTestCharacterActionProjectionWriter();
            _snapshots = new CharacterTestCharacterActionSnapshotAdapter(
                _catalog,
                _executions,
                _projection);
        }

        public CharacterTestCharacterActionCatalog Catalog => _catalog;
        internal int ActiveExecutionCount => _executions.Count;
        internal void AllocateExecutionGatewayForTesting(CharacterTestCharacterEntityId entityId) =>
            _executions.GetOrCreate(entityId);
        public CharacterActionJsonImportResult ImportResult => _catalog.ImportResult;
        public CharacterActionConfigRegistry Registry => _catalog.Registry;

        public CharacterActionValidationReport ValidationReport => _catalog.ValidationReport;
        public CharacterActionSetConfig ActionSet => _catalog.ActionSet;
        public CharacterTestCharacterActionConfigSourceKind ConfigSourceKind => _catalog.ConfigSourceKind;
        public string ConfigSourceDescription => _catalog.ConfigSourceDescription;
        public string ActionSetsSourcePath => _catalog.ActionSetsSourcePath;
        public string ActionsSourcePath => _catalog.ActionsSourcePath;
        public IReadOnlyList<CharacterTestCharacterActionConfigSourceDiagnostic> ConfigSourceDiagnostics =>
            _catalog.ConfigSourceDiagnostics;
        public bool IsReady => _catalog.IsReady;
        internal string ActionIdText(CharacterActionId id) => _catalog.ActionIdText(id);

        /// <summary>
        /// Evaluates the event-level body-part dependency against the entity's current
        /// authority snapshot. The lookup is bounded and allocation-free after load.
        /// </summary>
        internal CharacterTestActionPartStateEligibility EvaluatePartStateEligibility(
            CharacterTestCharacterEntity entity,
            CharacterActionId actionId,
            int stableEventId)
        {
            if (entity == null || Registry == null
                || !Registry.TryGetAction(actionId, out CharacterActionConfig action)
                || action == null)
            {
                return CreatePartStateEligibility(
                    CharacterTestActionPartStateEligibilityStatus.ActionUnavailable);
            }

            CombatTrackEvent[] events = action.CombatTrack?.Events;
            for (int i = 0; events != null && i < events.Length; i++)
            {
                CombatTrackEvent trackEvent = events[i];
                if (trackEvent.Kind == CharacterActionTrackEventKind.HitTraceWindow
                    && trackEvent.StableEventId == stableEventId)
                {
                    return EvaluatePartStateEligibility(entity.ComponentWorld, entity.Id, trackEvent);
                }
            }

            return CreatePartStateEligibility(
                CharacterTestActionPartStateEligibilityStatus.EventUnavailable,
                entity.BodyPartState?.Revision ?? 0L);
        }

        /// <summary>
        /// An action remains startable when it has no HitTraceWindow events or at least
        /// one such event is eligible. This gates tail-only attacks without gating an
        /// independent bite event in the same action.
        /// </summary>
        internal CharacterTestActionPartStateEligibility EvaluatePartStateEligibility(
            CharacterTestCharacterEntity entity,
            CharacterActionConfig action)
        {
            if (entity == null)
            {
                return CreatePartStateEligibility(
                    CharacterTestActionPartStateEligibilityStatus.ActionUnavailable);
            }
            return EvaluatePartStateEligibility(entity.ComponentWorld, entity.Id, action);
        }

        private CharacterTestActionPartStateEligibility EvaluatePartStateEligibility(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            CharacterActionConfig action)
        {
            if (components == null || !components.Contains(entityId)
                || action == null || Registry == null
                || !Registry.TryGetAction(action.ActionId, out CharacterActionConfig current))
            {
                return CreatePartStateEligibility(
                    CharacterTestActionPartStateEligibilityStatus.ActionUnavailable);
            }
            if (!ReferenceEquals(current, action)
                || current.ActionId != action.ActionId)
            {
                return CreatePartStateEligibility(
                    CharacterTestActionPartStateEligibilityStatus.StaleActionBinding,
                    components.GetBodyPartState(entityId)?.Revision ?? 0L);
            }

            CombatTrackEvent[] events = action.CombatTrack?.Events;
            bool foundTraceEvent = false;
            CharacterTestActionPartStateEligibility firstRejected = default;
            for (int i = 0; events != null && i < events.Length; i++)
            {
                CombatTrackEvent trackEvent = events[i];
                if (trackEvent.Kind != CharacterActionTrackEventKind.HitTraceWindow)
                    continue;

                foundTraceEvent = true;
                CharacterTestActionPartStateEligibility eligibility =
                    EvaluatePartStateEligibility(components, entityId, trackEvent);
                if (eligibility.IsEligible)
                    return eligibility;
                if (firstRejected.Status == CharacterTestActionPartStateEligibilityStatus.Eligible)
                    firstRejected = eligibility;
            }

            return foundTraceEvent
                ? firstRejected
                : CharacterTestActionPartStateEligibility.Eligible(
                    components.GetBodyPartState(entityId)?.Revision ?? 0L);
        }

        private static CharacterTestActionPartStateEligibility EvaluatePartStateEligibility(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            CombatTrackEvent trackEvent)
        {
            int partStateId = trackEvent.RequiredPartStateId;
            CharacterActionPartStructureStateMask allowedStates =
                trackEvent.AllowedPartStructureStateMask;
            bool hasPartStateId = !(partStateId <= 0);
            int rawMask = (int)allowedStates;
            if (!hasPartStateId && rawMask == 0)
            {
                return CharacterTestActionPartStateEligibility.Eligible(
                    components?.GetBodyPartState(entityId)?.Revision ?? 0L);
            }

            CharacterTestBodyPartStateComponent bodyPartState =
                components?.GetBodyPartState(entityId);
            long revision = bodyPartState?.Revision ?? 0L;
            if (!hasPartStateId
                || rawMask == 0
                || (rawMask & ~(int)CharacterActionPartStructureStateMask.AllKnown) != 0)
            {
                return new CharacterTestActionPartStateEligibility(
                    CharacterTestActionPartStateEligibilityStatus.InvalidDependency,
                    partStateId,
                    allowedStates,
                    CharacterActionPartStructureStateMask.None,
                    revision);
            }
            if (bodyPartState == null)
            {
                return new CharacterTestActionPartStateEligibility(
                    CharacterTestActionPartStateEligibilityStatus.UnknownPartState,
                    partStateId,
                    allowedStates,
                    CharacterActionPartStructureStateMask.None,
                    revision);
            }

            CharacterTestBodyPartDependencyStatus dependencyStatus =
                bodyPartState.GetPartStateDependencyStatus(partStateId);
            if (dependencyStatus == CharacterTestBodyPartDependencyStatus.InvalidBinding)
            {
                return new CharacterTestActionPartStateEligibility(
                    CharacterTestActionPartStateEligibilityStatus.InvalidPartStateBinding,
                    partStateId,
                    allowedStates,
                    CharacterActionPartStructureStateMask.None,
                    revision);
            }
            if (dependencyStatus == CharacterTestBodyPartDependencyStatus.UnknownPartState
                || !bodyPartState.TryResolveDefinitionIndexByPartStateId(
                    partStateId,
                    out int definitionIndex)
                || !bodyPartState.TryGetCurrentEntry(
                    definitionIndex,
                    out CharacterTestBodyPartStateEntry entry))
            {
                return new CharacterTestActionPartStateEligibility(
                    CharacterTestActionPartStateEligibilityStatus.UnknownPartState,
                    partStateId,
                    allowedStates,
                    CharacterActionPartStructureStateMask.None,
                    revision);
            }

            CharacterActionPartStructureStateMask observedState =
                ToPartStructureStateMask(entry.StructureState);
            if ((allowedStates & observedState) != 0)
            {
                return new CharacterTestActionPartStateEligibility(
                    CharacterTestActionPartStateEligibilityStatus.Eligible,
                    partStateId,
                    allowedStates,
                    observedState,
                    revision);
            }
            return new CharacterTestActionPartStateEligibility(
                entry.StructureState == CharacterTestBodyPartStructureState.Severed
                    ? CharacterTestActionPartStateEligibilityStatus.PartStateSevered
                    : CharacterTestActionPartStateEligibilityStatus.PartStateDisallowed,
                partStateId,
                allowedStates,
                observedState,
                revision);
        }

        private static CharacterActionPartStructureStateMask ToPartStructureStateMask(
            CharacterTestBodyPartStructureState state)
        {
            switch (state)
            {
                case CharacterTestBodyPartStructureState.Intact:
                    return CharacterActionPartStructureStateMask.Intact;
                case CharacterTestBodyPartStructureState.Broken:
                    return CharacterActionPartStructureStateMask.Broken;
                case CharacterTestBodyPartStructureState.Severed:
                    return CharacterActionPartStructureStateMask.Severed;
                default:
                    return CharacterActionPartStructureStateMask.None;
            }
        }

        private static CharacterTestActionPartStateEligibility CreatePartStateEligibility(
            CharacterTestActionPartStateEligibilityStatus status,
            long revision = 0L)
        {
            return new CharacterTestActionPartStateEligibility(
                status,
                0,
                CharacterActionPartStructureStateMask.None,
                CharacterActionPartStructureStateMask.None,
                revision);
        }

        /// <summary>Resolves the effective ActionSet for one runtime entity.</summary>
        /// <remarks>NoAlloc after the catalog is built.</remarks>
        public bool TryGetEntityActionSet(
            CharacterTestCharacterEntityId entityId,
            out CharacterActionSetConfig actionSet)
        {
            actionSet = _catalog.ResolveActionSet(_projection.Components, entityId);
            return actionSet != null;
        }

        /// <summary>Gets the effective entity ActionSet without allocating a context key.</summary>
        internal bool TryGetEntityActionSetNoAlloc(
            CharacterTestCharacterEntityId entityId,
            out CharacterActionSetConfig actionSet)
        {
            actionSet = _catalog.ResolveActionSetNoAlloc(_projection.Components, entityId);
            return actionSet != null;
        }
        internal CharacterTestCharacterActionTimelineEventBuffer TimelineEvents => _projection.TimelineEvents;

        /// <summary>Resolves an Ability through the entity's equipped ActionSet.</summary>
        /// <remarks>NoAlloc. The shared Ability remains the cooldown and cost owner.</remarks>
        internal CharacterActionId ResolveAbilityActionId(
            CharacterTestCharacterEntityId entityId,
            AbilityDefinitionConfig ability,
            CharacterActionId fallbackActionId = default)
        {
            if (ability == null)
                return default;
            CharacterActionId shared = ability.ActionBinding.ActionId.IsValid
                ? ability.ActionBinding.ActionId : fallbackActionId;
            if (!TryGetEntityActionSetNoAlloc(entityId, out CharacterActionSetConfig actionSet))
                return shared;

            CharacterActionId resolved = default;
            bool found = false;
            for (int i = 0; i < actionSet.AbilityBindings.Length; i++)
            {
                CharacterAbilityActionBinding binding = actionSet.AbilityBindings[i];
                if (binding == null || binding.AbilityId != ability.AbilityId)
                    continue;
                // Ambiguous scoped bindings must not silently select an action.
                if (found)
                    return default;
                found = true;
                resolved = binding.ActionId;
            }
            return found ? resolved : shared;
        }

        internal void BindRuntimeWorld(CharacterTestCharacterRuntimeWorld world)
        {
            _projection.BindRuntimeWorld(world);
            if (_preparedAnimationProfiles.Count == 0)
            {
                try
                {
                    PrepareAnimationDefinitions(new CharacterTestCharacterDefinitionRegistry());
                }
                catch
                {
                    // ignored in partial test environments
                }
            }
            world?.CharacterComponents.SetPreparedAnimationProfiles(_preparedAnimationProfiles);
        }

        private readonly Dictionary<int, CharacterTestPreparedAnimationBindings> _preparedAnimationProfiles =
            new Dictionary<int, CharacterTestPreparedAnimationBindings>();

        private CharacterTestCharacterDefinitionRegistry _preparedAnimationDefinitions;
        internal void PrepareAnimationDefinitions(CharacterTestCharacterDefinitionRegistry definitions)
        {
            if (ReferenceEquals(_preparedAnimationDefinitions, definitions)) return;
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            var prepared = new Dictionary<int, CharacterTestPreparedAnimationBindings>();
            foreach (CharacterTestCharacterDefinition definition in definitions.Definitions)
                prepared.Add(definition.CharacterId,
                    CharacterTestPreparedAnimationBindings.Prepare(definition.AnimationBinding, Registry.Actions, CollectRequiredAnimationActions(definition)));
            _preparedAnimationProfiles.Clear();
            foreach (var item in prepared) _preparedAnimationProfiles.Add(item.Key, item.Value);
            _preparedAnimationDefinitions = definitions;
        }

        // Session preparation validates only actions reachable through this character's authored
        // equipment states. Other known bindings remain available for explicit preview requests.
        private HashSet<CharacterActionId> CollectRequiredAnimationActions(CharacterTestCharacterDefinition definition)
        {
            var package = CharacterTestGeneratedPackageSource.LoadPackage(definition.GeneratedPackageRoot);
            if (package?.Configs?.Characters == null || package.Configs.Characters.Length == 0)
                return null; // Embedded and partial test definitions retain strict legacy validation.
            string characterStableId = package.Configs.Characters[0].StableId;
            var required = new HashSet<CharacterActionId>();
            foreach (CharacterActionSetConfig set in Registry.ActionSets)
            {
                if (set == null || (!string.IsNullOrEmpty(set.CharacterStableId)
                    && !string.Equals(set.CharacterStableId, characterStableId, StringComparison.Ordinal))) continue;
                required.Add(set.DefaultActionId);
                foreach (var binding in set.CommandBindings) if (binding != null) required.Add(binding.ActionId);
                foreach (var sequence in set.CommandSequences)
                    if (sequence != null) foreach (var id in sequence.ActionIds) required.Add(id);
                foreach (var binding in set.AbilityBindings) if (binding != null) required.Add(binding.ActionId);
                CollectReactionAnimationActions(set.ReactionProfileId, required);
                foreach (var binding in set.ReactionBindings)
                {
                    if (binding == null) continue;
                    required.Add(binding.DefaultActionId);
                    CollectReactionAnimationActions(binding.ReactionProfileId, required);
                }
                if (Registry.TryGetLandingProfile(set.LandingProfileId, out CharacterLandingProfile landing))
                {
                    required.Add(landing.LightLandingActionId);
                    required.Add(landing.HeavyLandingActionId);
                }
            }
            return required;
        }

        private void CollectReactionAnimationActions(string profileId, HashSet<CharacterActionId> required)
        {
            if (!Registry.TryGetReactionProfile(profileId, out CharacterReactionProfile reaction)) return;
            required.Add(reaction.DefaultActionId);
            foreach (var rule in reaction.Rules) if (rule != null) required.Add(rule.ActionId);
        }

        internal bool TryGetPreparedAnimation(int characterId, int bindingId, out MxFramework.Animation.MxAnimationActionBinding binding)
        {
            binding = null;
            return _preparedAnimationProfiles.TryGetValue(characterId, out CharacterTestPreparedAnimationBindings profile)
                && profile.TryGetById(bindingId, out binding);
        }

        internal bool ValidateAnimationSnapshot(CharacterTestNetworkCharacterSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.HasValidAnimationIdentityShape()) return false;
            if (!_preparedAnimationProfiles.TryGetValue(snapshot.CharacterId, out CharacterTestPreparedAnimationBindings profile)) return false;
            if (snapshot.LastAnimationBindingId != 0 && !TryGetPreparedAnimation(snapshot.CharacterId, snapshot.LastAnimationBindingId, out _)) return false;
            if (snapshot.AnimationBindingId != 0 && !TryGetPreparedAnimation(snapshot.CharacterId, snapshot.AnimationBindingId, out _)) return false;
            if (snapshot.AnimationAuthorityBindingId != profile.AuthorityBindingFor(snapshot.AnimationBindingId)) return false;
            if (snapshot.AnimationRequest == null || snapshot.AnimationRequest.Status == 0) return true;
            return profile.TryGet(new CharacterActionId(snapshot.AnimationRequest.ActionId), snapshot.AnimationRequest.StableEventId, out var binding)
                && binding.BindingId == snapshot.AnimationRequest.BindingId
                && binding.AuthorityBindingId == snapshot.AnimationRequest.AuthorityBindingId;
        }

        internal CharacterActionResolveResult ResolveIntent(
            CharacterTestCharacterEntity entity,
            int intentId,
            RuntimeFrame frame,
            int actionSlotId = 0)
        {
            return entity != null
                ? ResolveIntent(entity.ComponentWorld, entity.Id, intentId, frame, actionSlotId)
                : ResolveIntent(null, default, intentId, frame, actionSlotId);
        }

        public CharacterActionResolveResult ResolveIntent(
            CharacterTestCharacterEntityId entityId,
            int intentId,
            RuntimeFrame frame)
        {
            return ResolveIntent(entityId, intentId, frame, actionSlotId: 0);
        }

        public CharacterActionResolveResult ResolveIntent(
            CharacterTestCharacterEntityId entityId,
            int intentId,
            RuntimeFrame frame,
            int actionSlotId)
        {
            return ResolveIntent(_projection.Components, entityId, intentId, frame, actionSlotId);
        }

        internal CharacterActionResolveResult ResolveIntent(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            int intentId,
            RuntimeFrame frame,
            int actionSlotId = 0)
        {
            var request = new CharacterActionIntentRequest(
                new GameplayEntityId(entityId.IsValid ? entityId.Value : 1, 1),
                intentId,
                abilityId: null,
                abilityStableId: 0,
                requestedActionId: CharacterActionId.None,
                CharacterActionSourceKind.LocalInput,
                priority: 0,
                frame,
                new RuntimeTraceIdentity(CharacterTestActionTraceKinds.Intent, entityId.Value, frame.Value, intentId),
                actionSlotId);

            return _catalog.Resolver.ResolveCommand(CreateContext(components, entityId), request);
        }

        internal CharacterActionResolveResult ResolveActionSlot(
            CharacterTestCharacterEntity entity,
            int actionSlotId,
            CharacterActionId requestedActionId,
            RuntimeFrame frame,
            RuntimeTraceIdentity traceId = default)
        {
            return entity != null
                ? ResolveActionSlot(entity.ComponentWorld, entity.Id, actionSlotId, requestedActionId, frame, traceId)
                : ResolveActionSlot(null, default, actionSlotId, requestedActionId, frame, traceId);
        }

        public CharacterActionResolveResult ResolveActionSlot(
            CharacterTestCharacterEntityId entityId,
            int actionSlotId,
            CharacterActionId requestedActionId,
            RuntimeFrame frame,
            RuntimeTraceIdentity traceId = default)
        {
            return ResolveActionSlot(
                _projection.Components,
                entityId,
                actionSlotId,
                requestedActionId,
                frame,
                traceId);
        }

        internal CharacterActionResolveResult ResolveActionSlot(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            int actionSlotId,
            CharacterActionId requestedActionId,
            RuntimeFrame frame,
            RuntimeTraceIdentity traceId = default)
        {
            var request = new CharacterActionIntentRequest(
                new GameplayEntityId(entityId.IsValid ? entityId.Value : 1, 1),
                intentId: 0,
                abilityId: null,
                abilityStableId: 0,
                requestedActionId: requestedActionId,
                CharacterActionSourceKind.LocalInput,
                priority: 0,
                frame,
                _catalog.ResolveActionSlotTraceId(actionSlotId, traceId),
                actionSlotId);

            return _catalog.Resolver.ResolveActionSlot(CreateContext(components, entityId), request);
        }

        private bool TryResolveCachedActionSlot(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            int actionSlotId,
            CharacterActionId requestedActionId,
            RuntimeTraceIdentity traceId,
            bool hasRunningAction,
            CharacterActionExecutionGateway gateway,
            CharacterActionSourceKind sourceKind,
            out CharacterActionRuntimePlanEntry cachedPlan,
            out CharacterActionDiagnosticCode rejectCode)
        {
            cachedPlan = null;
            rejectCode = CharacterActionDiagnosticCode.None;

            if (actionSlotId <= 0)
            {
                rejectCode = CharacterActionDiagnosticCode.ActionSlotBlocked;
                return false;
            }

            if (!requestedActionId.IsValid)
            {
                rejectCode = CharacterActionDiagnosticCode.ActionSlotEmpty;
                return false;
            }

            if (!_catalog.TryResolvePlanCache(
                    components,
                    entityId,
                    out CharacterActionRuntimePlanCache cache))
            {
                rejectCode = CharacterActionDiagnosticCode.MissingActionSet;
                return false;
            }

            if (!cache.TryGetOrCreate(requestedActionId, traceId, out cachedPlan))
            {
                rejectCode = CharacterActionDiagnosticCode.MissingActionConfig;
                return false;
            }

            if (cachedPlan == null || !cachedPlan.IsSuccess)
            {
                rejectCode = cachedPlan != null && cachedPlan.Diagnostics.Length > 0
                    ? cachedPlan.Diagnostics[0].CodeId
                    : CharacterActionDiagnosticCode.MissingActionConfig;
                return false;
            }

            if (hasRunningAction
                && !TryValidateCachedActionSlotWindow(
                    gateway,
                    actionSlotId,
                    cachedPlan.Action.ActionId,
                    cachedPlan.Action.Category,
                    out rejectCode))
            {
                return false;
            }

            return true;
        }

        private bool TryValidateCachedActionSlotWindow(
            CharacterActionExecutionGateway gateway,
            int actionSlotId,
            CharacterActionId targetActionId,
            CharacterActionCategory requestCategory,
            out CharacterActionDiagnosticCode rejectCode)
        {
            rejectCode = CharacterActionDiagnosticCode.None;
            CharacterActionInstance active = gateway != null ? gateway.ActiveInstance : null;
            if (active == null || !active.IsRunning)
                return true;

            CharacterCancelRule[] activeCancelRules =
                Registry.TryGetAction(active.Plan.ActionId, out CharacterActionConfig activeAction)
                    ? activeAction.CancelRules
                    : null;

            CharacterCancelConflictResult cancel = CharacterCancelConflictClassifier.Classify(
                CharacterActionTimelineAuthority.CharacterAuthored,
                activeCancelRules,
                combatTimeline: null,
                active.LocalFrame,
                targetActionId,
                requestCategory,
                actionSlotId);
            if (cancel.Allowed)
                return true;

            rejectCode = cancel.CodeId == CharacterActionDiagnosticCode.None
                ? CharacterActionDiagnosticCode.ActionSlotBlocked
                : cancel.CodeId;
            return false;
        }

        /// <summary>Checks the authored cancel/transition window for a stable action id.</summary>
        /// <remarks>NoAlloc after the referenced action plan and slot trace have been cached.</remarks>
        internal bool CanTransitionToAction(
            CharacterTestCharacterEntityId entityId,
            CharacterActionId requestedActionId)
        {
            if (!entityId.IsValid
                || !requestedActionId.IsValid
                || !Registry.TryGetAction(requestedActionId, out CharacterActionConfig action))
            {
                return false;
            }
            int actionSlotId = ResolveTransitionActionSlot(action.Category);
            RuntimeTraceIdentity traceId = _catalog.ResolveActionSlotTraceId(actionSlotId, default);
            bool hasRunningAction = TryGetRunningGateway(entityId, out CharacterActionExecutionGateway gateway);
            return TryResolveCachedActionSlot(
                _projection.Components,
                entityId,
                actionSlotId,
                requestedActionId,
                traceId,
                hasRunningAction,
                gateway,
                CharacterActionSourceKind.LocalInput,
                out _,
                out _);
        }

        /// <summary>Returns true when no action is running or any authored cancel window is active.</summary>
        /// <remarks>NoAlloc. This is target-agnostic and checks only the current local frame.</remarks>
        internal bool IsCurrentActionInterruptible(CharacterTestCharacterEntityId entityId)
        {
            if (!entityId.IsValid)
                return false;
            if (!TryGetRunningGateway(entityId, out CharacterActionExecutionGateway gateway))
                return true;
            CharacterActionInstance active = gateway?.ActiveInstance;
            if (active == null || !active.IsRunning)
                return true;
            return IsAnyCancelWindowActive(active.Definition?.CancelRules, active.LocalFrame);
        }

        internal static bool IsAnyCancelWindowActive(
            IReadOnlyList<CharacterCancelRule> rules,
            int localFrame)
        {
            for (int i = 0; rules != null && i < rules.Count; i++)
            {
                CharacterCancelRule rule = rules[i];
                if (localFrame >= rule.StartFrame && localFrame <= rule.EndFrame)
                    return true;
            }
            return false;
        }

        private static int ResolveTransitionActionSlot(CharacterActionCategory category)
        {
            switch (category)
            {
                case CharacterActionCategory.Skill: return CharacterTestActionSlotIds.Skill;
                case CharacterActionCategory.Guard: return CharacterTestActionSlotIds.Guard;
                case CharacterActionCategory.Dodge: return CharacterTestActionSlotIds.Dodge;
                case CharacterActionCategory.Jump: return CharacterTestActionSlotIds.Jump;
                case CharacterActionCategory.Interaction: return CharacterTestActionSlotIds.Interact;
                default: return CharacterTestActionSlotIds.AttackPrimary;
            }
        }

        private CharacterActionResolveResult ResolveTransitionTargetIntent(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            int intentId,
            RuntimeFrame frame,
            CharacterActionSourceKind sourceKind,
            int actionSlotId)
        {
            var request = new CharacterActionIntentRequest(
                new GameplayEntityId(entityId.IsValid ? entityId.Value : 1, 1),
                intentId,
                abilityId: null,
                abilityStableId: 0,
                requestedActionId: CharacterActionId.None,
                sourceKind,
                priority: 0,
                frame,
                new RuntimeTraceIdentity(CharacterTestActionTraceKinds.Intent, entityId.Value, frame.Value, intentId),
                actionSlotId);

            return _catalog.Resolver.ResolveCommand(
                CreateTransitionTargetContext(components, entityId),
                request);
        }

        internal bool TryStartActionSlot(
            CharacterTestCharacterEntity entity,
            int actionSlotId,
            CharacterActionId requestedActionId,
            RuntimeFrame frame,
            RuntimeTraceIdentity traceId = default,
            CharacterActionSourceKind sourceKind = CharacterActionSourceKind.LocalInput)
        {
            if (entity == null)
                return false;

            return TryStartActionSlot(
                entity.ComponentWorld,
                entity.Id,
                actionSlotId,
                requestedActionId,
                frame,
                traceId,
                sourceKind);
        }

        public bool TryStartActionSlot(
            CharacterTestCharacterEntityId entityId,
            int actionSlotId,
            CharacterActionId requestedActionId,
            RuntimeFrame frame,
            RuntimeTraceIdentity traceId = default)
        {
            return TryStartActionSlot(
                _projection.Components,
                entityId,
                actionSlotId,
                requestedActionId,
                frame,
                traceId,
                CharacterActionSourceKind.LocalInput);
        }

        internal bool TryStartActionSlot(
            CharacterTestCharacterEntityId entityId,
            int actionSlotId,
            CharacterActionId requestedActionId,
            RuntimeFrame frame,
            CharacterActionSourceKind sourceKind,
            RuntimeTraceIdentity traceId = default)
        {
            return TryStartActionSlot(
                _projection.Components,
                entityId,
                actionSlotId,
                requestedActionId,
                frame,
                traceId,
                sourceKind);
        }

        internal bool TryStartActionSlot(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            int actionSlotId,
            CharacterActionId requestedActionId,
            RuntimeFrame frame,
            RuntimeTraceIdentity traceId = default,
            CharacterActionSourceKind sourceKind = CharacterActionSourceKind.LocalInput)
        {
            if (components == null || !components.Contains(entityId))
                return false;

            components.SetAction(entityId, components.GetAction(entityId).ClearFrameRequests());
            if (!CanStartNormalAction(components, entityId, frame))
            {
                components.SetAction(
                    entityId,
                    components.GetAction(entityId).WithReject(CharacterTestActionRejectCode.BattleStateBlocked));
                return false;
            }

            if (!IsReady)
            {
                components.SetAction(
                    entityId,
                    components.GetAction(entityId).WithReject(CharacterActionDiagnosticCode.MissingActionSet));
                return false;
            }

            RuntimeTraceIdentity resolvedTraceId = _catalog.ResolveActionSlotTraceId(actionSlotId, traceId);
            bool hasRunningAction = TryGetRunningGateway(entityId, out CharacterActionExecutionGateway gateway);
            CharacterActionRuntimePlanEntry cachedPlan;
            CharacterActionDiagnosticCode rejectCode;
            if (!TryResolveCachedActionSlot(
                    components,
                    entityId,
                    actionSlotId,
                    requestedActionId,
                    resolvedTraceId,
                    hasRunningAction,
                    gateway,
                    sourceKind,
                    out cachedPlan,
                    out rejectCode))
            {
                components.SetAction(
                    entityId,
                    components.GetAction(entityId).WithReject(rejectCode));
                return false;
            }

            CharacterTestActionPartStateEligibility eligibility =
                EvaluatePartStateEligibility(components, entityId, cachedPlan.Action);
            if (!eligibility.IsEligible)
            {
                components.SetAction(
                    entityId,
                    components.GetAction(entityId).WithReject(eligibility.RejectCodeId));
                return false;
            }

            if (!hasRunningAction)
                gateway = GetGateway(entityId);

            _executions.EventBuffer.EnsureCapacity(Math.Max(16, cachedPlan.Definition.TrackEvents.Length + 4));
            CharacterActionExecutionRequest request = CharacterActionExecutionRequest.StartSlot(
                cachedPlan.ResolveResult,
                cachedPlan.Definition,
                sourceKind,
                actionSlotId,
                cachedPlan.Plan != null ? cachedPlan.Plan.Priority : 0,
                resolvedTraceId);
            CharacterActionExecutionResult execution = gateway.Execute(in request, _executions.EventBuffer);
            _projection.ApplyOperation(components, entityId, execution, frame);
            return execution.Accepted;
        }

        internal bool TryStartIntent(
            CharacterTestCharacterEntity entity,
            int intentId,
            RuntimeFrame frame,
            int actionSlotId = 0)
        {
            if (entity == null)
                return false;

            return TryStartIntent(entity.ComponentWorld, entity.Id, intentId, frame, actionSlotId);
        }

        /// <remarks>NoAlloc after initialization when the runner event buffer has sufficient capacity.</remarks>
        internal bool TerminateForLifecycle(
            CharacterTestCharacterEntityId entityId,
            RuntimeFrame frame,
            CharacterActionLifecycleReason reason,
            RuntimeTraceIdentity traceId)
        {
            CharacterTestComponentWorld components = _projection.Components;
            if (components == null || !components.Contains(entityId))
                return false;
            if (!TryGetRunningGateway(entityId, out CharacterActionExecutionGateway gateway))
                return true;

            components.SetAction(entityId, components.GetAction(entityId).ClearFrameRequests());
            CharacterActionExecutionRequest request =
                CharacterActionExecutionRequest.TerminateForLifecycle(reason, traceId);
            CharacterActionExecutionResult execution = gateway.Execute(in request, _executions.EventBuffer);
            _projection.ApplyOperation(components, entityId, execution, frame);
            return execution.Accepted;
        }

        /// <summary>Completes the expected active action from an authoritative CharacterTest fact.</summary>
        /// <remarks>NoAlloc after the shared event buffer is warm.</remarks>
        internal bool CompleteExternal(
            CharacterTestCharacterEntityId entityId,
            CharacterActionId expectedActionId,
            CharacterActionExternalCompletionReason reason,
            RuntimeFrame frame,
            RuntimeTraceIdentity traceId = default)
        {
            CharacterTestComponentWorld components = _projection.Components;
            if (components == null || !components.Contains(entityId))
                return false;
            if (!TryGetRunningGateway(entityId, out CharacterActionExecutionGateway gateway))
                return true;

            components.SetAction(entityId, components.GetAction(entityId).ClearFrameRequests());
            CharacterActionExecutionRequest request =
                CharacterActionExecutionRequest.CompleteExternal(expectedActionId, reason, traceId);
            CharacterActionExecutionResult execution = gateway.Execute(in request, _executions.EventBuffer);
            _projection.ApplyOperation(components, entityId, execution, frame);
            if (execution.Accepted && CharacterTestCharacterActionExecutionStore.IsTerminal(execution))
            {
                _executions.Remove(entityId);
                components.SetAction(entityId, CharacterTestCharacterActionComponent.None);
            }

            return execution.Accepted;
        }

        /// <summary>Exits a running action through its authored transition window without starting a replacement action.</summary>
        /// <remarks>NoAlloc after the shared event buffer is warm.</remarks>
        internal bool TryCancelOnly(
            CharacterTestCharacterEntityId entityId,
            CharacterActionRequestType requestType,
            RuntimeFrame frame,
            CharacterActionSourceKind sourceKind,
            RuntimeTraceIdentity traceId)
        {
            CharacterTestComponentWorld components = _projection.Components;
            if (components == null || !components.Contains(entityId))
                return false;
            if (!TryGetRunningGateway(entityId, out CharacterActionExecutionGateway gateway))
                return false;

            components.SetAction(entityId, components.GetAction(entityId).ClearFrameRequests());
            CharacterActionExecutionRequest request = CharacterActionExecutionRequest.CancelOnly(
                requestType,
                sourceKind,
                actionSlotId: 0,
                traceId: traceId);
            CharacterActionExecutionResult execution = gateway.Execute(in request, _executions.EventBuffer);
            _projection.ApplyOperation(components, entityId, execution, frame);
            if (execution.Accepted && CharacterTestCharacterActionExecutionStore.IsTerminal(execution))
            {
                _executions.Remove(entityId);
                components.SetAction(entityId, CharacterTestCharacterActionComponent.None);
            }

            return execution.Accepted;
        }

        /// <summary>Atomically replaces the active action at an explicit lifecycle boundary.</summary>
        /// <remarks>NoAlloc after the target plan and event buffer are warm.</remarks>
        internal bool ReplaceForLifecycle(
            CharacterTestCharacterEntityId entityId,
            CharacterActionId targetActionId,
            CharacterActionLifecycleReason reason,
            RuntimeFrame frame,
            RuntimeTraceIdentity traceId = default)
        {
            CharacterTestComponentWorld components = _projection.Components;
            if (components == null || !components.Contains(entityId) || !targetActionId.IsValid)
                return false;
            if (!_catalog.TryResolvePlanCache(
                    components,
                    entityId,
                    out CharacterActionRuntimePlanCache cache))
            {
                components.SetAction(
                    entityId,
                    components.GetAction(entityId).WithReject(CharacterActionDiagnosticCode.MissingActionSet));
                return false;
            }
            if (!cache.TryGetOrCreate(targetActionId, traceId, out CharacterActionRuntimePlanEntry entry)
                || entry == null
                || !entry.IsSuccess)
            {
                return false;
            }

            CharacterActionExecutionGateway gateway = GetGateway(entityId);
            _executions.EventBuffer.EnsureCapacity(Math.Max(16, entry.Definition.TrackEvents.Length + 8));
            CharacterActionExecutionRequest request = CharacterActionExecutionRequest.ReplaceForLifecycle(
                entry.ResolveResult,
                entry.Definition,
                reason,
                traceId);
            CharacterActionExecutionResult execution = gateway.Execute(in request, _executions.EventBuffer);
            _projection.ApplyOperation(components, entityId, execution, frame);
            return execution.Accepted;
        }

        /// <summary>Atomically replaces the active action for a higher-level behavior-recipe jump.</summary>
        /// <remarks>NoAlloc after the target plan and event buffer are warm.</remarks>
        internal bool ReplaceForOrchestration(
            CharacterTestCharacterEntityId entityId,
            CharacterActionId targetActionId,
            CharacterActionOrchestrationReason reason,
            RuntimeFrame frame,
            RuntimeTraceIdentity traceId = default)
        {
            CharacterTestComponentWorld components = _projection.Components;
            if (components == null || !components.Contains(entityId) || !targetActionId.IsValid)
                return false;
            if (!components.GetCombatState(entityId).IsAlive)
                return false;
            if (!_catalog.TryResolvePlanCache(
                    components,
                    entityId,
                    out CharacterActionRuntimePlanCache cache))
            {
                components.SetAction(
                    entityId,
                    components.GetAction(entityId).WithReject(CharacterActionDiagnosticCode.MissingActionSet));
                return false;
            }
            if (!cache.TryGetOrCreate(targetActionId, traceId, out CharacterActionRuntimePlanEntry entry)
                || entry == null
                || !entry.IsSuccess)
            {
                return false;
            }

            CharacterActionExecutionGateway gateway = GetGateway(entityId);
            _executions.EventBuffer.EnsureCapacity(Math.Max(16, entry.Definition.TrackEvents.Length + 8));
            CharacterActionExecutionRequest request = CharacterActionExecutionRequest.ReplaceForOrchestration(
                entry.ResolveResult,
                entry.Definition,
                reason,
                traceId);
            CharacterActionExecutionResult execution = gateway.Execute(in request, _executions.EventBuffer);
            _projection.ApplyOperation(components, entityId, execution, frame);
            return execution.Accepted;
        }

        /// <summary>Terminates an active action for a higher-level behavior-recipe boundary.</summary>
        /// <remarks>NoAlloc after the shared event buffer is warm.</remarks>
        internal bool TerminateForOrchestration(
            CharacterTestCharacterEntityId entityId,
            RuntimeFrame frame,
            CharacterActionOrchestrationReason reason,
            RuntimeTraceIdentity traceId = default)
        {
            CharacterTestComponentWorld components = _projection.Components;
            if (components == null || !components.Contains(entityId))
                return false;
            if (!components.GetCombatState(entityId).IsAlive)
                return false;
            if (!TryGetRunningGateway(entityId, out CharacterActionExecutionGateway gateway))
                return true;

            components.SetAction(entityId, components.GetAction(entityId).ClearFrameRequests());
            CharacterActionExecutionRequest request =
                CharacterActionExecutionRequest.TerminateForOrchestration(reason, traceId);
            CharacterActionExecutionResult execution = gateway.Execute(in request, _executions.EventBuffer);
            _projection.ApplyOperation(components, entityId, execution, frame);
            if (execution.Accepted && CharacterTestCharacterActionExecutionStore.IsTerminal(execution))
            {
                _executions.Remove(entityId);
                components.SetAction(entityId, CharacterTestCharacterActionComponent.None);
            }
            return execution.Accepted;
        }

        public bool TryStartIntent(
            CharacterTestCharacterEntityId entityId,
            int intentId,
            RuntimeFrame frame)
        {
            return TryStartIntent(entityId, intentId, frame, actionSlotId: 0);
        }

        public bool TryStartIntent(
            CharacterTestCharacterEntityId entityId,
            int intentId,
            RuntimeFrame frame,
            int actionSlotId)
        {
            return TryStartIntent(_projection.Components, entityId, intentId, frame, actionSlotId);
        }

        internal bool TryStartIntent(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            int intentId,
            RuntimeFrame frame,
            int actionSlotId = 0)
        {
            if (components == null || !components.Contains(entityId))
                return false;

            components.SetAction(entityId, components.GetAction(entityId).ClearFrameRequests());
            if (!CanStartNormalAction(components, entityId, frame))
            {
                components.SetAction(
                    entityId,
                    components.GetAction(entityId).WithReject(CharacterTestActionRejectCode.BattleStateBlocked));
                return false;
            }

            if (!IsReady)
            {
                components.SetAction(
                    entityId,
                    components.GetAction(entityId).WithReject(CharacterActionDiagnosticCode.ActionConfigNotReady));
                return false;
            }

            if (_catalog.ResolveActionSet(components, entityId) == null)
            {
                components.SetAction(
                    entityId,
                    components.GetAction(entityId).WithReject(CharacterActionDiagnosticCode.MissingActionSet));
                return false;
            }

            bool hasRunningAction = TryGetRunningGateway(entityId, out CharacterActionExecutionGateway gateway);
            CharacterActionResolveResult resolve = hasRunningAction
                ? ResolveTransitionTargetIntent(
                    components,
                    entityId,
                    intentId,
                    frame,
                    CharacterActionSourceKind.LocalInput,
                    actionSlotId)
                : ResolveIntent(components, entityId, intentId, frame, actionSlotId);
            if (!resolve.IsSuccess)
            {
                components.SetAction(
                    entityId,
                    components.GetAction(entityId).WithReject(resolve.Diagnostics.Length > 0
                        ? resolve.Diagnostics[0].CodeId
                        : CharacterActionDiagnosticCodeText.FromRejectReason(resolve.RejectReason)));
                return false;
            }

            if (!Registry.TryGetAction(resolve.Plan.ActionId, out CharacterActionConfig action))
            {
                components.SetAction(
                    entityId,
                    components.GetAction(entityId).WithReject(CharacterActionDiagnosticCode.MissingActionConfig));
                return false;
            }

            CharacterTestActionPartStateEligibility eligibility =
                EvaluatePartStateEligibility(components, entityId, action);
            if (!eligibility.IsEligible)
            {
                components.SetAction(
                    entityId,
                    components.GetAction(entityId).WithReject(eligibility.RejectCodeId));
                return false;
            }

            CharacterActionRunnerActionDefinition definition =
                CharacterActionRunnerActionDefinition.FromConfig(action);
            if (!hasRunningAction)
                gateway = GetGateway(entityId);

            _executions.EventBuffer.EnsureCapacity(Math.Max(16, definition.TrackEvents.Length + 4));
            CharacterActionExecutionRequest request = CharacterActionExecutionRequest.StartIntent(
                resolve,
                definition,
                CharacterActionSourceKind.LocalInput,
                resolve.Plan != null ? resolve.Plan.Priority : 0,
                resolve.TraceId,
                actionSlotId);
            CharacterActionExecutionResult execution = gateway.Execute(in request, _executions.EventBuffer);
            _projection.ApplyOperation(components, entityId, execution, frame);
            return execution.Accepted;
        }

        internal bool TryReact(
            CharacterTestCharacterEntity entity,
            CharacterReactionContext reactionContext,
            bool allowFallback = true)
        {
            if (entity == null)
                return false;

            return TryReact(
                entity.ComponentWorld,
                entity.Id,
                reactionContext,
                allowFallback);
        }

        public bool TryReact(
            CharacterTestCharacterEntityId entityId,
            CharacterReactionContext reactionContext,
            bool allowFallback = true)
        {
            return TryReact(
                _projection.Components,
                entityId,
                reactionContext,
                allowFallback);
        }

        internal bool TryReact(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            CharacterReactionContext reactionContext,
            bool allowFallback = true)
        {
            if (components == null || !components.Contains(entityId))
                return false;
            if (reactionContext.SourceKind != CharacterReactionContextSourceKind.Death
                && reactionContext.SourceKind != CharacterReactionContextSourceKind.Lifecycle
                && !components.GetCombatState(entityId).IsAlive)
                return false;

            components.SetAction(entityId, components.GetAction(entityId).ClearFrameRequests());
            if (!IsReady)
            {
                components.SetAction(
                    entityId,
                    components.GetAction(entityId).WithReject(CharacterActionDiagnosticCode.ActionConfigNotReady));
                return false;
            }

            CharacterActionExecutionGateway gateway = GetGateway(entityId);
            CharacterActionResolverContext reactionResolverContext =
                _catalog.CreateResolverContext(components, entityId);
            if (reactionResolverContext.ActionSet == null)
            {
                components.SetAction(
                    entityId,
                    components.GetAction(entityId).WithReject(CharacterActionDiagnosticCode.MissingActionSet));
                return false;
            }
            if (!_catalog.TryResolvePlanCache(
                    components,
                    entityId,
                    out CharacterActionRuntimePlanCache cache))
            {
                components.SetAction(
                    entityId,
                    components.GetAction(entityId).WithReject(CharacterActionDiagnosticCode.MissingActionSet));
                return false;
            }
            CharacterActionReactionStartResult start = CharacterActionReactionRuntime.TryStart(
                reactionResolverContext,
                reactionContext,
                gateway,
                cache,
                _executions.ReactionBuffer,
                allowFallback);

            if (start.HasExecution)
                _projection.ApplyOperation(components, entityId, start.Execution, reactionContext.Frame);

            if (!start.Started)
            {
                components.SetAction(
                    entityId,
                    components.GetAction(entityId).WithReject(ResolveReactionStartRejectCode(start)));
                return false;
            }

            return true;
        }

        private CharacterActionConfig ResolveActionConfig(CharacterActionId actionId)
        {
            return Registry.TryGetAction(actionId, out CharacterActionConfig action)
                ? action
                : null;
        }

        private static CharacterActionDiagnosticCode ResolveReactionStartRejectCode(CharacterActionReactionStartResult start)
        {
            if (start.DiagnosticCodeId != CharacterActionDiagnosticCode.None)
                return start.DiagnosticCodeId;
            if (start.ResolveResult != null && start.ResolveResult.Diagnostics.Length > 0)
                return start.ResolveResult.Diagnostics[0].CodeId;
            if (start.ResolveResult != null)
                return CharacterActionDiagnosticCodeText.FromRejectReason(start.ResolveResult.RejectReason);
            return CharacterActionDiagnosticCode.MissingActionConfig;
        }

        private static bool CanStartNormalAction(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            RuntimeFrame frame)
        {
            CharacterTestBattleStateSystem.RefreshEntity(components, entityId, frame);
            CharacterTestCharacterCombatStateComponent combatState = components.GetCombatState(entityId);
            if (combatState.LifeState != CharacterTestCharacterLifeState.Alive)
                return false;

            CharacterTestCharacterActionComponent action = components.GetAction(entityId);
            if (CharacterTestBattleStateSystem.TryResolveActionReason(action, out _))
                return false;

            CharacterTestBattleStateComponent battleState = components.GetBattleState(entityId);
            return !battleState.IsValid || battleState.CanStartNormalAction;
        }

        internal void TickEntity(CharacterTestCharacterEntity entity, bool clearFrameRequests = true)
        {
            if (entity == null)
                return;

            TickEntity(entity.ComponentWorld, entity.Id, default, clearFrameRequests);
        }

        internal void TickEntity(
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame,
            bool clearFrameRequests = true)
        {
            if (entity == null)
                return;

            TickEntity(entity.ComponentWorld, entity.Id, frame, clearFrameRequests);
        }

        public void TickEntity(CharacterTestCharacterEntityId entityId, bool clearFrameRequests = true)
        {
            TickEntity(_projection.Components, entityId, default, clearFrameRequests);
        }

        public void TickEntity(
            CharacterTestCharacterEntityId entityId,
            RuntimeFrame frame,
            bool clearFrameRequests = true)
        {
            TickEntity(_projection.Components, entityId, frame, clearFrameRequests);
        }

        internal void TickEntity(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            bool clearFrameRequests = true)
        {
            TickEntity(components, entityId, default, clearFrameRequests);
        }

        internal void TickEntity(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            RuntimeFrame frame,
            bool clearFrameRequests = true)
        {
            if (components == null || !components.Contains(entityId))
                return;

            if (clearFrameRequests)
                components.SetAction(entityId, components.GetAction(entityId).ClearFrameRequests());

            _projection.BeginActionStep(components, entityId);
            if (!_executions.TryGet(entityId, out CharacterActionExecutionGateway gateway))
                return;

            CharacterActionExecutionResult tick = gateway.Tick(_executions.EventBuffer);
            _projection.ApplyOperation(components, entityId, tick, frame);
            if (CharacterTestCharacterActionExecutionStore.IsTerminal(tick))
            {
                _executions.Remove(entityId);
                components.SetAction(entityId, CharacterTestCharacterActionComponent.None);
            }
        }

        internal void FreezeEntityActionFrame(CharacterTestCharacterEntity entity)
        {
            if (entity == null)
                return;

            FreezeEntityActionFrame(entity.ComponentWorld, entity.Id);
        }

        public void FreezeEntityActionFrame(CharacterTestCharacterEntityId entityId)
        {
            FreezeEntityActionFrame(_projection.Components, entityId);
        }

        internal void FreezeEntityActionFrame(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId)
        {
            if (components == null || !components.Contains(entityId))
                return;

            components.SetAction(entityId, components.GetAction(entityId).ClearFrameRequests());
        }

        internal void TerminateAndReleaseEntityAction(
            CharacterTestCharacterEntity entity,
            RuntimeFrame frame,
            CharacterActionLifecycleReason reason,
            RuntimeTraceIdentity traceId = default)
        {
            if (entity == null)
                return;

            TerminateAndReleaseEntityAction(entity.ComponentWorld, entity.Id, frame, reason, traceId);
        }

        public void TerminateAndReleaseEntityAction(
            CharacterTestCharacterEntityId entityId,
            RuntimeFrame frame,
            CharacterActionLifecycleReason reason,
            RuntimeTraceIdentity traceId = default)
        {
            TerminateAndReleaseEntityAction(_projection.Components, entityId, frame, reason, traceId);
        }

        internal void TerminateAndReleaseEntityAction(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            RuntimeFrame frame,
            CharacterActionLifecycleReason reason,
            RuntimeTraceIdentity traceId = default)
        {
            if (components == null || !components.Contains(entityId))
                return;

            _projection.BeginActionStep(components, entityId);
            if (_executions.TryGet(entityId, out CharacterActionExecutionGateway gateway))
            {
                CharacterActionExecutionRequest request =
                    CharacterActionExecutionRequest.TerminateForLifecycle(reason, traceId);
                CharacterActionExecutionResult execution = gateway.Execute(in request, _executions.EventBuffer);
                _projection.ApplyOperation(components, entityId, execution, frame);
            }

            _executions.Remove(entityId);
            components.SetAction(entityId, CharacterTestCharacterActionComponent.None);
        }

        /// <summary>Terminates every live CharacterAction before its owning runtime boundary is released.</summary>
        /// <remarks>NoAlloc after the shared event buffer is warm.</remarks>
        internal void TerminateAll(
            CharacterTestCharacterRuntimeWorld world,
            RuntimeFrame frame,
            CharacterActionLifecycleReason reason,
            RuntimeTraceIdentity traceId)
        {
            if (world == null)
                return;

            CharacterTestComponentWorld components = world.CharacterComponents;
            IReadOnlyList<CharacterTestCharacterEntity> entities = world.Entities;
            for (int i = 0; i < entities.Count; i++)
            {
                TerminateAndReleaseEntityAction(
                    components,
                    entities[i].Id,
                    frame,
                    reason,
                    traceId);
            }
        }

        internal CharacterActionDebugSnapshot CreateDebugSnapshot(CharacterTestCharacterEntity entity)
        {
            return entity != null ? CreateDebugSnapshot(entity.Id) : null;
        }

        public CharacterActionDebugSnapshot CreateDebugSnapshot(CharacterTestCharacterEntityId entityId)
        {
            if (!_executions.TryGet(entityId, out CharacterActionExecutionGateway gateway))
                return null;

            return gateway.CreateDebugSnapshot();
        }

        /// <summary>Captures the admitted authority instance for lifecycle observation, including after gateway removal.</summary>
        /// <remarks>NoAlloc. Internal consumers may read the instance but must never mutate it.</remarks>
        internal CharacterActionInstance GetActionInstanceForObservation(CharacterTestCharacterEntityId entityId)
        {
            return _executions.TryGet(entityId, out CharacterActionExecutionGateway gateway) ? gateway.ActiveInstance : null;
        }

        /// <summary>Reads the currently running authored action without creating a debug snapshot.</summary>
        /// <remarks>NoAlloc. Internal presentation and diagnostics only.</remarks>
        internal bool TryGetActiveActionPresentationState(
            CharacterTestCharacterEntityId entityId,
            out long instanceId,
            out CharacterActionId actionId,
            out int localFrame,
            out int durationFrames)
        {
            instanceId = 0L;
            actionId = CharacterActionId.None;
            localFrame = 0;
            durationFrames = 0;
            if (!TryGetRunningGateway(entityId, out CharacterActionExecutionGateway gateway))
                return false;

            CharacterActionInstance active = gateway.ActiveInstance;
            if (active?.Plan == null)
                return false;

            instanceId = active.InstanceId;
            actionId = active.Plan.ActionId;
            localFrame = Math.Max(0, active.LocalFrame);
            durationFrames = Math.Max(0, active.Plan.DurationFrames);
            return instanceId > 0L && actionId.IsValid;
        }

        internal bool TryCreateRunnerRestoreState(
            CharacterTestCharacterEntityId entityId,
            out CharacterActionExecutionState state)
        {
            return _snapshots.TryCapture(entityId, out state);
        }

        internal bool TryRestoreRunner(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId,
            CharacterActionExecutionState state,
            out string diagnostic)
        {
            return _snapshots.TryRestore(components, entityId, state, out diagnostic);
        }

        internal CombatActionSupportProfile? GetCurrentCohesionSupportProfile(CharacterTestCharacterEntity entity)
        {
            return entity != null ? GetCurrentCohesionSupportProfile(entity.Id) : null;
        }

        public CombatActionSupportProfile? GetCurrentCohesionSupportProfile(
            CharacterTestCharacterEntityId entityId)
        {
            if (!_executions.TryGet(entityId, out CharacterActionExecutionGateway gateway))
                return null;

            CharacterActionInstance active = gateway.ActiveInstance;
            if (active == null || !active.IsRunning)
                return null;

            if (!Registry.TryGetAction(active.Plan.ActionId, out CharacterActionConfig action)
                || action.CohesionSupport == null
                || !action.CohesionSupport.Enabled
                || !action.CohesionSupport.HasCohesionSupportWindow)
            {
                return null;
            }

            var profile = new CombatActionSupportProfile(
                ToFix64(action.CohesionSupport.SupportRate),
                ToFix64(action.CohesionSupport.MinSupportRatio),
                true,
                new CombatFrameRange(
                    action.CohesionSupport.CohesionSupportWindowStartFrame,
                    action.CohesionSupport.CohesionSupportWindowEndFrame));
            return CombatCohesionLimitAdapter.IsInCohesionSupportWindow(profile, active.LocalFrame)
                ? profile
                : (CombatActionSupportProfile?)null;
        }

        private CharacterActionResolverContext CreateContext(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId)
        {
            return _catalog.CreateResolverContext(
                components,
                entityId,
                CreateResolverState(entityId, activeActionBlocksImmediateStart: true));
        }

        private CharacterActionResolverContext CreateTransitionTargetContext(
            CharacterTestComponentWorld components,
            CharacterTestCharacterEntityId entityId)
        {
            return _catalog.CreateResolverContext(
                components,
                entityId,
                CreateResolverState(entityId, activeActionBlocksImmediateStart: false));
        }

        private CharacterActionResolverState CreateResolverState(
            CharacterTestCharacterEntityId entityId,
            bool activeActionBlocksImmediateStart)
        {
            if (!_executions.TryGet(entityId, out CharacterActionExecutionGateway gateway))
                return default;

            CharacterActionDebugSnapshot snapshot = gateway.CreateDebugSnapshot();
            if (snapshot == null || snapshot.State != CharacterActionInstanceState.Running)
                return default;

            CharacterCancelRule[] activeCancelRules =
                Registry.TryGetAction(snapshot.ActionId, out CharacterActionConfig action)
                    ? action.CancelRules
                    : null;

            return new CharacterActionResolverState(
                hasActiveAction: true,
                activeActionBlocksImmediateStart: activeActionBlocksImmediateStart,
                activeActionId: snapshot.ActionId,
                activeActionLocalFrame: snapshot.LocalFrame,
                activeCancelRules: activeCancelRules);
        }

        private CharacterActionExecutionGateway GetGateway(CharacterTestCharacterEntityId entityId)
        {
            return _executions.GetOrCreate(entityId);
        }

        private bool TryGetRunningGateway(
            CharacterTestCharacterEntityId entityId,
            out CharacterActionExecutionGateway gateway)
        {
            return _executions.TryGetRunning(entityId, out gateway);
        }

        private static Fix64 ToFix64(float value)
        {
            if (value <= 0f)
                return Fix64.Zero;

            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }
    }
}
