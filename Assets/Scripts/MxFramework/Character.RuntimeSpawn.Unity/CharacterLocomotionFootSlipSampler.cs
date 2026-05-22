using System.Collections.Generic;
using MxFramework.Animation;
using UnityEngine;

namespace MxFramework.CharacterRuntimeSpawn.Unity
{
    public sealed class CharacterLocomotionFootSlipSnapshot
    {
        private readonly List<string> _diagnostics;

        public CharacterLocomotionFootSlipSnapshot(
            MxAnimationLocomotionCalibrationFrame frame,
            MxAnimationFootSlipGrade grade,
            bool grounded,
            bool leftFootResolved,
            bool rightFootResolved,
            IReadOnlyList<string> diagnostics)
        {
            Frame = frame;
            Grade = grade;
            Grounded = grounded;
            LeftFootResolved = leftFootResolved;
            RightFootResolved = rightFootResolved;
            _diagnostics = diagnostics != null ? new List<string>(diagnostics) : new List<string>();
        }

        public MxAnimationLocomotionCalibrationFrame Frame { get; }
        public MxAnimationFootSlipGrade Grade { get; }
        public bool Grounded { get; }
        public bool LeftFootResolved { get; }
        public bool RightFootResolved { get; }
        public IReadOnlyList<string> Diagnostics => _diagnostics;
    }

    public sealed class CharacterLocomotionFootSlipSampler
    {
        private readonly MxAnimationFootSlipThresholds _thresholds;
        private readonly float _contactThreshold;

        private Vector3 _lastRootPosition;
        private bool _hasLastRootPosition;
        private readonly FootState _left = new FootState();
        private readonly FootState _right = new FootState();

        public CharacterLocomotionFootSlipSampler(
            MxAnimationFootSlipThresholds thresholds,
            float contactThreshold = 0.5f)
        {
            _thresholds = thresholds;
            _contactThreshold = Mathf.Clamp01(contactThreshold);
        }

        public CharacterLocomotionFootSlipSnapshot Sample(
            long frame,
            float deltaTime,
            Transform root,
            Animator animator,
            MxAnimationSetDefinition definition,
            MxAnimationLocomotionBlendProbeSnapshot blendProbe,
            MxAnimationDiagnosticSnapshot animationSnapshot,
            bool grounded,
            string leftFootPath,
            string rightFootPath)
        {
            var diagnostics = new List<string>();
            Transform leftFoot = ResolveFoot(root, animator, HumanBodyBones.LeftFoot, leftFootPath, "left", diagnostics);
            Transform rightFoot = ResolveFoot(root, animator, HumanBodyBones.RightFoot, rightFootPath, "right", diagnostics);

            MxAnimationVelocity2D actualLocalVelocity = CalculateActualLocalVelocity(root, deltaTime);
            IReadOnlyList<MxAnimationClipPlaybackDiagnostic> playbacks = FindBaseLayerPlaybacks(animationSnapshot);
            if (playbacks == null || playbacks.Count == 0)
                diagnostics.Add(MxAnimationLocomotionCalibrationIssueCodes.ClipTimeMissing + ": no active clip playback time");

            IReadOnlyList<MxAnimationLocomotionClipCalibration> calibrations = definition != null
                ? definition.LocomotionClipCalibrations
                : null;
            if (calibrations == null || calibrations.Count == 0)
                diagnostics.Add(MxAnimationLocomotionCalibrationIssueCodes.FootMetadataMissing + ": no locomotion clip calibration metadata");

            MxAnimationVelocity2D blendedVelocity = blendProbe != null
                ? MxAnimationLocomotionCalibrationCalculator.BlendNativeVelocity(blendProbe.Weights, calibrations)
                : default;
            float velocityErrorRatio = MxAnimationLocomotionCalibrationCalculator.CalculateVelocityErrorRatio(
                actualLocalVelocity,
                blendedVelocity);

            float leftConfidence = blendProbe != null
                ? MxAnimationLocomotionCalibrationCalculator.CalculateWeightedFootContactConfidence(
                    MxAnimationLocomotionFoot.Left,
                    blendProbe.Weights,
                    playbacks,
                    calibrations)
                : 0f;
            float rightConfidence = blendProbe != null
                ? MxAnimationLocomotionCalibrationCalculator.CalculateWeightedFootContactConfidence(
                    MxAnimationLocomotionFoot.Right,
                    blendProbe.Weights,
                    playbacks,
                    calibrations)
                : 0f;

            float leftSlip = SampleFoot(_left, leftFoot, deltaTime, grounded && leftConfidence >= _contactThreshold, out float leftDistance);
            float rightSlip = SampleFoot(_right, rightFoot, deltaTime, grounded && rightConfidence >= _contactThreshold, out float rightDistance);
            float maxDistance = Mathf.Max(leftDistance, rightDistance);
            var grade = MxAnimationLocomotionCalibrationCalculator.ClassifySlip(
                Mathf.Max(leftSlip, rightSlip),
                maxDistance,
                _thresholds);

            string dominantClipId = blendProbe != null && blendProbe.HasDominantClip ? blendProbe.DominantClipKey.Id : string.Empty;
            var calibrationFrame = new MxAnimationLocomotionCalibrationFrame(
                frame,
                deltaTime,
                0f,
                0f,
                actualLocalVelocity.X,
                actualLocalVelocity.Y,
                blendedVelocity.X,
                blendedVelocity.Y,
                velocityErrorRatio,
                CalculateDirectionErrorDegrees(actualLocalVelocity, blendedVelocity),
                dominantClipId,
                leftConfidence,
                rightConfidence,
                leftSlip,
                rightSlip,
                maxDistance);

            return new CharacterLocomotionFootSlipSnapshot(
                calibrationFrame,
                grade,
                grounded,
                leftFoot != null,
                rightFoot != null,
                diagnostics);
        }

        public void Reset()
        {
            _hasLastRootPosition = false;
            _left.Reset();
            _right.Reset();
        }

        private MxAnimationVelocity2D CalculateActualLocalVelocity(Transform root, float deltaTime)
        {
            if (root == null || deltaTime <= 0.0001f)
                return default;

            Vector3 position = root.position;
            if (!_hasLastRootPosition)
            {
                _lastRootPosition = position;
                _hasLastRootPosition = true;
                return default;
            }

            Vector3 worldVelocity = (position - _lastRootPosition) / deltaTime;
            _lastRootPosition = position;
            Vector3 local = root.InverseTransformDirection(worldVelocity);
            return new MxAnimationVelocity2D(local.x, local.z);
        }

        private static IReadOnlyList<MxAnimationClipPlaybackDiagnostic> FindBaseLayerPlaybacks(MxAnimationDiagnosticSnapshot snapshot)
        {
            if (snapshot == null)
                return null;

            for (int i = 0; i < snapshot.LayerStates.Count; i++)
            {
                MxAnimationLayerDiagnostic layer = snapshot.LayerStates[i];
                if (layer != null && layer.LayerId.Equals(MxAnimationLayerId.Base))
                    return layer.ActiveClipPlaybacks;
            }

            return snapshot.LayerStates.Count > 0 ? snapshot.LayerStates[0].ActiveClipPlaybacks : null;
        }

        private static Transform ResolveFoot(
            Transform root,
            Animator animator,
            HumanBodyBones humanoidBone,
            string fallbackPath,
            string label,
            List<string> diagnostics)
        {
            if (animator != null && animator.isHuman && animator.avatar != null && animator.avatar.isHuman)
            {
                Transform bone = animator.GetBoneTransform(humanoidBone);
                if (bone != null)
                    return bone;
            }

            if (root != null && !string.IsNullOrWhiteSpace(fallbackPath))
            {
                Transform byPath = root.Find(fallbackPath);
                if (byPath != null)
                    return byPath;
            }

            Transform byName = root != null ? FindFootByName(root, label) : null;
            if (byName != null)
                return byName;

            diagnostics.Add(MxAnimationLocomotionCalibrationIssueCodes.FootBoneMissing + ": " + label + " foot transform was not found");
            return null;
        }

        private static Transform FindFootByName(Transform root, string label)
        {
            string normalizedLabel = label == "left" ? "left" : "right";
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                string normalized = child.name.Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
                if (normalized.Contains(normalizedLabel) && normalized.Contains("foot"))
                    return child;

                Transform nested = FindFootByName(child, label);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private static float SampleFoot(FootState state, Transform foot, float deltaTime, bool planted, out float maxDistance)
        {
            maxDistance = 0f;
            if (foot == null || !planted)
            {
                state.Reset();
                return 0f;
            }

            Vector3 position = ProjectHorizontal(foot.position);
            if (!state.Planted)
            {
                state.Planted = true;
                state.Anchor = position;
                state.Previous = position;
                state.MaxDistanceCm = 0f;
                return 0f;
            }

            float slip = MxAnimationLocomotionCalibrationCalculator.CalculateSlipCmPerSecond(
                state.Previous.x,
                state.Previous.z,
                position.x,
                position.z,
                deltaTime);
            float distance = MxAnimationLocomotionCalibrationCalculator.CalculateSlipDistanceCm(
                state.Anchor.x,
                state.Anchor.z,
                position.x,
                position.z);
            state.Previous = position;
            state.MaxDistanceCm = Mathf.Max(state.MaxDistanceCm, distance);
            maxDistance = state.MaxDistanceCm;
            return slip;
        }

        private static Vector3 ProjectHorizontal(Vector3 value)
        {
            return new Vector3(value.x, 0f, value.z);
        }

        private static float CalculateDirectionErrorDegrees(
            MxAnimationVelocity2D actual,
            MxAnimationVelocity2D blended)
        {
            if (actual.Magnitude <= 0.0001f || blended.Magnitude <= 0.0001f)
                return 0f;

            float dot = ((actual.X * blended.X) + (actual.Y * blended.Y)) / (actual.Magnitude * blended.Magnitude);
            dot = Mathf.Clamp(dot, -1f, 1f);
            return Mathf.Acos(dot) * Mathf.Rad2Deg;
        }

        private sealed class FootState
        {
            public bool Planted;
            public Vector3 Anchor;
            public Vector3 Previous;
            public float MaxDistanceCm;

            public void Reset()
            {
                Planted = false;
                Anchor = default;
                Previous = default;
                MaxDistanceCm = 0f;
            }
        }
    }
}
