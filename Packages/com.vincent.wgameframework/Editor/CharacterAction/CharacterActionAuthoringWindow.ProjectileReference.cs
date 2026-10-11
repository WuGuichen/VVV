using System;
using MxFramework.CharacterAction;
using MxFramework.Editor.ProjectileAuthoring;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private void AddProjectileGameplayEventEditor(
            VisualElement parent,
            CharacterActionJsonDto action,
            GameplayTrackEventJsonDto evt,
            CharacterActionProjectileAuthoringStore store,
            bool timelineInspector)
        {
            var section = new VisualElement { name = "projectile-request-event-editor" };
            section.AddToClassList("ca-projectile-event-editor");
            parent.Add(section);

            if (!TryGetProjectileReferenceState(
                    CharacterActionAuthoringIds.Label(evt.RequestId, 408),
                    store,
                    out CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record,
                    out string health))
            {
                AddProjectileStatusHeader(section, "发射投射物", health);
                section.Add(new HelpBox(
                    "投射物 Request 引用失效：" + CharacterActionAuthoringIds.Label(evt.RequestId, 408)
                    + "。CharacterAction 会保留原值；请在 Projectile Authoring 的 Problems 中修复。",
                    HelpBoxMessageType.Error));
                section.Add(CreateOpenProjectileAuthoringButton(CharacterActionAuthoringIds.Label(evt.RequestId, 408)));
                AddProjectileAdvancedFields(section, action, evt, store, timelineInspector, null);
                return;
            }

            AddProjectileStatusHeader(section, "发射投射物", health);
            if (timelineInspector)
                AddTimelineIntegerField(section, "触发帧", evt.Frame, value => evt.Frame = Math.Max(0, value));
            else
                AddIntegerField(section, "触发帧", evt.Frame, value => evt.Frame = Math.Max(0, value));

            AddGameplayRequestField(
                section,
                "投射物",
                evt.RequestId,
                store,
                action,
                value => evt.RequestId = value,
                timelineInspector);

            var compactSummary = new Label(BuildProjectileCompactSummary(record));
            compactSummary.name = "projectile-compact-summary";
            compactSummary.AddToClassList("ca-projectile-compact-summary");
            section.Add(compactSummary);
            section.Add(CreateOpenProjectileAuthoringButton(CharacterActionAuthoringIds.Label(evt.RequestId, 408)));
            AddProjectileAdvancedFields(section, action, evt, store, timelineInspector, record);
        }

        private void AddProjectileRequestReadOnlySummary(
            VisualElement parent,
            string requestStableId,
            CharacterActionProjectileAuthoringStore store,
            CharacterActionJsonDto currentAction)
        {
            var section = new VisualElement { name = "projectile-request-readonly-summary" };
            section.AddToClassList("ca-projectile-inspector");
            parent.Add(section);

            if (!TryGetProjectileReferenceState(
                    requestStableId,
                    store,
                    out CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record,
                    out string health))
            {
                AddProjectileStatusHeader(section, "发射投射物", health);
                section.Add(new HelpBox(
                    "投射物 Request 引用失效：" + (requestStableId ?? string.Empty)
                    + "。CharacterAction 会保留原值并且不会静默改写；请在 Projectile Authoring 的 Problems 中修复。",
                    HelpBoxMessageType.Error));
                section.Add(CreateOpenProjectileAuthoringButton(requestStableId));
                return;
            }

            AddProjectileStatusHeader(section, "发射投射物", health);
            var compactSummary = new Label(BuildProjectileCompactSummary(record));
            compactSummary.name = "projectile-compact-summary";
            compactSummary.AddToClassList("ca-projectile-compact-summary");
            section.Add(compactSummary);
            section.Add(CreateOpenProjectileAuthoringButton(requestStableId));

            var advanced = new Foldout
            {
                name = "projectile-reference-advanced",
                text = "高级信息",
                value = false,
            };
            advanced.AddToClassList("ca-projectile-advanced");
            AddProjectileReferenceDetails(advanced, record, requestStableId, currentAction, store);
            section.Add(advanced);
        }

        private void AddProjectileAdvancedFields(
            VisualElement parent,
            CharacterActionJsonDto currentAction,
            GameplayTrackEventJsonDto evt,
            CharacterActionProjectileAuthoringStore store,
            bool timelineInspector,
            CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record)
        {
            var advanced = new Foldout
            {
                name = "projectile-event-advanced",
                text = "高级信息",
                value = false,
            };
            advanced.AddToClassList("ca-projectile-advanced");

            if (timelineInspector)
            {
                AddTimelineEnumPopup<CharacterActionTrackEventKind>(
                    advanced,
                    "事件类型",
                    evt.Kind,
                    value => evt.Kind = value,
                    requestFullTracksRefresh: true);
                AddTimelineTextField(
                    advanced,
                    "稳定事件 ID",
                    evt.StableEventId,
                    value => evt.StableEventId = value);
            }
            else
            {
                AddEnumPopup<CharacterActionTrackEventKind>(
                    advanced,
                    "事件类型",
                    evt.Kind,
                    value => evt.Kind = value);
                AddTextField(
                    advanced,
                    "稳定事件 ID",
                    evt.StableEventId,
                    value => evt.StableEventId = value);
            }

            if (record != null)
                AddProjectileReferenceDetails(advanced, record, CharacterActionAuthoringIds.Label(evt.RequestId, 408), currentAction, store);
            parent.Add(advanced);
        }

        private void AddProjectileReferenceDetails(
            VisualElement parent,
            CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record,
            string requestStableId,
            CharacterActionJsonDto currentAction,
            CharacterActionProjectileAuthoringStore store)
        {
            AddReadOnlyLine(parent, "Definition", record.DefinitionStableId + " (#" + record.DefinitionId + ")");
            AddReadOnlyLine(
                parent,
                "Origin / Socket",
                record.OriginKind + (string.IsNullOrWhiteSpace(record.SocketId) ? string.Empty : " · " + record.SocketId));
            AddReadOnlyLine(
                parent,
                "Aim",
                record.AimKind + (record.AimMaxRange > 0f ? " · " + record.AimMaxRange.ToString("0.###") + "m" : string.Empty));
            AddReadOnlyLine(
                parent,
                "Presentation",
                record.HasPresentationBinding ? "Compiled binding" : "Missing");
            AddReadOnlyLine(parent, "Consumer", BuildProjectileConsumerSummary(requestStableId, currentAction, store));
        }

        private static bool TryGetProjectileReferenceState(
            string requestStableId,
            CharacterActionProjectileAuthoringStore store,
            out CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record,
            out string health)
        {
            record = null;
            if (store == null || !store.TryGetByStableId(requestStableId, out record))
            {
                health = "Missing";
                return false;
            }

            bool ready = record.HasSourceDefinition
                && record.HasSourceSpawnProfile
                && record.HasGeneratedDefinition
                && record.HasGeneratedSpawnProfile
                && record.GeneratedInSync;
            health = ready && !record.IsLegacyV1PresentationBinding
                ? "Ready"
                : ready && record.IsLegacyV1PresentationBinding
                    ? "Legacy V1"
                    : record.GeneratedInSync ? "Compatibility" : "Stale";
            return true;
        }

        private static void AddProjectileStatusHeader(
            VisualElement parent,
            string titleText,
            string health)
        {
            var header = new VisualElement();
            header.AddToClassList("ca-projectile-event-header");
            var title = new Label(titleText);
            title.name = "projectile-event-title";
            title.AddToClassList("ca-projectile-event-title");
            header.Add(title);

            var badge = new Label(health);
            badge.name = "projectile-health-badge";
            badge.AddToClassList("ca-projectile-badge");
            badge.AddToClassList(health == "Ready"
                ? "ca-projectile-badge--synced"
                : "ca-projectile-badge--stale");
            header.Add(badge);
            parent.Add(header);
        }

        private static string BuildProjectileCompactSummary(
            CharacterActionProjectileAuthoringStore.CharacterActionProjectileAuthoringRecord record)
        {
            string origin = string.IsNullOrWhiteSpace(record.SocketId)
                ? record.OriginKind
                : record.SocketId;
            string aim = record.AimKind;
            if (!string.IsNullOrEmpty(aim) && aim.EndsWith("Range", StringComparison.Ordinal))
                aim = aim.Substring(0, aim.Length - "Range".Length);
            string range = record.AimMaxRange > 0f
                ? record.AimMaxRange.ToString("0.###") + "m"
                : "默认射程";
            string presentation = record.HasPresentationBinding ? "表现已绑定" : "表现缺失";
            return origin + " · " + aim + " · " + range + " · " + presentation;
        }

        private static Button CreateOpenProjectileAuthoringButton(string stableId)
        {
            return new Button(() => ProjectileAuthoringWindow.OpenByStableId(stableId))
            {
                name = "open-projectile-authoring",
                text = "编辑投射物配置",
                tooltip = "打开唯一可写投射物工作台并定位同一 Request Stable ID。",
            };
        }

        private string BuildProjectileConsumerSummary(
            string requestStableId,
            CharacterActionJsonDto currentAction,
            CharacterActionProjectileAuthoringStore store)
        {
            CharacterActionProjectileConsumerIndex index = CharacterActionProjectileConsumerIndex.Build(
                store,
                _model,
                _abilityAuthoringRoot,
                string.Empty,
                CharacterActionMechanicTuningAuthoringStore.DefaultAbilityAuthoringPath);
            if (!index.TryGet(requestStableId, out CharacterActionProjectileConsumerIndex.Entry entry))
                return "0 reference(s)";

            int actionReferences = 0;
            int abilityReferences = 0;
            string firstSource = string.Empty;
            for (int i = 0; i < entry.Usages.Count; i++)
            {
                CharacterActionProjectileConsumerIndex.Usage usage = entry.Usages[i];
                if (usage.Kind == CharacterActionProjectileConsumerKind.ActionGameplayRequest)
                    actionReferences++;
                else if (usage.Kind == CharacterActionProjectileConsumerKind.AbilityEffectTimeline)
                    abilityReferences++;
                if (string.IsNullOrEmpty(firstSource))
                    firstSource = usage.ActionId.IsValid ? usage.ActionId.ToString() : usage.TimelineId;
            }

            string selected = currentAction == null || currentAction.ActionId <= 0
                ? string.Empty
                : "#" + currentAction.ActionId;
            return entry.UsageCount + " reference(s) · action=" + actionReferences + " · ability=" + abilityReferences
                + (string.IsNullOrEmpty(firstSource) ? string.Empty : " · first=" + firstSource)
                + (string.IsNullOrEmpty(selected) ? string.Empty : " · current=" + selected);
        }
    }
}
