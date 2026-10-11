using System;
using System.Linq;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    internal static class CharacterTestTacticalAuthoringEditorBridge
    {
        internal static Action<CharacterTestRuntimeAiPlannerDevSceneController, string, int, int, int> ImportRequested;
        internal static Action<CharacterTestRuntimeAiPlannerDevSceneController, CharacterTestTacticalSummaryRequest> SummaryRequested;
        internal static Action<CharacterTestRuntimeAiPlannerDevSceneController, CharacterTestTacticalSummaryRequest, bool> SummaryLibraryRequested;
    }

    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        internal long TacticalPresentationRevision { get; private set; }
        internal bool NativeTacticalExecutionView { get; set; }

        internal void RequestTacticalImport()
        {
            var workspace = DraftWorkspace;
            if (workspace == null || workspace.IsReadOnly) return;
            CharacterTestTacticalAuthoringEditorBridge.ImportRequested?.Invoke(this,
                workspace.WorkspaceId, SelectedSetupTeamId, SelectedSetupSlotIndex, workspace.CurrentRevision);
        }

        internal bool ImportTacticalSource(string workspaceId, int teamId, int slotIndex, int revision, string json, out string message)
        {
            if (!CharacterTestTacticalAuthoringJson.TryRead(json, out var source, out message)) return false;
            if (DraftWorkspace == null || !string.Equals(DraftWorkspace.WorkspaceId, workspaceId, StringComparison.Ordinal))
            { message = "工作副本已切换，请在当前工作副本重新导入。"; return false; }
            bool success = DraftWorkspace.TryImportTacticalSource(teamId, slotIndex, revision, source, out _, out message);
            if (success) _draftPersistence?.MarkDirty();
            _statusMessage = message;
            TacticalPresentationRevision++;
            return success;
        }

        /// <remarks>LowFreqAlloc: current-source preview only. Candidate/start/publish use their own validated boundary.</remarks>
        internal CharacterTestTacticalAuthoringPreview BuildTacticalPreview(CharacterTestRuntimeAiLabBehaviorCardDraft card,
            CharacterTestTacticalFrozenSource frozen = null, CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtimeIdentity = null)
        {
            if (_inspectedTacticalRuntimeSource != null && _inspectedTacticalRuntimeSource.CardId == card?.CardDraftId
                && _inspectedTacticalRuntimeGeneration == _timelineSessionGeneration)
                frozen = _inspectedTacticalRuntimeSource;
            if (frozen == null && card?.HasAuthoredSource == true) BindTacticalPreviewRole();
            InvalidateTacticalActorChanges();
            var key = CaptureTacticalPreviewCacheKey(card, frozen);
            if (_hasTacticalPreviewCache && _pendingCurrentTacticalLocation == null && key.Equals(_tacticalPreviewCacheKey))
            {
                TacticalPreviewCacheHits++;
                // Runtime ownership and frame locations are live; the authored projection is not.
                _compositionEditingSession?.BindRuntimeSources(runtimeIdentity);
                return _cachedTacticalPreview;
            }
            var result = BuildTacticalPreviewCore(card, frozen, runtimeIdentity);
            TacticalPreviewBuildCount++;
            _cachedTacticalPreview = result;
            _tacticalPreviewCacheKey = CaptureTacticalPreviewCacheKey(card, frozen);
            _hasTacticalPreviewCache = true;
            return result;
        }

        private CharacterTestTacticalAuthoringPreview BuildTacticalPreviewCore(CharacterTestRuntimeAiLabBehaviorCardDraft card,
            CharacterTestTacticalFrozenSource frozen, CharacterTestRuntimeAiLabBehaviorCardRuntimeIdentityResolution runtimeIdentity)
        {
            _activeTacticalCardId = card?.HasAuthoredSource == true ? card.CardDraftId : string.Empty;
            if (card?.HasCompositionSource == true)
            {
                if (_inspectedTacticalRuntimeSource != null)
                {
                    if (_inspectedTacticalRuntimeSource.CardId == card.CardDraftId && _inspectedTacticalRuntimeGeneration == _timelineSessionGeneration)
                        frozen = _inspectedTacticalRuntimeSource;
                    else _inspectedTacticalRuntimeSource = null;
                }
                if (_compositionEditingSession == null) _compositionEditingSession = new CharacterTestTacticalCompositionEditingSession(this);
                _compositionEditingSession.Bind(DraftWorkspace, card, frozen);
                _compositionEditingSession.BindRuntimeSources(runtimeIdentity);
                if (_pendingCurrentTacticalLocation != null && frozen == null)
                {
                    var location = _pendingCurrentTacticalLocation; _pendingCurrentTacticalLocation = null;
                    string nodeId = string.IsNullOrEmpty(location.NodeId) && location.Field == "activationConditionRoot"
                        ? _compositionEditingSession.Document.Root.NodeId : location.NodeId;
                    var node = CharacterTestTacticalCompositionEdits.Nodes(_compositionEditingSession.Document.Root).FirstOrDefault(n => n.NodeId == nodeId);
                    if (location.CardId == card.CardDraftId && node != null
                        && (string.IsNullOrEmpty(location.DefinitionId) || node.DefinitionId == location.DefinitionId)) _compositionEditingSession.SelectLocation(location);
                    else _compositionEditingSession.Feedback("原运行来源在当前草稿中已变化，已返回卡层重新选择。");
                }
                var wholeCompile = frozen == null ? CompileAuthoredCard(card, false) : null;
                var internalPreview = BuildCompositionInternalPreview(card, wholeCompile);
                if (internalPreview == null) return null;
                // Explicit shared editing uses source identities and frame references for writes.
                // The whole-card compiler above remains the authority for test and publish readiness.
                if (_compositionEditingSession.EditingSourceTimeline) return internalPreview;
                var wholeProgram = frozen?.CompiledProgram ?? (frozen == null ? wholeCompile?.Program : null);
                var locations = frozen?.Locations ?? wholeProgram?.TacticalSource?.Locations;
                string diagnostic = frozen != null ? "冻结执行数据不可用。" : wholeCompile?.Diagnostics.FirstOrDefault().Message;
                internalPreview.WholeCard = new CharacterTestTacticalWholeCardTimeline(wholeProgram,
                    _compositionEditingSession.Document, locations, _compositionEditingSession, diagnostic);
                return internalPreview;
            }
            _compositionEditingSession = null;
            if (card?.HasTacticalSource != true) return null;
            var legacyResult = CompileAuthoredCard(card, false);
            var currentEntry = CurrentTacticalCompilation(card);
            var legacySource = card.CloneTacticalSource();
            var preview = CharacterTestTacticalAuthoringPreview.FromPrepared(legacySource, currentEntry?.Inputs, currentEntry?.Legacy,
                legacyResult?.Diagnostics.FirstOrDefault().Message);
            if ((CharacterTestTacticalDependencies.Analyze(legacySource) & CharacterTestTacticalInputRequirements.SpatialSummary) != 0)
                preview.SummaryRequest = new CharacterTestTacticalSummaryRequest(_matchConfig?.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex)?.CharacterId ?? 0,
                    currentEntry?.Inputs?.Action?.ActionId.Value ?? 0, string.Empty, default, string.Empty, CharacterTestTacticalSummaryPreparation.StandardMeasurement());
            if (_tacticalEditingSession == null || _tacticalEditingSession.CardId != card.CardDraftId)
                _tacticalEditingSession = new CharacterTestTacticalEditingSession(this, card.CardDraftId);
            _tacticalEditingSession.Bind(DraftWorkspace, card, preview);
            preview.Editor = _tacticalEditingSession;
            return preview;
        }

        private CharacterTestTacticalAuthoringPreview BuildCompositionInternalPreview(CharacterTestRuntimeAiLabBehaviorCardDraft card, CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult wholeCompile)
        {
            var session = _compositionEditingSession;
            if (session.Frozen == null)
            {
                int selectedCharacter = _matchConfig?.TryGetSlot(_selectedSetupTeamId, _selectedSetupSlotIndex)?.CharacterId ?? 0;
                var prepared = CurrentTacticalCompilation(card);
                session.Description = prepared?.Description;
                var requirements = CharacterTestTacticalDependencies.Analyze(session.Document);
                session.SummaryRequest = (requirements & CharacterTestTacticalInputRequirements.SpatialSummary) == 0 ? null
                    : prepared?.Request;
            }
            var source = session.Target == CharacterTestTacticalEditTarget.SharedDefinition
                ? session.SelectedDefinition?.Source
                : (session.Target == CharacterTestTacticalEditTarget.Composition
                    ? (session.SelectedDefinition?.Source ?? session.ConditionProjection())
                    : session.ConditionProjection());
            if (source == null) return null;
            if (session.Target != CharacterTestTacticalEditTarget.SharedDefinition
                && (session.Target != CharacterTestTacticalEditTarget.Composition || session.SelectedDefinition == null))
            {
                var validation = wholeCompile;
                var conditions = CharacterTestTacticalAuthoringPreview.FromConditionProjection(source,
                    session.Frozen != null || validation?.Success == true,
                    validation != null && validation.Diagnostics.Count > 0 ? validation.Diagnostics[0].Message : "当前编排不可执行，请查看卡诊断。",
                    session.Frozen != null, session.Frozen?.CharacterId ?? 0);
                conditions.SummaryRequest = session.SummaryRequest;
                BindInternalEditor(conditions); return conditions;
            }
            if (session.Frozen != null)
            {
                if (!session.Frozen.SharedCompilations.TryGetValue(session.SelectedNodeId, out var compilation))
                    session.Frozen.SharedCompilations.TryGetValue(session.SelectedDefinition?.DefinitionId ?? string.Empty, out compilation);
                var historicalSource = source;
                if (session.EventFocusActive && !session.NativeExecutionView)
                { historicalSource = source.Clone(); session.EnsureFocusedBlock(historicalSource); }
                var historical = CharacterTestTacticalAuthoringPreview.FromFrozen(historicalSource, compilation, session.Frozen.CharacterId);
                if (session.EventFocusActive && !session.NativeExecutionView)
                {
                    historical.TryGetFrames(session.FocusEventId, out int origin, out _);
                    historical.SetEventFocus(session.FocusBlockId, origin);
                }
                BindInternalEditor(historical); return historical;
            }
            var current = CurrentTacticalCompilation(card);
            CharacterTestTacticalAuthoringCompilation shared = null;
            if (current?.Dsl != null && !current.Dsl.SharedCompilations.TryGetValue(session.SelectedNodeId, out shared))
                current.Dsl.SharedCompilations.TryGetValue(session.SelectedDefinition?.DefinitionId ?? string.Empty, out shared);
            var preview = CharacterTestTacticalAuthoringPreview.FromPrepared(source, current?.Inputs?.ForInstance(session.SelectedNodeId), shared,
                wholeCompile?.Diagnostics.FirstOrDefault().Message);
            if (session.EventFocusActive && !session.NativeExecutionView)
            {
                var focused = source.Clone();
                session.EnsureFocusedBlock(focused);
                var block = focused.Timelines.First(t => t.TimelineId == session.FocusTimelineId).EventBlocks.First(b => b.BlockId == session.FocusBlockId);
                if (block.Timing == null)
                {
                    bool found = preview.TryGetFrames(block.BlockId, out int start, out int end);
                    if (!found) preview.TryGetFrames(session.FocusEventId, out start, out end);
                    int length = Math.Max(1, end - start);
                    block.Timing = new CharacterTestTacticalBlockTiming { MinimumFrames = length, DurationFrames = length };
                }
                focused.Schema = CharacterTestTacticalAuthoringSource.TimingSchema; focused.Version = 4;
                preview = CharacterTestTacticalAuthoringPreview.FromSource(focused, current?.Inputs?.ForInstance(session.SelectedNodeId),
                    wholeCompile?.Diagnostics.FirstOrDefault().Message);
                preview.SetEventFocus(session.FocusBlockId);
                preview.EnableTimingSimulation(session.SimulationEndFrame);
            }
            preview.SummaryRequest = session.SummaryRequest;
            BindInternalEditor(preview);
            return preview;

            void BindInternalEditor(CharacterTestTacticalAuthoringPreview value)
            {
                if (_tacticalEditingSession == null || _tacticalEditingSession.CardId != card.CardDraftId)
                    _tacticalEditingSession = new CharacterTestTacticalEditingSession(this, card.CardDraftId);
                _tacticalEditingSession.Bind(DraftWorkspace, card, value, session.Token); value.Editor = _tacticalEditingSession;
            }
        }

        private CharacterTestTacticalSummaryRequest DescribeSelectedTacticalSummary(int character)
        {
            var measurement = CharacterTestTacticalSummaryPreparation.StandardMeasurement();
            var runtime = _context?.Slice?.CharacterRuntime;
            if (runtime != null && _sessionBuilder != null
                && _sessionBuilder.TryResolveArenaRuntimeAiPlannerProfileSlot(_selectedSetupTeamId, _selectedSetupSlotIndex, out int profile))
            {
                var entity = runtime.Entities.FirstOrDefault(e => e.Control.RuntimeAiPlannerProfileSlot == profile && e.Identity.CharacterId == character);
                if (entity != null) { CharacterTestTacticalSummaryPreparation.TryDescribe(runtime, entity, measurement, out var actual, out _, SelectedTacticalTestAction); return actual; }
            }
            try
            {
                var role = CharacterTestTacticalPreparationCatalog.Role(character, runtime?.ActionRuntime);
                if (!role.Actions.TryGetEntityActionSet(role.Source.Id, out var set)
                    || !CharacterTestTacticalCardInputs.TryResolveSelectedAction(set, role.Actions.Registry.Actions, SelectedTacticalTestAction, out var action, out _)
                    || !CharacterActionContentHashUtility.TryCompute(action, role.Source, role.Physics, CharacterTestDefaultBakedWeaponTraceSource.Instance,
                        CharacterTestDefaultWeaponTraceBindingResolver.Instance, out string hash, out _)) return null;
                return new CharacterTestTacticalSummaryRequest(character, action.ActionId.Value, set.StableId, role.Source.Equipment, hash, measurement, SelectedTacticalTestAction > 0);
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException) { return null; }
        }

        internal void RequestCompositionSummary()
        {
            var request = _compositionEditingSession?.SummaryRequest;
            if (request != null) request = DescribeSelectedTacticalSummary(request.CharacterId);
            if (request == null || IsHistoricalFrameSelected) return;
            RequestActionBrowserSummary(request.ActionId, false);
            TacticalPresentationRevision++;
        }

        internal void RequestTacticalSummary(CharacterTestTacticalAuthoringPreview preview)
        {
            if (preview?.SummaryRequest == null) return;
            var request = DescribeSelectedTacticalSummary(preview.SummaryRequest.CharacterId);
            if (request == null) return;
            RequestActionBrowserSummary(request.ActionId, false);
            TacticalPresentationRevision++;
        }

        internal void RefreshTacticalPreview() => TacticalPresentationRevision++;
    }
}
