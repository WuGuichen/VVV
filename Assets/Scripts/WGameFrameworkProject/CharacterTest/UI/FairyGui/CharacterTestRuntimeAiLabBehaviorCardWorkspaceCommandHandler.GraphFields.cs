using System;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabBehaviorCardWorkspaceCommandHandler
    {
        private const int GraphFieldDomainDetector = 1;
        private const int GraphFieldDomainCondition = 2;
        private const int GraphFieldDomainTransition = 3;

        private CharacterTestRuntimeAiLabStageEditIdentity _graphFieldIdentity;
        private long _graphFieldEpoch;
        private int _graphFieldDomain;
        private int _graphFieldKind;
        private string _graphFieldStableId = string.Empty;
        private string _graphFieldConditionId = string.Empty;

        internal bool HasActiveGraphFieldEdit => _graphFieldDomain != 0;

        internal bool BeginDetectorFieldEdit(
            string detectorId,
            CharacterTestRuntimeAiLabDetectorFieldKind field,
            out long epoch) => BeginGraphFieldEdit(
                GraphFieldDomainDetector,
                (int)field,
                detectorId,
                string.Empty,
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                out epoch);

        internal bool CommitDetectorFieldEdit(
            long epoch,
            string detectorId,
            CharacterTestRuntimeAiLabDetectorFieldKind field,
            in CharacterTestRuntimeAiLabDetectorFieldValue value)
        {
            if (!TryResolveGraphFieldCommit(
                    epoch,
                    GraphFieldDomainDetector,
                    (int)field,
                    detectorId,
                    string.Empty,
                    CharacterTestRuntimeAiLabLogicElementKind.Detector,
                    out int entryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity identity))
                return false;
            if (!_controller.TryEditLogicDetectorField(
                    entryIndex,
                    identity,
                    detectorId,
                    field,
                    value,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return false;
            }
            CompleteGraphFieldEdit();
            PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                detectorId,
                resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
            return true;
        }

        internal bool BeginConditionFieldEdit(
            string detectorId,
            string conditionId,
            CharacterTestRuntimeAiLabConditionFieldKind field,
            out long epoch) => BeginGraphFieldEdit(
                GraphFieldDomainCondition,
                (int)field,
                detectorId,
                conditionId,
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                out epoch);

        internal bool CommitConditionFieldEdit(
            long epoch,
            string detectorId,
            string conditionId,
            CharacterTestRuntimeAiLabConditionFieldKind field,
            in CharacterTestRuntimeAiLabConditionFieldValue value)
        {
            if (!TryResolveGraphFieldCommit(
                    epoch,
                    GraphFieldDomainCondition,
                    (int)field,
                    detectorId,
                    conditionId,
                    CharacterTestRuntimeAiLabLogicElementKind.Detector,
                    out int entryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity identity))
                return false;
            if (!_controller.TryEditLogicDetectorCondition(
                    entryIndex,
                    identity,
                    detectorId,
                    conditionId,
                    field,
                    value,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return false;
            }
            CompleteGraphFieldEdit();
            PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                detectorId,
                resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
            return true;
        }

        internal bool BeginTransitionFieldEdit(
            string transitionId,
            CharacterTestRuntimeAiLabTransitionFieldKind field,
            out long epoch) => BeginGraphFieldEdit(
                GraphFieldDomainTransition,
                (int)field,
                transitionId,
                string.Empty,
                CharacterTestRuntimeAiLabLogicElementKind.Transition,
                out epoch);

        internal bool CommitTransitionFieldEdit(
            long epoch,
            string transitionId,
            CharacterTestRuntimeAiLabTransitionFieldKind field,
            in CharacterTestRuntimeAiLabTransitionFieldValue value)
        {
            if (!TryResolveGraphFieldCommit(
                    epoch,
                    GraphFieldDomainTransition,
                    (int)field,
                    transitionId,
                    string.Empty,
                    CharacterTestRuntimeAiLabLogicElementKind.Transition,
                    out int entryIndex,
                    out CharacterTestRuntimeAiLabStageEditIdentity identity))
                return false;
            if (!_controller.TryEditLogicTransitionField(
                    entryIndex,
                    identity,
                    transitionId,
                    field,
                    value,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return false;
            }
            CompleteGraphFieldEdit();
            PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.Transition,
                transitionId,
                resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
            return true;
        }

        internal bool ApplyConditionField(
            string detectorId,
            string conditionId,
            CharacterTestRuntimeAiLabConditionFieldKind field,
            in CharacterTestRuntimeAiLabConditionFieldValue value)
        {
            if (!TryPrepareGraphMutation(out int entryIndex, out var identity))
                return false;
            if (!_controller.TryEditLogicDetectorCondition(
                    entryIndex,
                    identity,
                    detectorId,
                    conditionId,
                    field,
                    value,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return false;
            }
            PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                detectorId,
                resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
            return true;
        }

        internal bool AddDetectorCondition(string detectorId)
        {
            if (!TryPrepareGraphMutation(out int entryIndex, out var identity))
                return false;
            if (!_controller.TryAddLogicDetectorCondition(
                    entryIndex,
                    identity,
                    detectorId,
                    out _,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return false;
            }
            PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                detectorId,
                resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
            return true;
        }

        internal bool DeleteDetectorCondition(string detectorId, string conditionId)
        {
            if (!TryPrepareGraphMutation(out int entryIndex, out var identity))
                return false;
            if (!_controller.TryDeleteLogicDetectorCondition(
                    entryIndex,
                    identity,
                    detectorId,
                    conditionId,
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return false;
            }
            PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                detectorId,
                resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
            return true;
        }

        internal bool ReorderRecipeBranch(string detectorId, int direction, int steps)
        {
            if (!TryPrepareGraphMutation(out int entryIndex, out var identity))
                return false;
            if (!_controller.TryReorderLogicBranchPriority(
                    entryIndex,
                    identity,
                    detectorId,
                    direction,
                    Math.Max(1, steps),
                    out string resultingCardDraftId,
                    out string diagnostic))
            {
                LastMessage = diagnostic;
                return false;
            }
            PublishGraphContext(
                CharacterTestRuntimeAiLabLogicElementKind.Detector,
                detectorId,
                resultingCardDraftId);
            LastMessage = diagnostic;
            _refresh?.Invoke();
            return true;
        }

        internal void CancelGraphFieldEdit(bool deactivating)
        {
            if (_graphFieldEpoch != 0L)
                AdvanceGraphFieldEpoch();
            _graphFieldIdentity = default;
            _graphFieldDomain = 0;
            _graphFieldKind = 0;
            _graphFieldStableId = string.Empty;
            _graphFieldConditionId = string.Empty;
            if (deactivating)
                LastMessage = string.Empty;
        }

        private bool BeginGraphFieldEdit(
            int domain,
            int field,
            string stableId,
            string conditionId,
            CharacterTestRuntimeAiLabLogicElementKind elementKind,
            out long epoch)
        {
            epoch = 0L;
            if (!TryResolveElement(elementKind, stableId, out _)
                || !TryPrepareGraphMutation(out _, out var identity))
            {
                LastMessage = "Graph field identity is unavailable.";
                return false;
            }
            CancelStageEditSessions(clearSelection: false);
            CancelGraphFieldEdit(deactivating: false);
            _graphFieldIdentity = identity;
            _graphFieldDomain = domain;
            _graphFieldKind = field;
            _graphFieldStableId = stableId ?? string.Empty;
            _graphFieldConditionId = conditionId ?? string.Empty;
            AdvanceGraphFieldEpoch();
            epoch = _graphFieldEpoch;
            return true;
        }

        private bool TryResolveGraphFieldCommit(
            long epoch,
            int domain,
            int field,
            string stableId,
            string conditionId,
            CharacterTestRuntimeAiLabLogicElementKind elementKind,
            out int entryIndex,
            out CharacterTestRuntimeAiLabStageEditIdentity identity)
        {
            entryIndex = -1;
            identity = default;
            if (_graphFieldEpoch == 0L
                || epoch != _graphFieldEpoch
                || domain != _graphFieldDomain
                || field != _graphFieldKind
                || !string.Equals(stableId, _graphFieldStableId, StringComparison.Ordinal)
                || !string.Equals(conditionId ?? string.Empty, _graphFieldConditionId, StringComparison.Ordinal)
                || !TryResolveElement(elementKind, stableId, out _)
                || !TryPrepareGraphMutation(out entryIndex, out identity)
                || !_graphFieldIdentity.Matches(identity))
            {
                CancelGraphFieldEdit(deactivating: false);
                LastMessage = "Graph field edit expired; the draft was not changed.";
                _refresh?.Invoke();
                return false;
            }
            return true;
        }

        private void CompleteGraphFieldEdit()
        {
            _graphFieldIdentity = default;
            _graphFieldDomain = 0;
            _graphFieldKind = 0;
            _graphFieldStableId = string.Empty;
            _graphFieldConditionId = string.Empty;
            AdvanceGraphFieldEpoch();
        }

        private void AdvanceGraphFieldEpoch()
        {
            _graphFieldEpoch = _graphFieldEpoch == long.MaxValue ? 1L : _graphFieldEpoch + 1L;
        }

        private bool TryPrepareGraphMutation(
            out int entryIndex,
            out CharacterTestRuntimeAiLabStageEditIdentity identity)
        {
            bool resolved = _editHandler.TryPrepareGraphMutation(out entryIndex, out identity);
            if (!resolved)
                LastMessage = _editHandler.LastMessage;
            return resolved;
        }

        private bool TryResolveElement(
            CharacterTestRuntimeAiLabLogicElementKind kind,
            string stableId,
            out int index) => _editHandler.TryResolveElement(kind, stableId, out index);

        private void PublishGraphContext(
            CharacterTestRuntimeAiLabLogicElementKind kind,
            string stableId,
            string resultingCardDraftId) => _editHandler.PublishGraphContext(
                kind,
                stableId,
                resultingCardDraftId);
    }
}
