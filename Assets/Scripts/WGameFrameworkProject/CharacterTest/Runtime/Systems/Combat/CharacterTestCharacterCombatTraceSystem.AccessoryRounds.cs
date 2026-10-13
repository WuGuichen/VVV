using System;
using System.Collections.Generic;
using MxFramework.Combat.Hit;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterCombatTraceSystem
    {
        private TacticalSecondBatchHit[] _accessoryRoundHits = Array.Empty<TacticalSecondBatchHit>();
        private TacticalSecondBatchHitDecision[] _accessoryRoundDecisions = Array.Empty<TacticalSecondBatchHitDecision>();
        private CharacterTestResolvedHitKey[] _accessoryRoundKeys = Array.Empty<CharacterTestResolvedHitKey>();
        private readonly Dictionary<CharacterTestResolvedHitKey, TacticalSecondBatchHitDecision> _accessoryDecisions =
            new Dictionary<CharacterTestResolvedHitKey, TacticalSecondBatchHitDecision>();
        private readonly Dictionary<CharacterTestResolvedHitKey, TacticalSecondBatchHit> _accessoryReceipts =
            new Dictionary<CharacterTestResolvedHitKey, TacticalSecondBatchHit>();
        private readonly HashSet<CharacterTestResolvedHitKey> _uniqueAcceptedAccessoryHits = new HashSet<CharacterTestResolvedHitKey>();
        private Func<HitResolveResult, TacticalSecondBatchHitDecision> _secondDecisionResolver;

        private TacticalSecondBatchHitDecision ResolveSecondBatchDecision(HitResolveResult hit)
            => _accessoryDecisions.TryGetValue(new CharacterTestResolvedHitKey(hit), out var decision) ? decision : default;

        // Called once every producer has finished this phase, before any HP or attack-side publication.
        private void ResolveAccessoryRound(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame, int round)
        {
            _accessoryDecisions.Clear(); _accessoryReceipts.Clear();
            if (!world.DoubleEdgeState.HasSecondBatch) return;
            if (_accessoryRoundHits.Length < _results.Count) {
                int size = Math.Max(32, _results.Count * 2);
                _accessoryRoundHits = new TacticalSecondBatchHit[size];
                _accessoryRoundDecisions = new TacticalSecondBatchHitDecision[size];
                _accessoryRoundKeys = new CharacterTestResolvedHitKey[size];
            }
            int count = 0;
            foreach (var result in _results) {
                var key = new CharacterTestResolvedHitKey(result);
                var evidence = ResolveAccessoryEvidence(result);
                if (!evidence.Lineage.IsValid) {
                    if (world.DoubleEdgeState.SuppressesAttack(evidence.DoubleEdgeSource))
                        _accessoryDecisions[key] = new TacticalSecondBatchHitDecision(false, true, 1000, false, false);
                    else if (world.DoubleEdgeState.SecondBatchRoot(evidence.DoubleEdgeSource).IsValid)
                        throw new InvalidOperationException("accessory:MissingProducerLineage");
                    continue;
                }
                // Duplicate collider classifications carry no new collision identity or attack side effects.
                if (result.Kind == HitResolveKind.Duplicate) continue;
                var target = _entityLookup.FindEntityByCombatId(result.TargetId);
                if (target == null || target.GameplayEntityId != evidence.Target) {
                    _accessoryDecisions[key] = new TacticalSecondBatchHitDecision(false, true, 1000, false, false);
                    continue;
                }
                var hit = new TacticalSecondBatchHit(evidence.Lineage,
                    evidence.Lineage.CreateHitKey(frame.Value, unchecked((ulong)(uint)result.TraceId),
                        evidence.Target, ResolveProducerOccurrence(result, evidence.RootOccurrence)), evidence.Attacker, result.Kind,
                    target.CombatState.IsAlive, evidence.ActualEnemy);
                if (_accessoryReceipts.TryGetValue(key, out var known)) {
                    if (!known.Matches(hit)) throw new InvalidOperationException("accessory:AliasedCollisionEvidence");
                    continue;
                }
                _accessoryReceipts.Add(key, hit);
                _accessoryRoundHits[count] = hit; _accessoryRoundKeys[count++] = key;
            }
            if (!world.DoubleEdgeState.TryResolveSecondBatchRound(frame.Value, round,
                _accessoryRoundHits, count, _accessoryRoundDecisions))
                throw new InvalidOperationException("accessory:CompleteRoundRejected");
            for (int i = 0; i < count; i++) _accessoryDecisions[_accessoryRoundKeys[i]] = _accessoryRoundDecisions[i];
            // The shared accepted subset feeds body-part cache, HP, pressure, reaction, buffs and publication.
            int write = 0; _uniqueAcceptedAccessoryHits.Clear();
            for (int i = 0; i < _results.Count; i++) {
                var result = _results[i];
                var decision = ResolveSecondBatchDecision(result);
                if (!decision.SuppressAttackJudgment && !decision.IsReplay
                    && !(result.Kind == HitResolveKind.Duplicate && ResolveAccessoryEvidence(result).Lineage.IsValid)
                    && (!ResolveAccessoryEvidence(result).Lineage.IsValid || _uniqueAcceptedAccessoryHits.Add(new CharacterTestResolvedHitKey(result))))
                    _results[write++] = result;
            }
            if (write < _results.Count) _results.RemoveRange(write, _results.Count - write);
            // Stable insertion sort keeps the payload/evidence maps indexed by the original physical key.
            for (int i = 1; i < _results.Count; i++) {
                var next = _results[i]; int j = i - 1;
                while (j >= 0 && CompareAccessoryHits(_results[j], next) > 0) { _results[j + 1] = _results[j]; j--; }
                _results[j + 1] = next;
            }
        }

        // SourceNode already binds the complete native ID/generation. Preserve phase and occurrence
        // in the receipt as well; direct/area/periodic hits must not alias inside the same producer.
        internal static ulong ResolveProducerOccurrence(in HitResolveResult hit, ulong fallback)
        {
            var producer = hit.ProducerIdentity;
            if (!producer.IsValid) return fallback;
            if (producer.Occurrence > uint.MaxValue) throw new InvalidOperationException("accessory:ProducerOccurrenceExhausted");
            return ((ulong)producer.Kind << 32) | producer.Occurrence;
        }

        private int CompareAccessoryHits(HitResolveResult left, HitResolveResult right)
        {
            bool a = _accessoryReceipts.TryGetValue(new CharacterTestResolvedHitKey(left), out var first);
            bool b = _accessoryReceipts.TryGetValue(new CharacterTestResolvedHitKey(right), out var second);
            if (a && b) return first.CompareTo(second);
            if (a != b) return a ? 1 : -1;
            int value = left.TraceId.CompareTo(right.TraceId);
            if (value == 0) value = left.AttackerId.Value.CompareTo(right.AttackerId.Value);
            if (value == 0) value = left.ActionInstanceId.CompareTo(right.ActionInstanceId);
            if (value == 0) value = left.TargetId.Value.CompareTo(right.TargetId.Value);
            return value == 0 ? left.ProducerIdentity.CompareTo(right.ProducerIdentity) : value;
        }
    }
}
