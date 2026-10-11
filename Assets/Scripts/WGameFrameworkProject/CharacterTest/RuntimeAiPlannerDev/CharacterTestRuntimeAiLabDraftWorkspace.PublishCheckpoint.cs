using System;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiLabDraftWorkspace
    {
        internal sealed class PublishCurrentChangesCheckpoint
        {
            private CharacterTestRuntimeAiLabDraftWorkspace _owner;
            private WorkspaceState _before;
            private bool _completed;

            internal PublishCurrentChangesCheckpoint(
                CharacterTestRuntimeAiLabDraftWorkspace owner,
                object before)
            {
                _owner = owner;
                _before = (WorkspaceState)before;
            }

            internal void Commit()
            {
                if (_completed)
                    return;
                _owner._undo.Clear();
                _owner._redo.Clear();
                _completed = true;
                _owner = null;
                _before = null;
            }

            internal bool TryRollback(out string message)
            {
                if (_completed || _owner == null || _before == null)
                {
                    message = "发布 checkpoint 已结束，不能重复回滚。";
                    return false;
                }
                _owner.RestoreState(_before);
                _completed = true;
                _owner = null;
                _before = null;
                message = "已恢复发布前的草稿来源 checkpoint。";
                return true;
            }
        }

        /// <summary>
        /// Stages the draft-side Published checkpoints after the formal Card plus Group catalog
        /// transaction has reloaded successfully. The caller must persist the workspace and then
        /// either Commit or roll back this capability.
        /// </summary>
        /// <remarks>LowFreqAlloc: explicit successful aggregate publish boundary only.</remarks>
        internal bool TryStagePublishCurrentChangesCheckpoint(
            CharacterTestRuntimeAiLabPublishCurrentChangesResult result,
            string groupCatalogFileHash,
            out PublishCurrentChangesCheckpoint checkpoint,
            out string message,
            bool recovering = false)
        {
            checkpoint = null;
            if (IsReadOnly)
            {
                message = "Runtime AI Lab workspace is read-only.";
                return false;
            }
            if (result?.CatalogCommitSucceeded != true
                || result.Preview == null
                || result.Group == null
                || result.State?.Success != true)
            {
                message = "正式目录尚未完整提交，不能更新草稿 Published checkpoint。";
                return false;
            }
            CharacterTestRuntimeAiLabPublishCurrentChangesPreview preview = result.Preview;
            if (!string.Equals(WorkspaceId, preview.WorkspaceId, StringComparison.Ordinal)
                || (!recovering && CurrentRevision != preview.WorkspaceRevision))
            {
                message = "发布确认后工作区 revision 已变化，草稿 checkpoint 未更新。";
                return false;
            }
            if (!_groups.TryGetValue(
                    preview.GroupDraftId,
                    out CharacterTestRuntimeAiLabBehaviorGroupDraft group)
                || (!recovering && group.CurrentRevision != preview.GroupDraftRevision)
                || !string.Equals(
                    group.PublishedStableId,
                    result.Group.StableGroupId,
                    StringComparison.Ordinal)
                || (group.SourceProjectRevision != preview.SourceGroupRevision
                    && (!recovering || group.SourceProjectRevision != result.Group.Revision)))
            {
                message = "发布确认后行为组选择或来源 revision 已变化。";
                return false;
            }

            for (int i = 0; i < result.PublishedCards.Count; i++)
            {
                CharacterTestRuntimeAiLabPublishCurrentChangesPublishedCard published =
                    result.PublishedCards[i];
                if (published?.Card == null
                    || !_cards.TryGetValue(
                        published.CardDraftId,
                        out CharacterTestRuntimeAiLabBehaviorCardDraft card)
                    || (!recovering && card.CurrentRevision != published.ExpectedDraftRevision)
                    || ((card.SourcePublishedRevision != published.ExpectedSourceRevision
                        || card.SourcePublishedSemanticHash != published.ExpectedSourceSemanticHash
                        || card.SourcePublishedContentHash != published.ExpectedSourceContentHash)
                        && (!recovering || !card.MatchesPublishedSource(published.Card)))
                    || !string.Equals(
                        card.PublishedStableId,
                        published.Card.StableCardId,
                        StringComparison.Ordinal))
                {
                    message = "发布确认后行为卡草稿来源发生变化，checkpoint 未更新。";
                    return false;
                }
            }

            WorkspaceState before = CaptureState();
            try
            {
                for (int i = 0; i < result.PublishedCards.Count; i++)
                {
                    CharacterTestRuntimeAiLabPublishCurrentChangesPublishedCard published =
                        result.PublishedCards[i];
                    CharacterTestRuntimeAiLabBehaviorCardDraft card =
                        _cards[published.CardDraftId];
                    if (recovering)
                    {
                        // Recover the committed base while preserving any subsequent author edits.
                        card.SourcePublishedRevision = published.Card.Revision;
                        card.SourcePublishedSemanticHash = published.Card.SemanticHash;
                        card.SourcePublishedContentHash = published.Card.ContentHash;
                    }
                    else
                    {
                        ImportPublishedTacticalDependencies(published.Card);
                        card.CopyDefinitionFrom(CharacterTestRuntimeAiLabBehaviorCardDraft.CreatePublishedCopy(published.Card));
                    }
                    card.LastCompileStatus = "Published";
                    card.LastCompiledRevision = card.CurrentRevision;
                }

                group.SourceProjectRevision = result.Group.Revision;
                group.SourceProjectContentHash = result.Group.ContentHash;
                group.SourceCatalogFileHash = groupCatalogFileHash ?? string.Empty;
                if (!recovering) group.CurrentRevision = 1;
                TouchWorkspace();
                checkpoint = new PublishCurrentChangesCheckpoint(this, before);
                message = "已暂存行为卡与行为组 Published 来源 checkpoint。";
                return true;
            }
            catch (Exception exception)
            {
                RestoreState(before);
                message = "草稿 Published checkpoint 更新失败：" + exception.Message;
                return false;
            }
        }
    }
}
