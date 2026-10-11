using System;
using System.Collections.Generic;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestRuntimeAiPlannerPublishedBehaviorCardNumericIdentity
    {
        internal static Func<string, int> AuthoringAllocator { get; set; }

        internal static int AllocateForAuthoring(string stableCardId)
        {
            string key = "behavior-card/" + (stableCardId?.Trim() ?? string.Empty);
            int value = AuthoringAllocator?.Invoke(key) ?? 0;
            if (value <= 0)
                throw new InvalidOperationException("Published behavior card numeric identity allocator is unavailable.");
            return value;
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata
    {
        private readonly IReadOnlyList<string> _tags;
        private readonly IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> _requiredCapabilities;
        private readonly IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> _responsibilities;

        public CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(
            string displayName,
            string iconKey,
            int cost,
            string shortDescription,
            string useSummary,
            string riskSummary,
            IEnumerable<string> tags = null,
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorCapability> requiredCapabilities = null,
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities = null,
            float cooldownSeconds = 0f)
        {
            DisplayName = displayName?.Trim() ?? string.Empty;
            IconKey = iconKey?.Trim() ?? string.Empty;
            Cost = cost; CooldownSeconds = cooldownSeconds;
            ShortDescription = shortDescription?.Trim() ?? string.Empty;
            UseSummary = useSummary?.Trim() ?? string.Empty;
            RiskSummary = riskSummary?.Trim() ?? string.Empty;
            _tags = Array.AsReadOnly(CopyTags(tags));
            _requiredCapabilities = Array.AsReadOnly(CopyCapabilities(requiredCapabilities));
            _responsibilities = Array.AsReadOnly(CopyResponsibilities(responsibilities));
        }

        public string DisplayName { get; }
        public string IconKey { get; }
        public int Cost { get; }
        public float CooldownSeconds { get; }
        public string ShortDescription { get; }
        public string UseSummary { get; }
        public string RiskSummary { get; }
        public IReadOnlyList<string> Tags => _tags;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> RequiredCapabilities =>
            _requiredCapabilities;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> Responsibilities =>
            _responsibilities;

        public CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata Clone()
        {
            return new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(
                DisplayName,
                IconKey,
                Cost,
                ShortDescription,
                UseSummary,
                RiskSummary,
                _tags,
                _requiredCapabilities,
                _responsibilities, CooldownSeconds);
        }

        private static string[] CopyTags(IEnumerable<string> source)
        {
            if (source == null)
                return Array.Empty<string>();

            var result = new List<string>();
            foreach (string value in source)
            {
                string normalized = value?.Trim() ?? string.Empty;
                if (normalized.Length > 0)
                    result.Add(normalized);
            }
            string[] normalizedTags = result.ToArray();
            Array.Sort(normalizedTags, StringComparer.Ordinal);
            return normalizedTags;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCapability[] CopyCapabilities(
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorCapability> source)
        {
            if (source == null)
                return Array.Empty<CharacterTestRuntimeAiPlannerBehaviorCapability>();

            var result = new List<CharacterTestRuntimeAiPlannerBehaviorCapability>();
            foreach (CharacterTestRuntimeAiPlannerBehaviorCapability value in source)
                result.Add(value);
            CharacterTestRuntimeAiPlannerBehaviorCapability[] normalizedCapabilities = result.ToArray();
            Array.Sort(normalizedCapabilities);
            return normalizedCapabilities;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] CopyResponsibilities(
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> source)
        {
            var result = new List<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind>();
            if (source != null)
            {
                foreach (CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind value in source)
                    result.Add(value);
            }
            if (result.Count == 0)
                result.Add(CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other);
            CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[] normalized = result.ToArray();
            Array.Sort(normalized);
            return normalized;
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerPublishedBehaviorCard
    {
        private readonly CharacterTestRuntimeAiLabBehaviorCardLogicPayload _logic;

        public CharacterTestRuntimeAiPlannerPublishedBehaviorCard(
            string stableCardId,
            int revision,
            int minimumRuntimeContractVersion,
            int maximumRuntimeContractVersion,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata metadata,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic,
            long semanticHash,
            long contentHash,
            long legacySemanticHash = 0L,
            long legacyContentHash = 0L,
            CharacterTestTacticalPublishedSource tacticalSource = null,
            int numericId = 0)
        {
            StableCardId = stableCardId?.Trim() ?? string.Empty;
            Revision = revision;
            MinimumRuntimeContractVersion = minimumRuntimeContractVersion;
            MaximumRuntimeContractVersion = maximumRuntimeContractVersion;
            Metadata = metadata?.Clone();
            _logic = logic?.Clone();
            TacticalSource = tacticalSource;
            SemanticHash = semanticHash;
            ContentHash = contentHash;
            LegacySemanticHash = legacySemanticHash;
            LegacyContentHash = legacyContentHash;
            NumericId = numericId;
        }

        public string StableCardId { get; }
        public int Revision { get; }
        public int MinimumRuntimeContractVersion { get; }
        public int MaximumRuntimeContractVersion { get; }
        public CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata Metadata { get; }
        public long SemanticHash { get; }
        public long ContentHash { get; }
        public long LegacySemanticHash { get; }
        public long LegacyContentHash { get; }
        public int NumericId { get; }
        internal CharacterTestTacticalPublishedSource TacticalSource { get; }
        public string LogicSchema => TacticalSource != null ? CharacterTestTacticalPublishedSource.Schema : CharacterTestRuntimeAiLabBehaviorCardLogicPayload.Schema;

        public bool MatchesPinnedHash(long semanticHash, long contentHash) =>
            (SemanticHash == semanticHash && ContentHash == contentHash)
            || (LegacySemanticHash != 0L
                && LegacySemanticHash == semanticHash
                && LegacyContentHash == contentHash);

        /// <remarks>LowFreqAlloc: returns an authoring/bootstrap copy so published content stays immutable.</remarks>
        public CharacterTestRuntimeAiLabBehaviorCardLogicPayload CloneLogic()
        {
            return _logic?.Clone();
        }

        public bool IsCompatibleWith(int runtimeContractVersion)
        {
            return runtimeContractVersion >= MinimumRuntimeContractVersion
                && runtimeContractVersion <= MaximumRuntimeContractVersion;
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest
    {
        public CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest(
            string stableCardId,
            int revision,
            int minimumRuntimeContractVersion,
            int maximumRuntimeContractVersion,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata metadata,
            int numericId = 0)
        {
            StableCardId = stableCardId?.Trim() ?? string.Empty;
            Revision = revision;
            MinimumRuntimeContractVersion = minimumRuntimeContractVersion;
            MaximumRuntimeContractVersion = maximumRuntimeContractVersion;
            Metadata = metadata?.Clone();
            NumericId = numericId;
        }

        public string StableCardId { get; }
        public int Revision { get; }
        public int MinimumRuntimeContractVersion { get; }
        public int MaximumRuntimeContractVersion { get; }
        public CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata Metadata { get; }
        public int NumericId { get; }
    }

    internal sealed class CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult
    {
        public CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard card,
            CharacterTestRuntimeAiLabCompiledLogicProgram program,
            CharacterTestRuntimeAiLabCardDiagnostic[] diagnostics)
        {
            Card = card;
            Program = program;
            Diagnostics = Array.AsReadOnly(
                diagnostics ?? Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>());
        }

        public CharacterTestRuntimeAiPlannerPublishedBehaviorCard Card { get; }
        public CharacterTestRuntimeAiLabCompiledLogicProgram Program { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> Diagnostics { get; }
        public bool Success => Card != null
            && (Program != null || Card.TacticalSource != null)
            && !CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(Diagnostics);
    }

    internal static partial class CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher
    {
        public const int LegacyRootDistanceRuntimeContractVersion = 6;
        public const int LegacyObjectScopeRuntimeContractVersion = 7;
        public const int CurrentRuntimeContractVersion = 8;
        public const int MaximumPlayerCardCost = 10;

        /// <remarks>LowFreqAlloc: explicit publish validation and immutable asset creation.</remarks>
        public static CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult ValidateAndCreate(
            CharacterTestRuntimeAiLabBehaviorCardDraft draft,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishRequest request,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult authoredCompilation = null)
        {
            var diagnostics = new List<CharacterTestRuntimeAiLabCardDiagnostic>();
            if (draft == null)
            {
                Error(diagnostics, "PUBLISHED_CARD_DRAFT_MISSING", "$.draft", "Behavior card draft is required.");
                return Failed(diagnostics);
            }

            if (draft.HasAuthoredSource)
                return PublishTactical(draft, request, authoredCompilation);
            if (draft.LogicPayload == null)
            {
                Error(diagnostics, "PUBLISHED_CARD_LOGIC_MISSING", "$.logic", "Behavior recipe v11 payload is required.");
                return Failed(diagnostics);
            }

            if (request == null)
            {
                Error(diagnostics, "PUBLISHED_CARD_REQUEST_MISSING", "$", "Publish request is required.");
                return Failed(diagnostics);
            }
            if (references == null)
            {
                Error(
                    diagnostics,
                    "PUBLISHED_CARD_REFERENCES_MISSING",
                    "$.references",
                    "Authoritative Ability and CharacterAction references are required for publishing.");
            }

            ValidateEnvelope(
                request.StableCardId,
                request.Revision,
                request.MinimumRuntimeContractVersion,
                request.MaximumRuntimeContractVersion,
                request.Metadata,
                diagnostics);
            if (CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(diagnostics))
                return Failed(diagnostics);

            CharacterTestRuntimeAiLabBehaviorCardLogicPayload normalizedLogic = draft.LogicPayload.Clone();
            CharacterTestRuntimeAiLabBehaviorRecipeV11Migration.SynchronizeForWrite(normalizedLogic);
            CharacterTestRuntimeAiLabBehaviorRecipeV6Compiler.SynchronizeCharacterActionEventsForPublication(
                normalizedLogic,
                references);
            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(normalizedLogic, references);
            AddDiagnostics(diagnostics, compile.Diagnostics);
            if (!compile.Success)
                return Failed(diagnostics);

            CharacterTestRuntimeAiPlannerPublishedBehaviorCard card = CreateValidatedCard(
                request.StableCardId,
                request.Revision,
                request.MinimumRuntimeContractVersion,
                request.MaximumRuntimeContractVersion,
                request.Metadata,
                request.NumericId,
                normalizedLogic,
                compile.Program.SemanticHash);
            return new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult(
                card,
                compile.Program,
                diagnostics.ToArray());
        }

        /// <remarks>LowFreqAlloc: catalog load validation and immutable asset creation.</remarks>
        internal static CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult Restore(
            string stableCardId,
            int revision,
            int minimumRuntimeContractVersion,
            int maximumRuntimeContractVersion,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata metadata,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic,
            long expectedSemanticHash,
            long expectedContentHash,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            CharacterTestTacticalPublishedSource tacticalSource = null,
            int numericId = 0)
        {
            if (tacticalSource != null)
                return RestoreTactical(stableCardId, revision, minimumRuntimeContractVersion, maximumRuntimeContractVersion,
                    metadata, tacticalSource, expectedSemanticHash, expectedContentHash, numericId);
            var diagnostics = new List<CharacterTestRuntimeAiLabCardDiagnostic>();
            ValidateEnvelope(
                stableCardId,
                revision,
                minimumRuntimeContractVersion,
                maximumRuntimeContractVersion,
                metadata,
                diagnostics);
            if (references == null)
            {
                Error(
                    diagnostics,
                    "PUBLISHED_CARD_REFERENCES_MISSING",
                    "$.references",
                    "Authoritative Ability and CharacterAction references are required for catalog restore.");
            }
            if (logic == null)
                Error(diagnostics, "PUBLISHED_CARD_LOGIC_MISSING", "$.logic", "Behavior recipe v8 payload is required.");
            if (CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(diagnostics))
                return Failed(diagnostics);

            CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult compile =
                CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(logic, references);
            AddDiagnostics(diagnostics, compile.Diagnostics);
            if (!compile.Success)
                return Failed(diagnostics);

            CharacterTestRuntimeAiPlannerPublishedBehaviorCard card = CreateValidatedCard(
                stableCardId,
                revision,
                minimumRuntimeContractVersion,
                maximumRuntimeContractVersion,
                metadata,
                numericId,
                logic,
                compile.Program.SemanticHash);
            if (expectedSemanticHash != card.SemanticHash)
            {
                Error(diagnostics, "PUBLISHED_CARD_SEMANTIC_HASH_MISMATCH", "$.semanticHash", "Published behavior card semantic hash does not match its behavior recipe v11 payload.");
            }
            if (expectedContentHash != card.ContentHash)
            {
                Error(diagnostics, "PUBLISHED_CARD_CONTENT_HASH_MISMATCH", "$.contentHash", "Published behavior card content hash does not match its immutable content.");
            }
            if (CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(diagnostics))
                return Failed(diagnostics);

            return new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult(
                card,
                compile.Program,
                diagnostics.ToArray());
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCard CreateValidatedCard(
            string stableCardId,
            int revision,
            int minimumRuntimeContractVersion,
            int maximumRuntimeContractVersion,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata metadata,
            int numericId,
            CharacterTestRuntimeAiLabBehaviorCardLogicPayload logic,
            long semanticHash)
        {
            string normalizedStableCardId = stableCardId?.Trim() ?? string.Empty;
            long contentHash = ComputeContentHash(
                normalizedStableCardId,
                revision,
                minimumRuntimeContractVersion,
                maximumRuntimeContractVersion,
                metadata,
                semanticHash);
            return new CharacterTestRuntimeAiPlannerPublishedBehaviorCard(
                normalizedStableCardId,
                revision,
                minimumRuntimeContractVersion,
                maximumRuntimeContractVersion,
                metadata,
                logic,
                semanticHash,
                contentHash,
                numericId: numericId);
        }

        internal static long ComputeContentHash(
            string stableCardId,
            int revision,
            int minimumRuntimeContractVersion,
            int maximumRuntimeContractVersion,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata metadata,
            long semanticHash)
        {
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable("publishedCard.schema", CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.Schema);
            hash.AddStringStable("publishedCard.stableId", stableCardId);
            hash.AddInt("publishedCard.revision", revision);
            hash.AddInt("publishedCard.runtimeMin", minimumRuntimeContractVersion);
            hash.AddInt("publishedCard.runtimeMax", maximumRuntimeContractVersion);
            hash.AddLong("publishedCard.semanticHash", semanticHash);
            hash.AddStringStable("publishedCard.displayName", metadata.DisplayName);
            hash.AddStringStable("publishedCard.iconKey", metadata.IconKey);
            hash.AddInt("publishedCard.cost", metadata.Cost);
            if (metadata.CooldownSeconds != 0f)
                hash.AddStringStable("publishedCard.cooldownSeconds", metadata.CooldownSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            hash.AddStringStable("publishedCard.shortDescription", metadata.ShortDescription);
            hash.AddStringStable("publishedCard.useSummary", metadata.UseSummary);
            hash.AddStringStable("publishedCard.riskSummary", metadata.RiskSummary);
            hash.AddInt("publishedCard.tagCount", metadata.Tags.Count);
            for (int i = 0; i < metadata.Tags.Count; i++)
                hash.AddStringStable("publishedCard.tag", metadata.Tags[i]);
            hash.AddInt("publishedCard.capabilityCount", metadata.RequiredCapabilities.Count);
            for (int i = 0; i < metadata.RequiredCapabilities.Count; i++)
                hash.AddInt("publishedCard.capability", (int)metadata.RequiredCapabilities[i]);
            // `Other` is the historical implicit default. Keeping that exact value hash-neutral
            // lets existing authenticated card files acquire the explicit field without changing
            // identity; every authored non-default responsibility remains content-addressed.
            if (metadata.Responsibilities.Count != 1
                || metadata.Responsibilities[0]
                    != CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Other)
            {
                hash.AddInt("publishedCard.responsibilityCount", metadata.Responsibilities.Count);
                for (int i = 0; i < metadata.Responsibilities.Count; i++)
                    hash.AddInt("publishedCard.responsibility", (int)metadata.Responsibilities[i]);
            }
            return hash.ToHash();
        }

        internal static long ComputeContentHashWithoutResponsibilities(
            string stableCardId,
            int revision,
            int minimumRuntimeContractVersion,
            int maximumRuntimeContractVersion,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata metadata,
            long semanticHash)
        {
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable("publishedCard.schema", CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.Schema);
            hash.AddStringStable("publishedCard.stableId", stableCardId);
            hash.AddInt("publishedCard.revision", revision);
            hash.AddInt("publishedCard.runtimeMin", minimumRuntimeContractVersion);
            hash.AddInt("publishedCard.runtimeMax", maximumRuntimeContractVersion);
            hash.AddLong("publishedCard.semanticHash", semanticHash);
            hash.AddStringStable("publishedCard.displayName", metadata.DisplayName);
            hash.AddStringStable("publishedCard.iconKey", metadata.IconKey);
            hash.AddInt("publishedCard.cost", metadata.Cost);
            if (metadata.CooldownSeconds != 0f)
                hash.AddStringStable("publishedCard.cooldownSeconds", metadata.CooldownSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            hash.AddStringStable("publishedCard.shortDescription", metadata.ShortDescription);
            hash.AddStringStable("publishedCard.useSummary", metadata.UseSummary);
            hash.AddStringStable("publishedCard.riskSummary", metadata.RiskSummary);
            hash.AddInt("publishedCard.tagCount", metadata.Tags.Count);
            for (int i = 0; i < metadata.Tags.Count; i++)
                hash.AddStringStable("publishedCard.tag", metadata.Tags[i]);
            hash.AddInt("publishedCard.capabilityCount", metadata.RequiredCapabilities.Count);
            for (int i = 0; i < metadata.RequiredCapabilities.Count; i++)
                hash.AddInt("publishedCard.capability", (int)metadata.RequiredCapabilities[i]);
            return hash.ToHash();
        }

        private static void ValidateEnvelope(
            string stableCardId,
            int revision,
            int minimumRuntimeContractVersion,
            int maximumRuntimeContractVersion,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata metadata,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            if (!IsStableCardId(stableCardId))
                Error(diagnostics, "PUBLISHED_CARD_STABLE_ID_INVALID", "$.stableCardId", "Stable card id must use lower-case letters, digits, '.', '_' or '-'.");
            if (revision <= 0)
                Error(diagnostics, "PUBLISHED_CARD_REVISION_INVALID", "$.revision", "Published revision must be positive.");
            if (minimumRuntimeContractVersion <= 0)
                Error(diagnostics, "PUBLISHED_CARD_RUNTIME_MIN_INVALID", "$.minimumRuntimeContractVersion", "Minimum runtime contract version must be positive.");
            if (maximumRuntimeContractVersion < minimumRuntimeContractVersion)
                Error(diagnostics, "PUBLISHED_CARD_RUNTIME_RANGE_INVALID", "$.maximumRuntimeContractVersion", "Maximum runtime contract version must be greater than or equal to minimum.");
            if (metadata == null)
            {
                Error(diagnostics, "PUBLISHED_CARD_METADATA_MISSING", "$.metadata", "Player-facing metadata is required.");
                return;
            }
            if (string.IsNullOrWhiteSpace(metadata.DisplayName))
                Error(diagnostics, "PUBLISHED_CARD_DISPLAY_NAME_MISSING", "$.metadata.displayName", "Player-facing display name is required.");
            if (string.IsNullOrWhiteSpace(metadata.IconKey))
                Error(diagnostics, "PUBLISHED_CARD_ICON_MISSING", "$.metadata.iconKey", "Player-facing icon key is required.");
            if (float.IsNaN(metadata.CooldownSeconds) || float.IsInfinity(metadata.CooldownSeconds) || metadata.CooldownSeconds < 0f
                || metadata.CooldownSeconds * 60d >= int.MaxValue)
                Error(diagnostics, "PUBLISHED_CARD_COOLDOWN_INVALID", "$.metadata.cooldownSeconds", "Cooldown must be finite, nonnegative and fit the authority frame range.");
            if (metadata.Cost < 0 || metadata.Cost > MaximumPlayerCardCost)
                Error(diagnostics, "PUBLISHED_CARD_COST_INVALID", "$.metadata.cost", "Card cost must be between 0 and 10.");
            if (string.IsNullOrWhiteSpace(metadata.ShortDescription))
                Error(diagnostics, "PUBLISHED_CARD_DESCRIPTION_MISSING", "$.metadata.shortDescription", "Player-facing short description is required.");

            var tags = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < metadata.Tags.Count; i++)
            {
                if (!tags.Add(metadata.Tags[i]))
                    Error(diagnostics, "PUBLISHED_CARD_TAG_DUPLICATE", "$.metadata.tags[" + i + "]", "Player-facing tags must be unique.");
            }
            var capabilities = new HashSet<CharacterTestRuntimeAiPlannerBehaviorCapability>();
            for (int i = 0; i < metadata.RequiredCapabilities.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCapability capability = metadata.RequiredCapabilities[i];
                if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiPlannerBehaviorCapability), capability)
                    || capability == CharacterTestRuntimeAiPlannerBehaviorCapability.None)
                {
                    Error(diagnostics, "PUBLISHED_CARD_CAPABILITY_INVALID", "$.metadata.requiredCapabilities[" + i + "]", "Required capability is invalid.");
                }
                else if (!capabilities.Add(capability))
                {
                    Error(diagnostics, "PUBLISHED_CARD_CAPABILITY_DUPLICATE", "$.metadata.requiredCapabilities[" + i + "]", "Required capabilities must be unique.");
                }
            }
            var responsibilities = new HashSet<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind>();
            for (int i = 0; i < metadata.Responsibilities.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind responsibility =
                    metadata.Responsibilities[i];
                if (!Enum.IsDefined(typeof(CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind), responsibility))
                {
                    Error(diagnostics, "PUBLISHED_CARD_RESPONSIBILITY_INVALID", "$.metadata.responsibilities[" + i + "]", "Behavior-card responsibility is invalid.");
                }
                else if (!responsibilities.Add(responsibility))
                {
                    Error(diagnostics, "PUBLISHED_CARD_RESPONSIBILITY_DUPLICATE", "$.metadata.responsibilities[" + i + "]", "Behavior-card responsibilities must be unique.");
                }
            }
        }

        internal static bool IsStableCardId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            string normalized = value.Trim();
            char first = normalized[0];
            if (!((first >= 'a' && first <= 'z') || (first >= '0' && first <= '9')))
                return false;
            for (int i = 0; i < normalized.Length; i++)
            {
                char character = normalized[i];
                bool valid = character >= 'a' && character <= 'z'
                    || character >= '0' && character <= '9'
                    || character == '.'
                    || character == '_'
                    || character == '-';
                if (!valid)
                    return false;
            }
            return true;
        }

        private static void AddDiagnostics(
            List<CharacterTestRuntimeAiLabCardDiagnostic> destination,
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> source)
        {
            for (int i = 0; i < source.Count; i++)
                destination.Add(source[i]);
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult Failed(
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            return new CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult(
                null,
                null,
                diagnostics.ToArray());
        }

        private static void Error(
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            diagnostics.Add(new CharacterTestRuntimeAiLabCardDiagnostic(
                CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                code,
                path,
                message));
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult
    {
        public CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            CharacterTestRuntimeAiLabCardDiagnostic[] diagnostics)
        {
            Catalog = catalog;
            Diagnostics = Array.AsReadOnly(
                diagnostics ?? Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>());
        }

        public CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog Catalog { get; }
        public IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> Diagnostics { get; }
        public bool Success => Catalog != null
            && !CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(Diagnostics);
    }

    internal sealed class CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog
    {
        public const string Schema = "mx.character-test.runtime-ai-planner.published-behavior-card-catalog.v1";
        public const int CardNumericDomainId = 417;
        public const int CatalogNumericDomainId = 418;
        public const int DefaultCatalogNumericId = 1;

        private readonly CharacterTestRuntimeAiPlannerPublishedBehaviorCard[] _cards;
        private readonly IReadOnlyList<CharacterTestRuntimeAiPlannerPublishedBehaviorCard> _readOnlyCards;

        private CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog(
            string catalogId,
            int revision,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard[] cards,
            int catalogNumericId = DefaultCatalogNumericId)
        {
            CatalogId = catalogId?.Trim() ?? string.Empty;
            Revision = revision;
            CatalogNumericId = catalogNumericId;
            _cards = cards;
            _readOnlyCards = Array.AsReadOnly(_cards);
            CatalogHash = ComputeCatalogHash(CatalogId, Revision, _cards);
            NumericIdentityHash = ComputeNumericIdentityHash(Revision, CatalogNumericId, _cards);
        }

        /// <remarks>LowFreqAlloc: catalog authoring/bootstrap validation only.</remarks>
        public static CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult ValidateAndCreate(
            string catalogId,
            int revision,
            IEnumerable<CharacterTestRuntimeAiPlannerPublishedBehaviorCard> cards,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references,
            int catalogNumericId = DefaultCatalogNumericId)
        {
            var diagnostics = new List<CharacterTestRuntimeAiLabCardDiagnostic>();
            string normalizedCatalogId = catalogId?.Trim() ?? string.Empty;
            if (normalizedCatalogId.Length == 0)
                Error(diagnostics, "PUBLISHED_CARD_CATALOG_ID_MISSING", "$.catalogId", "Published behavior card catalog id is required.");
            if (revision <= 0)
                Error(diagnostics, "PUBLISHED_CARD_CATALOG_REVISION_INVALID", "$.revision", "Published behavior card catalog revision must be positive.");
            if (catalogNumericId <= 0 || !CharacterTestAuthoredContentIds.IsActive(CatalogNumericDomainId, catalogNumericId))
                Error(diagnostics, "PUBLISHED_CARD_CATALOG_NUMERIC_ID_INVALID", "$.catalogNumericId", "Published behavior card catalog numeric identity is unknown or retired.");
            if (references == null)
                Error(diagnostics, "PUBLISHED_CARD_REFERENCES_MISSING", "$.references", "Authoritative Ability and CharacterAction references are required for catalog creation.");

            CharacterTestRuntimeAiPlannerPublishedBehaviorCard[] suppliedCards =
                CopyAndSort(cards, diagnostics);
            for (int i = 1; i < suppliedCards.Length; i++)
            {
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard previous = suppliedCards[i - 1];
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard current = suppliedCards[i];
                if (previous.Revision == current.Revision
                    && string.Equals(previous.StableCardId, current.StableCardId, StringComparison.Ordinal))
                {
                    Error(
                        diagnostics,
                        "PUBLISHED_CARD_REVISION_DUPLICATE",
                        "$.cards[" + i + "]",
                        "Published behavior card stable id and revision must be unique.");
                }
            }
            var validatedCards = new List<CharacterTestRuntimeAiPlannerPublishedBehaviorCard>(
                suppliedCards.Length);
            if (references != null)
            {
                var numericIdsByStableCardId = new Dictionary<string, int>(StringComparer.Ordinal);
                var stableCardIdsByNumericId = new Dictionary<int, string>();
                for (int i = 0; i < suppliedCards.Length; i++)
                {
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCard supplied = suppliedCards[i];
                    string cardPath = "$.cards[" + i + "]";
                    if (supplied.NumericId <= 0 || !CharacterTestAuthoredContentIds.IsActive(CardNumericDomainId, supplied.NumericId))
                        Error(diagnostics, "PUBLISHED_CARD_NUMERIC_ID_INVALID", "$.cards[" + i + "].numericId", "Published behavior card numeric identity is unknown or retired.");
                    else
                    {
                        if (numericIdsByStableCardId.TryGetValue(supplied.StableCardId, out int existingNumericId)
                            && existingNumericId != supplied.NumericId)
                        {
                            Error(
                                diagnostics,
                                "PUBLISHED_CARD_NUMERIC_ID_CHANGED",
                                cardPath + ".numericId",
                                "Published behavior card numeric identity must remain stable across revisions.");
                        }
                        else if (stableCardIdsByNumericId.TryGetValue(supplied.NumericId, out string existingStableCardId)
                            && !string.Equals(existingStableCardId, supplied.StableCardId, StringComparison.Ordinal))
                        {
                            Error(
                                diagnostics,
                                "PUBLISHED_CARD_NUMERIC_ID_DUPLICATE",
                                cardPath + ".numericId",
                                "Published behavior card numeric identity must belong to only one stable card.");
                        }
                        else
                        {
                            numericIdsByStableCardId[supplied.StableCardId] = supplied.NumericId;
                            stableCardIdsByNumericId[supplied.NumericId] = supplied.StableCardId;
                        }
                    }
                    CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublishResult restored =
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.Restore(
                            supplied.StableCardId,
                            supplied.Revision,
                            supplied.MinimumRuntimeContractVersion,
                            supplied.MaximumRuntimeContractVersion,
                            supplied.Metadata,
                            supplied.CloneLogic(),
                            supplied.SemanticHash,
                            supplied.ContentHash,
                            references, supplied.TacticalSource, supplied.NumericId);
                    AddDiagnostics(diagnostics, restored.Diagnostics, "$.cards[" + i + "]");
                    if (restored.Success)
                    {
                        CharacterTestRuntimeAiPlannerPublishedBehaviorCard validated = restored.Card;
                        if (supplied.LegacySemanticHash != 0L)
                        {
                            validated = new CharacterTestRuntimeAiPlannerPublishedBehaviorCard(
                                validated.StableCardId,
                                validated.Revision,
                                validated.MinimumRuntimeContractVersion,
                                validated.MaximumRuntimeContractVersion,
                                validated.Metadata,
                                validated.CloneLogic(),
                                validated.SemanticHash,
                                validated.ContentHash,
                                supplied.LegacySemanticHash,
                                supplied.LegacyContentHash,
                                numericId: supplied.NumericId);
                        }
                        validatedCards.Add(validated);
                    }
                }
            }
            if (CharacterTestRuntimeAiLabBehaviorCardLogicValidator.HasErrors(diagnostics))
            {
                return new CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult(
                    null,
                    diagnostics.ToArray());
            }
            return new CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogCreateResult(
                new CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog(
                    normalizedCatalogId,
                    revision,
                    validatedCards.ToArray(),
                    catalogNumericId),
                diagnostics.ToArray());
        }

        public string CatalogId { get; }
        public int Revision { get; }
        public int CatalogNumericId { get; }
        public long CatalogHash { get; }
        public long NumericIdentityHash { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerPublishedBehaviorCard> Cards => _readOnlyCards;

        internal CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog CreateStableCardFileCatalog(
            string stableCardId)
        {
            string normalized = stableCardId?.Trim() ?? string.Empty;
            var cards = new List<CharacterTestRuntimeAiPlannerPublishedBehaviorCard>();
            for (int i = 0; i < _cards.Length; i++)
            {
                if (string.Equals(_cards[i].StableCardId, normalized, StringComparison.Ordinal))
                    cards.Add(_cards[i]);
            }
            return new CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog(
                CatalogId,
                Revision,
                cards.ToArray(),
                CatalogNumericId);
        }

        public bool TryResolveLatestCompatible(
            string stableCardId,
            int runtimeContractVersion,
            out CharacterTestRuntimeAiPlannerPublishedBehaviorCard card)
        {
            card = null;
            string normalized = stableCardId?.Trim() ?? string.Empty;
            for (int i = _cards.Length - 1; i >= 0; i--)
            {
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard candidate = _cards[i];
                int comparison = string.Compare(candidate.StableCardId, normalized, StringComparison.Ordinal);
                if (comparison > 0)
                    continue;
                if (comparison < 0)
                    break;
                if (candidate.IsCompatibleWith(runtimeContractVersion))
                {
                    card = candidate;
                    return true;
                }
            }
            return false;
        }

        public bool TryResolveLatestCompatible(
            int numericId,
            int runtimeContractVersion,
            out CharacterTestRuntimeAiPlannerPublishedBehaviorCard card)
        {
            card = null;
            for (int i = _cards.Length - 1; i >= 0; i--)
            {
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard candidate = _cards[i];
                if (candidate.NumericId != numericId)
                    continue;
                if (candidate.IsCompatibleWith(runtimeContractVersion))
                {
                    card = candidate;
                    return true;
                }
            }
            return false;
        }

        public bool TryGetExact(
            string stableCardId,
            int revision,
            out CharacterTestRuntimeAiPlannerPublishedBehaviorCard card)
        {
            string normalized = stableCardId?.Trim() ?? string.Empty;
            for (int i = 0; i < _cards.Length; i++)
            {
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard candidate = _cards[i];
                if (candidate.Revision == revision
                    && string.Equals(candidate.StableCardId, normalized, StringComparison.Ordinal))
                {
                    card = candidate;
                    return true;
                }
            }
            card = null;
            return false;
        }

        public bool TryGetExact(
            int numericId,
            int revision,
            out CharacterTestRuntimeAiPlannerPublishedBehaviorCard card)
        {
            for (int i = 0; i < _cards.Length; i++)
            {
                CharacterTestRuntimeAiPlannerPublishedBehaviorCard candidate = _cards[i];
                if (candidate.NumericId == numericId && candidate.Revision == revision)
                {
                    card = candidate;
                    return true;
                }
            }
            card = null;
            return false;
        }

        private static CharacterTestRuntimeAiPlannerPublishedBehaviorCard[] CopyAndSort(
            IEnumerable<CharacterTestRuntimeAiPlannerPublishedBehaviorCard> cards,
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics)
        {
            if (cards == null)
                return Array.Empty<CharacterTestRuntimeAiPlannerPublishedBehaviorCard>();
            var list = new List<CharacterTestRuntimeAiPlannerPublishedBehaviorCard>();
            int index = 0;
            foreach (CharacterTestRuntimeAiPlannerPublishedBehaviorCard card in cards)
            {
                if (card != null)
                    list.Add(card);
                else
                    Error(diagnostics, "PUBLISHED_CARD_DOCUMENT_MISSING", "$.cards[" + index + "]", "Published behavior card entry is missing.");
                index++;
            }
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard[] result = list.ToArray();
            Array.Sort(result, CompareCards);
            return result;
        }

        private static int CompareCards(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard left,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard right)
        {
            int stableId = string.Compare(left.StableCardId, right.StableCardId, StringComparison.Ordinal);
            return stableId != 0 ? stableId : left.Revision.CompareTo(right.Revision);
        }

        private static long ComputeCatalogHash(
            string catalogId,
            int revision,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard[] cards)
        {
            var hash = new RuntimeContentHashAccumulator();
            hash.AddStringStable("publishedCardCatalog.schema", Schema);
            hash.AddStringStable("publishedCardCatalog.catalogId", catalogId);
            hash.AddInt("publishedCardCatalog.revision", revision);
            hash.AddInt("publishedCardCatalog.cardCount", cards.Length);
            for (int i = 0; i < cards.Length; i++)
            {
                hash.AddStringStable("publishedCardCatalog.cardStableId", cards[i].StableCardId);
                hash.AddInt("publishedCardCatalog.cardRevision", cards[i].Revision);
                hash.AddLong("publishedCardCatalog.cardContentHash", cards[i].ContentHash);
            }
            return hash.ToHash();
        }

        private static long ComputeNumericIdentityHash(
            int revision,
            int catalogNumericId,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCard[] cards)
        {
            var hash = new RuntimeHashAccumulator(RuntimeHashSchemaIds.RuntimeAiPlannerSessionAuthority, 3);
            hash.AddInt(1, CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.CatalogNumericDomainId);
            hash.AddInt(2, catalogNumericId);
            hash.AddInt(3, CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.CardNumericDomainId);
            hash.AddInt(4, revision);
            hash.AddInt(5, cards.Length);
            for (int i = 0; i < cards.Length; i++)
            {
                hash.AddInt(6, i);
                hash.AddInt(7, cards[i].NumericId);
                hash.AddInt(8, cards[i].Revision);
            }
            return hash.ToHash();
        }

        private static void Error(
            List<CharacterTestRuntimeAiLabCardDiagnostic> diagnostics,
            string code,
            string path,
            string message)
        {
            diagnostics.Add(new CharacterTestRuntimeAiLabCardDiagnostic(
                CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                code,
                path,
                message));
        }

        private static void AddDiagnostics(
            List<CharacterTestRuntimeAiLabCardDiagnostic> destination,
            IReadOnlyList<CharacterTestRuntimeAiLabCardDiagnostic> source,
            string prefix)
        {
            for (int i = 0; i < source.Count; i++)
            {
                CharacterTestRuntimeAiLabCardDiagnostic item = source[i];
                string path = item.Path == "$"
                    ? prefix
                    : prefix + (item.Path.StartsWith("$.", StringComparison.Ordinal)
                        ? item.Path.Substring(1)
                        : ".logic");
                destination.Add(new CharacterTestRuntimeAiLabCardDiagnostic(
                    item.Severity,
                    item.Code,
                    path,
                    item.Message));
            }
        }
    }
}
