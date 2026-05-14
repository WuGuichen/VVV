using System;
using System.Collections.Generic;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Combat.GameplayBridge
{
    public sealed class CombatHitApplicationSystem : IGameplaySystem
    {
        public const string DefaultSystemId = "mxframework.bridge.combat.hit_application";

        private readonly CombatEntityGameplayMap _entityMap;
        private readonly Func<IReadOnlyList<HitResolveResult>> _getHitResults;
        private readonly int _hpAttributeId;
        private readonly List<RuntimeCommand> _outputCommands;

        public CombatHitApplicationSystem(
            CombatEntityGameplayMap entityMap,
            GameplayComponentWorld componentWorld,
            Func<IReadOnlyList<HitResolveResult>> getHitResults,
            int hpAttributeId,
            List<RuntimeCommand> outputCommands,
            string systemId = DefaultSystemId,
            GameplaySystemPhase phase = GameplaySystemPhase.Resolution,
            int priority = 80)
        {
            _entityMap = entityMap ?? throw new ArgumentNullException(nameof(entityMap));
            if (componentWorld == null)
            {
                throw new ArgumentNullException(nameof(componentWorld));
            }

            _getHitResults = getHitResults ?? throw new ArgumentNullException(nameof(getHitResults));
            _hpAttributeId = hpAttributeId;
            _outputCommands = outputCommands ?? throw new ArgumentNullException(nameof(outputCommands));
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
                if (!result.IsAcceptedDamage)
                    continue;

                if (!_entityMap.TryGetGameplayId(result.TargetId, out GameplayEntityId entityId))
                    continue;

                RuntimeCommand command = GameplayRuntimeCommandFactory.AddComponentAttribute(
                    context.Frame,
                    entityId,
                    _hpAttributeId,
                    -result.Damage,
                    sourceId: 0,
                    traceId: result.TraceId.ToString());
                _outputCommands.Add(command);
            }
        }
    }
}
