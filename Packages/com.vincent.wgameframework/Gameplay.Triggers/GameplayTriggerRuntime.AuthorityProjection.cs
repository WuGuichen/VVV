using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Gameplay.Triggers
{
    internal readonly struct GameplayTriggerInstanceAuthorityState
    {
        public GameplayTriggerInstanceAuthorityState(
            GameplayTriggerInstanceId instanceId,
            GameplayTriggerBindingId bindingId,
            GameplayTriggerSourceBindingId sourceBindingId,
            GameplayTriggerDefinitionId definitionId,
            int definitionVersion,
            GameplayTriggerSourceDefinitionId sourceDefinitionId,
            GameplayEntityId owner,
            bool enabled,
            int activeCount,
            long aggregatePresenceEpoch)
        {
            InstanceId = instanceId;
            BindingId = bindingId;
            SourceBindingId = sourceBindingId;
            DefinitionId = definitionId;
            DefinitionVersion = definitionVersion;
            SourceDefinitionId = sourceDefinitionId;
            Owner = owner;
            Enabled = enabled;
            ActiveCount = activeCount;
            AggregatePresenceEpoch = aggregatePresenceEpoch;
        }

        public GameplayTriggerInstanceId InstanceId { get; }
        public GameplayTriggerBindingId BindingId { get; }
        public GameplayTriggerSourceBindingId SourceBindingId { get; }
        public GameplayTriggerDefinitionId DefinitionId { get; }
        public int DefinitionVersion { get; }
        public GameplayTriggerSourceDefinitionId SourceDefinitionId { get; }
        public GameplayEntityId Owner { get; }
        public bool Enabled { get; }
        public int ActiveCount { get; }
        public long AggregatePresenceEpoch { get; }
    }

    internal readonly struct GameplayTriggerRelationAuthorityState
    {
        public GameplayTriggerRelationAuthorityState(
            GameplayTriggerInstanceId instanceId,
            GameplayEntityId subject,
            bool active,
            bool primed,
            Fix64 scalar,
            long presenceEpoch,
            long presenceStartFrame)
        {
            InstanceId = instanceId;
            Subject = subject;
            Active = active;
            Primed = primed;
            Scalar = scalar;
            PresenceEpoch = presenceEpoch;
            PresenceStartFrame = presenceStartFrame;
        }

        public GameplayTriggerInstanceId InstanceId { get; }
        public GameplayEntityId Subject { get; }
        public bool Active { get; }
        public bool Primed { get; }
        public Fix64 Scalar { get; }
        public long PresenceEpoch { get; }
        public long PresenceStartFrame { get; }
    }

    internal readonly struct GameplayTriggerRouteAuthorityState
    {
        public GameplayTriggerRouteAuthorityState(
            GameplayTriggerInstanceId instanceId,
            GameplayTriggerRouteId routeId,
            GameplayEntityId subject,
            GameplayEntityId instigator,
            int activationCount,
            bool hasActivated,
            long lastActivationFrame,
            long lastActivatedPresenceEpoch,
            bool hasStaySchedule,
            long nextStayFrame)
        {
            InstanceId = instanceId;
            RouteId = routeId;
            Subject = subject;
            Instigator = instigator;
            ActivationCount = activationCount;
            HasActivated = hasActivated;
            LastActivationFrame = lastActivationFrame;
            LastActivatedPresenceEpoch = lastActivatedPresenceEpoch;
            HasStaySchedule = hasStaySchedule;
            NextStayFrame = nextStayFrame;
        }

        public GameplayTriggerInstanceId InstanceId { get; }
        public GameplayTriggerRouteId RouteId { get; }
        public GameplayEntityId Subject { get; }
        public GameplayEntityId Instigator { get; }
        public int ActivationCount { get; }
        public bool HasActivated { get; }
        public long LastActivationFrame { get; }
        public long LastActivatedPresenceEpoch { get; }
        public bool HasStaySchedule { get; }
        public long NextStayFrame { get; }
    }

    public sealed partial class GameplayTriggerRuntime
    {
        /// <summary>Gets the immutable gameplay trigger catalog identity used by this runtime.</summary>
        /// <remarks>NoAlloc.</remarks>
        public long GameplayTriggerCatalogHash => _catalog.GameplayTriggerCatalogHash;

        /// <summary>Reads whether the source binding is currently enabled.</summary>
        /// <remarks>NoAlloc. Unknown source bindings return false and set <paramref name="enabled"/> to false.</remarks>
        public bool TryGetSourceEnabled(
            GameplayTriggerSourceBindingId sourceBindingId,
            out bool enabled)
        {
            for (int i = 0; i < _instances.Length; i++)
            {
                InstanceState state = _instances[i];
                GameplayTriggerBinding binding = _catalog.GetBinding(state.BindingIndex);
                if (binding.SourceBindingId != sourceBindingId)
                    continue;

                enabled = state.Enabled;
                return true;
            }

            enabled = false;
            return false;
        }

        /// <remarks>NoAlloc. Internal immutable authority read view for hash, persistence, and focused tests.</remarks>
        internal GameplayTriggerInstanceAuthorityState GetInstanceAuthorityState(int index)
        {
            InstanceState state = _instances[index];
            GameplayTriggerBinding binding = _catalog.GetBinding(state.BindingIndex);
            GameplayTriggerDefinition definition = _catalog.GetDefinition(state.DefinitionIndex);
            return new GameplayTriggerInstanceAuthorityState(
                state.InstanceId,
                binding.BindingId,
                binding.SourceBindingId,
                definition.DefinitionId,
                definition.DefinitionVersion,
                definition.SourceDefinitionId,
                binding.Owner,
                state.Enabled,
                state.ActiveCount,
                state.AggregatePresenceEpoch);
        }

        /// <remarks>NoAlloc. Internal immutable authority read view for hash, persistence, and focused tests.</remarks>
        internal GameplayTriggerRelationAuthorityState GetRelationAuthorityState(int index)
        {
            RelationState state = _relations[index];
            return new GameplayTriggerRelationAuthorityState(
                _instances[state.InstanceIndex].InstanceId,
                state.Subject,
                state.Active,
                state.Primed,
                state.Scalar,
                state.PresenceEpoch,
                state.PresenceStartFrame);
        }

        /// <remarks>NoAlloc. Internal immutable authority read view for hash, persistence, and focused tests.</remarks>
        internal GameplayTriggerRouteAuthorityState GetRouteAuthorityState(int index)
        {
            RouteState state = _routeStates[index];
            return new GameplayTriggerRouteAuthorityState(
                _instances[state.InstanceIndex].InstanceId,
                state.RouteId,
                state.Subject,
                state.Instigator,
                state.ActivationCount,
                state.HasActivated,
                state.LastActivationFrame,
                state.LastActivatedPresenceEpoch,
                state.HasStaySchedule,
                state.NextStayFrame);
        }

        /// <remarks>NoAlloc. Internal authority hash read view.</remarks>
        internal GameplayTriggerCatalog AuthorityCatalog => _catalog;

        /// <remarks>NoAlloc. Internal exact-composition identity.</remarks>
        internal GameplayTriggerRuntimeEventSource AuthorityEventSourceIdentity => _eventSource;

        /// <remarks>NoAlloc. Internal authority hash read view.</remarks>
        internal GameplayTriggerRuntimeOptions AuthorityOptions => _options;

        /// <remarks>NoAlloc. Internal authority hash read view.</remarks>
        internal bool AuthorityHasEvaluated => _hasEvaluated;

        /// <remarks>NoAlloc. Internal authority hash read view.</remarks>
        internal RuntimeFrame AuthorityLastEvaluatedFrame => _lastEvaluatedFrame;

        /// <remarks>NoAlloc. Internal authority hash read view.</remarks>
        internal int RelationCapacity => _relations.Length;

        /// <remarks>NoAlloc. Internal authority hash read view.</remarks>
        internal int RouteStateCapacity => _routeStates.Length;

        /// <remarks>NoAlloc. Internal replay admission read seam.</remarks>
        internal bool TryGetSourceKind(
            GameplayTriggerSourceBindingId sourceBindingId,
            out GameplayTriggerSourceKind sourceKind)
        {
            GameplayTriggerCatalog catalog = _catalog;
            for (int bindingIndex = 0; bindingIndex < catalog.BindingCount; bindingIndex++)
            {
                GameplayTriggerBinding binding = catalog.GetBinding(bindingIndex);
                if (binding.SourceBindingId != sourceBindingId)
                    continue;

                int definitionIndex = catalog.FindDefinition(binding.DefinitionId);
                GameplayTriggerDefinition definition = catalog.GetDefinition(definitionIndex);
                int sourceIndex = catalog.FindSource(definition.SourceDefinitionId);
                sourceKind = catalog.GetSource(sourceIndex).SourceKind;
                return true;
            }

            sourceKind = default;
            return false;
        }
    }
}
