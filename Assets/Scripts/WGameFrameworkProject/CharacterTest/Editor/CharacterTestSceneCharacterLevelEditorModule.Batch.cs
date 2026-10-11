using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.Editor.Levels;
using UnityEngine.UIElements;
namespace MxFramework.Game.CharacterTest.Editor
{
    public sealed partial class CharacterTestSceneCharacterLevelEditorModule : IMxLevelEditorBatchSelectionModule
    {
        public void BuildBatchInspector(MxLevelEditorContext context, IReadOnlyList<MxLevelEditorListRow> rows, VisualElement inspector)
        {
            _activeContext = context; EnsureLoaded();
            var selected = rows.Select(r => r.Payload as Row).Where(r => r != null).Select(r => r.Id).Distinct().ToArray();
            inspector.Add(new Label("已选择 " + selected.Length + " 个角色；Ctrl / Shift 可多选。"));
            var team = new IntegerField("目标队伍") { name = "mx-level-batch-team", value = 1 };
            var group = new TextField("目标分组") { name = "mx-level-batch-group", value = string.Empty };
            inspector.Add(team); inspector.Add(group);
            void Apply(int? teamId, bool setGroup)
            {
                try
                {
                    EnsureUndoSession();
                    using var change = _undoSession.BeginTransaction("批量修改角色队伍与分组");
                    CharacterTestLevelBatchEdits.Apply(_document, selected, teamId, setGroup, group.value);
                    MarkDirty(); change.Commit(); RebuildRows();
                    context?.RevealRow("document", _rows.First(r => r.Id == selected[0]).Key);
                    context?.RequestRefresh?.Invoke();
                }
                catch (Exception error) { _status = error.Message; context?.RequestRefresh?.Invoke(); }
            }
            inspector.Add(new Button(() => Apply(team.value, false)) { text = "应用队伍", name = "mx-level-batch-apply-team" });
            inspector.Add(new Button(() => Apply(null, true)) { text = "应用分组", name = "mx-level-batch-apply-group" });
            inspector.Add(new Button(() => Apply(team.value, true)) { text = "一起应用", name = "mx-level-batch-apply-both" });
        }
    }
}
