using System;
using System.Collections.Generic;

namespace MxFramework.Combat.Physics
{
    public sealed partial class CombatPhysicsWorld
    {
        private static readonly Comparison<CombatPhysicsAabbCollider> AabbCopyComparison = CombatPhysicsAabbColliderComparer.Instance.Compare;
        private static readonly Comparison<CombatPhysicsSphereCollider> SphereCopyComparison = CombatPhysicsSphereColliderComparer.Instance.Compare;
        private static readonly Comparison<CombatPhysicsCapsuleCollider> CapsuleCopyComparison = CombatPhysicsCapsuleColliderComparer.Instance.Compare;
        private static readonly Comparison<CombatPhysicsObbCollider> ObbCopyComparison = CombatPhysicsObbColliderComparer.Instance.Compare;

        private static void SortCopiedColliders<T>(List<T> values, int start, int count,
            IComparer<T> comparer, Comparison<T> comparison)
        {
            if (count < 2) return;
            // Mono creates a comparison delegate for IComparer range sorting on each call.
            // Reused empty buffers use a cached delegate; appended prefixes stay untouched.
            if (start == 0) values.Sort(comparison);
            else values.Sort(start, count, comparer);
        }

        public void SortResults(List<CombatQueryResult> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            SortResultsRange(results, 0, results.Count);
        }

        public void SortBatchResults(List<CombatPhysicsQueryBatchResult> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            SortBatchResultsRange(results, 0, results.Count);
        }

        private static void SortBatchResultsRange(
            List<CombatPhysicsQueryBatchResult> results,
            int startIndex,
            int count)
        {
            if (count <= 1)
                return;

            results.Sort(startIndex, count, CombatPhysicsQueryBatchResultComparer.Instance);
        }

        private static int CompareBatchResults(CombatPhysicsQueryBatchResult left, CombatPhysicsQueryBatchResult right)
        {
            int compare = CombatPhysicsQuery.CompareHeaders(left.Query.Header, right.Query.Header);
            if (compare != 0)
            {
                return compare;
            }

            return left.SourceIndex.CompareTo(right.SourceIndex);
        }

        private sealed class CombatPhysicsQueryBatchResultComparer : IComparer<CombatPhysicsQueryBatchResult>
        {
            public static readonly CombatPhysicsQueryBatchResultComparer Instance =
                new CombatPhysicsQueryBatchResultComparer();

            private CombatPhysicsQueryBatchResultComparer()
            {
            }

            public int Compare(CombatPhysicsQueryBatchResult left, CombatPhysicsQueryBatchResult right)
            {
                return CompareBatchResults(left, right);
            }
        }

        private static void SortResultsRange(List<CombatQueryResult> results, int startIndex, int count)
        {
            if (count <= 1)
                return;

            QuickSortResults(results, startIndex, startIndex + count - 1);
        }

        private static void QuickSortResults(
            List<CombatQueryResult> results,
            int left,
            int right)
        {
            while (left < right)
            {
                int i = left;
                int j = right;
                CombatQueryResult pivot = results[left + ((right - left) >> 1)];
                while (i <= j)
                {
                    while (CompareQueryResults(results[i], pivot) < 0)
                        i++;
                    while (CompareQueryResults(results[j], pivot) > 0)
                        j--;

                    if (i <= j)
                    {
                        if (i != j)
                        {
                            CombatQueryResult temp = results[i];
                            results[i] = results[j];
                            results[j] = temp;
                        }

                        i++;
                        j--;
                    }
                }

                if (j - left < right - i)
                {
                    if (left < j)
                        QuickSortResults(results, left, j);
                    left = i;
                }
                else
                {
                    if (i < right)
                        QuickSortResults(results, i, right);
                    right = j;
                }
            }
        }

        private static int CompareQueryResults(
            CombatQueryResult left,
            CombatQueryResult right)
        {
            int compare = left.Distance.RawValue.CompareTo(right.Distance.RawValue);
            if (compare != 0)
                return compare;

            compare = right.TargetPriority.CompareTo(left.TargetPriority);
            if (compare != 0)
                return compare;

            compare = left.TargetEntityId.Value.CompareTo(right.TargetEntityId.Value);
            if (compare != 0)
                return compare;

            compare = left.TargetBodyId.Value.CompareTo(right.TargetBodyId.Value);
            if (compare != 0)
                return compare;

            compare = left.TargetColliderId.Value.CompareTo(right.TargetColliderId.Value);
            if (compare != 0)
                return compare;

            compare = ((int)left.TargetShapeKind).CompareTo((int)right.TargetShapeKind);
            if (compare != 0)
                return compare;

            compare = left.Query.TraceId.CompareTo(right.Query.TraceId);
            if (compare != 0)
                return compare;

            compare = left.Query.ActionId.CompareTo(right.Query.ActionId);
            if (compare != 0)
                return compare;

            return left.Query.SourceOrder.CompareTo(right.Query.SourceOrder);
        }
    }
}
