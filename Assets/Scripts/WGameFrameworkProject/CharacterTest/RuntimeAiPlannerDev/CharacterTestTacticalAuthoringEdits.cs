using System;
using System.Collections.Generic;
using System.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestTacticalEditTarget { LegacySource, Composition, SharedDefinition, CardActivation, NodeCondition, NodeEnd }

    internal readonly struct CharacterTestTacticalEditToken
    {
        internal CharacterTestTacticalEditToken(CharacterTestRuntimeAiLabDraftWorkspace workspace, CharacterTestRuntimeAiLabBehaviorCardDraft card,
            CharacterTestTacticalEditTarget target = CharacterTestTacticalEditTarget.LegacySource, string nodeId = "",
            string definitionId = "", int definitionRevision = 0, long contextSerial = 0)
        { WorkspaceId = workspace.WorkspaceId; WorkspaceRevision = workspace.CurrentRevision; CardId = card.CardDraftId; CardRevision = card.CurrentRevision;
            Target = target; NodeId = nodeId; DefinitionId = definitionId; DefinitionRevision = definitionRevision; ContextSerial = contextSerial; }
        internal string WorkspaceId { get; }
        internal int WorkspaceRevision { get; }
        internal string CardId { get; }
        internal int CardRevision { get; }
        internal CharacterTestTacticalEditTarget Target { get; }
        internal string NodeId { get; }
        internal string DefinitionId { get; }
        internal int DefinitionRevision { get; }
        internal long ContextSerial { get; }
    }

    /// <summary>Published Inspector field parsing; mutation is owned by the original workspace transaction.</summary>
    internal static class CharacterTestTacticalPointAuthoringFields
    {
        internal static bool TryEdit(CharacterTestTacticalAuthoringSource source, int existingCost, float existingCooldown,
            string field, string text, out TacticalCard.TacticalPointMoveAuthoring contract,
            out int cost, out float cooldown, out string message)
        {
            contract = source?.Clone().ExecutionInput; cost = existingCost; cooldown = existingCooldown;
            message = "请输入有效数值（范围和容差为毫米，速度为千分比，冷却为秒）。";
            if (contract == null) return false;
            if (field == "cooldown")
            {
                if (!float.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out cooldown)) return false;
            }
            else
            {
                if (!int.TryParse(text, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out int value)) return false;
                switch (field)
                {
                    case "range": contract.RangeMillimeters = value; break;
                    case "arrival": contract.ArrivalMillimeters = value; break;
                    case "frames": contract.MaximumFrames = value; break;
                    case "speed": contract.SpeedMilli = value; break;
                    case "cost": cost = value; break;
                    default: return false;
                }
            }
            try { contract.Freeze(); }
            catch (ArgumentException error) { message = error.Message; return false; }
            if (cost < 0 || cost > CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.MaximumPlayerCardCost
                || float.IsNaN(cooldown) || float.IsInfinity(cooldown) || cooldown < 0 || cooldown * 60d >= int.MaxValue)
                return false;
            message = string.Empty; return true;
        }
    }

    internal sealed partial class CharacterTestRuntimeAiLabDraftWorkspace
    {
        /// <summary>One existing workspace transaction owns source, settlement metadata, revision and undo.</summary>
        internal bool TryEditPointMove(CharacterTestTacticalEditToken token,
            TacticalCard.TacticalPointMoveAuthoring contract, int cost, float cooldownSeconds, out string message)
        {
            if (IsReadOnly || token.WorkspaceId != WorkspaceId || token.WorkspaceRevision != CurrentRevision
                || !_cards.TryGetValue(token.CardId ?? string.Empty, out var card) || !card.HasTacticalSource
                || card.CurrentRevision != token.CardRevision)
            { message = "Point authoring selection is stale or read-only."; return false; }
            if (cost < 0 || cost > CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.MaximumPlayerCardCost
                || float.IsNaN(cooldownSeconds) || float.IsInfinity(cooldownSeconds) || cooldownSeconds < 0f
                || cooldownSeconds * 60d >= int.MaxValue)
            { message = "Invalid point cost or cooldown."; return false; }
            try
            {
                var source = CharacterTestTacticalAuthoringTemplates.CreatePointMove(card.DisplayName, contract);
                var before = CaptureState();
                card.SetTacticalSource(source); card.Cost = cost; card.CooldownSeconds = cooldownSeconds;
                card.CurrentRevision++; TouchWorkspace(); PushUndo(before, "Edit point move rules");
                message = "Point rules updated in the author draft."; return true;
            }
            catch (Exception error) when (error is ArgumentException || error is Newtonsoft.Json.JsonException)
            { message = error.Message; return false; }
        }

        internal bool TryDuplicateTacticalSource(CharacterTestTacticalEditToken token, int team, int slot, out string id, out string message)
        {
            id = string.Empty;
            if (token.WorkspaceId != WorkspaceId || token.WorkspaceRevision != CurrentRevision
                || !_cards.TryGetValue(token.CardId ?? string.Empty, out var card) || !card.HasTacticalSource || card.CurrentRevision != token.CardRevision)
            { message = "另存上下文已变化。"; return false; }
            var source = card.CloneTacticalSource(); source.Name = card.DisplayName + " 副本";
            if (!TryImportTacticalSource(team, slot, token.WorkspaceRevision, source, out id, out message)) return false;
            var copy = _cards[id];
            copy.IconKey = card.IconKey; copy.Cost = card.Cost; copy.CooldownSeconds = card.CooldownSeconds; copy.ShortDescription = card.ShortDescription;
            copy.UseSummary = card.UseSummary; copy.RiskSummary = card.RiskSummary; copy.TagsText = card.TagsText;
            copy.RequiredCapabilities = (CharacterTestRuntimeAiPlannerBehaviorCapability[])card.RequiredCapabilities.Clone();
            copy.ResponsibilityKinds = (CharacterTestRuntimeAiPlannerBehaviorResponsibilityKind[])card.ResponsibilityKinds.Clone();
            return true;
        }

        /// <remarks>LowFreqAlloc: one explicit authoring operation, never a runtime tick mutation.</remarks>
        internal bool TryEditTacticalSource(CharacterTestTacticalEditToken token, string label,
            Action<CharacterTestTacticalAuthoringSource> edit, out string message, string displayName = null)
        {
            if (IsReadOnly || token.WorkspaceId != WorkspaceId || token.WorkspaceRevision != CurrentRevision
                || !_cards.TryGetValue(token.CardId ?? string.Empty, out var card) || !card.HasTacticalSource
                || card.CurrentRevision != token.CardRevision)
            { message = "编辑上下文已变化，请重新选择对象；本次修改没有写入。"; return false; }
            try
            {
                var next = card.CloneTacticalSource();
                edit?.Invoke(next);
                string json = CharacterTestTacticalAuthoringJson.Write(next);
                CharacterTestTacticalAuthoringJson.Read(json);
                string name = displayName == null ? card.DisplayName : displayName.Trim();
                if (name.Length == 0) throw new ArgumentException("卡名称不能为空。");
                if (json == card.TacticalSourceJson && name == card.DisplayName)
                { message = "没有内容变化。"; return true; }
                var before = CaptureState();
                card.SetTacticalSource(next);
                card.DisplayName = name;
                card.CurrentRevision++;
                card.LastCompileStatus = "TacticalSourceChanged";
                TouchWorkspace();
                PushUndo(before, label);
                message = label + "已保存到工作副本。";
                return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException
                || error is OverflowException || error is Newtonsoft.Json.JsonException)
            { message = error.Message; return false; }
        }
    }

    internal static class CharacterTestTacticalAuthoringEdits
    {
        internal static CharacterTestTacticalAuthoringTimeline Timeline(CharacterTestTacticalAuthoringSource source, string id)
            => source.Timelines.FirstOrDefault(t => t?.TimelineId == id) ?? throw new ArgumentException("时间轴已不存在。");
        internal static CharacterTestTacticalAuthoringEvent Event(CharacterTestTacticalAuthoringSource source, string timeline, string id)
            => Timeline(source, timeline).Events.SingleOrDefault(e => e?.EventId == id) ?? throw new ArgumentException("事件已不存在。");

        internal static void SetReference(CharacterTestTacticalAuthoringSource source, string timeline, string id,
            bool end, CharacterTestTacticalFrameReference reference)
        {
            var block = Timeline(source, timeline).EventBlocks.FirstOrDefault(b => b.BlockId == id);
            if (block != null)
            {
                if (end || reference == null) throw new ArgumentException("事件段结束由成员推导。");
                block.Start = reference; return;
            }
            var ev = Event(source, timeline, id);
            if (end && (ev.Operation?.Kind == "CharacterAction" || ev.TimingKind == "Instant"))
                throw new ArgumentException("动作时长和单帧事件的结束边界不能手工修改。");
            if (reference == null) throw new ArgumentException("时间引用不能为空。");
            if (end) ev.End = reference; else ev.Start = reference;
        }

        internal static void Add(CharacterTestTacticalAuthoringSource source, string timelineId, string kind)
        {
            bool instant = kind.EndsWith(":Instant", StringComparison.Ordinal);
            if (instant) kind = kind.Substring(0, kind.Length - 8);
            if (instant && (!source.HasTimingVersion || kind != "ReturnToPlanner" && kind != "EnterTimeline"))
                throw new ArgumentException("该操作不支持单帧事件。");
            bool flow = source.HasTimingVersion && (kind == "LoopRegion" || kind == "EnterTimeline" || kind == "CompleteBlock");
            if (!new[] { "CharacterAction", "Movement", "ApproachOptimalRange", "TurnToTarget", "WaitForCondition", "ReturnToPlanner" }.Contains(kind)
                && !flow
                && !(kind == "RetreatFromTarget" && (source.Schema == CharacterTestTacticalAuthoringSource.EventSetSchema || source.HasBlocksVersion)))
                throw new ArgumentException("不支持的战术事件类型。");
            var timeline = Timeline(source, timelineId);
            timeline.Events.Add(new CharacterTestTacticalAuthoringEvent {
                EventId = "event-" + Guid.NewGuid().ToString("N"), Title = kind == "Movement" ? "移动" : kind == "CompleteBlock" ? "结束当前意图段" : kind,
                TimingKind = kind == "WaitForCondition" || instant ? "Instant" : "Duration",
                Start = CharacterTestTacticalAuthoringTemplates.Absolute(0),
                End = kind == "CharacterAction" || kind == "WaitForCondition" || instant ? null : CharacterTestTacticalAuthoringTemplates.Absolute(kind == "Movement" ? 120 : 1),
                Operation = new CharacterTestTacticalAuthoringOperation { Kind = kind, Movement = kind == "Movement" ? new CharacterTestTacticalMovement() : null,
                    TargetTimelineId = kind == "EnterTimeline" ? timelineId : null, MaxLoopRepeats = kind == "LoopRegion" ? 1 : 0,
                    Target = kind == "TurnToTarget" || kind == "RetreatFromTarget" ? "currentCombatTarget" : string.Empty },
                ConditionRoot = null });
        }

        internal static string[] DeleteImpact(CharacterTestTacticalAuthoringSource source, IEnumerable<string> eventIds)
        {
            var ids = new HashSet<string>(eventIds, StringComparer.Ordinal);
            var result = new List<string>();
            foreach (var timeline in source.Timelines)
            {
                foreach (var block in timeline.EventBlocks)
                    if (block.Start?.Kind == "Anchor" && block.Start.TargetKind == "Event" && ids.Contains(block.Start.SourceEventId))
                        result.Add(block.Title + ".事件段开始 → " + block.Start.SourceEventId);
                CollectSignals(timeline.EntryConditionRoot, timeline.TimelineId + " 进入条件", ids, result);
                foreach (var ev in timeline.Events)
                {
                    if (ids.Contains(ev.EventId)) continue;
                    if (ev.Start?.Kind == "Anchor" && ids.Contains(ev.Start.SourceEventId)) result.Add(ev.EventId + ".开始 → " + ev.Start.SourceEventId);
                    if (ev.End?.Kind == "Anchor" && ids.Contains(ev.End.SourceEventId)) result.Add(ev.EventId + ".结束 → " + ev.End.SourceEventId);
                    if (ids.Contains(ev.Operation?.SourceActionEventId ?? string.Empty)) result.Add(ev.EventId + ".关联动作 → " + ev.Operation.SourceActionEventId);
                    CollectSignals(ev.ConditionRoot, ev.EventId + " 条件", ids, result);
                }
            }
            CollectSignals(source.ActivationConditionRoot, "启用条件", ids, result);
            foreach (var binding in source.PredicateBindings)
            {
                if (ids.Contains(binding.SourceActionEventId)) result.Add(binding.NodeId + ".动作数据 → " + binding.SourceActionEventId);
                if (binding.SourceMovementEventId != null && ids.Contains(binding.SourceMovementEventId))
                    result.Add(binding.NodeId + ".移动到位 → " + binding.SourceMovementEventId);
                if (binding.SourceTurnEventId != null && ids.Contains(binding.SourceTurnEventId))
                    result.Add(binding.NodeId + ".面向达标 → " + binding.SourceTurnEventId);
            }
            return result.ToArray();
        }

        private static void CollectSignals(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode node, string owner,
            HashSet<string> ids, List<string> result)
        {
            if (node == null) return;
            if (node.Kind == CharacterTestRuntimeAiLabBehaviorRecipeConditionNodeKind.EventSignal && ids.Contains(node.SourceEventId))
                result.Add(owner + "/" + node.NodeId + " → " + node.SourceEventId);
            foreach (var child in node.Children) CollectSignals(child, owner, ids, result);
        }

        internal static void Delete(CharacterTestTacticalAuthoringSource source, string timelineId, string[] ids, bool confirmed)
        {
            var timeline = Timeline(source, timelineId);
            if (ids == null || ids.Length == 0 || ids.Distinct().Count() != ids.Length) throw new ArgumentException("删除选择无效。");
            foreach (string id in ids) Event(source, timelineId, id);
            if (DeleteImpact(source, ids).Length > 0 && !confirmed) throw new ArgumentException("请先确认删除影响。");
            var removedNodes = new HashSet<string>(StringComparer.Ordinal);
            void Visit(CharacterTestRuntimeAiLabBehaviorRecipeConditionNode n) { if (n == null) return; removedNodes.Add(n.NodeId); foreach (var c in n.Children) Visit(c); }
            foreach (var ev in timeline.Events) if (ids.Contains(ev.EventId)) Visit(ev.ConditionRoot);
            timeline.Events.RemoveAll(e => ids.Contains(e.EventId));
            foreach (var block in timeline.EventBlocks) block.EventIds.RemoveAll(ids.Contains);
            // Remove only bindings owned by the deleted condition subtree. Unrelated invalid data survives.
            source.PredicateBindings.RemoveAll(b => removedNodes.Contains(b.NodeId));
        }

        internal static void Move(CharacterTestTacticalAuthoringSource source, string timelineId, string[] ids, int delta)
        {
            if (source.HasBlocksVersion) { CharacterTestTacticalBlockEdits.Move(source, timelineId, ids, delta); return; }
            var timeline = Timeline(source, timelineId);
            if (ids == null || ids.Length == 0 || ids.Distinct().Count() != ids.Length) throw new ArgumentException("移动选择无效。");
            var selected = new HashSet<string>(ids, StringComparer.Ordinal);
            var events = timeline.Events.ToDictionary(e => e.EventId, StringComparer.Ordinal);
            foreach (var id in ids) if (!events.ContainsKey(id)) throw new ArgumentException("移动选择包含已删除或其它时间轴的事件。");
            void CheckGraph(string id, bool end, HashSet<string> visiting)
            {
                if (!events.TryGetValue(id, out var ev)) throw new ArgumentException("时间关联失效，无法移动。");
                string key = id + (end ? ":end" : ":start");
                if (!visiting.Add(key)) throw new ArgumentException("时间关联成环，无法移动。");
                if (end && (ev.Operation?.Kind == "CharacterAction" || ev.TimingKind == "Instant")) end = false;
                var r = end ? ev.End : ev.Start;
                if (r == null) throw new ArgumentException("时间引用缺失。");
                if (r.Kind == "Anchor") CheckGraph(r.SourceEventId, r.Anchor == "End", visiting);
                visiting.Remove(key);
            }
            foreach (var id in ids) { CheckGraph(id, false, new HashSet<string>()); CheckGraph(id, true, new HashSet<string>()); }
            bool Affected(string id, bool end, HashSet<string> visiting)
            {
                if (!events.TryGetValue(id, out var ev)) throw new ArgumentException("时间关联失效，无法移动。");
                if (selected.Contains(id)) return true;
                string key = id + (end ? ":end" : ":start");
                if (!visiting.Add(key)) throw new ArgumentException("时间关联成环，无法移动。");
                if (end && (ev.Operation?.Kind == "CharacterAction" || ev.TimingKind == "Instant")) end = false;
                var r = end ? ev.End : ev.Start;
                bool affected = r?.Kind == "Anchor" && Affected(r.SourceEventId, r.Anchor == "End", visiting);
                visiting.Remove(key);
                return affected;
            }
            foreach (string id in ids)
            {
                var ev = events[id];
                void Shift(CharacterTestTacticalFrameReference r)
                {
                    if (r == null) throw new ArgumentException("时间引用缺失。");
                    if (r.Kind == "Absolute") { r.Frame = checked(r.Frame + delta); if (r.Frame < 0) throw new ArgumentException("移动后帧不能为负数。"); }
                    else if (!Affected(r.SourceEventId, r.Anchor == "End", new HashSet<string>(StringComparer.Ordinal)))
                        r.OffsetFrames = checked(r.OffsetFrames + delta);
                }
                Shift(ev.Start);
                if (ev.TimingKind != "Instant" && ev.Operation?.Kind != "CharacterAction") Shift(ev.End);
            }
        }
    }
}
