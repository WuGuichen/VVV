using System;
using System.Collections.Generic;
using MxFramework.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace MxFramework.Game.CharacterTest
{

    /// <summary>
    /// Gameplay-authoritative Runtime AI Planner behavior selection summary.
    /// </summary>
    /// <remarks>
    /// NoAlloc value type. It stores stable catalog ids and the derived compiled profile slot, not
    /// editor strings, JSON, or compiled evaluator instances.
    /// </remarks>
    public readonly struct CharacterTestRuntimeAiBehaviorSelection
    {
        /// <summary>
        /// Creates a gameplay-owned behavior selection summary.
        /// </summary>
        /// <remarks>NoAlloc. Invalid non-positive ids normalize to zero.</remarks>
        public CharacterTestRuntimeAiBehaviorSelection(
            int loadoutId,
            int variantId,
            long catalogHash,
            int profileSlot)
        {
            LoadoutId = loadoutId > 0 ? loadoutId : 0;
            VariantId = variantId > 0 ? variantId : 0;
            CatalogHash = catalogHash;
            ProfileSlot = profileSlot > 0 ? profileSlot : 0;
        }

        /// <summary>Published behavior loadout id.</summary>
        public int LoadoutId { get; }
        /// <summary>Published loadout variant id.</summary>
        public int VariantId { get; }
        /// <summary>Stable catalog hash used to validate replay/snapshot compatibility.</summary>
        public long CatalogHash { get; }
        /// <summary>Compiled Runtime AI Planner profile slot derived from the selection.</summary>
        public int ProfileSlot { get; }
        /// <summary>Returns true when ids and derived profile slot are present.</summary>
        public bool HasSelection => LoadoutId > 0 && VariantId > 0 && ProfileSlot > 0;

        /// <summary>No selection. Existing profile path/slot fallback remains in effect.</summary>
        public static CharacterTestRuntimeAiBehaviorSelection None => default;
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition
    {
        public CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition(
            int variantId,
            string stableId,
            string displayName,
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile composition)
            : this(
                variantId,
                stableId,
                displayName,
                composition != null ? composition.GroupId : string.Empty,
                composition)
        {
        }

        public CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition(
            int variantId,
            string stableId,
            string displayName,
            string behaviorGroupId,
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile behaviorGroup)
        {
            VariantId = variantId > 0 ? variantId : 0;
            StableId = stableId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            BehaviorGroup = behaviorGroup != null
                ? behaviorGroup.Clone()
                : CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile();
            BehaviorGroupId = string.IsNullOrWhiteSpace(behaviorGroupId)
                ? BehaviorGroup.GroupId
                : behaviorGroupId;
            BehaviorGroup.GroupId = BehaviorGroupId;
        }

        public int VariantId { get; }
        public string StableId { get; }
        public string DisplayName { get; }
        public string BehaviorGroupId { get; }
        public CharacterTestRuntimeAiPlannerBehaviorCompositionProfile BehaviorGroup { get; }
        public CharacterTestRuntimeAiPlannerBehaviorCompositionProfile Composition => BehaviorGroup;
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition
    {
        private readonly CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition[] _variants;
        private readonly string[] _allowedGroups;

        public CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition(
            int loadoutId,
            string stableId,
            string displayName,
            string description,
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition> variants,
            IEnumerable<string> allowedGroups = null,
            CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyKind playerStrategyKind =
                CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyKind.Assault,
            string playerShortDescription = "",
            string playerPrimaryActionSummary = "",
            string playerSuitableScenario = "",
            string playerRiskText = "")
        {
            LoadoutId = loadoutId > 0 ? loadoutId : 0;
            StableId = stableId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            _variants = CopyVariants(variants);
            _allowedGroups = CopyStrings(allowedGroups);
            PlayerStrategyKind = playerStrategyKind;
            PlayerShortDescription = playerShortDescription ?? string.Empty;
            PlayerPrimaryActionSummary = playerPrimaryActionSummary ?? string.Empty;
            PlayerSuitableScenario = playerSuitableScenario ?? string.Empty;
            PlayerRiskText = playerRiskText ?? string.Empty;
        }

        public int LoadoutId { get; }
        public string StableId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyKind PlayerStrategyKind { get; }
        public string PlayerShortDescription { get; }
        public string PlayerPrimaryActionSummary { get; }
        public string PlayerSuitableScenario { get; }
        public string PlayerRiskText { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition> Variants => _variants;
        public IReadOnlyList<string> AllowedGroups => _allowedGroups;

        private static CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition[] CopyVariants(
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition> variants)
        {
            if (variants == null)
                return Array.Empty<CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition>();

            var list = new List<CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition>();
            foreach (CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition variant in variants)
            {
                if (variant != null)
                    list.Add(variant);
            }

            return list.ToArray();
        }

        private static string[] CopyStrings(IEnumerable<string> source)
        {
            if (source == null)
                return Array.Empty<string>();

            var list = new List<string>();
            foreach (string item in source)
            {
                if (!string.IsNullOrWhiteSpace(item))
                    list.Add(item);
            }

            return list.ToArray();
        }
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog
    {
        public const string Schema = "mx.character-test.runtime-ai-planner.behavior-loadout-catalog.v1";

        private readonly CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition[] _loadouts;

        public CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog(
            string catalogId,
            int revision,
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition> loadouts)
        {
            CatalogId = string.IsNullOrWhiteSpace(catalogId)
                ? "character-test.runtime-ai-planner.behavior-loadouts.default"
                : catalogId;
            Revision = revision < 0 ? 0 : revision;
            _loadouts = CopyLoadouts(loadouts);
            CatalogHash = ComputeCatalogHash(this);
        }

        public string CatalogId { get; }
        public int Revision { get; }
        public long CatalogHash { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition> Loadouts => _loadouts;

        public static CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog CreateDefault(
            CharacterTestRuntimeAiPlannerProfile runtimeProfile = null)
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile composition =
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile(runtimeProfile);
            var variant = new CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition(
                1,
                "default",
                "Default",
                composition);
            var loadout = new CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition(
                1,
                "character-test.behavior.default-melee",
                "Default melee",
                "Baseline nearest-target melee behavior compiled from the built-in behavior entries.",
                new[] { variant },
                new[] { "default", "npc", "ally" },
                CharacterTestRuntimeAiPlannerPlayerBehaviorStrategyKind.Assault,
                "锁定最近敌人并主动接近，进入范围后优先普通攻击。",
                "接近、攻击、绕障；目标失活时等待。",
                "适合默认近战单位和低复杂度敌人/NPC。",
                "容易被远程风筝；危险区域优先撤离会打断进攻节奏。");
            return new CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog(
                "character-test.runtime-ai-planner.behavior-loadouts.default",
                1,
                new[] { loadout });
        }

        private static CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition[] CopyLoadouts(
            IEnumerable<CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition> loadouts)
        {
            if (loadouts == null)
                return Array.Empty<CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition>();

            var list = new List<CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition>();
            foreach (CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition loadout in loadouts)
            {
                if (loadout != null)
                    list.Add(loadout);
            }

            return list.ToArray();
        }

        private static long ComputeCatalogHash(
            CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog catalog)
        {
            var accumulator = new RuntimeContentHashAccumulator();
            accumulator.AddStringStable("behaviorLoadout.catalogId", catalog.CatalogId);
            accumulator.AddInt("behaviorLoadout.revision", catalog.Revision);
            accumulator.AddInt("behaviorLoadout.count", catalog._loadouts.Length);
            for (int i = 0; i < catalog._loadouts.Length; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition loadout = catalog._loadouts[i];
                accumulator.AddInt("behaviorLoadout.index", i);
                accumulator.AddInt("behaviorLoadout.loadoutId", loadout.LoadoutId);
                accumulator.AddStringStable("behaviorLoadout.stableId", loadout.StableId);
                accumulator.AddInt("behaviorLoadout.allowedGroupCount", loadout.AllowedGroups.Count);
                for (int groupIndex = 0; groupIndex < loadout.AllowedGroups.Count; groupIndex++)
                    accumulator.AddStringStable(
                        "behaviorLoadout.allowedGroup",
                        loadout.AllowedGroups[groupIndex]);
                accumulator.AddInt("behaviorLoadout.variantCount", loadout.Variants.Count);
                for (int j = 0; j < loadout.Variants.Count; j++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition variant =
                        loadout.Variants[j];
                    accumulator.AddInt("behaviorLoadout.variantIndex", j);
                    accumulator.AddInt("behaviorLoadout.variantId", variant.VariantId);
                    accumulator.AddStringStable("behaviorLoadout.variantStableId", variant.StableId);
                    accumulator.AddStringStable(
                        "behaviorLoadout.behaviorGroupId",
                        variant.BehaviorGroupId);
                    AddCompositionHash(accumulator, variant.BehaviorGroup);
                }
            }

            return accumulator.ToHash();
        }

        private static void AddCompositionHash(
            RuntimeContentHashAccumulator accumulator,
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile composition)
        {
            CharacterTestRuntimeAiPlannerBehaviorCompositionProfile safe =
                composition ?? CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.CreateDefaultProfile();
            CharacterTestRuntimeAiPlannerPerceptionSettings perception =
                CharacterTestRuntimeAiPlannerPerceptionSettings.Normalize(safe.PerceptionSettings);
            CharacterTestRuntimeAiPlannerMovementSettings movement =
                CharacterTestRuntimeAiPlannerMovementSettings.Normalize(safe.MovementSettings);

            accumulator.AddInt("behaviorLoadout.targetSelectionMode", (int)safe.TargetSelectionMode);
            AddFloatHash(accumulator, "behaviorLoadout.attackRange", safe.AttackRange);
            AddFloatHash(accumulator, "behaviorLoadout.perception.visionRange", perception.VisionRange);
            accumulator.AddInt("behaviorLoadout.perception.targetMemoryFrames", perception.TargetMemoryFrames);
            accumulator.AddInt(
                "behaviorLoadout.perception.lineOfSightObstacleLayerMask",
                perception.LineOfSightObstacleLayerMask);
            AddFloatHash(
                accumulator,
                "behaviorLoadout.perception.environmentSampleRadius",
                perception.EnvironmentSampleRadius);
            AddFloatHash(
                accumulator,
                "behaviorLoadout.perception.threatDistanceWeight",
                perception.ThreatDistanceWeight);
            AddFloatHash(accumulator, "behaviorLoadout.movement.speedScale", movement.SpeedScale);
            AddFloatHash(
                accumulator,
                "behaviorLoadout.movement.steeringResponsiveness",
                movement.SteeringResponsiveness);
            AddFloatHash(
                accumulator,
                "behaviorLoadout.movement.approachLateralBlend",
                movement.ApproachLateralBlend);
            AddFloatHash(
                accumulator,
                "behaviorLoadout.movement.preferredDistanceMin",
                movement.PreferredDistanceMin);
            AddFloatHash(
                accumulator,
                "behaviorLoadout.movement.preferredDistanceMax",
                movement.PreferredDistanceMax);
            AddFloatHash(
                accumulator,
                "behaviorLoadout.movement.arrivalSlowdownDistance",
                movement.ArrivalSlowdownDistance);
            accumulator.AddInt(
                "behaviorLoadout.movement.strafeSwitchMinFrames",
                movement.StrafeSwitchMinFrames);
            accumulator.AddInt(
                "behaviorLoadout.movement.strafeSwitchMaxFrames",
                movement.StrafeSwitchMaxFrames);
            accumulator.AddInt("behaviorLoadout.movement.directionHoldFrames", movement.DirectionHoldFrames);
            accumulator.AddInt("behaviorLoadout.movement.reactionDelayFrames", movement.ReactionDelayFrames);

            accumulator.AddInt("behaviorLoadout.entryCount", safe.Entries.Count);
            for (int k = 0; k < safe.Entries.Count; k++)
            {
                CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry = safe.Entries[k];
                accumulator.AddInt("behaviorLoadout.entryIndex", k);
                accumulator.AddStringStable("behaviorLoadout.entryId", entry.EntryId);
                accumulator.AddStringStable("behaviorLoadout.behaviorId", entry.BehaviorId);
                accumulator.AddInt("behaviorLoadout.entryDisabled", entry.Disabled ? 1 : 0);
                accumulator.AddInt("behaviorLoadout.entryOrder", entry.Order);
                AddModuleParametersHash(accumulator, entry);
            }
        }

        private static void AddModuleParametersHash(
            RuntimeContentHashAccumulator accumulator,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry module)
        {
            if (CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.TryGetDefinition(
                    module.ModuleId,
                    out CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition))
            {
                accumulator.AddInt("behaviorLoadout.module.parameterCount", definition.Parameters.Count);
                for (int i = 0; i < definition.Parameters.Count; i++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter =
                        definition.Parameters[i];
                    accumulator.AddStringStable("behaviorLoadout.module.parameterKey", parameter.Key);
                    accumulator.AddInt("behaviorLoadout.module.parameterKind", (int)parameter.Kind);
                    AddEffectiveParameterHash(accumulator, module, definition, parameter);
                }

                return;
            }

            if (module.Parameters == null || module.Parameters.Count == 0)
            {
                accumulator.AddInt("behaviorLoadout.module.parameterCount", 0);
                return;
            }

            var sortedParameters =
                new List<KeyValuePair<string, CharacterTestRuntimeAiPlannerBehaviorParameterValue>>(
                    module.Parameters.Count);
            foreach (KeyValuePair<string, CharacterTestRuntimeAiPlannerBehaviorParameterValue> pair in
                module.Parameters)
            {
                sortedParameters.Add(pair);
            }

            sortedParameters.Sort(CompareParameterPairs);
            accumulator.AddInt("behaviorLoadout.module.parameterCount", sortedParameters.Count);
            for (int i = 0; i < sortedParameters.Count; i++)
            {
                KeyValuePair<string, CharacterTestRuntimeAiPlannerBehaviorParameterValue> pair =
                    sortedParameters[i];
                accumulator.AddStringStable("behaviorLoadout.module.parameterKey", pair.Key);
                accumulator.AddInt("behaviorLoadout.module.parameterKind", (int)pair.Value.Kind);
                AddRawParameterHash(accumulator, pair.Value);
            }
        }

        private static void AddEffectiveParameterHash(
            RuntimeContentHashAccumulator accumulator,
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry module,
            CharacterTestRuntimeAiPlannerBehaviorModuleDefinition definition,
            CharacterTestRuntimeAiPlannerBehaviorParameterDefinition parameter)
        {
            switch (parameter.Kind)
            {
                case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Bool:
                    accumulator.AddInt(
                        "behaviorLoadout.module.parameterBool",
                        module.GetBool(definition, parameter.Key) ? 1 : 0);
                    break;
                case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Int:
                    accumulator.AddInt(
                        "behaviorLoadout.module.parameterInt",
                        module.GetInt(definition, parameter.Key));
                    break;
                case CharacterTestRuntimeAiPlannerBehaviorParameterKind.Float:
                    AddFloatHash(
                        accumulator,
                        "behaviorLoadout.module.parameterFloat",
                        module.GetFloat(definition, parameter.Key));
                    break;
            }
        }

        private static void AddRawParameterHash(
            RuntimeContentHashAccumulator accumulator,
            CharacterTestRuntimeAiPlannerBehaviorParameterValue value)
        {
            accumulator.AddInt("behaviorLoadout.module.parameterBool", value.BoolValue ? 1 : 0);
            accumulator.AddInt("behaviorLoadout.module.parameterInt", value.IntValue);
            AddFloatHash(accumulator, "behaviorLoadout.module.parameterFloat", value.FloatValue);
        }

        private static void AddFloatHash(RuntimeContentHashAccumulator accumulator, string key, float value)
        {
            float safeValue = CharacterTestRuntimeAiPlannerProfileValidator.IsFinite(value) ? value : 0f;
            accumulator.AddDoubleQuantized(key, safeValue, 1000d);
        }

        private static int CompareParameterPairs(
            KeyValuePair<string, CharacterTestRuntimeAiPlannerBehaviorParameterValue> left,
            KeyValuePair<string, CharacterTestRuntimeAiPlannerBehaviorParameterValue> right)
        {
            return string.Compare(left.Key, right.Key, StringComparison.Ordinal);
        }
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorLoadoutEntry
    {
        public CharacterTestRuntimeAiPlannerBehaviorLoadoutEntry(
            int loadoutId,
            int variantId,
            string stableId,
            string variantStableId,
            string displayName,
            string variantDisplayName,
            CharacterTestRuntimeAiBehaviorSelection selection)
        {
            LoadoutId = loadoutId;
            VariantId = variantId;
            StableId = stableId ?? string.Empty;
            VariantStableId = variantStableId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            VariantDisplayName = variantDisplayName ?? string.Empty;
            Selection = selection;
        }

        public int LoadoutId { get; }
        public int VariantId { get; }
        public string StableId { get; }
        public string VariantStableId { get; }
        public string DisplayName { get; }
        public string VariantDisplayName { get; }
        public CharacterTestRuntimeAiBehaviorSelection Selection { get; }
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorLoadoutBuildResult
    {
        public CharacterTestRuntimeAiPlannerBehaviorLoadoutBuildResult(
            CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog catalog,
            CharacterTestRuntimeAiPlannerProfileBinding[] profileBindings,
            CharacterTestRuntimeAiPlannerBehaviorLoadoutEntry[] entries,
            CharacterTestRuntimeAiPlannerValidationReport report)
        {
            Catalog = catalog;
            ProfileBindings = profileBindings ?? Array.Empty<CharacterTestRuntimeAiPlannerProfileBinding>();
            Entries = entries ?? Array.Empty<CharacterTestRuntimeAiPlannerBehaviorLoadoutEntry>();
            Report = report ?? new CharacterTestRuntimeAiPlannerValidationReport(null);
        }

        public CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog Catalog { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerProfileBinding> ProfileBindings { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorLoadoutEntry> Entries { get; }
        public CharacterTestRuntimeAiPlannerValidationReport Report { get; }
        public bool Success => Catalog != null && Report.Success;
    }

    internal sealed class CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime
    {
        private readonly CharacterTestRuntimeAiPlannerBehaviorLoadoutEntry[] _entries;
        private readonly CharacterTestRuntimeAiPlannerProfileBinding[] _profileBindings;
        private readonly List<CharacterTestRuntimeAiBehaviorSelection> _sessionProfileSlotAliases =
            new List<CharacterTestRuntimeAiBehaviorSelection>();
        private readonly RuntimeLogBuffer _logBuffer = new RuntimeLogBuffer(160);

        private CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime(
            CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog catalog,
            CharacterTestRuntimeAiPlannerBehaviorLoadoutEntry[] entries,
            CharacterTestRuntimeAiPlannerProfileBinding[] profileBindings,
            CharacterTestRuntimeAiPlannerValidationReport report)
        {
            Catalog = catalog;
            _entries = entries ?? Array.Empty<CharacterTestRuntimeAiPlannerBehaviorLoadoutEntry>();
            _profileBindings = profileBindings ?? Array.Empty<CharacterTestRuntimeAiPlannerProfileBinding>();
            Report = report ?? new CharacterTestRuntimeAiPlannerValidationReport(null);
            DefaultSelection = _entries.Length > 0
                ? _entries[0].Selection
                : CharacterTestRuntimeAiBehaviorSelection.None;
        }

        public CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog Catalog { get; }
        public long CatalogHash => Catalog != null ? Catalog.CatalogHash : 0L;
        public CharacterTestRuntimeAiPlannerValidationReport Report { get; }
        public CharacterTestRuntimeAiBehaviorSelection DefaultSelection { get; }
        public IReadOnlyList<CharacterTestRuntimeAiPlannerProfileBinding> ProfileBindings => _profileBindings;
        public IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorLoadoutEntry> Entries => _entries;

        public static CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime CreateDefault(
            CharacterTestRuntimeAiPlannerProfile runtimeProfile,
            IReadOnlyList<CharacterTestRuntimeAiPlannerProfileBinding> existingProfileBindings,
            IRuntimeLogger logger = null)
        {
            CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog catalog =
                CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault(runtimeProfile);
            return Create(
                catalog,
                ResolveNextProfileSlot(existingProfileBindings),
                logger);
        }

        public static CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime Create(
            CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog catalog,
            int firstProfileSlot,
            IRuntimeLogger logger = null)
        {
            CharacterTestRuntimeAiPlannerBehaviorLoadoutBuildResult result =
                Build(catalog, firstProfileSlot);
            var runtime = new CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime(
                result.Catalog,
                CopyEntries(result.Entries),
                CopyBindings(result.ProfileBindings),
                result.Report);
            if (!result.Success && logger != null)
            {
                runtime._logBuffer.Clear()
                    .Append("Runtime AI Planner behavior loadout catalog compile failed: ")
                    .Append(result.Report.ToText());
                logger.Warning("CharacterTest.RuntimeAiPlanner", runtime._logBuffer);
            }

            return runtime;
        }

        public static CharacterTestRuntimeAiPlannerProfileBinding[] CombineProfileBindings(
            IReadOnlyList<CharacterTestRuntimeAiPlannerProfileBinding> existing,
            CharacterTestRuntimeAiPlannerBehaviorLoadoutRuntime runtime)
        {
            int existingCount = existing != null ? existing.Count : 0;
            int loadoutCount = runtime != null ? runtime.ProfileBindings.Count : 0;
            if (existingCount == 0 && loadoutCount == 0)
                return Array.Empty<CharacterTestRuntimeAiPlannerProfileBinding>();

            var result = new CharacterTestRuntimeAiPlannerProfileBinding[existingCount + loadoutCount];
            for (int i = 0; i < existingCount; i++)
                result[i] = existing[i];
            for (int i = 0; i < loadoutCount; i++)
                result[existingCount + i] = runtime.ProfileBindings[i];
            return result;
        }

        public bool TryResolveSelection(
            int loadoutId,
            int variantId,
            out CharacterTestRuntimeAiBehaviorSelection selection,
            out string message)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorLoadoutEntry entry = _entries[i];
                if (entry.LoadoutId != loadoutId || entry.VariantId != variantId)
                    continue;

                selection = entry.Selection;
                message = string.Empty;
                return true;
            }

            selection = CharacterTestRuntimeAiBehaviorSelection.None;
            message = "Runtime AI Planner behavior loadout is not available. loadoutId="
                + loadoutId
                + " variantId="
                + variantId;
            return false;
        }

        public bool TryGetEntry(
            CharacterTestRuntimeAiBehaviorSelection selection,
            out CharacterTestRuntimeAiPlannerBehaviorLoadoutEntry entry)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorLoadoutEntry candidate = _entries[i];
                if (candidate.LoadoutId == selection.LoadoutId
                    && candidate.VariantId == selection.VariantId
                    && candidate.Selection.CatalogHash == selection.CatalogHash)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = default;
            return false;
        }

        public bool TryValidateSelection(
            CharacterTestRuntimeAiBehaviorSelection selection,
            out string message)
        {
            if (!selection.HasSelection)
            {
                message = string.Empty;
                return true;
            }

            if (selection.CatalogHash != Catalog.CatalogHash)
            {
                message = "Runtime AI Planner behavior catalog hash mismatch.";
                return false;
            }

            if (!TryResolveSelection(
                    selection.LoadoutId,
                    selection.VariantId,
                    out CharacterTestRuntimeAiBehaviorSelection resolved,
                    out message))
            {
                return false;
            }

            if (selection.ProfileSlot != resolved.ProfileSlot
                && !IsRegisteredSessionProfileSlotAlias(selection))
            {
                message = "Runtime AI Planner behavior profile slot mismatch.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        internal bool TryRegisterSessionProfileSlotAlias(
            CharacterTestRuntimeAiBehaviorSelection selection,
            out string message)
        {
            if (!selection.HasSelection)
            {
                message = "Runtime AI Planner behavior profile slot alias requires a selection.";
                return false;
            }
            if (selection.CatalogHash != Catalog.CatalogHash)
            {
                message = "Runtime AI Planner behavior profile slot alias catalog hash mismatch.";
                return false;
            }
            if (!TryResolveSelection(
                    selection.LoadoutId,
                    selection.VariantId,
                    out CharacterTestRuntimeAiBehaviorSelection resolved,
                    out message))
            {
                return false;
            }
            if (selection.ProfileSlot == resolved.ProfileSlot)
            {
                message = string.Empty;
                return true;
            }

            for (int i = 0; i < _sessionProfileSlotAliases.Count; i++)
            {
                CharacterTestRuntimeAiBehaviorSelection existing =
                    _sessionProfileSlotAliases[i];
                if (existing.ProfileSlot != selection.ProfileSlot)
                    continue;
                if (existing.LoadoutId == selection.LoadoutId
                    && existing.VariantId == selection.VariantId
                    && existing.CatalogHash == selection.CatalogHash)
                {
                    message = string.Empty;
                    return true;
                }

                message = "Runtime AI Planner behavior profile slot alias is already assigned.";
                return false;
            }

            _sessionProfileSlotAliases.Add(selection);
            message = string.Empty;
            return true;
        }

        private bool IsRegisteredSessionProfileSlotAlias(
            CharacterTestRuntimeAiBehaviorSelection selection)
        {
            for (int i = 0; i < _sessionProfileSlotAliases.Count; i++)
            {
                CharacterTestRuntimeAiBehaviorSelection candidate =
                    _sessionProfileSlotAliases[i];
                if (candidate.LoadoutId == selection.LoadoutId
                    && candidate.VariantId == selection.VariantId
                    && candidate.CatalogHash == selection.CatalogHash
                    && candidate.ProfileSlot == selection.ProfileSlot)
                {
                    return true;
                }
            }
            return false;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorLoadoutBuildResult Build(
            CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog catalog,
            int firstProfileSlot)
        {
            CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog source =
                catalog ?? CharacterTestRuntimeAiPlannerBehaviorLoadoutCatalog.CreateDefault();
            int nextSlot = firstProfileSlot > 0 ? firstProfileSlot : 1;
            var diagnostics = new List<CharacterTestRuntimeAiPlannerDiagnostic>();
            var bindings = new List<CharacterTestRuntimeAiPlannerProfileBinding>();
            var entries = new List<CharacterTestRuntimeAiPlannerBehaviorLoadoutEntry>();
            var loadoutIds = new HashSet<int>();
            var selectionIds = new HashSet<long>();

            if (source.Loadouts.Count == 0)
            {
                diagnostics.Add(new CharacterTestRuntimeAiPlannerDiagnostic(
                    CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                    "behaviorLoadout.empty",
                    "Behavior loadout catalog must contain at least one loadout."));
            }

            for (int i = 0; i < source.Loadouts.Count; i++)
            {
                CharacterTestRuntimeAiPlannerBehaviorLoadoutDefinition loadout = source.Loadouts[i];
                if (loadout.LoadoutId <= 0)
                {
                    diagnostics.Add(new CharacterTestRuntimeAiPlannerDiagnostic(
                        CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                        "behaviorLoadout.id",
                        "Behavior loadout id must be positive."));
                    continue;
                }

                if (!loadoutIds.Add(loadout.LoadoutId))
                {
                    diagnostics.Add(new CharacterTestRuntimeAiPlannerDiagnostic(
                        CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                        "behaviorLoadout.duplicate",
                        "Duplicate behavior loadout id: " + loadout.LoadoutId));
                    continue;
                }

                if (loadout.Variants.Count == 0)
                {
                    diagnostics.Add(new CharacterTestRuntimeAiPlannerDiagnostic(
                        CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                        "behaviorLoadout.variantMissing",
                        "Behavior loadout has no variants: " + loadout.LoadoutId));
                    continue;
                }

                for (int j = 0; j < loadout.Variants.Count; j++)
                {
                    CharacterTestRuntimeAiPlannerBehaviorLoadoutVariantDefinition variant =
                        loadout.Variants[j];
                    if (variant.VariantId <= 0)
                    {
                        diagnostics.Add(new CharacterTestRuntimeAiPlannerDiagnostic(
                            CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                            "behaviorLoadout.variantId",
                            "Behavior loadout variant id must be positive."));
                        continue;
                    }

                    long selectionKey = (((long)loadout.LoadoutId) << 32) | (uint)variant.VariantId;
                    if (!selectionIds.Add(selectionKey))
                    {
                        diagnostics.Add(new CharacterTestRuntimeAiPlannerDiagnostic(
                            CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                            "behaviorLoadout.variantDuplicate",
                            "Duplicate behavior loadout variant: "
                            + loadout.LoadoutId
                            + "/"
                            + variant.VariantId));
                        continue;
                    }

                    CharacterTestRuntimeAiPlannerBehaviorCompileResult compile =
                        CharacterTestRuntimeAiPlannerBehaviorCompositionCompiler.Compile(variant.BehaviorGroup);
                    if (!compile.Success)
                    {
                        diagnostics.Add(new CharacterTestRuntimeAiPlannerDiagnostic(
                            CharacterTestRuntimeAiPlannerDiagnosticSeverity.Error,
                            "behaviorLoadout.compile",
                            "Behavior loadout variant failed to compile: "
                            + loadout.LoadoutId
                            + "/"
                            + variant.VariantId
                            + " "
                            + (compile.Report != null ? compile.Report.ToText() : string.Empty)));
                        continue;
                    }

                    int slot = nextSlot++;
                    var selection = new CharacterTestRuntimeAiBehaviorSelection(
                        loadout.LoadoutId,
                        variant.VariantId,
                        source.CatalogHash,
                        slot);
                    bindings.Add(new CharacterTestRuntimeAiPlannerProfileBinding(
                        slot,
                        "behavior-loadout:"
                        + loadout.StableId
                        + ":"
                        + variant.StableId,
                        compile.Profile,
                        compile.Report));
                    entries.Add(new CharacterTestRuntimeAiPlannerBehaviorLoadoutEntry(
                        loadout.LoadoutId,
                        variant.VariantId,
                        loadout.StableId,
                        variant.StableId,
                        loadout.DisplayName,
                        variant.DisplayName,
                        selection));
                }
            }

            return new CharacterTestRuntimeAiPlannerBehaviorLoadoutBuildResult(
                source,
                bindings.ToArray(),
                entries.ToArray(),
                new CharacterTestRuntimeAiPlannerValidationReport(diagnostics));
        }

        private static int ResolveNextProfileSlot(
            IReadOnlyList<CharacterTestRuntimeAiPlannerProfileBinding> existing)
        {
            int max = 0;
            if (existing != null)
            {
                for (int i = 0; i < existing.Count; i++)
                {
                    if (existing[i].Slot > max)
                        max = existing[i].Slot;
                }
            }

            return max + 1;
        }

        private static CharacterTestRuntimeAiPlannerBehaviorLoadoutEntry[] CopyEntries(
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorLoadoutEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return Array.Empty<CharacterTestRuntimeAiPlannerBehaviorLoadoutEntry>();

            var copy = new CharacterTestRuntimeAiPlannerBehaviorLoadoutEntry[entries.Count];
            for (int i = 0; i < entries.Count; i++)
                copy[i] = entries[i];
            return copy;
        }

        private static CharacterTestRuntimeAiPlannerProfileBinding[] CopyBindings(
            IReadOnlyList<CharacterTestRuntimeAiPlannerProfileBinding> bindings)
        {
            if (bindings == null || bindings.Count == 0)
                return Array.Empty<CharacterTestRuntimeAiPlannerProfileBinding>();

            var copy = new CharacterTestRuntimeAiPlannerProfileBinding[bindings.Count];
            for (int i = 0; i < bindings.Count; i++)
                copy[i] = bindings[i];
            return copy;
        }
    }

}
