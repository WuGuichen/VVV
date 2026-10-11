using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;

namespace MxFramework.Tests.Gameplay.Triggers
{
    /// <summary>
    /// Test-only integrated authority oracle. It composes the independent source and policy
    /// models, owns its allocator, and materializes typed activation/intent projections.
    /// </summary>
    internal sealed class GameplayTriggerAuthorityProjectionReferenceModel
    {
        private readonly GameplayTriggerReferenceModel _source;
        private readonly GameplayTriggerPolicyReferenceModel _policy =
            new GameplayTriggerPolicyReferenceModel();
        private readonly GameplayTriggerInstanceId _instanceId;
        private readonly GameplayTriggerDefinitionId _definitionId;
        private readonly GameplayTriggerBindingId _bindingId;
        private readonly GameplayEntityId _owner;
        private readonly GameplayTriggerRouteId _routeId;
        private readonly GameplayTriggerOccurrenceKind _occurrenceMask;
        private readonly GameplayTriggerActivationPolicy _activationPolicy;
        private readonly GameplayTriggerActionInstruction _action;
        private readonly List<GameplayTriggerAuthorityReferenceActivation> _activations =
            new List<GameplayTriggerAuthorityReferenceActivation>();
        private readonly List<GameplayTriggerAuthorityReferenceIntent> _intents =
            new List<GameplayTriggerAuthorityReferenceIntent>();
        private readonly List<GameplayTriggerReferenceRelationState> _relations =
            new List<GameplayTriggerReferenceRelationState>();
        private readonly List<GameplayTriggerPolicyReferenceState> _routeStates =
            new List<GameplayTriggerPolicyReferenceState>();
        private long _nextActivationId = 1L;

        public GameplayTriggerAuthorityProjectionReferenceModel(
            GameplayTriggerInstanceId instanceId,
            GameplayTriggerDefinitionId definitionId,
            GameplayTriggerBindingId bindingId,
            GameplayEntityId owner,
            GameplayTriggerRouteId routeId,
            GameplayTriggerOccurrenceKind occurrenceMask,
            in GameplayTriggerActivationPolicy activationPolicy,
            in GameplayTriggerActionInstruction action)
        {
            _instanceId = instanceId;
            _definitionId = definitionId;
            _bindingId = bindingId;
            _owner = owner;
            _routeId = routeId;
            _occurrenceMask = occurrenceMask;
            _activationPolicy = activationPolicy;
            _action = action;
            _source = new GameplayTriggerReferenceModel(instanceId);
        }

        public IReadOnlyList<GameplayTriggerReferenceOccurrence> Occurrences =>
            _source.Occurrences;
        public IReadOnlyList<GameplayTriggerReferenceRelationState> Relations => _relations;
        public IReadOnlyList<GameplayTriggerPolicyReferenceState> RouteStates => _routeStates;
        public IReadOnlyList<GameplayTriggerAuthorityReferenceActivation> Activations =>
            _activations;
        public IReadOnlyList<GameplayTriggerAuthorityReferenceIntent> Intents => _intents;
        public int ActiveCount => _source.OccupancyActiveCount;
        public long AggregatePresenceEpoch => _source.OccupancyAggregatePresenceEpoch;
        public long NextActivationId => _nextActivationId;

        public void EvaluateOccupancy(
            long frame,
            IReadOnlyList<GameplayEntityId> subjects,
            bool reservationAccepted = true)
        {
            _source.ReduceOccupancy(frame, subjects);
            _activations.Clear();
            _intents.Clear();

            for (int i = 0; i < _source.Occurrences.Count; i++)
            {
                GameplayTriggerReferenceOccurrence occurrence = _source.Occurrences[i];
                if ((_occurrenceMask & occurrence.Kind) == 0)
                {
                    continue;
                }

                var policyOccurrence = new GameplayTriggerPolicyReferenceOccurrence(
                    occurrence.Frame,
                    occurrence.InstanceId,
                    occurrence.Kind,
                    occurrence.Subject,
                    occurrence.Instigator,
                    occurrence.PresenceEpoch,
                    occurrence.PresenceStartFrame,
                    _source.OccupancyAggregatePresenceEpoch);
                if (_policy.Check(_routeId, _activationPolicy, policyOccurrence) !=
                    GameplayTriggerPolicyReferenceDecision.Accepted)
                {
                    continue;
                }
                if (!reservationAccepted)
                {
                    continue;
                }

                _policy.Commit(_routeId, _activationPolicy, policyOccurrence);
                long activationId = _nextActivationId++;
                _activations.Add(new GameplayTriggerAuthorityReferenceActivation(
                    activationId,
                    _definitionId,
                    _bindingId,
                    _routeId,
                    occurrence));
                _intents.Add(new GameplayTriggerAuthorityReferenceIntent(
                    activationId,
                    _bindingId,
                    _owner,
                    ResolveTarget(occurrence),
                    _routeId,
                    actionIndex: 0,
                    occurrence,
                    _action));
            }

            _source.CaptureOccupancyRelations(_relations);
            _policy.CaptureStates(_routeStates);
        }

        private GameplayTriggerTargetRef ResolveTarget(
            in GameplayTriggerReferenceOccurrence occurrence)
        {
            switch (_action.TargetSelector)
            {
                case GameplayTriggerTargetSelector.Owner:
                    return GameplayTriggerTargetRef.FromGameplayEntity(_owner);
                case GameplayTriggerTargetSelector.Subject:
                    return GameplayTriggerTargetRef.FromGameplayEntity(occurrence.Subject);
                case GameplayTriggerTargetSelector.Instigator:
                    return GameplayTriggerTargetRef.FromGameplayEntity(occurrence.Instigator);
                default:
                    throw new InvalidOperationException(
                        "The focused authority oracle does not define this target selector.");
            }
        }
    }

    internal readonly struct GameplayTriggerAuthorityReferenceActivation
    {
        public GameplayTriggerAuthorityReferenceActivation(
            long activationId,
            GameplayTriggerDefinitionId definitionId,
            GameplayTriggerBindingId bindingId,
            GameplayTriggerRouteId routeId,
            in GameplayTriggerReferenceOccurrence occurrence)
        {
            ActivationId = activationId;
            DefinitionId = definitionId;
            BindingId = bindingId;
            RouteId = routeId;
            Occurrence = occurrence;
        }

        public long ActivationId { get; }
        public GameplayTriggerDefinitionId DefinitionId { get; }
        public GameplayTriggerBindingId BindingId { get; }
        public GameplayTriggerRouteId RouteId { get; }
        public GameplayTriggerReferenceOccurrence Occurrence { get; }
    }

    internal readonly struct GameplayTriggerAuthorityReferenceIntent
    {
        public GameplayTriggerAuthorityReferenceIntent(
            long activationId,
            GameplayTriggerBindingId bindingId,
            GameplayEntityId owner,
            GameplayTriggerTargetRef target,
            GameplayTriggerRouteId routeId,
            int actionIndex,
            in GameplayTriggerReferenceOccurrence occurrence,
            in GameplayTriggerActionInstruction instruction)
        {
            ActivationId = activationId;
            BindingId = bindingId;
            Owner = owner;
            Target = target;
            RouteId = routeId;
            ActionIndex = actionIndex;
            Occurrence = occurrence;
            Instruction = instruction;
        }

        public long ActivationId { get; }
        public GameplayTriggerBindingId BindingId { get; }
        public GameplayEntityId Owner { get; }
        public GameplayTriggerTargetRef Target { get; }
        public GameplayTriggerRouteId RouteId { get; }
        public int ActionIndex { get; }
        public GameplayTriggerReferenceOccurrence Occurrence { get; }
        public GameplayTriggerActionInstruction Instruction { get; }
    }

    internal sealed class GameplayTriggerRuntimeAuthorityProjection
    {
        private readonly List<GameplayTriggerInstanceAuthorityState> _instances =
            new List<GameplayTriggerInstanceAuthorityState>();
        private readonly List<GameplayTriggerRelationAuthorityState> _relations =
            new List<GameplayTriggerRelationAuthorityState>();
        private readonly List<GameplayTriggerRouteAuthorityState> _routeStates =
            new List<GameplayTriggerRouteAuthorityState>();
        private readonly List<GameplayTriggerOccurrence> _occurrences =
            new List<GameplayTriggerOccurrence>();
        private readonly List<GameplayTriggerActivation> _activations =
            new List<GameplayTriggerActivation>();
        private readonly List<GameplayTriggerActionIntent> _intents =
            new List<GameplayTriggerActionIntent>();

        public IReadOnlyList<GameplayTriggerInstanceAuthorityState> Instances => _instances;
        public IReadOnlyList<GameplayTriggerRelationAuthorityState> Relations => _relations;
        public IReadOnlyList<GameplayTriggerRouteAuthorityState> RouteStates => _routeStates;
        public IReadOnlyList<GameplayTriggerOccurrence> Occurrences => _occurrences;
        public IReadOnlyList<GameplayTriggerActivation> Activations => _activations;
        public IReadOnlyList<GameplayTriggerActionIntent> Intents => _intents;
        public long NextActivationId { get; private set; }

        public void Capture(GameplayTriggerRuntime runtime)
        {
            _instances.Clear();
            _relations.Clear();
            _routeStates.Clear();
            _occurrences.Clear();
            _activations.Clear();
            _intents.Clear();

            for (int i = 0; i < runtime.InstanceCount; i++)
                _instances.Add(runtime.GetInstanceAuthorityState(i));
            for (int i = 0; i < runtime.RelationCount; i++)
                _relations.Add(runtime.GetRelationAuthorityState(i));
            for (int i = 0; i < runtime.RouteStateCount; i++)
                _routeStates.Add(runtime.GetRouteAuthorityState(i));
            for (int i = 0; i < runtime.OccurrenceCount; i++)
                _occurrences.Add(runtime.GetOccurrence(i));
            for (int i = 0; i < runtime.ActivationCount; i++)
                _activations.Add(runtime.GetActivation(i));
            for (int i = 0; i < runtime.IntentCount; i++)
                _intents.Add(runtime.GetIntent(i));

            CanonicalizeInstances(_instances);
            CanonicalizeRelations(_relations);
            CanonicalizeRouteStates(_routeStates);
            NextActivationId = runtime.NextActivationId;
        }

        internal static void CanonicalizeInstances(
            List<GameplayTriggerInstanceAuthorityState> states) =>
            states.Sort(CompareInstances);

        internal static void CanonicalizeRelations(
            List<GameplayTriggerRelationAuthorityState> states) =>
            states.Sort(CompareRelations);

        internal static void CanonicalizeRouteStates(
            List<GameplayTriggerRouteAuthorityState> states) =>
            states.Sort(CompareRouteStates);

        private static int CompareInstances(
            GameplayTriggerInstanceAuthorityState left,
            GameplayTriggerInstanceAuthorityState right) =>
            left.InstanceId.CompareTo(right.InstanceId);

        private static int CompareRelations(
            GameplayTriggerRelationAuthorityState left,
            GameplayTriggerRelationAuthorityState right)
        {
            int result = left.InstanceId.CompareTo(right.InstanceId);
            return result != 0 ? result : left.Subject.CompareTo(right.Subject);
        }

        private static int CompareRouteStates(
            GameplayTriggerRouteAuthorityState left,
            GameplayTriggerRouteAuthorityState right)
        {
            int result = left.InstanceId.CompareTo(right.InstanceId);
            if (result != 0) return result;
            result = left.RouteId.CompareTo(right.RouteId);
            if (result != 0) return result;
            result = left.Subject.CompareTo(right.Subject);
            return result != 0 ? result : left.Instigator.CompareTo(right.Instigator);
        }
    }
}
