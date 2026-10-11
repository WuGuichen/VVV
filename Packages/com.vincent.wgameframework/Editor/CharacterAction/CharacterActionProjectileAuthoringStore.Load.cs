using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace MxFramework.Editor.CharacterAction
{
    internal sealed partial class CharacterActionProjectileAuthoringStore
    {
        public static CharacterActionProjectileAuthoringStore Load(
            CharacterActionProjectileAuthoringStoreOptions options,
            string characterStableId = "")
        {
            options = options ?? CharacterActionProjectileAuthoringStoreOptions.Default;
            var diagnostics = new List<string>();
            JObject sourceRoot = TryReadJson(options.AbilityAuthoringPath, diagnostics, "投射物 authoring 源");
            JObject generatedDefinitionsRoot = TryReadJson(options.GeneratedDefinitionsPath, diagnostics, "投射物生成定义");
            JObject generatedSpawnProfilesRoot = TryReadJson(options.GeneratedSpawnProfilesPath, diagnostics, "投射物生成发射配置");
            JObject generatedBindingsRoot = TryReadJson(options.GeneratedPresentationBindingsPath, diagnostics, "投射物生成表现绑定");

            Dictionary<string, CharacterActionProjectileDefinitionData> sourceDefinitions =
                ReadDefinitions(sourceRoot?["projectileDefinitions"] as JArray, options.AbilityAuthoringPath, true, diagnostics);
            Dictionary<string, CharacterActionProjectileDefinitionData> generatedDefinitions =
                ReadDefinitions(generatedDefinitionsRoot?["definitions"] as JArray, options.GeneratedDefinitionsPath, false, diagnostics);
            Dictionary<string, CharacterActionProjectileSpawnProfileData> sourceSpawnProfiles =
                ReadSpawnProfiles(sourceRoot?["projectileSpawnProfiles"] as JArray, options.AbilityAuthoringPath, true, diagnostics);
            Dictionary<string, CharacterActionProjectileSpawnProfileData> generatedSpawnProfiles =
                ReadSpawnProfiles(generatedSpawnProfilesRoot?["profiles"] as JArray, options.GeneratedSpawnProfilesPath, false, diagnostics);
            Dictionary<int, CharacterActionProjectilePresentationBindingData> sourceBindings =
                ReadBindings(sourceRoot?["projectilePresentationBindings"] as JArray, options.AbilityAuthoringPath, true, diagnostics);
            Dictionary<int, CharacterActionProjectilePresentationBindingData> generatedBindings =
                ReadBindings(generatedBindingsRoot?["bindings"] as JArray, options.GeneratedPresentationBindingsPath, false, diagnostics);
            ReadProjectileRequestSources(
                options.ProjectileSourceDirectoryPath,
                sourceDefinitions,
                sourceSpawnProfiles,
                sourceBindings,
                diagnostics);

            var orderedStableIds = new List<string>();
            var projectileStableIdsWithSpawnProfiles = new HashSet<string>(StringComparer.Ordinal);
            AddProjectileStableIds(projectileStableIdsWithSpawnProfiles, sourceSpawnProfiles.Values);
            AddProjectileStableIds(projectileStableIdsWithSpawnProfiles, generatedSpawnProfiles.Values);
            AddStableIds(orderedStableIds, sourceSpawnProfiles.Keys);
            AddStableIds(orderedStableIds, generatedSpawnProfiles.Keys);
            AddDefinitionStableIds(orderedStableIds, sourceDefinitions.Keys, projectileStableIdsWithSpawnProfiles);
            AddDefinitionStableIds(orderedStableIds, generatedDefinitions.Keys, projectileStableIdsWithSpawnProfiles);
            orderedStableIds.Sort(StringComparer.Ordinal);

            var records = new List<CharacterActionProjectileAuthoringRecord>(orderedStableIds.Count);
            for (int i = 0; i < orderedStableIds.Count; i++)
            {
                string stableId = orderedStableIds[i];
                sourceSpawnProfiles.TryGetValue(stableId, out CharacterActionProjectileSpawnProfileData sourceSpawnProfile);
                generatedSpawnProfiles.TryGetValue(stableId, out CharacterActionProjectileSpawnProfileData generatedSpawnProfile);
                CharacterActionProjectileSpawnProfileData spawnProfile = sourceSpawnProfile ?? generatedSpawnProfile;
                string projectileStableId = string.IsNullOrWhiteSpace(spawnProfile?.ProjectileStableId)
                    ? stableId
                    : spawnProfile.ProjectileStableId;
                sourceDefinitions.TryGetValue(projectileStableId, out CharacterActionProjectileDefinitionData sourceDefinition);
                generatedDefinitions.TryGetValue(projectileStableId, out CharacterActionProjectileDefinitionData generatedDefinition);
                CharacterActionProjectileDefinitionData definition = sourceDefinition ?? generatedDefinition;
                if (definition == null)
                    continue;

                sourceBindings.TryGetValue(definition.DefinitionId, out CharacterActionProjectilePresentationBindingData sourceBinding);
                generatedBindings.TryGetValue(definition.DefinitionId, out CharacterActionProjectilePresentationBindingData generatedBinding);
                CharacterActionProjectilePresentationBindingData binding = sourceBinding ?? generatedBinding;
                bool definitionSynced = generatedDefinition != null
                    && CharacterActionProjectileDefinitionData.ValueEquals(definition, generatedDefinition);
                bool bindingSynced = generatedBinding != null
                    && CharacterActionProjectilePresentationBindingData.ValueEquals(binding, generatedBinding);
                bool spawnProfileSynced = spawnProfile == null
                    || (generatedSpawnProfile != null
                        && CharacterActionProjectileSpawnProfileData.ValueEquals(spawnProfile, generatedSpawnProfile));
                string sourcePath = string.IsNullOrWhiteSpace(sourceSpawnProfile?.SourcePath)
                    ? sourceDefinition?.SourcePath ?? string.Empty
                    : sourceSpawnProfile.SourcePath;

                records.Add(new CharacterActionProjectileAuthoringRecord(
                    spawnProfile,
                    definition,
                    binding,
                    sourceDefinition != null,
                    sourceSpawnProfile != null || spawnProfile == null,
                    generatedDefinition != null,
                    generatedSpawnProfile != null || spawnProfile == null,
                    generatedBinding != null,
                    definitionSynced && bindingSynced && spawnProfileSynced,
                    sourceDefinition?.SourcePath ?? string.Empty,
                    sourceSpawnProfile?.SourcePath ?? string.Empty,
                    sourceBinding?.SourcePath ?? string.Empty,
                    string.IsNullOrWhiteSpace(sourcePath) ? options.GeneratedDefinitionsPath : sourcePath,
                    options.GeneratedDefinitionsPath,
                    options.GeneratedSpawnProfilesPath,
                    options.GeneratedPresentationBindingsPath));
            }

            return new CharacterActionProjectileAuthoringStore(
                options,
                records.ToArray(),
                diagnostics.ToArray());
        }
        private static Dictionary<string, CharacterActionProjectileDefinitionData> ReadDefinitions(
            JArray definitions,
            string sourcePath,
            bool isAuthoringSource,
            List<string> diagnostics)
        {
            var result = new Dictionary<string, CharacterActionProjectileDefinitionData>(StringComparer.Ordinal);
            for (int i = 0; definitions != null && i < definitions.Count; i++)
            {
                if (!(definitions[i] is JObject fields))
                    continue;

                try
                {
                    CharacterActionProjectileDefinitionData definition =
                        CharacterActionProjectileDefinitionData.FromJson(fields, sourcePath, isAuthoringSource);
                    if (!string.IsNullOrWhiteSpace(definition.StableId))
                        result[definition.StableId] = definition;
                }
                catch (Exception ex)
                {
                    diagnostics?.Add("读取投射物定义失败：" + sourcePath + "#" + i + "，" + ex.Message);
                }
            }

            return result;
        }

        private static Dictionary<int, CharacterActionProjectilePresentationBindingData> ReadBindings(
            JArray bindings,
            string sourcePath,
            bool isAuthoringSource,
            List<string> diagnostics)
        {
            var result = new Dictionary<int, CharacterActionProjectilePresentationBindingData>();
            for (int i = 0; bindings != null && i < bindings.Count; i++)
            {
                if (!(bindings[i] is JObject fields))
                    continue;

                try
                {
                    CharacterActionProjectilePresentationBindingData binding =
                        CharacterActionProjectilePresentationBindingData.FromJson(fields, sourcePath, isAuthoringSource);
                    if (binding.ProjectileDefinitionId > 0)
                        result[binding.ProjectileDefinitionId] = binding;
                }
                catch (Exception ex)
                {
                    diagnostics?.Add("读取投射物表现绑定失败：" + sourcePath + "#" + i + "，" + ex.Message);
                }
            }

            return result;
        }

        private static Dictionary<string, CharacterActionProjectileSpawnProfileData> ReadSpawnProfiles(
            JArray profiles,
            string sourcePath,
            bool isAuthoringSource,
            List<string> diagnostics)
        {
            var result = new Dictionary<string, CharacterActionProjectileSpawnProfileData>(StringComparer.Ordinal);
            for (int i = 0; profiles != null && i < profiles.Count; i++)
            {
                if (!(profiles[i] is JObject fields))
                    continue;

                try
                {
                    CharacterActionProjectileSpawnProfileData profile =
                        CharacterActionProjectileSpawnProfileData.FromJson(fields, sourcePath, isAuthoringSource);
                    if (!string.IsNullOrWhiteSpace(profile.StableId))
                        result[profile.StableId] = profile;
                }
                catch (Exception ex)
                {
                    diagnostics?.Add("读取投射物发射配置失败：" + sourcePath + "#" + i + "，" + ex.Message);
                }
            }

            return result;
        }

        private static void ReadProjectileRequestSources(
            string directory,
            Dictionary<string, CharacterActionProjectileDefinitionData> definitions,
            Dictionary<string, CharacterActionProjectileSpawnProfileData> spawnProfiles,
            Dictionary<int, CharacterActionProjectilePresentationBindingData> bindings,
            List<string> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return;

            string[] files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
            Array.Sort(files, StringComparer.Ordinal);
            for (int i = 0; i < files.Length; i++)
            {
                JObject root = TryReadJson(files[i], diagnostics, "投射物请求 source");
                if (root == null)
                    continue;

                JObject definitionObject = root["definition"] as JObject;
                if (definitionObject != null)
                {
                    try
                    {
                        CharacterActionProjectileDefinitionData definition =
                            CharacterActionProjectileDefinitionData.FromJson(definitionObject, files[i], true);
                        if (!string.IsNullOrWhiteSpace(definition.StableId))
                            definitions[definition.StableId] = definition;
                    }
                    catch (Exception ex)
                    {
                        diagnostics?.Add("读取投射物请求 definition 失败：" + files[i] + "：" + ex.Message);
                    }
                }

                JObject spawnProfileObject = root["spawnProfile"] as JObject;
                if (spawnProfileObject != null)
                {
                    try
                    {
                        CharacterActionProjectileSpawnProfileData spawnProfile =
                            CharacterActionProjectileSpawnProfileData.FromJson(spawnProfileObject, files[i], true);
                        if (!string.IsNullOrWhiteSpace(spawnProfile.StableId))
                            spawnProfiles[spawnProfile.StableId] = spawnProfile;
                    }
                    catch (Exception ex)
                    {
                        diagnostics?.Add("读取投射物请求 spawnProfile 失败：" + files[i] + "：" + ex.Message);
                    }
                }

                JObject bindingObject = root["presentationBinding"] as JObject;
                if (bindingObject != null)
                {
                    try
                    {
                        CharacterActionProjectilePresentationBindingData binding =
                            CharacterActionProjectilePresentationBindingData.FromJson(bindingObject, files[i], true);
                        if (binding.ProjectileDefinitionId > 0)
                            bindings[binding.ProjectileDefinitionId] = binding;
                    }
                    catch (Exception ex)
                    {
                        diagnostics?.Add("读取投射物请求 presentationBinding 失败：" + files[i] + "：" + ex.Message);
                    }
                }
            }
        }
        private static JObject TryReadJson(string path, List<string> diagnostics, string label)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                if (diagnostics != null && !string.IsNullOrWhiteSpace(label))
                    diagnostics.Add(label + "不存在：" + (path ?? string.Empty));
                return null;
            }

            try
            {
                return JObject.Parse(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                diagnostics?.Add("读取 " + label + " 失败：" + path + "，" + ex.Message);
                return null;
            }
        }
        private static void AddStableIds(List<string> target, IEnumerable<string> source)
        {
            if (source == null)
                return;
            foreach (string stableId in source)
            {
                if (!string.IsNullOrWhiteSpace(stableId) && !target.Contains(stableId))
                    target.Add(stableId);
            }
        }

        private static void AddDefinitionStableIds(
            List<string> target,
            IEnumerable<string> source,
            HashSet<string> projectileStableIdsWithSpawnProfiles)
        {
            if (source == null)
                return;
            foreach (string stableId in source)
            {
                if (string.IsNullOrWhiteSpace(stableId)
                    || target.Contains(stableId)
                    || (projectileStableIdsWithSpawnProfiles != null
                        && projectileStableIdsWithSpawnProfiles.Contains(stableId)))
                {
                    continue;
                }

                target.Add(stableId);
            }
        }

        private static void AddProjectileStableIds(
            HashSet<string> target,
            IEnumerable<CharacterActionProjectileSpawnProfileData> source)
        {
            if (target == null || source == null)
                return;
            foreach (CharacterActionProjectileSpawnProfileData profile in source)
            {
                if (!string.IsNullOrWhiteSpace(profile?.ProjectileStableId))
                    target.Add(profile.ProjectileStableId);
            }
        }

        private static void AddDiagnostics(List<string> target, IReadOnlyList<string> source)
        {
            if (target == null || source == null)
                return;
            for (int i = 0; i < source.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(source[i]))
                    target.Add(source[i]);
            }
        }
    }
}
