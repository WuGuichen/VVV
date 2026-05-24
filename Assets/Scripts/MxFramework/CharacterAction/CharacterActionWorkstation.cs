using System;
using System.Collections.Generic;
using System.Globalization;
using MxFramework.Combat.Animation;

namespace MxFramework.CharacterAction
{
    public enum CharacterActionWorkstationCapability
    {
        ReadOnlyPreview = 0,
        LightEditing = 1,
        UnityAssetEditing = 2,
    }

    public enum CharacterActionWorkstationRowKind
    {
        Phase = 0,
        Motion = 10,
        Combat = 20,
        Gameplay = 30,
        Animation = 40,
        Presentation = 50,
        Debug = 60,
        Cancel = 70,
        Interrupt = 80,
    }

    public sealed class CharacterActionWorkstationBuildRequest
    {
        public CharacterActionWorkstationBuildRequest(
            CharacterActionConfig action,
            CombatActionTimeline combatTimeline = null,
            CharacterActionDurationPolicy durationPolicy = default(CharacterActionDurationPolicy),
            CharacterActionRunnerEvent[] runnerEvents = null,
            CharacterActionDiagnostic[] additionalDiagnostics = null,
            CharacterActionWorkstationCapability[] requestedCapabilities = null)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            CombatTimeline = combatTimeline;
            DurationPolicy = durationPolicy;
            RunnerEvents = runnerEvents ?? Array.Empty<CharacterActionRunnerEvent>();
            AdditionalDiagnostics = additionalDiagnostics ?? Array.Empty<CharacterActionDiagnostic>();
            RequestedCapabilities = requestedCapabilities ?? Array.Empty<CharacterActionWorkstationCapability>();
        }

        public CharacterActionConfig Action { get; }
        public CombatActionTimeline CombatTimeline { get; }
        public CharacterActionDurationPolicy DurationPolicy { get; }
        public CharacterActionRunnerEvent[] RunnerEvents { get; }
        public CharacterActionDiagnostic[] AdditionalDiagnostics { get; }
        public CharacterActionWorkstationCapability[] RequestedCapabilities { get; }
    }

    public readonly struct CharacterActionWorkstationTimelineEntry : IEquatable<CharacterActionWorkstationTimelineEntry>
    {
        public CharacterActionWorkstationTimelineEntry(
            CharacterActionWorkstationRowKind rowKind,
            int startFrame,
            int endFrame,
            CharacterActionPhaseKind phaseKind = CharacterActionPhaseKind.None,
            CharacterActionTrackEventKind eventKind = CharacterActionTrackEventKind.None,
            CharacterActionSourceKind sourceKind = CharacterActionSourceKind.None,
            string stableId = "",
            string payload = "")
        {
            if (!Enum.IsDefined(typeof(CharacterActionWorkstationRowKind), rowKind))
                throw new ArgumentOutOfRangeException(nameof(rowKind), "Workstation row kind is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionPhaseKind), phaseKind))
                throw new ArgumentOutOfRangeException(nameof(phaseKind), "Phase kind is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionTrackEventKind), eventKind))
                throw new ArgumentOutOfRangeException(nameof(eventKind), "Track event kind is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionSourceKind), sourceKind))
                throw new ArgumentOutOfRangeException(nameof(sourceKind), "Action source kind is not defined.");
            if (startFrame < -1)
                throw new ArgumentOutOfRangeException(nameof(startFrame), "Timeline entry start frame cannot be less than -1.");
            if (endFrame < -1)
                throw new ArgumentOutOfRangeException(nameof(endFrame), "Timeline entry end frame cannot be less than -1.");
            if (startFrame >= 0 && endFrame >= 0 && endFrame < startFrame)
                throw new ArgumentOutOfRangeException(nameof(endFrame), "Timeline entry end frame cannot be before start frame.");

            RowKind = rowKind;
            StartFrame = startFrame;
            EndFrame = endFrame;
            PhaseKind = phaseKind;
            EventKind = eventKind;
            SourceKind = sourceKind;
            StableId = stableId ?? string.Empty;
            Payload = payload ?? string.Empty;
        }

        public CharacterActionWorkstationRowKind RowKind { get; }
        public int StartFrame { get; }
        public int EndFrame { get; }
        public CharacterActionPhaseKind PhaseKind { get; }
        public CharacterActionTrackEventKind EventKind { get; }
        public CharacterActionSourceKind SourceKind { get; }
        public string StableId { get; }
        public string Payload { get; }

        public bool Equals(CharacterActionWorkstationTimelineEntry other)
        {
            return RowKind == other.RowKind
                && StartFrame == other.StartFrame
                && EndFrame == other.EndFrame
                && PhaseKind == other.PhaseKind
                && EventKind == other.EventKind
                && SourceKind == other.SourceKind
                && string.Equals(StableId, other.StableId, StringComparison.Ordinal)
                && string.Equals(Payload, other.Payload, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionWorkstationTimelineEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)RowKind;
                hash = (hash * 397) ^ StartFrame;
                hash = (hash * 397) ^ EndFrame;
                hash = (hash * 397) ^ (int)PhaseKind;
                hash = (hash * 397) ^ (int)EventKind;
                hash = (hash * 397) ^ (int)SourceKind;
                hash = (hash * 397) ^ (StableId == null ? 0 : StableId.GetHashCode());
                hash = (hash * 397) ^ (Payload == null ? 0 : Payload.GetHashCode());
                return hash;
            }
        }
    }

    public sealed class CharacterActionWorkstationTimelineRow
    {
        public CharacterActionWorkstationTimelineRow(
            CharacterActionWorkstationRowKind kind,
            string label,
            CharacterActionWorkstationTimelineEntry[] entries)
        {
            if (!Enum.IsDefined(typeof(CharacterActionWorkstationRowKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind), "Workstation row kind is not defined.");

            Kind = kind;
            Label = label ?? string.Empty;
            Entries = entries ?? Array.Empty<CharacterActionWorkstationTimelineEntry>();
        }

        public CharacterActionWorkstationRowKind Kind { get; }
        public string Label { get; }
        public CharacterActionWorkstationTimelineEntry[] Entries { get; }
    }

    public readonly struct CharacterActionWorkstationPreviewEvent : IEquatable<CharacterActionWorkstationPreviewEvent>
    {
        public CharacterActionWorkstationPreviewEvent(int sequence, CharacterActionRunnerEvent runnerEvent)
        {
            if (sequence < 0)
                throw new ArgumentOutOfRangeException(nameof(sequence), "Preview event sequence cannot be negative.");

            Sequence = sequence;
            RunnerEvent = runnerEvent;
            ReplayLine = runnerEvent.ToReplayLine();
        }

        public int Sequence { get; }
        public CharacterActionRunnerEvent RunnerEvent { get; }
        public string ReplayLine { get; }

        public bool Equals(CharacterActionWorkstationPreviewEvent other)
        {
            return Sequence == other.Sequence
                && RunnerEvent.Equals(other.RunnerEvent)
                && string.Equals(ReplayLine, other.ReplayLine, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionWorkstationPreviewEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Sequence;
                hash = (hash * 397) ^ RunnerEvent.GetHashCode();
                hash = (hash * 397) ^ (ReplayLine == null ? 0 : ReplayLine.GetHashCode());
                return hash;
            }
        }
    }

    public sealed class CharacterActionWorkstationSnapshot
    {
        public CharacterActionWorkstationSnapshot(
            string actionId,
            string displayName,
            CharacterActionCategory category,
            CharacterActionTimelineAuthority timelineAuthority,
            int durationFrames,
            bool durationResolved,
            CharacterActionWorkstationTimelineRow[] timelineRows,
            CharacterActionResourceDependency[] dependencies,
            CharacterActionDiagnostic[] diagnostics,
            string[] formattedDiagnostics,
            CharacterActionWorkstationPreviewEvent[] previewEvents,
            string[] exportLines)
        {
            if (!Enum.IsDefined(typeof(CharacterActionCategory), category))
                throw new ArgumentOutOfRangeException(nameof(category), "Action category is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionTimelineAuthority), timelineAuthority))
                throw new ArgumentOutOfRangeException(nameof(timelineAuthority), "Timeline authority is not defined.");
            if (durationFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(durationFrames), "Duration frames cannot be negative.");

            ActionId = actionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Category = category;
            TimelineAuthority = timelineAuthority;
            DurationFrames = durationFrames;
            DurationResolved = durationResolved;
            TimelineRows = timelineRows ?? Array.Empty<CharacterActionWorkstationTimelineRow>();
            Dependencies = dependencies ?? Array.Empty<CharacterActionResourceDependency>();
            Diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
            FormattedDiagnostics = formattedDiagnostics ?? Array.Empty<string>();
            PreviewEvents = previewEvents ?? Array.Empty<CharacterActionWorkstationPreviewEvent>();
            ExportLines = exportLines ?? Array.Empty<string>();
        }

        public string ActionId { get; }
        public string DisplayName { get; }
        public CharacterActionCategory Category { get; }
        public CharacterActionTimelineAuthority TimelineAuthority { get; }
        public int DurationFrames { get; }
        public bool DurationResolved { get; }
        public CharacterActionWorkstationTimelineRow[] TimelineRows { get; }
        public CharacterActionResourceDependency[] Dependencies { get; }
        public CharacterActionDiagnostic[] Diagnostics { get; }
        public string[] FormattedDiagnostics { get; }
        public CharacterActionWorkstationPreviewEvent[] PreviewEvents { get; }
        public string[] ExportLines { get; }
        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Diagnostics.Length; i++)
                {
                    if (Diagnostics[i].Severity == CharacterActionDiagnosticSeverity.Error)
                        return true;
                }

                return false;
            }
        }

        public string ExportText()
        {
            return string.Join("\n", ExportLines);
        }
    }

    public static class CharacterActionWorkstation
    {
        public static CharacterActionWorkstationSnapshot BuildSnapshot(CharacterActionWorkstationBuildRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            CharacterActionConfig action = request.Action;
            CharacterActionPlanDurationResult duration = CharacterActionPlanDurationResolver.Resolve(
                action,
                request.CombatTimeline,
                request.DurationPolicy);
            int durationFrames = duration.Resolved
                ? duration.DurationFrames
                : action.DurationFrames.GetValueOrDefault();

            CharacterActionResourceDependency[] dependencies = SortDependencies(
                CharacterActionResourceDependencyCollector.Collect(action));
            CharacterActionDiagnostic[] diagnostics = BuildDiagnostics(request, duration);
            string[] formattedDiagnostics = FormatDiagnostics(action.StableId, diagnostics);
            CharacterActionWorkstationTimelineRow[] rows = BuildRows(action, durationFrames);
            CharacterActionWorkstationPreviewEvent[] previewEvents = BuildPreviewEvents(request.RunnerEvents);
            string[] exportLines = BuildExportLines(
                action,
                durationFrames,
                duration.Resolved,
                rows,
                dependencies,
                diagnostics,
                formattedDiagnostics,
                previewEvents);

            return new CharacterActionWorkstationSnapshot(
                action.StableId,
                action.DisplayName,
                action.Category,
                action.TimelineAuthority,
                durationFrames,
                duration.Resolved,
                rows,
                dependencies,
                diagnostics,
                formattedDiagnostics,
                previewEvents,
                exportLines);
        }

        public static CharacterActionWorkstationSnapshot BuildSnapshot(
            CharacterActionConfig action,
            CombatActionTimeline combatTimeline = null)
        {
            return BuildSnapshot(new CharacterActionWorkstationBuildRequest(action, combatTimeline));
        }

        private static CharacterActionDiagnostic[] BuildDiagnostics(
            CharacterActionWorkstationBuildRequest request,
            CharacterActionPlanDurationResult duration)
        {
            var diagnostics = new List<CharacterActionDiagnostic>();
            diagnostics.AddRange(duration.Diagnostics);
            diagnostics.AddRange(CharacterActionValidation.ValidateActionConfig(request.Action, request.CombatTimeline));
            diagnostics.AddRange(request.AdditionalDiagnostics);

            for (int i = 0; i < request.RequestedCapabilities.Length; i++)
            {
                CharacterActionWorkstationCapability capability = request.RequestedCapabilities[i];
                if (capability == CharacterActionWorkstationCapability.ReadOnlyPreview)
                    continue;

                string message = capability == CharacterActionWorkstationCapability.UnityAssetEditing
                    ? "Character Action Workstation MVP does not edit Unity assets; use Editor menus, Unity MCP, or dedicated generators in a later phase."
                    : "Character Action Workstation MVP is read-only; light editing is not implemented in Phase 7.";
                diagnostics.Add(CharacterActionDiagnostic.Warning(
                    CharacterActionDiagnosticCodes.WorkstationEditingUnsupported,
                    message));
            }

            return diagnostics.ToArray();
        }

        private static CharacterActionWorkstationTimelineRow[] BuildRows(
            CharacterActionConfig action,
            int durationFrames)
        {
            return new[]
            {
                new CharacterActionWorkstationTimelineRow(
                    CharacterActionWorkstationRowKind.Phase,
                    "Phase",
                    BuildPhaseEntries(action.Phases)),
                new CharacterActionWorkstationTimelineRow(
                    CharacterActionWorkstationRowKind.Motion,
                    "Motion",
                    BuildMotionEntries(action.MotionTrack.Events)),
                new CharacterActionWorkstationTimelineRow(
                    CharacterActionWorkstationRowKind.Combat,
                    "Combat",
                    BuildCombatEntries(action.CombatTrack)),
                new CharacterActionWorkstationTimelineRow(
                    CharacterActionWorkstationRowKind.Gameplay,
                    "Gameplay",
                    BuildGameplayEntries(action.GameplayTrack.Events)),
                new CharacterActionWorkstationTimelineRow(
                    CharacterActionWorkstationRowKind.Animation,
                    "Animation",
                    BuildAnimationEntries(action.AnimationTrack.Events)),
                new CharacterActionWorkstationTimelineRow(
                    CharacterActionWorkstationRowKind.Presentation,
                    "Presentation",
                    BuildPresentationEntries(action.PresentationTrack.Events)),
                new CharacterActionWorkstationTimelineRow(
                    CharacterActionWorkstationRowKind.Debug,
                    "Debug",
                    BuildDebugEntries(action.DebugTrack.Events)),
                new CharacterActionWorkstationTimelineRow(
                    CharacterActionWorkstationRowKind.Cancel,
                    "Cancel",
                    BuildCancelEntries(action.CancelRules)),
                new CharacterActionWorkstationTimelineRow(
                    CharacterActionWorkstationRowKind.Interrupt,
                    "Interrupt",
                    BuildInterruptEntries(action.InterruptRules, durationFrames)),
            };
        }

        private static CharacterActionWorkstationTimelineEntry[] BuildPhaseEntries(CharacterActionPhase[] phases)
        {
            phases = phases ?? Array.Empty<CharacterActionPhase>();
            var entries = new List<CharacterActionWorkstationTimelineEntry>(phases.Length);
            for (int i = 0; i < phases.Length; i++)
            {
                CharacterActionPhase phase = phases[i];
                entries.Add(new CharacterActionWorkstationTimelineEntry(
                    CharacterActionWorkstationRowKind.Phase,
                    phase.StartFrame,
                    phase.EndFrame,
                    phase.Kind,
                    payload: "combatAnchor=" + phase.CombatPhaseAnchor + " requiresCombatMatch=" + phase.RequiresCombatPhaseMatch));
            }

            return SortEntries(entries);
        }

        private static CharacterActionWorkstationTimelineEntry[] BuildMotionEntries(MotionTrackEvent[] events)
        {
            events = events ?? Array.Empty<MotionTrackEvent>();
            var entries = new List<CharacterActionWorkstationTimelineEntry>(events.Length);
            for (int i = 0; i < events.Length; i++)
            {
                MotionTrackEvent trackEvent = events[i];
                entries.Add(new CharacterActionWorkstationTimelineEntry(
                    CharacterActionWorkstationRowKind.Motion,
                    trackEvent.Frame,
                    trackEvent.Frame,
                    eventKind: trackEvent.Kind,
                    stableId: trackEvent.StableEventId,
                    payload: "movementMode=" + trackEvent.MovementMode
                        + " vector=" + FormatFloat(trackEvent.X) + "," + FormatFloat(trackEvent.Y) + "," + FormatFloat(trackEvent.Z)));
            }

            return SortEntries(entries);
        }

        private static CharacterActionWorkstationTimelineEntry[] BuildCombatEntries(CombatTrackConfig track)
        {
            track = track ?? CombatTrackConfig.Empty;
            var entries = new List<CharacterActionWorkstationTimelineEntry>(track.Events.Length);
            for (int i = 0; i < track.Events.Length; i++)
            {
                CombatTrackEvent trackEvent = track.Events[i];
                string combatActionId = string.IsNullOrEmpty(trackEvent.CombatActionId)
                    ? track.CombatActionId
                    : trackEvent.CombatActionId;
                entries.Add(new CharacterActionWorkstationTimelineEntry(
                    CharacterActionWorkstationRowKind.Combat,
                    trackEvent.Frame,
                    trackEvent.Frame,
                    eventKind: trackEvent.Kind,
                    stableId: trackEvent.StableEventId,
                    payload: "combatActionId=" + EmptyOrValue(combatActionId)
                        + " traceProfileId=" + EmptyOrValue(trackEvent.TraceProfileId)));
            }

            return SortEntries(entries);
        }

        private static CharacterActionWorkstationTimelineEntry[] BuildGameplayEntries(GameplayTrackEvent[] events)
        {
            events = events ?? Array.Empty<GameplayTrackEvent>();
            var entries = new List<CharacterActionWorkstationTimelineEntry>(events.Length);
            for (int i = 0; i < events.Length; i++)
            {
                GameplayTrackEvent trackEvent = events[i];
                entries.Add(new CharacterActionWorkstationTimelineEntry(
                    CharacterActionWorkstationRowKind.Gameplay,
                    trackEvent.Frame,
                    trackEvent.Frame,
                    eventKind: trackEvent.Kind,
                    stableId: trackEvent.StableEventId,
                    payload: "requestId=" + EmptyOrValue(trackEvent.RequestId)
                        + " abilityStableId=" + EmptyOrValue(trackEvent.AbilityStableId)));
            }

            return SortEntries(entries);
        }

        private static CharacterActionWorkstationTimelineEntry[] BuildAnimationEntries(AnimationTrackEvent[] events)
        {
            events = events ?? Array.Empty<AnimationTrackEvent>();
            var entries = new List<CharacterActionWorkstationTimelineEntry>(events.Length);
            for (int i = 0; i < events.Length; i++)
            {
                AnimationTrackEvent trackEvent = events[i];
                entries.Add(new CharacterActionWorkstationTimelineEntry(
                    CharacterActionWorkstationRowKind.Animation,
                    trackEvent.Frame,
                    trackEvent.Frame,
                    eventKind: trackEvent.Kind,
                    stableId: trackEvent.StableEventId,
                    payload: "animationActionKey=" + EmptyOrValue(trackEvent.AnimationActionKey)
                        + " transitionSeconds=" + FormatFloat(trackEvent.TransitionSeconds)));
            }

            return SortEntries(entries);
        }

        private static CharacterActionWorkstationTimelineEntry[] BuildPresentationEntries(PresentationTrackEvent[] events)
        {
            events = events ?? Array.Empty<PresentationTrackEvent>();
            var entries = new List<CharacterActionWorkstationTimelineEntry>(events.Length);
            for (int i = 0; i < events.Length; i++)
            {
                PresentationTrackEvent trackEvent = events[i];
                entries.Add(new CharacterActionWorkstationTimelineEntry(
                    CharacterActionWorkstationRowKind.Presentation,
                    trackEvent.Frame,
                    trackEvent.Frame,
                    eventKind: trackEvent.Kind,
                    stableId: trackEvent.StableEventId,
                    payload: "cueId=" + EmptyOrValue(trackEvent.CueId)
                        + " resourceKey=" + EmptyOrValue(trackEvent.ResourceKey)));
            }

            return SortEntries(entries);
        }

        private static CharacterActionWorkstationTimelineEntry[] BuildDebugEntries(DebugTrackEvent[] events)
        {
            events = events ?? Array.Empty<DebugTrackEvent>();
            var entries = new List<CharacterActionWorkstationTimelineEntry>(events.Length);
            for (int i = 0; i < events.Length; i++)
            {
                DebugTrackEvent trackEvent = events[i];
                entries.Add(new CharacterActionWorkstationTimelineEntry(
                    CharacterActionWorkstationRowKind.Debug,
                    trackEvent.Frame,
                    trackEvent.Frame,
                    eventKind: trackEvent.Kind,
                    stableId: trackEvent.StableEventId,
                    payload: "markerId=" + EmptyOrValue(trackEvent.MarkerId)));
            }

            return SortEntries(entries);
        }

        private static CharacterActionWorkstationTimelineEntry[] BuildCancelEntries(CharacterCancelRule[] rules)
        {
            rules = rules ?? Array.Empty<CharacterCancelRule>();
            var entries = new List<CharacterActionWorkstationTimelineEntry>(rules.Length);
            for (int i = 0; i < rules.Length; i++)
            {
                CharacterCancelRule rule = rules[i];
                entries.Add(new CharacterActionWorkstationTimelineEntry(
                    CharacterActionWorkstationRowKind.Cancel,
                    rule.StartFrame,
                    rule.EndFrame,
                    sourceKind: rule.SourceKind,
                    stableId: "cancel." + i.ToString(CultureInfo.InvariantCulture),
                    payload: "targetActionId=" + rule.TargetActionId.ToString(CultureInfo.InvariantCulture)
                        + " allow=" + rule.Allow));
            }

            return SortEntries(entries);
        }

        private static CharacterActionWorkstationTimelineEntry[] BuildInterruptEntries(CharacterInterruptRule[] rules, int durationFrames)
        {
            rules = rules ?? Array.Empty<CharacterInterruptRule>();
            var entries = new List<CharacterActionWorkstationTimelineEntry>(rules.Length);
            int startFrame = durationFrames > 0 ? 0 : -1;
            int endFrame = durationFrames > 0 ? durationFrames - 1 : -1;
            for (int i = 0; i < rules.Length; i++)
            {
                CharacterInterruptRule rule = rules[i];
                entries.Add(new CharacterActionWorkstationTimelineEntry(
                    CharacterActionWorkstationRowKind.Interrupt,
                    startFrame,
                    endFrame,
                    sourceKind: rule.SourceKind,
                    stableId: "interrupt." + i.ToString(CultureInfo.InvariantCulture),
                    payload: "minimumPriority=" + rule.MinimumPriority.ToString(CultureInfo.InvariantCulture)
                        + " targetActionId=" + rule.TargetActionId.ToString(CultureInfo.InvariantCulture)
                        + " allow=" + rule.Allow));
            }

            return SortEntries(entries);
        }

        private static CharacterActionWorkstationPreviewEvent[] BuildPreviewEvents(CharacterActionRunnerEvent[] runnerEvents)
        {
            runnerEvents = runnerEvents ?? Array.Empty<CharacterActionRunnerEvent>();
            var events = new CharacterActionWorkstationPreviewEvent[runnerEvents.Length];
            for (int i = 0; i < runnerEvents.Length; i++)
                events[i] = new CharacterActionWorkstationPreviewEvent(i, runnerEvents[i]);
            return events;
        }

        private static string[] FormatDiagnostics(string actionId, CharacterActionDiagnostic[] diagnostics)
        {
            var contexts = new CharacterActionDiagnosticFormatContext[diagnostics.Length];
            for (int i = 0; i < contexts.Length; i++)
                contexts[i] = new CharacterActionDiagnosticFormatContext(actionId: actionId);
            return CharacterActionDiagnosticFormatter.FormatMany(diagnostics, contexts);
        }

        private static CharacterActionResourceDependency[] SortDependencies(CharacterActionResourceDependency[] dependencies)
        {
            var sorted = new List<CharacterActionResourceDependency>(dependencies ?? Array.Empty<CharacterActionResourceDependency>());
            sorted.Sort(CompareDependencies);
            return sorted.ToArray();
        }

        private static CharacterActionWorkstationTimelineEntry[] SortEntries(List<CharacterActionWorkstationTimelineEntry> entries)
        {
            entries.Sort(CompareEntries);
            return entries.ToArray();
        }

        private static int CompareEntries(CharacterActionWorkstationTimelineEntry left, CharacterActionWorkstationTimelineEntry right)
        {
            int compare = left.StartFrame.CompareTo(right.StartFrame);
            if (compare != 0)
                return compare;
            compare = left.EndFrame.CompareTo(right.EndFrame);
            if (compare != 0)
                return compare;
            compare = left.PhaseKind.CompareTo(right.PhaseKind);
            if (compare != 0)
                return compare;
            compare = left.EventKind.CompareTo(right.EventKind);
            if (compare != 0)
                return compare;
            compare = left.SourceKind.CompareTo(right.SourceKind);
            if (compare != 0)
                return compare;
            compare = string.CompareOrdinal(left.StableId, right.StableId);
            if (compare != 0)
                return compare;
            return string.CompareOrdinal(left.Payload, right.Payload);
        }

        private static int CompareDependencies(CharacterActionResourceDependency left, CharacterActionResourceDependency right)
        {
            int compare = left.Kind.CompareTo(right.Kind);
            if (compare != 0)
                return compare;
            compare = left.Frame.CompareTo(right.Frame);
            if (compare != 0)
                return compare;
            compare = left.TrackKind.CompareTo(right.TrackKind);
            if (compare != 0)
                return compare;
            compare = left.EventKind.CompareTo(right.EventKind);
            if (compare != 0)
                return compare;
            compare = string.CompareOrdinal(left.StableId, right.StableId);
            if (compare != 0)
                return compare;
            return string.CompareOrdinal(left.StableEventId, right.StableEventId);
        }

        private static string[] BuildExportLines(
            CharacterActionConfig action,
            int durationFrames,
            bool durationResolved,
            CharacterActionWorkstationTimelineRow[] rows,
            CharacterActionResourceDependency[] dependencies,
            CharacterActionDiagnostic[] diagnostics,
            string[] formattedDiagnostics,
            CharacterActionWorkstationPreviewEvent[] previewEvents)
        {
            var lines = new List<string>();
            lines.Add("workstation action=" + EmptyOrValue(action.StableId)
                + " displayName=" + EmptyOrValue(action.DisplayName)
                + " category=" + action.Category
                + " authority=" + action.TimelineAuthority
                + " duration=" + durationFrames.ToString(CultureInfo.InvariantCulture)
                + " durationResolved=" + durationResolved);

            for (int i = 0; i < rows.Length; i++)
            {
                CharacterActionWorkstationTimelineRow row = rows[i];
                lines.Add("row kind=" + row.Kind
                    + " label=" + EmptyOrValue(row.Label)
                    + " entries=" + row.Entries.Length.ToString(CultureInfo.InvariantCulture));
                for (int j = 0; j < row.Entries.Length; j++)
                {
                    CharacterActionWorkstationTimelineEntry entry = row.Entries[j];
                    lines.Add("entry row=" + entry.RowKind
                        + " start=" + entry.StartFrame.ToString(CultureInfo.InvariantCulture)
                        + " end=" + entry.EndFrame.ToString(CultureInfo.InvariantCulture)
                        + " phase=" + entry.PhaseKind
                        + " event=" + entry.EventKind
                        + " source=" + entry.SourceKind
                        + " stable=" + EmptyOrValue(entry.StableId)
                        + " payload=" + EmptyOrValue(entry.Payload));
                }
            }

            for (int i = 0; i < dependencies.Length; i++)
            {
                CharacterActionResourceDependency dependency = dependencies[i];
                lines.Add("dependency kind=" + dependency.Kind
                    + " id=" + EmptyOrValue(dependency.StableId)
                    + " action=" + EmptyOrValue(dependency.ActionId)
                    + " track=" + dependency.TrackKind
                    + " event=" + dependency.EventKind
                    + " frame=" + dependency.Frame.ToString(CultureInfo.InvariantCulture)
                    + " stableEvent=" + EmptyOrValue(dependency.StableEventId)
                    + " missing=" + dependency.IsMissing);
            }

            for (int i = 0; i < diagnostics.Length; i++)
            {
                string formatted = i < formattedDiagnostics.Length ? formattedDiagnostics[i] : CharacterActionDiagnosticFormatter.Format(diagnostics[i]);
                lines.Add("diagnostic " + formatted);
            }

            for (int i = 0; i < previewEvents.Length; i++)
            {
                CharacterActionWorkstationPreviewEvent previewEvent = previewEvents[i];
                lines.Add("preview sequence=" + previewEvent.Sequence.ToString(CultureInfo.InvariantCulture)
                    + " " + previewEvent.ReplayLine);
            }

            return lines.ToArray();
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("G9", CultureInfo.InvariantCulture);
        }

        private static string EmptyOrValue(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : Escape(value);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "-";

            return value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
