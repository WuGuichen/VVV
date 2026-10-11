using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Runtime;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>
    /// Captures the minimal presentation diagnostics retained by explicit Runtime AI Lab state capture.
    /// </summary>
    /// <remarks>
    /// LowFreqAlloc. This probe is only called after explicit Lab state-capture operations and never from gameplay Tick.
    /// </remarks>
    internal static partial class CharacterTestRuntimeAiLabPresentationDiagnosticProbe
    {
        private const float OverlayEpsilon = 0.001f;

        public static CharacterTestRuntimeAiLabPresentationDiagnosticSample Capture(
            GameSlice slice,
            CharacterTestRuntimeAiPlannerDevPreviewItem preview)
        {
            if (slice == null)
            {
                return new CharacterTestRuntimeAiLabPresentationDiagnosticSample(
                    Array.Empty<CharacterTestRuntimeAiLabPresentationCharacterSample>(),
                    default,
                    CharacterTestRuntimeAiLabFrameSyncOverlaySample.FromPreview(preview));
            }

            IReadOnlyList<CharacterTestCharacterEntity> entities = slice.CharacterEntities;
            int count = entities != null ? entities.Count : 0;
            var characters = new CharacterTestRuntimeAiLabPresentationCharacterSample[count];
            CharacterTestCharacterUnityViewBridge viewBridge = slice.CharacterRuntime?.ViewBridge;
            for (int i = 0; i < count; i++)
                characters[i] = CaptureCharacter(entities[i], viewBridge);

            return new CharacterTestRuntimeAiLabPresentationDiagnosticSample(
                characters,
                CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample.Capture(slice.VfxRuntime),
                CharacterTestRuntimeAiLabFrameSyncOverlaySample.FromPreview(preview));
        }

        internal static long ComputeContentHash(
            CharacterTestRuntimeAiLabPresentationDiagnosticSample sample)
        {
            unchecked
            {
                ulong hash = 1469598103934665603UL;
                if (sample == null)
                {
                    MixPresentationHash(ref hash, 0L);
                    return (long)hash;
                }

                MixPresentationHash(ref hash, 1L);
                IReadOnlyList<CharacterTestRuntimeAiLabPresentationCharacterSample> characters = sample.Characters;
                int characterCount = characters?.Count ?? 0;
                MixPresentationHash(ref hash, characterCount);
                for (int i = 0; i < characterCount; i++)
                    AddCharacterContentHash(ref hash, characters[i]);
                AddVfxContentHash(ref hash, sample.VfxRuntime);
                AddOverlayContentHash(ref hash, sample.Overlay);
                return (long)hash;
            }
        }

        private static void AddCharacterContentHash(
            ref ulong hash,
            CharacterTestRuntimeAiLabPresentationCharacterSample sample)
        {
            MixPresentationHash(ref hash, sample.EntityId);
            MixPresentationHash(ref hash, sample.HasUnityView);
            MixPresentationHash(ref hash, sample.UnityViewXMillimeters);
            MixPresentationHash(ref hash, sample.UnityViewYMillimeters);
            MixPresentationHash(ref hash, sample.UnityViewZMillimeters);
            MixPresentationHash(ref hash, sample.UnityViewYawMilliDegrees);
            MixPresentationHash(ref hash, sample.AnimationBlendXMilli);
            MixPresentationHash(ref hash, sample.AnimationBlendYMilli);
            MixPresentationHash(ref hash, sample.AnimationSpeed01Milli);
            MixPresentationHash(ref hash, sample.AnimationActionKey);
            MixPresentationHash(ref hash, sample.AnimationGrounded);
            MixPresentationHash(ref hash, sample.AnimationAirborne);
            AddAnimationPresentationContentHash(ref hash, sample.PresentationState);
        }

        private static void AddVfxContentHash(
            ref ulong hash,
            CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample sample)
        {
            MixPresentationHash(ref hash, sample.RuntimeAvailable);
            MixPresentationHash(ref hash, sample.ModuleId);
            MixPresentationHash(ref hash, sample.DefinitionHash);
            MixPresentationHash(ref hash, sample.NextInstanceId);
            MixPresentationHash(ref hash, sample.ActiveCount);
            MixPresentationHash(ref hash, sample.PendingSpawnCount);
            MixPresentationHash(ref hash, sample.PendingStopCount);
            MixPresentationHash(ref hash, sample.PendingEventCount);
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample> instances = sample.ActiveInstances;
            int count = instances?.Count ?? 0;
            MixPresentationHash(ref hash, count);
            for (int i = 0; i < count; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample instance = instances[i];
                MixPresentationHash(ref hash, instance.InstanceId);
                MixPresentationHash(ref hash, instance.DefinitionId);
                MixPresentationHash(ref hash, instance.DefinitionHash);
                MixPresentationHash(ref hash, instance.PrefabResource);
                MixPresentationHash(ref hash, instance.StartFrame);
                MixPresentationHash(ref hash, instance.ExpireFrame);
                MixPresentationHash(ref hash, instance.AnchorKind);
                MixPresentationHash(ref hash, instance.AnchorSourceId);
                MixPresentationHash(ref hash, instance.AnchorTargetId);
                MixPresentationHash(ref hash, instance.AnchorSocketId);
                MixPresentationHash(ref hash, instance.AnchorPositionXRaw);
                MixPresentationHash(ref hash, instance.AnchorPositionYRaw);
                MixPresentationHash(ref hash, instance.AnchorPositionZRaw);
                MixPresentationHash(ref hash, instance.AnchorForwardXRaw);
                MixPresentationHash(ref hash, instance.AnchorForwardYRaw);
                MixPresentationHash(ref hash, instance.AnchorForwardZRaw);
                MixPresentationHash(ref hash, instance.Parameter0);
                MixPresentationHash(ref hash, instance.Parameter1);
                MixPresentationHash(ref hash, instance.Parameter2);
                MixPresentationHash(ref hash, instance.Parameter3);
                MixPresentationHash(ref hash, instance.SourceDomain);
                MixPresentationHash(ref hash, instance.SourceFrame);
                MixPresentationHash(ref hash, instance.LocalFrame);
                MixPresentationHash(ref hash, instance.SourceOrder);
                MixPresentationHash(ref hash, instance.TriggerId);
                MixPresentationHash(ref hash, instance.SourceId);
                MixPresentationHash(ref hash, instance.TargetId);
                MixPresentationHash(ref hash, instance.TraceId.KindId);
                MixPresentationHash(ref hash, instance.TraceId.SourceId);
                MixPresentationHash(ref hash, instance.TraceId.ExecutionId);
                MixPresentationHash(ref hash, instance.TraceId.Sequence);
            }
        }

        private static void AddOverlayContentHash(
            ref ulong hash,
            CharacterTestRuntimeAiLabFrameSyncOverlaySample sample)
        {
            MixPresentationHash(ref hash, sample.Available);
            MixPresentationHash(ref hash, sample.Frame);
            MixPresentationHash(ref hash, sample.AgentEntityId);
            MixPresentationHash(ref hash, sample.TargetEntityId);
            MixPresentationHash(ref hash, VectorChecksum(sample.SourceCenter));
            MixPresentationHash(ref hash, VectorChecksum(sample.TargetCenter));
            MixPresentationHash(ref hash, VectorChecksum(sample.MoveEnd));
            MixPresentationHash(ref hash, Quantize(sample.AttackRange));
        }

        private static void MixPresentationHash(ref ulong hash, bool value)
        {
            MixPresentationHash(ref hash, value ? 1L : 0L);
        }

        private static void MixPresentationHash(ref ulong hash, int value)
        {
            MixPresentationHash(ref hash, (long)value);
        }

        private static void MixPresentationHash(ref ulong hash, long value)
        {
            unchecked
            {
                hash ^= (ulong)value;
                hash *= 1099511628211UL;
            }
        }

        private static void MixPresentationHash(ref ulong hash, string value)
        {
            string text = value ?? string.Empty;
            MixPresentationHash(ref hash, text.Length);
            for (int i = 0; i < text.Length; i++)
                MixPresentationHash(ref hash, text[i]);
        }

        public static CharacterTestRuntimeAiLabFrameSyncReport Compare(
            CharacterTestRuntimeAiLabPresentationDiagnosticSample expected,
            CharacterTestRuntimeAiLabPresentationDiagnosticSample actual)
        {
            var rows = new List<CharacterTestRuntimeAiLabFrameSyncMismatch>(16);
            CompareCharacters(expected?.Characters, actual?.Characters, rows);
            CompareVfxRuntime(
                expected != null ? expected.VfxRuntime : default,
                actual != null ? actual.VfxRuntime : default,
                rows);
            CompareOverlay(
                expected != null ? expected.Overlay : default,
                actual != null ? actual.Overlay : default,
                rows);
            return new CharacterTestRuntimeAiLabFrameSyncReport(rows);
        }

        private static CharacterTestRuntimeAiLabPresentationCharacterSample CaptureCharacter(
            CharacterTestCharacterEntity entity,
            CharacterTestCharacterUnityViewBridge viewBridge)
        {
            Vector3 viewPosition = Vector3.zero;
            Quaternion viewRotation = Quaternion.identity;
            bool hasUnityView = entity != null
                && viewBridge != null
                && viewBridge.TryGetViewPose(entity.Id, out viewPosition, out viewRotation);
            CharacterTestRuntimeCharacterPresentationState presentationState = null;
            if (entity != null)
                viewBridge?.TryCaptureCharacterPresentationState(entity.Id, out presentationState);

            return new CharacterTestRuntimeAiLabPresentationCharacterSample(
                entity != null ? entity.Id.Value : 0,
                hasUnityView,
                hasUnityView ? CharacterTestDeterministicUnits.MetersToMillimeters(viewPosition.x) : 0,
                hasUnityView ? CharacterTestDeterministicUnits.MetersToMillimeters(viewPosition.y) : 0,
                hasUnityView ? CharacterTestDeterministicUnits.MetersToMillimeters(viewPosition.z) : 0,
                hasUnityView ? CharacterTestDeterministicUnits.DegreesToMilliDegrees(viewRotation.eulerAngles.y) : 0,
                presentationState,
                entity != null ? CharacterTestDeterministicUnits.AxisToMilli(entity.Animation.LocomotionBlend.x) : 0,
                entity != null ? CharacterTestDeterministicUnits.AxisToMilli(entity.Animation.LocomotionBlend.y) : 0,
                entity != null ? CharacterTestDeterministicUnits.ScaleToMilli(entity.Animation.LocomotionSpeed01) : 0,
                entity?.Animation.ActionKey,
                entity != null && entity.Animation.Grounded,
                entity != null && entity.Animation.Airborne);
        }

        private static void CompareCharacters(
            IReadOnlyList<CharacterTestRuntimeAiLabPresentationCharacterSample> expected,
            IReadOnlyList<CharacterTestRuntimeAiLabPresentationCharacterSample> actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            int expectedCount = expected != null ? expected.Count : 0;
            int actualCount = actual != null ? actual.Count : 0;
            AddMismatch(rows, "view", 0, "count", expectedCount, actualCount);
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabPresentationCharacterSample left = expected[i];
                if (!TryFindCharacter(actual, left.EntityId, out CharacterTestRuntimeAiLabPresentationCharacterSample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "view",
                        left.EntityId,
                        "missing",
                        "present",
                        "missing"));
                    continue;
                }

                AddMismatch(rows, "view", left.EntityId, "available", left.HasUnityView, right.HasUnityView);
                AddMismatch(rows, "view", left.EntityId, "x", left.UnityViewXMillimeters, right.UnityViewXMillimeters);
                AddMismatch(rows, "view", left.EntityId, "y", left.UnityViewYMillimeters, right.UnityViewYMillimeters);
                AddMismatch(rows, "view", left.EntityId, "z", left.UnityViewZMillimeters, right.UnityViewZMillimeters);
                AddMismatch(rows, "view", left.EntityId, "yaw", left.UnityViewYawMilliDegrees, right.UnityViewYawMilliDegrees);
                AddMismatch(rows, "animationInput", left.EntityId, "blendX", left.AnimationBlendXMilli, right.AnimationBlendXMilli);
                AddMismatch(rows, "animationInput", left.EntityId, "blendY", left.AnimationBlendYMilli, right.AnimationBlendYMilli);
                AddMismatch(rows, "animationInput", left.EntityId, "speed", left.AnimationSpeed01Milli, right.AnimationSpeed01Milli);
                AddMismatch(rows, "animationInput", left.EntityId, "actionKey", left.AnimationActionKey, right.AnimationActionKey);
                AddMismatch(rows, "animationInput", left.EntityId, "grounded", left.AnimationGrounded, right.AnimationGrounded);
                AddMismatch(rows, "animationInput", left.EntityId, "airborne", left.AnimationAirborne, right.AnimationAirborne);
                CompareAnimationPresentation(left.EntityId, left.PresentationState, right.PresentationState, rows);
            }
        }

        private static bool TryFindCharacter(
            IReadOnlyList<CharacterTestRuntimeAiLabPresentationCharacterSample> samples,
            int entityId,
            out CharacterTestRuntimeAiLabPresentationCharacterSample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.EntityId == entityId)
                    return true;
            }

            sample = default;
            return false;
        }

        private static void CompareVfxRuntime(
            CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample expected,
            CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "vfx", 0, "runtimeAvailable", expected.RuntimeAvailable, actual.RuntimeAvailable);
            AddMismatch(rows, "vfx", 0, "moduleId", expected.ModuleId, actual.ModuleId);
            AddMismatch(rows, "vfx", 0, "definitionHash", expected.DefinitionHash, actual.DefinitionHash);
            AddMismatch(rows, "vfx", 0, "nextInstanceId", expected.NextInstanceId, actual.NextInstanceId);
            AddMismatch(rows, "vfx", 0, "activeCount", expected.ActiveCount, actual.ActiveCount);
            AddMismatch(rows, "vfx", 0, "pendingSpawnCount", expected.PendingSpawnCount, actual.PendingSpawnCount);
            AddMismatch(rows, "vfx", 0, "pendingStopCount", expected.PendingStopCount, actual.PendingStopCount);
            AddMismatch(rows, "vfx", 0, "pendingEventCount", expected.PendingEventCount, actual.PendingEventCount);

            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample> expectedInstances = expected.ActiveInstances;
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample> actualInstances = actual.ActiveInstances;
            int expectedCount = expectedInstances != null ? expectedInstances.Count : 0;
            int actualCount = actualInstances != null ? actualInstances.Count : 0;
            AddMismatch(rows, "vfx", 0, "instance.count", expectedCount, actualCount);
            for (int i = 0; i < expectedCount; i++)
            {
                CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample left = expectedInstances[i];
                if (!TryFindVfxInstance(actualInstances, left.InstanceId, out CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample right))
                {
                    rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                        "vfx",
                        ToProbeEntityId(left.InstanceId),
                        "instance.missing",
                        left.InstanceId.ToString(CultureInfo.InvariantCulture),
                        "missing"));
                    continue;
                }

                int probeId = ToProbeEntityId(left.InstanceId);
                AddMismatch(rows, "vfx", probeId, "instanceId", left.InstanceId, right.InstanceId);
                AddMismatch(rows, "vfx", probeId, "definitionId", left.DefinitionId, right.DefinitionId);
                AddMismatch(rows, "vfx", probeId, "definitionHash", left.DefinitionHash, right.DefinitionHash);
                AddMismatch(rows, "vfx", probeId, "prefabResource", left.PrefabResource, right.PrefabResource);
                AddMismatch(rows, "vfx", probeId, "startFrame", left.StartFrame, right.StartFrame);
                AddMismatch(rows, "vfx", probeId, "expireFrame", left.ExpireFrame, right.ExpireFrame);
                AddMismatch(rows, "vfx", probeId, "anchorKind", left.AnchorKind, right.AnchorKind);
                AddMismatch(rows, "vfx", probeId, "anchorSourceId", left.AnchorSourceId, right.AnchorSourceId);
                AddMismatch(rows, "vfx", probeId, "anchorTargetId", left.AnchorTargetId, right.AnchorTargetId);
                AddMismatch(rows, "vfx", probeId, "anchorSocketId", left.AnchorSocketId, right.AnchorSocketId);
                AddMismatch(rows, "vfx", probeId, "anchorPositionXRaw", left.AnchorPositionXRaw, right.AnchorPositionXRaw);
                AddMismatch(rows, "vfx", probeId, "anchorPositionYRaw", left.AnchorPositionYRaw, right.AnchorPositionYRaw);
                AddMismatch(rows, "vfx", probeId, "anchorPositionZRaw", left.AnchorPositionZRaw, right.AnchorPositionZRaw);
                AddMismatch(rows, "vfx", probeId, "anchorForwardXRaw", left.AnchorForwardXRaw, right.AnchorForwardXRaw);
                AddMismatch(rows, "vfx", probeId, "anchorForwardYRaw", left.AnchorForwardYRaw, right.AnchorForwardYRaw);
                AddMismatch(rows, "vfx", probeId, "anchorForwardZRaw", left.AnchorForwardZRaw, right.AnchorForwardZRaw);
                AddMismatch(rows, "vfx", probeId, "parameter0", left.Parameter0, right.Parameter0);
                AddMismatch(rows, "vfx", probeId, "parameter1", left.Parameter1, right.Parameter1);
                AddMismatch(rows, "vfx", probeId, "parameter2", left.Parameter2, right.Parameter2);
                AddMismatch(rows, "vfx", probeId, "parameter3", left.Parameter3, right.Parameter3);
                AddMismatch(rows, "vfx", probeId, "sourceDomain", left.SourceDomain, right.SourceDomain);
                AddMismatch(rows, "vfx", probeId, "sourceFrame", left.SourceFrame, right.SourceFrame);
                AddMismatch(rows, "vfx", probeId, "localFrame", left.LocalFrame, right.LocalFrame);
                AddMismatch(rows, "vfx", probeId, "sourceOrder", left.SourceOrder, right.SourceOrder);
                AddMismatch(rows, "vfx", probeId, "triggerId", left.TriggerId, right.TriggerId);
                AddMismatch(rows, "vfx", probeId, "sourceId", left.SourceId, right.SourceId);
                AddMismatch(rows, "vfx", probeId, "targetId", left.TargetId, right.TargetId);
                AddMismatch(rows, "vfx", probeId, "traceId", left.TraceId, right.TraceId);
            }
        }

        private static bool TryFindVfxInstance(
            IReadOnlyList<CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample> samples,
            long instanceId,
            out CharacterTestRuntimeAiLabFrameSyncVfxInstanceSample sample)
        {
            int count = samples != null ? samples.Count : 0;
            for (int i = 0; i < count; i++)
            {
                sample = samples[i];
                if (sample.InstanceId == instanceId)
                    return true;
            }

            sample = default;
            return false;
        }

        private static int ToProbeEntityId(long id)
        {
            if (id > int.MaxValue)
                return int.MaxValue;
            if (id < int.MinValue)
                return int.MinValue;
            return (int)id;
        }

        private static void CompareOverlay(
            CharacterTestRuntimeAiLabFrameSyncOverlaySample expected,
            CharacterTestRuntimeAiLabFrameSyncOverlaySample actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "overlay", expected.AgentEntityId, "available", expected.Available, actual.Available);
            AddMismatch(rows, "overlay", expected.AgentEntityId, "frame", expected.Frame, actual.Frame);
            AddMismatch(rows, "overlay", expected.AgentEntityId, "agentEntityId", expected.AgentEntityId, actual.AgentEntityId);
            AddMismatch(rows, "overlay", expected.AgentEntityId, "targetEntityId", expected.TargetEntityId, actual.TargetEntityId);
            AddVectorMismatch(rows, "overlay", expected.AgentEntityId, "sourceCenter", expected.SourceCenter, actual.SourceCenter);
            AddVectorMismatch(rows, "overlay", expected.AgentEntityId, "targetCenter", expected.TargetCenter, actual.TargetCenter);
            AddVectorMismatch(rows, "overlay", expected.AgentEntityId, "moveEnd", expected.MoveEnd, actual.MoveEnd);
            AddFloatMismatch(rows, "overlay", expected.AgentEntityId, "attackRange", expected.AttackRange, actual.AttackRange);
        }

        private static void AddVectorMismatch(
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            string layer,
            int entityId,
            string field,
            Vector3 expected,
            Vector3 actual)
        {
            AddFloatMismatch(rows, layer, entityId, field + ".x", expected.x, actual.x);
            AddFloatMismatch(rows, layer, entityId, field + ".y", expected.y, actual.y);
            AddFloatMismatch(rows, layer, entityId, field + ".z", expected.z, actual.z);
        }

        private static void AddFloatMismatch(
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            string layer,
            int entityId,
            string field,
            float expected,
            float actual)
        {
            if (Math.Abs(expected - actual) <= OverlayEpsilon)
                return;

            rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                layer,
                entityId,
                field,
                expected.ToString("0.###", CultureInfo.InvariantCulture),
                actual.ToString("0.###", CultureInfo.InvariantCulture)));
        }

        private static void AddMismatch(
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            string layer,
            int entityId,
            string field,
            int expected,
            int actual)
        {
            if (expected == actual)
                return;
            rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                layer,
                entityId,
                field,
                expected.ToString(CultureInfo.InvariantCulture),
                actual.ToString(CultureInfo.InvariantCulture)));
        }

        private static void AddMismatch(
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            string layer,
            int entityId,
            string field,
            long expected,
            long actual)
        {
            if (expected == actual)
                return;
            rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                layer,
                entityId,
                field,
                expected.ToString(CultureInfo.InvariantCulture),
                actual.ToString(CultureInfo.InvariantCulture)));
        }

        private static void AddMismatch(
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            string layer,
            int entityId,
            string field,
            bool expected,
            bool actual)
        {
            if (expected == actual)
                return;
            rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                layer,
                entityId,
                field,
                expected ? "true" : "false",
                actual ? "true" : "false"));
        }

        private static void AddMismatch(
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            string layer,
            int entityId,
            string field,
            string expected,
            string actual)
        {
            if (string.Equals(expected ?? string.Empty, actual ?? string.Empty, StringComparison.Ordinal))
                return;
            rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                layer,
                entityId,
                field,
                expected ?? string.Empty,
                actual ?? string.Empty));
        }

        private static void AddMismatch(
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows,
            string layer,
            int entityId,
            string field,
            RuntimeTraceIdentity expected,
            RuntimeTraceIdentity actual)
        {
            if (expected.Equals(actual))
                return;
            rows.Add(new CharacterTestRuntimeAiLabFrameSyncMismatch(
                layer,
                entityId,
                field,
                expected.ToString(),
                actual.ToString()));
        }
    }

    internal sealed class CharacterTestRuntimeAiLabPresentationDiagnosticSample
    {
        public CharacterTestRuntimeAiLabPresentationDiagnosticSample(
            CharacterTestRuntimeAiLabPresentationCharacterSample[] characters,
            CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample vfxRuntime,
            CharacterTestRuntimeAiLabFrameSyncOverlaySample overlay)
        {
            Characters = characters ?? Array.Empty<CharacterTestRuntimeAiLabPresentationCharacterSample>();
            VfxRuntime = vfxRuntime;
            Overlay = overlay;
        }

        public IReadOnlyList<CharacterTestRuntimeAiLabPresentationCharacterSample> Characters { get; }
        public CharacterTestRuntimeAiLabFrameSyncVfxRuntimeSample VfxRuntime { get; }
        public CharacterTestRuntimeAiLabFrameSyncOverlaySample Overlay { get; }
    }

    internal readonly struct CharacterTestRuntimeAiLabPresentationCharacterSample
    {
        public CharacterTestRuntimeAiLabPresentationCharacterSample(
            int entityId,
            bool hasUnityView,
            int unityViewXMillimeters,
            int unityViewYMillimeters,
            int unityViewZMillimeters,
            int unityViewYawMilliDegrees,
            CharacterTestRuntimeCharacterPresentationState presentationState,
            int animationBlendXMilli,
            int animationBlendYMilli,
            int animationSpeed01Milli,
            string animationActionKey,
            bool animationGrounded,
            bool animationAirborne)
        {
            EntityId = entityId;
            HasUnityView = hasUnityView;
            UnityViewXMillimeters = unityViewXMillimeters;
            UnityViewYMillimeters = unityViewYMillimeters;
            UnityViewZMillimeters = unityViewZMillimeters;
            UnityViewYawMilliDegrees = unityViewYawMilliDegrees;
            PresentationState = presentationState;
            AnimationBlendXMilli = animationBlendXMilli;
            AnimationBlendYMilli = animationBlendYMilli;
            AnimationSpeed01Milli = animationSpeed01Milli;
            AnimationActionKey = animationActionKey ?? string.Empty;
            AnimationGrounded = animationGrounded;
            AnimationAirborne = animationAirborne;
        }

        public int EntityId { get; }
        public bool HasUnityView { get; }
        public int UnityViewXMillimeters { get; }
        public int UnityViewYMillimeters { get; }
        public int UnityViewZMillimeters { get; }
        public int UnityViewYawMilliDegrees { get; }
        public CharacterTestRuntimeCharacterPresentationState PresentationState { get; }
        public int AnimationBlendXMilli { get; }
        public int AnimationBlendYMilli { get; }
        public int AnimationSpeed01Milli { get; }
        public string AnimationActionKey { get; }
        public bool AnimationGrounded { get; }
        public bool AnimationAirborne { get; }
    }
}
