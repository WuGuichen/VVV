using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring
{
    public static class StorySourceAuthoringValidator
    {
        public const string SourceConfigKind = "story_source";
        public const string CanonConfigKind = "story_canon";
        public const string OutlineConfigKind = "story_outline";
        public const string PacketConfigKind = "story_packet";
        public const string PacketSchemaId = "wgame.story.input_packet.v1";

        public static readonly string[] RequiredStringFields =
        {
            "storySourceId",
            "version",
            "approvalStatus",
            "worldPremise",
            "coreEvent",
            "locationFunction",
            "civilizationOrigin",
            "playerRole"
        };

        public static readonly string[] RequiredArrayFields =
        {
            "targetConsumers",
            "canonRefs",
            "outlineRefs",
            "enemyOrigins",
            "powerRules",
            "symbolMeanings",
            "toneTags",
            "visualDoNotUse"
        };

        private static readonly HashSet<string> CanonTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "world",
            "location",
            "faction",
            "character_archetype",
            "enemy_origin",
            "power_rule",
            "symbol",
            "tone",
            "forbidden_content"
        };

        private static readonly HashSet<string> CanonStatuses = new HashSet<string>(StringComparer.Ordinal)
        {
            "draft",
            "canon",
            "deprecated",
            "rejected"
        };

        private static readonly HashSet<string> OutlineStages = new HashSet<string>(StringComparer.Ordinal)
        {
            "draft",
            "review",
            "approved-for-packet",
            "superseded"
        };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public static StorySourceValidationResult ValidateProject(string projectRootPath)
        {
            var result = new StorySourceValidationResult
            {
                ProjectRootPath = projectRootPath ?? string.Empty
            };

            result.Index = LoadJson<StorySourceValidationIndexDocument>(
                result,
                StorySourceAuthoringResourceProvider.IndexRelativePath,
                required: true) ?? new StorySourceValidationIndexDocument();
            result.Canon = LoadJson<StorySourceValidationCanonDocument>(
                result,
                StorySourceAuthoringResourceProvider.CanonRelativePath,
                required: false) ?? new StorySourceValidationCanonDocument();
            result.Outlines = LoadJson<StorySourceValidationOutlineDocument>(
                result,
                StorySourceAuthoringResourceProvider.OutlinesIndexRelativePath,
                required: false) ?? new StorySourceValidationOutlineDocument();

            result.Index.Packets ??= new List<StorySourceValidationPacketIndexEntry>();
            result.Canon.Facts ??= new List<StorySourceValidationCanonFact>();
            result.Outlines.Outlines ??= new List<StorySourceValidationOutlineEntry>();

            BuildPacketDocuments(result);
            ValidateCanonFacts(result);
            ValidateOutlines(result);
            ValidatePackets(result);
            return result;
        }

        public static List<AuthoringResourceDiagnostic> GetOutlineApprovalDiagnostics(string projectRootPath, string outlineId)
        {
            StorySourceValidationResult result = ValidateProject(projectRootPath);
            StorySourceValidationOutlineEntry outline = result.FindOutline(outlineId);
            if (outline == null)
            {
                return new List<AuthoringResourceDiagnostic>
                {
                    CreateDiagnostic(
                        CharacterAuthoringValidationSeverity.Error,
                        AuthoringResourceDiagnosticCodes.StorySourceOutlineRefMissing,
                        OutlineConfigKind,
                        outlineId ?? string.Empty,
                        "找不到故事大纲：" + (outlineId ?? string.Empty),
                        "刷新 StorySource 或检查 story_outlines.json。",
                        "outlineId")
                };
            }

            return ValidateOutlineApproval(result, outline);
        }

        public static List<AuthoringResourceDiagnostic> GetPacketApprovalDiagnostics(string projectRootPath, string storySourceId)
        {
            StorySourceValidationResult result = ValidateProject(projectRootPath);
            StorySourceValidationPacketEntry packet = result.FindPacket(storySourceId);
            if (packet == null)
            {
                return new List<AuthoringResourceDiagnostic>
                {
                    CreateDiagnostic(
                        CharacterAuthoringValidationSeverity.Error,
                        AuthoringResourceDiagnosticCodes.StorySourceOutlineRefMissing,
                        PacketConfigKind,
                        storySourceId ?? string.Empty,
                        "找不到故事输入包：" + (storySourceId ?? string.Empty),
                        "刷新 StorySource 或检查 story_packets.json。",
                        "storySourceId")
                };
            }

            var diagnostics = new List<AuthoringResourceDiagnostic>();
            diagnostics.AddRange(result.GetDiagnostics(PacketConfigKind, packet.StorySourceId)
                .Where(diagnostic => diagnostic != null && diagnostic.Severity == CharacterAuthoringValidationSeverity.Error));
            diagnostics.AddRange(ValidatePacketApproval(result, packet, requireApprovedStatus: false));
            return diagnostics;
        }

        public static StorySourceVerifiedPacket FindVerifiedApprovedArtPacket(string projectRootPath, string artSourceTaskId)
        {
            StorySourceValidationResult result = ValidateProject(projectRootPath);
            for (int i = 0; i < result.Packets.Count; i++)
            {
                StorySourceValidationPacketEntry packet = result.Packets[i];
                if (packet == null || packet.Document == null || packet.IndexEntry == null)
                    continue;

                if (!EqualsStatus(packet.Document.ApprovalStatus, "approved-for-art") ||
                    !EqualsStatus(packet.IndexEntry.ApprovalStatus, "approved-for-art"))
                    continue;

                if (!ContainsValue(packet.Document.TargetConsumers, "artSource") ||
                    !ContainsValue(packet.IndexEntry.TargetConsumers, "artSource"))
                    continue;

                if (!string.IsNullOrWhiteSpace(artSourceTaskId) &&
                    !string.Equals(packet.IndexEntry.ArtSourceTaskId ?? string.Empty, artSourceTaskId, StringComparison.Ordinal))
                    continue;

                if (HasBlockingDiagnostics(ValidatePacketApproval(result, packet, requireApprovedStatus: true)))
                    continue;

                return new StorySourceVerifiedPacket
                {
                    StorySourceId = packet.Document.StorySourceId ?? string.Empty,
                    Version = packet.Document.Version ?? string.Empty,
                    PacketPath = packet.RelativePacketPath ?? string.Empty,
                    ArtSourceTaskId = packet.IndexEntry.ArtSourceTaskId ?? string.Empty
                };
            }

            return null;
        }

        public static bool HasBlockingDiagnostics(IEnumerable<AuthoringResourceDiagnostic> diagnostics)
        {
            if (diagnostics == null)
                return false;

            foreach (AuthoringResourceDiagnostic diagnostic in diagnostics)
            {
                if (diagnostic == null)
                    continue;
                if (diagnostic.Severity == CharacterAuthoringValidationSeverity.Error ||
                    diagnostic.Severity == CharacterAuthoringValidationSeverity.Warning)
                    return true;
            }

            return false;
        }

        public static List<string> GetMissingRequiredFields(StorySourceValidationInputPacketDocument packet)
        {
            var result = new List<string>();
            if (packet == null)
            {
                result.AddRange(RequiredStringFields);
                result.AddRange(RequiredArrayFields);
                return result;
            }

            for (int i = 0; i < RequiredStringFields.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(GetStringField(packet, RequiredStringFields[i])))
                    result.Add(RequiredStringFields[i]);
            }

            for (int i = 0; i < RequiredArrayFields.Length; i++)
            {
                if (NormalizeStringList(GetArrayField(packet, RequiredArrayFields[i])).Count == 0)
                    result.Add(RequiredArrayFields[i]);
            }

            return result;
        }

        public static List<string> NormalizeStringList(List<string> values)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; values != null && i < values.Count; i++)
            {
                string value = (values[i] ?? string.Empty).Trim();
                if (value.Length == 0 || seen.Contains(value))
                    continue;
                seen.Add(value);
                result.Add(value);
            }
            return result;
        }

        private static T LoadJson<T>(StorySourceValidationResult result, string relativePath, bool required) where T : class
        {
            if (result == null || string.IsNullOrWhiteSpace(result.ProjectRootPath))
                return null;

            string path = ResolveProjectPath(result.ProjectRootPath, relativePath);
            if (!File.Exists(path))
            {
                if (required)
                {
                    result.Diagnostics.Add(CreateDiagnostic(
                        CharacterAuthoringValidationSeverity.Warning,
                        AuthoringResourceDiagnosticCodes.StorySourcePacketsMissing,
                        SourceConfigKind,
                        string.Empty,
                        "StorySource 文件不存在：" + relativePath,
                        "创建 StorySource 文件后刷新 Resource Manager。",
                        relativePath));
                }
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
            }
            catch (JsonException ex)
            {
                result.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Error,
                    AuthoringResourceDiagnosticCodes.StorySourceJsonInvalid,
                    SourceConfigKind,
                    string.Empty,
                    "StorySource JSON 无效：" + ex.Message,
                    "修复 JSON 后再保存或审批。",
                    relativePath));
                return null;
            }
        }

        private static void BuildPacketDocuments(StorySourceValidationResult result)
        {
            for (int i = 0; result.Index != null && result.Index.Packets != null && i < result.Index.Packets.Count; i++)
            {
                StorySourceValidationPacketIndexEntry indexEntry = result.Index.Packets[i];
                if (indexEntry == null)
                    continue;

                string storySourceId = FirstNonEmpty(indexEntry.StorySourceId, "story.packet." + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                string packetPath = FirstNonEmpty(indexEntry.PacketPath, BuildDefaultPacketPath(storySourceId));
                var entry = new StorySourceValidationPacketEntry
                {
                    StorySourceId = storySourceId,
                    IndexEntry = indexEntry,
                    RelativePacketPath = packetPath,
                    PacketExists = File.Exists(ResolveProjectPath(result.ProjectRootPath, packetPath))
                };

                if (entry.PacketExists)
                {
                    try
                    {
                        entry.Document = JsonSerializer.Deserialize<StorySourceValidationInputPacketDocument>(
                            File.ReadAllText(ResolveProjectPath(result.ProjectRootPath, packetPath)),
                            JsonOptions) ?? new StorySourceValidationInputPacketDocument();
                    }
                    catch (JsonException ex)
                    {
                        result.Diagnostics.Add(CreateDiagnostic(
                            CharacterAuthoringValidationSeverity.Error,
                            AuthoringResourceDiagnosticCodes.StorySourceJsonInvalid,
                            PacketConfigKind,
                            storySourceId,
                            "故事输入包 JSON 无效：" + ex.Message,
                            "修复 packet JSON 后再保存或审批。",
                            packetPath));
                        entry.Document = CreatePacketFallback(indexEntry);
                    }
                }
                else
                {
                    result.Diagnostics.Add(CreateDiagnostic(
                        CharacterAuthoringValidationSeverity.Error,
                        AuthoringResourceDiagnosticCodes.StorySourceJsonInvalid,
                        PacketConfigKind,
                        storySourceId,
                        "故事输入包文件不存在：" + packetPath,
                        "创建 packet 文件或修正 story_packets.json 的 packetPath。",
                        "packetPath"));
                    entry.Document = CreatePacketFallback(indexEntry);
                }

                ApplyIndexFallback(indexEntry, entry.Document);
                result.Packets.Add(entry);
            }
        }

        private static void ValidateCanonFacts(StorySourceValidationResult result)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; result.Canon != null && result.Canon.Facts != null && i < result.Canon.Facts.Count; i++)
            {
                StorySourceValidationCanonFact fact = result.Canon.Facts[i];
                if (fact == null)
                    continue;

                string canonId = FirstNonEmpty(fact.CanonId, "story.canon." + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                fact.CanonId = canonId;
                if (!result.CanonById.ContainsKey(canonId))
                    result.CanonById.Add(canonId, fact);

                if (!seen.Add(canonId))
                    result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceVersionConflict, CanonConfigKind, canonId, "重复的 canonId：" + canonId, "每个 canonId 只保留一条活动事实。", "canonId"));
                if (!CanonTypes.Contains(fact.Type ?? string.Empty))
                    result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceJsonInvalid, CanonConfigKind, canonId, "不支持的世界观记忆类型：" + (fact.Type ?? string.Empty), "使用固定 Canon fact type。", "type"));
                if (!CanonStatuses.Contains(fact.Status ?? string.Empty))
                    result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceJsonInvalid, CanonConfigKind, canonId, "不支持的世界观记忆状态：" + (fact.Status ?? string.Empty), "使用 draft、canon、deprecated 或 rejected。", "status"));
            }
        }

        private static void ValidateOutlines(StorySourceValidationResult result)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; result.Outlines != null && result.Outlines.Outlines != null && i < result.Outlines.Outlines.Count; i++)
            {
                StorySourceValidationOutlineEntry outline = result.Outlines.Outlines[i];
                if (outline == null)
                    continue;

                string outlineId = FirstNonEmpty(outline.OutlineId, "story.outline." + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                outline.OutlineId = outlineId;
                if (!result.OutlineById.ContainsKey(outlineId))
                    result.OutlineById.Add(outlineId, outline);

                if (!seen.Add(outlineId))
                    result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceVersionConflict, OutlineConfigKind, outlineId, "重复的 outlineId：" + outlineId, "每个 outlineId 只保留一个活动大纲。", "outlineId"));
                if (!OutlineStages.Contains(outline.Stage ?? string.Empty))
                    result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceJsonInvalid, OutlineConfigKind, outlineId, "不支持的故事大纲阶段：" + (outline.Stage ?? string.Empty), "使用 draft、review、approved-for-packet 或 superseded。", "stage"));

                AddOutlineContentDiagnostics(result, outline);
            }
        }

        private static void ValidatePackets(StorySourceValidationResult result)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < result.Packets.Count; i++)
            {
                StorySourceValidationPacketEntry packetEntry = result.Packets[i];
                if (packetEntry == null || packetEntry.Document == null || packetEntry.IndexEntry == null)
                    continue;

                StorySourceValidationInputPacketDocument packet = packetEntry.Document;
                string storySourceId = FirstNonEmpty(packetEntry.IndexEntry.StorySourceId, packet.StorySourceId, packetEntry.StorySourceId);
                packetEntry.StorySourceId = storySourceId;

                if (!string.IsNullOrWhiteSpace(packet.StorySourceId) && !seen.Add(packet.StorySourceId))
                    result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceVersionConflict, PacketConfigKind, storySourceId, "重复的 storySourceId：" + packet.StorySourceId, "每个 storySourceId/version 只保留一个活动 packet。", "storySourceId"));

                AddPacketConsistencyDiagnostics(result, packetEntry);
                AddPacketRequiredFieldDiagnostics(result, packetEntry);
                AddPacketDerivationDiagnostics(result, packetEntry, forApproval: false);

                if (!EqualsStatus(packet.ApprovalStatus, "approved-for-art"))
                    result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Info, AuthoringResourceDiagnosticCodes.StorySourceNotApproved, PacketConfigKind, storySourceId, "Story Input Packet 尚未批准给 ArtSource。", "提交审批后 ArtSource 才能生产正式参考图。", "approvalStatus"));
                if (EqualsStatus(packet.ApprovalStatus, "superseded"))
                    result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceSuperseded, PacketConfigKind, storySourceId, "Story Input Packet 已被替代。", "新生产工作应使用更新版本 packet。", "approvalStatus"));
                if (TargetsArtSource(packet) && !HasArtSourceTask(result.ProjectRootPath, packetEntry.IndexEntry.ArtSourceTaskId))
                    result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceArtReferenceMissing, PacketConfigKind, storySourceId, "Packet 指向 ArtSource，但找不到 ArtSource task：" + FirstNonEmpty(packetEntry.IndexEntry.ArtSourceTaskId, "(empty)"), "补充或修正 story_packets.json 的 artSourceTaskId。", "artSourceTaskId"));
            }
        }

        private static void AddPacketConsistencyDiagnostics(StorySourceValidationResult result, StorySourceValidationPacketEntry packetEntry)
        {
            StorySourceValidationInputPacketDocument packet = packetEntry.Document;
            StorySourceValidationPacketIndexEntry index = packetEntry.IndexEntry;
            string id = FirstNonEmpty(packet.StorySourceId, index.StorySourceId, packetEntry.StorySourceId);

            if (!packetEntry.PacketExists)
                return;

            if (!string.Equals(index.StorySourceId ?? string.Empty, packet.StorySourceId ?? string.Empty, StringComparison.Ordinal))
                result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceVersionConflict, PacketConfigKind, id, "Index storySourceId 与 packet storySourceId 不一致。", "修正 story_packets.json 或 packet 文件，使二者一致。", "storySourceId"));
            if (!string.Equals(index.Version ?? string.Empty, packet.Version ?? string.Empty, StringComparison.Ordinal))
                result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceVersionConflict, PacketConfigKind, id, "Index version 与 packet version 不一致。", "修正 story_packets.json 或 packet 文件，使二者一致。", "version"));
            if (!string.Equals(index.ApprovalStatus ?? string.Empty, packet.ApprovalStatus ?? string.Empty, StringComparison.Ordinal))
                result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceVersionConflict, PacketConfigKind, id, "Index approvalStatus 与 packet approvalStatus 不一致。", "只能通过 approve endpoint 进入 approved-for-art，并保持 index 与 packet 同步。", "approvalStatus"));
            if (!StringListsEqual(index.TargetConsumers, packet.TargetConsumers))
                result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceVersionConflict, PacketConfigKind, id, "Index targetConsumers 与 packet targetConsumers 不一致。", "修正 story_packets.json 或 packet 文件，使消费方一致。", "targetConsumers"));
        }

        private static void AddPacketRequiredFieldDiagnostics(StorySourceValidationResult result, StorySourceValidationPacketEntry packetEntry)
        {
            List<string> missingFields = GetMissingRequiredFields(packetEntry.Document);
            for (int i = 0; i < missingFields.Count; i++)
            {
                result.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Warning,
                    AuthoringResourceDiagnosticCodes.StorySourceRequiredFieldMissing,
                    PacketConfigKind,
                    packetEntry.StorySourceId,
                    "Story Input Packet 缺少必填字段：" + missingFields[i],
                    "批准给美术前必须补齐该字段。",
                    missingFields[i]));
            }
        }

        private static void AddPacketDerivationDiagnostics(StorySourceValidationResult result, StorySourceValidationPacketEntry packetEntry, bool forApproval)
        {
            bool blocked = false;
            StorySourceValidationInputPacketDocument packet = packetEntry.Document;
            string id = packetEntry.StorySourceId ?? string.Empty;
            List<string> canonRefs = NormalizeStringList(packet != null ? packet.CanonRefs : null);
            if (canonRefs.Count == 0)
            {
                blocked = true;
                result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceCanonRefMissing, PacketConfigKind, id, "Story Input Packet 必须引用 Canon facts。", "绑定 canonRefs 到已批准的世界观记忆。", "canonRefs"));
            }

            for (int i = 0; i < canonRefs.Count; i++)
            {
                string canonId = canonRefs[i];
                if (!result.CanonById.TryGetValue(canonId, out StorySourceValidationCanonFact fact))
                {
                    blocked = true;
                    result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceCanonRefMissing, PacketConfigKind, id, "缺少 Canon fact：" + canonId, "创建 Canon fact 或移除该引用。", "canonRefs"));
                }
                else if (!string.Equals(fact.Status ?? string.Empty, "canon", StringComparison.Ordinal))
                {
                    blocked = true;
                    result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceCanonRefNotApproved, PacketConfigKind, id, "引用的 Canon fact 不是 canon：" + canonId, "先将该 Canon fact 批准为 canon。", "canonRefs"));
                }
            }

            List<string> outlineRefs = NormalizeStringList(packet != null ? packet.OutlineRefs : null);
            if (outlineRefs.Count == 0)
            {
                blocked = true;
                result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceOutlineRefMissing, PacketConfigKind, id, "Story Input Packet 必须引用已批准的 Story Outline。", "绑定 outlineRefs 到 approved-for-packet 的故事大纲。", "outlineRefs"));
            }
            for (int i = 0; i < outlineRefs.Count; i++)
            {
                string outlineId = outlineRefs[i];
                if (!result.OutlineById.TryGetValue(outlineId, out StorySourceValidationOutlineEntry outline))
                {
                    blocked = true;
                    result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceOutlineRefMissing, PacketConfigKind, id, "缺少 Story Outline：" + outlineId, "创建大纲或移除该引用。", "outlineRefs"));
                }
                else if (!string.Equals(outline.Stage ?? string.Empty, "approved-for-packet", StringComparison.Ordinal))
                {
                    blocked = true;
                    result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceOutlineRefMissing, PacketConfigKind, id, "引用的大纲未批准给输入包：" + outlineId, "先通过大纲审批。", "outlineRefs"));
                }
                else if (HasBlockingDiagnostics(ValidateOutlineApproval(result, outline)))
                {
                    blocked = true;
                    result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceOutlineBeatInvalid, PacketConfigKind, id, "引用的大纲未通过派生校验：" + outlineId, "修复大纲 Markdown、entry、beat 或 Canon 引用后再审批 packet。", "outlineRefs"));
                }
            }

            if (blocked)
            {
                result.Diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourcePacketDerivationBlocked, PacketConfigKind, id, "Packet 的 Canon/Outline 派生条件未满足。", "先批准引用的 Canon facts 和 Outlines，再批准 packet。", forApproval ? "approvalStatus" : "canonRefs"));
            }
        }

        private static List<AuthoringResourceDiagnostic> ValidateOutlineApproval(StorySourceValidationResult result, StorySourceValidationOutlineEntry outline)
        {
            var diagnostics = new List<AuthoringResourceDiagnostic>();
            if (result == null || outline == null)
                return diagnostics;

            string id = outline.OutlineId ?? string.Empty;
            if (!OutlineStages.Contains(outline.Stage ?? string.Empty))
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceJsonInvalid, OutlineConfigKind, id, "不支持的故事大纲阶段：" + (outline.Stage ?? string.Empty), "使用 draft、review、approved-for-packet 或 superseded。", "stage"));

            List<string> canonRefs = NormalizeStringList(outline.CanonRefs);
            if (canonRefs.Count == 0)
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceCanonRefMissing, OutlineConfigKind, id, "大纲必须引用 Canon facts。", "将世界观事实放入 Canon 并在 canonRefs 引用。", "canonRefs"));

            for (int i = 0; i < canonRefs.Count; i++)
            {
                string canonId = canonRefs[i];
                if (!result.CanonById.TryGetValue(canonId, out StorySourceValidationCanonFact fact))
                    diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceCanonRefMissing, OutlineConfigKind, id, "缺少 Canon fact：" + canonId, "创建 Canon fact 或移除该引用。", "canonRefs"));
                else if (!string.Equals(fact.Status ?? string.Empty, "canon", StringComparison.Ordinal))
                    diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceCanonRefNotApproved, OutlineConfigKind, id, "引用的 Canon fact 不是 canon：" + canonId, "先将该 Canon fact 批准为 canon。", "canonRefs"));
            }

            string outlineIssue = GetOutlineContentIssue(result.ProjectRootPath, outline);
            if (!string.IsNullOrWhiteSpace(outlineIssue))
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceOutlineBeatInvalid, OutlineConfigKind, id, outlineIssue, "修正 story_outlines.json 和 Markdown front matter/Beat 标题后再批准。", "beats"));

            return diagnostics;
        }

        private static List<AuthoringResourceDiagnostic> ValidatePacketApproval(StorySourceValidationResult result, StorySourceValidationPacketEntry packetEntry, bool requireApprovedStatus)
        {
            var diagnostics = new List<AuthoringResourceDiagnostic>();
            if (result == null || packetEntry == null || packetEntry.Document == null || packetEntry.IndexEntry == null)
                return diagnostics;

            string id = FirstNonEmpty(packetEntry.Document.StorySourceId, packetEntry.IndexEntry.StorySourceId, packetEntry.StorySourceId);
            if (!packetEntry.PacketExists)
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Error, AuthoringResourceDiagnosticCodes.StorySourceJsonInvalid, PacketConfigKind, id, "故事输入包文件不存在：" + (packetEntry.RelativePacketPath ?? string.Empty), "创建 packet 文件或修正 packetPath。", "packetPath"));

            if (string.IsNullOrWhiteSpace(packetEntry.Document.Schema) || !string.Equals(packetEntry.Document.Schema, PacketSchemaId, StringComparison.Ordinal))
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceJsonInvalid, PacketConfigKind, id, "Story packet schema 必须是 " + PacketSchemaId + "。", "修正 schema 后再审批。", "schema"));

            AddConsistencyDiagnosticsToList(diagnostics, packetEntry);
            if (requireApprovedStatus &&
                (!EqualsStatus(packetEntry.Document.ApprovalStatus, "approved-for-art") ||
                 !EqualsStatus(packetEntry.IndexEntry.ApprovalStatus, "approved-for-art")))
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceNotApproved, PacketConfigKind, id, "Packet 尚未处于 approved-for-art。", "只能通过 StorySource approve endpoint 批准。", "approvalStatus"));

            if (!ContainsValue(packetEntry.Document.TargetConsumers, "artSource") ||
                !ContainsValue(packetEntry.IndexEntry.TargetConsumers, "artSource"))
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceArtReferenceMissing, PacketConfigKind, id, "批准给美术前 targetConsumers 必须包含 artSource。", "在 index 与 packet 中保持 targetConsumers 包含 artSource。", "targetConsumers"));

            List<string> missingFields = GetMissingRequiredFields(packetEntry.Document);
            for (int i = 0; i < missingFields.Count; i++)
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceRequiredFieldMissing, PacketConfigKind, id, "批准给美术前必须补齐字段：" + missingFields[i], "填写该字段后再审批。", missingFields[i]));

            diagnostics.AddRange(CreatePacketDerivationApprovalDiagnostics(result, packetEntry));
            if (TargetsArtSource(packetEntry.Document) && !HasArtSourceTask(result.ProjectRootPath, packetEntry.IndexEntry.ArtSourceTaskId))
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceArtReferenceMissing, PacketConfigKind, id, "Packet 指向 ArtSource，但找不到 ArtSource task：" + FirstNonEmpty(packetEntry.IndexEntry.ArtSourceTaskId, "(empty)"), "补充或修正 story_packets.json 的 artSourceTaskId。", "artSourceTaskId"));
            return diagnostics;
        }

        private static List<AuthoringResourceDiagnostic> CreatePacketDerivationApprovalDiagnostics(StorySourceValidationResult result, StorySourceValidationPacketEntry packetEntry)
        {
            var diagnostics = new List<AuthoringResourceDiagnostic>();
            string id = packetEntry.StorySourceId ?? string.Empty;
            StorySourceValidationInputPacketDocument packet = packetEntry.Document;

            List<string> canonRefs = NormalizeStringList(packet != null ? packet.CanonRefs : null);
            if (canonRefs.Count == 0)
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceCanonRefMissing, PacketConfigKind, id, "packet 缺少 canonRefs。", "批准给美术前必须引用 Canon facts。", "canonRefs"));
            for (int i = 0; i < canonRefs.Count; i++)
            {
                string canonId = canonRefs[i];
                if (!result.CanonById.TryGetValue(canonId, out StorySourceValidationCanonFact fact))
                    diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceCanonRefMissing, PacketConfigKind, id, "缺少 Canon：" + canonId, "创建 Canon fact 或移除该引用。", "canonRefs"));
                else if (!string.Equals(fact.Status ?? string.Empty, "canon", StringComparison.Ordinal))
                    diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceCanonRefNotApproved, PacketConfigKind, id, "Canon 未批准为 canon：" + canonId, "先将该 Canon fact 批准为 canon。", "canonRefs"));
            }

            List<string> outlineRefs = NormalizeStringList(packet != null ? packet.OutlineRefs : null);
            if (outlineRefs.Count == 0)
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceOutlineRefMissing, PacketConfigKind, id, "packet 缺少 outlineRefs。", "批准给美术前必须引用 approved-for-packet 的故事大纲。", "outlineRefs"));
            for (int i = 0; i < outlineRefs.Count; i++)
            {
                string outlineId = outlineRefs[i];
                if (!result.OutlineById.TryGetValue(outlineId, out StorySourceValidationOutlineEntry outline))
                {
                    diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceOutlineRefMissing, PacketConfigKind, id, "缺少 Outline：" + outlineId, "创建大纲或移除该引用。", "outlineRefs"));
                    continue;
                }

                if (!string.Equals(outline.Stage ?? string.Empty, "approved-for-packet", StringComparison.Ordinal))
                    diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceOutlineRefMissing, PacketConfigKind, id, "Outline 未批准给输入包：" + outlineId, "先通过大纲审批。", "outlineRefs"));
                diagnostics.AddRange(ValidateOutlineApproval(result, outline).Select(diagnostic => CloneForPacket(diagnostic, id, "outlineRefs")));
            }

            if (diagnostics.Count > 0)
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourcePacketDerivationBlocked, PacketConfigKind, id, "批准给美术前必须解决 Canon/Outline 引用。", "先修复派生链路，再批准 packet。", "approvalStatus"));
            return diagnostics;
        }

        private static void AddOutlineContentDiagnostics(StorySourceValidationResult result, StorySourceValidationOutlineEntry outline)
        {
            string issue = GetOutlineContentIssue(result.ProjectRootPath, outline);
            if (!string.IsNullOrWhiteSpace(issue))
            {
                result.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Warning,
                    AuthoringResourceDiagnosticCodes.StorySourceOutlineBeatInvalid,
                    OutlineConfigKind,
                    outline != null ? outline.OutlineId ?? string.Empty : string.Empty,
                    issue,
                    "补齐 beats，并保持 Markdown front matter entry 与 ## Beat 标题一致。",
                    "beats"));
            }

            List<string> canonRefs = NormalizeStringList(outline != null ? outline.CanonRefs : null);
            if (canonRefs.Count == 0)
            {
                result.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Warning,
                    AuthoringResourceDiagnosticCodes.StorySourceCanonRefMissing,
                    OutlineConfigKind,
                    outline != null ? outline.OutlineId ?? string.Empty : string.Empty,
                    "Story outline 必须引用 Canon facts。",
                    "将世界观事实移入 Canon，并在大纲 canonRefs 引用。",
                    "canonRefs"));
            }

            for (int i = 0; i < canonRefs.Count; i++)
            {
                string canonId = canonRefs[i];
                if (!result.CanonById.TryGetValue(canonId, out StorySourceValidationCanonFact fact))
                {
                    result.Diagnostics.Add(CreateDiagnostic(
                        CharacterAuthoringValidationSeverity.Warning,
                        AuthoringResourceDiagnosticCodes.StorySourceCanonRefMissing,
                        OutlineConfigKind,
                        outline != null ? outline.OutlineId ?? string.Empty : string.Empty,
                        "Story outline 引用的 Canon fact 不存在：" + canonId,
                        "创建 Canon fact 或移除该引用。",
                        "canonRefs"));
                }
                else if (!string.Equals(fact.Status ?? string.Empty, "canon", StringComparison.Ordinal))
                {
                    result.Diagnostics.Add(CreateDiagnostic(
                        CharacterAuthoringValidationSeverity.Warning,
                        AuthoringResourceDiagnosticCodes.StorySourceCanonRefNotApproved,
                        OutlineConfigKind,
                        outline != null ? outline.OutlineId ?? string.Empty : string.Empty,
                        "Story outline 引用的 Canon fact 不是 canon：" + canonId,
                        "先将该 Canon fact 批准为 canon。",
                        "canonRefs"));
                }
            }
        }

        private static string GetOutlineContentIssue(string projectRootPath, StorySourceValidationOutlineEntry outline)
        {
            if (outline == null || outline.Beats == null || outline.Beats.Count == 0)
                return "故事大纲至少需要一个 beat。";
            if (string.IsNullOrWhiteSpace(outline.OutlinePath))
                return "故事大纲缺少 outlinePath。";

            string path = ResolveProjectPath(projectRootPath, outline.OutlinePath);
            if (!File.Exists(path))
                return "故事大纲 Markdown 不存在：" + outline.OutlinePath;

            string text = File.ReadAllText(path);
            string entry = ReadFrontMatterValue(text, "entry");
            if (string.IsNullOrWhiteSpace(entry))
                return "故事大纲 Markdown front matter 缺少 entry。";

            bool beatInIndex = false;
            for (int i = 0; i < outline.Beats.Count; i++)
            {
                if (string.Equals(outline.Beats[i] != null ? outline.Beats[i].Slug ?? string.Empty : string.Empty, entry, StringComparison.Ordinal))
                {
                    beatInIndex = true;
                    break;
                }
            }
            if (!beatInIndex)
                return "故事大纲 entry 未指向 story_outlines.json 中的 beat：" + entry;
            if (!ContainsBeatHeading(text, entry))
                return "故事大纲 Markdown 缺少 entry 对应的 Beat 标题：## Beat " + entry;
            return string.Empty;
        }

        private static void AddConsistencyDiagnosticsToList(List<AuthoringResourceDiagnostic> diagnostics, StorySourceValidationPacketEntry packetEntry)
        {
            StorySourceValidationInputPacketDocument packet = packetEntry.Document;
            StorySourceValidationPacketIndexEntry index = packetEntry.IndexEntry;
            string id = FirstNonEmpty(packet.StorySourceId, index.StorySourceId, packetEntry.StorySourceId);
            if (!string.Equals(index.StorySourceId ?? string.Empty, packet.StorySourceId ?? string.Empty, StringComparison.Ordinal))
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceVersionConflict, PacketConfigKind, id, "Index storySourceId 与 packet storySourceId 不一致。", "保持 index 与 packet 一致。", "storySourceId"));
            if (!string.Equals(index.Version ?? string.Empty, packet.Version ?? string.Empty, StringComparison.Ordinal))
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceVersionConflict, PacketConfigKind, id, "Index version 与 packet version 不一致。", "保持 index 与 packet 一致。", "version"));
            if (!string.Equals(index.ApprovalStatus ?? string.Empty, packet.ApprovalStatus ?? string.Empty, StringComparison.Ordinal))
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceVersionConflict, PacketConfigKind, id, "Index approvalStatus 与 packet approvalStatus 不一致。", "保持 index 与 packet 一致。", "approvalStatus"));
            if (!StringListsEqual(index.TargetConsumers, packet.TargetConsumers))
                diagnostics.Add(CreateDiagnostic(CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceVersionConflict, PacketConfigKind, id, "Index targetConsumers 与 packet targetConsumers 不一致。", "保持 index 与 packet 一致。", "targetConsumers"));
        }

        private static AuthoringResourceDiagnostic CloneForPacket(AuthoringResourceDiagnostic diagnostic, string packetId, string sourceField)
        {
            return CreateDiagnostic(
                diagnostic != null ? diagnostic.Severity : CharacterAuthoringValidationSeverity.Warning,
                diagnostic != null ? diagnostic.Code : AuthoringResourceDiagnosticCodes.StorySourcePacketDerivationBlocked,
                PacketConfigKind,
                packetId ?? string.Empty,
                diagnostic != null ? diagnostic.Message ?? string.Empty : string.Empty,
                diagnostic != null ? diagnostic.SuggestedFix ?? string.Empty : string.Empty,
                sourceField ?? string.Empty);
        }

        private static bool HasArtSourceTask(string projectRootPath, string taskId)
        {
            if (string.IsNullOrWhiteSpace(projectRootPath) || string.IsNullOrWhiteSpace(taskId))
                return false;

            string path = ResolveProjectPath(projectRootPath, StorySourceAuthoringResourceProvider.ArtTasksRelativePath);
            if (!File.Exists(path))
                return false;

            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                if (!document.RootElement.TryGetProperty("tasks", out JsonElement tasks) || tasks.ValueKind != JsonValueKind.Array)
                    return false;

                foreach (JsonElement task in tasks.EnumerateArray())
                {
                    if (task.ValueKind == JsonValueKind.Object &&
                        task.TryGetProperty("taskId", out JsonElement id) &&
                        id.ValueKind == JsonValueKind.String &&
                        string.Equals(id.GetString() ?? string.Empty, taskId, StringComparison.Ordinal))
                        return true;
                }
            }
            catch (JsonException)
            {
                return false;
            }

            return false;
        }

        private static string ReadFrontMatterValue(string text, string key)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(key))
                return string.Empty;

            using StringReader reader = new StringReader(text);
            string line;
            bool inFrontMatter = false;
            bool sawOpening = false;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (!sawOpening)
                {
                    if (trimmed == "---")
                    {
                        sawOpening = true;
                        inFrontMatter = true;
                    }
                    continue;
                }

                if (inFrontMatter && trimmed == "---")
                    break;

                if (inFrontMatter && trimmed.StartsWith(key + ":", StringComparison.Ordinal))
                    return trimmed.Substring(key.Length + 1).Trim();
            }

            return string.Empty;
        }

        private static bool ContainsBeatHeading(string text, string slug)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(slug))
                return false;

            using StringReader reader = new StringReader(text);
            string line;
            string expected = "## Beat " + slug;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.Equals(line.Trim(), expected, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string GetStringField(StorySourceValidationInputPacketDocument packet, string field)
        {
            if (field == "storySourceId") return packet.StorySourceId;
            if (field == "version") return packet.Version;
            if (field == "approvalStatus") return packet.ApprovalStatus;
            if (field == "worldPremise") return packet.WorldPremise;
            if (field == "coreEvent") return packet.CoreEvent;
            if (field == "locationFunction") return packet.LocationFunction;
            if (field == "civilizationOrigin") return packet.CivilizationOrigin;
            if (field == "playerRole") return packet.PlayerRole;
            return string.Empty;
        }

        private static List<string> GetArrayField(StorySourceValidationInputPacketDocument packet, string field)
        {
            if (field == "targetConsumers") return packet.TargetConsumers ?? new List<string>();
            if (field == "canonRefs") return packet.CanonRefs ?? new List<string>();
            if (field == "outlineRefs") return packet.OutlineRefs ?? new List<string>();
            if (field == "enemyOrigins") return packet.EnemyOrigins ?? new List<string>();
            if (field == "powerRules") return packet.PowerRules ?? new List<string>();
            if (field == "symbolMeanings") return packet.SymbolMeanings ?? new List<string>();
            if (field == "toneTags") return packet.ToneTags ?? new List<string>();
            if (field == "visualDoNotUse") return packet.VisualDoNotUse ?? new List<string>();
            return new List<string>();
        }

        private static StorySourceValidationInputPacketDocument CreatePacketFallback(StorySourceValidationPacketIndexEntry entry)
        {
            return new StorySourceValidationInputPacketDocument
            {
                Schema = PacketSchemaId,
                StorySourceId = entry != null ? entry.StorySourceId ?? string.Empty : string.Empty,
                Version = entry != null ? entry.Version ?? string.Empty : string.Empty,
                ApprovalStatus = entry != null ? entry.ApprovalStatus ?? string.Empty : string.Empty,
                TargetConsumers = entry != null ? entry.TargetConsumers ?? new List<string>() : new List<string>(),
                ReviewStatus = entry != null ? entry.ReviewStatus ?? string.Empty : string.Empty,
                ExportPath = entry != null ? entry.ExportPath ?? string.Empty : string.Empty
            };
        }

        private static void ApplyIndexFallback(StorySourceValidationPacketIndexEntry entry, StorySourceValidationInputPacketDocument packet)
        {
            if (entry == null || packet == null)
                return;
            if (string.IsNullOrWhiteSpace(packet.StorySourceId)) packet.StorySourceId = entry.StorySourceId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(packet.Version)) packet.Version = entry.Version ?? string.Empty;
            if (string.IsNullOrWhiteSpace(packet.ApprovalStatus)) packet.ApprovalStatus = entry.ApprovalStatus ?? string.Empty;
            if (packet.TargetConsumers == null || packet.TargetConsumers.Count == 0) packet.TargetConsumers = entry.TargetConsumers ?? new List<string>();
            if (string.IsNullOrWhiteSpace(packet.ReviewStatus)) packet.ReviewStatus = entry.ReviewStatus ?? string.Empty;
            if (string.IsNullOrWhiteSpace(packet.ExportPath)) packet.ExportPath = entry.ExportPath ?? string.Empty;
        }

        private static bool TargetsArtSource(StorySourceValidationInputPacketDocument packet)
        {
            return packet != null && ContainsValue(packet.TargetConsumers, "artSource");
        }

        private static bool ContainsValue(List<string> values, string expected)
        {
            for (int i = 0; values != null && i < values.Count; i++)
            {
                if (string.Equals(values[i] ?? string.Empty, expected ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool StringListsEqual(List<string> left, List<string> right)
        {
            List<string> normalizedLeft = NormalizeStringList(left);
            List<string> normalizedRight = NormalizeStringList(right);
            if (normalizedLeft.Count != normalizedRight.Count)
                return false;
            for (int i = 0; i < normalizedLeft.Count; i++)
            {
                if (!string.Equals(normalizedLeft[i], normalizedRight[i], StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static string ResolveProjectPath(string projectRootPath, string relativePath)
        {
            return AuthoringResourceProviderUtilities.ResolveProjectPath(projectRootPath ?? string.Empty, relativePath ?? string.Empty);
        }

        private static string BuildDefaultPacketPath(string packetId)
        {
            return StorySourceAuthoringResourceProvider.PacketsRootRelativePath + "/" + AuthoringResourceProviderUtilities.SanitizeStableSegment(packetId) + ".json";
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return AuthoringResourceProviderUtilities.FirstNonEmpty(values);
        }

        private static bool EqualsStatus(string value, string expected)
        {
            return string.Equals(value ?? string.Empty, expected ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static AuthoringResourceDiagnostic CreateDiagnostic(
            CharacterAuthoringValidationSeverity severity,
            string code,
            string sourceConfigKind,
            string sourceStableId,
            string message,
            string suggestedFix,
            string sourceField)
        {
            return new AuthoringResourceDiagnostic
            {
                Severity = severity,
                Code = code ?? string.Empty,
                ProviderId = AuthoringResourceProviderIds.StorySource,
                SourceConfigKind = sourceConfigKind ?? SourceConfigKind,
                SourceStableId = sourceStableId ?? string.Empty,
                SourceField = sourceField ?? string.Empty,
                Message = message ?? string.Empty,
                SuggestedFix = suggestedFix ?? string.Empty
            };
        }
    }

    public sealed class StorySourceValidationResult
    {
        public string ProjectRootPath { get; set; } = string.Empty;
        public StorySourceValidationIndexDocument Index { get; set; } = new StorySourceValidationIndexDocument();
        public StorySourceValidationCanonDocument Canon { get; set; } = new StorySourceValidationCanonDocument();
        public StorySourceValidationOutlineDocument Outlines { get; set; } = new StorySourceValidationOutlineDocument();
        public List<StorySourceValidationPacketEntry> Packets { get; set; } = new List<StorySourceValidationPacketEntry>();
        public Dictionary<string, StorySourceValidationCanonFact> CanonById { get; set; } = new Dictionary<string, StorySourceValidationCanonFact>(StringComparer.Ordinal);
        public Dictionary<string, StorySourceValidationOutlineEntry> OutlineById { get; set; } = new Dictionary<string, StorySourceValidationOutlineEntry>(StringComparer.Ordinal);
        public List<AuthoringResourceDiagnostic> Diagnostics { get; set; } = new List<AuthoringResourceDiagnostic>();

        public StorySourceValidationOutlineEntry FindOutline(string outlineId)
        {
            if (string.IsNullOrWhiteSpace(outlineId))
                return null;
            return OutlineById.TryGetValue(outlineId, out StorySourceValidationOutlineEntry outline) ? outline : null;
        }

        public StorySourceValidationPacketEntry FindPacket(string storySourceId)
        {
            for (int i = 0; i < Packets.Count; i++)
            {
                StorySourceValidationPacketEntry packet = Packets[i];
                if (packet != null &&
                    string.Equals(packet.StorySourceId ?? string.Empty, storySourceId ?? string.Empty, StringComparison.Ordinal))
                    return packet;
            }
            return null;
        }

        public List<AuthoringResourceDiagnostic> GetDiagnostics(string sourceConfigKind, string sourceStableId)
        {
            var result = new List<AuthoringResourceDiagnostic>();
            for (int i = 0; Diagnostics != null && i < Diagnostics.Count; i++)
            {
                AuthoringResourceDiagnostic diagnostic = Diagnostics[i];
                if (diagnostic == null)
                    continue;
                if (string.Equals(diagnostic.SourceConfigKind ?? string.Empty, sourceConfigKind ?? string.Empty, StringComparison.Ordinal) &&
                    string.Equals(diagnostic.SourceStableId ?? string.Empty, sourceStableId ?? string.Empty, StringComparison.Ordinal))
                    result.Add(diagnostic);
            }
            return result;
        }
    }

    public sealed class StorySourceVerifiedPacket
    {
        public string StorySourceId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string PacketPath { get; set; } = string.Empty;
        public string ArtSourceTaskId { get; set; } = string.Empty;
    }

    public sealed class StorySourceValidationPacketEntry
    {
        public string StorySourceId { get; set; } = string.Empty;
        public StorySourceValidationPacketIndexEntry IndexEntry { get; set; } = new StorySourceValidationPacketIndexEntry();
        public StorySourceValidationInputPacketDocument Document { get; set; } = new StorySourceValidationInputPacketDocument();
        public string RelativePacketPath { get; set; } = string.Empty;
        public bool PacketExists { get; set; }
    }

    public sealed class StorySourceValidationIndexDocument
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }
        [JsonPropertyName("projectId")]
        public string ProjectId { get; set; } = string.Empty;
        [JsonPropertyName("updated")]
        public string Updated { get; set; } = string.Empty;
        [JsonPropertyName("packets")]
        public List<StorySourceValidationPacketIndexEntry> Packets { get; set; } = new List<StorySourceValidationPacketIndexEntry>();
    }

    public sealed class StorySourceValidationPacketIndexEntry
    {
        [JsonPropertyName("storySourceId")]
        public string StorySourceId { get; set; } = string.Empty;
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
        [JsonPropertyName("approvalStatus")]
        public string ApprovalStatus { get; set; } = string.Empty;
        [JsonPropertyName("targetConsumers")]
        public List<string> TargetConsumers { get; set; } = new List<string>();
        [JsonPropertyName("canonRefs")]
        public List<string> CanonRefs { get; set; } = new List<string>();
        [JsonPropertyName("outlineRefs")]
        public List<string> OutlineRefs { get; set; } = new List<string>();
        [JsonPropertyName("reviewStatus")]
        public string ReviewStatus { get; set; } = string.Empty;
        [JsonPropertyName("packetPath")]
        public string PacketPath { get; set; } = string.Empty;
        [JsonPropertyName("exportPath")]
        public string ExportPath { get; set; } = string.Empty;
        [JsonPropertyName("artSourceTaskId")]
        public string ArtSourceTaskId { get; set; } = string.Empty;
        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;
    }

    public sealed class StorySourceValidationInputPacketDocument
    {
        [JsonPropertyName("schema")]
        public string Schema { get; set; } = string.Empty;
        [JsonPropertyName("storySourceId")]
        public string StorySourceId { get; set; } = string.Empty;
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
        [JsonPropertyName("approvalStatus")]
        public string ApprovalStatus { get; set; } = string.Empty;
        [JsonPropertyName("targetConsumers")]
        public List<string> TargetConsumers { get; set; } = new List<string>();
        [JsonPropertyName("canonRefs")]
        public List<string> CanonRefs { get; set; } = new List<string>();
        [JsonPropertyName("outlineRefs")]
        public List<string> OutlineRefs { get; set; } = new List<string>();
        [JsonPropertyName("reviewStatus")]
        public string ReviewStatus { get; set; } = string.Empty;
        [JsonPropertyName("exportPath")]
        public string ExportPath { get; set; } = string.Empty;
        [JsonPropertyName("worldPremise")]
        public string WorldPremise { get; set; } = string.Empty;
        [JsonPropertyName("coreEvent")]
        public string CoreEvent { get; set; } = string.Empty;
        [JsonPropertyName("locationFunction")]
        public string LocationFunction { get; set; } = string.Empty;
        [JsonPropertyName("civilizationOrigin")]
        public string CivilizationOrigin { get; set; } = string.Empty;
        [JsonPropertyName("playerRole")]
        public string PlayerRole { get; set; } = string.Empty;
        [JsonPropertyName("enemyOrigins")]
        public List<string> EnemyOrigins { get; set; } = new List<string>();
        [JsonPropertyName("powerRules")]
        public List<string> PowerRules { get; set; } = new List<string>();
        [JsonPropertyName("symbolMeanings")]
        public List<string> SymbolMeanings { get; set; } = new List<string>();
        [JsonPropertyName("toneTags")]
        public List<string> ToneTags { get; set; } = new List<string>();
        [JsonPropertyName("visualDoNotUse")]
        public List<string> VisualDoNotUse { get; set; } = new List<string>();
        [JsonPropertyName("changeLog")]
        public List<StorySourceValidationPacketChangeLogEntry> ChangeLog { get; set; } = new List<StorySourceValidationPacketChangeLogEntry>();
    }

    public sealed class StorySourceValidationCanonDocument
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }
        [JsonPropertyName("projectId")]
        public string ProjectId { get; set; } = string.Empty;
        [JsonPropertyName("updated")]
        public string Updated { get; set; } = string.Empty;
        [JsonPropertyName("facts")]
        public List<StorySourceValidationCanonFact> Facts { get; set; } = new List<StorySourceValidationCanonFact>();
    }

    public sealed class StorySourceValidationCanonFact
    {
        [JsonPropertyName("canonId")]
        public string CanonId { get; set; } = string.Empty;
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new List<string>();
        [JsonPropertyName("relations")]
        public List<StorySourceValidationCanonRelation> Relations { get; set; } = new List<StorySourceValidationCanonRelation>();
        [JsonPropertyName("usedByPacketIds")]
        public List<string> UsedByPacketIds { get; set; } = new List<string>();
        [JsonPropertyName("usedByOutlineIds")]
        public List<string> UsedByOutlineIds { get; set; } = new List<string>();
    }

    public sealed class StorySourceValidationCanonRelation
    {
        [JsonPropertyName("relation")]
        public string Relation { get; set; } = string.Empty;
        [JsonPropertyName("targetId")]
        public string TargetId { get; set; } = string.Empty;
    }

    public sealed class StorySourceValidationOutlineDocument
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }
        [JsonPropertyName("projectId")]
        public string ProjectId { get; set; } = string.Empty;
        [JsonPropertyName("updated")]
        public string Updated { get; set; } = string.Empty;
        [JsonPropertyName("outlines")]
        public List<StorySourceValidationOutlineEntry> Outlines { get; set; } = new List<StorySourceValidationOutlineEntry>();
    }

    public sealed class StorySourceValidationOutlineEntry
    {
        [JsonPropertyName("outlineId")]
        public string OutlineId { get; set; } = string.Empty;
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
        [JsonPropertyName("stage")]
        public string Stage { get; set; } = string.Empty;
        [JsonPropertyName("outlinePath")]
        public string OutlinePath { get; set; } = string.Empty;
        [JsonPropertyName("canonRefs")]
        public List<string> CanonRefs { get; set; } = new List<string>();
        [JsonPropertyName("beats")]
        public List<StorySourceValidationOutlineBeat> Beats { get; set; } = new List<StorySourceValidationOutlineBeat>();
        [JsonPropertyName("conflicts")]
        public List<string> Conflicts { get; set; } = new List<string>();
        [JsonPropertyName("openQuestions")]
        public List<string> OpenQuestions { get; set; } = new List<string>();
        [JsonPropertyName("targetConsumers")]
        public List<string> TargetConsumers { get; set; } = new List<string>();
    }

    public sealed class StorySourceValidationOutlineBeat
    {
        [JsonPropertyName("slug")]
        public string Slug { get; set; } = string.Empty;
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;
    }

    public sealed class StorySourceValidationPacketChangeLogEntry
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;
    }
}
