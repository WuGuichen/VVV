using System;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Resolves the current entity and providers again after equipment changes, reload or world restore.</summary>
    /// <remarks>LowFreqAlloc. Intended for compile/start/restore boundaries, never for per-frame condition sampling.</remarks>
    internal sealed class CharacterTestTacticalCardRuntimeInputs : ICharacterTestTacticalCardInputSource
    {
        private readonly CharacterTestCharacterRuntimeModule _runtime;
        private readonly int _entityId;
        private readonly CharacterActionTacticalMetricsStore _store;
        private readonly CharacterActionSpatialMeasurement _measurement;

        internal CharacterTestTacticalCardRuntimeInputs(CharacterTestCharacterRuntimeModule runtime, int entityId,
            CharacterActionTacticalMetricsStore store, CharacterActionSpatialMeasurement measurement)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            if (entityId <= 0) throw new ArgumentOutOfRangeException(nameof(entityId));
            _entityId = entityId;
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _measurement = measurement ?? throw new ArgumentNullException(nameof(measurement));
        }

        public bool TryPrepare(out CharacterTestTacticalCardInputs inputs, out string issue)
            => TryPrepare(0, out inputs, out issue);

        public bool TryPrepare(int actionId, out CharacterTestTacticalCardInputs inputs, out string issue)
        {
            inputs = null;
            CharacterTestCharacterEntity source = null;
            for (int i = 0; i < _runtime.Entities.Count; i++)
            {
                if (_runtime.Entities[i].Id.Value != _entityId) continue;
                source = _runtime.Entities[i];
                break;
            }
            if (source == null)
            {
                issue = "binding:SourceEntityUnavailable";
                return false;
            }
            if (!CharacterTestTacticalCardInputs.TryPrepare(_runtime.ActionRuntime, source,
                _runtime.PhysicsRuntime, _runtime.CombatTraceSystem.BakedTraceSource,
                _runtime.CombatTraceSystem.WeaponTraceBindingResolver, _store, _measurement, out inputs, out issue,
                testActionId: actionId)) return false;
            inputs = inputs.WithSimulationTickRate(_runtime.SimulationTickRate); return true;
        }
    }
}
