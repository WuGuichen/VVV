using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Demo.CombatAnimation
{
    public sealed class CombatRuntimeDiagnosticHudPresenter
    {
        private static readonly string[] NoRows = Array.Empty<string>();

        private readonly List<string> _rows = new List<string>();

        public CombatRuntimeDiagnosticHudModel Build(
            RuntimeFrame frame,
            GameplayComponentWorld componentWorld,
            CombatEntityGameplayMap entityMap,
            IReadOnlyList<RuntimeCommand> hitApplicationCommands,
            IReadOnlyList<HitResolveResult> hitResults,
            IReadOnlyList<IRuntimeHashContributor> hashContributors)
        {
            var model = new CombatRuntimeDiagnosticHudModel();

            model.ActionStateRows = BuildActionStateRows(componentWorld);
            model.HitApplicationRows = BuildHitApplicationRows(hitApplicationCommands, hitResults);
            model.GameplayAttributeRows = BuildGameplayAttributeRows(componentWorld);
            model.BridgeMapRows = BuildBridgeMapRows(entityMap);
            model.RuntimeHashRows = BuildRuntimeHashRows(frame, hashContributors);
            model.EventQueueRows = BuildEventQueueRows(componentWorld);

            return model;
        }

        private IReadOnlyList<string> BuildActionStateRows(GameplayComponentWorld componentWorld)
        {
            if (componentWorld == null
                || !componentWorld.TryGetStore(out GameplayComponentStore<CombatActionStateComponent> store)
                || store.Count == 0)
            {
                return One("No combat action state components.");
            }

            GameplayComponentSnapshot<CombatActionStateComponent>[] snapshot = store.CreateSnapshot();
            _rows.Clear();
            for (int i = 0; i < snapshot.Length; i++)
            {
                CombatActionStateComponent component = snapshot[i].Component;
                string state = component.IsActive
                    ? "action=" + component.ActionId
                        + " phase=" + component.Phase
                        + " localFrame=" + component.LocalFrame
                    : "inactive";
                _rows.Add("Entity " + snapshot[i].EntityId + " " + state);
            }

            return CopyRows();
        }

        private IReadOnlyList<string> BuildHitApplicationRows(
            IReadOnlyList<RuntimeCommand> hitApplicationCommands,
            IReadOnlyList<HitResolveResult> hitResults)
        {
            if ((hitApplicationCommands == null || hitApplicationCommands.Count == 0)
                && (hitResults == null || hitResults.Count == 0))
            {
                return One("No hit application commands this frame.");
            }

            _rows.Clear();
            if (hitApplicationCommands != null)
            {
                for (int i = 0; i < hitApplicationCommands.Count; i++)
                {
                    RuntimeCommand command = hitApplicationCommands[i];
                    _rows.Add("Command frame=" + command.Frame
                        + " entity=" + command.TargetId + ":" + command.Payload0
                        + " attribute=" + command.Payload1
                        + " delta=" + command.Payload2
                        + " trace=" + EmptyToken(command.TraceId));
                }
            }

            if (_rows.Count == 0 && hitResults != null)
            {
                for (int i = 0; i < hitResults.Count; i++)
                {
                    HitResolveResult result = hitResults[i];
                    _rows.Add("Result " + result.Kind
                        + " target=" + result.TargetId.Value
                        + " damage=" + result.Damage
                        + " trace=" + result.TraceId);
                }
            }

            return _rows.Count == 0 ? One("No accepted hit application commands this frame.") : CopyRows();
        }

        private IReadOnlyList<string> BuildGameplayAttributeRows(GameplayComponentWorld componentWorld)
        {
            if (componentWorld == null
                || !componentWorld.TryGetStore(out GameplayComponentStore<GameplayAttributeSetComponent> store)
                || store.Count == 0)
            {
                return One("No gameplay attribute components.");
            }

            GameplayComponentSnapshot<GameplayAttributeSetComponent>[] snapshot = store.CreateSnapshot();
            _rows.Clear();
            for (int i = 0; i < snapshot.Length; i++)
            {
                GameplayAttributeValue[] values = snapshot[i].Component.ToArray();
                if (values.Length == 0)
                {
                    _rows.Add("Entity " + snapshot[i].EntityId + " has no attributes.");
                    continue;
                }

                for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
                {
                    _rows.Add("Entity " + snapshot[i].EntityId
                        + " attribute=" + values[valueIndex].AttributeId
                        + " current=" + values[valueIndex].CurrentValue);
                }
            }

            return CopyRows();
        }

        private IReadOnlyList<string> BuildBridgeMapRows(CombatEntityGameplayMap entityMap)
        {
            if (entityMap == null || entityMap.Count == 0)
            {
                return One("No combat/gameplay bridge mappings.");
            }

            CombatEntityId[] combatIds = entityMap.CreateCombatIdSnapshot();
            _rows.Clear();
            for (int i = 0; i < combatIds.Length; i++)
            {
                if (entityMap.TryGetGameplayId(combatIds[i], out GameplayEntityId gameplayId))
                {
                    _rows.Add("Combat " + combatIds[i].Value + " <-> Gameplay " + gameplayId);
                }
            }

            return _rows.Count == 0 ? One("No combat/gameplay bridge mappings.") : CopyRows();
        }

        private IReadOnlyList<string> BuildRuntimeHashRows(
            RuntimeFrame frame,
            IReadOnlyList<IRuntimeHashContributor> hashContributors)
        {
            if (hashContributors == null || hashContributors.Count == 0)
            {
                return One("Demo diagnostic hash unavailable: no contributors.");
            }

            try
            {
                long hash = RuntimeHashCombiner.ComputeHash(frame, hashContributors);
                _rows.Clear();
                _rows.Add("Demo diagnostic hash frame=" + frame + " value=" + hash);
                _rows.Add("Contributors: " + FormatContributorIds(hashContributors));
                return CopyRows();
            }
            catch (Exception exception)
            {
                return One("Demo diagnostic hash unavailable: " + exception.GetType().Name);
            }
        }

        private static IReadOnlyList<string> BuildEventQueueRows(GameplayComponentWorld componentWorld)
        {
            if (componentWorld == null)
            {
                return One("Event queue unavailable.");
            }

            RuntimeEventQueueSnapshot snapshot = componentWorld.Events.CreateSnapshot();
            if (!snapshot.HasPending)
            {
                return One("Pending=0 type=" + snapshot.EventTypeName + " nextSequence=" + snapshot.NextSequence);
            }

            return One("Pending=" + snapshot.PendingCount
                + " frames=" + snapshot.OldestFrame + "-" + snapshot.NewestFrame
                + " type=" + snapshot.EventTypeName
                + " nextSequence=" + snapshot.NextSequence);
        }

        private static string FormatContributorIds(IReadOnlyList<IRuntimeHashContributor> hashContributors)
        {
            if (hashContributors == null || hashContributors.Count == 0)
            {
                return "-";
            }

            string result = string.Empty;
            for (int i = 0; i < hashContributors.Count; i++)
            {
                if (i > 0)
                {
                    result += ", ";
                }

                result += hashContributors[i].ContributorId;
            }

            return result;
        }

        private IReadOnlyList<string> CopyRows()
        {
            if (_rows.Count == 0)
            {
                return NoRows;
            }

            return _rows.ToArray();
        }

        private static IReadOnlyList<string> One(string value)
        {
            return new[] { value };
        }

        private static string EmptyToken(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }
    }

    public sealed class CombatRuntimeDiagnosticHudModel
    {
        public IReadOnlyList<string> ActionStateRows { get; set; }
        public IReadOnlyList<string> HitApplicationRows { get; set; }
        public IReadOnlyList<string> GameplayAttributeRows { get; set; }
        public IReadOnlyList<string> BridgeMapRows { get; set; }
        public IReadOnlyList<string> RuntimeHashRows { get; set; }
        public IReadOnlyList<string> EventQueueRows { get; set; }
    }
}
