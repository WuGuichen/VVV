using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.CharacterAction;
using MxFramework.UI;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest
{
    internal sealed class CharacterTestCardUiPreviewData
    {
        internal string Id, Name, IconKey, Description, DescriptionDetails, Notice, Token, UnboundDescription;
        internal CharacterTestTacticalActionLocation[] Actions;
        internal CharacterTestTacticalApproachActionHint[] Hints;
        internal CharacterTestTacticalDescriptionSegment[] Segments;
        internal CharacterTestTacticalDescriptionFlow DescriptionFlow;
        internal CharacterTestTacticalUtilityFace UtilityFace;
        internal bool HasStructuredDescription;
        internal double FrameSeconds;
        internal string HintActionPath;
        internal CharacterTestUiTextProviderSignature TextSignature;
        internal int Cost;
        internal bool Dirty, Prepared, HasSwordArtwork;
    }

    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        internal IMxUiTextProvider CardDescriptionTextProvider => _context?.LocalizationRuntime?.TextProvider ?? (IMxUiTextProvider)MxUiNullTextProvider.Instance;
        internal double CardDescriptionFrameSeconds => _context?.SessionBuilder?.ResolveSimulationDeltaTime()
            ?? 1d / Math.Max(1, _context?.Profile?.TargetFrameRate ?? CharacterTestDeterministicUnits.DefaultTickRate);
        internal int CardDescriptionCharacterId => _matchConfig?.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex)?.CharacterId ?? 0;

        /// <remarks>LowFreqAlloc. Reads one working copy; never installs, saves or starts a card.</remarks>
        internal bool TryGetConfiguredCardUiPreview(string id, out CharacterTestCardUiPreviewData data, out string issue)
            => TryGetConfiguredCardUiPreview(id, null, out data, out issue);

        /// <remarks>LowFreqAlloc. Preview overrides belong to this read only projection, never to the working copy.</remarks>
        internal bool TryGetConfiguredCardUiPreview(string id, IReadOnlyDictionary<string, int> previewActions,
            out CharacterTestCardUiPreviewData data, out string issue, bool automaticallyPrepareSummaries = false)
        {
            data = null;
            issue = string.Empty;
            try
            {
                var copy = ConfiguredWorkingCopy(id);
                var card = copy.Card.Clone();
                var locations = ConfiguredActionLocations(card);
                if (previewActions != null)
                    foreach (var pair in previewActions)
                    {
                        if (pair.Value < 0 || !locations.Any(a => a.Path == pair.Key))
                            throw new InvalidOperationException("预览动作槽已变化，请恢复当前装配后重试。");
                        card.Actions[pair.Key] = pair.Value;
                    }
                var snapshot = CharacterTestTacticalPublishedSource.Read(card.Source);
                var document = CardUiPreviewDocument(card, snapshot);
                var notes = new List<string>();
                int character = CardDescriptionCharacterId;
                var inputs = PrepareCardUiPreviewInputs(card, document, character, notes, out string prepareIssue, automaticallyPrepareSummaries);
                locations = ConfiguredActionLocations(card);
                var catalog = CharacterTestActionBrowserCatalog.Get();
                var selectedNames = new Dictionary<string, string>(StringComparer.Ordinal);
                bool swordArtwork = false;
                foreach (var location in locations)
                {
                    int actionId = ResolveAuthoringActionId(location.ActionId);
                    var selected = catalog.Actions.FirstOrDefault(a => a.ActionId == actionId);
                    location.ActionName = actionId <= 0 ? CharacterTestUiText.Resolve(CardDescriptionTextProvider, "ui.character_test.tactical.description.unbound", "未装配")
                        : selected?.Name ?? CharacterTestUiText.Resolve(CardDescriptionTextProvider, "ui.character_test.tactical.description.invalid_action", "动作已失效");
                    if (locations.Length == 1 && selected?.Category == CharacterActionCategory.BasicAttack
                        && selected.Issues.Length == 0 && selected.WeaponTypes.Contains("sword")) swordArtwork = true;
                    if (selected != null) selectedNames.Add((snapshot.IsLegacy ? "preview-legacy/" : "") + location.Path, selected.Name);
                }
                bool active = _configuredCard?.Id == id && _configuredRole == TestCardRole;
                data = new CharacterTestCardUiPreviewData {
                    Id = id, Name = card.Name, IconKey = card.Metadata.IconKey, Cost = card.Metadata.Cost,
                    HasSwordArtwork = swordArtwork, Actions = locations, Token = ConfiguredFaceToken(copy),
                    Dirty = copy.ExpectedRevision == 0 || (active ? RecoverySignature() != _configuredSavedSignature
                        : _testCardLibrary.ReadRecovery(TestCardRole, id) != null)
                };
                CharacterTestTacticalCardPresentationProjection.Project(snapshot, document, data, inputs,
                    prepareIssue, selectedNames, CardDescriptionTextProvider, CardDescriptionFrameSeconds, notes);
                return true;
            }
            catch (Exception error) when (error is InvalidOperationException || error is ArgumentException || error is JsonException)
            {
                issue = error.Message;
                return false;
            }
        }

        private static CharacterTestTacticalDslSource CardUiPreviewDocument(CharacterTestConfiguredCard card,
            CharacterTestTacticalPublishedSource snapshot)
            => CharacterTestTacticalCardPresentationProjection.Document(card.Id, card.Name, snapshot);

        private CharacterTestTacticalCardInputs PrepareCardUiPreviewInputs(CharacterTestConfiguredCard card,
            CharacterTestTacticalDslSource document, int character, List<string> notes, out string issue, bool automaticallyPrepareSummaries)
            => CharacterTestTacticalCardPresentationProjection.Prepare(CharacterTestTacticalPublishedSource.Read(card.Source),
                document, ConfiguredActionLocations(card), character, notes,
                () => CharacterTestTacticalPreparationCatalog.Role(character, _context?.Slice?.CharacterRuntime?.ActionRuntime),
                action => { var compatibility = EvaluateBrowserAction(action, force: true);
                    return compatibility.CanSelect || automaticallyPrepareSummaries && NeedsAutomaticSummaryRebuild(compatibility)
                        ? null : compatibility.Reason; }, out issue,
                automaticallyPrepareSummaries && !IsHistoricalFrameSelected ? _cardUiSummaries.Prepare : null);
    }

    /// <summary>Read-only card-face projection shared by configured previews and exact published cards.
    /// Isolated authored metrics prepare display text only; this never creates a command, reserves motion, or saves a card.</summary>
    internal static class CharacterTestTacticalCardPresentationProjection
    {
        internal static CharacterTestCardUiPreviewData FromDeployment(TacticalCard.TacticalCardDefinition definition,
            TacticalCard.TacticalDeploymentParticipant participant, TacticalCard.CharacterTacticalContentCatalog catalog,
            CharacterTestCharacterActionRuntime actions, IMxUiTextProvider provider, double frameSeconds)
        {
            var display = TacticalCard.TacticalCardDisplayData.FromDefinition(definition);
            var data = new CharacterTestCardUiPreviewData { Id = display.PublishedCardId, Name = display.Name,
                Cost = display.Cost, IconKey = display.IconKey, Description = display.Description,
                Token = $"{display.PublishedCardId}@{display.PublishedRevision}:{display.PublishedContentHash}/{display.DeckEntryInstanceId}",
                Actions = Array.Empty<CharacterTestTacticalActionLocation>() };
            if (definition is TacticalCard.TacticalUtilityCardDefinition utility
                && utility.Payload is TacticalCard.InteractionPreparationPayload preparation)
            {
                var fixedDisplay = CharacterTestTacticalDslDescription.GenerateInteractionPreparation(preparation);
                data.UtilityFace = fixedDisplay.UtilityFace; data.Description = fixedDisplay.Text;
                data.DescriptionDetails = fixedDisplay.Details + "\n" + string.Join("\n", fixedDisplay.Warnings);
                data.UnboundDescription = data.Description; data.Prepared = true;
                return data;
            }
            var metadata = catalog?.Cards.FirstOrDefault(card => card.CardId == display.PublishedCardId
                && card.Revision == display.PublishedRevision && card.ContentHash == display.PublishedContentHash);
            var source = metadata?.PresentationSource;
            if (source == null || participant == null
                || !participant.TryGetBindings(display.DeckEntryInstanceId, out var bindings)) return data;
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            var locations = new List<CharacterTestTacticalActionLocation>();
            foreach (var pair in bindings)
            {
                var action = catalog.Actions.FirstOrDefault(value => value.ActionId == pair.Value
                    && value.CharacterStableIds.Contains(participant.CharacterStableId));
                string name = action?.DisplayName ?? "未装配动作";
                names.Add((source.IsLegacy ? "preview-legacy/" : string.Empty) + pair.Key, name);
                locations.Add(new CharacterTestTacticalActionLocation { Path = pair.Key, Label = pair.Key,
                    ActionName = name, ActionId = CharacterTestRuntimeAiPlannerDevSceneController.AuthoringActionKey(pair.Value) });
            }
            data.Actions = locations.ToArray();
            var document = Document(display.PublishedCardId, display.Name, source);
            var notes = new List<string>();
            var inputs = Prepare(source, document, data.Actions, participant.CharacterId, notes,
                () => actions == null ? null : CharacterTestTacticalPreparationCatalog.Role(participant.CharacterId, actions),
                null, out string issue);
            Project(source, document, data, inputs, issue, names, provider, frameSeconds, notes);
            return data;
        }

        internal static CharacterTestTacticalDslSource Document(string id, string name,
            CharacterTestTacticalPublishedSource snapshot)
        {
            if (!snapshot.IsLegacy)
            {
                var composition = snapshot.CloneComposition();
                return new CharacterTestTacticalDslSource { CardId = id, Name = name,
                    Root = composition.Root, ActivationConditionRoot = composition.ActivationConditionRoot,
                    Definitions = snapshot.CloneDefinitions().ToList() };
            }
            var legacy = snapshot.CloneLegacy();
            var activation = legacy.ActivationConditionRoot;
            legacy.ActivationConditionRoot = null;
            return new CharacterTestTacticalDslSource { CardId = id, Name = name,
                ActivationConditionRoot = activation,
                Root = new CharacterTestTacticalDslNode { NodeId = "preview-root", Kind = "Sequence",
                    Children = new List<CharacterTestTacticalDslNode> {
                        CharacterTestTacticalDslTrialTemplates.Reference("preview-legacy", "preview-definition") } },
                Definitions = new List<CharacterTestTacticalSharedDefinition> {
                    new CharacterTestTacticalSharedDefinition { DefinitionId = "preview-definition", Source = legacy } } };
        }


        internal static CharacterTestTacticalCardInputs Prepare(CharacterTestTacticalPublishedSource source,
            CharacterTestTacticalDslSource document, CharacterTestTacticalActionLocation[] locations, int character, List<string> notes,
            Func<CharacterTestTacticalRoleData> roleFactory, Func<int, string> eligibilityIssue, out string issue,
            Func<CharacterTestTacticalRoleData, int, string, string> prepareSummary = null,
            CharacterActionTacticalMetricsStore store = null)
        {
            issue = string.Empty;
            var prepared = new Dictionary<string, CharacterTestTacticalCardInputs>(StringComparer.Ordinal);
            var requirements = CharacterTestTacticalDependencies.Analyze(document);
            if (locations.Length == 0 && (requirements & CharacterTestTacticalInputRequirements.FunctionalActions) == 0)
                return CharacterTestTacticalCardInputs.BindSlots(character, prepared);
            if (locations.Length > 0 && locations.All(a => CharacterTestRuntimeAiPlannerDevSceneController.ResolveAuthoringActionId(a.ActionId) <= 0))
            { issue = "尚未装配动作，当前展示未装配说明。"; return CharacterTestTacticalCardInputs.BindSlots(character, prepared); }
            CharacterTestTacticalRoleData role;
            try { role = roleFactory(); }
            catch (Exception error) when (error is InvalidOperationException || error is ArgumentException)
            { issue = error.Message; return CharacterTestTacticalCardInputs.BindSlots(character, prepared); }
            if (role == null && (locations.Length != 0 || (requirements & CharacterTestTacticalInputRequirements.FunctionalActions) != 0))
            { issue = "当前角色装配不可用；预览仅展示可解析的源意图。"; return CharacterTestTacticalCardInputs.BindSlots(character, prepared); }
            store ??= new CharacterActionTacticalMetricsStore();
            bool legacy = source.IsLegacy;
            foreach (var location in locations)
            {
                int actionId = CharacterTestRuntimeAiPlannerDevSceneController.ResolveAuthoringActionId(location.ActionId);
                if (actionId <= 0)
                { issue = "部分动作尚未装配。"; notes.Add(location.Label + "：未装配"); continue; }
                string incompatible = eligibilityIssue?.Invoke(actionId);
                if (!string.IsNullOrEmpty(incompatible))
                { issue = "动作装配不兼容。"; notes.Add(location.Label + "：" + incompatible); continue; }
                if (!CharacterTestTacticalCardInputs.TryPrepare(role.Actions, role.Source, role.Physics,
                    CharacterTestDefaultBakedWeaponTraceSource.Instance, CharacterTestDefaultWeaponTraceBindingResolver.Instance,
                    store, CharacterTestTacticalSummaryPreparation.StandardMeasurement(), out var value, out string slotIssue, actionId,
                    CharacterTestTacticalInputRequirements.PrimaryAction | CharacterTestTacticalInputRequirements.SpatialSummary
                    | CharacterTestTacticalInputRequirements.FunctionalActions))
                { issue = "部分装配准备失败。";
                    notes.Add(location.Label + "：" + (prepareSummary?.Invoke(role, actionId, slotIssue) ?? slotIssue));
                    // Nominal action timing is authored data, independent of a valid spatial measurement.
                    // Keep the preparation issue so partial display inputs cannot mark the card executable.
                    if (slotIssue.StartsWith("ActionSummary:", StringComparison.Ordinal)
                        && CharacterTestTacticalCardInputs.TryPrepare(role.Actions, role.Source, role.Physics,
                            CharacterTestDefaultBakedWeaponTraceSource.Instance, CharacterTestDefaultWeaponTraceBindingResolver.Instance,
                            store, CharacterTestTacticalSummaryPreparation.StandardMeasurement(), out var actionOnly, out _, actionId,
                            CharacterTestTacticalInputRequirements.PrimaryAction | CharacterTestTacticalInputRequirements.FunctionalActions))
                        prepared.Add((legacy ? "preview-legacy/" : string.Empty) + location.Path, actionOnly);
                    continue; }
                prepared.Add((legacy ? "preview-legacy/" : string.Empty) + location.Path, value);
                if (value.UsesDefaultSummary)
                    notes.Add(location.Label + "：距离推荐借用默认角色 " + value.SummarySourceCharacterId + " 的同动作测量摘要。");
            }
            if (locations.Length == 0 && (requirements & CharacterTestTacticalInputRequirements.FunctionalActions) != 0)
            {
                if (!CharacterTestTacticalCardInputs.TryPrepare(role.Actions, role.Source, role.Physics,
                    CharacterTestDefaultBakedWeaponTraceSource.Instance, CharacterTestDefaultWeaponTraceBindingResolver.Instance,
                    store, CharacterTestTacticalSummaryPreparation.StandardMeasurement(), out var step, out issue,
                    0, CharacterTestTacticalInputRequirements.FunctionalActions)) return CharacterTestTacticalCardInputs.BindSlots(character, prepared);
                return CharacterTestTacticalCardInputs.BindSlots(character, prepared, step.StepBackAction, step.StepBackContentHash, step.StepForwardAction, step.StepForwardContentHash);
            }
            return CharacterTestTacticalCardInputs.BindSlots(character, prepared);
        }

        internal static void Project(CharacterTestTacticalPublishedSource source, CharacterTestTacticalDslSource document,
            CharacterTestCardUiPreviewData data, CharacterTestTacticalCardInputs inputs, string preparationIssue,
            IReadOnlyDictionary<string, string> selectedNames, IMxUiTextProvider provider, double frameSeconds, List<string> notes)
        {
            data.UtilityFace = null;
            // Fixed preparation payloads have no DSL event body; share the Web description authority.
            if (source.InteractionPreparation != null)
            {
                var preparation = CharacterTestTacticalDslDescription.GenerateInteractionPreparation(source.InteractionPreparation);
                data.UtilityFace = preparation.UtilityFace;
                data.Description = preparation.Text;
                data.DescriptionDetails = preparation.Details + "\n" + string.Join("\n", preparation.Warnings);
                data.Hints = preparation.Hints; data.Segments = preparation.Segments;
                data.DescriptionFlow = preparation.DescriptionFlow; data.HasStructuredDescription = preparation.HasStructuredDescription;
                data.FrameSeconds = frameSeconds; data.UnboundDescription = data.Description; data.HintActionPath = null;
                data.TextSignature = CharacterTestUiTextProviderSignature.Create(provider);
                notes.AddRange(preparation.Warnings);
                data.Notice = string.Join("\n", notes.Distinct()); data.Prepared = true;
                return;
            }
            if (source.PositionRecall != null)
            {
                var recall = CharacterTestTacticalDslDescription.GeneratePositionRecall(source.PositionRecall, frameSeconds);
                data.Description = recall.Text; data.DescriptionDetails = recall.Details + "\n" + string.Join("\n", recall.Warnings);
                data.Hints = Array.Empty<CharacterTestTacticalApproachActionHint>(); data.Segments = Array.Empty<CharacterTestTacticalDescriptionSegment>();
                data.DescriptionFlow = null; data.HasStructuredDescription = false; data.FrameSeconds = frameSeconds;
                data.UnboundDescription = data.Description; data.HintActionPath = null; data.TextSignature = CharacterTestUiTextProviderSignature.Create(provider);
                data.Notice = string.Join("\n", notes.Distinct()); data.Prepared = true; return;
            }
            // Point programs are built per execution; their deferred source has no DSL event body.
            if (source.PointMove != null)
            {
                var rules = source.PointMove;
                string M(int value) => (value / 1000d).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                if (rules.IsGroundTraction)
                {
                    var traction = CharacterTestTacticalDslDescription.GenerateGroundTraction(rules, frameSeconds);
                    data.Description = traction.Text; data.DescriptionDetails = traction.Details + "\n" + string.Join("\n", traction.Warnings);
                    data.Hints = Array.Empty<CharacterTestTacticalApproachActionHint>();
                    data.Segments = Array.Empty<CharacterTestTacticalDescriptionSegment>();
                    data.DescriptionFlow = null; data.HasStructuredDescription = false; data.FrameSeconds = frameSeconds;
                    data.UnboundDescription = data.Description; data.HintActionPath = null;
                    data.TextSignature = CharacterTestUiTextProviderSignature.Create(provider);
                    data.Notice = string.Join("\n", notes.Distinct()); data.Prepared = true; return;
                }
                data.Description = CharacterTestUiText.Resolve(provider, "ui.character_test.tactical.description.point_move", "移动至指定位置");
                string movementDetails = CharacterTestUiText.Resolve(provider, "ui.character_test.tactical.description.point_move_rules",
                    "选择 {range} 米范围内的可达地面点，以 {speed} 倍常规移动前往；距地点 {arrival} 米内停止。")
                    .Replace("{range}", M(rules.Input.RadiusMillimeters)).Replace("{speed}", M(rules.SpeedMilli))
                    .Replace("{arrival}", M(rules.ArrivalMillimeters));
                data.DescriptionDetails = movementDetails + "\n" + CharacterTestUiText.Resolve(provider, "ui.character_test.tactical.description.point_move_details",
                    "确认及实际开始时按当前位置检查范围与路径；有效移动接纳后开始结算，取消退回预留。执行上限 {seconds} 秒。")
                    .Replace("{seconds}", (rules.MaximumFrames * frameSeconds).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                data.Hints = Array.Empty<CharacterTestTacticalApproachActionHint>();
                data.Segments = Array.Empty<CharacterTestTacticalDescriptionSegment>();
                data.DescriptionFlow = null;
                data.HasStructuredDescription = false; data.FrameSeconds = frameSeconds;
                data.UnboundDescription = data.Description; data.HintActionPath = null;
                data.TextSignature = CharacterTestUiTextProviderSignature.Create(provider);
                data.Notice = string.Join("\n", notes.Distinct()); data.Prepared = true;
                return;
            }
            CharacterTestTacticalDslCompilation compiled = null;
            if (string.IsNullOrEmpty(preparationIssue))
                CharacterTestTacticalDslCompiler.TryCompile(document, inputs, out compiled, out preparationIssue);
            var description = CharacterTestTacticalDslDescription.GenerateLocalized(document, inputs, compiled,
                attempted: true, preparationIssue: preparationIssue, cardFace: true, selectedActions: selectedNames,
                textProvider: provider, frameSeconds: frameSeconds);
            if (!string.IsNullOrEmpty(preparationIssue)) notes.Add(preparationIssue);
            notes.AddRange(description.Warnings);
            if (string.IsNullOrWhiteSpace(data.IconKey)) notes.Add("图标未配置，使用中性占位图标。");
            data.Description = description.Text; data.DescriptionDetails = description.Details; data.DescriptionFlow = description.DescriptionFlow;
            data.Hints = description.Hints; data.Segments = description.Segments;
            data.HasStructuredDescription = description.HasStructuredDescription; data.FrameSeconds = frameSeconds;
            data.HintActionPath = description.Hints.Length == 1 ? (source.IsLegacy
                ? description.Hints[0].ActionSlotPath.Substring("preview-legacy/".Length) : description.Hints[0].ActionSlotPath) : null;
            var unbound = CharacterTestTacticalCardInputs.BindSlots(inputs.SourceCharacterId,
                new Dictionary<string, CharacterTestTacticalCardInputs>(StringComparer.Ordinal));
            data.UnboundDescription = CharacterTestTacticalDslDescription.GenerateLocalized(document, unbound,
                attempted: true, cardFace: true, textProvider: provider, frameSeconds: frameSeconds).Text;
            data.TextSignature = CharacterTestUiTextProviderSignature.Create(provider);
            data.Notice = string.Join("\n", notes.Distinct()); data.Prepared = compiled != null;
        }
    }
}
