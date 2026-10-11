using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MxFramework.Editor.CharacterAction;
using MxFramework.Resources;
using MxFramework.Vfx.Unity;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Editor
{
    internal static class VfxConsumerContractResolver
    {
        private const string CharacterActionsPath =
            "Assets/MxFrameworkGenerated/CharacterActions/Libraries/Common/character_actions.json";
        private const string ProjectileDefinitionsPath =
            "Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_definitions.json";
        private const string ProjectileBindingsPath =
            "Assets/MxFrameworkGenerated/Combat/Projectiles/projectile_presentation_bindings.json";
        private const string VfxDefinitionsPath =
            "Assets/MxFrameworkGenerated/Vfx/vfx_definitions.json";
        internal static readonly ResourceKey FireballBurstChargeKey =
            new ResourceKey("vfx.magic.fireball_burst.charge", ResourceTypeIds.GameObject, packageId: "mxframework.samples");
        internal static readonly ResourceKey FireballBurstFlightKey =
            new ResourceKey("vfx.magic.fireball_burst.flight", ResourceTypeIds.GameObject, packageId: "mxframework.samples");
        internal static readonly ResourceKey FireballBurstImpactKey =
            new ResourceKey("vfx.magic.fireball_burst.impact", ResourceTypeIds.GameObject, packageId: "mxframework.samples");

        internal static IReadOnlyList<VfxConsumerContract> ResolveFireballBurst()
        {
            return ResolveFireballBurst(
                ReadObject(CharacterActionsPath),
                ReadObject(ProjectileDefinitionsPath),
                ReadObject(ProjectileBindingsPath),
                ReadObject(VfxDefinitionsPath));
        }

        // Pure projection used by the normal publication path and isolated regression tests.
        internal static IReadOnlyList<VfxConsumerContract> ResolveFireballBurst(
            JObject actionsRoot, JObject projectileDefinitionsRoot,
            JObject bindingsRoot, JObject vfxDefinitionsRoot)
        {
            JObject action = RequireSingle(actionsRoot?["actions"] as JArray,
                value => ((value["gameplayTrack"] as JObject)?["events"] as JArray)?.OfType<JObject>().Any(
                    request => request.Value<string>("kind") == "SendGameplayRequest"
                        && request.Value<string>("requestLabel") == "character_test.fireball_burst.projectile") == true,
                "action consuming the fireball_burst projectile request");
            JObject visual = RequireSingle((action["presentationTrack"] as JObject)?["events"] as JArray,
                value => value.Value<string>("kind") == "SpawnVisualCue", "fireball_burst visual cue");
            ResourceKey chargeKey;
            if (!CharacterActionVfxSelectionCatalog.TryParseResourceKey(visual.Value<string>("resourceKey"), out chargeKey)
                || chargeKey.TypeId != ResourceTypeIds.GameObject)
                throw new InvalidDataException("fireball_burst visual cue has an invalid GameObject ResourceKey.");
            int start = visual.Value<int?>("frame") ?? -1;
            int end = visual.Value<int?>("endFrame") ?? -1;
            if (start < 0 || end < start)
                throw new InvalidDataException("fireball_burst visual cue has no finite active frame interval.");
            JObject attachment = visual["vfxAttachment"] as JObject;
            string chargeAnchor = attachment?.Value<string>("mode") ?? "CasterRoot";
            if (chargeAnchor == "FollowSocket")
            {
                string socket = attachment.Value<string>("socketId");
                if (string.IsNullOrWhiteSpace(socket))
                    throw new InvalidDataException("fireball_burst FollowSocket cue is missing its socket.");
                chargeAnchor += ":" + socket;
            }

            JObject binding = RequireSingle(bindingsRoot?["bindings"] as JArray,
                value => value.Value<int?>("projectileDefinitionId") == 91002, "projectile 91002 binding");
            JObject projectile = RequireSingle(projectileDefinitionsRoot?["definitions"] as JArray,
                value => value.Value<int?>("definitionId") == 91002
                    && value.Value<string>("stableId") == "character_test.fireball_burst.projectile",
                "fireball_burst projectile definition");
            int flightFrames = (projectile["motion"] as JObject)?.Value<int?>("lifetimeFrames") ?? 0;
            if (flightFrames <= 0)
                throw new InvalidDataException("fireball_burst projectile has no positive lifetime.");
            JObject flight = (binding["slots"] as JObject)?["flightBody"] as JObject;
            ResourceKey flightKey = RequireResource(flight?["resource"] as JObject, "91002.flightBody");
            JObject impact = (binding["slots"] as JObject)?["areaImpact"] as JObject;
            ResourceKey impactKey = RequireResource(impact?["resource"] as JObject, "91002.areaImpact");
            JObject definition = RequireSingle(vfxDefinitionsRoot?["definitions"] as JArray,
                value => ReadResource(value["prefabResource"] as JObject) == impactKey,
                "91002.areaImpact VFX definition");
            int definitionId = definition.Value<int?>("definitionId") ?? 0;
            int cacheId = impact.Value<int?>("vfxDefinitionId") ?? 0;
            if (definitionId <= 0 || (cacheId > 0 && cacheId != definitionId))
                throw new InvalidDataException("91002.areaImpact cached VFX ID disagrees with its authoritative ResourceKey.");
            int impactFrames = impact.Value<int?>("lifetimeFramesOverride") ?? 0;
            if (impactFrames <= 0) impactFrames = definition.Value<int?>("defaultLifetimeFrames") ?? 0;
            if (impactFrames <= 0)
                throw new InvalidDataException("91002.areaImpact has no positive lifetime.");
            if (chargeKey == flightKey || chargeKey == impactKey || flightKey == impactKey)
                throw new InvalidDataException("fireball_burst bundle reuses a resource across incompatible consumer roles.");

            return new List<VfxConsumerContract>(3)
            {
                Create("蓄力", chargeKey, "CharacterAction fireball_burst presentationTrack",
                    end - start + 1, "OneShot", chargeAnchor,
                    VfxCanonicalJson.Hash(new { SourcePath = CharacterActionsPath, ActionId = action.Value<int?>("actionId"),
                        GameplayEvents = (action["gameplayTrack"] as JObject)?["events"], Event = visual })),
                Create("飞行", flightKey, "Projectile definition 91002 flightBody",
                    flightFrames, "Continuous", "Projectile",
                    VfxCanonicalJson.Hash(new { DefinitionPath = ProjectileDefinitionsPath, Definition = projectile,
                        BindingPath = ProjectileBindingsPath, Slot = flight, ExitPolicy = binding["exitPolicy"] })),
                Create("命中", impactKey, "Projectile definition 91002 areaImpact + VFX definition " + definitionId,
                    impactFrames, "OneShot", "HitPoint",
                    VfxCanonicalJson.Hash(new { DefinitionPath = VfxDefinitionsPath, Definition = definition,
                        BindingPath = ProjectileBindingsPath, Slot = impact, ImpactPolicy = projectile["impact"] }))
            };
        }

        internal static bool TryResolveFireballBurst(out IReadOnlyList<VfxConsumerContract> contracts, out string message)
        {
            try
            {
                contracts = ResolveFireballBurst();
                message = string.Empty;
                return true;
            }
            catch (Exception exception) when (exception is InvalidDataException || exception is JsonException)
            {
                contracts = null;
                message = "Published VFX consumer inputs are invalid: " + exception.Message;
                return false;
            }
        }

        internal static VfxConsumerContract Find(ResourceKey key)
        {
            IReadOnlyList<VfxConsumerContract> contracts;
            string message;
            if (!TryResolveFireballBurst(out contracts, out message))
                return null;
            return contracts.FirstOrDefault(value => value.ResourceKey == key);
        }

        private static JObject RequireSingle(JArray values, Func<JObject, bool> match, string source)
        {
            JObject[] found = values?.OfType<JObject>().Where(match).ToArray();
            if (found == null || found.Length != 1)
                throw new InvalidDataException("Expected exactly one " + source + ".");
            return found[0];
        }

        private static ResourceKey ReadResource(JObject value)
        {
            return value == null ? default : new ResourceKey(value.Value<string>("id"),
                value.Value<string>("typeId"), value.Value<string>("variant"), value.Value<string>("packageId"));
        }

        private static ResourceKey RequireResource(JObject value, string source)
        {
            ResourceKey key = ReadResource(value);
            if (!key.IsValid || key.TypeId != ResourceTypeIds.GameObject)
                throw new InvalidDataException(source + " has an invalid GameObject ResourceKey.");
            return key;
        }

        internal static bool ValidateEvidence(VfxPublishedBindingManifest manifest,
            IReadOnlyList<VfxConsumerContract> contracts, out string message)
        {
            foreach (VfxConsumerContract contract in contracts)
            {
                VfxPublishedBindingRecord[] bindings = manifest.Bindings.Where(
                    value => value?.ResourceKey?.ToResourceKey() == contract.ResourceKey).ToArray();
                if (bindings.Length != 1 || !string.Equals(bindings[0].ContractHash,
                    contract.ContractHash, StringComparison.OrdinalIgnoreCase))
                {
                    message = "Published VFX consumer contract evidence is stale: " + contract.ResourceKey.Id;
                    return false;
                }
            }
            message = string.Empty;
            return true;
        }


        private static VfxConsumerContract Create(
            string label,
            ResourceKey key,
            string source,
            int activeFrames,
            string loopMode,
            string anchor,
            string sourceEvidenceHash)
        {
            var contract = new VfxConsumerContract
            {
                SlotLabel = label,
                ResourceKey = key,
                SourceKind = source,
                ActiveFrames = activeFrames,
                LoopMode = loopMode,
                Anchor = anchor
            };
            contract.ContractHash = VfxCanonicalJson.Hash(new
            {
                ResourceKey = key.ToString(),
                Source = source,
                ActiveFrames = activeFrames,
                LoopMode = loopMode,
                Anchor = anchor,
                SourceEvidenceHash = sourceEvidenceHash
            });
            return contract;
        }

        private static JObject ReadObject(string path)
        {
            if (!File.Exists(path))
                return null;
            try
            {
                return JObject.Parse(File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }

    }

    internal sealed class VfxConsumerContractResolverService : IVfxConsumerContractResolver
    {
        public IReadOnlyList<VfxConsumerContract> ResolveFireballBurst() =>
            VfxConsumerContractResolver.ResolveFireballBurst();

        public VfxConsumerContract Find(ResourceKey key) =>
            VfxConsumerContractResolver.Find(key);
    }

    internal sealed class VfxConsumerCompatibilityReport
    {
        internal bool Compatible;
        internal readonly List<string> Diagnostics = new List<string>();

        internal string Summary =>
            Diagnostics.Count == 0
                ? (Compatible ? "技能合同兼容。" : "技能合同不兼容。")
                : string.Join("\n", Diagnostics);
    }

    internal static class VfxConsumerCompatibilityValidator
    {
        internal static int ResolveOneShotActiveFrames(VfxCompositionBakeReceipt receipt)
        {
            return PhaseFrames(receipt, "Build")
                + Math.Max(1, PhaseFrames(receipt, "Loop"))
                + PhaseFrames(receipt, "Complete");
        }

        internal static VfxConsumerCompatibilityReport ValidateCandidateArtifact(
            VfxCompositionBakeReceipt receipt)
        {
            var report = new VfxConsumerCompatibilityReport();
            if (receipt == null)
            {
                report.Diagnostics.Add("尚未烘焙 Candidate receipt。");
                return report;
            }

            if (!string.Equals(
                    receipt.ReceiptHash,
                    VfxCanonicalJson.Hash(receipt, "receiptHash"),
                    StringComparison.OrdinalIgnoreCase))
            {
                report.Diagnostics.Add("Candidate receipt hash 已失效。");
                return report;
            }

            if (!File.Exists(receipt.OutputPrefabPath)
                || !string.Equals(
                    AssetDatabase.AssetPathToGUID(receipt.OutputPrefabPath),
                    receipt.OutputPrefabGuid,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    VfxCompositionDraftService.HashFile(receipt.OutputPrefabPath),
                    receipt.CandidateContentHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                report.Diagnostics.Add("Candidate Prefab、GUID 或内容 hash 已变化，请重新烘焙。");
                return report;
            }

            report.Compatible = true;
            return report;
        }

        internal static VfxConsumerCompatibilityReport Validate(
            VfxConsumerContract contract,
            VfxCompositionBakeReceipt receipt)
        {
            var report = new VfxConsumerCompatibilityReport();
            if (contract == null)
            {
                report.Diagnostics.Add("尚未选择真实技能槽位。");
                return report;
            }
            if (contract.ActiveFrames <= 0 || string.IsNullOrWhiteSpace(contract.Anchor))
            {
                report.Diagnostics.Add(
                    "无法从当前 CharacterAction / Projectile / VFX Definition 配置解析有效技能合同；请先修复生成配置。");
                return report;
            }

            VfxConsumerCompatibilityReport artifact = ValidateCandidateArtifact(receipt);
            if (!artifact.Compatible)
                return artifact;

            int buildFrames = PhaseFrames(receipt, "Build");
            int loopFrames = PhaseFrames(receipt, "Loop");
            if (string.Equals(contract.SlotLabel, "蓄力", StringComparison.Ordinal))
            {
                if (!string.Equals(receipt.LoopPlaybackMode, "OneShot", StringComparison.Ordinal))
                    report.Diagnostics.Add("Charge 必须使用 OneShot Loop。");
                if (buildFrames + loopFrames > contract.ActiveFrames)
                    report.Diagnostics.Add(
                        "Charge 的 Build + Loop 为 " + (buildFrames + loopFrames)
                        + " 帧，超过技能前摇 " + contract.ActiveFrames
                        + " 帧；禁止整体比例压缩，请使用语义重定时调整 Entry/Body。");
            }
            else if (string.Equals(contract.SlotLabel, "飞行", StringComparison.Ordinal))
            {
                if (!string.Equals(receipt.LoopPlaybackMode, "Continuous", StringComparison.Ordinal))
                    report.Diagnostics.Add("Flight 必须使用 Continuous Loop，持续跟随 Projectile。");
                if (buildFrames > contract.ActiveFrames)
                    report.Diagnostics.Add(
                        "Flight Build 为 " + buildFrames + " 帧，超过 Projectile 最长 "
                        + contract.ActiveFrames + " 帧。");
                if (loopFrames < 1)
                    report.Diagnostics.Add("Flight 缺少可持续播放的 Loop。");
            }
            else if (string.Equals(contract.SlotLabel, "命中", StringComparison.Ordinal))
            {
                if (!string.Equals(receipt.LoopPlaybackMode, "OneShot", StringComparison.Ordinal))
                    report.Diagnostics.Add("Impact 必须使用 OneShot Loop。");
                if (buildFrames + loopFrames > contract.ActiveFrames)
                    report.Diagnostics.Add(
                        "Impact 的 Build + Loop 为 " + (buildFrames + loopFrames)
                        + " 帧，主峰必须落在 Definition 的 " + contract.ActiveFrames + " 帧合同内。");
            }

            report.Compatible = report.Diagnostics.Count == 0;
            if (report.Compatible)
            {
                report.Diagnostics.Add(
                    contract.SlotLabel + " 合同通过："
                    + contract.ActiveFrames + " 帧 · "
                    + contract.LoopMode + " · " + contract.Anchor + "。");
            }

            return report;
        }

        private static int PhaseFrames(VfxCompositionBakeReceipt receipt, string phase)
        {
            VfxCompositionBakedPhaseView value = receipt.Phases?.FirstOrDefault(
                item => string.Equals(item.Phase, phase, StringComparison.Ordinal));
            return Math.Max(0, value?.DurationFrames ?? 0);
        }
    }

    internal static class VfxConsumerContractVariantCompiler
    {
        internal static bool TryCompile(
            VfxCompositionDraftView source,
            VfxConsumerContract contract,
            out VfxCompositionDraftView variant,
            out string message)
        {
            variant = null;
            if (source == null || contract == null || contract.ActiveFrames <= 0)
            {
                message = "草稿或真实技能合同无效。";
                return false;
            }

            variant = JsonConvert.DeserializeObject<VfxCompositionDraftView>(
                JsonConvert.SerializeObject(source));
            if (variant == null)
            {
                message = "无法复制技能发布变体。";
                return false;
            }

            string role = ContractRole(contract);
            variant.DraftId = source.DraftId + ".consumer." + role.ToLowerInvariant();
            variant.DisplayName = source.DisplayName + " · " + contract.SlotLabel + "试玩变体";
            variant.TargetEffectId = contract.ResourceKey.Id;
            variant.PhaseDurationOverrides ??= new VfxCompositionPhaseDurationOverridesView();

            if (contract.LoopMode == "Continuous")
            {
                variant.LoopPlaybackMode = "Continuous";
                int buildFrames = PhaseDesiredFrames(variant, "Build");
                if (buildFrames > contract.ActiveFrames
                    && !TryFitPhase(variant, "Build", contract.ActiveFrames, out message))
                    return false;
            }
            else
            {
                variant.LoopPlaybackMode = "OneShot";
                if (!TryFitEntryAndBodyPhases(variant, contract.ActiveFrames, out message))
                    return false;
            }

            message = "已从原草稿生成绑定 " + contract.ResourceKey.Id
                + " 的非破坏技能变体；Entry/Exit 和最小 Body 保持绝对帧。";
            return true;
        }

        internal static void BindReceipt(
            VfxCompositionBakeReceipt receipt,
            VfxConsumerContract contract)
        {
            receipt.ConsumerResourceKey = contract.ResourceKey.ToString();
            receipt.ConsumerContractHash = contract.ContractHash;
            receipt.ReceiptHash = VfxCanonicalJson.Hash(receipt, "receiptHash");
            string receiptPath = VfxCompositionCandidateBaker.ReceiptRoot + "/"
                + VfxCompositionDraftService.FileToken(receipt.DraftId) + ".json";
            File.WriteAllText(
                receiptPath,
                JsonConvert.SerializeObject(receipt, Formatting.Indented) + Environment.NewLine,
                new UTF8Encoding(false));
        }

        private static bool TryFitEntryAndBodyPhases(
            VfxCompositionDraftView draft,
            int contractFrames,
            out string message)
        {
            int buildMinimum = PhaseMinimumFrames(draft, "Build");
            int loopMinimum = PhaseMinimumFrames(draft, "Loop");
            int minimumTotal = buildMinimum + loopMinimum;
            if (minimumTotal > contractFrames)
            {
                message = "Build/Loop 的 Entry、Exit、最小 Body 与事件保护点至少需要 "
                    + minimumTotal + " 帧，超过技能合同 " + contractFrames
                    + " 帧；必须先调整明确指出的 Layer Profile，禁止整体比例压缩。";
                return false;
            }

            int buildDesired = Math.Max(buildMinimum, PhaseDesiredFrames(draft, "Build"));
            int loopDesired = Math.Max(loopMinimum, PhaseDesiredFrames(draft, "Loop"));
            int extra = contractFrames - minimumTotal;
            int desiredExtra = Math.Max(0, buildDesired - buildMinimum)
                + Math.Max(0, loopDesired - loopMinimum);
            int buildExtra = desiredExtra == 0
                ? 0
                : (int)Math.Round(
                    extra * (Math.Max(0, buildDesired - buildMinimum) / (double)desiredExtra),
                    MidpointRounding.AwayFromZero);
            int buildTarget = Math.Min(contractFrames - loopMinimum, buildMinimum + buildExtra);
            int loopTarget = contractFrames - buildTarget;
            if (!TryFitPhase(draft, "Build", buildTarget, out message)
                || !TryFitPhase(draft, "Loop", loopTarget, out message))
                return false;
            return true;
        }

        private static bool TryFitPhase(
            VfxCompositionDraftView draft,
            string phase,
            int targetFrames,
            out string message)
        {
            List<VfxCompositionLayerView> layers = draft.Layers
                .Where(value => value.Enabled && string.Equals(value.Phase, phase, StringComparison.Ordinal))
                .ToList();
            int minimum = PhaseMinimumFrames(draft, phase);
            if (minimum > targetFrames)
            {
                message = phase + " 至少需要 " + minimum + " 帧，超过可用 "
                    + targetFrames + " 帧。";
                return false;
            }

            for (int i = 0; i < layers.Count; i++)
            {
                VfxCompositionLayerView layer = layers[i];
                layer.Timing ??= new VfxLayerTimingView();
                VfxTimeSegmentsView segments = layer.Timing.SourceSegments;
                if (segments == null)
                {
                    message = phase + "/" + layer.LayerId
                        + " 缺少 Component Profile 时间分段，不能安全重定时。";
                    return false;
                }

                int available = targetFrames - Math.Max(0, layer.StartFrame);
                if (available < segments.MinimumArtisticFrames)
                {
                    message = phase + "/" + layer.LayerId + " 的 Entry/Exit/最小 Body 需要 "
                        + segments.MinimumArtisticFrames + " 帧，但当前只剩 " + available + " 帧。";
                    return false;
                }
                int current = layer.Timing.TargetArtisticFrames > 0
                    ? layer.Timing.TargetArtisticFrames
                    : segments.ArtisticFrames;
                layer.Timing.TargetArtisticFrames = Math.Min(current, available);
                if (string.Equals(layer.Timing.RetimeMode, nameof(VfxRetimeMode.Fixed), StringComparison.Ordinal)
                    && current != layer.Timing.TargetArtisticFrames)
                {
                    message = phase + "/" + layer.LayerId
                        + " 是 Fixed 长度，不能为技能合同自动缩短。";
                    return false;
                }
            }

            draft.PhaseDurationOverrides.Set(phase, targetFrames);
            message = string.Empty;
            return true;
        }

        private static int PhaseDesiredFrames(VfxCompositionDraftView draft, string phase)
        {
            int result = string.Equals(phase, "Loop", StringComparison.Ordinal) ? 1 : 0;
            for (int i = 0; i < draft.Layers.Count; i++)
            {
                VfxCompositionLayerView layer = draft.Layers[i];
                if (!layer.Enabled || !string.Equals(layer.Phase, phase, StringComparison.Ordinal))
                    continue;
                int duration = layer.Timing?.TargetArtisticFrames > 0
                    ? layer.Timing.TargetArtisticFrames
                    : layer.Timing?.SourceSegments?.ArtisticFrames ?? Math.Max(1, layer.EstimatedSourceFrames);
                result = Math.Max(result, Math.Max(0, layer.StartFrame) + duration);
            }
            return result;
        }

        private static int PhaseMinimumFrames(VfxCompositionDraftView draft, string phase)
        {
            int result = string.Equals(phase, "Loop", StringComparison.Ordinal) ? 1 : 0;
            for (int i = 0; i < draft.Layers.Count; i++)
            {
                VfxCompositionLayerView layer = draft.Layers[i];
                if (!layer.Enabled || !string.Equals(layer.Phase, phase, StringComparison.Ordinal))
                    continue;
                VfxTimeSegmentsView segments = layer.Timing?.SourceSegments;
                if (segments == null)
                    return int.MaxValue / 4;
                result = Math.Max(
                    result,
                    Math.Max(0, layer.StartFrame) + segments.MinimumArtisticFrames);
            }
            return result;
        }

        private static string ContractRole(VfxConsumerContract contract)
        {
            if (contract.SlotLabel == "蓄力")
                return "Charge";
            if (contract.SlotLabel == "飞行")
                return "Flight";
            return "Impact";
        }
    }
}
