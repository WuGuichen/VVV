using System;
using System.IO;
using System.Linq;
using MxFramework.Config.Levels;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal static class CharacterTestLevelAuthoringOperations
    {
        internal static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        internal static string DirectoryPath => Path.Combine(ProjectRoot, CharacterTestLevelCatalog.AuthoringRelativePath);
        internal static string RuntimeRoot => Path.Combine(Application.streamingAssetsPath, "MxFramework/CharacterTest/Levels");
        internal static CharacterTestLevelCatalog Catalog => CharacterTestLevelCatalog.Read(DirectoryPath);
        internal static GameManager Manager => UnityEngine.Object.FindFirstObjectByType<GameManager>();

        internal static JObject Library()
        {
            var catalog = Catalog;
            var groups = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            if (!groups.Success) throw new InvalidOperationException(groups.Diagnostic);
            return new JObject { ["levels"] = JArray.FromObject(catalog.Levels), ["defaultLevelId"] = catalog.DefaultLevelId,
                ["groups"] = JArray.FromObject(groups.GroupCatalog.Groups.Select(g => new { groupId = g.StableGroupId, revision = g.Revision, contentHash = g.ContentHash.ToString(), displayName = g.DisplayName,
                    cards = g.Cards.Select(c => { groups.CardCatalog.TryGetExact(c.StableCardId, c.CardRevision, out var card);
                        return new { enabled = c.Enabled, entryId = c.EntryId, cardId = c.StableCardId, revision = c.CardRevision, semanticHash = c.SemanticHash.ToString(), contentHash = c.ContentHash.ToString(), slots = CharacterTestLevelBehaviorCompiler.Slots(card.TacticalSource) }; }) })),
                ["actions"] = JArray.FromObject(CharacterTestActionBrowserCatalog.Get().Actions.Select(a => new { a.ActionId, a.Name })) };
        }

        internal static JObject Open(string levelId)
        {
            var document = Catalog.Load(levelId, ProjectRoot, false, out var path);
            string source = File.ReadAllText(path);
            return new JObject { ["levelId"] = levelId, ["sourceHash"] = CharacterTestLevelCatalog.Hash(source),
                ["document"] = WebNumbers(JObject.Parse(MxLevelAuthoringJson.SaveToJson(document))) };
        }

        private static JToken WebNumbers(JToken token)
        {
            if (token is JValue value && value.Type == JTokenType.Integer && (value.Value<long>() > 9007199254740991L || value.Value<long>() < -9007199254740991L)) return new JValue(value.Value<long>().ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (token is JContainer container) foreach (var child in container.Children().ToArray()) { if (child is JProperty prop) prop.Value = WebNumbers(prop.Value); else if (child is JValue) child.Replace(WebNumbers(child)); else WebNumbers(child); }
            return token;
        }

        private static MxLevelAuthoringDocument CheckRequest(JObject request, out string path)
        {
            string id = (string)request["levelId"] ?? throw new InvalidOperationException("levelId required");
            Catalog.Load(id, ProjectRoot, false, out path);
            if ((string)request["expectedSourceHash"] != CharacterTestLevelCatalog.Hash(File.ReadAllText(path)))
                throw new InvalidOperationException(id + "/stale source; reload before saving");
            var document = MxLevelAuthoringJson.LoadFromJson((request["document"] ?? throw new InvalidOperationException("document required")).ToString());
            if (CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(document, out var characters, out _))
                CharacterTestSceneCharacterAuthoringAdapter.UpsertCharactersModule(document, characters);
            if (document.LevelId != id) throw new InvalidOperationException(id + "/document identity mismatch");
            return document;
        }

        internal static T SelectSceneOwner<T>(string scenePath, System.Collections.Generic.IEnumerable<T> owners,
            Func<T, string> ownerScenePath) where T : class
        {
            if (string.IsNullOrWhiteSpace(scenePath)) throw new InvalidOperationException("Target Scene path is required for level preflight.");
            var matches = owners.Where(owner => owner != null && string.Equals(ownerScenePath(owner), scenePath, StringComparison.Ordinal)).ToArray();
            if (matches.Length == 0) throw new InvalidOperationException("预检和发布前，请在 Unity 打开目标 Scene：" + scenePath + "；其中需要 GameManager。保存关卡不需要切换 Scene。");
            if (matches.Length != 1) throw new InvalidOperationException("目标 Scene 中需要唯一的 GameManager：" + scenePath + "（找到 " + matches.Length + " 个）。");
            return matches[0];
        }

        private static GameManager ManagerForScene(string scenePath)
            => SelectSceneOwner(scenePath, UnityEngine.Object.FindObjectsByType<GameManager>(FindObjectsInactive.Include),
                manager => manager.gameObject.scene.path);

        internal sealed class PreflightProfileScope : IDisposable
        {
            private bool _ownsDefault;
            internal CharacterTestSceneProfile Profile { get; private set; }

            internal PreflightProfileScope(CharacterTestSceneProfile configuredProfile)
            {
                _ownsDefault = configuredProfile == null;
                Profile = CharacterTestSceneProfile.ResolveStartupProfile(configuredProfile);
            }

            public void Dispose()
            {
                if (_ownsDefault && Profile != null) UnityEngine.Object.DestroyImmediate(Profile);
                Profile = null;
                _ownsDefault = false;
            }
        }

        internal static JObject Save(JObject request)
        {
            var document = CheckRequest(request, out var path);
            var validation = MxLevelAuthoringValidator.Validate(document);
            if (validation.HasErrors) throw new InvalidOperationException("Invalid level envelope: " + validation);
            CharacterTestLevelCatalog.AtomicWrite(path, MxLevelAuthoringJson.SaveToJson(document));
            AssetDatabase.ImportAsset(Path.GetRelativePath(ProjectRoot, path).Replace('\\', '/'));
            return Open(document.LevelId);
        }

        internal static JObject Preflight(JObject request) => Preflight(request, out _);

        private static JObject Preflight(JObject request, out MxLevelAuthoringDocument frozenDocument)
        {
            var document = CheckRequest(request, out var path);
            var manager = ManagerForScene(document.ScenePath);
            using var profile = new PreflightProfileScope(manager.ConfiguredProfile);
            // A detached candidate consumes the target Scene configuration without changing its live session.
            var builder = new CharacterTestRuntimeSessionBuilder(manager, new CharacterTestRuntimeContext(manager, profile.Profile), profile.Profile);
            using var prepared = builder.PrepareLevelSession(document, path, publishPins: true, createViews: false);
            frozenDocument = prepared.Document;
            string json = MxLevelAuthoringJson.SaveToJson(prepared.Document);
            return new JObject { ["levelId"] = document.LevelId, ["sourceHash"] = request["expectedSourceHash"],
                ["document"] = WebNumbers(JObject.Parse(json)), ["previewToken"] = CharacterTestLevelCatalog.Hash(json),
                ["actorCount"] = prepared.Slice.Services.CharacterEntities.Count, ["message"] = "角色、动作、行为、碰撞、导航及触发器候选已验证。" };
        }

        internal static JObject Publish(JObject request)
        {
            var result = Preflight(request, out var document);
            if ((string)request["previewToken"] != (string)result["previewToken"])
                throw new InvalidOperationException("Preview changed; preflight again before publishing.");
            Catalog.Load(document.LevelId, ProjectRoot, false, out var sourcePath);
            string hash = CharacterTestLevelCatalog.PublishWithAuthoringSource(document,
                Catalog.Levels.Single(l => l.LevelId == document.LevelId).DisplayName, RuntimeRoot,
                sourcePath, (string)request["expectedSourceHash"]);
            // Runtime directory commit is the publication result. Asset refresh failures
            // are presentation diagnostics and must not turn a committed publish into
            // a reported source-save failure.
            var saved = new JObject { ["levelId"] = document.LevelId, ["sourceHash"] = hash,
                ["document"] = result["document"], ["publishedHash"] = hash };
            try { AssetDatabase.ImportAsset(Path.GetRelativePath(ProjectRoot, sourcePath).Replace('\\', '/')); AssetDatabase.Refresh(); }
            catch (Exception error) { saved["assetRefreshDiagnostic"] = error.Message; }
            return saved;
        }

        internal static JObject Execute(string operation, JObject request)
        {
            if (request?["scope"] != null && (string)request["scope"] != "main") throw new InvalidOperationException("Level operations require the explicit main level directory.");
            switch (operation) {
                case "levels.refresh": return Library();
                case "levels.open": return Open((string)request["levelId"]);
                case "levels.save": return Save(request);
                case "levels.preflight": return Preflight(request);
                case "levels.publish": return Publish(request);
                default: throw new InvalidOperationException("Unknown level operation.");
            }
        }
    }
}
