using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Immutable source and binding evidence owned by one compiled revision, never by the live editor.</summary>
    internal sealed class CharacterTestTacticalFrozenSource
    {
        private string _compositionDocument;
        private readonly string _legacySource;
        private readonly CharacterTestTacticalCardInputs _explicitInputs;
        private readonly CharacterTestTacticalDslSource _ownedDocument;
        private string DocumentText => _compositionDocument ??= CharacterTestTacticalDslJson.Write(_ownedDocument);
        private readonly ReadOnlyDictionary<string, CharacterTestTacticalSourceLocation> _locations;
        private readonly ReadOnlyCollection<string> _descriptionWarnings;
        private readonly ReadOnlyDictionary<string, CharacterTestTacticalAuthoringCompilation> _sharedCompilations;
        private CharacterTestTacticalFrozenSource(string workspace, int workspaceRevision, string card, int cardRevision,
            string document, string legacy, CharacterTestTacticalCardInputs inputs,
            IReadOnlyDictionary<string, CharacterTestTacticalSourceLocation> locations, CharacterTestTacticalDslDescription description,
            IReadOnlyDictionary<string, CharacterTestTacticalAuthoringCompilation> sharedCompilations = null,
            CharacterTestRuntimeAiLabCompiledLogicProgram compiledProgram = null, CharacterTestTacticalDslSource ownedDocument = null)
        {
            _ownedDocument = ownedDocument;
            _explicitInputs = inputs.HasExplicitBindings ? inputs : null;
            ConfiguredActionIds = Array.AsReadOnly(inputs.CaptureConfiguredActionIds());
            WorkspaceId = workspace; WorkspaceRevision = workspaceRevision; CardId = card; CardRevision = cardRevision;
            CompiledProgram = compiledProgram;
            _compositionDocument = document; _legacySource = legacy;
            RequiredExecutionInputKind = legacy != null && CharacterTestTacticalAuthoringJson.Read(legacy).ExecutionInput != null
                ? TacticalCard.TacticalExecutionInputKind.WorldPoint : TacticalCard.TacticalExecutionInputKind.None;
            CharacterId = inputs.SourceCharacterId; ActionId = inputs.Action?.ActionId.Value ?? 0; ActionSetId = inputs.ActionSetId;
            TestActionOverride = inputs.TestActionOverride;
            StepBackActionId = inputs.StepBackAction?.ActionId.Value ?? 0; StepBackContentHash = inputs.StepBackContentHash;
            StepForwardActionId = inputs.StepForwardAction?.ActionId.Value ?? 0; StepForwardContentHash = inputs.StepForwardContentHash;
            Requirements = inputs.Requirements;
            ActionContentHash = inputs.Metrics?.ActionContentHash ?? string.Empty; InputFingerprint = inputs.DependencyFingerprint;
            SummarySourceCharacterId = inputs.SummarySourceCharacterId; UsesDefaultSummary = inputs.UsesDefaultSummary;
            _locations = new ReadOnlyDictionary<string, CharacterTestTacticalSourceLocation>(locations.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
            Description = description.Text; DescriptionDetails = description.Details;
            _descriptionWarnings = Array.AsReadOnly((string[])description.Warnings.Clone());
            _sharedCompilations = new ReadOnlyDictionary<string, CharacterTestTacticalAuthoringCompilation>(sharedCompilations == null
                ? new Dictionary<string, CharacterTestTacticalAuthoringCompilation>() : sharedCompilations.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
        }
        internal CharacterTestTacticalInputRequirements Requirements { get; }
        internal TacticalCard.TacticalExecutionInputKind RequiredExecutionInputKind { get; }
        internal bool HasExplicitBindings => _explicitInputs != null;
        // Derived from the existing immutable input fingerprint; not a new persisted schema or cooldown owner.
        internal IReadOnlyList<int> ConfiguredActionIds { get; }

        internal static int[] CaptureCooldownActionIds(CharacterTestRuntimeAiLabCompiledLogicProgram program,
            IReadOnlyList<int> usedActions)
        {
            if (program?.TacticalSource == null) return Array.Empty<int>();
            var ids = new SortedSet<int>(program.TacticalSource.ConfiguredActionIds);
            for (int i = 0; i < usedActions.Count; i++) ids.Add(usedActions[i]);
            return ids.ToArray();
        }
        internal bool TryPrepareExplicit(CharacterTestCharacterRuntimeModule runtime, CharacterTestCharacterEntity entity,
            out CharacterTestTacticalCardInputs inputs, out string issue)
        {
            inputs = null; issue = "Explicit frozen slot bindings are missing.";
            return _explicitInputs != null && _explicitInputs.TryReprepareExplicit(runtime, entity, out inputs, out issue);
        }
        internal string WorkspaceId { get; }
        internal CharacterTestRuntimeAiLabCompiledLogicProgram CompiledProgram { get; }
        internal int WorkspaceRevision { get; }
        internal string CardId { get; }
        internal int CardRevision { get; }
        internal int CharacterId { get; }
        internal int ActionId { get; }
        internal bool TestActionOverride { get; }
        internal int StepBackActionId { get; }
        internal string StepBackContentHash { get; }
        internal int StepForwardActionId { get; }
        internal string StepForwardContentHash { get; }
        internal string ActionSetId { get; }
        internal string ActionContentHash { get; }
        internal int SummarySourceCharacterId { get; }
        internal bool UsesDefaultSummary { get; }
        internal string InputFingerprint { get; }
        internal string Description { get; }
        internal string DescriptionDetails { get; }
        internal IReadOnlyList<string> DescriptionWarnings => _descriptionWarnings;
        internal IReadOnlyDictionary<string, CharacterTestTacticalSourceLocation> Locations => _locations;
        internal IReadOnlyDictionary<string, CharacterTestTacticalAuthoringCompilation> SharedCompilations => _sharedCompilations;
        internal bool IsLegacy => _legacySource != null;
        internal CharacterTestTacticalDslSource CloneDocument() => CharacterTestTacticalDslJson.Read(DocumentText);
        internal CharacterTestTacticalAuthoringSource CloneLegacySource() => _legacySource == null ? null : CharacterTestTacticalAuthoringJson.Read(_legacySource);

        internal static CharacterTestTacticalFrozenSource Capture(CharacterTestRuntimeAiLabDraftWorkspace workspace,
            CharacterTestRuntimeAiLabBehaviorCardDraft card, CharacterTestTacticalDslSource document,
            CharacterTestTacticalCardInputs inputs, CharacterTestTacticalDslCompilation compiled)
        {
            var owned = CharacterTestTacticalDslJson.Read(CharacterTestTacticalDslJson.Write(document));
            var used = new HashSet<string>(Nodes(owned.Root).Where(n => n.Kind == "Reference").Select(n => n.DefinitionId), StringComparer.Ordinal);
            owned.Definitions.RemoveAll(d => !used.Contains(d.DefinitionId));
            return new CharacterTestTacticalFrozenSource(workspace.WorkspaceId, workspace.CurrentRevision, card.CardDraftId, card.CurrentRevision,
                null, null, inputs, compiled.SourceLocations,
                CharacterTestTacticalDslDescription.Generate(owned, inputs, compiled, true), compiled.SharedCompilations, compiled.Program, owned);
        }

        internal static CharacterTestTacticalFrozenSource CapturePublished(string id, int revision, CharacterTestTacticalDslSource document,
            CharacterTestTacticalCardInputs inputs, CharacterTestTacticalDslCompilation compiled)
            => new CharacterTestTacticalFrozenSource("published:" + id, revision, id, revision,
                CharacterTestTacticalDslJson.Write(document), null, inputs, compiled.SourceLocations,
                CharacterTestTacticalDslDescription.Generate(document, inputs, compiled, true), compiled.SharedCompilations, compiled.Program);

        internal static CharacterTestTacticalFrozenSource CaptureLegacy(CharacterTestRuntimeAiLabDraftWorkspace workspace,
            CharacterTestRuntimeAiLabBehaviorCardDraft card, CharacterTestTacticalCardInputs inputs,
            CharacterTestTacticalAuthoringCompilation compiled)
        {
            var source = card.CloneTacticalSource();
            var locations = new Dictionary<string, CharacterTestTacticalSourceLocation>(StringComparer.Ordinal);
            foreach (var timeline in source.Timelines)
                foreach (var evt in timeline.Events)
                    locations.Add(evt.EventId, new CharacterTestTacticalSourceLocation(card.CardDraftId, timeline.TimelineId, "events", evt: evt.EventId));
            var activation = source.ActivationConditionRoot; source.ActivationConditionRoot = null;
            // Description-only projection. The original v2 program and source are retained unchanged.
            var descriptionSource = new CharacterTestTacticalDslSource { CardId = card.CardDraftId, Name = card.DisplayName,
                ActivationConditionRoot = activation, Root = new CharacterTestTacticalDslNode { NodeId = "legacy-root", Kind = "Sequence",
                    Children = new List<CharacterTestTacticalDslNode> { CharacterTestTacticalDslTrialTemplates.Reference("legacy-use", "legacy-definition") } },
                Definitions = new List<CharacterTestTacticalSharedDefinition> { new CharacterTestTacticalSharedDefinition { DefinitionId = "legacy-definition", Source = source } } };
            return new CharacterTestTacticalFrozenSource(workspace?.WorkspaceId ?? "published:" + card.PublishedStableId,
                workspace?.CurrentRevision ?? card.CurrentRevision, card.CardDraftId, card.CurrentRevision,
                CharacterTestTacticalDslJson.Write(descriptionSource), card.TacticalSourceJson, inputs, locations,
                source.InteractionPreparation != null ? CharacterTestTacticalDslDescription.GenerateInteractionPreparation(source.InteractionPreparation.Freeze()) :
                source.PositionRecall != null ? CharacterTestTacticalDslDescription.GeneratePositionRecall(source.PositionRecall.Freeze()) :
                source.ExecutionInput?.Mechanism == "GroundTraction"
                    ? CharacterTestTacticalDslDescription.GenerateGroundTraction(source.ExecutionInput.Freeze())
                    : CharacterTestTacticalDslDescription.Generate(descriptionSource, inputs));
        }

        internal CharacterTestTacticalFrozenSource Rebind(CharacterTestTacticalCardInputs inputs,
            IReadOnlyDictionary<string, CharacterTestTacticalSourceLocation> locations,
            IReadOnlyDictionary<string, CharacterTestTacticalAuthoringCompilation> sharedCompilations = null,
            CharacterTestRuntimeAiLabCompiledLogicProgram compiledProgram = null)
        {
            var document = CloneDocument();
            return new CharacterTestTacticalFrozenSource(WorkspaceId, WorkspaceRevision, CardId, CardRevision,
                DocumentText, _legacySource, inputs, locations ?? _locations, CharacterTestTacticalDslDescription.Generate(document, inputs),
                sharedCompilations ?? _sharedCompilations, compiledProgram);
        }

        /// <remarks>Low-frequency installation/start only. No Library read and never a per-frame guard.</remarks>
        internal bool ValidateBinding(CharacterTestCharacterRuntimeModule runtime, CharacterTestCharacterEntity entity, out string issue)
        {
            issue = string.Empty;
            if (RequiredExecutionInputKind == TacticalCard.TacticalExecutionInputKind.WorldPoint)
            { issue = "execution-input:PointRequired"; return false; }
            if (runtime == null || entity == null || entity.Identity.CharacterId != CharacterId)
            { issue = "Frozen tactical character binding does not match the actual actor."; return false; }
            if (_explicitInputs != null) return _explicitInputs.ValidateExplicit(runtime, entity, out issue);
            if ((Requirements & CharacterTestTacticalInputRequirements.PrimaryAction) != 0)
            {
                if (!runtime.ActionRuntime.TryGetEntityActionSet(entity.Id, out var set)
                    || !CharacterTestTacticalCardInputs.TryResolveSelectedAction(set, runtime.ActionRuntime.Registry.Actions,
                        TestActionOverride ? ActionId : 0, out var action, out issue)
                    || action.ActionId.Value != ActionId)
                { issue = "Frozen tactical functional action changed. " + issue; return false; }
                if (!CharacterActionContentHashUtility.TryCompute(action, entity, runtime.PhysicsRuntime,
                    runtime.CombatTraceSystem.BakedTraceSource, runtime.CombatTraceSystem.WeaponTraceBindingResolver, out string actual, out issue)) return false;
                if (!string.Equals(actual, ActionContentHash, StringComparison.Ordinal))
                { issue = "Frozen tactical action/geometry dependencies changed; prepare and compile again."; return false; }
            }
            if (StepForwardActionId > 0)
            {
                if (!CharacterTestTacticalCardInputs.TryResolveStepForward(runtime.ActionRuntime.Registry.Actions, out var forward)
                    || forward.ActionId.Value != StepForwardActionId)
                { issue = "Frozen step-forward action binding changed."; return false; }
                if (!CharacterActionContentHashUtility.TryCompute(forward, entity, runtime.PhysicsRuntime,
                    runtime.CombatTraceSystem.BakedTraceSource, runtime.CombatTraceSystem.WeaponTraceBindingResolver, out var hash, out issue)) return false;
                if (hash != StepForwardContentHash) { issue = "Frozen step-forward action content changed."; return false; }
            }
            if (StepBackActionId > 0)
            {
                if (!CharacterTestTacticalCardInputs.TryResolveStepBack(runtime.ActionRuntime.Registry.Actions, out var step)
                    || step.ActionId.Value != StepBackActionId)
                { issue = "Frozen step-back action binding changed."; return false; }
                if (!CharacterActionContentHashUtility.TryCompute(step, entity, runtime.PhysicsRuntime,
                    runtime.CombatTraceSystem.BakedTraceSource, runtime.CombatTraceSystem.WeaponTraceBindingResolver, out var hash, out issue)) return false;
                if (hash != StepBackContentHash) { issue = "Frozen step-back action content changed."; return false; }
            }
            return true;
        }

        private static IEnumerable<CharacterTestTacticalDslNode> Nodes(CharacterTestTacticalDslNode node)
        {
            yield return node;
            foreach (var child in node.Children ?? new List<CharacterTestTacticalDslNode>())
                foreach (var item in Nodes(child)) yield return item;
        }
    }
}
