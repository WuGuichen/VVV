using System;
using System.Collections.Generic;
using System.Text;
using MxFramework.Config.Runtime;
using MxFramework.Gameplay;
using MxFramework.UI.Toolkit;

namespace MxFramework.Demo
{
    public static class RuntimeAbilitySliceDiagnosticViewModelBuilder
    {
        private const int SummaryEventLimit = 4;

        public static MxRuntimeDiagnosticViewModel Build(
            GameplayDiagnosticSnapshot snapshot,
            RuntimeConfigChangeSummary configSummary)
        {
            var view = new MxRuntimeDiagnosticViewModel();
            Fill(view, snapshot, configSummary);
            return view;
        }

        public static void Fill(
            MxRuntimeDiagnosticViewModel view,
            GameplayDiagnosticSnapshot snapshot,
            RuntimeConfigChangeSummary configSummary)
        {
            if (view == null)
                return;

            view.HeaderText = BuildHeader(snapshot);
            view.LastCastText = BuildLastCastText(snapshot);
            view.ConfigSourceText = BuildConfigSourceText(configSummary);
            view.ErrorSummaryText = BuildErrorSummaryText(snapshot, configSummary);
            view.EntitySummaryLines = BuildEntitySummaryLines(snapshot);
            view.EntityTechnicalLines = BuildEntityTechnicalLines(snapshot);
            view.AbilityEventSummaryLines = BuildAbilityEventSummaryLines(snapshot);
            view.AbilityEventTechnicalLines = BuildAbilityEventTechnicalLines(snapshot);
            view.AttributeEventSummaryLines = BuildAttributeEventSummaryLines(snapshot);
            view.AttributeEventTechnicalLines = BuildAttributeEventTechnicalLines(snapshot);
            view.ConfigSummaryLines = BuildConfigSummaryLines(configSummary);
            view.ConfigTechnicalLines = BuildConfigTechnicalLines(configSummary);
            view.ErrorSummaryLines = BuildErrorLines(snapshot, configSummary, latestOnly: true);
            view.ErrorTechnicalLines = BuildErrorLines(snapshot, configSummary, latestOnly: false);
        }

        private static string BuildHeader(GameplayDiagnosticSnapshot snapshot)
        {
            if (snapshot == null)
                return "Snapshot: waiting for runtime data";

            return "Snapshot "
                + Unknown(snapshot.SourceName)
                + " | Ability "
                + Unknown(snapshot.AbilitySource)
                + " | Entities "
                + snapshot.Entities.Count
                + " | Ability Events "
                + snapshot.AbilityEvents.Count
                + " | AttributeChanged Events "
                + snapshot.AttributeEvents.Count;
        }

        private static string BuildLastCastText(GameplayDiagnosticSnapshot snapshot)
        {
            if (snapshot == null)
                return "Last cast: waiting";

            string targets = JoinIds(snapshot.LastTargetEntityIds, "none");
            if (!string.IsNullOrEmpty(snapshot.LastFailureReason))
                return "Last cast failure: " + snapshot.LastFailureReason + " | targets=" + targets;

            return snapshot.LastCastSuccess
                ? "Last cast: success | targets=" + targets
                : "Last cast: not recorded | targets=" + targets;
        }

        private static string BuildConfigSourceText(RuntimeConfigChangeSummary summary)
        {
            if (summary == null)
                return "Config Source: no runtime config summary";

            return "Config Source: "
                + Unknown(summary.SourceName)
                + " | previous="
                + Unknown(summary.PreviousSourceName)
                + " | policy="
                + Unknown(summary.ApplyPolicy)
                + " | changed="
                + summary.ChangedAbilityCount
                + "/"
                + summary.ChangedBuffCount
                + "/"
                + summary.ChangedModifierCount
                + " | rebuilt="
                + summary.RebuiltAbilityCount
                + " | failed="
                + summary.FailedAbilityCount;
        }

        private static string BuildErrorSummaryText(
            GameplayDiagnosticSnapshot snapshot,
            RuntimeConfigChangeSummary summary)
        {
            if (snapshot != null && !string.IsNullOrEmpty(snapshot.LastFailureReason))
                return "Last cast failure: " + snapshot.LastFailureReason;

            if (summary != null && summary.Errors.Count > 0)
                return "Config error: " + summary.Errors[summary.Errors.Count - 1];

            return "No runtime errors";
        }

        private static IReadOnlyList<string> BuildEntitySummaryLines(GameplayDiagnosticSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Entities.Count == 0)
                return Array.Empty<string>();

            var lines = new List<string>(snapshot.Entities.Count);
            for (int i = 0; i < snapshot.Entities.Count; i++)
            {
                GameplayEntitySnapshot entity = snapshot.Entities[i];
                lines.Add(
                    "Entity#"
                    + entity.EntityId
                    + " T"
                    + entity.TeamId
                    + " "
                    + (entity.IsAlive ? "Alive" : "Down")
                    + " | "
                    + FormatAttributes(entity.Attributes)
                    + " | buffs="
                    + entity.Buffs.Count
                    + " modifiers="
                    + entity.Modifiers.Count);
            }

            return lines;
        }

        private static IReadOnlyList<string> BuildEntityTechnicalLines(GameplayDiagnosticSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Entities.Count == 0)
                return Array.Empty<string>();

            var lines = new List<string>(snapshot.Entities.Count);
            for (int i = 0; i < snapshot.Entities.Count; i++)
            {
                GameplayEntitySnapshot entity = snapshot.Entities[i];
                lines.Add(
                    "Entity#"
                    + entity.EntityId
                    + " team="
                    + entity.TeamId
                    + " alive="
                    + entity.IsAlive
                    + "\nAttributes: "
                    + FormatAttributes(entity.Attributes)
                    + "\nBuffs: "
                    + FormatBuffs(entity.Buffs)
                    + "\nModifiers: "
                    + FormatModifiers(entity.Modifiers));
            }

            return lines;
        }

        private static IReadOnlyList<string> BuildAbilityEventSummaryLines(GameplayDiagnosticSnapshot snapshot)
        {
            if (snapshot == null || snapshot.AbilityEvents.Count == 0)
                return Array.Empty<string>();

            return BuildLatestLines(snapshot.AbilityEvents, FormatAbilityEvent);
        }

        private static IReadOnlyList<string> BuildAbilityEventTechnicalLines(GameplayDiagnosticSnapshot snapshot)
        {
            if (snapshot == null || snapshot.AbilityEvents.Count == 0)
                return Array.Empty<string>();

            var lines = new List<string>(snapshot.AbilityEvents.Count);
            for (int i = 0; i < snapshot.AbilityEvents.Count; i++)
                lines.Add("#" + i + " " + FormatAbilityEvent(snapshot.AbilityEvents[i]));
            return lines;
        }

        private static IReadOnlyList<string> BuildAttributeEventSummaryLines(GameplayDiagnosticSnapshot snapshot)
        {
            if (snapshot == null || snapshot.AttributeEvents.Count == 0)
                return Array.Empty<string>();

            return BuildLatestLines(snapshot.AttributeEvents, FormatAttributeEvent);
        }

        private static IReadOnlyList<string> BuildAttributeEventTechnicalLines(GameplayDiagnosticSnapshot snapshot)
        {
            if (snapshot == null || snapshot.AttributeEvents.Count == 0)
                return Array.Empty<string>();

            var lines = new List<string>(snapshot.AttributeEvents.Count);
            for (int i = 0; i < snapshot.AttributeEvents.Count; i++)
                lines.Add("#" + i + " " + FormatAttributeEvent(snapshot.AttributeEvents[i]));
            return lines;
        }

        private static IReadOnlyList<string> BuildConfigSummaryLines(RuntimeConfigChangeSummary summary)
        {
            if (summary == null)
                return Array.Empty<string>();

            return new[]
            {
                "source=" + Unknown(summary.SourceName)
                    + " previous=" + Unknown(summary.PreviousSourceName)
                    + " policy=" + Unknown(summary.ApplyPolicy),
                "changed abilities=" + summary.ChangedAbilityCount
                    + " buffs=" + summary.ChangedBuffCount
                    + " modifiers=" + summary.ChangedModifierCount
                    + " rebuilt=" + summary.RebuiltAbilityCount
                    + " failed=" + summary.FailedAbilityCount
            };
        }

        private static IReadOnlyList<string> BuildConfigTechnicalLines(RuntimeConfigChangeSummary summary)
        {
            if (summary == null)
                return Array.Empty<string>();

            return new[]
            {
                "source=" + Unknown(summary.SourceName),
                "previous=" + Unknown(summary.PreviousSourceName),
                "policy=" + Unknown(summary.ApplyPolicy),
                "changed abilities=" + JoinIds(summary.ChangedAbilityIds, "none"),
                "changed buffs=" + JoinIds(summary.ChangedBuffIds, "none"),
                "changed modifiers=" + JoinIds(summary.ChangedModifierIds, "none"),
                "rebuilt abilities=" + JoinIds(summary.RebuiltAbilityIds, "none"),
                "failed abilities=" + JoinIds(summary.FailedAbilityIds, "none")
            };
        }

        private static IReadOnlyList<string> BuildErrorLines(
            GameplayDiagnosticSnapshot snapshot,
            RuntimeConfigChangeSummary summary,
            bool latestOnly)
        {
            var lines = new List<string>();
            if (snapshot != null && !string.IsNullOrEmpty(snapshot.LastFailureReason))
                lines.Add("Last cast failure: " + snapshot.LastFailureReason);

            if (summary != null)
            {
                for (int i = 0; i < summary.Errors.Count; i++)
                    lines.Add("Config error: " + summary.Errors[i]);
            }

            if (!latestOnly || lines.Count <= 1)
                return lines;

            return new[] { lines[lines.Count - 1] };
        }

        private static IReadOnlyList<string> BuildLatestLines<T>(
            IReadOnlyList<T> events,
            Func<T, string> format)
        {
            int total = events.Count;
            int shown = Math.Min(SummaryEventLimit, total);
            int start = total - shown;
            var lines = new List<string>(shown + 1)
            {
                "showing latest " + shown + " / total " + total
            };

            for (int i = total - 1; i >= start; i--)
                lines.Add(format(events[i]));

            return lines;
        }

        private static string FormatAbilityEvent(GameplayAbilityEventSnapshot evt)
        {
            string target = evt.TargetEntityId.HasValue ? evt.TargetEntityId.Value.ToString() : "none";
            string caster = evt.CasterEntityId.HasValue ? evt.CasterEntityId.Value.ToString() : "none";
            string failure = string.IsNullOrEmpty(evt.FailureReason) ? string.Empty : " failure=" + evt.FailureReason;
            return evt.EventType
                + " ability="
                + evt.AbilityId
                + " caster="
                + caster
                + " target="
                + target
                + failure;
        }

        private static string FormatAttributeEvent(GameplayAttributeEventSnapshot evt)
        {
            return FormatAttributeName(evt.AttributeId)
                + " base="
                + evt.BaseValue
                + " old="
                + evt.OldValue
                + " new="
                + evt.NewValue
                + " delta="
                + evt.Delta
                + " source="
                + Unknown(evt.SourceName);
        }

        private static string FormatAttributes(IReadOnlyList<GameplayAttributeSnapshot> attributes)
        {
            if (attributes == null || attributes.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < attributes.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");

                builder.Append(FormatAttributeName(attributes[i].AttributeId))
                    .Append("=")
                    .Append(attributes[i].FinalValue);
            }

            return builder.ToString();
        }

        private static string FormatBuffs(IReadOnlyList<GameplayBuffSnapshot> buffs)
        {
            if (buffs == null || buffs.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < buffs.Count; i++)
            {
                GameplayBuffSnapshot buff = buffs[i];
                if (i > 0)
                    builder.Append("; ");

                builder.Append("id=")
                    .Append(buff.BuffId)
                    .Append(" duration=")
                    .Append(buff.Duration.ToString("0.##"))
                    .Append(" remaining=")
                    .Append(buff.RemainingTime.ToString("0.##"))
                    .Append(" layers=")
                    .Append(buff.CurrentLayers)
                    .Append("/")
                    .Append(buff.MaxLayers)
                    .Append(" permanent=")
                    .Append(buff.IsPermanent)
                    .Append(" expired=")
                    .Append(buff.IsExpired);
            }

            return builder.ToString();
        }

        private static string FormatModifiers(IReadOnlyList<GameplayModifierSnapshot> modifiers)
        {
            if (modifiers == null || modifiers.Count == 0)
                return "none";

            var builder = new StringBuilder();
            for (int i = 0; i < modifiers.Count; i++)
            {
                GameplayModifierSnapshot modifier = modifiers[i];
                if (i > 0)
                    builder.Append("; ");

                builder.Append("id=")
                    .Append(modifier.ModifierId)
                    .Append(" paramIndex=")
                    .Append(modifier.ParamIndex);
            }

            return builder.ToString();
        }

        private static string FormatAttributeName(int attributeId)
        {
            switch (attributeId)
            {
                case AbilityConst.AttrHp:
                    return "HP";
                case AbilityConst.AttrAttack:
                    return "ATK";
                case AbilityConst.AttrDefense:
                    return "DEF";
                default:
                    return "Attr#" + attributeId;
            }
        }

        private static string JoinIds(IReadOnlyList<int> ids, string empty)
        {
            if (ids == null || ids.Count == 0)
                return empty;

            var builder = new StringBuilder();
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0)
                    builder.Append(", ");

                builder.Append(ids[i]);
            }

            return builder.ToString();
        }

        private static string Unknown(string value)
        {
            return string.IsNullOrEmpty(value) ? "(unknown)" : value;
        }
    }
}
