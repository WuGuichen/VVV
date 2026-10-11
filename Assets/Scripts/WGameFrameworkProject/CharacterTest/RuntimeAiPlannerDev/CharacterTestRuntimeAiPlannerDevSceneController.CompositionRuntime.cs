using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private readonly Dictionary<string, int> _tacticalTestActions = new Dictionary<string, int>();
        internal string TacticalPreviewRoleLabel
        {
            get
            {
                var slot = _matchConfig?.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex);
                if (slot == null || slot.CharacterId <= 0) return "预览角色未配置";
                string name = "角色 " + slot.CharacterId;
                foreach (var definition in CharacterTestPlayableCharacterCatalog.Discover())
                    if (definition.CharacterId == slot.CharacterId) { name = definition.DisplayName; break; }
                return "T" + _selectedSetupTeamId + "·" + (_selectedSetupSlotIndex + 1) + " · " + name
                    + (slot.Enabled ? string.Empty : "（槽位未启用）");
            }
        }

        internal bool TacticalPreviewRoleEnabled =>
            _matchConfig?.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex)?.Enabled == true;

        // The authoring role owns preview binding, rather than whichever agent was last inspected.
        internal void BindTacticalPreviewRole()
        {
            if (IsHistoricalFrameSelected || !_frameBuffer.TryGetSelected(out var frame)
                || _sessionBuilder == null
                || !_sessionBuilder.TryResolveArenaRuntimeAiPlannerProfileSlot(
                    _selectedSetupTeamId, _selectedSetupSlotIndex, out int profile)) return;
            for (int i = 0; i < frame.Agents.Count; i++)
                if (frame.Agents[i].RuntimeAiPlannerProfileSlot == profile)
                { _selectedAgentIndex = i; return; }
        }
        private string TacticalTestActionKey(int team, int slot, int character)
            => DraftWorkspace?.WorkspaceId + ":" + team + ":" + slot + ":" + character;
        private int TacticalTestAction(int team, int slot, int character)
            => _tacticalTestActions.TryGetValue(TacticalTestActionKey(team, slot, character), out int id) ? id : 0;
        internal int SelectedTacticalTestAction => TacticalTestAction(_selectedSetupTeamId, _selectedSetupSlotIndex,
            _matchConfig?.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex)?.CharacterId ?? 0);
        internal string SelectedTacticalTestActionKey => TacticalTestActionKey(_selectedSetupTeamId, _selectedSetupSlotIndex,
            _matchConfig?.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex)?.CharacterId ?? 0);

        internal List<(int id, string label)> GetTacticalTestActionChoices()
        {
            var result = new List<(int, string)>();
            int character = _matchConfig?.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex)?.CharacterId ?? 0;
            try
            {
                foreach (var action in ResolveCharacterActionRegistry().Actions)
                        if (CharacterTestTacticalCardInputs.TryValidateAction(action, out _))
                            result.Add((action.ActionId.Value, action.DisplayName + " · " + action.ActionId.Value));
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
            { _statusMessage = "动作目录不可用：" + error.Message; }
            return result;
        }

        internal bool SelectTacticalTestAction(int actionId, string expectedKey = null)
        {
            if (expectedKey != null && expectedKey != SelectedTacticalTestActionKey)
            { _statusMessage = "当前角色槽位已变化，请重新选择动作。"; return false; }
            if (DraftWorkspace == null || DraftWorkspace.IsReadOnly || IsHistoricalFrameSelected) return false;
            int character = _matchConfig?.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex)?.CharacterId ?? 0;
            if (character <= 0) return false;
            if (actionId > 0)
            {
                try
                {
                    if (!ResolveCharacterActionRegistry().TryGetAction(new CharacterActionId(actionId), out var selectedAction))
                    { _statusMessage = "所选动作不存在。"; return false; }
                    if (!CharacterTestTacticalCardInputs.TryValidateAction(selectedAction, out var issue))
                        { _statusMessage = issue; return false; }
                }
                catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
                { _statusMessage = "所选动作不可用于当前装配：" + error.Message; return false; }
            }
            string key = TacticalTestActionKey(_selectedSetupTeamId, _selectedSetupSlotIndex, character);
            if (actionId <= 0) _tacticalTestActions.Remove(key); else _tacticalTestActions[key] = actionId;
            RefreshTacticalPreview();
            _statusMessage = actionId <= 0 ? "主攻击槽已恢复默认；测试当前修改后生效。" : "测试主攻击槽已替换；准备所选动作摘要后测试当前修改。";
            return true;
        }

        private int TacticalTestActionForEntity(CharacterTestCharacterEntity entity)
        {
            foreach (int team in new[] { CharacterTestArenaMatchConfig.Team1Id, CharacterTestArenaMatchConfig.Team2Id })
                for (int slot = 0; slot < CharacterTestArenaMatchConfig.SlotsPerTeam; slot++)
                    if (_sessionBuilder != null && _sessionBuilder.TryResolveArenaRuntimeAiPlannerProfileSlot(team, slot, out int profile)
                        && profile == entity.Control.RuntimeAiPlannerProfileSlot)
                        return TacticalTestAction(team, slot, entity.Identity.CharacterId);
            return 0;
        }
        private CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult CompileAuthoredCard(CharacterTestRuntimeAiLabBehaviorCardDraft card, bool fresh)
        {
            using var startupProfile = CharacterTestStartupProfiler.CompileCard.Auto();
            var workspace = DraftWorkspace;
            if (workspace == null || card == null) return TacticalFailure("TACTICAL_WORKSPACE_UNAVAILABLE", "当前工作副本不可用。");
            int character = _matchConfig?.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex)?.CharacterId ?? 0;
            string owner = workspace.WorkspaceId + ":" + character + ":" + _selectedSetupTeamId + ":" + _selectedSetupSlotIndex
                + ":" + (_context?.Slice?.GetHashCode() ?? 0) + ":" + CharacterTestTacticalPreparationCatalog.Revision
                + ":" + _tacticalSlotPreviewRevision + ":" + (TacticalCard.CharacterTacticalServices.Player?.Revision ?? 0);
            if (owner != _tacticalCompileOwner) { _tacticalCompiled.Clear(); _tacticalCompileOwner = owner; }
            InvalidateTacticalActorChanges();
            _tacticalCompiled.TryGetValue(card.CardDraftId, out var prior);
            if (!fresh && prior?.CardRevision == card.CurrentRevision
                && prior.ActionChoice == ((prior.Requirements & CharacterTestTacticalInputRequirements.PrimaryAction) != 0 ? SelectedTacticalTestAction : 0)) return prior.Result;
            var document = card.HasCompositionSource ? workspace.ResolveComposition(card, referencedOnly: true) : null;
            var legacy = document == null ? card.CloneTacticalSource() : null;
            var requirements = document != null ? CharacterTestTacticalDependencies.Analyze(document) : CharacterTestTacticalDependencies.Analyze(legacy);
            string semantic = document != null ? CharacterTestTacticalDslJson.Fingerprint(document, document.Definitions)
                : CharacterTestTacticalAuthoringJson.SemanticFingerprint(legacy);
            var entry = new TacticalCompiledEntry { CardRevision = card.CurrentRevision, SemanticSource = semantic, Requirements = requirements,
                ActionChoice = (requirements & CharacterTestTacticalInputRequirements.PrimaryAction) != 0 ? SelectedTacticalTestAction : 0 };
            if (!TryPrepareCardSlotBindings(card, character, out var inputs, out string issue))
            {
                entry.Request = _preparedSummaryRequest;
                entry.Result = TacticalFailure("TACTICAL_INPUTS_UNAVAILABLE", issue);
                if (document != null) entry.Description = CharacterTestTacticalDslDescription.Generate(document, null, attempted: true, preparationIssue: issue);
                _tacticalCompiled[card.CardDraftId] = entry; return entry.Result;
            }
            entry.Inputs = inputs; entry.Request = _preparedSummaryRequest;
            if (document != null)
            {
                if (prior?.Dsl != null && prior.SemanticSource == semantic && prior.Inputs?.DependencyFingerprint == inputs.DependencyFingerprint)
                    entry.Dsl = prior.Dsl;
                else
                {
                    TacticalCompileBuildCount++;
                    CharacterTestTacticalDslCompiler.TryCompile(document, inputs, out entry.Dsl, out issue);
                }
                if (entry.Dsl == null) entry.Result = TacticalFailure("TACTICAL_COMPOSITION_INVALID", issue);
                else
                {
                    var frozen = CharacterTestTacticalFrozenSource.Capture(workspace, card, document, inputs, entry.Dsl);
                    var program = entry.Dsl.Program.WithTacticalSource(frozen);
                    entry.Result = new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(program
                        .WithSelectionDefinition(CharacterTestBehaviorCardSelectionDefinition.FromTacticalCard(card, program)), Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>());
                }
                entry.Description = CharacterTestTacticalDslDescription.Generate(document, inputs, entry.Dsl, true, issue);
            }
            else
            {
                TacticalCompileBuildCount++;
                if (!CharacterTestTacticalAuthoringCompiler.TryCompile(legacy, inputs, out var compiled, out var errors))
                    entry.Result = TacticalFailure("TACTICAL_SOURCE_INVALID", string.Join("; ", errors));
                else
                {
                    entry.Legacy = compiled;
                    var program = compiled.Program.WithTacticalSource(CharacterTestTacticalFrozenSource.CaptureLegacy(workspace, card, inputs, compiled));
                    entry.Result = new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(program
                        .WithSelectionDefinition(CharacterTestBehaviorCardSelectionDefinition.FromTacticalCard(card, program)), Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>());
                }
            }
            _tacticalCompiled[card.CardDraftId] = entry; return entry.Result;
        }

        private static CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult TacticalFailure(string code, string message)
            => new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(null, new[] {
                new CharacterTestRuntimeAiLabCardDiagnostic(CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error, code, "$", message) });

        private bool TryPrepareSelectedTacticalInputs(int character, out CharacterTestTacticalCardInputs inputs, out string issue)
            => TryPrepareTacticalSlotInputs(_selectedSetupTeamId, _selectedSetupSlotIndex, character, out inputs, out issue);

        private bool TryPrepareTacticalSlotInputs(int team, int slot, int character, out CharacterTestTacticalCardInputs inputs, out string issue)
            => TryPrepareTacticalSlotInputsCore(team, slot, character, CharacterTestTacticalInputRequirements.All, false, out inputs, out issue);

        private static string TargetIssue(int team, int slot, int character, int action, string issue)
            => "团队 " + team + " / 槽 " + slot + " / 角色 " + character + " / 动作 " + (action > 0 ? action.ToString() : "未解析") + "：" + issue;

        private bool TryValidateTacticalGroupTargets(CharacterTestRuntimeAiLabBehaviorGroupDraft group, out string diagnostic)
        {
            diagnostic = string.Empty;
            var workspace = DraftWorkspace;
            var cards = group.Entries.Where(e => e != null && workspace.Cards.ContainsKey(e.CardDraftId))
                .Select(e => workspace.Cards[e.CardDraftId]).Where(c => c.HasAuthoredSource).Distinct().ToArray();
            if (cards.Length == 0) return true;
            int groupId = _matchConfig.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex)?.BehaviorGroupDraftId ?? 0;
            foreach (int team in new[] { CharacterTestArenaMatchConfig.Team1Id, CharacterTestArenaMatchConfig.Team2Id })
                for (int slot = 0; slot < CharacterTestArenaMatchConfig.SlotsPerTeam; slot++)
                {
                    var configured = _matchConfig.TryGetSlot(team, slot);
                    if (configured == null || !configured.Enabled || configured.BehaviorGroupDraftId != groupId) continue;
                    foreach (var card in cards)
                    {
                        var document = card.HasCompositionSource ? workspace.ResolveComposition(card, referencedOnly: true) : null;
                        var legacy = document == null ? card.CloneTacticalSource() : null;
                        var requirements = document == null ? CharacterTestTacticalDependencies.Analyze(legacy) : CharacterTestTacticalDependencies.Analyze(document);
                        if (!TryPrepareTacticalSlotInputsCore(team, slot, configured.CharacterId, requirements, false, out var inputs, out diagnostic)) return false;
                        bool valid;
                        string issue;
                        if (card.HasCompositionSource) valid = CharacterTestTacticalDslCompiler.TryCompile(document, inputs, out _, out issue);
                        else
                        {
                            valid = CharacterTestTacticalAuthoringCompiler.TryCompile(legacy, inputs, out _, out var errors);
                            issue = string.Join("; ", errors);
                        }
                        if (!valid) { diagnostic = TargetIssue(team, slot, configured.CharacterId, inputs.Action?.ActionId.Value ?? 0, card.DisplayName + "：" + issue); return false; }
                    }
                }
            return true;
        }

        private bool PrepareTacticalInputs(CharacterTestCharacterRuntimeModule runtime, CharacterTestCharacterEntity entity,
            out CharacterTestTacticalCardInputs inputs, out string issue, int? testActionId = null,
            CharacterTestTacticalInputRequirements requirements = CharacterTestTacticalInputRequirements.All)
            => CharacterTestTacticalCardInputs.TryPrepare(runtime.ActionRuntime, entity, runtime.PhysicsRuntime,
                runtime.CombatTraceSystem.BakedTraceSource, runtime.CombatTraceSystem.WeaponTraceBindingResolver,
                new CharacterActionTacticalMetricsStore(), CharacterTestTacticalSummaryPreparation.StandardMeasurement(), out inputs, out issue,
                testActionId ?? TacticalTestActionForEntity(entity), requirements);

        private CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult CompileBehaviorCardForStart(CharacterTestRuntimeAiLabBehaviorCardDraft card)
        {
            EnsureBehaviorCardReferences();
            return card?.HasAuthoredSource == true ? CompileAuthoredCard(card, true) : CompileBehaviorCardLogic(card);
        }

        internal bool TryResolveFrozenTacticalLocation(int frame, int profileSlot, long authorityHash, long programHash,
            string elementId, out CharacterTestTacticalFrozenSource source, out CharacterTestTacticalSourceLocation location)
        {
            source = null; location = null;
            if (!TryResolveAppliedRevisionAtFrame(frame, profileSlot, authorityHash, out var revision)) return false;
            foreach (var program in revision.LogicPrograms)
            {
                if (program?.SemanticHash != programHash || program.TacticalSource == null) continue;
                source = program.TacticalSource;
                return elementId != null && source.Locations.TryGetValue(elementId, out location);
            }
            return false;
        }
    }
}
