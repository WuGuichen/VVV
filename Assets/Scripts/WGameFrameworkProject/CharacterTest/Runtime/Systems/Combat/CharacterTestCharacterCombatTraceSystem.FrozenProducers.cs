using MxFramework.Combat.Core;
using MxFramework.Combat.Hit;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterCombatTraceSystem
    {
        private readonly FrozenProducerRelations _frozenProducerRelations = new FrozenProducerRelations();
        private TacticalDoubleEdgeBattleState.ProjectileActor CaptureProducerActor(CharacterTestCharacterEntity source)
        {
            var world = _entityLookup.RuntimeWorld;
            if (source == null || world == null || !world.GameplayComponentWorld.TryGetStore<GameplayAttributeSetComponent>(out var attributes)
                || !attributes.TryGet(source.GameplayEntityId, out var set)) return null;
            var values = set.ToArray();
            for (int i = 0; i < values.Length; i++) values[i] = new GameplayAttributeValue(values[i].AttributeId, values[i].BaseValue,
                world.GetCurrentAttribute(source.GameplayEntityId, values[i].AttributeId, includeModifiers: true));
            return new TacticalDoubleEdgeBattleState.ProjectileActor(0, 0, _physicsRuntime.GetEntityId(source).Value,
                source.Team.TeamId, source.GameplayEntityId, values);
        }
        private ITeamRelationProvider ProducerRelations(TacticalDoubleEdgeBattleState.ProjectileActor actor)
        {
            if (actor == null) return _teamRelationProvider;
            _frozenProducerRelations.Configure(actor, _entityLookup, _teamRelationProvider); return _frozenProducerRelations;
        }
        private sealed class FrozenProducerRelations : ITeamRelationProvider
        {
            private TacticalDoubleEdgeBattleState.ProjectileActor _actor;
            private CharacterTestCombatEntityLookup _lookup;
            private ITeamRelationProvider _fallback;
            internal void Configure(TacticalDoubleEdgeBattleState.ProjectileActor actor, CharacterTestCombatEntityLookup lookup, ITeamRelationProvider fallback)
            { _actor = actor; _lookup = lookup; _fallback = fallback; }
            public bool AreHostile(CombatEntityId a, CombatEntityId b)
                => a.Value == _actor.CombatSource && _lookup.FindEntityByCombatId(b) is CharacterTestCharacterEntity target
                    ? _actor.Actor != target.GameplayEntityId && _actor.Team != target.Team.TeamId : _fallback?.AreHostile(a, b) == true;
            public bool AreFriendly(CombatEntityId a, CombatEntityId b) => IsSameTeam(a, b);
            public bool IsSameTeam(CombatEntityId a, CombatEntityId b)
                => a.Value == _actor.CombatSource && _lookup.FindEntityByCombatId(b) is CharacterTestCharacterEntity target
                    ? _actor.Team == target.Team.TeamId : _fallback?.IsSameTeam(a, b) == true;
        }
    }
}
