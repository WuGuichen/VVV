using System.Net;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MxFramework.Authoring;

namespace MxFramework.Authoring.Cli;

internal static partial class EditorServer
{
    internal static List<object> ListAnimationPackages(string rootPath, string defaultPackage, JsonSerializerOptions jsonOptions)
    {
        var result = new List<object>();
        AddAnimationPackageListItem(result, rootPath, IsCharacterPackage(rootPath, defaultPackage) ? defaultPackage : DefaultCharacterPackageRelativePath, jsonOptions);

        foreach (string dir in EnumerateCharacterPackageDirectories(rootPath))
        {
            string relative = ToAuthoringRelativePath(rootPath, dir);
            AddAnimationPackageListItem(result, rootPath, relative, jsonOptions);
        }

        return result;
    }

    private static void AddAnimationPackageListItem(List<object> result, string rootPath, string relative, JsonSerializerOptions jsonOptions)
    {
        if (!IsCharacterPackage(rootPath, relative))
            return;

        string packagePath = ResolveSafePath(rootPath, relative);
        string documentPath = Path.Combine(packagePath, AnimationAuthoringDocumentRelativePath);
        CharacterPackageManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<CharacterPackageManifest>(File.ReadAllText(Path.Combine(packagePath, "manifest.json")), jsonOptions) ?? new CharacterPackageManifest();
        }
        catch
        {
            return;
        }

        string normalized = ToAuthoringRelativePath(rootPath, packagePath);
        if (result.Any(item => string.Equals(GetAnonymousProperty(item, "relative"), normalized, StringComparison.Ordinal)))
            return;

        AnimationAuthoringPackage animation = File.Exists(documentPath)
            ? ReadAnimationPackage(rootPath, documentPath, jsonOptions)
            : CreateAnimationPackageFromCharacterManifest(manifest);

        result.Add(new
        {
            relative = normalized,
            packageId = string.IsNullOrWhiteSpace(animation.PackageId) ? manifest.PackageId : animation.PackageId,
            characterPackageId = manifest.PackageId,
            stableId = string.IsNullOrWhiteSpace(animation.StableId) ? manifest.StableId : animation.StableId,
            displayName = string.IsNullOrWhiteSpace(animation.DisplayName) ? manifest.DisplayName : animation.DisplayName,
            document = ToAuthoringRelativePath(rootPath, documentPath),
            exists = File.Exists(documentPath),
            kind = "AnimationAuthoring"
        });
    }

    internal static object ReadAnimationPackageState(string rootPath, string packageOrPath, string defaultPackage, JsonSerializerOptions jsonOptions)
    {
        string documentPath = ResolveAnimationDocumentPath(rootPath, packageOrPath, defaultPackage, jsonOptions);
        AnimationAuthoringPackage package = ReadAnimationPackage(rootPath, documentPath, jsonOptions);
        return new
        {
            packageRelative = ToAnimationPackageRelative(rootPath, documentPath),
            documentRelative = ToAuthoringRelativePath(rootPath, documentPath),
            exists = File.Exists(documentPath),
            canWrite = true,
            package,
            fieldSpecs = CreateAnimationFieldSpecs(),
            validation = ValidateAnimationPackage(package)
        };
    }

    internal static object SaveAnimationPackage(
        string rootPath,
        string packageOrPath,
        string defaultPackage,
        AnimationAuthoringPackage package,
        JsonSerializerOptions jsonOptions)
    {
        if (package == null)
            throw new ArgumentException("animation package is required.");

        string documentPath = ResolveAnimationDocumentPath(rootPath, packageOrPath, defaultPackage, jsonOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(documentPath)!);
        WriteJsonFileAtomic(documentPath, package, jsonOptions);
        return ReadAnimationPackageState(rootPath, ToAuthoringRelativePath(rootPath, documentPath), defaultPackage, jsonOptions);
    }

    internal static CharacterAuthoringValidationReport ValidateAnimationPackage(AnimationAuthoringPackage package)
    {
        var report = new CharacterAuthoringValidationReport
        {
            PackageId = package != null ? package.PackageId ?? string.Empty : string.Empty
        };
        if (package == null)
        {
            report.Issues.Add(CreateAnimationValidationIssue("ANIM_PACKAGE_MISSING", "package", string.Empty, "Animation authoring package is missing.", CharacterAuthoringValidationSeverity.Error));
            return report;
        }

        var setIds = new HashSet<string>(StringComparer.Ordinal);
        for (int setIndex = 0; package.Sets != null && setIndex < package.Sets.Count; setIndex++)
        {
            AnimationAuthoringSet set = package.Sets[setIndex];
            if (set == null)
                continue;
            AddDuplicateIssue(report, setIds, set.SetId, "ANIM_DUPLICATE_SET_ID", "sets/" + setIndex, "setId");

            var layerIds = new HashSet<string>(StringComparer.Ordinal);
            for (int layerIndex = 0; set.Layers != null && layerIndex < set.Layers.Count; layerIndex++)
            {
                AnimationLayerAuthoring layer = set.Layers[layerIndex];
                if (layer != null)
                    AddDuplicateIssue(report, layerIds, layer.LayerId, "ANIM_DUPLICATE_LAYER_ID", "sets/" + setIndex + "/layers/" + layerIndex, "layerId");
            }

            var groupIds = new HashSet<string>(StringComparer.Ordinal);
            for (int groupIndex = 0; set.Groups != null && groupIndex < set.Groups.Count; groupIndex++)
            {
                AnimationGroupAuthoring group = set.Groups[groupIndex];
                if (group == null)
                    continue;
                AddDuplicateIssue(report, groupIds, group.GroupId, "ANIM_DUPLICATE_GROUP_ID", "sets/" + setIndex + "/groups/" + groupIndex, "groupId");

                var clipIds = new HashSet<string>(StringComparer.Ordinal);
                for (int clipIndex = 0; group.Clips != null && clipIndex < group.Clips.Count; clipIndex++)
                {
                    AnimationClipMappingAuthoring clip = group.Clips[clipIndex];
                    if (clip == null)
                        continue;
                    string path = "sets/" + setIndex + "/groups/" + groupIndex + "/clips/" + clipIndex;
                    AddDuplicateIssue(report, clipIds, clip.ClipId, "ANIM_DUPLICATE_CLIP_ID", path, "clipId");
                    if (IsSelectionEmpty(clip.SourceSelection))
                    {
                        report.Issues.Add(CreateAnimationValidationIssue(
                            "ANIM_MISSING_SOURCE_SELECTION",
                            path,
                            "sourceSelection",
                            "Animation clip mapping is missing a source ResourceSelectionRef.",
                            CharacterAuthoringValidationSeverity.Error));
                    }
                }

                var blend1DIds = new HashSet<string>(StringComparer.Ordinal);
                for (int blendIndex = 0; group.Blend1D != null && blendIndex < group.Blend1D.Count; blendIndex++)
                {
                    AnimationBlend1DAuthoring blend = group.Blend1D[blendIndex];
                    if (blend != null)
                        AddDuplicateIssue(report, blend1DIds, blend.BlendId, "ANIM_DUPLICATE_BLEND_ID", "sets/" + setIndex + "/groups/" + groupIndex + "/blend1D/" + blendIndex, "blendId");
                }

                var blend2DIds = new HashSet<string>(StringComparer.Ordinal);
                for (int blendIndex = 0; group.Blend2D != null && blendIndex < group.Blend2D.Count; blendIndex++)
                {
                    AnimationBlend2DAuthoring blend = group.Blend2D[blendIndex];
                    if (blend != null)
                        AddDuplicateIssue(report, blend2DIds, blend.BlendId, "ANIM_DUPLICATE_BLEND_ID", "sets/" + setIndex + "/groups/" + groupIndex + "/blend2D/" + blendIndex, "blendId");
                }

                var timelineIds = new HashSet<string>(StringComparer.Ordinal);
                for (int timelineIndex = 0; group.Timelines != null && timelineIndex < group.Timelines.Count; timelineIndex++)
                {
                    AnimationTimelineAuthoring timeline = group.Timelines[timelineIndex];
                    if (timeline == null)
                        continue;
                    AddDuplicateIssue(report, timelineIds, timeline.TimelineId, "ANIM_DUPLICATE_TIMELINE_ID", "sets/" + setIndex + "/groups/" + groupIndex + "/timelines/" + timelineIndex, "timelineId");

                    var eventIds = new HashSet<string>(StringComparer.Ordinal);
                    for (int eventIndex = 0; timeline.Events != null && eventIndex < timeline.Events.Count; eventIndex++)
                    {
                        AnimationTimelineEventAuthoring timelineEvent = timeline.Events[eventIndex];
                        if (timelineEvent != null)
                            AddDuplicateIssue(report, eventIds, timelineEvent.EventId, "ANIM_DUPLICATE_EVENT_ID", "sets/" + setIndex + "/groups/" + groupIndex + "/timelines/" + timelineIndex + "/events/" + eventIndex, "eventId");
                    }
                }
            }
        }

        var profileIds = new HashSet<string>(StringComparer.Ordinal);
        for (int profileIndex = 0; package.Profiles != null && profileIndex < package.Profiles.Count; profileIndex++)
        {
            AnimationAuthoringProfile profile = package.Profiles[profileIndex];
            if (profile == null)
                continue;
            AddDuplicateIssue(report, profileIds, profile.ProfileId, "ANIM_DUPLICATE_PROFILE_ID", "profiles/" + profileIndex, "profileId");
        }

        return report;
    }

    internal static AnimationAuthoringCompileResult CompileAnimationPackage(
        string rootPath,
        string packageOrPath,
        string defaultPackage,
        AnimationAuthoringPackage package,
        JsonSerializerOptions jsonOptions)
    {
        string documentPath = ResolveAnimationDocumentPath(rootPath, packageOrPath, defaultPackage, jsonOptions);
        AnimationAuthoringPackage animation = package ?? ReadAnimationPackage(rootPath, documentPath, jsonOptions);
        string packageRoot = ResolveAnimationPackageRootPath(documentPath);
        CharacterPackageResourceCatalog catalog = ReadAnimationResourceCatalog(packageRoot, jsonOptions);
        return AnimationAuthoringCompiler.Compile(new AnimationAuthoringCompileRequest
        {
            Package = animation,
            PackageRootPath = packageRoot,
            ResourceCatalog = catalog,
            RuntimeResourceCatalog = AnimationPackageCommands.ReadProjectRuntimeResourceCatalogs(rootPath, jsonOptions)
        });
    }

    internal static object ReadAnimationPreview(
        string rootPath,
        string packageOrPath,
        string defaultPackage,
        AnimationAuthoringPackage package,
        JsonSerializerOptions jsonOptions)
    {
        string documentPath = ResolveAnimationDocumentPath(rootPath, packageOrPath, defaultPackage, jsonOptions);
        string packageRoot = ResolveAnimationPackageRootPath(documentPath);
        string packageRelative = ToAnimationPackageRelative(rootPath, documentPath);
        AnimationAuthoringCompileResult compile = CompileAnimationPackage(rootPath, packageOrPath, defaultPackage, package, jsonOptions);
        CharacterPackageResourceCatalog catalog = ReadAnimationResourceCatalog(packageRoot, jsonOptions);
        AuthoringResourceCollection authoringResources = TryReadAuthoringResources(rootPath, packageRelative, jsonOptions);

        return new
        {
            package = packageRelative,
            packageRoot = ToAuthoringRelativePath(rootPath, packageRoot),
            serviceStatus = "ready",
            compileResult = compile,
            animationSetDefinition = compile.AnimationSetDefinition,
            animationClipRegistry = compile.AnimationClipRegistry,
            animationResourcePlan = compile.AnimationResourcePlan,
            animationValidationReport = compile.AnimationValidationReport,
            previewResources = BuildAnimationPreviewResources(rootPath, packageRoot, catalog, compile.AnimationClipRegistry, compile.AnimationSetDefinition),
            unityPreviewReport = BuildAnimationUnityPreviewReport(rootPath, packageRoot, catalog, compile.AnimationClipRegistry, compile.AnimationSetDefinition, authoringResources),
            diagnostics = compile.AnimationValidationReport?.Issues ?? new List<CharacterAuthoringValidationIssue>()
        };
    }

    private static AuthoringResourceCollection TryReadAuthoringResources(string rootPath, string packageRelative, JsonSerializerOptions jsonOptions)
    {
        try
        {
            return ReadAuthoringResources(rootPath, packageRelative, jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static object BuildAnimationPreviewResources(
        string rootPath,
        string packageRoot,
        CharacterPackageResourceCatalog catalog,
        AnimationClipRegistryDocument clipRegistry,
        AnimationSetDefinitionDocument setDefinition)
    {
        var resourcesByKey = new Dictionary<string, object>(StringComparer.Ordinal);
        var entriesByKey = new Dictionary<string, CharacterPackageResourceEntry>(StringComparer.Ordinal);
        for (int i = 0; catalog != null && catalog.Entries != null && i < catalog.Entries.Count; i++)
        {
            CharacterPackageResourceEntry entry = catalog.Entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.ResourceKey))
                continue;

            string fullPath = CharacterPackageResourcePipeline.ResolvePackagePath(packageRoot, entry.RelativePath);
            string projectRelative = !string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath)
                ? ToProjectRelativePath(rootPath, fullPath)
                : string.Empty;
            string url = string.IsNullOrWhiteSpace(projectRelative)
                ? string.Empty
                : "/" + projectRelative.Replace('\\', '/');
            resourcesByKey[entry.ResourceKey] = new
            {
                resourceKey = entry.ResourceKey,
                stableId = entry.StableId,
                kind = entry.TypeId,
                usage = entry.Usage,
                relativePath = entry.RelativePath,
                projectRelativePath = projectRelative,
                url,
                exists = !string.IsNullOrWhiteSpace(projectRelative),
                tags = entry.Tags ?? new List<string>()
            };
            entriesByKey[entry.ResourceKey] = entry;
        }

        var animationClips = new List<object>();
        for (int i = 0; clipRegistry != null && clipRegistry.Clips != null && i < clipRegistry.Clips.Count; i++)
        {
            AnimationClipRegistryEntry clip = clipRegistry.Clips[i];
            resourcesByKey.TryGetValue(clip.RuntimeResourceKey ?? string.Empty, out object resource);
            object previewModelResource = null;
            string previewModelResourceKey = string.Empty;
            if (entriesByKey.TryGetValue(clip.RuntimeResourceKey ?? string.Empty, out CharacterPackageResourceEntry clipResourceEntry))
                previewModelResourceKey = clipResourceEntry.Preview?.PreviewMeshResourceKey ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(previewModelResourceKey))
                resourcesByKey.TryGetValue(previewModelResourceKey, out previewModelResource);
            AnimationSetDefinitionClipRef clipRef = FindAnimationSetDefinitionClip(setDefinition, clip);
            animationClips.Add(new
            {
                setId = clip.SetId,
                groupId = clip.GroupId,
                clipId = clip.ClipId,
                displayName = clip.DisplayName,
                sourceClipName = clip.SourceClipName,
                sourceSubClipId = clip.SourceSubClipId,
                runtimeResourceKey = clip.RuntimeResourceKey,
                loop = clipRef != null && clipRef.Loop,
                speed = clipRef != null ? clipRef.Speed : 1f,
                rootMotionPolicy = clipRef != null ? clipRef.RootMotionPolicy : string.Empty,
                resource,
                animationResource = resource,
                previewModelResourceKey,
                previewModelResource
            });
        }

        return new
        {
            resources = resourcesByKey.Values.ToArray(),
            animationClips
        };
    }

    private static object BuildAnimationUnityPreviewReport(
        string rootPath,
        string packageRoot,
        CharacterPackageResourceCatalog catalog,
        AnimationClipRegistryDocument clipRegistry,
        AnimationSetDefinitionDocument setDefinition,
        AuthoringResourceCollection authoringResources)
    {
        var clipReports = new List<object>();
        for (int i = 0; clipRegistry != null && clipRegistry.Clips != null && i < clipRegistry.Clips.Count; i++)
        {
            AnimationClipRegistryEntry clip = clipRegistry.Clips[i];
            AnimationSetDefinitionClipRef clipRef = FindAnimationSetDefinitionClip(setDefinition, clip);
            CharacterPackageResourceEntry packageEntry = FindPackageResourceEntryByKey(catalog, clip.RuntimeResourceKey);
            CharacterPackageResourceEntry previewModelEntry = FindPackageResourceEntryByKey(catalog, packageEntry?.Preview?.PreviewMeshResourceKey);
            if (previewModelEntry == null)
                previewModelEntry = FindDefaultAnimationPreviewModel(catalog);
            AuthoringResourceItem sourceItem = FindAnimationSourceResource(authoringResources, clip.SourceSelection, clip.RuntimeResourceKey);
            AuthoringResourceProviderBinding unityBinding = GetAuthoringUnityBinding(sourceItem);
            AuthoringResourceProviderBinding runtimeBinding = GetAuthoringRuntimeBinding(sourceItem);
            string unityAssetPath = FirstNonEmpty(
                clip.SourceSelection != null ? clip.SourceSelection.UnityAssetPath : string.Empty,
                unityBinding != null ? unityBinding.UnityAssetPath : string.Empty);
            string unityGuid = FirstNonEmpty(
                clip.SourceSelection != null ? clip.SourceSelection.UnityGuid : string.Empty,
                unityBinding != null ? unityBinding.UnityGuid : string.Empty);
            string runtimeResourceKey = FirstNonEmpty(
                clip.RuntimeResourceKey,
                clip.SourceSelection != null ? clip.SourceSelection.RuntimeResourceKey : string.Empty,
                runtimeBinding != null ? runtimeBinding.RuntimeResourceKey : string.Empty);
            string subClipId = FirstNonEmpty(
                clip.SourceSubClipId,
                GetAuthoringResourceMetadata(sourceItem, "subClipId"),
                GetAuthoringResourceMetadata(sourceItem, "subClipName"),
                GetAuthoringResourceMetadata(sourceItem, "clipName"),
                clip.SourceClipName);
            bool unityAssetExists = IsUnityAssetPathPresent(rootPath, unityAssetPath);
            bool canPreviewInUnity = !string.IsNullOrWhiteSpace(unityAssetPath) && unityAssetExists && IsAnimationClipPreviewCandidate(sourceItem, packageEntry);
            bool canPreviewInWeb = IsWebAnimationPreviewCandidate(packageEntry, packageRoot);
            List<object> diagnostics = BuildAnimationUnityPreviewDiagnostics(clip, sourceItem, packageEntry, previewModelEntry, unityAssetPath, unityAssetExists, canPreviewInUnity, canPreviewInWeb);

            clipReports.Add(new
            {
                setId = clip.SetId,
                groupId = clip.GroupId,
                clipId = clip.ClipId,
                displayName = clip.DisplayName,
                runtimeResourceKey,
                sourceStableId = clip.SourceSelection != null ? clip.SourceSelection.ResourceStableId : string.Empty,
                sourceProviderId = clip.SourceSelection != null ? clip.SourceSelection.SourceProviderId : string.Empty,
                sourceBindingKind = clip.SourceSelection != null ? clip.SourceSelection.BindingKind.ToString() : AuthoringResourceBindingKind.None.ToString(),
                sourceSubClipId = subClipId,
                sourceClipName = FirstNonEmpty(clip.SourceClipName, subClipId),
                loop = clipRef != null && clipRef.Loop,
                speed = clipRef != null ? clipRef.Speed : 1f,
                rootMotionPolicy = clipRef != null ? clipRef.RootMotionPolicy : string.Empty,
                canPreviewInUnity,
                canPreviewInWeb,
                previewAuthority = canPreviewInUnity ? "UnityPreview" : canPreviewInWeb ? "WebPreviewArtifact" : "Unavailable",
                unity = new
                {
                    unityGuid,
                    unityAssetPath,
                    unityAssetExists,
                    subClipId,
                    assetType = FirstNonEmpty(unityBinding != null ? unityBinding.AssetType : string.Empty, GetAuthoringResourceMetadata(sourceItem, "assetType"))
                },
                web = new
                {
                    resourceKey = packageEntry != null ? packageEntry.ResourceKey : string.Empty,
                    relativePath = packageEntry != null ? packageEntry.RelativePath : string.Empty,
                    previewModelResourceKey = previewModelEntry != null ? previewModelEntry.ResourceKey : string.Empty,
                    previewModelRelativePath = previewModelEntry != null ? previewModelEntry.RelativePath : string.Empty
                },
                diagnostics
            });
        }

        return new
        {
            mode = "UnityAuthoritativePreview",
            description = "Unity preview is authoritative when canPreviewInUnity is true. Web preview artifacts are approximate editor aids.",
            clips = clipReports
        };
    }

    private static CharacterPackageResourceEntry FindDefaultAnimationPreviewModel(CharacterPackageResourceCatalog catalog)
    {
        for (int i = 0; catalog != null && catalog.Entries != null && i < catalog.Entries.Count; i++)
        {
            CharacterPackageResourceEntry entry = catalog.Entries[i];
            if (entry != null &&
                string.Equals(entry.TypeId, CharacterPackageResourceTypeIds.Model, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.Usage, CharacterPackageResourceUsageIds.CharacterModel, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        return null;
    }

    private static List<object> BuildAnimationUnityPreviewDiagnostics(
        AnimationClipRegistryEntry clip,
        AuthoringResourceItem sourceItem,
        CharacterPackageResourceEntry packageEntry,
        CharacterPackageResourceEntry previewModelEntry,
        string unityAssetPath,
        bool unityAssetExists,
        bool canPreviewInUnity,
        bool canPreviewInWeb)
    {
        var diagnostics = new List<object>();
        if (sourceItem == null && string.IsNullOrWhiteSpace(clip.RuntimeResourceKey))
        {
            diagnostics.Add(CreateAnimationPreviewDiagnostic(
                "Error",
                "ANIM_UNITY_PREVIEW_SOURCE_MISSING",
                "Animation clip has no resolvable source selection or runtime resource key.",
                "Select a Unity AnimationClip or runtime-ready animation resource in Animation Editor."));
        }

        if (!string.IsNullOrWhiteSpace(unityAssetPath) && !unityAssetExists)
        {
            diagnostics.Add(CreateAnimationPreviewDiagnostic(
                "Error",
                "ANIM_UNITY_PREVIEW_ASSET_MISSING",
                "Unity preview source asset does not exist: " + unityAssetPath,
                "Refresh Unity resource sync or reimport the animation source."));
        }

        if (string.IsNullOrWhiteSpace(clip.SourceSubClipId) && string.Equals(packageEntry != null ? packageEntry.Usage : string.Empty, CharacterPackageResourceUsageIds.AnimationClipGroup, StringComparison.Ordinal))
        {
            diagnostics.Add(CreateAnimationPreviewDiagnostic(
                "Warning",
                "ANIM_UNITY_PREVIEW_SUBCLIP_UNRESOLVED",
                "Animation source is a clip group but no source sub-clip id is configured.",
                "Choose a concrete sub-clip from the Animation.SourceClip picker."));
        }

        if (!canPreviewInUnity && canPreviewInWeb)
        {
            diagnostics.Add(CreateAnimationPreviewDiagnostic(
                "Info",
                "ANIM_WEB_PREVIEW_ARTIFACT_ONLY",
                "Only web preview artifact is currently available; this is not authoritative Unity playback.",
                "Use a Unity AnimationClip source for authoritative preview."));
        }

        if (previewModelEntry == null)
        {
            diagnostics.Add(CreateAnimationPreviewDiagnostic(
                "Warning",
                "ANIM_UNITY_PREVIEW_TARGET_MISSING",
                "No preview model resource is linked for this animation clip.",
                "Link a skeleton or character model preview target before running Unity preview."));
        }

        return diagnostics;
    }

    private static object CreateAnimationPreviewDiagnostic(string severity, string code, string message, string suggestedFix)
    {
        return new
        {
            severity,
            code,
            message,
            suggestedFix
        };
    }

    private static CharacterPackageResourceEntry FindPackageResourceEntryByKey(CharacterPackageResourceCatalog catalog, string resourceKey)
    {
        if (catalog == null || catalog.Entries == null || string.IsNullOrWhiteSpace(resourceKey))
            return null;

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            CharacterPackageResourceEntry entry = catalog.Entries[i];
            if (entry != null && string.Equals(entry.ResourceKey, resourceKey, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }

    private static AuthoringResourceItem FindAnimationSourceResource(AuthoringResourceCollection collection, AuthoringResourceSelectionRef selection, string runtimeResourceKey)
    {
        if (collection == null || collection.Items == null)
            return null;

        for (int i = 0; i < collection.Items.Count; i++)
        {
            AuthoringResourceItem item = collection.Items[i];
            if (item == null)
                continue;

            if (selection != null &&
                (AuthoringResourceMatches(selection.ResourceStableId, item, string.Empty) ||
                 AuthoringResourceMatches(selection.RuntimeResourceKey, item, string.Empty) ||
                 AuthoringResourceMatches(selection.UnityGuid, item, string.Empty) ||
                 AuthoringResourceMatches(selection.UnityAssetPath, item, string.Empty) ||
                 AuthoringResourceMatches(selection.ProviderResourceKey, item, string.Empty) ||
                 AuthoringResourceMatches(selection.PackageResourceKey, item, string.Empty)))
                return item;

            if (AuthoringResourceMatches(runtimeResourceKey, item, string.Empty))
                return item;
        }

        return null;
    }

    private static bool IsUnityAssetPathPresent(string rootPath, string unityAssetPath)
    {
        if (string.IsNullOrWhiteSpace(unityAssetPath))
            return false;

        string normalized = unityAssetPath.Replace('\\', '/');
        if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            return false;

        return File.Exists(Path.Combine(rootPath, normalized));
    }

    private static bool IsAnimationClipPreviewCandidate(AuthoringResourceItem item, CharacterPackageResourceEntry packageEntry)
    {
        string usage = FirstNonEmpty(item != null ? item.Usage : string.Empty, packageEntry != null ? packageEntry.Usage : string.Empty);
        string kind = FirstNonEmpty(item != null ? item.Kind : string.Empty, packageEntry != null ? packageEntry.TypeId : string.Empty);
        return string.Equals(kind, CharacterPackageResourceTypeIds.Animation, StringComparison.Ordinal) &&
            (string.Equals(usage, AnimationAuthoringResourceUsages.AnimationClip, StringComparison.Ordinal) ||
             string.Equals(usage, CharacterPackageResourceUsageIds.AnimationClipGroup, StringComparison.Ordinal));
    }

    private static bool IsWebAnimationPreviewCandidate(CharacterPackageResourceEntry packageEntry, string packageRoot)
    {
        if (packageEntry == null || string.IsNullOrWhiteSpace(packageEntry.RelativePath))
            return false;

        string extension = Path.GetExtension(packageEntry.RelativePath);
        if (!string.Equals(extension, ".glb", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".gltf", StringComparison.OrdinalIgnoreCase))
            return false;

        string fullPath = CharacterPackageResourcePipeline.ResolvePackagePath(packageRoot, packageEntry.RelativePath);
        return !string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath);
    }

    private static AnimationSetDefinitionClipRef FindAnimationSetDefinitionClip(
        AnimationSetDefinitionDocument setDefinition,
        AnimationClipRegistryEntry clip)
    {
        for (int setIndex = 0; setDefinition != null && setDefinition.Sets != null && setIndex < setDefinition.Sets.Count; setIndex++)
        {
            AnimationSetDefinitionSet set = setDefinition.Sets[setIndex];
            if (set == null || !string.Equals(set.SetId, clip.SetId, StringComparison.Ordinal))
                continue;

            for (int groupIndex = 0; set.Groups != null && groupIndex < set.Groups.Count; groupIndex++)
            {
                AnimationSetDefinitionGroup group = set.Groups[groupIndex];
                if (group == null || !string.Equals(group.GroupId, clip.GroupId, StringComparison.Ordinal))
                    continue;

                for (int clipIndex = 0; group.Clips != null && clipIndex < group.Clips.Count; clipIndex++)
                {
                    AnimationSetDefinitionClipRef item = group.Clips[clipIndex];
                    if (item != null && string.Equals(item.ClipId, clip.ClipId, StringComparison.Ordinal))
                        return item;
                }
            }
        }

        return null;
    }

    private static object CreateAnimationFieldSpecs()
    {
        return new
        {
            sourceClip = AnimationAuthoringResourceFieldSpecs.CreateSourceClip(),
            avatarMask = AnimationAuthoringResourceFieldSpecs.CreateAvatarMask(),
            bakeArtifact = AnimationAuthoringResourceFieldSpecs.CreateBakeArtifact(),
            compatibilityProfile = AnimationAuthoringResourceFieldSpecs.CreateCompatibilityProfile(),
            eventVfx = AnimationAuthoringResourceFieldSpecs.CreateEventVfx(),
            eventAudioCue = AnimationAuthoringResourceFieldSpecs.CreateEventAudioCue(),
            eventAudioDefinition = AnimationAuthoringResourceFieldSpecs.CreateEventAudioCue(AuthoringResourceSelectionOutputKind.AudioEventDefinitionId)
        };
    }

    private static void AddDuplicateIssue(
        CharacterAuthoringValidationReport report,
        HashSet<string> ids,
        string id,
        string code,
        string sourceObjectPath,
        string field)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;
        if (ids.Add(id))
            return;

        report.Issues.Add(CreateAnimationValidationIssue(
            code,
            sourceObjectPath,
            field,
            "Animation authoring id is duplicated: " + id,
            CharacterAuthoringValidationSeverity.Error));
    }

    private static CharacterAuthoringValidationIssue CreateAnimationValidationIssue(
        string code,
        string sourceObjectPath,
        string field,
        string message,
        CharacterAuthoringValidationSeverity severity)
    {
        return new CharacterAuthoringValidationIssue
        {
            Severity = severity,
            Gate = severity == CharacterAuthoringValidationSeverity.Error ? CharacterAuthoringValidationGate.ExportBlocked : CharacterAuthoringValidationGate.WarningOnly,
            Code = code,
            SourcePath = AnimationAuthoringDocumentRelativePath,
            SourceObjectPath = sourceObjectPath,
            Field = field,
            Message = message,
            SuggestedFix = "Fix the animation authoring draft, then run validation again."
        };
    }

    private static bool IsSelectionEmpty(AuthoringResourceSelectionRef selection)
    {
        return selection == null ||
            (string.IsNullOrWhiteSpace(selection.ResourceStableId) &&
             string.IsNullOrWhiteSpace(selection.ProviderResourceKey) &&
             string.IsNullOrWhiteSpace(selection.PackageResourceKey) &&
             string.IsNullOrWhiteSpace(selection.RuntimeResourceKey) &&
             string.IsNullOrWhiteSpace(selection.UnityGuid) &&
             string.IsNullOrWhiteSpace(selection.UnityAssetPath) &&
             string.IsNullOrWhiteSpace(selection.AudioCueId) &&
             string.IsNullOrWhiteSpace(selection.AudioEventDefinitionId));
    }

    private static string ResolveAnimationDocumentPath(string rootPath, string packageOrPath, string defaultPackage, JsonSerializerOptions jsonOptions)
    {
        string requested = packageOrPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(requested))
            requested = IsCharacterPackage(rootPath, defaultPackage) ? defaultPackage : DefaultCharacterPackageRelativePath;

        string resolved = ResolveSafePath(rootPath, requested);
        if (File.Exists(resolved) || string.Equals(Path.GetExtension(resolved), ".json", StringComparison.OrdinalIgnoreCase))
            return resolved;
        if (Directory.Exists(resolved) || requested.Contains('/') || requested.Contains('\\'))
            return Path.Combine(resolved, AnimationAuthoringDocumentRelativePath);

        string matched = FindAnimationPackageRelativeById(rootPath, requested, defaultPackage, jsonOptions);
        if (!string.IsNullOrWhiteSpace(matched))
            return Path.Combine(ResolveSafePath(rootPath, matched), AnimationAuthoringDocumentRelativePath);

        return Path.Combine(resolved, AnimationAuthoringDocumentRelativePath);
    }

    private static string FindAnimationPackageRelativeById(string rootPath, string id, string defaultPackage, JsonSerializerOptions jsonOptions)
    {
        foreach (object item in ListAnimationPackages(rootPath, defaultPackage, jsonOptions))
        {
            string relative = GetAnonymousProperty(item, "relative");
            string packageId = GetAnonymousProperty(item, "packageId");
            string characterPackageId = GetAnonymousProperty(item, "characterPackageId");
            string stableId = GetAnonymousProperty(item, "stableId");
            if (string.Equals(id, packageId, StringComparison.Ordinal) ||
                string.Equals(id, characterPackageId, StringComparison.Ordinal) ||
                string.Equals(id, stableId, StringComparison.Ordinal))
                return relative;
        }

        return string.Empty;
    }

    private static string ToAnimationPackageRelative(string rootPath, string documentPath)
    {
        string normalized = Path.GetFullPath(documentPath);
        string fileName = Path.GetFileName(normalized);
        if (string.Equals(fileName, Path.GetFileName(AnimationAuthoringDocumentRelativePath), StringComparison.OrdinalIgnoreCase))
        {
            string packagePath = Path.GetDirectoryName(Path.GetDirectoryName(normalized)!)!;
            return ToAuthoringRelativePath(rootPath, packagePath);
        }

        return ToAuthoringRelativePath(rootPath, normalized);
    }

    private static string ResolveAnimationPackageRootPath(string documentPath)
    {
        string normalized = Path.GetFullPath(documentPath);
        string fileName = Path.GetFileName(normalized);
        if (string.Equals(fileName, Path.GetFileName(AnimationAuthoringDocumentRelativePath), StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(Path.GetDirectoryName(normalized)!)!;

        return Path.GetDirectoryName(normalized)!;
    }

    private static CharacterPackageResourceCatalog ReadAnimationResourceCatalog(string packageRoot, JsonSerializerOptions jsonOptions)
    {
        string path = Path.Combine(packageRoot, "resource_catalog.json");
        if (!File.Exists(path))
            return new CharacterPackageResourceCatalog();

        return JsonSerializer.Deserialize<CharacterPackageResourceCatalog>(File.ReadAllText(path), jsonOptions) ?? new CharacterPackageResourceCatalog();
    }

    private static AnimationAuthoringPackage ReadAnimationPackage(string rootPath, string documentPath, JsonSerializerOptions jsonOptions)
    {
        if (File.Exists(documentPath))
            return JsonSerializer.Deserialize<AnimationAuthoringPackage>(File.ReadAllText(documentPath), jsonOptions) ?? new AnimationAuthoringPackage();

        string packagePath = Path.GetDirectoryName(Path.GetDirectoryName(documentPath)!)!;
        string manifestPath = Path.Combine(packagePath, "manifest.json");
        if (File.Exists(manifestPath))
        {
            CharacterPackageManifest manifest = JsonSerializer.Deserialize<CharacterPackageManifest>(File.ReadAllText(manifestPath), jsonOptions) ?? new CharacterPackageManifest();
            return CreateAnimationPackageFromCharacterManifest(manifest);
        }

        return new AnimationAuthoringPackage();
    }

    private static AnimationAuthoringPackage CreateAnimationPackageFromCharacterManifest(CharacterPackageManifest manifest)
    {
        string packageId = manifest != null ? manifest.PackageId ?? string.Empty : string.Empty;
        string stableId = manifest != null ? manifest.StableId ?? string.Empty : string.Empty;
        string displayName = manifest != null ? manifest.DisplayName ?? string.Empty : string.Empty;
        return new AnimationAuthoringPackage
        {
            PackageId = string.IsNullOrWhiteSpace(packageId) ? string.Empty : "animation." + packageId,
            StableId = string.IsNullOrWhiteSpace(stableId) ? string.Empty : stableId + ".animation",
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Animation Authoring" : displayName + " Animation"
        };
    }

    private static string GetAnonymousProperty(object value, string propertyName)
    {
        return value?.GetType().GetProperty(propertyName)?.GetValue(value)?.ToString() ?? string.Empty;
    }

    private static AnimationAuthoringSaveRequest ReadAnimationAuthoringSaveRequest(HttpListenerContext context, JsonSerializerOptions jsonOptions)
    {
        string body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding).ReadToEnd();
        if (string.IsNullOrWhiteSpace(body))
            return new AnimationAuthoringSaveRequest();

        AnimationAuthoringSaveRequest request = JsonSerializer.Deserialize<AnimationAuthoringSaveRequest>(body, jsonOptions);
        if (request != null && (request.animation != null || !string.IsNullOrWhiteSpace(request.package)))
            return request;

        AnimationAuthoringPackage package = JsonSerializer.Deserialize<AnimationAuthoringPackage>(body, jsonOptions);
        return new AnimationAuthoringSaveRequest { animation = package };
    }

    private sealed class AnimationAuthoringSaveRequest
    {
        public string package { get; set; } = string.Empty;
        public AnimationAuthoringPackage animation { get; set; }
    }
}
