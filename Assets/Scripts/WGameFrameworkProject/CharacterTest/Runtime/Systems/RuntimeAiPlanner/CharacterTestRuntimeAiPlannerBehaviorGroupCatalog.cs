using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestRuntimeAiPlannerBehaviorGroupCardReference
    {
        public CharacterTestRuntimeAiPlannerBehaviorGroupCardReference(
            string entryId,
            string stableCardId,
            int cardRevision,
            long semanticHash,
            long contentHash,
            bool enabled,
            int order)
        {
            EntryId = entryId?.Trim() ?? string.Empty;
            StableCardId = stableCardId?.Trim() ?? string.Empty;
            CardRevision = cardRevision;
            SemanticHash = semanticHash;
            ContentHash = contentHash;
            Enabled = enabled;
            Order = order;
        }

        public string EntryId { get; }
        public string StableCardId { get; }
        public int CardRevision { get; }
        public long SemanticHash { get; }
        public long ContentHash { get; }
        public bool Enabled { get; }
        public int Order { get; }
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorGroupRevision
    {
        private readonly string[] _tags;
        private readonly CharacterTestRuntimeAiPlannerBehaviorGroupCardReference[] _cards;

        public CharacterTestRuntimeAiPlannerBehaviorGroupRevision(
            string stableGroupId,
            int revision,
            string displayName,
            string description,
            IEnumerable<string> tags,
            int sortOrder,
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorGroupCardReference> cards,
            long contentHash = 0)
        {
            StableGroupId = stableGroupId?.Trim() ?? string.Empty;
            Revision = revision;
            DisplayName = displayName?.Trim() ?? string.Empty;
            Description = description?.Trim() ?? string.Empty;
            _tags = CopyTags(tags);
            SortOrder = sortOrder;
            Profile = profile?.Clone();
            if (Profile != null)
            {
                Profile.GroupId = StableGroupId;
                if (!string.IsNullOrWhiteSpace(DisplayName))
                    Profile.DisplayName = DisplayName;
            }
            _cards = CopyCards(cards);
            ContentHash = contentHash != 0 ? contentHash : ComputeContentHash(this);
        }

        public string StableGroupId { get; }
        public int Revision { get; }
        public long ContentHash { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public IReadOnlyList<string> Tags => _tags;
        public int SortOrder { get; }
        public CharacterTestRuntimeAiPlannerBehaviorCompositionProfile Profile { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorGroupCardReference> Cards => _cards;

        internal static long ComputeContentHash(CharacterTestRuntimeAiPlannerBehaviorGroupRevision group)
        {
            return ComputeContentHash(
                group,
                CharacterTestRuntimeAiPlannerBehaviorGroupCatalog.Schema,
                "behaviorGroup.profile.responsibility",
                includeResponsibilitySet: true);
        }

        internal static long ComputePreviousContentHash(CharacterTestRuntimeAiPlannerBehaviorGroupRevision group)
        {
            return ComputeContentHash(
                group,
                CharacterTestRuntimeAiPlannerBehaviorGroupCatalog.PreviousSchema,
                "behaviorGroup.profile.responsibility",
                includeResponsibilitySet: false);
        }

        internal static long ComputeLegacyContentHash(CharacterTestRuntimeAiPlannerBehaviorGroupRevision group)
        {
            return ComputeContentHash(
                group,
                CharacterTestRuntimeAiPlannerBehaviorGroupCatalog.LegacySchema,
                "behaviorGroup.profile.slot",
                includeResponsibilitySet: false);
        }

        private static long ComputeContentHash(
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision group,
            string schema,
            string responsibilityHashKey,
            bool includeResponsibilitySet)
        {
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable("behaviorGroup.schema", schema);
            hash.AddStringStable("behaviorGroup.stableGroupId", group.StableGroupId);
            hash.AddInt("behaviorGroup.revision", group.Revision);
            hash.AddStringStable("behaviorGroup.displayName", group.DisplayName);
            hash.AddStringStable("behaviorGroup.description", group.Description);
            hash.AddInt("behaviorGroup.sortOrder", group.SortOrder);
            hash.AddInt("behaviorGroup.tagCount", group._tags.Length);
            for (int i = 0; i < group._tags.Length; i++)
                hash.AddStringStable("behaviorGroup.tag", group._tags[i]);
            AddProfileHash(
                ref hash,
                group.Profile,
                responsibilityHashKey,
                includeResponsibilitySet);
            hash.AddInt("behaviorGroup.cardCount", group._cards.Length);
            for (int i = 0; i < group._cards.Length; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorGroupCardReference card = group._cards[i];
                hash.AddStringStable("behaviorGroup.card.entryId", card.EntryId);
                hash.AddStringStable("behaviorGroup.card.stableCardId", card.StableCardId);
                hash.AddInt("behaviorGroup.card.revision", card.CardRevision);
                hash.AddLong("behaviorGroup.card.semanticHash", card.SemanticHash);
                hash.AddLong("behaviorGroup.card.contentHash", card.ContentHash);
                hash.AddInt("behaviorGroup.card.enabled", card.Enabled ? 1 : 0);
                hash.AddInt("behaviorGroup.card.order", card.Order);
            }
            return hash.ToHash();
        }

        private static void AddProfileHash(
            ref RuntimeContentHashAccumulator hash,
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            string responsibilityHashKey,
            bool includeResponsibilitySet)
        {
            hash.AddStringStable("behaviorGroup.profile.groupId", profile?.GroupId);
            hash.AddStringStable("behaviorGroup.profile.displayName", profile?.DisplayName);
            hash.AddInt("behaviorGroup.profile.targeting", profile != null ? (int)profile.TargetSelectionMode : 0);
            AddFloat(ref hash, "behaviorGroup.profile.attackRange", profile?.AttackRange ?? 0f);
            CharacterTestRuntimeAiPlannerPerceptionSettings perception =
                CharacterTestRuntimeAiPlannerPerceptionSettings.Normalize(profile?.PerceptionSettings);
            AddFloat(ref hash, "behaviorGroup.profile.visionRange", perception.VisionRange);
            hash.AddInt("behaviorGroup.profile.memoryFrames", perception.TargetMemoryFrames);
            hash.AddInt("behaviorGroup.profile.obstacleMask", perception.LineOfSightObstacleLayerMask);
            AddFloat(ref hash, "behaviorGroup.profile.sampleRadius", perception.EnvironmentSampleRadius);
            AddFloat(ref hash, "behaviorGroup.profile.threatWeight", perception.ThreatDistanceWeight);
            CharacterTestRuntimeAiPlannerMovementSettings movement =
                CharacterTestRuntimeAiPlannerMovementSettings.Normalize(profile?.MovementSettings);
            AddFloat(ref hash, "behaviorGroup.profile.speedScale", movement.SpeedScale);
            AddFloat(ref hash, "behaviorGroup.profile.steering", movement.SteeringResponsiveness);
            AddFloat(ref hash, "behaviorGroup.profile.lateralBlend", movement.ApproachLateralBlend);
            AddFloat(ref hash, "behaviorGroup.profile.preferredMin", movement.PreferredDistanceMin);
            AddFloat(ref hash, "behaviorGroup.profile.preferredMax", movement.PreferredDistanceMax);
            AddFloat(ref hash, "behaviorGroup.profile.slowdown", movement.ArrivalSlowdownDistance);
            hash.AddInt("behaviorGroup.profile.strafeMin", movement.StrafeSwitchMinFrames);
            hash.AddInt("behaviorGroup.profile.strafeMax", movement.StrafeSwitchMaxFrames);
            hash.AddInt("behaviorGroup.profile.holdFrames", movement.DirectionHoldFrames);
            hash.AddInt("behaviorGroup.profile.reactionDelay", movement.ReactionDelayFrames);

            int entryCount = profile?.Entries.Count ?? 0;
            hash.AddInt("behaviorGroup.profile.entryCount", entryCount);
            for (int i = 0; i < entryCount; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = profile.Entries[i];
                hash.AddStringStable("behaviorGroup.profile.entryId", entry.EntryId);
                hash.AddStringStable("behaviorGroup.profile.behaviorId", entry.BehaviorId);
                hash.AddInt("behaviorGroup.profile.order", entry.Order);
                hash.AddInt("behaviorGroup.profile.disabled", entry.Disabled ? 1 : 0);
                if (includeResponsibilitySet)
                {
                    hash.AddInt(
                        responsibilityHashKey + ".count",
                        entry.ResponsibilityKinds.Count);
                    for (int responsibilityIndex = 0;
                         responsibilityIndex < entry.ResponsibilityKinds.Count;
                         responsibilityIndex++)
                    {
                        hash.AddInt(
                            responsibilityHashKey,
                            (int)entry.ResponsibilityKinds[responsibilityIndex]);
                    }
                }
                else
                {
#pragma warning disable CS0618
                    hash.AddInt(responsibilityHashKey, (int)entry.ResponsibilityKind);
#pragma warning restore CS0618
                }
                var keys = new List<string>(entry.ParameterOverrides.Keys);
                keys.Sort(StringComparer.Ordinal);
                hash.AddInt("behaviorGroup.profile.parameterCount", keys.Count);
                for (int j = 0; j < keys.Count; j++)
                {
                    string key = keys[j];
                    CharacterTestRuntimeAiPlannerBehaviorParameterValue value = entry.ParameterOverrides[key];
                    hash.AddStringStable("behaviorGroup.profile.parameterKey", key);
                    hash.AddInt("behaviorGroup.profile.parameterKind", (int)value.Kind);
                    hash.AddInt("behaviorGroup.profile.parameterBool", value.BoolValue ? 1 : 0);
                    hash.AddInt("behaviorGroup.profile.parameterInt", value.IntValue);
                    AddFloat(ref hash, "behaviorGroup.profile.parameterFloat", value.FloatValue);
                }
            }
        }

        private static void AddFloat(ref RuntimeContentHashAccumulator hash, string key, float value)
        {
            hash.AddInt(key, BitConverter.SingleToInt32Bits(value));
        }

        private static string[] CopyTags(IEnumerable<string> source)
        {
            if (source == null)
                return Array.Empty<string>();
            var result = new List<string>();
            foreach (string value in source)
            {
                string normalized = value?.Trim() ?? string.Empty;
                if (normalized.Length > 0 && !result.Contains(normalized))
                    result.Add(normalized);
            }
            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }

        private static CharacterTestRuntimeAiPlannerBehaviorGroupCardReference[] CopyCards(
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorGroupCardReference> source)
        {
            if (source == null)
                return Array.Empty<CharacterTestRuntimeAiPlannerBehaviorGroupCardReference>();
            var result = new List<CharacterTestRuntimeAiPlannerBehaviorGroupCardReference>();
            foreach (CharacterTestRuntimeAiPlannerBehaviorGroupCardReference card in source)
            {
                if (card != null)
                    result.Add(card);
            }
            return result.ToArray();
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorGroupCatalog
    {
        public const string Schema = "mx.character-test.runtime-ai-planner.behavior-group-catalog.v3";
        internal const int EntryNumericDomainId = 420;
        internal const string PreviousSchema = "mx.character-test.runtime-ai-planner.behavior-group-catalog.v2";
        internal const string LegacySchema = "mx.character-test.runtime-ai-planner.behavior-group-catalog.v1";
        private readonly CharacterTestRuntimeAiPlannerBehaviorGroupRevision[] _groups;

        public CharacterTestRuntimeAiPlannerBehaviorGroupCatalog(
            string catalogId,
            int revision,
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorGroupRevision> groups)
        {
            CatalogId = catalogId?.Trim() ?? string.Empty;
            Revision = revision;
            var copy = new List<CharacterTestRuntimeAiPlannerBehaviorGroupRevision>();
            if (groups != null)
            {
                foreach (CharacterTestRuntimeAiPlannerBehaviorGroupRevision group in groups)
                {
                    if (group != null)
                        copy.Add(group);
                }
            }
            _groups = copy.ToArray();
            Array.Sort(_groups, CompareGroups);
            Groups = Array.AsReadOnly(_groups);
            CatalogHash = ComputeCatalogHash();
        }

        public string CatalogId { get; }
        public int Revision { get; }
        public long CatalogHash { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorGroupRevision> Groups { get; }

        public bool TryGetExact(string stableGroupId, int revision, out CharacterTestRuntimeAiPlannerBehaviorGroupRevision group)
        {
            string normalized = stableGroupId?.Trim() ?? string.Empty;
            for (int i = 0; i < _groups.Length; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorGroupRevision candidate = _groups[i];
                if (candidate.Revision == revision
                    && string.Equals(candidate.StableGroupId, normalized, StringComparison.Ordinal))
                {
                    group = candidate;
                    return true;
                }
            }
            group = null;
            return false;
        }

        public bool TryGetLatest(string stableGroupId, out CharacterTestRuntimeAiPlannerBehaviorGroupRevision group)
        {
            group = null;
            string normalized = stableGroupId?.Trim() ?? string.Empty;
            for (int i = 0; i < _groups.Length; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorGroupRevision candidate = _groups[i];
                if (string.Equals(candidate.StableGroupId, normalized, StringComparison.Ordinal)
                    && (group == null || candidate.Revision > group.Revision))
                {
                    group = candidate;
                }
            }
            return group != null;
        }

        public CharacterTestRuntimeAiPlannerBehaviorGroupRevision[] GetLatestRevisions()
        {
            var latest = new List<CharacterTestRuntimeAiPlannerBehaviorGroupRevision>();
            for (int i = 0; i < _groups.Length; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorGroupRevision candidate = _groups[i];
                int existing = -1;
                for (int j = 0; j < latest.Count; j++)
                {
                    if (string.Equals(latest[j].StableGroupId, candidate.StableGroupId, StringComparison.Ordinal))
                    {
                        existing = j;
                        break;
                    }
                }
                if (existing < 0)
                    latest.Add(candidate);
                else if (candidate.Revision > latest[existing].Revision)
                    latest[existing] = candidate;
            }
            latest.Sort((left, right) =>
            {
                int order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(left.StableGroupId, right.StableGroupId, StringComparison.Ordinal);
            });
            return latest.ToArray();
        }

        private long ComputeCatalogHash()
        {
            return ComputeCatalogHash(Schema, CatalogId, Revision, _groups);
        }

        internal static long ComputeLegacyCatalogHash(
            string catalogId,
            int revision,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorGroupRevision> groups)
        {
            return ComputeCatalogHash(LegacySchema, catalogId, revision, groups);
        }

        internal static long ComputePreviousCatalogHash(
            string catalogId,
            int revision,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorGroupRevision> groups)
        {
            return ComputeCatalogHash(PreviousSchema, catalogId, revision, groups);
        }

        private static long ComputeCatalogHash(
            string schema,
            string catalogId,
            int revision,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorGroupRevision> groups)
        {
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable("behaviorGroupCatalog.schema", schema);
            hash.AddStringStable("behaviorGroupCatalog.catalogId", catalogId);
            hash.AddInt("behaviorGroupCatalog.revision", revision);
            hash.AddInt("behaviorGroupCatalog.groupCount", groups?.Count ?? 0);
            for (int i = 0; groups != null && i < groups.Count; i++)
            {
                hash.AddStringStable("behaviorGroupCatalog.groupId", groups[i].StableGroupId);
                hash.AddInt("behaviorGroupCatalog.groupRevision", groups[i].Revision);
                hash.AddLong("behaviorGroupCatalog.groupHash", groups[i].ContentHash);
            }
            return hash.ToHash();
        }

        private static int CompareGroups(
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision left,
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision right)
        {
            int stable = string.Compare(left.StableGroupId, right.StableGroupId, StringComparison.Ordinal);
            return stable != 0 ? stable : left.Revision.CompareTo(right.Revision);
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult
    {
        public CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult(
            CharacterTestRuntimeAiPlannerBehaviorGroupCatalog catalog,
            string diagnostic)
        {
            Catalog = catalog;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public CharacterTestRuntimeAiPlannerBehaviorGroupCatalog Catalog { get; }
        public string Diagnostic { get; }
        public bool Success => Catalog != null && Diagnostic.Length == 0;
    }

    internal static class CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = new List<JsonConverter> { new StringEnumConverter() },
            Formatting = Formatting.Indented,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        public static string ToJson(CharacterTestRuntimeAiPlannerBehaviorGroupCatalog catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            return JsonConvert.SerializeObject(CatalogDocument.FromCatalog(catalog), Settings);
        }

        public static CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult Load(
            string json,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog cardCatalog,
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog definitionCatalog)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Failed("项目行为组目录 JSON 为空。");
            CatalogDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<CatalogDocument>(json, Settings);
            }
            catch (JsonException exception)
            {
                return Failed("项目行为组目录 JSON 解析失败：" + exception.Message);
            }
            if (document == null
                || (!string.Equals(document.Schema, CharacterTestRuntimeAiPlannerBehaviorGroupCatalog.Schema, StringComparison.Ordinal)
                    && !string.Equals(document.Schema, CharacterTestRuntimeAiPlannerBehaviorGroupCatalog.PreviousSchema, StringComparison.Ordinal)
                    && !string.Equals(document.Schema, CharacterTestRuntimeAiPlannerBehaviorGroupCatalog.LegacySchema, StringComparison.Ordinal)))
                return Failed("项目行为组目录 schema 不兼容。");
            bool legacyDocument = string.Equals(
                document.Schema,
                CharacterTestRuntimeAiPlannerBehaviorGroupCatalog.LegacySchema,
                StringComparison.Ordinal);
            bool previousDocument = string.Equals(
                document.Schema,
                CharacterTestRuntimeAiPlannerBehaviorGroupCatalog.PreviousSchema,
                StringComparison.Ordinal);
            if (string.IsNullOrWhiteSpace(document.CatalogId) || document.Revision <= 0 || document.Groups == null)
                return Failed("项目行为组目录缺少 catalogId、revision 或 groups。");
            if (cardCatalog == null)
                return Failed("项目行为卡目录不可用，无法验证行为组固定版本引用。");

            var groups = new List<CharacterTestRuntimeAiPlannerBehaviorGroupRevision>();
            var authenticatedGroups = new List<CharacterTestRuntimeAiPlannerBehaviorGroupRevision>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            var numericIdsByEntryId = new Dictionary<string, int>(StringComparer.Ordinal);
            var entryIdsByNumericId = new Dictionary<int, string>();
            for (int i = 0; i < document.Groups.Length; i++)
            {
                GroupDocument source = document.Groups[i];
                string path = "groups[" + i + "]";
                if (source == null || !CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.IsStableCardId(source.StableGroupId))
                    return Failed(path + " 的 stableGroupId 非法。");
                if (source.Revision <= 0 || !identities.Add(source.StableGroupId + "@" + source.Revision))
                    return Failed(path + " 的 revision 非法或重复。");
                CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile = source.ToProfile();
                var groupNumericIds = new HashSet<int>();
                for (int j = 0; j < profile.Entries.Count; j++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = profile.Entries[j];
                    string entryPath = path + ".entries[" + j + "]";
                    if (entry == null
                        || entry.NumericId <= 0
                        || !CharacterTestAuthoredContentIds.IsActive(
                            CharacterTestRuntimeAiPlannerBehaviorGroupCatalog.EntryNumericDomainId,
                            entry.NumericId))
                        return Failed(entryPath + ".numericId 必须是正数且来自已持久化的行为条目身份。");
                    if (entry.DefinitionNumericId <= 0)
                        return Failed(entryPath + ".definitionNumericId 必须是正数且来自已持久化的行为定义身份。");
                    if (!groupNumericIds.Add(entry.NumericId))
                        return Failed(entryPath + ".numericId 在同一行为组内重复。");

                    string entryId = entry.EntryId?.Trim() ?? string.Empty;
                    if (entryId.Length == 0)
                        return Failed(entryPath + ".entryId 缺失，无法认证行为条目数字身份。");
                    if (numericIdsByEntryId.TryGetValue(entryId, out int existingNumericId)
                        && existingNumericId != entry.NumericId)
                    {
                        return Failed(entryPath + ".numericId 与同一 entryId 在其它 revision 中不一致。");
                    }
                    numericIdsByEntryId[entryId] = entry.NumericId;
                    if (entryIdsByNumericId.TryGetValue(entry.NumericId, out string existingEntryId)
                        && !string.Equals(existingEntryId, entryId, StringComparison.Ordinal))
                    {
                        return Failed(entryPath + ".numericId 已被其它 entryId 使用。");
                    }
                    entryIdsByNumericId[entry.NumericId] = entryId;
                }
                CardReferenceDocument[] sourceCards = source.Cards ?? Array.Empty<CardReferenceDocument>();
                var cards = new CharacterTestRuntimeAiPlannerBehaviorGroupCardReference[sourceCards.Length];
                var authenticatedCards = new CharacterTestRuntimeAiPlannerBehaviorGroupCardReference[sourceCards.Length];
                int totalCost = 0;
                var entryIds = new HashSet<string>(StringComparer.Ordinal);
                for (int j = 0; j < sourceCards.Length; j++)
                {
                    CardReferenceDocument card = sourceCards[j];
                    if (card == null
                        || !entryIds.Add(card.EntryId ?? string.Empty))
                    {
                        return Failed(path + ".cards[" + j + "] 引用缺失或重复。");
                    }
                    if (!cardCatalog.TryGetExact(card.StableCardId, card.CardRevision, out CharacterTestRuntimeAiPlannerPublishedBehaviorCard exact))
                        return Failed(path + ".cards[" + j + "] 找不到固定的行为卡 revision。");
                    if (!exact.MatchesPinnedHash(card.SemanticHash, card.ContentHash))
                        return Failed(path + ".cards[" + j + "] 的 hash 与项目行为卡目录不一致。");
                    if (!TryFindEditableEntry(
                            profile,
                            card.EntryId,
                            out CharacterTestRuntimeAiPlannerBehaviorModuleEntry referencedEntry))
                        return Failed(path + ".cards[" + j + "] 没有对应的非核心行为条目。");
                    if (referencedEntry.DefinitionNumericId != exact.NumericId)
                        return Failed(path + ".cards[" + j + "] 的 definitionNumericId 与固定行为卡数字身份不一致。");
                    if (referencedEntry.Disabled == card.Enabled || referencedEntry.Order != card.Order)
                        return Failed(path + ".cards[" + j + "] 的启用状态或组内顺序与行为组条目不一致。");
                    totalCost += exact.Metadata != null ? exact.Metadata.Cost : 0;
                    cards[j] = new CharacterTestRuntimeAiPlannerBehaviorGroupCardReference(
                        card.EntryId,
                        card.StableCardId,
                        card.CardRevision,
                        exact.SemanticHash,
                        exact.ContentHash,
                        card.Enabled,
                        card.Order);
                    authenticatedCards[j] = new CharacterTestRuntimeAiPlannerBehaviorGroupCardReference(
                        card.EntryId,
                        card.StableCardId,
                        card.CardRevision,
                        card.SemanticHash,
                        card.ContentHash,
                        card.Enabled,
                        card.Order);
                }
                if (totalCost > CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.MaximumPlayerCardCost)
                    return Failed(path + " 的行为卡总费用超过 10。");
                if (CountEditableEntries(profile, entryIds) != cards.Length)
                    return Failed(path + " 的非核心行为条目必须逐一固定到行为卡版本。");
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotReport coreReport =
                    CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.Validate(profile.Entries, definitionCatalog);
                if (!coreReport.Success)
                    return Failed(path + " 的行为职责归属无效：" + coreReport.Message);
                CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(profile, definitionCatalog);
                if (!compile.Success)
                    return Failed(path + " 的运行时 Profile 无法编译：" + FirstDiagnostic(compile.Report));
                var authenticatedGroup = new CharacterTestRuntimeAiPlannerBehaviorGroupRevision(
                    source.StableGroupId,
                    source.Revision,
                    source.DisplayName,
                    source.Description,
                    source.Tags,
                    source.SortOrder,
                    profile,
                    authenticatedCards,
                    source.ContentHash);
                long expectedContentHash = legacyDocument
                    ? CharacterTestRuntimeAiPlannerBehaviorGroupRevision.ComputeLegacyContentHash(authenticatedGroup)
                    : previousDocument
                        ? CharacterTestRuntimeAiPlannerBehaviorGroupRevision.ComputePreviousContentHash(authenticatedGroup)
                        : CharacterTestRuntimeAiPlannerBehaviorGroupRevision.ComputeContentHash(authenticatedGroup);
                if (authenticatedGroup.ContentHash != expectedContentHash)
                    return Failed(path + " 的 contentHash 不匹配。");
                authenticatedGroups.Add(authenticatedGroup);
                var group = new CharacterTestRuntimeAiPlannerBehaviorGroupRevision(
                    source.StableGroupId,
                    source.Revision,
                    source.DisplayName,
                    source.Description,
                    source.Tags,
                    source.SortOrder,
                    profile,
                    cards);
                groups.Add(group);
            }
            var sourceCatalog = new CharacterTestRuntimeAiPlannerBehaviorGroupCatalog(
                document.CatalogId,
                document.Revision,
                groups);
            var authenticatedCatalog = new CharacterTestRuntimeAiPlannerBehaviorGroupCatalog(
                document.CatalogId,
                document.Revision,
                authenticatedGroups);
            long expectedCatalogHash = legacyDocument
                ? CharacterTestRuntimeAiPlannerBehaviorGroupCatalog.ComputeLegacyCatalogHash(
                    document.CatalogId,
                    document.Revision,
                    authenticatedCatalog.Groups)
                : previousDocument
                    ? CharacterTestRuntimeAiPlannerBehaviorGroupCatalog.ComputePreviousCatalogHash(
                        document.CatalogId,
                        document.Revision,
                        authenticatedCatalog.Groups)
                    : authenticatedCatalog.CatalogHash;
            if (expectedCatalogHash != document.CatalogHash)
                return Failed("项目行为组目录 catalogHash 不匹配。");
            if (!legacyDocument && !previousDocument)
                return new CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult(sourceCatalog, string.Empty);

            var migratedGroups = new CharacterTestRuntimeAiPlannerBehaviorGroupRevision[sourceCatalog.Groups.Count];
            for (int i = 0; i < migratedGroups.Length; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorGroupRevision source = sourceCatalog.Groups[i];
                migratedGroups[i] = new CharacterTestRuntimeAiPlannerBehaviorGroupRevision(
                    source.StableGroupId,
                    source.Revision,
                    source.DisplayName,
                    source.Description,
                    source.Tags,
                    source.SortOrder,
                    source.Profile,
                    source.Cards);
            }
            var migratedCatalog = new CharacterTestRuntimeAiPlannerBehaviorGroupCatalog(
                document.CatalogId,
                document.Revision,
                migratedGroups);
            return new CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult(migratedCatalog, string.Empty);
        }

        /// <summary>
        /// Rebinds the fixed card identities in an existing project catalog to a newly generated
        /// card catalog while preserving every authored group and profile entry. This is used only
        /// by the explicit Editor recovery command when a behavior-card schema upgrade changes the
        /// semantic/content hashes of otherwise stable card revisions.
        /// </summary>
        /// <remarks>LowFreqAlloc: explicit Editor catalog recovery only.</remarks>
        internal static CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult RebindCardReferences(
            string json,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog cardCatalog,
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog definitionCatalog)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Failed("待恢复的项目行为组目录 JSON 为空。");
            if (cardCatalog == null)
                return Failed("新的项目行为卡目录不可用，无法重绑行为组引用。");

            CatalogDocument document;
            try
            {
                document = JsonConvert.DeserializeObject<CatalogDocument>(json, Settings);
            }
            catch (JsonException exception)
            {
                return Failed("待恢复的项目行为组目录 JSON 解析失败：" + exception.Message);
            }
            if (document?.Groups == null || document.Groups.Length == 0)
                return Failed("待恢复的项目行为组目录没有行为组。");

            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog definitions =
                definitionCatalog ?? CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn();
            var groups = new List<CharacterTestRuntimeAiPlannerBehaviorGroupRevision>(document.Groups.Length);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < document.Groups.Length; i++)
            {
                GroupDocument source = document.Groups[i];
                string path = "groups[" + i + "]";
                if (source == null
                    || !CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.IsStableCardId(source.StableGroupId)
                    || source.Revision <= 0
                    || !identities.Add(source.StableGroupId + "@" + source.Revision))
                {
                    return Failed(path + " 的身份无效或重复。");
                }

                CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile = source.ToProfile();
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotReport responsibilityReport =
                    CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.Validate(profile.Entries, definitions);
                if (!responsibilityReport.Success)
                    return Failed(path + " 的行为职责归属无效：" + responsibilityReport.Message);
                CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(profile, definitions);
                if (!compile.Success)
                    return Failed(path + " 的运行时 Profile 无法编译：" + FirstDiagnostic(compile.Report));

                CardReferenceDocument[] sourceCards = source.Cards ?? Array.Empty<CardReferenceDocument>();
                var cards = new CharacterTestRuntimeAiPlannerBehaviorGroupCardReference[sourceCards.Length];
                var entryIds = new HashSet<string>(StringComparer.Ordinal);
                for (int j = 0; j < sourceCards.Length; j++)
                {
                    CardReferenceDocument sourceCard = sourceCards[j];
                    if (sourceCard == null || !entryIds.Add(sourceCard.EntryId ?? string.Empty))
                        return Failed(path + ".cards[" + j + "] 引用缺失或重复。");
                    if (!cardCatalog.TryGetExact(
                            sourceCard.StableCardId,
                            sourceCard.CardRevision,
                            out CharacterTestRuntimeAiPlannerPublishedBehaviorCard exact))
                    {
                        return Failed(path + ".cards[" + j + "] 在新行为卡目录中不存在。");
                    }
                    if (!TryFindEditableEntry(profile, sourceCard.EntryId, out CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry))
                        return Failed(path + ".cards[" + j + "] 没有对应行为条目。");
                    cards[j] = new CharacterTestRuntimeAiPlannerBehaviorGroupCardReference(
                        sourceCard.EntryId,
                        exact.StableCardId,
                        exact.Revision,
                        exact.SemanticHash,
                        exact.ContentHash,
                        !entry.Disabled,
                        entry.Order);
                }
                if (CountEditableEntries(profile, entryIds) != cards.Length)
                    return Failed(path + " 的行为条目无法完整重绑到行为卡目录。");

                groups.Add(new CharacterTestRuntimeAiPlannerBehaviorGroupRevision(
                    source.StableGroupId,
                    source.Revision,
                    source.DisplayName,
                    source.Description,
                    source.Tags,
                    source.SortOrder,
                    profile,
                    cards));
            }

            var rebound = new CharacterTestRuntimeAiPlannerBehaviorGroupCatalog(
                string.IsNullOrWhiteSpace(document.CatalogId)
                    ? "character-test.runtime-ai-planner.project-behavior-groups"
                    : document.CatalogId,
                Math.Max(1, document.Revision),
                groups);
            return Load(ToJson(rebound), cardCatalog, definitions);
        }

        /// <summary>
        /// Imports one legacy behavior-group.v2 document into a one-revision catalog. Every
        /// editable entry must be resolved to an exact published card revision by the caller.
        /// </summary>
        /// <remarks>LowFreqAlloc: explicit legacy import and project recovery only.</remarks>
        public static CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult ImportLegacyV2(
            string legacyJson,
            string catalogId,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog cardCatalog,
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog definitionCatalog,
            Func<CharacterTestRuntimeAiPlannerBehaviorModuleEntry,
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard> resolveExactCard)
        {
            if (cardCatalog == null || resolveExactCard == null)
                return Failed("旧行为组导入需要项目行为卡目录和明确的固定版本映射。");
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog definitions =
                definitionCatalog ?? CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn();
            if (!CharacterTestRuntimeAiPlannerBehaviorGroupJson.TryLoad(
                    legacyJson,
                    definitions,
                    out CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
                    out CharacterTestRuntimeAiPlannerValidationReport report))
            {
                return Failed("旧 behavior-group.v2 导入失败：" + FirstDiagnostic(report));
            }

            var cards = new List<CharacterTestRuntimeAiPlannerBehaviorGroupCardReference>();
            for (int i = 0; i < profile.Entries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = profile.Entries[i];
                if (!IsEditableCardEntry(entry))
                    continue;
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard exact = resolveExactCard(entry);
                if (exact == null
                    || !cardCatalog.TryGetExact(exact.StableCardId, exact.Revision, out CharacterTestRuntimeAiPlannerPublishedBehaviorCard catalogCard)
                    || catalogCard.SemanticHash != exact.SemanticHash
                    || catalogCard.ContentHash != exact.ContentHash)
                {
                    return Failed("旧行为组条目“" + entry.EntryId + "”没有可验证的固定行为卡版本映射。");
                }
                cards.Add(new CharacterTestRuntimeAiPlannerBehaviorGroupCardReference(
                    entry.EntryId,
                    catalogCard.StableCardId,
                    catalogCard.Revision,
                    catalogCard.SemanticHash,
                    catalogCard.ContentHash,
                    !entry.Disabled,
                    entry.Order));
            }

            var imported = new CharacterTestRuntimeAiPlannerBehaviorGroupRevision(
                profile.GroupId,
                1,
                profile.DisplayName,
                "Imported from behavior-group.v2.",
                new[] { "legacy-import" },
                0,
                profile,
                cards);
            var catalog = new CharacterTestRuntimeAiPlannerBehaviorGroupCatalog(
                string.IsNullOrWhiteSpace(catalogId)
                    ? "character-test.runtime-ai-planner.imported-behavior-groups"
                    : catalogId,
                1,
                new[] { imported });
            return Load(ToJson(catalog), cardCatalog, definitions);
        }

        private static int CountEditableEntries(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            ISet<string> referencedEntryIds)
        {
            int count = 0;
            for (int i = 0; profile != null && i < profile.Entries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = profile.Entries[i];
                if (entry != null
                    && (referencedEntryIds?.Contains(entry.EntryId) == true
                        || IsEditableCardEntry(entry)))
                    count++;
            }
            return count;
        }

        private static bool TryFindEditableEntry(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            string entryId,
            out CharacterTestRuntimeAiPlannerBehaviorModuleEntry found)
        {
            for (int i = 0; profile != null && i < profile.Entries.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = profile.Entries[i];
                if (entry != null
                    && string.Equals(entry.EntryId, entryId, StringComparison.Ordinal))
                {
                    found = entry;
                    return true;
                }
            }
            found = null;
            return false;
        }

        private static bool IsEditableCardEntry(CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry)
        {
            return entry != null
                && !string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                    StringComparison.Ordinal)
                && !string.Equals(
                    entry.BehaviorId,
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    StringComparison.Ordinal)
                && !CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.IsNoOpPlaceholder(
                    entry.BehaviorId);
        }

        private static string FirstDiagnostic(CharacterTestRuntimeAiPlannerValidationReport report)
        {
            return report != null && report.Diagnostics.Count > 0
                ? report.Diagnostics[0].Message
                : "未知错误";
        }

        private static CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult Failed(string diagnostic)
        {
            return new CharacterTestRuntimeAiPlannerBehaviorGroupCatalogLoadResult(null, diagnostic);
        }

        private sealed class CatalogDocument
        {
            public string Schema { get; set; }
            public string CatalogId { get; set; }
            public int Revision { get; set; }
            public long CatalogHash { get; set; }
            public GroupDocument[] Groups { get; set; }

            public static CatalogDocument FromCatalog(CharacterTestRuntimeAiPlannerBehaviorGroupCatalog catalog)
            {
                var groups = new GroupDocument[catalog.Groups.Count];
                for (int i = 0; i < groups.Length; i++)
                    groups[i] = GroupDocument.FromGroup(catalog.Groups[i]);
                return new CatalogDocument
                {
                    Schema = CharacterTestRuntimeAiPlannerBehaviorGroupCatalog.Schema,
                    CatalogId = catalog.CatalogId,
                    Revision = catalog.Revision,
                    CatalogHash = catalog.CatalogHash,
                    Groups = groups
                };
            }
        }

        private sealed class GroupDocument
        {
            public string StableGroupId { get; set; }
            public int Revision { get; set; }
            public long ContentHash { get; set; }
            public string DisplayName { get; set; }
            public string Description { get; set; }
            public string[] Tags { get; set; }
            public int SortOrder { get; set; }
            public CharacterTestAiTargetSelectionMode Targeting { get; set; }
            public float AttackRange { get; set; }
            public PerceptionDocument Perception { get; set; }
            public MovementDocument Movement { get; set; }
            public EntryDocument[] Entries { get; set; }
            public CardReferenceDocument[] Cards { get; set; }

            public static GroupDocument FromGroup(CharacterTestRuntimeAiPlannerBehaviorGroupRevision group)
            {
                var cards = new CardReferenceDocument[group.Cards.Count];
                for (int i = 0; i < cards.Length; i++)
                    cards[i] = CardReferenceDocument.FromReference(group.Cards[i]);
                var tags = new string[group.Tags.Count];
                for (int i = 0; i < tags.Length; i++)
                    tags[i] = group.Tags[i];
                return new GroupDocument
                {
                    StableGroupId = group.StableGroupId,
                    Revision = group.Revision,
                    ContentHash = group.ContentHash,
                    DisplayName = group.DisplayName,
                    Description = group.Description,
                    Tags = tags,
                    SortOrder = group.SortOrder,
                    Targeting = group.Profile.TargetSelectionMode,
                    AttackRange = group.Profile.AttackRange,
                    Perception = PerceptionDocument.FromSettings(group.Profile.PerceptionSettings),
                    Movement = MovementDocument.FromSettings(group.Profile.MovementSettings),
                    Entries = EntryDocument.FromEntries(group.Profile.Entries),
                    Cards = cards
                };
            }

            public CharacterTestRuntimeAiPlannerBehaviorCompositionProfile ToProfile()
            {
                var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile(
                    StableGroupId,
                    Targeting,
                    AttackRange,
                    Perception != null
                        ? Perception.ToSettings()
                        : CharacterTestRuntimeAiPlannerPerceptionSettings.CreateDefault(),
                    Movement != null
                        ? Movement.ToSettings()
                        : CharacterTestRuntimeAiPlannerMovementSettings.CreateDefault())
                {
                    DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? StableGroupId : DisplayName
                };
                for (int i = 0; Entries != null && i < Entries.Length; i++)
                {
                    if (Entries[i] != null)
                        profile.Entries.Add(Entries[i].ToEntry());
                }
                return profile;
            }
        }

        private sealed class EntryDocument
        {
            public string EntryId { get; set; }
            public int NumericId { get; set; }
            public int DefinitionNumericId { get; set; }
            public string BehaviorId { get; set; }
            public int Order { get; set; }
            public bool Disabled { get; set; }
            public CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] ResponsibilityKinds { get; set; }

            [JsonProperty("responsibilityKind")]
            public CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind? LegacyResponsibilityKind { get; set; }

            [JsonProperty("slotKind")]
            public CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind? LegacySlotKind { get; set; }

            public bool ShouldSerializeLegacySlotKind() => false;
            public bool ShouldSerializeLegacyResponsibilityKind() => false;
            public ParameterDocument[] ParameterOverrides { get; set; }

            public static EntryDocument[] FromEntries(
                IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries)
            {
                var result = new EntryDocument[entries?.Count ?? 0];
                for (int i = 0; i < result.Length; i++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = entries[i];
                    var parameters = new List<ParameterDocument>();
                    foreach (KeyValuePair<string, CharacterTestRuntimeAiPlannerBehaviorParameterValue> pair in entry.ParameterOverrides)
                        parameters.Add(ParameterDocument.FromValue(pair.Key, pair.Value));
                    parameters.Sort((left, right) => string.Compare(left.Key, right.Key, StringComparison.Ordinal));
                    result[i] = new EntryDocument
                    {
                        EntryId = entry.EntryId,
                        NumericId = entry.NumericId,
                        DefinitionNumericId = entry.DefinitionNumericId,
                        BehaviorId = entry.BehaviorId,
                        Order = entry.Order,
                        Disabled = entry.Disabled,
                        ResponsibilityKinds = CopyResponsibilities(entry.ResponsibilityKinds),
                        ParameterOverrides = parameters.ToArray()
                    };
                }
                return result;
            }

            public CharacterTestRuntimeAiPlannerBehaviorModuleEntry ToEntry()
            {
                var entry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                    BehaviorId,
                    !Disabled,
                    Order)
                {
                    EntryId = string.IsNullOrWhiteSpace(EntryId) ? BehaviorId : EntryId,
                    NumericId = NumericId,
                    DefinitionNumericId = DefinitionNumericId,
                    BehaviorId = BehaviorId ?? string.Empty,
                    Disabled = Disabled
                };
                entry.SetResponsibilities(
                    ResponsibilityKinds
                    ?? new[]
                    {
                        LegacyResponsibilityKind
                        ?? CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.ToResponsibility(
                            LegacySlotKind ?? CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Flexible)
                    });
                for (int i = 0; ParameterOverrides != null && i < ParameterOverrides.Length; i++)
                {
                    ParameterDocument parameter = ParameterOverrides[i];
                    if (parameter != null && !string.IsNullOrWhiteSpace(parameter.Key))
                        entry.ParameterOverrides[parameter.Key] = parameter.ToValue();
                }
                return entry;
            }

            private static CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] CopyResponsibilities(
                IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> source)
            {
                var result = new CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[source?.Count ?? 0];
                for (int i = 0; i < result.Length; i++)
                    result[i] = source[i];
                return result;
            }
        }

        private sealed class ParameterDocument
        {
            public string Key { get; set; }
            public CharacterTestRuntimeAiPlannerBehaviorParameterKind Kind { get; set; }
            public bool BoolValue { get; set; }
            public int IntValue { get; set; }
            public float FloatValue { get; set; }

            public static ParameterDocument FromValue(
                string key,
                CharacterTestRuntimeAiPlannerBehaviorParameterValue value)
            {
                return new ParameterDocument
                {
                    Key = key,
                    Kind = value.Kind,
                    BoolValue = value.BoolValue,
                    IntValue = value.IntValue,
                    FloatValue = value.FloatValue
                };
            }

            public CharacterTestRuntimeAiPlannerBehaviorParameterValue ToValue()
            {
                switch (Kind)
                {
                    case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Bool:
                        return CharacterTestRuntimeAiPlannerBehaviorParameterValue.Bool(BoolValue);
                    case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Int:
                        return CharacterTestRuntimeAiPlannerBehaviorParameterValue.Int(IntValue);
                    default:
                        return CharacterTestRuntimeAiPlannerBehaviorParameterValue.Float(FloatValue);
                }
            }
        }

        private sealed class PerceptionDocument
        {
            public float VisionRange { get; set; }
            public int TargetMemoryFrames { get; set; }
            public int LineOfSightObstacleLayerMask { get; set; }
            public float EnvironmentSampleRadius { get; set; }
            public float ThreatDistanceWeight { get; set; }

            public static PerceptionDocument FromSettings(CharacterTestRuntimeAiPlannerPerceptionSettings settings)
            {
                CharacterTestRuntimeAiPlannerPerceptionSettings source =
                    CharacterTestRuntimeAiPlannerPerceptionSettings.Normalize(settings);
                return new PerceptionDocument
                {
                    VisionRange = source.VisionRange,
                    TargetMemoryFrames = source.TargetMemoryFrames,
                    LineOfSightObstacleLayerMask = source.LineOfSightObstacleLayerMask,
                    EnvironmentSampleRadius = source.EnvironmentSampleRadius,
                    ThreatDistanceWeight = source.ThreatDistanceWeight
                };
            }

            public CharacterTestRuntimeAiPlannerPerceptionSettings ToSettings()
            {
                return new CharacterTestRuntimeAiPlannerPerceptionSettings(
                    VisionRange,
                    TargetMemoryFrames,
                    LineOfSightObstacleLayerMask,
                    EnvironmentSampleRadius,
                    ThreatDistanceWeight);
            }
        }

        private sealed class MovementDocument
        {
            public float SpeedScale { get; set; }
            public float SteeringResponsiveness { get; set; }
            public float ApproachLateralBlend { get; set; }
            public float PreferredDistanceMin { get; set; }
            public float PreferredDistanceMax { get; set; }
            public float ArrivalSlowdownDistance { get; set; }
            public int StrafeSwitchMinFrames { get; set; }
            public int StrafeSwitchMaxFrames { get; set; }
            public int DirectionHoldFrames { get; set; }
            public int ReactionDelayFrames { get; set; }

            public static MovementDocument FromSettings(CharacterTestRuntimeAiPlannerMovementSettings settings)
            {
                CharacterTestRuntimeAiPlannerMovementSettings source =
                    CharacterTestRuntimeAiPlannerMovementSettings.Normalize(settings);
                return new MovementDocument
                {
                    SpeedScale = source.SpeedScale,
                    SteeringResponsiveness = source.SteeringResponsiveness,
                    ApproachLateralBlend = source.ApproachLateralBlend,
                    PreferredDistanceMin = source.PreferredDistanceMin,
                    PreferredDistanceMax = source.PreferredDistanceMax,
                    ArrivalSlowdownDistance = source.ArrivalSlowdownDistance,
                    StrafeSwitchMinFrames = source.StrafeSwitchMinFrames,
                    StrafeSwitchMaxFrames = source.StrafeSwitchMaxFrames,
                    DirectionHoldFrames = source.DirectionHoldFrames,
                    ReactionDelayFrames = source.ReactionDelayFrames
                };
            }

            public CharacterTestRuntimeAiPlannerMovementSettings ToSettings()
            {
                return new CharacterTestRuntimeAiPlannerMovementSettings(
                    SpeedScale,
                    SteeringResponsiveness,
                    ApproachLateralBlend,
                    PreferredDistanceMin,
                    PreferredDistanceMax,
                    ArrivalSlowdownDistance,
                    StrafeSwitchMinFrames,
                    StrafeSwitchMaxFrames,
                    DirectionHoldFrames,
                    ReactionDelayFrames);
            }
        }

        private sealed class CardReferenceDocument
        {
            public string EntryId { get; set; }
            public string StableCardId { get; set; }
            public int CardRevision { get; set; }
            public long SemanticHash { get; set; }
            public long ContentHash { get; set; }
            public bool Enabled { get; set; }
            public int Order { get; set; }

            public static CardReferenceDocument FromReference(CharacterTestRuntimeAiPlannerBehaviorGroupCardReference card)
            {
                return new CardReferenceDocument
                {
                    EntryId = card.EntryId,
                    StableCardId = card.StableCardId,
                    CardRevision = card.CardRevision,
                    SemanticHash = card.SemanticHash,
                    ContentHash = card.ContentHash,
                    Enabled = card.Enabled,
                    Order = card.Order
                };
            }
        }
    }
}
