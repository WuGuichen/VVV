using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Runtime;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestArenaBehaviorGroupDraft
    {
        public CharacterTestArenaBehaviorGroupDraft(
            int draftId,
            string stableId,
            string displayName,
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            int revision = 1,
            int sourceProjectRevision = 0,
            long sourceProjectContentHash = 0)
        {
            DraftId = draftId > 0 ? draftId : CharacterTestArenaBehaviorGroupDraftStore.DefaultDraftId;
            StableId = string.IsNullOrWhiteSpace(stableId)
                ? "character-test.arena.behavior-group." + DraftId.ToString(CultureInfo.InvariantCulture)
                : stableId;
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? StableId
                : displayName;
            Profile = profile != null
                ? profile.Clone()
                : CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile();
            Profile.GroupId = StableId;
            Profile.DisplayName = DisplayName;
            Revision = revision < 0 ? 0 : revision;
            SourceProjectRevision = sourceProjectRevision;
            SourceProjectContentHash = sourceProjectContentHash;
        }

        public int DraftId { get; }
        public string StableId { get; }
        public string DisplayName { get; set; }
        public CharacterTestRuntimeAiPlannerBehaviorCompositionProfile Profile { get; private set; }
        public int Revision { get; private set; }
        public int SourceProjectRevision { get; private set; }
        public long SourceProjectContentHash { get; private set; }
        public bool HasProjectSource => SourceProjectRevision > 0 && SourceProjectContentHash != 0;

        public int EntryCount => Profile != null ? Profile.Entries.Count : 0;

        public CharacterTestArenaBehaviorGroupDraft Clone()
        {
            return new CharacterTestArenaBehaviorGroupDraft(
                DraftId,
                StableId,
                DisplayName,
                Profile,
                Revision,
                SourceProjectRevision,
                SourceProjectContentHash);
        }

        public void MarkEdited()
        {
            Revision++;
        }

        public void AdoptProjectRevision(CharacterTestRuntimeAiPlannerBehaviorGroupRevision source)
        {
            if (source == null || !string.Equals(source.StableGroupId, StableId, StringComparison.Ordinal))
                throw new ArgumentException("The project behavior group revision does not match this draft.", nameof(source));
            DisplayName = source.DisplayName;
            Profile = source.Profile.Clone();
            Profile.GroupId = StableId;
            Profile.DisplayName = DisplayName;
            SourceProjectRevision = source.Revision;
            SourceProjectContentHash = source.ContentHash;
            Revision++;
        }

        public void AdoptProjectBasicInfoRevision(
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision source)
        {
            if (source == null || !string.Equals(source.StableGroupId, StableId, StringComparison.Ordinal))
                throw new ArgumentException("The project behavior group revision does not match this draft.", nameof(source));
            DisplayName = source.DisplayName;
            Profile.DisplayName = DisplayName;
            SourceProjectRevision = source.Revision;
            SourceProjectContentHash = source.ContentHash;
            Revision++;
        }
    }

    internal sealed class CharacterTestArenaBehaviorGroupDraftStore
    {
        public const int DefaultDraftId = 1;
        public const int SessionLoadoutIdBase = 10000;
        public const int SessionVariantId = 1;

        internal const string TrainingDummyStableId = "arena.training-dummy";
        private const int EntryOrderStep = 10;
        private readonly List<CharacterTestArenaBehaviorGroupDraft> _drafts =
            new List<CharacterTestArenaBehaviorGroupDraft>(4);
        private readonly CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog _definitionCatalog;
        private int _nextDraftId = DefaultDraftId;

        private CharacterTestArenaBehaviorGroupDraftStore(
            CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog definitionCatalog)
        {
            _definitionCatalog =
                definitionCatalog ?? CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn();
        }

        public IReadOnlyList<CharacterTestArenaBehaviorGroupDraft> Drafts => _drafts;
        public CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog DefinitionCatalog => _definitionCatalog;
        public int Revision { get; private set; } = 1;

        public static CharacterTestArenaBehaviorGroupDraftStore CreateDefault(
            CharacterTestRuntimeAiPlannerProfile runtimeProfile = null)
        {
            var store = new CharacterTestArenaBehaviorGroupDraftStore(
                CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
            store.AddDraft(CreateDefaultMeleeProfile(runtimeProfile), "arena.default-melee", "Default melee");
            store.AddDraft(CreateKiteProfile(runtimeProfile), "arena.kite-survival", "Kite survival");
            store.AddDraft(CreateTrainingDummyProfile(runtimeProfile), "arena.training-dummy", "Training dummy");
            return store;
        }

        public static CharacterTestArenaBehaviorGroupDraftStore CreateEmpty()
        {
            return new CharacterTestArenaBehaviorGroupDraftStore(
                CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
        }

        public static CharacterTestArenaBehaviorGroupDraftStore CreateFromProjectCatalog(
            CharacterTestRuntimeAiPlannerBehaviorGroupCatalog catalog)
        {
            CharacterTestArenaBehaviorGroupDraftStore store = CreateEmpty();
            if (catalog == null)
                return store;
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision[] groups = catalog.GetLatestRevisions();
            for (int i = 0; i < groups.Length; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorGroupRevision group = groups[i];
                store.AddDraft(
                    group.Profile,
                    group.StableGroupId,
                    group.DisplayName,
                    group.Revision,
                    group.ContentHash);
            }
            return store;
        }

        public CharacterTestArenaBehaviorGroupDraftStore Clone()
        {
            var copy = new CharacterTestArenaBehaviorGroupDraftStore(_definitionCatalog)
            {
                _nextDraftId = _nextDraftId,
                Revision = Revision
            };
            for (int i = 0; i < _drafts.Count; i++)
                copy._drafts.Add(_drafts[i].Clone());
            return copy;
        }

        public int SelectNextDraft(int currentDraftId, int direction)
        {
            if (_drafts.Count == 0)
                return DefaultDraftId;

            int index = IndexOfDraft(currentDraftId);
            if (index < 0)
                index = 0;
            else
                index = WrapIndex(index + (direction < 0 ? -1 : 1), _drafts.Count);

            return _drafts[index].DraftId;
        }

        public bool TryGetDraft(int draftId, out CharacterTestArenaBehaviorGroupDraft draft)
        {
            for (int i = 0; i < _drafts.Count; i++)
            {
                if (_drafts[i].DraftId != draftId)
                    continue;

                draft = _drafts[i];
                return true;
            }

            draft = null;
            return false;
        }

        public bool TryAdoptProjectRevision(CharacterTestRuntimeAiPlannerBehaviorGroupRevision source)
        {
            if (source == null)
                return false;
            for (int i = 0; i < _drafts.Count; i++)
            {
                if (!string.Equals(_drafts[i].StableId, source.StableGroupId, StringComparison.Ordinal))
                    continue;
                _drafts[i].AdoptProjectRevision(source);
                Revision++;
                return true;
            }
            return false;
        }

        public bool TryAdoptProjectBasicInfoRevision(
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision source)
        {
            if (source == null)
                return false;
            for (int i = 0; i < _drafts.Count; i++)
            {
                if (!string.Equals(_drafts[i].StableId, source.StableGroupId, StringComparison.Ordinal))
                    continue;
                _drafts[i].AdoptProjectBasicInfoRevision(source);
                Revision++;
                return true;
            }
            return false;
        }

        internal bool TryAddProjectGroup(
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision source,
            out int draftId)
        {
            draftId = 0;
            if (source == null)
                return false;
            for (int i = 0; i < _drafts.Count; i++)
            {
                if (string.Equals(_drafts[i].StableId, source.StableGroupId, StringComparison.Ordinal))
                    return false;
            }

            CharacterTestArenaBehaviorGroupDraft draft = AddDraft(
                source.Profile,
                source.StableGroupId,
                source.DisplayName,
                source.Revision,
                source.ContentHash);
            draftId = draft.DraftId;
            return true;
        }

        internal bool TryRemoveProjectGroup(string stableGroupId, out int removedDraftId)
        {
            removedDraftId = 0;
            if (string.IsNullOrWhiteSpace(stableGroupId) || _drafts.Count <= 1)
                return false;
            for (int i = 0; i < _drafts.Count; i++)
            {
                if (!string.Equals(_drafts[i].StableId, stableGroupId, StringComparison.Ordinal))
                    continue;
                removedDraftId = _drafts[i].DraftId;
                _drafts.RemoveAt(i);
                Revision++;
                return true;
            }
            return false;
        }

        public CharacterTestArenaBehaviorGroupDraft GetDefaultDraft()
        {
            if (TryGetDraft(DefaultDraftId, out CharacterTestArenaBehaviorGroupDraft draft))
                return draft;
            return _drafts.Count > 0 ? _drafts[0] : null;
        }

        public bool TryGetDraftDisplay(
            int draftId,
            out string displayName,
            out string statusText,
            out bool compileSuccess)
        {
            displayName = "Unavailable behavior group";
            statusText = "Missing draft";
            compileSuccess = false;
            if (!TryGetDraft(draftId, out CharacterTestArenaBehaviorGroupDraft draft))
                return false;

            displayName = draft.DisplayName;
            CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(
                    draft.Profile,
                    _definitionCatalog);
            compileSuccess = compile.Success;
            statusText = compile.Success
                ? "Compile passed"
                : "Compile failed: " + CreateReportText(compile.Report);
            return true;
        }

        public bool TryCompileAll(out string message)
        {
            if (_drafts.Count == 0)
            {
                message = "行为组草稿为空。请重新选择行为组，或重新加载项目行为目录。";
                return false;
            }

            for (int i = 0; i < _drafts.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotReport contract =
                    CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.Validate(
                        _drafts[i].Profile.Entries,
                        _definitionCatalog);
                if (!contract.Success)
                {
                    message = "Arena behavior group contract failed: "
                        + _drafts[i].DisplayName
                        + " | "
                        + contract.Message;
                    return false;
                }
                CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(
                        _drafts[i].Profile,
                        _definitionCatalog);
                if (compile.Success)
                    continue;

                message = "Arena behavior group compile failed: "
                    + _drafts[i].DisplayName
                    + " | "
                    + CreateReportText(compile.Report);
                return false;
            }

            message = string.Empty;
            return true;
        }

        public CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime CreateRuntime(
            CharacterTestRuntimeAiPlannerProfile runtimeProfile,
            IReadOnlyList<CharacterTestRuntimeAiPlannerProfileBinding> existingProfileBindings,
            IRuntimeLogger logger)
        {
            IReadOnlyList<CharacterTestArenaBehaviorGroupDraft> source = _drafts;
            var loadouts =
                new CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                CharacterTestArenaBehaviorGroupDraft draft = source[i];
                CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile =
                    CreateRuntimeProfile(draft.StableId, draft.Profile, runtimeProfile);
                profile.GroupId = draft.StableId;
                profile.DisplayName = draft.DisplayName;
                var variant = new CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition(
                    SessionVariantId,
                    "arena-session",
                    "Arena session",
                    draft.StableId,
                    profile);
                loadouts[i] = new CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition(
                    ToSessionLoadoutId(draft.DraftId),
                    draft.StableId,
                    draft.DisplayName,
                    "Session-only Arena behavior group draft.",
                    new[] { variant },
                    new[] { "arena", "test" },
                    CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyKind.Assault,
                    draft.DisplayName,
                    CreateActionSummary(profile),
                    "Arena test session",
                    "Session-only draft; not saved to JSON.");
            }

            var catalog = new CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog(
                "character-test.arena.behavior-groups.session",
                Revision,
                loadouts);
            return CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime.Create(
                catalog,
                ResolveNextProfileSlot(existingProfileBindings),
                logger);
        }

        internal static CharacterTestRuntimeAiPlannerBehaviorCompositionProfile CreateRuntimeProfile(
            string stableId,
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile source,
            CharacterTestRuntimeAiPlannerProfile runtimeProfile = null)
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile = source != null
                ? source.Clone()
                : CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile(runtimeProfile);
            return profile;
        }

        private static void AddTrainingDummyAuthoredBehaviors(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile)
        {
            if (profile == null)
                return;

            profile.Entries.Add(CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.CreateEntry(
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Targeting,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack));
            profile.Entries.Add(CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.CreateEntry(
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback,
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle));
        }

        public bool TryResolveSelection(
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime runtime,
            int draftId,
            out CharacterTestRuntimeAiBehaviorSelection selection,
            out string message)
        {
            selection = CharacterTestRuntimeAiBehaviorSelection.None;
            if (runtime == null)
            {
                message = "Arena behavior group runtime is unavailable.";
                return false;
            }

            if (!TryGetDraft(draftId, out _))
            {
                message = "Arena behavior group draft is not available. draftId="
                    + draftId.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            return runtime.TryResolveSelection(
                ToSessionLoadoutId(draftId),
                SessionVariantId,
                out selection,
                out message);
        }

        public bool TryAddEntry(int draftId, string behaviorId, out string message)
        {
            message = string.Empty;
            if (!TryGetDraft(draftId, out CharacterTestArenaBehaviorGroupDraft draft))
            {
                message = "Arena behavior group draft is not available.";
                return false;
            }

            if (!_definitionCatalog.TryGetDefinition(
                    behaviorId,
                    out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition))
            {
                message = "Behavior definition is not available: " + (behaviorId ?? string.Empty);
                return false;
            }

            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry =
                CreateEntryFromDefinition(definition, ResolveNextOrder(draft.Profile.Entries));
            draft.Profile.Entries.Add(entry);
            Touch(draft);
            return true;
        }

        public bool TryDuplicateEntry(int draftId, int entryIndex, out string message)
        {
            message = string.Empty;
            if (!TryGetEntry(draftId, entryIndex, out CharacterTestArenaBehaviorGroupDraft draft, out CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry, out message))
                return false;

            CharacterTestRuntimeAiPlannerBehaviorModuleEntry clone = entry.Clone();
            clone.EntryId = CreateUniqueEntryId(draft.Profile.Entries, entry.BehaviorId);
            clone.Order = entry.Order + 1;
            draft.Profile.Entries.Insert(entryIndex + 1, clone);
            NormalizeEntryOrders(draft.Profile.Entries);
            Touch(draft);
            return true;
        }

        public bool TryRemoveEntry(int draftId, int entryIndex, out string message)
        {
            message = string.Empty;
            if (!TryGetDraft(draftId, out CharacterTestArenaBehaviorGroupDraft draft))
            {
                message = "Arena behavior group draft is not available.";
                return false;
            }

            if (entryIndex < 0 || entryIndex >= draft.Profile.Entries.Count)
            {
                message = "Arena behavior group entry is not available.";
                return false;
            }
            if (CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.IsCore(
                draft.Profile.Entries[entryIndex].CoreSlotKind))
            {
                message = "Core behavior slots can only be replaced.";
                return false;
            }

            draft.Profile.Entries.RemoveAt(entryIndex);
            NormalizeEntryOrders(draft.Profile.Entries);
            Touch(draft);
            return true;
        }

        public bool TryMoveEntry(int draftId, int entryIndex, int direction, out string message)
        {
            message = string.Empty;
            if (!TryGetDraft(draftId, out CharacterTestArenaBehaviorGroupDraft draft))
            {
                message = "Arena behavior group draft is not available.";
                return false;
            }

            int nextIndex = entryIndex + (direction < 0 ? -1 : 1);
            if (entryIndex < 0
                || entryIndex >= draft.Profile.Entries.Count
                || nextIndex < 0
                || nextIndex >= draft.Profile.Entries.Count)
            {
                message = "Arena behavior group entry cannot move further.";
                return false;
            }

            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = draft.Profile.Entries[entryIndex];
            if (CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.IsCore(entry.CoreSlotKind)
                || CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.IsCore(
                    draft.Profile.Entries[nextIndex].CoreSlotKind))
            {
                message = "Flexible behaviors cannot cross the fixed core-slot region.";
                return false;
            }
            draft.Profile.Entries.RemoveAt(entryIndex);
            draft.Profile.Entries.Insert(nextIndex, entry);
            NormalizeEntryOrders(draft.Profile.Entries);
            Touch(draft);
            return true;
        }

        public bool TryToggleEntryDisabled(int draftId, int entryIndex, out string message)
        {
            if (!TryGetEntry(draftId, entryIndex, out CharacterTestArenaBehaviorGroupDraft draft, out CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry, out message))
                return false;
            if (CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.IsCore(entry.CoreSlotKind))
            {
                message = "Core behavior slots must remain enabled.";
                return false;
            }

            entry.Disabled = !entry.Disabled;
            Touch(draft);
            message = entry.Disabled ? "Entry disabled." : "Entry enabled.";
            return true;
        }

        /// <summary>Atomically replaces one core slot with a compatible behavior definition.</summary>
        /// <remarks>LowFreqAlloc: explicit Arena authoring command.</remarks>
        public bool TryReplaceCoreSlot(
            int draftId,
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind,
            string behaviorId,
            out string message)
        {
            if (!CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.IsCore(slotKind))
            {
                message = "A core behavior slot is required.";
                return false;
            }
            if (!TryGetDraft(draftId, out CharacterTestArenaBehaviorGroupDraft draft))
            {
                message = "Arena behavior group draft is not available.";
                return false;
            }
            if (!_definitionCatalog.TryGetDefinition(
                behaviorId,
                out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
                || !CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.IsCompatible(slotKind, definition))
            {
                message = "The selected behavior is incompatible with the "
                    + CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.GetDisplayName(slotKind)
                    + " core slot.";
                return false;
            }

            int index = -1;
            for (int i = 0; i < draft.Profile.Entries.Count; i++)
            {
                if (draft.Profile.Entries[i].CoreSlotKind == slotKind)
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
            {
                message = "The requested core slot is missing.";
                return false;
            }

            CharacterTestRuntimeAiPlannerBehaviorModuleEntry previous = draft.Profile.Entries[index];
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry replacement =
                CreateEntryFromDefinition(definition, previous.Order);
            replacement.EntryId = previous.EntryId;
            replacement.CoreSlotKind = slotKind;
            draft.Profile.Entries[index] = replacement;
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotReport report =
                CharacterTestRuntimeAiPlannerBehaviorCoreSlotPolicy.Validate(
                    draft.Profile.Entries,
                    _definitionCatalog);
            if (!report.Success)
            {
                draft.Profile.Entries[index] = previous;
                message = report.Message;
                return false;
            }

            Touch(draft);
            message = "Core behavior replaced.";
            return true;
        }

        public bool TryAdjustParameter(
            int draftId,
            int entryIndex,
            int parameterIndex,
            int direction,
            out string message)
        {
            if (!TryGetParameter(
                    draftId,
                    entryIndex,
                    parameterIndex,
                    out CharacterTestArenaBehaviorGroupDraft draft,
                    out CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
                    out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
                    out CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter,
                    out message))
            {
                return false;
            }

            int sign = direction < 0 ? -1 : 1;
            switch (parameter.Kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Bool:
                    entry.SetBool(parameter.Key, !entry.GetBool(definition, parameter.Key));
                    break;
                case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Int:
                    entry.SetInt(
                        parameter.Key,
                        Clamp(entry.GetInt(definition, parameter.Key) + sign, parameter.MinInt, parameter.MaxInt));
                    break;
                default:
                    float step = ResolveFloatStep(parameter);
                    entry.SetFloat(
                        parameter.Key,
                        Clamp(
                            entry.GetFloat(definition, parameter.Key) + step * sign,
                            parameter.MinFloat,
                            parameter.MaxFloat));
                    NormalizePreferredDistanceRange(entry, definition, parameter.Key);
                    break;
            }

            Touch(draft);
            message = "Parameter updated.";
            return true;
        }

        public bool TryResetEntryParameters(int draftId, int entryIndex, out string message)
        {
            if (!TryGetEntry(draftId, entryIndex, out CharacterTestArenaBehaviorGroupDraft draft, out CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry, out message))
                return false;

            entry.Parameters.Clear();
            if (_definitionCatalog.TryGetDefinition(
                    entry.BehaviorId,
                    out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition))
            {
                ApplyDefaultParameters(entry, definition);
            }

            Touch(draft);
            message = "Entry parameters reset.";
            return true;
        }

        public CharacterTestRuntimeAiPlannerBehaviorCompileResult CompileDraft(int draftId)
        {
            CharacterTestArenaBehaviorGroupDraft draft = TryGetDraft(draftId, out CharacterTestArenaBehaviorGroupDraft found)
                ? found
                : GetDefaultDraft();
            return CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(
                draft?.Profile,
                _definitionCatalog);
        }

        public static int ToSessionLoadoutId(int draftId)
        {
            return SessionLoadoutIdBase + (draftId > 0 ? draftId : DefaultDraftId);
        }

        internal static string CreateActionSummary(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile)
        {
            if (profile == null || profile.Entries.Count == 0)
                return "No behavior entries.";

            var parts = new List<string>(profile.Entries.Count);
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry[] entries = CopySortedEntries(profile.Entries);
            for (int i = 0; i < entries.Length; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = entries[i];
                if (entry.Disabled)
                    continue;

                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.TryGetDefinition(
                    entry.BehaviorId,
                    out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition);
                string name = definition != null ? definition.DisplayName : entry.BehaviorId;
                string action = definition != null
                    ? ToActionLabel(definition.ActionPrimitive)
                    : "Unknown";
                parts.Add(name + " -> " + action);
            }

            return parts.Count == 0
                ? "All entries are disabled."
                : string.Join(" | ", parts);
        }

        internal static string ToActionLabel(
            CharacterTestRuntimeAiPlannerBehaviorActionPrimitive primitive)
        {
            switch (primitive)
            {
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.MoveToTarget:
                    return "Move to target";
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Strafe:
                    return "Strafe";
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Retreat:
                    return "Retreat";
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.PrimaryAttack:
                    return "Primary attack";
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.CastAbilitySlot:
                    return "Cast ability";
                case CharacterTestRuntimeAiPlannerBehaviorActionPrimitive.Wait:
                    return "Wait";
                default:
                    return "Facts only";
            }
        }

        internal static string FormatConditions(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            if (definition == null || definition.Conditions.Count == 0)
                return "Always";

            var parts = new string[definition.Conditions.Count];
            for (int i = 0; i < definition.Conditions.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorConditionDefinition condition =
                    definition.Conditions[i];
                parts[i] = condition.Fact.ToString()
                    + " "
                    + condition.Operator
                    + " "
                    + (string.IsNullOrWhiteSpace(condition.FloatParameterKey)
                        ? (condition.Operator == CharacterTestRuntimeAiPlannerConditionOperator.Equals
                            ? condition.BoolValue.ToString(CultureInfo.InvariantCulture)
                            : condition.FloatValue.ToString("0.##", CultureInfo.InvariantCulture))
                        : condition.FloatParameterKey);
            }

            return string.Join(", ", parts);
        }

        internal static string FormatRequiredFacts(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            if (definition == null || definition.RequiredFacts.Count == 0)
                return "none";

            var facts = new string[definition.RequiredFacts.Count];
            for (int i = 0; i < definition.RequiredFacts.Count; i++)
                facts[i] = definition.RequiredFacts[i].ToString();
            return string.Join(", ", facts);
        }

        internal static string CreateReportText(CharacterTestRuntimeAiPlannerValidationReport report)
        {
            return report != null && !report.Success
                ? report.ToText()
                : "passed";
        }

        private CharacterTestArenaBehaviorGroupDraft AddDraft(
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            string stableId,
            string displayName,
            int sourceProjectRevision = 0,
            long sourceProjectContentHash = 0)
        {
            int draftId = _nextDraftId++;
            var draft = new CharacterTestArenaBehaviorGroupDraft(
                draftId,
                stableId,
                displayName,
                profile,
                revision: 1,
                sourceProjectRevision: sourceProjectRevision,
                sourceProjectContentHash: sourceProjectContentHash);
            _drafts.Add(draft);
            Revision++;
            return draft;
        }

        private int IndexOfDraft(int draftId)
        {
            for (int i = 0; i < _drafts.Count; i++)
            {
                if (_drafts[i].DraftId == draftId)
                    return i;
            }

            return -1;
        }

        private void Touch(CharacterTestArenaBehaviorGroupDraft draft)
        {
            draft?.MarkEdited();
            Revision++;
        }

        private bool TryGetEntry(
            int draftId,
            int entryIndex,
            out CharacterTestArenaBehaviorGroupDraft draft,
            out CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            out string message)
        {
            entry = null;
            if (!TryGetDraft(draftId, out draft))
            {
                message = "Arena behavior group draft is not available.";
                return false;
            }

            if (entryIndex < 0 || entryIndex >= draft.Profile.Entries.Count)
            {
                message = "Arena behavior group entry is not available.";
                return false;
            }

            entry = draft.Profile.Entries[entryIndex];
            message = string.Empty;
            return true;
        }

        private bool TryGetParameter(
            int draftId,
            int entryIndex,
            int parameterIndex,
            out CharacterTestArenaBehaviorGroupDraft draft,
            out CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            out CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter,
            out string message)
        {
            definition = null;
            parameter = default;
            if (!TryGetEntry(draftId, entryIndex, out draft, out entry, out message))
                return false;

            if (!_definitionCatalog.TryGetDefinition(entry.BehaviorId, out definition))
            {
                message = "Behavior definition is not available: " + entry.BehaviorId;
                return false;
            }

            if (parameterIndex < 0 || parameterIndex >= definition.Parameters.Count)
            {
                message = "Behavior parameter is not available.";
                return false;
            }

            parameter = definition.Parameters[parameterIndex];
            message = string.Empty;
            return true;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCompositionProfile CreateDefaultMeleeProfile(
            CharacterTestRuntimeAiPlannerProfile runtimeProfile)
        {
            return CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile(runtimeProfile);
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCompositionProfile CreateKiteProfile(
            CharacterTestRuntimeAiPlannerProfile runtimeProfile)
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile(runtimeProfile);
            profile.DisplayName = "Kite survival";
            CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.TryGetDefinition(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition);
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry =
                CreateEntryFromDefinition(definition, 15);
            profile.Entries.Add(entry);
            NormalizeEntryOrders(profile.Entries);
            return profile;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorCompositionProfile CreateTrainingDummyProfile(
            CharacterTestRuntimeAiPlannerProfile runtimeProfile)
        {
            CharacterTestRuntimeAiPlannerProfile source =
                runtimeProfile ?? CharacterTestRuntimeAiPlannerProfileSource.CreateDefault();
            var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile(
                "character-test.runtime-ai-planner.composition.training-dummy",
                source.TargetSelectionMode,
                source.AttackRange,
                source.PerceptionSettings,
                source.MovementSettings)
            {
                DisplayName = "Training dummy"
            };
            AddTrainingDummyAuthoredBehaviors(profile);
            return profile;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorModuleEntry CreateEntryFromDefinition(
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            int order)
        {
            string behaviorId = definition != null
                ? definition.BehaviorId
                : CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle;
            var entry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                behaviorId,
                enabled: true,
                order: order);
            entry.EntryId = behaviorId + ".entry";
            ApplyDefaultParameters(entry, definition);
            return entry;
        }

        private static void ApplyDefaultParameters(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition)
        {
            if (entry == null || definition == null)
                return;

            for (int i = 0; i < definition.Parameters.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter =
                    definition.Parameters[i];
                switch (parameter.Kind)
                {
                    case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Bool:
                        entry.SetBool(parameter.Key, parameter.DefaultBool);
                        break;
                    case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Int:
                        entry.SetInt(parameter.Key, parameter.DefaultInt);
                        break;
                    default:
                        entry.SetFloat(parameter.Key, parameter.DefaultFloat);
                        break;
                }
            }
        }

        private static int ResolveNextOrder(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries)
        {
            int max = -EntryOrderStep;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                if (entries[i].Order > max)
                    max = entries[i].Order;
            }

            return max + EntryOrderStep;
        }

        private static void NormalizeEntryOrders(
            List<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries)
        {
            if (entries == null)
                return;

            entries.Sort(CompareEntries);
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].Order = i * EntryOrderStep;
                if (string.IsNullOrWhiteSpace(entries[i].EntryId)
                    || HasDuplicateEntryIdBefore(entries, i))
                {
                    entries[i].EntryId = CreateUniqueEntryId(entries, entries[i].BehaviorId, i);
                }
            }
        }

        private static bool HasDuplicateEntryIdBefore(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries,
            int index)
        {
            string entryId = entries[index]?.EntryId ?? string.Empty;
            for (int i = 0; i < index; i++)
            {
                if (string.Equals(entries[i]?.EntryId, entryId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string CreateUniqueEntryId(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries,
            string behaviorId,
            int allowedIndex = -1)
        {
            string prefix = string.IsNullOrWhiteSpace(behaviorId) ? "behavior" : behaviorId;
            int suffix = 1;
            while (suffix < 10000)
            {
                string candidate = prefix + "." + suffix.ToString(CultureInfo.InvariantCulture);
                bool exists = false;
                for (int i = 0; entries != null && i < entries.Count; i++)
                {
                    if (i == allowedIndex)
                        continue;
                    if (string.Equals(entries[i].EntryId, candidate, StringComparison.Ordinal))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    return candidate;
                suffix++;
            }

            return prefix + "." + Guid.NewGuid().ToString("N");
        }

        private static CharacterTestRuntimeAiPlannerBehaviorModuleEntry[] CopySortedEntries(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorModuleEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return Array.Empty<CharacterTestRuntimeAiPlannerBehaviorModuleEntry>();

            var copy = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry[entries.Count];
            for (int i = 0; i < entries.Count; i++)
                copy[i] = entries[i];
            Array.Sort(copy, CompareEntries);
            return copy;
        }

        private static int CompareEntries(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry left,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry right)
        {
            int leftSlot = CoreSlotSortIndex(
                left?.CoreSlotKind ?? CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Flexible);
            int rightSlot = CoreSlotSortIndex(
                right?.CoreSlotKind ?? CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Flexible);
            int slot = leftSlot.CompareTo(rightSlot);
            if (slot != 0)
                return slot;
            int leftOrder = left != null ? left.Order : int.MaxValue;
            int rightOrder = right != null ? right.Order : int.MaxValue;
            int order = leftOrder.CompareTo(rightOrder);
            if (order != 0)
                return order;
            return string.Compare(
                left != null ? left.EntryId : string.Empty,
                right != null ? right.EntryId : string.Empty,
                StringComparison.Ordinal);
        }

        private static int CoreSlotSortIndex(
            CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind slotKind)
        {
            switch (slotKind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Targeting:
                    return 0;
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.EngageOrPressure:
                    return 1;
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Damage:
                    return 2;
                case CharacterTestRuntimeAiPlannerBehaviorCoreSlotKind.Fallback:
                    return 3;
                default:
                    return 4;
            }
        }

        private static int ResolveNextProfileSlot(
            IReadOnlyList<CharacterTestRuntimeAiPlannerProfileBinding> existing)
        {
            int max = 0;
            for (int i = 0; existing != null && i < existing.Count; i++)
            {
                if (existing[i].Slot > max)
                    max = existing[i].Slot;
            }

            return max + 1;
        }

        private static float ResolveFloatStep(
            CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter)
        {
            float range = parameter.MaxFloat - parameter.MinFloat;
            if (range <= 1f)
                return 0.05f;
            if (range <= 5f)
                return 0.25f;
            if (range <= 20f)
                return 0.5f;
            return 1f;
        }

        private static void NormalizePreferredDistanceRange(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            string changedKey)
        {
            const string minKey = CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.MinDistanceKey;
            const string maxKey = CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.MaxDistanceKey;

            if (!definition.TryGetParameter(minKey, out CharacterTestRuntimeAiPlannerBehaviorParameterDefinition minParameter)
                || !definition.TryGetParameter(maxKey, out CharacterTestRuntimeAiPlannerBehaviorParameterDefinition maxParameter))
            {
                return;
            }

            float min = entry.GetFloat(definition, minKey);
            float max = entry.GetFloat(definition, maxKey);
            if (min <= max)
                return;

            if (string.Equals(changedKey, minKey, StringComparison.Ordinal))
            {
                entry.SetFloat(maxKey, Clamp(min, maxParameter.MinFloat, maxParameter.MaxFloat));
                return;
            }

            if (string.Equals(changedKey, maxKey, StringComparison.Ordinal))
            {
                entry.SetFloat(minKey, Clamp(max, minParameter.MinFloat, minParameter.MaxFloat));
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (min != max)
            {
                if (value < min)
                    return min;
                if (value > max)
                    return max;
            }

            return value;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (!CharacterTestRuntimeAiPlannerProfileValidator.IsFinite(value))
                return min;
            if (min != max)
            {
                if (value < min)
                    return min;
                if (value > max)
                    return max;
            }

            return value;
        }

        private static int WrapIndex(int index, int count)
        {
            if (count <= 0)
                return 0;
            int value = index % count;
            return value < 0 ? value + count : value;
        }
    }
}
