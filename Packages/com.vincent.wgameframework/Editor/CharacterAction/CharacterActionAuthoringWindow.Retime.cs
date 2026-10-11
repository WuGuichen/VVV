using MxFramework.CharacterAction;
using UnityEngine.UIElements;

namespace MxFramework.Editor.CharacterAction
{
    public sealed partial class CharacterActionAuthoringWindow
    {
        private void AddActionRateControls(VisualElement parent, CharacterActionJsonDto action)
        {
            var rate = new FloatField("整体速率")
            {
                name = "character-action-rate",
                value = 0.8f,
                isDelayed = true,
                tooltip = "相对于当前动作：0.8 放慢，1 不变，1.2 加快。应用后可撤销，保存后生效。",
            };
            parent.Add(rate);
            var message = new Label("同步缩放全部轨道、阶段和切换窗口；保留总位移与伤害。")
            {
                style = { whiteSpace = WhiteSpace.Normal },
            };
            var apply = new Button(() =>
            {
                if (!ReferenceEquals(action, _model?.SelectedAction))
                    return;
                if (!CharacterActionRateUtility.TryCreateScaledAction(action, rate.value,
                        out CharacterActionJsonDto scaled, out string error))
                {
                    message.text = error;
                    return;
                }
                if (rate.value == 1f)
                {
                    message.text = "1 倍速保持当前动作不变。";
                    return;
                }
                ApplyStructuralChange(() =>
                {
                    _timelinePlayback?.Pause();
                    CharacterActionRateUtility.CopyTiming(scaled, action);
                }, UiRefreshScope.ActionDetail);
            })
            {
                name = "character-action-apply-rate",
                text = "应用到所有事件",
                tooltip = "仅修改当前动作。动画和特效变速；音效、技能事件调整触发时间，独立资源内容不变。重复应用会继续缩放。",
            };
            apply.SetEnabled(!_openRequestReadOnly);
            parent.Add(apply);
            parent.Add(message);
        }
    }
}
