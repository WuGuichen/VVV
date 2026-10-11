using System;
using MxFramework.UI;

namespace MxFramework.NoEngineTests.UI.Guidance
{
    internal static class Program
    {
        private static int Main()
        {
            StableIds_UseOrdinalValueSemantics();
            GuidanceLine_RequiresClosedSemanticsAndLocalizedText();
            CardSnapshot_RequiresStableScopeRevisionAndThreeLines();
            Notice_RejectsNeutralAndInvalidDuration();
            NoticeDuration_UsesToneDefaultsAndClamp();
            NoticeSeverity_IsStrictlyOrdered();
            CardState_RequiresMonotonicRevisionAndReplacesScope();
            NoticeState_MergesDuplicatesAndRefreshesLifetime();
            NoticeState_UsesTwoSlotsAndSeverityReplacement();
            GuidanceState_ExpiresAndClearsOnlyMatchingScope();
            Console.WriteLine("UI guidance noEngine tests passed.");
            return 0;
        }

        private static void StableIds_UseOrdinalValueSemantics()
        {
            var scope = new MxUiGuidanceScopeId("tutorial.move");
            Require(scope.IsValid, "non-empty scope should be valid");
            Require(scope == new MxUiGuidanceScopeId("tutorial.move"), "equal scope ids should compare equal");
            Require(scope != new MxUiGuidanceScopeId("Tutorial.Move"), "scope comparison should be ordinal");
            Require(!default(MxUiGuidanceScopeId).IsValid, "default scope should be invalid");

            var message = new MxUiGuidanceMessageId("entered.range");
            Require(message.IsValid, "non-empty message should be valid");
            Require(message == new MxUiGuidanceMessageId("entered.range"), "equal message ids should compare equal");
            Require(message != new MxUiGuidanceMessageId("entered.Range"), "message comparison should be ordinal");
            Require(!default(MxUiGuidanceMessageId).IsValid, "default message should be invalid");
        }

        private static void GuidanceLine_RequiresClosedSemanticsAndLocalizedText()
        {
            MxUiGuidanceLine line = Line(
                MxUiGuidanceGlyph.Action,
                MxUiGuidanceTone.Info,
                "ui.guidance.action",
                "Action",
                "ui.guidance.move",
                "Move forward");
            Require(line.IsValid, "complete guidance line should be valid");

            var invalidTone = new MxUiGuidanceLine(
                MxUiGuidanceGlyph.Action,
                (MxUiGuidanceTone)99,
                Text("label", "Label"),
                Text("body", "Body"));
            Require(!invalidTone.IsValid, "unknown tone should invalidate a line");

            var invalidGlyph = new MxUiGuidanceLine(
                (MxUiGuidanceGlyph)99,
                MxUiGuidanceTone.Info,
                Text("label", "Label"),
                Text("body", "Body"));
            Require(!invalidGlyph.IsValid, "unknown glyph should invalidate a line");
        }

        private static void CardSnapshot_RequiresStableScopeRevisionAndThreeLines()
        {
            MxUiGuidanceLine action = Line(MxUiGuidanceGlyph.Move, MxUiGuidanceTone.Info, "label.action", "Action", "body.action", "Approach the edge");
            MxUiGuidanceLine focus = Line(MxUiGuidanceGlyph.Focus, MxUiGuidanceTone.Info, "label.focus", "Observe", "body.focus", "Contact and stop");
            MxUiGuidanceLine status = Line(MxUiGuidanceGlyph.Progress, MxUiGuidanceTone.Neutral, "label.status", "Status", "body.status", "Waiting");
            var snapshot = new MxUiGuidanceCardSnapshot(
                new MxUiGuidanceScopeId("validation.boundary"),
                4L,
                true,
                Text("title.validation", "Current validation"),
                action,
                focus,
                status);
            Require(snapshot.IsValid, "complete fixed card should be valid");

            var negativeRevision = new MxUiGuidanceCardSnapshot(
                snapshot.ScopeId,
                -1L,
                true,
                snapshot.Title,
                action,
                focus,
                status);
            Require(!negativeRevision.IsValid, "negative card revision should be invalid");
        }

        private static void Notice_RejectsNeutralAndInvalidDuration()
        {
            var valid = new MxUiGuidanceNotice(
                new MxUiGuidanceScopeId("interaction.target"),
                new MxUiGuidanceMessageId("target.locked"),
                MxUiGuidanceTone.Success,
                MxUiGuidanceGlyph.Success,
                Text("notice.target.locked", "Target locked"));
            Require(valid.IsValid, "success notice with default duration should be valid");

            var neutral = new MxUiGuidanceNotice(
                valid.ScopeId,
                valid.MessageId,
                MxUiGuidanceTone.Neutral,
                valid.Glyph,
                valid.Text);
            Require(!neutral.IsValid, "neutral should remain card-only");

            var infinite = new MxUiGuidanceNotice(
                valid.ScopeId,
                valid.MessageId,
                valid.Tone,
                valid.Glyph,
                valid.Text,
                float.PositiveInfinity);
            Require(!infinite.IsValid, "infinite notice should be invalid");
        }

        private static void NoticeDuration_UsesToneDefaultsAndClamp()
        {
            RequireNear(2.8f, MxUiGuidanceValidation.ResolveNoticeDurationSeconds(MxUiGuidanceTone.Info, 0f), "info default");
            RequireNear(2.8f, MxUiGuidanceValidation.ResolveNoticeDurationSeconds(MxUiGuidanceTone.Success, 0f), "success default");
            RequireNear(4f, MxUiGuidanceValidation.ResolveNoticeDurationSeconds(MxUiGuidanceTone.Warning, 0f), "warning default");
            RequireNear(4f, MxUiGuidanceValidation.ResolveNoticeDurationSeconds(MxUiGuidanceTone.Error, 0f), "error default");
            RequireNear(1f, MxUiGuidanceValidation.ResolveNoticeDurationSeconds(MxUiGuidanceTone.Info, 0.2f), "minimum clamp");
            RequireNear(8f, MxUiGuidanceValidation.ResolveNoticeDurationSeconds(MxUiGuidanceTone.Error, 30f), "maximum clamp");
        }

        private static void NoticeSeverity_IsStrictlyOrdered()
        {
            int info = MxUiGuidanceValidation.NoticeSeverity(MxUiGuidanceTone.Info);
            int success = MxUiGuidanceValidation.NoticeSeverity(MxUiGuidanceTone.Success);
            int warning = MxUiGuidanceValidation.NoticeSeverity(MxUiGuidanceTone.Warning);
            int error = MxUiGuidanceValidation.NoticeSeverity(MxUiGuidanceTone.Error);
            Require(info < success && success < warning && warning < error, "notice severity should be Error > Warning > Success > Info");
        }

        private static void CardState_RequiresMonotonicRevisionAndReplacesScope()
        {
            var state = new MxUiGuidanceState();
            MxUiGuidanceCardSnapshot first = Card("tutorial.move", 1L, "Move");
            Require(state.TryPresentCard(in first), "first card should be accepted");
            long version = state.Version;

            MxUiGuidanceCardSnapshot stale = Card("tutorial.move", 1L, "Stale");
            Require(!state.TryPresentCard(in stale), "same-scope equal revision should be rejected");
            Require(state.Card.Title.FallbackText == "Move", "stale card must not replace the current card");
            Require(state.Version == version, "rejected card must not advance state version");
            Require(state.LastRejectionReason == MxUiGuidanceRejectionReason.StaleCardRevision, "stale card should expose a fixed reason");

            MxUiGuidanceCardSnapshot newer = Card("tutorial.move", 2L, "Move now");
            Require(state.TryPresentCard(in newer), "newer same-scope revision should be accepted");
            MxUiGuidanceCardSnapshot replacement = Card("objective.exit", 0L, "Reach exit");
            Require(state.TryPresentCard(in replacement), "different scope should replace without sharing revision history");
            Require(state.Card.ScopeId == new MxUiGuidanceScopeId("objective.exit"), "replacement scope should become current");
        }

        private static void NoticeState_MergesDuplicatesAndRefreshesLifetime()
        {
            var state = new MxUiGuidanceState();
            MxUiGuidanceNotice notice = Notice("interaction", "locked", MxUiGuidanceTone.Success, "Locked", 2f);
            Require(state.TryPushNotice(in notice), "first notice should be accepted");
            Require(state.TryGetNotice(0, out MxUiGuidanceNoticeState first), "first notice should occupy slot zero");
            state.Advance(1.5f);

            MxUiGuidanceNotice duplicate = Notice("interaction", "locked", MxUiGuidanceTone.Success, "Locked again", 3f);
            Require(state.TryPushNotice(in duplicate), "duplicate identity should merge");
            Require(state.NoticeCount == 1, "duplicate identity must not allocate another visible slot");
            Require(state.TryGetNotice(0, out MxUiGuidanceNoticeState refreshed), "refreshed notice should remain visible");
            Require(first.Sequence == refreshed.Sequence, "duplicate refresh should preserve insertion identity");
            Require(refreshed.Revision == first.Revision + 1L, "duplicate refresh should advance content revision");
            RequireNear(3f, refreshed.RemainingSeconds, "duplicate refresh duration");
            Require(refreshed.Notice.Text.FallbackText == "Locked again", "duplicate refresh should update text");
        }

        private static void NoticeState_UsesTwoSlotsAndSeverityReplacement()
        {
            var state = new MxUiGuidanceState();
            MxUiGuidanceNotice info = Notice("a", "info", MxUiGuidanceTone.Info, "Info");
            MxUiGuidanceNotice success = Notice("b", "success", MxUiGuidanceTone.Success, "Success");
            Require(state.TryPushNotice(in info), "info should enter first slot");
            Require(state.TryPushNotice(in success), "success should enter newest slot");
            Require(state.TryGetNotice(0, out MxUiGuidanceNoticeState top) && top.Notice.MessageId == success.MessageId, "new notice should be in the first slot");
            Require(state.TryGetNotice(1, out MxUiGuidanceNoticeState bottom) && bottom.Notice.MessageId == info.MessageId, "older notice should move down");

            MxUiGuidanceNotice anotherInfo = Notice("c", "info", MxUiGuidanceTone.Info, "Another info");
            Require(!state.TryPushNotice(in anotherInfo), "equal-or-lower severity must be dropped when full");
            Require(state.NoticeOverflowCount == 1, "capacity rejection should be counted");

            MxUiGuidanceNotice error = Notice("d", "error", MxUiGuidanceTone.Error, "Error");
            Require(state.TryPushNotice(in error), "higher severity should replace the oldest lower-severity notice");
            Require(state.TryGetNotice(0, out top) && top.Notice.MessageId == error.MessageId, "replacement should enter at the first slot");
            Require(state.TryGetNotice(1, out bottom) && bottom.Notice.MessageId == success.MessageId, "surviving newer notice should move down");
        }

        private static void GuidanceState_ExpiresAndClearsOnlyMatchingScope()
        {
            var state = new MxUiGuidanceState();
            MxUiGuidanceCardSnapshot card = Card("scope.a", 1L, "A");
            MxUiGuidanceNotice shortNotice = Notice("scope.a", "short", MxUiGuidanceTone.Info, "Short", 1f);
            MxUiGuidanceNotice otherNotice = Notice("scope.b", "other", MxUiGuidanceTone.Warning, "Other", 4f);
            state.PresentCard(in card);
            state.TryPushNotice(in shortNotice);
            state.TryPushNotice(in otherNotice);
            state.Advance(1.1f);
            Require(state.NoticeCount == 1, "expired notice should be removed without a queue");
            Require(state.TryGetNotice(0, out MxUiGuidanceNoticeState active) && active.Notice.ScopeId == new MxUiGuidanceScopeId("scope.b"), "remaining notice should be packed into the first slot");

            state.ClearScope(new MxUiGuidanceScopeId("scope.a"));
            Require(!state.HasCard, "matching card scope should clear");
            Require(state.NoticeCount == 1, "different notice scope should remain");
            state.ClearScope(new MxUiGuidanceScopeId("scope.b"));
            Require(state.NoticeCount == 0, "matching notice scope should clear");
        }

        private static MxUiGuidanceCardSnapshot Card(string scope, long revision, string title)
        {
            return new MxUiGuidanceCardSnapshot(
                new MxUiGuidanceScopeId(scope),
                revision,
                true,
                Text("title." + scope, title),
                Line(MxUiGuidanceGlyph.Action, MxUiGuidanceTone.Info, "action.label", "Action", "action.body", "Act"),
                Line(MxUiGuidanceGlyph.Focus, MxUiGuidanceTone.Info, "focus.label", "Focus", "focus.body", "Observe"),
                Line(MxUiGuidanceGlyph.Progress, MxUiGuidanceTone.Neutral, "status.label", "Status", "status.body", "Waiting"));
        }

        private static MxUiGuidanceNotice Notice(
            string scope,
            string message,
            MxUiGuidanceTone tone,
            string text,
            float duration = 0f)
        {
            return new MxUiGuidanceNotice(
                new MxUiGuidanceScopeId(scope),
                new MxUiGuidanceMessageId(message),
                tone,
                tone == MxUiGuidanceTone.Success ? MxUiGuidanceGlyph.Success : MxUiGuidanceGlyph.Warning,
                Text("notice." + message, text),
                duration);
        }

        private static MxUiGuidanceLine Line(
            MxUiGuidanceGlyph glyph,
            MxUiGuidanceTone tone,
            string labelKey,
            string labelFallback,
            string bodyKey,
            string bodyFallback)
        {
            return new MxUiGuidanceLine(
                glyph,
                tone,
                Text(labelKey, labelFallback),
                Text(bodyKey, bodyFallback));
        }

        private static MxUiLocalizedTextRequest Text(string key, string fallback)
        {
            return new MxUiLocalizedTextRequest(new MxUiTextKey(key), fallback);
        }

        private static void RequireNear(float expected, float actual, string message)
        {
            if (Math.Abs(expected - actual) > 0.0001f)
                throw new InvalidOperationException(message + ": expected=" + expected + " actual=" + actual);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
