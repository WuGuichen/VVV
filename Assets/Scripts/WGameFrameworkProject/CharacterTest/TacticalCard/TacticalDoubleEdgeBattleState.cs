using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest.TacticalCard
{
    /// <summary>Stage-owned state. Only the joint submission owner publishes prepared entries.
    /// Transported projections are deliberately never accepted as authority.</summary>
    internal sealed partial class TacticalDoubleEdgeBattleState
    {
        internal const int MaximumFields = 4;
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<ProjectileBirth> _projectiles = new List<ProjectileBirth>();
        private ulong _stage, _turn, _revision;
        internal int TimelineNextInstanceId { get; private set; } = 1;
        internal void RecordTimelineIdentity(int next)
        {
            if (next < TimelineNextInstanceId) throw new InvalidOperationException("accessory:TimelineIdentityRewind");
            if (next != TimelineNextInstanceId) { TimelineNextInstanceId = next; _revision++; }
        }
        internal ulong Stage => _stage;
        internal ulong Turn => _turn;
        internal bool HasState => _entries.Count != 0 || _stage != 0 && (_thirdExplicitSeed || _projectileActors.Count != 0
            || _pendingProjectileActors.Count != 0 || _timelineActors.Count != 0 || TimelineNextInstanceId != 1
            || _thirdAttacks.Count != 0 || _thirdSources.Count != 0 || _thirdDeparted.Count != 0);
        internal int EntryCount => _entries.Count;

        internal void Bind(ulong stage, ulong turn, uint? coinSeed = null)
        {
            if (stage == 0 || turn == 0 || _stage != 0) throw new InvalidOperationException("double-edge:StageBinding");
            _stage = stage; _turn = turn; _thirdExplicitSeed = coinSeed.HasValue; _coinLedger = new TacticalThirdBatchCoinLedger(stage, coinSeed.GetValueOrDefault()); _revision++;
        }

        internal void Clear()
        { foreach (var entry in _entries) entry.SecondBatch?.InvalidateStage();
            ClearThirdState();
            _entries.Clear(); _projectiles.Clear(); _producerBindings.Clear(); _pendingAccessoryProjectiles.Clear(); _buffLineages.Clear(); _projectileActors.Clear(); _pendingProjectileActors.Clear(); _timelineActors.Clear(); _stage = _turn = 0; _revision++; }

        /// <remarks>NoAlloc. Called at actual turn settlement, after all damage/physics of the completed step.</remarks>
        internal bool AdvanceTurn(ulong stage, ulong turn)
        {
            if (stage != _stage || stage == 0 || turn < _turn) return false;
            if (turn != _turn) {
                foreach (var entry in _entries) {
                    var leaf = entry.SecondBatch;
                    if (leaf != null && HasUnclosedBirth(leaf.ClosedFrame, leaf.ClosedRound + 1)) return false;
                }
                _turn = turn; _revision++;
            }
            return true;
        }

        internal int OccupiedFieldSlots
        {
            get {
                int count = 0;
                for (int i = 0; i < _entries.Count; i++) {
                    var entry = _entries[i];
                    if (entry.Parameters.BlackHole && (!entry.Terminal
                        || entry.Generated && _turn <= entry.Turn + 1)) count++;
                }
                return count;
            }
        }

        /// <remarks>LowFreqAlloc. No payment, field reservation, or authority write occurs during preparation.
        /// The caller proves actual enemy, actual player deck and joint payment before publication.</remarks>
        internal bool TryPrepare(in TacticalDoubleEdgeCommitKey key, ulong turn, GameplayEntityId opponent,
            in TacticalDoubleEdgeParameters parameters, bool actualEnemy, bool realPlayerDeck, out Prepared prepared)
            => TryPrepare(key, turn, opponent, parameters, default, actualEnemy, realPlayerDeck, out prepared);

        internal bool TryPrepare(in TacticalDoubleEdgeCommitKey key, ulong turn, GameplayEntityId opponent,
            in TacticalDoubleEdgeParameters parameters, in TacticalSecondBatchParameters secondBatch,
            bool actualEnemy, bool realPlayerDeck, out Prepared prepared)
            => TryPrepare(key, turn, opponent, parameters, secondBatch, default, actualEnemy, realPlayerDeck, out prepared);

        internal bool TryPrepare(in TacticalDoubleEdgeCommitKey key, ulong turn, GameplayEntityId opponent,
            in TacticalDoubleEdgeParameters parameters, in TacticalSecondBatchParameters secondBatch,
            in TacticalThirdBatchParameters thirdBatch, bool actualEnemy, bool realPlayerDeck, out Prepared prepared)
        {
            prepared = null;
            if (!key.IsValid || key.Stage != _stage || turn != _turn || (!parameters.IsValid && !secondBatch.IsValid && !thirdBatch.IsValid)
                || !parameters.IsValid && !parameters.IsNeutral
                || !secondBatch.IsValid && (secondBatch.NoTurningBack || secondBatch.NonLethalOath || secondBatch.OnePunch)
                || thirdBatch.IsValid && (turn == ulong.MaxValue || _thirdRoundOpen || key.Frame < _thirdFrame || key.Execution >= (1UL << 56) || turn >= (1UL << 56) || _thirdNextCommit == ulong.MaxValue)
                || thirdBatch.LuckyCoin && _coinLedger.DrawCount == long.MaxValue
                || thirdBatch.LuckyCoin && (!actualEnemy || !opponent.IsValid || opponent == key.Actor)
                || parameters.Shattered && !realPlayerDeck
                || parameters.Duel && (!actualEnemy || !opponent.IsValid || opponent == key.Actor)
                || parameters.BlackHole && (turn == ulong.MaxValue || OccupiedFieldSlots >= MaximumFields)) return false;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Key.Actor == key.Actor && (_entries[i].Key.Commit == key.Commit
                    || _entries[i].Key.Execution == key.Execution
                    || _entries[i].Parameters.Shattered && _entries[i].Key.BattleCardInstance == key.BattleCardInstance)) return false;
            if (_entries.Capacity < _entries.Count + 1) _entries.Capacity = Math.Max(4, _entries.Count * 2);
            prepared = new Prepared(this, key, turn, opponent, parameters, secondBatch, thirdBatch); return true;
        }

        internal sealed class Prepared
        {
            private readonly TacticalDoubleEdgeBattleState _owner;
            private readonly Entry _entry;
            private readonly ulong _revision;
            private bool _published;
            private readonly TacticalThirdBatchCoinLedger _coinCandidate;
            internal Prepared(TacticalDoubleEdgeBattleState owner, in TacticalDoubleEdgeCommitKey key,
                ulong turn, GameplayEntityId opponent, in TacticalDoubleEdgeParameters parameters, in TacticalSecondBatchParameters secondBatch,
                in TacticalThirdBatchParameters thirdBatch)
            {
                _owner = owner; _entry = new Entry(key, turn, opponent, parameters) { SecondBatchParameters = secondBatch, ThirdBatchParameters = thirdBatch };
                if (thirdBatch.IsValid) {
                    var order = ThirdOrder(key.Frame, owner.ThirdBirthRound(key.Frame), 0, 1,
                        ThirdNode(key.Actor, 34, (long)key.Execution, 0, key.Commit), key.Actor, key.Commit);
                    var commit = new TacticalThirdBatchCommit(key.Stage, owner._thirdNextCommit, key.Execution, key.Actor, key.BattleCardInstance, turn, order);
                    TacticalThirdBatchCoinReceipt receipt = default;
                    if (thirdBatch.LuckyCoin) {
                        _coinCandidate = owner._coinLedger.CloneForTrustedPause();
                        if (!_coinCandidate.TryPublish(true, commit, opponent, out receipt)) throw new InvalidOperationException("third-batch:CoinPreparationRejected");
                    }
                    _entry.ThirdBatch = new TacticalAccessoryThirdBatchState(commit, thirdBatch.GlassCannon, thirdBatch.IronThrone, receipt);
                }
                _revision = owner._revision;
            }
            internal bool IsCurrent => !_published && _owner._revision == _revision;
            /// <remarks>LowFreqAlloc. Called inside the non-reentrant joint publication, after real payment.</remarks>
            internal bool TryPublish()
            {
                if (!IsCurrent) return false;
                if (_entry.SecondBatchParameters.IsValid && !TacticalAccessorySecondBatchState.TryCreateForConfirmedCommit(
                    _entry.Key, _entry.Turn, _entry.SecondBatchParameters, out _entry.SecondBatch))
                    throw new InvalidOperationException("accessory:ConfirmedLeafCreationFailed");
                if (_coinCandidate != null) _owner._coinLedger = _coinCandidate;
                if (_entry.ThirdBatch != null) _owner._thirdNextCommit++;
                _owner._entries.Add(_entry); _owner._revision++; _published = true; return true;
            }
        }

        /// <remarks>LowFreqAlloc on action acknowledgment. Sources are retained through delayed projectile impact.</remarks>
        internal bool AcknowledgeAction(in TacticalDoubleEdgeCommitKey key, int instance, ulong reuseRevision, long frame,
            out TacticalDirectAttackStamp stamp)
        {
            stamp = default;
            var entry = Find(key);
            if (entry == null || entry.Terminal) return false;
            var candidate = new TacticalDirectAttackStamp(key, instance, reuseRevision, frame);
            if (!candidate.IsValid || reuseRevision >= (1UL << 63)) return false;
            foreach (var saved in entry.Sources) {
                if (saved.ActionInstance == instance && saved.ActionRevision == reuseRevision) {
                    if (saved.CreatedFrame != frame) return false;
                    stamp = saved; return true;
                }
            }
            if (entry.Sources.Count > 0) {
                var previous = entry.Sources[entry.Sources.Count - 1];
                if (reuseRevision <= previous.ActionRevision || frame < previous.CreatedFrame) return false;
            }
            // A consumed one-punch still allows ordinary ACK/animation. Its attack gate remains closed.
            if (entry.SecondBatch != null && !entry.SecondBatch.SuppressFutureAttackJudgments
                && !entry.SecondBatch.TryRegisterRoot(reuseRevision, instance, reuseRevision, frame, out _)) return false;
            entry.Sources.Add(candidate); _revision++; stamp = candidate; return true;
        }

        /// <remarks>NoAlloc. Only producers call this while spawning a projectile or producing a direct trace.</remarks>
        internal TacticalDirectAttackStamp FindSource(in TacticalDoubleEdgeCommitKey key, int actionInstance, ulong reuseRevision)
        {
            var entry = Find(key);
            if (entry == null) return default;
            for (int j = entry.Sources.Count - 1; j >= 0; j--)
                if (entry.Sources[j].ActionInstance == actionInstance && entry.Sources[j].ActionRevision == reuseRevision)
                    return entry.Sources[j];
            return default;
        }

        /// <remarks>NoAlloc. Direct projectiles supply their birth stamp; never resolve their source from current execution.</remarks>
        internal int ScaleDamage(int damage, in TacticalDoubleEdgeDamageFacts facts, in TacticalDirectAttackStamp source)
        {
            DamageFactors(facts, source, out int duel, out int shattered);
            return TacticalDoubleEdgeDamageRules.TryScale(damage, duel, shattered, out int scaled) ? scaled : damage;
        }

        internal void DamageFactors(in TacticalDoubleEdgeDamageFacts facts, in TacticalDirectAttackStamp source,
            out int duel, out int shattered)
        {
            duel = shattered = 1000;
            if (facts.Stage != _stage || facts.Turn != _turn) return;
            for (int i = 0; i < _entries.Count; i++) {
                var entry = _entries[i];
                if (entry.Parameters.Duel && !entry.PairRevoked) {
                    var rule = new TacticalDuelInvitationRule(_stage, entry.Turn, entry.Key.Frame,
                        entry.Key.Actor, entry.Opponent, entry.Parameters.DuelScaleMilli);
                    if (rule.Qualifies(facts)) duel = Math.Max(duel, rule.DamageScaleMilli);
                }
                if (!entry.Parameters.Shattered || !source.Qualifies(entry.Key, facts)) continue;
                for (int j = 0; j < entry.Sources.Count; j++) {
                    var witness = entry.Sources[j];
                    if (witness.ActionInstance == source.ActionInstance && witness.ActionRevision == source.ActionRevision
                        && witness.CreatedFrame == source.CreatedFrame) shattered = entry.Parameters.ShatteredScaleMilli;
                }
            }
        }

        /// <remarks>NoAlloc. Called only with the whole main host's terminal provenance. First terminal wins.
        /// Center is copied at the terminal frame, not during later smooth pause or recovery settlement.</remarks>
        internal bool CompleteHost(in TacticalDoubleEdgeCommitKey key, bool naturalSuccess, long frame, int x, int y, int z)
        {
            var entry = Find(key);
            if (entry == null || frame < key.Frame) return false;
            if (entry.Terminal) return true;
            if (entry.Sources.Count > 0 && frame < entry.Sources[entry.Sources.Count - 1].CreatedFrame) return false;
            if (naturalSuccess && entry.Parameters.BlackHole
                && !TacticalBlackHoleLifetime.TryCreate(_stage, entry.Turn, frame, out _)) return false;
            ThirdHostEnded(entry, frame);
            entry.SecondBatch?.EndHost();
            entry.Terminal = true; entry.NaturalSuccess = naturalSuccess; entry.TerminalFrame = frame;
            entry.Generated = naturalSuccess && entry.Parameters.BlackHole;
            if (entry.Generated) { entry.X = x; entry.Y = y; entry.Z = z; }
            _revision++; return true;
        }

        internal bool MustExhaust(in TacticalDoubleEdgeCommitKey key)
        { var entry = Find(key); return entry != null && entry.Parameters.Shattered; }
        internal bool MarkExhausted(in TacticalDoubleEdgeCommitKey key)
        {
            var entry = Find(key);
            if (entry == null || !entry.Parameters.Shattered || !entry.Terminal) return false;
            if (!entry.Exhausted) { entry.Exhausted = true; _revision++; }
            return true;
        }

        /// <remarks>NoAlloc. Revoke the frozen pair permanently, including after same-generation resurrection.
        /// Independent generated fields and delayed projectile sources remain stage-owned.</remarks>
        internal void RevokePair(GameplayEntityId actor)
        {
            for (int i = 0; i < _entries.Count; i++) {
                var entry = _entries[i];
                if (entry.Key.Actor == actor) entry.SecondBatch?.RevokeRearOnActorDeparture();
                if (!entry.PairRevoked && entry.Parameters.Duel && (entry.Key.Actor == actor || entry.Opponent == actor))
                { entry.PairRevoked = true; _revision++; }
            }
        }

        /// <remarks>NoAlloc. The runtime calls once per target before the existing Motion consumer.
        /// Expired fields produce no queued request and all field vectors share one final limit.</remarks>
        internal bool TryPull(long frame, int x, int y, int z, int mass, int pushScale, bool canPush,
            int velocityX, int velocityZ, int tickRate, out TacticalBlackHoleAcceleration acceleration)
        {
            acceleration = default; decimal sumX = 0, sumZ = 0; int cap = 0, speed = 0;
            for (int i = 0; i < _entries.Count; i++) {
                var entry = _entries[i];
                if (!entry.Generated || !TacticalBlackHoleLifetime.TryCreate(_stage, entry.Turn, entry.TerminalFrame, out var life)
                    || !life.IsActive(_stage, _turn, frame)) continue;
                if (!TacticalBlackHoleRules.TrySampleWide(entry.Parameters.Pull, (long)entry.X - x, (long)entry.Y - y,
                    (long)entry.Z - z, mass, pushScale, canPush, out decimal contributionX, out decimal contributionZ)) return false;
                sumX += contributionX; sumZ += contributionZ;
                cap = cap == 0 ? entry.Parameters.MaximumAcceleration : Math.Min(cap, entry.Parameters.MaximumAcceleration);
                speed = speed == 0 ? entry.Parameters.MaximumRadialSpeed : Math.Min(speed, entry.Parameters.MaximumRadialSpeed);
            }
            return cap == 0 || TacticalBlackHoleRules.TryLimitWideCombined(sumX, sumZ, velocityX, velocityZ, cap, speed, tickRate, out acceleration);
        }

        private Entry Find(in TacticalDoubleEdgeCommitKey key)
        {
            for (int i = 0; i < _entries.Count; i++) if (_entries[i].Key.Matches(key)) return _entries[i];
            return null;
        }

        // LowFreqAlloc at actual projectile birth. Only an acknowledged, exact source may become durable.
        internal bool BindProjectile(int projectile, int sequence, in TacticalDirectAttackStamp source, long frame = -1)
            => BindBirth(projectile, sequence, source, 1, frame);
        internal bool BindTrace(int window, in TacticalDirectAttackStamp source, long frame = -1) => BindBirth(window, 0, source, 2, frame);
        private bool BindBirth(int projectile, int sequence, in TacticalDirectAttackStamp source, int kind, long frame)
        {
            if (projectile <= 0 || sequence < 0 || !source.IsValid) return false;
            var trusted = FindSource(source.Commit, source.ActionInstance, source.ActionRevision);
            if (!trusted.IsValid || trusted.CreatedFrame != source.CreatedFrame) return false;
            foreach (var birth in _projectiles)
                if (birth.Kind == kind && birth.Projectile == projectile && birth.Sequence == sequence)
                    return birth.Source.Commit.Matches(source.Commit) && birth.Source.ActionInstance == source.ActionInstance
                        && birth.Source.ActionRevision == source.ActionRevision && birth.Source.CreatedFrame == source.CreatedFrame;
            var root = SecondBatchRoot(source);
            if (root.IsValid && !SuppressesAttack(source) && !TryRegisterSecondBatchBirth(root,
                ProducerNode(kind, projectile, sequence, source.ActionRevision), root.Producer,
                frame < 0 ? source.CreatedFrame : frame, default, out _)) return false;
            _projectiles.Add(new ProjectileBirth(projectile, sequence, source, kind)); _revision++; return true;
        }

        internal TacticalDirectAttackStamp ProjectileSource(int projectile, int sequence)
        {
            for (int i = 0; i < _projectiles.Count; i++)
                if (_projectiles[i].Kind == 1 && _projectiles[i].Projectile == projectile && _projectiles[i].Sequence == sequence) return _projectiles[i].Source;
            return default;
        }

        /// <summary>Cold numeric transport projection. This data cannot create authority; paused restore uses a trusted clone.</summary>
        internal long[][] CaptureProjection()
        {
            if (!HasState) return null;
            var rows = new List<long[]> { new[] { 1L, unchecked((long)_stage), unchecked((long)_turn) } };
            for (int i = 0; i < _entries.Count; i++) {
                var e = _entries[i]; var k = e.Key; var p = e.Parameters;
                rows.Add(new long[] { 2, i, unchecked((long)k.Stage), unchecked((long)k.Commit), unchecked((long)k.Execution),
                    k.Actor.Index, k.Actor.Generation, unchecked((long)k.BattleCardInstance), k.Frame, unchecked((long)e.Turn),
                    e.Opponent.Index, e.Opponent.Generation, p.DuelScaleMilli, p.ShatteredScaleMilli, p.BlackHole ? 1 : 0,
                    p.Pull.RadiusMillimeters, p.Pull.StopRadiusMillimeters, p.Pull.AccelerationMillimetersPerSecondSquared,
                    p.Pull.ReferenceMassGrams, p.MaximumAcceleration, p.MaximumRadialSpeed, e.PairRevoked ? 1 : 0,
                    e.Terminal ? 1 : 0, e.NaturalSuccess ? 1 : 0, e.Generated ? 1 : 0, e.Exhausted ? 1 : 0,
                    e.TerminalFrame, e.X, e.Y, e.Z });
                foreach (var s in e.Sources) rows.Add(new long[] { 3, i, s.ActionInstance, unchecked((long)s.ActionRevision), s.CreatedFrame });
            }
            foreach (var b in _projectiles) {
                var s = b.Source; var k = s.Commit;
                rows.Add(new long[] { b.Kind == 1 ? 4 : 5, b.Projectile, b.Sequence, unchecked((long)k.Stage), unchecked((long)k.Commit),
                    unchecked((long)k.Execution), k.Actor.Index, k.Actor.Generation, unchecked((long)k.BattleCardInstance),
                    k.Frame, s.ActionInstance, unchecked((long)s.ActionRevision), s.CreatedFrame, b.Direct ? 1 : 0 });
            }
            AppendSecondBatchProjection(rows);
            AppendProducerProjection(rows);
            AppendPendingProjectileProjection(rows);
            AppendBuffLineageProjection(rows);
            AppendProjectileActorProjection(rows);
            AppendPendingActorProjection(rows);
            AppendTimelineActorProjection(rows);
            AppendThirdProjection(rows);
            rows.Add(new long[] { 13, TimelineNextInstanceId });
            return rows.ToArray();
        }

        private readonly struct ProjectileBirth
        {
            internal readonly int Projectile, Sequence, Kind;
            internal readonly bool Direct;
            internal readonly TacticalDirectAttackStamp Source;
            internal ProjectileBirth(int projectile, int sequence, in TacticalDirectAttackStamp source, int kind, bool direct = true)
            { Projectile = projectile; Sequence = sequence; Source = source; Kind = kind; Direct = direct; }
        }

        /// <summary>Cold trusted copy for the existing paused restore context. No JSON-to-authority factory.</summary>
        internal TacticalDoubleEdgeBattleState CloneTrusted()
        {
            var copy = new TacticalDoubleEdgeBattleState { _stage = _stage, _turn = _turn, _revision = _revision, TimelineNextInstanceId = TimelineNextInstanceId };
            foreach (var entry in _entries) copy._entries.Add(entry.Clone());
            copy._projectiles.AddRange(_projectiles);
            copy._producerBindings.AddRange(_producerBindings);
            copy._pendingAccessoryProjectiles.AddRange(_pendingAccessoryProjectiles);
            copy._buffLineages.AddRange(_buffLineages);
            copy._projectileActors.AddRange(_projectileActors);
            copy._pendingProjectileActors.AddRange(_pendingProjectileActors);
            copy._timelineActors.AddRange(_timelineActors);
            CopyThirdState(copy);
            return copy;
        }

        /// <summary>NoAlloc. Caller opens the registered stage-level hash object, independent of alive actors.</summary>
        internal void AddHash(ref RuntimeHashAccumulator hash)
        {
            hash.AddLong(1, unchecked((long)_stage)); hash.AddLong(2, unchecked((long)_turn));
            hash.BeginArray(3, _entries.Count);
            for (int i = 0; i < _entries.Count; i++) {
                var entry = _entries[i]; var key = entry.Key; var p = entry.Parameters;
                hash.BeginElement(i);
                hash.AddLong(1, unchecked((long)key.Stage)); hash.AddLong(2, unchecked((long)key.Commit));
                hash.AddLong(3, unchecked((long)key.Execution)); hash.AddInt(4, key.Actor.Index); hash.AddInt(5, key.Actor.Generation);
                hash.AddLong(6, unchecked((long)key.BattleCardInstance)); hash.AddLong(7, key.Frame); hash.AddLong(8, unchecked((long)entry.Turn));
                hash.AddInt(9, entry.Opponent.Index); hash.AddInt(10, entry.Opponent.Generation);
                hash.AddInt(11, p.DuelScaleMilli); hash.AddInt(12, p.ShatteredScaleMilli); hash.AddBool(13, p.BlackHole);
                hash.AddInt(14, p.Pull.RadiusMillimeters); hash.AddInt(15, p.Pull.StopRadiusMillimeters);
                hash.AddInt(16, p.Pull.AccelerationMillimetersPerSecondSquared); hash.AddInt(17, p.Pull.ReferenceMassGrams);
                hash.AddInt(18, p.MaximumAcceleration); hash.AddInt(19, p.MaximumRadialSpeed);
                hash.AddBool(20, entry.PairRevoked); hash.AddBool(21, entry.Terminal); hash.AddBool(22, entry.NaturalSuccess);
                hash.AddBool(23, entry.Generated); hash.AddBool(24, entry.Exhausted); hash.AddLong(25, entry.TerminalFrame);
                hash.AddInt(26, entry.X); hash.AddInt(27, entry.Y); hash.AddInt(28, entry.Z);
                hash.BeginArray(29, entry.Sources.Count);
                for (int j = 0; j < entry.Sources.Count; j++) {
                    var source = entry.Sources[j]; hash.BeginElement(j);
                    hash.AddInt(1, source.ActionInstance); hash.AddLong(2, unchecked((long)source.ActionRevision));
                    hash.AddLong(3, source.CreatedFrame); hash.EndElement();
                }
                hash.EndArray(); hash.EndElement();
            }
            hash.EndArray();
            hash.BeginArray(4, _projectiles.Count);
            for (int i = 0; i < _projectiles.Count; i++) {
                var b = _projectiles[i]; var s = b.Source; var k = s.Commit;
                hash.BeginElement(i); hash.AddInt(1, b.Projectile); hash.AddInt(2, b.Sequence);
                hash.AddLong(3, unchecked((long)k.Stage)); hash.AddLong(4, unchecked((long)k.Commit));
                hash.AddLong(5, unchecked((long)k.Execution)); hash.AddInt(6, k.Actor.Index); hash.AddInt(7, k.Actor.Generation);
                hash.AddLong(8, unchecked((long)k.BattleCardInstance)); hash.AddLong(9, k.Frame);
                hash.AddInt(10, s.ActionInstance); hash.AddLong(11, unchecked((long)s.ActionRevision)); hash.AddLong(12, s.CreatedFrame);
                hash.AddInt(13, b.Kind); hash.AddBool(14, b.Direct);
                hash.EndElement();
            }
            hash.EndArray();
            AddSecondBatchHash(ref hash);
            AddProducerHash(ref hash);
            AddPendingProjectileHash(ref hash);
            AddBuffLineageHash(ref hash);
            AddProjectileActorHash(ref hash);
            AddPendingActorHash(ref hash);
            AddTimelineActorHash(ref hash);
            AddThirdHash(ref hash);
            hash.AddInt(9, TimelineNextInstanceId);
        }

        private sealed class Entry
        {
            internal readonly TacticalDoubleEdgeCommitKey Key;
            internal readonly ulong Turn;
            internal readonly GameplayEntityId Opponent;
            internal readonly TacticalDoubleEdgeParameters Parameters;
            internal readonly List<TacticalDirectAttackStamp> Sources = new List<TacticalDirectAttackStamp>();
            internal TacticalSecondBatchParameters SecondBatchParameters;
            internal TacticalAccessorySecondBatchState SecondBatch;
            internal TacticalThirdBatchParameters ThirdBatchParameters;
            internal TacticalAccessoryThirdBatchState ThirdBatch;
            internal bool PairRevoked, Terminal, NaturalSuccess, Generated, Exhausted;
            internal long TerminalFrame = -1;
            internal int X, Y, Z;
            internal Entry(in TacticalDoubleEdgeCommitKey key, ulong turn, GameplayEntityId opponent, in TacticalDoubleEdgeParameters parameters)
            { Key = key; Turn = turn; Opponent = opponent; Parameters = parameters; }
            internal Entry Clone()
            {
                var entry = new Entry(Key, Turn, Opponent, Parameters) { PairRevoked = PairRevoked, Terminal = Terminal,
                    NaturalSuccess = NaturalSuccess, Generated = Generated, Exhausted = Exhausted, TerminalFrame = TerminalFrame, X = X, Y = Y, Z = Z };
                entry.SecondBatchParameters = SecondBatchParameters;
                entry.SecondBatch = SecondBatch?.CloneForTrustedPause();
                entry.ThirdBatchParameters = ThirdBatchParameters; entry.ThirdBatch = ThirdBatch?.CloneForTrustedPause();
                entry.Sources.AddRange(Sources); return entry;
            }
        }
    }

    /// <summary>Frozen explicit configuration. A neutral scale (1000) means that effect is absent.</summary>
    internal readonly struct TacticalDoubleEdgeParameters
    {
        internal TacticalDoubleEdgeParameters(int duel, int shattered, bool blackHole, in TacticalBlackHolePullParameters pull,
            int maximumAcceleration, int maximumRadialSpeed)
        { DuelScaleMilli = duel; ShatteredScaleMilli = shattered; BlackHole = blackHole; Pull = pull;
            MaximumAcceleration = maximumAcceleration; MaximumRadialSpeed = maximumRadialSpeed; }
        internal int DuelScaleMilli { get; }
        internal int ShatteredScaleMilli { get; }
        internal bool BlackHole { get; }
        internal TacticalBlackHolePullParameters Pull { get; }
        internal int MaximumAcceleration { get; }
        internal int MaximumRadialSpeed { get; }
        internal bool Duel => DuelScaleMilli > 1000;
        internal bool Shattered => ShatteredScaleMilli > 1000;
        internal bool IsNeutral => !BlackHole && (DuelScaleMilli == 0 && ShatteredScaleMilli == 0
            || DuelScaleMilli == 1000 && ShatteredScaleMilli == 1000);
        internal bool IsValid => DuelScaleMilli >= 1000 && ShatteredScaleMilli >= 1000 && (Duel || Shattered || BlackHole)
            && (!BlackHole || Pull.IsValid && MaximumAcceleration > 0 && MaximumRadialSpeed > 0);
    }
}
