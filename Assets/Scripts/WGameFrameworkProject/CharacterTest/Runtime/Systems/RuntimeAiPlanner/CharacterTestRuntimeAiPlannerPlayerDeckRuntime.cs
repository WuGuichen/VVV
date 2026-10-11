using System;
using System.Collections.Generic;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason
    {
        None = 0,
        ConditionsSatisfied = 1,
        TargetAcquired = 2,
        TargetOutsideRange = 3,
        TargetInAttackRange = 4,
        AbilityOpportunity = 5,
        IncomingThreat = 6
    }

    internal readonly struct CharacterTestRuntimeAiPlannerBehaviorCardRuntimePresentation
    {
        public CharacterTestRuntimeAiPlannerBehaviorCardRuntimePresentation(
            string stableCardId,
            long semanticHash,
            long authorityHash,
            string stageId,
            CharacterTestRuntimeAiLabLogicRuntimeState runtimeState,
            CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason switchReason)
        {
            StableCardId = stableCardId ?? string.Empty;
            SemanticHash = semanticHash;
            AuthorityHash = authorityHash;
            StageId = stageId ?? string.Empty;
            RuntimeState = runtimeState;
            SwitchReason = switchReason;
        }

        public string StableCardId { get; }
        public long SemanticHash { get; }
        public long AuthorityHash { get; }
        public string StageId { get; }
        public CharacterTestRuntimeAiLabLogicRuntimeState RuntimeState { get; }
        public CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason SwitchReason { get; }
        public bool HasActiveCard => StableCardId.Length > 0;
    }

    /// <summary>
    /// Converts an authority-pinned player deck into the existing Logic v2 runtime-overlay input.
    /// </summary>
    /// <remarks>
    /// LowFreqAlloc: invoked once at the pre-battle lock boundary. Tick continues to use the existing
    /// no-allocation behavior-card runtime overlay.
    /// </remarks>
    internal static class CharacterTestRuntimeAiPlannerPlayerDeckRuntimeCompiler
    {
        private const float BaseScore = 10f;
        private const float EngageScore = 100f;
        private const float DamageScore = 200f;
        private const float DamagePursuitScore = 300f;
        private const float DefenseScore = 400f;

        public static bool TryCompile(
            CharacterTestRuntimeAiPlannerSessionDeckLock sessionLock,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog catalog,
            out CharacterTestRuntimeAiPlannerBehaviorCompositionProfile profile,
            out CharacterTestRuntimeAiLabCompiledLogicProgram[] programs,
            out string error,
            CharacterTestTacticalCardInputs tacticalInputs = null,
            IReadOnlyDictionary<string, CharacterTestTacticalCardInputs> cardInputs = null)
        {
            profile = null;
            programs = Array.Empty<CharacterTestRuntimeAiLabCompiledLogicProgram>();
            error = string.Empty;
            if (sessionLock == null || catalog == null)
            {
                error = "Player behavior deck runtime compilation requires a session lock and catalog.";
                return false;
            }
            if (!string.Equals(sessionLock.CatalogId, catalog.CatalogId, StringComparison.Ordinal)
                || sessionLock.CatalogRevision != catalog.Revision
                || sessionLock.CatalogHash != catalog.CatalogHash)
            {
                error = "Player behavior deck catalog identity no longer matches the session lock.";
                return false;
            }

            IReadOnlyList<CharacterTestRuntimeAiPlannerPinnedBehaviorCard> pinned = sessionLock.Cards;
            var candidateProfile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile(
                "character-test.runtime-ai-planner.player-deck." + sessionLock.CharacterStableId);
            var compiledPrograms = new CharacterTestRuntimeAiLabCompiledLogicProgram[pinned.Count];
            for (int i = 0; i < pinned.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPinnedBehaviorCard identity = pinned[i];
                if (!catalog.TryGetExact(identity.StableCardId, identity.Revision, out CharacterTestRuntimeAiPlannerPublishedBehaviorCard card)
                    || card.SemanticHash != identity.SemanticHash
                    || card.ContentHash != identity.ContentHash)
                {
                    error = "Pinned behavior card is missing or changed: " + identity.StableCardId;
                    return false;
                }

                CharacterTestRuntimeAiLabCompiledLogicProgram program;
                if (card.TacticalSource != null)
                {
                    var input = tacticalInputs;
                    if (cardInputs != null && cardInputs.TryGetValue(card.StableCardId, out var explicitInput)) input = explicitInput;
                    if (!card.TacticalSource.TryCompile(card.StableCardId, card.Metadata.DisplayName, card.Revision,
                        input, out program, out error)) return false;
                    var actual = CharacterTestBehaviorExecutionUsage.Analyze(program, true).Capabilities;
                    if (!new HashSet<CharacterTestRuntimeAiPlannerBehaviorCapability>(actual).SetEquals(card.Metadata.RequiredCapabilities))
                    { error = "Published tactical capabilities do not match the bound source: " + identity.StableCardId; return false; }
                }
                else
                {
                    var compile = CharacterTestRuntimeAiLabBehaviorCardLogicCompiler.Compile(card.CloneLogic());
                    if (!compile.Success || compile.Program == null || compile.Program.SemanticHash != identity.SemanticHash)
                    {
                        error = "Pinned behavior card could not be compiled: " + identity.StableCardId;
                        return false;
                    }
                    program = compile.Program;
                }

                CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason reason =
                    ClassifySwitchReason(program);
                var entry = new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                    CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                    enabled: true,
                    order: i)
                {
                    EntryId = identity.StableCardId
                };
                ConfigureCanonicalArbitration(entry, card.Metadata);
                candidateProfile.Entries.Add(entry);
                compiledPrograms[i] = program.WithSelectionDefinition(CharacterTestBehaviorCardSelectionDefinition.FromPublished(card));
            }

            var order = new List<int>(compiledPrograms.Length);
            for (int i = 0; i < compiledPrograms.Length; i++) order.Add(i);
            order.Sort((left, right) => CharacterTestBehaviorCardSelectionDefinition.Compare(
                compiledPrograms[left].SelectionDefinition, compiledPrograms[right].SelectionDefinition));
            var entries = candidateProfile.Entries.ToArray(); candidateProfile.Entries.Clear();
            programs = new CharacterTestRuntimeAiLabCompiledLogicProgram[compiledPrograms.Length];
            for (int i = 0; i < order.Count; i++)
            {
                var entry = entries[order[i]]; entry.Order = i;
                candidateProfile.Entries.Add(entry); programs[i] = compiledPrograms[order[i]];
            }
            profile = candidateProfile;
            return true;
        }

        public static CharacterTestRuntimeAiPlannerBehaviorCompositionProfile CreateFallbackOnlyProfile(
            string characterStableId)
        {
            var profile = new CharacterTestRuntimeAiPlannerBehaviorCompositionProfile(
                "character-test.runtime-ai-planner.player-deck." + (characterStableId ?? string.Empty));
            profile.Entries.Add(new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationApproachAndAttack,
                enabled: true,
                order: 0));
            profile.Entries.Add(new CharacterTestRuntimeAiPlannerBehaviorModuleEntry(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.FoundationIdle,
                enabled: true,
                order: 1));
            return profile;
        }

        public static CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason ClassifySwitchReason(
            CharacterTestRuntimeAiLabCompiledLogicProgram program)
        {
            CharacterTestRuntimeAiLabCompiledLogicStage entryStage = program != null
                && program.EntryStageIndex >= 0
                && program.EntryStageIndex < program.Stages.Count
                    ? program.Stages[program.EntryStageIndex]
                    : null;
            bool entryIsAttack = entryStage != null
                && entryStage.Kind == CharacterTestRuntimeAiLabCardStepKind.CharacterAction;
            bool entryIsMovement = entryStage != null
                && entryStage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget;
            bool entryIsAbility = entryStage != null
                && entryStage.Kind == CharacterTestRuntimeAiLabCardStepKind.UseAbility;
            bool targetExists = false;
            bool targetOutsideRange = false;
            bool targetInRange = false;
            bool abilityOpportunity = false;
            bool incomingThreat = false;
            IReadOnlyList<CharacterTestRuntimeAiLabCompiledCondition> conditions =
                program?.ActivationConditions;
            for (int i = 0; conditions != null && i < conditions.Count; i++)
            {
                CharacterTestRuntimeAiLabCompiledCondition condition = conditions[i];
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetAttackWillHitSelf
                    && condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Equals
                    && condition.BoolValue)
                {
                    incomingThreat = true;
                }
                if (entryIsAbility
                    && condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.AbilityAvailable
                    && condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Equals
                    && condition.BoolValue)
                    abilityOpportunity = true;
                if (entryIsMovement
                    && condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                    && (condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.GreaterOrEqual
                        || condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.GreaterThan))
                    targetOutsideRange = true;
                if (entryIsAttack
                    && condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetDistance
                    && (condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.LessOrEqual
                        || condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Between))
                    targetInRange = true;
                if (condition.Fact == CharacterTestRuntimeAiLabCardConditionFact.TargetExists
                    && condition.Operator == CharacterTestRuntimeAiLabCardConditionOperator.Equals
                    && condition.BoolValue)
                {
                    targetExists = true;
                }
            }
            if (incomingThreat)
                return CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason.IncomingThreat;
            if (abilityOpportunity)
                return CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason.AbilityOpportunity;
            if (targetInRange)
                return CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason.TargetInAttackRange;
            if (targetOutsideRange)
                return CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason.TargetOutsideRange;
            return targetExists
                ? CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason.TargetAcquired
                : CharacterTestRuntimeAiPlannerBehaviorCardSwitchReason.ConditionsSatisfied;
        }

        internal static void ConfigureCanonicalArbitration(
            CharacterTestRuntimeAiPlannerBehaviorModuleEntry entry,
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata metadata)
        {
            if (entry == null)
                return;
            entry.Parameters.Clear();
            entry.SetFloat(
                CharacterTestRuntimeAiPlannerBehaviorCompositionCatalog.ScoreKey,
                ResolveCanonicalScore(metadata));
        }

        internal static float ResolveCanonicalScore(
            CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata metadata)
            => ResolveCanonicalScoreFromCapabilities(metadata?.RequiredCapabilities, metadata?.Responsibilities);

        internal static float ResolveCanonicalScoreFromCapabilities(IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorCapability> capabilities,
            IReadOnlyList<CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind> responsibilities = null)
        {
            bool defense = false;
            bool damage = false;
            bool pursuit = false;
            bool fallback = false;
            for (int i = 0; capabilities != null && i < capabilities.Count; i++)
            {
                switch (capabilities[i])
                {
                    case CharacterTestRuntimeAiPlannerBehaviorCapability.Defense:
                        defense = true;
                        break;
                    case CharacterTestRuntimeAiPlannerBehaviorCapability.Damage:
                        damage = true;
                        break;
                    case CharacterTestRuntimeAiPlannerBehaviorCapability.Pursuit:
                    case CharacterTestRuntimeAiPlannerBehaviorCapability.Engage:
                        pursuit = true;
                        break;
                    case CharacterTestRuntimeAiPlannerBehaviorCapability.Fallback:
                        fallback = true;
                        break;
                }
            }
            if (defense)
                return DefenseScore;
            if (damage && pursuit)
                return DamagePursuitScore;
            if (damage)
                return DamageScore;
            if (pursuit)
                return EngageScore;
            for (int i = 0; responsibilities != null && i < responsibilities.Count; i++)
                fallback |= responsibilities[i] == CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind.Fallback;
            return fallback ? 0f : BaseScore;
        }
    }
}
