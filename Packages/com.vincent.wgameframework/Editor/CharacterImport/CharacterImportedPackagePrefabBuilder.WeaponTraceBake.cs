using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MxFramework.Animation;
using MxFramework.CharacterApplication;
using MxFramework.CharacterAction;
using MxFramework.CharacterRuntimeSpawn;
using MxFramework.CharacterRuntimeSpawn.Unity;
using MxFramework.Editor.CharacterAction;
using MxFramework.Resources;
using MxFramework.Resources.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterImport
{
    public static partial class CharacterImportedPackagePrefabBuilder
    {
        /// <summary>Offline sampling with the same root-motion extraction and graph layout as runtime presentation.</summary>
        internal sealed class WeaponTracePoseSampler : IDisposable
        {
            private readonly Animator _animator;
            private readonly bool _applyRootMotion;
            private readonly AnimatorCullingMode _cullingMode;
            private PlayableGraph _graph;
            private readonly AnimationClipPlayable _playable;

            internal WeaponTracePoseSampler(GameObject instance, AnimationClip clip)
            {
                _animator = instance.GetComponentInChildren<Animator>(true);
                if (_animator == null)
                    throw new InvalidOperationException("Weapon trace bake requires the runtime character Animator.");
                _applyRootMotion = _animator.applyRootMotion;
                _cullingMode = _animator.cullingMode;
                _animator.applyRootMotion = false;
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                _animator.Rebind();
                _graph = PlayableGraph.Create("Character.WeaponTraceBake");
                _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var rootMixer = AnimationLayerMixerPlayable.Create(_graph, 1);
                var layer = AnimationMixerPlayable.Create(_graph, 1);
                _graph.Connect(layer, 0, rootMixer, 0);
                rootMixer.SetInputWeight(0, 1f);
                _playable = AnimationClipPlayable.Create(_graph, clip);
                _playable.SetSpeed(0d);
                _graph.Connect(_playable, 0, layer, 0);
                layer.SetInputWeight(0, 1f);
                var output = AnimationPlayableOutput.Create(_graph, "Animation", _animator);
                output.SetSourcePlayable(rootMixer);
                _graph.Play();
                Sample(0f);
            }

            internal void Sample(float time)
            {
                _playable.SetTime(time);
                _graph.Evaluate(0f);
            }

            public void Dispose()
            {
                if (_graph.IsValid())
                    _graph.Destroy();
                if (_animator != null)
                {
                    _animator.applyRootMotion = _applyRootMotion;
                    _animator.cullingMode = _cullingMode;
                }
            }
        }

        internal sealed class BodyPartContactMotionBakeRequest
        {
            internal BodyPartContactMotionBakeRequest(string clipKey, int groupSocketId, int maxFrame, string sourceClipHash)
            {
                ClipKey = clipKey;
                GroupSocketId = groupSocketId;
                MaxFrame = maxFrame;
                SourceClipHash = sourceClipHash;
            }

            internal string ClipKey { get; }
            internal int GroupSocketId { get; }
            internal int MaxFrame { get; }
            internal string SourceClipHash { get; }
        }

        private sealed class WeaponTraceBakeStatus
        {
            public string Status { get; set; } = string.Empty;
            public string Hash { get; set; } = string.Empty;
            public int RequestCount { get; set; }
            public int ArtifactCount { get; set; }
            public int CheckedFrameCount { get; set; }
            public CharacterWeaponTraceBakeIssueReport[] Issues { get; set; } =
                Array.Empty<CharacterWeaponTraceBakeIssueReport>();
        }

        private static WeaponTraceBakeStatus CollectWeaponTraceBakeStatus(CharacterImportedPackage package)
        {
            if (package == null)
            {
                return FailedWeaponTraceBakeStatus(
                    "WeaponTraceBakePackageMissing",
                    "Imported package is required before weapon trace bake validation.");
            }

            bool hasActionRegistry = TryLoadCharacterActionRegistryForPackage(
                package,
                out CharacterActionConfigRegistry registry,
                out string registryIssue);
            if (!hasActionRegistry)
            {
                return FailedWeaponTraceBakeStatus(
                    "WeaponTraceBakeActionRegistryMissing",
                    registryIssue);
            }

            CharacterWeaponTraceBakeRequest[] requests = CreateWeaponTraceBakeRequests(package, registry);

            CharacterWeaponTraceBakeCatalog catalog = BuildSampledWeaponTraceBakeCatalog(
                package,
                requests,
                out CharacterWeaponTraceBakeCatalog contacts,
                out CharacterWeaponTraceBakeBuildReport report);
            if (report.Success)
                WriteWeaponTraceBakeCatalog(package, requests, catalog, contacts);
            return new WeaponTraceBakeStatus
            {
                Status = report.Success ? "Ready" : "Failed",
                Hash = ComputeWeaponTraceBakeHash(package, requests),
                RequestCount = report.RequestCount,
                ArtifactCount = report.ArtifactCount,
                CheckedFrameCount = report.CheckedFrameCount,
                Issues = ConvertWeaponTraceBakeIssues(report.Issues)
            };
        }

        private static WeaponTraceBakeStatus FailedWeaponTraceBakeStatus(string code, string message)
        {
            return new WeaponTraceBakeStatus
            {
                Status = "Failed",
                Issues = new[]
                {
                    new CharacterWeaponTraceBakeIssueReport
                    {
                        Code = code ?? string.Empty,
                        Message = message ?? string.Empty,
                        Frame = -1,
                        SourcePath = "package_cache/import_report.json",
                        SourceObjectPath = "weaponTraceBake"
                    }
                }
            };
        }

        private static void WriteWeaponTraceBakeCatalog(
            CharacterImportedPackage package,
            CharacterWeaponTraceBakeRequest[] requests,
            CharacterWeaponTraceBakeCatalog catalog,
            CharacterWeaponTraceBakeCatalog contacts)
        {
            if (package == null || string.IsNullOrWhiteSpace(package.RootPath))
                return;

            CharacterWeaponTraceBakeCatalog weapons = CharacterWeaponTraceBakeStorage.SelectWeaponWindows(catalog, requests);
            AssetDatabase.ReleaseCachedFileHandles();
            CharacterWeaponTraceBakeStorage.Write(package.RootPath, package.PackageId, weapons, contacts);
        }

        private static CharacterWeaponTraceBakeCatalog BuildSampledWeaponTraceBakeCatalog(
            CharacterImportedPackage package, CharacterWeaponTraceBakeRequest[] requests,
            out CharacterWeaponTraceBakeCatalog contacts, out CharacterWeaponTraceBakeBuildReport report)
        {
            var state = new WeaponTraceSamplingState(package, requests, false);
            using (var routine = SampleWeaponTraceBakeRoutine(state).GetEnumerator())
                while (routine.MoveNext()) { }
            contacts = state.Contacts;
            report = state.Report;
            return state.Weapons;
        }

        private static IEnumerable<object> SampleWeaponTraceBakeRoutine(WeaponTraceSamplingState state)
        {
            CharacterImportedPackage package = state.Package;
            CharacterWeaponTraceBakeRequest[] requests = state.Requests;
            CharacterWeaponTraceBakeCatalog catalog = state.Weapons;
            CharacterWeaponTraceBakeCatalog contacts = state.Contacts;
            BodyPartContactMotionBakeRequest[] contactRequests = state.ContactRequests;
            int requestCount = (requests?.Length ?? 0) + contactRequests.Length;
            var issues = state.Issues;
            if (requestCount == 0)
            {
                state.Report = new CharacterWeaponTraceBakeBuildReport(
                    0,
                    0,
                    0,
                    Array.Empty<CharacterWeaponTraceBakeBuildIssue>());
                yield break;
            }

            var keys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (CharacterWeaponTraceBakeRequest request in requests) keys.Add(request.ClipKey);
            foreach (BodyPartContactMotionBakeRequest request in contactRequests) keys.Add(request.ClipKey);
            foreach (string key in keys)
            {
                AnimationClip clip = LoadAnimationClipForRuntimeKey(package.RootPath, key);
                state.Clips[key] = clip;
                if (clip == null)
                    issues.Add(CreateWeaponTraceBakeIssue("BakeClipMissing", "Animation clip could not be loaded before sampling.",
                        CharacterActionId.None, key, 0, -1, "config/animation_clip_registry.json", "clips/" + key));
                yield return null;
            }
            if (issues.Count > 0)
            {
                state.Report = new CharacterWeaponTraceBakeBuildReport(requestCount, 0, 0, issues.ToArray());
                yield break;
            }
            if (state.PrepareWeaponPrefabs && requests.Length > 0)
            {
                BuildWeaponPrefabs(package, ReadResourcePreviewInfos(package, package.RootPath), package.RootPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                yield return null;
            }

            string prefabPath = GetPreviewPrefabPath(package);
            GameObject prefab = string.IsNullOrWhiteSpace(prefabPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                issues.Add(CreateWeaponTraceBakeIssue(
                    "WeaponTraceBakePreviewPrefabMissing",
                    "Generated preview prefab is required before sampling weapon trace endpoints.",
                    CharacterActionId.None,
                    string.Empty,
                    0,
                    -1,
                    prefabPath,
                    "prefabs/" + (package?.PackageId ?? string.Empty) + "_character_preview"));
                state.Report = new CharacterWeaponTraceBakeBuildReport(
                    requestCount,
                    0,
                    0,
                    issues.ToArray());
                yield break;
            }

            GameObject instance = null;
            CharacterDefaultEquipmentRuntimeBinder binder = null;
            CharacterBoneRuntimeSync boneSync = null;
            ResourceManager resourceManager = null;
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab, previewScene) as GameObject;
                if (instance == null)
                {
                    instance = UnityEngine.Object.Instantiate(prefab);
                    SceneManager.MoveGameObjectToScene(instance, previewScene);
                }
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.SetActive(true);

                if (requests != null && requests.Length > 0)
                {
                    CharacterRuntimeSpawnResult spawnResult = CharacterRuntimeSpawnResolver.Resolve(package, default);
                    resourceManager = CreateWeaponTraceBakeResourceManager(package);
                    CharacterRuntimePrefabCompositionResult composition = CharacterRuntimePrefabComposer.Compose(
                        instance,
                        new CharacterRuntimePrefabCompositionOptions(package, spawnResult)
                        {
                            ResourceManager = resourceManager,
                            InstantiateDefaultWeapons = false,
                            InstallInputMotionController = false,
                            EnableInputMotion = false,
                            UseExternalRuntimeTick = true
                        });
                    if (!composition.Success)
                    {
                        issues.Add(CreateWeaponTraceBakeIssue(
                            "WeaponTraceBakeRuntimeCompositionFailed",
                            "Runtime prefab composition failed before weapon trace sampling: " + string.Join("; ", composition.Issues),
                            CharacterActionId.None,
                            string.Empty,
                            0,
                            -1,
                            prefabPath,
                            "components/CharacterRuntimePrefabComposer"));
                        state.Report = new CharacterWeaponTraceBakeBuildReport(
                            requestCount,
                            0,
                            0,
                            issues.ToArray());
                        yield break;
                    }

                    binder = composition.DefaultEquipmentBinder;
                    boneSync = composition.BoneSync;
                    if (binder == null)
                    {
                        issues.Add(CreateWeaponTraceBakeIssue(
                            "WeaponTraceBakeDefaultWeaponBinderMissing",
                            "Runtime composition did not install a default equipment binder for weapon trace sampling.",
                            CharacterActionId.None,
                            string.Empty,
                            0,
                            -1,
                            prefabPath,
                            "components/CharacterDefaultEquipmentRuntimeBinder"));
                        state.Report = new CharacterWeaponTraceBakeBuildReport(
                            requestCount,
                            0,
                            0,
                            issues.ToArray());
                        yield break;
                    }

                    binder.DisableStandaloneAnimationPlayback();
                    binder.ConfigureResourceManager(resourceManager, instantiateDefaultWeapons: false);
                    binder.ClearSpawnedWeapons();
                    binder.InstantiateDefaultWeapons(resourceManager);
                    EnsureSpawnedWeaponTraceLocators(package, binder);
                }

                int artifactCount = 0;
                int checkedFrameCount = 0;
                for (int i = 0; requests != null && i < requests.Length; i++)
                {
                    CharacterWeaponTraceBakeRequest request = requests[i];
                    if (request == null)
                        continue;

                    CharacterWeaponTraceRuntimeBinding trace =
                        CharacterWeaponTraceBakeCatalogBuilder.FindTraceBinding(package, request.TraceId);
                    if (trace == null)
                    {
                        issues.Add(CreateWeaponTraceBakeIssue(
                            CharacterWeaponTraceBakeCatalogBuilder.IssueTraceBindingMissing,
                            "Trace binding was not found in the imported character geometry.",
                            request.ActionId,
                            request.ClipKey,
                            request.TraceId,
                            -1,
                            "geometry/traces.json",
                            "geometry/traces/" + (request.TraceId.ToString(System.Globalization.CultureInfo.InvariantCulture))));
                        continue;
                    }

                    // Common actions can reference a weapon from another authored loadout.
                    // Only the private sampling instance changes equipment; runtime defaults stay intact.
                    if (!TryResolveSpawnedWeaponTransform(binder, trace, out _))
                    {
                        if (TryResolveWeaponTraceBakeSpawn(package, trace, out CharacterRuntimeSpawnResult traceSpawn))
                        {
                            binder.ClearSpawnedWeapons();
                            CharacterRuntimePrefabComposer.Compose(instance,
                                new CharacterRuntimePrefabCompositionOptions(package, traceSpawn)
                                {
                                    ResourceManager = resourceManager,
                                    InstantiateDefaultWeapons = true,
                                    InstallControllerBinding = false,
                                    InstallInputMotionController = false,
                                    InstallLocomotionController = false,
                                    InstallBoneRuntimeSync = false,
                                    UseExternalRuntimeTick = true
                                });
                            EnsureSpawnedWeaponTraceLocators(package, binder);
                        }
                    }

                    AnimationClip clip = state.Clips[request.ClipKey];
                    if (clip == null)
                    {
                        issues.Add(CreateWeaponTraceBakeIssue(
                            "WeaponTraceBakeClipMissing",
                            "Animation clip could not be loaded for weapon trace sampling.",
                            request.ActionId,
                            request.ClipKey,
                            request.TraceId,
                            -1,
                            "config/animation_clip_registry.json",
                            "clips/" + (request.ClipKey ?? string.Empty)));
                        continue;
                    }

                    MxAnimationBakeArtifact artifact = null;
                    foreach (object step in SampleWeaponTraceFrames(instance, package, binder, boneSync, clip, request, trace, issues,
                        result => artifact = result))
                        yield return step;
                    if (artifact == null)
                        continue;

                    catalog.RegisterArtifact(request.ClipKey, request.TraceId, artifact);
                    artifactCount++;
                    for (int frame = request.ActiveStartFrame; frame <= request.ActiveEndFrame; frame++)
                    {
                        checkedFrameCount++;
                        if (catalog.TryGetFrame(request.ClipKey, request.TraceId, frame, out _))
                            continue;

                        issues.Add(CreateWeaponTraceBakeIssue(
                            CharacterWeaponTraceBakeCatalogBuilder.IssueFrameMissing,
                            "Sampled weapon trace catalog has no frame for this active hit frame.",
                            request.ActionId,
                            request.ClipKey,
                            request.TraceId,
                            frame,
                            CharacterWeaponTraceBakeCatalogJson.DefaultRelativePath,
                            "bakes/" + request.ActionId.Value + "/" + (request.TraceId.ToString(System.Globalization.CultureInfo.InvariantCulture))));
                    }
                }

                for (int i = 0; i < contactRequests.Length; i++)
                {
                    BodyPartContactMotionBakeRequest request = contactRequests[i];
                    AnimationClip clip = state.Clips[request.ClipKey];
                    if (clip == null)
                    {
                        issues.Add(CreateWeaponTraceBakeIssue(
                            "BodyPartContactMotionBakeClipMissing",
                            "Animation clip could not be loaded for contact motion sampling.",
                            CharacterActionId.None, request.ClipKey, 0, -1,
                            "config/animation_clip_registry.json", "clips/" + request.ClipKey));
                        continue;
                    }

                    MxAnimationBakeArtifact artifact = null;
                    foreach (object step in SampleBodyPartContactMotionFrames(instance, package, clip, request, issues,
                        result => artifact = result))
                        yield return step;
                    if (artifact == null)
                        continue;

                    // Contact payload groups use an existing motion socket identity, not a weapon Trace.
                    contacts.RegisterArtifact(request.ClipKey, request.GroupSocketId, artifact);
                    artifactCount++;
                    checkedFrameCount += request.MaxFrame + 1;
                }

                state.Report = new CharacterWeaponTraceBakeBuildReport(
                    requestCount,
                    artifactCount,
                    checkedFrameCount,
                    issues.ToArray());
                yield break;
            }
            finally
            {
                if (binder != null)
                    binder.ClearSpawnedWeapons();
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static ResourceManager CreateWeaponTraceBakeResourceManager(CharacterImportedPackage package)
        {
            var manager = new ResourceManager();
            manager.RegisterProvider(new ResourcesProvider());
            manager.RegisterProvider(new EditorAssetDatabaseResourceProvider());
            var entries = new List<ResourceCatalogEntry>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            CharacterRuntimePrefabResourceCatalog.AddWeaponPrefabEntries(
                package,
                package?.RootPath ?? string.Empty,
                entries,
                ids,
                new[] { "CharacterWeaponTraceBake", "CharacterEquipment" });
            AddRuntimePublicEquipmentCatalogEntries(package, entries, ids);
            manager.AddCatalog(new ResourceCatalog(
                "character.weaponTraceBake." + (package?.PackageId ?? string.Empty),
                package?.PackageId ?? string.Empty,
                entries));
            manager.ValidateCatalogs();
            return manager;
        }

        private static void AddRuntimePublicEquipmentCatalogEntries(
            CharacterImportedPackage package,
            List<ResourceCatalogEntry> entries,
            HashSet<string> ids)
        {
            ResourceCatalog runtimeCatalog = package?.RuntimeResourceCatalog;
            if (runtimeCatalog == null || entries == null)
                return;

            for (int i = 0; i < runtimeCatalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = runtimeCatalog.Entries[i];
                if (!IsRuntimePublicEquipmentResource(entry))
                    continue;

                string assetPath = ResolveUnityAssetPath(entry, GetUnityAssetGuid(entry), requireGameObject: true);
                if (string.IsNullOrWhiteSpace(assetPath))
                    continue;

                string packageId = entry.Id.StartsWith("weapon.", StringComparison.Ordinal)
                    ? "common"
                    : entry.PackageId;
                var providerData = entry.ProviderData != null
                    ? new Dictionary<string, string>(entry.ProviderData, StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal);
                providerData[EditorAssetDatabaseResourceProvider.AssetPathProviderDataKey] = NormalizeProjectPath(assetPath);
                if (ids != null && !ids.Add(entry.Id))
                    continue;

                entries.Add(new ResourceCatalogEntry(
                    entry.Id,
                    ResourceTypeIds.GameObject,
                    EditorAssetDatabaseResourceProvider.Id,
                    NormalizeProjectPath(assetPath),
                    entry.Variant,
                    string.IsNullOrWhiteSpace(packageId) ? "common" : packageId,
                    entry.Dependencies,
                    entry.Labels,
                    entry.Hash,
                    entry.Size,
                    entry.AllowOverride,
                    providerData));
            }
        }

        private static bool IsRuntimePublicEquipmentResource(ResourceCatalogEntry entry)
        {
            if (entry == null)
                return false;
            if (entry.Id.StartsWith("weapon.", StringComparison.Ordinal))
                return true;

            IReadOnlyList<string> labels = entry.Labels;
            for (int i = 0; labels != null && i < labels.Count; i++)
            {
                string label = labels[i];
                if (string.Equals(label, "domain.weapon", StringComparison.Ordinal)
                    || string.Equals(label, "usage.weaponmodel", StringComparison.Ordinal)
                    || string.Equals(label, "character.usage.weaponmodel", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return string.Equals(GetProviderData(entry, "usage"), "weaponModel", StringComparison.OrdinalIgnoreCase);
        }

        private static MxAnimationBakeArtifact SampleWeaponTraceArtifact(
            GameObject instance, CharacterImportedPackage package, CharacterDefaultEquipmentRuntimeBinder binder,
            CharacterBoneRuntimeSync boneSync, AnimationClip clip, CharacterWeaponTraceBakeRequest request,
            CharacterWeaponTraceRuntimeBinding trace, List<CharacterWeaponTraceBakeBuildIssue> issues)
        {
            MxAnimationBakeArtifact artifact = null;
            foreach (object step in SampleWeaponTraceFrames(instance, package, binder, boneSync, clip, request, trace, issues,
                result => artifact = result)) { }
            return artifact;
        }

        internal static MxAnimationBakeArtifact SampleBodyPartContactMotionArtifact(
            GameObject instance, CharacterImportedPackage package, AnimationClip clip,
            BodyPartContactMotionBakeRequest request, List<CharacterWeaponTraceBakeBuildIssue> issues)
        {
            MxAnimationBakeArtifact artifact = null;
            foreach (object step in SampleBodyPartContactMotionFrames(instance, package, clip, request, issues,
                result => artifact = result)) { }
            return artifact;
        }

        private static IEnumerable<object> SampleWeaponTraceFrames(
            GameObject instance,
            CharacterImportedPackage package,
            CharacterDefaultEquipmentRuntimeBinder binder,
            CharacterBoneRuntimeSync boneSync,
            AnimationClip clip,
            CharacterWeaponTraceBakeRequest request,
            CharacterWeaponTraceRuntimeBinding trace,
            List<CharacterWeaponTraceBakeBuildIssue> issues, Action<MxAnimationBakeArtifact> complete)
        {
            int maxFrame = Math.Max(request.MaxFrame, request.ActiveEndFrame);
            int quantizationScale = CharacterWeaponTraceBakeArtifactBuilder.DefaultQuantizationScale;
            var frames = new List<MxAnimationBakedWeaponTraceFrame>(maxFrame + 1);
            using var poseSampler = new WeaponTracePoseSampler(instance, clip);
            Vector3 previousRoot;
            Vector3 previousTip;
            if (!TrySampleWeaponTraceEndpoints(
                    poseSampler,
                    instance,
                    binder,
                    boneSync,
                    clip,
                    request,
                    trace,
                    0,
                    out previousRoot,
                    out previousTip,
                    issues))
            {
                yield break;
            }
            for (int frame = 0; frame <= maxFrame; frame++)
            {
                if (!TrySampleWeaponTraceEndpoints(
                        poseSampler,
                        instance,
                        binder,
                        boneSync,
                        clip,
                        request,
                        trace,
                        frame,
                        out Vector3 root,
                        out Vector3 tip,
                        issues))
                {
                    yield break;
                }

                frames.Add(new MxAnimationBakedWeaponTraceFrame(
                    frame,
                    trace.TraceId,
                    ResolveTraceSocketId(package, trace),
                    Bake(previousRoot, quantizationScale),
                    Bake(previousTip, quantizationScale),
                    Bake(root, quantizationScale),
                    Bake(tip, quantizationScale)));
                previousRoot = root;
                previousTip = tip;
                yield return null;
            }

            var profile = new MxAnimationBakeProfile(
                "character.weaponTraceBake."
                + StableSuffix(request.ActionId.Value.ToString())
                + "."
                + StableSuffix(request.ClipKey)
                + "."
                + request.TraceId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                new ResourceKey(request.ClipKey, ResourceTypeIds.AnimationClip),
                request.SourceClipHash,
                "character." + request.ActionId.Value,
                CreateSampledTraceFingerprint(trace, clip),
                60,
                quantizationScale,
                MxAnimationBakeCoordinateSpace.Local,
                MxAnimationBakeRoundingPolicy.RoundNearest,
                CreateSampledTraceFingerprint(trace, clip));
            complete(new MxAnimationBakeArtifact(
                profile,
                frames,
                rootMotionFrames: null,
                eventMarkers: null,
                artifactHash: string.Empty));
        }

        private static IEnumerable<object> SampleBodyPartContactMotionFrames(
            GameObject instance,
            CharacterImportedPackage package,
            AnimationClip clip,
            BodyPartContactMotionBakeRequest request,
            List<CharacterWeaponTraceBakeBuildIssue> issues, Action<MxAnimationBakeArtifact> complete)
        {
            CharacterContactMotionSocketSample[] samples = ResolveBodyPartContactMotionSocketSamples(
                instance, package, clip, request, issues);
            if (samples.Length == 0)
                yield break;

            int quantizationScale = CharacterWeaponTraceBakeArtifactBuilder.DefaultQuantizationScale;
            var frames = new List<MxAnimationBakedSocketFrame>((request.MaxFrame + 1) * samples.Length);
            var previous = new Vector3[samples.Length];
            using (var sampler = new WeaponTracePoseSampler(instance, clip))
            {
                ReadBodyPartContactMotionSocketLocals(instance, samples, previous, 0f);
                for (int frame = 0; frame <= request.MaxFrame; frame++)
                {
                    float time = ResolveBakeSampleTime(clip, frame);
                    sampler.Sample(time);
                    AddBodyPartContactMotionSocketFrames(
                        instance, samples, frame, time, previous, frames, quantizationScale);
                    yield return null;
                }
            }

            string fingerprint = ComputeStringContentHash(JsonConvert.SerializeObject(package.Geometry.Sockets));
            var profile = new MxAnimationBakeProfile(
                "character.contactMotionBake." + package.PackageId + "." + StableSuffix(request.ClipKey),
                new ResourceKey(request.ClipKey, ResourceTypeIds.AnimationClip),
                request.SourceClipHash,
                "character." + package.PackageId,
                fingerprint,
                60,
                quantizationScale,
                MxAnimationBakeCoordinateSpace.Local,
                MxAnimationBakeRoundingPolicy.RoundNearest,
                fingerprint);
            complete(new MxAnimationBakeArtifact(profile, socketFrames: frames));
        }

        private static CharacterContactMotionSocketSample[] ResolveBodyPartContactMotionSocketSamples(
            GameObject instance,
            CharacterImportedPackage package,
            AnimationClip clip,
            BodyPartContactMotionBakeRequest request,
            List<CharacterWeaponTraceBakeBuildIssue> issues)
        {
            CharacterBodyPartContactPointConfig[] contactPoints = package?.Configs?.BodyPartContactPoints
                ?? Array.Empty<CharacterBodyPartContactPointConfig>();
            CharacterSocketRuntimeBinding[] sockets = package?.Geometry?.Sockets
                ?? Array.Empty<CharacterSocketRuntimeBinding>();
            if (contactPoints.Length == 0 || sockets.Length == 0)
                return Array.Empty<CharacterContactMotionSocketSample>();

            Transform bodyModel = FindChildByName(instance != null ? instance.transform : null, "BodyModel");
            if (bodyModel == null && instance != null)
                bodyModel = instance.transform;

            var samples = new List<CharacterContactMotionSocketSample>();
            var includedSocketIds = new HashSet<int>();
            for (int i = 0; i < contactPoints.Length; i++)
            {
                CharacterBodyPartContactPointConfig contactPoint = contactPoints[i];
                if (contactPoint == null
                    || contactPoint.MotionMode != CharacterBodyPartContactMotionMode.BakedSocket
                    || contactPoint.MotionSocketId <= 0
                    || !includedSocketIds.Add(contactPoint.MotionSocketId))
                {
                    continue;
                }

                CharacterSocketRuntimeBinding socket = FindSocketBinding(sockets, contactPoint.MotionSocketId);
                if (socket == null)
                {
                    AddBodyPartContactMotionSocketIssue(
                        issues,
                        request.ClipKey,
                        contactPoint.MotionSocketId,
                        "BodyPartContactMotionSocketBindingMissing",
                        "Body part contact point motion socket binding was not found in imported geometry.");
                    continue;
                }

                if (!TryResolveBodyPartContactMotionSocketSample(
                        instance,
                        bodyModel,
                        socket,
                        clip,
                        request.ClipKey,
                        issues,
                        out CharacterContactMotionSocketSample sample))
                {
                    continue;
                }

                samples.Add(sample);
            }

            return samples.Count > 0 ? samples.ToArray() : Array.Empty<CharacterContactMotionSocketSample>();
        }

        private static CharacterSocketRuntimeBinding FindSocketBinding(
            CharacterSocketRuntimeBinding[] sockets,
            int socketId)
        {
            for (int i = 0; sockets != null && i < sockets.Length; i++)
            {
                CharacterSocketRuntimeBinding socket = sockets[i];
                if (socket != null && socket.SocketId == socketId)
                    return socket;
            }

            return null;
        }

        private static bool TryResolveBodyPartContactMotionSocketSample(
            GameObject instance,
            Transform bodyModel,
            CharacterSocketRuntimeBinding socket,
            AnimationClip clip,
            string clipKey,
            List<CharacterWeaponTraceBakeBuildIssue> issues,
            out CharacterContactMotionSocketSample sample)
        {
            sample = default;
            if (instance == null || socket == null)
                return false;

            Transform root = instance.transform;
            CharacterContactMotionCurveSampler curveSampler = CreateBodyPartContactMotionCurveSampler(clip, socket);
            Transform bone = FindBodyChild(
                bodyModel,
                socket.BonePath,
                string.IsNullOrWhiteSpace(socket.LocalPose.ParentPath) ? socket.SocketLabel : socket.LocalPose.ParentPath);
            if (bone != null)
            {
                sample = new CharacterContactMotionSocketSample(
                    socket.SocketId,
                    ResolveSocketPath(socket),
                    bone,
                    ToVector3(socket.LocalPose.Position, Vector3.zero),
                    curveSampler,
                    curveSampler != null
                        ? CharacterContactMotionSocketSampleSourceKind.HumanoidCurve
                        : CharacterContactMotionSocketSampleSourceKind.AnimatedBone);
                return true;
            }

            Transform marker = FindChildByName(root, "Socket_" + socket.SocketId);
            if (marker != null)
            {
                AddBodyPartContactMotionSocketIssue(
                    issues,
                    clipKey,
                    socket.SocketId,
                    "BodyPartContactMotionSocketMarkerFallback",
                    "Body part contact point motion socket could not resolve an animated bone and would fall back to a static socket marker.");
                sample = new CharacterContactMotionSocketSample(
                    socket.SocketId,
                    ResolveSocketPath(socket),
                    marker,
                    Vector3.zero,
                    null,
                    CharacterContactMotionSocketSampleSourceKind.MarkerFallback);
                return true;
            }

            Transform locator = FindLocatorTransform(root, socket.LocatorPath);
            if (locator != null)
            {
                AddBodyPartContactMotionSocketIssue(
                    issues,
                    clipKey,
                    socket.SocketId,
                    "BodyPartContactMotionSocketLocatorFallback",
                    "Body part contact point motion socket could not resolve an animated bone and would fall back to a locator transform.");
                sample = new CharacterContactMotionSocketSample(
                    socket.SocketId,
                    ResolveSocketPath(socket),
                    locator,
                    Vector3.zero,
                    null,
                    CharacterContactMotionSocketSampleSourceKind.LocatorFallback);
                return true;
            }

            AddBodyPartContactMotionSocketIssue(
                issues,
                clipKey,
                socket.SocketId,
                "BodyPartContactMotionSocketTransformMissing",
                "Body part contact point motion socket transform could not be resolved while sampling animation.");
            return false;
        }

        private static void ReadBodyPartContactMotionSocketLocals(
            GameObject instance,
            CharacterContactMotionSocketSample[] socketSamples,
            Vector3[] locals,
            float time)
        {
            if (instance == null || socketSamples == null || locals == null)
                return;

            Transform root = instance.transform;
            int count = Math.Min(socketSamples.Length, locals.Length);
            for (int i = 0; i < count; i++)
            {
                locals[i] = socketSamples[i].TryGetLocalPosition(root, time, out Vector3 local)
                    ? local
                    : Vector3.zero;
            }
        }

        private static void AddBodyPartContactMotionSocketFrames(
            GameObject instance,
            CharacterContactMotionSocketSample[] socketSamples,
            int frame,
            float time,
            Vector3[] previousSocketLocals,
            List<MxAnimationBakedSocketFrame> socketFrames,
            int quantizationScale)
        {
            if (instance == null
                || socketSamples == null
                || socketSamples.Length == 0
                || socketFrames == null
                || previousSocketLocals == null)
            {
                return;
            }

            Transform root = instance.transform;
            int count = Math.Min(socketSamples.Length, previousSocketLocals.Length);
            for (int i = 0; i < count; i++)
            {
                CharacterContactMotionSocketSample sample = socketSamples[i];
                if (!sample.TryGetLocalPosition(root, time, out Vector3 local))
                    continue;

                Vector3 delta = frame == 0 ? Vector3.zero : local - previousSocketLocals[i];
                socketFrames.Add(new MxAnimationBakedSocketFrame(
                    frame,
                    sample.SocketId,
                    FormatContactMotionSocketPath(sample),
                    Bake(local, quantizationScale),
                    Bake(delta, quantizationScale)));
                previousSocketLocals[i] = local;
            }
        }

        private static string FormatContactMotionSocketPath(CharacterContactMotionSocketSample sample)
        {
            string source = sample.SourceKind.ToString();
            if (string.IsNullOrWhiteSpace(sample.SocketPath))
                return source;
            return source + ":" + sample.SocketPath;
        }

        private static CharacterContactMotionCurveSampler CreateBodyPartContactMotionCurveSampler(
            AnimationClip clip,
            CharacterSocketRuntimeBinding socket)
        {
            string prefix = ResolveBodyPartContactMotionCurvePrefix(socket);
            if (clip == null || string.IsNullOrWhiteSpace(prefix))
                return null;

            AnimationCurve x = null;
            AnimationCurve y = null;
            AnimationCurve z = null;
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding binding = bindings[i];
                if (!string.IsNullOrWhiteSpace(binding.path))
                    continue;

                if (string.Equals(binding.propertyName, prefix + ".x", StringComparison.Ordinal))
                    x = AnimationUtility.GetEditorCurve(clip, binding);
                else if (string.Equals(binding.propertyName, prefix + ".y", StringComparison.Ordinal))
                    y = AnimationUtility.GetEditorCurve(clip, binding);
                else if (string.Equals(binding.propertyName, prefix + ".z", StringComparison.Ordinal))
                    z = AnimationUtility.GetEditorCurve(clip, binding);
            }

            if (x == null && y == null && z == null)
                return null;
            return new CharacterContactMotionCurveSampler(x, y, z);
        }

        private static string ResolveBodyPartContactMotionCurvePrefix(CharacterSocketRuntimeBinding socket)
        {
            string key = ((socket?.SocketLabel ?? string.Empty)
                + " "
                + (socket?.BonePath ?? string.Empty)
                + " "
                + (socket?.LocalPose.ParentPath ?? string.Empty)).ToLowerInvariant();
            if (key.Contains("foot.l") || key.Contains("foot_l") || key.Contains("leftfoot"))
                return "LeftFootT";
            if (key.Contains("foot.r") || key.Contains("foot_r") || key.Contains("rightfoot"))
                return "RightFootT";
            return string.Empty;
        }

        private static string ResolveSocketPath(CharacterSocketRuntimeBinding socket)
        {
            if (socket == null)
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(socket.BonePath))
                return socket.BonePath;
            if (!string.IsNullOrWhiteSpace(socket.LocalPose.ParentPath))
                return socket.LocalPose.ParentPath;
            return socket.LocatorPath ?? string.Empty;
        }

        private static void AddBodyPartContactMotionSocketIssue(
            List<CharacterWeaponTraceBakeBuildIssue> issues,
            string clipKey,
            int socketId,
            string code,
            string message)
        {
            if (issues == null)
                return;

            issues.Add(CreateWeaponTraceBakeIssue(
                code,
                message,
                CharacterActionId.None,
                clipKey ?? string.Empty,
                socketId,
                -1,
                "geometry/sockets.json",
                "geometry/sockets/" + (socketId.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        }

        private static bool TrySampleWeaponTraceEndpoints(
            WeaponTracePoseSampler poseSampler,
            GameObject instance,
            CharacterDefaultEquipmentRuntimeBinder binder,
            CharacterBoneRuntimeSync boneSync,
            AnimationClip clip,
            CharacterWeaponTraceBakeRequest request,
            CharacterWeaponTraceRuntimeBinding trace,
            int frame,
            out Vector3 rootLocal,
            out Vector3 tipLocal,
            List<CharacterWeaponTraceBakeBuildIssue> issues)
        {
            rootLocal = Vector3.zero;
            tipLocal = Vector3.zero;
            float time = ResolveBakeSampleTime(clip, frame);
            poseSampler.Sample(time);
            boneSync?.ApplyBindings();

            if (!TryResolveSampledWeaponTraceWorldPose(instance, binder, trace, out Vector3 rootWorld, out Vector3 tipWorld))
            {
                issues.Add(CreateWeaponTraceBakeIssue(
                    "WeaponTraceBakeLocatorMissing",
                    "Weapon trace locator could not be resolved while sampling animation.",
                    request.ActionId,
                    request.ClipKey,
                    request.TraceId,
                    frame,
                    "geometry/traces.json",
                    "geometry/traces/" + (request.TraceId.ToString(System.Globalization.CultureInfo.InvariantCulture))));
                return false;
            }

            Transform root = instance.transform;
            rootLocal = root.InverseTransformPoint(rootWorld);
            tipLocal = root.InverseTransformPoint(tipWorld);
            return true;
        }

        private static float ResolveBakeSampleTime(AnimationClip clip, int frame)
        {
            if (clip == null)
                return 0f;
            return Mathf.Min(Mathf.Max(0f, frame / 60f), Mathf.Max(0f, clip.length));
        }

        private static bool TryResolveSampledWeaponTraceWorldPose(
            GameObject instance,
            CharacterDefaultEquipmentRuntimeBinder binder,
            CharacterWeaponTraceRuntimeBinding trace,
            out Vector3 rootWorld,
            out Vector3 tipWorld)
        {
            rootWorld = Vector3.zero;
            tipWorld = Vector3.zero;
            if (TryResolveSpawnedWeaponTransform(binder, trace, out Transform weaponRoot))
            {
                Transform startLocator = FindLocatorTransform(weaponRoot, trace.StartLocatorPath);
                Transform endLocator = FindLocatorTransform(weaponRoot, trace.EndLocatorPath);
                if (startLocator != null && endLocator != null)
                {
                    rootWorld = startLocator.position;
                    tipWorld = endLocator.position;
                    return true;
                }
            }

            return false;
        }

        private static GameObject ResolveAnimationSamplingRoot(GameObject instance)
        {
            if (instance == null)
                return null;

            Animator animator = instance.GetComponentInChildren<Animator>(includeInactive: true);
            return animator != null ? animator.gameObject : instance;
        }

        private static void SampleAnimationClipForWeaponTraceBake(
            GameObject instance,
            AnimationClip clip,
            float time)
        {
            if (instance == null || clip == null)
                return;

            GameObject samplingRoot = ResolveAnimationSamplingRoot(instance);
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(samplingRoot, clip, time);
            AnimationMode.EndSampling();
        }

        private static void EnsureSpawnedWeaponTraceLocators(
            CharacterImportedPackage package,
            CharacterDefaultEquipmentRuntimeBinder binder)
        {
            if (package == null || package.Geometry == null || binder == null)
                return;

            IReadOnlyList<CharacterDefaultWeaponPrefabBinding> bindings = binder.DefaultWeapons;
            IReadOnlyList<GameObject> spawned = binder.SpawnedWeapons;
            CharacterWeaponTraceRuntimeBinding[] traces = package.Geometry.WeaponTraces;
            int count = Math.Min(bindings.Count, spawned.Count);
            for (int i = 0; i < count; i++)
            {
                CharacterDefaultWeaponPrefabBinding binding = bindings[i];
                GameObject weapon = spawned[i];
                if (binding == null || weapon == null)
                    continue;

                CharacterWeaponAttachmentRuntimeBinding attachment = FindAttachmentForDefaultWeaponBinding(
                    package.Geometry.WeaponAttachments,
                    binding);
                if (attachment == null)
                    continue;

                for (int traceIndex = 0; traces != null && traceIndex < traces.Length; traceIndex++)
                {
                    CharacterWeaponTraceRuntimeBinding trace = traces[traceIndex];
                    if (!TraceBelongsToAttachment(trace, attachment))
                        continue;

                    EnsureSpawnedWeaponTraceLocator(
                        weapon.transform,
                        trace.StartLocatorPath,
                        binding,
                        trace.StartPose);
                    EnsureSpawnedWeaponTraceLocator(
                        weapon.transform,
                        trace.EndLocatorPath,
                        binding,
                        trace.EndPose);
                }
            }
        }

        private static CharacterWeaponAttachmentRuntimeBinding FindAttachmentForDefaultWeaponBinding(
            CharacterWeaponAttachmentRuntimeBinding[] attachments,
            CharacterDefaultWeaponPrefabBinding binding)
        {
            if (attachments == null || binding == null)
                return null;

            for (int i = 0; i < attachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = attachments[i];
                if (attachment == null)
                    continue;
                if (!MatchesNonEmpty(binding.WeaponId, attachment.WeaponId))
                    continue;
                if (!MatchesNonEmpty(binding.EquipSlot, attachment.EquipSlot))
                    continue;
                if ((binding.SocketId > 0 && binding.SocketId != attachment.AttachSocketId))
                    continue;
                if (!MatchesNonEmpty(binding.TraceId, attachment.TraceId))
                    continue;

                return attachment;
            }

            return null;
        }

        private static bool MatchesNonEmpty(int left, int right) => left <= 0 || right <= 0 || left == right;

        private static bool MatchesNonEmpty(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return true;
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private static void EnsureSpawnedWeaponTraceLocator(
            Transform weaponRoot,
            string locatorPath,
            CharacterDefaultWeaponPrefabBinding binding,
            CharacterRuntimePose tracePose)
        {
            if (weaponRoot == null || string.IsNullOrWhiteSpace(locatorPath))
                return;
            if (FindLocatorTransform(weaponRoot, locatorPath) != null)
                return;

            Transform locator = EnsureNestedTransform(weaponRoot, locatorPath);
            if (locator == null)
                return;

            ResolveSpawnedWeaponTraceLocatorPose(
                weaponRoot,
                binding,
                tracePose,
                out Vector3 localPosition,
                out Quaternion localRotation,
                out Vector3 localScale);
            locator.localPosition = localPosition;
            locator.localRotation = localRotation;
            locator.localScale = localScale;
        }

        private static void ResolveSpawnedWeaponTraceLocatorPose(
            Transform weaponRoot,
            CharacterDefaultWeaponPrefabBinding binding,
            CharacterRuntimePose tracePose,
            out Vector3 localPosition,
            out Quaternion localRotation,
            out Vector3 localScale)
        {
            localPosition = ToVector3(tracePose.Position, Vector3.zero);
            localRotation = ToQuaternion(tracePose.Rotation);
            localScale = ToVector3(tracePose.Scale, Vector3.one);
            if (weaponRoot == null || binding == null || !IsDefaultWeaponSocketPose(binding, tracePose))
                return;

            Transform socket = weaponRoot.parent;
            if (socket != null)
            {
                Vector3 worldPosition = socket.TransformPoint(localPosition);
                Quaternion worldRotation = socket.rotation * localRotation;
                localPosition = weaponRoot.InverseTransformPoint(worldPosition);
                localRotation = Quaternion.Inverse(weaponRoot.rotation) * worldRotation;
                localScale = DivideScale(localScale, weaponRoot.lossyScale);
                return;
            }

            Vector3 bindingScale = binding.LocalScale == Vector3.zero ? Vector3.one : binding.LocalScale;
            Matrix4x4 bindingMatrix = Matrix4x4.TRS(binding.LocalPosition, binding.LocalRotation, bindingScale);
            Matrix4x4 inverseBinding = bindingMatrix.inverse;
            localPosition = inverseBinding.MultiplyPoint3x4(localPosition);
            localRotation = Quaternion.Inverse(binding.LocalRotation) * localRotation;
            localScale = DivideScale(localScale, bindingScale);
        }

        private static bool IsDefaultWeaponSocketPose(
            CharacterDefaultWeaponPrefabBinding binding,
            CharacterRuntimePose tracePose)
        {
            if (!string.Equals(tracePose.ParentKind, "Socket", StringComparison.Ordinal)
                && !string.Equals(tracePose.ParentKind, "Bone", StringComparison.Ordinal))
            {
                return false;
            }

            string parent = tracePose.ParentPath ?? string.Empty;
            return string.Equals(parent, binding.SocketLabel, StringComparison.Ordinal)
                || string.Equals(parent, binding.EquipmentSlotLabel, StringComparison.Ordinal);
        }

        private static bool TryResolveWeaponTraceBakeSpawn(
            CharacterImportedPackage package,
            CharacterWeaponTraceRuntimeBinding trace,
            out CharacterRuntimeSpawnResult spawn)
        {
            spawn = null;
            SpawnProfileConfig profile = package?.Configs?.GetDefaultSpawnProfile();
            if (profile == null || trace == null)
                return false;

            foreach (EquipmentLoadoutConfig loadout in package.Configs.Loadouts)
            {
                CharacterRuntimeSpawnResult candidate = CharacterRuntimeSpawnResolver.Resolve(package,
                    new CharacterSpawnRequest(profile.SpawnProfileId, loadoutOverride: loadout.LoadoutId));
                if (!candidate.IsSuccess)
                    continue;
                CharacterWeaponAttachmentRuntimeBinding[] attachments =
                    candidate.Binding.WeaponAttachmentBindingPlan?.Attachments
                    ?? Array.Empty<CharacterWeaponAttachmentRuntimeBinding>();
                if (!attachments.Any(attachment => TraceBelongsToAttachment(trace, attachment)))
                    continue;
                spawn = candidate;
                return true;
            }
            return false;
        }

        private static bool TryResolveSpawnedWeaponTransform(
            CharacterDefaultEquipmentRuntimeBinder binder,
            CharacterWeaponTraceRuntimeBinding trace,
            out Transform weaponRoot)
        {
            weaponRoot = null;
            if (binder == null)
                return false;

            IReadOnlyList<CharacterDefaultWeaponPrefabBinding> bindings = binder.DefaultWeapons;
            IReadOnlyList<GameObject> spawned = binder.SpawnedWeapons;
            int count = Math.Min(bindings.Count, spawned.Count);
            for (int i = 0; i < count; i++)
            {
                CharacterDefaultWeaponPrefabBinding binding = bindings[i];
                GameObject instance = spawned[i];
                if (binding == null || instance == null)
                    continue;
                if ((trace.TraceId > 0)
                    && binding.TraceId != trace.TraceId)
                {
                    continue;
                }
                if ((trace.WeaponId > 0)
                    && binding.WeaponId != trace.WeaponId)
                {
                    continue;
                }
                if ((trace.EquipSlot > 0)
                    && binding.EquipSlot != trace.EquipSlot)
                {
                    continue;
                }

                weaponRoot = instance.transform;
                return true;
            }

            return false;
        }

        private static Transform FindLocatorTransform(Transform root, string locatorPath)
        {
            if (root == null || string.IsNullOrWhiteSpace(locatorPath))
                return null;

            string normalized = NormalizeProjectPath(locatorPath).Trim('/');
            Transform byPath = root.Find(normalized);
            if (byPath != null)
                return byPath;

            int slash = normalized.LastIndexOf('/');
            string locatorName = slash >= 0 ? normalized.Substring(slash + 1) : normalized;
            return FindChildByName(root, locatorName);
        }

        private static AnimationClip LoadAnimationClipForRuntimeKey(string importedPackageRoot, string runtimeResourceKey)
        {
            string registryPath = NormalizeProjectPath(importedPackageRoot) + "/config/animation_clip_registry.json";
            JArray clips = File.Exists(registryPath)
                ? JObject.Parse(File.ReadAllText(registryPath))["clips"] as JArray
                : null;
            string assetPath = ResolveAnimationBakeAssetPath(importedPackageRoot, clips, runtimeResourceKey, out string sourceClipName);
            return LoadAnimationClipAsset(assetPath, sourceClipName);
        }

        /// <summary>LowFreqAlloc: collects the file inputs used by weapon/contact sampling, including fallback animation assets.</summary>
        public static string[] CollectWeaponTraceBakeInputAssetPaths(string importedPackageRoot)
            => CollectWeaponTraceBakeInputAssetPaths(importedPackageRoot, out _);

        /// <summary>LowFreqAlloc: returns file dependencies and the effective action sampling requests; gameplay/presentation-only edits do not change the projection.</summary>
        public static string[] CollectWeaponTraceBakeInputAssetPaths(string importedPackageRoot, out string actionSamplingProjection)
        {
            string root = NormalizeProjectPath(importedPackageRoot);
            CharacterImportedPackage package = LoadImportedPackage(root, "bake input dependency collection");
            if (package == null)
                throw new InvalidDataException("Imported package could not be loaded for bake input dependency collection: " + root);
            if (!TryLoadCharacterActionRegistryForPackage(package, out CharacterActionConfigRegistry registry, out string issue))
                throw new InvalidDataException(issue);

            var files = new SortedSet<string>(StringComparer.Ordinal);
            string configRoot = root + "/config";
            foreach (string path in Directory.GetFiles(configRoot, "*.json", SearchOption.AllDirectories))
                files.Add(NormalizeProjectPath(path));
            string modelRoot = root + "/resources/models";
            if (Directory.Exists(modelRoot))
                foreach (string path in Directory.GetFiles(modelRoot, "*.fbx", SearchOption.AllDirectories))
                    AddBakeAssetInput(files, NormalizeProjectPath(path));

            string profilePath = GeneratedCharacterActionsRoot + "/Profiles/" + package.PackageId + "/" + CharacterActionJsonImporter.DefaultProfilesSourcePath;
            files.Add(profilePath);
            string bundleRoot = GeneratedCharacterActionsRoot + "/ActionSets/" + package.PackageId;
            foreach (string path in Directory.GetFiles(bundleRoot, "*.json", SearchOption.TopDirectoryOnly))
                files.Add(NormalizeProjectPath(path));
            foreach (string path in ResolveCharacterActionLibraryPaths(
                profilePath,
                GeneratedCharacterActionsRoot + "/Libraries/" + package.PackageId + "/" + CharacterActionJsonImporter.DefaultActionsSourcePath,
                GeneratedCharacterActionsRoot + "/Libraries/Common/" + CharacterActionJsonImporter.DefaultActionsSourcePath))
                files.Add(path);

            JArray clips = JObject.Parse(File.ReadAllText(root + "/config/animation_clip_registry.json"))["clips"] as JArray;
            var clipKeys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (JObject clip in clips?.OfType<JObject>() ?? Array.Empty<JObject>())
            {
                string key = ReadString(clip, "runtimeResourceKey");
                if (!string.IsNullOrWhiteSpace(key)) clipKeys.Add(key);
            }
            CharacterWeaponTraceBakeRequest[] requests = CreateWeaponTraceBakeRequests(package, registry);
            actionSamplingProjection = CreateActionSamplingProjection(requests, registry);
            foreach (CharacterWeaponTraceBakeRequest request in requests)
                clipKeys.Add(request.ClipKey);
            foreach (string clipKey in clipKeys)
                AddBakeAssetInput(files, ResolveAnimationBakeAssetPath(root, clips, clipKey, out _));
            return new List<string>(files).ToArray();
        }

        internal static string CreateActionSamplingProjection(CharacterWeaponTraceBakeRequest[] requests, CharacterActionConfigRegistry registry = null)
        {
            var rows = new JArray();
            foreach (CharacterWeaponTraceBakeRequest request in requests)
            {
                var animation = registry?.Actions.FirstOrDefault(action => action.ActionId == request.ActionId)?.AnimationTrack;
                rows.Add(new JObject
                {
                    ["actionId"] = request.ActionId.Value, ["clipKey"] = request.ClipKey,
                    ["traceId"] = request.TraceId, ["start"] = request.ActiveStartFrame,
                    ["end"] = request.ActiveEndFrame, ["maxFrame"] = request.MaxFrame,
                    ["sourceClipHash"] = request.SourceClipHash,
                    ["animation"] = animation != null ? JToken.FromObject(animation) : JValue.CreateNull(),
                });
            }
            return rows.ToString(Formatting.None);
        }

        private static void AddBakeAssetInput(SortedSet<string> files, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            files.Add(path);
            files.Add(path + ".meta");
        }

        private static string ResolveAnimationBakeAssetPath(
            string importedPackageRoot, JArray clips, string runtimeResourceKey, out string sourceClipName)
        {
            sourceClipName = string.Empty;
            for (int i = 0; clips != null && i < clips.Count; i++)
            {
                JObject clip = clips[i] as JObject;
                if (clip == null)
                    continue;
                if (!string.Equals(ReadString(clip, "runtimeResourceKey"), runtimeResourceKey, StringComparison.Ordinal))
                    continue;

                string assetPath = ReadString(clip["sourceSelection"] as JObject, "unityAssetPath");
                sourceClipName = ReadString(clip, "sourceClipName");
                if (string.IsNullOrWhiteSpace(assetPath))
                    assetPath = ResolveUnityAssetPathForRuntimeKey(importedPackageRoot, runtimeResourceKey);
                return assetPath;
            }

            return ResolveSampleAnimationClipAssetPath(runtimeResourceKey);
        }

        private static string ResolveUnityAssetPathForRuntimeKey(
            string importedPackageRoot,
            string runtimeResourceKey)
        {
            string catalogPath = NormalizeProjectPath(importedPackageRoot) + "/config/unity_resource_catalog.json";
            if (!File.Exists(catalogPath) || string.IsNullOrWhiteSpace(runtimeResourceKey))
                return string.Empty;

            JObject root = JObject.Parse(File.ReadAllText(catalogPath));
            JArray entries = root["entries"] as JArray;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                JObject entry = entries[i] as JObject;
                if (entry == null
                    || !string.Equals(ReadString(entry, "id"), runtimeResourceKey, StringComparison.Ordinal))
                {
                    continue;
                }

                string assetPath = ReadString(entry, "unityAssetPath");
                if (string.IsNullOrWhiteSpace(assetPath))
                    assetPath = ReadString(entry["providerData"] as JObject, "unityAssetPath");
                if (string.IsNullOrWhiteSpace(assetPath))
                    assetPath = ReadString(entry, "address");
                return NormalizeProjectPath(assetPath);
            }

            return string.Empty;
        }

        private static string ResolveSampleAnimationClipAssetPath(string runtimeResourceKey)
        {
            const string prefix = "art.character.skeleton.animation.";
            if (string.IsNullOrWhiteSpace(runtimeResourceKey)
                || !runtimeResourceKey.StartsWith(prefix, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            string clipName = runtimeResourceKey.Substring(prefix.Length);
            if (string.IsNullOrWhiteSpace(clipName))
                return string.Empty;

            string path = "Assets/Art/MxFramework/Samples/Characters/Skeleton/AnimationClips/"
                + SanitizeName(clipName)
                + ".anim";
            return path;
        }

        private static string GetPreviewPrefabPath(CharacterImportedPackage package)
        {
            if (package == null || string.IsNullOrWhiteSpace(package.RootPath) || string.IsNullOrWhiteSpace(package.PackageId))
                return string.Empty;

            return NormalizeProjectPath(package.RootPath)
                + "/prefabs/"
                + package.PackageId
                + "_character_preview.prefab";
        }

        private static CharacterWeaponTraceBakeBuildIssue CreateWeaponTraceBakeIssue(
            string code,
            string message,
            CharacterActionId actionId,
            string clipKey,
            int traceId,
            int frame,
            string sourcePath,
            string sourceObjectPath)
        {
            return new CharacterWeaponTraceBakeBuildIssue(
                code,
                message,
                actionId,
                clipKey,
                traceId,
                frame,
                sourcePath,
                sourceObjectPath);
        }

        private static MxAnimationBakedVector3 Bake(Vector3 value, int quantizationScale)
        {
            return new MxAnimationBakedVector3(
                (long)Math.Round(value.x * quantizationScale),
                (long)Math.Round(value.y * quantizationScale),
                (long)Math.Round(value.z * quantizationScale));
        }

        private static int ResolveTraceSocketId(CharacterImportedPackage package, CharacterWeaponTraceRuntimeBinding trace)
        {
            if (trace == null) return 0;
            if (trace.SocketId > 0)
                return trace.SocketId;

            CharacterWeaponAttachmentRuntimeBinding[] attachments = package?.Geometry?.WeaponAttachments
                ?? Array.Empty<CharacterWeaponAttachmentRuntimeBinding>();
            for (int i = 0; i < attachments.Length; i++)
            {
                CharacterWeaponAttachmentRuntimeBinding attachment = attachments[i];
                if (attachment != null
                    && attachment.TraceId == trace.TraceId
                    && attachment.AttachSocketId > 0)
                {
                    return attachment.AttachSocketId;
                }
            }

            if (trace.EquipSlot > 0)
                return trace.EquipSlot;

            throw new InvalidOperationException("Weapon trace source requires a persisted numeric socket identity before baking.");
        }

        private static string CreateSampledTraceFingerprint(CharacterWeaponTraceRuntimeBinding trace, AnimationClip clip)
        {
            return "trace="
                + (trace?.TraceLabel ?? string.Empty)
                + "|weapon="
                + (trace?.WeaponLabel ?? string.Empty)
                + "|slot="
                + (trace?.EquipmentSlotLabel ?? string.Empty)
                + "|startLocator="
                + (trace?.StartLocatorPath ?? string.Empty)
                + "|endLocator="
                + (trace?.EndLocatorPath ?? string.Empty)
                + "|clip="
                + (clip != null ? clip.name : string.Empty)
                + "|proximalExtension="
                + (trace != null ? trace.ProximalExtension.ToString(System.Globalization.CultureInfo.InvariantCulture) : "0")
                + "|proximalRadius="
                + (trace != null ? trace.ProximalRadius.ToString(System.Globalization.CultureInfo.InvariantCulture) : "0")
                + "|source=sampledWeaponLocators|pose=runtimePlayablesNoRootMotion.v1";
        }

        private static string StableSuffix(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "none";

            var chars = new char[value.Length];
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                chars[i] = char.IsLetterOrDigit(c) ? c : '_';
            }

            return new string(chars);
        }

        private static bool TryLoadCharacterActionRegistryForPackage(
            CharacterImportedPackage package,
            out CharacterActionConfigRegistry registry,
            out string issue)
        {
            registry = null;
            issue = string.Empty;
            string packageId = package?.PackageId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(packageId))
            {
                issue = "Package id is required before loading CharacterAction config.";
                return false;
            }

            string actionSetPath = GeneratedCharacterActionsRoot
                + "/ActionSets/"
                + SanitizeName(packageId)
                + "/"
                + CharacterActionJsonImporter.DefaultActionSetsSourcePath;
            string actionProfilePath = GeneratedCharacterActionsRoot
                + "/Profiles/"
                + SanitizeName(packageId)
                + "/"
                + CharacterActionJsonImporter.DefaultProfilesSourcePath;
            string packageActionsPath = GeneratedCharacterActionsRoot
                + "/Libraries/"
                + SanitizeName(packageId)
                + "/"
                + CharacterActionJsonImporter.DefaultActionsSourcePath;
            string commonActionsPath = GeneratedCharacterActionsRoot
                + "/Libraries/"
                + DefaultCharacterActionLibraryId
                + "/"
                + CharacterActionJsonImporter.DefaultActionsSourcePath;
            if (!File.Exists(actionSetPath))
            {
                issue = "CharacterAction ActionSet file is missing: " + actionSetPath;
                return false;
            }

            if (!File.Exists(actionProfilePath))
            {
                issue = "CharacterAction Profile file is missing: " + actionProfilePath;
                return false;
            }

            if (!File.Exists(packageActionsPath))
            {
                issue = "CharacterAction package action library file is missing: " + packageActionsPath;
                return false;
            }

            if (!File.Exists(commonActionsPath))
            {
                issue = "CharacterAction common action library file is missing: " + commonActionsPath;
                return false;
            }

            string mergedActionsJson;
            string[] actionLibraryPaths;
            try
            {
                actionLibraryPaths = ResolveCharacterActionLibraryPaths(
                    actionProfilePath,
                    packageActionsPath,
                    commonActionsPath);
                mergedActionsJson = MergeCharacterActionLibraries(actionLibraryPaths);
            }
            catch (Exception ex)
            {
                issue = "CharacterAction libraries could not be merged: " + ex.Message;
                return false;
            }

            string actionsSourcePath = string.Join(", ", actionLibraryPaths);
            CharacterActionJsonImportResult import = CharacterActionJsonImporter.Import(
                File.ReadAllText(actionSetPath),
                mergedActionsJson,
                actionSetPath,
                actionsSourcePath);
            if (import == null || !import.Success)
            {
                string diagnostic = import != null && import.Diagnostics.Length > 0
                    ? import.Diagnostics[0].Code + " " + import.Diagnostics[0].Message
                    : "unknown import failure";
                issue = "CharacterAction import failed while preparing weapon trace bake validation: " + diagnostic;
                return false;
            }

            registry = new CharacterActionConfigRegistry(
                import.ActionSets,
                import.Actions,
                actionSetPath,
                actionsSourcePath);
            return true;
        }

        private static string[] ResolveCharacterActionLibraryPaths(
            string actionProfilePath,
            string packageActionsPath,
            string commonActionsPath)
        {
            var result = new List<string>();
            AddUniquePath(result, packageActionsPath);
            AddUniquePath(result, commonActionsPath);
            JObject root = JObject.Parse(File.ReadAllText(actionProfilePath));
            JArray profiles = root["profiles"] as JArray
                ?? throw new InvalidDataException("CharacterAction profile document has no profiles array: " + actionProfilePath);
            for (int profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
            {
                JArray libraryIds = profiles[profileIndex]?["actionLibraryIds"] as JArray;
                for (int libraryIndex = 0; libraryIds != null && libraryIndex < libraryIds.Count; libraryIndex++)
                {
                    string libraryId = libraryIds[libraryIndex]?.Value<string>()?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(libraryId))
                        continue;
                    string path = GeneratedCharacterActionsRoot
                        + "/Libraries/"
                        + SanitizeName(libraryId)
                        + "/"
                        + CharacterActionJsonImporter.DefaultActionsSourcePath;
                    if (!File.Exists(path))
                        throw new FileNotFoundException("Referenced CharacterAction library is missing.", path);
                    AddUniquePath(result, path);
                }
            }

            return result.ToArray();
        }

        private static void AddUniquePath(List<string> paths, string path)
        {
            if (!paths.Contains(path))
                paths.Add(path);
        }

        private static string MergeCharacterActionLibraries(params string[] paths)
        {
            var merged = new JArray();
            for (int i = 0; paths != null && i < paths.Length; i++)
                AppendCharacterActionLibrary(paths[i], merged);
            return new JObject
            {
                ["schemaVersion"] = CharacterActionJsonImporter.CurrentSchemaVersion,
                ["actions"] = merged
            }.ToString(Formatting.None);
        }

        private static void AppendCharacterActionLibrary(string path, JArray merged)
        {
            JObject root = JObject.Parse(File.ReadAllText(path));
            JArray actions = root["actions"] as JArray
                ?? throw new InvalidDataException("CharacterAction library has no actions array: " + path);
            for (int i = 0; i < actions.Count; i++)
                merged.Add(actions[i].DeepClone());
        }

        private static CharacterWeaponTraceBakeRequest[] CreateWeaponTraceBakeRequests(
            CharacterImportedPackage package,
            CharacterActionConfigRegistry registry)
        {
            var requests = new List<CharacterWeaponTraceBakeRequest>();
            CharacterActionConfig[] actions = registry?.Actions ?? Array.Empty<CharacterActionConfig>();
            for (int actionIndex = 0; actionIndex < actions.Length; actionIndex++)
            {
                CharacterActionConfig action = actions[actionIndex];
                if (action == null)
                    continue;

                CombatTrackEvent[] combatEvents = action.CombatTrack?.Events
                    ?? Array.Empty<CombatTrackEvent>();
                for (int eventIndex = 0; eventIndex < combatEvents.Length; eventIndex++)
                {
                    CombatTrackEvent start = combatEvents[eventIndex];
                    if (start.Kind != CharacterActionTrackEventKind.HitTraceWindow
                        || start.HitVolume.HasVolume)
                    {
                        continue;
                    }

                    int endFrame = start.EndFrame;
                    string clipKey = ResolveActiveClipKey(action, start.Frame);
                    int traceId = CharacterWeaponTraceBakeCatalogBuilder.ResolveTraceIdForEquipmentSlot(
                        package,
                        start.EquipmentSlotId,
                        start.TraceProfileId);
                    AddOrExpandWeaponTraceBakeRequest(
                        requests,
                        new CharacterWeaponTraceBakeRequest(
                            action.ActionId,
                            clipKey,
                            traceId,
                            Math.Max(0, start.Frame),
                            Math.Max(start.Frame, endFrame),
                            ResolveTraceBakeMaxFrame(action, endFrame),
                            "unity-import.action="
                            + action.ActionId
                            + "|clip="
                            + (clipKey ?? string.Empty)
                            + "|trace="
                            + (traceId.ToString(System.Globalization.CultureInfo.InvariantCulture))));
                }
            }

            return requests.ToArray();
        }

        internal static BodyPartContactMotionBakeRequest[] CreateBodyPartContactMotionBakeRequests(
            CharacterImportedPackage package)
        {
            int groupSocketId = ResolveContactMotionGroupSocketId(package);
            if (groupSocketId <= 0)
                return Array.Empty<BodyPartContactMotionBakeRequest>();

            string registryPath = NormalizeProjectPath(package.RootPath) + "/config/animation_clip_registry.json";
            if (!File.Exists(registryPath))
                return Array.Empty<BodyPartContactMotionBakeRequest>();

            var requests = new List<BodyPartContactMotionBakeRequest>();
            var clipKeys = new HashSet<string>(StringComparer.Ordinal);
            JObject root = JObject.Parse(File.ReadAllText(registryPath));
            JArray clips = root["clips"] as JArray;
            for (int i = 0; clips != null && i < clips.Count; i++)
            {
                JObject clip = clips[i] as JObject;
                if (clip == null)
                    continue;

                string clipKey = ReadString(clip, "runtimeResourceKey");
                if (string.IsNullOrWhiteSpace(clipKey) || !clipKeys.Add(clipKey))
                    continue;

                AnimationClip animationClip = LoadAnimationClipForRuntimeKey(package.RootPath, clipKey);
                int maxFrame = ResolveAnimationClipBakeMaxFrame(animationClip);
                requests.Add(new BodyPartContactMotionBakeRequest(
                    clipKey,
                    groupSocketId,
                    maxFrame,
                    FirstNonEmpty(ReadString(clip, "hash"), "unity-import.bodyPartContactMotion|clip=" + clipKey)));
            }
            return requests.ToArray();
        }

        private static int ResolveContactMotionGroupSocketId(CharacterImportedPackage package)
        {
            int groupSocketId = 0;
            CharacterBodyPartContactPointConfig[] contactPoints = package?.Configs?.BodyPartContactPoints
                ?? Array.Empty<CharacterBodyPartContactPointConfig>();
            for (int i = 0; i < contactPoints.Length; i++)
            {
                CharacterBodyPartContactPointConfig contactPoint = contactPoints[i];
                if (contactPoint != null
                    && contactPoint.MotionMode == CharacterBodyPartContactMotionMode.BakedSocket
                    && contactPoint.MotionSocketId > 0)
                {
                    groupSocketId = groupSocketId == 0
                        ? contactPoint.MotionSocketId
                        : Math.Min(groupSocketId, contactPoint.MotionSocketId);
                }
            }

            return groupSocketId;
        }

        private static int ResolveAnimationClipBakeMaxFrame(AnimationClip clip)
        {
            if (clip == null || clip.length <= 0f)
                return 1;

            return Math.Max(1, (int)Math.Ceiling(clip.length * 60f));
        }

        private static void AddOrExpandWeaponTraceBakeRequest(
            List<CharacterWeaponTraceBakeRequest> requests,
            CharacterWeaponTraceBakeRequest request)
        {
            if (requests == null
                || request == null
                || string.IsNullOrWhiteSpace(request.ClipKey)
                || (request.TraceId <= 0))
            {
                return;
            }

            for (int i = 0; i < requests.Count; i++)
            {
                CharacterWeaponTraceBakeRequest existing = requests[i];
                if (existing == null
                    || !string.Equals(existing.ClipKey, request.ClipKey, StringComparison.Ordinal)
                    || existing.TraceId != request.TraceId)
                {
                    continue;
                }

                requests[i] = new CharacterWeaponTraceBakeRequest(
                    existing.ActionId.IsValid ? existing.ActionId : request.ActionId,
                    existing.ClipKey,
                    existing.TraceId,
                    Math.Min(existing.ActiveStartFrame, request.ActiveStartFrame),
                    Math.Max(existing.ActiveEndFrame, request.ActiveEndFrame),
                    Math.Max(existing.MaxFrame, request.MaxFrame),
                    FirstNonEmpty(existing.SourceClipHash, request.SourceClipHash));
                return;
            }

            requests.Add(request);
        }

        private static string ResolveActiveClipKey(CharacterActionConfig action, int frame)
        {
            AnimationTrackEvent[] events = action.AnimationTrack?.Events
                ?? Array.Empty<AnimationTrackEvent>();
            string fallback = string.Empty;
            for (int i = 0; i < events.Length; i++)
            {
                AnimationTrackEvent candidate = events[i];
                string key = candidate.EffectiveAnimationKey;
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                if (string.IsNullOrWhiteSpace(fallback))
                    fallback = key;
                if (frame >= candidate.Frame && frame <= candidate.EndFrame)
                    return key;
            }

            return fallback;
        }

        private static int ResolveTraceBakeMaxFrame(CharacterActionConfig action, int endFrame)
        {
            int maxFrame = Math.Max(1, endFrame);
            if (action.DurationFrames.HasValue)
                maxFrame = Math.Max(maxFrame, action.DurationFrames.Value);

            AnimationTrackEvent[] events = action.AnimationTrack?.Events
                ?? Array.Empty<AnimationTrackEvent>();
            for (int i = 0; i < events.Length; i++)
                maxFrame = Math.Max(maxFrame, events[i].EndFrame);
            return maxFrame;
        }

        private static CharacterWeaponTraceBakeIssueReport[] ConvertWeaponTraceBakeIssues(
            CharacterWeaponTraceBakeBuildIssue[] issues)
        {
            if (issues == null || issues.Length == 0)
                return Array.Empty<CharacterWeaponTraceBakeIssueReport>();

            var result = new CharacterWeaponTraceBakeIssueReport[issues.Length];
            for (int i = 0; i < issues.Length; i++)
            {
                CharacterWeaponTraceBakeBuildIssue issue = issues[i];
                result[i] = new CharacterWeaponTraceBakeIssueReport
                {
                    Code = issue.Code,
                    Message = issue.Message,
                    ActionId = issue.ActionId.Value,
                    ClipKey = issue.ClipKey,
                    TraceId = issue.TraceId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Frame = issue.Frame,
                    SourcePath = issue.SourcePath,
                    SourceObjectPath = issue.SourceObjectPath
                };
            }

            return result;
        }

        private static string ComputeWeaponTraceBakeHash(
            CharacterImportedPackage package,
            CharacterWeaponTraceBakeRequest[] requests)
        {
            var builder = new StringBuilder();
            builder.Append("contact-motion-independent-v1|");
            builder.Append(package?.ImportReport?.GeometryBindingHash ?? string.Empty);
            builder.Append('|');
            builder.Append(package?.ImportReport?.ResourceMappingHash ?? string.Empty);
            builder.Append('|').Append(JsonConvert.SerializeObject(package?.Configs?.BodyPartContactPoints));
            string contactRegistryPath = NormalizeProjectPath(package.RootPath) + "/config/animation_clip_registry.json";
            if (File.Exists(contactRegistryPath))
                builder.Append('|').Append(ComputeStringContentHash(File.ReadAllText(contactRegistryPath)));
            for (int i = 0; requests != null && i < requests.Length; i++)
            {
                CharacterWeaponTraceBakeRequest request = requests[i];
                if (request == null)
                    continue;

                builder.Append('|')
                    .Append(request.ActionId.Value)
                    .Append(':')
                    .Append(request.ClipKey)
                    .Append(':')
                    .Append(request.TraceId)
                    .Append(':')
                    .Append(request.ActiveStartFrame.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Append("..")
                    .Append(request.ActiveEndFrame.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
                byte[] hash = sha.ComputeHash(bytes);
                return "sha256:" + ToHex(hash);
            }
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            var chars = new char[bytes.Length * 2];
            const string alphabet = "0123456789abcdef";
            for (int i = 0; i < bytes.Length; i++)
            {
                chars[i * 2] = alphabet[bytes[i] >> 4];
                chars[(i * 2) + 1] = alphabet[bytes[i] & 0x0F];
            }

            return new string(chars);
        }

        private static string[] AppendIssue(string[] issues, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return issues ?? Array.Empty<string>();

            var result = new List<string>(issues ?? Array.Empty<string>())
            {
                message
            };
            return result.ToArray();
        }

        private static string ComputeUnityArtifactAggregateHash(ResourceCatalog catalog)
        {
            if (catalog == null || catalog.Entries == null || catalog.Entries.Count == 0)
                return string.Empty;

            var values = new List<string>();
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                ResourceCatalogEntry entry = catalog.Entries[i];
                if (entry == null || entry.ProviderData == null)
                    continue;

                if (entry.ProviderData.TryGetValue("artifactHash", out string artifactHash)
                    && !string.IsNullOrWhiteSpace(artifactHash))
                {
                    values.Add(entry.Id + "=" + artifactHash);
                }

                if (entry.ProviderData.TryGetValue("avatarSourceContentHash", out string avatarSourceContentHash)
                    && !string.IsNullOrWhiteSpace(avatarSourceContentHash))
                {
                    values.Add(entry.Id + ".avatarSource=" + avatarSourceContentHash);
                }
            }

            if (values.Count == 0)
                return string.Empty;

            values.Sort(StringComparer.Ordinal);
            return ComputeStringContentHash(string.Join("\n", values));
        }

        private static string ComputeStringContentHash(string value)
        {
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            return "sha256:" + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
