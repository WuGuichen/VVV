using System;

namespace MxFramework.Game.CharacterTest
{
    internal enum CharacterTestBehaviorDeckOperationKind
    {
        None = 0,
        Install = 1,
        Remove = 2
    }

    internal enum CharacterTestBehaviorDeckOperationOutcome
    {
        None = 0,
        Success = 1,
        Rejected = 2
    }

    internal readonly struct CharacterTestBehaviorDeckOperationFeedback
    {
        public CharacterTestBehaviorDeckOperationFeedback(
            long sequence,
            string characterStableId,
            string stableCardId,
            CharacterTestBehaviorDeckOperationKind kind,
            CharacterTestBehaviorDeckOperationOutcome outcome,
            int beforeSlotCount,
            int afterSlotCount,
            int beforeCost,
            int afterCost,
            int installedSlotIndex,
            string diagnosticCode,
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity severity,
            string message,
            double expiresAtUnscaledSeconds)
        {
            Sequence = sequence;
            CharacterStableId = characterStableId ?? string.Empty;
            StableCardId = stableCardId ?? string.Empty;
            Kind = kind;
            Outcome = outcome;
            BeforeSlotCount = beforeSlotCount;
            AfterSlotCount = afterSlotCount;
            BeforeCost = beforeCost;
            AfterCost = afterCost;
            InstalledSlotIndex = installedSlotIndex;
            DiagnosticCode = diagnosticCode ?? string.Empty;
            Severity = severity;
            Message = message ?? string.Empty;
            ExpiresAtUnscaledSeconds = expiresAtUnscaledSeconds;
        }

        public long Sequence { get; }
        public string CharacterStableId { get; }
        public string StableCardId { get; }
        public CharacterTestBehaviorDeckOperationKind Kind { get; }
        public CharacterTestBehaviorDeckOperationOutcome Outcome { get; }
        public int BeforeSlotCount { get; }
        public int AfterSlotCount { get; }
        public int BeforeCost { get; }
        public int AfterCost { get; }
        public int InstalledSlotIndex { get; }
        public string DiagnosticCode { get; }
        public CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity Severity { get; }
        public string Message { get; }
        public double ExpiresAtUnscaledSeconds { get; }
        public bool Visible => Outcome != CharacterTestBehaviorDeckOperationOutcome.None;
    }

    internal readonly struct CharacterTestBehaviorDeckFocusIntent
    {
        public CharacterTestBehaviorDeckFocusIntent(
            long sequence,
            CharacterTestFairyGuiBehaviorDeckCardOrigin origin,
            string stableCardId,
            CharacterTestFairyGuiBehaviorDeckCardActionRole actionRole,
            CharacterTestFairyGuiBehaviorDeckCardAction action,
            int pageIndex)
        {
            Sequence = sequence;
            Origin = origin;
            StableCardId = stableCardId ?? string.Empty;
            ActionRole = actionRole;
            Action = action;
            PageIndex = pageIndex;
        }

        public long Sequence { get; }
        public CharacterTestFairyGuiBehaviorDeckCardOrigin Origin { get; }
        public string StableCardId { get; }
        public CharacterTestFairyGuiBehaviorDeckCardActionRole ActionRole { get; }
        public CharacterTestFairyGuiBehaviorDeckCardAction Action { get; }
        public int PageIndex { get; }
    }

    internal sealed class CharacterTestBehaviorDeckOperationFeedbackController
    {
        private const double FeedbackDurationSeconds = 2.4d;
        private readonly Func<double> _unscaledTimeProvider;
        private CharacterTestRuntimeAiPlannerPlayerDeckWorkbench _expectedWorkbench;
        private CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView _expectedView;
        private long _feedbackVersion;
        private long _focusSequence;
        private CharacterTestBehaviorDeckOperationFeedback _feedback;
        private CharacterTestBehaviorDeckFocusIntent _focusIntent;

        public CharacterTestBehaviorDeckOperationFeedbackController(Func<double> unscaledTimeProvider)
        {
            _unscaledTimeProvider = unscaledTimeProvider ?? throw new ArgumentNullException(nameof(unscaledTimeProvider));
        }

        public long FeedbackVersion => _feedbackVersion;
        public CharacterTestBehaviorDeckOperationFeedback Feedback => _feedback;
        public CharacterTestBehaviorDeckFocusIntent FocusIntent => _focusIntent;

        public void RecordFocusIntent(CharacterTestFairyGuiBehaviorDeckCardActionPayload payload, int pageIndex)
        {
            if (payload == null)
                return;
            _focusIntent = new CharacterTestBehaviorDeckFocusIntent(
                ++_focusSequence,
                payload.Origin,
                payload.StableCardId?.Trim(),
                payload.ActionRole,
                payload.Action,
                Math.Max(0, pageIndex));
        }

        public void RecordOperation(
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench workbench,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView before,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchResult result,
            CharacterTestFairyGuiBehaviorDeckCardActionPayload payload,
            CharacterTestBehaviorDeckOperationKind kind,
            string message)
        {
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView after = result?.View ?? workbench?.View;
            bool success = result != null && result.Success;
            CharacterTestRuntimeAiPlannerInstalledDeckDiagnostic diagnostic =
                !success && result != null && result.Diagnostics.Count > 0
                    ? result.Diagnostics[0]
                    : null;
            _expectedWorkbench = workbench;
            _expectedView = after;
            _feedback = new CharacterTestBehaviorDeckOperationFeedback(
                ++_feedbackVersion,
                after?.CharacterStableId ?? before?.CharacterStableId,
                payload?.StableCardId?.Trim(),
                kind,
                success
                    ? CharacterTestBehaviorDeckOperationOutcome.Success
                    : CharacterTestBehaviorDeckOperationOutcome.Rejected,
                before?.SlotCount ?? 0,
                after?.SlotCount ?? before?.SlotCount ?? 0,
                before?.KnownCost ?? 0,
                after?.KnownCost ?? before?.KnownCost ?? 0,
                success && kind == CharacterTestBehaviorDeckOperationKind.Install
                    ? ResolveInstalledSlotIndex(after, payload?.StableCardId)
                    : -1,
                diagnostic?.Code,
                diagnostic?.Severity ?? (success
                    ? CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Info
                    : CharacterTestRuntimeAiPlannerInstalledDeckDiagnosticSeverity.Error),
                message,
                _unscaledTimeProvider() + FeedbackDurationSeconds);
        }

        public bool ReconcileAndExpire(
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbench workbench,
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView view)
        {
            if (!_feedback.Visible)
                return false;
            if (!ReferenceEquals(workbench, _expectedWorkbench)
                || !ReferenceEquals(view, _expectedView)
                || _unscaledTimeProvider() >= _feedback.ExpiresAtUnscaledSeconds)
            {
                ClearFeedback();
                return true;
            }
            return false;
        }

        public void ClearFeedback()
        {
            if (!_feedback.Visible && _expectedWorkbench == null && _expectedView == null)
                return;
            _feedback = default;
            _expectedWorkbench = null;
            _expectedView = null;
            _feedbackVersion++;
        }

        public void ClearAll()
        {
            ClearFeedback();
            _focusIntent = default;
        }

        private static int ResolveInstalledSlotIndex(
            CharacterTestRuntimeAiPlannerPlayerDeckWorkbenchView view,
            string stableCardId)
        {
            int slot = 0;
            for (int i = 0; view != null && i < view.Cards.Count; i++)
            {
                CharacterTestRuntimeAiPlannerPlayerDeckCardView card = view.Cards[i];
                if (!card.IsInstalled)
                    continue;
                if (string.Equals(card.StableCardId, stableCardId, StringComparison.Ordinal))
                    return slot;
                slot++;
            }
            return -1;
        }
    }
}
