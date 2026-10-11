using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession
    {
        private string _publishRecoveryJournalPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
            "../UserSettings/RuntimeAiLab/PublishRecovery/current.json"));
        private bool _hasPublishRecoveryJournal;
        private JObject _publishRecoveryJournal;
        private CharacterTestRuntimeAiPlannerProjectCatalogState _recoverySourceState;
        internal bool HasPendingPublishRecovery => _hasPublishRecoveryJournal || _recoveryPublishCurrentChangesPlan != null;
        internal void SetPublishRecoveryJournalForTests(string path)
        {
            _publishRecoveryJournalPath = path; _publishRecoveryJournal = null;
            _hasPublishRecoveryJournal = path != null && File.Exists(path);
        }

        internal CharacterTestRuntimeAiLabPublishCurrentChangesPreview GetPublishRecoveryPreview()
        {
            if (_recoveryPublishCurrentChangesPlan != null) return _recoveryPublishCurrentChangesPlan.Preview;
            if (_hasPublishRecoveryJournal)
            {
                try { return ReadRecoveryPreview(ReadPublishRecoveryJournal()); }
                catch (Exception error) when (error is IOException || error is JsonException || error is ArgumentException || error is InvalidOperationException)
                { return null; }
            }
            return _lastCommittedPublishCurrentChangesResult?.Preview;
        }

        private bool TryWritePublishRecoveryJournal(PublishCurrentChangesPlan plan, out string issue)
        {
            issue = string.Empty;
            if (_publishRecoveryJournalPath == null) return true;
            try
            {
                var preview = plan.Preview;
                var document = JObject.FromObject(new {
                    schema = "mx.character-test.publish-recovery.v1",
                    preview = new { preview.Token, preview.WorkspaceId, preview.WorkspaceRevision, preview.GroupDraftId,
                        preview.GroupDraftRevision, preview.StableGroupId, preview.GroupDisplayName, preview.SourceGroupRevision,
                        preview.TargetGroupRevision, preview.DirectCardCount, preview.SourceIdentityHash, preview.CandidateIdentityHash,
                        cards = preview.Cards.Select(c => new { c.CardDraftId, c.StableCardId, c.DisplayName, c.ExpectedDraftRevision,
                            c.SourceRevision, c.TargetRevision, c.EventCount, c.AffectedOtherGroupCount, c.SharedDependencies }).ToArray() },
                    published = plan.PublishedCards.Select(c => new { c.CardDraftId, c.ExpectedDraftRevision, c.ExpectedSourceRevision,
                        c.ExpectedSourceSemanticHash, c.ExpectedSourceContentHash, c.Card.StableCardId, c.Card.Revision }).ToArray(),
                    cardCatalog = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(plan.CardCatalog),
                    groupCatalog = plan.GroupCatalogJson,
                    beforeCards = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.ToJson(_state.CardCatalog),
                    beforeGroups = CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(_state.GroupCatalog),
                    _state.CardCatalogPath, _state.GroupCatalogPath, _state.CardFileHash, _state.GroupFileHash });
                Directory.CreateDirectory(Path.GetDirectoryName(_publishRecoveryJournalPath));
                string temporary = _publishRecoveryJournalPath + ".tmp";
                byte[] bytes = Encoding.UTF8.GetBytes(document.ToString(Formatting.Indented));
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(_publishRecoveryJournalPath)) File.Replace(temporary, _publishRecoveryJournalPath, null);
                else File.Move(temporary, _publishRecoveryJournalPath);
                _publishRecoveryJournal = document; _hasPublishRecoveryJournal = true; _recoverySourceState = _state;
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { issue = error.Message; return false; }
        }

        private JObject ReadPublishRecoveryJournal()
        {
            if (_publishRecoveryJournal != null) return _publishRecoveryJournal;
            var info = new FileInfo(_publishRecoveryJournalPath);
            if (info.Length > 32 * 1024 * 1024) throw new JsonSerializationException("Publish recovery record is oversized.");
            var json = JObject.Parse(File.ReadAllText(_publishRecoveryJournalPath), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if ((string)json["schema"] != "mx.character-test.publish-recovery.v1"
                || !string.Equals(Path.GetFullPath((string)json["CardCatalogPath"]), Path.GetFullPath(_state.CardCatalogPath), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetFullPath((string)json["GroupCatalogPath"]), Path.GetFullPath(_state.GroupCatalogPath), StringComparison.OrdinalIgnoreCase))
                throw new JsonSerializationException("Publish recovery record does not belong to the current catalog paths.");
            _publishRecoveryJournal = json; return json;
        }

        private static CharacterTestRuntimeAiLabPublishCurrentChangesPreview ReadRecoveryPreview(JObject document)
        {
            var v = document["preview"];
            var cards = ((JArray)v["cards"]).Select(c => new CharacterTestRuntimeAiLabPublishCurrentChangesCardPreview(
                (string)c["CardDraftId"], (string)c["StableCardId"], (string)c["DisplayName"], (int)c["ExpectedDraftRevision"],
                (int)c["SourceRevision"], (int)c["TargetRevision"], (int)c["EventCount"], (int)c["AffectedOtherGroupCount"],
                (string)c["SharedDependencies"])).ToArray();
            return new CharacterTestRuntimeAiLabPublishCurrentChangesPreview((string)v["Token"], (string)v["WorkspaceId"],
                (int)v["WorkspaceRevision"], (string)v["GroupDraftId"], (int)v["GroupDraftRevision"], (string)v["StableGroupId"],
                (string)v["GroupDisplayName"], (int)v["SourceGroupRevision"], (int)v["TargetGroupRevision"], (int)v["DirectCardCount"],
                cards, (long)v["SourceIdentityHash"], (long)v["CandidateIdentityHash"]);
        }

        private bool TryRestorePublishRecoveryPlan(CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog references, out string issue)
        {
            issue = string.Empty;
            if (_recoveryPublishCurrentChangesPlan != null || !_hasPublishRecoveryJournal) return true;
            try
            {
                var json = ReadPublishRecoveryJournal();
                var cards = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load((string)json["cardCatalog"], references);
                var beforeCards = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalogJson.Load((string)json["beforeCards"], references);
                if (!cards.Success || !beforeCards.Success) throw new InvalidOperationException("恢复记录的固定卡目录校验失败。");
                var definitions = CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn();
                var groups = CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.Load((string)json["groupCatalog"], cards.Catalog, definitions);
                var beforeGroups = CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.Load((string)json["beforeGroups"], beforeCards.Catalog, definitions);
                if (!groups.Success || !beforeGroups.Success) throw new InvalidOperationException("恢复记录的行为组目录校验失败。");
                var preview = ReadRecoveryPreview(json);
                if (!groups.Catalog.TryGetExact(preview.StableGroupId, preview.TargetGroupRevision, out var group))
                    throw new InvalidOperationException("恢复记录缺少预期行为组 revision。");
                var published = ((JArray)json["published"]).Select(p => {
                    if (!cards.Catalog.TryGetExact((string)p["StableCardId"], (int)p["Revision"], out var card))
                        throw new InvalidOperationException("恢复记录缺少预期行为卡 revision。");
                    return new CharacterTestRuntimeAiLabPublishCurrentChangesPublishedCard((string)p["CardDraftId"], (int)p["ExpectedDraftRevision"],
                        (int)p["ExpectedSourceRevision"], (long)p["ExpectedSourceSemanticHash"], (long)p["ExpectedSourceContentHash"], card);
                }).ToArray();
                _recoverySourceState = new CharacterTestRuntimeAiPlannerProjectCatalogState(beforeCards.Catalog, beforeGroups.Catalog,
                    (string)json["CardCatalogPath"], (string)json["GroupCatalogPath"], (string)json["CardFileHash"], (string)json["GroupFileHash"], string.Empty);
                _recoveryPublishCurrentChangesPlan = new PublishCurrentChangesPlan { Preview = preview, CardCatalog = cards.Catalog,
                    GroupCatalog = groups.Catalog, GroupCatalogJson = (string)json["groupCatalog"], PublishedCards = published, PublishedGroup = group };
                return true;
            }
            catch (Exception error) when (error is IOException || error is JsonException || error is ArgumentException || error is InvalidOperationException)
            { issue = error.Message; return false; }
        }

        internal bool CompletePublishRecoveryJournal(out string issue)
        {
            issue = string.Empty;
            try
            {
                if (_publishRecoveryJournalPath != null && File.Exists(_publishRecoveryJournalPath)) File.Delete(_publishRecoveryJournalPath);
                _hasPublishRecoveryJournal = false; _publishRecoveryJournal = null; _recoverySourceState = null;
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { issue = error.Message; return false; }
        }
    }
}
