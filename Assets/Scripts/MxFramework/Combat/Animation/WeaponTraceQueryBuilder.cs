using System;
using System.Collections.Generic;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;

namespace MxFramework.Combat.Animation
{
    public static class WeaponTraceQueryBuilder
    {
        public static CombatCapsuleQuery BuildCurrentBladeCapsule(
            WeaponTraceFrame frame,
            CombatEntityId sourceEntityId,
            int actionId,
            int queryId,
            int sourceOrder)
        {
            CombatQueryHeader header = CreateHeader(
                frame,
                sourceEntityId,
                actionId,
                queryId,
                sourceOrder);

            return new CombatCapsuleQuery(
                header,
                frame.RootNow,
                frame.TipNow,
                frame.Radius);
        }

        public static int GetTipSweepSubstepCount(
            WeaponTraceFrame frame,
            Fix64 maxTipDistancePerSubstep,
            int maxSubsteps)
        {
            if (maxTipDistancePerSubstep <= Fix64.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTipDistancePerSubstep), "Max tip distance per substep must be positive.");
            }

            if (maxSubsteps <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSubsteps), "Max substeps must be positive.");
            }

            Fix64 distanceSquared = frame.TipDeltaLengthSquared();
            Fix64 maxDistanceSquared = maxTipDistancePerSubstep * maxTipDistancePerSubstep;

            int count = 1;
            while (count < maxSubsteps)
            {
                Fix64 covered = maxDistanceSquared * Fix64.FromInt(count * count);
                if (covered >= distanceSquared)
                {
                    break;
                }

                count++;
            }

            return count;
        }

        public static int BuildTipSweepSegments(
            WeaponTraceFrame frame,
            Fix64 maxTipDistancePerSubstep,
            int maxSubsteps,
            List<WeaponTraceSegment> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            int substeps = GetTipSweepSubstepCount(frame, maxTipDistancePerSubstep, maxSubsteps);
            int startCount = results.Count;

            for (int i = 0; i < substeps; i++)
            {
                Fix64 t0 = Fix64.FromRatio(i, substeps);
                Fix64 t1 = Fix64.FromRatio(i + 1, substeps);
                results.Add(new WeaponTraceSegment(
                    frame.TraceId,
                    i,
                    Lerp(frame.TipPrev, frame.TipNow, t0),
                    Lerp(frame.TipPrev, frame.TipNow, t1),
                    frame.Radius,
                    frame.TargetMask));
            }

            return results.Count - startCount;
        }

        public static int BuildTipSweepCapsules(
            WeaponTraceFrame frame,
            CombatEntityId sourceEntityId,
            int actionId,
            int queryIdStart,
            Fix64 maxTipDistancePerSubstep,
            int maxSubsteps,
            List<CombatCapsuleQuery> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (queryIdStart < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(queryIdStart), "Query id start cannot be negative.");
            }

            int substeps = GetTipSweepSubstepCount(frame, maxTipDistancePerSubstep, maxSubsteps);
            int startCount = results.Count;

            for (int i = 0; i < substeps; i++)
            {
                Fix64 t0 = Fix64.FromRatio(i, substeps);
                Fix64 t1 = Fix64.FromRatio(i + 1, substeps);
                CombatQueryHeader header = CreateHeader(
                    frame,
                    sourceEntityId,
                    actionId,
                    checked(queryIdStart + i),
                    i);

                results.Add(new CombatCapsuleQuery(
                    header,
                    Lerp(frame.TipPrev, frame.TipNow, t0),
                    Lerp(frame.TipPrev, frame.TipNow, t1),
                    frame.Radius));
            }

            return results.Count - startCount;
        }

        private static CombatQueryHeader CreateHeader(
            WeaponTraceFrame frame,
            CombatEntityId sourceEntityId,
            int actionId,
            int queryId,
            int sourceOrder)
        {
            return new CombatQueryHeader(
                queryId,
                CombatQueryKind.Capsule,
                sourceEntityId,
                frame.TraceId,
                actionId,
                sourceOrder,
                frame.TargetMask);
        }

        private static FixVector3 Lerp(FixVector3 from, FixVector3 to, Fix64 t)
        {
            return from + ((to - from) * t);
        }
    }
}
