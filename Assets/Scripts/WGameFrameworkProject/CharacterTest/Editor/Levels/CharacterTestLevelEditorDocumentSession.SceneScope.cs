using System;
using System.Linq;
using MxFramework.Editor.Levels;
using UnityEditor;
using UnityEngine;
namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestLevelEditorDocumentSession
    {
        public bool TryDiscardDraft(out string message)
        {
            if (string.IsNullOrEmpty(CurrentPath))
            {
                message = "请选择正式关卡后再使用关卡范围的放弃；原有 Scene 编辑保持不变。"; return false;
            }
            if (_sceneBaseline == null) { message = "当前缺少关卡范围快照，不能安全放弃 Scene 编辑；草稿保留。"; return false; }
            if (!_sceneBaseline.TryRestore(MxLevelEditorModuleRegistry.Modules, out message)) return false;
            ReloadSavedDocument(); return true;
        }

        private void ApplySelectedActorPoses()
        {
            if (!CharacterTestSceneCharacterAuthoringAdapter.TryExtractCharactersDocument(_draft, out var document, out _)) return;
            var module = MxLevelEditorModuleRegistry.Modules.OfType<CharacterTestSceneCharacterLevelEditorModule>().Single();
            var context = new MxLevelEditorContext(_activeWindow, module, null);
            var rows = module.BuildRows(context, "document");
            var poses = document.Characters.Where(c => c.SpawnPose != null).Select(actor => {
                var row = rows.SingleOrDefault(r => r.Payload is CharacterTestSceneCharacterLevelEditorModule.Row own && own.Id == actor.StableId);
                if (row == null || !module.TryResolveRowObject(context, row, out var target)) throw new InvalidOperationException("角色位置无法映射到 Scene：" + actor.StableId);
                var gameObject = target as GameObject ?? (target as Component)?.gameObject;
                if (gameObject == null) throw new InvalidOperationException("角色锚点不可用：" + actor.StableId);
                return (Actor: actor, Transform: gameObject.transform);
            }).ToArray();
            // Resolve every anchor before changing the first transform.
            foreach (var pose in poses)
            {
                var position = pose.Actor.SpawnPose.Position;
                var value = position;
                var rotation = Quaternion.Euler(0, pose.Actor.SpawnPose.YawDegrees, 0);
                if (pose.Transform.position == value && pose.Transform.rotation == rotation) continue;
                Undo.RecordObject(pose.Transform, "载入当前关卡角色位置");
                pose.Transform.SetPositionAndRotation(value, rotation);
            }
        }
    }
}
