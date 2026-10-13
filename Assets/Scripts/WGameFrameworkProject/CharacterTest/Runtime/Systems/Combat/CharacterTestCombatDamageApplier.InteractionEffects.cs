using System.Collections.Generic;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Game.CharacterTest.TacticalCard;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestCombatDamageApplier
    {
        private bool _applyingInteractionDerived;
        private readonly HitResolveResult[] _singleDamageResult = new HitResolveResult[1];
        internal readonly struct DerivedMarkRequest
        {
            internal readonly HitResolveResult Result;
            internal readonly GameplayEntityId Target;
            internal DerivedMarkRequest(HitResolveResult result, GameplayEntityId target) { Result = result; Target = target; }
        }
        private readonly List<DerivedMarkRequest> _derivedMarkRequests = new List<DerivedMarkRequest>(16);
        internal IReadOnlyList<DerivedMarkRequest> PendingDerivedMarks => _derivedMarkRequests;
        internal void ClearDerivedMarks() => _derivedMarkRequests.Clear();
        private readonly List<GameplayScatterPressureRequest> _interactionPressureRequests = new List<GameplayScatterPressureRequest>(16);
        private void CommitInteractionHpCommands(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame)
        {
            if (_damageApplicationCommands.Count == 0) return;
            _attributeCommandSystem.Tick(CreateGameplayContext(world, frame, _damageApplicationCommands, _attributeCommandState));
            _damageApplicationCommands.Clear();
        }
        private void CommitInteractionReceipt(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame, HitResolveResult result,
            DamageProfileResolveResult payload, int actualHpLoss, bool targetWasAlive)
        {
            if (_applyingInteractionDerived) return;
            var attacker = FindEntityByCombatId(result.AttackerId); var target = FindEntityByCombatId(result.TargetId);
            if (attacker == null || target == null || result.Frame.Value != frame.Value) return;
            var receipt = new TacticalInteractionReceipt(attacker.InteractionEffects.Stage, attacker.InteractionEffects.Turn, frame.Value, attacker.GameplayEntityId, target.GameplayEntityId, result.ActionInstanceId, result.TraceId, result.ProducerIdentity);
            bool activeSource = payload.Success && (payload.Usage.SourceKind == DamageProfileSourceKind.WeaponTrace
                || payload.Usage.SourceKind == DamageProfileSourceKind.ProjectileImpact || payload.Usage.SourceKind == DamageProfileSourceKind.AreaPulse);
            bool validHit = targetWasAlive && result.IsAcceptedDamage && activeSource;
            bool ordinaryBlock = targetWasAlive && result.Kind == HitResolveKind.Blocked && activeSource;
            bool pressureAvailable = ordinaryBlock && world.TryGetScatterPressure(target, out var pressure) && pressure.HasValidState()
                && world.GameplayComponentWorld.IsAlive(target.GameplayEntityId)
                && world.GetCurrentAttribute(target.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp) > 0;
            world.TryGetTacticalMotion(attacker.Id.Value, out var attackingLedger);
            world.TryGetTacticalMotion(target.Id.Value, out var defendingLedger);
            int extra = 0, extraPressure = 0;
            if (world.GetCurrentAttribute(attacker.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp) > 0)
                extra = attacker.InteractionEffects.CommitReceipt(receipt, actualHpLoss, validHit, ordinaryBlock,
                    derived: false, attackingLedger, out extraPressure, pressureAvailable);
            if (extraPressure > 0)
                _interactionPressureRequests.Add(new GameplayScatterPressureRequest(target.GameplayEntityId, extraPressure, attacker.Id.Value,
                    GameplayScatterPressureEvents.ApplyPressureReason, CreateTraceIdentity(result.TraceId)));
            if (world.GetCurrentAttribute(target.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp) > 0)
                target.InteractionEffects.CommitReceipt(new TacticalInteractionReceipt(target.InteractionEffects.Stage, target.InteractionEffects.Turn, receipt.Frame, receipt.Attacker, receipt.Target, receipt.ActionInstance, receipt.Trace, receipt.ProducerIdentity), actualHpLoss, validHit, result.Kind == HitResolveKind.Blocked,
                    derived: false, defendingLedger);
            if (extra > 0 && world.GetCurrentAttribute(target.GameplayEntityId, CharacterTestCharacterAttributeIds.Hp) > 0)
                _derivedMarkRequests.Add(new DerivedMarkRequest(new HitResolveResult(result.AttackerId, result.TargetId, result.ActionId, result.ActionInstanceId, result.TraceId,
                    result.Frame, HitResolveKind.Damage, extra, 0, FixVector3.Zero, result.HitPointWorld, result.HitNormalWorld).WithProducerIdentity(result.ProducerIdentity), target.GameplayEntityId));
        }
        private void EnqueueInteractionPressureRequests()
        {
            for (int i = 0; i < _interactionPressureRequests.Count; i++) _scatterPressureSystem.Enqueue(_interactionPressureRequests[i]);
            _interactionPressureRequests.Clear();
        }
        private void ApplyDerivedMarkRequests(CharacterTestCharacterRuntimeWorld world, RuntimeFrame frame, GameplaySystemContext context)
        {
            for (int i = 0; i < _derivedMarkRequests.Count; i++)
            {
                var request = _derivedMarkRequests[i];
                var target = FindEntityByCombatId(request.Result.TargetId);
                // Later roots can kill/remove/replace the marked target after this response was queued.
                // Gate BEFORE the bridge mutates armor or emits telemetry/events; do not retarget or refund.
                if (target == null || target.GameplayEntityId != request.Target || !target.CombatState.IsAlive
                    || !world.GameplayComponentWorld.IsAlive(request.Target)
                    || world.GetCurrentAttribute(request.Target, CharacterTestCharacterAttributeIds.Hp) <= 0) continue;
                _singleDamageResult[0] = request.Result;
                _currentDamagePayloadResolver = ResolveDerivedMarkPayload;
                _attributeCommandState.Clear(); // Separate derived transaction; original trace does not suppress its HP command.
                _applyingInteractionDerived = true;
                try { GetDamageApplicationSystem(world).Tick(context); CommitInteractionHpCommands(world, frame); }
                finally { _applyingInteractionDerived = false; }
            }
            _derivedMarkRequests.Clear();
        }
        internal static DamageProfileResolveResult ResolveDerivedMarkPayload(HitResolveResult result)
        {
            var usage = new DamageProfileUsage(DamageProfileSourceKind.Scripted, actionInstanceId: result.ActionInstanceId,
                sourceEntityId: result.AttackerId.Value, targetEntityId: result.TargetId.Value);
            return new DamageProfileResolveResult(true, string.Empty, 0, 0, DamageFormulaIds.Flat, result.Damage, 0, 0,
                FixVector3.Zero, 0, 0, 0, usage);
        }
    }
}
