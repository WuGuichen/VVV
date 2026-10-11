using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MxFramework.Resources;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    [Serializable]
    internal sealed class VfxPublishedBindingManifest
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.published-bindings.v1";
        [JsonProperty("catalogId")] public string CatalogId = "mxframework.vfx.published-bindings";
        [JsonProperty("bindings")] public List<VfxPublishedBindingRecord> Bindings = new List<VfxPublishedBindingRecord>();
        [JsonProperty("bindingSets")] public List<VfxPublishedBindingSet> BindingSets = new List<VfxPublishedBindingSet>();
        [JsonProperty("manifestHash")] public string ManifestHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxPublishedBindingRecord
    {
        [JsonProperty("bindingId")] public string BindingId = string.Empty;
        [JsonProperty("effectId")] public string EffectId = string.Empty;
        [JsonProperty("resourceKey")] public VfxPublishedResourceKey ResourceKey = new VfxPublishedResourceKey();
        [JsonProperty("address")] public string Address = string.Empty;
        [JsonProperty("assetPath")] public string AssetPath = string.Empty;
        [JsonProperty("assetGuid")] public string AssetGuid = string.Empty;
        [JsonProperty("assetContentHash")] public string AssetContentHash = string.Empty;
        [JsonProperty("contractHash")] public string ContractHash = string.Empty;
        [JsonProperty("visualMaturity")] public string VisualMaturity = "LegacyPlaceholder";
        [JsonProperty("reviewStatus")] public string ReviewStatus = "ReviewNeeded";
        [JsonProperty("activationReceiptHash")] public string ActivationReceiptHash = string.Empty;
    }

    [Serializable]
    internal sealed class VfxPublishedResourceKey
    {
        [JsonProperty("id")] public string Id = string.Empty;
        [JsonProperty("typeId")] public string TypeId = ResourceTypeIds.GameObject;
        [JsonProperty("variant")] public string Variant = string.Empty;
        [JsonProperty("packageId")] public string PackageId = string.Empty;

        internal ResourceKey ToResourceKey()
        {
            return new ResourceKey(Id, TypeId, Variant, PackageId);
        }
    }

    [Serializable]
    internal sealed class VfxPublishedBindingSet
    {
        [JsonProperty("bindingSetId")] public string BindingSetId = string.Empty;
        [JsonProperty("displayName")] public string DisplayName = string.Empty;
        [JsonProperty("atomic")] public bool Atomic = true;
        [JsonProperty("members")] public List<VfxPublishedBindingSetMember> Members = new List<VfxPublishedBindingSetMember>();
    }

    [Serializable]
    internal sealed class VfxPublishedBindingSetMember
    {
        [JsonProperty("role")] public string Role = string.Empty;
        [JsonProperty("bindingId")] public string BindingId = string.Empty;
    }

    [Serializable]
    internal sealed class VfxPlaytestOverrideDocument
    {
        [JsonProperty("schemaVersion")] public string SchemaVersion = "mx.vfx.playtest-overrides.v1";
        [JsonProperty("projectId")] public string ProjectId = string.Empty;
        [JsonProperty("entries")] public List<VfxPlaytestOverrideRecord> Entries = new List<VfxPlaytestOverrideRecord>();
    }

    [Serializable]
    internal sealed class VfxPlaytestOverrideRecord
    {
        [JsonProperty("resourceKey")] public VfxPublishedResourceKey ResourceKey = new VfxPublishedResourceKey();
        [JsonProperty("candidateAssetPath")] public string CandidateAssetPath = string.Empty;
        [JsonProperty("candidateAssetGuid")] public string CandidateAssetGuid = string.Empty;
        [JsonProperty("candidateContentHash")] public string CandidateContentHash = string.Empty;
        [JsonProperty("compositionHash")] public string CompositionHash = string.Empty;
        [JsonProperty("receiptPath")] public string ReceiptPath = string.Empty;
        [JsonProperty("receiptHash")] public string ReceiptHash = string.Empty;
        [JsonProperty("consumerContractHash")] public string ConsumerContractHash = string.Empty;
        [JsonProperty("humanPlaytested")] public bool HumanPlaytested;
    }

    internal interface IVfxConsumerContractResolver
    {
        IReadOnlyList<VfxConsumerContract> ResolveFireballBurst();
        VfxConsumerContract Find(ResourceKey key);
    }

    internal interface IVfxPublishedBindingStore
    {
        VfxPublishedBindingManifest Load(out string message);
        bool Validate(VfxPublishedBindingManifest manifest, out string message);
        bool GenerateProjection(VfxPublishedBindingManifest manifest, out string message);
    }

    internal interface IVfxPlaytestOverrideStore
    {
        bool Set(VfxConsumerContract contract, VfxCompositionBakeReceipt receipt, out string message);
        bool SetFireballBurstBundle(
            IReadOnlyList<VfxConsumerContract> contracts,
            IReadOnlyList<VfxCompositionBakeReceipt> receipts,
            out string message);
        bool MarkHumanPlaytested(ResourceKey resourceKey, out string message);
        void Clear();
    }

    internal interface IVfxBindingActivationPlanner
    {
        bool CreateFireballBurstPlan(out VfxBindingActivationPlan plan, out string message);
    }

    internal interface IVfxBindingActivationExecutor
    {
        bool Execute(
            VfxBindingActivationPlan plan,
            out VfxBindingActivationReceipt receipt,
            out string message);
        bool RollbackLatestFireballBurst(out string message);
    }

    internal sealed class VfxConsumerContract
    {
        internal string SlotLabel = string.Empty;
        internal ResourceKey ResourceKey;
        internal string SourceKind = string.Empty;
        internal int ActiveFrames;
        internal string LoopMode = string.Empty;
        internal string Anchor = string.Empty;
        internal string ContractHash = string.Empty;
    }
}

