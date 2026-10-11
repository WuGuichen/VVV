using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Explicit one-time rebuild requested by the author. No initialization or automatic migration hook.</summary>
    internal static class CharacterTestTacticalVisualContentRebuild
    {
        [MenuItem("MxFramework/CharacterTest/Tactical Content/Add Visible Approach Completion Events")]
        internal static void AddVisibleApproachCompletionEventsMenu() => Debug.Log(AddVisibleApproachCompletionEvents());

        internal static string AddVisibleApproachCompletionEvents()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出播放，避免旧会话回写。");
            var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, CharacterTestCharacterActionConfigSource.Load().Registry);
            var state = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load(references);
            if (!state.Success) throw new InvalidOperationException(state.Diagnostic);
            string backup = Path.GetFullPath(Path.Combine("Library", "ApproachCompletionUpgrade", DateTime.Now.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(backup);
            Backup(Path.GetDirectoryName(state.CardCatalogPath), Path.Combine(backup, "published"));
            Backup("Authoring/RuntimeAiLab/Drafts", Path.Combine(backup, "drafts"));
            Backup("Authoring/RuntimeAiLab/TestCards", Path.Combine(backup, "test-cards"));
            Backup("UserSettings/RuntimeAiLab/TestCards", Path.Combine(backup, "test-card-recovery"));
            var cards = state.CardCatalog.Cards.ToList();
            int published = 0, drafts = 0, tests = 0, events = 0;
            foreach (var previous in state.CardCatalog.Cards.Where(c => c.TacticalSource != null).GroupBy(c => c.StableCardId).Select(g => g.OrderByDescending(c => c.Revision).First()))
            {
                var source = CharacterTestTacticalApproachActionSplit.UpgradePublished(previous.TacticalSource, out int count);
                if (count == 0) continue;
                int revision = checked(previous.Revision + 1);
                long hash = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(previous.StableCardId, revision,
                    previous.MinimumRuntimeContractVersion, previous.MaximumRuntimeContractVersion, previous.Metadata, source.SemanticHash);
                cards.Add(new CharacterTestRuntimeAiPlannerPublishedBehaviorCard(previous.StableCardId, revision, previous.MinimumRuntimeContractVersion,
                    previous.MaximumRuntimeContractVersion, previous.Metadata, null, source.SemanticHash, hash, tacticalSource: source, numericId: previous.NumericId));
                published++; events += count;
            }
            if (published > 0)
            {
                var created = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(state.CardCatalog.CatalogId,
                    checked(state.CardCatalog.Revision + 1), cards, references, state.CardCatalog.CatalogNumericId);
                if (!created.Success) throw new InvalidOperationException(string.Join(";", created.Diagnostics.Select(d => d.Message)));
                var write = CharacterTestRuntimeAiPlannerProjectCatalogWriteBridge.Write(new CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest(
                    state.CardCatalogPath, state.GroupCatalogPath, state.CardFileHash, state.GroupFileHash, state.CardCatalog.CatalogHash,
                    created.Catalog, CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(state.GroupCatalog)));
                if (!write.Success) throw new InvalidOperationException(write.Diagnostic);
            }
            var persistence = new CharacterTestRuntimeAiLabDraftPersistence(); persistence.SetLatestWorkspacePointerUpdateEnabled(false);
            var unreadable = new List<string>();
            foreach (string directory in Directory.GetDirectories(persistence.DraftsRoot))
            {
                string id = Path.GetFileName(directory); if (!Guid.TryParse(id, out _)) continue;
                if (!persistence.TryLoad(id, out var workspace, out string issue)) { unreadable.Add(id + ": " + issue); continue; }
                int count = workspace.AddVisibleApproachCompletionEvents();
                if (count == 0) continue;
                persistence.AdoptWorkspace(workspace);
                if (!persistence.Flush()) throw new InvalidOperationException(persistence.LastStatus);
                drafts++; events += count;
            }
            var library = new CharacterTestConfiguredCardLibrary();
            var owners = new List<string> { "" };
            const string characterRoot = "Authoring/RuntimeAiLab/TestCards/Characters";
            if (Directory.Exists(characterRoot)) owners.AddRange(Directory.GetDirectories(characterRoot).Select(Path.GetFileName));
            var testCards = owners.SelectMany(owner => library.List(owner)).GroupBy(c => c.Id).Select(g => g.First()).ToArray();
            foreach (var card in testCards)
            {
                var source = CharacterTestTacticalApproachActionSplit.UpgradePublished(CharacterTestTacticalPublishedSource.Read(card.Source), out int count);
                if (count == 0) continue;
                card.Source = source.ToJson();
                if (!library.Save(card, card.Revision, card.FileHash, out string issue)) throw new InvalidOperationException(issue);
                tests++; events += count;
            }
            int recoveries = 0;
            const string recoveryRoot = "UserSettings/RuntimeAiLab/TestCards";
            if (Directory.Exists(recoveryRoot)) foreach (string folder in Directory.GetDirectories(recoveryRoot))
            {
                string owner = Path.GetFileName(folder);
                foreach (string path in Directory.GetFiles(folder, "*.json"))
                {
                    var recovery = library.ReadRecovery(owner, Path.GetFileNameWithoutExtension(path));
                    var source = CharacterTestTacticalApproachActionSplit.UpgradePublished(CharacterTestTacticalPublishedSource.Read(recovery.Card.Source), out int count);
                    if (count == 0) continue;
                    var saved = library.List(owner).FirstOrDefault(c => c.Id == recovery.Card.Id);
                    if (saved != null && recovery.ExpectedRevision != saved.Revision)
                    {
                        bool proven = false;
                        foreach (string batch in Directory.GetDirectories(Path.Combine("Library", "ApproachCompletionUpgrade")))
                        {
                            string relative = saved.Shared ? Path.Combine("Shared", saved.Id + ".json")
                                : Path.Combine("Characters", saved.OwnerCharacter, saved.Id + ".json");
                            string originalPath = Path.Combine(batch, "test-cards", relative);
                            if (!File.Exists(originalPath)) continue;
                            string text = File.ReadAllText(originalPath);
                            if (CharacterTestActionBrowserCatalog.Hash(System.Text.Encoding.UTF8.GetBytes(text)) != recovery.ExpectedHash) continue;
                            var original = JsonConvert.DeserializeObject<CharacterTestConfiguredCard>(text);
                            var upgraded = CharacterTestTacticalApproachActionSplit.UpgradePublished(CharacterTestTacticalPublishedSource.Read(original.Source), out _);
                            original.Source = upgraded.ToJson(); original.Revision = saved.Revision; original.FileHash = saved.FileHash; original.Name = original.Name.Trim();
                            if (!JToken.DeepEquals(JToken.FromObject(original), JToken.FromObject(saved))) continue;
                            proven = true; break;
                        }
                        if (!proven) unreadable.Add("Test-card recovery retains its existing base conflict: " + path);
                        else { recovery.ExpectedRevision = saved.Revision; recovery.ExpectedHash = saved.FileHash; }
                    }
                    recovery.Card.Source = source.ToJson();
                    library.Recover(owner, recovery.Card, recovery.ExpectedRevision, recovery.ExpectedHash);
                    recoveries++; events += count;
                }
            }
            AssetDatabase.Refresh();
            return $"Visible approach completion: published cards {published}, draft workspaces {drafts}, test cards {tests}, events {events}. Backup: {backup}"
                + $"\nUpgraded test-card recoveries: {recoveries}"
                + (unreadable.Count == 0 ? "" : "\nUnchanged unreadable workspaces: " + string.Join("\n", unreadable));
        }

        internal static string Execute()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出播放，避免旧会话回写。");
            var references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, CharacterTestCharacterActionConfigSource.Load().Registry);
            var state = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load(references);
            if (!state.Success) throw new InvalidOperationException(state.Diagnostic);
            string backup = Path.GetFullPath(Path.Combine("Library", "TacticalVisualRebuild", DateTime.Now.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(backup);
            Backup(Path.GetDirectoryName(state.CardCatalogPath), Path.Combine(backup, "published"));
            Backup("Authoring/RuntimeAiLab/Drafts", Path.Combine(backup, "drafts"));
            Backup("UserSettings/RuntimeAiLab/DraftRecovery", Path.Combine(backup, "recovery"));
            var cards = state.CardCatalog.Cards.Where(c => c.TacticalSource == null).ToList();
            int removed = state.CardCatalog.Cards.Count - cards.Count, published = 0, drafts = 0;
            foreach (var previous in state.CardCatalog.Cards.Where(c => c.TacticalSource != null).GroupBy(c => c.StableCardId).Select(g => g.OrderByDescending(c => c.Revision).First()))
            {
                var source = CharacterTestTacticalAuthoringTemplates.CreateVisualCard(previous.Metadata.DisplayName);
                // Keep the public functional slot key used by character default loadouts.
                string oldSlot = previous.TacticalSource.IsLegacy ? previous.TacticalSource.CloneLegacy().Slots.FirstOrDefault()?.SlotId : null;
                if (oldSlot != null && source.Slots.Count == 1 && oldSlot != source.Slots[0].SlotId)
                {
                    string from = source.Slots[0].SlotId; source.Slots[0].SlotId = oldSlot;
                    foreach (var ev in source.Timelines.SelectMany(t => t.Events))
                    { if (ev.Operation.SlotId == from) ev.Operation.SlotId = oldSlot; if (ev.Operation.Movement?.ActionSlotId == from) ev.Operation.Movement.ActionSlotId = oldSlot; }
                }
                var issues = CharacterTestTacticalAuthoringCompiler.ValidateShared(source);
                if (issues.Count != 0) throw new InvalidOperationException(string.Join(";", issues));
                var snapshot = CharacterTestTacticalPublishedSource.Read(new JObject { ["schema"] = CharacterTestTacticalPublishedSource.Schema,
                    ["legacy"] = JObject.Parse(CharacterTestTacticalAuthoringJson.Write(source)) });
                var descriptionSource = source.Clone(); var activation = descriptionSource.ActivationConditionRoot; descriptionSource.ActivationConditionRoot = null;
                var root = CharacterTestTacticalCompositionEdits.NewNode("Sequence"); root.Children.Add(CharacterTestTacticalCompositionEdits.NewNode("Reference", "description"));
                var description = CharacterTestTacticalDslDescription.Generate(new CharacterTestTacticalDslSource {
                    CardId = previous.StableCardId, Name = previous.Metadata.DisplayName, Root = root, ActivationConditionRoot = activation,
                    Definitions = new List<CharacterTestTacticalSharedDefinition> { new CharacterTestTacticalSharedDefinition { DefinitionId = "description", Source = descriptionSource } } }, null);
                var metadata = new CharacterTestRuntimeAiPlannerPublishedBehaviorCardMetadata(previous.Metadata.DisplayName, previous.Metadata.IconKey,
                    previous.Metadata.Cost, description.Text, description.Details, string.Join("; ", description.Warnings), previous.Metadata.Tags,
                    previous.Metadata.RequiredCapabilities, previous.Metadata.Responsibilities);
                long hash = CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.ComputeContentHash(previous.StableCardId, 1,
                    previous.MinimumRuntimeContractVersion, previous.MaximumRuntimeContractVersion, metadata, snapshot.SemanticHash);
                cards.Add(new CharacterTestRuntimeAiPlannerPublishedBehaviorCard(previous.StableCardId, 1, previous.MinimumRuntimeContractVersion,
                    previous.MaximumRuntimeContractVersion, metadata, null, snapshot.SemanticHash, hash, tacticalSource: snapshot, numericId: previous.NumericId));
                published++;
            }
            // Existing formal groups currently contain no tactical-card pins. Refuse to write stale pins if that changes.
            var changedIds = new HashSet<string>(cards.Where(c => c.TacticalSource != null).Select(c => c.StableCardId));
            if (state.GroupCatalog.Groups.Any(g => g.Cards.Any(c => changedIds.Contains(c.StableCardId))))
                throw new InvalidOperationException("正式行为组出现战术卡固定引用，需先重建这些引用；目录未写入。");
            var created = CharacterTestRuntimeAiPlannerPublishedBehaviorCardCatalog.ValidateAndCreate(state.CardCatalog.CatalogId,
                state.CardCatalog.Revision + 1, cards, references, state.CardCatalog.CatalogNumericId);
            if (!created.Success) throw new InvalidOperationException(string.Join(";", created.Diagnostics.Select(d => d.Message)));
            var write = CharacterTestRuntimeAiPlannerProjectCatalogWriteBridge.Write(new CharacterTestRuntimeAiPlannerProjectCatalogWriteRequest(
                state.CardCatalogPath, state.GroupCatalogPath, state.CardFileHash, state.GroupFileHash, state.CardCatalog.CatalogHash,
                created.Catalog, CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.ToJson(state.GroupCatalog)));
            if (!write.Success) throw new InvalidOperationException(write.Diagnostic);
            var persistence = new CharacterTestRuntimeAiLabDraftPersistence(); persistence.SetLatestWorkspacePointerUpdateEnabled(false);
            foreach (string directory in Directory.GetDirectories(persistence.DraftsRoot))
            {
                string id = Path.GetFileName(directory); if (!Guid.TryParse(id, out _)) continue;
                string cardDirectory = Path.Combine(directory, "cards");
                if (!Directory.Exists(cardDirectory) || !Directory.GetFiles(cardDirectory, "*.json").Any(path =>
                    ((string)JObject.Parse(File.ReadAllText(path))["payloadSchema"])?.Contains("tactical") == true)) continue;
                if (!persistence.TryLoad(id, out var workspace, out string error)) throw new InvalidOperationException(error);
                int count = workspace.RebuildTacticalDrafts(created.Catalog); if (count == 0) continue;
                persistence.ActivateLoadedWorkspace(workspace, "重建战术卡");
                if (!persistence.Flush()) throw new InvalidOperationException(persistence.LastStatus);
                drafts += count;
            }
            AssetDatabase.Refresh();
            string report = JsonConvert.SerializeObject(new { removedPublishedVersions = removed, rebuiltPublishedCards = published,
                rebuiltDraftCards = drafts, backup, completedUtc = DateTime.UtcNow }, Formatting.Indented);
            File.WriteAllText(Path.Combine(backup, "receipt.json"), report);
            return report;
        }
        private static void Backup(string source, string target)
        {
            if (!Directory.Exists(source)) return;
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), false);
            foreach (string directory in Directory.GetDirectories(source)) Backup(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }
}
