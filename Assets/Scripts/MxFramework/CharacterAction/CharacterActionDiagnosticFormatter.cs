using System;

namespace MxFramework.CharacterAction
{
    public readonly struct CharacterActionDiagnosticFormatContext : IEquatable<CharacterActionDiagnosticFormatContext>
    {
        public CharacterActionDiagnosticFormatContext(
            string actionId = "",
            CharacterActionPhaseKind phaseKind = CharacterActionPhaseKind.None,
            CharacterActionTrackKind trackKind = CharacterActionTrackKind.Motion,
            bool hasTrack = false,
            int frame = -1,
            string suggestedFix = "")
        {
            if (!Enum.IsDefined(typeof(CharacterActionPhaseKind), phaseKind))
                throw new ArgumentOutOfRangeException(nameof(phaseKind), "Phase kind is not defined.");
            if (!Enum.IsDefined(typeof(CharacterActionTrackKind), trackKind))
                throw new ArgumentOutOfRangeException(nameof(trackKind), "Track kind is not defined.");

            ActionId = actionId ?? string.Empty;
            PhaseKind = phaseKind;
            TrackKind = trackKind;
            HasTrack = hasTrack;
            Frame = frame;
            SuggestedFix = suggestedFix ?? string.Empty;
        }

        public string ActionId { get; }
        public CharacterActionPhaseKind PhaseKind { get; }
        public CharacterActionTrackKind TrackKind { get; }
        public bool HasTrack { get; }
        public int Frame { get; }
        public string SuggestedFix { get; }

        public bool Equals(CharacterActionDiagnosticFormatContext other)
        {
            return string.Equals(ActionId, other.ActionId, StringComparison.Ordinal)
                && PhaseKind == other.PhaseKind
                && TrackKind == other.TrackKind
                && HasTrack == other.HasTrack
                && Frame == other.Frame
                && string.Equals(SuggestedFix, other.SuggestedFix, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterActionDiagnosticFormatContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ActionId != null ? ActionId.GetHashCode() : 0;
                hash = (hash * 397) ^ (int)PhaseKind;
                hash = (hash * 397) ^ (int)TrackKind;
                hash = (hash * 397) ^ HasTrack.GetHashCode();
                hash = (hash * 397) ^ Frame;
                hash = (hash * 397) ^ (SuggestedFix != null ? SuggestedFix.GetHashCode() : 0);
                return hash;
            }
        }
    }

    public static class CharacterActionDiagnosticFormatter
    {
        public static string Format(CharacterActionDiagnostic diagnostic)
        {
            return Format(diagnostic, default);
        }

        public static string Format(CharacterActionDiagnostic diagnostic, CharacterActionDiagnosticFormatContext context)
        {
            return "code=" + Escape(diagnostic.Code)
                + " severity=" + diagnostic.Severity
                + " action=" + EmptyOrValue(context.ActionId)
                + " phase=" + (context.PhaseKind == CharacterActionPhaseKind.None ? "-" : context.PhaseKind.ToString())
                + " track=" + (context.HasTrack ? context.TrackKind.ToString() : "-")
                + " frame=" + (context.Frame >= 0 ? context.Frame.ToString() : "-")
                + " message=" + Escape(diagnostic.Message)
                + " suggestedFix=" + EmptyOrValue(context.SuggestedFix);
        }

        public static string[] FormatMany(
            CharacterActionDiagnostic[] diagnostics,
            CharacterActionDiagnosticFormatContext[] contexts = null)
        {
            diagnostics = diagnostics ?? Array.Empty<CharacterActionDiagnostic>();
            contexts = contexts ?? Array.Empty<CharacterActionDiagnosticFormatContext>();
            var lines = new string[diagnostics.Length];
            for (int i = 0; i < diagnostics.Length; i++)
            {
                CharacterActionDiagnosticFormatContext context = i < contexts.Length
                    ? contexts[i]
                    : default;
                lines[i] = Format(diagnostics[i], context);
            }

            return lines;
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
