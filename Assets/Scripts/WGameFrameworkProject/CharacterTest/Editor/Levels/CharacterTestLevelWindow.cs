using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.Config.Levels;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed class CharacterTestLevelWindow : EditorWindow
    {
        private CharacterTestLevelCatalog _catalog;
        private MxLevelAuthoringDocument _document;
        private CharacterTestSceneCharacterAuthoringDocument _characters;
        private CharacterTestRuntimeAiPlannerProjectCatalogState _groups;
        private string _sourceHash, _previewToken, _message;
        private int _level, _actor;
        private bool _dirty;
        private Vector2 _scroll;

        [MenuItem("MxFramework/Character Test/Levels")]
        public static void OpenWindow() => MxFramework.Editor.Levels.MxLevelEditorWindow.OpenWindow();

        private void OnEnable() { if (File.Exists(CharacterTestLevelAuthoringOperations.DirectoryPath)) Reload(); }
        private void Reload()
        {
            _catalog = CharacterTestLevelAuthoringOperations.Catalog;
            _groups = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
            _level = Mathf.Clamp(_level, 0, _catalog.Levels.Count - 1);
            var result = CharacterTestLevelAuthoringOperations.Open(_catalog.Levels[_level].LevelId);
            Apply(result); _dirty = false; _previewToken = null;
        }
        private void Apply(JObject result)
        {
            _document = MxLevelAuthoringJson.LoadFromJson(result["document"].ToString());
            CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(_document, out _characters, out _);
            _sourceHash = (string)result["sourceHash"];
        }
        private JObject Request()
        {
            CharacterTestSceneCharacterAuthoringAdapter.UpsertCharactersModule(_document, _characters);
            return new JObject { ["levelId"] = _document.LevelId, ["expectedSourceHash"] = _sourceHash,
                ["document"] = JObject.Parse(MxLevelAuthoringJson.SaveToJson(_document)), ["previewToken"] = _previewToken };
        }
        private void Run(Action operation)
        {
            try { operation(); }
            catch (Exception e) { _message = e.Message; Debug.LogWarning("[Levels] " + e.Message); }
        }
        private void Changed() { _dirty = true; _previewToken = null; }
        private void OnGUI()
        {
            if (_catalog == null) { EditorGUILayout.HelpBox("关卡目录未配置。使用下方入口从当前权威文档登记关卡。", MessageType.Info);
                if (GUILayout.Button("登记当前关卡文档")) Run(RegisterCurrent); return; }
            int selected = EditorGUILayout.Popup("关卡", _level, _catalog.Levels.Select(l => l.DisplayName + " / " + l.LevelId).ToArray());
            if (selected != _level && (!_dirty || EditorUtility.DisplayDialog("尚未保存", "切换将放弃当前关卡的未保存修改。", "放弃并切换", "继续编辑")))
            { _level = selected; Run(Reload); }
            if (_document == null) return;
            EditorGUILayout.LabelField("Scene", _document.ScenePath);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开目标 Scene")) Run(() => {
                    if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before opening the Scene.");
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(_document.ScenePath); });
                if (GUILayout.Button("作为本 Scene 启动关卡")) Run(() => {
                    var manager = CharacterTestLevelAuthoringOperations.Manager;
                    if (EditorApplication.isPlaying || manager == null || manager.gameObject.scene.path != _document.ScenePath) throw new InvalidOperationException("请先在编辑模式打开目标 Scene。");
                    Undo.RecordObject(manager, "Set initial level"); manager.SetInitialLevelId(_document.LevelId); EditorSceneManager.MarkSceneDirty(manager.gameObject.scene); _message = "保存此 Scene 后，Player/Play 将按 levelId 装载。"; });
                if (GUILayout.Button("打开关卡源文件")) AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<TextAsset>(_catalog.Levels[_level].Path));
                if (GUILayout.Button("刷新")) Run(() => { if (!_dirty) Reload(); else _message = "请先保存或切换并放弃当前修改。"; });
            }
            if (_characters?.Characters.Count > 0)
            {
                _actor = Mathf.Clamp(_actor, 0, _characters.Characters.Count - 1);
                _actor = EditorGUILayout.Popup("角色实例", _actor, _characters.Characters.Select(a => a.DisplayName + " / " + a.StableId).ToArray());
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                DrawActor(_characters.Characters[_actor]);
                EditorGUILayout.EndScrollView();
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("保存")) Run(() => { Apply(CharacterTestLevelAuthoringOperations.Save(Request())); _dirty = false; _message = "已保存，尚未发布。"; });
                if (GUILayout.Button("预检")) Run(() => { var result = CharacterTestLevelAuthoringOperations.Preflight(Request()); Apply(result);
                    _previewToken = (string)result["previewToken"]; _message = (string)result["message"]; });
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_previewToken)))
                    if (GUILayout.Button("发布此关")) Run(() => { Apply(CharacterTestLevelAuthoringOperations.Publish(Request())); _dirty = false;
                        _previewToken = null; _message = "正式关卡已发布。"; });
            }
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                if (GUILayout.Button("装载已发布关卡 / 重建会话")) Run(() => { CharacterTestLevelAuthoringOperations.Manager.LoadLevel(_document.LevelId); _message = "会话已切换为 " + _document.LevelId; });
            EditorGUILayout.LabelField("状态", (_dirty ? "未保存 · " : "") + (_message ?? ""));
        }
        private void DrawActor(CharacterTestSceneCharacterDefinition actor)
        {
            EditorGUILayout.LabelField("实例 ID", actor.StableId); EditorGUILayout.LabelField("角色模板", actor.CharacterId.ToString());
            EditorGUI.BeginChangeCheck();
            actor.Enabled = EditorGUILayout.Toggle("启用", actor.Enabled);
            actor.ControlOwner = (CharacterTestCharacterControlOwner)EditorGUILayout.EnumPopup("控制者", actor.ControlOwner);
            actor.TeamId = EditorGUILayout.IntField("阵营", actor.TeamId);
            if (actor.SpawnPose != null) { actor.SpawnPose.Position = EditorGUILayout.Vector3Field("出生位置", actor.SpawnPose.Position);
                actor.SpawnPose.YawDegrees = EditorGUILayout.FloatField("朝向", actor.SpawnPose.YawDegrees); }
            if (EditorGUI.EndChangeCheck()) Changed();
            if (_groups?.Success != true) { EditorGUILayout.HelpBox(_groups?.Diagnostic ?? "正式组库不可用。", MessageType.Error); return; }
            var groups = _groups.GroupCatalog.Groups.OrderBy(g => g.DisplayName).ThenBy(g => g.Revision).ToArray();
            int current = actor.BehaviorBinding == null ? 0 : Array.FindIndex(groups, g => g.StableGroupId == actor.BehaviorBinding.GroupId && g.Revision == actor.BehaviorBinding.GroupRevision) + 1;
            var names = new[] { "无正式行为组" }.Concat(groups.Select(g => g.DisplayName + " / " + g.StableGroupId + " @" + g.Revision)).ToArray();
            int selected = EditorGUILayout.Popup("正式行为组版本", current, names);
            if (selected != current) { actor.BehaviorBinding = selected == 0 ? null : CharacterTestLevelBehaviorCompiler.Select(groups[selected - 1]);
                if (selected != 0) actor.RuntimeAiPlannerProfilePath = string.Empty; Changed(); }
            var binding = actor.BehaviorBinding; if (binding == null) return;
            EditorGUILayout.LabelField("锁定组 hash", binding.GroupContentHash.ToString());
            var group = groups.SingleOrDefault(g => g.StableGroupId == binding.GroupId && g.Revision == binding.GroupRevision);
            if (group == null) { EditorGUILayout.HelpBox("引用版本已缺失，需显式选择另一个版本。", MessageType.Error); return; }
            foreach (var card in group.Cards.Where(c => c.Enabled).OrderBy(c => c.Order))
            {
                _groups.CardCatalog.TryGetExact(card.StableCardId, card.CardRevision, out var published);
                EditorGUILayout.LabelField(published.Metadata.DisplayName + " @" + card.CardRevision, EditorStyles.boldLabel);
                foreach (string path in CharacterTestLevelBehaviorCompiler.Slots(published.TacticalSource))
                {
                    var action = binding.Actions.SingleOrDefault(a => a.EntryId == card.EntryId && a.SlotPath == path);
                    if (action == null) { action = new CharacterTestLevelActionBinding { EntryId = card.EntryId, SlotPath = path }; binding.Actions.Add(action); }
                    var available = CharacterTestActionBrowserCatalog.Get().Actions.ToArray();
                    int chosen = Array.FindIndex(available, a => a.ActionId == action.ActionId) + 1;
                    int next = EditorGUILayout.Popup(path, chosen, new[] { "请选择动作" }.Concat(available.Select(a => a.Name + " / " + a.ActionId)).ToArray());
                    if (next != chosen) { action.ActionId = next == 0 ? 0 : available[next - 1].ActionId; action.DependencyFingerprint = ""; Changed(); }
                }
            }
            if (GUILayout.Button("清空此实例组与动作绑定")) { actor.BehaviorBinding = null; Changed(); }
        }
        private void RegisterCurrent()
        {
            var manager = CharacterTestLevelAuthoringOperations.Manager ?? throw new InvalidOperationException("GameManager missing.");
            string path = manager.CharacterSceneCache.AuthoringJsonPath;
            string full = Path.Combine(CharacterTestLevelAuthoringOperations.ProjectRoot, path);
            var envelope = MxLevelAuthoringJson.LoadFromJson(File.ReadAllText(full));
            var catalog = File.Exists(CharacterTestLevelAuthoringOperations.DirectoryPath) ? CharacterTestLevelAuthoringOperations.Catalog
                : new CharacterTestLevelCatalog { DefaultLevelId = envelope.LevelId };
            if (!catalog.Levels.Any(l => l.LevelId == envelope.LevelId)) catalog.Levels.Add(new CharacterTestLevelEntry { LevelId = envelope.LevelId, DisplayName = envelope.LevelId, Path = path });
            CharacterTestLevelCatalog.AtomicWrite(CharacterTestLevelAuthoringOperations.DirectoryPath, Newtonsoft.Json.JsonConvert.SerializeObject(catalog, Newtonsoft.Json.Formatting.Indented));
            AssetDatabase.Refresh(); Reload();
        }
    }
}
