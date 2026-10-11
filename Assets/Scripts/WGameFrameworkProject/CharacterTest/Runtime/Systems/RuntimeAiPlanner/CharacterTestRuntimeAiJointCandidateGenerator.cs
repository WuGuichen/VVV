using System;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiJointCandidateSource
    {
        EvaluatorBacked = 0,
        EvaluatorBackedApproach = 1,
        CompatibilityLegacyRange = 2
    }

    internal enum CharacterTestRuntimeAiJointTruncationPhase
    {
        None = 0,
        ActorScan = 1,
        ActorTopK = 2,
        ReadyAction = 3,
        ActionZoneBroadphase = 4,
        JointCandidate = 5,
        LeafPreviewTrace = 6,
        CandidatePose = 7,
        PathQuery = 8,
        TracePlanCache = 9,
        ResultCache = 10
    }

    internal readonly struct CharacterTestRuntimeAiJointAabb
    {
        internal CharacterTestRuntimeAiJointAabb(
            int minX,
            int minY,
            int minZ,
            int maxX,
            int maxY,
            int maxZ)
        {
            MinX = minX;
            MinY = minY;
            MinZ = minZ;
            MaxX = maxX;
            MaxY = maxY;
            MaxZ = maxZ;
        }

        internal int MinX { get; }
        internal int MinY { get; }
        internal int MinZ { get; }
        internal int MaxX { get; }
        internal int MaxY { get; }
        internal int MaxZ { get; }
        internal bool IsValid => MinX <= MaxX && MinY <= MaxY && MinZ <= MaxZ;

        internal bool Intersects(CharacterTestRuntimeAiJointAabb other)
        {
            return IsValid
                && other.IsValid
                && MinX <= other.MaxX
                && MaxX >= other.MinX
                && MinY <= other.MaxY
                && MaxY >= other.MinY
                && MinZ <= other.MaxZ
                && MaxZ >= other.MinZ;
        }
    }

    internal readonly struct CharacterTestRuntimeAiJointActorInput
    {
        internal CharacterTestRuntimeAiJointActorInput(
            int targetEntityId,
            int teamId,
            CharacterTestCharacterLifeState lifeState,
            bool snapshotCurrent,
            long rootSpatialDistanceMillimetersSquared,
            bool maskCompatible = true,
            bool withinSoftSearchRadius = true)
        {
            TargetEntityId = targetEntityId;
            TeamId = teamId;
            LifeState = lifeState;
            SnapshotCurrent = snapshotCurrent;
            MaskCompatible = maskCompatible;
            WithinSoftSearchRadius = withinSoftSearchRadius;
            RootSpatialDistanceMillimetersSquared = rootSpatialDistanceMillimetersSquared < 0L
                ? long.MaxValue
                : rootSpatialDistanceMillimetersSquared;
        }

        internal int TargetEntityId { get; }
        internal int TeamId { get; }
        internal CharacterTestCharacterLifeState LifeState { get; }
        internal bool SnapshotCurrent { get; }
        internal bool MaskCompatible { get; }
        internal bool WithinSoftSearchRadius { get; }
        internal long RootSpatialDistanceMillimetersSquared { get; }
    }

    internal readonly struct CharacterTestRuntimeAiJointActionInput
    {
        internal CharacterTestRuntimeAiJointActionInput(
            int plannerActionId,
            CharacterActionId resolvedActionId,
            CharacterTestRuntimeAiJointCandidateSource source,
            bool ready,
            bool resolvable,
            int actionUtilityMilli,
            CharacterTestRuntimeAiJointAabb conservativeWorldEnvelope,
            int actionBindingRevision = 0,
            int animationRevision = 0,
            int rootMotionRevision = 0,
            int tracePolicyRevision = 0)
        {
            PlannerActionId = plannerActionId;
            ResolvedActionId = resolvedActionId;
            Source = source;
            Ready = ready;
            Resolvable = resolvable;
            ActionUtilityMilli = actionUtilityMilli;
            ConservativeWorldEnvelope = conservativeWorldEnvelope;
            ActionBindingRevision = actionBindingRevision;
            AnimationRevision = animationRevision;
            RootMotionRevision = rootMotionRevision;
            TracePolicyRevision = tracePolicyRevision;
        }

        internal int PlannerActionId { get; }
        internal CharacterActionId ResolvedActionId { get; }
        internal CharacterTestRuntimeAiJointCandidateSource Source { get; }
        internal bool Ready { get; }
        internal bool Resolvable { get; }
        internal int ActionUtilityMilli { get; }
        internal CharacterTestRuntimeAiJointAabb ConservativeWorldEnvelope { get; }
        internal int ActionBindingRevision { get; }
        internal int AnimationRevision { get; }
        internal int RootMotionRevision { get; }
        internal int TracePolicyRevision { get; }
        internal bool IsCompatibility =>
            Source == CharacterTestRuntimeAiJointCandidateSource.CompatibilityLegacyRange;
    }

    internal readonly struct CharacterTestRuntimeAiJointZoneInput
    {
        internal CharacterTestRuntimeAiJointZoneInput(
            int targetEntityId,
            int zoneId,
            bool hittable,
            bool approachable,
            int authoredPriority,
            CharacterTestRuntimeAiJointAabb worldBounds)
        {
            TargetEntityId = targetEntityId;
            ZoneId = zoneId;
            Hittable = hittable;
            Approachable = approachable;
            AuthoredPriority = authoredPriority;
            WorldBounds = worldBounds;
        }

        internal int TargetEntityId { get; }
        internal int ZoneId { get; }
        internal bool Hittable { get; }
        internal bool Approachable { get; }
        internal int AuthoredPriority { get; }
        internal CharacterTestRuntimeAiJointAabb WorldBounds { get; }
    }

    internal readonly struct CharacterTestRuntimeAiJointCandidateSeed
    {
        internal CharacterTestRuntimeAiJointCandidateSeed(
            CharacterTestRuntimeAiJointActorInput actor,
            CharacterTestRuntimeAiJointActionInput action,
            int zoneId,
            bool zoneApproachable,
            int zonePriority,
            bool explicitEnvelopeIntersection)
        {
            Actor = actor;
            Action = action;
            ZoneId = zoneId;
            ZoneApproachable = zoneApproachable;
            ZonePriority = zonePriority;
            ExplicitEnvelopeIntersection = explicitEnvelopeIntersection;
        }

        internal CharacterTestRuntimeAiJointActorInput Actor { get; }
        internal CharacterTestRuntimeAiJointActionInput Action { get; }
        internal int TargetEntityId => Actor.TargetEntityId;
        internal int ZoneId { get; }
        internal bool ZoneApproachable { get; }
        internal int ZonePriority { get; }
        internal bool ExplicitEnvelopeIntersection { get; }
        internal bool IsCompatibility => Action.IsCompatibility;
    }

    internal readonly struct CharacterTestRuntimeAiJointGenerationBudget
    {
        internal const int DefaultActorScanLimit = 64;
        internal const int DefaultActorTopKLimit = 32;
        internal const int DefaultReadyActionLimit = 8;
        internal const int DefaultActionZoneBroadphaseLimit = 512;
        internal const int DefaultJointCandidateLimit = 512;
        internal const int DefaultLeafPreviewTraceLimit = 512;
        internal const int DefaultCandidatePosePreviewLimit = 512;
        internal const int DefaultPathQueryLimit = 512;
        internal const int HardActorScanLimit = 256;
        internal const int HardActorTopKLimit = 64;
        internal const int HardReadyActionLimit = 64;
        internal const int HardActionZoneBroadphaseLimit = 1024;
        internal const int HardJointCandidateLimit = 1024;
        internal const int HardLeafPreviewTraceLimit = 2048;
        internal const int HardCandidatePosePreviewLimit = 512;
        internal const int HardPathQueryLimit = 512;

        internal CharacterTestRuntimeAiJointGenerationBudget(
            int actorScanLimit = DefaultActorScanLimit,
            int actorTopKLimit = DefaultActorTopKLimit,
            int readyActionLimit = DefaultReadyActionLimit,
            int actionZoneBroadphaseLimit = DefaultActionZoneBroadphaseLimit,
            int jointCandidateLimit = DefaultJointCandidateLimit,
            int leafPreviewTraceLimit = DefaultLeafPreviewTraceLimit,
            int candidatePosePreviewLimit = DefaultCandidatePosePreviewLimit,
            int pathQueryLimit = DefaultPathQueryLimit)
        {
            ActorScanLimit = Clamp(actorScanLimit, HardActorScanLimit);
            ActorTopKLimit = Clamp(actorTopKLimit, HardActorTopKLimit);
            ReadyActionLimit = Clamp(readyActionLimit, HardReadyActionLimit);
            ActionZoneBroadphaseLimit = Clamp(
                actionZoneBroadphaseLimit,
                HardActionZoneBroadphaseLimit);
            JointCandidateLimit = Clamp(jointCandidateLimit, HardJointCandidateLimit);
            LeafPreviewTraceLimit = Clamp(
                leafPreviewTraceLimit,
                HardLeafPreviewTraceLimit);
            CandidatePosePreviewLimit = Clamp(
                candidatePosePreviewLimit,
                HardCandidatePosePreviewLimit);
            PathQueryLimit = Clamp(pathQueryLimit, HardPathQueryLimit);
        }

        internal int ActorScanLimit { get; }
        internal int ActorTopKLimit { get; }
        internal int ReadyActionLimit { get; }
        internal int ActionZoneBroadphaseLimit { get; }
        internal int JointCandidateLimit { get; }
        internal int LeafPreviewTraceLimit { get; }
        internal int CandidatePosePreviewLimit { get; }
        internal int PathQueryLimit { get; }

        internal CharacterTestRuntimeAiJointGenerationBudget Normalize()
        {
            return new CharacterTestRuntimeAiJointGenerationBudget(
                ActorScanLimit > 0 ? ActorScanLimit : DefaultActorScanLimit,
                ActorTopKLimit > 0 ? ActorTopKLimit : DefaultActorTopKLimit,
                ReadyActionLimit > 0 ? ReadyActionLimit : DefaultReadyActionLimit,
                ActionZoneBroadphaseLimit > 0
                    ? ActionZoneBroadphaseLimit
                    : DefaultActionZoneBroadphaseLimit,
                JointCandidateLimit > 0
                    ? JointCandidateLimit
                    : DefaultJointCandidateLimit,
                LeafPreviewTraceLimit > 0
                    ? LeafPreviewTraceLimit
                    : DefaultLeafPreviewTraceLimit,
                CandidatePosePreviewLimit > 0
                    ? CandidatePosePreviewLimit
                    : DefaultCandidatePosePreviewLimit,
                PathQueryLimit > 0 ? PathQueryLimit : DefaultPathQueryLimit);
        }

        private static int Clamp(int value, int hardLimit)
        {
            if (value <= 0)
                return 1;
            return value > hardLimit ? hardLimit : value;
        }
    }

    internal readonly struct CharacterTestRuntimeAiJointGenerationDiagnostics
    {
        internal CharacterTestRuntimeAiJointGenerationDiagnostics(
            int observedActorCount,
            int validActorCount,
            int retainedActorCount,
            int observedActionCount,
            int readyActionCount,
            int actionZoneBroadphaseCount,
            int candidateCount,
            CharacterTestRuntimeAiJointTruncationPhase truncationPhase,
            int truncationObservedCount,
            int truncationAcceptedCount,
            int truncationLimit)
        {
            ObservedActorCount = observedActorCount;
            ValidActorCount = validActorCount;
            RetainedActorCount = retainedActorCount;
            ObservedActionCount = observedActionCount;
            ReadyActionCount = readyActionCount;
            ActionZoneBroadphaseCount = actionZoneBroadphaseCount;
            CandidateCount = candidateCount;
            TruncationPhase = truncationPhase;
            TruncationObservedCount = truncationObservedCount;
            TruncationAcceptedCount = truncationAcceptedCount;
            TruncationLimit = truncationLimit;
        }

        internal int ObservedActorCount { get; }
        internal int ValidActorCount { get; }
        internal int RetainedActorCount { get; }
        internal int ObservedActionCount { get; }
        internal int ReadyActionCount { get; }
        internal int ActionZoneBroadphaseCount { get; }
        internal int CandidateCount { get; }
        internal CharacterTestRuntimeAiJointTruncationPhase TruncationPhase { get; }
        internal int TruncationObservedCount { get; }
        internal int TruncationAcceptedCount { get; }
        internal int TruncationLimit { get; }
        internal bool CandidateSetTruncated =>
            TruncationPhase != CharacterTestRuntimeAiJointTruncationPhase.None;
    }

    /// <summary>
    /// Allocation-free canonical coarse candidate generation for CD-M4. Inputs
    /// are immutable frame samples; no lock, reservation, cache or command state
    /// is mutated here.
    /// </summary>
    internal sealed class CharacterTestRuntimeAiJointCandidateGenerator
    {
        private readonly RankedActor[] _actorScratch =
            new RankedActor[CharacterTestRuntimeAiJointGenerationBudget.HardActorScanLimit];
        private readonly CharacterTestRuntimeAiJointActionInput[] _actionScratch =
            new CharacterTestRuntimeAiJointActionInput[
                CharacterTestRuntimeAiJointGenerationBudget.HardReadyActionLimit];
        private readonly CharacterTestRuntimeAiJointZoneInput[] _zoneScratch =
            new CharacterTestRuntimeAiJointZoneInput[
                CharacterTestRuntimeAiJointGenerationBudget.HardActionZoneBroadphaseLimit];
        private readonly CharacterTestRuntimeAiJointCandidateSeed[] _candidates =
            new CharacterTestRuntimeAiJointCandidateSeed[
                CharacterTestRuntimeAiJointGenerationBudget.HardJointCandidateLimit];

        private int _candidateCount;

        internal int CandidateCount => _candidateCount;

        internal CharacterTestRuntimeAiJointCandidateSeed GetCandidate(int index)
        {
            if ((uint)index >= (uint)_candidateCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _candidates[index];
        }

        internal CharacterTestRuntimeAiJointGenerationDiagnostics Generate(
            int sourceEntityId,
            int sourceTeamId,
            CharacterTestRuntimeAiJointActorInput[] actors,
            int actorCount,
            CharacterTestRuntimeAiJointActionInput[] actions,
            int actionCount,
            CharacterTestRuntimeAiJointZoneInput[] zones,
            int zoneCount,
            CharacterTestRuntimeAiJointGenerationBudget budget)
        {
            budget = budget.Normalize();
            _candidateCount = 0;
            int safeActorCount = ClampCount(actors, actorCount);
            int safeActionCount = ClampCount(actions, actionCount);
            int safeZoneCount = ClampCount(zones, zoneCount);
            CharacterTestRuntimeAiJointTruncationPhase truncationPhase =
                CharacterTestRuntimeAiJointTruncationPhase.None;
            int truncationObserved = 0;
            int truncationAccepted = 0;
            int truncationLimit = 0;

            int readyActionCount = CollectReadyActions(
                actions,
                safeActionCount,
                budget.ReadyActionLimit,
                ref truncationPhase,
                ref truncationObserved,
                ref truncationAccepted,
                ref truncationLimit);
            int canonicalZoneCount = CollectZones(zones, safeZoneCount);
            int validActorCount = CollectActors(
                sourceEntityId,
                sourceTeamId,
                actors,
                safeActorCount,
                readyActionCount,
                canonicalZoneCount,
                budget.ActorScanLimit,
                ref truncationPhase,
                ref truncationObserved,
                ref truncationAccepted,
                ref truncationLimit);

            SortActors(validActorCount);
            int retainedActorCount = validActorCount;
            if (retainedActorCount > budget.ActorTopKLimit)
            {
                SetFirstTruncation(
                    ref truncationPhase,
                    ref truncationObserved,
                    ref truncationAccepted,
                    ref truncationLimit,
                    CharacterTestRuntimeAiJointTruncationPhase.ActorTopK,
                    retainedActorCount,
                    budget.ActorTopKLimit,
                    budget.ActorTopKLimit);
                retainedActorCount = budget.ActorTopKLimit;
            }

            int broadphaseCount = 0;
            bool stop = false;
            for (int actorIndex = 0; actorIndex < retainedActorCount && !stop; actorIndex++)
            {
                RankedActor ranked = _actorScratch[actorIndex];
                for (int actionIndex = 0; actionIndex < readyActionCount && !stop; actionIndex++)
                {
                    CharacterTestRuntimeAiJointActionInput action = _actionScratch[actionIndex];
                    if (action.IsCompatibility)
                    {
                        stop = !TryAppendCandidate(
                            new CharacterTestRuntimeAiJointCandidateSeed(
                                ranked.Actor,
                                action,
                                0,
                                false,
                                int.MinValue,
                                false),
                            budget.JointCandidateLimit,
                            ref truncationPhase,
                            ref truncationObserved,
                            ref truncationAccepted,
                            ref truncationLimit);
                        continue;
                    }

                    for (int zoneIndex = 0; zoneIndex < canonicalZoneCount; zoneIndex++)
                    {
                        CharacterTestRuntimeAiJointZoneInput zone = _zoneScratch[zoneIndex];
                        if (zone.TargetEntityId != ranked.Actor.TargetEntityId || !zone.Hittable)
                            continue;
                        broadphaseCount++;
                        if (broadphaseCount > budget.ActionZoneBroadphaseLimit)
                        {
                            SetFirstTruncation(
                                ref truncationPhase,
                                ref truncationObserved,
                                ref truncationAccepted,
                                ref truncationLimit,
                                CharacterTestRuntimeAiJointTruncationPhase.ActionZoneBroadphase,
                                broadphaseCount,
                                broadphaseCount - 1,
                                budget.ActionZoneBroadphaseLimit);
                            stop = true;
                            break;
                        }

                        bool intersects = action.ConservativeWorldEnvelope.Intersects(zone.WorldBounds);
                        if (!intersects && !zone.Approachable)
                            continue;
                        if (!TryAppendCandidate(
                                new CharacterTestRuntimeAiJointCandidateSeed(
                                    ranked.Actor,
                                    action,
                                    zone.ZoneId,
                                    zone.Approachable,
                                    zone.AuthoredPriority,
                                    intersects),
                                budget.JointCandidateLimit,
                                ref truncationPhase,
                                ref truncationObserved,
                                ref truncationAccepted,
                                ref truncationLimit))
                        {
                            stop = true;
                            break;
                        }
                    }
                }
            }

            return new CharacterTestRuntimeAiJointGenerationDiagnostics(
                safeActorCount,
                validActorCount,
                retainedActorCount,
                safeActionCount,
                readyActionCount,
                broadphaseCount,
                _candidateCount,
                truncationPhase,
                truncationObserved,
                truncationAccepted,
                truncationLimit);
        }

        private int CollectReadyActions(
            CharacterTestRuntimeAiJointActionInput[] actions,
            int actionCount,
            int limit,
            ref CharacterTestRuntimeAiJointTruncationPhase truncationPhase,
            ref int truncationObserved,
            ref int truncationAccepted,
            ref int truncationLimit)
        {
            int count = 0;
            for (int i = 0; i < actionCount; i++)
            {
                CharacterTestRuntimeAiJointActionInput action = actions[i];
                if (!action.Ready
                    || !action.Resolvable
                    || action.PlannerActionId <= 0
                    || !action.ResolvedActionId.IsValid)
                {
                    continue;
                }

                InsertActionCanonical(action, ref count, limit);
            }
            if (count > limit)
                count = limit;
            if (CountReadyActions(actions, actionCount) > limit)
            {
                SetFirstTruncation(
                    ref truncationPhase,
                    ref truncationObserved,
                    ref truncationAccepted,
                    ref truncationLimit,
                    CharacterTestRuntimeAiJointTruncationPhase.ReadyAction,
                    CountReadyActions(actions, actionCount),
                    limit,
                    limit);
            }
            return count;
        }

        private void InsertActionCanonical(
            CharacterTestRuntimeAiJointActionInput action,
            ref int count,
            int limit)
        {
            int capacity = limit < _actionScratch.Length ? limit : _actionScratch.Length;
            int insert = count < capacity ? count : capacity;
            while (insert > 0 && CompareAction(action, _actionScratch[insert - 1]) < 0)
                insert--;
            if (insert >= capacity)
                return;
            int moveEnd = count < capacity ? count : capacity - 1;
            for (int i = moveEnd; i > insert; i--)
                _actionScratch[i] = _actionScratch[i - 1];
            _actionScratch[insert] = action;
            if (count < capacity)
                count++;
        }

        private int CollectZones(CharacterTestRuntimeAiJointZoneInput[] zones, int zoneCount)
        {
            int count = zoneCount < _zoneScratch.Length ? zoneCount : _zoneScratch.Length;
            for (int i = 0; i < count; i++)
                _zoneScratch[i] = zones[i];
            for (int i = 1; i < count; i++)
            {
                CharacterTestRuntimeAiJointZoneInput value = _zoneScratch[i];
                int index = i;
                while (index > 0 && CompareZone(value, _zoneScratch[index - 1]) < 0)
                {
                    _zoneScratch[index] = _zoneScratch[index - 1];
                    index--;
                }
                _zoneScratch[index] = value;
            }
            return count;
        }

        private int CollectActors(
            int sourceEntityId,
            int sourceTeamId,
            CharacterTestRuntimeAiJointActorInput[] actors,
            int actorCount,
            int readyActionCount,
            int zoneCount,
            int limit,
            ref CharacterTestRuntimeAiJointTruncationPhase truncationPhase,
            ref int truncationObserved,
            ref int truncationAccepted,
            ref int truncationLimit)
        {
            int validCount = 0;
            int observedValidCount = 0;
            int capacity = limit < _actorScratch.Length ? limit : _actorScratch.Length;
            for (int i = 0; i < actorCount; i++)
            {
                CharacterTestRuntimeAiJointActorInput actor = actors[i];
                if (actor.TargetEntityId <= 0
                    || actor.TargetEntityId == sourceEntityId
                    || actor.TeamId == sourceTeamId
                    || actor.LifeState != CharacterTestCharacterLifeState.Alive
                    || !actor.SnapshotCurrent
                    || !actor.MaskCompatible)
                {
                    continue;
                }
                bool explicitIntersection =
                    HasExplicitIntersection(actor.TargetEntityId, readyActionCount, zoneCount);
                if (!explicitIntersection && !actor.WithinSoftSearchRadius)
                    continue;
                observedValidCount++;
                var ranked = new RankedActor(
                    actor,
                    explicitIntersection);
                InsertActorCanonical(ranked, ref validCount, capacity);
            }
            if (observedValidCount > capacity)
            {
                SetFirstTruncation(
                    ref truncationPhase,
                    ref truncationObserved,
                    ref truncationAccepted,
                    ref truncationLimit,
                    CharacterTestRuntimeAiJointTruncationPhase.ActorScan,
                    observedValidCount,
                    capacity,
                    capacity);
            }
            return validCount;
        }

        private void InsertActorCanonical(RankedActor actor, ref int count, int capacity)
        {
            int insert = count < capacity ? count : capacity;
            while (insert > 0 && CompareActorRank(actor, _actorScratch[insert - 1]) < 0)
                insert--;
            if (insert >= capacity)
                return;
            int moveEnd = count < capacity ? count : capacity - 1;
            for (int i = moveEnd; i > insert; i--)
                _actorScratch[i] = _actorScratch[i - 1];
            _actorScratch[insert] = actor;
            if (count < capacity)
                count++;
        }

        private bool HasExplicitIntersection(int targetEntityId, int actionCount, int zoneCount)
        {
            for (int actionIndex = 0; actionIndex < actionCount; actionIndex++)
            {
                CharacterTestRuntimeAiJointActionInput action = _actionScratch[actionIndex];
                if (action.IsCompatibility)
                    continue;
                for (int zoneIndex = 0; zoneIndex < zoneCount; zoneIndex++)
                {
                    CharacterTestRuntimeAiJointZoneInput zone = _zoneScratch[zoneIndex];
                    if (zone.TargetEntityId == targetEntityId
                        && zone.Hittable
                        && action.ConservativeWorldEnvelope.Intersects(zone.WorldBounds))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void SortActors(int count)
        {
            for (int i = 1; i < count; i++)
            {
                RankedActor value = _actorScratch[i];
                int index = i;
                while (index > 0 && CompareActorRank(value, _actorScratch[index - 1]) < 0)
                {
                    _actorScratch[index] = _actorScratch[index - 1];
                    index--;
                }
                _actorScratch[index] = value;
            }
        }

        private bool TryAppendCandidate(
            CharacterTestRuntimeAiJointCandidateSeed candidate,
            int limit,
            ref CharacterTestRuntimeAiJointTruncationPhase truncationPhase,
            ref int truncationObserved,
            ref int truncationAccepted,
            ref int truncationLimit)
        {
            if (_candidateCount >= limit || _candidateCount >= _candidates.Length)
            {
                SetFirstTruncation(
                    ref truncationPhase,
                    ref truncationObserved,
                    ref truncationAccepted,
                    ref truncationLimit,
                    CharacterTestRuntimeAiJointTruncationPhase.JointCandidate,
                    _candidateCount + 1,
                    _candidateCount,
                    limit);
                return false;
            }
            _candidates[_candidateCount++] = candidate;
            return true;
        }

        private static int CountReadyActions(
            CharacterTestRuntimeAiJointActionInput[] actions,
            int count)
        {
            int ready = 0;
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiJointActionInput action = actions[i];
                if (action.Ready
                    && action.Resolvable
                    && action.PlannerActionId > 0
                    && action.ResolvedActionId.IsValid)
                {
                    ready++;
                }
            }
            return ready;
        }

        private static int CompareActorRank(RankedActor left, RankedActor right)
        {
            int intersection = right.ExplicitIntersection.CompareTo(left.ExplicitIntersection);
            if (intersection != 0)
                return intersection;
            int distance = left.Actor.RootSpatialDistanceMillimetersSquared.CompareTo(
                right.Actor.RootSpatialDistanceMillimetersSquared);
            if (distance != 0)
                return distance;
            return left.Actor.TargetEntityId.CompareTo(right.Actor.TargetEntityId);
        }

        private static int CompareAction(
            CharacterTestRuntimeAiJointActionInput left,
            CharacterTestRuntimeAiJointActionInput right)
        {
            int stable = left.ResolvedActionId.CompareTo(right.ResolvedActionId);
            if (stable != 0)
                return stable;
            return left.PlannerActionId.CompareTo(right.PlannerActionId);
        }

        private static int CompareZone(
            CharacterTestRuntimeAiJointZoneInput left,
            CharacterTestRuntimeAiJointZoneInput right)
        {
            int target = left.TargetEntityId.CompareTo(right.TargetEntityId);
            if (target != 0)
                return target;
            return left.ZoneId.CompareTo(right.ZoneId);
        }

        private static int ClampCount<T>(T[] values, int count)
        {
            if (values == null || count <= 0)
                return 0;
            return count < values.Length ? count : values.Length;
        }

        private static void SetFirstTruncation(
            ref CharacterTestRuntimeAiJointTruncationPhase phase,
            ref int observed,
            ref int accepted,
            ref int limit,
            CharacterTestRuntimeAiJointTruncationPhase nextPhase,
            int nextObserved,
            int nextAccepted,
            int nextLimit)
        {
            if (phase != CharacterTestRuntimeAiJointTruncationPhase.None)
                return;
            phase = nextPhase;
            observed = nextObserved;
            accepted = nextAccepted;
            limit = nextLimit;
        }

        private readonly struct RankedActor
        {
            internal RankedActor(
                CharacterTestRuntimeAiJointActorInput actor,
                bool explicitIntersection)
            {
                Actor = actor;
                ExplicitIntersection = explicitIntersection;
            }

            internal CharacterTestRuntimeAiJointActorInput Actor { get; }
            internal bool ExplicitIntersection { get; }
        }
    }
}
