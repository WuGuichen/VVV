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
        private const string RootSocketId = "root";
        private const string WeaponSocketId = "weapon";
        private const string WeaponTipPath = "WeaponTip";

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
            var socketFrames = new List<MxAnimationBakedSocketFrame>((frameCount + 1) * 2);
            var traceFrames = new List<MxAnimationBakedWeaponTraceFrame>(frameCount);
            MxAnimationBakedVector3 previousRoot = SampleVector(clip, string.Empty, 0f, profile);
            MxAnimationBakedVector3 previousTip = SampleWeaponTip(clip, 0f, profile, previousRoot);

            rootFrames.Add(new MxAnimationBakedRootMotionFrame(0, previousRoot, MxAnimationBakedVector3.Zero));
            AddSocketFrame(socketFrames, 0, RootSocketId, string.Empty, previousRoot, MxAnimationBakedVector3.Zero);
            AddSocketFrame(socketFrames, 0, WeaponSocketId, WeaponTipPath, previousTip, MxAnimationBakedVector3.Zero);
            for (int frame = 1; frame <= frameCount; frame++)
            {
                float time = Mathf.Min(clip.length, frame / (float)profile.SampleTickRate);
                MxAnimationBakedVector3 root = SampleVector(clip, string.Empty, time, profile);
                MxAnimationBakedVector3 tip = SampleWeaponTip(clip, time, profile, root);
                MxAnimationBakedVector3 rootDelta = Delta(previousRoot, root);
                MxAnimationBakedVector3 tipDelta = Delta(previousTip, tip);
                rootFrames.Add(new MxAnimationBakedRootMotionFrame(frame, root, rootDelta));
                AddSocketFrame(socketFrames, frame, RootSocketId, string.Empty, root, rootDelta);
                AddSocketFrame(socketFrames, frame, WeaponSocketId, WeaponTipPath, tip, tipDelta);
                traceFrames.Add(new MxAnimationBakedWeaponTraceFrame(frame, 0, WeaponSocketId, previousRoot, previousTip, root, tip));
                previousRoot = root;
                previousTip = tip;
            }

            return new MxAnimationBakeArtifact(
                profile,
                traceFrames,
                rootFrames,
                CreateEventMarkers(clip, profile),
                socketFrames: socketFrames);
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
                builder.Append("socketTrajectoryFrames: ").Append(artifact.SocketFrames.Count).Append('\n');
                builder.Append("eventMarkers: ").Append(artifact.EventMarkers.Count).Append('\n');
                builder.Append("rootMotion:\n");
                for (int i = 0; i < artifact.RootMotionFrames.Count; i++)
                {
                    MxAnimationBakedRootMotionFrame frame = artifact.RootMotionFrames[i];
                    builder.Append("- frame: ").Append(frame.LocalFrame)
                        .Append(" position: ").Append(frame.RootPosition)
                        .Append(" delta: ").Append(frame.DeltaPosition).Append('\n');
                }

                builder.Append("socketTrajectory:\n");
                for (int i = 0; i < artifact.SocketFrames.Count; i++)
                {
                    MxAnimationBakedSocketFrame frame = artifact.SocketFrames[i];
                    builder.Append("- frame: ").Append(frame.LocalFrame)
                        .Append(" socket: ").Append(frame.SocketId)
                        .Append(" path: ").Append(string.IsNullOrEmpty(frame.SocketPath) ? "<root>" : frame.SocketPath)
                        .Append(" position: ").Append(frame.Position)
                        .Append(" delta: ").Append(frame.DeltaPosition).Append('\n');
                }

                builder.Append("weaponTrace:\n");
                for (int i = 0; i < artifact.WeaponTraceFrames.Count; i++)
                {
                    MxAnimationBakedWeaponTraceFrame frame = artifact.WeaponTraceFrames[i];
                    builder.Append("- frame: ").Append(frame.LocalFrame).Append(" trace: ").Append(frame.TraceId)
                        .Append(" rootNow: ").Append(frame.RootNow)
                        .Append(" tipNow: ").Append(frame.TipNow).Append('\n');
                }

                builder.Append("eventAlignment:\n");
                for (int i = 0; i < artifact.EventMarkers.Count; i++)
                {
                    MxAnimationBakedEventMarker marker = artifact.EventMarkers[i];
                    builder.Append("- frame: ").Append(marker.LocalFrame)
                        .Append(" presentationFrame: ").Append(marker.PresentationFrame)
                        .Append(" combatFrame: ").Append(marker.CombatFrame)
                        .Append(" event: ").Append(marker.EventId)
                        .Append(" kind: ").Append(marker.Kind)
                        .Append(" sourceOrder: ").Append(marker.SourceOrder).Append('\n');
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
                        .Append(" location=").Append(issue.Location)
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
            if (TrySampleVector(clip, WeaponTipPath, time, profile, out MxAnimationBakedVector3 tip))
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
                markers.Add(new MxAnimationBakedEventMarker(
                    frame,
                    eventId,
                    ClassifyEvent(eventId),
                    sourceOrder: i,
                    presentationFrame: frame,
                    combatFrame: -1));
            }

            return markers;
        }

        private static void AddSocketFrame(
            List<MxAnimationBakedSocketFrame> frames,
            int localFrame,
            string socketId,
            string socketPath,
            MxAnimationBakedVector3 position,
            MxAnimationBakedVector3 deltaPosition)
        {
            frames.Add(new MxAnimationBakedSocketFrame(localFrame, socketId, socketPath, position, deltaPosition));
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

    public static class MxAnimationCompatibilityEditorExtractor
    {
        public static MxAnimationSkeletonCompatibilityProfile CreateSkeletonProfile(
            GameObject root,
            string profileId,
            IEnumerable<string> socketPaths = null)
        {
            var bonePaths = new List<string>();
            if (root != null)
                CollectTransformPaths(root.transform, root.transform, bonePaths);

            return new MxAnimationSkeletonCompatibilityProfile(
                profileId,
                bonePaths: bonePaths,
                socketPaths: FilterKnownPaths(socketPaths, bonePaths));
        }

        public static MxAnimationClipCompatibilityProfile CreateClipProfile(
            AnimationClip clip,
            ResourceKey clipKey,
            MxAnimationSkeletonCompatibilityProfile skeletonProfile = null,
            string skeletonProfileId = "",
            string skeletonProfileHash = "")
        {
            IReadOnlyList<string> bindingPaths = ExtractClipBindingPaths(clip);
            return new MxAnimationClipCompatibilityProfile(
                clipKey,
                ResolveClipSkeletonProfileId(skeletonProfile, bindingPaths, skeletonProfileId),
                ResolveClipSkeletonProfileHash(skeletonProfile, bindingPaths, skeletonProfileHash),
                bindingPaths);
        }

        public static MxAnimationAvatarMaskCompatibilityProfile CreateAvatarMaskProfile(
            AvatarMask avatarMask,
            ResourceKey avatarMaskKey,
            MxAnimationSkeletonCompatibilityProfile skeletonProfile = null,
            string skeletonProfileId = "",
            string skeletonProfileHash = "")
        {
            return new MxAnimationAvatarMaskCompatibilityProfile(
                avatarMaskKey,
                ResolveSkeletonProfileId(skeletonProfile, skeletonProfileId),
                ResolveSkeletonProfileHash(skeletonProfile, skeletonProfileHash),
                ExtractAvatarMaskActivePaths(avatarMask));
        }

        public static MxAnimationCompatibilityProfile CreateProfile(
            MxAnimationSkeletonCompatibilityProfile skeletonProfile,
            IEnumerable<MxAnimationClipCompatibilityProfile> clipProfiles = null,
            IEnumerable<MxAnimationAvatarMaskCompatibilityProfile> avatarMaskProfiles = null,
            IEnumerable<MxAnimationBakeArtifact> bakeArtifacts = null)
        {
            return new MxAnimationCompatibilityProfile(
                skeletonProfile,
                clipProfiles,
                avatarMaskProfiles,
                bakeArtifacts);
        }

        public static MxAnimationCompatibilityValidationReport Validate(
            MxAnimationCompatibilityProfile profile,
            MxAnimationCompatibilityExpectation expectation)
        {
            return MxAnimationCompatibilityValidator.Validate(profile, expectation);
        }

        public static IReadOnlyList<string> ExtractClipBindingPaths(AnimationClip clip)
        {
            if (clip == null)
                return Array.Empty<string>();

            var paths = new List<string>();
            EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(clip);
            for (int i = 0; i < curveBindings.Length; i++)
                AddPath(paths, curveBindings[i].path);

            EditorCurveBinding[] objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            for (int i = 0; i < objectBindings.Length; i++)
                AddPath(paths, objectBindings[i].path);

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        public static IReadOnlyList<string> ExtractAvatarMaskActivePaths(AvatarMask avatarMask)
        {
            if (avatarMask == null)
                return Array.Empty<string>();

            var paths = new List<string>();
            for (int i = 0; i < avatarMask.transformCount; i++)
            {
                if (!avatarMask.GetTransformActive(i))
                    continue;

                AddPath(paths, avatarMask.GetTransformPath(i));
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        private static void CollectTransformPaths(Transform root, Transform current, List<string> paths)
        {
            if (root == null || current == null)
                return;

            AddPath(paths, CreateRelativePath(root, current));
            for (int i = 0; i < current.childCount; i++)
                CollectTransformPaths(root, current.GetChild(i), paths);
        }

        private static IReadOnlyList<string> FilterKnownPaths(IEnumerable<string> paths, IReadOnlyList<string> knownPaths)
        {
            if (paths == null)
                return Array.Empty<string>();

            var filtered = new List<string>();
            foreach (string path in paths)
            {
                string normalized = NormalizePath(path);
                if (string.IsNullOrWhiteSpace(normalized) || !ContainsPath(knownPaths, normalized) || filtered.Contains(normalized))
                    continue;

                filtered.Add(normalized);
            }

            filtered.Sort(StringComparer.Ordinal);
            return filtered;
        }

        private static string CreateRelativePath(Transform root, Transform current)
        {
            if (current == root)
                return string.Empty;

            var names = new List<string>();
            Transform cursor = current;
            while (cursor != null && cursor != root)
            {
                names.Add(cursor.name);
                cursor = cursor.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static void AddPath(List<string> paths, string path)
        {
            string normalized = NormalizePath(path);
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            if (!paths.Contains(normalized))
                paths.Add(normalized);
        }

        private static string ResolveClipSkeletonProfileId(
            MxAnimationSkeletonCompatibilityProfile profile,
            IReadOnlyList<string> bindingPaths,
            string fallback)
        {
            if (!string.IsNullOrWhiteSpace(fallback))
                return fallback;

            return ClipBindingsMatchSkeleton(profile, bindingPaths) ? profile.ProfileId : string.Empty;
        }

        private static string ResolveClipSkeletonProfileHash(
            MxAnimationSkeletonCompatibilityProfile profile,
            IReadOnlyList<string> bindingPaths,
            string fallback)
        {
            if (!string.IsNullOrWhiteSpace(fallback))
                return fallback;

            return ClipBindingsMatchSkeleton(profile, bindingPaths) ? profile.ProfileHash : string.Empty;
        }

        private static string ResolveSkeletonProfileId(
            MxAnimationSkeletonCompatibilityProfile profile,
            string fallback)
        {
            return profile != null ? profile.ProfileId : fallback ?? string.Empty;
        }

        private static string ResolveSkeletonProfileHash(
            MxAnimationSkeletonCompatibilityProfile profile,
            string fallback)
        {
            return profile != null ? profile.ProfileHash : fallback ?? string.Empty;
        }

        private static bool ClipBindingsMatchSkeleton(
            MxAnimationSkeletonCompatibilityProfile profile,
            IReadOnlyList<string> bindingPaths)
        {
            if (profile == null)
                return false;

            for (int i = 0; i < bindingPaths.Count; i++)
            {
                string path = bindingPaths[i];
                if (!profile.ContainsBonePath(path) && !profile.ContainsSocketPath(path))
                    return false;
            }

            return true;
        }

        private static bool ContainsPath(IReadOnlyList<string> paths, string path)
        {
            string normalized = NormalizePath(path);
            for (int i = 0; i < paths.Count; i++)
            {
                if (string.Equals(paths[i], normalized, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim().Trim('/');
        }
    }
}
