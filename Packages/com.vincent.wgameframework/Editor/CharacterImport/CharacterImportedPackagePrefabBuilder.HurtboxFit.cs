using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.CharacterRuntimeSpawn;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterImport
{
    public static partial class CharacterImportedPackagePrefabBuilder
    {
        private const int HurtboxFitSamplesPerRenderer = 4096;
        private const int HurtboxFitPointCapacityPerCollider = 20000;
        private const int HurtboxFitMaximumColliders = 32;
        private const int HurtboxFitMaximumObbs = 8;
        private const float HurtboxFitMaximumFalseNegativePercent = 0f;

        private static JObject WriteHurtboxFitDraft(CharacterImportedPackage package)
        {
            CharacterBodyColliderRuntimeBinding[] source = package?.Geometry?.BodyColliders ?? Array.Empty<CharacterBodyColliderRuntimeBinding>();
            if (package == null || source.Length == 0 || source.Length > HurtboxFitMaximumColliders) return null;

            CharacterAnimationClipRegistryEntry[] clips = package.CompiledAnimationArtifacts?.AnimationClipRegistry?.Clips
                ?? Array.Empty<CharacterAnimationClipRegistryEntry>();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GetPreviewPrefabPath(package));
            if (prefab == null || clips.Length == 0) return null;

            string authoringRoot = ResolveAuthoringPackageRoot(package.PackageId);
            if (string.IsNullOrWhiteSpace(authoringRoot) || !Directory.Exists(authoringRoot))
                return BuildHurtboxFitDraft(package, prefab, clips, source);
            string cacheRoot = Path.Combine(authoringRoot, "package_cache");
            Directory.CreateDirectory(cacheRoot);
            string authoritativeColliderHash = ComputeAuthoritativeColliderHash(authoringRoot);
            string inputHash = ComputeHurtboxFitInputHash(package, prefab, clips, authoritativeColliderHash);
            JObject fitCache = ReadEditorImportCache(package.RootPath, "hurtbox_fit");
            JObject draft = null;
            if (string.Equals((string)fitCache?["inputHash"], inputHash, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace((string)fitCache?["draft"]))
            {
                try { draft = JObject.Parse((string)fitCache["draft"]); }
                catch (JsonException) { draft = null; }
            }
            if (draft == null)
            {
                draft = BuildHurtboxFitDraft(package, prefab, clips, source);
                if (draft == null)
                    return null;
                WriteEditorImportCache(package.RootPath, "hurtbox_fit", new JObject
                {
                    ["inputHash"] = inputHash,
                    ["draft"] = draft.ToString(Formatting.None)
                });
            }

            string draftHash = ComputeStringContentHash(((JArray)draft["colliders"]).ToString(Formatting.None));
            draft["draftHash"] = draftHash;
            draft["authoritativeColliderHash"] = authoritativeColliderHash;
            ApplyPersistedHurtboxFitReview(cacheRoot, authoritativeColliderHash, draft);
            string path = Path.Combine(cacheRoot, "hurtbox_fit_draft.json");
            string json = draft.ToString(Formatting.Indented) + Environment.NewLine;
            if (!File.Exists(path) || !string.Equals(File.ReadAllText(path), json, StringComparison.Ordinal))
                File.WriteAllText(path, json);
            return draft;
        }

        private static string ComputeHurtboxFitInputHash(
            CharacterImportedPackage package,
            GameObject prefab,
            CharacterAnimationClipRegistryEntry[] clips,
            string authoritativeColliderHash)
        {
            var value = new System.Text.StringBuilder("CharacterHurtboxFit.v2|")
                .Append(BuilderVersion).Append('|')
                .Append(authoritativeColliderHash ?? string.Empty).Append('|')
                .Append(package?.ImportReport?.GeometryBindingHash ?? string.Empty).Append('|')
                .Append(AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(prefab)));
            for (int i = 0; clips != null && i < clips.Length; i++)
            {
                string clipKey = clips[i]?.RuntimeResourceKey ?? string.Empty;
                AnimationClip clip = LoadAnimationClipForRuntimeKey(package.RootPath, clipKey);
                value.Append("|clip:").Append(clipKey).Append(':')
                    .Append(clip != null ? AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(clip)).ToString() : "missing");
            }
            return ComputeStringContentHash(value.ToString());
        }

        private static string ComputeAuthoritativeColliderHash(string authoringRoot)
        {
            string path = Path.Combine(authoringRoot, "geometry", "body_colliders.json");
            return File.Exists(path) ? ComputeStringContentHash(File.ReadAllText(path)) : string.Empty;
        }

        private static void ApplyPersistedHurtboxFitReview(string cacheRoot, string authoritativeColliderHash, JObject draft)
        {
            draft["authoritativeReviewStatus"] = "ReviewRequired";
            string path = Path.Combine(cacheRoot, "hurtbox_fit_review.json");
            if (!File.Exists(path)) return;
            try
            {
                JObject review = JObject.Parse(File.ReadAllText(path));
                if (string.IsNullOrWhiteSpace(authoritativeColliderHash)
                    || !string.Equals((string)review["colliderHash"], authoritativeColliderHash, StringComparison.Ordinal)) return;
                bool acceptedAsAuthored = (bool?)review["acceptedAsAuthored"] ?? false;
                JObject quality = (JObject)draft["quality"];
                quality["falsePositiveStatus"] = acceptedAsAuthored ? "AcceptedAsAuthored" : "ManualConfirmationRequired";
                quality["acceptedAsAuthored"] = acceptedAsAuthored;
                quality["measurementStatus"] = (string)review["measurementStatus"] ?? "NotRequired";
                draft["authoritativeReviewStatus"] = acceptedAsAuthored ? "Confirmed" : "ReviewRequired";
                draft["review"] = review;
            }
            catch (Exception exception)
            {
                ((JArray)draft["issues"]).Add(FitIssue(
                    "HURTBOX_FIT_REVIEW_INVALID",
                    string.Empty,
                    string.Empty,
                    "Six-direction review could not be read: " + exception.Message));
                draft["status"] = "Blocked";
            }
        }

        private static JObject BuildHurtboxFitDraft(
            CharacterImportedPackage package,
            GameObject prefab,
            CharacterAnimationClipRegistryEntry[] clipEntries,
            CharacterBodyColliderRuntimeBinding[] bindings)
        {
            var issues = new JArray();
            var templates = new List<HurtboxFitTemplate>(bindings.Length);
            var proposed = new JArray();
            var reports = new JArray();
            GameObject instance = null;
            List<HurtboxFitRendererSamples> rendererSamples = null;
            bool startedAnimationMode = false;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject ?? UnityEngine.Object.Instantiate(prefab);
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.SetActive(true);
                Transform bodyModel = FindChildByName(instance.transform, "BodyModel") ?? instance.transform;

                for (int i = 0; i < bindings.Length; i++)
                {
                    CharacterBodyColliderRuntimeBinding binding = bindings[i];
                    if (!TryResolveHurtboxParent(instance.transform, bodyModel, binding, out Transform parent))
                    {
                        issues.Add(FitIssue("HURTBOX_FIT_BINDING_UNRESOLVED", binding?.ColliderLabel, string.Empty, "Collider parent could not be resolved."));
                        continue;
                    }
                    templates.Add(new HurtboxFitTemplate(binding, parent));
                }
                if (templates.Count != bindings.Length) return FitFailure(package, bindings.Length, issues);
                templates = BuildCoarseConservativeFitTemplates(templates);

                SkinnedMeshRenderer[] renderers = bodyModel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length == 0)
                {
                    issues.Add(FitIssue("HURTBOX_FIT_SKINNED_MESH_MISSING", string.Empty, string.Empty, "No SkinnedMeshRenderer was found below BodyModel."));
                    return FitFailure(package, bindings.Length, issues);
                }

                rendererSamples = new List<HurtboxFitRendererSamples>(renderers.Length);
                for (int i = 0; i < renderers.Length; i++)
                {
                    HurtboxFitRendererSamples samples = BuildRendererSamples(renderers[i], templates, issues);
                    if (samples != null) rendererSamples.Add(samples);
                }
                if (issues.Count != 0 || rendererSamples.Count == 0) return FitFailure(package, bindings.Length, issues);

                if (!AnimationMode.InAnimationMode())
                {
                    AnimationMode.StartAnimationMode();
                    startedAnimationMode = true;
                }

                var orderedClips = new List<CharacterAnimationClipRegistryEntry>(clipEntries);
                orderedClips.Sort((left, right) => string.CompareOrdinal(left?.RuntimeResourceKey, right?.RuntimeResourceKey));
                int sampledFrames = 0;
                for (int clipIndex = 0; clipIndex < orderedClips.Count; clipIndex++)
                {
                    string clipKey = orderedClips[clipIndex]?.RuntimeResourceKey ?? string.Empty;
                    AnimationClip clip = LoadAnimationClipForRuntimeKey(package.RootPath, clipKey);
                    if (clip == null)
                    {
                        issues.Add(FitIssue("HURTBOX_FIT_CLIP_MISSING", string.Empty, clipKey, "Formal .anim resource could not be loaded."));
                        continue;
                    }
                    // Authoring primitives describe body thickness in one stable reference pose. Runtime motion
                    // is supplied by the 60 FPS bake tracks, so evaluating every frame of every clip here only
                    // repeated whole-skeleton work without contributing any fit points.
                    if (clipKey.IndexOf("idle", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    SampleAnimationClipForWeaponTraceBake(instance, clip, ResolveBakeSampleTime(clip, 0));
                    for (int rendererIndex = 0; rendererIndex < rendererSamples.Count; rendererIndex++)
                        rendererSamples[rendererIndex].CollectFrame(templates);
                    sampledFrames++;
                }
                if (issues.Count != 0) return FitFailure(package, bindings.Length, issues);
                templates = SplitHeadFitTemplates(templates);

                var shapeIndices = new Dictionary<string, int>(StringComparer.Ordinal);
                int obbCount = 0;
                float worstFalseNegative = 0f;
                float worstDepth = 0f;
                for (int i = 0; i < templates.Count; i++)
                {
                    HurtboxFitTemplate template = templates[i];
                    if (template.Points.Count < 24)
                    {
                        continue;
                    }
                    HurtboxFitShape fit = FitPrimitive(template);
                    if (fit.Kind == "Box") obbCount++;
                    string indexKey = template.Binding.PartId + "|" + fit.Kind.ToLowerInvariant();
                    int shapeIndex = shapeIndices.TryGetValue(indexKey, out int current) ? current : 0;
                    shapeIndices[indexKey] = shapeIndex + 1;
                    string colliderId = "hurt." + SanitizeStableSegment(template.Binding.PartLabel) + "." +
                        (fit.Kind == "Box" ? "obb" : fit.Kind.ToLowerInvariant()) + "." + shapeIndex;
                    proposed.Add(CreateFittedCollider(colliderId, template, fit));
                    reports.Add(new JObject
                    {
                        ["colliderId"] = colliderId,
                        ["sourceColliderId"] = template.Binding.ColliderId,
                        ["sourceColliderIds"] = new JArray(template.SourceColliderIds),
                        ["partId"] = template.Binding.PartId,
                        ["shape"] = fit.Kind,
                        ["coveragePolicy"] = "ConservativeEnvelope",
                        ["framePolicy"] = string.IsNullOrWhiteSpace(fit.FramePolicy)
                            ? GetFitFramePolicy(template)
                            : fit.FramePolicy,
                        ["surfacePaddingMeters"] = fit.SurfacePaddingMeters,
                        ["sampleCount"] = template.Points.Count,
                        ["fitSampleCount"] = fit.FitPointCount > 0 ? fit.FitPointCount : template.Points.Count,
                        ["excludedOutlierPercent"] = fit.ExcludedOutlierPercent,
                        ["falseNegativePercent"] = fit.FalseNegativePercent,
                        ["firstHitDepthP95Meters"] = fit.DepthP95,
                        ["shapeCost"] = fit.Kind == "Sphere" ? 1 : fit.Kind == "Capsule" ? 2 : 4
                    });
                    worstFalseNegative = Mathf.Max(worstFalseNegative, fit.FalseNegativePercent);
                    worstDepth = Mathf.Max(worstDepth, fit.DepthP95);
                }

                if (proposed.Count > HurtboxFitMaximumColliders)
                    issues.Add(FitIssue("HURTBOX_FIT_COLLIDER_BUDGET", string.Empty, string.Empty, "Proposed collider count exceeds 32."));
                if (obbCount > HurtboxFitMaximumObbs)
                    issues.Add(FitIssue("HURTBOX_FIT_OBB_BUDGET", string.Empty, string.Empty, "Proposed OBB count exceeds 8."));
                if (worstFalseNegative > HurtboxFitMaximumFalseNegativePercent)
                    issues.Add(FitIssue("HURTBOX_FIT_FALSE_NEGATIVE_GATE", string.Empty, string.Empty, "At least one fitted collider misses a sampled visible surface. Conservative envelopes require 0% false negatives."));

                return new JObject
                {
                    ["format"] = "mx.character.hurtboxFitDraft.v1",
                    ["packageId"] = package.PackageId,
                    ["sourcePackageHash"] = package.ImportReport?.SourcePackageHash ?? string.Empty,
                    ["status"] = issues.Count == 0 ? "ReviewRequired" : "Blocked",
                    ["sampleRate"] = 60,
                    ["sampledClipCount"] = orderedClips.Count,
                    ["sampledFrameCount"] = sampledFrames,
                    ["colliderCount"] = proposed.Count,
                    ["obbCount"] = obbCount,
                    ["limits"] = new JObject { ["colliders"] = HurtboxFitMaximumColliders, ["obbs"] = HurtboxFitMaximumObbs },
                    ["quality"] = new JObject
                    {
                        ["falseNegativeMaxPercent"] = worstFalseNegative,
                        ["falseNegativeGatePercent"] = HurtboxFitMaximumFalseNegativePercent,
                        ["firstHitDepthP95MaxMeters"] = worstDepth,
                        ["authoringFitPose"] = "IdleFrame0",
                        ["falsePositiveStatus"] = "RequiresSixDirectionReview"
                    },
                    ["colliders"] = proposed,
                    ["reports"] = reports,
                    ["issues"] = issues
                };
            }
            finally
            {
                if (startedAnimationMode) AnimationMode.StopAnimationMode();
                if (rendererSamples != null)
                {
                    for (int i = 0; i < rendererSamples.Count; i++) rendererSamples[i].Dispose();
                }
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static HurtboxFitRendererSamples BuildRendererSamples(
            SkinnedMeshRenderer renderer,
            List<HurtboxFitTemplate> templates,
            JArray issues)
        {
            Mesh mesh = renderer.sharedMesh;
            if (mesh == null) return null;
            Vector2[] uvs = mesh.uv;
            BoneWeight[] weights = mesh.boneWeights;
            if (uvs == null || uvs.Length != mesh.vertexCount || weights == null || weights.Length != mesh.vertexCount)
            {
                issues.Add(FitIssue("HURTBOX_FIT_UV_OR_WEIGHTS_MISSING", renderer.name, string.Empty, "Mesh must expose one UV and BoneWeight entry per vertex."));
                return null;
            }

            Material[] materials = renderer.sharedMaterials;
            var descriptors = new List<HurtboxFitSurfaceSample>();
            int triangleCount = 0;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++) triangleCount += (int)mesh.GetIndexCount(subMesh) / 3;
            int stride = Math.Max(1, triangleCount / HurtboxFitSamplesPerRenderer);
            int ordinal = 0;
            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                int[] triangles = mesh.GetTriangles(subMesh);
                HurtboxFitAlphaSampler alpha = CreateAlphaSampler(materials, subMesh, renderer.name, issues);
                if (alpha.IsBlocked) return null;
                for (int index = 0; index + 2 < triangles.Length; index += 3, ordinal++)
                {
                    if ((ordinal % stride) != 0) continue;
                    int a = triangles[index], b = triangles[index + 1], c = triangles[index + 2];
                    Vector2 uv = (uvs[a] + uvs[b] + uvs[c]) / 3f;
                    if (!alpha.IsVisible(uv)) continue;
                    int templateA = FindDominantVertexTemplate(weights[a], renderer.bones, templates);
                    int templateB = FindDominantVertexTemplate(weights[b], renderer.bones, templates);
                    int templateC = FindDominantVertexTemplate(weights[c], renderer.bones, templates);
                    if (templateA < 0 && templateB < 0 && templateC < 0) continue;
                    descriptors.Add(new HurtboxFitSurfaceSample(a, b, c, templateA, templateB, templateC));
                }
                alpha.Dispose();
            }
            return descriptors.Count == 0 ? null : new HurtboxFitRendererSamples(renderer, descriptors.ToArray());
        }

        private static HurtboxFitAlphaSampler CreateAlphaSampler(Material[] materials, int subMesh, string rendererName, JArray issues)
        {
            Material material = subMesh < materials.Length ? materials[subMesh] : null;
            bool alphaClip = material != null && (material.IsKeywordEnabled("_ALPHATEST_ON") || material.renderQueue == 2450);
            if (!alphaClip) return HurtboxFitAlphaSampler.Opaque;
            Texture2D texture = material.GetTexture("_BaseMap") as Texture2D;
            if (texture == null)
            {
                issues.Add(FitIssue("HURTBOX_FIT_ALPHA_TEXTURE_MISSING", rendererName, string.Empty, "AlphaClip material has no readable _BaseMap texture."));
                return HurtboxFitAlphaSampler.Blocked;
            }
            float cutoff = material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") :
                material.HasProperty("_AlphaCutoff") ? material.GetFloat("_AlphaCutoff") : 0.5f;
            try { return HurtboxFitAlphaSampler.Read(texture, cutoff); }
            catch (Exception ex)
            {
                issues.Add(FitIssue("HURTBOX_FIT_ALPHA_TEXTURE_UNREADABLE", rendererName, string.Empty, ex.Message));
                return HurtboxFitAlphaSampler.Blocked;
            }
        }

        private static List<HurtboxFitTemplate> BuildCoarseConservativeFitTemplates(List<HurtboxFitTemplate> source)
        {
            var result = new List<HurtboxFitTemplate>(source.Count);
            HurtboxFitTemplate head = FindFitTemplate(source, CharacterAuthorityPoseRegionIds.Head);
            HurtboxFitTemplate jaw = FindFitTemplate(source, CharacterAuthorityPoseRegionIds.Jaw);
            HurtboxFitTemplate neck = FindFitTemplate(source, CharacterAuthorityPoseRegionIds.Neck);
            bool mergeHeadAndJaw = CanMergeAsOneCombatRegion(head, jaw);

            for (int i = 0; i < source.Count; i++)
            {
                HurtboxFitTemplate template = source[i];
                if (template == jaw && mergeHeadAndJaw) continue;

                if (template.Binding.PartId == CharacterAuthorityPoseRegionIds.Head)
                {
                    if (template != head) continue;
                    var merged = new HurtboxFitTemplate(template.Binding, template.Parent, HurtboxFitMode.ConservativeHeadRegion);
                    for (int headIndex = 0; headIndex < source.Count; headIndex++)
                    {
                        HurtboxFitTemplate candidate = source[headIndex];
                        if (candidate.Binding.PartId == CharacterAuthorityPoseRegionIds.Head
                            && CanMergeAsOneCombatRegion(template, candidate))
                            merged.AddSource(candidate);
                    }
                    if (mergeHeadAndJaw) merged.AddSource(jaw);
                    result.Add(merged);
                    continue;
                }

                if (template.Binding.PartId == CharacterAuthorityPoseRegionIds.Neck)
                {
                    if (template != neck) continue;
                    var merged = new HurtboxFitTemplate(template.Binding, template.Parent, HurtboxFitMode.ConservativeCapsule);
                    for (int neckIndex = 0; neckIndex < source.Count; neckIndex++)
                    {
                        HurtboxFitTemplate candidate = source[neckIndex];
                        if (candidate.Binding.PartId == CharacterAuthorityPoseRegionIds.Neck
                            && CanMergeAsOneCombatRegion(template, candidate))
                            merged.AddSource(candidate);
                    }
                    result.Add(merged);
                    continue;
                }

                result.Add(template);
            }

            return result;
        }

        private static List<HurtboxFitTemplate> SplitHeadFitTemplates(List<HurtboxFitTemplate> source)
        {
            var result = new List<HurtboxFitTemplate>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                HurtboxFitTemplate template = source[i];
                if (template.Mode != HurtboxFitMode.ConservativeHeadRegion || template.Points.Count < 48)
                {
                    result.Add(template);
                    continue;
                }

                // The head is deliberately one coarse combat region. Curved paired horns cannot be
                // represented by a single capsule without a very large radius, while segmenting
                // them would consume the remaining primitive budget. A deterministic OBB over all
                // visible head/jaw/horn samples is both cheaper and a better conservative envelope.
                var core = new HurtboxFitTemplate(template.Binding, template.Parent, HurtboxFitMode.ConservativeHeadCoreObb);
                core.AddSource(template);
                for (int pointIndex = 0; pointIndex < template.Points.Count; pointIndex++)
                    core.AddPoint(template.Points[pointIndex]);
                result.Add(core);
            }
            return result;
        }

        private static HurtboxFitTemplate FindFitTemplate(List<HurtboxFitTemplate> templates, int partId)
        {
            for (int i = 0; i < templates.Count; i++)
                if (templates[i].Binding.PartId == partId) return templates[i];
            return null;
        }

        private static bool CanMergeAsOneCombatRegion(HurtboxFitTemplate left, HurtboxFitTemplate right)
        {
            if (left == null || right == null) return false;
            CharacterBodyColliderRuntimeBinding a = left.Binding;
            CharacterBodyColliderRuntimeBinding b = right.Binding;
            return a.IsWeakPoint == b.IsWeakPoint
                && Mathf.Approximately(a.DamageMultiplierOverride, b.DamageMultiplierOverride)
                && Mathf.Approximately(a.ImpactPowerScaleOverride, b.ImpactPowerScaleOverride)
                && string.Equals(a.PhysicsLayer, b.PhysicsLayer, StringComparison.Ordinal)
                && string.Equals(a.MaterialStableId, b.MaterialStableId, StringComparison.Ordinal);
        }

        private static HurtboxFitShape FitPrimitive(HurtboxFitTemplate template)
        {
            if (template.Mode == HurtboxFitMode.ConservativeHeadRegion)
                return FitHeadBoneFrameObb(template);
            if (template.Mode == HurtboxFitMode.ConservativeHeadCoreObb)
                return FitHeadCoreObb(template);
            if (template.Mode == HurtboxFitMode.ConservativeCapsule)
                return FitCapsule(template.Points);

            // Wing authoring is driven by the articulated leading-edge chain. PCA is useful for
            // measuring a cross-section, but it is not an anatomical frame: its axes can swap or
            // flip and a curved membrane can rotate the longest axis away from the wing bones.
            // Keep this first slice deliberately limited to the left wing so the right wing remains
            // an unchanged visual control in CharacterStudio.
            if (template.Binding.PartId == CharacterAuthorityPoseRegionIds.LeftWingRoot)
                return FitBoneAlignedCapsule(template);
            if (template.Binding.PartId == CharacterAuthorityPoseRegionIds.LeftWingMembrane)
                return FitBoneAlignedWingObb(template);
            string requested = template.Binding.Shape ?? string.Empty;
            if (string.Equals(requested, "Sphere", StringComparison.OrdinalIgnoreCase)) return FitSphere(template.Points);
            if (string.Equals(requested, "Box", StringComparison.OrdinalIgnoreCase) || string.Equals(requested, "Obb", StringComparison.OrdinalIgnoreCase)) return FitObb(template.Points);
            return FitCapsule(template.Points);
        }

        private static HurtboxFitShape FitBoneAlignedCapsule(HurtboxFitTemplate template)
        {
            if (!TryGetPrimaryBoneSegment(template.Parent, out Vector3 axis, out _))
                return FitCapsule(template.Points);

            List<Vector3> points = template.Points;
            Vector3 mean = Mean(points);
            Vector3 transverseCenter = mean - axis * Vector3.Dot(mean, axis);
            var axial = new float[points.Count];
            var radial = new float[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                axial[i] = Vector3.Dot(points[i], axis);
                radial[i] = Vector3.ProjectOnPlane(points[i] - transverseCenter, axis).magnitude;
            }

            // Use the bone link only as the stable direction. The robust axial interval still
            // comes from the visible weighted surface so thick joints remain covered; forcing the
            // endpoints to the raw bone length would cut away legitimate skin around both joints.
            float min = Min(axial);
            float max = Max(axial);
            if (max < min) { float swap = min; min = max; max = swap; }
            float padding = ComputeConservativePadding(points);
            float radius = Mathf.Max(0.01f, Max(radial) + padding);
            Vector3 center = transverseCenter + axis * ((min + max) * 0.5f);
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, axis);
            float segment = Mathf.Max(0f, max - min) + padding * 2f;
            HurtboxFitShape shape = new HurtboxFitShape("Capsule", center, rotation, Vector3.zero, radius, segment + radius * 2f);
            shape.SurfacePaddingMeters = padding;
            return Evaluate(shape, points);
        }

        private static HurtboxFitShape FitBoneAlignedWingObb(HurtboxFitTemplate template)
        {
            if (!TryGetPrimaryBoneSegment(template.Parent, out Vector3 chainAxis, out _))
                return FitObb(template.Points);

            List<Vector3> points = template.Points;
            Vector3 origin = Mean(points);

            // The membrane hangs away from its leading-edge bone. That signed offset supplies a
            // stable in-plane chord direction; covariance is only a fallback when the centroid is
            // almost exactly on the bone axis. This removes PCA's axis-order and sign ambiguity.
            Vector3 chordAxis = Vector3.ProjectOnPlane(origin, chainAxis);
            if (chordAxis.sqrMagnitude < 1e-8f)
                chordAxis = ComputeConstrainedSecondaryAxis(points, origin, chainAxis);
            if (chordAxis.sqrMagnitude < 1e-8f)
                chordAxis = StablePerpendicular(chainAxis);
            chordAxis.Normalize();

            Vector3 normalAxis = Vector3.Cross(chainAxis, chordAxis).normalized;
            chordAxis = Vector3.Cross(normalAxis, chainAxis).normalized;
            return Evaluate(FitObbAxes(points, origin, chainAxis, chordAxis, normalAxis), points);
        }

        private static HurtboxFitShape FitHeadBoneFrameObb(HurtboxFitTemplate template)
        {
            List<Vector3> points = template.Points;
            if (points.Count < 24) return FitObb(points);

            // PCA is deliberately not used to establish the head direction: long horns dominate
            // covariance and can swap or flip axes between visually equivalent meshes. Instead the
            // stable anatomical frame is captured from neck -> head -> mouth before animation
            // sampling. A bounded search may refine that frame, but can never rotate far enough to
            // lose the meaning of "muzzle to occiput".
            ResolveHeadAnatomicalFrame(template, out Vector3 baseForward, out Vector3 baseUp);
            Vector3 baseSide = Vector3.Cross(baseForward, baseUp).normalized;
            Vector3 origin = Mean(points);
            HurtboxFitShape anatomicalShape = default;
            float bestScore = float.MaxValue;
            float bestDeviation = float.MaxValue;
            for (int yaw = -15; yaw <= 15; yaw += 5)
            {
                for (int pitch = -15; pitch <= 15; pitch += 5)
                {
                    Quaternion directionAdjustment = Quaternion.AngleAxis(yaw, baseUp)
                        * Quaternion.AngleAxis(pitch, baseSide);
                    Vector3 forward = (directionAdjustment * baseForward).normalized;
                    Vector3 unrolledUp = Vector3.ProjectOnPlane(baseUp, forward);
                    if (unrolledUp.sqrMagnitude < 1e-8f) continue;
                    unrolledUp.Normalize();
                    for (int roll = -30; roll <= 30; roll += 5)
                    {
                        Vector3 up = (Quaternion.AngleAxis(roll, forward) * unrolledUp).normalized;
                        Vector3 side = Vector3.Cross(forward, up).normalized;
                        up = Vector3.Cross(side, forward).normalized;
                        HurtboxFitShape candidate = FitObbAxes(points, origin, forward, up, side);
                        float volume = candidate.Size.x * candidate.Size.y * candidate.Size.z;
                        float deviation = Mathf.Abs(yaw) + Mathf.Abs(pitch) + Mathf.Abs(roll) * 0.25f;
                        float score = volume * (1f + deviation * 0.0005f);
                        if (score < bestScore - 0.000001f
                            || (Mathf.Abs(score - bestScore) <= 0.000001f && deviation < bestDeviation))
                        {
                            anatomicalShape = candidate;
                            bestScore = score;
                            bestDeviation = deviation;
                        }
                    }
                }
            }
            if (bestScore == float.MaxValue)
                anatomicalShape = FitObbAxes(points, origin, baseForward, baseUp, baseSide);

            HurtboxFitShape pcaShape = FitHeadStablePcaObb(points, baseForward, baseUp);
            float anatomicalVolume = ObbVolume(anatomicalShape);
            float pcaVolume = ObbVolume(pcaShape);
            HurtboxFitShape shape = pcaVolume < anatomicalVolume * 0.98f
                ? pcaShape
                : anatomicalShape;
            shape.FramePolicy = pcaVolume < anatomicalVolume * 0.98f
                ? "HeadStablePcaMinimumVolume"
                : "HeadNeckMouthConstrained";
            shape.FitPointCount = points.Count;
            shape.ExcludedOutlierPercent = 0f;
            return Evaluate(shape, points);
        }

        private static HurtboxFitShape FitHeadCoreObb(HurtboxFitTemplate template)
        {
            ResolveHeadAnatomicalFrame(template, out Vector3 forward, out Vector3 up);
            HurtboxFitShape shape = FitHeadSupportFrameObb(template.Points, forward, up);
            shape.FramePolicy = "HeadMuzzleCrownConstrainedMinimumObb";
            return Evaluate(shape, template.Points);
        }

        private static HurtboxFitShape FitHeadSupportFrameObb(
            List<Vector3> points,
            Vector3 anatomicalForward,
            Vector3 anatomicalUp)
        {
            Vector3 origin = Mean(points);
            Vector3 anatomicalSide = Vector3.Cross(anatomicalForward, anatomicalUp);
            if (anatomicalSide.sqrMagnitude < 1e-8f) anatomicalSide = StablePerpendicular(anatomicalForward);
            anatomicalSide.Normalize();

            // A creature head with swept horns has a stable visual diagonal from the foremost
            // muzzle support point to the highest crown support point. PCA does not preserve this
            // semantic axis: symmetric horns can make the covariance axis almost vertical even
            // when a diagonally oriented box is much tighter in screen space and volume.
            Vector3 muzzle = ExtremePoint(points, anatomicalForward);
            Vector3 crown = ExtremePoint(points, anatomicalUp);
            Vector3 longAxis = Vector3.ProjectOnPlane(crown - muzzle, anatomicalSide);
            if (longAxis.sqrMagnitude < 1e-8f) longAxis = anatomicalForward;
            longAxis.Normalize();
            if (Vector3.Dot(longAxis, anatomicalUp) < 0f) longAxis = -longAxis;

            Vector3 baseSide = Vector3.ProjectOnPlane(anatomicalSide, longAxis);
            if (baseSide.sqrMagnitude < 1e-8f) baseSide = StablePerpendicular(longAxis);
            baseSide.Normalize();
            Vector3 baseUp = Vector3.Cross(baseSide, longAxis).normalized;
            if (Vector3.Dot(baseUp, anatomicalUp) < 0f)
            {
                baseSide = -baseSide;
                baseUp = -baseUp;
            }

            HurtboxFitShape best = default;
            float bestScore = float.MaxValue;
            float bestDeviation = float.MaxValue;
            for (int yaw = -25; yaw <= 25; yaw += 5)
            {
                for (int pitch = -25; pitch <= 25; pitch += 5)
                {
                    Quaternion directionAdjustment = Quaternion.AngleAxis(yaw, baseUp)
                        * Quaternion.AngleAxis(pitch, baseSide);
                    Vector3 candidateLong = (directionAdjustment * longAxis).normalized;
                    Vector3 candidateUpSeed = Vector3.ProjectOnPlane(baseUp, candidateLong);
                    if (candidateUpSeed.sqrMagnitude < 1e-8f) continue;
                    candidateUpSeed.Normalize();
                    for (int roll = -20; roll <= 20; roll += 5)
                    {
                        Vector3 candidateUp = (Quaternion.AngleAxis(roll, candidateLong) * candidateUpSeed).normalized;
                        Vector3 candidateSide = Vector3.Cross(candidateLong, candidateUp).normalized;
                        candidateUp = Vector3.Cross(candidateSide, candidateLong).normalized;
                        HurtboxFitShape candidate = FitHeadObbAxes(
                            points,
                            origin,
                            candidateLong,
                            candidateUp,
                            candidateSide);
                        float deviation = Mathf.Abs(yaw) + Mathf.Abs(pitch) + Mathf.Abs(roll) * 0.5f;
                        float score = ObbVolume(candidate) * (1f + deviation * 0.0005f);
                        if (score < bestScore - 0.000001f
                            || (Mathf.Abs(score - bestScore) <= 0.000001f && deviation < bestDeviation))
                        {
                            best = candidate;
                            bestScore = score;
                            bestDeviation = deviation;
                        }
                    }
                }
            }

            if (bestScore == float.MaxValue)
                best = FitHeadObbAxes(points, origin, longAxis, baseUp, baseSide);
            best.FitPointCount = points.Count;
            best.ExcludedOutlierPercent = 0f;
            return best;
        }

        private static HurtboxFitShape FitHeadObbAxes(
            List<Vector3> points,
            Vector3 origin,
            Vector3 x,
            Vector3 y,
            Vector3 z)
        {
            HurtboxFitShape shape = FitObbAxes(points, origin, x, y, z);
            const float headPaddingMeters = 0.03f;
            float reductionPerSide = Mathf.Max(0f, shape.SurfacePaddingMeters - headPaddingMeters);
            shape.Size = Vector3.Max(
                Vector3.one * (headPaddingMeters * 2f),
                shape.Size - Vector3.one * (reductionPerSide * 2f));
            shape.SurfacePaddingMeters = headPaddingMeters;
            return shape;
        }

        private static Vector3 ExtremePoint(List<Vector3> points, Vector3 axis)
        {
            Vector3 result = points[0];
            float bestProjection = Vector3.Dot(result, axis);
            for (int i = 1; i < points.Count; i++)
            {
                float projection = Vector3.Dot(points[i], axis);
                if (projection > bestProjection)
                {
                    result = points[i];
                    bestProjection = projection;
                }
            }
            return result;
        }

        private static HurtboxFitShape FitHeadStablePcaObb(
            List<Vector3> points,
            Vector3 anatomicalForward,
            Vector3 anatomicalUp)
        {
            ComputePca(points, out Vector3 center, out Vector3 first, out Vector3 second, out _, out _);
            Vector3 third = Vector3.Cross(first, second).normalized;
            second = Vector3.Cross(third, first).normalized;
            var axes = new[] { first, second, third };

            int forwardIndex = 0;
            float forwardAlignment = Mathf.Abs(Vector3.Dot(axes[0], anatomicalForward));
            for (int i = 1; i < axes.Length; i++)
            {
                float alignment = Mathf.Abs(Vector3.Dot(axes[i], anatomicalForward));
                if (alignment > forwardAlignment)
                {
                    forwardIndex = i;
                    forwardAlignment = alignment;
                }
            }

            Vector3 forward = axes[forwardIndex];
            if (Vector3.Dot(forward, anatomicalForward) < 0f) forward = -forward;
            Vector3 upCandidateA = axes[(forwardIndex + 1) % 3];
            Vector3 upCandidateB = axes[(forwardIndex + 2) % 3];
            Vector3 up = Mathf.Abs(Vector3.Dot(upCandidateA, anatomicalUp))
                >= Mathf.Abs(Vector3.Dot(upCandidateB, anatomicalUp))
                ? upCandidateA
                : upCandidateB;
            if (Vector3.Dot(up, anatomicalUp) < 0f) up = -up;
            up = Vector3.ProjectOnPlane(up, forward).normalized;
            Vector3 side = Vector3.Cross(forward, up).normalized;
            up = Vector3.Cross(side, forward).normalized;

            HurtboxFitShape best = default;
            float bestVolume = float.MaxValue;
            for (int yaw = -12; yaw <= 12; yaw += 3)
            {
                for (int pitch = -12; pitch <= 12; pitch += 3)
                {
                    Quaternion adjustment = Quaternion.AngleAxis(yaw, up)
                        * Quaternion.AngleAxis(pitch, side);
                    Vector3 candidateForward = (adjustment * forward).normalized;
                    Vector3 candidateUpSeed = Vector3.ProjectOnPlane(up, candidateForward);
                    if (candidateUpSeed.sqrMagnitude < 1e-8f) continue;
                    candidateUpSeed.Normalize();
                    for (int roll = -18; roll <= 18; roll += 3)
                    {
                        Vector3 candidateUp = (Quaternion.AngleAxis(roll, candidateForward) * candidateUpSeed).normalized;
                        Vector3 candidateSide = Vector3.Cross(candidateForward, candidateUp).normalized;
                        candidateUp = Vector3.Cross(candidateSide, candidateForward).normalized;
                        HurtboxFitShape candidate = FitObbAxes(
                            points,
                            center,
                            candidateForward,
                            candidateUp,
                            candidateSide);
                        float volume = ObbVolume(candidate);
                        if (volume < bestVolume)
                        {
                            best = candidate;
                            bestVolume = volume;
                        }
                    }
                }
            }
            return bestVolume < float.MaxValue
                ? best
                : FitObbAxes(points, center, forward, up, side);
        }

        private static float ObbVolume(HurtboxFitShape shape) =>
            Mathf.Max(0f, shape.Size.x) * Mathf.Max(0f, shape.Size.y) * Mathf.Max(0f, shape.Size.z);

        private static void ResolveHeadAnatomicalFrame(
            HurtboxFitTemplate template,
            out Vector3 forward,
            out Vector3 up)
        {
            forward = template.AnatomicalForwardAxis;
            up = template.AnatomicalUpAxis;
            if (forward.sqrMagnitude < 1e-8f) forward = -Vector3.right;
            forward.Normalize();
            up = Vector3.ProjectOnPlane(up, forward);
            if (up.sqrMagnitude < 1e-8f) up = StablePerpendicular(forward);
            up.Normalize();
        }

        private static string GetFitFramePolicy(HurtboxFitTemplate template)
        {
            if (template.Mode == HurtboxFitMode.ConservativeHeadRegion)
                return "HeadRegionUnsplitFallback";
            if (template.Mode == HurtboxFitMode.ConservativeHeadCoreObb)
                return "HeadMuzzleCrownConstrainedMinimumObb";
            if (template.Mode == HurtboxFitMode.ConservativeCapsule)
                return "MergedSurfacePca";
            return "ShapeDefault";
        }

        private static bool TryGetPrimaryBoneSegment(Transform parent, out Vector3 axis, out float length)
        {
            axis = Vector3.zero;
            length = 0f;
            if (parent == null) return false;

            Transform child = null;
            int parentOrdinal = ParseWingBoneOrdinal(parent.name);
            int bestOrdinal = int.MaxValue;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform candidate = parent.GetChild(i);
                int candidateOrdinal = ParseWingBoneOrdinal(candidate.name);
                if (candidateOrdinal <= parentOrdinal || candidateOrdinal >= bestOrdinal) continue;
                child = candidate;
                bestOrdinal = candidateOrdinal;
            }
            if (child == null) return false;

            Vector3 delta = parent.InverseTransformPoint(child.position);
            length = delta.magnitude;
            if (length < 1e-5f) return false;
            axis = delta / length;
            return true;
        }

        private static int ParseWingBoneOrdinal(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return -1;
            int marker = name.LastIndexOf("_wing_", StringComparison.OrdinalIgnoreCase);
            if (marker < 0) return -1;
            return int.TryParse(name.Substring(marker + 6), out int ordinal) ? ordinal : -1;
        }

        private static Vector3 ComputeConstrainedSecondaryAxis(List<Vector3> points, Vector3 center, Vector3 reject)
        {
            Vector3 value = StablePerpendicular(reject);
            for (int iteration = 0; iteration < 16; iteration++)
            {
                Vector3 next = Vector3.zero;
                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 delta = Vector3.ProjectOnPlane(points[i] - center, reject);
                    next += delta * Vector3.Dot(delta, value);
                }
                next = Vector3.ProjectOnPlane(next, reject);
                if (next.sqrMagnitude < 1e-10f) break;
                value = next.normalized;
            }
            return value;
        }

        private static Vector3 StablePerpendicular(Vector3 axis)
        {
            Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.9f ? Vector3.up : Vector3.forward;
            return Vector3.ProjectOnPlane(reference, axis).normalized;
        }

        private static HurtboxFitShape FitSphere(List<Vector3> points)
        {
            Vector3 center = Mean(points);
            var distances = new float[points.Count];
            for (int i = 0; i < points.Count; i++) distances[i] = Vector3.Distance(points[i], center);
            float padding = ComputeConservativePadding(points);
            HurtboxFitShape shape = new HurtboxFitShape("Sphere", center, Quaternion.identity, Vector3.zero, Max(distances) + padding, 0f);
            shape.SurfacePaddingMeters = padding;
            return Evaluate(shape, points);
        }

        private static HurtboxFitShape FitCapsule(List<Vector3> points)
        {
            ComputePca(points, out Vector3 center, out Vector3 axis, out _, out _, out _);
            var axial = new float[points.Count];
            var radial = new float[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 delta = points[i] - center;
                axial[i] = Vector3.Dot(delta, axis);
                radial[i] = (delta - axis * axial[i]).magnitude;
            }
            float min = Min(axial);
            float max = Max(axial);
            float padding = ComputeConservativePadding(points);
            float radius = Mathf.Max(0.01f, Max(radial) + padding);
            Vector3 fittedCenter = center + axis * ((min + max) * 0.5f);
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, axis);
            float segment = Mathf.Max(0f, max - min) + padding * 2f;
            HurtboxFitShape shape = new HurtboxFitShape("Capsule", fittedCenter, rotation, Vector3.zero, radius, segment + radius * 2f);
            shape.SurfacePaddingMeters = padding;
            return Evaluate(shape, points);
        }

        private static HurtboxFitShape FitObb(List<Vector3> points)
        {
            ComputePca(points, out Vector3 center, out Vector3 axisX, out Vector3 axisY, out _, out _);
            Vector3 axisZ = Vector3.Cross(axisX, axisY).normalized;
            axisY = Vector3.Cross(axisZ, axisX).normalized;
            HurtboxFitShape best = default;
            float bestVolume = float.MaxValue;
            for (int angle = -12; angle <= 12; angle += 3)
            {
                Quaternion spin = Quaternion.AngleAxis(angle, axisX);
                Vector3 y = spin * axisY;
                Vector3 z = spin * axisZ;
                HurtboxFitShape candidate = FitObbAxes(points, center, axisX, y, z);
                float volume = candidate.Size.x * candidate.Size.y * candidate.Size.z;
                if (volume < bestVolume) { best = candidate; bestVolume = volume; }
            }
            return Evaluate(best, points);
        }

        private static HurtboxFitShape FitObbAxes(List<Vector3> points, Vector3 origin, Vector3 x, Vector3 y, Vector3 z)
        {
            var px = new float[points.Count]; var py = new float[points.Count]; var pz = new float[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 delta = points[i] - origin;
                px[i] = Vector3.Dot(delta, x); py[i] = Vector3.Dot(delta, y); pz[i] = Vector3.Dot(delta, z);
            }
            float minX = Min(px), maxX = Max(px);
            float minY = Min(py), maxY = Max(py);
            float minZ = Min(pz), maxZ = Max(pz);
            float padding = ComputeConservativePadding(points);
            Vector3 center = origin + x * ((minX + maxX) * 0.5f) + y * ((minY + maxY) * 0.5f) + z * ((minZ + maxZ) * 0.5f);
            Quaternion rotation = Quaternion.LookRotation(z, y);
            HurtboxFitShape shape = new HurtboxFitShape(
                "Box",
                center,
                rotation,
                new Vector3(maxX - minX, maxY - minY, maxZ - minZ) + Vector3.one * (padding * 2f),
                0f,
                0f);
            shape.SurfacePaddingMeters = padding;
            return shape;
        }

        private static HurtboxFitShape Evaluate(HurtboxFitShape shape, List<Vector3> points)
        {
            var outside = new float[points.Count];
            int misses = 0;
            Quaternion inverse = Quaternion.Inverse(shape.Rotation);
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 local = inverse * (points[i] - shape.Center);
                float distance;
                if (shape.Kind == "Sphere") distance = local.magnitude - shape.Radius;
                else if (shape.Kind == "Capsule")
                {
                    float halfSegment = Mathf.Max(0f, shape.Height * 0.5f - shape.Radius);
                    Vector3 closest = new Vector3(0f, Mathf.Clamp(local.y, -halfSegment, halfSegment), 0f);
                    distance = Vector3.Distance(local, closest) - shape.Radius;
                }
                else
                {
                    Vector3 q = Abs(local) - shape.Size * 0.5f;
                    distance = new Vector3(Mathf.Max(0f, q.x), Mathf.Max(0f, q.y), Mathf.Max(0f, q.z)).magnitude;
                }
                outside[i] = Mathf.Max(0f, distance);
                if (distance > 0.002f) misses++;
            }
            shape.FalseNegativePercent = misses * 100f / Math.Max(1, points.Count);
            shape.DepthP95 = Percentile(outside, 0.95f);
            return shape;
        }

        private static JObject CreateFittedCollider(string colliderId, HurtboxFitTemplate template, HurtboxFitShape fit)
        {
            CharacterBodyColliderRuntimeBinding source = template.Binding;
            return new JObject
            {
                ["colliderId"] = colliderId,
                ["partId"] = source.PartId,
                ["hitZoneId"] = source.HitZoneId,
                ["shape"] = fit.Kind,
                ["localPose"] = new JObject
                {
                    ["parentKind"] = source.LocalPose.ParentKind,
                    ["parentPath"] = source.LocalPose.ParentPath,
                    ["position"] = VectorJson(fit.Center),
                    ["rotation"] = QuaternionJson(fit.Rotation),
                    ["scale"] = VectorJson(Vector3.one),
                    ["eulerHint"] = VectorJson(fit.Rotation.eulerAngles)
                },
                ["size"] = VectorJson(fit.Size),
                ["radius"] = fit.Radius,
                ["height"] = fit.Height,
                ["priority"] = source.Priority,
                ["isWeakPoint"] = source.IsWeakPoint,
                ["damageMultiplierOverride"] = source.IsWeakPoint ? 1.25f : (source.DamageMultiplierOverride > 0f ? source.DamageMultiplierOverride : 1f),
                ["impactPowerScaleOverride"] = source.ImpactPowerScaleOverride > 0f ? source.ImpactPowerScaleOverride : 1f,
                ["physicsLayer"] = string.IsNullOrWhiteSpace(source.PhysicsLayer) ? "CharacterHurtbox" : source.PhysicsLayer,
                ["materialStableId"] = source.MaterialStableId
            };
        }

        private static void ComputePca(List<Vector3> points, out Vector3 center, out Vector3 first, out Vector3 second, out float major, out float minor)
        {
            center = Mean(points);
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 d = points[i] - center;
                xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
                yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
            }
            first = PowerAxis(xx, xy, xz, yy, yz, zz, new Vector3(1f, 0.5f, 0.25f), Vector3.zero);
            second = PowerAxis(xx, xy, xz, yy, yz, zz, new Vector3(0.25f, 1f, 0.5f), first);
            major = Variance(points, center, first);
            minor = Variance(points, center, second);
        }

        private static Vector3 PowerAxis(float xx, float xy, float xz, float yy, float yz, float zz, Vector3 seed, Vector3 reject)
        {
            Vector3 value = seed.normalized;
            for (int i = 0; i < 16; i++)
            {
                value = new Vector3(xx * value.x + xy * value.y + xz * value.z, xy * value.x + yy * value.y + yz * value.z, xz * value.x + yz * value.y + zz * value.z);
                if (reject.sqrMagnitude > 0f) value -= reject * Vector3.Dot(value, reject);
                if (value.sqrMagnitude < 1e-10f) value = Vector3.Cross(reject.sqrMagnitude > 0f ? reject : Vector3.up, Vector3.forward);
                value.Normalize();
            }
            return value;
        }

        private static float Variance(List<Vector3> points, Vector3 center, Vector3 axis)
        {
            float value = 0f;
            for (int i = 0; i < points.Count; i++) { float d = Vector3.Dot(points[i] - center, axis); value += d * d; }
            return value / Math.Max(1, points.Count);
        }

        private static Vector3 Mean(List<Vector3> points)
        {
            Vector3 value = Vector3.zero;
            for (int i = 0; i < points.Count; i++) value += points[i];
            return value / Math.Max(1, points.Count);
        }

        private static Vector3 CoordinateMedian(List<Vector3> points)
        {
            var x = new float[points.Count];
            var y = new float[points.Count];
            var z = new float[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                x[i] = points[i].x;
                y[i] = points[i].y;
                z[i] = points[i].z;
            }
            return new Vector3(Percentile(x, 0.5f), Percentile(y, 0.5f), Percentile(z, 0.5f));
        }

        private static float ComputeConservativePadding(List<Vector3> points)
        {
            if (points == null || points.Count == 0) return 0.03f;
            Vector3 min = points[0];
            Vector3 max = points[0];
            for (int i = 1; i < points.Count; i++)
            {
                min = Vector3.Min(min, points[i]);
                max = Vector3.Max(max, points[i]);
            }
            float longestDimension = Mathf.Max(max.x - min.x, Mathf.Max(max.y - min.y, max.z - min.z));
            return Mathf.Clamp(longestDimension * 0.05f, 0.03f, 0.12f);
        }

        private static float Min(float[] values)
        {
            if (values == null || values.Length == 0) return 0f;
            float result = values[0];
            for (int i = 1; i < values.Length; i++) result = Mathf.Min(result, values[i]);
            return result;
        }

        private static float Max(float[] values)
        {
            if (values == null || values.Length == 0) return 0f;
            float result = values[0];
            for (int i = 1; i < values.Length; i++) result = Mathf.Max(result, values[i]);
            return result;
        }

        private static float Percentile(float[] values, float percentile)
        {
            Array.Sort(values);
            if (values.Length == 0) return 0f;
            int index = Mathf.Clamp(Mathf.RoundToInt((values.Length - 1) * percentile), 0, values.Length - 1);
            return values[index];
        }

        private static int FindBestTemplate(Transform bone, List<HurtboxFitTemplate> templates)
        {
            int best = -1, bestDepth = -1;
            for (int i = 0; i < templates.Count; i++)
            {
                Transform parent = templates[i].Parent;
                if (bone != parent && !bone.IsChildOf(parent)) continue;
                int depth = TransformDepth(parent);
                if (depth > bestDepth
                    || (depth == bestDepth && best >= 0 && PreferSurfaceTemplate(templates[i], templates[best])))
                {
                    best = i;
                    bestDepth = depth;
                }
            }
            return best;
        }

        private static int FindDominantVertexTemplate(
            BoneWeight weight,
            Transform[] bones,
            List<HurtboxFitTemplate> templates)
        {
            int boneIndex = DominantBoneIndex(weight);
            return boneIndex < 0 || boneIndex >= bones.Length
                ? -1
                : FindBestTemplate(bones[boneIndex], templates);
        }

        private static int CollectWeightedTemplateIndices(
            BoneWeight[] weights,
            int a,
            int b,
            int c,
            Transform[] bones,
            List<HurtboxFitTemplate> templates,
            int[] results)
        {
            int count = 0;
            count = AddWeightedVertexTemplates(weights[a], bones, templates, results, count);
            count = AddWeightedVertexTemplates(weights[b], bones, templates, results, count);
            count = AddWeightedVertexTemplates(weights[c], bones, templates, results, count);
            if (count != 0) return count;

            int vertex = DominantWeightedVertex(weights, a, b, c);
            int boneIndex = DominantBoneIndex(weights[vertex]);
            if (boneIndex < 0 || boneIndex >= bones.Length) return 0;
            int templateIndex = FindBestTemplate(bones[boneIndex], templates);
            if (templateIndex >= 0) results[count++] = templateIndex;
            return count;
        }

        private static int AddWeightedVertexTemplates(
            BoneWeight weight,
            Transform[] bones,
            List<HurtboxFitTemplate> templates,
            int[] results,
            int count)
        {
            count = AddWeightedBoneTemplate(weight.boneIndex0, weight.weight0, bones, templates, results, count);
            count = AddWeightedBoneTemplate(weight.boneIndex1, weight.weight1, bones, templates, results, count);
            count = AddWeightedBoneTemplate(weight.boneIndex2, weight.weight2, bones, templates, results, count);
            return AddWeightedBoneTemplate(weight.boneIndex3, weight.weight3, bones, templates, results, count);
        }

        private static int AddWeightedBoneTemplate(
            int boneIndex,
            float weight,
            Transform[] bones,
            List<HurtboxFitTemplate> templates,
            int[] results,
            int count)
        {
            if (weight < 0.15f || boneIndex < 0 || boneIndex >= bones.Length) return count;
            int templateIndex = FindBestTemplate(bones[boneIndex], templates);
            if (templateIndex < 0) return count;
            for (int i = 0; i < count; i++) if (results[i] == templateIndex) return count;
            if (count < results.Length) results[count++] = templateIndex;
            return count;
        }

        private static bool PreferSurfaceTemplate(HurtboxFitTemplate candidate, HurtboxFitTemplate current)
        {
            bool candidateBox = string.Equals(candidate.Binding.Shape, "Box", StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.Binding.Shape, "Obb", StringComparison.OrdinalIgnoreCase);
            bool currentBox = string.Equals(current.Binding.Shape, "Box", StringComparison.OrdinalIgnoreCase)
                || string.Equals(current.Binding.Shape, "Obb", StringComparison.OrdinalIgnoreCase);
            if (candidateBox != currentBox) return candidateBox;
            return string.CompareOrdinal(candidate.Binding.PartLabel, current.Binding.PartLabel) < 0;
        }

        private static int TransformDepth(Transform value) { int depth = 0; while (value != null) { depth++; value = value.parent; } return depth; }
        private static int DominantWeightedVertex(BoneWeight[] weights, int a, int b, int c) => MaxWeight(weights[a]) >= MaxWeight(weights[b]) ? (MaxWeight(weights[a]) >= MaxWeight(weights[c]) ? a : c) : (MaxWeight(weights[b]) >= MaxWeight(weights[c]) ? b : c);
        private static float MaxWeight(BoneWeight value) => Mathf.Max(value.weight0, Mathf.Max(value.weight1, Mathf.Max(value.weight2, value.weight3)));
        private static int DominantBoneIndex(BoneWeight value) { float max = value.weight0; int index = value.boneIndex0; if (value.weight1 > max) { max = value.weight1; index = value.boneIndex1; } if (value.weight2 > max) { max = value.weight2; index = value.boneIndex2; } if (value.weight3 > max) index = value.boneIndex3; return index; }
        private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        private static string SanitizeStableSegment(string value) => string.IsNullOrWhiteSpace(value) ? "part" : value.Trim().ToLowerInvariant().Replace(' ', '_');
        private static JObject VectorJson(Vector3 value) => new JObject { ["x"] = Round(value.x), ["y"] = Round(value.y), ["z"] = Round(value.z) };
        private static JObject QuaternionJson(Quaternion value) => new JObject { ["x"] = Round(value.x), ["y"] = Round(value.y), ["z"] = Round(value.z), ["w"] = Round(value.w) };
        private static float Round(float value) => (float)Math.Round(value, 5, MidpointRounding.AwayFromZero);
        private static JObject FitIssue(string code, string colliderId, string clipKey, string message) => new JObject { ["code"] = code, ["colliderId"] = colliderId ?? string.Empty, ["clipKey"] = clipKey ?? string.Empty, ["message"] = message };
        private static JObject FitFailure(CharacterImportedPackage package, int colliderCount, JArray issues) => new JObject { ["format"] = "mx.character.hurtboxFitDraft.v1", ["packageId"] = package?.PackageId ?? string.Empty, ["status"] = "Blocked", ["colliderCount"] = colliderCount, ["colliders"] = new JArray(), ["reports"] = new JArray(), ["issues"] = issues };

        private static string ResolveAuthoringPackageRoot(string packageId)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            string direct = Path.Combine(projectRoot, "Authoring", "CharacterPackages", "character-" + (packageId ?? string.Empty));
            if (Directory.Exists(direct)) return direct;
            string root = Path.Combine(projectRoot, "Authoring", "CharacterPackages");
            if (!Directory.Exists(root)) return string.Empty;
            string[] manifests = Directory.GetFiles(root, "manifest.json", SearchOption.AllDirectories);
            for (int i = 0; i < manifests.Length; i++)
            {
                try { if (string.Equals((string)JObject.Parse(File.ReadAllText(manifests[i]))["packageId"], packageId, StringComparison.Ordinal)) return Path.GetDirectoryName(manifests[i]); }
                catch (JsonException) { }
            }
            return string.Empty;
        }

        private enum HurtboxFitMode
        {
            Default,
            ConservativeHeadRegion,
            ConservativeHeadCoreObb,
            ConservativeCapsule
        }

        private sealed class HurtboxFitTemplate
        {
            private long _seenPointCount;
            public HurtboxFitTemplate(
                CharacterBodyColliderRuntimeBinding binding,
                Transform parent,
                HurtboxFitMode mode = HurtboxFitMode.Default)
            {
                Binding = binding;
                Parent = parent;
                Mode = mode;
                Points = new List<Vector3>(HurtboxFitPointCapacityPerCollider);
                SourceColliderIds = new List<string> { binding?.ColliderLabel ?? string.Empty };
                CaptureAnatomicalFrame(parent, out Vector3 anatomicalForward, out Vector3 anatomicalUp);
                AnatomicalForwardAxis = anatomicalForward;
                AnatomicalUpAxis = anatomicalUp;
            }
            public CharacterBodyColliderRuntimeBinding Binding { get; }
            public Transform Parent { get; }
            public HurtboxFitMode Mode { get; }
            public List<Vector3> Points { get; }
            public List<string> SourceColliderIds { get; }
            public Vector3 AnatomicalForwardAxis { get; }
            public Vector3 AnatomicalUpAxis { get; }
            public void AddSource(HurtboxFitTemplate source)
            {
                if (source == null) return;
                for (int i = 0; i < source.SourceColliderIds.Count; i++)
                {
                    string colliderId = source.SourceColliderIds[i];
                    if (!string.IsNullOrWhiteSpace(colliderId) && !SourceColliderIds.Contains(colliderId))
                        SourceColliderIds.Add(colliderId);
                }
                SourceColliderIds.Sort(StringComparer.Ordinal);
            }
            public void AddPoint(Vector3 point)
            {
                long ordinal = _seenPointCount++;
                if (Points.Count < HurtboxFitPointCapacityPerCollider) { Points.Add(point); return; }
                ulong mixed = unchecked((ulong)ordinal * 11400714819323198485UL + 0x9e3779b97f4a7c15UL);
                long slot = (long)(mixed % (ulong)(ordinal + 1));
                if (slot < HurtboxFitPointCapacityPerCollider) Points[(int)slot] = point;
            }

            private static void CaptureAnatomicalFrame(
                Transform parent,
                out Vector3 forward,
                out Vector3 up)
            {
                forward = -Vector3.right;
                up = Vector3.up;
                if (parent == null) return;

                Vector3 awayFromNeck = Vector3.zero;
                if (parent.parent != null)
                    awayFromNeck = -parent.InverseTransformPoint(parent.parent.position);

                Transform mouthTip = FindChildByName(parent, "Dragon15_tongue3")
                    ?? FindChildByName(parent, "Dragon15_tongue2")
                    ?? FindChildByName(parent, "Dragon15_tongue1")
                    ?? FindChildByName(parent, "Dragon15_jaw");
                Vector3 towardMouth = mouthTip != null
                    ? parent.InverseTransformPoint(mouthTip.position)
                    : Vector3.zero;

                if (awayFromNeck.sqrMagnitude > 1e-8f) awayFromNeck.Normalize();
                if (towardMouth.sqrMagnitude > 1e-8f) towardMouth.Normalize();
                if (awayFromNeck.sqrMagnitude > 1e-8f && towardMouth.sqrMagnitude > 1e-8f
                    && Vector3.Dot(awayFromNeck, towardMouth) < 0f)
                    towardMouth = -towardMouth;

                // The incoming neck segment is not the skull's long axis on a creature with an
                // upright neck. It is only a sign/fallback hint. The deepest tongue locator gives
                // the stable muzzle direction and must therefore be authoritative whenever it is
                // available; averaging it with the neck direction tilts the capsule vertically.
                if (towardMouth.sqrMagnitude > 1e-8f) forward = towardMouth;
                else if (awayFromNeck.sqrMagnitude > 1e-8f) forward = awayFromNeck;

                Vector3 rootUp = parent.root != null ? parent.root.up : Vector3.up;
                Vector3 localRootUp = parent.InverseTransformDirection(rootUp);
                up = localRootUp.sqrMagnitude > 1e-8f
                    ? localRootUp.normalized
                    : Vector3.up;
            }
        }

        private readonly struct HurtboxFitSurfaceSample
        {
            public HurtboxFitSurfaceSample(int a, int b, int c, int templateA, int templateB, int templateC)
            {
                A = a;
                B = b;
                C = c;
                TemplateA = templateA;
                TemplateB = templateB;
                TemplateC = templateC;
            }

            public int A { get; }
            public int B { get; }
            public int C { get; }
            public int TemplateA { get; }
            public int TemplateB { get; }
            public int TemplateC { get; }
        }

        private sealed class HurtboxFitRendererSamples : IDisposable
        {
            private readonly SkinnedMeshRenderer _renderer;
            private readonly HurtboxFitSurfaceSample[] _samples;
            private readonly Vector3[] _sourceVertices;
            private readonly BoneWeight[] _weights;
            private readonly Matrix4x4[] _bindPoses;

            public HurtboxFitRendererSamples(SkinnedMeshRenderer renderer, HurtboxFitSurfaceSample[] samples)
            {
                _renderer = renderer;
                _samples = samples;
                _sourceVertices = renderer.sharedMesh.vertices;
                _weights = renderer.sharedMesh.boneWeights;
                _bindPoses = renderer.sharedMesh.bindposes;
            }

            public void Dispose() { }

            public void CollectFrame(List<HurtboxFitTemplate> templates)
            {
                for (int i = 0; i < _samples.Length; i++)
                {
                    HurtboxFitSurfaceSample sample = _samples[i];
                    AddSkinnedPoint(sample.A, sample.TemplateA, templates);
                    AddSkinnedPoint(sample.B, sample.TemplateB, templates);
                    AddSkinnedPoint(sample.C, sample.TemplateC, templates);
                }
            }

            private void AddSkinnedPoint(int vertexIndex, int templateIndex, List<HurtboxFitTemplate> templates)
            {
                if (templateIndex < 0 || vertexIndex < 0 || vertexIndex >= _sourceVertices.Length) return;
                Vector3 world = SkinVertexWorld(vertexIndex);
                HurtboxFitTemplate template = templates[templateIndex];
                template.AddPoint(template.Parent.InverseTransformPoint(world));
            }

            private Vector3 SkinVertexWorld(int vertexIndex)
            {
                BoneWeight weight = _weights[vertexIndex];
                Vector3 source = _sourceVertices[vertexIndex];
                Vector3 world = Vector3.zero;
                AddBoneContribution(ref world, source, weight.boneIndex0, weight.weight0);
                AddBoneContribution(ref world, source, weight.boneIndex1, weight.weight1);
                AddBoneContribution(ref world, source, weight.boneIndex2, weight.weight2);
                AddBoneContribution(ref world, source, weight.boneIndex3, weight.weight3);
                return world;
            }

            private void AddBoneContribution(ref Vector3 world, Vector3 source, int boneIndex, float weight)
            {
                if (weight <= 0f || boneIndex < 0 || boneIndex >= _renderer.bones.Length || boneIndex >= _bindPoses.Length) return;
                world += (_renderer.bones[boneIndex].localToWorldMatrix * _bindPoses[boneIndex]).MultiplyPoint3x4(source) * weight;
            }
        }

        private struct HurtboxFitShape
        {
            public HurtboxFitShape(string kind, Vector3 center, Quaternion rotation, Vector3 size, float radius, float height) { Kind = kind; Center = center; Rotation = rotation; Size = size; Radius = radius; Height = height; FalseNegativePercent = 0f; DepthP95 = 0f; FitPointCount = 0; ExcludedOutlierPercent = 0f; SurfacePaddingMeters = 0f; FramePolicy = string.Empty; }
            public string Kind; public Vector3 Center; public Quaternion Rotation; public Vector3 Size; public float Radius; public float Height; public float FalseNegativePercent; public float DepthP95; public int FitPointCount; public float ExcludedOutlierPercent; public float SurfacePaddingMeters; public string FramePolicy;
        }

        private sealed class HurtboxFitAlphaSampler : IDisposable
        {
            public static HurtboxFitAlphaSampler Opaque { get; } = new HurtboxFitAlphaSampler(null, 0f, false);
            public static HurtboxFitAlphaSampler Blocked { get; } = new HurtboxFitAlphaSampler(null, 0f, true);
            private readonly Texture2D _texture; private readonly float _cutoff;
            private HurtboxFitAlphaSampler(Texture2D texture, float cutoff, bool blocked) { _texture = texture; _cutoff = cutoff; IsBlocked = blocked; }
            public bool IsBlocked { get; }
            public static HurtboxFitAlphaSampler Read(Texture2D source, float cutoff)
            {
                var target = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
                RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                RenderTexture previous = RenderTexture.active;
                try { Graphics.Blit(source, temporary); RenderTexture.active = temporary; target.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); target.Apply(false, false); }
                finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); }
                return new HurtboxFitAlphaSampler(target, cutoff, false);
            }
            public bool IsVisible(Vector2 uv) => _texture == null || _texture.GetPixelBilinear(uv.x - Mathf.Floor(uv.x), uv.y - Mathf.Floor(uv.y)).a >= _cutoff;
            public void Dispose() { if (_texture != null) UnityEngine.Object.DestroyImmediate(_texture); }
        }
    }
}
