using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;

namespace MxFramework.Combat.Physics
{
    public enum CombatPhysicsQueryDebugRowStatus
    {
        Hit = 1,
        Miss = 2,
        FilteredLayer = 3,
        FilteredSource = 4,
    }

    public readonly struct CombatPhysicsQueryDebugRow : IEquatable<CombatPhysicsQueryDebugRow>
    {
        public CombatPhysicsQueryDebugRow(
            int candidateIndex,
            CombatEntityId entityId,
            CombatBodyId bodyId,
            CombatColliderId colliderId,
            int layer,
            CombatPhysicsQueryDebugRowStatus status)
        {
            if (candidateIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(candidateIndex), "Query debug candidate index cannot be negative.");
            }

            CandidateIndex = candidateIndex;
            EntityId = entityId;
            BodyId = bodyId;
            ColliderId = colliderId;
            Layer = layer;
            Status = status;
        }

        public int CandidateIndex { get; }

        public CombatEntityId EntityId { get; }

        public CombatBodyId BodyId { get; }

        public CombatColliderId ColliderId { get; }

        public int Layer { get; }

        public CombatPhysicsQueryDebugRowStatus Status { get; }

        public bool EnteredNarrowphase => Status == CombatPhysicsQueryDebugRowStatus.Hit
            || Status == CombatPhysicsQueryDebugRowStatus.Miss;

        public bool WasFiltered => Status == CombatPhysicsQueryDebugRowStatus.FilteredLayer
            || Status == CombatPhysicsQueryDebugRowStatus.FilteredSource;

        public string StatusToken
        {
            get
            {
                switch (Status)
                {
                    case CombatPhysicsQueryDebugRowStatus.Hit:
                        return "hit";
                    case CombatPhysicsQueryDebugRowStatus.Miss:
                        return "miss";
                    case CombatPhysicsQueryDebugRowStatus.FilteredLayer:
                        return "filtered-layer";
                    case CombatPhysicsQueryDebugRowStatus.FilteredSource:
                        return "filtered-source";
                    default:
                        return "unknown";
                }
            }
        }

        public string StatusReason
        {
            get
            {
                switch (Status)
                {
                    case CombatPhysicsQueryDebugRowStatus.Hit:
                        return "narrowphase accepted candidate";
                    case CombatPhysicsQueryDebugRowStatus.Miss:
                        return "narrowphase rejected candidate";
                    case CombatPhysicsQueryDebugRowStatus.FilteredLayer:
                        return "layer mask rejected candidate";
                    case CombatPhysicsQueryDebugRowStatus.FilteredSource:
                        return "source entity excluded";
                    default:
                        return "unknown candidate state";
                }
            }
        }

        public string Label => "candidate[" + CandidateIndex
            + "] entity=" + EntityId.Value
            + " body=" + BodyId.Value
            + " collider=" + ColliderId.Value
            + " layer=" + Layer
            + " status=" + StatusToken
            + " reason=" + StatusReason;

        public bool Equals(CombatPhysicsQueryDebugRow other)
        {
            return CandidateIndex == other.CandidateIndex
                && EntityId.Equals(other.EntityId)
                && BodyId.Equals(other.BodyId)
                && ColliderId.Equals(other.ColliderId)
                && Layer == other.Layer
                && Status == other.Status;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatPhysicsQueryDebugRow other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = CandidateIndex;
                hash = (hash * 397) ^ EntityId.Value;
                hash = (hash * 397) ^ BodyId.Value;
                hash = (hash * 397) ^ ColliderId.Value;
                hash = (hash * 397) ^ Layer;
                hash = (hash * 397) ^ (int)Status;
                return hash;
            }
        }
    }

    public readonly struct CombatPhysicsQueryDebugSummary : IEquatable<CombatPhysicsQueryDebugSummary>
    {
        public CombatPhysicsQueryDebugSummary(
            int broadphaseRawCandidateCount,
            int broadphaseCandidateCount,
            int broadphaseCellCount,
            int filteredSourceCount,
            int filteredLayerCount,
            int postFilterCandidateCount,
            int hitCount)
        {
            BroadphaseRawCandidateCount = broadphaseRawCandidateCount;
            BroadphaseCandidateCount = broadphaseCandidateCount;
            BroadphaseCellCount = broadphaseCellCount;
            FilteredSourceCount = filteredSourceCount;
            FilteredLayerCount = filteredLayerCount;
            PostFilterCandidateCount = postFilterCandidateCount;
            HitCount = hitCount;
        }

        public int BroadphaseRawCandidateCount { get; }

        public int BroadphaseCandidateCount { get; }

        public int BroadphaseCellCount { get; }

        public int BroadphaseDuplicateCandidateCount => Max(0, BroadphaseRawCandidateCount - BroadphaseCandidateCount);

        public int BroadphaseDeduplicationPermille => CalculatePermille(BroadphaseDuplicateCandidateCount, BroadphaseRawCandidateCount);

        public int FilteredSourceCount { get; }

        public int FilteredLayerCount { get; }

        public int FilteredCandidateCount => FilteredSourceCount + FilteredLayerCount;

        public int FilteredCandidatePermille => CalculatePermille(FilteredCandidateCount, BroadphaseCandidateCount);

        public int PostFilterCandidateCount { get; }

        public int NarrowphaseCandidateCount => PostFilterCandidateCount;

        public int HitCount { get; }

        public int MissCount => Max(0, PostFilterCandidateCount - HitCount);

        public bool HasHit => HitCount > 0;

        public bool Equals(CombatPhysicsQueryDebugSummary other)
        {
            return BroadphaseRawCandidateCount == other.BroadphaseRawCandidateCount
                && BroadphaseCandidateCount == other.BroadphaseCandidateCount
                && BroadphaseCellCount == other.BroadphaseCellCount
                && FilteredSourceCount == other.FilteredSourceCount
                && FilteredLayerCount == other.FilteredLayerCount
                && PostFilterCandidateCount == other.PostFilterCandidateCount
                && HitCount == other.HitCount;
        }

        public override bool Equals(object obj)
        {
            return obj is CombatPhysicsQueryDebugSummary other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = BroadphaseRawCandidateCount;
                hash = (hash * 397) ^ BroadphaseCandidateCount;
                hash = (hash * 397) ^ BroadphaseCellCount;
                hash = (hash * 397) ^ FilteredSourceCount;
                hash = (hash * 397) ^ FilteredLayerCount;
                hash = (hash * 397) ^ PostFilterCandidateCount;
                hash = (hash * 397) ^ HitCount;
                return hash;
            }
        }

        private static int CalculatePermille(int numerator, int denominator)
        {
            if (numerator <= 0 || denominator <= 0)
            {
                return 0;
            }

            long permille = ((long)numerator * 1000) / denominator;
            return permille > int.MaxValue ? int.MaxValue : (int)permille;
        }

        private static int Max(int left, int right)
        {
            return left > right ? left : right;
        }
    }

    public sealed class CombatPhysicsQueryDebugReport
    {
        public CombatPhysicsQueryDebugReport(
            CombatPhysicsQuery query,
            int candidateCount,
            int filteredSourceCount,
            int filteredLayerCount,
            int hitCount,
            string unsupportedReason,
            IReadOnlyList<CombatPhysicsQueryDebugRow> rows,
            int broadphaseRawCandidateCount = -1,
            int broadphaseCandidateCount = -1,
            int postFilterCandidateCount = -1,
            int broadphaseCellCount = -1)
        {
            if (candidateCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(candidateCount), "Query debug candidate count cannot be negative.");
            }

            if (filteredSourceCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(filteredSourceCount), "Query debug source filter count cannot be negative.");
            }

            if (filteredLayerCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(filteredLayerCount), "Query debug layer filter count cannot be negative.");
            }

            if (hitCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(hitCount), "Query debug hit count cannot be negative.");
            }

            Query = query;
            ShapeKind = query.Shape.Kind;
            CandidateCount = candidateCount;
            FilteredSourceCount = filteredSourceCount;
            FilteredLayerCount = filteredLayerCount;
            HitCount = hitCount;
            UnsupportedReason = unsupportedReason ?? string.Empty;
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
            BroadphaseRawCandidateCount = broadphaseRawCandidateCount < 0 ? candidateCount : broadphaseRawCandidateCount;
            BroadphaseCandidateCount = broadphaseCandidateCount < 0 ? candidateCount : broadphaseCandidateCount;
            PostFilterCandidateCount = postFilterCandidateCount < 0 ? candidateCount - filteredSourceCount - filteredLayerCount : postFilterCandidateCount;
            BroadphaseCellCount = broadphaseCellCount < 0 ? 0 : broadphaseCellCount;
            Summary = new CombatPhysicsQueryDebugSummary(
                BroadphaseRawCandidateCount,
                BroadphaseCandidateCount,
                BroadphaseCellCount,
                FilteredSourceCount,
                FilteredLayerCount,
                PostFilterCandidateCount,
                HitCount);
            SummaryLines = BuildSummaryLines(ShapeKind, UnsupportedReason, Summary);
        }

        public CombatPhysicsQuery Query { get; }

        public CombatPhysicsShapeKind ShapeKind { get; }

        public int CandidateCount { get; }

        public int FilteredSourceCount { get; }

        public int FilteredLayerCount { get; }

        public int HitCount { get; }

        public string UnsupportedReason { get; }

        public bool IsUnsupported => UnsupportedReason.Length > 0;

        public IReadOnlyList<CombatPhysicsQueryDebugRow> Rows { get; }

        public CombatPhysicsQueryDebugSummary Summary { get; }

        public IReadOnlyList<string> SummaryLines { get; }

        public int BroadphaseRawCandidateCount { get; }

        public int BroadphaseCandidateCount { get; }

        public int PostFilterCandidateCount { get; }

        public int BroadphaseCellCount { get; }

        public int BroadphaseDuplicateCandidateCount => Summary.BroadphaseDuplicateCandidateCount;

        public int BroadphaseDeduplicationPermille => Summary.BroadphaseDeduplicationPermille;

        public int FilteredCandidateCount => Summary.FilteredCandidateCount;

        public int FilteredCandidatePermille => Summary.FilteredCandidatePermille;

        public int NarrowphaseCandidateCount => Summary.NarrowphaseCandidateCount;

        public int MissCount => Summary.MissCount;

        private static IReadOnlyList<string> BuildSummaryLines(
            CombatPhysicsShapeKind shapeKind,
            string unsupportedReason,
            CombatPhysicsQueryDebugSummary summary)
        {
            if (!string.IsNullOrEmpty(unsupportedReason))
            {
                return new[]
                {
                    "shape=" + shapeKind + " supported=false reason=" + unsupportedReason,
                };
            }

            return new[]
            {
                "shape=" + shapeKind + " supported=true",
                "broadphase cells=" + summary.BroadphaseCellCount
                    + " raw=" + summary.BroadphaseRawCandidateCount
                    + " dedup=" + summary.BroadphaseCandidateCount
                    + " duplicate=" + summary.BroadphaseDuplicateCandidateCount
                    + " duplicatePermille=" + summary.BroadphaseDeduplicationPermille,
                "filters source=" + summary.FilteredSourceCount
                    + " layer=" + summary.FilteredLayerCount
                    + " postFilter=" + summary.PostFilterCandidateCount
                    + " filteredPermille=" + summary.FilteredCandidatePermille,
                "narrowphase hit=" + summary.HitCount
                    + " miss=" + summary.MissCount,
            };
        }
    }
}
