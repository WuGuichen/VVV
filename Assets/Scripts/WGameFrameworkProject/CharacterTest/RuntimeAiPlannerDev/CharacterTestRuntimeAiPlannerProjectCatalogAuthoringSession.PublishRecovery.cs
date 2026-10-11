using System;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession
    {
        private static CharacterTestRuntimeAiLabPublishCurrentChangesResult RecoveryRequired(CharacterTestRuntimeAiLabPublishCurrentChangesPreview preview)
            => CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(CharacterTestRuntimeAiLabPublishCurrentChangesStatus.ReloadFailed,
                CharacterTestRuntimeAiLabPublishCurrentChangesStage.CatalogReload, "PUBLISH_CURRENT_CHANGES_RECOVERY_REQUIRED",
                "发布提交状态需要核对，请先恢复；本次没有再次写入。", preview, catalogChanged: true, requiresRecovery: true);

        internal CharacterTestRuntimeAiLabPublishCurrentChangesResult RecoverPublishCurrentChanges(string token,
            CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references = null)
        {
            if (!TryRestorePublishRecoveryPlan(references, out string restoreIssue))
                return CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(CharacterTestRuntimeAiLabPublishCurrentChangesStatus.ReloadFailed,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.CatalogReload, "PUBLISH_RECOVERY_RECORD_INVALID",
                    "恢复记录暂不可用，提交状态仍待核实：" + restoreIssue, GetPublishRecoveryPreview(), catalogChanged: true, requiresRecovery: true);
            if (_recoveryPublishCurrentChangesPlan == null)
            {
                if (token == _lastCommittedPublishCurrentChangesToken && _lastCommittedPublishCurrentChangesResult != null)
                    return _lastCommittedPublishCurrentChangesResult.AsDuplicateConfirmation();
                return CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(CharacterTestRuntimeAiLabPublishCurrentChangesStatus.PreviewExpired,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.CatalogReload, "PUBLISH_RECOVERY_CONTEXT_MISSING", "没有对应的待恢复发布请求。");
            }
            var plan = _recoveryPublishCurrentChangesPlan;
            if (!string.Equals(token, plan.Preview.Token, StringComparison.Ordinal)) return RecoveryRequired(plan.Preview);
            var reloaded = ReloadPublishCurrentChanges();
            if (TryValidateReloadedPublishCurrentChanges(plan, reloaded, out string issue))
            {
                _state = reloaded;
                _lastCommittedPublishCurrentChangesToken = token;
                _lastCommittedPublishCurrentChangesResult = new CharacterTestRuntimeAiLabPublishCurrentChangesResult(
                    CharacterTestRuntimeAiLabPublishCurrentChangesStatus.CatalogCommitted, CharacterTestRuntimeAiLabPublishCurrentChangesStage.CatalogReload,
                    plan.Preview, plan.PublishedCards, plan.PublishedGroup, reloaded, false, true, false, true,
                    "PUBLISH_RECOVERY_COMMIT_CONFIRMED", "已核对正式目录提交版本；继续恢复草稿和实验配置，本次没有发布新 revision。");
                _recoveryPublishCurrentChangesPlan = null;
                return _lastCommittedPublishCurrentChangesResult;
            }
            if (TryConfirmPublishCurrentChangesSourceState(reloaded, out _))
            {
                if (!CompletePublishRecoveryJournal(out _)) return RecoveryRequired(plan.Preview);
                _state = reloaded; _recoveryPublishCurrentChangesPlan = null; _pendingPublishCurrentChangesPlan = null;
                return CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(CharacterTestRuntimeAiLabPublishCurrentChangesStatus.Cancelled,
                    CharacterTestRuntimeAiLabPublishCurrentChangesStage.CatalogReload, "PUBLISH_RECOVERY_NO_COMMIT_CONFIRMED",
                    "已确认目录保持发布前版本，本次未提交。草稿保留，可重新生成发布预览。", plan.Preview);
            }
            return CharacterTestRuntimeAiLabPublishCurrentChangesResult.Fail(CharacterTestRuntimeAiLabPublishCurrentChangesStatus.ReloadFailed,
                CharacterTestRuntimeAiLabPublishCurrentChangesStage.CatalogReload, "PUBLISH_RECOVERY_STILL_UNCONFIRMED",
                "提交状态仍待核实：" + issue, plan.Preview, catalogChanged: true, requiresRecovery: true);
        }
    }
}
