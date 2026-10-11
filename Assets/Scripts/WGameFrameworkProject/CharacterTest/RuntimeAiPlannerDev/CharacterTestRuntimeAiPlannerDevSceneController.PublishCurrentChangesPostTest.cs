using System;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        private readonly CharacterTestRuntimeAiLabPublishCurrentChangesPostTestCoordinator
            _publishCurrentChangesPostTestCoordinator =
                new CharacterTestRuntimeAiLabPublishCurrentChangesPostTestCoordinator();
        private CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult
            _lastPublishCurrentChangesPostTestResult;

        internal CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult
            LastPublishCurrentChangesPostTestResult =>
                _lastPublishCurrentChangesPostTestResult;

        /// <summary>
        /// Applies the explicit post-publish test choice. Restart reuses the UX-05 delayed
        /// save/validate/session-start transaction only after proving that the selected Card and
        /// Group are pristine projections of the newly Published revisions. Continue performs no
        /// runtime mutation and verifies the retained session fingerprint.
        /// </summary>
        /// <remarks>LowFreqAlloc: explicit post-publish user choice only.</remarks>
        internal bool TryChoosePublishCurrentChangesPostTestAction(
            string previewToken,
            CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice choice,
            int selectedEntryIndex,
            out CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult result,
            out string message)
        {
            result = _publishCurrentChangesPostTestCoordinator.Choose(
                _lastPublishCurrentChangesResult,
                previewToken,
                choice,
                BuildSessionStartRecoveryFingerprint,
                () => QueuePublishedVersionRestartAfterPublish(selectedEntryIndex));
            _lastPublishCurrentChangesPostTestResult = result;
            message = result.Message;
            _statusMessage = message;
            return result.Success;
        }

        private CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult
            QueuePublishedVersionRestartAfterPublish(int selectedEntryIndex)
        {
            int resolvedEntryIndex = ResolvePostPublishEntryIndex(selectedEntryIndex);
            string captureDiagnostic = string.Empty;
            if (resolvedEntryIndex < 0
                || !TryCaptureTestCurrentChangesRequest(
                    resolvedEntryIndex,
                    out CharacterTestRuntimeAiLabTestCurrentChangesRequest request,
                    out captureDiagnostic))
            {
                return CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult.Failed(
                    CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice
                        .RestartPublishedVersion,
                    "PUBLISH_CURRENT_CHANGES_RESTART_TARGET_UNAVAILABLE",
                    "新版本已发布，但原行为卡定位无法恢复；当前测试没有改变。 "
                        + (captureDiagnostic ?? string.Empty));
            }

            CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
            if (!ArePublishedGroupAndDirectCardsPristine(workspace, request))
            {
                return CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult.Failed(
                    CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice
                        .RestartPublishedVersion,
                    "PUBLISH_CURRENT_CHANGES_RESTART_NOT_PUBLISHED_PRISTINE",
                    "发布后又出现了新的未发布修改；当前测试没有改变。请先重新发布，或使用“测试当前修改”。");
            }

            if (!QueueTestCurrentChanges(resolvedEntryIndex, out string queueMessage))
            {
                return CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult.Failed(
                    CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice
                        .RestartPublishedVersion,
                    "PUBLISH_CURRENT_CHANGES_RESTART_QUEUE_FAILED",
                    "新版本已发布，但重新测试未能开始；当前测试没有改变。 "
                        + (queueMessage ?? string.Empty));
            }

            return CharacterTestRuntimeAiLabPublishCurrentChangesPostTestResult.Succeeded(
                CharacterTestRuntimeAiLabPublishCurrentChangesPostTestChoice
                    .RestartPublishedVersion,
                "正在用新发布版本从第 0 帧重新开始测试…",
                runtimeIdentityPreserved: false);
        }

        private bool ArePublishedGroupAndDirectCardsPristine(
            CharacterTestRuntimeAiLabDraftWorkspace workspace,
            CharacterTestRuntimeAiLabTestCurrentChangesRequest request)
        {
            if (!IsTestCurrentChangesPublishedContentPristine(workspace, request)
                || workspace == null
                || !workspace.Groups.TryGetValue(
                    request.GroupDraftId,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || group == null
                || HasUnpublishedBehaviorGroupContent(group, out _))
            {
                return false;
            }

            for (int i = 0; i < group.Entries.Count; i++)
            {
                CharacterTestRuntimeAiLabBehaviorEntryDraft entry = group.Entries[i];
                if (entry == null
                    || !workspace.Cards.TryGetValue(
                        entry.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                    || card?.HasPublishedSource != true)
                {
                    continue;
                }
                if (HasUnpublishedBehaviorCardContent(group, i, out _))
                    return false;
            }
            return true;
        }

        private int ResolvePostPublishEntryIndex(int requestedEntryIndex)
        {
            CharacterTestRuntimeAiLabDraftWorkspace workspace = DraftWorkspace;
            if (workspace == null
                || !workspace.TryGetGroupForSlot(
                    _selectedSetupTeamId,
                    _selectedSetupSlotIndex,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || group == null
                || group.Entries.Count == 0)
            {
                return -1;
            }

            if (requestedEntryIndex >= 0 && requestedEntryIndex < group.Entries.Count)
                return requestedEntryIndex;

            string shellEntryId = _shellCoordinator?.CurrentState?.PageContext?.EntryId
                ?? string.Empty;
            if (shellEntryId.Length > 0)
            {
                for (int i = 0; i < group.Entries.Count; i++)
                {
                    if (string.Equals(
                        group.Entries[i]?.EntryId,
                        shellEntryId,
                        StringComparison.Ordinal))
                    {
                        return i;
                    }
                }
            }

            return 0;
        }
    }
}
