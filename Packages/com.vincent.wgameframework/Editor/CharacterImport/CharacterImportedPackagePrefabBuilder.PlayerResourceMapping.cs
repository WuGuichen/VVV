using System;
using System.Collections.Generic;
using System.IO;
using MxFramework.ResourceMapping.Authoring;
using UnityEditor;

namespace MxFramework.Editor.CharacterImport
{
    public static partial class CharacterImportedPackagePrefabBuilder
    {
        /// <summary>Publishes generated spawn prefabs through the sole v2 mapping aggregator/compiler.</summary>
        /// <remarks>AllocByDesign. Editor composition declares the complete set of Player spawn packages.</remarks>
        public static void PublishPlayerPrefabMappings(IDictionary<string, string> prefabsByPackage)
        {
            const string producer = "character.generated-player-prefabs";
            var snapshot = new ResourceMappingProducerSnapshot { ProducerId = producer };
            foreach (KeyValuePair<string, string> pair in prefabsByPackage)
            {
                string expected = "Assets/MxFrameworkGenerated/CharacterPackages/" + pair.Key
                    + "/prefabs/" + pair.Key + "_character_preview.prefab";
                if (pair.Value != expected || !File.Exists(expected) || !File.Exists(expected + ".meta")
                    || AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(expected) == null)
                    throw new InvalidOperationException("Generated Player prefab is missing or outside its owned path: " + pair.Value);
                string id = "char." + pair.Key + ".prefab.character_preview";
                snapshot.Entries.Add(new ResourceMappingEntry {
                    AuthoringRecordId = "generated-player-prefab." + pair.Key,
                    ProducerReceipt = new ResourceMappingProducerReceipt { ProducerId = producer,
                        ProducerContractVersion = "1", SourceRecordFingerprint = ResourceMappingCanonicalJson.ComputeFingerprint(expected) },
                    ResourceKey = new ResourceMappingResourceKey { Id = id, PackageId = pair.Key, TypeId = "GameObject", Variant = "default" },
                    DeliveryIntent = ResourceMappingDeliveryIntent.Internal, OverrideIntent = ResourceMappingOverrideIntent.Forbid,
                    SourceLocator = new ResourceMappingSourceLocator { Kind = ResourceMappingSourceLocatorKind.GeneratedUnityAsset,
                        CachedPath = expected, GeneratedAssetId = id },
                    BundleIntent = new ResourceMappingBundleIntent { RuleId = "character.package.assets" },
                    Labels = new List<string> { "CharacterSpawn", "PlayableCharacter", "package." + pair.Key } });
            }
            var aggregator = new ResourceMappingProfileFileAggregator();
            string path = GlobalResourceMappingBuilder.ProfilePath;
            ResourceMappingProfileFileState state = aggregator.Read(path);
            if (!state.Success) throw new InvalidOperationException("Cannot read the resource mapping profile.");
            ResourceMappingProfileFileAggregationResult result = aggregator.ApplyCompleteSnapshots(path, state.Revision,
                new[] { snapshot }, EditorUserBuildSettings.activeBuildTarget.ToString());
            if (!result.Success) throw new InvalidOperationException("Player prefab producer failed: " + result.ErrorCode + " " + result.Message);
            if (result.Changed) AssetDatabase.ImportAsset(path);
        }
    }
}
