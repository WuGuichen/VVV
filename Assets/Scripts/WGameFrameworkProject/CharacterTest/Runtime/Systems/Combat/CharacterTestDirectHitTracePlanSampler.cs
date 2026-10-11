using System;
using System.Collections.Generic;
using MxFramework.Combat.Animation;
using MxFramework.Combat.Core;
using MxFramework.Combat.Physics;
using MxFramework.Core.Math;
using MxFramework.Animation;
using MxFramework.CharacterAction;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// CharacterTest-only capsule plan generation shared by actual direct-hit
    /// execution and future read-only preview queries.
    /// </summary>
    internal static class CharacterTestDirectHitTracePlanSampler
    {
        private const int ProximalTraceResolvePriority = -1;

        /// <summary>
        /// Builds a read-only preview plan from caller-owned immutable local
        /// root-motion offsets. Absolute world poses are composed only from the
        /// sealed <paramref name="poseIdentity"/> inside this builder; callers
        /// cannot provide a second, divergent world-space pose authority.
        /// </summary>
        internal static bool TryBuildPreviewPlan(
            RuntimeFrame runtimeFrame,
            int combatPhysicsRevision,
            CombatEntityId sourceEntityId,
            int resolvedActionRevision,
            CharacterTestDirectHitTracePlanPoseIdentity poseIdentity,
            IReadOnlyList<CharacterTestDirectHitTraceSourcePoseOffset> activeFrameSourcePoseOffsets,
            IReadOnlyList<CharacterTestDirectHitTracePreviewWeaponFrame> weaponFrames,
            IReadOnlyList<CharacterTestDirectHitTracePreviewCapsuleFrame> capsuleFrames,
            bool useTipSweepCapsules,
            Fix64 maxTipDistancePerSubstep,
            int maxSubsteps,
            List<CombatCapsuleQuery> queryBuffer,
            CharacterTestDirectHitTracePlanBuffer output,
            out CharacterTestDirectHitTracePlanView plan,
            IReadOnlyList<int> requiredActiveFrames = null)
        {
            plan = default(CharacterTestDirectHitTracePlanView);
            if (output == null)
                return false;

            // Invalidate any previous view before validating new input. A
            // failed preview must never leave an older result looking current.
            output.Reset(runtimeFrame, combatPhysicsRevision);
            if (combatPhysicsRevision < 0
                || sourceEntityId.IsNone
                || resolvedActionRevision <= 0
                || !poseIdentity.IsPreviewBound
                || activeFrameSourcePoseOffsets == null
                || activeFrameSourcePoseOffsets.Count == 0
                || weaponFrames == null
                || capsuleFrames == null
                || queryBuffer == null
                || (useTipSweepCapsules && (maxTipDistancePerSubstep <= Fix64.Zero || maxSubsteps <= 0))
                || !HasContiguousOrderedSourcePoseOffsets(activeFrameSourcePoseOffsets)
                || !ValidateRequiredActiveFrames(requiredActiveFrames, activeFrameSourcePoseOffsets)
                || !ValidatePreviewInputs(
                    resolvedActionRevision,
                    activeFrameSourcePoseOffsets,
                    weaponFrames,
                    capsuleFrames))
            {
                output.Reset(runtimeFrame, combatPhysicsRevision);
                return false;
            }

            int planIndex = output.GetOrBeginPlan(
                sourceEntityId,
                resolvedActionRevision,
                poseIdentity);
            if (planIndex < 0)
            {
                output.Reset(runtimeFrame, combatPhysicsRevision);
                return false;
            }
            int queryId = 0;
            int sampleOrder = 0;
            CharacterTestDirectHitTraceBounds bounds = output.GetPlanBounds(planIndex);
            List<CharacterTestDirectHitTracePlanSample> samples =
                output.GetPlanSamplesBuffer(planIndex);
            if (samples == null)
            {
                output.Reset(runtimeFrame, combatPhysicsRevision);
                return false;
            }
            int sampleStart = samples.Count;

            int requiredFrameIndex = 0;
            for (int poseIndex = 0; poseIndex < activeFrameSourcePoseOffsets.Count; poseIndex++)
            {
                CharacterTestDirectHitTraceSourcePose sourcePose =
                    activeFrameSourcePoseOffsets[poseIndex].Resolve(poseIdentity);
                bool producedFrameSample = false;
                bool frameRequired = requiredActiveFrames == null;
                if (requiredActiveFrames != null && requiredFrameIndex < requiredActiveFrames.Count
                    && requiredActiveFrames[requiredFrameIndex] == sourcePose.LocalFrame)
                { frameRequired = true; requiredFrameIndex++; }

                for (int i = 0; i < weaponFrames.Count; i++)
                {
                    CharacterTestDirectHitTracePreviewWeaponFrame input = weaponFrames[i];
                    if (input.LocalFrame != sourcePose.LocalFrame)
                        continue;

                    WeaponTraceFrame frame = input.Resolve(sourcePose);
                    AppendWeaponTraceSamples(
                        frame,
                        sourceEntityId,
                        input.ActionId,
                        resolvedActionRevision,
                        input.LocalFrame,
                        input.TraceIndex,
                        input.PolicyEventId,
                        useTipSweepCapsules,
                        maxTipDistancePerSubstep,
                        maxSubsteps,
                        ref queryId,
                        ref sampleOrder,
                        queryBuffer,
                        samples,
                        ref bounds);
                    producedFrameSample = true;
                }

                for (int i = 0; i < capsuleFrames.Count; i++)
                {
                    CharacterTestDirectHitTracePreviewCapsuleFrame input = capsuleFrames[i];
                    if (input.LocalFrame != sourcePose.LocalFrame)
                        continue;

                    if (!TryAppendHitVolumeSample(
                            input.Kind,
                            sourceEntityId,
                            input.ActionId,
                            input.TraceId,
                            input.PolicyEventId,
                            input.HitWindowId,
                            input.LocalFrame,
                            input.SourceOrder,
                            input.QueryOffset,
                            input.TargetMask,
                            sourcePose.TransformLocal(input.PointALocal),
                            sourcePose.TransformLocal(input.PointBLocal),
                            input.Radius,
                            ref sampleOrder,
                            samples,
                            ref bounds,
                            out _))
                    {
                        output.Reset(runtimeFrame, combatPhysicsRevision);
                        return false;
                    }

                    producedFrameSample = true;
                }

                // A declared idle gap has no hit query; an active window still requires complete trace data.
                if (producedFrameSample != frameRequired)
                {
                    output.Reset(runtimeFrame, combatPhysicsRevision);
                    return false;
                }
            }

            int sampleCount = samples.Count - sampleStart;
            if (sampleCount <= 0)
            {
                output.Reset(runtimeFrame, combatPhysicsRevision);
                return false;
            }

            if (!output.UpdatePlan(planIndex, sampleCount, bounds)
                || !output.Seal(runtimeFrame, combatPhysicsRevision))
            {
                output.Reset(runtimeFrame, combatPhysicsRevision);
                return false;
            }

            return output.TryGetPlan(output.Generation, planIndex, out plan);
        }

        private static bool HasContiguousOrderedSourcePoseOffsets(
            IReadOnlyList<CharacterTestDirectHitTraceSourcePoseOffset> sourcePoseOffsets)
        {
            int previousFrame = -1;
            for (int i = 0; i < sourcePoseOffsets.Count; i++)
            {
                int localFrame = sourcePoseOffsets[i].LocalFrame;
                if (localFrame < 0
                    || (i > 0 && (long)localFrame != (long)previousFrame + 1L))
                    return false;
                previousFrame = localFrame;
            }

            return true;
        }

        private static bool ValidateRequiredActiveFrames(IReadOnlyList<int> frames,
            IReadOnlyList<CharacterTestDirectHitTraceSourcePoseOffset> poses)
        {
            if (frames == null) return true;
            if (frames.Count == 0 || frames.Count > poses.Count) return false;
            int first = poses[0].LocalFrame, last = poses[poses.Count - 1].LocalFrame, previous = -1;
            for (int i = 0; i < frames.Count; i++)
            {
                int frame = frames[i];
                if (frame < first || frame > last || frame <= previous) return false;
                previous = frame;
            }
            return true;
        }

        private static bool ValidatePreviewInputs(
            int resolvedActionRevision,
            IReadOnlyList<CharacterTestDirectHitTraceSourcePoseOffset> sourcePoseOffsets,
            IReadOnlyList<CharacterTestDirectHitTracePreviewWeaponFrame> weaponFrames,
            IReadOnlyList<CharacterTestDirectHitTracePreviewCapsuleFrame> capsuleFrames)
        {
            int actionId = 0;
            int previousFrame = -1;
            int previousTraceIndex = -1;
            for (int i = 0; i < weaponFrames.Count; i++)
            {
                CharacterTestDirectHitTracePreviewWeaponFrame input = weaponFrames[i];
                if (input.LocalFrame < 0
                    || input.TraceIndex < 0
                    || input.TraceId <= 0
                    || input.PolicyEventId <= 0
                    || input.ActionId <= 0
                    || input.Radius <= Fix64.Zero
                    || !ContainsSourcePoseOffset(sourcePoseOffsets, input.LocalFrame)
                    || input.LocalFrame < previousFrame
                    || (input.LocalFrame == previousFrame && input.TraceIndex <= previousTraceIndex)
                    || !AcceptActionId(input.ActionId, ref actionId))
                {
                    return false;
                }

                previousTraceIndex = input.TraceIndex;
                previousFrame = input.LocalFrame;
            }

            previousFrame = -1;
            int previousSourceOrder = -1;
            int previousQueryOffset = -1;
            for (int i = 0; i < capsuleFrames.Count; i++)
            {
                CharacterTestDirectHitTracePreviewCapsuleFrame input = capsuleFrames[i];
                bool sameFrame = input.LocalFrame == previousFrame;
                if (!IsPreviewCapsuleKind(input.Kind)
                    || input.LocalFrame < 0
                    || input.SourceOrder < 0
                    || input.QueryOffset < 0
                    || input.ActionId <= 0
                    || input.TraceId <= 0
                    || input.PolicyEventId <= 0
                    || input.HitWindowId != resolvedActionRevision
                    || input.Radius <= Fix64.Zero
                    || !ContainsSourcePoseOffset(sourcePoseOffsets, input.LocalFrame)
                    || input.LocalFrame < previousFrame
                    || (sameFrame && input.SourceOrder < previousSourceOrder)
                    || (sameFrame
                        && input.SourceOrder == previousSourceOrder
                        && input.QueryOffset <= previousQueryOffset)
                    || !AcceptActionId(input.ActionId, ref actionId))
                {
                    return false;
                }

                previousSourceOrder = input.SourceOrder;
                previousQueryOffset = input.QueryOffset;
                previousFrame = input.LocalFrame;
            }

            return actionId > 0;
        }

        private static bool AcceptActionId(int candidate, ref int actionId)
        {
            if (actionId == 0)
            {
                actionId = candidate;
                return true;
            }

            return actionId == candidate;
        }

        private static bool ContainsSourcePoseOffset(
            IReadOnlyList<CharacterTestDirectHitTraceSourcePoseOffset> sourcePoseOffsets,
            int localFrame)
        {
            for (int i = 0; i < sourcePoseOffsets.Count; i++)
            {
                if (sourcePoseOffsets[i].LocalFrame == localFrame)
                    return true;
            }

            return false;
        }

        private static bool IsPreviewCapsuleKind(CharacterTestDirectHitTraceSampleKind kind)
        {
            return kind == CharacterTestDirectHitTraceSampleKind.ManualCapsule
                || kind == CharacterTestDirectHitTraceSampleKind.CasterBody
                || kind == CharacterTestDirectHitTraceSampleKind.BakedSocketCapsule;
        }

        internal static int AppendWeaponTraceSamples(
            WeaponTraceFrame frame,
            CombatEntityId sourceEntityId,
            int actionId,
            int hitWindowId,
            int localFrame,
            bool useTipSweepCapsules,
            Fix64 maxTipDistancePerSubstep,
            int maxSubsteps,
            ref int queryId,
            ref int sampleOrder,
            List<CombatCapsuleQuery> queryBuffer,
            List<CharacterTestDirectHitTracePlanSample> samples,
            ref CharacterTestDirectHitTraceBounds sourceBounds,
            WeaponTraceFrame? incomingMotion = null)
        {
            return AppendWeaponTraceSamples(
                frame,
                sourceEntityId,
                actionId,
                hitWindowId,
                localFrame,
                0,
                frame.TraceId,
                useTipSweepCapsules,
                maxTipDistancePerSubstep,
                maxSubsteps,
                ref queryId,
                ref sampleOrder,
                queryBuffer,
                samples,
                ref sourceBounds,
                incomingMotion);
        }

        internal static int AppendWeaponTraceSamples(
            WeaponTraceFrame frame,
            CombatEntityId sourceEntityId,
            int actionId,
            int hitWindowId,
            int localFrame,
            int traceIndex,
            bool useTipSweepCapsules,
            Fix64 maxTipDistancePerSubstep,
            int maxSubsteps,
            ref int queryId,
            ref int sampleOrder,
            List<CombatCapsuleQuery> queryBuffer,
            List<CharacterTestDirectHitTracePlanSample> samples,
            ref CharacterTestDirectHitTraceBounds sourceBounds)
        {
            return AppendWeaponTraceSamples(
                frame,
                sourceEntityId,
                actionId,
                hitWindowId,
                localFrame,
                traceIndex,
                frame.TraceId,
                useTipSweepCapsules,
                maxTipDistancePerSubstep,
                maxSubsteps,
                ref queryId,
                ref sampleOrder,
                queryBuffer,
                samples,
                ref sourceBounds);
        }

        internal static int AppendWeaponTraceSamples(
            WeaponTraceFrame frame,
            CombatEntityId sourceEntityId,
            int actionId,
            int hitWindowId,
            int localFrame,
            int traceIndex,
            int policyEventId,
            bool useTipSweepCapsules,
            Fix64 maxTipDistancePerSubstep,
            int maxSubsteps,
            ref int queryId,
            ref int sampleOrder,
            List<CombatCapsuleQuery> queryBuffer,
            List<CharacterTestDirectHitTracePlanSample> samples,
            ref CharacterTestDirectHitTraceBounds sourceBounds,
            WeaponTraceFrame? incomingMotion = null)
        {
            if (queryBuffer == null)
                throw new ArgumentNullException(nameof(queryBuffer));
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));

            int firstQueryId = queryId;
            if (useTipSweepCapsules)
            {
                queryBuffer.Clear();
                int builtQueries = WeaponTraceQueryBuilder.BuildTipSweepCapsules(
                    frame,
                    sourceEntityId,
                    actionId,
                    queryId,
                    maxTipDistancePerSubstep,
                    maxSubsteps,
                    queryBuffer);
                queryId += builtQueries;
                for (int i = 0; i < queryBuffer.Count; i++)
                {
                    AppendWeaponSample(
                        queryBuffer[i],
                        CharacterTestDirectHitTraceSampleKind.WeaponTraceTipSweep,
                        localFrame,
                        queryBuffer[i].Header.SourceOrder,
                        actionId,
                        hitWindowId,
                        policyEventId,
                        ref sampleOrder,
                        samples,
                        ref sourceBounds,
                        weaponMotion: incomingMotion ?? frame);
                }
            }
            else
            {
                CombatCapsuleQuery query = WeaponTraceQueryBuilder.BuildCurrentBladeCapsule(
                    frame,
                    sourceEntityId,
                    actionId,
                    queryId++,
                    traceIndex);
                AppendWeaponSample(
                    query,
                    CharacterTestDirectHitTraceSampleKind.WeaponTraceBlade,
                    localFrame,
                    query.Header.SourceOrder,
                    actionId,
                    hitWindowId,
                    policyEventId,
                    ref sampleOrder,
                    samples,
                    ref sourceBounds,
                    weaponMotion: incomingMotion ?? frame);
            }

            int proximalQueryId = queryId++;
            if (WeaponTraceQueryBuilder.TryBuildCurrentProximalCapsule(
                    frame,
                    sourceEntityId,
                    actionId,
                    proximalQueryId,
                    traceIndex,
                    out CombatCapsuleQuery proximalQuery))
            {
                AppendWeaponSample(
                    proximalQuery,
                    CharacterTestDirectHitTraceSampleKind.WeaponTraceProximal,
                    localFrame,
                    proximalQuery.Header.SourceOrder,
                    actionId,
                    hitWindowId,
                    policyEventId,
                    ref sampleOrder,
                    samples,
                    ref sourceBounds,
                    ProximalTraceResolvePriority,
                    weaponMotion: incomingMotion ?? frame);
            }

            return queryId - firstQueryId;
        }

        internal static bool TryAppendHitVolumeSample(
            CharacterTestDirectHitTraceSampleKind kind,
            CombatEntityId sourceEntityId,
            int actionId,
            int traceId,
            int hitWindowId,
            int localFrame,
            int sourceOrder,
            int queryOffset,
            CombatPhysicsLayerMask targetMask,
            FixVector3 pointA,
            FixVector3 pointB,
            Fix64 radius,
            ref int sampleOrder,
            List<CharacterTestDirectHitTracePlanSample> samples,
            ref CharacterTestDirectHitTraceBounds sourceBounds,
            out CharacterTestDirectHitTracePlanSample sample)
        {
            return TryAppendHitVolumeSample(
                kind,
                sourceEntityId,
                actionId,
                traceId,
                traceId,
                hitWindowId,
                localFrame,
                sourceOrder,
                queryOffset,
                targetMask,
                pointA,
                pointB,
                radius,
                ref sampleOrder,
                samples,
                ref sourceBounds,
                out sample);
        }

        internal static bool TryAppendHitVolumeSample(
            CharacterTestDirectHitTraceSampleKind kind,
            CombatEntityId sourceEntityId,
            int actionId,
            int traceId,
            int policyEventId,
            int hitWindowId,
            int localFrame,
            int sourceOrder,
            int queryOffset,
            CombatPhysicsLayerMask targetMask,
            FixVector3 pointA,
            FixVector3 pointB,
            Fix64 radius,
            ref int sampleOrder,
            List<CharacterTestDirectHitTracePlanSample> samples,
            ref CharacterTestDirectHitTraceBounds sourceBounds,
            out CharacterTestDirectHitTracePlanSample sample)
        {
            sample = default;
            if (samples == null || radius <= Fix64.Zero)
                return false;

            CombatQueryHeader header = new CombatQueryHeader(
                queryId: traceId + sourceOrder + 1 + queryOffset,
                kind: CombatQueryKind.Capsule,
                sourceEntityId: sourceEntityId,
                traceId: traceId,
                actionId: actionId,
                // Preserve the current CharacterTest hit-volume header semantics.
                sourceOrder: hitWindowId,
                layerMask: targetMask);
            CombatCapsuleQuery query = new CombatCapsuleQuery(header, pointA, pointB, radius);
            sample = AppendSample(
                query,
                kind,
                localFrame,
                sampleOrder++,
                queryOffset,
                hitWindowId,
                actionId,
                traceId,
                policyEventId,
                resolvePriority: 0,
                samples,
                ref sourceBounds);
            return true;
        }

        internal static bool TryBuildManualCapsule(
            CharacterTestDirectHitTraceSourcePose sourcePose,
            CharacterActionHitVolumeConfig hitVolume,
            out FixVector3 pointA,
            out FixVector3 pointB,
            out Fix64 radius)
        {
            pointA = sourcePose.TransformLocal(new FixVector3(
                ToFix64(hitVolume.LocalPointAX),
                ToFix64(hitVolume.LocalPointAY),
                ToFix64(hitVolume.LocalPointAZ)));
            pointB = sourcePose.TransformLocal(new FixVector3(
                ToFix64(hitVolume.LocalPointBX),
                ToFix64(hitVolume.LocalPointBY),
                ToFix64(hitVolume.LocalPointBZ)));
            radius = ToFix64(hitVolume.Radius);
            return radius > Fix64.Zero;
        }

        internal static bool TryBuildCasterBodyCapsule(
            CharacterTestDirectHitTraceSourcePose sourcePose,
            FixVector3 bodyCenter,
            FixVector3 halfExtents,
            CharacterActionHitVolumeConfig hitVolume,
            out FixVector3 center,
            out FixVector3 pointA,
            out FixVector3 pointB,
            out Fix64 radius,
            out Fix64 segmentHalf)
        {
            center = bodyCenter + sourcePose.RotateLocal(new FixVector3(
                ToFix64(hitVolume.BodyOffsetX),
                ToFix64(hitVolume.BodyOffsetY),
                ToFix64(hitVolume.BodyOffsetZ)));
            radius = halfExtents.X * ToFix64(hitVolume.BodyRadiusScale)
                + ToFix64(hitVolume.BodyRadiusAdd);
            if (radius <= Fix64.Zero)
            {
                pointA = FixVector3.Zero;
                pointB = FixVector3.Zero;
                segmentHalf = Fix64.Zero;
                return false;
            }

            Fix64 height = (halfExtents.Y * Fix64.FromInt(2)) * ToFix64(hitVolume.BodyHeightScale)
                + ToFix64(hitVolume.BodyHeightAdd);
            if (height <= Fix64.Zero)
            {
                pointA = FixVector3.Zero;
                pointB = FixVector3.Zero;
                segmentHalf = Fix64.Zero;
                return false;
            }

            segmentHalf = height / Fix64.FromInt(2) - radius;
            if (segmentHalf < Fix64.Zero)
                segmentHalf = Fix64.Zero;
            FixVector3 vertical = new FixVector3(Fix64.Zero, segmentHalf, Fix64.Zero);
            pointA = center - vertical;
            pointB = center + vertical;
            return true;
        }

        internal static bool TryBuildBakedSocketCapsule(
            CharacterTestDirectHitTraceSourcePose sourcePose,
            MxAnimationBakedVector3 start,
            MxAnimationBakedVector3 end,
            float authoredRadius,
            out FixVector3 pointA,
            out FixVector3 pointB,
            out Fix64 radius)
        {
            pointA = sourcePose.TransformLocal(ToBakedSocketLocal(start));
            pointB = sourcePose.TransformLocal(ToBakedSocketLocal(end));
            radius = ToFix64(authoredRadius);
            return radius > Fix64.Zero;
        }

        internal static FixVector3 TransformLocal(
            CharacterTestDirectHitTraceSourcePose sourcePose,
            FixVector3 local)
        {
            return sourcePose.TransformLocal(local);
        }

        private static void AppendWeaponSample(
            CombatCapsuleQuery query,
            CharacterTestDirectHitTraceSampleKind kind,
            int localFrame,
            int queryOffset,
            int actionId,
            int hitWindowId,
            int policyEventId,
            ref int sampleOrder,
            List<CharacterTestDirectHitTracePlanSample> samples,
            ref CharacterTestDirectHitTraceBounds sourceBounds,
            int resolvePriority = 0,
            WeaponTraceFrame? weaponMotion = null)
        {
            AppendSample(
                query,
                kind,
                localFrame,
                sampleOrder++,
                queryOffset,
                hitWindowId,
                actionId,
                query.Header.TraceId,
                policyEventId,
                resolvePriority,
                samples,
                ref sourceBounds,
                weaponMotion);
        }

        private static CharacterTestDirectHitTracePlanSample AppendSample(
            CombatCapsuleQuery query,
            CharacterTestDirectHitTraceSampleKind kind,
            int localFrame,
            int sampleOrder,
            int queryOffset,
            int hitWindowId,
            int actionId,
            int traceId,
            int policyEventId,
            int resolvePriority,
            List<CharacterTestDirectHitTracePlanSample> samples,
            ref CharacterTestDirectHitTraceBounds sourceBounds,
            WeaponTraceFrame? weaponMotion = null)
        {
            int stableSampleId = BuildStableSampleId(
                query.Header.SourceEntityId,
                actionId,
                traceId,
                policyEventId,
                kind,
                localFrame,
                queryOffset);
            var sample = new CharacterTestDirectHitTracePlanSample(
                query,
                kind,
                localFrame,
                sampleOrder,
                queryOffset,
                hitWindowId,
                actionId,
                traceId,
                policyEventId,
                resolvePriority,
                stableSampleId,
                weaponMotion);
            samples.Add(sample);
            sourceBounds.IncludeCapsule(query.PointA, query.PointB, query.Radius);
            return sample;
        }

        private static int BuildStableSampleId(
            CombatEntityId sourceEntityId,
            int actionId,
            int traceId,
            int policyEventId,
            CharacterTestDirectHitTraceSampleKind kind,
            int localFrame,
            int queryOffset)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + sourceEntityId.Value;
                hash = hash * 31 + actionId;
                hash = hash * 31 + traceId;
                hash = hash * 31 + policyEventId;
                hash = hash * 31 + (int)kind;
                hash = hash * 31 + localFrame;
                hash = hash * 31 + queryOffset;
                hash &= 0x7fffffff;
                return hash == 0 ? 1 : hash;
            }
        }

        private static FixVector3 ToBakedSocketLocal(MxAnimationBakedVector3 value)
        {
            const long quantizationScale = 1000000L;
            return new FixVector3(
                Fix64.FromRaw(value.X * Fix64.Scale / quantizationScale),
                Fix64.FromRaw(value.Y * Fix64.Scale / quantizationScale),
                Fix64.FromRaw(value.Z * Fix64.Scale / quantizationScale));
        }

        private static Fix64 ToFix64(double value)
        {
            return Fix64.FromRaw((long)Math.Round(value * Fix64.Scale));
        }
    }
}
