using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor.CharacterImport
{
    internal enum CharacterAnimationClipRegistrationStatus
    {
        Registered,
        Removed,
        AlreadyRegistered,
        NotRegistered,
        Conflict,
        MissingRuntimeResource,
        Failed
    }

    internal sealed class CharacterAnimationClipRegistrationRecord
    {
        public string AssetPath { get; set; }
        public string ClipName { get; set; }
        public string RuntimeResourceKey { get; set; }
        public CharacterAnimationClipRegistrationStatus Status { get; set; }
        public string Message { get; set; }
    }

    internal sealed class CharacterAnimationClipRegistrationReport
    {
        private readonly List<CharacterAnimationClipRegistrationRecord> _records = new List<CharacterAnimationClipRegistrationRecord>();

        public IReadOnlyList<CharacterAnimationClipRegistrationRecord> Records => _records;
        public int RegisteredCount { get; private set; }
        public int RemovedCount { get; private set; }
        public int AlreadyRegisteredCount { get; private set; }
        public int NotRegisteredCount { get; private set; }
        public int ConflictCount { get; private set; }
        public int MissingRuntimeResourceCount { get; private set; }
        public int FailedCount { get; private set; }

        public bool HasBlockingIssues => ConflictCount > 0 || MissingRuntimeResourceCount > 0 || FailedCount > 0;

        public void Add(CharacterAnimationClipRegistrationRecord record)
        {
            if (record == null)
                return;

            _records.Add(record);
            switch (record.Status)
            {
                case CharacterAnimationClipRegistrationStatus.Registered:
                    RegisteredCount++;
                    break;
                case CharacterAnimationClipRegistrationStatus.Removed:
                    RemovedCount++;
                    break;
                case CharacterAnimationClipRegistrationStatus.AlreadyRegistered:
                    AlreadyRegisteredCount++;
                    break;
                case CharacterAnimationClipRegistrationStatus.NotRegistered:
                    NotRegisteredCount++;
                    break;
                case CharacterAnimationClipRegistrationStatus.Conflict:
                    ConflictCount++;
                    break;
                case CharacterAnimationClipRegistrationStatus.MissingRuntimeResource:
                    MissingRuntimeResourceCount++;
                    break;
                case CharacterAnimationClipRegistrationStatus.Failed:
                    FailedCount++;
                    break;
            }
        }
    }

    internal static class CharacterAnimationClipRegistrationCommand
    {
        private const string RegisterMenuPath = "MxFramework/Character Package/Register Selected Animation Clips";
        private const string UnregisterMenuPath = "MxFramework/Character Package/Unregister Selected Animation Clips";
        private const string DefaultCharacterPackagePath = "Authoring/CharacterPackages/character-000001";
        private const string DefaultGroupId = "group.combat";
        private const string AnimationAuthoringRelativePath = "config/animation_authoring.json";
        private const string RuntimeCatalogRoot = "Assets/Config/MxFramework/ResourceCatalogs";
        private const string ResourceMappingProfilePath = "Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json";

        [MenuItem(RegisterMenuPath, priority = 43)]
        public static void RegisterSelectedAnimationClips()
        {
            string[] assetPaths = CollectSelectedAnimationClipPaths();
            if (assetPaths.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Register Animation Clips",
                    "Select one or more .anim assets, or folders containing .anim assets.",
                    "OK");
                return;
            }

            string packagePath = ResolveCharacterPackagePath();
            if (string.IsNullOrWhiteSpace(packagePath) || !Directory.Exists(packagePath))
            {
                EditorUtility.DisplayDialog(
                    "Register Animation Clips",
                    "Character package was not found. Import a character package first, or keep the default sample package at " + DefaultCharacterPackagePath + ".",
                    "OK");
                return;
            }

            string animationAuthoringPath = Path.Combine(packagePath, AnimationAuthoringRelativePath);
            CharacterAnimationClipRegistrationReport report = RegisterAnimationClips(animationAuthoringPath, assetPaths, DefaultGroupId);
            string reportText = CreateReportText(report, packagePath, DefaultGroupId);
            Debug.Log(reportText);

            if (report.RegisteredCount > 0)
            {
                AssetDatabase.Refresh();
                int exitCode = CharacterPackageImportCommand.ImportPackage(packagePath);
                if (exitCode != 0)
                {
                    EditorUtility.DisplayDialog(
                        "Register Animation Clips",
                        "Registered " + report.RegisteredCount + " clip(s), but character package reimport failed with exit code " + exitCode + ". Check Console for details.",
                        "OK");
                    return;
                }
            }

            EditorUtility.DisplayDialog(
                "Register Animation Clips",
                "Registered: " + report.RegisteredCount +
                "\nAlready registered: " + report.AlreadyRegisteredCount +
                "\nMissing runtime resource: " + report.MissingRuntimeResourceCount +
                "\nConflicts: " + report.ConflictCount +
                "\nFailed: " + report.FailedCount,
                "OK");
        }

        [MenuItem(RegisterMenuPath, validate = true)]
        private static bool CanRegisterSelectedAnimationClips()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && HasSelectedAnimationClipOrFolder();
        }

        [MenuItem(UnregisterMenuPath, priority = 44)]
        public static void UnregisterSelectedAnimationClips()
        {
            string[] assetPaths = CollectSelectedAnimationClipPaths();
            if (assetPaths.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Unregister Animation Clips",
                    "Select one or more .anim assets, or folders containing .anim assets.",
                    "OK");
                return;
            }

            string packagePath = ResolveCharacterPackagePath();
            if (string.IsNullOrWhiteSpace(packagePath) || !Directory.Exists(packagePath))
            {
                EditorUtility.DisplayDialog(
                    "Unregister Animation Clips",
                    "Character package was not found. Import a character package first, or keep the default sample package at " + DefaultCharacterPackagePath + ".",
                    "OK");
                return;
            }

            string animationAuthoringPath = Path.Combine(packagePath, AnimationAuthoringRelativePath);
            CharacterAnimationClipRegistrationReport report = UnregisterAnimationClips(animationAuthoringPath, assetPaths);
            string reportText = CreateReportText(report, packagePath, "all groups");
            Debug.Log(reportText);

            if (report.RemovedCount > 0)
            {
                AssetDatabase.Refresh();
                int exitCode = CharacterPackageImportCommand.ImportPackage(packagePath);
                if (exitCode != 0)
                {
                    EditorUtility.DisplayDialog(
                        "Unregister Animation Clips",
                        "Removed " + report.RemovedCount + " clip(s), but character package reimport failed with exit code " + exitCode + ". Check Console for details.",
                        "OK");
                    return;
                }
            }

            EditorUtility.DisplayDialog(
                "Unregister Animation Clips",
                "Removed: " + report.RemovedCount +
                "\nNot registered: " + report.NotRegisteredCount +
                "\nConflicts: " + report.ConflictCount +
                "\nFailed: " + report.FailedCount,
                "OK");
        }

        [MenuItem(UnregisterMenuPath, validate = true)]
        private static bool CanUnregisterSelectedAnimationClips()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && HasSelectedAnimationClipOrFolder();
        }

        public static CharacterAnimationClipRegistrationReport RegisterAnimationClips(
            string animationAuthoringPath,
            IEnumerable<string> selectedAssetPaths,
            string targetGroupId)
        {
            var report = new CharacterAnimationClipRegistrationReport();
            if (string.IsNullOrWhiteSpace(animationAuthoringPath) || !File.Exists(animationAuthoringPath))
            {
                report.Add(new CharacterAnimationClipRegistrationRecord
                {
                    AssetPath = string.Empty,
                    Status = CharacterAnimationClipRegistrationStatus.Failed,
                    Message = "animation_authoring.json was not found: " + animationAuthoringPath
                });
                return report;
            }

            JObject document;
            try
            {
                document = JObject.Parse(File.ReadAllText(animationAuthoringPath));
            }
            catch (Exception ex)
            {
                report.Add(new CharacterAnimationClipRegistrationRecord
                {
                    AssetPath = animationAuthoringPath,
                    Status = CharacterAnimationClipRegistrationStatus.Failed,
                    Message = "Failed to parse animation_authoring.json: " + ex.Message
                });
                return report;
            }

            if (!TryFindClipArray(document, targetGroupId, out JArray clips))
            {
                report.Add(new CharacterAnimationClipRegistrationRecord
                {
                    AssetPath = animationAuthoringPath,
                    Status = CharacterAnimationClipRegistrationStatus.Failed,
                    Message = "Animation group was not found: " + targetGroupId
                });
                return report;
            }

            RuntimeAnimationResourceIndex resourceIndex = RuntimeAnimationResourceIndex.Load();
            var existingClipIds = new HashSet<string>(StringComparer.Ordinal);
            var existingRuntimeKeys = new HashSet<string>(StringComparer.Ordinal);
            CollectExistingClipKeys(document, existingClipIds, existingRuntimeKeys);

            bool changed = false;
            string[] assetPaths = NormalizeSelection(selectedAssetPaths);
            for (int i = 0; i < assetPaths.Length; i++)
            {
                string assetPath = assetPaths[i];
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (clip == null)
                {
                    report.Add(new CharacterAnimationClipRegistrationRecord
                    {
                        AssetPath = assetPath,
                        Status = CharacterAnimationClipRegistrationStatus.Failed,
                        Message = "Selected asset is not a standalone AnimationClip."
                    });
                    continue;
                }

                string clipName = string.IsNullOrWhiteSpace(clip.name)
                    ? Path.GetFileNameWithoutExtension(assetPath)
                    : clip.name;
                string clipId = NormalizeClipId(clipName);
                if (!resourceIndex.TryFind(assetPath, out RuntimeAnimationResourceInfo resource))
                {
                    report.Add(new CharacterAnimationClipRegistrationRecord
                    {
                        AssetPath = assetPath,
                        ClipName = clipName,
                        Status = CharacterAnimationClipRegistrationStatus.MissingRuntimeResource,
                        Message = "AnimationClip is not listed in a runtime Resource Catalog/Profile. Generate the sample Resource Catalog, then add it to the global Resource Build Profile first."
                    });
                    continue;
                }

                if (existingRuntimeKeys.Contains(resource.RuntimeResourceKey))
                {
                    report.Add(new CharacterAnimationClipRegistrationRecord
                    {
                        AssetPath = assetPath,
                        ClipName = clipName,
                        RuntimeResourceKey = resource.RuntimeResourceKey,
                        Status = CharacterAnimationClipRegistrationStatus.AlreadyRegistered,
                        Message = "Runtime resource key is already registered."
                    });
                    continue;
                }

                if (existingClipIds.Contains(clipId))
                {
                    report.Add(new CharacterAnimationClipRegistrationRecord
                    {
                        AssetPath = assetPath,
                        ClipName = clipName,
                        RuntimeResourceKey = resource.RuntimeResourceKey,
                        Status = CharacterAnimationClipRegistrationStatus.Conflict,
                        Message = "ClipId is already used by another animation entry: " + clipId
                    });
                    continue;
                }

                clips.Add(CreateClipEntry(assetPath, clipName, clipId, resource, clip.isLooping));
                existingClipIds.Add(clipId);
                existingRuntimeKeys.Add(resource.RuntimeResourceKey);
                changed = true;
                report.Add(new CharacterAnimationClipRegistrationRecord
                {
                    AssetPath = assetPath,
                    ClipName = clipName,
                    RuntimeResourceKey = resource.RuntimeResourceKey,
                    Status = CharacterAnimationClipRegistrationStatus.Registered,
                    Message = "Registered to " + targetGroupId + "."
                });
            }

            if (changed)
                File.WriteAllText(animationAuthoringPath, document.ToString(Formatting.Indented) + Environment.NewLine, new UTF8Encoding(false));

            return report;
        }

        public static CharacterAnimationClipRegistrationReport UnregisterAnimationClips(
            string animationAuthoringPath,
            IEnumerable<string> selectedAssetPaths)
        {
            var report = new CharacterAnimationClipRegistrationReport();
            if (string.IsNullOrWhiteSpace(animationAuthoringPath) || !File.Exists(animationAuthoringPath))
            {
                report.Add(new CharacterAnimationClipRegistrationRecord
                {
                    AssetPath = string.Empty,
                    Status = CharacterAnimationClipRegistrationStatus.Failed,
                    Message = "animation_authoring.json was not found: " + animationAuthoringPath
                });
                return report;
            }

            JObject document;
            try
            {
                document = JObject.Parse(File.ReadAllText(animationAuthoringPath));
            }
            catch (Exception ex)
            {
                report.Add(new CharacterAnimationClipRegistrationRecord
                {
                    AssetPath = animationAuthoringPath,
                    Status = CharacterAnimationClipRegistrationStatus.Failed,
                    Message = "Failed to parse animation_authoring.json: " + ex.Message
                });
                return report;
            }

            RuntimeAnimationResourceIndex resourceIndex = RuntimeAnimationResourceIndex.Load();
            bool changed = false;
            string[] assetPaths = NormalizeSelection(selectedAssetPaths);
            for (int i = 0; i < assetPaths.Length; i++)
            {
                string assetPath = assetPaths[i];
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (clip == null)
                {
                    report.Add(new CharacterAnimationClipRegistrationRecord
                    {
                        AssetPath = assetPath,
                        Status = CharacterAnimationClipRegistrationStatus.Failed,
                        Message = "Selected asset is not a standalone AnimationClip."
                    });
                    continue;
                }

                string clipName = string.IsNullOrWhiteSpace(clip.name)
                    ? Path.GetFileNameWithoutExtension(assetPath)
                    : clip.name;
                string clipId = NormalizeClipId(clipName);
                resourceIndex.TryFind(assetPath, out RuntimeAnimationResourceInfo resource);
                string runtimeKey = resource.RuntimeResourceKey;

                if (!TryFindRegisteredClip(document, assetPath, clipName, clipId, runtimeKey, out JArray ownerClips, out JObject registeredClip))
                {
                    report.Add(new CharacterAnimationClipRegistrationRecord
                    {
                        AssetPath = assetPath,
                        ClipName = clipName,
                        RuntimeResourceKey = runtimeKey,
                        Status = CharacterAnimationClipRegistrationStatus.NotRegistered,
                        Message = "AnimationClip is not registered in animation_authoring.json."
                    });
                    continue;
                }

                string registeredClipId = ReadString(registeredClip, "clipId");
                List<string> references = FindClipReferences(document, registeredClipId);
                if (references.Count > 0)
                {
                    report.Add(new CharacterAnimationClipRegistrationRecord
                    {
                        AssetPath = assetPath,
                        ClipName = clipName,
                        RuntimeResourceKey = FirstNonEmpty(runtimeKey, ReadString(registeredClip, "runtimeResourceKey")),
                        Status = CharacterAnimationClipRegistrationStatus.Conflict,
                        Message = "Clip is still referenced by " + FormatReferenceList(references) + ". Clear those references before unregistering."
                    });
                    continue;
                }

                ownerClips.Remove(registeredClip);
                changed = true;
                report.Add(new CharacterAnimationClipRegistrationRecord
                {
                    AssetPath = assetPath,
                    ClipName = clipName,
                    RuntimeResourceKey = FirstNonEmpty(runtimeKey, ReadString(registeredClip, "runtimeResourceKey")),
                    Status = CharacterAnimationClipRegistrationStatus.Removed,
                    Message = "Removed from animation_authoring.json."
                });
            }

            if (changed)
                File.WriteAllText(animationAuthoringPath, document.ToString(Formatting.Indented) + Environment.NewLine, new UTF8Encoding(false));

            return report;
        }

        public static string CreateReportText(
            CharacterAnimationClipRegistrationReport report,
            string packagePath,
            string targetGroupId)
        {
            var builder = new StringBuilder();
            builder.Append("MxFramework Character Animation Clip Registration Report\n");
            builder.Append("packagePath: ").Append(packagePath ?? string.Empty).Append('\n');
            builder.Append("targetGroupId: ").Append(targetGroupId ?? string.Empty).Append('\n');
            builder.Append("registered: ").Append(report != null ? report.RegisteredCount : 0).Append('\n');
            builder.Append("removed: ").Append(report != null ? report.RemovedCount : 0).Append('\n');
            builder.Append("alreadyRegistered: ").Append(report != null ? report.AlreadyRegisteredCount : 0).Append('\n');
            builder.Append("notRegistered: ").Append(report != null ? report.NotRegisteredCount : 0).Append('\n');
            builder.Append("missingRuntimeResource: ").Append(report != null ? report.MissingRuntimeResourceCount : 0).Append('\n');
            builder.Append("conflict: ").Append(report != null ? report.ConflictCount : 0).Append('\n');
            builder.Append("failed: ").Append(report != null ? report.FailedCount : 0).Append('\n');
            builder.Append("entries:\n");

            if (report == null || report.Records.Count == 0)
            {
                builder.Append("- none\n");
                return builder.ToString();
            }

            for (int i = 0; i < report.Records.Count; i++)
            {
                CharacterAnimationClipRegistrationRecord record = report.Records[i];
                builder.Append("- assetPath: ").Append(record.AssetPath).Append('\n');
                builder.Append("  clipName: ").Append(record.ClipName).Append('\n');
                builder.Append("  runtimeResourceKey: ").Append(record.RuntimeResourceKey).Append('\n');
                builder.Append("  status: ").Append(record.Status).Append('\n');
                builder.Append("  message: ").Append(record.Message).Append('\n');
            }

            return builder.ToString();
        }

        private static string ResolveCharacterPackagePath()
        {
            string packagePath = EditorPrefs.GetString(CharacterPackageImportCommand.LastPackagePreferenceKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(packagePath))
            {
                string resolvedPackagePath = ToFileSystemPath(packagePath);
                if (Directory.Exists(resolvedPackagePath))
                    return resolvedPackagePath;
            }

            string defaultPath = Path.GetFullPath(DefaultCharacterPackagePath);
            return Directory.Exists(defaultPath) ? defaultPath : string.Empty;
        }

        private static string[] CollectSelectedAnimationClipPaths()
        {
            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            string[] guids = Selection.assetGUIDs ?? Array.Empty<string>();
            for (int i = 0; i < guids.Length; i++)
                AddSelectedAssetPath(paths, seen, AssetDatabase.GUIDToAssetPath(guids[i]));

            UnityEngine.Object[] objects = Selection.objects ?? Array.Empty<UnityEngine.Object>();
            for (int i = 0; i < objects.Length; i++)
                AddSelectedAssetPath(paths, seen, AssetDatabase.GetAssetPath(objects[i]));

            paths.Sort(StringComparer.Ordinal);
            return paths.ToArray();
        }

        private static bool HasSelectedAnimationClipOrFolder()
        {
            string[] guids = Selection.assetGUIDs ?? Array.Empty<string>();
            for (int i = 0; i < guids.Length; i++)
            {
                if (IsAnimationClipOrFolder(AssetDatabase.GUIDToAssetPath(guids[i])))
                    return true;
            }

            UnityEngine.Object[] objects = Selection.objects ?? Array.Empty<UnityEngine.Object>();
            for (int i = 0; i < objects.Length; i++)
            {
                if (IsAnimationClipOrFolder(AssetDatabase.GetAssetPath(objects[i])))
                    return true;
            }

            return false;
        }

        private static void AddSelectedAssetPath(List<string> paths, HashSet<string> seen, string assetPath)
        {
            assetPath = NormalizeProjectPath(assetPath);
            if (string.IsNullOrWhiteSpace(assetPath))
                return;

            if (AssetDatabase.IsValidFolder(assetPath))
            {
                string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { assetPath });
                Array.Sort(guids, StringComparer.Ordinal);
                for (int i = 0; i < guids.Length; i++)
                    AddAnimationClipAssetPath(paths, seen, AssetDatabase.GUIDToAssetPath(guids[i]));
                return;
            }

            AddAnimationClipAssetPath(paths, seen, assetPath);
        }

        private static void AddAnimationClipAssetPath(List<string> paths, HashSet<string> seen, string assetPath)
        {
            assetPath = NormalizeProjectPath(assetPath);
            if (string.IsNullOrWhiteSpace(assetPath) ||
                !assetPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(ToFileSystemPath(assetPath)))
            {
                return;
            }

            if (seen.Add(assetPath))
                paths.Add(assetPath);
        }

        private static bool IsAnimationClipOrFolder(string assetPath)
        {
            assetPath = NormalizeProjectPath(assetPath);
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            return AssetDatabase.IsValidFolder(assetPath) ||
                assetPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase);
        }

        private static string[] NormalizeSelection(IEnumerable<string> selectedAssetPaths)
        {
            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (selectedAssetPaths == null)
                return Array.Empty<string>();

            foreach (string path in selectedAssetPaths)
                AddAnimationClipAssetPath(paths, seen, path);
            paths.Sort(StringComparer.Ordinal);
            return paths.ToArray();
        }

        private static bool TryFindClipArray(JObject document, string targetGroupId, out JArray clips)
        {
            clips = null;
            JArray sets = document?["sets"] as JArray;
            for (int setIndex = 0; sets != null && setIndex < sets.Count; setIndex++)
            {
                JArray groups = sets[setIndex]?["groups"] as JArray;
                for (int groupIndex = 0; groups != null && groupIndex < groups.Count; groupIndex++)
                {
                    JObject group = groups[groupIndex] as JObject;
                    if (!string.Equals(ReadString(group, "groupId"), targetGroupId, StringComparison.Ordinal))
                        continue;

                    clips = group["clips"] as JArray;
                    if (clips == null)
                    {
                        clips = new JArray();
                        group["clips"] = clips;
                    }
                    return true;
                }
            }

            return false;
        }

        private static void CollectExistingClipKeys(
            JObject document,
            HashSet<string> clipIds,
            HashSet<string> runtimeKeys)
        {
            JArray sets = document?["sets"] as JArray;
            for (int setIndex = 0; sets != null && setIndex < sets.Count; setIndex++)
            {
                JArray groups = sets[setIndex]?["groups"] as JArray;
                for (int groupIndex = 0; groups != null && groupIndex < groups.Count; groupIndex++)
                {
                    JArray clips = groups[groupIndex]?["clips"] as JArray;
                    for (int clipIndex = 0; clips != null && clipIndex < clips.Count; clipIndex++)
                    {
                        JObject clip = clips[clipIndex] as JObject;
                        string clipId = ReadString(clip, "clipId");
                        string runtimeKey = ReadString(clip, "runtimeResourceKey");
                        if (!string.IsNullOrWhiteSpace(clipId))
                            clipIds.Add(clipId);
                        if (!string.IsNullOrWhiteSpace(runtimeKey))
                            runtimeKeys.Add(runtimeKey);
                    }
                }
            }
        }

        private static bool TryFindRegisteredClip(
            JObject document,
            string assetPath,
            string clipName,
            string clipId,
            string runtimeKey,
            out JArray ownerClips,
            out JObject registeredClip)
        {
            ownerClips = null;
            registeredClip = null;
            JArray sets = document?["sets"] as JArray;
            for (int setIndex = 0; sets != null && setIndex < sets.Count; setIndex++)
            {
                JArray groups = sets[setIndex]?["groups"] as JArray;
                for (int groupIndex = 0; groups != null && groupIndex < groups.Count; groupIndex++)
                {
                    JArray clips = groups[groupIndex]?["clips"] as JArray;
                    for (int clipIndex = 0; clips != null && clipIndex < clips.Count; clipIndex++)
                    {
                        JObject clip = clips[clipIndex] as JObject;
                        if (!IsRegisteredClipMatch(clip, assetPath, clipName, clipId, runtimeKey))
                            continue;

                        ownerClips = clips;
                        registeredClip = clip;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsRegisteredClipMatch(
            JObject clip,
            string assetPath,
            string clipName,
            string clipId,
            string runtimeKey)
        {
            if (clip == null)
                return false;

            JObject sourceSelection = clip["sourceSelection"] as JObject;
            string registeredRuntimeKey = FirstNonEmpty(
                ReadString(clip, "runtimeResourceKey"),
                ReadString(sourceSelection, "runtimeResourceKey"),
                ReadString(sourceSelection, "providerResourceKey"));
            if (!string.IsNullOrWhiteSpace(runtimeKey) &&
                string.Equals(registeredRuntimeKey, runtimeKey, StringComparison.Ordinal))
            {
                return true;
            }

            string registeredAssetPath = NormalizeProjectPath(ReadString(sourceSelection, "unityAssetPath"));
            if (!string.IsNullOrWhiteSpace(registeredAssetPath) &&
                string.Equals(registeredAssetPath, NormalizeProjectPath(assetPath), StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(registeredAssetPath))
                return false;

            string registeredClipId = ReadString(clip, "clipId");
            string registeredSourceClipName = ReadString(clip, "sourceClipName");
            return string.Equals(registeredClipId, clipId, StringComparison.Ordinal) ||
                string.Equals(registeredSourceClipName, clipName, StringComparison.Ordinal);
        }

        private static List<string> FindClipReferences(JObject document, string clipId)
        {
            var references = new List<string>();
            if (string.IsNullOrWhiteSpace(clipId))
                return references;

            JArray sets = document?["sets"] as JArray;
            for (int setIndex = 0; sets != null && setIndex < sets.Count; setIndex++)
            {
                JObject set = sets[setIndex] as JObject;
                string setPath = "sets/" + setIndex;
                AddReferenceIfEquals(references, set, "defaultClipId", clipId, setPath + "/defaultClipId");
                AddReferenceIfEquals(references, set, "fallbackClipId", clipId, setPath + "/fallbackClipId");
                AddRequiredClipReferences(references, set?["warmup"] as JObject, clipId, setPath + "/warmup/requiredClipIds");

                JArray groups = set?["groups"] as JArray;
                for (int groupIndex = 0; groups != null && groupIndex < groups.Count; groupIndex++)
                {
                    JObject group = groups[groupIndex] as JObject;
                    string groupPath = setPath + "/groups/" + groupIndex;
                    AddBlendPointReferences(references, group?["blend1D"] as JArray, clipId, groupPath + "/blend1D");
                    AddBlendPointReferences(references, group?["blend2D"] as JArray, clipId, groupPath + "/blend2D");
                    AddTimelineReferences(references, group?["timelines"] as JArray, clipId, groupPath + "/timelines");
                }

                JArray actionBindings = set?["actionBindings"] as JArray;
                for (int bindingIndex = 0; actionBindings != null && bindingIndex < actionBindings.Count; bindingIndex++)
                    AddReferenceIfEquals(references, actionBindings[bindingIndex] as JObject, "clipId", clipId, setPath + "/actionBindings/" + bindingIndex + "/clipId");
            }

            JArray profiles = document?["profiles"] as JArray;
            for (int profileIndex = 0; profiles != null && profileIndex < profiles.Count; profileIndex++)
            {
                JObject profile = profiles[profileIndex] as JObject;
                string profilePath = "profiles/" + profileIndex;
                AddRequiredClipReferences(references, profile?["warmup"] as JObject, clipId, profilePath + "/warmup/requiredClipIds");

                JArray slots = profile?["slots"] as JArray;
                for (int slotIndex = 0; slots != null && slotIndex < slots.Count; slotIndex++)
                    AddReferenceIfEquals(references, slots[slotIndex] as JObject, "defaultClipId", clipId, profilePath + "/slots/" + slotIndex + "/defaultClipId");
            }

            return references;
        }

        private static void AddBlendPointReferences(List<string> references, JArray blends, string clipId, string path)
        {
            for (int blendIndex = 0; blends != null && blendIndex < blends.Count; blendIndex++)
            {
                JArray points = blends[blendIndex]?["points"] as JArray;
                for (int pointIndex = 0; points != null && pointIndex < points.Count; pointIndex++)
                    AddReferenceIfEquals(references, points[pointIndex] as JObject, "clipId", clipId, path + "/" + blendIndex + "/points/" + pointIndex + "/clipId");
            }
        }

        private static void AddTimelineReferences(List<string> references, JArray timelines, string clipId, string path)
        {
            for (int timelineIndex = 0; timelines != null && timelineIndex < timelines.Count; timelineIndex++)
            {
                JObject timeline = timelines[timelineIndex] as JObject;
                string timelinePath = path + "/" + timelineIndex;
                AddReferenceIfEquals(references, timeline, "clipId", clipId, timelinePath + "/clipId");

                JArray events = timeline?["events"] as JArray;
                for (int eventIndex = 0; events != null && eventIndex < events.Count; eventIndex++)
                    AddReferenceIfEquals(references, events[eventIndex] as JObject, "clipId", clipId, timelinePath + "/events/" + eventIndex + "/clipId");
            }
        }

        private static void AddRequiredClipReferences(List<string> references, JObject warmup, string clipId, string path)
        {
            JArray requiredClipIds = warmup?["requiredClipIds"] as JArray;
            for (int i = 0; requiredClipIds != null && i < requiredClipIds.Count; i++)
            {
                if (string.Equals(requiredClipIds[i]?.Value<string>(), clipId, StringComparison.Ordinal))
                    references.Add(path + "/" + i);
            }
        }

        private static void AddReferenceIfEquals(
            List<string> references,
            JObject obj,
            string field,
            string clipId,
            string path)
        {
            if (string.Equals(ReadString(obj, field), clipId, StringComparison.Ordinal))
                references.Add(path);
        }

        private static string FormatReferenceList(List<string> references)
        {
            if (references == null || references.Count == 0)
                return "no references";

            int visibleCount = Math.Min(references.Count, 6);
            string text = string.Join(", ", references.GetRange(0, visibleCount));
            if (references.Count > visibleCount)
                text += ", +" + (references.Count - visibleCount) + " more";
            return text;
        }

        private static JObject CreateClipEntry(
            string assetPath,
            string clipName,
            string clipId,
            RuntimeAnimationResourceInfo resource,
            bool loop)
        {
            return new JObject
            {
                ["clipId"] = clipId,
                ["displayName"] = ToDisplayName(clipName),
                ["sourceSelection"] = new JObject
                {
                    ["resourceStableId"] = "runtime." + resource.RuntimeResourceKey,
                    ["sourceProviderId"] = "runtimeCatalog",
                    ["bindingKind"] = "ResourceManagerAsset",
                    ["expectedKind"] = "animation",
                    ["expectedUsage"] = "animationClip",
                    ["expectedHash"] = resource.Hash ?? string.Empty,
                    ["unityGuid"] = string.Empty,
                    ["unityAssetPath"] = assetPath,
                    ["runtimeResourceKey"] = resource.RuntimeResourceKey,
                    ["providerResourceKey"] = resource.RuntimeResourceKey,
                    ["packageResourceKey"] = string.Empty,
                    ["audioCueId"] = string.Empty,
                    ["audioEventDefinitionId"] = string.Empty
                },
                ["sourceSubClipId"] = clipName,
                ["sourceClipName"] = clipName,
                ["runtimeResourceKey"] = resource.RuntimeResourceKey,
                ["loop"] = loop,
                ["speed"] = 1,
                ["rootMotionPolicy"] = "Ignore",
                ["calibration"] = new JObject
                {
                    ["nativeVelocityX"] = 0,
                    ["nativeVelocityY"] = 0,
                    ["playbackSpeed"] = 1,
                    ["cycleDurationSeconds"] = 0,
                    ["leftFootContactWindows"] = new JArray(),
                    ["rightFootContactWindows"] = new JArray()
                },
                ["tags"] = CreateTags(clipName),
                ["generatedArtifactSelections"] = new JArray(),
                ["diagnostics"] = new JArray(),
                ["metadata"] = new JObject
                {
                    ["sourceClipNameDerived"] = "true",
                    ["sourceRuntimeAvailability"] = "RuntimeReady",
                    ["sourceImportStatus"] = resource.InGlobalProfile ? "Clean" : "CatalogOnly"
                }
            };
        }

        private static JArray CreateTags(string clipName)
        {
            var tags = new JArray();
            string normalized = NormalizeClipId(clipName);
            if (normalized.Contains("atk") || normalized.Contains("attack"))
                tags.Add("attack");
            else if (normalized.Contains("block") || normalized.Contains("guard"))
                tags.Add("guard");
            else if (normalized.Contains("evade") || normalized.Contains("dodge"))
                tags.Add("evade");
            else if (normalized.Contains("skill"))
                tags.Add("skill");

            if (normalized.Contains("katana"))
                tags.Add("katana");
            return tags;
        }

        private static string NormalizeClipId(string clipName)
        {
            string value = (clipName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return "clip";

            var builder = new StringBuilder(value.Length);
            bool previousSeparator = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(char.ToLowerInvariant(c));
                    previousSeparator = false;
                }
                else if (!previousSeparator)
                {
                    builder.Append('_');
                    previousSeparator = true;
                }
            }

            return builder.ToString().Trim('_');
        }

        private static string ToDisplayName(string clipName)
        {
            string normalized = NormalizeClipId(clipName).Replace('_', ' ');
            if (string.IsNullOrWhiteSpace(normalized))
                return "Clip";

            string[] parts = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0)
                    continue;
                parts[i] = char.ToUpperInvariant(parts[i][0]) + (parts[i].Length > 1 ? parts[i].Substring(1) : string.Empty);
            }

            return string.Join(" ", parts);
        }

        private static string ReadString(JObject obj, string key)
        {
            return obj?[key]?.Value<string>() ?? string.Empty;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; values != null && i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            }

            return string.Empty;
        }

        private static string NormalizeProjectPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private static string ToFileSystemPath(string projectOrAbsolutePath)
        {
            if (string.IsNullOrWhiteSpace(projectOrAbsolutePath))
                return string.Empty;

            if (Path.IsPathRooted(projectOrAbsolutePath))
                return Path.GetFullPath(projectOrAbsolutePath);

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, projectOrAbsolutePath));
        }

        private readonly struct RuntimeAnimationResourceInfo
        {
            public RuntimeAnimationResourceInfo(string runtimeResourceKey, string assetPath, string hash, bool inGlobalProfile)
            {
                RuntimeResourceKey = runtimeResourceKey ?? string.Empty;
                AssetPath = assetPath ?? string.Empty;
                Hash = hash ?? string.Empty;
                InGlobalProfile = inGlobalProfile;
            }

            public string RuntimeResourceKey { get; }
            public string AssetPath { get; }
            public string Hash { get; }
            public bool InGlobalProfile { get; }
        }

        private sealed class RuntimeAnimationResourceIndex
        {
            private readonly Dictionary<string, RuntimeAnimationResourceInfo> _byAssetPath =
                new Dictionary<string, RuntimeAnimationResourceInfo>(StringComparer.Ordinal);

            public static RuntimeAnimationResourceIndex Load()
            {
                var index = new RuntimeAnimationResourceIndex();
                HashSet<string> globalProfileKeys = LoadGlobalProfileRuntimeKeys();
                index.LoadRuntimeCatalogs(globalProfileKeys);
                index.LoadGlobalProfileEntries();
                return index;
            }

            public bool TryFind(string assetPath, out RuntimeAnimationResourceInfo resource)
            {
                return _byAssetPath.TryGetValue(NormalizeProjectPath(assetPath), out resource);
            }

            private void LoadRuntimeCatalogs(HashSet<string> globalProfileKeys)
            {
                string catalogRoot = ToFileSystemPath(RuntimeCatalogRoot);
                if (!Directory.Exists(catalogRoot))
                    return;

                string[] files = Directory.GetFiles(catalogRoot, "*.json", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.Ordinal);
                for (int i = 0; i < files.Length; i++)
                    LoadRuntimeCatalog(files[i], globalProfileKeys);
            }

            private void LoadRuntimeCatalog(string path, HashSet<string> globalProfileKeys)
            {
                JObject document;
                try
                {
                    document = JObject.Parse(File.ReadAllText(path));
                }
                catch
                {
                    return;
                }

                JArray entries = document["entries"] as JArray;
                for (int i = 0; entries != null && i < entries.Count; i++)
                {
                    JObject entry = entries[i] as JObject;
                    string runtimeKey = ReadString(entry, "id");
                    string assetType = ReadString(entry, "type");
                    JObject providerData = entry?["providerData"] as JObject;
                    string assetPath = FirstNonEmpty(
                        ReadString(providerData, "unityAssetPath"),
                        ReadString(providerData, "assetPath"),
                        ReadString(entry, "unityAssetPath"));

                    if (string.IsNullOrWhiteSpace(runtimeKey) ||
                        string.IsNullOrWhiteSpace(assetPath) ||
                        !assetPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(assetType, "AnimationClip", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Put(new RuntimeAnimationResourceInfo(
                        runtimeKey,
                        assetPath,
                        ReadString(entry, "hash"),
                        globalProfileKeys.Contains(runtimeKey)));
                }
            }

            private void LoadGlobalProfileEntries()
            {
                string profilePath = ToFileSystemPath(ResourceMappingProfilePath);
                if (!File.Exists(profilePath))
                    return;

                JObject document;
                try
                {
                    document = JObject.Parse(File.ReadAllText(profilePath));
                }
                catch
                {
                    return;
                }

                JArray entries = document["entries"] as JArray;
                for (int i = 0; entries != null && i < entries.Count; i++)
                {
                    JObject entry = entries[i] as JObject;
                    JObject resourceKey = entry?["resourceKey"] as JObject;
                    JObject source = entry?["sourceLocator"] as JObject;
                    string runtimeKey = ReadString(resourceKey, "id");
                    string assetPath = FirstNonEmpty(
                        ReadString(source, "cachedPath"),
                        ReadString(entry?["providerData"] as JObject, "unityAssetPath"));

                    if (string.IsNullOrWhiteSpace(runtimeKey) ||
                        string.IsNullOrWhiteSpace(assetPath) ||
                        !assetPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Put(new RuntimeAnimationResourceInfo(runtimeKey, assetPath, string.Empty, true));
                }
            }

            private void Put(RuntimeAnimationResourceInfo resource)
            {
                string assetPath = NormalizeProjectPath(resource.AssetPath);
                if (string.IsNullOrWhiteSpace(assetPath))
                    return;

                if (_byAssetPath.TryGetValue(assetPath, out RuntimeAnimationResourceInfo existing) &&
                    existing.InGlobalProfile)
                {
                    return;
                }

                _byAssetPath[assetPath] = new RuntimeAnimationResourceInfo(
                    resource.RuntimeResourceKey,
                    assetPath,
                    resource.Hash,
                    resource.InGlobalProfile);
            }

            private static HashSet<string> LoadGlobalProfileRuntimeKeys()
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                string profilePath = ToFileSystemPath(ResourceMappingProfilePath);
                if (!File.Exists(profilePath))
                    return keys;

                JObject document;
                try
                {
                    document = JObject.Parse(File.ReadAllText(profilePath));
                }
                catch
                {
                    return keys;
                }

                JArray entries = document["entries"] as JArray;
                for (int i = 0; entries != null && i < entries.Count; i++)
                {
                    JObject entry = entries[i] as JObject;
                    string runtimeKey = ReadString(entry?["resourceKey"] as JObject, "id");
                    if (!string.IsNullOrWhiteSpace(runtimeKey))
                        keys.Add(runtimeKey);
                }

                return keys;
            }

            private static string FirstNonEmpty(params string[] values)
            {
                for (int i = 0; values != null && i < values.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(values[i]))
                        return values[i];
                }

                return string.Empty;
            }
        }
    }
}
