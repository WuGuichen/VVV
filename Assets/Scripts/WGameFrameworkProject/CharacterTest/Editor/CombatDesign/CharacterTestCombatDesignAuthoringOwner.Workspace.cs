using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.CharacterAction;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static partial class CharacterTestCombatDesignAuthoringOwner
    {
        private sealed partial class AuthoringContext : IDisposable
        {
            private readonly string _scope, _fixture, _workspaceDirectory, _registryPath, _catalogDirectory;
            private readonly CharacterTestRuntimeAiLabDraftPersistence _persistence;
            private readonly CharacterTestRuntimeAiLabDraftWorkspace _workspace;
            private readonly CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog _references;
            private readonly CharacterTestRuntimeAiPlannerProjectCatalogState _catalog;
            private readonly bool _canPublish;
            private readonly string _role;

            internal AuthoringContext(string scope)
            {
                _scope = scope;
                string workspaceId;
                if (scope.StartsWith("fixture:", StringComparison.Ordinal))
                {
                    string handle = scope.Substring(8); Need(Token(handle), "INVALID_FIXTURE_HANDLE");
                    _fixture = Path.Combine(ProjectRoot, "Temp/MxFramework/CombatDesign/fixtures", handle.Substring(0, 12));
                    var manifest = Read(Path.Combine(_fixture, "fixture.json"));
                    Need((string)manifest["handle"] == handle, "FIXTURE_IDENTITY_MISMATCH");
                    workspaceId = (string)manifest["workspaceId"];
                    _registryPath = Path.Combine(_fixture, "fixture.json");
                    _persistence = new CharacterTestRuntimeAiLabDraftPersistence(Path.Combine(_fixture, "Drafts"), Path.Combine(_fixture, "DraftRecovery"));
                    _catalogDirectory = Path.Combine(_fixture, "StreamingAssets", CharacterTestRuntimeAiPlannerProjectCatalogLoader.RelativeDirectory);
                    _role = "隔离验收工作区"; _canPublish = true;
                }
                else
                {
                    Need(scope == "main" || scope == "experimentA" || scope == "experimentB", "WORKSPACE_SCOPE_INVALID");
                    _registryPath = Path.Combine(ProjectRoot, "Authoring/RuntimeAiLab/Drafts/workspace-roles.json");
                    Safe(_registryPath);
                    var registry = new CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistry(_registryPath,
                        Path.Combine(ProjectRoot, "UserSettings/RuntimeAiLab/active-workspace-role.txt"));
                    Need(registry.Load(out var roles, out string issue) == CharacterTestRuntimeAiLabAuthoringWorkspaceRoleRegistryLoadStatus.Loaded, issue);
                    var entry = scope == "main" ? roles.Main : scope == "experimentA" ? roles.ExperimentA : roles.ExperimentB;
                    Need(entry != null, "WORKSPACE_NOT_REGISTERED: 该工作区尚未建立；请在现有工作区管理入口建立。");
                    workspaceId = entry.WorkspaceId;
                    _persistence = new CharacterTestRuntimeAiLabDraftPersistence();
                    _catalogDirectory = Path.Combine(Application.streamingAssetsPath, CharacterTestRuntimeAiPlannerProjectCatalogLoader.RelativeDirectory);
                    _role = scope == "main" ? "Main Workspace · 正式作者源" : scope + " · 隔离实验作者源";
                    _canPublish = scope == "main";
                }
                Need(Guid.TryParse(workspaceId, out _), "WORKSPACE_ID_INVALID");
                _workspaceDirectory = Path.Combine(_persistence.DraftsRoot, workspaceId);
                Fingerprint(_workspaceDirectory); // Validate every path before the persistence owner resolves references.
                Need(_persistence.TryLoad(workspaceId, out _workspace, out string loadIssue), loadIssue);
                Need(!_workspace.IsReadOnly && !_workspace.RecoveredFromPrevious, "WORKSPACE_RECOVERY_REQUIRED: 当前源需要在原工作区恢复，网页未覆盖它。");
                _persistence.SetLatestWorkspacePointerUpdateEnabled(false);
                _references = new CharacterTestRuntimeAiLabBehaviorCardReferenceCatalog(null, CharacterTestCharacterActionConfigSource.Load().Registry);
                _catalog = LoadCatalog(); Need(_catalog.Success, _catalog.Diagnostic);
            }

            public void Dispose() { }
            private CharacterTestRuntimeAiPlannerProjectCatalogState LoadCatalog()
            {
                if (_fixture == null) return CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load(_references);
                string cardsPath = Path.Combine(_catalogDirectory, CharacterTestRuntimeAiPlannerProjectCatalogLoader.CardCatalogFileName);
                string groupsPath = Path.Combine(_catalogDirectory, CharacterTestRuntimeAiPlannerProjectCatalogLoader.GroupCatalogFileName);
                Safe(cardsPath); Safe(groupsPath);
                var cards = CharacterTestRuntimeAiPlannerProjectCardStorage.Load(File.ReadAllText(cardsPath), _catalogDirectory, _references);
                Need(cards.Success, string.Join("; ", cards.Diagnostics.Select(d => d.Message)));
                var groups = CharacterTestRuntimeAiPlannerBehaviorGroupCatalogJson.Load(File.ReadAllText(groupsPath), cards.Catalog, CharacterTestRuntimeAiPlannerBehaviorDefinitionCatalog.CreateBuiltIn());
                Need(groups.Success, groups.Diagnostic);
                return new CharacterTestRuntimeAiPlannerProjectCatalogState(cards.Catalog, groups.Catalog, cardsPath, groupsPath,
                    CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(cardsPath), CharacterTestRuntimeAiPlannerProjectCatalogLoader.ComputeFileHash(groupsPath), string.Empty);
            }

            private static string Fingerprint(string directory)
            {
                Safe(directory); if (!Directory.Exists(directory)) return "missing";
                var files = new List<string>();
                void Scan(string current)
                {
                    Safe(current);
                    foreach (string file in Directory.GetFiles(current).OrderBy(f => f, StringComparer.Ordinal))
                    { Safe(file); files.Add(file.Substring(directory.Length) + ":" + Hash(File.ReadAllText(file))); }
                    foreach (string sub in Directory.GetDirectories(current).OrderBy(d => d, StringComparer.Ordinal)) Scan(sub);
                }
                Scan(directory); return Hash(string.Join("\n", files));
            }
            private string WorkspaceToken() => Hash(Fingerprint(_workspaceDirectory) + "|" + File.ReadAllText(_registryPath));
            private string SnapshotToken() => Hash(WorkspaceToken() + "|" + Fingerprint(_catalogDirectory) + "|"
                + typeof(CharacterTestTacticalDslCompiler).Module.ModuleVersionId + "|" + typeof(CharacterTestCombatDesignAuthoringOwner).Module.ModuleVersionId);
            private void Verify(JObject request) => Need((string)request["snapshotToken"] == SnapshotToken(), "SOURCE_CONFLICT: 作者源、共享依赖或发布目录已变化。保留本地编辑并重新加载。");
            private CharacterTestRuntimeAiLabBehaviorCardDraft Card(JObject request)
            {
                Need(_workspace.Cards.TryGetValue((string)request["cardId"] ?? "", out var card) && card.HasAuthoredSource, "CARD_NOT_FOUND");
                return card;
            }

            internal JObject Library()
            {
                var rows = CharacterTestRuntimeAiPlannerDevSceneController.BuildTacticalLibraryMetadata(_workspace, _catalog.CardCatalog);
                return new JObject { ["scope"] = _scope, ["role"] = _role, ["canPublish"] = _canPublish,
                    ["workspaceId"] = _workspace.WorkspaceId, ["workspaceRevision"] = _workspace.CurrentRevision,
                    ["snapshotToken"] = SnapshotToken(), ["catalogRevision"] = _catalog.CardCatalog.Revision,
                    ["catalogHash"] = _catalog.CardCatalog.CatalogHash.ToString(),
                    ["cards"] = new JArray(rows.Select(r => new JObject { ["key"] = r.Key, ["name"] = r.Name, ["description"] = r.Description, ["revision"] = r.Revision, ["draft"] = r.Draft })),
                    ["characters"] = new JArray(CharacterTestPlayableCharacterCatalog.Discover().Select(c => new JObject { ["id"] = c.CharacterId, ["name"] = c.DisplayName })),
                    ["actions"] = new JArray(CharacterTestCharacterActionConfigSource.Load().Registry.Actions.Select(a => new JObject { ["id"] = a.ActionId.Value, ["name"] = a.DisplayName })),
                    ["message"] = _fixture != null ? "隔离目录，仅用于验收。" : _canPublish ? "Main 作者工作区；发布不会修改行为组或授予玩家卡。" : "实验工作区可保存，正式发布前需在原入口应用到 Main。" };
            }

            private JObject Detail(CharacterTestRuntimeAiLabBehaviorCardDraft card)
            {
                var source = card.HasCompositionSource ? JObject.Parse(card.CompositionSourceJson) : JObject.Parse(card.TacticalSourceJson);
                return new JObject { ["scope"] = _scope, ["role"] = _role, ["canPublish"] = _canPublish,
                    ["workspaceId"] = _workspace.WorkspaceId, ["workspaceRevision"] = _workspace.CurrentRevision,
                    ["snapshotToken"] = SnapshotToken(), ["cardId"] = card.CardDraftId, ["stableId"] = card.PublishedStableId,
                    ["name"] = card.DisplayName, ["cost"] = card.Cost, ["cooldown"] = card.CooldownSeconds,
                    ["revision"] = card.CurrentRevision, ["publishedRevision"] = card.SourcePublishedRevision, ["source"] = source,
                    ["sourcePath"] = Path.Combine(_workspaceDirectory, "cards", card.CardDraftId + ".json").Substring(ProjectRoot.Length + 1).Replace('\\', '/'),
                    ["pendingPublication"] = File.Exists(JournalPath(card.CardDraftId)) ? Read(JournalPath(card.CardDraftId)) : null,
                    ["slots"] = new JArray(Slots(card)), ["shared"] = new JArray(_workspace.CloneTacticalDefinitions().Select(d => new JObject {
                        ["id"] = d.DefinitionId, ["revision"] = d.Revision, ["name"] = d.Source.Name,
                        ["hash"] = Hash(CharacterTestTacticalCompositionJson.WriteShared(d)), ["source"] = JObject.Parse(CharacterTestTacticalAuthoringJson.Write(d.Source)) })) };
            }

            internal JObject Open(JObject request)
            {
                Verify(request);
                string key = (string)request["key"];
                if (key?.StartsWith("draft:", StringComparison.Ordinal) == true)
                { request["cardId"] = key.Substring(6); return Detail(Card(request)); }
                Need(key?.StartsWith("published:", StringComparison.Ordinal) == true && (bool?)request["createDraft"] == true, "CREATE_DRAFT_CONFIRMATION_REQUIRED");
                Need(_catalog.CardCatalog.TryResolveLatestCompatible(key.Substring(10), CharacterTestRuntimeAiPlannerPublishedBehaviorCardPublisher.CurrentRuntimeContractVersion, out var published), "PUBLISHED_CARD_NOT_FOUND");
                Need(_workspace.TryOpenStandaloneTacticalCard(published, out var draft, out string issue), issue);
                Verify(request); Persist(); return Detail(draft);
            }

            internal JObject Save(JObject request)
            {
                Verify(request); var card = Card(request);
                Need(!File.Exists(JournalPath(card.CardDraftId)), "RECOVERY_REQUIRED: 请先恢复此前发布的草稿检查点。");
                Need(request["source"] is JObject && request["cost"]?.Type == JTokenType.Integer && request["cooldown"] != null, "EDIT_PAYLOAD_INVALID");
                Need(_workspace.TryEditStandaloneTacticalSource(new CharacterTestTacticalEditToken(_workspace, card), request["source"].ToString(Formatting.None),
                    (string)request["name"], (int)request["cost"], (float)request["cooldown"], out string issue), issue);
                Verify(request); Persist(); var detail = Detail(card); detail["message"] = "作者源已保存。尚未发布。"; return detail;
            }
            private void Persist()
            {
                _persistence.AdoptWorkspace(_workspace); _workspace.LastExplicitSaveRevision = _workspace.CurrentRevision;
                Need(_persistence.Flush(), _persistence.LastStatus);
            }
            private string[] Slots(CharacterTestRuntimeAiLabBehaviorCardDraft card)
            {
                if (card.HasTacticalSource) return card.CloneTacticalSource().Slots.Where(s => !s.IsFunctional).Select(s => s.SlotId).ToArray();
                var document = _workspace.ResolveComposition(card, referencedOnly: true);
                return CharacterTestTacticalCompositionEdits.Nodes(document.Root).Where(n => n.Kind == "Reference")
                    .SelectMany(n => document.Definitions.First(d => d.DefinitionId == n.DefinitionId).Source.Slots.Where(s => !s.IsFunctional).Select(s => n.NodeId + "/" + s.SlotId)).Distinct().ToArray();
            }
        }
    }
}
