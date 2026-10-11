using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestTacticalCardInputs
    {
        private Dictionary<string, CharacterTestTacticalCardInputs> _slotInputs;
        internal bool HasExplicitBindings => _slotInputs != null;

        // Cold capture of configured slots, including distance-only and currently unreachable dependencies.
        // Functional fallback actions are not user-configured slots and retain their existing execution checks.
        internal int[] CaptureConfiguredActionIds()
        {
            var ids = new SortedSet<int>();
            if (_slotInputs != null)
            {
                foreach (var pair in _slotInputs) ids.Add(pair.Value.Action.ActionId.Value);
            }
            else if (Action != null) ids.Add(Action.ActionId.Value);
            return ids.ToArray();
        }

        internal bool TryReprepareExplicit(CharacterTestCharacterRuntimeModule runtime, CharacterTestCharacterEntity entity,
            out CharacterTestTacticalCardInputs inputs, out string issue)
        {
            inputs = null; issue = string.Empty;
            if (_slotInputs == null) { issue = "Explicit slot bindings are missing."; return false; }
            var prepared = new Dictionary<string, CharacterTestTacticalCardInputs>(StringComparer.Ordinal);
            var store = new CharacterActionTacticalMetricsStore();
            foreach (var pair in _slotInputs)
            {
                if (!TryPrepare(runtime.ActionRuntime, entity, runtime.PhysicsRuntime, runtime.CombatTraceSystem.BakedTraceSource,
                    runtime.CombatTraceSystem.WeaponTraceBindingResolver, store, CharacterTestTacticalSummaryPreparation.StandardMeasurement(),
                    out var value, out issue, pair.Value.Action.ActionId.Value,
                    pair.Value.Requirements)) return false;
                prepared.Add(pair.Key, value);
            }
            if (!TryPrepare(runtime.ActionRuntime, entity, runtime.PhysicsRuntime, runtime.CombatTraceSystem.BakedTraceSource,
                runtime.CombatTraceSystem.WeaponTraceBindingResolver, store, CharacterTestTacticalSummaryPreparation.StandardMeasurement(),
                out var functional, out issue, 0, CharacterTestTacticalInputRequirements.FunctionalActions)) return false;
            inputs = BindSlots(entity.Identity.CharacterId, prepared, functional.StepBackAction, functional.StepBackContentHash,
                functional.StepForwardAction, functional.StepForwardContentHash).WithSimulationTickRate(runtime.SimulationTickRate); return true;
        }

        internal bool ValidateExplicit(CharacterTestCharacterRuntimeModule runtime, CharacterTestCharacterEntity entity, out string issue)
        {
            issue = string.Empty;
            if (StepForwardAction != null)
            {
                if (!TryResolveStepForward(runtime.ActionRuntime.Registry.Actions, out var forward)
                    || !CharacterActionContentHashUtility.TryCompute(forward, entity, runtime.PhysicsRuntime,
                        runtime.CombatTraceSystem.BakedTraceSource, runtime.CombatTraceSystem.WeaponTraceBindingResolver, out string forwardHash, out issue)) return false;
                if (forwardHash != StepForwardContentHash) { issue = "ActionSlot: 前向垫步动作已变化。"; return false; }
            }
            if (_slotInputs == null) return true;
            runtime.ActionRuntime.TryGetEntityActionSet(entity.Id, out var set);
            foreach (var pair in _slotInputs)
            {
                var prepared = pair.Value;
                if (!TryResolveSelectedAction(set, runtime.ActionRuntime.Registry.Actions, prepared.Action.ActionId.Value, out var action, out issue)
                    || !CharacterActionContentHashUtility.TryCompute(action, entity, runtime.PhysicsRuntime,
                        runtime.CombatTraceSystem.BakedTraceSource, runtime.CombatTraceSystem.WeaponTraceBindingResolver, out string hash, out issue)) return false;
                if (hash != prepared.Metrics.ActionContentHash)
                { issue = "ActionSlot: 动作或几何数据已变化 " + pair.Key; return false; }
            }
            return true;
        }

        internal static CharacterTestTacticalCardInputs BindSlots(int characterId,
            IReadOnlyDictionary<string, CharacterTestTacticalCardInputs> bindings,
            CharacterActionConfig stepBack = null, string stepBackHash = null,
            CharacterActionConfig stepForward = null, string stepForwardHash = null)
        {
            var owned = new Dictionary<string, CharacterTestTacticalCardInputs>(StringComparer.Ordinal);
            var identity = new StringBuilder("tactical-explicit-slots-v1|").Append(characterId);
            foreach (var pair in bindings.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value?.Action == null || pair.Value.HasExplicitBindings
                    || pair.Value.SourceCharacterId != characterId)
                    throw new ArgumentException("Invalid explicit tactical slot binding.");
                owned.Add(pair.Key, pair.Value);
                if (stepForward == null && pair.Value.StepForwardAction != null)
                { stepForward = pair.Value.StepForwardAction; stepForwardHash = pair.Value.StepForwardContentHash; }
                if (stepBack == null && pair.Value.StepBackAction != null)
                {
                    stepBack = pair.Value.StepBackAction;
                    stepBackHash = pair.Value.StepBackContentHash;
                }
                identity.Append('|').Append(pair.Key.Length).Append(':').Append(pair.Key).Append(':').Append(pair.Value.DependencyFingerprint);
            }
            string hash;
            if (stepForward != null) identity.Append("|stepForward:").Append(stepForward.ActionId.Value).Append(':').Append(stepForwardHash);
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity.ToString()))).Replace("-", string.Empty);
            var result = new CharacterTestTacticalCardInputs(null, null, hash, string.Empty, characterId,
                stepBack: stepBack, stepBackHash: stepBackHash,
                requirements: CharacterTestTacticalInputRequirements.None);
            result._slotInputs = owned;
            result.SetStepForward(stepForward, stepForwardHash);
            return result;
        }

        internal CharacterTestTacticalCardInputs SlotInputs(string path)
        {
            if (_slotInputs == null) return this;
            if (_slotInputs.TryGetValue(path ?? string.Empty, out var value)) return value;
            throw new InvalidOperationException("ActionSlot: 未装配槽位 " + path);
        }

        internal CharacterTestTacticalActionRange SlotRange(string path)
        {
            var summary = SlotInputs(path).SelectedRange;
            return summary ?? throw new InvalidOperationException("ActionSlot: 动作缺少所需空间摘要 " + path);
        }

        internal CharacterTestTacticalCardInputs ForInstance(string nodeId)
        {
            if (_slotInputs == null) return this;
            string prefix = nodeId + "/";
            var selected = new Dictionary<string, CharacterTestTacticalCardInputs>(StringComparer.Ordinal);
            foreach (var pair in _slotInputs)
                if (pair.Key.StartsWith(prefix, StringComparison.Ordinal)) selected.Add(pair.Key.Substring(prefix.Length), pair.Value);
            return BindSlots(SourceCharacterId, selected, StepBackAction, StepBackContentHash, StepForwardAction, StepForwardContentHash).WithSimulationTickRate(SimulationTickRate);
        }
    }
}
