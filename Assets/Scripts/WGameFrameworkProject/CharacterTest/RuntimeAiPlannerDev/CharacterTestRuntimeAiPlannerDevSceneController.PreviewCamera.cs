using System.Collections.Generic;
using UnityEngine;

namespace MxFramework.Game.CharacterTest
{
    internal readonly struct CharacterTestPreviewCameraSubject
    {
        internal CharacterTestPreviewCameraSubject(int entityId, Vector3 center, bool primary)
        { EntityId = entityId; Center = center; Primary = primary; }
        internal int EntityId { get; }
        internal Vector3 Center { get; }
        internal bool Primary { get; }
    }

    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        internal object SetupPreviewResourceIdentity => _context?.Slice?.Services.Resources?.ResourceManager;

        internal bool TryCreateSetupCharacterPreview(int team, int slotIndex,
            out CharacterTestRuntimeCharacterView view, out string issue)
        {
            view = null;
            issue = string.Empty;
            var slot = _matchConfig?.TryGetSlot(team, slotIndex);
            var resources = _context?.Slice?.Services.Resources?.ResourceManager;
            if (!IsShellSetupPhase || slot == null || !slot.Enabled || resources == null)
            { issue = "角色预览资源尚未准备。"; return false; }
            foreach (var definition in CharacterTestPlayableCharacterCatalog.Discover())
            {
                if (definition.CharacterId != slot.CharacterId) continue;
                var loaded = resources.Load<GameObject>(definition.PrefabKey);
                if (!loaded.Success || loaded.Value?.Value == null)
                { issue = "角色模型加载失败：" + definition.DisplayName + " · " + loaded.Error.Message; return false; }
                var loader = new CharacterTestRuntimeCharacterLoader(resources, _context.Logger, false);
                if (!loader.Instantiate(new CharacterTestCharacterEntityId(team * CharacterTestArenaMatchConfig.SlotsPerTeam + slotIndex + 1),
                    loaded.Value, Vector3.zero, "TacticalPreview_" + definition.DisplayName,
                    definition.AnimationBinding, definition.GeneratedPackageRoot, CharacterTestCharacterControlOwner.None, out view))
                { resources.Release(loaded.Value); issue = "角色预览组装失败：" + definition.DisplayName; return false; }
                // Presentation only. Never register these instances as combat entities or input consumers.
                foreach (var script in view.Instance.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
                foreach (var collider in view.Instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                foreach (var body in view.Instance.GetComponentsInChildren<Rigidbody>(true))
                { body.isKinematic = true; body.detectCollisions = false; }
                foreach (var animator in view.Instance.GetComponentsInChildren<Animator>(true)) animator.applyRootMotion = false;
                return true;
            }
            issue = "没有找到角色模型：" + slot.CharacterId;
            return false;
        }

        internal bool TryGetPreviewTargetHurtboxes(out CharacterTestDamageGeometrySnapshot geometry, out Vector3 root)
        {
            geometry = null; root = default;
            // Live geometry must never be overlaid on a different historical pose.
            if (IsHistoricalFrameSelected || !_frameBuffer.TryGetSelected(out var frame)
                || frame.Agents.Count == 0) return false;
            var agent = frame.Agents[Mathf.Clamp(_selectedAgentIndex, 0, frame.Agents.Count - 1)];
            var runtime = _context?.Slice?.CharacterRuntime;
            var entities = _context?.Slice?.Services.CharacterEntities;
            if (!agent.TargetExists || runtime == null || entities == null
                || !runtime.TryGetDamageGeometrySnapshot(agent.TargetEntityId, out geometry)
                || geometry == null || !geometry.IsValid) return false;
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.Id.Value != agent.TargetEntityId) continue;
                root = new Vector3(entity.Transform.XMillimeters, entity.Transform.YMillimeters,
                    entity.Transform.ZMillimeters) * 0.001f;
                return true;
            }
            geometry = null;
            return false;
        }

        // Read the displayed frame, including during history browsing; never query latest live transforms.
        internal bool CollectPreviewCameraSubjects(List<CharacterTestPreviewCameraSubject> output, bool all,
            out int frameNumber)
        {
            output.Clear(); frameNumber = 0;
            if (IsShellSetupPhase && !IsHistoricalFrameSelected)
                return CollectSetupPreviewSubjects(output, all);
            if (!_frameBuffer.TryGetSelected(out var frame)) return false;
            frameNumber = frame.Frame;
            return CollectPreviewCameraSubjects(frame, _selectedAgentIndex, all, output);
        }

        private bool CollectSetupPreviewSubjects(List<CharacterTestPreviewCameraSubject> output, bool all)
        {
            if (!Add(_selectedSetupTeamId, _selectedSetupSlotIndex, true)) return false;
            if (all)
                for (int team = CharacterTestArenaMatchConfig.Team1Id; team <= CharacterTestArenaMatchConfig.Team2Id; team++)
                    for (int slot = 0; slot < CharacterTestArenaMatchConfig.SlotsPerTeam; slot++)
                        if (team != _selectedSetupTeamId || slot != _selectedSetupSlotIndex) Add(team, slot, false);
            return true;

            bool Add(int team, int index, bool primary)
            {
                var slot = _matchConfig?.TryGetSlot(team, index);
                if (slot == null || !slot.Enabled || slot.CharacterId <= 0
                    || !_matchConfig.TryGetSpawnPosition(team, index, out Vector2 position)) return false;
                // Setup framing identities belong to the view only; these are not runtime entity IDs.
                output.Add(new CharacterTestPreviewCameraSubject(team * CharacterTestArenaMatchConfig.SlotsPerTeam + index + 1,
                    new Vector3(position.x, 0f, position.y), primary));
                return true;
            }
        }

        internal static bool CollectPreviewCameraSubjects(CharacterTestRuntimeAiPlannerDevFrameSnapshot frame,
            int selectedIndex, bool all, List<CharacterTestPreviewCameraSubject> output)
        {
            output.Clear();
            if (frame == null || frame.Agents.Count == 0) return false;
            int selected = Mathf.Clamp(selectedIndex, 0, frame.Agents.Count - 1);
            var primary = frame.Agents[selected];
            if (!primary.HasScenePreviewGeometry) return false;
            Add(primary.EntityId, primary.ScenePreviewSourceCenterX, primary.ScenePreviewSourceCenterY, primary.ScenePreviewSourceCenterZ, true);
            if (primary.TargetExists)
                Add(primary.TargetEntityId, primary.ScenePreviewTargetCenterX, primary.ScenePreviewTargetCenterY, primary.ScenePreviewTargetCenterZ, false);
            if (all)
                for (int i = 0; i < frame.Agents.Count; i++)
                {
                    var agent = frame.Agents[i];
                    if (!agent.HasScenePreviewGeometry) continue;
                    Add(agent.EntityId, agent.ScenePreviewSourceCenterX, agent.ScenePreviewSourceCenterY, agent.ScenePreviewSourceCenterZ, false);
                    if (agent.TargetExists)
                        Add(agent.TargetEntityId, agent.ScenePreviewTargetCenterX, agent.ScenePreviewTargetCenterY, agent.ScenePreviewTargetCenterZ, false);
                }
            return output.Count > 0;

            void Add(int id, float x, float y, float z, bool isPrimary)
            {
                if (id <= 0 || float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)
                    || float.IsInfinity(x) || float.IsInfinity(y) || float.IsInfinity(z)) return;
                for (int i = 0; i < output.Count; i++) if (output[i].EntityId == id) return;
                output.Add(new CharacterTestPreviewCameraSubject(id, new Vector3(x, y, z), isPrimary));
            }
        }
    }
}
