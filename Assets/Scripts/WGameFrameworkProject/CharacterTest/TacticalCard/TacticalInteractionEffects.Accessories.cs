using System;
using System.Collections.Generic;
using MxFramework.CharacterControl;
using MxFramework.Core.Math;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    // The real motion owner acknowledges admission before the effect owner publishes consumption.
    internal interface ITacticalAccessoryMotionSink
    {
        bool TryEnqueue(in CharacterMotionRequest request);
    }

    /// <summary>Trusted P2 unified-commit owner, using immutable frozen publication references and admission evidence.
    /// No default implementation exists. Positive IDs and an inventory lookup alone cannot implement this contract.</summary>
    internal interface ITacticalAccessoryAdmissionAuthority
    {
        // Validate exact committed stage/generation/card/assembly/commit identity, kind, lifetime and parameters.
        bool ValidateActivation(in TacticalAccessoryCombatState candidate);
        // Validate the complete expected activation manifest, including omissions/null, against the frozen commit owner.
        // Must use the SAME frozen references and policies as activation; must not mutate the incoming snapshot.
        bool ValidateRestoration(TacticalAccessoryAuthoritySnapshot snapshot, ulong stage, ulong turn,
            MxFramework.Gameplay.GameplayEntityId actor);
    }

    // Primitive DTOs deliberately avoid readonly mechanism structs/private setters in JSON.
    internal sealed class TacticalAccessoryActivationData
    {
        public ulong Stage, FirstTurn, LastTurn, MainCardInstance, CommitId, ActivationId, ArmedAfterSequence, ConsumedSequence;
        public int ActorIndex, ActorGeneration, AccessoryItemInstanceId, Kind, Status, MomentumAmount;
        public long ArmedFrame;
        public int ReferenceSpeed, ReferenceMass, MinimumMassScale, MaximumMassScale, MaximumSpeed;
        internal TacticalAccessoryActivationData Clone() => (TacticalAccessoryActivationData)MemberwiseClone();
        internal static TacticalAccessoryActivationData From(in TacticalAccessoryCombatState state)
        {
            var id = state.Identity;
            var push = state.Push;
            return new TacticalAccessoryActivationData { Stage = id.Stage, FirstTurn = id.FirstTurn, LastTurn = id.LastTurn,
                ActorIndex = id.Actor.Index, ActorGeneration = id.Actor.Generation, MainCardInstance = id.MainCardInstance,
                AccessoryItemInstanceId = id.AccessoryItemInstanceId, CommitId = id.CommitId, ActivationId = id.ActivationId,
                ArmedFrame = id.ArmedFrame, ArmedAfterSequence = id.ArmedAfterReceiptSequence, ConsumedSequence = state.ConsumedSequence,
                Kind = (int)state.Kind, Status = (int)state.Status, MomentumAmount = state.MomentumAmount,
                ReferenceSpeed = push.ReferenceSpeedMillimetersPerSecond, ReferenceMass = push.ReferenceMassGrams,
                MinimumMassScale = push.MinimumMassScaleMilli, MaximumMassScale = push.MaximumMassScaleMilli,
                MaximumSpeed = push.MaximumSpeedMillimetersPerSecond };
        }
        internal bool TryRestore(ulong stage, ulong turn, MxFramework.Gameplay.GameplayEntityId actor,
            long frame, ulong sequence, out TacticalAccessoryCombatState state)
        {
            state = default;
            var id = new TacticalAccessoryActivation(Stage, FirstTurn, LastTurn,
                new MxFramework.Gameplay.GameplayEntityId(ActorIndex, ActorGeneration), MainCardInstance,
                AccessoryItemInstanceId, CommitId, ActivationId, ArmedFrame, ArmedAfterSequence);
            var push = new TacticalAccessoryPushParameters(ReferenceSpeed, ReferenceMass, MinimumMassScale, MaximumMassScale, MaximumSpeed);
            if (Stage != stage || id.Actor != actor || FirstTurn > turn || ArmedFrame > frame || ArmedAfterSequence > sequence
                || !TacticalAccessoryCombatState.TryArm((TacticalAccessoryCombatKind)Kind, id, MomentumAmount, push, out state)) return false;
            if (Status == (int)TacticalAccessoryCombatStatus.Consumed)
            {
                if (ConsumedSequence <= ArmedAfterSequence || ConsumedSequence > sequence) return false;
                state.Consume(ConsumedSequence);
            }
            else if (ConsumedSequence != 0) return false;
            else if (Status == (int)TacticalAccessoryCombatStatus.Expired)
            {
                if (turn <= LastTurn) return false;
                state.AdvanceTurn(stage, turn);
            }
            else if (Status != (int)TacticalAccessoryCombatStatus.Armed || turn > LastTurn) return false;
            return true;
        }
    }

    internal struct TacticalAccessoryReceiptData
    {
        public ulong Sequence;
        public TacticalInteractionReceiptData Identity;
        public int SourceKind, Origin, Outcome;
        public int ActionId, TargetMassGrams, TargetPushScaleMilli;
        public bool AttackerWasAlive, TargetWasAlive, TargetRemainsAlive;
        public long DirectionX, DirectionZ;
        internal TacticalInteractionReceipt Key => new TacticalInteractionReceipt(Identity.Stage, 0,
            Identity.Frame, Identity.Attacker, Identity.Target, Identity.ActionInstance, Identity.Trace);
    }

    internal struct TacticalAccessoryConsumptionData
    {
        public ulong ActivationId, Sequence;
        public int MomentumGranted, MomentumSpent, PushX, PushZ;
    }

    internal sealed class TacticalAccessoryAuthoritySnapshot
    {
        public int Version = 1;
        public ulong Sequence;
        public long LastFrame = -1;
        public TacticalAccessoryActivationData[] Activations = Array.Empty<TacticalAccessoryActivationData>();
        public TacticalAccessoryReceiptData[] Receipts = Array.Empty<TacticalAccessoryReceiptData>();
        public TacticalAccessoryConsumptionData[] Consumptions = Array.Empty<TacticalAccessoryConsumptionData>();
        internal TacticalAccessoryAuthoritySnapshot Clone()
        {
            var copy = (TacticalAccessoryAuthoritySnapshot)MemberwiseClone();
            copy.Activations = new TacticalAccessoryActivationData[Activations.Length];
            for (int i = 0; i < Activations.Length; i++) copy.Activations[i] = Activations[i]?.Clone();
            copy.Receipts = (TacticalAccessoryReceiptData[])Receipts.Clone();
            copy.Consumptions = (TacticalAccessoryConsumptionData[])Consumptions.Clone();
            return copy;
        }
        internal bool IsValid(ulong stage, ulong turn, MxFramework.Gameplay.GameplayEntityId actor, long frame)
        {
            if (Version != 1 || stage == 0 || turn == 0 || !actor.IsValid || Activations == null || Receipts == null || Consumptions == null
                || Activations.Length > TacticalInteractionEffectAuthority.MaxAccessoryActivations
                || Receipts.Length > TacticalInteractionEffectAuthority.MaxReceipts || Consumptions.Length > Activations.Length
                || Sequence != (ulong)Receipts.Length || LastFrame < -1 || LastFrame > frame) return false;
            var keys = new HashSet<TacticalInteractionReceipt>();
            for (int i = 0; i < Receipts.Length; i++)
            {
                var r = Receipts[i]; var id = r.Identity;
                if (r.Sequence != (ulong)i + 1 || id.Stage != stage || id.Turn == 0 || id.Turn > turn
                    || id.Frame < 0 || id.Frame > frame || (i > 0 && id.Frame < Receipts[i - 1].Identity.Frame)
                    || !id.Attacker.IsValid || !id.Target.IsValid || id.Attacker == id.Target
                    || (id.Attacker != actor && id.Target != actor) || id.ActionInstance <= 0 || id.Trace <= 0
                    || r.ActionId <= 0 || !r.AttackerWasAlive || !r.TargetWasAlive
                    || r.TargetMassGrams < 0 || r.TargetPushScaleMilli < 0
                    || r.DirectionX < -Fix64.Scale || r.DirectionX > Fix64.Scale || r.DirectionZ < -Fix64.Scale || r.DirectionZ > Fix64.Scale
                    || r.Origin != (int)TacticalAccessoryDamageOrigin.DirectAttack
                    || (r.SourceKind != (int)MxFramework.Combat.Damage.DamageProfileSourceKind.WeaponTrace
                        && r.SourceKind != (int)MxFramework.Combat.Damage.DamageProfileSourceKind.ProjectileImpact)
                    || r.Outcome < (int)MxFramework.Combat.Hit.HitResolveKind.Damage
                    || r.Outcome > (int)MxFramework.Combat.Hit.HitResolveKind.Evaded
                    || !keys.Add(r.Key)) return false;
            }
            if (LastFrame != (Receipts.Length == 0 ? -1 : Receipts[Receipts.Length - 1].Identity.Frame)) return false;
            for (int i = 0; i < Consumptions.Length; i++)
                if (Consumptions[i].Sequence == 0 || Consumptions[i].Sequence > Sequence || (i > 0
                    && (Consumptions[i].Sequence < Consumptions[i - 1].Sequence || Consumptions[i].Sequence == Consumptions[i - 1].Sequence
                        && Consumptions[i].ActivationId <= Consumptions[i - 1].ActivationId))) return false;
            ulong previous = 0; int consumedCount = 0;
            var consumed = new HashSet<ulong>();
            for (int i = 0; i < Activations.Length; i++)
            {
                var a = Activations[i];
                if (a == null || a.ActivationId <= previous || !a.TryRestore(stage, turn, actor, frame, Sequence, out var state)) return false;
                previous = a.ActivationId;
                if (state.Status != TacticalAccessoryCombatStatus.Consumed) continue;
                consumedCount++;
                var r = Receipts[checked((int)state.ConsumedSequence - 1)];
                if (r.Identity.Frame < a.ArmedFrame || r.Identity.Turn < a.FirstTurn || r.Identity.Turn > a.LastTurn) return false;
                bool found = false;
                for (int j = 0; j < Consumptions.Length; j++)
                {
                    var c = Consumptions[j];
                    if (c.ActivationId != a.ActivationId) continue;
                    if (found || !consumed.Add(c.ActivationId) || c.Sequence != a.ConsumedSequence
                        || c.MomentumGranted < 0 || c.MomentumSpent < 0) return false;
                    bool defense = r.Outcome == (int)MxFramework.Combat.Hit.HitResolveKind.Blocked || r.Outcome == (int)MxFramework.Combat.Hit.HitResolveKind.Parried;
                    if (state.Kind == TacticalAccessoryCombatKind.BorrowMomentum
                        && (!defense || !r.TargetRemainsAlive || r.Identity.Target != actor || c.MomentumGranted > a.MomentumAmount || c.MomentumSpent != 0 || c.PushX != 0 || c.PushZ != 0)) return false;
                    if (state.Kind == TacticalAccessoryCombatKind.BreakMomentum
                        && (!defense || !r.TargetRemainsAlive || r.Identity.Attacker != actor || c.MomentumSpent > a.MomentumAmount || c.MomentumGranted != 0 || c.PushX != 0 || c.PushZ != 0)) return false;
                    if (state.Kind == TacticalAccessoryCombatKind.PushAway
                        && (r.Outcome != (int)MxFramework.Combat.Hit.HitResolveKind.Damage || r.Identity.Attacker != actor
                            || c.MomentumGranted != 0 || c.MomentumSpent != 0
                            || Math.Abs((long)c.PushX) > a.MaximumSpeed || Math.Abs((long)c.PushZ) > a.MaximumSpeed)) return false;
                    if (state.Kind == TacticalAccessoryCombatKind.PushAway)
                    {
                        TacticalAccessoryCombatState.TryArm(state.Kind, state.Identity, 0, state.Push, out var probe);
                        var proof = new TacticalAccessoryCombatReceipt(r.Identity.Stage, r.Identity.Turn, r.Identity.Frame, r.Sequence,
                            r.Identity.Attacker, r.Identity.Target, new MxFramework.CharacterAction.CharacterActionId(r.ActionId),
                            r.Identity.ActionInstance, r.Identity.Trace, (MxFramework.Combat.Damage.DamageProfileSourceKind)r.SourceKind,
                            (TacticalAccessoryDamageOrigin)r.Origin, (MxFramework.Combat.Hit.HitResolveKind)r.Outcome,
                            r.AttackerWasAlive, r.TargetWasAlive, r.TargetRemainsAlive,
                            new FixVector3(Fix64.FromRaw(r.DirectionX), Fix64.Zero, Fix64.FromRaw(r.DirectionZ)), r.TargetMassGrams, r.TargetPushScaleMilli);
                        if (TacticalAccessoryCombatRules.Resolve(ref probe, proof, null, null, out var response) != TacticalAccessoryCombatResult.Consumed
                            || response.Push.XRaw != c.PushX || response.Push.ZRaw != c.PushZ) return false;
                    }
                    found = true;
                }
                if (!found) return false;
            }
            return consumedCount == Consumptions.Length;
        }
    }

    internal sealed partial class TacticalInteractionEffectAuthority
    {
        // Only the trusted joint owner may bind this after real payment and batch publication.
        // This binding performs no payment and has no DTO/JSON reconstruction path.
        internal TacticalFixedAccessoryAdmissionAuthority ConfirmedFixedAssembly { get; private set; }
        internal bool HasFixedAssemblyState => _accessories.Count != 0 || ConfirmedFixedAssembly != null;
        internal bool TryBindConfirmedFixedAssembly(TacticalFixedAccessoryAdmissionAuthority authority)
        {
            if (authority == null || !authority.IsRestorable
                || ConfirmedFixedAssembly != null && !authority.Extends(ConfirmedFixedAssembly)
                || !authority.ValidateRestoration(CaptureAccessories(), Stage, Turn, Actor)) return false;
            ConfirmedFixedAssembly = authority;
            return true;
        }
        // Storage safety bound, not a card/slot/stacking rule or a published balance limit.
        internal const int MaxAccessoryActivations = 256;
        private readonly List<TacticalAccessoryCombatState> _accessories = new List<TacticalAccessoryCombatState>(8);
        private readonly List<TacticalAccessoryReceiptData> _accessoryReceipts = new List<TacticalAccessoryReceiptData>(128);
        private readonly List<TacticalAccessoryConsumptionData> _accessoryConsumptions = new List<TacticalAccessoryConsumptionData>(8);
        private readonly HashSet<TacticalInteractionReceipt> _accessorySeen = new HashSet<TacticalInteractionReceipt>();
        // Local preflight freshness only; not gameplay state, a hash field or a persisted counter.
        private ulong _accessoryMutationVersion;
        internal ulong AccessoryReceiptSequence => (ulong)_accessoryReceipts.Count;
        internal int AccessoryActivationCount => _accessories.Count;
        internal TacticalAccessoryCombatState AccessoryAt(int index) => _accessories[index];

        /// <summary>Private frozen batch. The caller prepares every other participant before publishing it.
        /// No simulation step, callback or reentrant admission is allowed between preparation and publication.</summary>
        internal sealed class PreparedAccessoryBatch
        {
            private readonly TacticalInteractionEffectAuthority _owner;
            private readonly TacticalAccessoryCombatState[] _states;
            private readonly ulong _stage, _turn, _sequence, _mutationVersion;
            private readonly MxFramework.Gameplay.GameplayEntityId _actor;
            private readonly int _activationCount;
            private bool _published;

            private PreparedAccessoryBatch(TacticalInteractionEffectAuthority owner, TacticalAccessoryCombatState[] states)
            {
                _owner = owner; _states = states;
                _stage = owner.Stage; _turn = owner.Turn; _actor = owner.Actor;
                _sequence = owner.AccessoryReceiptSequence; _activationCount = owner._accessories.Count;
                _mutationVersion = owner._accessoryMutationVersion;
            }

            internal bool IsCurrent => !_published && _owner.Stage == _stage && _owner.Turn == _turn
                && _owner.Actor == _actor && _owner.AccessoryReceiptSequence == _sequence
                && _owner._accessories.Count == _activationCount && _owner._accessoryMutationVersion == _mutationVersion;

            internal static bool TryCreate(TacticalInteractionEffectAuthority owner,
                IReadOnlyList<TacticalAccessoryCombatState> candidates, ITacticalAccessoryAdmissionAuthority admission,
                out PreparedAccessoryBatch prepared)
            {
                prepared = null;
                if (candidates == null || admission == null || owner.Stage == 0 || owner.Turn == 0 || !owner.Actor.IsValid
                    || owner._accessoryReceipts.Count >= MaxReceipts
                    || candidates.Count > MaxAccessoryActivations - owner._accessories.Count) return false;
                ulong stage = owner.Stage, turn = owner.Turn, sequence = owner.AccessoryReceiptSequence;
                var actor = owner.Actor; int count = owner._accessories.Count;
                ulong version = owner._accessoryMutationVersion;
                var frozen = new TacticalAccessoryCombatState[candidates.Count];
                ulong previous = count == 0 ? 0 : owner._accessories[count - 1].Identity.ActivationId;
                long lastFrame = owner._accessoryReceipts.Count == 0 ? -1
                    : owner._accessoryReceipts[owner._accessoryReceipts.Count - 1].Identity.Frame;
                for (int i = 0; i < frozen.Length; i++)
                {
                    var candidate = candidates[i]; var identity = candidate.Identity;
                    if (candidate.Status != TacticalAccessoryCombatStatus.Armed || candidate.ConsumedSequence != 0
                        || identity.Stage != stage || identity.Actor != actor || identity.FirstTurn != turn
                        || identity.ArmedAfterReceiptSequence != sequence || identity.ArmedFrame < lastFrame
                        || identity.ActivationId <= previous
                        || !TacticalAccessoryCombatState.TryArm(candidate.Kind, identity, candidate.MomentumAmount,
                            candidate.Push, out var canonical)
                        || !admission.ValidateActivation(canonical)) return false;
                    frozen[i] = canonical; previous = identity.ActivationId;
                }
                if (owner.Stage != stage || owner.Turn != turn || owner.Actor != actor
                    || owner.AccessoryReceiptSequence != sequence || owner._accessories.Count != count
                    || owner._accessoryMutationVersion != version) return false;
                // Allocate bounded publication/consumption capacity before the caller's payment boundary.
                int required = count + frozen.Length;
                if (owner._accessories.Capacity < required) owner._accessories.Capacity = required;
                if (owner._accessoryConsumptions.Capacity < required) owner._accessoryConsumptions.Capacity = required;
                prepared = new PreparedAccessoryBatch(owner, frozen);
                return true;
            }

            // This is an invariant assertion, never an ordinary post-payment rejection path.
            internal void Publish()
            {
                if (!IsCurrent) throw new InvalidOperationException("accessory-batch:PreparedBoundaryChanged");
                _owner._accessoryMutationVersion++;
                for (int i = 0; i < _states.Length; i++) _owner._accessories.Add(_states[i]);
                _published = true;
            }
        }

        /// <summary>LowFreqAlloc. Validates the complete ordered batch without publishing activation, receipt or identity.
        /// Fixed definition sorting belongs to the unified commit owner; this owner checks monotonic activation IDs.</summary>
        internal bool TryPrepareAccessoryBatch(IReadOnlyList<TacticalAccessoryCombatState> candidates,
            ITacticalAccessoryAdmissionAuthority admission, out PreparedAccessoryBatch prepared)
            => PreparedAccessoryBatch.TryCreate(this, candidates, admission, out prepared);

        // Low-level admission seam only. A future unified main-card commit must validate frozen references,
        // stacking policy, ledger payment and action admission BEFORE calling this; no legacy card invokes it.
        internal bool TryActivateAccessory(TacticalAccessoryCombatKind kind, in TacticalAccessoryActivation identity,
            int momentumAmount, in TacticalAccessoryPushParameters push, ITacticalAccessoryAdmissionAuthority admission = null)
        {
            if (!TacticalAccessoryCombatState.TryArm(kind, identity, momentumAmount, push, out var state)
                || !TryPrepareAccessoryBatch(new[] { state }, admission, out var prepared)) return false;
            prepared.Publish();
            return true;
        }

        /// <remarks>NoAlloc after bounded buffers warm. Track every certified root from actor binding,
        /// including before the first activation; a later replay cannot receive a fresh post-arm sequence.
        /// Failed motion admission retains the armed state;
        /// the failed root remains deduplicated, so another eligible root may trigger it. No consumed push is dropped.</remarks>
        internal int CommitAccessoryReceipt(in TacticalAccessoryCombatReceipt receipt, IMomentumLedger actorLedger,
            IMomentumLedger defendingLedger, ITacticalAccessoryMotionSink motionSink)
        {
            if (Stage == 0 || receipt.Stage != Stage || receipt.Turn != Turn || !receipt.IsDirectAttack
                || receipt.SourceKind == MxFramework.Combat.Damage.DamageProfileSourceKind.AreaPulse
                || !receipt.Attacker.IsValid || !receipt.Target.IsValid || receipt.Attacker == receipt.Target
                || (receipt.Attacker != Actor && receipt.Target != Actor) || !receipt.ActionId.IsValid
                || receipt.ActionInstance <= 0 || receipt.Trace <= 0 || receipt.Frame < 0
                || !receipt.AttackerWasAlive || !receipt.TargetWasAlive || receipt.TargetMassGrams < 0 || receipt.TargetPushScaleMilli < 0
                || receipt.AttackDirection.X < -Fix64.One || receipt.AttackDirection.X > Fix64.One
                || receipt.AttackDirection.Z < -Fix64.One || receipt.AttackDirection.Z > Fix64.One
                || receipt.Outcome < MxFramework.Combat.Hit.HitResolveKind.Damage
                || receipt.Outcome > MxFramework.Combat.Hit.HitResolveKind.Evaded) return 0;
            var id = new TacticalInteractionReceipt(Stage, Turn, receipt.Frame, receipt.Attacker, receipt.Target, receipt.ActionInstance, receipt.Trace);
            var key = new TacticalInteractionReceipt(Stage, 0, receipt.Frame, receipt.Attacker, receipt.Target, receipt.ActionInstance, receipt.Trace);
            if (_accessorySeen.Contains(key) || _accessoryReceipts.Count == MaxReceipts || receipt.Sequence != AccessoryReceiptSequence + 1
                || (_accessoryReceipts.Count > 0 && receipt.Frame < _accessoryReceipts[_accessoryReceipts.Count - 1].Identity.Frame)) return 0;
            _accessoryMutationVersion++;
            _accessorySeen.Add(key);
            _accessoryReceipts.Add(new TacticalAccessoryReceiptData { Sequence = receipt.Sequence, Identity = new TacticalInteractionReceiptData(id),
                SourceKind = (int)receipt.SourceKind, Origin = (int)receipt.Origin, Outcome = (int)receipt.Outcome,
                ActionId = receipt.ActionId.Value, TargetMassGrams = receipt.TargetMassGrams, TargetPushScaleMilli = receipt.TargetPushScaleMilli,
                AttackerWasAlive = receipt.AttackerWasAlive, TargetWasAlive = receipt.TargetWasAlive, TargetRemainsAlive = receipt.TargetRemainsAlive,
                DirectionX = receipt.AttackDirection.X.RawValue, DirectionZ = receipt.AttackDirection.Z.RawValue });
            int consumed = 0;
            for (int i = 0; i < _accessories.Count; i++)
            {
                var next = _accessories[i];
                var outcome = TacticalAccessoryCombatRules.Resolve(ref next, receipt, actorLedger, defendingLedger, out var response);
                if (outcome != TacticalAccessoryCombatResult.Consumed) { _accessories[i] = next; continue; }
                // Evaluate on a local copy. Consumption is published only AFTER the target buffer acknowledges a push.
                if (response.Push.HasValue && (motionSink == null || !motionSink.TryEnqueue(response.Push))) continue;
                _accessories[i] = next;
                _accessoryConsumptions.Add(new TacticalAccessoryConsumptionData { ActivationId = next.Identity.ActivationId, Sequence = receipt.Sequence,
                    MomentumGranted = response.MomentumGranted, MomentumSpent = response.MomentumSpent,
                    PushX = response.Push.XRaw, PushZ = response.Push.ZRaw });
                consumed++;
            }
            return consumed;
        }

        private static bool ValidateAccessoryAdmission(TacticalInteractionSnapshot snapshot, ITacticalAccessoryAdmissionAuthority admission)
        {
            // A cursor-only fragment contains no frozen card/effect identity and is compatible with
            // the legacy paused envelope. Actual activations still require the immutable P2 owner.
            // If provided, that owner can also reject an omitted/null activation manifest.
            return admission == null ? snapshot.Accessories == null || snapshot.Accessories.Activations.Length == 0
                : admission.ValidateRestoration(snapshot.Accessories, snapshot.Stage, snapshot.Turn, snapshot.Actor);
        }
        private void ClearAccessories()
        { _accessoryMutationVersion++; _accessories.Clear(); _accessoryReceipts.Clear(); _accessorySeen.Clear(); _accessoryConsumptions.Clear(); }
        private void AdvanceAccessories(ulong turn)
        { _accessoryMutationVersion++; for (int i = 0; i < _accessories.Count; i++) { var state = _accessories[i]; state.AdvanceTurn(Stage, turn); _accessories[i] = state; } }
        private TacticalAccessoryAuthoritySnapshot CaptureAccessories()
        {
            if (_accessories.Count == 0 && _accessoryReceipts.Count == 0) return null;
            var data = new TacticalAccessoryActivationData[_accessories.Count];
            for (int i = 0; i < data.Length; i++) data[i] = TacticalAccessoryActivationData.From(_accessories[i]);
            return new TacticalAccessoryAuthoritySnapshot { Sequence = AccessoryReceiptSequence,
                LastFrame = _accessoryReceipts.Count == 0 ? -1 : _accessoryReceipts[_accessoryReceipts.Count - 1].Identity.Frame,
                Activations = data, Receipts = _accessoryReceipts.ToArray(), Consumptions = _accessoryConsumptions.ToArray() };
        }
        private void RestoreAccessories(TacticalAccessoryAuthoritySnapshot snapshot, long frame)
        {
            ClearAccessories(); if (snapshot == null) return;
            foreach (var data in snapshot.Activations) { data.TryRestore(Stage, Turn, Actor, frame, snapshot.Sequence, out var state); _accessories.Add(state); }
            _accessoryReceipts.AddRange(snapshot.Receipts); _accessoryConsumptions.AddRange(snapshot.Consumptions);
            foreach (var data in snapshot.Receipts) _accessorySeen.Add(data.Key);
        }
        private void AddAccessoryHash(ref RuntimeHashAccumulator hash)
        {
            if (_accessories.Count == 0 && _accessoryReceipts.Count == 0) return; // Existing five-effect hashes remain identical.
            // Local owner field 356 wraps one array element. No global schema is reused or invented.
            hash.BeginArray(TacticalInteractionHashFields.Accessories, 1);
            hash.BeginElement(0);
            hash.AddLong(1, unchecked((long)AccessoryReceiptSequence));
            hash.BeginArray(2, _accessories.Count);
            for (int i = 0; i < _accessories.Count; i++)
            {
                hash.BeginElement(i); var s = _accessories[i]; var id = s.Identity; var p = s.Push;
                hash.AddLong(1, unchecked((long)id.Stage)); hash.AddLong(2, unchecked((long)id.FirstTurn)); hash.AddLong(3, unchecked((long)id.LastTurn));
                hash.AddInt(4, id.Actor.Index); hash.AddInt(5, id.Actor.Generation); hash.AddLong(6, unchecked((long)id.MainCardInstance));
                hash.AddInt(7, id.AccessoryItemInstanceId); hash.AddLong(8, unchecked((long)id.CommitId)); hash.AddLong(9, unchecked((long)id.ActivationId));
                hash.AddLong(10, id.ArmedFrame); hash.AddLong(11, unchecked((long)id.ArmedAfterReceiptSequence));
                hash.AddInt(12, (int)s.Kind); hash.AddInt(13, (int)s.Status); hash.AddInt(14, s.MomentumAmount); hash.AddLong(15, unchecked((long)s.ConsumedSequence));
                hash.AddInt(16, p.ReferenceSpeedMillimetersPerSecond); hash.AddInt(17, p.ReferenceMassGrams); hash.AddInt(18, p.MinimumMassScaleMilli);
                hash.AddInt(19, p.MaximumMassScaleMilli); hash.AddInt(20, p.MaximumSpeedMillimetersPerSecond); hash.EndElement();
            }
            hash.EndArray(); hash.BeginArray(3, _accessoryReceipts.Count);
            for (int i = 0; i < _accessoryReceipts.Count; i++)
            {
                hash.BeginElement(i); var r = _accessoryReceipts[i]; var id = r.Identity;
                hash.AddLong(1, unchecked((long)r.Sequence)); hash.AddLong(2, unchecked((long)id.Stage)); hash.AddLong(3, unchecked((long)id.Turn)); hash.AddLong(4, id.Frame);
                hash.AddInt(5, id.Attacker.Index); hash.AddInt(6, id.Attacker.Generation); hash.AddInt(7, id.Target.Index); hash.AddInt(8, id.Target.Generation);
                hash.AddInt(9, id.ActionInstance); hash.AddInt(10, id.Trace); hash.AddInt(11, r.SourceKind); hash.AddInt(12, r.Origin); hash.AddInt(13, r.Outcome);
                hash.AddLong(14, r.DirectionX); hash.AddLong(15, r.DirectionZ); hash.AddInt(16, r.ActionId);
                hash.AddInt(17, r.TargetMassGrams); hash.AddInt(18, r.TargetPushScaleMilli); hash.AddBool(19, r.AttackerWasAlive);
                hash.AddBool(20, r.TargetWasAlive); hash.AddBool(21, r.TargetRemainsAlive); hash.EndElement();
            }
            hash.EndArray(); hash.BeginArray(4, _accessoryConsumptions.Count);
            for (int i = 0; i < _accessoryConsumptions.Count; i++)
            {
                hash.BeginElement(i); var c = _accessoryConsumptions[i]; hash.AddLong(1, unchecked((long)c.ActivationId)); hash.AddLong(2, unchecked((long)c.Sequence));
                hash.AddInt(3, c.MomentumGranted); hash.AddInt(4, c.MomentumSpent); hash.AddInt(5, c.PushX); hash.AddInt(6, c.PushZ); hash.EndElement();
            }
            hash.EndArray(); hash.EndElement(); hash.EndArray();
        }
    }
}
