using System;
using System.Collections.Generic;
using MxFramework.Combat.Hit;
using MxFramework.Gameplay;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>A single confirmed commit's leaf data, held by the EXISTING effect/commit owner.
    /// No admission, payment, clock, damage applier, command queue, independent card play or snapshot protocol.
    /// Keep this state after host completion while descendants exist; stage teardown invalidates it.
    /// The shared owner remains responsible for atomic publication, storage limits, hashing and recovery.</summary>
    internal sealed class TacticalAccessorySecondBatchState
    {
        private readonly List<TacticalSecondBatchSource> _sources = new List<TacticalSecondBatchSource>();
        private readonly List<TacticalSecondBatchHit> _receipts = new List<TacticalSecondBatchHit>();
        private readonly List<GameplayEntityId> _interruptAttempts = new List<GameplayEntityId>();
        private bool _invalidated, _hostEnded, _rearRevoked, _onePunchConsumed;
        private TacticalSecondBatchHitKey _lastOrder, _winner;
        private long _closedFrame = -1;
        private int _closedRound = -1;

        private TacticalAccessorySecondBatchState(in TacticalDoubleEdgeCommitKey commit, ulong turn,
            in TacticalSecondBatchParameters parameters)
        { Commit = commit; ActivatedTurn = turn; Parameters = parameters; }
        internal TacticalDoubleEdgeCommitKey Commit { get; }
        internal ulong ActivatedTurn { get; }
        internal TacticalSecondBatchParameters Parameters { get; }
        internal bool OnePunchConsumed => _onePunchConsumed;
        internal bool SuppressFutureAttackJudgments => !_invalidated && Parameters.OnePunch && _onePunchConsumed;
        internal TacticalSecondBatchHitKey Winner => _winner;
        internal int SourceCount => _sources.Count;
        internal int InterruptAttemptCount => _interruptAttempts.Count;
        internal int ReceiptCount => _receipts.Count;
        // Read-only leaf data for the shared owner's existing numeric hash/projection contract. No IDs minted here.
        internal bool IsInvalidated => _invalidated;
        internal bool HostEnded => _hostEnded;
        internal bool RearRevoked => _rearRevoked;
        internal TacticalSecondBatchHitKey LastOrder => _lastOrder;
        internal long ClosedFrame => _closedFrame;
        internal int ClosedRound => _closedRound;
        internal TacticalSecondBatchSource SourceAt(int index) => _sources[index];
        internal TacticalSecondBatchHit ReceiptAt(int index) => _receipts[index];
        internal GameplayEntityId InterruptAttemptAt(int index) => _interruptAttempts[index];

        /// <remarks>LowFreqAlloc. Only the existing joint owner calls this AFTER confirmed unified main-card payment.
        /// A valid value identity alone does not prove payment; this method does not create an admission receipt.</remarks>
        internal static bool TryCreateForConfirmedCommit(in TacticalDoubleEdgeCommitKey commit, ulong turn,
            in TacticalSecondBatchParameters parameters, out TacticalAccessorySecondBatchState state)
        {
            state = null;
            if (!commit.IsValid || turn == 0 || !parameters.IsValid) return false;
            state = new TacticalAccessorySecondBatchState(commit, turn, parameters); return true;
        }

        /// <remarks>LowFreqAlloc at actual action acknowledgement. Caller supplies a stable producer node ID.
        /// Repeated exact registration is idempotent; a pooled action must change revision.</remarks>
        internal bool TryRegisterRoot(ulong node, int actionInstance, ulong actionRevision, long frame,
            out TacticalSecondBatchSource source)
        {
            source = default;
            var candidate = new TacticalSecondBatchSource(Commit, node, 0, Commit.Actor, actionInstance, actionRevision, frame);
            if (_invalidated || !candidate.IsValid) return false;
            int existing = FindNode(node);
            if (existing >= 0) { if (!_sources[existing].Matches(candidate)) return false; source = _sources[existing]; return true; }
            if (_hostEnded || SuppressFutureAttackJudgments || !BirthIsOpen(frame, 0)) return false;
            for (int i = 0; i < _sources.Count; i++)
                if (_sources[i].Parent == 0 && _sources[i].RootActionInstance == actionInstance
                    && (_sources[i].RootActionRevision >= actionRevision || _sources[i].CreatedFrame > frame)) return false;
            AddSourceInNodeOrder(candidate); source = candidate; return true;
        }

        /// <remarks>LowFreqAlloc at non-hit production, including AFTER host completion. Birth inherits its parent's
        /// still-open same-frame round, or round zero on a new frame. Closed-round late births are rejected;
        /// actual on-hit births must instead give TryRegisterDerivedAfterHit their exact accepted parent receipt.</remarks>
        internal bool TryRegisterDerived(in TacticalSecondBatchSource parent, ulong node, GameplayEntityId producer,
            long frame, out TacticalSecondBatchSource source)
        {
            source = default;
            if (!Owns(parent) || frame < parent.CreatedFrame) return false;
            var candidate = new TacticalSecondBatchSource(Commit, node, parent.Node, producer,
                parent.RootActionInstance, parent.RootActionRevision, frame, parent.RoundAt(frame));
            return TryRegisterBirth(candidate, out source);
        }

        /// <remarks>LowFreqAlloc at actual on-hit production. Same-frame children belong to parent round +1;
        /// delayed children belong to their actual new frame's round zero. No implicit causal inference or frame deferral.</remarks>
        internal bool TryRegisterDerivedAfterHit(in TacticalSecondBatchSource parent,
            in TacticalSecondBatchHitKey acceptedParent, ulong node, GameplayEntityId producer, long frame,
            out TacticalSecondBatchSource source)
        {
            source = default;
            if (!Owns(parent) || !acceptedParent.IsValid || frame < acceptedParent.Frame) return false;
            int receipt = FindReceipt(acceptedParent);
            if (receipt < 0 || !_receipts[receipt].Source.Matches(parent) || !_receipts[receipt].ConsumesUniqueHit
                || !_receipts[receipt].TargetAlive || Parameters.OnePunch && _winner.CompareTo(acceptedParent) != 0) return false;
            int round = 0;
            if (frame == acceptedParent.Frame
                && !TacticalAccessorySecondBatchRules.TryNextCausalRound(acceptedParent.CausalRound, out round)) return false;
            var candidate = new TacticalSecondBatchSource(Commit, node, parent.Node, producer,
                parent.RootActionInstance, parent.RootActionRevision, frame, round, acceptedParent);
            return TryRegisterBirth(candidate, out source);
        }

        private bool TryRegisterBirth(in TacticalSecondBatchSource candidate, out TacticalSecondBatchSource source)
        {
            source = default;
            if (!candidate.IsValid) return false;
            ulong node = candidate.Node;
            int existing = FindNode(node);
            if (existing >= 0) { if (!_sources[existing].Matches(candidate)) return false; source = _sources[existing]; return true; }
            if (!BirthIsOpen(candidate.CreatedFrame, candidate.BirthRound)) return false;
            AddSourceInNodeOrder(candidate); source = candidate; return true;
        }

        // Stable numeric source view for the shared hash/projection owner, independent of producer enumeration.
        private void AddSourceInNodeOrder(in TacticalSecondBatchSource source)
        {
            int index = _sources.Count;
            while (index > 0 && _sources[index - 1].Node > source.Node) index--;
            _sources.Insert(index, source);
        }

        private bool BirthIsOpen(long frame, int round) => frame > _closedFrame
            || frame == _closedFrame && round > _closedRound;

        /// <remarks>NoAlloc. This also gates already airborne projectiles, future windows and queued attack requests.
        /// The owner must preserve animation, locomotion, payment and ordinary host completion.</remarks>
        internal bool Suppresses(in TacticalSecondBatchSource source) => Owns(source) && SuppressFutureAttackJudgments;

        /// <remarks>NoAlloc. Turn bonus survives host end, but never actor departure/death.</remarks>
        internal int RearScale(in TacticalDoubleEdgeDamageFacts facts, in TacticalSecondBatchDirection direction)
            => TacticalAccessorySecondBatchRules.RearScale(Commit, ActivatedTurn,
                !_invalidated && !_rearRevoked && Parameters.NoTurningBack, Parameters.RearScaleMilli, facts, direction);
        /// <remarks>NoAlloc. Fold qualifying rear rules with MAX, including when both participants are armed.
        /// Rear is one factor per accepted root, never a product of the attacking and defending copies.</remarks>
        internal int AccumulateRearScale(int existingRearScaleMilli, in TacticalDoubleEdgeDamageFacts facts,
            in TacticalSecondBatchDirection direction)
            => Math.Max(Math.Max(1000, existingRearScaleMilli), RearScale(facts, direction));
        internal void RevokeRearOnActorDeparture() { _rearRevoked = true; }
        internal void EndHost() { _hostEnded = true; }
        internal void InvalidateStage() { _invalidated = true; }

        /// <summary>Compatibility wrapper for a complete, nonempty causal round. Replays are read-only.
        /// The public owner uses TryResolveRound/TryCloseCausalRound explicitly, including EMPTY rounds.</summary>
        /// <remarks>LowFreqAlloc at new receipt/target high-water growth.</remarks>
        internal bool TryResolveBatch(TacticalSecondBatchHit[] hits, int count, TacticalSecondBatchHitDecision[] decisions)
        {
            if (!ValidBuffers(hits, count, decisions)) return false;
            for (int i = 0; i < count; i++)
                if (FindReceipt(hits[i].Key) < 0)
                    return TryResolveRound(hits[i].Key.Frame, hits[i].Key.CausalRound, hits, count, decisions);
            return TryReplayBatch(hits, count, decisions);
        }

        /// <summary>PRE-PUBLICATION classification of one COMPLETE causal round across all producers.
        /// Round zero contains the frame's existing production; on-hit descendants are gathered for the next round
        /// in the SAME simulation frame. Explicit arguments close even an empty round. Stable-key sort writes
        /// decisions in sorted-hit order. Same-round late new candidates are rejected, regardless of event order.</summary>
        /// <remarks>LowFreqAlloc at receipt/target high-water growth. Single-threaded and non-reentrant with the shared
        /// owner; no dispatch or world mutation. Publish this candidate leaf atomically with the shared hit transaction.</remarks>
        internal bool TryResolveRound(long frame, int causalRound, TacticalSecondBatchHit[] hits, int count,
            TacticalSecondBatchHitDecision[] decisions)
        {
            if (!ValidBuffers(hits, count, decisions) || !ValidRoundIdentity(frame, causalRound)) return false;
            // Check the declared phase before EVERY branch, including known receipts and the latest closed replay.
            // A replay is evidence for its original phase, never authorization to close another round or frame.
            for (int i = 0; i < count; i++)
                if (hits[i].Key.Frame != frame || hits[i].Key.CausalRound != causalRound) return false;
            if (frame < _closedFrame || frame == _closedFrame && causalRound < _closedRound) {
                // Old rounds/frames remain replayable through the explicit protocol too, without reopening them.
                if (count == 0) return false;
                for (int i = 0; i < count; i++)
                    if (hits[i].Key.Frame != frame || hits[i].Key.CausalRound != causalRound) return false;
                return TryReplayBatch(hits, count, decisions);
            }
            if (frame == _closedFrame && causalRound == _closedRound) return TryReplayBatch(hits, count, decisions);
            if (!CanCloseRound(frame, causalRound)) return false;
            for (int i = 0; i < count; i++) if (!Valid(hits[i])) return false;
            for (int i = 1; i < count; i++) {
                var next = hits[i]; int j = i - 1;
                while (j >= 0 && hits[j].Key.CompareTo(next.Key) > 0) { hits[j + 1] = hits[j]; j--; }
                hits[j + 1] = next;
            }
            // Validate the entire batch before consuming anything, including altered replays and late arrivals.
            for (int i = 0; i < count; i++) {
                int seen = FindReceipt(hits[i].Key);
                if (seen >= 0 ? !_receipts[seen].Matches(hits[i])
                    : hits[i].Key.Frame != frame || hits[i].Key.CausalRound != causalRound
                        || _lastOrder.IsValid && hits[i].Key.CompareTo(_lastOrder) <= 0) return false;
                if (i > 0 && hits[i - 1].Key.CompareTo(hits[i].Key) == 0 && !hits[i - 1].Matches(hits[i])) return false;
            }
            for (int i = 0; i < count; i++) {
                var hit = hits[i];
                if (FindReceipt(hit.Key) >= 0) {
                    decisions[i] = new TacticalSecondBatchHitDecision(true, true, 1000, false, false); continue;
                }
                bool suppress = SuppressFutureAttackJudgments;
                int scale = 1000; bool interrupt = false;
                bool nonLethal = !suppress && Parameters.NonLethalOath && hit.Outcome == HitResolveKind.Damage;
                if (!suppress && hit.TargetAlive && hit.ConsumesUniqueHit) {
                    if (Parameters.OnePunch) {
                        _onePunchConsumed = true; _winner = hit.Key;
                        if (hit.Outcome == HitResolveKind.Damage) scale = Parameters.OnePunchScaleMilli;
                    }
                    if (nonLethal && hit.ActualEnemy && !AttemptedInterrupt(hit.Key.Target)) {
                        _interruptAttempts.Add(hit.Key.Target); interrupt = true;
                    }
                }
                _receipts.Add(hit); _lastOrder = hit.Key;
                decisions[i] = new TacticalSecondBatchHitDecision(false, suppress, scale, nonLethal, interrupt);
            }
            _closedFrame = frame; _closedRound = causalRound; return true;
        }

        /// <remarks>NoAlloc. Explicitly close a complete EMPTY round, or idempotently confirm the latest closure.
        /// Within a frame closures are consecutive (0,1,...); a new frame starts at zero. Never skip a round.</remarks>
        internal bool TryCloseCausalRound(long frame, int causalRound)
        {
            if (_invalidated || !ValidRoundIdentity(frame, causalRound)) return false;
            if (frame == _closedFrame && causalRound == _closedRound) return true;
            if (!CanCloseRound(frame, causalRound)) return false;
            _closedFrame = frame; _closedRound = causalRound; return true;
        }
        /// <remarks>NoAlloc. The stage validates every leaf before publishing any receipt.
        /// Does not reorder input or change authority, decisions, watermarks or capacity.</remarks>
        internal bool CanResolveRound(long frame, int round, TacticalSecondBatchHit[] hits, int count,
            TacticalSecondBatchHitDecision[] decisions)
        {
            if (!ValidBuffers(hits, count, decisions) || !ValidRoundIdentity(frame, round)) return false;
            bool closed = frame < _closedFrame || frame == _closedFrame && round <= _closedRound;
            if (closed && count == 0 && (frame != _closedFrame || round != _closedRound)) return false;
            if (!closed && !CanCloseRound(frame, round)) return false;
            for (int i = 0; i < count; i++) {
                var hit = hits[i];
                if (hit.Key.Frame != frame || hit.Key.CausalRound != round || !Valid(hit)) return false;
                int known = FindReceipt(hit.Key);
                if (known >= 0 ? !_receipts[known].Matches(hit)
                    : closed || _lastOrder.IsValid && hit.Key.CompareTo(_lastOrder) <= 0) return false;
                for (int j = 0; j < i; j++)
                    if (hits[j].Key.CompareTo(hit.Key) == 0 && !hits[j].Matches(hit)) return false;
            }
            return true;
        }

        /// <remarks>LowFreqAlloc at high-water growth, before the stage's non-reentrant publication.</remarks>
        internal void ReserveRound(int count)
        {
            int receipts = checked(_receipts.Count + count), attempts = checked(_interruptAttempts.Count + count);
            if (_receipts.Capacity < receipts) _receipts.Capacity = receipts;
            if (_interruptAttempts.Capacity < attempts) _interruptAttempts.Capacity = attempts;
        }

        private bool ValidRoundIdentity(long frame, int round) => frame >= Commit.Frame && frame < long.MaxValue
            && round >= 0 && round < int.MaxValue;
        private bool CanCloseRound(long frame, int round) => ValidRoundIdentity(frame, round) && (frame > _closedFrame
                ? round == 0 && !HasUnclosedBirthRound()
                : frame == _closedFrame && _closedRound < int.MaxValue - 1 && round == _closedRound + 1);
        private bool HasUnclosedBirthRound()
        {
            for (int i = 0; i < _sources.Count; i++)
                if (_sources[i].CreatedFrame == _closedFrame && _sources[i].BirthRound > _closedRound) return true;
            return false;
        }
        private bool ValidBuffers(TacticalSecondBatchHit[] hits, int count, TacticalSecondBatchHitDecision[] decisions)
            => !_invalidated && hits != null && decisions != null && count >= 0 && count <= hits.Length && count <= decisions.Length;
        private bool TryReplayBatch(TacticalSecondBatchHit[] hits, int count, TacticalSecondBatchHitDecision[] decisions)
        {
            for (int i = 0; i < count; i++) {
                int receipt = FindReceipt(hits[i].Key);
                if (!Valid(hits[i]) || receipt < 0 || !_receipts[receipt].Matches(hits[i])) return false;
            }
            for (int i = 0; i < count; i++) decisions[i] = new TacticalSecondBatchHitDecision(true, true, 1000, false, false);
            return true;
        }

        private bool Valid(in TacticalSecondBatchHit hit) => Owns(hit.Source) && hit.Key.IsValid
            && hit.Key.SourceNode == hit.Source.Node && hit.Key.Frame >= hit.Source.CreatedFrame
            && hit.Key.CausalRound == hit.Source.RoundAt(hit.Key.Frame)
            && hit.ActualProducer == hit.Source.Producer
            // Real SelfDamage (or an earlier Duplicate/Evaded rejection) is a non-consuming classification,
            // not invalid evidence that poisons other candidates. Never allow a defended/accepted self attack.
            && (!hit.ConsumesUniqueHit || hit.ActualProducer != hit.Key.Target)
            && (!hit.ConsumesUniqueHit || hit.TargetAlive)
            && (int)hit.Outcome >= (int)HitResolveKind.Damage && (int)hit.Outcome <= (int)HitResolveKind.Evaded;

        private bool Owns(in TacticalSecondBatchSource source)
        {
            if (_invalidated || !source.IsValid || !Commit.Matches(source.Commit)) return false;
            int index = FindNode(source.Node); return index >= 0 && _sources[index].Matches(source);
        }
        private int FindNode(ulong node)
        { for (int i = 0; i < _sources.Count; i++) if (_sources[i].Node == node) return i; return -1; }
        private int FindReceipt(in TacticalSecondBatchHitKey key)
        { for (int i = 0; i < _receipts.Count; i++) if (_receipts[i].Key.CompareTo(key) == 0) return i; return -1; }
        private bool AttemptedInterrupt(GameplayEntityId target)
        { for (int i = 0; i < _interruptAttempts.Count; i++) if (_interruptAttempts[i] == target) return true; return false; }

        /// <remarks>AllocByDesign. Detached trusted in-process pause copy only, not transport restoration/admission.
        /// The common owner still implements the one shared snapshot/hash contract.</remarks>
        internal TacticalAccessorySecondBatchState CloneForTrustedPause()
        {
            var copy = new TacticalAccessorySecondBatchState(Commit, ActivatedTurn, Parameters) {
                _invalidated = _invalidated, _hostEnded = _hostEnded, _rearRevoked = _rearRevoked,
                _onePunchConsumed = _onePunchConsumed, _lastOrder = _lastOrder, _winner = _winner,
                _closedFrame = _closedFrame, _closedRound = _closedRound
            };
            copy._sources.AddRange(_sources); copy._receipts.AddRange(_receipts);
            copy._interruptAttempts.AddRange(_interruptAttempts); return copy;
        }
    }
}
