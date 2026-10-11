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
    internal static StorySourcePacketResponse SaveStorySourcePacketUpdate(string rootPath, StorySourcePacketUpdateRequest request, JsonSerializerOptions jsonOptions)
    {
        request ??= new StorySourcePacketUpdateRequest();
        StorySourcePacketLoadResult load = LoadStorySourcePacketForWrite(rootPath, request.storySourceId, request.expectedHash, jsonOptions, AuthoringResourceDiagnosticCodes.StorySourcePacketUpdateConflict);
        if (load.Response != null)
            return load.Response;

        string approvalStatus = NormalizeStoryApprovalStatus(request.approvalStatus);
        if (string.IsNullOrWhiteSpace(approvalStatus))
            return StorySourcePacketResponse.InvalidUpdate(load.RelativeIndexPath, "不支持的故事输入包审批状态：" + (request.approvalStatus ?? string.Empty), request.storySourceId, load.CurrentHash);
        if (approvalStatus == "approved-for-art")
            return StorySourcePacketResponse.InvalidUpdate(
                load.RelativePacketPath,
                "故事输入包不能通过保存草稿进入 approved-for-art，请使用“批准给美术”。",
                request.storySourceId,
                load.CurrentHash);

        ApplyStoryPacketUpdate(load.Packet, request, approvalStatus);

        UpdateStoryPacketIndexEntry(load.IndexEntry, load.Packet);
        load.Index.Updated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        WriteJsonFileAtomic(load.PacketPath, load.Packet, jsonOptions);
        WriteJsonFileAtomic(load.IndexPath, load.Index, jsonOptions);
        InvalidateAuthoringResourceCache(rootPath);

        string nextHash = StorySourceAuthoringResourceProvider.ComputeStorySourceHash(rootPath);
        return StorySourcePacketResponse.Success(load.RelativePacketPath, load.Packet.StorySourceId, load.CurrentHash, nextHash, "故事输入包草稿已保存。");
    }

    internal static StorySourcePacketResponse SaveStorySourcePacketApprove(string rootPath, StorySourcePacketApproveRequest request, JsonSerializerOptions jsonOptions)
    {
        request ??= new StorySourcePacketApproveRequest();
        StorySourcePacketLoadResult load = LoadStorySourcePacketForWrite(rootPath, request.storySourceId, request.expectedHash, jsonOptions, AuthoringResourceDiagnosticCodes.StorySourcePacketApproveConflict);
        if (load.Response != null)
            return load.Response;

        List<AuthoringResourceDiagnostic> approvalDiagnostics = StorySourceAuthoringValidator.GetPacketApprovalDiagnostics(rootPath, request.storySourceId);
        if (StorySourceAuthoringValidator.HasBlockingDiagnostics(approvalDiagnostics))
            return StorySourcePacketResponse.InvalidApprove(
                load.RelativePacketPath,
                "批准给美术前必须解决：" + FormatStorySourceDiagnosticMessages(approvalDiagnostics),
                request.storySourceId,
                load.CurrentHash,
                approvalDiagnostics);

        load.Packet.ApprovalStatus = "approved-for-art";
        load.Packet.ReviewStatus = string.IsNullOrWhiteSpace(request.reviewStatus)
            ? "approved-for-art"
            : request.reviewStatus.Trim();
        load.Packet.ChangeLog ??= new List<StorySourcePacketChangeLogEntry>();
        load.Packet.ChangeLog.Add(new StorySourcePacketChangeLogEntry
        {
            Date = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Status = "approved-for-art",
            Summary = "Approved for ArtSource production reference handoff."
        });

        UpdateStoryPacketIndexEntry(load.IndexEntry, load.Packet);
        load.Index.Updated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        WriteJsonFileAtomic(load.PacketPath, load.Packet, jsonOptions);
        WriteJsonFileAtomic(load.IndexPath, load.Index, jsonOptions);
        InvalidateAuthoringResourceCache(rootPath);

        string nextHash = StorySourceAuthoringResourceProvider.ComputeStorySourceHash(rootPath);
        return StorySourcePacketResponse.Success(load.RelativePacketPath, load.Packet.StorySourceId, load.CurrentHash, nextHash, "故事输入包已批准给美术。");
    }

    internal static StorySourcePacketResponse SaveStorySourceCanonUpdate(string rootPath, StorySourceCanonUpdateRequest request, JsonSerializerOptions jsonOptions)
    {
        request ??= new StorySourceCanonUpdateRequest();
        StorySourceWriteGateResult gate = ValidateStorySourceWriteGate(rootPath, request.expectedHash, AuthoringResourceDiagnosticCodes.StorySourcePacketUpdateConflict);
        if (gate.Response != null)
            return gate.Response;

        string canonId = (request.canonId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(canonId))
            return StorySourcePacketResponse.InvalidUpdate(StorySourceAuthoringResourceProvider.CanonRelativePath, "必须提供 canonId。", canonId, gate.CurrentHash);

        StorySourceCanonDocument document = LoadStorySourceJsonFile<StorySourceCanonDocument>(rootPath, StorySourceAuthoringResourceProvider.CanonRelativePath, jsonOptions) ?? new StorySourceCanonDocument();
        document.Facts ??= new List<StorySourceCanonFact>();
        StorySourceCanonFact fact = document.Facts.FirstOrDefault(item => string.Equals(item != null ? item.CanonId : string.Empty, canonId, StringComparison.Ordinal));
        if (fact == null)
            return StorySourcePacketResponse.InvalidUpdate(StorySourceAuthoringResourceProvider.CanonRelativePath, "找不到世界观记忆：" + canonId, canonId, gate.CurrentHash);

        string type = NormalizeStoryCanonType(request.type);
        if (string.IsNullOrWhiteSpace(type))
            return StorySourcePacketResponse.InvalidUpdate(StorySourceAuthoringResourceProvider.CanonRelativePath, "不支持的世界观记忆类型：" + (request.type ?? string.Empty), canonId, gate.CurrentHash);
        string status = NormalizeStoryCanonStatus(request.status);
        if (string.IsNullOrWhiteSpace(status))
            return StorySourcePacketResponse.InvalidUpdate(StorySourceAuthoringResourceProvider.CanonRelativePath, "不支持的世界观记忆状态：" + (request.status ?? string.Empty), canonId, gate.CurrentHash);

        fact.Type = type;
        fact.Title = (request.title ?? string.Empty).Trim();
        fact.Summary = (request.summary ?? string.Empty).Trim();
        fact.Status = status;
        fact.Tags = NormalizeStringList(request.tags);
        fact.Relations = NormalizeStoryCanonRelations(request.relations);
        fact.UsedByPacketIds = NormalizeStringList(request.usedByPacketIds);
        fact.UsedByOutlineIds = NormalizeStringList(request.usedByOutlineIds);
        document.Updated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        string path = Path.Combine(rootPath, StorySourceAuthoringResourceProvider.CanonRelativePath);
        WriteJsonFileAtomic(path, document, jsonOptions);
        InvalidateAuthoringResourceCache(rootPath);
        string nextHash = StorySourceAuthoringResourceProvider.ComputeStorySourceHash(rootPath);
        return StorySourcePacketResponse.Success(StorySourceAuthoringResourceProvider.CanonRelativePath, canonId, gate.CurrentHash, nextHash, "世界观记忆已保存。");
    }

    internal static StorySourcePacketResponse SaveStorySourceOutlineUpdate(string rootPath, StorySourceOutlineUpdateRequest request, JsonSerializerOptions jsonOptions)
    {
        request ??= new StorySourceOutlineUpdateRequest();
        StorySourceWriteGateResult gate = ValidateStorySourceWriteGate(rootPath, request.expectedHash, AuthoringResourceDiagnosticCodes.StorySourcePacketUpdateConflict);
        if (gate.Response != null)
            return gate.Response;

        string outlineId = (request.outlineId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(outlineId))
            return StorySourcePacketResponse.InvalidUpdate(StorySourceAuthoringResourceProvider.OutlinesIndexRelativePath, "必须提供 outlineId。", outlineId, gate.CurrentHash);

        StorySourceOutlineDocument document = LoadStorySourceJsonFile<StorySourceOutlineDocument>(rootPath, StorySourceAuthoringResourceProvider.OutlinesIndexRelativePath, jsonOptions) ?? new StorySourceOutlineDocument();
        document.Outlines ??= new List<StorySourceOutlineEntry>();
        StorySourceOutlineEntry outline = document.Outlines.FirstOrDefault(item => string.Equals(item != null ? item.OutlineId : string.Empty, outlineId, StringComparison.Ordinal));
        if (outline == null)
            return StorySourcePacketResponse.InvalidUpdate(StorySourceAuthoringResourceProvider.OutlinesIndexRelativePath, "找不到故事大纲：" + outlineId, outlineId, gate.CurrentHash);

        string stage = NormalizeStoryOutlineStage(request.stage);
        if (string.IsNullOrWhiteSpace(stage))
            return StorySourcePacketResponse.InvalidUpdate(StorySourceAuthoringResourceProvider.OutlinesIndexRelativePath, "不支持的故事大纲阶段：" + (request.stage ?? string.Empty), outlineId, gate.CurrentHash);
        if (stage == "approved-for-packet")
            return StorySourcePacketResponse.InvalidUpdate(
                StorySourceAuthoringResourceProvider.OutlinesIndexRelativePath,
                "故事大纲不能通过保存草稿进入 approved-for-packet，请使用“批准给输入包”。",
                outlineId,
                gate.CurrentHash);

        outline.Title = (request.title ?? string.Empty).Trim();
        outline.Stage = stage;
        outline.OutlinePath = (request.outlinePath ?? string.Empty).Trim();
        outline.CanonRefs = NormalizeStringList(request.canonRefs);
        outline.Beats = NormalizeStoryOutlineBeats(request.beats);
        outline.Conflicts = NormalizeStringList(request.conflicts);
        outline.OpenQuestions = NormalizeStringList(request.openQuestions);
        outline.TargetConsumers = NormalizeStringList(request.targetConsumers);
        document.Updated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        string path = Path.Combine(rootPath, StorySourceAuthoringResourceProvider.OutlinesIndexRelativePath);
        WriteJsonFileAtomic(path, document, jsonOptions);
        InvalidateAuthoringResourceCache(rootPath);
        string nextHash = StorySourceAuthoringResourceProvider.ComputeStorySourceHash(rootPath);
        return StorySourcePacketResponse.Success(StorySourceAuthoringResourceProvider.OutlinesIndexRelativePath, outlineId, gate.CurrentHash, nextHash, "故事大纲已保存。");
    }

    internal static StorySourcePacketResponse SaveStorySourceOutlineApproveForPacket(string rootPath, StorySourceOutlineApproveRequest request, JsonSerializerOptions jsonOptions)
    {
        request ??= new StorySourceOutlineApproveRequest();
        StorySourceWriteGateResult gate = ValidateStorySourceWriteGate(rootPath, request.expectedHash, AuthoringResourceDiagnosticCodes.StorySourcePacketApproveConflict);
        if (gate.Response != null)
            return gate.Response;

        string outlineId = (request.outlineId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(outlineId))
            return StorySourcePacketResponse.InvalidApprove(StorySourceAuthoringResourceProvider.OutlinesIndexRelativePath, "必须提供 outlineId。", outlineId, gate.CurrentHash);

        StorySourceOutlineDocument document = LoadStorySourceJsonFile<StorySourceOutlineDocument>(rootPath, StorySourceAuthoringResourceProvider.OutlinesIndexRelativePath, jsonOptions) ?? new StorySourceOutlineDocument();
        document.Outlines ??= new List<StorySourceOutlineEntry>();
        StorySourceOutlineEntry outline = document.Outlines.FirstOrDefault(item => string.Equals(item != null ? item.OutlineId : string.Empty, outlineId, StringComparison.Ordinal));
        if (outline == null)
            return StorySourcePacketResponse.InvalidApprove(StorySourceAuthoringResourceProvider.OutlinesIndexRelativePath, "找不到故事大纲：" + outlineId, outlineId, gate.CurrentHash);

        List<AuthoringResourceDiagnostic> approvalDiagnostics = StorySourceAuthoringValidator.GetOutlineApprovalDiagnostics(rootPath, outlineId);
        if (StorySourceAuthoringValidator.HasBlockingDiagnostics(approvalDiagnostics))
            return StorySourcePacketResponse.InvalidApprove(
                StorySourceAuthoringResourceProvider.OutlinesIndexRelativePath,
                "批准给输入包前必须解决：" + FormatStorySourceDiagnosticMessages(approvalDiagnostics),
                outlineId,
                gate.CurrentHash,
                approvalDiagnostics);

        outline.Stage = "approved-for-packet";
        document.Updated = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        string path = Path.Combine(rootPath, StorySourceAuthoringResourceProvider.OutlinesIndexRelativePath);
        WriteJsonFileAtomic(path, document, jsonOptions);
        InvalidateAuthoringResourceCache(rootPath);
        string nextHash = StorySourceAuthoringResourceProvider.ComputeStorySourceHash(rootPath);
        return StorySourcePacketResponse.Success(StorySourceAuthoringResourceProvider.OutlinesIndexRelativePath, outlineId, gate.CurrentHash, nextHash, "故事大纲已批准给输入包。");
    }

    private static StorySourcePacketLoadResult LoadStorySourcePacketForWrite(
        string rootPath,
        string storySourceId,
        string expectedHash,
        JsonSerializerOptions jsonOptions,
        string conflictCode)
    {
        var result = new StorySourcePacketLoadResult();
        string indexPath = Path.Combine(rootPath, StorySourceAuthoringResourceProvider.IndexRelativePath);
        result.IndexPath = indexPath;
        result.RelativeIndexPath = ToProjectRelativePath(rootPath, indexPath);

        if (string.IsNullOrWhiteSpace(storySourceId))
        {
            result.Response = conflictCode == AuthoringResourceDiagnosticCodes.StorySourcePacketApproveConflict
                ? StorySourcePacketResponse.InvalidApprove(result.RelativeIndexPath, "必须提供 storySourceId。")
                : StorySourcePacketResponse.InvalidUpdate(result.RelativeIndexPath, "必须提供 storySourceId。");
            return result;
        }

        if (!File.Exists(indexPath))
        {
            result.Response = conflictCode == AuthoringResourceDiagnosticCodes.StorySourcePacketApproveConflict
                ? StorySourcePacketResponse.InvalidApprove(result.RelativeIndexPath, "StorySource packet index 不存在：" + StorySourceAuthoringResourceProvider.IndexRelativePath, storySourceId)
                : StorySourcePacketResponse.InvalidUpdate(result.RelativeIndexPath, "StorySource packet index 不存在：" + StorySourceAuthoringResourceProvider.IndexRelativePath, storySourceId);
            return result;
        }

        string currentHash = StorySourceAuthoringResourceProvider.ComputeStorySourceHash(rootPath);
        result.CurrentHash = currentHash;
        string normalizedExpected = CharacterPackageHashUtility.NormalizeSha256(expectedHash);
        if (string.IsNullOrWhiteSpace(normalizedExpected))
        {
            result.Response = conflictCode == AuthoringResourceDiagnosticCodes.StorySourcePacketApproveConflict
                ? StorySourcePacketResponse.InvalidApprove(result.RelativeIndexPath, "保存 StorySource 前必须提供 expectedHash。", storySourceId, currentHash)
                : StorySourcePacketResponse.InvalidUpdate(result.RelativeIndexPath, "保存 StorySource 前必须提供 expectedHash。", storySourceId, currentHash);
            return result;
        }

        if (!string.Equals(normalizedExpected, currentHash, StringComparison.Ordinal))
        {
            result.Response = StorySourcePacketResponse.Conflict(result.RelativeIndexPath, storySourceId, normalizedExpected, currentHash, conflictCode);
            return result;
        }

        try
        {
            result.Index = JsonSerializer.Deserialize<StorySourceIndexDocument>(File.ReadAllText(indexPath), jsonOptions) ?? new StorySourceIndexDocument();
        }
        catch (JsonException ex)
        {
            result.Response = conflictCode == AuthoringResourceDiagnosticCodes.StorySourcePacketApproveConflict
                ? StorySourcePacketResponse.InvalidApprove(result.RelativeIndexPath, "StorySource packet index JSON 无效：" + ex.Message, storySourceId, currentHash)
                : StorySourcePacketResponse.InvalidUpdate(result.RelativeIndexPath, "StorySource packet index JSON 无效：" + ex.Message, storySourceId, currentHash);
            return result;
        }

        result.Index.Packets ??= new List<StorySourcePacketIndexEntry>();
        result.IndexEntry = result.Index.Packets.FirstOrDefault(item => string.Equals(item != null ? item.StorySourceId : string.Empty, storySourceId, StringComparison.Ordinal));
        if (result.IndexEntry == null)
        {
            result.Response = conflictCode == AuthoringResourceDiagnosticCodes.StorySourcePacketApproveConflict
                ? StorySourcePacketResponse.InvalidApprove(result.RelativeIndexPath, "找不到故事输入包：" + storySourceId, storySourceId, currentHash)
                : StorySourcePacketResponse.InvalidUpdate(result.RelativeIndexPath, "找不到故事输入包：" + storySourceId, storySourceId, currentHash);
            return result;
        }

        string packetPath = Path.Combine(rootPath, result.IndexEntry.PacketPath ?? string.Empty);
        result.PacketPath = packetPath;
        result.RelativePacketPath = ToProjectRelativePath(rootPath, packetPath);
        if (string.IsNullOrWhiteSpace(result.IndexEntry.PacketPath) || !File.Exists(packetPath))
        {
            result.Response = conflictCode == AuthoringResourceDiagnosticCodes.StorySourcePacketApproveConflict
                ? StorySourcePacketResponse.InvalidApprove(result.RelativeIndexPath, "故事输入包文件不存在：" + (result.IndexEntry.PacketPath ?? string.Empty), storySourceId, currentHash)
                : StorySourcePacketResponse.InvalidUpdate(result.RelativeIndexPath, "故事输入包文件不存在：" + (result.IndexEntry.PacketPath ?? string.Empty), storySourceId, currentHash);
            return result;
        }

        try
        {
            result.Packet = JsonSerializer.Deserialize<StorySourceInputPacketDocument>(File.ReadAllText(packetPath), jsonOptions) ?? new StorySourceInputPacketDocument();
        }
        catch (JsonException ex)
        {
            result.Response = conflictCode == AuthoringResourceDiagnosticCodes.StorySourcePacketApproveConflict
                ? StorySourcePacketResponse.InvalidApprove(result.RelativePacketPath, "故事输入包 JSON 无效：" + ex.Message, storySourceId, currentHash)
                : StorySourcePacketResponse.InvalidUpdate(result.RelativePacketPath, "故事输入包 JSON 无效：" + ex.Message, storySourceId, currentHash);
            return result;
        }

        if (!string.Equals(result.Packet.StorySourceId ?? string.Empty, storySourceId, StringComparison.Ordinal))
        {
            result.Response = conflictCode == AuthoringResourceDiagnosticCodes.StorySourcePacketApproveConflict
                ? StorySourcePacketResponse.InvalidApprove(result.RelativePacketPath, "请求 id 与 packet storySourceId 不一致。", storySourceId, currentHash)
                : StorySourcePacketResponse.InvalidUpdate(result.RelativePacketPath, "请求 id 与 packet storySourceId 不一致。", storySourceId, currentHash);
        }

        return result;
    }

    private static void ApplyStoryPacketUpdate(StorySourceInputPacketDocument packet, StorySourcePacketUpdateRequest request, string approvalStatus)
    {
        packet.Schema = string.IsNullOrWhiteSpace(packet.Schema) ? "wgame.story.input_packet.v1" : packet.Schema.Trim();
        packet.StorySourceId = (request.storySourceId ?? string.Empty).Trim();
        packet.Version = (request.version ?? string.Empty).Trim();
        packet.ApprovalStatus = approvalStatus;
        packet.TargetConsumers = NormalizeStringList(request.targetConsumers);
        packet.CanonRefs = NormalizeStringList(request.canonRefs);
        packet.OutlineRefs = NormalizeStringList(request.outlineRefs);
        packet.ReviewStatus = (request.reviewStatus ?? string.Empty).Trim();
        packet.ExportPath = (request.exportPath ?? string.Empty).Trim();
        packet.WorldPremise = (request.worldPremise ?? string.Empty).Trim();
        packet.CoreEvent = (request.coreEvent ?? string.Empty).Trim();
        packet.LocationFunction = (request.locationFunction ?? string.Empty).Trim();
        packet.CivilizationOrigin = (request.civilizationOrigin ?? string.Empty).Trim();
        packet.PlayerRole = (request.playerRole ?? string.Empty).Trim();
        packet.EnemyOrigins = NormalizeStringList(request.enemyOrigins);
        packet.PowerRules = NormalizeStringList(request.powerRules);
        packet.SymbolMeanings = NormalizeStringList(request.symbolMeanings);
        packet.ToneTags = NormalizeStringList(request.toneTags);
        packet.VisualDoNotUse = NormalizeStringList(request.visualDoNotUse);
    }

    private static void UpdateStoryPacketIndexEntry(StorySourcePacketIndexEntry entry, StorySourceInputPacketDocument packet)
    {
        entry.Version = packet.Version ?? string.Empty;
        entry.ApprovalStatus = packet.ApprovalStatus ?? string.Empty;
        entry.TargetConsumers = packet.TargetConsumers ?? new List<string>();
        entry.CanonRefs = packet.CanonRefs ?? new List<string>();
        entry.OutlineRefs = packet.OutlineRefs ?? new List<string>();
        entry.ReviewStatus = packet.ReviewStatus ?? string.Empty;
        entry.ExportPath = packet.ExportPath ?? string.Empty;
    }

    private static string FormatStorySourceDiagnosticMessages(IEnumerable<AuthoringResourceDiagnostic> diagnostics)
    {
        var messages = new List<string>();
        foreach (AuthoringResourceDiagnostic diagnostic in diagnostics ?? Enumerable.Empty<AuthoringResourceDiagnostic>())
        {
            if (diagnostic == null)
                continue;
            if (diagnostic.Severity != CharacterAuthoringValidationSeverity.Error &&
                diagnostic.Severity != CharacterAuthoringValidationSeverity.Warning)
                continue;
            string message = diagnostic.Message ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message))
                continue;
            if (!messages.Contains(message))
                messages.Add(message);
            if (messages.Count >= 5)
                break;
        }

        return messages.Count > 0 ? string.Join("; ", messages) : "StorySource 校验未通过。";
    }

    private static string NormalizeStoryApprovalStatus(string status)
    {
        string value = (status ?? string.Empty).Trim().ToLowerInvariant();
        return value is "draft" or "approved-for-art" or "superseded" or "rejected"
            ? value
            : string.Empty;
    }

    private static StorySourceWriteGateResult ValidateStorySourceWriteGate(string rootPath, string expectedHash, string conflictCode)
    {
        var result = new StorySourceWriteGateResult
        {
            CurrentHash = StorySourceAuthoringResourceProvider.ComputeStorySourceHash(rootPath)
        };
        string normalizedExpected = CharacterPackageHashUtility.NormalizeSha256(expectedHash);
        if (string.IsNullOrWhiteSpace(normalizedExpected))
        {
            result.Response = StorySourcePacketResponse.InvalidUpdate("StorySource/Projects/WGame", "保存 StorySource 前必须提供 expectedHash。", string.Empty, result.CurrentHash);
            return result;
        }

        if (!string.Equals(normalizedExpected, result.CurrentHash, StringComparison.Ordinal))
        {
            result.Response = StorySourcePacketResponse.Conflict("StorySource/Projects/WGame", string.Empty, normalizedExpected, result.CurrentHash, conflictCode);
            return result;
        }

        return result;
    }

    private static T LoadStorySourceJsonFile<T>(string rootPath, string relativePath, JsonSerializerOptions jsonOptions) where T : class
    {
        string path = Path.Combine(rootPath, relativePath);
        if (!File.Exists(path))
            return null;
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), jsonOptions);
    }

    private static string NormalizeStoryCanonType(string type)
    {
        string value = (type ?? string.Empty).Trim().ToLowerInvariant();
        return value is "world" or "location" or "faction" or "character_archetype" or "enemy_origin" or "power_rule" or "symbol" or "tone" or "forbidden_content"
            ? value
            : string.Empty;
    }

    private static string NormalizeStoryCanonStatus(string status)
    {
        string value = (status ?? string.Empty).Trim().ToLowerInvariant();
        return value is "draft" or "canon" or "deprecated" or "rejected"
            ? value
            : string.Empty;
    }

    private static string NormalizeStoryOutlineStage(string stage)
    {
        string value = (stage ?? string.Empty).Trim().ToLowerInvariant();
        return value is "draft" or "review" or "approved-for-packet" or "superseded"
            ? value
            : string.Empty;
    }

    private static List<StorySourceCanonRelation> NormalizeStoryCanonRelations(List<StorySourceCanonRelation> relations)
    {
        var result = new List<StorySourceCanonRelation>();
        for (int i = 0; relations != null && i < relations.Count; i++)
        {
            StorySourceCanonRelation relation = relations[i];
            if (relation == null)
                continue;
            string name = (relation.Relation ?? string.Empty).Trim();
            string target = (relation.TargetId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(target))
                continue;
            result.Add(new StorySourceCanonRelation { Relation = name, TargetId = target });
        }

        return result;
    }

    private static List<StorySourceOutlineBeat> NormalizeStoryOutlineBeats(List<StorySourceOutlineBeat> beats)
    {
        var result = new List<StorySourceOutlineBeat>();
        for (int i = 0; beats != null && i < beats.Count; i++)
        {
            StorySourceOutlineBeat beat = beats[i];
            if (beat == null)
                continue;
            string slug = (beat.Slug ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(slug))
                continue;
            result.Add(new StorySourceOutlineBeat
            {
                Slug = slug,
                Title = (beat.Title ?? string.Empty).Trim(),
                Summary = (beat.Summary ?? string.Empty).Trim()
            });
        }

        return result;
    }

    private static List<string> NormalizeStringList(List<string> values)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (values == null)
            return result;

        for (int i = 0; i < values.Count; i++)
        {
            string value = (values[i] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value) || seen.Contains(value))
                continue;
            seen.Add(value);
            result.Add(value);
        }

        return result;
    }

    internal sealed class StorySourcePacketUpdateRequest
    {
        public string storySourceId { get; set; } = string.Empty;
        public string version { get; set; } = string.Empty;
        public string approvalStatus { get; set; } = string.Empty;
        public List<string> targetConsumers { get; set; } = new();
        public List<string> canonRefs { get; set; } = new();
        public List<string> outlineRefs { get; set; } = new();
        public string reviewStatus { get; set; } = string.Empty;
        public string exportPath { get; set; } = string.Empty;
        public string worldPremise { get; set; } = string.Empty;
        public string coreEvent { get; set; } = string.Empty;
        public string locationFunction { get; set; } = string.Empty;
        public string civilizationOrigin { get; set; } = string.Empty;
        public string playerRole { get; set; } = string.Empty;
        public List<string> enemyOrigins { get; set; } = new();
        public List<string> powerRules { get; set; } = new();
        public List<string> symbolMeanings { get; set; } = new();
        public List<string> toneTags { get; set; } = new();
        public List<string> visualDoNotUse { get; set; } = new();
        public string expectedHash { get; set; } = string.Empty;
    }

    internal sealed class StorySourcePacketApproveRequest
    {
        public string storySourceId { get; set; } = string.Empty;
        public string reviewStatus { get; set; } = string.Empty;
        public string expectedHash { get; set; } = string.Empty;
    }

    internal sealed class StorySourceCanonUpdateRequest
    {
        public string canonId { get; set; } = string.Empty;
        public string type { get; set; } = string.Empty;
        public string title { get; set; } = string.Empty;
        public string summary { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public List<string> tags { get; set; } = new();
        public List<StorySourceCanonRelation> relations { get; set; } = new();
        public List<string> usedByPacketIds { get; set; } = new();
        public List<string> usedByOutlineIds { get; set; } = new();
        public string expectedHash { get; set; } = string.Empty;
    }

    internal sealed class StorySourceOutlineUpdateRequest
    {
        public string outlineId { get; set; } = string.Empty;
        public string title { get; set; } = string.Empty;
        public string stage { get; set; } = string.Empty;
        public string outlinePath { get; set; } = string.Empty;
        public List<string> canonRefs { get; set; } = new();
        public List<StorySourceOutlineBeat> beats { get; set; } = new();
        public List<string> conflicts { get; set; } = new();
        public List<string> openQuestions { get; set; } = new();
        public List<string> targetConsumers { get; set; } = new();
        public string expectedHash { get; set; } = string.Empty;
    }

    internal sealed class StorySourceOutlineApproveRequest
    {
        public string outlineId { get; set; } = string.Empty;
        public string expectedHash { get; set; } = string.Empty;
    }

    internal sealed class StorySourcePacketResponse
    {
        public bool success { get; set; }
        public string code { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public int httpStatus { get; set; }
        public string packetPath { get; set; } = string.Empty;
        public string storySourceId { get; set; } = string.Empty;
        public string expectedHash { get; set; } = string.Empty;
        public string previousHash { get; set; } = string.Empty;
        public string currentHash { get; set; } = string.Empty;
        public List<AuthoringResourceDiagnostic> diagnostics { get; set; } = new();

        public static StorySourcePacketResponse Success(string packetPath, string storySourceId, string previousHash, string currentHash, string message)
        {
            return new StorySourcePacketResponse
            {
                success = true,
                httpStatus = 200,
                packetPath = packetPath ?? string.Empty,
                storySourceId = storySourceId ?? string.Empty,
                previousHash = previousHash ?? string.Empty,
                currentHash = currentHash ?? string.Empty,
                message = message ?? string.Empty
            };
        }

        public static StorySourcePacketResponse InvalidUpdate(string packetPath, string message, string storySourceId = "", string currentHash = "")
        {
            return Invalid(packetPath, message, storySourceId, currentHash, AuthoringResourceDiagnosticCodes.StorySourcePacketUpdateInvalid);
        }

        public static StorySourcePacketResponse InvalidUpdate(string packetPath, string message, string storySourceId, string currentHash, List<AuthoringResourceDiagnostic> diagnostics)
        {
            return Invalid(packetPath, message, storySourceId, currentHash, AuthoringResourceDiagnosticCodes.StorySourcePacketUpdateInvalid, diagnostics);
        }

        public static StorySourcePacketResponse InvalidApprove(string packetPath, string message, string storySourceId = "", string currentHash = "")
        {
            return Invalid(packetPath, message, storySourceId, currentHash, AuthoringResourceDiagnosticCodes.StorySourcePacketApproveInvalid);
        }

        public static StorySourcePacketResponse InvalidApprove(string packetPath, string message, string storySourceId, string currentHash, List<AuthoringResourceDiagnostic> diagnostics)
        {
            return Invalid(packetPath, message, storySourceId, currentHash, AuthoringResourceDiagnosticCodes.StorySourcePacketApproveInvalid, diagnostics);
        }

        public static StorySourcePacketResponse Conflict(string packetPath, string storySourceId, string expectedHash, string currentHash, string code)
        {
            return new StorySourcePacketResponse
            {
                success = false,
                code = code ?? AuthoringResourceDiagnosticCodes.StorySourcePacketUpdateConflict,
                httpStatus = 409,
                packetPath = packetPath ?? string.Empty,
                storySourceId = storySourceId ?? string.Empty,
                expectedHash = expectedHash ?? string.Empty,
                currentHash = currentHash ?? string.Empty,
                message = "StorySource 已在本页加载后发生变化。请刷新后再保存。"
            };
        }

        private static StorySourcePacketResponse Invalid(string packetPath, string message, string storySourceId, string currentHash, string code, List<AuthoringResourceDiagnostic> diagnostics = null)
        {
            return new StorySourcePacketResponse
            {
                success = false,
                code = code ?? AuthoringResourceDiagnosticCodes.StorySourcePacketUpdateInvalid,
                httpStatus = 400,
                packetPath = packetPath ?? string.Empty,
                storySourceId = storySourceId ?? string.Empty,
                currentHash = currentHash ?? string.Empty,
                message = message ?? string.Empty,
                diagnostics = diagnostics ?? new List<AuthoringResourceDiagnostic>()
            };
        }
    }

    internal sealed class StorySourcePacketLoadResult
    {
        public string IndexPath { get; set; } = string.Empty;
        public string PacketPath { get; set; } = string.Empty;
        public string RelativeIndexPath { get; set; } = string.Empty;
        public string RelativePacketPath { get; set; } = string.Empty;
        public string CurrentHash { get; set; } = string.Empty;
        public StorySourcePacketResponse Response { get; set; }
        public StorySourceIndexDocument Index { get; set; } = new();
        public StorySourcePacketIndexEntry IndexEntry { get; set; }
        public StorySourceInputPacketDocument Packet { get; set; } = new();
    }

    internal sealed class StorySourceWriteGateResult
    {
        public string CurrentHash { get; set; } = string.Empty;
        public StorySourcePacketResponse Response { get; set; }
    }

    internal sealed class StorySourceIndexDocument
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("projectId")]
        public string ProjectId { get; set; } = string.Empty;

        [JsonPropertyName("updated")]
        public string Updated { get; set; } = string.Empty;

        [JsonPropertyName("packets")]
        public List<StorySourcePacketIndexEntry> Packets { get; set; } = new();
    }

    internal sealed class StorySourcePacketIndexEntry
    {
        [JsonPropertyName("storySourceId")]
        public string StorySourceId { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("approvalStatus")]
        public string ApprovalStatus { get; set; } = string.Empty;

        [JsonPropertyName("targetConsumers")]
        public List<string> TargetConsumers { get; set; } = new();

        [JsonPropertyName("canonRefs")]
        public List<string> CanonRefs { get; set; } = new();

        [JsonPropertyName("outlineRefs")]
        public List<string> OutlineRefs { get; set; } = new();

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

    internal sealed class StorySourceInputPacketDocument
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
        public List<string> TargetConsumers { get; set; } = new();

        [JsonPropertyName("canonRefs")]
        public List<string> CanonRefs { get; set; } = new();

        [JsonPropertyName("outlineRefs")]
        public List<string> OutlineRefs { get; set; } = new();

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
        public List<string> EnemyOrigins { get; set; } = new();

        [JsonPropertyName("powerRules")]
        public List<string> PowerRules { get; set; } = new();

        [JsonPropertyName("symbolMeanings")]
        public List<string> SymbolMeanings { get; set; } = new();

        [JsonPropertyName("toneTags")]
        public List<string> ToneTags { get; set; } = new();

        [JsonPropertyName("visualDoNotUse")]
        public List<string> VisualDoNotUse { get; set; } = new();

        [JsonPropertyName("changeLog")]
        public List<StorySourcePacketChangeLogEntry> ChangeLog { get; set; } = new();
    }

    internal sealed class StorySourceCanonDocument
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("projectId")]
        public string ProjectId { get; set; } = string.Empty;

        [JsonPropertyName("updated")]
        public string Updated { get; set; } = string.Empty;

        [JsonPropertyName("facts")]
        public List<StorySourceCanonFact> Facts { get; set; } = new();
    }

    internal sealed class StorySourceCanonFact
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
        public List<string> Tags { get; set; } = new();

        [JsonPropertyName("relations")]
        public List<StorySourceCanonRelation> Relations { get; set; } = new();

        [JsonPropertyName("usedByPacketIds")]
        public List<string> UsedByPacketIds { get; set; } = new();

        [JsonPropertyName("usedByOutlineIds")]
        public List<string> UsedByOutlineIds { get; set; } = new();
    }

    internal sealed class StorySourceCanonRelation
    {
        [JsonPropertyName("relation")]
        public string Relation { get; set; } = string.Empty;

        [JsonPropertyName("targetId")]
        public string TargetId { get; set; } = string.Empty;
    }

    internal sealed class StorySourceOutlineDocument
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("projectId")]
        public string ProjectId { get; set; } = string.Empty;

        [JsonPropertyName("updated")]
        public string Updated { get; set; } = string.Empty;

        [JsonPropertyName("outlines")]
        public List<StorySourceOutlineEntry> Outlines { get; set; } = new();
    }

    internal sealed class StorySourceOutlineEntry
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
        public List<string> CanonRefs { get; set; } = new();

        [JsonPropertyName("beats")]
        public List<StorySourceOutlineBeat> Beats { get; set; } = new();

        [JsonPropertyName("conflicts")]
        public List<string> Conflicts { get; set; } = new();

        [JsonPropertyName("openQuestions")]
        public List<string> OpenQuestions { get; set; } = new();

        [JsonPropertyName("targetConsumers")]
        public List<string> TargetConsumers { get; set; } = new();
    }

    internal sealed class StorySourceOutlineBeat
    {
        [JsonPropertyName("slug")]
        public string Slug { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;
    }

    internal sealed class StorySourcePacketChangeLogEntry
    {
        [JsonPropertyName("date")]
        public string Date { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;
    }
}
