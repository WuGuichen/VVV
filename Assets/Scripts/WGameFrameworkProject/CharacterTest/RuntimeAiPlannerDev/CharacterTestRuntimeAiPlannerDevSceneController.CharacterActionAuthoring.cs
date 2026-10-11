using System.Collections.Generic;
using MxFramework.CharacterAction;

namespace MxFramework.Game.CharacterTest
{
    public sealed partial class CharacterTestRuntimeAiPlannerDevSceneController
    {
        internal bool TryGetSelectedCharacterActionAuthoringContext(
            out CharacterTestRuntimeAiLabCharacterActionAuthoringContext context,
            out string diagnostic)
        {
            context = default;
            diagnostic = string.Empty;
            int entityId = ResolveSelectedAgentEntityId();
            if (entityId <= 0)
            {
                diagnostic = "当前没有可用于动作编辑的选中角色。";
                return false;
            }

            IReadOnlyList<CharacterTestCharacterEntity> entities =
                _context?.Slice?.Services.CharacterEntities;
            CharacterTestCharacterEntity entity = null;
            for (int i = 0; entities != null && i < entities.Count; i++)
            {
                if (entities[i]?.Id.Value == entityId)
                {
                    entity = entities[i];
                    break;
                }
            }
            if (entity == null)
            {
                diagnostic = "当前选中角色实体已经失效。";
                return false;
            }

            CharacterTestCharacterActionRuntime actionRuntime =
                _context?.Slice?.Services.ActionRuntime;
            if (actionRuntime == null
                || !actionRuntime.TryGetEntityActionSet(entity.Id, out CharacterActionSetConfig actionSet)
                || actionSet == null)
            {
                diagnostic = "当前角色没有可解析的 CharacterAction ActionSet。";
                return false;
            }

            context = new CharacterTestRuntimeAiLabCharacterActionAuthoringContext(
                entity.Id.Value,
                entity.Identity.CharacterStableId,
                entity.Equipment.EquipmentStateStableId,
                actionSet.StableId);
            return true;
        }
    }
}
