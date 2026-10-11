using System;
using System.Collections.Generic;
using MxFramework.Gameplay;
using MxFramework.Gameplay.Triggers;
using MxFramework.Gameplay.Triggers.Config;
using MxFramework.Gameplay.Triggers.GameplayBridge;
using MxFramework.Gameplay.Triggers.StoryBridge;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestLevelGameplayTriggerAuthoringDocument
    {
        internal const string StoryRaiseTriggerEnterOnceTemplateId =
            "region.story.raise_trigger.enter_once.v1";
        internal const string GameplayApplyBuffEnterOnceTemplateId =
            "region.gameplay.apply_buff.enter_once.v1";
        internal const string GameplayApplyRemoveBuffPresenceTemplateId =
            "region.gameplay.apply_remove_buff.presence.v1";
        internal const string PlayerHpLowStoryTemplateId =
            "attribute.local_player_hp.low.story.v1";
        internal const string TacticalBattleVictoryStoryTemplateId = "tactical.battle.victory.story.v1";
        internal const string TacticalBattleDefeatStoryTemplateId = "tactical.battle.defeat.story.v1";

        private const int DefaultRegionLayerMask =
            1 << CharacterTestCharacterPhysicsRuntime.DynamicCharacterLayer;
        private const int DefaultRegionQueryHitCapacity = 64;
        private const int DefaultRegionSubjectCapacity = 8;
        private const int StoryTargetSlotId = 1;
        internal const int DefaultPlayerHpEnterThreshold = 30;
        internal const int DefaultPlayerHpExitThreshold = 35;
        internal const int MinimumPlayerHpThreshold = 1;
        internal const int MaximumPlayerHpThreshold =
            CharacterTestCharacterAttributeIds.DefaultMaxHp;

        internal CharacterTestLevelGameplayTriggerEntry
            AddPlayerHpLowStoryTemplate(
                int storyTriggerId,
                int enterThreshold = DefaultPlayerHpEnterThreshold,
                int exitThreshold = DefaultPlayerHpExitThreshold,
                int payload1 = 0,
                int payload2 = 0)
        {
            ValidatePlayerHpThresholds(enterThreshold, exitThreshold);
            if (storyTriggerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(storyTriggerId));

            int sourceDefinitionId =
                AllocateId(GameplayTriggerConfigIdKind.SourceDefinition);
            int conditionId = AllocateId(GameplayTriggerConfigIdKind.Condition);
            int definitionId =
                AllocateId(GameplayTriggerConfigIdKind.Definition);
            int bindingId = AllocateId(GameplayTriggerConfigIdKind.Binding);
            int sourceBindingId =
                AllocateId(GameplayTriggerConfigIdKind.SourceBinding);
            GameplayTriggerRouteAuthoring[] routes = CreateRoutes(
                PlayerHpLowStoryTemplateId,
                conditionId,
                sourceBindingId,
                storyTriggerId,
                payload1,
                payload2,
                out List<int> routeIds);
            var entry = new CharacterTestLevelGameplayTriggerEntry
            {
                StableId = "trigger.attribute.localPlayerHp." + sourceBindingId,
                DisplayName = TemplateDisplayName(PlayerHpLowStoryTemplateId),
                TemplateId = PlayerHpLowStoryTemplateId,
                SourceDefinitionId = sourceDefinitionId,
                ConditionId = conditionId,
                DefinitionId = definitionId,
                RouteIds = routeIds,
                BindingId = bindingId,
                SourceBindingId = sourceBindingId
            };

            GameplayTriggerAuthoringDocument source =
                Authoring ?? CreateEmptyAuthoring();
            Authoring = new GameplayTriggerAuthoringDocument(
                source.SchemaId,
                source.SchemaVersion,
                Append(
                    source.Sources,
                    CreatePlayerHpThresholdSource(
                        sourceDefinitionId,
                        enterThreshold,
                        exitThreshold)),
                Append(
                    source.Conditions,
                    new GameplayTriggerConditionAuthoring(
                        conditionId,
                        new GameplayTriggerConditionNodeAuthoring(
                            GameplayTriggerConditionOperation.PushTrue))),
                Append(
                    source.Definitions,
                    new GameplayTriggerDefinitionAuthoring(
                        definitionId,
                        1,
                        sourceDefinitionId,
                        routes)),
                Append(
                    source.Bindings,
                    CreateBinding(
                        PlayerHpLowStoryTemplateId,
                        bindingId,
                        definitionId,
                        sourceBindingId)));
            Entries ??= new List<CharacterTestLevelGameplayTriggerEntry>();
            Entries.Add(entry);
            return entry;
        }

        internal CharacterTestLevelGameplayTriggerEntry AddTemplate(
            string templateId,
            string regionStableId,
            long payload0 = 1L,
            long payload1 = 0L,
            long payload2 = 0L)
        {
            if (IsTacticalBattleOutcomeTemplate(templateId))
            {
                AddTacticalBattleOutcomeTemplates();
                return Entries[Entries.Count - (templateId == TacticalBattleVictoryStoryTemplateId ? 2 : 1)];
            }
            string normalizedRegionId = NormalizeRequiredText(
                regionStableId,
                nameof(regionStableId));
            if (!IsSupportedTemplate(templateId))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(templateId),
                    "The CharacterTest Trigger template is not supported.");
            }

            int sourceDefinitionId =
                AllocateId(GameplayTriggerConfigIdKind.SourceDefinition);
            int conditionId = AllocateId(GameplayTriggerConfigIdKind.Condition);
            int definitionId =
                AllocateId(GameplayTriggerConfigIdKind.Definition);
            int bindingId = AllocateId(GameplayTriggerConfigIdKind.Binding);
            int sourceBindingId =
                AllocateId(GameplayTriggerConfigIdKind.SourceBinding);

            GameplayTriggerRouteAuthoring[] routes = CreateRoutes(
                templateId,
                conditionId,
                sourceBindingId,
                payload0,
                payload1,
                payload2,
                out List<int> routeIds);
            var entry = new CharacterTestLevelGameplayTriggerEntry
            {
                StableId = CreateStableId(normalizedRegionId, sourceBindingId),
                DisplayName = TemplateDisplayName(templateId),
                TemplateId = templateId,
                SourceDefinitionId = sourceDefinitionId,
                ConditionId = conditionId,
                DefinitionId = definitionId,
                RouteIds = routeIds,
                BindingId = bindingId,
                SourceBindingId = sourceBindingId
            };

            GameplayTriggerAuthoringDocument source =
                Authoring ?? CreateEmptyAuthoring();
            Authoring = new GameplayTriggerAuthoringDocument(
                source.SchemaId,
                source.SchemaVersion,
                Append(
                    source.Sources,
                    new GameplayTriggerSourceAuthoring(
                        sourceDefinitionId,
                        GameplayTriggerSourceKind.Occupancy,
                        initialSamplePolicy:
                            GameplayTriggerInitialSamplePolicy
                                .EmitEnteredIfActive,
                        departurePolicy:
                            GameplayTriggerSubjectDeparturePolicy.EmitExited)),
                Append(
                    source.Conditions,
                    new GameplayTriggerConditionAuthoring(
                        conditionId,
                        new GameplayTriggerConditionNodeAuthoring(
                            GameplayTriggerConditionOperation.PushTrue))),
                Append(
                    source.Definitions,
                    new GameplayTriggerDefinitionAuthoring(
                        definitionId,
                        1,
                        sourceDefinitionId,
                        routes)),
                Append(
                    source.Bindings,
                    CreateBinding(
                        templateId,
                        bindingId,
                        definitionId,
                        sourceBindingId)));

            Entries ??= new List<CharacterTestLevelGameplayTriggerEntry>();
            Entries.Add(entry);
            RegionBindings ??=
                new List<CharacterTestLevelGameplayTriggerRegionBinding>();
            RegionBindings.Add(
                new CharacterTestLevelGameplayTriggerRegionBinding
                {
                    SourceBindingId = sourceBindingId,
                    RegionStableId = normalizedRegionId,
                    ProviderId =
                        CharacterTestLevelGameplayTriggerAuthoringAdapter
                            .CombatRegionProviderId,
                    LayerMask = DefaultRegionLayerMask,
                    MaxQueryHits = DefaultRegionQueryHitCapacity,
                    MaxMappedSubjects = DefaultRegionSubjectCapacity,
                    SubjectFilter =
                        CharacterTestLevelGameplayTriggerSubjectFilter
                            .CreateTemplateDefault(templateId)
                });
            return entry;
        }

        internal bool TryUpdateTemplatePayloads(
            string stableId,
            long payload0,
            long payload1,
            long payload2 = 0L)
        {
            if (!TryFindEntry(stableId, out CharacterTestLevelGameplayTriggerEntry entry))
                return false;

            GameplayTriggerDefinitionAuthoring definition =
                FindDefinition(entry.DefinitionId);
            if (definition == null)
                return false;

            GameplayTriggerRouteAuthoring[] routes = CreateRoutesWithExistingIds(
                entry,
                payload0,
                payload1,
                payload2);
            if (definition.Routes == null
                || definition.Routes.Length != routes.Length)
            {
                return false;
            }
            for (int i = 0; i < routes.Length; i++)
            {
                routes[i] = CopyRoutePolicy(
                    routes[i],
                    definition.Routes[i]);
            }
            Authoring = new GameplayTriggerAuthoringDocument(
                Authoring.SchemaId,
                Authoring.SchemaVersion,
                Authoring.Sources,
                Authoring.Conditions,
                ReplaceDefinition(
                    Authoring.Definitions,
                    new GameplayTriggerDefinitionAuthoring(
                        definition.DefinitionId,
                        definition.DefinitionVersion,
                        definition.SourceDefinitionId,
                        routes)),
                Authoring.Bindings);
            return true;
        }

        internal bool TryGetTemplateSettings(
            CharacterTestLevelGameplayTriggerEntry entry,
            out bool initiallyEnabled,
            out GameplayTriggerStateScope stateScope,
            out GameplayTriggerPresenceLimit presenceLimit,
            out long cooldownFrames,
            out int maxActivations)
        {
            initiallyEnabled = false;
            stateScope = GameplayTriggerStateScope.PerInstance;
            presenceLimit = GameplayTriggerPresenceLimit.Ignore;
            cooldownFrames = 0L;
            maxActivations = 0;
            GameplayTriggerDefinitionAuthoring definition =
                entry == null ? null : FindDefinition(entry.DefinitionId);
            GameplayTriggerBindingAuthoring binding =
                entry == null ? null : FindBinding(entry.BindingId);
            if (definition?.Routes == null
                || definition.Routes.Length == 0
                || definition.Routes[0] == null
                || binding == null)
            {
                return false;
            }

            GameplayTriggerRouteAuthoring first = definition.Routes[0];
            for (int i = 1; i < definition.Routes.Length; i++)
            {
                GameplayTriggerRouteAuthoring route = definition.Routes[i];
                if (route == null
                    || route.StateScope != first.StateScope
                    || route.PresenceLimit != first.PresenceLimit
                    || route.CooldownFrames != first.CooldownFrames
                    || route.MaxActivations != first.MaxActivations)
                {
                    return false;
                }
            }

            initiallyEnabled = binding.InitiallyEnabled;
            stateScope = first.StateScope;
            presenceLimit = first.PresenceLimit;
            cooldownFrames = first.CooldownFrames;
            maxActivations = first.MaxActivations;
            return true;
        }

        internal bool TrySetTemplateEnabled(
            string stableId,
            bool initiallyEnabled)
        {
            if (!TryFindEntry(
                    stableId,
                    out CharacterTestLevelGameplayTriggerEntry entry))
            {
                return false;
            }

            GameplayTriggerBindingAuthoring binding =
                FindBinding(entry.BindingId);
            if (binding == null)
                return false;

            Authoring = new GameplayTriggerAuthoringDocument(
                Authoring.SchemaId,
                Authoring.SchemaVersion,
                Authoring.Sources,
                Authoring.Conditions,
                Authoring.Definitions,
                ReplaceBinding(
                    Authoring.Bindings,
                    new GameplayTriggerBindingAuthoring(
                        binding.BindingId,
                        binding.DefinitionId,
                        binding.SourceBindingId,
                        binding.OwnerIndex,
                        binding.OwnerGeneration,
                        initiallyEnabled,
                        binding.ExplicitTargets)));
            return true;
        }

        internal bool TryUpdateTemplatePolicy(
            string stableId,
            GameplayTriggerStateScope stateScope,
            GameplayTriggerPresenceLimit presenceLimit,
            long cooldownFrames,
            int maxActivations)
        {
            if ((stateScope != GameplayTriggerStateScope.PerInstance
                    && stateScope != GameplayTriggerStateScope.PerSubject)
                || (presenceLimit != GameplayTriggerPresenceLimit.Ignore
                    && presenceLimit
                        != GameplayTriggerPresenceLimit.OncePerPresence)
                || cooldownFrames < 0L
                || maxActivations < 0
                || !TryFindEntry(
                    stableId,
                    out CharacterTestLevelGameplayTriggerEntry entry))
            {
                return false;
            }

            if (stateScope == GameplayTriggerStateScope.PerInstance)
                presenceLimit = GameplayTriggerPresenceLimit.Ignore;

            GameplayTriggerDefinitionAuthoring definition =
                FindDefinition(entry.DefinitionId);
            if (definition?.Routes == null
                || definition.Routes.Length == 0)
            {
                return false;
            }

            int maxStateKeys = stateScope
                == GameplayTriggerStateScope.PerInstance
                    ? 1
                    : ResolveTemplateMaxStateKeys(entry.SourceBindingId);
            var routes =
                new GameplayTriggerRouteAuthoring[definition.Routes.Length];
            for (int i = 0; i < routes.Length; i++)
            {
                GameplayTriggerRouteAuthoring route = definition.Routes[i];
                if (route == null)
                    return false;

                routes[i] = new GameplayTriggerRouteAuthoring(
                    route.RouteId,
                    route.OccurrenceMask,
                    route.ConditionId,
                    route.Actions,
                    stateScope,
                    maxActivations,
                    cooldownFrames,
                    presenceLimit,
                    route.StayIntervalFrames,
                    route.ExecutionOrder,
                    route.ExclusiveGroupId,
                    route.WinnerPriority,
                    route.ArbitrationScope,
                    maxStateKeys,
                    route.AllowRepeatingCycle,
                    route.MinIntervalFrames);
            }

            Authoring = new GameplayTriggerAuthoringDocument(
                Authoring.SchemaId,
                Authoring.SchemaVersion,
                Authoring.Sources,
                Authoring.Conditions,
                ReplaceDefinition(
                    Authoring.Definitions,
                    new GameplayTriggerDefinitionAuthoring(
                        definition.DefinitionId,
                        definition.DefinitionVersion,
                        definition.SourceDefinitionId,
                        routes)),
                Authoring.Bindings);
            return true;
        }

        internal bool TryDuplicateEntry(
            string stableId,
            out CharacterTestLevelGameplayTriggerEntry duplicate)
        {
            duplicate = null;
            if (!TryFindEntry(stableId, out CharacterTestLevelGameplayTriggerEntry sourceEntry)
                || !TryGetTemplatePayloads(
                    sourceEntry,
                    out long payload0,
                    out long payload1,
                    out long payload2))
            {
                return false;
            }

            CharacterTestLevelGameplayTriggerRegionBinding regionBinding = null;
            if (string.Equals(
                    sourceEntry.TemplateId,
                    PlayerHpLowStoryTemplateId,
                    StringComparison.Ordinal))
            {
                if (!TryGetPlayerHpThresholds(
                        sourceEntry,
                        out int enterThreshold,
                        out int exitThreshold))
                {
                    return false;
                }
                duplicate = AddPlayerHpLowStoryTemplate(
                    checked((int)payload0),
                    enterThreshold,
                    exitThreshold,
                    checked((int)payload1),
                    checked((int)payload2));
            }
            else
            {
                if (!TryFindRegionBinding(
                        sourceEntry.SourceBindingId,
                        out regionBinding))
                {
                    return false;
                }
                duplicate = AddTemplate(
                    sourceEntry.TemplateId,
                    regionBinding.RegionStableId,
                    payload0,
                    payload1,
                    payload2);
            }
            duplicate.DisplayName = sourceEntry.DisplayName + " 副本";
            duplicate.Notes = sourceEntry.Notes;

            if (regionBinding != null)
            {
                CharacterTestLevelGameplayTriggerRegionBinding duplicateBinding =
                    FindRegionBinding(duplicate.SourceBindingId);
                duplicateBinding.ProviderId = regionBinding.ProviderId;
                duplicateBinding.LayerMask = regionBinding.LayerMask;
                duplicateBinding.MaxQueryHits = regionBinding.MaxQueryHits;
                duplicateBinding.MaxMappedSubjects =
                    regionBinding.MaxMappedSubjects;
                duplicateBinding.SubjectFilter =
                    regionBinding.SubjectFilter?.Normalize();
            }
            if (TryGetTemplateSettings(
                    sourceEntry,
                    out bool initiallyEnabled,
                    out GameplayTriggerStateScope stateScope,
                    out GameplayTriggerPresenceLimit presenceLimit,
                    out long cooldownFrames,
                    out int maxActivations))
            {
                TrySetTemplateEnabled(
                    duplicate.StableId,
                    initiallyEnabled);
                TryUpdateTemplatePolicy(
                    duplicate.StableId,
                    stateScope,
                    presenceLimit,
                    cooldownFrames,
                    maxActivations);
            }
            return true;
        }

        internal bool TryGetSubjectFilter(
            CharacterTestLevelGameplayTriggerEntry entry,
            out CharacterTestLevelGameplayTriggerSubjectFilter subjectFilter)
        {
            subjectFilter = entry == null
                ? null
                : FindRegionBinding(entry.SourceBindingId)?.SubjectFilter;
            return subjectFilter != null;
        }

        internal bool TryUpdateSubjectRelation(
            string stableId,
            GameplayTargetRelationFilter relationFilter,
            int referenceTeamId)
        {
            if (relationFilter != GameplayTargetRelationFilter.Any
                && relationFilter != GameplayTargetRelationFilter.SameTeam
                && relationFilter != GameplayTargetRelationFilter.Enemy
                && relationFilter != GameplayTargetRelationFilter.Neutral)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(relationFilter),
                    relationFilter,
                    "CharacterTest region relation is not supported.");
            }
            if (!TryFindEntry(
                    stableId,
                    out CharacterTestLevelGameplayTriggerEntry entry))
            {
                return false;
            }

            CharacterTestLevelGameplayTriggerRegionBinding binding =
                FindRegionBinding(entry.SourceBindingId);
            if (binding == null)
                return false;

            CharacterTestLevelGameplayTriggerSubjectFilter current =
                binding.SubjectFilter
                ?? CharacterTestLevelGameplayTriggerSubjectFilter
                    .CreateTemplateDefault(entry.TemplateId);
            var replacement = current.Normalize();
            replacement.RelationFilter = relationFilter;
            replacement.ReferenceTeamId =
                relationFilter == GameplayTargetRelationFilter.Any
                    ? 0
                    : referenceTeamId;
            replacement.ToRuntimeFilter();
            binding.SubjectFilter = replacement;
            return true;
        }

        internal bool TryUpdateSubjectRequiredCapabilities(
            string stableId,
            GameplayTargetCapabilities requiredCapabilities)
        {
            const GameplayTargetCapabilities SupportedCapabilities =
                GameplayTargetCapabilities.Damageable
                | GameplayTargetCapabilities.Movable;
            if ((requiredCapabilities & ~SupportedCapabilities) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(requiredCapabilities),
                    requiredCapabilities,
                    "CharacterTest region subject capabilities are not supported.");
            }
            if (!TryFindEntry(
                    stableId,
                    out CharacterTestLevelGameplayTriggerEntry entry))
            {
                return false;
            }

            GameplayTargetCapabilities minimumCapabilities =
                CharacterTestLevelGameplayTriggerSubjectFilter
                    .CreateTemplateDefault(entry.TemplateId)
                    .RequiredCapabilities;
            if ((requiredCapabilities & minimumCapabilities)
                != minimumCapabilities)
            {
                throw new ArgumentException(
                    "Required capabilities cannot remove the template minimum.",
                    nameof(requiredCapabilities));
            }

            CharacterTestLevelGameplayTriggerRegionBinding binding =
                FindRegionBinding(entry.SourceBindingId);
            if (binding == null)
                return false;

            CharacterTestLevelGameplayTriggerSubjectFilter current =
                binding.SubjectFilter
                ?? CharacterTestLevelGameplayTriggerSubjectFilter
                    .CreateTemplateDefault(entry.TemplateId);
            var replacement = current.Normalize();
            replacement.RequiredCapabilities = requiredCapabilities;
            replacement.ToRuntimeFilter();
            binding.SubjectFilter = replacement;
            return true;
        }

        internal bool TryGetTemplatePayloads(
            CharacterTestLevelGameplayTriggerEntry entry,
            out long payload0,
            out long payload1,
            out long payload2)
        {
            payload0 = 0L;
            payload1 = 0L;
            payload2 = 0L;
            GameplayTriggerDefinitionAuthoring definition =
                entry == null ? null : FindDefinition(entry.DefinitionId);
            if (definition?.Routes == null
                || definition.Routes.Length == 0
                || definition.Routes[0]?.Actions == null
                || definition.Routes[0].Actions.Length == 0)
            {
                return false;
            }

            GameplayTriggerActionAuthoring action =
                definition.Routes[0].Actions[0];
            payload0 = action.Payload0;
            payload1 = action.Payload1;
            payload2 = action.Payload2;
            return true;
        }

        internal bool TryFindEntry(
            string stableId,
            out CharacterTestLevelGameplayTriggerEntry entry)
        {
            entry = null;
            if (Entries == null || string.IsNullOrWhiteSpace(stableId))
                return false;

            string candidateId = stableId.Trim();
            for (int i = 0; i < Entries.Count; i++)
            {
                CharacterTestLevelGameplayTriggerEntry candidate = Entries[i];
                if (candidate != null
                    && string.Equals(
                        candidate.StableId,
                        candidateId,
                        StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }
            return false;
        }

        internal IReadOnlyList<CharacterTestLevelGameplayTriggerEntry>
            FindEntriesForRegion(string regionStableId)
        {
            var result = new List<CharacterTestLevelGameplayTriggerEntry>();
            if (Entries == null
                || RegionBindings == null
                || string.IsNullOrWhiteSpace(regionStableId))
            {
                return result;
            }

            string candidateId = regionStableId.Trim();
            for (int bindingIndex = 0;
                 bindingIndex < RegionBindings.Count;
                 bindingIndex++)
            {
                CharacterTestLevelGameplayTriggerRegionBinding binding =
                    RegionBindings[bindingIndex];
                if (binding == null
                    || !string.Equals(
                        binding.RegionStableId,
                        candidateId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                for (int entryIndex = 0;
                     entryIndex < Entries.Count;
                     entryIndex++)
                {
                    CharacterTestLevelGameplayTriggerEntry entry =
                        Entries[entryIndex];
                    if (entry?.SourceBindingId == binding.SourceBindingId)
                        result.Add(entry);
                }
            }
            return result;
        }

        internal static bool IsSupportedTemplate(string templateId)
        {
            return IsTacticalBattleOutcomeTemplate(templateId) || string.Equals(
                    templateId,
                    StoryRaiseTriggerEnterOnceTemplateId,
                    StringComparison.Ordinal)
                || string.Equals(
                    templateId,
                    GameplayApplyBuffEnterOnceTemplateId,
                    StringComparison.Ordinal)
                || string.Equals(
                    templateId,
                    GameplayApplyRemoveBuffPresenceTemplateId,
                    StringComparison.Ordinal)
                || string.Equals(
                    templateId,
                    PlayerHpLowStoryTemplateId,
                    StringComparison.Ordinal);
        }

        internal static bool IsPlayerHpThresholdTemplate(string templateId)
        {
            return string.Equals(
                templateId,
                PlayerHpLowStoryTemplateId,
                StringComparison.Ordinal);
        }

        internal static bool IsTacticalBattleOutcomeTemplate(string templateId)
            => string.Equals(templateId, TacticalBattleVictoryStoryTemplateId, StringComparison.Ordinal)
                || string.Equals(templateId, TacticalBattleDefeatStoryTemplateId, StringComparison.Ordinal);

        internal bool TryGetPlayerHpThresholds(
            CharacterTestLevelGameplayTriggerEntry entry,
            out int enterThreshold,
            out int exitThreshold)
        {
            enterThreshold = 0;
            exitThreshold = 0;
            GameplayTriggerSourceAuthoring source =
                entry == null ? null : FindSource(entry.SourceDefinitionId);
            if (!IsPlayerHpThresholdTemplate(entry?.TemplateId)
                || source == null
                || source.SourceKind != GameplayTriggerSourceKind.Threshold
                || source.EnterThreshold.Kind
                    != GameplayTriggerFix64InputKind.InvariantDecimal
                || source.ExitThreshold.Kind
                    != GameplayTriggerFix64InputKind.InvariantDecimal
                || !int.TryParse(source.EnterThreshold.InvariantDecimal,
                    out enterThreshold)
                || !int.TryParse(source.ExitThreshold.InvariantDecimal,
                    out exitThreshold))
            {
                return false;
            }
            return true;
        }

        internal bool TryUpdatePlayerHpThresholds(
            string stableId,
            int enterThreshold,
            int exitThreshold)
        {
            ValidatePlayerHpThresholds(enterThreshold, exitThreshold);
            if (!TryFindEntry(
                    stableId,
                    out CharacterTestLevelGameplayTriggerEntry entry)
                || !IsPlayerHpThresholdTemplate(entry.TemplateId))
            {
                return false;
            }

            GameplayTriggerSourceAuthoring[] rows = Authoring?.Sources;
            if (rows == null)
                return false;
            var replacement = CreatePlayerHpThresholdSource(
                entry.SourceDefinitionId,
                enterThreshold,
                exitThreshold);
            var result = new GameplayTriggerSourceAuthoring[rows.Length];
            bool replaced = false;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i]?.SourceDefinitionId == entry.SourceDefinitionId)
                {
                    result[i] = replacement;
                    replaced = true;
                }
                else
                {
                    result[i] = rows[i];
                }
            }
            if (!replaced)
                return false;
            Authoring = new GameplayTriggerAuthoringDocument(
                Authoring.SchemaId,
                Authoring.SchemaVersion,
                result,
                Authoring.Conditions,
                Authoring.Definitions,
                Authoring.Bindings);
            return true;
        }

        private GameplayTriggerRouteAuthoring[] CreateRoutes(
            string templateId,
            int conditionId,
            int sourceBindingId,
            long payload0,
            long payload1,
            long payload2,
            out List<int> routeIds)
        {
            int firstRouteId = AllocateId(GameplayTriggerConfigIdKind.Route);
            routeIds = new List<int> { firstRouteId };
            if (string.Equals(
                    templateId,
                    GameplayApplyRemoveBuffPresenceTemplateId,
                    StringComparison.Ordinal))
            {
                int secondRouteId =
                    AllocateId(GameplayTriggerConfigIdKind.Route);
                routeIds.Add(secondRouteId);
                return CreatePresenceRoutes(
                    firstRouteId,
                    secondRouteId,
                    conditionId,
                    payload0,
                    payload1);
            }

            return new[]
            {
                CreateSingleRoute(
                    templateId,
                    firstRouteId,
                    conditionId,
                    sourceBindingId,
                    payload0,
                    payload1,
                    payload2)
            };
        }

        private GameplayTriggerRouteAuthoring[] CreateRoutesWithExistingIds(
            CharacterTestLevelGameplayTriggerEntry entry,
            long payload0,
            long payload1,
            long payload2)
        {
            if (string.Equals(
                    entry.TemplateId,
                    GameplayApplyRemoveBuffPresenceTemplateId,
                    StringComparison.Ordinal))
            {
                if (entry.RouteIds == null || entry.RouteIds.Count != 2)
                    throw new InvalidOperationException(
                        "Presence template must own exactly two routes.");
                return CreatePresenceRoutes(
                    entry.RouteIds[0],
                    entry.RouteIds[1],
                    entry.ConditionId,
                    payload0,
                    payload1);
            }

            if (entry.RouteIds == null || entry.RouteIds.Count != 1)
                throw new InvalidOperationException(
                    "Single-event template must own exactly one route.");
            return new[]
            {
                CreateSingleRoute(
                    entry.TemplateId,
                    entry.RouteIds[0],
                    entry.ConditionId,
                    entry.SourceBindingId,
                    payload0,
                    payload1,
                    payload2)
            };
        }

        private static GameplayTriggerRouteAuthoring CreateSingleRoute(
            string templateId,
            int routeId,
            int conditionId,
            int sourceBindingId,
            long payload0,
            long payload1,
            long payload2)
        {
            if (string.Equals(
                    templateId,
                    StoryRaiseTriggerEnterOnceTemplateId,
                    StringComparison.Ordinal)
                || IsPlayerHpThresholdTemplate(templateId))
            {
                return new GameplayTriggerRouteAuthoring(
                    routeId,
                    GameplayTriggerOccurrenceKind.Entered,
                    conditionId,
                    new[]
                    {
                        new GameplayTriggerActionAuthoring(
                            GameplayTriggerStoryCommandActionKinds
                                .RaiseTriggerValue,
                            GameplayTriggerTargetSelector
                                .ExplicitBindingTarget,
                            StoryTargetSlotId,
                            payload0,
                            payload1,
                            payload2)
                    },
                    stateScope: IsPlayerHpThresholdTemplate(templateId)
                        ? GameplayTriggerStateScope.PerSubject
                        : GameplayTriggerStateScope.PerInstance,
                    maxActivations: IsPlayerHpThresholdTemplate(templateId)
                        ? 0
                        : 1,
                    presenceLimit: IsPlayerHpThresholdTemplate(templateId)
                        ? GameplayTriggerPresenceLimit.OncePerPresence
                        : GameplayTriggerPresenceLimit.Ignore,
                    maxStateKeys: 1);
            }

            return new GameplayTriggerRouteAuthoring(
                routeId,
                GameplayTriggerOccurrenceKind.Entered,
                conditionId,
                new[]
                {
                    new GameplayTriggerActionAuthoring(
                        GameplayTriggerGameplayBuffActionKinds
                            .ApplyComponentBuffValue,
                        GameplayTriggerTargetSelector.Subject,
                        payload0: payload0,
                        payload1: payload1)
                },
                stateScope: GameplayTriggerStateScope.PerSubject,
                maxActivations: 1,
                presenceLimit: GameplayTriggerPresenceLimit.OncePerPresence,
                maxStateKeys: DefaultRegionSubjectCapacity);
        }

        private static GameplayTriggerRouteAuthoring[] CreatePresenceRoutes(
            int enterRouteId,
            int exitRouteId,
            int conditionId,
            long payload0,
            long payload1)
        {
            return new[]
            {
                new GameplayTriggerRouteAuthoring(
                    enterRouteId,
                    GameplayTriggerOccurrenceKind.Entered
                    | GameplayTriggerOccurrenceKind.Stayed,
                    conditionId,
                    new[]
                    {
                        new GameplayTriggerActionAuthoring(
                            GameplayTriggerGameplayBuffActionKinds
                                .ApplyComponentBuffValue,
                            GameplayTriggerTargetSelector.Subject,
                            payload0: payload0,
                            payload1: payload1)
                    },
                    stateScope: GameplayTriggerStateScope.PerSubject,
                    presenceLimit:
                        GameplayTriggerPresenceLimit.Ignore,
                    stayIntervalFrames: 1L,
                    executionOrder: 0,
                    maxStateKeys: DefaultRegionSubjectCapacity),
                new GameplayTriggerRouteAuthoring(
                    exitRouteId,
                    GameplayTriggerOccurrenceKind.Exited,
                    conditionId,
                    new[]
                    {
                        new GameplayTriggerActionAuthoring(
                            GameplayTriggerGameplayBuffActionKinds
                                .RemoveComponentBuffValue,
                            GameplayTriggerTargetSelector.Subject,
                            payload0: payload0,
                            payload1: payload1)
                    },
                    stateScope: GameplayTriggerStateScope.PerSubject,
                    presenceLimit:
                        GameplayTriggerPresenceLimit.Ignore,
                    executionOrder: 1,
                    maxStateKeys: DefaultRegionSubjectCapacity)
            };
        }

        private static GameplayTriggerBindingAuthoring CreateBinding(
            string templateId,
            int bindingId,
            int definitionId,
            int sourceBindingId)
        {
            GameplayTriggerBindingTargetAuthoring[] targets =
                string.Equals(
                    templateId,
                    StoryRaiseTriggerEnterOnceTemplateId,
                    StringComparison.Ordinal)
                || IsPlayerHpThresholdTemplate(templateId)
                    ? new[]
                    {
                        new GameplayTriggerBindingTargetAuthoring(
                            StoryTargetSlotId,
                            GameplayTriggerTargetRef
                                .GameplayTriggerSourceBindingDomainId,
                            sourceBindingId)
                    }
                    : Array.Empty<GameplayTriggerBindingTargetAuthoring>();
            return new GameplayTriggerBindingAuthoring(
                bindingId,
                definitionId,
                sourceBindingId,
                explicitTargets: targets);
        }

        private static string TemplateDisplayName(string templateId)
        {
            if (string.Equals(
                    templateId,
                    StoryRaiseTriggerEnterOnceTemplateId,
                    StringComparison.Ordinal))
            {
                return "进入时触发剧情";
            }
            if (IsPlayerHpThresholdTemplate(templateId))
                return "玩家低生命时触发剧情";
            if (string.Equals(
                    templateId,
                    GameplayApplyBuffEnterOnceTemplateId,
                    StringComparison.Ordinal))
            {
                return "进入时施加 Buff";
            }
            return "区域内维持 Buff";
        }

        private static string CreateStableId(
            string regionStableId,
            int sourceBindingId)
        {
            return "trigger."
                + regionStableId.Replace('/', '.').Replace('\\', '.')
                + "."
                + sourceBindingId;
        }

        private GameplayTriggerDefinitionAuthoring FindDefinition(
            int definitionId)
        {
            GameplayTriggerDefinitionAuthoring[] rows = Authoring?.Definitions;
            if (rows == null)
                return null;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i]?.DefinitionId == definitionId)
                    return rows[i];
            }
            return null;
        }

        private GameplayTriggerSourceAuthoring FindSource(
            int sourceDefinitionId)
        {
            GameplayTriggerSourceAuthoring[] rows = Authoring?.Sources;
            if (rows == null)
                return null;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i]?.SourceDefinitionId == sourceDefinitionId)
                    return rows[i];
            }
            return null;
        }

        private GameplayTriggerBindingAuthoring FindBinding(int bindingId)
        {
            GameplayTriggerBindingAuthoring[] rows = Authoring?.Bindings;
            if (rows == null)
                return null;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i]?.BindingId == bindingId)
                    return rows[i];
            }
            return null;
        }

        private int ResolveTemplateMaxStateKeys(int sourceBindingId)
        {
            if (Entries != null)
            {
                for (int i = 0; i < Entries.Count; i++)
                {
                    if (Entries[i]?.SourceBindingId == sourceBindingId
                        && IsPlayerHpThresholdTemplate(
                            Entries[i].TemplateId))
                    {
                        return 1;
                    }
                }
            }
            CharacterTestLevelGameplayTriggerRegionBinding binding =
                FindRegionBinding(sourceBindingId);
            return Math.Max(
                1,
                binding?.MaxMappedSubjects ?? DefaultRegionSubjectCapacity);
        }

        private bool TryFindRegionBinding(
            int sourceBindingId,
            out CharacterTestLevelGameplayTriggerRegionBinding binding)
        {
            binding = FindRegionBinding(sourceBindingId);
            return binding != null;
        }

        private CharacterTestLevelGameplayTriggerRegionBinding FindRegionBinding(
            int sourceBindingId)
        {
            if (RegionBindings == null)
                return null;
            for (int i = 0; i < RegionBindings.Count; i++)
            {
                if (RegionBindings[i]?.SourceBindingId == sourceBindingId)
                    return RegionBindings[i];
            }
            return null;
        }

        private static T[] Append<T>(T[] rows, T value)
        {
            int count = rows?.Length ?? 0;
            var result = new T[count + 1];
            if (count > 0)
                Array.Copy(rows, result, count);
            result[count] = value;
            return result;
        }

        private static GameplayTriggerDefinitionAuthoring[] ReplaceDefinition(
            GameplayTriggerDefinitionAuthoring[] rows,
            GameplayTriggerDefinitionAuthoring replacement)
        {
            var result =
                new GameplayTriggerDefinitionAuthoring[rows?.Length ?? 0];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = rows[i]?.DefinitionId == replacement.DefinitionId
                    ? replacement
                    : rows[i];
            }
            return result;
        }

        private static GameplayTriggerBindingAuthoring[] ReplaceBinding(
            GameplayTriggerBindingAuthoring[] rows,
            GameplayTriggerBindingAuthoring replacement)
        {
            var result =
                new GameplayTriggerBindingAuthoring[rows?.Length ?? 0];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = rows[i]?.BindingId == replacement.BindingId
                    ? replacement
                    : rows[i];
            }
            return result;
        }

        private static GameplayTriggerRouteAuthoring CopyRoutePolicy(
            GameplayTriggerRouteAuthoring payloadRoute,
            GameplayTriggerRouteAuthoring policyRoute)
        {
            if (payloadRoute == null || policyRoute == null)
                return payloadRoute;

            return new GameplayTriggerRouteAuthoring(
                payloadRoute.RouteId,
                payloadRoute.OccurrenceMask,
                payloadRoute.ConditionId,
                payloadRoute.Actions,
                policyRoute.StateScope,
                policyRoute.MaxActivations,
                policyRoute.CooldownFrames,
                policyRoute.PresenceLimit,
                policyRoute.StayIntervalFrames,
                policyRoute.ExecutionOrder,
                policyRoute.ExclusiveGroupId,
                policyRoute.WinnerPriority,
                policyRoute.ArbitrationScope,
                policyRoute.MaxStateKeys,
                policyRoute.AllowRepeatingCycle,
                policyRoute.MinIntervalFrames);
        }

        private static string NormalizeRequiredText(
            string value,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A stable region ID is required.", parameterName);
            return value.Trim();
        }

        private static GameplayTriggerSourceAuthoring
            CreatePlayerHpThresholdSource(
                int sourceDefinitionId,
                int enterThreshold,
                int exitThreshold)
        {
            return new GameplayTriggerSourceAuthoring(
                sourceDefinitionId,
                GameplayTriggerSourceKind.Threshold,
                thresholdDirection: GameplayTriggerThresholdDirection.Low,
                enterThreshold:
                    GameplayTriggerFix64Authoring.FromInvariantDecimal(
                        enterThreshold.ToString(
                            System.Globalization.CultureInfo.InvariantCulture)),
                exitThreshold:
                    GameplayTriggerFix64Authoring.FromInvariantDecimal(
                        exitThreshold.ToString(
                            System.Globalization.CultureInfo.InvariantCulture)),
                initialSamplePolicy:
                    GameplayTriggerInitialSamplePolicy.PrimeSilently,
                departurePolicy:
                    GameplayTriggerSubjectDeparturePolicy.EmitExited);
        }

        private static void ValidatePlayerHpThresholds(
            int enterThreshold,
            int exitThreshold)
        {
            if (enterThreshold < MinimumPlayerHpThreshold
                || enterThreshold > MaximumPlayerHpThreshold)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enterThreshold));
            }
            if (exitThreshold < MinimumPlayerHpThreshold
                || exitThreshold > MaximumPlayerHpThreshold
                || exitThreshold < enterThreshold)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exitThreshold));
            }
        }
    }
}
