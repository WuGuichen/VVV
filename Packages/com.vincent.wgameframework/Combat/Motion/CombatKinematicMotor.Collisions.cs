using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Motion
{
    public sealed partial class CombatKinematicMotor
    {
        private FixVector3 ResolveCollisions(
            CombatFrame frame,
            FixVector3 startPosition,
            FixVector3 desiredDelta,
            Fix64 wallSlideMinimumHorizontalDelta,
            bool allowStepUp,
            CombatPhysicsWorld physicsWorld,
            CombatEntityId sourceEntityId,
            CombatKinematicMotorCandidateCache candidateCache,
            out CombatMotionCollisionFlags collisionFlags,
            CombatBodyId ignoredBodyA = default,
            CombatBodyId ignoredBodyB = default,
            bool allowOpposingWallRecovery = true)
        {
            collisionFlags = CombatMotionCollisionFlags.None;
            if (physicsWorld == null || desiredDelta.IsZero || Config.ObstacleLayerMask.Value == 0)
            {
                return startPosition + desiredDelta;
            }

            PrepareStepBroadphaseCache(startPosition, desiredDelta, allowStepUp, physicsWorld, sourceEntityId, candidateCache);
            try
            {
                if (_stepBaseEligibleBroadphaseCandidateIndices.Count == 0)
                {
                    return startPosition + desiredDelta;
                }

                FixVector3 position = startPosition;
                FixVector3 remaining = desiredDelta;
                Fix64 remainingTimeFraction = Fix64.One;
                CombatMotionCollision previousWall = default;
                bool hasPreviousWall = false;
                for (int iteration = 0; iteration < Config.MaxCollisionIterations; iteration++)
                {
                    if (remaining.IsZero)
                    {
                        return position;
                    }

                    if (!TryFindFirstCollision(
                            position,
                            remaining,
                            physicsWorld,
                            sourceEntityId,
                            out CombatMotionCollision collision,
                            out CombatPhysicsBroadphaseCandidate collisionCandidate,
                            out FixVector3 collisionNonEnteringDirection,
                            out Fix64 collisionDeltaLength,
                            out bool hasCollisionDeltaLength,
                            ignoredBodyA,
                            ignoredBodyB))
                    {
                        return position + remaining;
                    }

                    CombatMotionCollisionFlags frameFlags = ClassifyCollision(collision.Normal);
                    bool wallCollision =
                        (frameFlags & CombatMotionCollisionFlags.Wall) != 0;
                    if (allowOpposingWallRecovery
                        && wallCollision
                        && hasPreviousWall
                        && !collision.TargetBodyId.Equals(previousWall.TargetBodyId)
                        && AreOpposingWallConstraints(previousWall.Normal, collision.Normal))
                    {
                        collisionFlags |= frameFlags;
                        _collisions.Add(new CombatMotionCollision(
                            collision.TargetEntityId,
                            collision.TargetBodyId,
                            collision.TargetColliderId,
                            collision.Normal,
                            collision.Distance,
                            collision.Fraction,
                            frameFlags,
                            collision.SurfaceMaterial));
                        FixVector3 recoverySourceDelta = desiredDelta * remainingTimeFraction;
                        if (!TryBuildOpposingWallEscapeDelta(
                                recoverySourceDelta,
                                previousWall.Normal,
                                collision.Normal,
                                out FixVector3 escapeDelta))
                        {
                            return position;
                        }

                        FixVector3 recoveredPosition = ResolveCollisions(
                            frame,
                            position,
                            escapeDelta,
                            wallSlideMinimumHorizontalDelta,
                            allowStepUp,
                            physicsWorld,
                            sourceEntityId,
                            candidateCache,
                            out CombatMotionCollisionFlags recoveryFlags,
                            previousWall.TargetBodyId,
                            collision.TargetBodyId,
                            allowOpposingWallRecovery: false);
                        collisionFlags |= recoveryFlags;
                        return recoveredPosition;
                    }

                    if (wallCollision)
                    {
                        previousWall = collision;
                        hasPreviousWall = true;
                    }
                    if (TryResolveStepUp(
                        position,
                        remaining,
                        collisionCandidate,
                        frameFlags,
                        allowStepUp,
                        physicsWorld,
                        sourceEntityId,
                        candidateCache,
                        out FixVector3 stepPosition,
                        out CombatMotionCollision stepCollision))
                    {
                        CombatMotionCollisionFlags stepFlags = CombatMotionCollisionFlags.Grounded | CombatMotionCollisionFlags.StepUp;
                        collisionFlags |= stepFlags;
                        _collisions.Add(new CombatMotionCollision(
                            stepCollision.TargetEntityId,
                            stepCollision.TargetBodyId,
                            stepCollision.TargetColliderId,
                            stepCollision.Normal,
                            stepCollision.Distance,
                            stepCollision.Fraction,
                            stepFlags,
                            stepCollision.SurfaceMaterial));
                        return stepPosition;
                    }

                    collisionFlags |= frameFlags;
                    _collisions.Add(new CombatMotionCollision(
                        collision.TargetEntityId,
                        collision.TargetBodyId,
                        collision.TargetColliderId,
                        collision.Normal,
                        collision.Distance,
                        collision.Fraction,
                        frameFlags,
                        collision.SurfaceMaterial));

                    Fix64 safeFraction = CalculateSafeFraction(
                        collision,
                        remaining,
                        hasCollisionDeltaLength,
                        collisionDeltaLength);
                    FixVector3 safeDelta = remaining * safeFraction;
                    position += safeDelta;

                    bool preserveWallTangentTime =
                        (frameFlags & CombatMotionCollisionFlags.Wall) != 0
                        && (frameFlags & CombatMotionCollisionFlags.SteepSlope) == 0;
                    Fix64 remainingFraction = Fix64.One - (preserveWallTangentTime
                        ? safeFraction
                        : collision.Fraction);
                    if (remainingFraction <= Fix64.Zero)
                    {
                        return position;
                    }

                    remainingTimeFraction *= remainingFraction;
                    FixVector3 afterHit = remaining * remainingFraction;
                    remaining = ResolveRemainingDelta(
                        afterHit,
                        collision.Normal,
                        collisionNonEnteringDirection,
                        frameFlags,
                        wallSlideMinimumHorizontalDelta * remainingTimeFraction);
                }

                collisionFlags |= CombatMotionCollisionFlags.IterationLimit;
                return position;
            }
            finally
            {
                ClearStepBroadphaseCache();
            }
        }

        private static bool TryBuildOpposingWallEscapeDelta(
            FixVector3 desiredDelta,
            FixVector3 firstNormal,
            FixVector3 secondNormal,
            out FixVector3 escapeDelta)
        {
            escapeDelta = default;
            FixVector3 firstHorizontal = Horizontal(firstNormal);
            FixVector3 secondHorizontal = Horizontal(secondNormal);

            FixVector3 separator = firstHorizontal - secondHorizontal;
            if (!separator.TryNormalize(out FixVector3 separatorNormal))
                return false;

            FixVector3 requestedHorizontal = new FixVector3(
                desiredDelta.X,
                Fix64.Zero,
                desiredDelta.Z);
            if (requestedHorizontal.IsZero)
                return false;

            FixVector3 tangent = ProjectOnPlane(requestedHorizontal, separatorNormal);
            Fix64 requestedLengthSquared = requestedHorizontal.LengthSquared();
            Fix64 tangentLengthSquared = tangent.LengthSquared();
            if (tangentLengthSquared < requestedLengthSquared / Fix64.FromInt(16))
                return false;

            escapeDelta = new FixVector3(tangent.X, desiredDelta.Y, tangent.Z);
            return !escapeDelta.IsZero;
        }

        private static bool AreOpposingWallConstraints(
            FixVector3 firstNormal,
            FixVector3 secondNormal)
        {
            FixVector3 firstHorizontal = Horizontal(firstNormal);
            FixVector3 secondHorizontal = Horizontal(secondNormal);
            return !firstHorizontal.IsZero
                && !secondHorizontal.IsZero
                && firstHorizontal.Dot(secondHorizontal) < Fix64.Zero;
        }

        private static FixVector3 Horizontal(FixVector3 value)
        {
            return new FixVector3(value.X, Fix64.Zero, value.Z);
        }

        private void ClearStepBroadphaseCache()
        {
            _stepBroadphaseCacheActive = false;
            _stepBroadphaseCacheIncludesStepUpEnvelope = false;
            _stepHasStepUpEnvelope = false;
            _stepStartMin = default;
            _stepStartMax = default;
            _stepDesiredDelta = default;
            _stepBroadphaseCandidateView = Array.Empty<CombatPhysicsBroadphaseCandidate>();
            _stepBaseEligibleBroadphaseCandidateIndices.Clear();
            _stepEligibleBroadphaseCandidateIndices.Clear();
        }

        private bool TryResolveStepUp(
            FixVector3 position,
            FixVector3 remaining,
            CombatPhysicsBroadphaseCandidate blockingCandidate,
            CombatMotionCollisionFlags collisionFlags,
            bool allowStepUp,
            CombatPhysicsWorld physicsWorld,
            CombatEntityId sourceEntityId,
            CombatKinematicMotorCandidateCache candidateCache,
            out FixVector3 stepPosition,
            out CombatMotionCollision stepCollision)
        {
            stepPosition = default;
            stepCollision = default;
            if (!allowStepUp
                || Config.MaxStepHeight <= Fix64.Zero
                || physicsWorld == null
                || (collisionFlags & CombatMotionCollisionFlags.Wall) == 0)
            {
                return false;
            }

            if (!CanAttemptStepUpOverAabb(
                    position,
                    blockingCandidate))
            {
                return false;
            }

            FixVector3 horizontalDelta = new FixVector3(remaining.X, Fix64.Zero, remaining.Z);
            if (horizontalDelta.IsZero)
                return false;

            EnsureStepUpBroadphaseCache(physicsWorld, sourceEntityId, candidateCache);
            FixVector3 stepUpDelta = new FixVector3(Fix64.Zero, Config.MaxStepHeight, Fix64.Zero);
            _lastStepUpProbeCount++;
            if (TryFindFirstCollision(
                position,
                stepUpDelta,
                physicsWorld,
                sourceEntityId,
                out _,
                out _,
                out _,
                out _,
                out _))
            {
                return false;
            }

            FixVector3 raisedPosition = position + stepUpDelta;
            _lastStepUpProbeCount++;
            if (TryFindFirstCollision(
                raisedPosition,
                horizontalDelta,
                physicsWorld,
                sourceEntityId,
                out _,
                out _,
                out _,
                out _,
                out _))
            {
                return false;
            }

            FixVector3 horizontalPosition = raisedPosition + horizontalDelta;
            FixVector3 stepDownDelta = new FixVector3(
                Fix64.Zero,
                -(Config.MaxStepHeight + Config.SkinWidth + Config.SkinWidth),
                Fix64.Zero);
            _lastStepUpProbeCount++;
            if (!TryFindFirstCollision(
                horizontalPosition,
                stepDownDelta,
                physicsWorld,
                sourceEntityId,
                out CombatMotionCollision groundCollision,
                out _,
                out _,
                out Fix64 stepDownDeltaLength,
                out bool hasStepDownDeltaLength))
            {
                return false;
            }

            CombatMotionCollisionFlags groundFlags = ClassifyCollision(groundCollision.Normal);
            if ((groundFlags & CombatMotionCollisionFlags.Grounded) == 0)
                return false;

            Fix64 safeFraction = CalculateSafeFraction(
                groundCollision,
                stepDownDelta,
                hasStepDownDeltaLength,
                stepDownDeltaLength);
            stepPosition = horizontalPosition + (stepDownDelta * safeFraction);
            stepCollision = groundCollision;
            return true;
        }

        private bool TryFindFirstCollision(
            FixVector3 position,
            FixVector3 delta,
            CombatPhysicsWorld physicsWorld,
            CombatEntityId sourceEntityId,
            out CombatMotionCollision collision,
            out CombatPhysicsBroadphaseCandidate collisionCandidate,
            out FixVector3 nonEnteringDirection,
            out Fix64 resolvedDeltaLength,
            out bool hasResolvedDeltaLength,
            CombatBodyId ignoredBodyA = default,
            CombatBodyId ignoredBodyB = default)
        {
            Config.CapsuleProxy.GetBounds(position, out FixVector3 startMin, out FixVector3 startMax);
            FixVector3 endMin = startMin + delta;
            FixVector3 endMax = startMax + delta;
            FixVector3 skin = SkinVector();
            FixVector3 queryMin = Min(startMin, endMin) - skin;
            FixVector3 queryMax = Max(startMax, endMax) + skin;
            Fix64 deltaLength = default;
            bool hasDeltaLength = false;
            FixVector3 capsuleCenter = default;
            bool hasCapsuleCenter = false;

            _lastQueryCount++;
            IReadOnlyList<CombatPhysicsBroadphaseCandidate> candidates =
                ResolveBroadphaseCandidates(
                    physicsWorld,
                    queryMin,
                    queryMax,
                    sourceEntityId,
                    out bool filterByQueryAabb,
                    out bool candidatesAreEligible,
                    out List<int> cachedCandidateIndices);

            bool found = false;
            int filteredCandidateCount = 0;
            CombatMotionCollision best = default;
            CombatPhysicsBroadphaseCandidate bestCandidate = default;
            FixVector3 bestNonEnteringDirection = default;
            CombatSurfaceMaterial bestSurfaceMaterial = default;
            FixVector3 bestPrefixMin = default;
            FixVector3 bestPrefixMax = default;
            int candidateCount = cachedCandidateIndices != null
                ? cachedCandidateIndices.Count
                : candidates.Count;
            for (int i = 0; i < candidateCount; i++)
            {
                int candidateIndex = cachedCandidateIndices != null
                    ? cachedCandidateIndices[i]
                    : i;
                CombatPhysicsBroadphaseCandidate hit = candidates[candidateIndex];
                if ((!ignoredBodyA.Equals(CombatBodyId.None)
                        && hit.Body.BodyId.Equals(ignoredBodyA))
                    || (!ignoredBodyB.Equals(CombatBodyId.None)
                        && hit.Body.BodyId.Equals(ignoredBodyB)))
                {
                    continue;
                }
                if (filterByQueryAabb && !OverlapsInclusive(queryMin, queryMax, hit.Min, hit.Max))
                {
                    continue;
                }

                filteredCandidateCount++;
                if (!candidatesAreEligible
                    && !CombatPhysicsWorld.CanHitBroadphaseCandidate(
                        sourceEntityId,
                        Config.ObstacleLayerMask,
                        includeSourceEntity: false,
                        hit))
                {
                    continue;
                }

                if (found && !OverlapsInclusive(bestPrefixMin, bestPrefixMax, hit.Min, hit.Max))
                {
                    _lastEarlyCandidateSkipCount++;
                    continue;
                }

                _lastNarrowphaseCandidateCount++;
                if (!TryBuildCollisionFromCandidate(
                        position,
                        delta,
                        ref hasDeltaLength,
                        ref deltaLength,
                        startMin,
                        startMax,
                        ref hasCapsuleCenter,
                        ref capsuleCenter,
                        hit,
                        out CombatMotionCollision candidate,
                        out FixVector3 candidateNonEnteringDirection))
                {
                    continue;
                }

                if (!found)
                {
                    best = candidate;
                    bestCandidate = hit;
                    bestNonEnteringDirection = candidateNonEnteringDirection;
                    bestSurfaceMaterial = candidate.SurfaceMaterial;
                    BuildBestPrefixAabb(
                        startMin,
                        startMax,
                        delta,
                        skin,
                        best.Fraction,
                        out bestPrefixMin,
                        out bestPrefixMax);
                    found = true;
                    continue;
                }

                int compare = CompareCollision(candidate, best);
                if (compare < 0)
                {
                    if (candidate.Fraction == best.Fraction && candidate.Normal.Equals(best.Normal))
                    {
                        bestSurfaceMaterial = SelectHigherPrioritySurfaceMaterial(
                            bestSurfaceMaterial,
                            candidate.SurfaceMaterial);
                    }
                    else
                    {
                        bestSurfaceMaterial = candidate.SurfaceMaterial;
                    }

                    best = candidate;
                    bestCandidate = hit;
                    bestNonEnteringDirection = candidateNonEnteringDirection;
                    BuildBestPrefixAabb(
                        startMin,
                        startMax,
                        delta,
                        skin,
                        best.Fraction,
                        out bestPrefixMin,
                        out bestPrefixMax);
                    continue;
                }

                if (candidate.Fraction == best.Fraction && candidate.Normal.Equals(best.Normal))
                {
                    bestSurfaceMaterial = SelectHigherPrioritySurfaceMaterial(
                        bestSurfaceMaterial,
                        candidate.SurfaceMaterial);
                }
            }

            if (found)
            {
                best = WithSurfaceMaterial(best, bestSurfaceMaterial);
                best = WithFlags(best, ClassifyCollision(best.Normal));
            }

            _lastBroadphaseCandidateCount += filteredCandidateCount;
            collision = best;
            collisionCandidate = bestCandidate;
            nonEnteringDirection = bestNonEnteringDirection;
            resolvedDeltaLength = deltaLength;
            hasResolvedDeltaLength = hasDeltaLength;
            return found;
        }

        private void PrepareStepBroadphaseCache(
            FixVector3 startPosition,
            FixVector3 desiredDelta,
            bool allowStepUp,
            CombatPhysicsWorld physicsWorld,
            CombatEntityId sourceEntityId,
            CombatKinematicMotorCandidateCache candidateCache)
        {
            _stepBroadphaseCacheActive = false;
            _stepBroadphaseCacheIncludesStepUpEnvelope = false;
            _stepHasStepUpEnvelope = false;
            _stepBroadphaseCandidateView = Array.Empty<CombatPhysicsBroadphaseCandidate>();
            _stepBaseEligibleBroadphaseCandidateIndices.Clear();
            _stepEligibleBroadphaseCandidateIndices.Clear();

            Config.CapsuleProxy.GetBounds(startPosition, out FixVector3 startMin, out FixVector3 startMax);
            _stepStartMin = startMin;
            _stepStartMax = startMax;
            _stepDesiredDelta = desiredDelta;
            FixVector3 endMin = startMin + desiredDelta;
            FixVector3 endMax = startMax + desiredDelta;
            FixVector3 skin = SkinVector();
            Fix64 horizontalPadding = Config.SkinWidth;
            FixVector3 baseQueryMin = Min(startMin, endMin) - new FixVector3(
                horizontalPadding,
                Config.SkinWidth,
                horizontalPadding);
            FixVector3 baseQueryMax = Max(startMax, endMax) + new FixVector3(
                horizontalPadding,
                Config.SkinWidth,
                horizontalPadding);
            bool hasHorizontalDelta = !desiredDelta.X.IsZero || !desiredDelta.Z.IsZero;
            if (allowStepUp && hasHorizontalDelta && Config.MaxStepHeight > Fix64.Zero)
            {
                Fix64 verticalPadding = Config.MaxStepHeight + skin.Y + skin.Y;
                _stepUpQueryMin = new FixVector3(baseQueryMin.X, baseQueryMin.Y - verticalPadding, baseQueryMin.Z);
                _stepUpQueryMax = new FixVector3(baseQueryMax.X, baseQueryMax.Y + verticalPadding, baseQueryMax.Z);
                _stepHasStepUpEnvelope = true;
            }

            PopulateStepBroadphaseCache(
                baseQueryMin,
                baseQueryMax,
                baseQueryMin,
                baseQueryMax,
                physicsWorld,
                sourceEntityId,
                candidateCache,
                includesStepUpEnvelope: false);
        }

        private void EnsureStepUpBroadphaseCache(
            CombatPhysicsWorld physicsWorld,
            CombatEntityId sourceEntityId,
            CombatKinematicMotorCandidateCache candidateCache)
        {
            if (!_stepBroadphaseCacheActive
                || _stepBroadphaseCacheIncludesStepUpEnvelope
                || !_stepHasStepUpEnvelope)
            {
                return;
            }

            PopulateStepBroadphaseCache(
                _stepUpQueryMin,
                _stepUpQueryMax,
                _stepBaseQueryMin,
                _stepBaseQueryMax,
                physicsWorld,
                sourceEntityId,
                candidateCache,
                includesStepUpEnvelope: true);
        }

        private void PopulateStepBroadphaseCache(
            FixVector3 queryMin,
            FixVector3 queryMax,
            FixVector3 baseQueryMin,
            FixVector3 baseQueryMax,
            CombatPhysicsWorld physicsWorld,
            CombatEntityId sourceEntityId,
            CombatKinematicMotorCandidateCache candidateCache,
            bool includesStepUpEnvelope)
        {
            _stepBroadphaseCacheActive = false;
            _stepBroadphaseCandidateView = Array.Empty<CombatPhysicsBroadphaseCandidate>();
            _stepBaseEligibleBroadphaseCandidateIndices.Clear();
            _stepEligibleBroadphaseCandidateIndices.Clear();

            int broadphaseCellCount;
            IReadOnlyList<CombatPhysicsBroadphaseCandidate> candidates;
            if (candidateCache != null
                && candidateCache.TryCollectCandidatesAabb(
                    physicsWorld,
                    queryMin,
                    queryMax,
                    CombatPhysicsBroadphaseCandidateOrdering.Unsorted,
                    sourceEntityId,
                    Config.ObstacleLayerMask,
                    includeSourceEntity: false,
                    _stepCachedBroadphaseCandidates,
                    out broadphaseCellCount))
            {
                candidates = _stepCachedBroadphaseCandidates;
                _lastCandidateCacheHitCount++;
            }
            else
            {
                CombatPhysicsBroadphaseResult broadphase = physicsWorld.CollectBroadphaseCandidatesAabb(
                    queryMin,
                    queryMax,
                    CombatPhysicsBroadphaseCandidateOrdering.Unsorted,
                    sourceEntityId,
                    Config.ObstacleLayerMask,
                    includeSourceEntity: false);
                candidates = broadphase.Candidates;
                broadphaseCellCount = broadphase.CellCount;
                _lastCandidateCacheFallbackCount++;
            }

            if (_stepEligibleBroadphaseCandidateIndices.Capacity < candidates.Count)
            {
                _stepEligibleBroadphaseCandidateIndices.Capacity = candidates.Count;
            }

            if (_stepBaseEligibleBroadphaseCandidateIndices.Capacity < candidates.Count)
            {
                _stepBaseEligibleBroadphaseCandidateIndices.Capacity = candidates.Count;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                CombatPhysicsBroadphaseCandidate candidate = candidates[i];
                _stepEligibleBroadphaseCandidateIndices.Add(i);
                if (OverlapsInclusive(baseQueryMin, baseQueryMax, candidate.Min, candidate.Max))
                {
                    _stepBaseEligibleBroadphaseCandidateIndices.Add(i);
                }
            }

            PrioritizeStepBroadphaseCandidateIndices(candidates);

            _stepBaseQueryMin = baseQueryMin;
            _stepBaseQueryMax = baseQueryMax;
            _stepBroadphaseCandidateView = candidates;
            _lastBroadphaseCellCount += broadphaseCellCount;
            _stepBroadphaseCacheIncludesStepUpEnvelope = includesStepUpEnvelope;
            _stepBroadphaseCacheActive = true;
        }

        private IReadOnlyList<CombatPhysicsBroadphaseCandidate> ResolveBroadphaseCandidates(
            CombatPhysicsWorld physicsWorld,
            FixVector3 queryMin,
            FixVector3 queryMax,
            CombatEntityId sourceEntityId,
            out bool filterByQueryAabb,
            out bool candidatesAreEligible,
            out List<int> cachedCandidateIndices)
        {
            if (!_stepBroadphaseCacheActive)
            {
                CombatPhysicsBroadphaseResult broadphase = physicsWorld.CollectBroadphaseCandidatesAabb(
                    queryMin,
                    queryMax,
                    CombatPhysicsBroadphaseCandidateOrdering.Unsorted,
                    sourceEntityId,
                    Config.ObstacleLayerMask,
                    includeSourceEntity: false);
                _lastBroadphaseCellCount += broadphase.CellCount;
                filterByQueryAabb = false;
                candidatesAreEligible = true;
                cachedCandidateIndices = null;
                return broadphase.Candidates;
            }

            candidatesAreEligible = true;
            bool useBaseCandidates = ContainsInclusive(
                _stepBaseQueryMin,
                _stepBaseQueryMax,
                queryMin,
                queryMax);
            cachedCandidateIndices = useBaseCandidates
                ? _stepBaseEligibleBroadphaseCandidateIndices
                : _stepEligibleBroadphaseCandidateIndices;
            filterByQueryAabb = useBaseCandidates
                ? !SameAabb(_stepBaseQueryMin, _stepBaseQueryMax, queryMin, queryMax)
                : !(_stepBroadphaseCacheIncludesStepUpEnvelope
                    && SameAabb(_stepUpQueryMin, _stepUpQueryMax, queryMin, queryMax));
            return _stepBroadphaseCandidateView;
        }

        private void PrioritizeStepBroadphaseCandidateIndices(IReadOnlyList<CombatPhysicsBroadphaseCandidate> candidates)
        {
            PrioritizeFirstStepBroadphaseCandidate(_stepEligibleBroadphaseCandidateIndices, candidates);
            PrioritizeFirstStepBroadphaseCandidate(_stepBaseEligibleBroadphaseCandidateIndices, candidates);
        }

        private void PrioritizeFirstStepBroadphaseCandidate(
            List<int> candidateIndices,
            IReadOnlyList<CombatPhysicsBroadphaseCandidate> candidates)
        {
            if (candidateIndices.Count <= 1)
            {
                return;
            }

            int bestListIndex = 0;
            int bestCandidateIndex = candidateIndices[0];
            CombatPhysicsBroadphaseCandidate bestCandidate = candidates[bestCandidateIndex];
            Fix64 bestEntry = CalculateSweepEntryFraction(
                _stepStartMin,
                _stepStartMax,
                _stepDesiredDelta,
                bestCandidate.Min,
                bestCandidate.Max);
            for (int i = 1; i < candidateIndices.Count; i++)
            {
                int candidateIndex = candidateIndices[i];
                CombatPhysicsBroadphaseCandidate candidate = candidates[candidateIndex];
                Fix64 candidateEntry = CalculateSweepEntryFraction(
                    _stepStartMin,
                    _stepStartMax,
                    _stepDesiredDelta,
                    candidate.Min,
                    candidate.Max);
                if (CompareSweepCandidatePriority(candidateEntry, candidate, bestEntry, bestCandidate) < 0)
                {
                    bestListIndex = i;
                    bestCandidateIndex = candidateIndex;
                    bestCandidate = candidate;
                    bestEntry = candidateEntry;
                }
            }

            if (bestListIndex == 0)
            {
                return;
            }

            candidateIndices[bestListIndex] = candidateIndices[0];
            candidateIndices[0] = bestCandidateIndex;
        }

        private static bool SameAabb(
            FixVector3 aMin,
            FixVector3 aMax,
            FixVector3 bMin,
            FixVector3 bMax)
        {
            return aMin.Equals(bMin) && aMax.Equals(bMax);
        }

        private bool CanAttemptStepUpOverAabb(
            FixVector3 position,
            CombatPhysicsBroadphaseCandidate blockingCandidate)
        {
            if (blockingCandidate.ShapeKind != CombatPhysicsShapeKind.Aabb)
            {
                return false;
            }

            Config.CapsuleProxy.GetBounds(position, out FixVector3 characterMin, out _);
            Fix64 obstacleTop = blockingCandidate.Max.Y;
            Fix64 stepReach = characterMin.Y + Config.MaxStepHeight + Config.SkinWidth;
            return obstacleTop <= stepReach;
        }

        private static void BuildBestPrefixAabb(
            FixVector3 startMin,
            FixVector3 startMax,
            FixVector3 delta,
            FixVector3 skin,
            Fix64 bestFraction,
            out FixVector3 prefixMin,
            out FixVector3 prefixMax)
        {
            FixVector3 bestEndMin = startMin + delta * bestFraction;
            FixVector3 bestEndMax = startMax + delta * bestFraction;
            prefixMin = Min(startMin, bestEndMin) - skin;
            prefixMax = Max(startMax, bestEndMax) + skin;
        }

        private static Fix64 CalculateSweepEntryFraction(
            FixVector3 movingMin,
            FixVector3 movingMax,
            FixVector3 delta,
            FixVector3 obstacleMin,
            FixVector3 obstacleMax)
        {
            Fix64 entryX = CalculateSweepEntryAxis(
                movingMin.X,
                movingMax.X,
                delta.X,
                obstacleMin.X,
                obstacleMax.X);
            Fix64 entryY = CalculateSweepEntryAxis(
                movingMin.Y,
                movingMax.Y,
                delta.Y,
                obstacleMin.Y,
                obstacleMax.Y);
            Fix64 entryZ = CalculateSweepEntryAxis(
                movingMin.Z,
                movingMax.Z,
                delta.Z,
                obstacleMin.Z,
                obstacleMax.Z);
            return Fix64.Max(entryX, Fix64.Max(entryY, entryZ));
        }

        private static Fix64 CalculateSweepEntryAxis(
            Fix64 movingMin,
            Fix64 movingMax,
            Fix64 delta,
            Fix64 obstacleMin,
            Fix64 obstacleMax)
        {
            if (delta.IsZero)
            {
                return movingMax < obstacleMin || movingMin > obstacleMax
                    ? LargePositive
                    : LargeNegative;
            }

            if (delta > Fix64.Zero)
            {
                if (movingMax < obstacleMin)
                {
                    return (obstacleMin - movingMax) / delta;
                }

                return movingMin > obstacleMax ? LargePositive : LargeNegative;
            }

            if (movingMin > obstacleMax)
            {
                return (obstacleMax - movingMin) / delta;
            }

            return movingMax < obstacleMin ? LargePositive : LargeNegative;
        }

        private static int CompareSweepCandidatePriority(
            Fix64 leftEntry,
            CombatPhysicsBroadphaseCandidate left,
            Fix64 rightEntry,
            CombatPhysicsBroadphaseCandidate right)
        {
            int compare = leftEntry.RawValue.CompareTo(rightEntry.RawValue);
            return compare != 0 ? compare : CompareCandidateDeterministic(left, right);
        }

        private static int CompareCandidateDeterministic(
            CombatPhysicsBroadphaseCandidate left,
            CombatPhysicsBroadphaseCandidate right)
        {
            int compare = left.Body.EntityId.CompareTo(right.Body.EntityId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.Body.BodyId.CompareTo(right.Body.BodyId);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.ColliderId.CompareTo(right.ColliderId);
            if (compare != 0)
            {
                return compare;
            }

            compare = ((int)left.ShapeKind).CompareTo((int)right.ShapeKind);
            if (compare != 0)
            {
                return compare;
            }

            compare = left.Layer.CompareTo(right.Layer);
            return compare != 0 ? compare : left.ColliderIndex.CompareTo(right.ColliderIndex);
        }

        private static Fix64 ResolveDeltaLength(
            FixVector3 delta,
            ref bool hasDeltaLength,
            ref Fix64 deltaLength)
        {
            if (!hasDeltaLength)
            {
                Fix64 deltaLengthSquared = delta.LengthSquared();
                deltaLength = deltaLengthSquared.IsZero ? Fix64.Zero : deltaLengthSquared.Sqrt();
                hasDeltaLength = true;
            }

            return deltaLength;
        }

        private FixVector3 ResolveCapsuleCenter(
            FixVector3 position,
            ref bool hasCapsuleCenter,
            ref FixVector3 capsuleCenter)
        {
            if (!hasCapsuleCenter)
            {
                capsuleCenter = Config.CapsuleProxy.GetWorldCenter(position);
                hasCapsuleCenter = true;
            }

            return capsuleCenter;
        }

        private static bool SweptVerticalMayOverlap(
            Fix64 startMinY,
            Fix64 startMaxY,
            Fix64 deltaY,
            Fix64 targetMinY,
            Fix64 targetMaxY)
        {
            Fix64 endMinY = startMinY + deltaY;
            Fix64 endMaxY = startMaxY + deltaY;
            Fix64 sweepMinY = Fix64.Min(startMinY, endMinY);
            Fix64 sweepMaxY = Fix64.Max(startMaxY, endMaxY);
            return sweepMaxY > targetMinY && targetMaxY > sweepMinY;
        }

        private static bool OverlapsInclusive(
            FixVector3 leftMin,
            FixVector3 leftMax,
            FixVector3 rightMin,
            FixVector3 rightMax)
        {
            return leftMin.X <= rightMax.X
                && leftMax.X >= rightMin.X
                && leftMin.Y <= rightMax.Y
                && leftMax.Y >= rightMin.Y
                && leftMin.Z <= rightMax.Z
                && leftMax.Z >= rightMin.Z;
        }

        private static bool ContainsInclusive(
            FixVector3 outerMin,
            FixVector3 outerMax,
            FixVector3 innerMin,
            FixVector3 innerMax)
        {
            return outerMin.X <= innerMin.X
                && outerMax.X >= innerMax.X
                && outerMin.Y <= innerMin.Y
                && outerMax.Y >= innerMax.Y
                && outerMin.Z <= innerMin.Z
                && outerMax.Z >= innerMax.Z;
        }

        private static CombatSurfaceMaterial SelectHigherPrioritySurfaceMaterial(
            CombatSurfaceMaterial current,
            CombatSurfaceMaterial candidate)
        {
            return CompareSurfaceMaterialPriority(candidate, current) < 0 ? candidate : current;
        }

        private static CombatMotionCollision WithSurfaceMaterial(
            CombatMotionCollision collision,
            CombatSurfaceMaterial surfaceMaterial)
        {
            if (surfaceMaterial.Equals(collision.SurfaceMaterial))
            {
                return collision;
            }

            return new CombatMotionCollision(
                collision.TargetEntityId,
                collision.TargetBodyId,
                collision.TargetColliderId,
                collision.Normal,
                collision.Distance,
                collision.Fraction,
                collision.Flags,
                surfaceMaterial);
        }

        private static CombatMotionCollision WithFlags(
            CombatMotionCollision collision,
            CombatMotionCollisionFlags flags)
        {
            if (flags == collision.Flags)
            {
                return collision;
            }

            return new CombatMotionCollision(
                collision.TargetEntityId,
                collision.TargetBodyId,
                collision.TargetColliderId,
                collision.Normal,
                collision.Distance,
                collision.Fraction,
                flags,
                collision.SurfaceMaterial);
        }

        private bool TryBuildCollisionFromCandidate(
            FixVector3 position,
            FixVector3 delta,
            ref bool hasDeltaLength,
            ref Fix64 deltaLength,
            FixVector3 startMin,
            FixVector3 startMax,
            ref bool hasCapsuleCenter,
            ref FixVector3 capsuleCenter,
            CombatPhysicsBroadphaseCandidate hit,
            out CombatMotionCollision collision,
            out FixVector3 nonEnteringDirection)
        {
            collision = default;
            nonEnteringDirection = default;
            CombatPhysicsBody targetBody = hit.Body;
            switch (hit.ShapeKind)
            {
                case CombatPhysicsShapeKind.Sphere:
                {
                    FixVector3 center = hit.Shape.Center;
                    Fix64 radius = hit.Shape.Radius;
                    if (!SweptVerticalMayOverlap(startMin.Y, startMax.Y, delta.Y, center.Y - radius, center.Y + radius))
                    {
                        return false;
                    }

                    FixVector3 resolvedCapsuleCenter = ResolveCapsuleCenter(
                        position,
                        ref hasCapsuleCenter,
                        ref capsuleCenter);
                    if (!TrySweepCapsuleCircleTarget(
                            delta,
                            ref hasDeltaLength,
                            ref deltaLength,
                            startMin.Y,
                            startMax.Y,
                            resolvedCapsuleCenter,
                            center,
                            radius,
                            center.Y - radius,
                            center.Y + radius,
                            out Fix64 fraction,
                            out Fix64 distance,
                            out FixVector3 normal))
                    {
                        return false;
                    }

                    collision = new CombatMotionCollision(
                        targetBody.EntityId,
                        targetBody.BodyId,
                        hit.ColliderId,
                        normal,
                        distance,
                        fraction,
                        CombatMotionCollisionFlags.None,
                        hit.SurfaceMaterial);
                    nonEnteringDirection = normal;
                    return true;
                }
                case CombatPhysicsShapeKind.Capsule:
                {
                    FixVector3 pointA = hit.Shape.PointA;
                    FixVector3 pointB = hit.Shape.PointB;
                    FixVector3 center = new FixVector3(
                        (pointA.X + pointB.X) / Fix64.FromInt(2),
                        (pointA.Y + pointB.Y) / Fix64.FromInt(2),
                        (pointA.Z + pointB.Z) / Fix64.FromInt(2));
                    Fix64 targetMinY = Fix64.Min(pointA.Y, pointB.Y) - hit.Shape.Radius;
                    Fix64 targetMaxY = Fix64.Max(pointA.Y, pointB.Y) + hit.Shape.Radius;
                    if (!SweptVerticalMayOverlap(startMin.Y, startMax.Y, delta.Y, targetMinY, targetMaxY))
                    {
                        return false;
                    }

                    FixVector3 resolvedCapsuleCenter = ResolveCapsuleCenter(
                        position,
                        ref hasCapsuleCenter,
                        ref capsuleCenter);
                    if (!TrySweepCapsuleCircleTarget(
                            delta,
                            ref hasDeltaLength,
                            ref deltaLength,
                            startMin.Y,
                            startMax.Y,
                            resolvedCapsuleCenter,
                            center,
                            hit.Shape.Radius,
                            targetMinY,
                            targetMaxY,
                            out Fix64 fraction,
                            out Fix64 distance,
                            out FixVector3 normal))
                    {
                        return false;
                    }

                    collision = new CombatMotionCollision(
                        targetBody.EntityId,
                        targetBody.BodyId,
                        hit.ColliderId,
                        normal,
                        distance,
                        fraction,
                        CombatMotionCollisionFlags.None,
                        hit.SurfaceMaterial);
                    nonEnteringDirection = normal;
                    return true;
                }
                case CombatPhysicsShapeKind.Obb:
                {
                    bool castResult = CombatMotionObbCast.TryCast(
                            Config.CapsuleProxy,
                            position,
                            delta,
                            hit.Shape,
                            out Fix64 fraction,
                            out Fix64 distance,
                            out FixVector3 normal,
                            out nonEnteringDirection,
                            out CombatObbCastStatus status);
                    if (!RouteObbCastResult(
                            castResult,
                            status,
                            targetBody.BodyId,
                            hit.ColliderId))
                    {
                        return false;
                    }

                    collision = new CombatMotionCollision(
                        targetBody.EntityId,
                        targetBody.BodyId,
                        hit.ColliderId,
                        normal,
                        distance,
                        fraction,
                        CombatMotionCollisionFlags.None,
                        hit.SurfaceMaterial);
                    return true;
                }
                default:
                {
                    FixVector3 obstacleMin = hit.Shape.Min;
                    FixVector3 obstacleMax = hit.Shape.Max;
                    if (!TrySweepAabb(
                            startMin,
                            startMax,
                            delta,
                            ref hasDeltaLength,
                            ref deltaLength,
                            obstacleMin,
                            obstacleMax,
                            out Fix64 fraction,
                            out Fix64 distance,
                            out FixVector3 normal))
                    {
                        return false;
                    }

                    collision = new CombatMotionCollision(
                        targetBody.EntityId,
                        targetBody.BodyId,
                        hit.ColliderId,
                        normal,
                        distance,
                        fraction,
                        CombatMotionCollisionFlags.None,
                        hit.SurfaceMaterial);
                    nonEnteringDirection = normal;
                    return true;
                }
            }
        }

        internal static bool RouteObbCastResult(
            bool castResult,
            CombatObbCastStatus status,
            CombatBodyId targetBodyId,
            CombatColliderId targetColliderId)
        {
            if (castResult)
            {
                switch (status)
                {
                    case CombatObbCastStatus.Hit:
                    case CombatObbCastStatus.InitialTouching:
                    case CombatObbCastStatus.InitialOverlap:
                    case CombatObbCastStatus.IterationLimitConservativeHit:
                    case CombatObbCastStatus.PrecisionLimitConservativeHit:
                        return true;
                }
            }
            else
            {
                switch (status)
                {
                    case CombatObbCastStatus.Miss:
                    case CombatObbCastStatus.ZeroDelta:
                    case CombatObbCastStatus.InitialTouching:
                    case CombatObbCastStatus.InitialOverlap:
                        return false;
                }
            }

            throw new InvalidOperationException(
                "Invalid OBB cast result for body "
                + targetBodyId.Value
                + ", collider "
                + targetColliderId.Value
                + ", castResult "
                + castResult
                + ", status "
                + status
                + ".");
        }

        private bool TrySweepCapsuleCircleTarget(
            FixVector3 delta,
            ref bool hasDeltaLength,
            ref Fix64 deltaLength,
            Fix64 startMinY,
            Fix64 startMaxY,
            FixVector3 capsuleCenter,
            FixVector3 targetCenter,
            Fix64 targetRadius,
            Fix64 targetMinY,
            Fix64 targetMaxY,
            out Fix64 fraction,
            out Fix64 distance,
            out FixVector3 normal)
        {
            Fix64 combinedRadius = Config.CapsuleProxy.Radius + targetRadius;
            Fix64 dx = capsuleCenter.X - targetCenter.X;
            Fix64 dz = capsuleCenter.Z - targetCenter.Z;
            Fix64 deltaX = delta.X;
            Fix64 deltaZ = delta.Z;
            Fix64 c = (dx * dx) + (dz * dz) - (combinedRadius * combinedRadius);
            if (c <= Fix64.Zero)
            {
                if (!VerticalOverlapsCircleTargetAtFraction(startMinY, startMaxY, delta.Y, Fix64.Zero, targetMinY, targetMaxY))
                    return NoSweep(out fraction, out distance, out normal);

                FixVector3 initialNormal = ResolveHorizontalNormal(dx, dz, -deltaX, -deltaZ);
                if (delta.Dot(initialNormal) >= Fix64.Zero)
                    return NoSweep(out fraction, out distance, out normal);

                fraction = Fix64.Zero;
                distance = Fix64.Zero;
                normal = initialNormal;
                return true;
            }

            Fix64 a = (deltaX * deltaX) + (deltaZ * deltaZ);
            if (a.IsZero)
                return NoSweep(out fraction, out distance, out normal);

            Fix64 b = (dx * deltaX) + (dz * deltaZ);
            if (b >= Fix64.Zero)
                return NoSweep(out fraction, out distance, out normal);

            Fix64 discriminant = (b * b) - (a * c);
            if (discriminant < Fix64.Zero)
                return NoSweep(out fraction, out distance, out normal);

            fraction = (-b - discriminant.Sqrt()) / a;
            if (fraction < Fix64.Zero || fraction > Fix64.One)
                return NoSweep(out fraction, out distance, out normal);

            if (!VerticalOverlapsCircleTargetAtFraction(startMinY, startMaxY, delta.Y, fraction, targetMinY, targetMaxY))
                return NoSweep(out fraction, out distance, out normal);

            Fix64 hitX = dx + deltaX * fraction;
            Fix64 hitZ = dz + deltaZ * fraction;
            normal = ResolveHorizontalNormal(hitX, hitZ, -deltaX, -deltaZ);
            distance = ResolveDeltaLength(delta, ref hasDeltaLength, ref deltaLength) * fraction;
            return true;
        }

        private static bool VerticalOverlapsCircleTargetAtFraction(
            Fix64 startMinY,
            Fix64 startMaxY,
            Fix64 deltaY,
            Fix64 fraction,
            Fix64 targetMinY,
            Fix64 targetMaxY)
        {
            Fix64 minY = startMinY + deltaY * fraction;
            Fix64 maxY = startMaxY + deltaY * fraction;
            return maxY > targetMinY && targetMaxY > minY;
        }

        private static FixVector3 ResolveHorizontalNormal(
            Fix64 x,
            Fix64 z,
            Fix64 fallbackX,
            Fix64 fallbackZ)
        {
            var normal = new FixVector3(x, Fix64.Zero, z);
            if (normal.TryNormalize(out FixVector3 normalized))
                return normalized;

            var fallback = new FixVector3(fallbackX, Fix64.Zero, fallbackZ);
            return fallback.TryNormalize(out normalized) ? normalized : AxisX;
        }
    }
}
