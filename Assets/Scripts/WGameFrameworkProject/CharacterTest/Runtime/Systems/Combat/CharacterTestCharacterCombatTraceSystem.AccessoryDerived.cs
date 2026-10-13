using System;
using System.Collections.Generic;
using MxFramework.Combat.Hit;
using MxFramework.Combat.Projectiles;
using MxFramework.Core.Math;
using MxFramework.Game.CharacterTest.TacticalCard;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestCharacterCombatTraceSystem
    {
        private Func<HitResolveResult, TacticalSecondBatchHit> _buffReceiptResolver;
        private TacticalSecondBatchHit ResolveBuffReceipt(HitResolveResult result)
            => _accessoryReceipts.TryGetValue(new CharacterTestResolvedHitKey(result), out var receipt) ? receipt : default;
        private readonly List<CombatProjectileImpactEvent> _deferredAccessoryImpacts = new List<CombatProjectileImpactEvent>();
        private readonly HashSet<CharacterTestResolvedHitKey> _accessoryCarrierHits = new HashSet<CharacterTestResolvedHitKey>();
        private readonly List<HitResolveResult> _accessoryDamageSubset = new List<HitResolveResult>();
        private readonly List<HitResolveResult> _frameAcceptedAccessoryHits = new List<HitResolveResult>();

        private void ApplyAccessoryRounds(CharacterTestCharacterRuntimeWorld world,
            CharacterTestCharacterActionRuntime actionRuntime, RuntimeFrame frame)
        {
            world.DoubleEdgeState.FlushThirdLifecycleBefore(frame.Value);
            _frameAcceptedAccessoryHits.Clear();
            _secondDecisionResolver ??= ResolveSecondBatchDecision;
            _buffReceiptResolver ??= ResolveBuffReceipt;
            for (int round = 0; ; round++) {
                if (round >= 256) throw new InvalidOperationException("accessory:CausalRoundBudgetExceeded");
                ResolveAccessoryRound(world, frame, round);
                BeginThirdAccessoryRound(world, frame, round);
                _accessoryDamageSubset.Clear();
                foreach (var hit in _results) if (!_accessoryCarrierHits.Contains(new CharacterTestResolvedHitKey(hit))) _accessoryDamageSubset.Add(hit);
                _damageApplier.Apply(world, actionRuntime, frame, _accessoryDamageSubset, _resolveDamagePayload,
                    _hitZonesByHit, _traceRecorder, _resolveAccessoryEvidence, _secondDecisionResolver);
                if (_hasThirdRound && !world.DoubleEdgeState.EndThirdRound()) throw new InvalidOperationException("third-batch:UnconsumedRound");
                _guardAuraRuntime.ApplyOnHitBuffs(world, _physicsRuntime, frame, _accessoryDamageSubset, _resolveOnHitBuffs,
                    _resolveAccessoryEvidence, _buffReceiptResolver);
                _frameAcceptedAccessoryHits.AddRange(_accessoryDamageSubset);
                ReleaseAcceptedProjectileImpacts(world, frame);
                _results.Clear();
                GatherDerivedMarkCandidates(world, frame);
                _effectTimelineRuntime?.PumpFrameZero(frame);
                if (_results.Count == 0 && !world.DoubleEdgeState.HasUnclosedBirth(frame.Value, round + 1)
                    && !world.DoubleEdgeState.HasPendingThirdEvents(frame.Value, round + 1)) break;
            }
            _damageApplier.CompleteAccessoryFrame(world, actionRuntime, frame);
            world.DoubleEdgeState.FlushThirdLifecycle();
            RecordProjectilePhysicalInteractions(frame, _projectileFrameEvents.PhysicalEvents);
            _results.AddRange(_frameAcceptedAccessoryHits);
        }

        private void ReleaseAcceptedProjectileImpacts(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame)
        {
            // Iterate the published stable order, never the projectile callback order.
            foreach (var result in _results) {
                if (result.Kind != HitResolveKind.Damage && result.Kind != HitResolveKind.Blocked && result.Kind != HitResolveKind.Parried) continue;
                var key = new CharacterTestResolvedHitKey(result);
                for (int i = 0; i < _deferredAccessoryImpacts.Count; i++) {
                    var impact = _deferredAccessoryImpacts[i];
                    if (!key.Equals(new CharacterTestResolvedHitKey(impact.ResolveResult))) continue;
                    _accessoryReceipts.TryGetValue(key, out _projectileCausalParent);
                    try { PublishProjectileImpactEffects(world, impact, frame); }
                    finally { _projectileCausalParent = default; }
                }
            }
            _deferredAccessoryImpacts.Clear();
        }

        private void PublishProjectileImpactEffects(CharacterTestCharacterRuntimeWorld world,
            CombatProjectileImpactEvent impact, RuntimeFrame frame)
        {
            StartEffectTimelineFromProjectileImpact(impact, frame);
            if (IsArcMineProjectile(impact.Projectile)) CreateArcMineFromProjectileImpact(world, impact, frame);
            else if (IsPoisonBottleProjectile(impact.Projectile) && impact.ResolveResult.IsAcceptedDamage)
                CreatePoisonBottlePoolFromProjectileImpact(world, impact, frame);
            TryRegisterHookChainPullFromProjectileImpact(world, impact, frame);
            CreateRuneTurretFromProjectileImpact(world, impact, frame);
        }

        private void GatherDerivedMarkCandidates(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame)
        {
            foreach (var request in _damageApplier.PendingDerivedMarks) {
                var original = request.Result; var target = _entityLookup.FindEntityByCombatId(original.TargetId);
                if (target == null || target.GameplayEntityId != request.Target || !target.CombatState.IsAlive
                    || world.GetCurrentAttribute(request.Target, CharacterTestCharacterAttributeIds.Hp) <= 0) continue;
                var physicalKey = new CharacterTestResolvedHitKey(original);
                var parentEvidence = ResolveAccessoryEvidence(original);
                _accessoryReceipts.TryGetValue(physicalKey, out var parent);
                ulong node = TacticalDoubleEdgeBattleState.ProducerNode(10, frame.Value,
                    original.TraceId ^ unchecked((long)parentEvidence.RootOccurrence),
                    TacticalDoubleEdgeBattleState.ProducerNode(10, original.AttackerId.Value, original.ActionInstanceId, parent.Source.Node)
                        ^ unchecked((ulong)(uint)request.Target.Index << 32 | (uint)request.Target.Generation));
                TacticalSecondBatchSource child = default;
                if (parent.Source.IsValid && !world.DoubleEdgeState.TryRegisterSecondBatchBirth(parent.Source,
                    node, parent.ActualProducer, frame.Value, parent.Key, out child))
                    throw new InvalidOperationException("accessory:DerivedMarkParentRejected");
                int occurrence = (int)(node & int.MaxValue); if (occurrence == 0) occurrence = 1;
                var result = new HitResolveResult(original.AttackerId, original.TargetId, original.ActionId, occurrence,
                    original.TraceId, original.Frame, HitResolveKind.Damage, original.Damage, 0,
                    FixVector3.Zero, original.HitPointWorld, original.HitNormalWorld).WithProducerIdentity(
                    new CombatHitProducerIdentity(CombatHitProducerKind.DerivedDamage, node, 0, 0));
                var key = new CharacterTestResolvedHitKey(result);
                if (_damagePayloadsByHit.ContainsKey(key)) throw new InvalidOperationException("accessory:DerivedMarkIdentityCollision");
                var evidence = CharacterTestAccessoryHitEvidence.Mark.WithSource(parentEvidence.DoubleEdgeSource)
                    .WithCollision(parentEvidence.Attacker.IsValid ? parentEvidence.Attacker
                        : _entityLookup.FindEntityByCombatId(original.AttackerId)?.GameplayEntityId ?? default,
                        target, parentEvidence.ActualEnemy, child, node);
                SetAccessoryAwareDamagePayload(key, CharacterTestCombatDamageApplier.ResolveDerivedMarkPayload(result), evidence);
                _results.Add(result);
            }
            _damageApplier.ClearDerivedMarks();
        }
    }
}
