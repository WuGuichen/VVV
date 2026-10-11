using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Animation;
using MxFramework.CharacterRuntimeSpawn.Unity;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal static partial class CharacterTestRuntimeAiLabPresentationDiagnosticProbe
    {
        private static void AddAnimationPresentationContentHash(
            ref ulong hash,
            CharacterTestRuntimeCharacterPresentationState state)
        {
            MixPresentationHash(ref hash, state != null);
            if (state == null)
                return;

            MixPresentationHash(ref hash, state.EntityId);
            AddAnimationControllerContentHash(ref hash, state.Animation);
            AddBonePoseContentHash(ref hash, state.BonePose);
            AddProceduralPoseContentHash(ref hash, state.Procedural);
        }

        private static void AddAnimationControllerContentHash(
            ref ulong hash,
            CharacterRuntimeAnimationPlaybackState state)
        {
            MixPresentationHash(ref hash, state != null);
            if (state == null)
                return;

            MixPresentationHash(ref hash, state.LastActionKey);
            MixPresentationHash(ref hash, Quantize(state.FallbackPhase));
            MixPresentationHash(ref hash, Quantize(state.Blend.x));
            MixPresentationHash(ref hash, Quantize(state.Blend.y));
            MixPresentationHash(ref hash, Quantize(state.Speed01));
            MixPresentationHash(ref hash, state.UsingFallback);
            MixPresentationHash(ref hash, Quantize(state.RuntimeAnimationClock));
            MixPresentationHash(ref hash, Quantize(state.SuppressLocomotionBlendUntilTime));
            MixPresentationHash(ref hash, Quantize(state.PlaybackSpeedCompensation));
            AddAnimationBackendContentHash(ref hash, state.BackendState);
        }

        private static void AddAnimationBackendContentHash(
            ref ulong hash,
            MxAnimationPlaybackState state)
        {
            MixPresentationHash(ref hash, state != null);
            if (state == null)
                return;

            MixPresentationHash(ref hash, state.BackendName);
            MixPresentationHash(ref hash, state.AnimationSetId);
            int layerCount = state.Layers?.Count ?? 0;
            MixPresentationHash(ref hash, layerCount);
            for (int i = 0; i < layerCount; i++)
            {
                MxAnimationLayerPlaybackState layer = state.Layers[i];
                MixPresentationHash(ref hash, layer.LayerId.ToString());
                MixPresentationHash(ref hash, layer.Status.ToString());
                MixPresentationHash(ref hash, Quantize(layer.Weight));
                MixPresentationHash(ref hash, Quantize(layer.TargetWeight));
                MixPresentationHash(ref hash, Quantize(FadeProgress(
                    layer.BlendFadeElapsedSeconds,
                    layer.BlendFadeDurationSeconds)));
                MixPresentationHash(ref hash, layer.ActiveBlendId);
                AddBlend1DContentHash(ref hash, layer.Blend1DWeights);
                AddBlend2DContentHash(ref hash, layer.Blend2DWeights);
                AddAnimationSlotContentHash(ref hash, layer.Slots);
            }
        }

        private static void AddBlend1DContentHash(
            ref ulong hash,
            IReadOnlyList<MxAnimationBlend1DWeight> weights)
        {
            int count = weights?.Count ?? 0;
            MixPresentationHash(ref hash, count);
            for (int i = 0; i < count; i++)
            {
                MxAnimationBlend1DWeight weight = weights[i];
                MixPresentationHash(ref hash, weight.ClipKey.ToString());
                MixPresentationHash(ref hash, Quantize(weight.Threshold));
                MixPresentationHash(ref hash, Quantize(weight.Weight));
                MixPresentationHash(ref hash, Quantize(weight.PlaybackSpeed));
            }
        }

        private static void AddBlend2DContentHash(
            ref ulong hash,
            IReadOnlyList<MxAnimationBlend2DWeight> weights)
        {
            int count = weights?.Count ?? 0;
            MixPresentationHash(ref hash, count);
            for (int i = 0; i < count; i++)
            {
                MxAnimationBlend2DWeight weight = weights[i];
                MixPresentationHash(ref hash, weight.ClipKey.ToString());
                MixPresentationHash(ref hash, Quantize(weight.X));
                MixPresentationHash(ref hash, Quantize(weight.Y));
                MixPresentationHash(ref hash, Quantize(weight.Weight));
                MixPresentationHash(ref hash, Quantize(weight.PlaybackSpeed));
            }
        }

        private static void AddAnimationSlotContentHash(
            ref ulong hash,
            IReadOnlyList<MxAnimationPlaybackSlotState> slots)
        {
            int count = slots?.Count ?? 0;
            MixPresentationHash(ref hash, count);
            for (int i = 0; i < count; i++)
            {
                MxAnimationPlaybackSlotState slot = slots[i];
                MixPresentationHash(ref hash, slot.Role.ToString());
                MixPresentationHash(ref hash, slot.ClipKey.ToString());
                MixPresentationHash(ref hash, Quantize(slot.TimeSeconds));
                MixPresentationHash(ref hash, Quantize(slot.PlaybackSpeed));
                MixPresentationHash(ref hash, Quantize(slot.Weight));
                MixPresentationHash(ref hash, Quantize(slot.TargetWeight));
            }
        }

        private static void AddBonePoseContentHash(
            ref ulong hash,
            CharacterTestBonePoseState state)
        {
            IReadOnlyList<CharacterTestBonePoseEntry> entries = state?.Entries;
            int count = entries?.Count ?? 0;
            MixPresentationHash(ref hash, count);
            for (int i = 0; i < count; i++)
            {
                CharacterTestBonePoseEntry entry = entries[i];
                MixPresentationHash(ref hash, entry.Path);
                MixPresentationHash(ref hash, VectorChecksum(entry.LocalPosition));
                MixPresentationHash(ref hash, QuaternionChecksum(entry.LocalRotation));
                MixPresentationHash(ref hash, VectorChecksum(entry.LocalScale));
            }
        }

        private static void AddProceduralPoseContentHash(
            ref ulong hash,
            CharacterProceduralBonePlaybackState state)
        {
            MixPresentationHash(ref hash, state != null);
            if (state == null)
                return;

            MixPresentationHash(ref hash, Quantize(state.IkSolveSecondsRemaining));
            int chainCount = state.SecondaryChains?.Count ?? 0;
            MixPresentationHash(ref hash, chainCount);
            for (int i = 0; i < chainCount; i++)
            {
                CharacterSecondaryBoneChainPlaybackState chain = state.SecondaryChains[i];
                MixPresentationHash(ref hash, VectorChecksum(chain.OffsetDegrees));
                MixPresentationHash(ref hash, VectorChecksum(chain.AngularVelocityDegrees));
                MixPresentationHash(ref hash, Quantize(chain.RemainingImpulseSeconds));
            }
            AddRagdollContentHash(ref hash, state.Ragdoll);
        }

        private static void AddRagdollContentHash(
            ref ulong hash,
            CharacterViewOnlyRagdollPlaybackState state)
        {
            MixPresentationHash(ref hash, state != null);
            if (state == null)
                return;

            MixPresentationHash(ref hash, state.Mode.ToString());
            MixPresentationHash(ref hash, Quantize(state.DynamicSecondsRemaining));
            int bodyCount = state.Bodies?.Count ?? 0;
            MixPresentationHash(ref hash, bodyCount);
            for (int i = 0; i < bodyCount; i++)
            {
                CharacterViewOnlyRagdollBodyPlaybackState body = state.Bodies[i];
                MixPresentationHash(ref hash, VectorChecksum(body.Position));
                MixPresentationHash(ref hash, QuaternionChecksum(body.Rotation));
                MixPresentationHash(ref hash, VectorChecksum(body.LinearVelocity));
                MixPresentationHash(ref hash, VectorChecksum(body.AngularVelocity));
                MixPresentationHash(ref hash, body.IsKinematic);
            }
        }

        private static void CompareAnimationPresentation(
            int entityId,
            CharacterTestRuntimeCharacterPresentationState expected,
            CharacterTestRuntimeCharacterPresentationState actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "animation", entityId, "available", expected != null, actual != null);
            if (expected == null || actual == null)
                return;

            CompareAnimationController(entityId, expected.Animation, actual.Animation, rows);
            CompareBonePose(entityId, expected.BonePose, actual.BonePose, rows);
            CompareProceduralPose(entityId, expected.Procedural, actual.Procedural, rows);
        }

        private static void CompareAnimationController(
            int entityId,
            CharacterRuntimeAnimationPlaybackState expected,
            CharacterRuntimeAnimationPlaybackState actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "animation", entityId, "controllerAvailable", expected != null, actual != null);
            if (expected == null || actual == null)
                return;

            AddMismatch(rows, "animation", entityId, "lastActionKey", expected.LastActionKey, actual.LastActionKey);
            AddMismatch(rows, "animation", entityId, "fallbackPhase", Quantize(expected.FallbackPhase), Quantize(actual.FallbackPhase));
            AddMismatch(rows, "animation", entityId, "locomotionBlendX", Quantize(expected.Blend.x), Quantize(actual.Blend.x));
            AddMismatch(rows, "animation", entityId, "locomotionBlendY", Quantize(expected.Blend.y), Quantize(actual.Blend.y));
            AddMismatch(rows, "animation", entityId, "locomotionSpeed", Quantize(expected.Speed01), Quantize(actual.Speed01));
            AddMismatch(rows, "animation", entityId, "usingFallback", expected.UsingFallback, actual.UsingFallback);
            AddMismatch(rows, "animation", entityId, "runtimeClock", Quantize(expected.RuntimeAnimationClock), Quantize(actual.RuntimeAnimationClock));
            AddMismatch(rows, "animation", entityId, "suppressionDeadline", Quantize(expected.SuppressLocomotionBlendUntilTime), Quantize(actual.SuppressLocomotionBlendUntilTime));
            AddMismatch(rows, "animation", entityId, "playbackSpeedCompensation", Quantize(expected.PlaybackSpeedCompensation), Quantize(actual.PlaybackSpeedCompensation));
            CompareAnimationBackend(entityId, expected.BackendState, actual.BackendState, rows);
        }

        private static void CompareAnimationBackend(
            int entityId,
            MxAnimationPlaybackState expected,
            MxAnimationPlaybackState actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "animation", entityId, "backendAvailable", expected != null, actual != null);
            if (expected == null || actual == null)
                return;

            AddMismatch(rows, "animation", entityId, "backendName", expected.BackendName, actual.BackendName);
            AddMismatch(rows, "animation", entityId, "animationSetId", expected.AnimationSetId, actual.AnimationSetId);
            int expectedLayerCount = expected.Layers != null ? expected.Layers.Count : 0;
            int actualLayerCount = actual.Layers != null ? actual.Layers.Count : 0;
            AddMismatch(rows, "animation", entityId, "layerCount", expectedLayerCount, actualLayerCount);
            int layerCount = Math.Min(expectedLayerCount, actualLayerCount);
            for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
            {
                MxAnimationLayerPlaybackState left = expected.Layers[layerIndex];
                MxAnimationLayerPlaybackState right = actual.Layers[layerIndex];
                string prefix = "layer[" + layerIndex.ToString(CultureInfo.InvariantCulture) + "].";
                AddMismatch(rows, "animation", entityId, prefix + "id", left.LayerId.ToString(), right.LayerId.ToString());
                AddMismatch(rows, "animation", entityId, prefix + "status", left.Status.ToString(), right.Status.ToString());
                AddMismatch(rows, "animation", entityId, prefix + "weight", Quantize(left.Weight), Quantize(right.Weight));
                AddMismatch(rows, "animation", entityId, prefix + "targetWeight", Quantize(left.TargetWeight), Quantize(right.TargetWeight));
                AddMismatch(rows, "animation", entityId, prefix + "crossFadeWeight", Quantize(FadeProgress(left.BlendFadeElapsedSeconds, left.BlendFadeDurationSeconds)), Quantize(FadeProgress(right.BlendFadeElapsedSeconds, right.BlendFadeDurationSeconds)));
                AddMismatch(rows, "animation", entityId, prefix + "blendId", left.ActiveBlendId, right.ActiveBlendId);
                CompareBlend1DWeights(entityId, prefix, left.Blend1DWeights, right.Blend1DWeights, rows);
                CompareBlend2DWeights(entityId, prefix, left.Blend2DWeights, right.Blend2DWeights, rows);
                CompareAnimationSlots(entityId, prefix, left.Slots, right.Slots, rows);
            }
        }

        private static void CompareBlend1DWeights(
            int entityId,
            string prefix,
            IReadOnlyList<MxAnimationBlend1DWeight> expected,
            IReadOnlyList<MxAnimationBlend1DWeight> actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            int expectedCount = expected != null ? expected.Count : 0;
            int actualCount = actual != null ? actual.Count : 0;
            AddMismatch(rows, "animation", entityId, prefix + "blend1DCount", expectedCount, actualCount);
            int count = Math.Min(expectedCount, actualCount);
            for (int i = 0; i < count; i++)
            {
                MxAnimationBlend1DWeight left = expected[i];
                MxAnimationBlend1DWeight right = actual[i];
                string weightPrefix = prefix + "blend1D[" + i.ToString(CultureInfo.InvariantCulture) + "].";
                AddMismatch(rows, "animation", entityId, weightPrefix + "clip", left.ClipKey.ToString(), right.ClipKey.ToString());
                AddMismatch(rows, "animation", entityId, weightPrefix + "threshold", left.Threshold, right.Threshold);
                AddMismatch(rows, "animation", entityId, weightPrefix + "weight", Quantize(left.Weight), Quantize(right.Weight));
                AddMismatch(rows, "animation", entityId, weightPrefix + "speed", Quantize(left.PlaybackSpeed), Quantize(right.PlaybackSpeed));
            }
        }

        private static void CompareBlend2DWeights(
            int entityId,
            string prefix,
            IReadOnlyList<MxAnimationBlend2DWeight> expected,
            IReadOnlyList<MxAnimationBlend2DWeight> actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            int expectedCount = expected != null ? expected.Count : 0;
            int actualCount = actual != null ? actual.Count : 0;
            AddMismatch(rows, "animation", entityId, prefix + "blend2DCount", expectedCount, actualCount);
            int count = Math.Min(expectedCount, actualCount);
            for (int i = 0; i < count; i++)
            {
                MxAnimationBlend2DWeight left = expected[i];
                MxAnimationBlend2DWeight right = actual[i];
                string weightPrefix = prefix + "blend2D[" + i.ToString(CultureInfo.InvariantCulture) + "].";
                AddMismatch(rows, "animation", entityId, weightPrefix + "clip", left.ClipKey.ToString(), right.ClipKey.ToString());
                AddMismatch(rows, "animation", entityId, weightPrefix + "x", left.X, right.X);
                AddMismatch(rows, "animation", entityId, weightPrefix + "y", left.Y, right.Y);
                AddMismatch(rows, "animation", entityId, weightPrefix + "weight", Quantize(left.Weight), Quantize(right.Weight));
                AddMismatch(rows, "animation", entityId, weightPrefix + "speed", Quantize(left.PlaybackSpeed), Quantize(right.PlaybackSpeed));
            }
        }

        private static void CompareAnimationSlots(
            int entityId,
            string prefix,
            IReadOnlyList<MxAnimationPlaybackSlotState> expected,
            IReadOnlyList<MxAnimationPlaybackSlotState> actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            int expectedCount = expected != null ? expected.Count : 0;
            int actualCount = actual != null ? actual.Count : 0;
            AddMismatch(rows, "animation", entityId, prefix + "slotCount", expectedCount, actualCount);
            int count = Math.Min(expectedCount, actualCount);
            for (int i = 0; i < count; i++)
            {
                MxAnimationPlaybackSlotState left = expected[i];
                MxAnimationPlaybackSlotState right = actual[i];
                string slotPrefix = prefix + "slot[" + i.ToString(CultureInfo.InvariantCulture) + "].";
                AddMismatch(rows, "animation", entityId, slotPrefix + "role", left.Role.ToString(), right.Role.ToString());
                AddMismatch(rows, "animation", entityId, slotPrefix + "clip", left.ClipKey.ToString(), right.ClipKey.ToString());
                AddMismatch(rows, "animation", entityId, slotPrefix + "clipTime", Quantize(left.TimeSeconds), Quantize(right.TimeSeconds));
                AddMismatch(rows, "animation", entityId, slotPrefix + "speed", Quantize(left.PlaybackSpeed), Quantize(right.PlaybackSpeed));
                AddMismatch(rows, "animation", entityId, slotPrefix + "weight", Quantize(left.Weight), Quantize(right.Weight));
                AddMismatch(rows, "animation", entityId, slotPrefix + "targetWeight", Quantize(left.TargetWeight), Quantize(right.TargetWeight));
            }
        }

        private static void CompareBonePose(
            int entityId,
            CharacterTestBonePoseState expected,
            CharacterTestBonePoseState actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            IReadOnlyList<CharacterTestBonePoseEntry> leftEntries = expected != null
                ? expected.Entries
                : Array.Empty<CharacterTestBonePoseEntry>();
            IReadOnlyList<CharacterTestBonePoseEntry> rightEntries = actual != null
                ? actual.Entries
                : Array.Empty<CharacterTestBonePoseEntry>();
            AddMismatch(rows, "bonePose", entityId, "count", leftEntries.Count, rightEntries.Count);
            int count = Math.Min(leftEntries.Count, rightEntries.Count);
            for (int i = 0; i < count; i++)
            {
                CharacterTestBonePoseEntry left = leftEntries[i];
                CharacterTestBonePoseEntry right = rightEntries[i];
                string field = string.IsNullOrEmpty(left.Path) ? "root" : left.Path;
                AddMismatch(rows, "bonePose", entityId, field + ".path", left.Path, right.Path);
                AddMismatch(rows, "bonePose", entityId, field + ".position", VectorChecksum(left.LocalPosition), VectorChecksum(right.LocalPosition));
                AddMismatch(rows, "bonePose", entityId, field + ".rotation", QuaternionChecksum(left.LocalRotation), QuaternionChecksum(right.LocalRotation));
                AddMismatch(rows, "bonePose", entityId, field + ".scale", VectorChecksum(left.LocalScale), VectorChecksum(right.LocalScale));
            }
        }

        private static void CompareProceduralPose(
            int entityId,
            CharacterProceduralBonePlaybackState expected,
            CharacterProceduralBonePlaybackState actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "procedural", entityId, "available", expected != null, actual != null);
            if (expected == null || actual == null)
                return;

            AddMismatch(rows, "procedural", entityId, "ik.remaining", Quantize(expected.IkSolveSecondsRemaining), Quantize(actual.IkSolveSecondsRemaining));
            int expectedChainCount = expected.SecondaryChains != null ? expected.SecondaryChains.Count : 0;
            int actualChainCount = actual.SecondaryChains != null ? actual.SecondaryChains.Count : 0;
            AddMismatch(rows, "procedural", entityId, "secondary.count", expectedChainCount, actualChainCount);
            int chainCount = Math.Min(expectedChainCount, actualChainCount);
            for (int i = 0; i < chainCount; i++)
            {
                CharacterSecondaryBoneChainPlaybackState left = expected.SecondaryChains[i];
                CharacterSecondaryBoneChainPlaybackState right = actual.SecondaryChains[i];
                string prefix = "secondary[" + i.ToString(CultureInfo.InvariantCulture) + "].";
                AddMismatch(rows, "procedural", entityId, prefix + "offset", VectorChecksum(left.OffsetDegrees), VectorChecksum(right.OffsetDegrees));
                AddMismatch(rows, "procedural", entityId, prefix + "velocity", VectorChecksum(left.AngularVelocityDegrees), VectorChecksum(right.AngularVelocityDegrees));
                AddMismatch(rows, "procedural", entityId, prefix + "remaining", Quantize(left.RemainingImpulseSeconds), Quantize(right.RemainingImpulseSeconds));
            }

            CompareRagdoll(entityId, expected.Ragdoll, actual.Ragdoll, rows);
        }

        private static void CompareRagdoll(
            int entityId,
            CharacterViewOnlyRagdollPlaybackState expected,
            CharacterViewOnlyRagdollPlaybackState actual,
            List<CharacterTestRuntimeAiLabFrameSyncMismatch> rows)
        {
            AddMismatch(rows, "procedural", entityId, "ragdoll.available", expected != null, actual != null);
            if (expected == null || actual == null)
                return;

            AddMismatch(rows, "procedural", entityId, "ragdoll.mode", expected.Mode.ToString(), actual.Mode.ToString());
            AddMismatch(rows, "procedural", entityId, "ragdoll.remaining", Quantize(expected.DynamicSecondsRemaining), Quantize(actual.DynamicSecondsRemaining));
            int expectedCount = expected.Bodies != null ? expected.Bodies.Count : 0;
            int actualCount = actual.Bodies != null ? actual.Bodies.Count : 0;
            AddMismatch(rows, "procedural", entityId, "ragdoll.bodyCount", expectedCount, actualCount);
            int count = Math.Min(expectedCount, actualCount);
            for (int i = 0; i < count; i++)
            {
                CharacterViewOnlyRagdollBodyPlaybackState left = expected.Bodies[i];
                CharacterViewOnlyRagdollBodyPlaybackState right = actual.Bodies[i];
                string prefix = "ragdoll.body[" + i.ToString(CultureInfo.InvariantCulture) + "].";
                AddMismatch(rows, "procedural", entityId, prefix + "position", VectorChecksum(left.Position), VectorChecksum(right.Position));
                AddMismatch(rows, "procedural", entityId, prefix + "rotation", QuaternionChecksum(left.Rotation), QuaternionChecksum(right.Rotation));
                AddMismatch(rows, "procedural", entityId, prefix + "linearVelocity", VectorChecksum(left.LinearVelocity), VectorChecksum(right.LinearVelocity));
                AddMismatch(rows, "procedural", entityId, prefix + "angularVelocity", VectorChecksum(left.AngularVelocity), VectorChecksum(right.AngularVelocity));
                AddMismatch(rows, "procedural", entityId, prefix + "isKinematic", left.IsKinematic, right.IsKinematic);
            }
        }

        private static long Quantize(double value)
        {
            return (long)Math.Round(value * 1000000d, MidpointRounding.AwayFromZero);
        }

        private static float FadeProgress(float elapsed, float duration)
        {
            if (duration <= 0f)
                return elapsed > 0f ? 1f : 0f;
            return Mathf.Clamp01(elapsed / duration);
        }

        private static long VectorChecksum(Vector3 value)
        {
            unchecked
            {
                long hash = Quantize(value.x);
                hash = (hash * 397L) ^ Quantize(value.y);
                return (hash * 397L) ^ Quantize(value.z);
            }
        }

        private static long QuaternionChecksum(Quaternion value)
        {
            if (value.w < 0f)
                value = new Quaternion(-value.x, -value.y, -value.z, -value.w);
            unchecked
            {
                long hash = Quantize(value.x);
                hash = (hash * 397L) ^ Quantize(value.y);
                hash = (hash * 397L) ^ Quantize(value.z);
                return (hash * 397L) ^ Quantize(value.w);
            }
        }
    }
}
