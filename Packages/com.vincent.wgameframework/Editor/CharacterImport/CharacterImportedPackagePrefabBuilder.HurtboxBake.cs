using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterRuntimeSpawn;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterImport
{
    public static partial class CharacterImportedPackagePrefabBuilder
    {
        private sealed class HurtboxBakeStatus
        {
            public string Status { get; set; } = string.Empty;
            public string Hash { get; set; } = string.Empty;
            public int ClipCount { get; set; }
            public int ColliderCount { get; set; }
            public int ShapeCount { get; set; }
            public int CheckedFrameCount { get; set; }
            public CharacterWeaponTraceBakeIssueReport[] Issues { get; set; } = Array.Empty<CharacterWeaponTraceBakeIssueReport>();
        }

        private sealed class HurtboxBakeTrackState
        {
            public CharacterBodyColliderRuntimeBinding Collider;
            public Transform Parent;
            public CharacterHurtboxBakePose[] Frames = Array.Empty<CharacterHurtboxBakePose>();
            public bool IsValid = true;
        }

        private readonly struct HurtboxMotionRootCurves
        {
            private HurtboxMotionRootCurves(
                AnimationCurve translationX,
                AnimationCurve translationZ,
                AnimationCurve rotationX,
                AnimationCurve rotationY,
                AnimationCurve rotationZ,
                AnimationCurve rotationW)
            {
                TranslationX = translationX;
                TranslationZ = translationZ;
                RotationX = rotationX;
                RotationY = rotationY;
                RotationZ = rotationZ;
                RotationW = rotationW;
            }

            private AnimationCurve TranslationX { get; }
            private AnimationCurve TranslationZ { get; }
            private AnimationCurve RotationX { get; }
            private AnimationCurve RotationY { get; }
            private AnimationCurve RotationZ { get; }
            private AnimationCurve RotationW { get; }

            internal static HurtboxMotionRootCurves Create(AnimationClip clip)
            {
                AnimationCurve translationX = null;
                AnimationCurve translationZ = null;
                AnimationCurve rotationX = null;
                AnimationCurve rotationY = null;
                AnimationCurve rotationZ = null;
                AnimationCurve rotationW = null;
                EditorCurveBinding[] bindings = clip != null
                    ? AnimationUtility.GetCurveBindings(clip)
                    : Array.Empty<EditorCurveBinding>();
                for (int i = 0; i < bindings.Length; i++)
                {
                    EditorCurveBinding binding = bindings[i];
                    if (binding.type != typeof(Animator)
                        || !string.IsNullOrEmpty(binding.path))
                    {
                        continue;
                    }

                    switch (binding.propertyName)
                    {
                        case "RootT.x":
                            translationX = AnimationUtility.GetEditorCurve(clip, binding);
                            break;
                        case "RootT.z":
                            translationZ = AnimationUtility.GetEditorCurve(clip, binding);
                            break;
                        case "RootQ.x":
                            rotationX = AnimationUtility.GetEditorCurve(clip, binding);
                            break;
                        case "RootQ.y":
                            rotationY = AnimationUtility.GetEditorCurve(clip, binding);
                            break;
                        case "RootQ.z":
                            rotationZ = AnimationUtility.GetEditorCurve(clip, binding);
                            break;
                        case "RootQ.w":
                            rotationW = AnimationUtility.GetEditorCurve(clip, binding);
                            break;
                    }
                }

                return new HurtboxMotionRootCurves(
                    translationX,
                    translationZ,
                    rotationX,
                    rotationY,
                    rotationZ,
                    rotationW);
            }

            internal void Evaluate(
                float time,
                out Vector3 planarTranslation,
                out Quaternion inversePlanarRotation)
            {
                planarTranslation = new Vector3(
                    TranslationX != null ? TranslationX.Evaluate(time) : 0f,
                    0f,
                    TranslationZ != null ? TranslationZ.Evaluate(time) : 0f);
                inversePlanarRotation = Quaternion.identity;
                if (RotationX == null
                    || RotationY == null
                    || RotationZ == null
                    || RotationW == null)
                {
                    return;
                }

                var rootRotation = new Quaternion(
                    RotationX.Evaluate(time),
                    RotationY.Evaluate(time),
                    RotationZ.Evaluate(time),
                    RotationW.Evaluate(time));
                float magnitudeSquared = rootRotation.x * rootRotation.x
                    + rootRotation.y * rootRotation.y
                    + rootRotation.z * rootRotation.z
                    + rootRotation.w * rootRotation.w;
                if (!(magnitudeSquared > 0.000001f) || !float.IsFinite(magnitudeSquared))
                    return;

                rootRotation = Quaternion.Normalize(rootRotation);
                Vector3 forward = rootRotation * Vector3.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude <= 0.000001f)
                    return;
                Quaternion planarRotation = Quaternion.LookRotation(
                    forward.normalized,
                    Vector3.up);
                inversePlanarRotation = Quaternion.Inverse(planarRotation);
            }
        }

        private static HurtboxBakeStatus CollectHurtboxBakeStatus(CharacterImportedPackage package)
        {
            CharacterBodyColliderRuntimeBinding[] colliders = package?.Geometry?.BodyColliders ?? Array.Empty<CharacterBodyColliderRuntimeBinding>();
            if (package == null)
                return HurtboxFailure("HurtboxBakePackageMissing", "Imported character package is missing.", string.Empty, string.Empty, -1);

            if (colliders.Length == 0)
            {
                var empty = new CharacterHurtboxBakeCatalog();
                string json = WriteHurtboxBakeCatalog(package, empty);
                return new HurtboxBakeStatus { Status = "Ready", Hash = ComputeStringContentHash(json) };
            }

            CharacterAnimationClipRegistryEntry[] clips = package.CompiledAnimationArtifacts?.AnimationClipRegistry?.Clips
                ?? Array.Empty<CharacterAnimationClipRegistryEntry>();
            if (clips.Length == 0)
                return HurtboxFailure("HurtboxBakeClipRegistryMissing", "Formal animation clip registry is missing or empty.", string.Empty, string.Empty, -1);

            // Editor-only mesh analysis writes optional fit suggestions beside the Authoring package.
            // body_colliders.json remains authoritative; only review evidence for that exact saved file
            // does not participate in the import gate. Saving the authoritative Authoring file is the approval.
            WriteHurtboxFitDraft(package);

            CharacterHurtboxBakeCatalog catalog = BuildHurtboxBakeCatalog(package, clips, colliders, out List<CharacterWeaponTraceBakeIssueReport> issues, out int checkedFrames);
            string serialized = CharacterHurtboxBakeCatalogJson.Serialize(package.PackageId, catalog);
            if (issues.Count == 0)
                serialized = WriteHurtboxBakeCatalog(package, catalog);

            var distinctClips = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < clips.Length; i++)
                if (!string.IsNullOrWhiteSpace(clips[i]?.RuntimeResourceKey)) distinctClips.Add(clips[i].RuntimeResourceKey);
            return new HurtboxBakeStatus
            {
                Status = issues.Count == 0 ? "Ready" : "Failed",
                Hash = issues.Count == 0 ? ComputeStringContentHash(serialized) : string.Empty,
                ClipCount = distinctClips.Count,
                ColliderCount = colliders.Length,
                ShapeCount = catalog.TrackCount,
                CheckedFrameCount = checkedFrames,
                Issues = issues.ToArray()
            };
        }

        private static CharacterHurtboxBakeCatalog BuildHurtboxBakeCatalog(
            CharacterImportedPackage package,
            CharacterAnimationClipRegistryEntry[] clipEntries,
            CharacterBodyColliderRuntimeBinding[] colliders,
            out List<CharacterWeaponTraceBakeIssueReport> issues,
            out int checkedFrames)
        {
            var catalog = new CharacterHurtboxBakeCatalog();
            issues = new List<CharacterWeaponTraceBakeIssueReport>();
            checkedFrames = 0;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GetPreviewPrefabPath(package));
            if (prefab == null)
            {
                issues.Add(HurtboxIssue("HurtboxBakePreviewPrefabMissing", "Generated preview prefab is required for hurtbox sampling.", string.Empty, string.Empty, -1));
                return catalog;
            }

            var orderedClips = new List<CharacterAnimationClipRegistryEntry>(clipEntries);
            orderedClips.Sort((left, right) => string.CompareOrdinal(left?.RuntimeResourceKey, right?.RuntimeResourceKey));
            var orderedColliders = new List<CharacterBodyColliderRuntimeBinding>(colliders);
            orderedColliders.Sort((left, right) => string.CompareOrdinal(left?.ColliderLabel, right?.ColliderLabel));

            GameObject instance = null;
            bool startedAnimationMode = false;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject ?? UnityEngine.Object.Instantiate(prefab);
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.SetActive(true);
                if (!AnimationMode.InAnimationMode())
                {
                    AnimationMode.StartAnimationMode();
                    startedAnimationMode = true;
                }

                Transform bodyModel = FindChildByName(instance.transform, "BodyModel") ?? instance.transform;
                for (int clipIndex = 0; clipIndex < orderedClips.Count; clipIndex++)
                {
                    CharacterAnimationClipRegistryEntry clipEntry = orderedClips[clipIndex];
                    string clipKey = clipEntry?.RuntimeResourceKey ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(clipKey))
                    {
                        issues.Add(HurtboxIssue("HurtboxBakeClipKeyMissing", "Animation registry entry has no runtimeResourceKey.", string.Empty, string.Empty, -1));
                        continue;
                    }
                    AnimationClip clip = LoadAnimationClipForRuntimeKey(package.RootPath, clipKey);
                    if (clip == null)
                    {
                        issues.Add(HurtboxIssue("HurtboxBakeClipMissing", "Formal .anim resource could not be loaded.", clipKey, string.Empty, -1));
                        continue;
                    }

                    int frameCount = Math.Max(1, Mathf.CeilToInt(clip.length * 60f));
                    HurtboxMotionRootCurves motionRootCurves =
                        HurtboxMotionRootCurves.Create(clip);
                    var tracks = new List<HurtboxBakeTrackState>(orderedColliders.Count);
                    for (int colliderIndex = 0; colliderIndex < orderedColliders.Count; colliderIndex++)
                    {
                        CharacterBodyColliderRuntimeBinding collider = orderedColliders[colliderIndex];
                        if (!TryResolveHurtboxParent(instance.transform, bodyModel, collider, out Transform parent))
                        {
                            issues.Add(HurtboxIssue("HurtboxBakeBindingMissing", "Hurtbox parent binding could not be resolved.", clipKey, collider?.ColliderLabel, -1));
                            continue;
                        }

                        tracks.Add(new HurtboxBakeTrackState
                        {
                            Collider = collider,
                            Parent = parent,
                            Frames = new CharacterHurtboxBakePose[frameCount]
                        });
                    }

                    for (int frame = 0; frame < frameCount; frame++)
                    {
                        float time = ResolveBakeSampleTime(clip, frame);
                        SampleAnimationClipForWeaponTraceBake(instance, clip, time);
                        motionRootCurves.Evaluate(
                            time,
                            out Vector3 motionRootTranslation,
                            out Quaternion inverseMotionRootRotation);
                        for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
                        {
                            HurtboxBakeTrackState track = tracks[trackIndex];
                            if (!track.IsValid)
                                continue;
                            if (!TryBakeHurtboxPose(
                                    instance.transform,
                                    track.Parent,
                                    track.Collider,
                                    motionRootTranslation,
                                    inverseMotionRootRotation,
                                    out track.Frames[frame]))
                            {
                                issues.Add(HurtboxIssue("HurtboxBakeShapeInvalid", "Hurtbox shape or dimensions are invalid.", clipKey, track.Collider?.ColliderLabel, frame));
                                track.IsValid = false;
                                continue;
                            }
                            checkedFrames++;
                        }
                    }
                    for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
                    {
                        HurtboxBakeTrackState track = tracks[trackIndex];
                        if (track.IsValid)
                            catalog.RegisterTrack(clipKey, track.Collider.ColliderId, CharacterHurtboxBakeTrack.Compress(track.Frames));
                    }
                }
            }
            finally
            {
                if (startedAnimationMode) AnimationMode.StopAnimationMode();
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            }
            return catalog;
        }

        private static bool TryResolveHurtboxParent(Transform root, Transform bodyModel, CharacterBodyColliderRuntimeBinding collider, out Transform parent)
        {
            parent = null;
            if (collider == null) return false;
            string kind = collider.LocalPose.ParentKind ?? string.Empty;
            string path = collider.LocalPose.ParentPath ?? string.Empty;
            if (string.Equals(kind, "ModelRoot", StringComparison.Ordinal))
            {
                parent = bodyModel;
                return parent != null;
            }
            if (string.Equals(kind, "Bone", StringComparison.Ordinal))
                parent = FindBodyChild(bodyModel, path, collider.PartLabel);
            else if (string.Equals(kind, "Socket", StringComparison.Ordinal))
                parent = FindChildByName(root, "Socket_" + path);
            else if (string.Equals(kind, "Locator", StringComparison.Ordinal))
                parent = FindLocatorTransform(bodyModel, path);
            else if (string.Equals(kind, "BodyPart", StringComparison.Ordinal))
                parent = FindBodyChild(bodyModel, path, collider.PartLabel);
            return parent != null;
        }

        private static bool TryBakeHurtboxPose(
            Transform root,
            Transform parent,
            CharacterBodyColliderRuntimeBinding binding,
            Vector3 motionRootTranslation,
            Quaternion inverseMotionRootRotation,
            out CharacterHurtboxBakePose pose)
        {
            pose = default;
            if (root == null || parent == null || binding == null) return false;
            Vector3 position = ToVector3(binding.LocalPose.Position, Vector3.zero);
            Quaternion rotation = ToQuaternion(binding.LocalPose.Rotation);
            Vector3 scale = ToVector3(binding.LocalPose.Scale, Vector3.one);
            if (!IsFinitePositiveScale(scale)) return false;

            Vector3 RootPoint(Vector3 local) => inverseMotionRootRotation
                * (root.InverseTransformPoint(
                    parent.TransformPoint(
                        position + rotation * Vector3.Scale(local, scale)))
                    - motionRootTranslation);
            Vector3 RootDirection(Vector3 local) => inverseMotionRootRotation
                * root.InverseTransformVector(
                    parent.TransformVector(
                        rotation * Vector3.Scale(local, scale)));
            CharacterHurtboxBakeVector3 QPos(Vector3 value) => Quantize(value, CharacterHurtboxBakePose.MillimetresPerMetre);
            CharacterHurtboxBakeVector3 QAxis(Vector3 value) => Quantize(value.normalized, CharacterHurtboxBakePose.AxisQuantizationScale);

            if (string.Equals(binding.Shape, "Sphere", StringComparison.OrdinalIgnoreCase))
            {
                if (!(binding.Radius > 0f)) return false;
                float radius = binding.Radius * Mathf.Max(RootDirection(Vector3.right).magnitude, Mathf.Max(RootDirection(Vector3.up).magnitude, RootDirection(Vector3.forward).magnitude));
                pose = new CharacterHurtboxBakePose(CharacterHurtboxBakeShapeKind.Sphere, QPos(RootPoint(Vector3.zero)), default, default, default, default, default, QuantizeScalar(radius));
                return radius > 0f;
            }
            if (string.Equals(binding.Shape, "Capsule", StringComparison.OrdinalIgnoreCase))
            {
                if (!(binding.Radius > 0f) || !(binding.Height >= binding.Radius * 2f)) return false;
                float halfSegment = Mathf.Max(0f, binding.Height * 0.5f - binding.Radius);
                float radius = binding.Radius * Mathf.Max(RootDirection(Vector3.right).magnitude, RootDirection(Vector3.forward).magnitude);
                pose = new CharacterHurtboxBakePose(CharacterHurtboxBakeShapeKind.Capsule, QPos(RootPoint(Vector3.down * halfSegment)), QPos(RootPoint(Vector3.up * halfSegment)), default, default, default, default, QuantizeScalar(radius));
                return radius > 0f;
            }
            if (string.Equals(binding.Shape, "Box", StringComparison.OrdinalIgnoreCase)
                || string.Equals(binding.Shape, "Obb", StringComparison.OrdinalIgnoreCase))
            {
                Vector3 size = ToVector3(binding.Size, Vector3.zero);
                if (!(size.x > 0f) || !(size.y > 0f) || !(size.z > 0f)) return false;
                Vector3 x = RootDirection(Vector3.right), y = RootDirection(Vector3.up), z = RootDirection(Vector3.forward);
                if (x.sqrMagnitude <= 0f || y.sqrMagnitude <= 0f || z.sqrMagnitude <= 0f) return false;
                Vector3 halfExtents = Vector3.Scale(size * 0.5f, new Vector3(x.magnitude, y.magnitude, z.magnitude));
                pose = new CharacterHurtboxBakePose(CharacterHurtboxBakeShapeKind.Obb, QPos(RootPoint(Vector3.zero)), default, QPos(halfExtents), QAxis(x), QAxis(y), QAxis(z), 0L);
                return true;
            }
            return false;
        }

        private static bool IsFinitePositiveScale(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z)
            && value.x > 0f && value.y > 0f && value.z > 0f;

        private static long QuantizeScalar(float value) => (long)Math.Round(value * CharacterHurtboxBakePose.MillimetresPerMetre, MidpointRounding.AwayFromZero);
        private static CharacterHurtboxBakeVector3 Quantize(Vector3 value, int scale) => new CharacterHurtboxBakeVector3(
            (long)Math.Round(value.x * scale, MidpointRounding.AwayFromZero),
            (long)Math.Round(value.y * scale, MidpointRounding.AwayFromZero),
            (long)Math.Round(value.z * scale, MidpointRounding.AwayFromZero));

        private static string WriteHurtboxBakeCatalog(CharacterImportedPackage package, CharacterHurtboxBakeCatalog catalog)
        {
            byte[] binary = CharacterHurtboxBakeCatalogBinary.Serialize(catalog);
            string binaryHash = ComputeStringContentHash(Convert.ToBase64String(binary));
            string json = CharacterHurtboxBakeCatalogBinary.SerializeIndex(
                package.PackageId,
                catalog,
                CharacterHurtboxBakeCatalogBinary.DefaultBinaryRelativePath,
                binaryHash,
                binary.Length);
            string path = Path.Combine(package.RootPath, CharacterHurtboxBakeCatalogJson.DefaultRelativePath);
            string binaryPath = Path.Combine(package.RootPath, CharacterHurtboxBakeCatalogBinary.DefaultBinaryRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Directory.CreateDirectory(Path.GetDirectoryName(binaryPath));
            WriteHurtboxBakeBinaryIfChanged(binaryPath, binary);
            WriteHurtboxBakeIndexIfChanged(path, json);
            return json + "\n" + binaryHash;
        }

        private static void WriteHurtboxBakeBinaryIfChanged(string path, byte[] bytes)
        {
            if (HurtboxBakeBinaryMatches(path, bytes)) return;

            // Unity may keep imported .bytes assets memory-mapped while a generated
            // character prefab is open. Release AssetDatabase handles before a real
            // content change; unchanged catalogs must not be reopened for writing.
            AssetDatabase.ReleaseCachedFileHandles();
            File.WriteAllBytes(path, bytes);
        }

        private static bool HurtboxBakeBinaryMatches(string path, byte[] expected)
        {
            if (!File.Exists(path) || expected == null) return false;
            try
            {
                var info = new FileInfo(path);
                if (info.Length != expected.Length) return false;
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                for (int offset = 0; offset < expected.Length;)
                {
                    int value = stream.ReadByte();
                    if (value < 0 || value != expected[offset]) return false;
                    offset++;
                }
                return stream.ReadByte() < 0;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static void WriteHurtboxBakeIndexIfChanged(string path, string json)
        {
            if (File.Exists(path)
                && string.Equals(File.ReadAllText(path), json, StringComparison.Ordinal))
                return;
            File.WriteAllText(path, json);
        }

        private static HurtboxBakeStatus HurtboxFailure(string code, string message, string clipKey, string colliderId, int frame) => new HurtboxBakeStatus
        {
            Status = "Failed",
            Issues = new[] { HurtboxIssue(code, message, clipKey, colliderId, frame) }
        };

        private static CharacterWeaponTraceBakeIssueReport HurtboxIssue(string code, string message, string clipKey, string colliderId, int frame) => new CharacterWeaponTraceBakeIssueReport
        {
            Code = code ?? string.Empty,
            Message = message ?? string.Empty,
            ClipKey = clipKey ?? string.Empty,
            TraceId = colliderId ?? string.Empty,
            Frame = frame,
            SourcePath = CharacterHurtboxBakeCatalogJson.DefaultRelativePath,
            SourceObjectPath = "hurtboxBake/" + (clipKey ?? string.Empty) + "/" + (colliderId ?? string.Empty)
        };
    }
}
