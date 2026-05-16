using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Animation;
using MxFramework.Resources;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.Animation
{
    public sealed class MxAnimationBakeEditorResult
    {
        public MxAnimationBakeEditorResult(
            MxAnimationBakeArtifact artifact,
            MxAnimationBakeValidationReport validationReport,
            string outputPath,
            string reportText)
        {
            Artifact = artifact;
            ValidationReport = validationReport ?? new MxAnimationBakeValidationReport();
            OutputPath = outputPath ?? string.Empty;
            ReportText = reportText ?? string.Empty;
        }

        public MxAnimationBakeArtifact Artifact { get; }
        public MxAnimationBakeValidationReport ValidationReport { get; }
        public string OutputPath { get; }
        public string ReportText { get; }
        public bool Success => Artifact != null && !ValidationReport.HasErrors;
    }

    public static class MxAnimationBakeEditorTool
    {
        public const string DefaultOutputRoot = "Assets/Art/MxFramework/Samples/Characters/Skeleton/Bakes";
        public const int DefaultSampleTickRate = 30;
        public const int DefaultQuantizationScale = 1000;

        private const string MenuPath = "MxFramework/MxAnimation/Bake Selected Animation Clip MVP";
        private const string DefaultPackageId = "mxframework.samples";

        [MenuItem(MenuPath, priority = 132)]
        public static void BakeSelectedClipToDefaultArtifact()
        {
            AnimationClip clip = Selection.activeObject as AnimationClip;
            if (clip == null)
            {
                Debug.LogWarning("Select an AnimationClip before running MxAnimation bake.");
                return;
            }

            MxAnimationBakeEditorResult result = BakeClipToFile(clip, DefaultOutputRoot);
            if (result.Success)
                Debug.Log("MxAnimation bake artifact generated: " + result.OutputPath);
            else
                Debug.LogWarning(result.ReportText);
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool CanBakeSelectedClipToDefaultArtifact()
        {
            return Selection.activeObject is AnimationClip && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        public static MxAnimationBakeEditorResult BakeClipToFile(AnimationClip clip, string outputRoot)
        {
            MxAnimationBakeEditorResult result = BakeClip(clip);
            string normalizedRoot = NormalizeAssetPath(outputRoot);
            EnsureFolder(normalizedRoot);
            string outputPath = normalizedRoot + "/" + NormalizeName(clip != null ? clip.name : "clip") + ".mxbake.txt";
            if (result.Artifact != null)
                File.WriteAllText(outputPath, result.ReportText, Encoding.UTF8);

            AssetDatabase.ImportAsset(outputPath);
            AssetDatabase.SaveAssets();
            return new MxAnimationBakeEditorResult(result.Artifact, result.ValidationReport, outputPath, result.ReportText);
        }

        public static MxAnimationBakeEditorResult BakeClip(AnimationClip clip)
        {
            if (clip == null)
            {
                var report = new MxAnimationBakeValidationReport();
                report.AddError("BakeSourceClipMissing", "clip", "non-null", "null", "AnimationClip is required.");
                return new MxAnimationBakeEditorResult(null, report, string.Empty, CreateReportText(null, report));
            }

            string clipName = NormalizeName(clip.name);
            ResourceKey clipKey = new ResourceKey("art.character.skeleton.animation." + clipName, ResourceTypeIds.AnimationClip, packageId: DefaultPackageId);
            string clipHash = ComputeClipHash(clip);
            var profile = new MxAnimationBakeProfile(
                "mxanimation.bake." + clipName,
                clipKey,
                clipHash,
                "skeleton",
                "clip:" + clipHash,
                DefaultSampleTickRate,
                DefaultQuantizationScale,
                MxAnimationBakeCoordinateSpace.Local,
                MxAnimationBakeRoundingPolicy.RoundNearest,
                CreateImportFingerprint(clip));

            MxAnimationBakeArtifact artifact = BakeClip(clip, profile);
            MxAnimationBakeValidationReport validation = MxAnimationBakeArtifactValidator.Validate(
                artifact,
                new MxAnimationBakeExpectation(
                    sourceClipHash: profile.SourceClipHash,
                    profileHash: profile.ProfileHash,
                    skeletonProfileHash: profile.SkeletonProfileHash,
                    artifactHash: artifact.ArtifactHash));
            return new MxAnimationBakeEditorResult(artifact, validation, string.Empty, CreateReportText(artifact, validation));
        }

        public static MxAnimationBakeArtifact BakeClip(AnimationClip clip, MxAnimationBakeProfile profile)
        {
            if (clip == null)
                throw new ArgumentNullException(nameof(clip));
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            int frameCount = Math.Max(1, (int)Math.Ceiling(Math.Max(clip.length, 0.0001f) * profile.SampleTickRate));
            var rootFrames = new List<MxAnimationBakedRootMotionFrame>(frameCount + 1);
            var traceFrames = new List<MxAnimationBakedWeaponTraceFrame>(frameCount);
            MxAnimationBakedVector3 previousRoot = SampleVector(clip, string.Empty, 0f, profile);
            MxAnimationBakedVector3 previousTip = SampleWeaponTip(clip, 0f, profile, previousRoot);

            rootFrames.Add(new MxAnimationBakedRootMotionFrame(0, previousRoot, MxAnimationBakedVector3.Zero));
            for (int frame = 1; frame <= frameCount; frame++)
            {
                float time = Mathf.Min(clip.length, frame / (float)profile.SampleTickRate);
                MxAnimationBakedVector3 root = SampleVector(clip, string.Empty, time, profile);
                MxAnimationBakedVector3 tip = SampleWeaponTip(clip, time, profile, root);
                rootFrames.Add(new MxAnimationBakedRootMotionFrame(frame, root, Delta(previousRoot, root)));
                traceFrames.Add(new MxAnimationBakedWeaponTraceFrame(frame, traceId: 0, socketId: "weapon", previousRoot, previousTip, root, tip));
                previousRoot = root;
                previousTip = tip;
            }

            return new MxAnimationBakeArtifact(
                profile,
                traceFrames,
                rootFrames,
                CreateEventMarkers(clip, profile));
        }

        public static string CreateReportText(MxAnimationBakeArtifact artifact, MxAnimationBakeValidationReport validation)
        {
            var builder = new StringBuilder();
            builder.Append("MxAnimation Bake Artifact\n");
            builder.Append("success: ").Append(validation != null && !validation.HasErrors && artifact != null ? "true" : "false").Append('\n');
            if (artifact != null)
            {
                builder.Append("artifactHash: ").Append(artifact.ArtifactHash).Append('\n');
                builder.Append("profileHash: ").Append(artifact.Profile.ProfileHash).Append('\n');
                builder.Append("sourceClip: ").Append(artifact.Profile.SourceClipKey).Append('\n');
                builder.Append("sourceClipHash: ").Append(artifact.Profile.SourceClipHash).Append('\n');
                builder.Append("skeletonProfileHash: ").Append(artifact.Profile.SkeletonProfileHash).Append('\n');
                builder.Append("sampleTickRate: ").Append(artifact.Profile.SampleTickRate).Append('\n');
                builder.Append("quantizationScale: ").Append(artifact.Profile.QuantizationScale).Append('\n');
                builder.Append("weaponTraceFrames: ").Append(artifact.WeaponTraceFrames.Count).Append('\n');
                builder.Append("rootMotionFrames: ").Append(artifact.RootMotionFrames.Count).Append('\n');
                builder.Append("eventMarkers: ").Append(artifact.EventMarkers.Count).Append('\n');
                builder.Append("weaponTrace:\n");
                for (int i = 0; i < artifact.WeaponTraceFrames.Count; i++)
                {
                    MxAnimationBakedWeaponTraceFrame frame = artifact.WeaponTraceFrames[i];
                    builder.Append("- frame: ").Append(frame.LocalFrame).Append(" trace: ").Append(frame.TraceId)
                        .Append(" rootNow: ").Append(frame.RootNow)
                        .Append(" tipNow: ").Append(frame.TipNow).Append('\n');
                }
            }

            builder.Append("issues:\n");
            if (validation == null || validation.Issues.Count == 0)
            {
                builder.Append("- none\n");
            }
            else
            {
                for (int i = 0; i < validation.Issues.Count; i++)
                {
                    MxAnimationBakeIssue issue = validation.Issues[i];
                    builder.Append("- ").Append(issue.Severity)
                        .Append(' ').Append(issue.Code)
                        .Append(" field=").Append(issue.Field)
                        .Append(" expected=").Append(issue.Expected)
                        .Append(" actual=").Append(issue.Actual)
                        .Append(" message=").Append(issue.Message)
                        .Append('\n');
                }
            }

            return builder.ToString();
        }

        private static MxAnimationBakedVector3 SampleWeaponTip(
            AnimationClip clip,
            float time,
            MxAnimationBakeProfile profile,
            MxAnimationBakedVector3 fallbackRoot)
        {
            if (TrySampleVector(clip, "WeaponTip", time, profile, out MxAnimationBakedVector3 tip))
                return tip;

            long forward = profile.QuantizationScale;
            return new MxAnimationBakedVector3(fallbackRoot.X, fallbackRoot.Y, fallbackRoot.Z + forward);
        }

        private static MxAnimationBakedVector3 SampleVector(
            AnimationClip clip,
            string path,
            float time,
            MxAnimationBakeProfile profile)
        {
            return TrySampleVector(clip, path, time, profile, out MxAnimationBakedVector3 value)
                ? value
                : MxAnimationBakedVector3.Zero;
        }

        private static bool TrySampleVector(
            AnimationClip clip,
            string path,
            float time,
            MxAnimationBakeProfile profile,
            out MxAnimationBakedVector3 value)
        {
            bool x = TryEvaluateCurve(clip, path, "m_LocalPosition.x", time, out float fx)
                || TryEvaluateCurve(clip, path, "localPosition.x", time, out fx);
            bool y = TryEvaluateCurve(clip, path, "m_LocalPosition.y", time, out float fy)
                || TryEvaluateCurve(clip, path, "localPosition.y", time, out fy);
            bool z = TryEvaluateCurve(clip, path, "m_LocalPosition.z", time, out float fz)
                || TryEvaluateCurve(clip, path, "localPosition.z", time, out fz);

            if (!x && !y && !z)
            {
                value = MxAnimationBakedVector3.Zero;
                return false;
            }

            value = MxAnimationBakeQuantizer.QuantizeVector3(
                x ? fx : 0d,
                y ? fy : 0d,
                z ? fz : 0d,
                profile.QuantizationScale,
                profile.RoundingPolicy);
            return true;
        }

        private static bool TryEvaluateCurve(AnimationClip clip, string path, string propertyName, float time, out float value)
        {
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding binding = bindings[i];
                if (!string.Equals(binding.path ?? string.Empty, path ?? string.Empty, StringComparison.Ordinal)
                    || !string.Equals(binding.propertyName, propertyName, StringComparison.Ordinal))
                    continue;

                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null)
                    break;

                value = curve.Evaluate(time);
                return true;
            }

            value = 0f;
            return false;
        }

        private static IReadOnlyList<MxAnimationBakedEventMarker> CreateEventMarkers(
            AnimationClip clip,
            MxAnimationBakeProfile profile)
        {
            var markers = new List<MxAnimationBakedEventMarker>();
            AnimationEvent[] events = clip.events ?? Array.Empty<AnimationEvent>();
            for (int i = 0; i < events.Length; i++)
            {
                AnimationEvent evt = events[i];
                int frame = Math.Max(0, (int)Math.Round(evt.time * profile.SampleTickRate, MidpointRounding.AwayFromZero));
                string eventId = string.IsNullOrWhiteSpace(evt.functionName) ? "event:" + i.ToString(CultureInfo.InvariantCulture) : evt.functionName;
                markers.Add(new MxAnimationBakedEventMarker(frame, eventId, ClassifyEvent(eventId), sourceOrder: i));
            }

            return markers;
        }

        private static MxAnimationBakeEventKind ClassifyEvent(string eventId)
        {
            return eventId != null && eventId.IndexOf("foot", StringComparison.OrdinalIgnoreCase) >= 0
                ? MxAnimationBakeEventKind.Footstep
                : MxAnimationBakeEventKind.Marker;
        }

        private static MxAnimationBakedVector3 Delta(MxAnimationBakedVector3 from, MxAnimationBakedVector3 to)
        {
            return new MxAnimationBakedVector3(to.X - from.X, to.Y - from.Y, to.Z - from.Z);
        }

        private static string CreateImportFingerprint(AnimationClip clip)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrWhiteSpace(path))
                return "memory:" + clip.name + ":" + clip.frameRate.ToString("R", CultureInfo.InvariantCulture);

            Hash128 hash = AssetDatabase.GetAssetDependencyHash(path);
            return path + ":" + hash;
        }

        private static string ComputeClipHash(AnimationClip clip)
        {
            var builder = new StringBuilder();
            builder.Append("clip=").Append(clip.name).Append('\n');
            builder.Append("length=").Append(clip.length.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("frameRate=").Append(clip.frameRate.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            Array.Sort(bindings, CompareBinding);
            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding binding = bindings[i];
                builder.Append("binding=").Append(binding.path).Append('|').Append(binding.type != null ? binding.type.FullName : string.Empty).Append('|').Append(binding.propertyName).Append('\n');
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null)
                    continue;

                Keyframe[] keys = curve.keys;
                for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
                {
                    Keyframe key = keys[keyIndex];
                    builder.Append("key=").Append(key.time.ToString("R", CultureInfo.InvariantCulture))
                        .Append(',').Append(key.value.ToString("R", CultureInfo.InvariantCulture))
                        .Append(',').Append(key.inTangent.ToString("R", CultureInfo.InvariantCulture))
                        .Append(',').Append(key.outTangent.ToString("R", CultureInfo.InvariantCulture))
                        .Append('\n');
                }
            }

            AnimationEvent[] events = clip.events ?? Array.Empty<AnimationEvent>();
            Array.Sort(events, CompareEvent);
            for (int i = 0; i < events.Length; i++)
            {
                AnimationEvent evt = events[i];
                builder.Append("event=").Append(evt.time.ToString("R", CultureInfo.InvariantCulture))
                    .Append('|').Append(evt.functionName ?? string.Empty)
                    .Append('|').Append(evt.stringParameter ?? string.Empty)
                    .Append('|').Append(evt.floatParameter.ToString("R", CultureInfo.InvariantCulture))
                    .Append('|').Append(evt.intParameter.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
            }

            return "sha256:" + Sha256Hex(builder.ToString());
        }

        private static int CompareBinding(EditorCurveBinding left, EditorCurveBinding right)
        {
            int result = string.CompareOrdinal(left.path, right.path);
            if (result != 0)
                return result;
            result = string.CompareOrdinal(left.type != null ? left.type.FullName : string.Empty, right.type != null ? right.type.FullName : string.Empty);
            if (result != 0)
                return result;
            return string.CompareOrdinal(left.propertyName, right.propertyName);
        }

        private static int CompareEvent(AnimationEvent left, AnimationEvent right)
        {
            int result = left.time.CompareTo(right.time);
            if (result != 0)
                return result;
            result = string.CompareOrdinal(left.functionName ?? string.Empty, right.functionName ?? string.Empty);
            if (result != 0)
                return result;
            result = string.CompareOrdinal(left.stringParameter ?? string.Empty, right.stringParameter ?? string.Empty);
            if (result != 0)
                return result;
            result = left.floatParameter.CompareTo(right.floatParameter);
            if (result != 0)
                return result;
            return left.intParameter.CompareTo(right.intParameter);
        }

        private static string Sha256Hex(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
                byte[] hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static string NormalizeName(string value)
        {
            string name = (value ?? string.Empty).Trim().ToLowerInvariant();
            var builder = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool valid = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-';
                builder.Append(valid ? c : '_');
            }

            string result = builder.ToString().Trim('_');
            return string.IsNullOrEmpty(result) ? "clip" : result;
        }

        private static string NormalizeAssetPath(string path)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            return string.IsNullOrWhiteSpace(normalized) ? DefaultOutputRoot : normalized;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = NormalizeAssetPath(path).Split('/');
            if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.Ordinal))
                throw new InvalidOperationException("Output path must be under Assets/: " + path);

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
