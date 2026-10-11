using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.ResourceMapping.Authoring;
using MxFramework.Resources.Unity;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    public static class GlobalResourceMappingBuilder
    {
        public const string ProfilePath = "Assets/Config/MxFramework/ResourceProfiles/global_resource_mapping_profile.v2.json";
        private const string ValidateMenuPath = "MxFramework/Resources/Validate Resource Mapping";

        [MenuItem(ValidateMenuPath, priority = 129)]
        public static void ValidateProfile()
        {
            GlobalResourceMappingBuildInput result = CreateBuildInput(ProfilePath);
            if (result.HasErrors)
                Debug.LogError(CreateReportText(result));
            else
                Debug.Log("Resource Mapping v2 production validation passed. Plan fingerprint: " + result.Compilation.Plan.BuildPlanFingerprint + ".");
        }

        /// <remarks>AllocByDesign. Reads and resolves the low-frequency production Unity build input.</remarks>
        public static GlobalResourceMappingBuildInput CreateBuildInput(string profilePath)
        {
            var result = new GlobalResourceMappingBuildInput
            {
                Mode = "production-v2",
                ProfilePath = profilePath ?? string.Empty,
                ProductionActive = true,
                WritesPlayerArtifacts = true
            };
            if (string.IsNullOrWhiteSpace(profilePath) || !File.Exists(profilePath))
            {
                AddError(result, "ProfileMissing", "Production v2 Profile was not found: " + profilePath + ".", "$" );
                return result;
            }

            ResourceMappingProfileReadResult read = ResourceMappingProfileJson.ReadStrict(File.ReadAllText(profilePath));
            if (!read.Success)
            {
                result.Diagnostics.AddRange(read.Diagnostics);
                return result;
            }

            result.Compilation = new GlobalResourceBuildPlanCompiler().Compile(read.Profile, EditorUserBuildSettings.activeBuildTarget.ToString());
            result.Diagnostics.AddRange(result.Compilation.Plan.Diagnostics);
            if (!result.Compilation.Plan.Success)
                return result;

            for (int i = 0; i < result.Compilation.Plan.Entries.Count; i++)
            {
                GlobalResourceBuildPlanEntry entry = result.Compilation.Plan.Entries[i];
                if (entry.DeliveryIntent == ResourceMappingDeliveryIntent.Internal)
                    ResolveUnitySource(entry, result);
            }

            result.ResolvedSources.Sort((left, right) => string.Compare(left.ResourceKeyIdentity, right.ResourceKeyIdentity, StringComparison.Ordinal));
            result.SourceInputFingerprint = ResourceMappingCanonicalJson.ComputeFingerprint(ResourceMappingCanonicalJson.Serialize(result.ResolvedSources));
            return result;
        }

        /// <remarks>LowFreqAlloc. Formats production compiler and Unity source diagnostics.</remarks>
        public static string CreateReportText(GlobalResourceMappingBuildInput result)
        {
            if (result == null)
                return "Resource Mapping v2 production result is missing.";
            var lines = new List<string>
            {
                "Resource Mapping v2 production",
                "Profile: " + result.ProfilePath,
                "Plan: " + (result.Compilation != null ? result.Compilation.Plan.BuildPlanFingerprint : "-")
            };
            for (int i = 0; i < result.Diagnostics.Count; i++)
            {
                ResourceMappingDiagnostic diagnostic = result.Diagnostics[i];
                lines.Add(diagnostic.Severity + " " + diagnostic.Code + " " + diagnostic.Path + ": " + diagnostic.Message);
            }
            return string.Join("\n", lines);
        }

        private static void ResolveUnitySource(GlobalResourceBuildPlanEntry entry, GlobalResourceMappingBuildInput result)
        {
            ResourceMappingSourceLocator source = entry.SourceLocator;
            string path;
            string guid;
            if (source.Kind == ResourceMappingSourceLocatorKind.UnityAsset)
            {
                guid = source.UnityGuid ?? string.Empty;
                path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path))
                {
                    AddError(result, "UnityGuidMissing", "Unity asset GUID could not be resolved.", EntryPath(entry) + ".sourceLocator.unityGuid");
                    return;
                }
                if (!string.IsNullOrWhiteSpace(source.CachedPath) && !string.Equals(source.CachedPath, path, StringComparison.Ordinal))
                    AddWarning(result, "UnityPathDrift", "Cached Unity path differs from GUID resolution: " + path + ".", EntryPath(entry) + ".sourceLocator.cachedPath");
            }
            else if (source.Kind == ResourceMappingSourceLocatorKind.GeneratedUnityAsset)
            {
                path = source.CachedPath ?? string.Empty;
                guid = AssetDatabase.AssetPathToGUID(path);
            }
            else
            {
                AddError(result, "UnitySourceNotMaterialized", "Internal Unity build input must be a UnityAsset or GeneratedUnityAsset source.", EntryPath(entry) + ".sourceLocator.kind");
                return;
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                AddError(result, "UnityAssetMissing", "Unity source asset was not found: " + path + ".", EntryPath(entry) + ".sourceLocator");
                return;
            }

            Type expectedType = UnityResourceTypeResolver.Resolve(entry.ResourceKey.TypeId);
            if (expectedType == typeof(AnimationClip)
                && !path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
            {
                AddError(
                    result,
                    "UnityAnimationClipNotExtracted",
                    "Animation runtime resources must reference a standalone .anim AnimationClip; FBX is source-only: " + path + ".",
                    EntryPath(entry) + ".sourceLocator");
                return;
            }
            Type actualType = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (actualType == null || (expectedType != typeof(UnityEngine.Object) && !expectedType.IsAssignableFrom(actualType)))
            {
                AddError(result, "UnityAssetTypeMismatch", "Unity source type does not match canonical ResourceKey.TypeId.", EntryPath(entry) + ".resourceKey.typeId");
                return;
            }

            result.ResolvedSources.Add(new GlobalResourceMappingV2ResolvedSource
            {
                ResourceKeyIdentity = entry.ResourceKey.ToIdentityString(),
                UnityGuid = guid,
                UnityAssetPath = path.Replace('\\', '/'),
                UnityMainObjectType = actualType.Name,
                UnityDependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString()
            });
        }

        private static string EntryPath(GlobalResourceBuildPlanEntry entry)
        {
            return "entries." + (entry != null ? entry.AuthoringRecordId : string.Empty);
        }

        private static void AddError(GlobalResourceMappingBuildInput result, string code, string message, string path)
        {
            result.Diagnostics.Add(new ResourceMappingDiagnostic { Severity = ResourceMappingDiagnosticSeverity.Error, Code = code, Message = message, Path = path });
        }

        private static void AddWarning(GlobalResourceMappingBuildInput result, string code, string message, string path)
        {
            result.Diagnostics.Add(new ResourceMappingDiagnostic { Severity = ResourceMappingDiagnosticSeverity.Warning, Code = code, Message = message, Path = path });
        }
    }

    public sealed class GlobalResourceMappingBuildInput
    {
        public string Mode { get; set; } = "production-v2";
        public string ProfilePath { get; set; } = string.Empty;
        public bool ProductionActive { get; set; }
        public bool WritesPlayerArtifacts { get; set; }
        public string SourceInputFingerprint { get; set; } = string.Empty;
        public GlobalResourceBuildPlanCompilation Compilation { get; set; }
        public List<GlobalResourceMappingV2ResolvedSource> ResolvedSources { get; set; } = new List<GlobalResourceMappingV2ResolvedSource>();
        public List<ResourceMappingDiagnostic> Diagnostics { get; set; } = new List<ResourceMappingDiagnostic>();

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Diagnostics.Count; i++)
                {
                    if (Diagnostics[i].Severity == ResourceMappingDiagnosticSeverity.Error)
                        return true;
                }
                return false;
            }
        }
    }

    public sealed class GlobalResourceMappingV2ResolvedSource
    {
        public string ResourceKeyIdentity { get; set; } = string.Empty;
        public string UnityGuid { get; set; } = string.Empty;
        public string UnityAssetPath { get; set; } = string.Empty;
        public string UnityMainObjectType { get; set; } = string.Empty;
        public string UnityDependencyHash { get; set; } = string.Empty;
    }
}
