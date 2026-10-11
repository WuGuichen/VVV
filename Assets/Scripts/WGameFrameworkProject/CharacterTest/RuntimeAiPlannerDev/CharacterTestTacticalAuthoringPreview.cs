using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Disposable display projection of one owned source and the current role's prepared inputs.</summary>
    internal sealed partial class CharacterTestTacticalAuthoringPreview
    {
        internal CharacterTestTacticalAuthoringSource Source { get; private set; }
        internal CharacterTestTacticalAuthoringCompilation Compilation { get; private set; }
        internal CharacterTestTacticalSummaryRequest SummaryRequest { get; set; }
        internal CharacterTestRuntimeAiLabBehaviorCardLogicPayload DisplayPayload { get; private set; }
        internal string Diagnostic { get; private set; }
        internal CharacterTestTacticalEditingSession Editor { get; set; }
        internal CharacterTestTacticalWholeCardTimeline WholeCard { get; set; }
        internal bool IsFrozen { get; private set; }
        internal int FrozenCharacterId { get; private set; }
        internal bool IsConditionProjection { get; private set; }
        private bool _conditionProjectionValid;
        internal bool Ready => WholeCard != null ? WholeCard.Program != null : TimingPreview != null ? TimingPreview.Issues.Count == 0 : IsConditionProjection ? _conditionProjectionValid : Compilation != null;
        internal string StatusText => WholeCard != null ? Ready ? "整卡已编译；各时间轴使用局部帧。" : WholeCard.Diagnostic
            : Source?.ExecutionInput != null ? Ready ? "地点合同已校验；每次执行仍需本次选点。" : Diagnostic
            : IsFrozen ? "历史冻结源，只读。" : IsConditionProjection ? Diagnostic : Ready ? "当前源已按所选角色编译。"
            : Diagnostic?.Contains("SummaryLegacyFormatRequiresRemeasure") == true ? "旧格式摘要需要重新测量。请重建所选动作摘要或全量重建。"
            : Diagnostic?.Contains("ActionSummary:Missing") == true ? "当前动作摘要缺失。请打开摘要准备，显式重建后重新查询。"
            : Diagnostic?.Contains("ActionSummary:Stale") == true ? "动作或测量规格已变化，旧摘要失效。请显式重建后重新查询。"
            : Diagnostic;
        internal string PreparationSummary => IsFrozen ? "历史角色：" + FrozenCharacterId + " · 冻结动作：" + (Compilation?.ActionId ?? 0)
            : SummaryRequest == null ? "当前角色装配尚未准备"
            : "角色：" + SummaryRequest.CharacterId + " · 槽位动作：" + SummaryRequest.ActionId;
        internal string SourceFingerprint { get; private set; }

        private CharacterTestTacticalCardInputs _preparedInputs;
        internal CharacterTestTacticalAuthoringPreview WithEdits(Action<CharacterTestTacticalAuthoringSource> edit)
        {
            if (IsFrozen) throw new InvalidOperationException("历史冻结源不可编辑。");
            var copy = Source.Clone(); edit(copy);
            var preview = FromSource(copy, _preparedInputs, Diagnostic);
            preview.Editor = Editor; preview.SummaryRequest = SummaryRequest;
            if (FocusBlockId != null) preview.SetEventFocus(FocusBlockId, DisplayOrigin);
            if (TimingPreview != null) preview.EnableTimingSimulation(SimulationEnd);
            return preview;
        }
        internal static CharacterTestTacticalAuthoringPreview Create(CharacterTestRuntimeAiLabBehaviorCardDraft card,
            CharacterTestTacticalCardInputs inputs, string preparationIssue) => FromSource(card.CloneTacticalSource(), inputs, preparationIssue);
        internal static CharacterTestTacticalAuthoringPreview FromSource(CharacterTestTacticalAuthoringSource source,
            CharacterTestTacticalCardInputs inputs, string preparationIssue)
        {
            var result = new CharacterTestTacticalAuthoringPreview { Source = source, _preparedInputs = inputs };
            result.SourceFingerprint = CharacterTestTacticalAuthoringJson.SemanticFingerprint(result.Source);
            bool pointContract = source.ExecutionInput != null;
            var issues = pointContract ? CharacterTestTacticalAuthoringCompiler.Validate(result.Source)
                : CharacterTestTacticalAuthoringCompiler.ValidateShared(result.Source);
            var text = new StringBuilder();
            foreach (var issue in issues) text.AppendLine(issue.ToString());
            if (pointContract || inputs != null)
            {
                CharacterTestTacticalAuthoringCompilation compiled;
                System.Collections.Generic.IReadOnlyList<CharacterTestTacticalAuthoringDiagnostic> diagnostics;
                bool compiledOk = pointContract
                    ? CharacterTestTacticalAuthoringCompiler.TryCompile(result.Source, inputs ?? CharacterTestTacticalCardInputs.Unbound(1), out compiled, out diagnostics)
                    : CharacterTestTacticalAuthoringCompiler.TryCompileSharedTrial(result.Source, inputs, out compiled, out diagnostics);
                if (compiledOk)
                    result.Compilation = compiled;
                else foreach (var issue in diagnostics) text.AppendLine(issue.ToString());
            }
            else text.AppendLine(preparationIssue ?? "当前角色输入尚未准备。");
            result.Diagnostic = text.ToString().Trim();
            result.DisplayPayload = result.BuildDisplayPayload();
            return result;
        }

        internal static CharacterTestTacticalAuthoringPreview FromPrepared(CharacterTestTacticalAuthoringSource source,
            CharacterTestTacticalCardInputs inputs, CharacterTestTacticalAuthoringCompilation compilation, string issue)
        {
            if (source.ExecutionInput != null) return FromSource(source.Clone(), inputs, issue);
            var result = new CharacterTestTacticalAuthoringPreview { Source = source.Clone(), _preparedInputs = inputs, Compilation = compilation,
                SourceFingerprint = compilation?.SourceFingerprint ?? CharacterTestTacticalAuthoringJson.SemanticFingerprint(source), Diagnostic = issue ?? string.Empty };
            result.DisplayPayload = result.BuildDisplayPayload(); return result;
        }

        internal static CharacterTestTacticalAuthoringPreview FromFrozen(CharacterTestTacticalAuthoringSource source,
            CharacterTestTacticalAuthoringCompilation compilation, int characterId = 0)
        {
            var result = new CharacterTestTacticalAuthoringPreview { Source = source.Clone(), Compilation = compilation,
                IsFrozen = true, FrozenCharacterId = characterId,
                SourceFingerprint = CharacterTestTacticalAuthoringJson.SemanticFingerprint(source), Diagnostic = "历史冻结源，只读。" };
            result.DisplayPayload = result.BuildDisplayPayload(); return result;
        }
        internal static CharacterTestTacticalAuthoringPreview FromConditionProjection(CharacterTestTacticalAuthoringSource source,
            bool valid, string diagnostic, bool frozen = false, int characterId = 0)
        {
            var result = new CharacterTestTacticalAuthoringPreview { Source = source.Clone(), IsConditionProjection = true,
                _conditionProjectionValid = valid, IsFrozen = frozen, FrozenCharacterId = characterId,
                SourceFingerprint = CharacterTestTacticalAuthoringJson.SemanticFingerprint(source),
                Diagnostic = frozen ? "历史编排条件，只读。" : valid ? "当前卡编排条件已校验。" : diagnostic };
            result.DisplayPayload = result.BuildDisplayPayload(); return result;
        }

        internal CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult AsCompileResult()
            => new CharacterTestRuntimeAiLabBehaviorCardLogicCompileResult(WholeCard != null ? WholeCard.Program : Compilation?.Program,
                Ready ? Array.Empty<CharacterTestRuntimeAiLabCardDiagnostic>() : new[] {
                    new CharacterTestRuntimeAiLabCardDiagnostic(CharacterTestRuntimeAiLabCardDiagnosticSeverity.Error,
                        "TACTICAL_PREVIEW_BLOCKED", "$", WholeCard?.Diagnostic ?? Diagnostic) });

        internal CharacterTestTacticalAuthoringEvent FindEvent(string timelineId, string eventId)
        {
            foreach (var timeline in Source.Timelines)
                if (timeline?.TimelineId == timelineId)
                    foreach (var ev in timeline.Events)
                        if (ev?.EventId == eventId) return ev;
            return null;
        }

        internal bool TryGetFrames(string eventId, out int start, out int end)
        {
            start = end = 0;
            if (TimingPreview != null) return TimingPreview.TryFrames(eventId, out start, out end);
            if (Compilation == null) return false;
            if (Compilation.TryBlockFrames(eventId, out start, out end)) return true;
            foreach (var item in Compilation.Events)
                if (item.EventId == eventId) { start = item.StartFrame; end = item.EndFrame; return true; }
            return false;
        }

        internal static string DescribeReference(CharacterTestTacticalFrameReference reference)
            => reference == null ? "未设置" : reference.Kind == "Absolute"
                ? reference.Frame + "f" : reference.SourceEventId + "." + reference.Anchor
                    + (reference.OffsetFrames >= 0 ? " + " : " - ") + Math.Abs((long)reference.OffsetFrames) + "f";

        internal string DescribeEvent(string timelineId, string eventId)
        {
            var ev = FindEvent(timelineId, eventId);
            if (ev == null) return (IsFrozen ? "历史冻结源 · " : "当前作者源 · ") + Source.EntryTimelineId + "\n"
                + Source.Timelines.Count + " 条时间轴\n" + PreparationSummary + "\n" + StatusText;
            string range = TryGetFrames(eventId, out int start, out int end)
                ? (IsFrozen ? "冻结编译结果：" : "当前装配求值：") + start + " → " + end + "f"
                : "尚未求值；时间轴中的条目位置仅供选择，不是运行时间。";
            return (string.IsNullOrWhiteSpace(ev.Title) ? ev.EventId : ev.Title) + "\n"
                + "操作：" + ev.Operation?.Kind + "\n"
                + (ev.Operation?.Kind == "CharacterAction" ? "动作来源：当前角色对此事件的装配\n" : "")
                + "关联动作：" + ev.Operation?.SourceActionEventId + "\n"
                + "开始：" + DescribeReference(ev.Start) + "\n"
                + "结束：" + (ev.TimingKind == "Instant" ? "单帧采样，无结束字段" : ev.Operation?.Kind == "CharacterAction"
                    ? "随动作时长" : DescribeReference(ev.End)) + "\n" + range + "\n"
                + "优先级：" + ev.Priority + "；次数：" + ev.MaxExecutionsPerPass + "\n"
                + "起手条件：" + (ev.ConditionRoot == null ? "无" : "源条件树 " + ev.ConditionRoot.NodeId) + "\n"
                + (SummaryRequest != null ? "当前角色：" + SummaryRequest.CharacterId + "\n槽位动作：" + SummaryRequest.ActionId + "\n" : "")
                + (IsFrozen ? PreparationSummary + "\n" + StatusText : Ready ? "当前源已编译" + ResolveTestStatusSuffix() : Diagnostic);
        }

        private string ResolveTestStatusSuffix()
        {
            if (Editor?.Controller == null)
                return "。";
            switch (Editor.Controller.CurrentTestSource)
            {
                case CharacterTestRuntimeAiLabCurrentTestSource.UsingCurrentChanges:
                    return "；本轮使用当前修改。";
                case CharacterTestRuntimeAiLabCurrentTestSource.CurrentChangesUpdated:
                    return "；有更新待重测。";
                case CharacterTestRuntimeAiLabCurrentTestSource.PublishedVersion:
                    return "；本轮测试仍使用旧版本。";
                case CharacterTestRuntimeAiLabCurrentTestSource.NotStarted:
                    return "；尚未开始测试。";
                default:
                    string text = Editor.Controller.CurrentTestSourceStatusText;
                    return !string.IsNullOrEmpty(text) ? "；" + text + "。" : "。";
            }
        }

        private CharacterTestRuntimeAiLabBehaviorCardLogicPayload BuildDisplayPayload()
        {
            if (Compilation != null && TimingPreview == null) return BuildExecutionDisplay();
            // This object is never assigned to CardDraft.LogicPayload or sent to a writer/compiler.
            var payload = new CharacterTestRuntimeAiLabBehaviorCardLogicPayload {
                BehaviorRecipeVersion = 11, EntryTimelineId = Source.EntryTimelineId };
            foreach (var sourceTimeline in Source.Timelines)
            {
                if (sourceTimeline == null) continue;
                var timeline = new CharacterTestRuntimeAiLabBehaviorRecipeTimeline {
                    TimelineId = sourceTimeline.TimelineId, Title = sourceTimeline.Title, Order = sourceTimeline.Order };
                foreach (var sourceEvent in sourceTimeline.Events)
                {
                    if (sourceEvent == null) continue;
                    bool resolved = TryGetFrames(sourceEvent.EventId, out int start, out int end);
                    if (!resolved) { start = 0; end = 1; }
                    var operation = new CharacterTestRuntimeAiLabBehaviorRecipeEventOperation();
                    switch (sourceEvent.Operation?.Kind)
                    {
                        case "LoopRegion": operation.Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.LoopRegion; operation.MaxLoopRepeats = sourceEvent.Operation.MaxLoopRepeats; break;
                        case "EnterTimeline": operation.Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.EnterTimeline; operation.TargetTimelineId = sourceEvent.Operation.TargetTimelineId; break;
                        case "CharacterAction": operation.Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.CharacterAction;
                            operation.CharacterActionId = new CharacterActionId(Compilation?.EventActionId(sourceEvent.EventId) ?? 0); break;
                        case "Movement": operation.Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget;
                            if (sourceEvent.Operation.Movement != null) { operation.MovementStrategy = sourceEvent.Operation.Movement.Strategy; operation.MovementTargetKind = sourceEvent.Operation.Movement.Target; } break;
                        case "ApproachOptimalRange": operation.Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget; break;
                        case "RetreatFromTarget": operation.Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.MoveToTarget;
                            operation.MovementStrategy = CharacterTestRuntimeAiLabMovementStrategy.AvoidHazard; break;
                        case "WaitForCondition": operation.Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.WaitForCondition; break;
                        case "TurnToTarget": operation.Kind = CharacterTestBehaviorExecutionOperations.TurnToTarget; break;
                        case "ReturnToPlanner": operation.Kind = CharacterTestRuntimeAiLabBehaviorRecipeEventOperationKind.ReturnToPlanner; break;
                    }
                    timeline.Events.Add(new CharacterTestRuntimeAiLabBehaviorRecipeEvent {
                        EventId = sourceEvent.EventId, TimelineId = sourceTimeline.TimelineId, Title = sourceEvent.Title,
                        StartFrame = start, DurationFrames = Math.Max(1, end - start), Priority = sourceEvent.Priority,
                        MaxExecutionsPerPass = sourceEvent.MaxExecutionsPerPass, CooldownFrames = sourceEvent.CooldownFrames,
                        TimingKind = sourceEvent.TimingKind == "Instant" ? CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Instant
                            : CharacterTestRuntimeAiLabBehaviorRecipeEventTimingKind.Duration, Operation = operation });
                }
                payload.Timelines.Add(timeline);
            }
            ApplyBlockDisplay(payload);
            return payload;
        }
    }
}
