using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Config.Levels;
using UnityEditor;
using UnityEngine.UIElements;
namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestSceneCharacterLevelEditorModule
    {
        private void DrawBehaviorGroupActions(VisualElement inspector, CharacterTestSceneCharacterDefinition actor,
            CharacterTestRuntimeAiPlannerBehaviorGroupRevision group)
        {
            var binding = actor.BehaviorBinding;
            var uses = new List<string>();
            void Collect(string levelId, CharacterTestSceneCharacterAuthoringDocument document)
            {
                foreach (var character in document?.Characters ?? new List<CharacterTestSceneCharacterDefinition>())
                    if (character?.BehaviorBinding?.GroupId == binding.GroupId)
                        uses.Add(levelId + " / " + character.DisplayName + " / " + character.StableId + " @" + character.BehaviorBinding.GroupRevision);
            }
            Collect(_document.LevelId, _document);
            try
            {
                var catalog = CharacterTestLevelAuthoringOperations.Catalog;
                foreach (var level in catalog.Levels.Where(l => l.LevelId != _document.LevelId))
                {
                    var source = catalog.Load(level.LevelId, CharacterTestLevelAuthoringOperations.ProjectRoot, false, out _);
                    if (CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(source, out var characters, out _)) Collect(level.LevelId, characters);
                }
            }
            catch (Exception error) { inspector.Add(new HelpBox("共享影响清单未完整读取：" + error.Message, HelpBoxMessageType.Warning)); }
            inspector.Add(new Label("共享组使用实例：" + uses.Count));
            foreach (var use in uses) inspector.Add(new Label(use));
            inspector.Add(new HelpBox("角色锁定已发布版本。共享编辑发布新版本后，这些角色仍保持原版本；需显式重选版本并检查。", HelpBoxMessageType.Info));
            inspector.Add(new Button(() => {
                EditorUtility.DisplayDialog("编辑共享行为组", binding.GroupId + " @" + binding.GroupRevision
                    + "\n使用该组的实例：\n" + string.Join("\n", uses) + "\n新版本不会自动替换关卡中的版本绑定。", "打开现有编辑器");
                EditorApplication.ExecuteMenuItem("MxFramework/Editor/Open Editor Hub");
            }) { text = "打开行为组编辑器（Editor Hub）", name = "mx-level-edit-behavior-group" });
            if (group == null)
            {
                inspector.Add(new HelpBox("锁定的行为组版本不可用，请重新选择正式版本。", HelpBoxMessageType.Error)); return;
            }
            inspector.Add(new Label(group.DisplayName + " @" + group.Revision + " · " + group.Cards.Count(c => c.Enabled) + " 张启用卡"));
            inspector.Add(new Label(group.Description));
            inspector.Add(new Button(() => {
                try
                {
                    var state = CharacterTestRuntimeAiPlannerProjectCatalogLoader.Load();
                    string unsupported = KnownLevelGroupConsumerIssue(actor, group, state);
                    if (!string.IsNullOrEmpty(unsupported))
                    {
                        _status = "未复制或绑定，原配置保留：" + unsupported;
                        _activeContext?.RequestRefresh?.Invoke(); return;
                    }
                    var owner = new CharacterTestRuntimeAiPlannerProjectCatalogAuthoringSession(state);
                    if (!owner.TryCreateGroupCopy(group, out var copy, out var diagnostic)) throw new InvalidOperationException(diagnostic);
                    EnsureUndoSession(); using var change = _undoSession.BeginTransaction("复制独立行为组并绑定当前角色");
                    var replacement = CharacterTestLevelBehaviorCompiler.Select(copy);
                    replacement.Actions = actor.BehaviorBinding.Clone().Actions;
                    foreach (var action in replacement.Actions) action.DependencyFingerprint = string.Empty;
                    actor.BehaviorBinding = replacement; actor.RuntimeAiPlannerProfilePath = string.Empty;
                    MarkDirty(); change.Commit();
                    _status = "已复制独立行为组，仅当前角色重绑。副本已保存；撤销会恢复角色绑定。";
                    _activeContext?.RequestRefresh?.Invoke();
                }
                catch (Exception error) { _status = error.Message; _activeContext?.RequestRefresh?.Invoke(); }
            }) { text = "复制独立行为组并绑定此角色", name = "mx-level-copy-behavior-group" });
        }
    }
}
