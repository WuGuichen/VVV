using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MxFramework.Authoring
{
    public sealed class StorySourceAuthoringResourceProvider : IAuthoringResourceProvider
    {
        public const string IndexRelativePath = "StorySource/Projects/WGame/story_packets.json";
        public const string PacketsRootRelativePath = "StorySource/Projects/WGame/Packets";
        public const string CanonRelativePath = "StorySource/Projects/WGame/Canon/canon_facts.json";
        public const string OutlinesIndexRelativePath = "StorySource/Projects/WGame/Outlines/story_outlines.json";
        public const string ArtTasksRelativePath = "ArtSource/Projects/WGame/art_tasks.json";

        private const string PacketSchemaId = "wgame.story.input_packet.v1";

        private static readonly string[] RequiredStringFields =
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

        private static readonly string[] RequiredArrayFields =
        {
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

        public string ProviderId
        {
            get { return AuthoringResourceProviderIds.StorySource; }
        }

        public AuthoringResourceProviderDescriptor Describe(AuthoringResourceProviderContext context)
        {
            bool available = File.Exists(ResolveProjectPath(context, IndexRelativePath)) ||
                File.Exists(ResolveProjectPath(context, CanonRelativePath)) ||
                File.Exists(ResolveProjectPath(context, OutlinesIndexRelativePath));
            return new AuthoringResourceProviderDescriptor
            {
                ProviderId = ProviderId,
                DisplayName = "StorySource",
                SourceKind = AuthoringResourceSourceKind.ExternalFile,
                Available = available,
                Status = available ? "Ready" : "Unavailable",
                DiagnosticCode = available ? string.Empty : AuthoringResourceDiagnosticCodes.ProviderUnavailable,
                Message = available ? string.Empty : "StorySource project files are not available."
            };
        }

        public AuthoringResourceCollection BuildResourceCollection(AuthoringResourceProviderContext context)
        {
            var collection = new AuthoringResourceCollection
            {
                ScopeId = context != null && !string.IsNullOrWhiteSpace(context.ScopeId)
                    ? context.ScopeId
                    : "storySource"
            };

            collection.Providers.Add(Describe(context));
            AddCollectionMetadata(collection, context);

            StorySourceIndexDocument packetIndex = LoadJson<StorySourceIndexDocument>(
                context,
                collection,
                IndexRelativePath,
                AuthoringResourceDiagnosticCodes.StorySourcePacketsMissing,
                "StorySource packet index is not available: " + IndexRelativePath,
                required: true) ?? new StorySourceIndexDocument();
            StoryCanonDocument canonDocument = LoadJson<StoryCanonDocument>(
                context,
                collection,
                CanonRelativePath,
                AuthoringResourceDiagnosticCodes.StorySourceJsonInvalid,
                "StorySource canon fact index is not available: " + CanonRelativePath,
                required: false) ?? new StoryCanonDocument();
            StoryOutlineDocument outlineDocument = LoadJson<StoryOutlineDocument>(
                context,
                collection,
                OutlinesIndexRelativePath,
                AuthoringResourceDiagnosticCodes.StorySourceJsonInvalid,
                "StorySource outline index is not available: " + OutlinesIndexRelativePath,
                required: false) ?? new StoryOutlineDocument();

            packetIndex.Packets ??= new List<StoryPacketIndexEntry>();
            canonDocument.Facts ??= new List<StoryCanonFact>();
            outlineDocument.Outlines ??= new List<StoryOutlineEntry>();
            StorySourceValidationResult validation = StorySourceAuthoringValidator.ValidateProject(context != null ? context.ProjectRootPath : string.Empty);

            var canonById = new Dictionary<string, StoryCanonFact>(StringComparer.Ordinal);
            var outlineById = new Dictionary<string, StoryOutlineEntry>(StringComparer.Ordinal);
            var canonSnapshots = new List<StoryCanonSnapshot>();
            var outlineSnapshots = new List<StoryOutlineSnapshot>();
            var packetSnapshots = new List<StoryPacketSnapshot>();

            BuildCanonItems(context, collection, canonDocument.Facts, canonById, canonSnapshots, validation);
            BuildOutlineItems(context, collection, outlineDocument.Outlines, canonById, outlineById, outlineSnapshots, validation);
            BuildPacketItems(context, collection, packetIndex.Packets, canonById, outlineById, packetSnapshots, validation);

            collection.Metadata["storySourceProjectId"] = FirstNonEmpty(packetIndex.ProjectId, canonDocument.ProjectId, outlineDocument.ProjectId);
            collection.Metadata["storySourceUpdated"] = FirstNonEmpty(packetIndex.Updated, canonDocument.Updated, outlineDocument.Updated);
            collection.Metadata["storySourcePacketCount"] = packetSnapshots.Count.ToString(CultureInfo.InvariantCulture);
            collection.Metadata["storySourceApprovedForArtCount"] = CountApprovedForArt(packetSnapshots).ToString(CultureInfo.InvariantCulture);
            collection.Metadata["storySourceCanonFactCount"] = canonSnapshots.Count.ToString(CultureInfo.InvariantCulture);
            collection.Metadata["storySourceCanonApprovedCount"] = CountCanon(canonSnapshots).ToString(CultureInfo.InvariantCulture);
            collection.Metadata["storySourceOutlineCount"] = outlineSnapshots.Count.ToString(CultureInfo.InvariantCulture);
            collection.Metadata["storySourceOutlineApprovedCount"] = CountApprovedOutlines(outlineSnapshots).ToString(CultureInfo.InvariantCulture);
            AddSnapshotMetadata(collection, packetIndex, canonDocument, outlineDocument, packetSnapshots, canonSnapshots, outlineSnapshots, context);
            return collection;
        }

        public static string ComputeStorySourceHash(string projectRootPath)
        {
            if (string.IsNullOrWhiteSpace(projectRootPath))
                return string.Empty;

            var builder = new StringBuilder();
            AppendProjectFileHash(projectRootPath, builder, IndexRelativePath);
            AppendProjectFileHash(projectRootPath, builder, CanonRelativePath);
            AppendProjectFileHash(projectRootPath, builder, OutlinesIndexRelativePath);

            StorySourceIndexDocument packetIndex = ReadJsonSilently<StorySourceIndexDocument>(projectRootPath, IndexRelativePath);
            if (packetIndex != null && packetIndex.Packets != null)
            {
                for (int i = 0; i < packetIndex.Packets.Count; i++)
                {
                    string packetPath = packetIndex.Packets[i] != null ? packetIndex.Packets[i].PacketPath ?? string.Empty : string.Empty;
                    if (!string.IsNullOrWhiteSpace(packetPath))
                        AppendProjectFileHash(projectRootPath, builder, packetPath);
                }
            }

            StoryOutlineDocument outlineIndex = ReadJsonSilently<StoryOutlineDocument>(projectRootPath, OutlinesIndexRelativePath);
            if (outlineIndex != null && outlineIndex.Outlines != null)
            {
                for (int i = 0; i < outlineIndex.Outlines.Count; i++)
                {
                    string outlinePath = outlineIndex.Outlines[i] != null ? outlineIndex.Outlines[i].OutlinePath ?? string.Empty : string.Empty;
                    if (!string.IsNullOrWhiteSpace(outlinePath))
                        AppendProjectFileHash(projectRootPath, builder, outlinePath);
                }
            }

            return ComputeStringSha256(builder.ToString());
        }

        private static T LoadJson<T>(
            AuthoringResourceProviderContext context,
            AuthoringResourceCollection collection,
            string relativePath,
            string missingCode,
            string missingMessage,
            bool required) where T : class
        {
            if (context == null || string.IsNullOrWhiteSpace(context.ProjectRootPath))
                return null;

            string fullPath = ResolveProjectPath(context, relativePath);
            if (!File.Exists(fullPath))
            {
                if (required)
                {
                    collection.Diagnostics.Add(CreateDiagnostic(
                        CharacterAuthoringValidationSeverity.Warning,
                        missingCode,
                        string.Empty,
                        missingMessage,
                        "Create the StorySource file, then refresh Resource Manager.",
                        relativePath));
                }
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(fullPath), JsonOptions);
            }
            catch (JsonException ex)
            {
                collection.Diagnostics.Add(CreateDiagnostic(
                    CharacterAuthoringValidationSeverity.Error,
                    AuthoringResourceDiagnosticCodes.StorySourceJsonInvalid,
                    string.Empty,
                    "StorySource JSON is invalid: " + ex.Message,
                    "Fix the JSON before Resource Manager can validate StorySource.",
                    relativePath));
                return null;
            }
        }

        private static void BuildCanonItems(
            AuthoringResourceProviderContext context,
            AuthoringResourceCollection collection,
            List<StoryCanonFact> facts,
            Dictionary<string, StoryCanonFact> canonById,
            List<StoryCanonSnapshot> snapshots,
            StorySourceValidationResult validation)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < facts.Count; i++)
            {
                StoryCanonFact fact = facts[i];
                if (fact == null)
                    continue;

                string canonId = FirstNonEmpty(fact.CanonId, "story.canon." + i.ToString(CultureInfo.InvariantCulture));
                fact.CanonId = canonId;
                if (!canonById.ContainsKey(canonId))
                    canonById.Add(canonId, fact);

                AuthoringResourceItem item = CreateCanonItem(context, fact);
                seen.Add(canonId);
                AddValidationDiagnostics(item, validation, StorySourceAuthoringValidator.CanonConfigKind, canonId);

                collection.Items.Add(item);
                snapshots.Add(CreateCanonSnapshot(fact, item));
            }
        }

        private static void BuildOutlineItems(
            AuthoringResourceProviderContext context,
            AuthoringResourceCollection collection,
            List<StoryOutlineEntry> outlines,
            Dictionary<string, StoryCanonFact> canonById,
            Dictionary<string, StoryOutlineEntry> outlineById,
            List<StoryOutlineSnapshot> snapshots,
            StorySourceValidationResult validation)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < outlines.Count; i++)
            {
                StoryOutlineEntry outline = outlines[i];
                if (outline == null)
                    continue;

                string outlineId = FirstNonEmpty(outline.OutlineId, "story.outline." + i.ToString(CultureInfo.InvariantCulture));
                outline.OutlineId = outlineId;
                if (!outlineById.ContainsKey(outlineId))
                    outlineById.Add(outlineId, outline);

                string outlinePath = outline.OutlinePath ?? string.Empty;
                bool outlineExists = !string.IsNullOrWhiteSpace(outlinePath) && context != null && File.Exists(ResolveProjectPath(context, outlinePath));
                AuthoringResourceItem item = CreateOutlineItem(context, outline, outlineExists);
                seen.Add(outlineId);
                AddValidationDiagnostics(item, validation, StorySourceAuthoringValidator.OutlineConfigKind, outlineId);

                collection.Items.Add(item);
                snapshots.Add(CreateOutlineSnapshot(outline, item, outlineExists));
            }
        }

        private static void BuildPacketItems(
            AuthoringResourceProviderContext context,
            AuthoringResourceCollection collection,
            List<StoryPacketIndexEntry> entries,
            Dictionary<string, StoryCanonFact> canonById,
            Dictionary<string, StoryOutlineEntry> outlineById,
            List<StoryPacketSnapshot> snapshots,
            StorySourceValidationResult validation)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                StoryPacketIndexEntry entry = entries[i];
                if (entry == null)
                    continue;

                string packetId = FirstNonEmpty(entry.StorySourceId, "story.packet." + i.ToString(CultureInfo.InvariantCulture));
                string packetPath = FirstNonEmpty(entry.PacketPath, BuildDefaultPacketPath(packetId));
                string fullPacketPath = ResolveProjectPath(context, packetPath);
                StoryInputPacketDocument packet = null;
                bool packetExists = File.Exists(fullPacketPath);

                if (packetExists)
                {
                    try
                    {
                        packet = JsonSerializer.Deserialize<StoryInputPacketDocument>(File.ReadAllText(fullPacketPath), JsonOptions) ?? new StoryInputPacketDocument();
                    }
                    catch (JsonException ex)
                    {
                        collection.Diagnostics.Add(CreateDiagnostic(
                            CharacterAuthoringValidationSeverity.Error,
                            AuthoringResourceDiagnosticCodes.StorySourceJsonInvalid,
                            packetId,
                            "StorySource packet JSON is invalid: " + ex.Message,
                            "Fix the packet JSON before Resource Manager can validate or approve it.",
                            packetPath));
                    }
                }
                else
                {
                    collection.Diagnostics.Add(CreateDiagnostic(
                        CharacterAuthoringValidationSeverity.Error,
                        AuthoringResourceDiagnosticCodes.StorySourceJsonInvalid,
                        packetId,
                        "StorySource packet file is missing: " + packetPath,
                        "Create the packet file or update story_packets.json to the correct path.",
                        "packetPath"));
                }

                packet ??= CreatePacketFallback(entry);
                ApplyIndexFallback(entry, packet);

                AuthoringResourceItem item = CreatePacketItem(context, entry, packet, packetId, packetPath, packetExists);
                string diagnosticId = FirstNonEmpty(packet.StorySourceId, packetId);
                ids.Add(diagnosticId);
                AddValidationDiagnostics(item, validation, StorySourceAuthoringValidator.PacketConfigKind, diagnosticId);
                collection.Items.Add(item);
                snapshots.Add(CreatePacketSnapshot(entry, packet, item, packetPath, packetExists));
            }
        }

        private static AuthoringResourceItem CreateCanonItem(AuthoringResourceProviderContext context, StoryCanonFact fact)
        {
            string stableId = "story.canon." + AuthoringResourceProviderUtilities.SanitizeStableSegment(fact.CanonId);
            var item = CreateBaseItem(context, stableId, fact.CanonId, fact.CanonId, CanonRelativePath, "story-canon-fact", "canon-memory", true);
            item.Tags.Add("canon");
            AddTag(item.Tags, fact.Type);
            AddTag(item.Tags, fact.Status);
            item.Metadata["canonId"] = fact.CanonId ?? string.Empty;
            item.Metadata["type"] = fact.Type ?? string.Empty;
            item.Metadata["title"] = fact.Title ?? string.Empty;
            item.Metadata["summary"] = fact.Summary ?? string.Empty;
            item.Metadata["status"] = fact.Status ?? string.Empty;
            item.Metadata["tags"] = string.Join(",", fact.Tags ?? new List<string>());
            item.Metadata["sourcePath"] = CanonRelativePath;
            return item;
        }

        private static AuthoringResourceItem CreateOutlineItem(AuthoringResourceProviderContext context, StoryOutlineEntry outline, bool outlineExists)
        {
            string stableId = "story.outline." + AuthoringResourceProviderUtilities.SanitizeStableSegment(outline.OutlineId);
            var item = CreateBaseItem(context, stableId, outline.OutlineId, outline.Title, outline.OutlinePath, "story-outline", "outline-source", outlineExists);
            item.Tags.Add("outline");
            AddTag(item.Tags, outline.Stage);
            item.Metadata["outlineId"] = outline.OutlineId ?? string.Empty;
            item.Metadata["title"] = outline.Title ?? string.Empty;
            item.Metadata["stage"] = outline.Stage ?? string.Empty;
            item.Metadata["outlinePath"] = outline.OutlinePath ?? string.Empty;
            item.Metadata["canonRefs"] = string.Join(",", outline.CanonRefs ?? new List<string>());
            item.Metadata["targetConsumers"] = string.Join(",", outline.TargetConsumers ?? new List<string>());
            return item;
        }

        private static AuthoringResourceItem CreatePacketItem(
            AuthoringResourceProviderContext context,
            StoryPacketIndexEntry entry,
            StoryInputPacketDocument packet,
            string packetId,
            string packetPath,
            bool packetExists)
        {
            string stableId = "story.source." + AuthoringResourceProviderUtilities.SanitizeStableSegment(packetId);
            var item = CreateBaseItem(context, stableId, packetId, packetId, packetPath, "story-packet", "packet-source", packetExists);
            item.Tags.Add("story-packet");
            AddTag(item.Tags, packet.ApprovalStatus);
            AddTag(item.Tags, packet.ReviewStatus);
            item.Metadata["storySourceId"] = packet.StorySourceId ?? string.Empty;
            item.Metadata["version"] = packet.Version ?? string.Empty;
            item.Metadata["approvalStatus"] = packet.ApprovalStatus ?? string.Empty;
            item.Metadata["targetConsumers"] = string.Join(",", packet.TargetConsumers ?? new List<string>());
            item.Metadata["canonRefs"] = string.Join(",", packet.CanonRefs ?? new List<string>());
            item.Metadata["outlineRefs"] = string.Join(",", packet.OutlineRefs ?? new List<string>());
            item.Metadata["reviewStatus"] = FirstNonEmpty(packet.ReviewStatus, entry.ReviewStatus);
            item.Metadata["packetPath"] = packetPath ?? string.Empty;
            item.Metadata["exportPath"] = FirstNonEmpty(packet.ExportPath, entry.ExportPath);
            item.Metadata["artSourceTaskId"] = entry.ArtSourceTaskId ?? string.Empty;
            item.Metadata["schema"] = packet.Schema ?? string.Empty;
            item.Metadata["packetExists"] = packetExists ? "true" : "false";
            item.Metadata["missingRequiredFields"] = string.Join(",", GetMissingRequiredFields(packet));
            return item;
        }

        private static AuthoringResourceItem CreateBaseItem(
            AuthoringResourceProviderContext context,
            string stableId,
            string identity,
            string displayName,
            string sourcePath,
            string kind,
            string usage,
            bool sourceExists)
        {
            string effectiveDisplayName = FirstNonEmpty(displayName, identity, stableId);
            var item = new AuthoringResourceItem
            {
                ResourceId = AuthoringResourceProviderUtilities.BuildResourceId(AuthoringResourceProviderIds.StorySource, stableId, identity),
                StableId = stableId,
                DisplayName = effectiveDisplayName,
                CanonicalName = effectiveDisplayName,
                CanonicalIdentity = identity ?? string.Empty,
                TechnicalIdentity = sourcePath ?? string.Empty,
                Kind = kind,
                Usage = usage,
                SourceProviderId = AuthoringResourceProviderIds.StorySource,
                SourceKind = AuthoringResourceSourceKind.ExternalFile,
                BindingKind = AuthoringResourceBindingKind.ExternalSource,
                ImportStatus = sourceExists ? AuthoringResourceImportStatus.Clean : AuthoringResourceImportStatus.New,
                RuntimeAvailability = AuthoringResourceRuntimeAvailability.NotRuntimeLoadable,
                Tags = new List<string> { "story-source" }
            };

            item.Metadata["storySourceIndexPath"] = IndexRelativePath;
            item.Metadata["runtimeAvailability"] = item.RuntimeAvailability.ToString();
            item.Metadata["bindingKind"] = item.BindingKind.ToString();
            if (context != null)
                item.Metadata["projectRootPath"] = context.ProjectRootPath ?? string.Empty;

            item.ProviderBindings.Add(new AuthoringResourceProviderBinding
            {
                ProviderId = AuthoringResourceProviderIds.StorySource,
                BindingKind = AuthoringResourceBindingKind.ExternalSource,
                BindingKeyKind = AuthoringResourceBindingKeyKinds.ExternalSourcePath,
                DisplayValue = sourcePath ?? string.Empty,
                IsPrimary = true,
                ProviderResourceKey = identity ?? string.Empty,
                ExternalSourcePath = sourcePath ?? string.Empty,
                AssetType = kind,
                ProviderData = new Dictionary<string, string>(item.Metadata, StringComparer.Ordinal)
            });
            return item;
        }

        private static void ValidatePacket(
            AuthoringResourceProviderContext context,
            StoryPacketIndexEntry entry,
            StoryInputPacketDocument packet,
            AuthoringResourceItem item,
            HashSet<string> ids,
            Dictionary<string, StoryCanonFact> canonById,
            Dictionary<string, StoryOutlineEntry> outlineById)
        {
            if (packet == null || item == null)
                return;

            if (string.IsNullOrWhiteSpace(packet.Schema) || !string.Equals(packet.Schema, PacketSchemaId, StringComparison.Ordinal))
                AddItemDiagnostic(item, CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceJsonInvalid, "Story packet schema should be " + PacketSchemaId + ".", "Update the packet schema id before approval.", "schema");

            if (!string.IsNullOrWhiteSpace(packet.StorySourceId) && !ids.Add(packet.StorySourceId))
                AddItemDiagnostic(item, CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceVersionConflict, "Duplicate storySourceId appears in story_packets.json: " + packet.StorySourceId, "Keep one active packet per storySourceId/version or supersede the older entry.", "storySourceId");

            if (!string.Equals(entry.Version ?? string.Empty, packet.Version ?? string.Empty, StringComparison.Ordinal))
                AddItemDiagnostic(item, CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceVersionConflict, "Index version and packet version do not match.", "Update story_packets.json or the packet version so they match.", "version");

            List<string> missingFields = GetMissingRequiredFields(packet);
            for (int i = 0; i < missingFields.Count; i++)
                AddItemDiagnostic(item, CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceRequiredFieldMissing, "Story Input Packet is missing required field: " + missingFields[i], "Fill the required field before approving this packet for ArtSource.", missingFields[i]);

            bool derivationBlocked = false;
            if (packet.CanonRefs == null || packet.CanonRefs.Count == 0)
            {
                derivationBlocked = true;
                AddItemDiagnostic(item, CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceCanonRefMissing, "Story Input Packet must reference canon facts.", "Bind packet canonRefs to approved Canon memory before approval.", "canonRefs");
            }
            derivationBlocked |= ValidateCanonReferences(item, packet.CanonRefs, canonById, requireCanonStatus: true);
            derivationBlocked |= ValidateOutlineReferences(item, packet.OutlineRefs, outlineById);

            if (!EqualsAny(packet.ApprovalStatus, "approved-for-art"))
                AddItemDiagnostic(item, CharacterAuthoringValidationSeverity.Info, AuthoringResourceDiagnosticCodes.StorySourceNotApproved, "Story Input Packet is not approved for ArtSource production.", "Submit review and approve this packet before ArtSource produces production reference images.", "approvalStatus");

            if (EqualsAny(packet.ApprovalStatus, "superseded"))
                AddItemDiagnostic(item, CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceSuperseded, "Story Input Packet has been superseded.", "Use the newer packet version for new production work.", "approvalStatus");

            if (derivationBlocked)
                AddItemDiagnostic(item, CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourcePacketDerivationBlocked, "Packet cannot be approved because Canon or Outline derivation is not ready.", "Approve referenced Canon facts and Outlines before approving this packet.", "canonRefs");

            if (TargetsArtSource(packet) && !HasArtSourceTask(context, entry.ArtSourceTaskId))
                AddItemDiagnostic(item, CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceArtReferenceMissing, "Packet targets ArtSource but cannot find the referenced ArtSource task: " + FirstNonEmpty(entry.ArtSourceTaskId, "(empty)"), "Add or update artSourceTaskId in story_packets.json so ArtSource handoff can be traced.", "artSourceTaskId");
        }

        private static bool ValidateCanonReferences(AuthoringResourceItem item, List<string> refs, Dictionary<string, StoryCanonFact> canonById, bool requireCanonStatus)
        {
            bool blocked = false;
            List<string> values = NormalizeStringList(refs);
            for (int i = 0; i < values.Count; i++)
            {
                string canonId = values[i];
                if (!canonById.TryGetValue(canonId, out StoryCanonFact fact))
                {
                    blocked = true;
                    AddItemDiagnostic(item, CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceCanonRefMissing, "Referenced Canon fact is missing: " + canonId, "Create the Canon fact or remove the reference.", "canonRefs");
                    continue;
                }

                if (requireCanonStatus && !string.Equals(fact.Status ?? string.Empty, "canon", StringComparison.Ordinal))
                {
                    blocked = true;
                    AddItemDiagnostic(item, CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceCanonRefNotApproved, "Referenced Canon fact is not canon: " + canonId, "Set the referenced Canon fact to canon before packet approval.", "canonRefs");
                }
            }

            return blocked;
        }

        private static bool ValidateOutlineReferences(AuthoringResourceItem item, List<string> refs, Dictionary<string, StoryOutlineEntry> outlineById)
        {
            bool blocked = false;
            List<string> values = NormalizeStringList(refs);
            for (int i = 0; i < values.Count; i++)
            {
                string outlineId = values[i];
                if (!outlineById.TryGetValue(outlineId, out StoryOutlineEntry outline))
                {
                    blocked = true;
                    AddItemDiagnostic(item, CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceOutlineRefMissing, "Referenced Story Outline is missing: " + outlineId, "Create the outline or remove the reference.", "outlineRefs");
                    continue;
                }

                if (!string.Equals(outline.Stage ?? string.Empty, "approved-for-packet", StringComparison.Ordinal))
                {
                    blocked = true;
                    AddItemDiagnostic(item, CharacterAuthoringValidationSeverity.Warning, AuthoringResourceDiagnosticCodes.StorySourceOutlineRefMissing, "Referenced Story Outline is not approved for packet: " + outlineId, "Approve the outline for packet derivation before approving this packet.", "outlineRefs");
                }
            }

            return blocked;
        }

        private static bool HasValidOutlineBeat(AuthoringResourceProviderContext context, StoryOutlineEntry outline)
        {
            if (outline == null || outline.Beats == null || outline.Beats.Count == 0)
                return false;

            if (context == null || string.IsNullOrWhiteSpace(outline.OutlinePath))
                return true;

            string path = ResolveProjectPath(context, outline.OutlinePath);
            if (!File.Exists(path))
                return false;

            string text = File.ReadAllText(path);
            return text.Contains("entry:", StringComparison.Ordinal) && text.Contains("## Beat ", StringComparison.Ordinal);
        }

        private static bool HasArtSourceTask(AuthoringResourceProviderContext context, string taskId)
        {
            if (context == null || string.IsNullOrWhiteSpace(taskId))
                return false;

            string path = ResolveProjectPath(context, ArtTasksRelativePath);
            if (!File.Exists(path))
                return false;

            string text = File.ReadAllText(path);
            return text.Contains("\"taskId\"", StringComparison.Ordinal) && text.Contains(taskId, StringComparison.Ordinal);
        }

        private static StoryCanonSnapshot CreateCanonSnapshot(StoryCanonFact fact, AuthoringResourceItem item)
        {
            return new StoryCanonSnapshot
            {
                CanonId = fact.CanonId ?? string.Empty,
                Type = fact.Type ?? string.Empty,
                Title = fact.Title ?? string.Empty,
                Summary = fact.Summary ?? string.Empty,
                Status = fact.Status ?? string.Empty,
                Tags = fact.Tags ?? new List<string>(),
                Relations = fact.Relations ?? new List<StoryCanonRelation>(),
                UsedByPacketIds = fact.UsedByPacketIds ?? new List<string>(),
                UsedByOutlineIds = fact.UsedByOutlineIds ?? new List<string>(),
                ResourceKey = item.ResourceId,
                StableId = item.StableId,
                Diagnostics = CreateSnapshotDiagnostics(item)
            };
        }

        private static StoryOutlineSnapshot CreateOutlineSnapshot(StoryOutlineEntry outline, AuthoringResourceItem item, bool outlineExists)
        {
            return new StoryOutlineSnapshot
            {
                OutlineId = outline.OutlineId ?? string.Empty,
                Title = outline.Title ?? string.Empty,
                Stage = outline.Stage ?? string.Empty,
                OutlinePath = outline.OutlinePath ?? string.Empty,
                OutlineExists = outlineExists,
                CanonRefs = outline.CanonRefs ?? new List<string>(),
                Beats = outline.Beats ?? new List<StoryOutlineBeat>(),
                Conflicts = outline.Conflicts ?? new List<string>(),
                OpenQuestions = outline.OpenQuestions ?? new List<string>(),
                TargetConsumers = outline.TargetConsumers ?? new List<string>(),
                ResourceKey = item.ResourceId,
                StableId = item.StableId,
                Diagnostics = CreateSnapshotDiagnostics(item)
            };
        }

        private static StoryPacketSnapshot CreatePacketSnapshot(
            StoryPacketIndexEntry entry,
            StoryInputPacketDocument packet,
            AuthoringResourceItem item,
            string packetPath,
            bool packetExists)
        {
            return new StoryPacketSnapshot
            {
                StorySourceId = packet.StorySourceId ?? string.Empty,
                Version = packet.Version ?? string.Empty,
                ApprovalStatus = packet.ApprovalStatus ?? string.Empty,
                TargetConsumers = packet.TargetConsumers ?? new List<string>(),
                CanonRefs = packet.CanonRefs ?? new List<string>(),
                OutlineRefs = packet.OutlineRefs ?? new List<string>(),
                ReviewStatus = FirstNonEmpty(packet.ReviewStatus, entry.ReviewStatus),
                PacketPath = packetPath ?? string.Empty,
                ExportPath = FirstNonEmpty(packet.ExportPath, entry.ExportPath),
                ArtSourceTaskId = entry.ArtSourceTaskId ?? string.Empty,
                Notes = entry.Notes ?? string.Empty,
                Schema = packet.Schema ?? string.Empty,
                PacketExists = packetExists,
                ResourceKey = item.ResourceId,
                StableId = item.StableId,
                WorldPremise = packet.WorldPremise ?? string.Empty,
                CoreEvent = packet.CoreEvent ?? string.Empty,
                LocationFunction = packet.LocationFunction ?? string.Empty,
                CivilizationOrigin = packet.CivilizationOrigin ?? string.Empty,
                PlayerRole = packet.PlayerRole ?? string.Empty,
                EnemyOrigins = packet.EnemyOrigins ?? new List<string>(),
                PowerRules = packet.PowerRules ?? new List<string>(),
                SymbolMeanings = packet.SymbolMeanings ?? new List<string>(),
                ToneTags = packet.ToneTags ?? new List<string>(),
                VisualDoNotUse = packet.VisualDoNotUse ?? new List<string>(),
                MissingRequiredFields = GetMissingRequiredFields(packet),
                Diagnostics = CreateSnapshotDiagnostics(item)
            };
        }

        private static List<StorySnapshotDiagnostic> CreateSnapshotDiagnostics(AuthoringResourceItem item)
        {
            var result = new List<StorySnapshotDiagnostic>();
            if (item == null || item.Diagnostics == null)
                return result;

            for (int i = 0; i < item.Diagnostics.Count; i++)
            {
                AuthoringResourceDiagnostic diagnostic = item.Diagnostics[i];
                if (diagnostic == null)
                    continue;
                result.Add(new StorySnapshotDiagnostic
                {
                    Severity = diagnostic.Severity.ToString(),
                    Code = diagnostic.Code ?? string.Empty,
                    SourceField = diagnostic.SourceField ?? string.Empty,
                    Message = diagnostic.Message ?? string.Empty,
                    SuggestedFix = diagnostic.SuggestedFix ?? string.Empty
                });
            }

            return result;
        }

        private static void AddValidationDiagnostics(
            AuthoringResourceItem item,
            StorySourceValidationResult validation,
            string sourceConfigKind,
            string sourceStableId)
        {
            if (item == null || validation == null)
                return;

            List<AuthoringResourceDiagnostic> diagnostics = validation.GetDiagnostics(sourceConfigKind, sourceStableId);
            for (int i = 0; i < diagnostics.Count; i++)
            {
                AuthoringResourceDiagnostic source = diagnostics[i];
                if (source == null)
                    continue;
                item.Diagnostics.Add(new AuthoringResourceDiagnostic
                {
                    Severity = source.Severity,
                    Code = source.Code ?? string.Empty,
                    ResourceId = item.ResourceId ?? string.Empty,
                    ResourceStableId = item.StableId ?? string.Empty,
                    RuntimeResourceKey = source.RuntimeResourceKey ?? string.Empty,
                    ProviderId = source.ProviderId ?? AuthoringResourceProviderIds.StorySource,
                    SourceConfigKind = source.SourceConfigKind ?? StorySourceAuthoringValidator.SourceConfigKind,
                    SourceStableId = source.SourceStableId ?? string.Empty,
                    SourceField = source.SourceField ?? string.Empty,
                    Message = source.Message ?? string.Empty,
                    SuggestedFix = source.SuggestedFix ?? string.Empty
                });
            }
        }

        private static List<string> GetMissingRequiredFields(StoryInputPacketDocument packet)
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
                if (GetArrayField(packet, RequiredArrayFields[i]).Count == 0)
                    result.Add(RequiredArrayFields[i]);
            }

            return result;
        }

        private static string GetStringField(StoryInputPacketDocument packet, string field)
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

        private static List<string> GetArrayField(StoryInputPacketDocument packet, string field)
        {
            if (field == "enemyOrigins") return packet.EnemyOrigins ?? new List<string>();
            if (field == "powerRules") return packet.PowerRules ?? new List<string>();
            if (field == "symbolMeanings") return packet.SymbolMeanings ?? new List<string>();
            if (field == "toneTags") return packet.ToneTags ?? new List<string>();
            if (field == "visualDoNotUse") return packet.VisualDoNotUse ?? new List<string>();
            return new List<string>();
        }

        private static void AddCollectionMetadata(AuthoringResourceCollection collection, AuthoringResourceProviderContext context)
        {
            collection.Metadata["storySourceIndexPath"] = IndexRelativePath;
            collection.Metadata["storySourcePacketsRootPath"] = PacketsRootRelativePath;
            collection.Metadata["storySourceCanonPath"] = CanonRelativePath;
            collection.Metadata["storySourceOutlinesPath"] = OutlinesIndexRelativePath;
            if (context != null)
                collection.Metadata["projectRootPath"] = context.ProjectRootPath ?? string.Empty;
        }

        private static void AddSnapshotMetadata(
            AuthoringResourceCollection collection,
            StorySourceIndexDocument packetIndex,
            StoryCanonDocument canonDocument,
            StoryOutlineDocument outlineDocument,
            List<StoryPacketSnapshot> packets,
            List<StoryCanonSnapshot> canonFacts,
            List<StoryOutlineSnapshot> outlines,
            AuthoringResourceProviderContext context)
        {
            collection.Metadata["storySourcePacketsHash"] = context != null ? ComputeStorySourceHash(context.ProjectRootPath) : string.Empty;
            collection.Metadata["storySourcePacketSnapshotJson"] = JsonSerializer.Serialize(new StoryPacketSnapshotDocument
            {
                SchemaVersion = packetIndex != null ? packetIndex.SchemaVersion : 0,
                ProjectId = packetIndex != null ? packetIndex.ProjectId ?? string.Empty : string.Empty,
                Updated = packetIndex != null ? packetIndex.Updated ?? string.Empty : string.Empty,
                Packets = packets ?? new List<StoryPacketSnapshot>()
            }, JsonOptions);
            collection.Metadata["storySourceCanonSnapshotJson"] = JsonSerializer.Serialize(new StoryCanonSnapshotDocument
            {
                SchemaVersion = canonDocument != null ? canonDocument.SchemaVersion : 0,
                ProjectId = canonDocument != null ? canonDocument.ProjectId ?? string.Empty : string.Empty,
                Updated = canonDocument != null ? canonDocument.Updated ?? string.Empty : string.Empty,
                Facts = canonFacts ?? new List<StoryCanonSnapshot>()
            }, JsonOptions);
            collection.Metadata["storySourceOutlineSnapshotJson"] = JsonSerializer.Serialize(new StoryOutlineSnapshotDocument
            {
                SchemaVersion = outlineDocument != null ? outlineDocument.SchemaVersion : 0,
                ProjectId = outlineDocument != null ? outlineDocument.ProjectId ?? string.Empty : string.Empty,
                Updated = outlineDocument != null ? outlineDocument.Updated ?? string.Empty : string.Empty,
                Outlines = outlines ?? new List<StoryOutlineSnapshot>()
            }, JsonOptions);
        }

        private static int CountApprovedForArt(List<StoryPacketSnapshot> snapshots)
        {
            int count = 0;
            for (int i = 0; snapshots != null && i < snapshots.Count; i++)
            {
                if (snapshots[i] != null && EqualsAny(snapshots[i].ApprovalStatus, "approved-for-art"))
                    count++;
            }
            return count;
        }

        private static int CountCanon(List<StoryCanonSnapshot> snapshots)
        {
            int count = 0;
            for (int i = 0; snapshots != null && i < snapshots.Count; i++)
            {
                if (snapshots[i] != null && EqualsAny(snapshots[i].Status, "canon"))
                    count++;
            }
            return count;
        }

        private static int CountApprovedOutlines(List<StoryOutlineSnapshot> snapshots)
        {
            int count = 0;
            for (int i = 0; snapshots != null && i < snapshots.Count; i++)
            {
                if (snapshots[i] != null && EqualsAny(snapshots[i].Stage, "approved-for-packet"))
                    count++;
            }
            return count;
        }

        private static StoryInputPacketDocument CreatePacketFallback(StoryPacketIndexEntry entry)
        {
            return new StoryInputPacketDocument
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

        private static void ApplyIndexFallback(StoryPacketIndexEntry entry, StoryInputPacketDocument packet)
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

        private static bool TargetsArtSource(StoryInputPacketDocument packet)
        {
            List<string> consumers = packet != null ? packet.TargetConsumers : null;
            for (int i = 0; consumers != null && i < consumers.Count; i++)
            {
                if (string.Equals(consumers[i] ?? string.Empty, "artSource", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static List<string> NormalizeStringList(List<string> values)
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

        private static void AddItemDiagnostic(AuthoringResourceItem item, CharacterAuthoringValidationSeverity severity, string code, string message, string suggestedFix, string sourceField)
        {
            item.Diagnostics.Add(CreateDiagnostic(
                severity,
                code,
                item.CanonicalIdentity ?? string.Empty,
                message,
                suggestedFix,
                sourceField,
                item.ResourceId,
                item.StableId));
        }

        private static AuthoringResourceDiagnostic CreateDiagnostic(
            CharacterAuthoringValidationSeverity severity,
            string code,
            string sourceStableId,
            string message,
            string suggestedFix,
            string sourceField,
            string resourceId = "",
            string resourceStableId = "")
        {
            return new AuthoringResourceDiagnostic
            {
                Severity = severity,
                Code = code ?? string.Empty,
                ResourceId = resourceId ?? string.Empty,
                ResourceStableId = resourceStableId ?? string.Empty,
                ProviderId = AuthoringResourceProviderIds.StorySource,
                SourceConfigKind = "story_source",
                SourceStableId = sourceStableId ?? string.Empty,
                SourceField = sourceField ?? string.Empty,
                Message = message ?? string.Empty,
                SuggestedFix = suggestedFix ?? string.Empty
            };
        }

        private static string ResolveProjectPath(AuthoringResourceProviderContext context, string projectRelativePath)
        {
            return context != null
                ? AuthoringResourceProviderUtilities.ResolveProjectPath(context.ProjectRootPath, projectRelativePath)
                : projectRelativePath;
        }

        private static string BuildDefaultPacketPath(string packetId)
        {
            return PacketsRootRelativePath + "/" + AuthoringResourceProviderUtilities.SanitizeStableSegment(packetId) + ".json";
        }

        private static T ReadJsonSilently<T>(string projectRootPath, string relativePath) where T : class
        {
            string path = AuthoringResourceProviderUtilities.ResolveProjectPath(projectRootPath, relativePath);
            if (!File.Exists(path))
                return null;
            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static void AppendProjectFileHash(string projectRootPath, StringBuilder builder, string relativePath)
        {
            string fullPath = AuthoringResourceProviderUtilities.ResolveProjectPath(projectRootPath, relativePath);
            builder.Append(relativePath ?? string.Empty).Append('=');
            if (File.Exists(fullPath))
                builder.Append(CharacterPackageHashUtility.ComputeFileSha256(fullPath));
            else
                builder.Append("(missing)");
            builder.Append('\n');
        }

        private static string ComputeStringSha256(string value)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            byte[] hash = sha.ComputeHash(bytes);
            var builder = new StringBuilder("sha256:", "sha256:".Length + hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static bool EqualsAny(string value, params string[] options)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (string.Equals(value ?? string.Empty, options[i] ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return AuthoringResourceProviderUtilities.FirstNonEmpty(values);
        }

        private static void AddTag(List<string> tags, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                tags.Add(value);
        }

        private sealed class StorySourceIndexDocument
        {
            [JsonPropertyName("schemaVersion")]
            public int SchemaVersion { get; set; }

            [JsonPropertyName("projectId")]
            public string ProjectId { get; set; } = string.Empty;

            [JsonPropertyName("updated")]
            public string Updated { get; set; } = string.Empty;

            [JsonPropertyName("packets")]
            public List<StoryPacketIndexEntry> Packets { get; set; } = new List<StoryPacketIndexEntry>();
        }

        private sealed class StoryPacketIndexEntry
        {
            [JsonPropertyName("storySourceId")]
            public string StorySourceId { get; set; } = string.Empty;

            [JsonPropertyName("version")]
            public string Version { get; set; } = string.Empty;

            [JsonPropertyName("approvalStatus")]
            public string ApprovalStatus { get; set; } = string.Empty;

            [JsonPropertyName("targetConsumers")]
            public List<string> TargetConsumers { get; set; } = new List<string>();

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

        private sealed class StoryInputPacketDocument
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
        }

        private sealed class StoryCanonDocument
        {
            [JsonPropertyName("schemaVersion")]
            public int SchemaVersion { get; set; }

            [JsonPropertyName("projectId")]
            public string ProjectId { get; set; } = string.Empty;

            [JsonPropertyName("updated")]
            public string Updated { get; set; } = string.Empty;

            [JsonPropertyName("facts")]
            public List<StoryCanonFact> Facts { get; set; } = new List<StoryCanonFact>();
        }

        private sealed class StoryCanonFact
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
            public List<StoryCanonRelation> Relations { get; set; } = new List<StoryCanonRelation>();

            [JsonPropertyName("usedByPacketIds")]
            public List<string> UsedByPacketIds { get; set; } = new List<string>();

            [JsonPropertyName("usedByOutlineIds")]
            public List<string> UsedByOutlineIds { get; set; } = new List<string>();
        }

        private sealed class StoryCanonRelation
        {
            [JsonPropertyName("relation")]
            public string Relation { get; set; } = string.Empty;

            [JsonPropertyName("targetId")]
            public string TargetId { get; set; } = string.Empty;
        }

        private sealed class StoryOutlineDocument
        {
            [JsonPropertyName("schemaVersion")]
            public int SchemaVersion { get; set; }

            [JsonPropertyName("projectId")]
            public string ProjectId { get; set; } = string.Empty;

            [JsonPropertyName("updated")]
            public string Updated { get; set; } = string.Empty;

            [JsonPropertyName("outlines")]
            public List<StoryOutlineEntry> Outlines { get; set; } = new List<StoryOutlineEntry>();
        }

        private sealed class StoryOutlineEntry
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
            public List<StoryOutlineBeat> Beats { get; set; } = new List<StoryOutlineBeat>();

            [JsonPropertyName("conflicts")]
            public List<string> Conflicts { get; set; } = new List<string>();

            [JsonPropertyName("openQuestions")]
            public List<string> OpenQuestions { get; set; } = new List<string>();

            [JsonPropertyName("targetConsumers")]
            public List<string> TargetConsumers { get; set; } = new List<string>();
        }

        private sealed class StoryOutlineBeat
        {
            [JsonPropertyName("slug")]
            public string Slug { get; set; } = string.Empty;

            [JsonPropertyName("title")]
            public string Title { get; set; } = string.Empty;

            [JsonPropertyName("summary")]
            public string Summary { get; set; } = string.Empty;
        }

        private sealed class StoryPacketSnapshotDocument
        {
            [JsonPropertyName("schemaVersion")]
            public int SchemaVersion { get; set; }

            [JsonPropertyName("projectId")]
            public string ProjectId { get; set; } = string.Empty;

            [JsonPropertyName("updated")]
            public string Updated { get; set; } = string.Empty;

            [JsonPropertyName("packets")]
            public List<StoryPacketSnapshot> Packets { get; set; } = new List<StoryPacketSnapshot>();
        }

        private sealed class StoryCanonSnapshotDocument
        {
            [JsonPropertyName("schemaVersion")]
            public int SchemaVersion { get; set; }

            [JsonPropertyName("projectId")]
            public string ProjectId { get; set; } = string.Empty;

            [JsonPropertyName("updated")]
            public string Updated { get; set; } = string.Empty;

            [JsonPropertyName("facts")]
            public List<StoryCanonSnapshot> Facts { get; set; } = new List<StoryCanonSnapshot>();
        }

        private sealed class StoryOutlineSnapshotDocument
        {
            [JsonPropertyName("schemaVersion")]
            public int SchemaVersion { get; set; }

            [JsonPropertyName("projectId")]
            public string ProjectId { get; set; } = string.Empty;

            [JsonPropertyName("updated")]
            public string Updated { get; set; } = string.Empty;

            [JsonPropertyName("outlines")]
            public List<StoryOutlineSnapshot> Outlines { get; set; } = new List<StoryOutlineSnapshot>();
        }

        private sealed class StoryPacketSnapshot
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

            [JsonPropertyName("schema")]
            public string Schema { get; set; } = string.Empty;

            [JsonPropertyName("packetExists")]
            public bool PacketExists { get; set; }

            [JsonPropertyName("resourceKey")]
            public string ResourceKey { get; set; } = string.Empty;

            [JsonPropertyName("stableId")]
            public string StableId { get; set; } = string.Empty;

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

            [JsonPropertyName("missingRequiredFields")]
            public List<string> MissingRequiredFields { get; set; } = new List<string>();

            [JsonPropertyName("diagnostics")]
            public List<StorySnapshotDiagnostic> Diagnostics { get; set; } = new List<StorySnapshotDiagnostic>();
        }

        private sealed class StoryCanonSnapshot
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
            public List<StoryCanonRelation> Relations { get; set; } = new List<StoryCanonRelation>();

            [JsonPropertyName("usedByPacketIds")]
            public List<string> UsedByPacketIds { get; set; } = new List<string>();

            [JsonPropertyName("usedByOutlineIds")]
            public List<string> UsedByOutlineIds { get; set; } = new List<string>();

            [JsonPropertyName("resourceKey")]
            public string ResourceKey { get; set; } = string.Empty;

            [JsonPropertyName("stableId")]
            public string StableId { get; set; } = string.Empty;

            [JsonPropertyName("diagnostics")]
            public List<StorySnapshotDiagnostic> Diagnostics { get; set; } = new List<StorySnapshotDiagnostic>();
        }

        private sealed class StoryOutlineSnapshot
        {
            [JsonPropertyName("outlineId")]
            public string OutlineId { get; set; } = string.Empty;

            [JsonPropertyName("title")]
            public string Title { get; set; } = string.Empty;

            [JsonPropertyName("stage")]
            public string Stage { get; set; } = string.Empty;

            [JsonPropertyName("outlinePath")]
            public string OutlinePath { get; set; } = string.Empty;

            [JsonPropertyName("outlineExists")]
            public bool OutlineExists { get; set; }

            [JsonPropertyName("canonRefs")]
            public List<string> CanonRefs { get; set; } = new List<string>();

            [JsonPropertyName("beats")]
            public List<StoryOutlineBeat> Beats { get; set; } = new List<StoryOutlineBeat>();

            [JsonPropertyName("conflicts")]
            public List<string> Conflicts { get; set; } = new List<string>();

            [JsonPropertyName("openQuestions")]
            public List<string> OpenQuestions { get; set; } = new List<string>();

            [JsonPropertyName("targetConsumers")]
            public List<string> TargetConsumers { get; set; } = new List<string>();

            [JsonPropertyName("resourceKey")]
            public string ResourceKey { get; set; } = string.Empty;

            [JsonPropertyName("stableId")]
            public string StableId { get; set; } = string.Empty;

            [JsonPropertyName("diagnostics")]
            public List<StorySnapshotDiagnostic> Diagnostics { get; set; } = new List<StorySnapshotDiagnostic>();
        }

        private sealed class StorySnapshotDiagnostic
        {
            [JsonPropertyName("severity")]
            public string Severity { get; set; } = string.Empty;

            [JsonPropertyName("code")]
            public string Code { get; set; } = string.Empty;

            [JsonPropertyName("sourceField")]
            public string SourceField { get; set; } = string.Empty;

            [JsonPropertyName("message")]
            public string Message { get; set; } = string.Empty;

            [JsonPropertyName("suggestedFix")]
            public string SuggestedFix { get; set; } = string.Empty;
        }
    }
}
