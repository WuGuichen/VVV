using System;
using MxFramework.CharacterAction;
using MxFramework.CharacterControl;
using MxFramework.Combat.Damage;
using MxFramework.Combat.Hit;
using MxFramework.Core.Math;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    // Mechanism candidates, not published card IDs or an independent preparation-card entry point.
    internal enum TacticalAccessoryCombatKind { BorrowMomentum = 1, PushAway = 2, BreakMomentum = 3 }
    internal enum TacticalAccessoryDamageOrigin { Unclassified, DirectAttack, Periodic, Derived }
    internal enum TacticalAccessoryCombatStatus { Unarmed, Armed, Consumed, Expired }
    internal enum TacticalAccessoryCombatResult { NoTrigger, Consumed, MissingLedger, InvalidPushEvidence }

    /// <summary>Caller-owned identity from a future unified main-card/accessory commit.</summary>
    internal readonly struct TacticalAccessoryActivation
    {
        internal TacticalAccessoryActivation(ulong stage, ulong firstTurn, ulong lastTurn, GameplayEntityId actor,
            ulong mainCardInstance, int accessoryItemInstanceId, ulong commitId, ulong activationId,
            long armedFrame, ulong armedAfterReceiptSequence)
        {
            Stage = stage; FirstTurn = firstTurn; LastTurn = lastTurn; Actor = actor;
            MainCardInstance = mainCardInstance; AccessoryItemInstanceId = accessoryItemInstanceId;
            CommitId = commitId; ActivationId = activationId; ArmedFrame = armedFrame;
            ArmedAfterReceiptSequence = armedAfterReceiptSequence;
        }
        internal ulong Stage { get; }
        internal ulong FirstTurn { get; }
        internal ulong LastTurn { get; }
        internal GameplayEntityId Actor { get; }
        internal ulong MainCardInstance { get; }
        internal int AccessoryItemInstanceId { get; }
        internal ulong CommitId { get; }
        internal ulong ActivationId { get; }
        internal long ArmedFrame { get; }
        internal ulong ArmedAfterReceiptSequence { get; }
        internal bool IsValid => Stage != 0 && FirstTurn != 0 && LastTurn >= FirstTurn && Actor.IsValid
            && MainCardInstance != 0 && AccessoryItemInstanceId > 0 && CommitId != 0 && ActivationId != 0
            && ArmedFrame >= 0 && ArmedAfterReceiptSequence < ulong.MaxValue;
    }

    /// <summary>All values are explicit mechanism inputs; there are no trial balance defaults.</summary>
    internal readonly struct TacticalAccessoryPushParameters
    {
        internal TacticalAccessoryPushParameters(int referenceSpeedMillimetersPerSecond, int referenceMassGrams,
            int minimumMassScaleMilli, int maximumMassScaleMilli, int maximumSpeedMillimetersPerSecond)
        {
            ReferenceSpeedMillimetersPerSecond = referenceSpeedMillimetersPerSecond;
            ReferenceMassGrams = referenceMassGrams; MinimumMassScaleMilli = minimumMassScaleMilli;
            MaximumMassScaleMilli = maximumMassScaleMilli; MaximumSpeedMillimetersPerSecond = maximumSpeedMillimetersPerSecond;
        }
        internal int ReferenceSpeedMillimetersPerSecond { get; }
        internal int ReferenceMassGrams { get; }
        internal int MinimumMassScaleMilli { get; }
        internal int MaximumMassScaleMilli { get; }
        internal int MaximumSpeedMillimetersPerSecond { get; }
        internal bool IsValid => ReferenceSpeedMillimetersPerSecond > 0 && ReferenceMassGrams > 0
            && MinimumMassScaleMilli >= 0 && MaximumMassScaleMilli > 0
            && MaximumMassScaleMilli >= MinimumMassScaleMilli && MaximumSpeedMillimetersPerSecond > 0;
    }

    /// <summary>
    /// A committed combat outcome, supplied by the combat owner. Origin must come from its producer:
    /// ProjectileImpact alone is insufficient because the current tornado periodic tick uses that kind too.
    /// Sequence is strictly increasing within a stage, across turns; it is not inferred from action/trace IDs.
    /// </summary>
    internal readonly struct TacticalAccessoryCombatReceipt
    {
        internal TacticalAccessoryCombatReceipt(ulong stage, ulong turn, long frame, ulong sequence,
            GameplayEntityId attacker, GameplayEntityId target, CharacterActionId actionId, int actionInstance, int trace,
            DamageProfileSourceKind sourceKind, TacticalAccessoryDamageOrigin origin, HitResolveKind outcome,
            bool attackerWasAlive, bool targetWasAlive, bool targetRemainsAlive, FixVector3 attackDirection,
            int targetMassGrams, int targetPushScaleMilli)
        {
            Stage = stage; Turn = turn; Frame = frame; Sequence = sequence; Attacker = attacker; Target = target;
            ActionId = actionId; ActionInstance = actionInstance; Trace = trace; SourceKind = sourceKind;
            Origin = origin; Outcome = outcome; AttackerWasAlive = attackerWasAlive; TargetWasAlive = targetWasAlive;
            TargetRemainsAlive = targetRemainsAlive; AttackDirection = attackDirection;
            TargetMassGrams = targetMassGrams; TargetPushScaleMilli = targetPushScaleMilli;
        }
        internal ulong Stage { get; }
        internal ulong Turn { get; }
        internal long Frame { get; }
        internal ulong Sequence { get; }
        internal GameplayEntityId Attacker { get; }
        internal GameplayEntityId Target { get; }
        internal CharacterActionId ActionId { get; }
        internal int ActionInstance { get; }
        internal int Trace { get; }
        internal DamageProfileSourceKind SourceKind { get; }
        internal TacticalAccessoryDamageOrigin Origin { get; }
        internal HitResolveKind Outcome { get; }
        internal bool AttackerWasAlive { get; }
        internal bool TargetWasAlive { get; }
        internal bool TargetRemainsAlive { get; }
        // Horizontal direction witness scaled into [-1, 1]; never reconstructed from actor facing or position.
        internal FixVector3 AttackDirection { get; }
        // Supply current EffectiveMassGrams and EffectivePushScaleMilli; zero push scale includes CanBePushed=false.
        internal int TargetMassGrams { get; }
        internal int TargetPushScaleMilli { get; }
        internal bool IsDirectAttack => Origin == TacticalAccessoryDamageOrigin.DirectAttack
            && (SourceKind == DamageProfileSourceKind.WeaponTrace || SourceKind == DamageProfileSourceKind.ProjectileImpact
                || SourceKind == DamageProfileSourceKind.AreaPulse);
        internal bool IsSuccessfulDefense => Outcome == HitResolveKind.Blocked || Outcome == HitResolveKind.Parried;
    }

    /// <summary>A single activation's data, owned by the existing effect owner when integrated.</summary>
    internal struct TacticalAccessoryCombatState
    {
        internal TacticalAccessoryActivation Identity { get; private set; }
        internal TacticalAccessoryCombatKind Kind { get; private set; }
        internal TacticalAccessoryCombatStatus Status { get; private set; }
        internal int MomentumAmount { get; private set; }
        internal TacticalAccessoryPushParameters Push { get; private set; }
        internal ulong ConsumedSequence { get; private set; }

        /// <remarks>NoAlloc. The caller must only publish this candidate after unified card admission succeeds.</remarks>
        internal static bool TryArm(TacticalAccessoryCombatKind kind, in TacticalAccessoryActivation identity,
            int momentumAmount, in TacticalAccessoryPushParameters push, out TacticalAccessoryCombatState state)
        {
            state = default;
            if (!identity.IsValid || (kind != TacticalAccessoryCombatKind.BorrowMomentum
                && kind != TacticalAccessoryCombatKind.PushAway && kind != TacticalAccessoryCombatKind.BreakMomentum)) return false;
            if (kind == TacticalAccessoryCombatKind.PushAway ? !push.IsValid || momentumAmount != 0 : momentumAmount <= 0 || push.IsValid) return false;
            state = new TacticalAccessoryCombatState { Identity = identity, Kind = kind,
                Status = TacticalAccessoryCombatStatus.Armed, MomentumAmount = momentumAmount, Push = push };
            return true;
        }
        /// <remarks>NoAlloc. The same simulation turn owner must call this, never a UI/wall-clock timer.</remarks>
        internal void AdvanceTurn(ulong stage, ulong turn)
        {
            if (Status == TacticalAccessoryCombatStatus.Armed && stage == Identity.Stage && turn > Identity.LastTurn)
                Status = TacticalAccessoryCombatStatus.Expired;
        }
        internal void Clear() { this = default; }
        internal void Consume(ulong sequence) { Status = TacticalAccessoryCombatStatus.Consumed; ConsumedSequence = sequence; }
    }

    internal readonly struct TacticalAccessoryCombatResponse
    {
        internal TacticalAccessoryCombatResponse(int momentumGranted, int momentumSpent, CharacterMotionRequest push)
        { MomentumGranted = momentumGranted; MomentumSpent = momentumSpent; Push = push; }
        internal int MomentumGranted { get; }
        internal int MomentumSpent { get; }
        internal CharacterMotionRequest Push { get; }
    }

    /// <summary>
    /// Pure candidate combat mechanisms for 借力/推离/破势. No deck, second resource, clock, event bus,
    /// damage mutation or independent preparation-card entry point is created here.
    /// </summary>
    internal static class TacticalAccessoryCombatRules
    {
        /// <remarks>NoAlloc. Single-threaded with the existing momentum/combat owner. Repeated receipts cannot consume twice.</remarks>
        internal static TacticalAccessoryCombatResult Resolve(ref TacticalAccessoryCombatState state,
            in TacticalAccessoryCombatReceipt receipt, IMomentumLedger actorLedger, IMomentumLedger defendingLedger,
            out TacticalAccessoryCombatResponse response)
        {
            response = default;
            if (state.Status != TacticalAccessoryCombatStatus.Armed || receipt.Stage != state.Identity.Stage) return TacticalAccessoryCombatResult.NoTrigger;
            state.AdvanceTurn(receipt.Stage, receipt.Turn);
            if (state.Status != TacticalAccessoryCombatStatus.Armed || receipt.Turn < state.Identity.FirstTurn
                || receipt.Frame < state.Identity.ArmedFrame || receipt.Sequence <= state.Identity.ArmedAfterReceiptSequence
                || !receipt.Attacker.IsValid || !receipt.Target.IsValid || receipt.Attacker == receipt.Target
                || !receipt.ActionId.IsValid || receipt.ActionInstance <= 0 || receipt.Trace <= 0
                || !receipt.AttackerWasAlive || !receipt.TargetWasAlive || !receipt.IsDirectAttack) return TacticalAccessoryCombatResult.NoTrigger;

            bool outgoing = receipt.Attacker == state.Identity.Actor;
            bool incoming = receipt.Target == state.Identity.Actor;
            switch (state.Kind)
            {
                case TacticalAccessoryCombatKind.BorrowMomentum:
                    if (!incoming || !receipt.IsSuccessfulDefense || !receipt.TargetRemainsAlive) return TacticalAccessoryCombatResult.NoTrigger;
                    if (actorLedger == null) return TacticalAccessoryCombatResult.MissingLedger;
                    int before = actorLedger.Current;
                    actorLedger.Grant(state.MomentumAmount);
                    response = new TacticalAccessoryCombatResponse(actorLedger.Current - before, 0, default);
                    break;
                case TacticalAccessoryCombatKind.BreakMomentum:
                    if (!outgoing || !receipt.IsSuccessfulDefense || !receipt.TargetRemainsAlive) return TacticalAccessoryCombatResult.NoTrigger;
                    if (defendingLedger == null) return TacticalAccessoryCombatResult.MissingLedger;
                    // Partial drain, with the same ledger's reservation protected. Do not use TrySpend(fixedAmount).
                    int spend = Math.Min(state.MomentumAmount, Math.Max(0, defendingLedger.Spendable));
                    if (!defendingLedger.TrySpend(spend)) return TacticalAccessoryCombatResult.MissingLedger;
                    response = new TacticalAccessoryCombatResponse(0, spend, default);
                    break;
                case TacticalAccessoryCombatKind.PushAway:
                    // A Damage-kind direct hit qualifies even when armor absorbs all HP damage.
                    if (!outgoing || receipt.Outcome != HitResolveKind.Damage) return TacticalAccessoryCombatResult.NoTrigger;
                    if (receipt.TargetRemainsAlive)
                    {
                        if (!TryCreatePush(state.Push, receipt, out var push)) return TacticalAccessoryCombatResult.InvalidPushEvidence;
                        response = new TacticalAccessoryCombatResponse(0, 0, push);
                    }
                    break;
                default: return TacticalAccessoryCombatResult.NoTrigger;
            }
            // Zero available momentum, saturation, push immunity or a lethal hit still consume the qualifying interaction.
            state.Consume(receipt.Sequence);
            return TacticalAccessoryCombatResult.Consumed;
        }

        /// <remarks>
        /// NoAlloc. The resulting additive Impulse must pass through the existing request accumulator and collision motor.
        /// It requests initial velocity, never a transform relocation or ScatterPressure/ImpactPower change.
        /// </remarks>
        private static bool TryCreatePush(in TacticalAccessoryPushParameters parameters,
            in TacticalAccessoryCombatReceipt receipt, out CharacterMotionRequest push)
        {
            push = default;
            if (receipt.TargetMassGrams <= 0 || receipt.TargetPushScaleMilli < 0) return false;
            if (receipt.TargetPushScaleMilli == 0) return true;
            var direction = receipt.AttackDirection;
            if (direction.X < -Fix64.One || direction.X > Fix64.One || direction.Z < -Fix64.One || direction.Z > Fix64.One) return false;
            long largestComponent = Math.Max(Math.Abs(direction.X.RawValue), Math.Abs(direction.Z.RawValue));
            if (largestComponent == 0) return false;
            // Scale before squaring so a small but nonzero fixed-point direction cannot underflow to zero.
            var scaled = new FixVector3(Fix64.FromRatio(direction.X.RawValue, largestComponent), Fix64.Zero,
                Fix64.FromRatio(direction.Z.RawValue, largestComponent));
            if (!scaled.TryNormalize(out var horizontal)) return false;
            long massScale = (long)parameters.ReferenceMassGrams * 1000 / receipt.TargetMassGrams;
            massScale = Math.Max(parameters.MinimumMassScaleMilli, Math.Min(parameters.MaximumMassScaleMilli, massScale));
            long speed = Math.Min(int.MaxValue, (long)parameters.ReferenceSpeedMillimetersPerSecond * massScale / 1000);
            speed = Math.Min(parameters.MaximumSpeedMillimetersPerSecond, speed * receipt.TargetPushScaleMilli / 1000);
            if (speed <= 0) return true;
            int x = RoundVelocity(horizontal.X, (int)speed), z = RoundVelocity(horizontal.Z, (int)speed);
            if (x == 0 && z == 0) return true;
            push = new CharacterMotionRequest(CharacterMotionRequestKind.Impulse, x, 0, z,
                CharacterMotionRequestSpace.World, movementModeId: 0, lockMovement: false,
                movementControlScaleMilli: CharacterMotionRequest.DefaultControlScaleMilli,
                rotationControlScaleMilli: CharacterMotionRequest.DefaultControlScaleMilli,
                hasMovementControlScale: false, hasRotationControlScale: false,
                sourceActionId: receipt.ActionId, stableEventId: receipt.Trace);
            return true;
        }
        private static int RoundVelocity(Fix64 direction, int speed)
        {
            long value = direction.RawValue * speed;
            long half = Fix64.Scale / 2;
            return checked((int)((value < 0 ? value - half : value + half) / Fix64.Scale));
        }
    }
}
