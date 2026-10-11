using System;

namespace MxFramework.UI
{
    /// <summary>Identifies one continuing guidance context.</summary>
    /// <remarks>NoAlloc value type. String storage is supplied by the caller.</remarks>
    public readonly struct MxUiGuidanceScopeId : IEquatable<MxUiGuidanceScopeId>
    {
        public MxUiGuidanceScopeId(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(MxUiGuidanceScopeId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MxUiGuidanceScopeId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(MxUiGuidanceScopeId left, MxUiGuidanceScopeId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MxUiGuidanceScopeId left, MxUiGuidanceScopeId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>Identifies one mergeable notice kind inside a guidance scope.</summary>
    /// <remarks>NoAlloc value type. String storage is supplied by the caller.</remarks>
    public readonly struct MxUiGuidanceMessageId : IEquatable<MxUiGuidanceMessageId>
    {
        public MxUiGuidanceMessageId(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);

        public bool Equals(MxUiGuidanceMessageId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MxUiGuidanceMessageId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(MxUiGuidanceMessageId left, MxUiGuidanceMessageId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MxUiGuidanceMessageId left, MxUiGuidanceMessageId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>Defines the semantic emphasis of guidance content.</summary>
    public enum MxUiGuidanceTone : byte
    {
        Neutral = 0,
        Info = 1,
        Success = 2,
        Warning = 3,
        Error = 4
    }

    /// <summary>Defines the closed, surface-independent guidance glyph vocabulary.</summary>
    public enum MxUiGuidanceGlyph : byte
    {
        Context = 0,
        Action = 1,
        Move = 2,
        Focus = 3,
        Progress = 4,
        Success = 5,
        Warning = 6,
        Error = 7
    }

    /// <summary>One fixed label-and-body row in a guidance card.</summary>
    /// <remarks>NoAlloc value type. Localized text resolution is deferred to the presentation sink.</remarks>
    public readonly struct MxUiGuidanceLine
    {
        public MxUiGuidanceLine(
            MxUiGuidanceGlyph glyph,
            MxUiGuidanceTone tone,
            MxUiLocalizedTextRequest label,
            MxUiLocalizedTextRequest body)
        {
            Glyph = glyph;
            Tone = tone;
            Label = label;
            Body = body;
        }

        public MxUiGuidanceGlyph Glyph { get; }
        public MxUiGuidanceTone Tone { get; }
        public MxUiLocalizedTextRequest Label { get; }
        public MxUiLocalizedTextRequest Body { get; }
        public bool IsValid =>
            MxUiGuidanceValidation.IsValidGlyph(Glyph)
            && MxUiGuidanceValidation.IsValidTone(Tone)
            && Label.IsValid
            && Body.IsValid;
    }

    /// <summary>Complete fixed-layout state for the single visible guidance card.</summary>
    /// <remarks>NoAlloc value type. Presenting the snapshot through a sink is LowFreqAlloc.</remarks>
    public readonly struct MxUiGuidanceCardSnapshot
    {
        public MxUiGuidanceCardSnapshot(
            MxUiGuidanceScopeId scopeId,
            long revision,
            bool visible,
            MxUiLocalizedTextRequest title,
            MxUiGuidanceLine actionLine,
            MxUiGuidanceLine focusLine,
            MxUiGuidanceLine statusLine)
        {
            ScopeId = scopeId;
            Revision = revision;
            Visible = visible;
            Title = title;
            ActionLine = actionLine;
            FocusLine = focusLine;
            StatusLine = statusLine;
        }

        public MxUiGuidanceScopeId ScopeId { get; }
        public long Revision { get; }
        public bool Visible { get; }
        public MxUiLocalizedTextRequest Title { get; }
        public MxUiGuidanceLine ActionLine { get; }
        public MxUiGuidanceLine FocusLine { get; }
        public MxUiGuidanceLine StatusLine { get; }
        public bool IsValid =>
            ScopeId.IsValid
            && Revision >= 0L
            && Title.IsValid
            && ActionLine.IsValid
            && FocusLine.IsValid
            && StatusLine.IsValid;
    }

    /// <summary>One bounded, transient guidance notice request.</summary>
    /// <remarks>NoAlloc value type. Submitting the request through a sink is LowFreqAlloc.</remarks>
    public readonly struct MxUiGuidanceNotice
    {
        public MxUiGuidanceNotice(
            MxUiGuidanceScopeId scopeId,
            MxUiGuidanceMessageId messageId,
            MxUiGuidanceTone tone,
            MxUiGuidanceGlyph glyph,
            MxUiLocalizedTextRequest text,
            float durationSeconds = 0f)
        {
            ScopeId = scopeId;
            MessageId = messageId;
            Tone = tone;
            Glyph = glyph;
            Text = text;
            DurationSeconds = durationSeconds;
        }

        public MxUiGuidanceScopeId ScopeId { get; }
        public MxUiGuidanceMessageId MessageId { get; }
        public MxUiGuidanceTone Tone { get; }
        public MxUiGuidanceGlyph Glyph { get; }
        public MxUiLocalizedTextRequest Text { get; }
        public float DurationSeconds { get; }
        public bool IsValid =>
            ScopeId.IsValid
            && MessageId.IsValid
            && MxUiGuidanceValidation.IsNoticeTone(Tone)
            && MxUiGuidanceValidation.IsValidGlyph(Glyph)
            && Text.IsValid
            && !float.IsNaN(DurationSeconds)
            && !float.IsInfinity(DurationSeconds)
            && DurationSeconds >= 0f;
    }

    /// <summary>Receives low-frequency, owner-thread guidance presentation requests.</summary>
    public interface IMxUiGuidanceSink
    {
        /// <summary>Shows or updates the one project-owned card.</summary>
        /// <remarks>LowFreqAlloc. Call on the sink owner thread.</remarks>
        void PresentCard(in MxUiGuidanceCardSnapshot snapshot);

        /// <summary>Attempts to display one transient notice in the fixed two-slot capacity.</summary>
        /// <remarks>LowFreqAlloc. Returns false for invalid or rejected requests; does not throw into gameplay flow.</remarks>
        bool TryPushNotice(in MxUiGuidanceNotice notice);

        /// <summary>Clears the card and notices belonging to one scope.</summary>
        /// <remarks>NoAlloc after sink initialization. Call on the sink owner thread.</remarks>
        void ClearScope(MxUiGuidanceScopeId scopeId);
    }

    /// <summary>Shared validation and duration rules for guidance contracts.</summary>
    public static class MxUiGuidanceValidation
    {
        public const float MinimumNoticeDurationSeconds = 1f;
        public const float MaximumNoticeDurationSeconds = 8f;
        public const float DefaultInfoNoticeDurationSeconds = 2.8f;
        public const float DefaultSuccessNoticeDurationSeconds = 2.8f;
        public const float DefaultWarningNoticeDurationSeconds = 4f;
        public const float DefaultErrorNoticeDurationSeconds = 4f;

        /// <remarks>NoAlloc.</remarks>
        public static bool IsValidTone(MxUiGuidanceTone tone)
        {
            return tone >= MxUiGuidanceTone.Neutral && tone <= MxUiGuidanceTone.Error;
        }

        /// <remarks>NoAlloc. Neutral is reserved for the card and is not a notice tone.</remarks>
        public static bool IsNoticeTone(MxUiGuidanceTone tone)
        {
            return tone >= MxUiGuidanceTone.Info && tone <= MxUiGuidanceTone.Error;
        }

        /// <remarks>NoAlloc.</remarks>
        public static bool IsValidGlyph(MxUiGuidanceGlyph glyph)
        {
            return glyph >= MxUiGuidanceGlyph.Context && glyph <= MxUiGuidanceGlyph.Error;
        }

        /// <summary>Resolves the tone default or clamps an explicit transient duration.</summary>
        /// <remarks>NoAlloc.</remarks>
        public static float ResolveNoticeDurationSeconds(MxUiGuidanceTone tone, float durationSeconds)
        {
            if (durationSeconds <= 0f)
            {
                switch (tone)
                {
                    case MxUiGuidanceTone.Warning:
                        return DefaultWarningNoticeDurationSeconds;
                    case MxUiGuidanceTone.Error:
                        return DefaultErrorNoticeDurationSeconds;
                    case MxUiGuidanceTone.Success:
                        return DefaultSuccessNoticeDurationSeconds;
                    default:
                        return DefaultInfoNoticeDurationSeconds;
                }
            }

            if (durationSeconds < MinimumNoticeDurationSeconds)
                return MinimumNoticeDurationSeconds;
            if (durationSeconds > MaximumNoticeDurationSeconds)
                return MaximumNoticeDurationSeconds;
            return durationSeconds;
        }

        /// <remarks>NoAlloc. Higher values have stronger overflow replacement priority.</remarks>
        public static int NoticeSeverity(MxUiGuidanceTone tone)
        {
            switch (tone)
            {
                case MxUiGuidanceTone.Error:
                    return 4;
                case MxUiGuidanceTone.Warning:
                    return 3;
                case MxUiGuidanceTone.Success:
                    return 2;
                case MxUiGuidanceTone.Info:
                    return 1;
                default:
                    return 0;
            }
        }
    }
}
