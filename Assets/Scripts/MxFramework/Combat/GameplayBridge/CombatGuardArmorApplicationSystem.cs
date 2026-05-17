using System;
using System.Collections.Generic;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Combat.GameplayBridge
{
    public sealed class CombatGuardArmorApplicationSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.bridge.combat.guard_armor_application";

        private readonly CombatEntityGameplayMap _entityMap;
        private readonly GameplayComponentWorld _componentWorld;
        private readonly GameplayGuardPressureSystem _guardPressureSystem;
        private readonly Func<IReadOnlyList<HitResolveResult>> _getHitResults;
        private readonly int _hpAttributeId;
        private readonly List<RuntimeCommand> _outputCommands;
        private readonly Action<ArmorBreakEvent> _publishArmorBreakEvent;

        public CombatGuardArmorApplicationSystem(
            CombatEntityGameplayMap entityMap,
            GameplayComponentWorld componentWorld,
            GameplayGuardPressureSystem guardPressureSystem,
            Func<IReadOnlyList<HitResolveResult>> getHitResults,
            int hpAttributeId,
            List<RuntimeCommand> outputCommands,
            Action<ArmorBreakEvent> publishArmorBreakEvent = null,
            string systemId = DefaultSystemId,
            GameplaySystemPhase phase = GameplaySystemPhase.Resolution,
            int priority = 60)
        {
            _entityMap = entityMap ?? throw new ArgumentNullException(nameof(entityMap));
            _componentWorld = componentWorld ?? throw new ArgumentNullException(nameof(componentWorld));
            _guardPressureSystem = guardPressureSystem;
            _getHitResults = getHitResults ?? throw new ArgumentNullException(nameof(getHitResults));
            _hpAttributeId = hpAttributeId;
            _outputCommands = outputCommands ?? throw new ArgumentNullException(nameof(outputCommands));
            _publishArmorBreakEvent = publishArmorBreakEvent;
            SystemId = systemId ?? string.Empty;
            Phase = phase;
            Priority = priority;
        }

        public string SystemId { get; }
        public GameplaySystemPhase Phase { get; }
        public int Priority { get; }
        public bool IsEnabled { get; private set; } = true;

        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
        }

        public void Tick(GameplaySystemContext context)
        {
            if (!IsEnabled)
                return;

            IReadOnlyList<HitResolveResult> results = _getHitResults();
            if (results == null || results.Count == 0)
                return;

            for (int i = 0; i < results.Count; i++)
            {
                HitResolveResult result = results[i];
                if (!_entityMap.TryGetGameplayId(result.TargetId, out GameplayEntityId entityId))
                    continue;

                if (result.Kind == HitResolveKind.Blocked)
                {
                    EnqueueGuardPressure(entityId, result);
                    continue;
                }

                if (result.IsAcceptedDamage)
                {
                    int adjustedDamage = ApplyArmorIntegrity(context.Frame, entityId, result);
                    AppendHpDamageCommand(context, entityId, adjustedDamage, result.TraceId);
                }
            }
        }

        private void EnqueueGuardPressure(GameplayEntityId entityId, HitResolveResult result)
        {
            if (_guardPressureSystem == null || result.Damage <= 0)
                return;

            _guardPressureSystem.Enqueue(new GameplayGuardPressureRequest(
                entityId,
                result.Damage,
                true,
                result.TraceId.ToString()));
        }

        private int ApplyArmorIntegrity(RuntimeFrame frame, GameplayEntityId entityId, HitResolveResult result)
        {
            int incomingDamage = result.Damage;
            if (incomingDamage <= 0)
                return 0;

            if (!_componentWorld.TryGetStore(out GameplayComponentStore<GameplayArmorIntegrityComponent> armorStore) ||
                !armorStore.TryGet(entityId, out GameplayArmorIntegrityComponent armor) ||
                !HasUsableArmor(armor))
            {
                return incomingDamage;
            }

            int previousIntegrity = armor.CurrentIntegrity;
            long absorbed = (long)incomingDamage * previousIntegrity / armor.MaxIntegrity;
            int adjustedDamage = incomingDamage - (int)absorbed;
            armor.CurrentIntegrity = Math.Max(0, previousIntegrity - incomingDamage);
            bool brokeThisHit = previousIntegrity > 0 && armor.CurrentIntegrity == 0 && !armor.IsBroken;
            if (brokeThisHit)
                armor.IsBroken = true;

            armorStore.Set(entityId, armor);

            if (brokeThisHit)
                PublishArmorBreak(frame, entityId, previousIntegrity, armor, incomingDamage, result.TraceId);

            return adjustedDamage;
        }

        private void AppendHpDamageCommand(
            GameplaySystemContext context,
            GameplayEntityId entityId,
            int adjustedDamage,
            int traceId)
        {
            if (adjustedDamage <= 0)
                return;

            RuntimeCommand command = GameplayRuntimeCommandFactory.AddComponentAttribute(
                context.Frame,
                entityId,
                _hpAttributeId,
                -adjustedDamage,
                sourceId: 0,
                traceId: traceId.ToString());
            _outputCommands.Add(command);
        }

        private void PublishArmorBreak(
            RuntimeFrame frame,
            GameplayEntityId entityId,
            int previousIntegrity,
            GameplayArmorIntegrityComponent armor,
            int incomingDamage,
            int traceId)
        {
            if (_publishArmorBreakEvent == null)
                return;

            _publishArmorBreakEvent(new ArmorBreakEvent(
                frame,
                entityId,
                previousIntegrity,
                armor.CurrentIntegrity,
                armor.MaxIntegrity,
                incomingDamage,
                traceId.ToString()));
        }

        private static bool HasUsableArmor(GameplayArmorIntegrityComponent armor)
        {
            return armor.MaxIntegrity > 0
                && armor.CurrentIntegrity > 0
                && armor.CurrentIntegrity <= armor.MaxIntegrity
                && !armor.IsBroken;
        }
    }
}
