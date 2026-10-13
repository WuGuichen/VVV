using System;
using System.Collections.Generic;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Prepares only explicitly owned and bound actions. Input command mappings never choose a slot value.</summary>
    internal sealed class CharacterTacticalBoundCardInputs : ICharacterTestTacticalCardInputSource
    {
        private readonly CharacterTacticalPlayerService _player;
        private readonly CharacterTestCharacterRuntimeModule _runtime;
        private readonly int _entityId;
        private readonly string _character, _card;
        private readonly IReadOnlyDictionary<string, int> _confirmedBindings;
        private readonly CharacterActionTacticalMetricsStore _metrics = new CharacterActionTacticalMetricsStore();
        private bool _functionalResolvers;
        private CharacterTestTacticalInputRequirements _requirements = CharacterTestTacticalInputRequirements.PrimaryAction
            | CharacterTestTacticalInputRequirements.SpatialSummary | CharacterTestTacticalInputRequirements.FunctionalActions;
        internal CharacterTacticalBoundCardInputs(CharacterTacticalPlayerService player, CharacterTestCharacterRuntimeModule runtime,
            int entityId, string character, string card)
        { _player = player; _runtime = runtime; _entityId = entityId; _character = character; _card = card; }

        internal CharacterTacticalBoundCardInputs(CharacterTestCharacterRuntimeModule runtime, int entityId,
            IReadOnlyDictionary<string, int> confirmedBindings)
        { _runtime = runtime; _entityId = entityId; _confirmedBindings = confirmedBindings ?? throw new ArgumentNullException(nameof(confirmedBindings)); }

        public bool TryPrepare(int actionId, out CharacterTestTacticalCardInputs inputs, out string issue)
            => TryPrepare(out inputs, out issue);

        internal CharacterTacticalBoundCardInputs WithRequirements(CharacterTestTacticalInputRequirements requirements)
        { _requirements = requirements; return this; }

        internal CharacterTacticalBoundCardInputs UseFunctionalResolvers()
        {
            _functionalResolvers = true;
            return this;
        }

        internal bool TryPrepareFunctional(out CharacterTestTacticalCardInputs inputs, out string issue)
        {
            inputs = null;
            var entity = _runtime.FindEntityById(_entityId);
            if (entity == null) { issue = "binding:SourceEntityUnavailable"; return false; }
            return TryPrepareBoundAction(entity, CharacterTestTacticalSummaryPreparation.StandardMeasurement(), 0,
                _requirements == CharacterTestTacticalInputRequirements.None ? CharacterTestTacticalInputRequirements.None
                    : CharacterTestTacticalInputRequirements.FunctionalActions, out inputs, out issue);
        }

        private bool _allowSummaryRepair = true;
        /// <summary>Presentation preparation reads existing summaries only; never creates probes or rebuilds data.</summary>
        internal bool TryPrepareReadOnly(out CharacterTestTacticalCardInputs inputs, out string issue)
        {
            bool previous = _allowSummaryRepair; _allowSummaryRepair = false;
            try { return TryPrepare(out inputs, out issue); }
            finally { _allowSummaryRepair = previous; }
        }
        public bool TryPrepare(out CharacterTestTacticalCardInputs inputs, out string issue)
        {
            if (_functionalResolvers) return TryPrepareFunctional(out inputs, out issue);
            inputs = null;
            issue = string.Empty;
            IReadOnlyDictionary<string, int> bindings = _confirmedBindings;
            if (bindings == null)
            {
                if (!_player.TryGetBindings(_character, _card, out var owned, out issue)) return false;
                var selected = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var pair in owned) selected.Add(pair.Key, pair.Value.ActionId);
                bindings = selected;
            }
            var entity = _runtime.FindEntityById(_entityId);
            if (entity == null) { issue = "binding:SourceEntityUnavailable"; return false; }
            var measurement = CharacterTestTacticalSummaryPreparation.StandardMeasurement();
            var prepared = new Dictionary<string, CharacterTestTacticalCardInputs>(StringComparer.Ordinal);
            foreach (var pair in bindings)
            {
                if (!TryPrepareBoundAction(entity, measurement, pair.Value,
                    _requirements | CharacterTestTacticalInputRequirements.PrimaryAction,
                    out var value, out issue)) return false;
                prepared.Add(pair.Key, value);
            }
            inputs = CharacterTestTacticalCardInputs.BindSlots(entity.Identity.CharacterId, prepared).WithSimulationTickRate(_runtime.SimulationTickRate);
            return true;
        }

        /// <remarks>
        /// LowFreqAlloc. A card confirmation is a paused preparation boundary, so a missing or stale derived
        /// measurement may be rebuilt transactionally before any command, payment or world step is committed.
        /// </remarks>
        private bool TryPrepareBoundAction(CharacterTestCharacterEntity entity, CharacterActionSpatialMeasurement measurement,
            int actionId, CharacterTestTacticalInputRequirements requirements,
            out CharacterTestTacticalCardInputs inputs, out string issue)
        {
            if (actionId > 0 && _runtime.ActionRuntime.Registry.TryGetAction(new CharacterActionId(actionId), out var measuredAction)
                && CharacterActionProjectileReference.IsCandidate(measuredAction))
                measurement = CharacterActionProjectileReference.Measurement();
            if (TryPrepareBoundActionOnce(entity, measurement, actionId, requirements, out inputs, out issue)) return true;
            string originalIssue = issue;
            if (!_allowSummaryRepair || !IsRepairableSummaryIssue(originalIssue)) return false;
            if (!CharacterTestTacticalSummaryPreparation.TryDescribe(_runtime, entity, measurement,
                out var request, out string preparationIssue, actionId))
            {
                issue = originalIssue + "; auto-repair prepare failed: " + preparationIssue;
                return false;
            }
            if (!CharacterTestTacticalSummaryPreparation.TryRebuild(request, _metrics, out _, out string rebuildIssue))
            {
                issue = originalIssue + "; auto-repair failed: " + rebuildIssue;
                return false;
            }
            if (TryPrepareBoundActionOnce(entity, measurement, actionId, requirements, out inputs, out issue)) return true;
            issue = originalIssue + "; auto-repair retry failed: " + issue;
            return false;
        }

        private bool TryPrepareBoundActionOnce(CharacterTestCharacterEntity entity, CharacterActionSpatialMeasurement measurement,
            int actionId, CharacterTestTacticalInputRequirements requirements,
            out CharacterTestTacticalCardInputs inputs, out string issue)
        {
            if (!CharacterTestTacticalCardInputs.TryPrepare(_runtime.ActionRuntime, entity, _runtime.PhysicsRuntime,
                _runtime.CombatTraceSystem.BakedTraceSource, _runtime.CombatTraceSystem.WeaponTraceBindingResolver,
                _metrics, measurement, out inputs, out issue, actionId, requirements, _runtime.CombatTraceSystem.ReferenceProjectiles)) return false;
            inputs = inputs.WithSimulationTickRate(_runtime.SimulationTickRate); return true;
        }

        private static bool IsRepairableSummaryIssue(string issue)
            => CharacterTestTacticalSummaryPreparation.NeedsRebuild(issue);
    }
}
