using System;
using System.Collections.Generic;
using System.IO;

namespace MxFramework.Authoring
{
    public static class AuthoringResourceCollectionMerger
    {
        public static AuthoringResourceCollection Merge(params AuthoringResourceCollection[] collections)
        {
            var merged = new AuthoringResourceCollection();
            if (collections == null)
                return merged;

            for (int i = 0; i < collections.Length; i++)
                MergeInto(merged, collections[i]);

            EnrichRuntimeAnimationUnityBindings(merged);

            return merged;
        }

        public static void MergeInto(AuthoringResourceCollection target, AuthoringResourceCollection source)
        {
            if (target == null || source == null)
                return;

            if (string.IsNullOrWhiteSpace(target.ScopeId))
                target.ScopeId = source.ScopeId ?? string.Empty;

            CopyMetadata(target.Metadata, source.Metadata);
            if (source.Providers != null)
                target.Providers.AddRange(source.Providers);
            if (source.Items != null)
                target.Items.AddRange(source.Items);
            if (source.Diagnostics != null)
                target.Diagnostics.AddRange(source.Diagnostics);
        }

        private static void CopyMetadata(Dictionary<string, string> target, Dictionary<string, string> source)
        {
            if (target == null || source == null)
                return;

            foreach (KeyValuePair<string, string> pair in source)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && !target.ContainsKey(pair.Key))
                    target[pair.Key] = pair.Value ?? string.Empty;
            }
        }

        private static void EnrichRuntimeAnimationUnityBindings(AuthoringResourceCollection collection)
        {
            if (collection == null || collection.Items == null || collection.Items.Count == 0)
                return;

            var unityCandidatesByKey = new Dictionary<string, List<AuthoringResourceItem>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < collection.Items.Count; i++)
            {
                AuthoringResourceItem item = collection.Items[i];
                if (!IsUnityAnimationCandidate(item, out string unityAssetPath))
                    continue;

                List<string> keys = GetUnityMatchKeys(item, unityAssetPath);
                for (int keyIndex = 0; keyIndex < keys.Count; keyIndex++)
                    AddCandidate(unityCandidatesByKey, keys[keyIndex], item);
            }

            for (int i = 0; i < collection.Items.Count; i++)
            {
                AuthoringResourceItem item = collection.Items[i];
                if (!TryGetRuntimeAnimationBindingToEnrich(item, out AuthoringResourceProviderBinding runtimeBinding))
                    continue;

                AuthoringResourceItem match = FindUniqueUnityMatch(item, unityCandidatesByKey);
                if (match == null || !TryGetUnityBinding(match, out AuthoringResourceProviderBinding unityBinding))
                    continue;

                runtimeBinding.UnityAssetPath = FirstNonEmpty(runtimeBinding.UnityAssetPath, unityBinding.UnityAssetPath);
                runtimeBinding.UnityGuid = FirstNonEmpty(runtimeBinding.UnityGuid, unityBinding.UnityGuid);
                runtimeBinding.ProviderData = runtimeBinding.ProviderData ?? new Dictionary<string, string>(StringComparer.Ordinal);
                CopyProviderDataIfPresent(runtimeBinding.ProviderData, AuthoringResourceBindingKeyKinds.UnityAssetPath, unityBinding.UnityAssetPath);
                CopyProviderDataIfPresent(runtimeBinding.ProviderData, AuthoringResourceBindingKeyKinds.UnityGuid, unityBinding.UnityGuid);
                CopyProviderDataIfPresent(runtimeBinding.ProviderData, "parentUnityAssetPath", GetMetadata(match, "parentUnityAssetPath"));
                CopyProviderDataIfPresent(runtimeBinding.ProviderData, "unityObjectName", GetMetadata(match, "unityObjectName"));
                CopyProviderDataIfPresent(runtimeBinding.ProviderData, "unitySubAssetKey", GetMetadata(match, "unitySubAssetKey"));

                item.Metadata = item.Metadata ?? new Dictionary<string, string>(StringComparer.Ordinal);
                CopyProviderDataIfPresent(item.Metadata, AuthoringResourceBindingKeyKinds.UnityAssetPath, unityBinding.UnityAssetPath);
                CopyProviderDataIfPresent(item.Metadata, AuthoringResourceBindingKeyKinds.UnityGuid, unityBinding.UnityGuid);
                CopyProviderDataIfPresent(item.Metadata, "parentUnityAssetPath", GetMetadata(match, "parentUnityAssetPath"));
                CopyProviderDataIfPresent(item.Metadata, "unityObjectName", GetMetadata(match, "unityObjectName"));
                CopyProviderDataIfPresent(item.Metadata, "unitySubAssetKey", GetMetadata(match, "unitySubAssetKey"));
            }
        }

        private static bool IsUnityAnimationCandidate(AuthoringResourceItem item, out string unityAssetPath)
        {
            unityAssetPath = string.Empty;
            if (!IsAnimationClipItem(item))
                return false;

            if (!TryGetUnityBinding(item, out AuthoringResourceProviderBinding binding))
                return false;

            unityAssetPath = binding.UnityAssetPath ?? string.Empty;
            return !string.IsNullOrWhiteSpace(unityAssetPath);
        }

        private static bool TryGetRuntimeAnimationBindingToEnrich(
            AuthoringResourceItem item,
            out AuthoringResourceProviderBinding binding)
        {
            binding = null;
            if (!IsAnimationClipItem(item) ||
                item.ProviderBindings == null ||
                item.SourceProviderId != AuthoringResourceProviderIds.RuntimeCatalog)
                return false;

            for (int i = 0; i < item.ProviderBindings.Count; i++)
            {
                AuthoringResourceProviderBinding candidate = item.ProviderBindings[i];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.RuntimeResourceKey))
                    continue;

                if (!string.IsNullOrWhiteSpace(candidate.UnityAssetPath))
                    return false;

                binding = candidate;
                return true;
            }

            return false;
        }

        private static AuthoringResourceItem FindUniqueUnityMatch(
            AuthoringResourceItem runtimeItem,
            Dictionary<string, List<AuthoringResourceItem>> unityCandidatesByKey)
        {
            List<string> keys = GetRuntimeMatchKeys(runtimeItem);
            var unique = new Dictionary<string, AuthoringResourceItem>(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                if (!unityCandidatesByKey.TryGetValue(keys[i], out List<AuthoringResourceItem> matches))
                    continue;

                for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
                {
                    AuthoringResourceItem match = matches[matchIndex];
                    if (match == null)
                        continue;

                    string id = FirstNonEmpty(match.ResourceId, match.StableId, match.DisplayName);
                    if (!string.IsNullOrWhiteSpace(id) && !unique.ContainsKey(id))
                        unique.Add(id, match);
                }
            }

            if (unique.Count != 1)
                return null;

            foreach (KeyValuePair<string, AuthoringResourceItem> pair in unique)
                return pair.Value;

            return null;
        }

        private static List<string> GetRuntimeMatchKeys(AuthoringResourceItem item)
        {
            var keys = new List<string>();
            AddMatchKey(keys, GetMetadata(item, "subClipId"));
            AddMatchKey(keys, GetMetadata(item, "subClipName"));
            AddMatchKey(keys, GetMetadata(item, "clipName"));
            AddMatchKey(keys, item != null ? item.DisplayName : string.Empty);
            AddMatchKey(keys, GetRuntimeKeyFileName(item));
            return keys;
        }

        private static List<string> GetUnityMatchKeys(AuthoringResourceItem item, string unityAssetPath)
        {
            var keys = new List<string>();
            AddMatchKey(keys, GetMetadata(item, "unitySubAssetKey"));
            AddMatchKey(keys, GetMetadata(item, "subClipId"));
            AddMatchKey(keys, GetMetadata(item, "subClipName"));
            AddMatchKey(keys, GetMetadata(item, "clipName"));
            AddMatchKey(keys, item != null ? item.DisplayName : string.Empty);
            AddMatchKey(keys, Path.GetFileNameWithoutExtension(unityAssetPath));
            return keys;
        }

        private static bool TryGetUnityBinding(AuthoringResourceItem item, out AuthoringResourceProviderBinding binding)
        {
            binding = null;
            if (item == null || item.ProviderBindings == null)
                return false;

            for (int i = 0; i < item.ProviderBindings.Count; i++)
            {
                AuthoringResourceProviderBinding candidate = item.ProviderBindings[i];
                if (candidate != null && !string.IsNullOrWhiteSpace(candidate.UnityAssetPath))
                {
                    binding = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool IsAnimationClipItem(AuthoringResourceItem item)
        {
            return item != null &&
                string.Equals(item.Kind, CharacterPackageResourceTypeIds.Animation, StringComparison.Ordinal) &&
                string.Equals(item.Usage, AnimationAuthoringResourceUsages.AnimationClip, StringComparison.Ordinal);
        }

        private static string GetRuntimeKeyFileName(AuthoringResourceItem item)
        {
            string runtimeKey = GetMetadata(item, AuthoringResourceBindingKeyKinds.RuntimeResourceKey);
            if (string.IsNullOrWhiteSpace(runtimeKey))
                return string.Empty;

            int separator = runtimeKey.LastIndexOf('.');
            return separator >= 0 && separator < runtimeKey.Length - 1
                ? runtimeKey.Substring(separator + 1)
                : runtimeKey;
        }

        private static string GetMetadata(AuthoringResourceItem item, string key)
        {
            if (item == null || item.Metadata == null || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            return item.Metadata.TryGetValue(key, out string value) ? value ?? string.Empty : string.Empty;
        }

        private static void AddCandidate(
            Dictionary<string, List<AuthoringResourceItem>> index,
            string key,
            AuthoringResourceItem item)
        {
            if (index == null || string.IsNullOrWhiteSpace(key) || item == null)
                return;

            if (!index.TryGetValue(key, out List<AuthoringResourceItem> items))
            {
                items = new List<AuthoringResourceItem>();
                index[key] = items;
            }

            if (!items.Contains(item))
                items.Add(item);
        }

        private static void AddMatchKey(List<string> keys, string value)
        {
            string normalized = NormalizeMatchKey(value);
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            if (!keys.Contains(normalized))
                keys.Add(normalized);
        }

        private static string NormalizeMatchKey(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace('\\', '/');
        }

        private static void CopyProviderDataIfPresent(Dictionary<string, string> target, string key, string value)
        {
            if (target == null || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                return;

            target[key] = value;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
                return string.Empty;

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            }

            return string.Empty;
        }
    }
}
