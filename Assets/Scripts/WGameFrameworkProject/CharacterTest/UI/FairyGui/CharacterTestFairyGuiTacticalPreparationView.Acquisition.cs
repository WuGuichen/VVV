using System;
using System.Linq;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiTacticalPreparationView
    {
        private string _acquireCharacter, _acquireName, _acquireSearch = "", _acquireStatus = "";

        private void ShowAcquireActions()
        {
            var entity = _preparation.Find(_controller.SelectedEntityId);
            if (_busy || entity == null) return;
            if (_player.HasCharacterItemProgress)
            { SetResult(false, "实例学习需要明确获取请求与权威逻辑帧；此模板测试入口不可用于角色物品。"); return; }
            _popupEntity = entity.EntityId; _acquireCharacter = entity.CharacterStableId;
            _acquireName = entity.DisplayName; _acquireSearch = _acquireStatus = "";
            OpenPopup(PopupKind.AcquireActions, "测试获取动作", "");
            var search = Required(_popup, "input").asTextInput;
            search.visible = true; search.editable = true; search.text = ""; search.promptText = "搜索动作名称";
            search.onChanged.Set(() => { _acquireSearch = search.text ?? ""; RenderAcquireActions(); });
            Required(_popup, "cancel").asButton.title = "完成";
            Button(_popup, "confirm", SaveAcquiredActions).title = "重试保存";
            RenderAcquireActions();
        }

        private void RenderAcquireActions()
        {
            if (_root == null || _popupKind != PopupKind.AcquireActions) return;
            var catalog = _player.Catalog;
            var owned = _player.Read(_acquireCharacter);
            var pool = catalog.Pool(_acquireCharacter);
            var actions = catalog.Actions.Where(action => action.CharacterStableIds.Contains(_acquireCharacter))
                .GroupBy(action => action.ActionId).Select(group => group.First())
                .Where(action => string.IsNullOrWhiteSpace(_acquireSearch)
                    || (action.DisplayName ?? "").IndexOf(_acquireSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(action => action.DisplayName, StringComparer.Ordinal).ToArray();
            Label(_popup, "context", _acquireName + "\n仅向当前进度授予动作；不改初始配置，不自动装配。");
            Label(_popup, "issue", _busy ? "正在保存…" : !string.IsNullOrEmpty(_player.SaveIssue) ? _player.SaveIssue
                : actions.Length == 0 ? "没有符合角色范围及搜索条件的动作。" : _acquireStatus);
            Required(_popup, "confirm").visible = !_busy && _player.HasUnsavedChanges && !string.IsNullOrEmpty(_player.SaveIssue);
            Required(_popup, "input").asTextInput.editable = !_busy;
            _popupList.itemRenderer = (index, item) =>
            {
                var action = actions[index];
                bool has = catalog.ContainsAction(owned?.ActionIds, action.Id);
                bool allowed = catalog.ContainsAction(pool?.ActionIds, action.Id);
                var row = item.asButton;
                row.title = (has ? "✓ 已拥有   " : allowed ? "+ 获取   " : "不可获取   ") + action.DisplayName;
                RowButton(row, () => AcquireAction(action.Id)); row.enabled = !_busy && !has && allowed;
                row.tooltips = has ? "当前角色已拥有此动作" : allowed ? "获取后可在卡面动作名称中选择装配" : "此动作不在角色允许获取的范围内";
            };
            _popupList.numItems = actions.Length; _popupList.RefreshVirtualList(); LayoutAcquireActions();
        }

        private async void AcquireAction(string actionId)
        {
            if (_busy || _popupKind != PopupKind.AcquireActions || _controller.SelectedEntityId != _popupEntity
                || _preparation.Find(_popupEntity)?.CharacterStableId != _acquireCharacter) return;
            if (!_player.TryGrantAction(_acquireCharacter, actionId, out string issue))
            { _acquireStatus = issue; RenderAcquireActions(); return; }
            _acquireStatus = "已获取：" + (_player.Catalog.Action(actionId)?.DisplayName ?? actionId);
            await PersistAcquiredActions();
        }

        private async void SaveAcquiredActions() => await PersistAcquiredActions();

        private async System.Threading.Tasks.Task PersistAcquiredActions()
        {
            if (_busy || _root == null) return;
            var service = _player; int generation = _generation;
            _busy = true; SetPopupBusy(true); RenderAcquireActions();
            try
            {
                bool saved = await service.FlushAsync();
                if (_root == null || generation != _generation) return;
                if (saved) _acquireStatus = "✓ 已保存到当前进度。可关闭窗口后点击卡面动作名称装配。";
            }
            catch (Exception error) { if (_root != null && generation == _generation) _acquireStatus = "保存失败：" + error.Message; }
            finally
            {
                if (_root != null && generation == _generation)
                { _busy = false; _revision = -1; SetPopupBusy(false); RenderAcquireActions(); }
            }
        }

        private void LayoutAcquireActions()
        {
            float s = _uiScale, width = 620, height = Mathf.Min(690, (_root.height - 48)/s);
            float x = (_root.width-width*s)*.5f, y = (_root.height-height*s)*.5f;
            _popup.SetScale(s,s); Box(_popup,x,y,width,height);
            Box(Required(_popup,"backgroundImage"),-6,-6,width+12,height+12);
            Box(Required(_popup,"title"),24,20,width-100,40);
            Box(Required(_popup,"close"),width-64,20,40,40);
            Required(_popup,"context").visible=true;
            Box(Required(_popup,"context"),24,76,width-48,70);
            Box(Required(_popup,"input"),24,154,width-48,44);
            _popupList.SetScale(s,s); Box(_popupList,x+24*s,y+214*s,width-48,height-362);
            Box(Required(_popup,"issue"),24,height-132,width-48,54);
            Box(Required(_popup,"confirm"),24,height-64,180,40);
            Box(Required(_popup,"cancel"),width-180,height-64,156,40);
            Required(_root,"popupDismiss").SetSize(_root.width,_root.height);
        }
    }
}
