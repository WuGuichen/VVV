using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using MxFramework.Animation;
using MxFramework.Audio;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Combat.GameplayBridge;
using MxFramework.Combat.Physics;
using MxFramework.Combat.Shields;
using MxFramework.Combat.Projectiles;
using MxFramework.Gameplay;
using MxFramework.Runtime;
using MxFramework.Vfx;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestRuntimeAiLabFrameSyncProbe
    {
        private static void CompareTornado(
            CharacterTestRuntimeAiLabFrameSyncTornadoSample expected,
            CharacterTestRuntimeAiLabFrameSyncTornadoSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "tornado", 0, "lastDamageTickTargetCount", expected.LastDamageTickTargetCount, actual.LastDamageTickTargetCount);
            AddMismatch(rows, "tornado", 0, "interactionLastFrameCount", expected.InteractionLastFrameCount, actual.InteractionLastFrameCount);
            AddMismatch(rows, "tornado", 0, "interactionLastFrameHash", expected.InteractionLastFrameHash, actual.InteractionLastFrameHash);
        }

        private static void CompareChainLightning(
            CharacterTestRuntimeAiLabFrameSyncChainLightningSample expected,
            CharacterTestRuntimeAiLabFrameSyncChainLightningSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "chainLightning", 0, "lastTargetCount", expected.LastTargetCount, actual.LastTargetCount);
            AddMismatch(rows, "chainLightning", 0, "arcEventCount", expected.ArcEventCount, actual.ArcEventCount);
            AddMismatch(rows, "chainLightning", 0, "arcEventHash", expected.ArcEventHash, actual.ArcEventHash);

            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample> expectedEvents =
                expected.ArcEvents;
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample> actualEvents =
                actual.ArcEvents;
            int expectedCount = expectedEvents != null ? expectedEvents.Count : 0;
            int actualCount = actualEvents != null ? actualEvents.Count : 0;
            AddMismatch(rows, "chainLightning", 0, "arcEvent.count", expectedCount, actualCount);
            for (int i = 0; i < expectedCount; i++)
            {
                if (actualEvents == null || i >= actualCount)
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "chainLightning",
                        i,
                        "arcEvent.missing",
                        "present",
                        "missing"));
                    continue;
                }

                CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample left = expectedEvents[i];
                CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample right = actualEvents[i];
                AddMismatch(rows, "chainLightning", i, "arcEvent.frame", left.Frame, right.Frame);
                AddMismatch(rows, "chainLightning", i, "arcEvent.sourceEntityId", left.SourceEntityId, right.SourceEntityId);
                AddMismatch(rows, "chainLightning", i, "arcEvent.targetEntityId", left.TargetEntityId, right.TargetEntityId);
                AddMismatch(rows, "chainLightning", i, "arcEvent.segmentIndex", left.SegmentIndex, right.SegmentIndex);
                AddMismatch(rows, "chainLightning", i, "arcEvent.actionInstanceId", left.ActionInstanceId, right.ActionInstanceId);
                AddMismatch(rows, "chainLightning", i, "arcEvent.localFrame", left.LocalFrame, right.LocalFrame);
                AddMismatch(rows, "chainLightning", i, "arcEvent.originXRaw", left.OriginXRaw, right.OriginXRaw);
                AddMismatch(rows, "chainLightning", i, "arcEvent.originYRaw", left.OriginYRaw, right.OriginYRaw);
                AddMismatch(rows, "chainLightning", i, "arcEvent.originZRaw", left.OriginZRaw, right.OriginZRaw);
                AddMismatch(rows, "chainLightning", i, "arcEvent.directionXRaw", left.DirectionXRaw, right.DirectionXRaw);
                AddMismatch(rows, "chainLightning", i, "arcEvent.directionYRaw", left.DirectionYRaw, right.DirectionYRaw);
                AddMismatch(rows, "chainLightning", i, "arcEvent.directionZRaw", left.DirectionZRaw, right.DirectionZRaw);
                AddMismatch(rows, "chainLightning", i, "arcEvent.endpointXRaw", left.EndpointXRaw, right.EndpointXRaw);
                AddMismatch(rows, "chainLightning", i, "arcEvent.endpointYRaw", left.EndpointYRaw, right.EndpointYRaw);
                AddMismatch(rows, "chainLightning", i, "arcEvent.endpointZRaw", left.EndpointZRaw, right.EndpointZRaw);
                AddMismatch(rows, "chainLightning", i, "arcEvent.lengthMillimeters", left.LengthMillimeters, right.LengthMillimeters);
                AddMismatch(rows, "chainLightning", i, "arcEvent.hit", left.Hit, right.Hit);
            }
        }

        private static void CompareLaserPath(
            CharacterTestRuntimeAiLabFrameSyncLaserPathSample expected,
            CharacterTestRuntimeAiLabFrameSyncLaserPathSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "laserPath", 0, "eventCount", expected.EventCount, actual.EventCount);
            AddMismatch(rows, "laserPath", 0, "eventHash", expected.EventHash, actual.EventHash);

            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample> expectedEvents =
                expected.Events;
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample> actualEvents =
                actual.Events;
            int expectedCount = expectedEvents != null ? expectedEvents.Count : 0;
            int actualCount = actualEvents != null ? actualEvents.Count : 0;
            AddMismatch(rows, "laserPath", 0, "event.count", expectedCount, actualCount);
            for (int i = 0; i < expectedCount; i++)
            {
                if (actualEvents == null || i >= actualCount)
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "laserPath",
                        i,
                        "event.missing",
                        "present",
                        "missing"));
                    continue;
                }

                CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample left = expectedEvents[i];
                CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample right = actualEvents[i];
                AddMismatch(rows, "laserPath", i, "event.frame", left.Frame, right.Frame);
                AddMismatch(rows, "laserPath", i, "event.sourceEntityId", left.SourceEntityId, right.SourceEntityId);
                AddMismatch(rows, "laserPath", i, "event.targetEntityId", left.TargetEntityId, right.TargetEntityId);
                AddMismatch(rows, "laserPath", i, "event.projectileId", left.ProjectileId, right.ProjectileId);
                AddMismatch(rows, "laserPath", i, "event.actionInstanceId", left.ActionInstanceId, right.ActionInstanceId);
                AddMismatch(rows, "laserPath", i, "event.localFrame", left.LocalFrame, right.LocalFrame);
                AddMismatch(rows, "laserPath", i, "event.originXRaw", left.OriginXRaw, right.OriginXRaw);
                AddMismatch(rows, "laserPath", i, "event.originYRaw", left.OriginYRaw, right.OriginYRaw);
                AddMismatch(rows, "laserPath", i, "event.originZRaw", left.OriginZRaw, right.OriginZRaw);
                AddMismatch(rows, "laserPath", i, "event.directionXRaw", left.DirectionXRaw, right.DirectionXRaw);
                AddMismatch(rows, "laserPath", i, "event.directionYRaw", left.DirectionYRaw, right.DirectionYRaw);
                AddMismatch(rows, "laserPath", i, "event.directionZRaw", left.DirectionZRaw, right.DirectionZRaw);
                AddMismatch(rows, "laserPath", i, "event.endpointXRaw", left.EndpointXRaw, right.EndpointXRaw);
                AddMismatch(rows, "laserPath", i, "event.endpointYRaw", left.EndpointYRaw, right.EndpointYRaw);
                AddMismatch(rows, "laserPath", i, "event.endpointZRaw", left.EndpointZRaw, right.EndpointZRaw);
                AddMismatch(rows, "laserPath", i, "event.lengthMillimeters", left.LengthMillimeters, right.LengthMillimeters);
                AddMismatch(rows, "laserPath", i, "event.hit", left.Hit, right.Hit);
            }
        }

        private static void CompareInteraction(
            CharacterTestRuntimeAiLabFrameSyncInteractionSample expected,
            CharacterTestRuntimeAiLabFrameSyncInteractionSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "interaction", 0, "lastFrameEventCount", expected.LastFrameEventCount, actual.LastFrameEventCount);
            AddMismatch(rows, "interaction", 0, "lastFrameEventHash", expected.LastFrameEventHash, actual.LastFrameEventHash);
            AddMismatch(rows, "interaction", 0, "vfx.frame", expected.VfxFrame, actual.VfxFrame);
            AddMismatch(rows, "interaction", 0, "vfx.processedInteractionCount", expected.VfxProcessedInteractionCount, actual.VfxProcessedInteractionCount);
            AddMismatch(rows, "interaction", 0, "vfx.spawnedCount", expected.VfxSpawnedCount, actual.VfxSpawnedCount);
            AddMismatch(rows, "interaction", 0, "vfx.skippedCount", expected.VfxSkippedCount, actual.VfxSkippedCount);
            AddMismatch(rows, "interaction", 0, "vfx.lastStableId", expected.VfxLastStableId, actual.VfxLastStableId);
            AddMismatch(rows, "interaction", 0, "vfx.lastForceMode", expected.VfxLastForceMode, actual.VfxLastForceMode);
            AddMismatch(rows, "interaction", 0, "vfx.lastFieldShape", expected.VfxLastFieldShape, actual.VfxLastFieldShape);
            AddMismatch(rows, "interaction", 0, "vfx.lastVfxDefinitionId", expected.VfxLastVfxDefinitionId, actual.VfxLastVfxDefinitionId);

            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncInteractionEventSample> expectedEvents =
                expected.LastFrameEvents;
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncInteractionEventSample> actualEvents =
                actual.LastFrameEvents;
            int expectedCount = expectedEvents != null ? expectedEvents.Count : 0;
            int actualCount = actualEvents != null ? actualEvents.Count : 0;
            AddMismatch(rows, "interaction", 0, "event.count", expectedCount, actualCount);
            for (int i = 0; i < expectedCount; i++)
            {
                if (actualEvents == null || i >= actualCount)
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "interaction",
                        i,
                        "event.missing",
                        "present",
                        "missing"));
                    continue;
                }

                CharacterTestRuntimeAiLabFrameSyncInteractionEventSample left = expectedEvents[i];
                CharacterTestRuntimeAiLabFrameSyncInteractionEventSample right = actualEvents[i];
                AddMismatch(rows, "interaction", i, "event.frame", left.Frame, right.Frame);
                AddMismatch(rows, "interaction", i, "event.kind", left.Kind, right.Kind);
                AddMismatch(rows, "interaction", i, "event.stableId", left.StableId, right.StableId);
                AddMismatch(rows, "interaction", i, "event.sourceOrder", left.SourceOrder, right.SourceOrder);
                AddMismatch(rows, "interaction", i, "event.sourceEntityId", left.SourceEntityId, right.SourceEntityId);
                AddMismatch(rows, "interaction", i, "event.targetEntityId", left.TargetEntityId, right.TargetEntityId);
                AddMismatch(rows, "interaction", i, "event.anchorXRaw", left.AnchorXRaw, right.AnchorXRaw);
                AddMismatch(rows, "interaction", i, "event.anchorYRaw", left.AnchorYRaw, right.AnchorYRaw);
                AddMismatch(rows, "interaction", i, "event.anchorZRaw", left.AnchorZRaw, right.AnchorZRaw);
                AddMismatch(rows, "interaction", i, "event.directionXRaw", left.DirectionXRaw, right.DirectionXRaw);
                AddMismatch(rows, "interaction", i, "event.directionYRaw", left.DirectionYRaw, right.DirectionYRaw);
                AddMismatch(rows, "interaction", i, "event.directionZRaw", left.DirectionZRaw, right.DirectionZRaw);
                AddMismatch(rows, "interaction", i, "event.radiusRaw", left.RadiusRaw, right.RadiusRaw);
                AddMismatch(rows, "interaction", i, "event.intensity", left.Intensity, right.Intensity);
                AddMismatch(rows, "interaction", i, "event.traceId", left.TraceId, right.TraceId);
                AddMismatch(rows, "interaction", i, "event.sourceDomain", left.SourceDomain, right.SourceDomain);
                AddMismatch(rows, "interaction", i, "event.fieldShape", left.FieldShape, right.FieldShape);
                AddMismatch(rows, "interaction", i, "event.fieldForceMode", left.FieldForceMode, right.FieldForceMode);
                AddMismatch(rows, "interaction", i, "event.fieldTargetFilter", left.FieldTargetFilter, right.FieldTargetFilter);
                AddMismatch(rows, "interaction", i, "event.fieldCapsuleBottomOffsetRaw", left.FieldCapsuleBottomOffsetRaw, right.FieldCapsuleBottomOffsetRaw);
                AddMismatch(rows, "interaction", i, "event.fieldCapsuleTopOffsetRaw", left.FieldCapsuleTopOffsetRaw, right.FieldCapsuleTopOffsetRaw);
                AddMismatch(rows, "interaction", i, "event.fieldImpulseSpeedMillimetersPerSecond", left.FieldImpulseSpeedMillimetersPerSecond, right.FieldImpulseSpeedMillimetersPerSecond);
                AddMismatch(rows, "interaction", i, "event.fieldAccelerationMillimetersPerSecondSquared", left.FieldAccelerationMillimetersPerSecondSquared, right.FieldAccelerationMillimetersPerSecondSquared);
                AddMismatch(rows, "interaction", i, "event.fieldLinearFalloffMinimumScaleMilli", left.FieldLinearFalloffMinimumScaleMilli, right.FieldLinearFalloffMinimumScaleMilli);
                AddMismatch(rows, "interaction", i, "event.fieldMaxSpeedMillimetersPerSecond", left.FieldMaxSpeedMillimetersPerSecond, right.FieldMaxSpeedMillimetersPerSecond);
                AddMismatch(rows, "interaction", i, "event.fieldReferenceMassGrams", left.FieldReferenceMassGrams, right.FieldReferenceMassGrams);
                AddMismatch(rows, "interaction", i, "event.fieldMinimumMassScaleMilli", left.FieldMinimumMassScaleMilli, right.FieldMinimumMassScaleMilli);
                AddMismatch(rows, "interaction", i, "event.fieldMaximumMassScaleMilli", left.FieldMaximumMassScaleMilli, right.FieldMaximumMassScaleMilli);
                AddMismatch(rows, "interaction", i, "event.fieldUseMassScale", left.FieldUseMassScale, right.FieldUseMassScale);
                AddMismatch(rows, "interaction", i, "event.fieldUsePushScale", left.FieldUsePushScale, right.FieldUsePushScale);
                AddMismatch(rows, "interaction", i, "event.fieldUseMaterialPushScale", left.FieldUseMaterialPushScale, right.FieldUseMaterialPushScale);
            }
        }

        private static void CompareMovableNaturalObjects(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample> expected,
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample> actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            int expectedCount = expected != null ? expected.Count : 0;
            int actualCount = actual != null ? actual.Count : 0;
            AddMismatch(rows, "movableObject", 0, "count", expectedCount, actualCount);
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample left = expected[i];
                if (!TryFindMovableNaturalObject(
                    actual,
                    left.NumericId,
                    out CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "movableObject",
                        left.NumericId,
                        "missing",
                        "present",
                        "missing"));
                    continue;
                }

                AddMismatch(rows, "movableObject", left.NumericId, "stableId", left.StableId, right.StableId);
                AddMismatch(rows, "movableObject", left.NumericId, "category", left.Category, right.Category);
                AddMismatch(rows, "movableObject", left.NumericId, "massGrams", left.MassGrams, right.MassGrams);
                AddMismatch(rows, "movableObject", left.NumericId, "materialKind", left.MaterialKind, right.MaterialKind);
                AddMismatch(rows, "movableObject", left.NumericId, "shapeKind", left.ShapeKind, right.ShapeKind);
                AddMismatch(rows, "movableObject", left.NumericId, "halfExtentX", left.HalfExtentXMillimeters, right.HalfExtentXMillimeters);
                AddMismatch(rows, "movableObject", left.NumericId, "halfExtentY", left.HalfExtentYMillimeters, right.HalfExtentYMillimeters);
                AddMismatch(rows, "movableObject", left.NumericId, "halfExtentZ", left.HalfExtentZMillimeters, right.HalfExtentZMillimeters);
                AddMismatch(rows, "movableObject", left.NumericId, "x", left.XMillimeters, right.XMillimeters);
                AddMismatch(rows, "movableObject", left.NumericId, "y", left.YMillimeters, right.YMillimeters);
                AddMismatch(rows, "movableObject", left.NumericId, "z", left.ZMillimeters, right.ZMillimeters);
                AddMismatch(rows, "movableObject", left.NumericId, "velocityX", left.VelocityXMillimetersPerSecond, right.VelocityXMillimetersPerSecond);
                AddMismatch(rows, "movableObject", left.NumericId, "velocityY", left.VelocityYMillimetersPerSecond, right.VelocityYMillimetersPerSecond);
                AddMismatch(rows, "movableObject", left.NumericId, "velocityZ", left.VelocityZMillimetersPerSecond, right.VelocityZMillimetersPerSecond);
                AddMismatch(rows, "movableObject", left.NumericId, "contactVelocityX", left.ContactVelocityXMillimetersPerSecond, right.ContactVelocityXMillimetersPerSecond);
                AddMismatch(rows, "movableObject", left.NumericId, "contactVelocityZ", left.ContactVelocityZMillimetersPerSecond, right.ContactVelocityZMillimetersPerSecond);
                AddMismatch(rows, "movableObject", left.NumericId, "contactDisplacementX", left.ContactDisplacementXMillimeters, right.ContactDisplacementXMillimeters);
                AddMismatch(rows, "movableObject", left.NumericId, "contactDisplacementZ", left.ContactDisplacementZMillimeters, right.ContactDisplacementZMillimeters);
                AddMismatch(rows, "movableObject", left.NumericId, "contactPressure", left.ContactPressure, right.ContactPressure);
                AddMismatch(rows, "movableObject", left.NumericId, "contactCount", left.ContactCount, right.ContactCount);
                AddMismatch(rows, "movableObject", left.NumericId, "contactPair", left.ContactPair, right.ContactPair);
                AddMismatch(rows, "movableObject", left.NumericId, "behaviorState", left.BehaviorState, right.BehaviorState);
                AddMismatch(rows, "movableObject", left.NumericId, "basePotencyMotion", left.BasePotencyMotion, right.BasePotencyMotion);
                AddMismatch(rows, "movableObject", left.NumericId, "basePotencyCohesion", left.BasePotencyCohesion, right.BasePotencyCohesion);
                AddMismatch(rows, "movableObject", left.NumericId, "basePotencyScatter", left.BasePotencyScatter, right.BasePotencyScatter);
                AddMismatch(rows, "movableObject", left.NumericId, "currentPotencyMotion", left.CurrentPotencyMotion, right.CurrentPotencyMotion);
                AddMismatch(rows, "movableObject", left.NumericId, "currentPotencyCohesion", left.CurrentPotencyCohesion, right.CurrentPotencyCohesion);
                AddMismatch(rows, "movableObject", left.NumericId, "currentPotencyScatter", left.CurrentPotencyScatter, right.CurrentPotencyScatter);
                AddMismatch(rows, "movableObject", left.NumericId, "pendingPotencyMotion", left.PendingPotencyMotion, right.PendingPotencyMotion);
                AddMismatch(rows, "movableObject", left.NumericId, "pendingPotencyCohesion", left.PendingPotencyCohesion, right.PendingPotencyCohesion);
                AddMismatch(rows, "movableObject", left.NumericId, "pendingPotencyScatter", left.PendingPotencyScatter, right.PendingPotencyScatter);
                AddMismatch(rows, "movableObject", left.NumericId, "pendingPotencyInfluenceKind", left.PendingPotencyInfluenceKind, right.PendingPotencyInfluenceKind);
                AddMismatch(rows, "movableObject", left.NumericId, "latestPotencyFrame", left.LatestPotencyFrame, right.LatestPotencyFrame);
                AddMismatch(rows, "movableObject", left.NumericId, "latestPotencyTargetNumericId", left.LatestPotencyTargetNumericId, right.LatestPotencyTargetNumericId);
                AddMismatch(rows, "movableObject", left.NumericId, "latestPotencyInfluenceKind", left.LatestPotencyInfluenceKind, right.LatestPotencyInfluenceKind);
                AddMismatch(rows, "movableObject", left.NumericId, "latestPotencyAppliedMotion", left.LatestPotencyAppliedMotion, right.LatestPotencyAppliedMotion);
                AddMismatch(rows, "movableObject", left.NumericId, "latestPotencyAppliedCohesion", left.LatestPotencyAppliedCohesion, right.LatestPotencyAppliedCohesion);
                AddMismatch(rows, "movableObject", left.NumericId, "latestPotencyAppliedScatter", left.LatestPotencyAppliedScatter, right.LatestPotencyAppliedScatter);
                AddMismatch(rows, "movableObject", left.NumericId, "latestPotencyAfterMotion", left.LatestPotencyAfterMotion, right.LatestPotencyAfterMotion);
                AddMismatch(rows, "movableObject", left.NumericId, "latestPotencyAfterCohesion", left.LatestPotencyAfterCohesion, right.LatestPotencyAfterCohesion);
                AddMismatch(rows, "movableObject", left.NumericId, "latestPotencyAfterScatter", left.LatestPotencyAfterScatter, right.LatestPotencyAfterScatter);
                AddMismatch(rows, "movableObject", left.NumericId, "latestPotencyResultKind", left.LatestPotencyResultKind, right.LatestPotencyResultKind);
                AddMismatch(rows, "movableObject", left.NumericId, "latestPotencyResultSource", left.LatestPotencyResultSource, right.LatestPotencyResultSource);
            }
        }

        private static bool TryFindMovableNaturalObject(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample> samples,
            int numericId,
            out CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.NumericId == numericId)
                    return true;
            }

            sample = default;
            return false;
        }
    }
}
namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestRuntimeAiLabFrameSyncInteractionSample
    {
        public CharacterTestRuntimeAiLabFrameSyncInteractionSample(
            int lastFrameEventCount,
            long lastFrameEventHash,
            int vfxFrame,
            int vfxProcessedInteractionCount,
            int vfxSpawnedCount,
            int vfxSkippedCount,
            int vfxLastStableId,
            int vfxLastForceMode,
            int vfxLastFieldShape,
            int vfxLastVfxDefinitionId,
            CharacterTestRuntimeAiLabFrameSyncInteractionEventSample[] lastFrameEvents = null)
        {
            LastFrameEventCount = lastFrameEventCount < 0 ? 0 : lastFrameEventCount;
            LastFrameEventHash = lastFrameEventHash;
            VfxFrame = vfxFrame < 0 ? 0 : vfxFrame;
            VfxProcessedInteractionCount = vfxProcessedInteractionCount < 0 ? 0 : vfxProcessedInteractionCount;
            VfxSpawnedCount = vfxSpawnedCount < 0 ? 0 : vfxSpawnedCount;
            VfxSkippedCount = vfxSkippedCount < 0 ? 0 : vfxSkippedCount;
            VfxLastStableId = vfxLastStableId;
            VfxLastForceMode = vfxLastForceMode;
            VfxLastFieldShape = vfxLastFieldShape;
            VfxLastVfxDefinitionId = vfxLastVfxDefinitionId < 0 ? 0 : vfxLastVfxDefinitionId;
            LastFrameEvents = lastFrameEvents ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncInteractionEventSample>();
        }

        public int LastFrameEventCount { get; }
        public long LastFrameEventHash { get; }
        public int VfxFrame { get; }
        public int VfxProcessedInteractionCount { get; }
        public int VfxSpawnedCount { get; }
        public int VfxSkippedCount { get; }
        public int VfxLastStableId { get; }
        public int VfxLastForceMode { get; }
        public int VfxLastFieldShape { get; }
        public int VfxLastVfxDefinitionId { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncInteractionEventSample> LastFrameEvents { get; }

        public static CharacterTestRuntimeAiLabFrameSyncInteractionSample Capture(
            CharacterTestInteractionHistory interactionHistory,
            CharacterTestInteractionVfxSummary vfxSummary)
        {
            IReadOnlyList<CharacterTestInteractionEvent> events = interactionHistory != null
                ? interactionHistory.LastFrameEvents
                : null;
            int eventCount = events != null ? events.Count : 0;
            var eventSamples = new CharacterTestRuntimeAiLabFrameSyncInteractionEventSample[eventCount];
            for (int i = 0; i < eventCount; i++)
                eventSamples[i] = CharacterTestRuntimeAiLabFrameSyncInteractionEventSample.Capture(events[i]);

            int vfxFrame = vfxSummary.Frame.Value > int.MaxValue
                ? int.MaxValue
                : (int)vfxSummary.Frame.Value;
            return new CharacterTestRuntimeAiLabFrameSyncInteractionSample(
                eventCount,
                ComputeLastFrameInteractionHash(interactionHistory),
                vfxFrame,
                vfxSummary.ProcessedInteractionCount,
                vfxSummary.VfxSpawnedCount,
                vfxSummary.SkippedCount,
                vfxSummary.LastStableId,
                (int)vfxSummary.LastForceMode,
                (int)vfxSummary.LastFieldShape,
                vfxSummary.LastVfxDefinitionId,
                eventSamples);
        }

        private static long ComputeLastFrameInteractionHash(CharacterTestInteractionHistory interactionHistory)
        {
            IReadOnlyList<CharacterTestInteractionEvent> events = interactionHistory != null
                ? interactionHistory.LastFrameEvents
                : null;
            int count = events != null ? events.Count : 0;
            if (count <= 0)
                return 0L;

            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerLabFrameState, RuntimeHashSchemaIds.Version1);
            const int EventsKey = 1, EventKey = 2;
            accumulator.BeginArray(EventsKey, count);
            for (int i = 0; i < count; i++)
            {
                accumulator.BeginElement(i);
                events[i].Contribute(accumulator, EventKey);
                accumulator.EndElement();
            }
            accumulator.EndArray();

            return accumulator.ToHash();
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncInteractionEventSample
    {
        public CharacterTestRuntimeAiLabFrameSyncInteractionEventSample(
            long frame,
            int kind,
            int stableId,
            int sourceOrder,
            int sourceEntityId,
            int targetEntityId,
            long anchorXRaw,
            long anchorYRaw,
            long anchorZRaw,
            long directionXRaw,
            long directionYRaw,
            long directionZRaw,
            long radiusRaw,
            int intensity,
            int traceId,
            int sourceDomain,
            int fieldShape,
            int fieldForceMode,
            int fieldTargetFilter,
            long fieldCapsuleBottomOffsetRaw,
            long fieldCapsuleTopOffsetRaw,
            int fieldImpulseSpeedMillimetersPerSecond,
            int fieldAccelerationMillimetersPerSecondSquared,
            int fieldLinearFalloffMinimumScaleMilli,
            int fieldMaxSpeedMillimetersPerSecond,
            int fieldReferenceMassGrams,
            int fieldMinimumMassScaleMilli,
            int fieldMaximumMassScaleMilli,
            bool fieldUseMassScale,
            bool fieldUsePushScale,
            bool fieldUseMaterialPushScale)
        {
            Frame = frame;
            Kind = kind;
            StableId = stableId;
            SourceOrder = sourceOrder;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            AnchorXRaw = anchorXRaw;
            AnchorYRaw = anchorYRaw;
            AnchorZRaw = anchorZRaw;
            DirectionXRaw = directionXRaw;
            DirectionYRaw = directionYRaw;
            DirectionZRaw = directionZRaw;
            RadiusRaw = radiusRaw;
            Intensity = intensity;
            TraceId = traceId;
            SourceDomain = sourceDomain;
            FieldShape = fieldShape;
            FieldForceMode = fieldForceMode;
            FieldTargetFilter = fieldTargetFilter;
            FieldCapsuleBottomOffsetRaw = fieldCapsuleBottomOffsetRaw;
            FieldCapsuleTopOffsetRaw = fieldCapsuleTopOffsetRaw;
            FieldImpulseSpeedMillimetersPerSecond = fieldImpulseSpeedMillimetersPerSecond;
            FieldAccelerationMillimetersPerSecondSquared = fieldAccelerationMillimetersPerSecondSquared;
            FieldLinearFalloffMinimumScaleMilli = fieldLinearFalloffMinimumScaleMilli;
            FieldMaxSpeedMillimetersPerSecond = fieldMaxSpeedMillimetersPerSecond;
            FieldReferenceMassGrams = fieldReferenceMassGrams;
            FieldMinimumMassScaleMilli = fieldMinimumMassScaleMilli;
            FieldMaximumMassScaleMilli = fieldMaximumMassScaleMilli;
            FieldUseMassScale = fieldUseMassScale;
            FieldUsePushScale = fieldUsePushScale;
            FieldUseMaterialPushScale = fieldUseMaterialPushScale;
        }

        public long Frame { get; }
        public int Kind { get; }
        public int StableId { get; }
        public int SourceOrder { get; }
        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public long AnchorXRaw { get; }
        public long AnchorYRaw { get; }
        public long AnchorZRaw { get; }
        public long DirectionXRaw { get; }
        public long DirectionYRaw { get; }
        public long DirectionZRaw { get; }
        public long RadiusRaw { get; }
        public int Intensity { get; }
        public int TraceId { get; }
        public int SourceDomain { get; }
        public int FieldShape { get; }
        public int FieldForceMode { get; }
        public int FieldTargetFilter { get; }
        public long FieldCapsuleBottomOffsetRaw { get; }
        public long FieldCapsuleTopOffsetRaw { get; }
        public int FieldImpulseSpeedMillimetersPerSecond { get; }
        public int FieldAccelerationMillimetersPerSecondSquared { get; }
        public int FieldLinearFalloffMinimumScaleMilli { get; }
        public int FieldMaxSpeedMillimetersPerSecond { get; }
        public int FieldReferenceMassGrams { get; }
        public int FieldMinimumMassScaleMilli { get; }
        public int FieldMaximumMassScaleMilli { get; }
        public bool FieldUseMassScale { get; }
        public bool FieldUsePushScale { get; }
        public bool FieldUseMaterialPushScale { get; }

        public static CharacterTestRuntimeAiLabFrameSyncInteractionEventSample Capture(
            CharacterTestInteractionEvent evt)
        {
            CharacterTestInteractionPhysicalField field = evt.PhysicalField;
            return new CharacterTestRuntimeAiLabFrameSyncInteractionEventSample(
                evt.Frame.Value,
                (int)evt.Kind,
                evt.StableId,
                evt.SourceOrder,
                evt.SourceEntityId,
                evt.TargetEntityId,
                evt.AnchorPosition.X.RawValue,
                evt.AnchorPosition.Y.RawValue,
                evt.AnchorPosition.Z.RawValue,
                evt.Direction.X.RawValue,
                evt.Direction.Y.RawValue,
                evt.Direction.Z.RawValue,
                evt.Radius.RawValue,
                evt.Intensity,
                evt.TraceId,
                evt.SourceDomain,
                (int)field.Shape,
                (int)field.ForceMode,
                (int)field.TargetFilter,
                field.CapsuleBottomOffset.RawValue,
                field.CapsuleTopOffset.RawValue,
                field.ImpulseSpeedMillimetersPerSecond,
                field.AccelerationMillimetersPerSecondSquared,
                field.LinearFalloffMinimumScaleMilli,
                field.MaxSpeedMillimetersPerSecond,
                field.ReferenceMassGrams,
                field.MinimumMassScaleMilli,
                field.MaximumMassScaleMilli,
                field.UseMassScale,
                field.UsePushScale,
                field.UseMaterialPushScale);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncTornadoSample
    {
        public CharacterTestRuntimeAiLabFrameSyncTornadoSample(
            int lastDamageTickTargetCount,
            int interactionLastFrameCount,
            long interactionLastFrameHash)
        {
            LastDamageTickTargetCount = lastDamageTickTargetCount;
            InteractionLastFrameCount = interactionLastFrameCount;
            InteractionLastFrameHash = interactionLastFrameHash;
        }

        public int LastDamageTickTargetCount { get; }
        public int InteractionLastFrameCount { get; }
        public long InteractionLastFrameHash { get; }

        public static CharacterTestRuntimeAiLabFrameSyncTornadoSample Capture(
            int lastDamageTickTargetCount,
            CharacterTestInteractionHistory interactionHistory)
        {
            return new CharacterTestRuntimeAiLabFrameSyncTornadoSample(
                lastDamageTickTargetCount,
                interactionHistory != null ? interactionHistory.LastFrameEventCount : 0,
                ComputeLastFrameInteractionHash(interactionHistory));
        }

        private static long ComputeLastFrameInteractionHash(CharacterTestInteractionHistory interactionHistory)
        {
            IReadOnlyList<CharacterTestInteractionEvent> events = interactionHistory != null
                ? interactionHistory.LastFrameEvents
                : null;
            int count = events != null ? events.Count : 0;
            if (count <= 0)
                return 0L;

            var accumulator = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerLabFrameState, RuntimeHashSchemaIds.Version1);
            const int EventsKey = 1, EventKey = 2;
            accumulator.BeginArray(EventsKey, count);
            for (int i = 0; i < count; i++)
            {
                accumulator.BeginElement(i);
                events[i].Contribute(accumulator, EventKey);
                accumulator.EndElement();
            }
            accumulator.EndArray();

            return accumulator.ToHash();
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncChainLightningSample
    {
        public CharacterTestRuntimeAiLabFrameSyncChainLightningSample(
            int lastTargetCount,
            int arcEventCount,
            long arcEventHash,
            CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample[] arcEvents = null)
        {
            LastTargetCount = lastTargetCount;
            ArcEventCount = arcEventCount;
            ArcEventHash = arcEventHash;
            ArcEvents = arcEvents ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample>();
        }

        public int LastTargetCount { get; }
        public int ArcEventCount { get; }
        public long ArcEventHash { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample> ArcEvents { get; }

        public static CharacterTestRuntimeAiLabFrameSyncChainLightningSample Capture(
            int lastTargetCount,
            IReadOnlyList<CharacterTestChainLightningVfxEvent> arcEvents)
        {
            int count = arcEvents != null ? arcEvents.Count : 0;
            var samples = new CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample[count];
            for (int i = 0; i < count; i++)
                samples[i] = CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample.Capture(arcEvents[i]);

            return new CharacterTestRuntimeAiLabFrameSyncChainLightningSample(
                lastTargetCount,
                count,
                ComputeArcEventHash(arcEvents),
                samples);
        }

        private static long ComputeArcEventHash(IReadOnlyList<CharacterTestChainLightningVfxEvent> arcEvents)
        {
            int count = arcEvents != null ? arcEvents.Count : 0;
            if (count <= 0)
                return 0L;

            var accumulator = new RuntimeHashAccumulator(91041001, 1);
            const int Field1 = 1; // .frame
            const int Field2 = 2; // .sourceEntityId
            const int Field3 = 3; // .targetEntityId
            const int Field4 = 4; // .segmentIndex
            const int Field5 = 5; // .actionInstanceId
            const int Field6 = 6; // .localFrame
            const int Field7 = 7; // .origin.x.raw
            const int Field8 = 8; // .origin.y.raw
            const int Field9 = 9; // .origin.z.raw
            const int Field10 = 10; // .direction.x.raw
            const int Field11 = 11; // .direction.y.raw
            const int Field12 = 12; // .direction.z.raw
            const int Field13 = 13; // .endpoint.x.raw
            const int Field14 = 14; // .endpoint.y.raw
            const int Field15 = 15; // .endpoint.z.raw
            const int Field16 = 16; // .lengthMillimeters
            const int Field17 = 17; // .hit
            accumulator.BeginArray(1, count);
            for (int i = 0; i < count; i++)
            {
                CharacterTestChainLightningVfxEvent evt = arcEvents[i];
                accumulator.BeginElement(i);
                accumulator.AddLong(Field1, evt.Frame.Value);
                accumulator.AddInt(Field2, evt.SourceEntityId);
                accumulator.AddInt(Field3, evt.TargetEntityId);
                accumulator.AddInt(Field4, evt.SegmentIndex);
                accumulator.AddInt(Field5, evt.ActionInstanceId);
                accumulator.AddInt(Field6, evt.LocalFrame);
                accumulator.AddLong(Field7, evt.Origin.X.RawValue);
                accumulator.AddLong(Field8, evt.Origin.Y.RawValue);
                accumulator.AddLong(Field9, evt.Origin.Z.RawValue);
                accumulator.AddLong(Field10, evt.Direction.X.RawValue);
                accumulator.AddLong(Field11, evt.Direction.Y.RawValue);
                accumulator.AddLong(Field12, evt.Direction.Z.RawValue);
                accumulator.AddLong(Field13, evt.Endpoint.X.RawValue);
                accumulator.AddLong(Field14, evt.Endpoint.Y.RawValue);
                accumulator.AddLong(Field15, evt.Endpoint.Z.RawValue);
                accumulator.AddInt(Field16, evt.LengthMillimeters);
                accumulator.AddInt(Field17, evt.Hit ? 1 : 0);
                accumulator.EndElement();
            }

            accumulator.EndArray();
            return accumulator.ToHash();
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample
    {
        public CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample(
            long frame,
            int sourceEntityId,
            int targetEntityId,
            int segmentIndex,
            int actionInstanceId,
            int localFrame,
            long originXRaw,
            long originYRaw,
            long originZRaw,
            long directionXRaw,
            long directionYRaw,
            long directionZRaw,
            long endpointXRaw,
            long endpointYRaw,
            long endpointZRaw,
            int lengthMillimeters,
            bool hit)
        {
            Frame = frame;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            SegmentIndex = segmentIndex;
            ActionInstanceId = actionInstanceId;
            LocalFrame = localFrame;
            OriginXRaw = originXRaw;
            OriginYRaw = originYRaw;
            OriginZRaw = originZRaw;
            DirectionXRaw = directionXRaw;
            DirectionYRaw = directionYRaw;
            DirectionZRaw = directionZRaw;
            EndpointXRaw = endpointXRaw;
            EndpointYRaw = endpointYRaw;
            EndpointZRaw = endpointZRaw;
            LengthMillimeters = lengthMillimeters;
            Hit = hit;
        }

        public long Frame { get; }
        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public int SegmentIndex { get; }
        public int ActionInstanceId { get; }
        public int LocalFrame { get; }
        public long OriginXRaw { get; }
        public long OriginYRaw { get; }
        public long OriginZRaw { get; }
        public long DirectionXRaw { get; }
        public long DirectionYRaw { get; }
        public long DirectionZRaw { get; }
        public long EndpointXRaw { get; }
        public long EndpointYRaw { get; }
        public long EndpointZRaw { get; }
        public int LengthMillimeters { get; }
        public bool Hit { get; }

        public static CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample Capture(
            CharacterTestChainLightningVfxEvent evt)
        {
            return new CharacterTestRuntimeAiLabFrameSyncChainLightningArcEventSample(
                evt.Frame.Value,
                evt.SourceEntityId,
                evt.TargetEntityId,
                evt.SegmentIndex,
                evt.ActionInstanceId,
                evt.LocalFrame,
                evt.Origin.X.RawValue,
                evt.Origin.Y.RawValue,
                evt.Origin.Z.RawValue,
                evt.Direction.X.RawValue,
                evt.Direction.Y.RawValue,
                evt.Direction.Z.RawValue,
                evt.Endpoint.X.RawValue,
                evt.Endpoint.Y.RawValue,
                evt.Endpoint.Z.RawValue,
                evt.LengthMillimeters,
                evt.Hit);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncLaserPathSample
    {
        public CharacterTestRuntimeAiLabFrameSyncLaserPathSample(
            int eventCount,
            long eventHash,
            CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample[] events = null)
        {
            EventCount = eventCount;
            EventHash = eventHash;
            Events = events ?? Array.Empty<CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample>();
        }

        public int EventCount { get; }
        public long EventHash { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample> Events { get; }

        public static CharacterTestRuntimeAiLabFrameSyncLaserPathSample Capture(
            IReadOnlyList<CharacterTestLaserPathVfxEvent> events)
        {
            int count = events != null ? events.Count : 0;
            var samples = new CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample[count];
            for (int i = 0; i < count; i++)
                samples[i] = CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample.Capture(events[i]);

            return new CharacterTestRuntimeAiLabFrameSyncLaserPathSample(
                count,
                ComputeEventHash(events),
                samples);
        }

        private static long ComputeEventHash(IReadOnlyList<CharacterTestLaserPathVfxEvent> events)
        {
            int count = events != null ? events.Count : 0;
            if (count <= 0)
                return 0L;

            var accumulator = new RuntimeHashAccumulator(91041002, 1);
            const int Field1 = 1; // .frame
            const int Field2 = 2; // .sourceEntityId
            const int Field3 = 3; // .targetEntityId
            const int Field4 = 4; // .projectileId
            const int Field5 = 5; // .actionInstanceId
            const int Field6 = 6; // .localFrame
            const int Field7 = 7; // .origin.x.raw
            const int Field8 = 8; // .origin.y.raw
            const int Field9 = 9; // .origin.z.raw
            const int Field10 = 10; // .direction.x.raw
            const int Field11 = 11; // .direction.y.raw
            const int Field12 = 12; // .direction.z.raw
            const int Field13 = 13; // .endpoint.x.raw
            const int Field14 = 14; // .endpoint.y.raw
            const int Field15 = 15; // .endpoint.z.raw
            const int Field16 = 16; // .lengthMillimeters
            const int Field17 = 17; // .hit
            accumulator.BeginArray(1, count);
            for (int i = 0; i < count; i++)
            {
                CharacterTestLaserPathVfxEvent evt = events[i];
                accumulator.BeginElement(i);
                accumulator.AddLong(Field1, evt.Frame.Value);
                accumulator.AddInt(Field2, evt.SourceEntityId);
                accumulator.AddInt(Field3, evt.TargetEntityId);
                accumulator.AddInt(Field4, evt.ProjectileId);
                accumulator.AddInt(Field5, evt.ActionInstanceId);
                accumulator.AddInt(Field6, evt.LocalFrame);
                accumulator.AddLong(Field7, evt.Origin.X.RawValue);
                accumulator.AddLong(Field8, evt.Origin.Y.RawValue);
                accumulator.AddLong(Field9, evt.Origin.Z.RawValue);
                accumulator.AddLong(Field10, evt.Direction.X.RawValue);
                accumulator.AddLong(Field11, evt.Direction.Y.RawValue);
                accumulator.AddLong(Field12, evt.Direction.Z.RawValue);
                accumulator.AddLong(Field13, evt.Endpoint.X.RawValue);
                accumulator.AddLong(Field14, evt.Endpoint.Y.RawValue);
                accumulator.AddLong(Field15, evt.Endpoint.Z.RawValue);
                accumulator.AddInt(Field16, evt.LengthMillimeters);
                accumulator.AddInt(Field17, evt.Hit ? 1 : 0);
                accumulator.EndElement();
            }

            accumulator.EndArray();
            return accumulator.ToHash();
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample
    {
        public CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample(
            long frame,
            int sourceEntityId,
            int targetEntityId,
            int projectileId,
            int actionInstanceId,
            int localFrame,
            long originXRaw,
            long originYRaw,
            long originZRaw,
            long directionXRaw,
            long directionYRaw,
            long directionZRaw,
            long endpointXRaw,
            long endpointYRaw,
            long endpointZRaw,
            int lengthMillimeters,
            bool hit)
        {
            Frame = frame;
            SourceEntityId = sourceEntityId;
            TargetEntityId = targetEntityId;
            ProjectileId = projectileId;
            ActionInstanceId = actionInstanceId;
            LocalFrame = localFrame;
            OriginXRaw = originXRaw;
            OriginYRaw = originYRaw;
            OriginZRaw = originZRaw;
            DirectionXRaw = directionXRaw;
            DirectionYRaw = directionYRaw;
            DirectionZRaw = directionZRaw;
            EndpointXRaw = endpointXRaw;
            EndpointYRaw = endpointYRaw;
            EndpointZRaw = endpointZRaw;
            LengthMillimeters = lengthMillimeters;
            Hit = hit;
        }

        public long Frame { get; }
        public int SourceEntityId { get; }
        public int TargetEntityId { get; }
        public int ProjectileId { get; }
        public int ActionInstanceId { get; }
        public int LocalFrame { get; }
        public long OriginXRaw { get; }
        public long OriginYRaw { get; }
        public long OriginZRaw { get; }
        public long DirectionXRaw { get; }
        public long DirectionYRaw { get; }
        public long DirectionZRaw { get; }
        public long EndpointXRaw { get; }
        public long EndpointYRaw { get; }
        public long EndpointZRaw { get; }
        public int LengthMillimeters { get; }
        public bool Hit { get; }

        public static CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample Capture(
            CharacterTestLaserPathVfxEvent evt)
        {
            return new CharacterTestRuntimeAiLabFrameSyncLaserPathEventSample(
                evt.Frame.Value,
                evt.SourceEntityId,
                evt.TargetEntityId,
                evt.ProjectileId,
                evt.ActionInstanceId,
                evt.LocalFrame,
                evt.Origin.X.RawValue,
                evt.Origin.Y.RawValue,
                evt.Origin.Z.RawValue,
                evt.Direction.X.RawValue,
                evt.Direction.Y.RawValue,
                evt.Direction.Z.RawValue,
                evt.Endpoint.X.RawValue,
                evt.Endpoint.Y.RawValue,
                evt.Endpoint.Z.RawValue,
                evt.LengthMillimeters,
                evt.Hit);
        }
    }

    internal readonly struct CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample
    {
        public CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample(
            int numericId,
            string stableId,
            string category,
            int massGrams,
            int materialKind,
            int shapeKind,
            int halfExtentXMillimeters,
            int halfExtentYMillimeters,
            int halfExtentZMillimeters,
            int xMillimeters,
            int yMillimeters,
            int zMillimeters,
            int velocityXMillimetersPerSecond,
            int velocityYMillimetersPerSecond,
            int velocityZMillimetersPerSecond,
            int contactVelocityXMillimetersPerSecond,
            int contactVelocityZMillimetersPerSecond,
            int contactDisplacementXMillimeters,
            int contactDisplacementZMillimeters,
            int contactPressure,
            int contactCount,
            string contactPair,
            string behaviorState,
            int basePotencyMotion,
            int basePotencyCohesion,
            int basePotencyScatter,
            int currentPotencyMotion,
            int currentPotencyCohesion,
            int currentPotencyScatter,
            int pendingPotencyMotion,
            int pendingPotencyCohesion,
            int pendingPotencyScatter,
            int pendingPotencyInfluenceKind,
            long latestPotencyFrame,
            int latestPotencyTargetNumericId,
            int latestPotencyInfluenceKind,
            int latestPotencyAppliedMotion,
            int latestPotencyAppliedCohesion,
            int latestPotencyAppliedScatter,
            int latestPotencyAfterMotion,
            int latestPotencyAfterCohesion,
            int latestPotencyAfterScatter,
            int latestPotencyResultKind,
            string latestPotencyResultSource)
        {
            NumericId = numericId;
            StableId = stableId ?? string.Empty;
            Category = category ?? string.Empty;
            MassGrams = massGrams;
            MaterialKind = materialKind;
            ShapeKind = shapeKind;
            HalfExtentXMillimeters = halfExtentXMillimeters;
            HalfExtentYMillimeters = halfExtentYMillimeters;
            HalfExtentZMillimeters = halfExtentZMillimeters;
            XMillimeters = xMillimeters;
            YMillimeters = yMillimeters;
            ZMillimeters = zMillimeters;
            VelocityXMillimetersPerSecond = velocityXMillimetersPerSecond;
            VelocityYMillimetersPerSecond = velocityYMillimetersPerSecond;
            VelocityZMillimetersPerSecond = velocityZMillimetersPerSecond;
            ContactVelocityXMillimetersPerSecond = contactVelocityXMillimetersPerSecond;
            ContactVelocityZMillimetersPerSecond = contactVelocityZMillimetersPerSecond;
            ContactDisplacementXMillimeters = contactDisplacementXMillimeters;
            ContactDisplacementZMillimeters = contactDisplacementZMillimeters;
            ContactPressure = contactPressure;
            ContactCount = contactCount;
            ContactPair = contactPair ?? string.Empty;
            BehaviorState = behaviorState ?? string.Empty;
            BasePotencyMotion = basePotencyMotion;
            BasePotencyCohesion = basePotencyCohesion;
            BasePotencyScatter = basePotencyScatter;
            CurrentPotencyMotion = currentPotencyMotion;
            CurrentPotencyCohesion = currentPotencyCohesion;
            CurrentPotencyScatter = currentPotencyScatter;
            PendingPotencyMotion = pendingPotencyMotion;
            PendingPotencyCohesion = pendingPotencyCohesion;
            PendingPotencyScatter = pendingPotencyScatter;
            PendingPotencyInfluenceKind = pendingPotencyInfluenceKind;
            LatestPotencyFrame = latestPotencyFrame;
            LatestPotencyTargetNumericId = latestPotencyTargetNumericId;
            LatestPotencyInfluenceKind = latestPotencyInfluenceKind;
            LatestPotencyAppliedMotion = latestPotencyAppliedMotion;
            LatestPotencyAppliedCohesion = latestPotencyAppliedCohesion;
            LatestPotencyAppliedScatter = latestPotencyAppliedScatter;
            LatestPotencyAfterMotion = latestPotencyAfterMotion;
            LatestPotencyAfterCohesion = latestPotencyAfterCohesion;
            LatestPotencyAfterScatter = latestPotencyAfterScatter;
            LatestPotencyResultKind = latestPotencyResultKind;
            LatestPotencyResultSource = latestPotencyResultSource ?? string.Empty;
        }

        public int NumericId { get; }
        public string StableId { get; }
        public string Category { get; }
        public int MassGrams { get; }
        public int MaterialKind { get; }
        public int ShapeKind { get; }
        public int HalfExtentXMillimeters { get; }
        public int HalfExtentYMillimeters { get; }
        public int HalfExtentZMillimeters { get; }
        public int XMillimeters { get; }
        public int YMillimeters { get; }
        public int ZMillimeters { get; }
        public int VelocityXMillimetersPerSecond { get; }
        public int VelocityYMillimetersPerSecond { get; }
        public int VelocityZMillimetersPerSecond { get; }
        public int ContactVelocityXMillimetersPerSecond { get; }
        public int ContactVelocityZMillimetersPerSecond { get; }
        public int ContactDisplacementXMillimeters { get; }
        public int ContactDisplacementZMillimeters { get; }
        public int ContactPressure { get; }
        public int ContactCount { get; }
        public string ContactPair { get; }
        public string BehaviorState { get; }
        public int BasePotencyMotion { get; }
        public int BasePotencyCohesion { get; }
        public int BasePotencyScatter { get; }
        public int CurrentPotencyMotion { get; }
        public int CurrentPotencyCohesion { get; }
        public int CurrentPotencyScatter { get; }
        public int PendingPotencyMotion { get; }
        public int PendingPotencyCohesion { get; }
        public int PendingPotencyScatter { get; }
        public int PendingPotencyInfluenceKind { get; }
        public long LatestPotencyFrame { get; }
        public int LatestPotencyTargetNumericId { get; }
        public int LatestPotencyInfluenceKind { get; }
        public int LatestPotencyAppliedMotion { get; }
        public int LatestPotencyAppliedCohesion { get; }
        public int LatestPotencyAppliedScatter { get; }
        public int LatestPotencyAfterMotion { get; }
        public int LatestPotencyAfterCohesion { get; }
        public int LatestPotencyAfterScatter { get; }
        public int LatestPotencyResultKind { get; }
        public string LatestPotencyResultSource { get; }

        public static CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample[] Capture(
            IReadOnlyList<CharacterTestMovableNaturalObjectState> states)
        {
            int count = states != null ? states.Count : 0;
            if (count <= 0)
                return Array.Empty<CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample>();

            var samples = new CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample[count];
            for (int i = 0; i < count; i++)
            {
                CharacterTestMovableNaturalObjectState state = states[i];
                if (state == null)
                    continue;

                CharacterTestGenesisPotencyApplicationRecord latest =
                    state.LatestPotencyApplication;
                samples[i] = new CharacterTestRuntimeAiLabFrameSyncMovableNaturalObjectSample(
                    state.NumericId,
                    state.StableId,
                    state.Category,
                    state.MassGrams,
                    (int)state.Material.Kind,
                    (int)state.ShapeKind,
                    state.HalfExtentXMillimeters,
                    state.HalfExtentYMillimeters,
                    state.HalfExtentZMillimeters,
                    state.XMillimeters,
                    state.YMillimeters,
                    state.ZMillimeters,
                    state.VelocityXMillimetersPerSecond,
                    state.VelocityYMillimetersPerSecond,
                    state.VelocityZMillimetersPerSecond,
                    state.ContactVelocityXMillimetersPerSecond,
                    state.ContactVelocityZMillimetersPerSecond,
                    state.ContactDisplacementXMillimeters,
                    state.ContactDisplacementZMillimeters,
                    state.ContactPressure,
                    state.ContactCount,
                    state.ContactPair,
                    state.BehaviorState,
                    state.BasePotency.Motion,
                    state.BasePotency.Cohesion,
                    state.BasePotency.Scatter,
                    state.CurrentPotency.Motion,
                    state.CurrentPotency.Cohesion,
                    state.CurrentPotency.Scatter,
                    state.PendingPotency.Motion,
                    state.PendingPotency.Cohesion,
                    state.PendingPotency.Scatter,
                    (int)state.PendingPotencyInfluence,
                    latest.Frame.Value,
                    latest.TargetNumericId,
                    (int)latest.InfluenceKind,
                    latest.AppliedDelta.Motion,
                    latest.AppliedDelta.Cohesion,
                    latest.AppliedDelta.Scatter,
                    latest.After.Motion,
                    latest.After.Cohesion,
                    latest.After.Scatter,
                    (int)latest.Result,
                    latest.ResultSource);
            }

            return samples;
        }
    }

    /// <summary>
    /// Read-only overlay input sample for Runtime AI Lab diagnostics.
    /// </summary>
}
