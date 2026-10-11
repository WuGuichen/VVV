using MxFramework.CharacterAction;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private void BuildReferencesTab(VisualElement root)
        {
            CharacterActionAuthoringReferenceContext references = _model.ReferenceContext ?? CharacterActionAuthoringReferenceContext.CreateDefault();
            _model.ReferenceContext = references;

            VisualElement tools = AddCard(root, "引用工具");
            AddButtonRow(tools,
                ("从当前动作收集", () => ApplyFieldChange(() => _model.ReferenceContext.MergeFromModel(_model))),
                ("从生成角色包收集", () => ApplyFieldChange(() => CharacterActionPreviewBindingDiscovery.AddGeneratedReferences(_model.ReferenceContext))),
                ("扫描项目引用", () => ApplyStructuralChange(DiscoverProjectReferences, UiRefreshScope.Content)),
                ("添加选中动作默认值", () => ApplyFieldChange(AddSelectedActionReferenceDefaults)));

            string discoveryText = _referenceDiscovery != null
                ? _referenceDiscovery.SummaryText
                : "本会话尚未执行项目引用扫描，当前使用手动/回退引用列表。";
            tools.Add(CreateTextArea("扫描来源", discoveryText, compact: true));

            VisualElement animation = AddCard(root, "动画 / 战斗");
            AddCsvField(animation, "Animation Action Keys", references.AnimationActionKeys);
            AddCsvField(animation, "Combat Action Ids", references.CombatActionIds);

            VisualElement resources = AddCard(root, "表现资源");
            AddCsvField(resources, "Audio Cue Ids", references.AudioCueIds);
            AddCsvField(resources, "Resource Keys", references.PresentationResourceKeys);

            VisualElement character = AddCard(root, "可选筛选上下文");
            AddCsvField(character, "Character Stable Ids", references.CharacterStableIds);
            AddCsvField(character, "Equipment State Stable Ids", references.EquipmentStateStableIds);
            AddCsvField(character, "Equipment Tags", references.EquipmentTags);
        }

        private void DiscoverProjectReferences()
        {
            _referenceDiscovery = CharacterActionReferenceDiscoveryService.Discover(_model);
            _model.ReferenceContext = _referenceDiscovery.ReferenceContext;
        }

        private void AddSelectedActionReferenceDefaults()
        {
            CharacterActionJsonDto action = _model.SelectedAction;
            if (action == null)
                return;

            _model.ReferenceContext.AddAnimationActionKey("anim." + action.ActionId);
            _model.ReferenceContext.AddCombatActionId(CharacterActionAuthoringIds.Resolve("combat." + action.ActionId, 412));
            _model.ReferenceContext.AddAudioCueId(CharacterActionAuthoringIds.Resolve("sfx." + action.ActionId, 401));
            _model.ReferenceContext.AddPresentationResourceKey("vfx." + action.ActionId);
        }

    }
}
