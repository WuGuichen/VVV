using System;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiJointEvaluationStatus
    {
        Unselectable = 0,
        CompatibilityLegacyRange = 1,
        EvaluatorValid = 2
    }

    internal readonly struct CharacterTestRuntimeAiJointEvaluation
    {
        internal CharacterTestRuntimeAiJointEvaluation(
            CharacterTestRuntimeAiJointEvaluationStatus status,
            bool currentPose,
            bool turnInPlace,
            bool approachPose,
            int pathCostMillimeters,
            int earliestHitTick,
            int anchorId,
            CharacterTestAttackOpportunityResult opportunity = default)
        {
            Status = status;
            CurrentPose = currentPose;
            TurnInPlace = turnInPlace;
            ApproachPose = approachPose;
            PathCostMillimeters = pathCostMillimeters < 0
                ? int.MaxValue
                : pathCostMillimeters;
            EarliestHitTick = earliestHitTick < 0 ? int.MaxValue : earliestHitTick;
            AnchorId = anchorId;
            Opportunity = opportunity;
        }

        internal CharacterTestRuntimeAiJointEvaluationStatus Status { get; }
        internal bool CurrentPose { get; }
        internal bool TurnInPlace { get; }
        internal bool ApproachPose { get; }
        internal int PathCostMillimeters { get; }
        internal int EarliestHitTick { get; }
        internal int AnchorId { get; }
        internal CharacterTestAttackOpportunityResult Opportunity { get; }
        internal bool IsSelectable =>
            Status == CharacterTestRuntimeAiJointEvaluationStatus.CompatibilityLegacyRange
            || (Status == CharacterTestRuntimeAiJointEvaluationStatus.EvaluatorValid
                && (CurrentPose || TurnInPlace || ApproachPose));
    }

    internal readonly struct CharacterTestRuntimeAiJointSelectionCandidate
    {
        internal CharacterTestRuntimeAiJointSelectionCandidate(
            CharacterTestRuntimeAiJointCandidateSeed seed,
            CharacterTestRuntimeAiJointEvaluation evaluation)
        {
            Seed = seed;
            Evaluation = evaluation;
        }

        internal CharacterTestRuntimeAiJointCandidateSeed Seed { get; }
        internal CharacterTestRuntimeAiJointEvaluation Evaluation { get; }
        internal bool IsSelectable => Evaluation.IsSelectable;
        internal int TargetEntityId => Seed.TargetEntityId;
        internal CharacterActionId ResolvedActionId => Seed.Action.ResolvedActionId;
        internal int ZoneId => Seed.ZoneId;
        internal int AnchorId => Evaluation.AnchorId;
    }

    internal readonly struct CharacterTestRuntimeAiJointSelectionResult
    {
        internal CharacterTestRuntimeAiJointSelectionResult(
            bool hasSelection,
            CharacterTestRuntimeAiJointSelectionCandidate selection,
            int evaluatedCount,
            int selectableCount)
        {
            HasSelection = hasSelection;
            Selection = selection;
            EvaluatedCount = evaluatedCount;
            SelectableCount = selectableCount;
        }

        internal bool HasSelection { get; }
        internal CharacterTestRuntimeAiJointSelectionCandidate Selection { get; }
        internal int EvaluatedCount { get; }
        internal int SelectableCount { get; }
    }

    internal static class CharacterTestRuntimeAiJointReducer
    {
        internal static CharacterTestRuntimeAiJointSelectionResult Select(
            CharacterTestRuntimeAiJointSelectionCandidate[] candidates,
            int count)
        {
            int safeCount = candidates == null || count <= 0
                ? 0
                : count < candidates.Length ? count : candidates.Length;
            bool found = false;
            int selectableCount = 0;
            CharacterTestRuntimeAiJointSelectionCandidate selected = default;
            for (int i = 0; i < safeCount; i++)
            {
                CharacterTestRuntimeAiJointSelectionCandidate candidate = candidates[i];
                if (!candidate.IsSelectable)
                    continue;
                selectableCount++;
                if (!found || IsBetter(candidate, selected))
                {
                    selected = candidate;
                    found = true;
                }
            }
            return new CharacterTestRuntimeAiJointSelectionResult(
                found,
                selected,
                safeCount,
                selectableCount);
        }

        internal static bool IsBetter(
            CharacterTestRuntimeAiJointSelectionCandidate candidate,
            CharacterTestRuntimeAiJointSelectionCandidate current)
        {
            int status = candidate.Evaluation.Status.CompareTo(current.Evaluation.Status);
            if (status != 0)
                return status > 0;

            int life = LifeRank(candidate.Seed.Actor.LifeState).CompareTo(
                LifeRank(current.Seed.Actor.LifeState));
            if (life != 0)
                return life > 0;

            int currentPose = candidate.Evaluation.CurrentPose.CompareTo(
                current.Evaluation.CurrentPose);
            if (currentPose != 0)
                return currentPose > 0;
            int turn = candidate.Evaluation.TurnInPlace.CompareTo(
                current.Evaluation.TurnInPlace);
            if (turn != 0)
                return turn > 0;
            int approach = candidate.Evaluation.ApproachPose.CompareTo(
                current.Evaluation.ApproachPose);
            if (approach != 0)
                return approach > 0;

            int utility = candidate.Seed.Action.ActionUtilityMilli.CompareTo(
                current.Seed.Action.ActionUtilityMilli);
            if (utility != 0)
                return utility > 0;
            int path = candidate.Evaluation.PathCostMillimeters.CompareTo(
                current.Evaluation.PathCostMillimeters);
            if (path != 0)
                return path < 0;
            int tick = candidate.Evaluation.EarliestHitTick.CompareTo(
                current.Evaluation.EarliestHitTick);
            if (tick != 0)
                return tick < 0;
            int priority = candidate.Seed.ZonePriority.CompareTo(current.Seed.ZonePriority);
            if (priority != 0)
                return priority > 0;
            int target = candidate.TargetEntityId.CompareTo(current.TargetEntityId);
            if (target != 0)
                return target < 0;
            int action = candidate.ResolvedActionId.CompareTo(current.ResolvedActionId);
            if (action != 0)
                return action < 0;
            int zone = candidate.ZoneId.CompareTo(current.ZoneId);
            if (zone != 0)
                return zone < 0;
            int anchor = candidate.AnchorId.CompareTo(current.AnchorId);
            if (anchor != 0)
                return anchor < 0;
            return candidate.Seed.Action.PlannerActionId
                < current.Seed.Action.PlannerActionId;
        }

        private static int LifeRank(CharacterTestCharacterLifeState lifeState)
        {
            return lifeState == CharacterTestCharacterLifeState.Alive ? 1 : 0;
        }
    }

    internal readonly struct CharacterTestRuntimeAiJointTracePlanCacheKey :
        IEquatable<CharacterTestRuntimeAiJointTracePlanCacheKey>
    {
        internal CharacterTestRuntimeAiJointTracePlanCacheKey(
            int sourceEntityId,
            CharacterActionId resolvedActionId,
            CharacterTestDirectHitTracePlanPoseKind poseKind,
            int sourceXMillimeters,
            int sourceYMillimeters,
            int sourceZMillimeters,
            int sourceYawMilliDegrees,
            int completedFrame,
            int physicsRevision,
            int sourcePoseRevision,
            int sourceFacingRevision,
            int actionBindingRevision,
            int animationRevision,
            int rootMotionRevision,
            int tracePolicyRevision,
            int targetEntityId = 0,
            int requiredTurnYawMilliDegrees = 0)
        {
            SourceEntityId = sourceEntityId;
            ResolvedActionId = resolvedActionId;
            PoseKind = poseKind;
            SourceXMillimeters = sourceXMillimeters;
            SourceYMillimeters = sourceYMillimeters;
            SourceZMillimeters = sourceZMillimeters;
            SourceYawMilliDegrees = sourceYawMilliDegrees;
            CompletedFrame = completedFrame;
            PhysicsRevision = physicsRevision;
            SourcePoseRevision = sourcePoseRevision;
            SourceFacingRevision = sourceFacingRevision;
            ActionBindingRevision = actionBindingRevision;
            AnimationRevision = animationRevision;
            RootMotionRevision = rootMotionRevision;
            TracePolicyRevision = tracePolicyRevision;
            TargetEntityId = targetEntityId;
            RequiredTurnYawMilliDegrees = requiredTurnYawMilliDegrees;
        }

        internal int SourceEntityId { get; }
        internal CharacterActionId ResolvedActionId { get; }
        internal CharacterTestDirectHitTracePlanPoseKind PoseKind { get; }
        internal int SourceXMillimeters { get; }
        internal int SourceYMillimeters { get; }
        internal int SourceZMillimeters { get; }
        internal int SourceYawMilliDegrees { get; }
        internal int CompletedFrame { get; }
        internal int PhysicsRevision { get; }
        internal int SourcePoseRevision { get; }
        internal int SourceFacingRevision { get; }
        internal int ActionBindingRevision { get; }
        internal int AnimationRevision { get; }
        internal int RootMotionRevision { get; }
        internal int TracePolicyRevision { get; }
        internal int TargetEntityId { get; }
        internal int RequiredTurnYawMilliDegrees { get; }

        public bool Equals(CharacterTestRuntimeAiJointTracePlanCacheKey other)
        {
            return SourceEntityId == other.SourceEntityId
                && ResolvedActionId == other.ResolvedActionId
                && PoseKind == other.PoseKind
                && SourceXMillimeters == other.SourceXMillimeters
                && SourceYMillimeters == other.SourceYMillimeters
                && SourceZMillimeters == other.SourceZMillimeters
                && SourceYawMilliDegrees == other.SourceYawMilliDegrees
                && CompletedFrame == other.CompletedFrame
                && PhysicsRevision == other.PhysicsRevision
                && SourcePoseRevision == other.SourcePoseRevision
                && SourceFacingRevision == other.SourceFacingRevision
                && ActionBindingRevision == other.ActionBindingRevision
                && AnimationRevision == other.AnimationRevision
                && RootMotionRevision == other.RootMotionRevision
                && TracePolicyRevision == other.TracePolicyRevision
                && TargetEntityId == other.TargetEntityId
                && RequiredTurnYawMilliDegrees == other.RequiredTurnYawMilliDegrees;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestRuntimeAiJointTracePlanCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SourceEntityId;
                hash = hash * 397 ^ ResolvedActionId.GetHashCode();
                hash = hash * 397 ^ (int)PoseKind;
                hash = hash * 397 ^ SourceXMillimeters;
                hash = hash * 397 ^ SourceYMillimeters;
                hash = hash * 397 ^ SourceZMillimeters;
                hash = hash * 397 ^ SourceYawMilliDegrees;
                hash = hash * 397 ^ CompletedFrame;
                hash = hash * 397 ^ PhysicsRevision;
                hash = hash * 397 ^ SourcePoseRevision;
                hash = hash * 397 ^ SourceFacingRevision;
                hash = hash * 397 ^ ActionBindingRevision;
                hash = hash * 397 ^ AnimationRevision;
                hash = hash * 397 ^ RootMotionRevision;
                hash = hash * 397 ^ TracePolicyRevision;
                hash = hash * 397 ^ TargetEntityId;
                return hash * 397 ^ RequiredTurnYawMilliDegrees;
            }
        }
    }

    internal readonly struct CharacterTestRuntimeAiJointResultCacheKey :
        IEquatable<CharacterTestRuntimeAiJointResultCacheKey>
    {
        internal CharacterTestRuntimeAiJointResultCacheKey(
            CharacterTestRuntimeAiJointTracePlanCacheKey tracePlan,
            int targetEntityId,
            int targetHurtboxRevision,
            int zoneBindingRevision,
            CharacterTestAttackEvaluationMode mode,
            int zoneId,
            int anchorId,
            int candidateXMillimeters,
            int candidateYMillimeters,
            int candidateZMillimeters,
            int candidateYawMilliDegrees,
            int lockRevision,
            int reservationRevision,
            int reservationCount,
            int graphRevision,
            long graphContentHash,
            int worldClassificationFrame,
            int worldClassificationRevision,
            RuntimePreparedDigest anchorContent = default)
        {
            TracePlan = tracePlan;
            TargetEntityId = targetEntityId;
            TargetHurtboxRevision = targetHurtboxRevision;
            ZoneBindingRevision = zoneBindingRevision;
            Mode = mode;
            ZoneId = zoneId;
            AnchorId = anchorId;
            CandidateXMillimeters = candidateXMillimeters;
            CandidateYMillimeters = candidateYMillimeters;
            CandidateZMillimeters = candidateZMillimeters;
            CandidateYawMilliDegrees = candidateYawMilliDegrees;
            LockRevision = lockRevision;
            ReservationRevision = reservationRevision;
            ReservationCount = reservationCount;
            GraphRevision = graphRevision;
            GraphContentHash = graphContentHash;
            WorldClassificationFrame = worldClassificationFrame;
            WorldClassificationRevision = worldClassificationRevision;
            AnchorContent = anchorContent;
        }

        internal CharacterTestRuntimeAiJointTracePlanCacheKey TracePlan { get; }
        internal int TargetEntityId { get; }
        internal int TargetHurtboxRevision { get; }
        internal int ZoneBindingRevision { get; }
        internal CharacterTestAttackEvaluationMode Mode { get; }
        internal int ZoneId { get; }
        internal int AnchorId { get; }
        internal int CandidateXMillimeters { get; }
        internal int CandidateYMillimeters { get; }
        internal int CandidateZMillimeters { get; }
        internal int CandidateYawMilliDegrees { get; }
        internal int LockRevision { get; }
        internal int ReservationRevision { get; }
        internal int ReservationCount { get; }
        internal int GraphRevision { get; }
        internal long GraphContentHash { get; }
        internal int WorldClassificationFrame { get; }
        internal int WorldClassificationRevision { get; }
        internal RuntimePreparedDigest AnchorContent { get; }

        public bool Equals(CharacterTestRuntimeAiJointResultCacheKey other)
        {
            return TracePlan.Equals(other.TracePlan)
                && TargetEntityId == other.TargetEntityId
                && TargetHurtboxRevision == other.TargetHurtboxRevision
                && ZoneBindingRevision == other.ZoneBindingRevision
                && AnchorContent.Equals(other.AnchorContent)
                && Mode == other.Mode
                && (ZoneId == other.ZoneId)
                && (AnchorId == other.AnchorId)
                && CandidateXMillimeters == other.CandidateXMillimeters
                && CandidateYMillimeters == other.CandidateYMillimeters
                && CandidateZMillimeters == other.CandidateZMillimeters
                && CandidateYawMilliDegrees == other.CandidateYawMilliDegrees
                && LockRevision == other.LockRevision
                && ReservationRevision == other.ReservationRevision
                && ReservationCount == other.ReservationCount
                && GraphRevision == other.GraphRevision
                && GraphContentHash == other.GraphContentHash
                && WorldClassificationFrame == other.WorldClassificationFrame
                && WorldClassificationRevision == other.WorldClassificationRevision;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterTestRuntimeAiJointResultCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = TracePlan.GetHashCode();
                hash = hash * 397 ^ TargetEntityId;
                hash = hash * 397 ^ TargetHurtboxRevision;
                hash = hash * 397 ^ ZoneBindingRevision;
                hash = hash * 397 ^ (int)Mode;
                hash = hash * 397 ^ ZoneId;
                hash = hash * 397 ^ AnchorId;
                hash = hash * 397 ^ AnchorContent.GetHashCode();
                hash = hash * 397 ^ CandidateXMillimeters;
                hash = hash * 397 ^ CandidateYMillimeters;
                hash = hash * 397 ^ CandidateZMillimeters;
                hash = hash * 397 ^ CandidateYawMilliDegrees;
                hash = hash * 397 ^ LockRevision;
                hash = hash * 397 ^ ReservationRevision;
                hash = hash * 397 ^ ReservationCount;
                hash = hash * 397 ^ GraphRevision;
                hash = hash * 397 ^ GraphContentHash.GetHashCode();
                hash = hash * 397 ^ WorldClassificationFrame;
                return hash * 397 ^ WorldClassificationRevision;
            }
        }
    }

    internal readonly struct CharacterTestRuntimeAiJointTracePlanCacheValue
    {
        internal CharacterTestRuntimeAiJointTracePlanCacheValue(
            CharacterTestDirectHitTracePlanView plan,
            int sampleCount,
            bool valid)
        {
            Plan = plan;
            SampleCount = sampleCount;
            Valid = valid;
            PreparedAttack = default;
        }

        internal CharacterTestRuntimeAiJointTracePlanCacheValue(
            CharacterTestRuntimeAiJointPreparedAttack preparedAttack,
            int sampleCount)
        {
            Plan = default;
            SampleCount = sampleCount;
            Valid = preparedAttack.IsValid;
            PreparedAttack = preparedAttack;
        }

        internal CharacterTestDirectHitTracePlanView Plan { get; }
        internal int SampleCount { get; }
        internal bool Valid { get; }
        internal CharacterTestRuntimeAiJointPreparedAttack PreparedAttack { get; }
    }

    internal sealed class CharacterTestRuntimeAiJointFrameCache<TKey, TValue>
        where TKey : struct, IEquatable<TKey>
        where TValue : struct
    {
        private readonly Entry[] _entries;
        private int _cursor;
        private int _writesThisFrame;

        internal CharacterTestRuntimeAiJointFrameCache(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _entries = new Entry[capacity];
        }

        internal int Capacity => _entries.Length;
        internal int HitCount { get; private set; }
        internal int MissCount { get; private set; }
        internal int StoreCount { get; private set; }
        internal int OverflowCount { get; private set; }

        internal void BeginFrame()
        {
            _cursor = 0;
            _writesThisFrame = 0;
            HitCount = 0;
            MissCount = 0;
            StoreCount = 0;
            OverflowCount = 0;
        }

        internal bool TryGet(TKey key, out TValue value)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                Entry entry = _entries[i];
                if (entry.Occupied && entry.Key.Equals(key))
                {
                    HitCount++;
                    value = entry.Value;
                    return true;
                }
            }
            MissCount++;
            value = default;
            return false;
        }

        internal bool TryStore(TKey key, TValue value)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Occupied && _entries[i].Key.Equals(key))
                {
                    _entries[i] = new Entry(key, value);
                    return true;
                }
            }
            if (_writesThisFrame >= _entries.Length)
            {
                OverflowCount++;
                return false;
            }
            _entries[_cursor] = new Entry(key, value);
            _cursor++;
            if (_cursor == _entries.Length)
                _cursor = 0;
            _writesThisFrame++;
            StoreCount++;
            return true;
        }

        internal void Clear()
        {
            Array.Clear(_entries, 0, _entries.Length);
            BeginFrame();
        }

        private readonly struct Entry
        {
            internal Entry(TKey key, TValue value)
            {
                Key = key;
                Value = value;
                Occupied = true;
            }

            internal TKey Key { get; }
            internal TValue Value { get; }
            internal bool Occupied { get; }
        }
    }

    internal sealed class CharacterTestRuntimeAiJointSelectionCache
    {
        internal const int DefaultTracePlanCapacity = 128;
        internal const int DefaultResultCapacity = 512;
        internal const int HardTracePlanCapacity = 512;
        internal const int HardResultCapacity = 2048;

        private readonly CharacterTestRuntimeAiJointFrameCache<
            CharacterTestRuntimeAiJointTracePlanCacheKey,
            CharacterTestRuntimeAiJointTracePlanCacheValue> _tracePlans;
        private readonly CharacterTestRuntimeAiJointFrameCache<
            CharacterTestRuntimeAiJointResultCacheKey,
            CharacterTestRuntimeAiJointEvaluation> _results;

        internal CharacterTestRuntimeAiJointSelectionCache(
            int tracePlanCapacity = DefaultTracePlanCapacity,
            int resultCapacity = DefaultResultCapacity)
        {
            if (tracePlanCapacity <= 0 || tracePlanCapacity > HardTracePlanCapacity)
                throw new ArgumentOutOfRangeException(nameof(tracePlanCapacity));
            if (resultCapacity <= 0 || resultCapacity > HardResultCapacity)
                throw new ArgumentOutOfRangeException(nameof(resultCapacity));
            _tracePlans = new CharacterTestRuntimeAiJointFrameCache<
                CharacterTestRuntimeAiJointTracePlanCacheKey,
                CharacterTestRuntimeAiJointTracePlanCacheValue>(tracePlanCapacity);
            _results = new CharacterTestRuntimeAiJointFrameCache<
                CharacterTestRuntimeAiJointResultCacheKey,
                CharacterTestRuntimeAiJointEvaluation>(resultCapacity);
        }

        internal CharacterTestRuntimeAiJointFrameCache<
            CharacterTestRuntimeAiJointTracePlanCacheKey,
            CharacterTestRuntimeAiJointTracePlanCacheValue> TracePlans => _tracePlans;
        internal CharacterTestRuntimeAiJointFrameCache<
            CharacterTestRuntimeAiJointResultCacheKey,
            CharacterTestRuntimeAiJointEvaluation> Results => _results;

        internal void BeginFrame()
        {
            _tracePlans.BeginFrame();
            _results.BeginFrame();
        }

        internal void Clear()
        {
            _tracePlans.Clear();
            _results.Clear();
        }
    }

    internal interface ICharacterTestRuntimeAiJointEvaluationSource
    {
        bool TryCreateCacheKeys(
            CharacterTestRuntimeAiJointCandidateSeed seed,
            out CharacterTestRuntimeAiJointTracePlanCacheKey tracePlanKey,
            out CharacterTestRuntimeAiJointResultCacheKey resultKey);

        bool TryBuildTracePlan(
            CharacterTestRuntimeAiJointCandidateSeed seed,
            out CharacterTestRuntimeAiJointTracePlanCacheValue tracePlan);

        bool TryEvaluate(
            CharacterTestRuntimeAiJointCandidateSeed seed,
            CharacterTestRuntimeAiJointTracePlanCacheValue tracePlan,
            out CharacterTestRuntimeAiJointEvaluation evaluation);
    }

    internal readonly struct CharacterTestRuntimeAiJointSelectorDiagnostics
    {
        internal CharacterTestRuntimeAiJointSelectorDiagnostics(
            CharacterTestRuntimeAiJointGenerationDiagnostics generation,
            int evaluationCount,
            int tracePlanBuildCount,
            int resultCacheHitCount,
            CharacterTestRuntimeAiJointTruncationPhase truncationPhase,
            int truncationObservedCount,
            int truncationAcceptedCount,
            int truncationLimit)
        {
            Generation = generation;
            EvaluationCount = evaluationCount;
            TracePlanBuildCount = tracePlanBuildCount;
            ResultCacheHitCount = resultCacheHitCount;
            TruncationPhase = truncationPhase;
            TruncationObservedCount = truncationObservedCount;
            TruncationAcceptedCount = truncationAcceptedCount;
            TruncationLimit = truncationLimit;
        }

        internal CharacterTestRuntimeAiJointGenerationDiagnostics Generation { get; }
        internal int EvaluationCount { get; }
        internal int TracePlanBuildCount { get; }
        internal int ResultCacheHitCount { get; }
        internal CharacterTestRuntimeAiJointTruncationPhase TruncationPhase { get; }
        internal int TruncationObservedCount { get; }
        internal int TruncationAcceptedCount { get; }
        internal int TruncationLimit { get; }
        internal bool CandidateSetTruncated =>
            TruncationPhase != CharacterTestRuntimeAiJointTruncationPhase.None;
    }

    /// <summary>
    /// Side-effect-free T2 orchestration. Runtime-specific evaluators supply
    /// immutable witnesses and M1-M3 preview results; this type owns bounded
    /// caching and deterministic reduction only.
    /// </summary>
    internal sealed class CharacterTestRuntimeAiJointSelector
    {
        private readonly CharacterTestRuntimeAiJointSelectionCandidate[] _evaluated =
            new CharacterTestRuntimeAiJointSelectionCandidate[
                CharacterTestRuntimeAiJointGenerationBudget.HardJointCandidateLimit];
        private readonly CharacterTestRuntimeAiJointSelectionCache _cache;

        internal CharacterTestRuntimeAiJointSelector(
            int tracePlanCacheCapacity =
                CharacterTestRuntimeAiJointSelectionCache.DefaultTracePlanCapacity,
            int resultCacheCapacity =
                CharacterTestRuntimeAiJointSelectionCache.DefaultResultCapacity)
        {
            _cache = new CharacterTestRuntimeAiJointSelectionCache(
                tracePlanCacheCapacity,
                resultCacheCapacity);
        }

        internal CharacterTestRuntimeAiJointSelectionCache Cache => _cache;

        internal void BeginFrame()
        {
            _cache.BeginFrame();
        }

        internal void ResetForRollbackReplay()
        {
            _cache.Clear();
        }

        internal CharacterTestRuntimeAiJointSelectionResult Select(
            CharacterTestRuntimeAiJointCandidateGenerator generator,
            ICharacterTestRuntimeAiJointEvaluationSource evaluationSource,
            CharacterTestRuntimeAiJointGenerationDiagnostics generation,
            out CharacterTestRuntimeAiJointSelectorDiagnostics diagnostics,
            int leafPreviewTraceLimit =
                CharacterTestRuntimeAiJointGenerationBudget.DefaultLeafPreviewTraceLimit)
        {
            if (leafPreviewTraceLimit <= 0
                || leafPreviewTraceLimit
                    > CharacterTestRuntimeAiJointGenerationBudget.HardLeafPreviewTraceLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(leafPreviewTraceLimit));
            }
            int evaluatedCount = 0;
            int evaluationCount = 0;
            int tracePlanBuildCount = 0;
            int resultCacheHitCount = 0;
            int leafPreviewCount = 0;
            CharacterTestRuntimeAiJointTruncationPhase phase = generation.TruncationPhase;
            int observed = generation.TruncationObservedCount;
            int accepted = generation.TruncationAcceptedCount;
            int limit = generation.TruncationLimit;

            int candidateCount = generator?.CandidateCount ?? 0;
            for (int i = 0; i < candidateCount; i++)
            {
                CharacterTestRuntimeAiJointCandidateSeed seed = generator.GetCandidate(i);
                CharacterTestRuntimeAiJointEvaluation evaluation;
                if (seed.IsCompatibility)
                {
                    evaluation = new CharacterTestRuntimeAiJointEvaluation(
                        CharacterTestRuntimeAiJointEvaluationStatus.CompatibilityLegacyRange,
                        false,
                        false,
                        false,
                        int.MaxValue,
                        int.MaxValue,
                        0);
                }
                else if (evaluationSource == null
                    || !evaluationSource.TryCreateCacheKeys(
                        seed,
                        out CharacterTestRuntimeAiJointTracePlanCacheKey traceKey,
                        out CharacterTestRuntimeAiJointResultCacheKey resultKey))
                {
                    evaluation = default;
                }
                else if (leafPreviewCount >= leafPreviewTraceLimit)
                {
                    SetCacheTruncation(
                        ref phase,
                        ref observed,
                        ref accepted,
                        ref limit,
                        CharacterTestRuntimeAiJointTruncationPhase.LeafPreviewTrace,
                        leafPreviewCount + 1,
                        leafPreviewCount,
                        leafPreviewTraceLimit);
                    break;
                }
                else if (_cache.Results.TryGet(resultKey, out evaluation))
                {
                    leafPreviewCount++;
                    resultCacheHitCount++;
                }
                else
                {
                    leafPreviewCount++;
                    if (!_cache.TracePlans.TryGet(
                            traceKey,
                            out CharacterTestRuntimeAiJointTracePlanCacheValue tracePlan))
                    {
                        if (!evaluationSource.TryBuildTracePlan(seed, out tracePlan))
                        {
                            evaluation = default;
                            _evaluated[evaluatedCount++] =
                                new CharacterTestRuntimeAiJointSelectionCandidate(seed, evaluation);
                            continue;
                        }
                        tracePlanBuildCount++;
                        if (!_cache.TracePlans.TryStore(traceKey, tracePlan))
                        {
                            SetCacheTruncation(
                                ref phase,
                                ref observed,
                                ref accepted,
                                ref limit,
                                CharacterTestRuntimeAiJointTruncationPhase.TracePlanCache,
                                i + 1,
                                evaluatedCount,
                                _cache.TracePlans.Capacity);
                            break;
                        }
                    }

                    evaluationCount++;
                    if (!evaluationSource.TryEvaluate(seed, tracePlan, out evaluation))
                        evaluation = default;
                    if (!_cache.Results.TryStore(resultKey, evaluation))
                    {
                        SetCacheTruncation(
                            ref phase,
                            ref observed,
                            ref accepted,
                            ref limit,
                            CharacterTestRuntimeAiJointTruncationPhase.ResultCache,
                            i + 1,
                            evaluatedCount,
                            _cache.Results.Capacity);
                        break;
                    }
                }

                _evaluated[evaluatedCount++] =
                    new CharacterTestRuntimeAiJointSelectionCandidate(seed, evaluation);
            }

            CharacterTestRuntimeAiJointSelectionResult result =
                CharacterTestRuntimeAiJointReducer.Select(_evaluated, evaluatedCount);
            diagnostics = new CharacterTestRuntimeAiJointSelectorDiagnostics(
                generation,
                evaluationCount,
                tracePlanBuildCount,
                resultCacheHitCount,
                phase,
                observed,
                accepted,
                limit);
            return result;
        }

        private static void SetCacheTruncation(
            ref CharacterTestRuntimeAiJointTruncationPhase phase,
            ref int observed,
            ref int accepted,
            ref int limit,
            CharacterTestRuntimeAiJointTruncationPhase next,
            int nextObserved,
            int nextAccepted,
            int nextLimit)
        {
            if (phase != CharacterTestRuntimeAiJointTruncationPhase.None)
                return;
            phase = next;
            observed = nextObserved;
            accepted = nextAccepted;
            limit = nextLimit;
        }
    }
}
