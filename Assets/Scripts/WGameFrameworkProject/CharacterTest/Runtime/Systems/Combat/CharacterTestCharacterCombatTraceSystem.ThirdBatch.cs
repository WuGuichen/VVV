using System;
using MxFramework.Combat.Hit;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterCombatTraceSystem
    {
        private TacticalThirdBatchEvent[] _thirdDamageEvents = Array.Empty<TacticalThirdBatchEvent>();
        private bool _hasThirdRound;
        private readonly System.Collections.Generic.HashSet<CharacterTestResolvedHitKey> _thirdUniqueHits = new System.Collections.Generic.HashSet<CharacterTestResolvedHitKey>();
        internal bool IsThirdDirectProjectileRequest(int request)
            => CharacterTestFireballProjectileAdapter.IsDirectProjectileRequest(_projectileDefinitions, _projectileSpawnProfiles, request);
        private void BeginThirdAccessoryRound(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame, int round)
        {
            _hasThirdRound = world.DoubleEdgeState.HasThirdBatch;
            if (!_hasThirdRound) return;
            if (_thirdDamageEvents.Length < _results.Count) _thirdDamageEvents = new TacticalThirdBatchEvent[Math.Max(32, _results.Count * 2)];
            int count = 0, write = 0; _thirdUniqueHits.Clear();
            for (int i = 0; i < _results.Count; i++) {
                var hit = _results[i];
                var physical = new CharacterTestResolvedHitKey(hit);
                var evidence = ResolveAccessoryEvidence(hit); var payload = _resolveDamagePayload(hit);
                if (!_accessoryCarrierHits.Contains(physical)
                    && CharacterTestCombatDamageApplier.TryThirdHit(world, hit, payload, evidence, round, out var item)) {
                    if (world.DoubleEdgeState.HasAcceptedThirdRoot(item) || !_thirdUniqueHits.Add(physical)) continue;
                    _thirdDamageEvents[count++] = item;
                }
                _results[write++] = hit;
            }
            if (write < _results.Count) _results.RemoveRange(write, _results.Count - write);
            if (!world.DoubleEdgeState.BeginThirdRound(frame.Value, round, _thirdDamageEvents, count))
                throw new InvalidOperationException("third-batch:CompleteRoundRejected");
            // Complete producer keys, never callback arrival, define the HP order for all batches.
            for (int i = 1; i < _results.Count; i++) {
                var next = _results[i]; int j = i - 1;
                while (j >= 0 && CompareThirdHits(world, _results[j], next, round) > 0) { _results[j + 1] = _results[j]; j--; }
                _results[j + 1] = next;
            }
        }
        private int CompareThirdHits(CharacterTestCharacterRuntimeWorld world, HitResolveResult a, HitResolveResult b, int round)
        {
            var left = UnifiedThirdOrder(world, a, round); var right = UnifiedThirdOrder(world, b, round);
            int result = left.CompareTo(right);
            return result != 0 ? result : CompareAccessoryHits(a, b);
        }
        private TacticalThirdBatchOrder UnifiedThirdOrder(CharacterTestCharacterRuntimeWorld world, HitResolveResult hit, int round)
        {
            var evidence = ResolveAccessoryEvidence(hit);
            if (CharacterTestCombatDamageApplier.TryThirdHit(world, hit, _resolveDamagePayload(hit), evidence, round, out var item)) return item.Order;
            var producer = hit.ProducerIdentity;
            ulong node = evidence.Lineage.IsValid ? evidence.Lineage.Node
                : TacticalDoubleEdgeBattleState.ProducerNode((int)producer.Kind + 50, (long)producer.Id, (long)producer.Generation,
                    TacticalDoubleEdgeBattleState.ProducerNode(50, hit.AttackerId.Value, hit.ActionInstanceId, 1));
            var recipient = evidence.Target.IsValid ? evidence.Target : new MxFramework.Gameplay.GameplayEntityId(hit.TargetId.Value, 1);
            ulong occurrence = ResolveProducerOccurrence(hit, evidence.RootOccurrence);
            return TacticalDoubleEdgeBattleState.ThirdOrder(hit.Frame.Value, round, 3, unchecked((uint)hit.TraceId), node, recipient, occurrence == 0 ? 1 : occurrence);
        }
    }

    internal sealed partial class CharacterTestCombatDamageApplier
    {
        private static TacticalThirdBatchAttack ThirdMotionSource(CharacterTestCharacterRuntimeWorld world, in HitResolveResult hit,
            MxFramework.Combat.Damage.DamageProfileSourceKind origin)
        {
            var p = hit.ProducerIdentity;
            if (p.IsValid) {
                if (p.Kind == CombatHitProducerKind.ProjectileDirect || p.Kind == CombatHitProducerKind.ProjectileArea || p.Kind == CombatHitProducerKind.ProjectilePeriodic)
                    return world.DoubleEdgeState.ThirdSource(1, checked((int)p.Id), checked((int)p.Generation)).Attack;
                if (p.Kind == CombatHitProducerKind.EffectTimeline) return world.DoubleEdgeState.ThirdSource(3, checked((int)p.Id), 0).Attack;
                return default;
            }
            return origin == MxFramework.Combat.Damage.DamageProfileSourceKind.WeaponTrace
                ? world.DoubleEdgeState.ThirdSource(2, hit.ActionInstanceId, 0).Attack : default;
        }
        internal static bool TryThirdHit(CharacterTestCharacterRuntimeWorld world, in HitResolveResult hit,
            in MxFramework.Combat.Damage.DamageProfileResolveResult payload, in CharacterTestAccessoryHitEvidence evidence,
            int round, out TacticalThirdBatchEvent item)
        {
            item = default;
            if (!world.DoubleEdgeState.HasThirdBatch || !payload.Success || !evidence.Action.IsValid || evidence.Action != payload.Usage.ActionId
                || hit.Kind == HitResolveKind.Duplicate || !evidence.Target.IsValid) return false;
            TacticalThirdBatchSource source = default;
            if (evidence.Producer == CharacterTestAccessoryHitProducer.WeaponTrace
                && payload.Usage.SourceKind == MxFramework.Combat.Damage.DamageProfileSourceKind.WeaponTrace)
                source = world.DoubleEdgeState.ThirdSource(2, hit.ActionInstanceId, 0);
            else if (evidence.Producer == CharacterTestAccessoryHitProducer.ProjectileDirectSweep
                && payload.Usage.SourceKind == MxFramework.Combat.Damage.DamageProfileSourceKind.ProjectileImpact
                && hit.ProducerIdentity.Kind == CombatHitProducerKind.ProjectileDirect)
                source = world.DoubleEdgeState.ThirdSource(1, checked((int)hit.ProducerIdentity.Id), checked((int)hit.ProducerIdentity.Generation));
            if (!source.IsValid || source.Attack.Actor != evidence.Attacker) return false;
            ulong occurrence = CharacterTestCharacterCombatTraceSystem.ResolveProducerOccurrence(hit, evidence.RootOccurrence);
            if (occurrence == 0) occurrence = 1;
            var order = TacticalDoubleEdgeBattleState.ThirdOrder(hit.Frame.Value, round, 3, unchecked((uint)hit.TraceId), source.Node, evidence.Target, occurrence);
            item = TacticalThirdBatchEvent.Hit(order, source, hit.Kind, true, evidence.ActualEnemy); return true;
        }
    }
}
