using System;
using System.Globalization;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    internal sealed partial class CharacterTestTacticalExecutionAdapter
    {
        private TacticalCardInstance _behindPreparedCard;
        private uint _behindPreparedVersion;
        private int[] _behindPreparedActionIds = Array.Empty<int>();
        private string FindCharacterStableId()
        {
            foreach (var definition in CharacterTestPlayableCharacterCatalog.Discover())
                if (definition.CharacterId == _entity.Identity.CharacterId) return definition.TacticalDefaults?.CharacterStableId;
            return null;
        }

        private bool ValidatePublishedOwnership(TacticalActionCardDefinition card, out string issue)
        {
            if (_deployment != null)
            {
                issue = string.Empty;
                return _deployment.Contains(card) || Reject("execution:CardNotInConfirmedDeployment", out issue);
            }
            var service = CharacterTacticalServices.Player;
            var owned = service?.FindCard(FindCharacterStableId(), card.PublishedCardId);
            if (owned == null || owned.Revision != card.PublishedRevision || owned.ContentHash != card.PublishedContentHash)
                return Reject("execution:PublishedCardNotOwnedOrChanged", out issue);
            if (!service.TryGetBindings(FindCharacterStableId(), card.PublishedCardId, out _, out issue)) return Reject(issue, out issue);
            return true;
        }

        private bool TryResolvePublished(TacticalActionCardDefinition card,
            out CharacterTestRuntimeAiPlannerPublishedBehaviorCard published, out string issue)
        {
            published = null;
            if (!ValidatePublishedOwnership(card, out issue)) return false;
            var state = _publishedCatalogLoader(
                new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, _runtime.ActionRuntime.Registry));
            if (!state.Success) return Reject(state.Diagnostic, out issue);

            bool identityFound = false;
            foreach (var candidate in state.CardCatalog.Cards)
            {
                if (candidate.StableCardId != card.PublishedCardId || candidate.Revision != card.PublishedRevision) continue;
                identityFound = true;
                if (candidate.ContentHash.ToString(CultureInfo.InvariantCulture) == card.PublishedContentHash)
                { published = candidate; break; }
            }
            if (published == null) return Reject(identityFound
                ? "execution:PublishedCardHashMismatch" : "execution:PublishedCardVersionMissing", out issue);
            if (published.TacticalSource == null) return Reject("execution:PublishedTacticalSourceMissing", out issue);
            issue = string.Empty; return true;
        }

        // Both TryPrepare and TryAdmit call this before the behavior host can start its first movement.
        // A configured action is checked even when the card begins with movement or only uses its range.
        internal bool ValidateBoundActionCooldowns(TacticalActionCardDefinition card, out string issue)
        {
            issue = string.Empty;
            if (!card.HasPublishedBehavior) return ValidateActionCooldown(card.ActionId.Value, out issue);
            if (_deployment != null)
            {
                if (!_deployment.TryGetBindings(card.DeckEntryInstanceId, out var bindings))
                { issue = "execution:ConfirmedBindingsMissing"; return false; }
                return ValidateBoundActionCooldowns(bindings, _runtime.ActionRuntime.Registry, _runtime.ActionUseAuthority,
                    _entity.Id.Value, _frame, out issue);
            }
            else
            {
                var service = CharacterTacticalServices.Player;
                if (service == null || !service.TryGetBindings(FindCharacterStableId(), card.PublishedCardId, out var bindings, out issue))
                { issue = "execution:BindingsUnavailable"; return false; }
                foreach (var pair in bindings)
                    if (!ValidateActionCooldown(pair.Value.ActionId, out issue)) return false;
            }
            return true;
        }

        private CardPlayEvaluation EvaluateBoundActionCooldowns(TacticalActionCardDefinition card)
        {
            if (!card.HasPublishedBehavior)
                return EvaluateActionCooldown(_runtime.ActionRuntime.Registry, _runtime.ActionUseAuthority,
                    _entity.Id.Value, card.ActionId.Value, _frame, _runtime.SimulationTickRate);
            if (_deployment != null)
            {
                if (!_deployment.TryGetBindings(card.DeckEntryInstanceId, out var bindings))
                    return new CardPlayEvaluation(CardPlayFailure.MissingActionOrSlotBinding, 0, 0);
                return EvaluateBoundActionCooldowns(bindings, _runtime.ActionRuntime.Registry,
                    _runtime.ActionUseAuthority, _entity.Id.Value, _frame, _runtime.SimulationTickRate);
            }
            var service = CharacterTacticalServices.Player;
            if (service == null || !service.TryGetBindings(FindCharacterStableId(), card.PublishedCardId, out var current, out _))
                return new CardPlayEvaluation(CardPlayFailure.MissingActionOrSlotBinding, 0, 0);
            var result = new CardPlayEvaluation(CardPlayFailure.None, 0, 0);
            foreach (var pair in current)
            {
                var next = EvaluateActionCooldown(_runtime.ActionRuntime.Registry, _runtime.ActionUseAuthority,
                    _entity.Id.Value, pair.Value.ActionId, _frame, _runtime.SimulationTickRate);
                if (!next.IsLegal && next.Failure != CardPlayFailure.BoundActionOnCooldown) return next;
                if (next.RemainingCooldownFrames > result.RemainingCooldownFrames) result = next;
            }
            return result;
        }

        internal static CardPlayEvaluation EvaluateBoundActionCooldowns(System.Collections.Generic.IReadOnlyDictionary<string, int> bindings,
            MxFramework.CharacterAction.CharacterActionConfigRegistry registry, CharacterTestActionUseAuthority authority,
            int entityId, MxFramework.Runtime.RuntimeFrame frame, int simulationTickRate)
        {
            var result = new CardPlayEvaluation(CardPlayFailure.None, 0, 0);
            foreach (var pair in bindings)
            {
                var next = EvaluateActionCooldown(registry, authority, entityId, pair.Value, frame, simulationTickRate);
                if (!next.IsLegal && next.Failure != CardPlayFailure.BoundActionOnCooldown) return next;
                if (next.RemainingCooldownFrames > result.RemainingCooldownFrames) result = next;
            }
            return result;
        }

        private CardPlayEvaluation EvaluateBoundActionCosts(TacticalActionCardDefinition card,int additionalCardMotion,int reservationCredit=0)
        {
            int maximumCost=card.BaseMomentumCost;
            if(!card.HasPublishedBehavior)return EvaluateActionCosts(card.ActionId.Value,additionalCardMotion,card.BaseMomentumCost,reservationCredit);
            // Bindings are candidates, not an execution count. Each actual action still pays at its own commit.
            if(_deployment!=null)
            {
                if(!_deployment.TryGetBindings(card.DeckEntryInstanceId,out var bindings))
                    return new CardPlayEvaluation(CardPlayFailure.MissingActionOrSlotBinding,0,0);
                foreach(var binding in bindings)
                {
                    var evaluation=EvaluateActionCosts(binding.Value,additionalCardMotion,card.BaseMomentumCost,reservationCredit);
                    if(!evaluation.IsLegal)return evaluation;
                    maximumCost=Math.Max(maximumCost,evaluation.EffectiveCost);
                }
            }
            else
            {
                var service=CharacterTacticalServices.Player;
                if(service==null || !service.TryGetBindings(FindCharacterStableId(),card.PublishedCardId,out var bindings,out _))
                    return new CardPlayEvaluation(CardPlayFailure.MissingActionOrSlotBinding,0,0);
                foreach(var binding in bindings)
                {
                    var evaluation=EvaluateActionCosts(binding.Value.ActionId,additionalCardMotion,card.BaseMomentumCost,reservationCredit);
                    if(!evaluation.IsLegal)return evaluation;
                    maximumCost=Math.Max(maximumCost,evaluation.EffectiveCost);
                }
            }
            return new CardPlayEvaluation(CardPlayFailure.None,maximumCost,0);
        }
        private CardPlayEvaluation EvaluateActionCosts(int actionId,int additionalCardMotion,int baseCardMotion,int reservationCredit)
        {
            if(!_runtime.ActionRuntime.Registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(actionId),out var action))
                return new CardPlayEvaluation(CardPlayFailure.BoundActionUnavailable,0,0,actionId);
            var usage=_runtime.ActionUseAuthority.EvaluateCardCosts(_entity.Id.Value,action,additionalCardMotion,out int motion,reservationCredit);
            long total=(long)baseCardMotion+motion;
            var failure=usage.Status==CharacterTestActionUseStatus.WeaponTypeUnclassified?CardPlayFailure.WeaponTypeUnclassified:
                usage.Status==CharacterTestActionUseStatus.WeaponTypeUnavailable?CardPlayFailure.WeaponTypeMismatch:
                usage.Available && total<=int.MaxValue?CardPlayFailure.None:
                total>int.MaxValue || usage.Status==CharacterTestActionUseStatus.ResourceUnavailable && usage.AttributeId==CharacterTestCharacterAttributeIds.Motion
                ?CardPlayFailure.InsufficientMomentum:CardPlayFailure.BoundActionUnavailable;
            return new CardPlayEvaluation(failure,
                total<=int.MaxValue?(int)total:int.MaxValue,0,actionId);
        }

        private static CardPlayEvaluation EvaluateActionCooldown(MxFramework.CharacterAction.CharacterActionConfigRegistry registry,
            CharacterTestActionUseAuthority authority, int entityId, int actionId, MxFramework.Runtime.RuntimeFrame frame, int tickRate)
        {
            if (registry == null || authority == null || !registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(actionId), out var action))
                return new CardPlayEvaluation(CardPlayFailure.BoundActionUnavailable, 0, 0, actionId);
            var result = authority.EvaluateCooldown(entityId, action, frame, out long remaining);
            return new CardPlayEvaluation(result.Available ? CardPlayFailure.None
                : result.Status == CharacterTestActionUseStatus.WeaponTypeUnclassified ? CardPlayFailure.WeaponTypeUnclassified
                : result.Status == CharacterTestActionUseStatus.WeaponTypeUnavailable ? CardPlayFailure.WeaponTypeMismatch
                : remaining > 0 ? CardPlayFailure.BoundActionOnCooldown : CardPlayFailure.BoundActionUnavailable,
                0, 0, actionId, remaining, tickRate);
        }

        private bool ValidateActionCooldown(int actionId, out string issue)
            => ValidateActionCooldown(_runtime.ActionRuntime.Registry, _runtime.ActionUseAuthority, _entity.Id.Value, actionId, _frame, out issue);

        internal static bool ValidateBoundActionCooldowns(System.Collections.Generic.IReadOnlyDictionary<string, int> bindings,
            MxFramework.CharacterAction.CharacterActionConfigRegistry registry, CharacterTestActionUseAuthority authority,
            int entityId, MxFramework.Runtime.RuntimeFrame frame, out string issue)
        {
            issue = string.Empty;
            foreach (var pair in bindings)
                if (!ValidateActionCooldown(registry, authority, entityId, pair.Value, frame, out issue)) return false;
            return true;
        }

        private static bool ValidateActionCooldown(MxFramework.CharacterAction.CharacterActionConfigRegistry registry,
            CharacterTestActionUseAuthority authority, int entityId, int actionId, MxFramework.Runtime.RuntimeFrame frame, out string issue)
        {
            issue = string.Empty;
            if (!registry.TryGetAction(new MxFramework.CharacterAction.CharacterActionId(actionId), out var action))
            { issue = "execution:BoundActionMissing"; return false; }
            var types = authority.EvaluateWeaponTypes(entityId, action, out issue);
            if (!types.Available) { issue = "execution:" + issue; return false; }
            var result = authority.EvaluateCooldown(entityId, action, frame, out long remaining);
            if (result.Available) return true;
            issue = remaining > 0 ? "execution:BoundActionOnCooldown" : "execution:BoundActionCooldownUnavailable";
            return false;
        }

        // Cold selection query: exact catalog lookup/compilation, with no session preparation, command, payment or summary repair.
        public bool TryPrepareBehindPreview(TacticalCardInstance instance, out CharacterTestBehaviorExecutionMovement movement, out string issue)
        {
            movement = default; issue = string.Empty;
            _behindPreparedCard = null; _behindPreparedActionIds = Array.Empty<int>();
            if (_entity == null || instance?.Definition is not TacticalActionCardDefinition card || !card.HasPublishedBehavior)
                return false;
            var state = _publishedCatalogLoader(new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, _runtime.ActionRuntime.Registry));
            if (!state.Success) return false;
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard published = null;
            foreach (var candidate in state.CardCatalog.Cards)
                if (candidate.StableCardId == card.PublishedCardId && candidate.Revision == card.PublishedRevision
                    && candidate.ContentHash.ToString(CultureInfo.InvariantCulture) == card.PublishedContentHash)
                { published = candidate; break; }
            if (published?.TacticalSource == null) return false;
            var definitions = published.TacticalSource.IsLegacy
                ? new[] { published.TacticalSource.CloneLegacy() }
                : System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(published.TacticalSource.CloneDefinitions(), d => d.Source));
            bool applies = false;
            foreach (var definition in definitions)
                foreach (var timeline in definition.Timelines)
                    foreach (var evt in timeline.Events)
                        applies |= evt.Operation?.Movement?.BehindTarget != null;
            if (!applies) return false;
            CharacterTestTacticalCardInputs inputs;
            if (!published.TacticalSource.HasPlayerActionSlot)
                inputs = CharacterTestTacticalCardInputs.BindSlots(_entity.Identity.CharacterId,
                    new System.Collections.Generic.Dictionary<string, CharacterTestTacticalCardInputs>()).WithSimulationTickRate(_runtime.SimulationTickRate);
            else
            {
                CharacterTacticalBoundCardInputs bindings;
                if (_deployment != null)
                {
                    if (!_deployment.TryGetBindings(card.DeckEntryInstanceId, out var confirmed))
                    { issue = "preview:BindingsUnavailable"; return true; }
                    bindings = new CharacterTacticalBoundCardInputs(_runtime, _entity.Id.Value, confirmed);
                }
                else bindings = new CharacterTacticalBoundCardInputs(CharacterTacticalServices.Player, _runtime,
                    _entity.Id.Value, FindCharacterStableId(), card.PublishedCardId);
                bindings.WithRequirements(published.TacticalSource.InputRequirements);
                if (!bindings.TryPrepareReadOnly(out inputs, out issue)) return true;
            }
            if (!published.TacticalSource.TryCompile(published.StableCardId, published.Metadata.DisplayName, published.Revision,
                inputs, out var compiled, out issue)) return true;
            if (CharacterTestBehindTargetPreview.TrySelectInitialMovement(compiled.Execution, out movement))
            {
                _behindPreparedCard = instance; _behindPreparedVersion = instance.Version;
                _behindPreparedActionIds = inputs.CaptureConfiguredActionIds();
                return true;
            }
            issue = "preview:InitialMovementUnavailable"; return true;
        }

        public bool TrySampleBehindPreview(TacticalCardInstance card, in CharacterTestBehaviorExecutionMovement movement,
            out TacticalWorldPoint origin, out CharacterTestRuntimeAiLabMovementTargetSample sample)
        {
            sample = default; origin = default;
            // Admission state is live; do not cache a cooldown rejection with the compiled movement.
            if (_behindPreparedCard == null || !ReferenceEquals(card, _behindPreparedCard)
                || card.Version != _behindPreparedVersion || _entity == null || !_hasContext
                || !_runtime.ActionUseAuthority.EvaluateCooldowns(_entity.Id.Value, _behindPreparedActionIds,
                    _runtime.ActionRuntime.Registry, _frame).Available) return false;
            if (!TryGetPointOrigin(out origin) || !movement.BehindTarget.Enabled) return false;
            var target = ResolveAdmissionTarget();
            if (target == null || !target.CombatState.IsAlive || target == _entity
                || target.Team.TeamId == _entity.Team.TeamId) return false;
            UnityEngine.Vector2 toward = CharacterTestDirectAiNavigationService.ToTarget(_entity, target);
            float rootDistance = toward.magnitude;
            float surfaceDistance;
            if (_facts != null && _facts.TargetExists)
            {
                if (!_facts.TargetDistanceAvailable) return false;
                surfaceDistance = _facts.TargetDistance;
            }
            else
            {
                if (!_runtime.PerceptionFrameCache.TryAcquireNearestHurtbox(_frame, _entity, target, out _, out var distance)
                    || !distance.HasDistance) return false;
                surfaceDistance = distance.SurfaceGapMeters;
            }
            sample = new CharacterTestRuntimeAiLabMovementTargetSample(true, surfaceDistance,
                toward.normalized, toward.normalized, true, target.Transform.YawMilliDegrees, rootDistance, target.Id.Value);
            return true;
        }

        private bool TryCompilePublished(TacticalActionCardDefinition card, out string issue)
        {
            if (!TryResolvePublished(card, out var published, out issue)) return false;
            CharacterTacticalBoundCardInputs source;
            if (_deployment != null)
            {
                if (!_deployment.TryGetBindings(card.DeckEntryInstanceId, out var bindings)) return Reject("execution:ConfirmedBindingsMissing", out issue);
                source = new CharacterTacticalBoundCardInputs(_runtime, _entity.Id.Value, bindings);
            }
            else source = new CharacterTacticalBoundCardInputs(CharacterTacticalServices.Player, _runtime, _entity.Id.Value,
                FindCharacterStableId(), card.PublishedCardId);
            source.WithRequirements(published.TacticalSource.InputRequirements);
            if (!published.TacticalSource.HasPlayerActionSlot || _deployment?.UsesLegacyFunctionalResolverFallback == true)
                source.UseFunctionalResolvers();
            if (!source.TryPrepare(out var inputs, out issue)) return Reject(issue, out issue);
            if (!published.TacticalSource.TryCompile(published.StableCardId, published.Metadata.DisplayName, published.Revision,
                inputs, out var program, out issue)) return Reject(issue, out issue);
            if (!_session.TryCompilePublished(published.Metadata.DisplayName, published.ContentHash.ToString(CultureInfo.InvariantCulture),
                program, inputs, source)) return Reject(_session.LastIssue, out issue);
            issue = string.Empty; return true;
        }
    }
}
