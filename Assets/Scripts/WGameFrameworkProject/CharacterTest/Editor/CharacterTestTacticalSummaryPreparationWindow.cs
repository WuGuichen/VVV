using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MxFramework.CharacterAction;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    /// <summary>Explicit Editor preparation surface; the formal authoring workbench remains the AI Lab.</summary>
    internal sealed partial class CharacterTestTacticalSummaryPreparationWindow : EditorWindow
    {
        [SerializeField] private int _characterId = CharacterTestCharacterDefinitionRegistry.DefaultPlayableCharacterId;
        [SerializeField] private bool _fixtureMeasurement;
        [SerializeField] private bool _useDefaultEquipment = true;
        [SerializeField] private string _equipmentId = string.Empty;
        [SerializeField] private string _expectedHash = string.Empty;
        [SerializeField] private int _expectedAction;
        [SerializeField] private int _testActionId;
        [SerializeField] private string _expectedActionSet = string.Empty;
        private Label _status;
        private Label _details;
        private Button _query, _rebuild;
        private VisualElement _inputControls;
        private readonly List<int> _characterIds = new List<int>();

        [MenuItem("MxFramework/CharacterTest/Tactical Metrics/Prepare Character Opener Summary")]
        [MenuItem("MxFramework/CharacterTest/Tactical Metrics/Rebuild Character Action Summaries")]
        private static void OpenMenu()
        {
            var window = GetWindow<CharacterTestTacticalSummaryPreparationWindow>("动作摘要准备");
            window.minSize = new Vector2(560, 560);
            window.Show();
        }

        [MenuItem("MxFramework/CharacterTest/Tactical Metrics/Prepare Character Opener Summary", true)]
        [MenuItem("MxFramework/CharacterTest/Tactical Metrics/Rebuild Character Action Summaries", true)]
        private static bool CanOpenMenu() => !EditorApplication.isPlayingOrWillChangePlaymode;

        internal static void OpenForRequest(CharacterTestTacticalSummaryRequest request)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("摘要准备窗口需要等待 Unity 退出播放后打开。");
            ValidateMeasurement(request);
            var window = GetWindow<CharacterTestTacticalSummaryPreparationWindow>("动作摘要准备");
            if (window._batchRunning) throw new InvalidOperationException("已有摘要重建正在进行，请等待完成或取消。");
            window.minSize = new Vector2(560, 560);
            window._characterId = request.CharacterId;
            window._fixtureMeasurement = request.Measurement.ReferenceDummy.Version == 2;
            window._useDefaultEquipment = request.Equipment.UseDefaultLoadout;
            window._equipmentId = request.Equipment.EquipmentStateStableId;
            window._expectedHash = request.ContentHash;
            window._expectedAction = request.ActionId;
            window._testActionId = request.ActionId;
            window._expectedActionSet = request.ActionSetId;
            window.CreateGUI();
            window.Show();
        }

        private static void ValidateMeasurement(CharacterTestTacticalSummaryRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var profile = request.Measurement.ReferenceDummy.Version == 2
                ? CharacterTestTacticalSummaryPreparation.DualLoadoutFixtureMeasurement()
                : CharacterTestTacticalSummaryPreparation.StandardMeasurement();
            if (profile.ProberVersion != request.Measurement.ProberVersion
                || profile.StepMillimeters != request.Measurement.StepMillimeters
                || profile.SearchMinMillimeters != request.Measurement.SearchMinMillimeters
                || profile.SearchMaxMillimeters != request.Measurement.SearchMaxMillimeters
                || CharacterActionContentHashUtility.ComputeReferenceDummyHash(profile.ReferenceDummy)
                    != CharacterActionContentHashUtility.ComputeReferenceDummyHash(request.Measurement.ReferenceDummy))
                throw new ArgumentException("该入口不支持此测量规格；不能静默替换当前请求。", nameof(request));
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var root = new ScrollView(); root.style.flexGrow = 1;
            rootVisualElement.Add(root);
            root.style.paddingLeft = root.style.paddingRight = 12;
            root.style.paddingTop = root.style.paddingBottom = 12;
            var note = new Label("为所选角色和装备测量动作摘要。全量重建遍历整个动作库，不受搜索、分类或持有限制；当前测量器仅支持攻击类，闪避无需空间摘要。只更新有效派生缓存，失败保留原文件。");
            note.style.whiteSpace = WhiteSpace.Normal;
            root.Add(note);
            _inputControls = new VisualElement(); root.Add(_inputControls);
            var names = new List<string>();
            _characterIds.Clear();
            foreach (var definition in new CharacterTestCharacterDefinitionRegistry().Definitions)
            { _characterIds.Add(definition.CharacterId); names.Add(definition.DisplayName); }
            if (names.Count == 0) { root.Add(new Label("项目没有可用角色，请先完成正式角色导入。")); return; }
            int selected = _characterIds.IndexOf(_characterId);
            if (selected < 0 && !string.IsNullOrEmpty(_expectedHash))
            { root.Add(new Label("原请求的角色已不可用。请回到制作上下文重新准备，不会自动替换成其它角色。")); return; }
            selected = Math.Max(0, selected);
            _characterId = _characterIds[selected];
            var characters = new PopupField<string>("角色", names, selected);
            characters.RegisterValueChangedCallback(evt =>
            {
                _characterId = _characterIds[characters.index];
                _expectedHash = string.Empty; _useDefaultEquipment = true; _equipmentId = string.Empty; _testActionId = 0;
                CreateGUI();
            });
            _inputControls.Add(characters);
            var actions = CharacterTestActionBrowserCatalog.Get().Actions;
            var actionChoices = new List<string> { "请选择动作" };
            actionChoices.AddRange(actions.Select(a => a.Name + " · " + a.ActionId));
            int actionIndex = Array.FindIndex(actions, a => a.ActionId == _testActionId) + 1;
            var actionField = new PopupField<string>("单项动作", actionChoices, actionIndex);
            actionField.RegisterValueChangedCallback(evt => {
                _testActionId = actionField.index == 0 ? 0 : actions[actionField.index - 1].ActionId;
                _expectedHash = string.Empty; _status.text = "所选动作已改变，请重新查询或重建。";
            });
            _inputControls.Add(actionField);
            _inputControls.Add(new Label("装备：" + (_useDefaultEquipment ? "所选角色默认装备" : _equipmentId)));
            var measurement = new PopupField<string>("测量规格",
                new List<string> { "标准人形靶", "双装配验证靶（巨型偏移，仅验证）" }, _fixtureMeasurement ? 1 : 0);
            measurement.RegisterValueChangedCallback(evt => { _fixtureMeasurement = measurement.index == 1; _status.text = "测量规格已改变，请重新查询。"; });
            _inputControls.Add(measurement);
            _query = new Button(() => Execute(false)) { text = "查询所选动作摘要" };
            _rebuild = new Button(() => Execute(true)) { text = "重建所选动作摘要" };
            root.Add(_query); root.Add(_rebuild);
            AddBatchControls(root);
            AddReturnControls(root);
            _status = new Label("准备就绪。可重建所选动作或当前角色全部动作摘要。");
            _status.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_status);
            var details = new Foldout { text = "输入与诊断详情", value = false };
            _details = new Label(); _details.style.whiteSpace = WhiteSpace.Normal; details.Add(_details); root.Add(details);
            RefreshEnabled();
        }

        private void OnEnable() { EditorApplication.update += RefreshEnabled; EditorApplication.update += TickBatch; }
        private void OnDisable() {
            EditorApplication.update -= RefreshEnabled; EditorApplication.update -= TickBatch;
            if (_batchRunning) FinishBatch("已中断（窗口关闭或脚本重载）");
        }
        private void RefreshEnabled()
        {
            bool available = !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating;
            _query?.SetEnabled(available && !_batchRunning && _testActionId > 0);
            _rebuild?.SetEnabled(available && !_batchRunning && _testActionId > 0);
            _inputControls?.SetEnabled(!_batchRunning);
            _batchStart?.SetEnabled(available && !_batchRunning);
            _batchCancel?.SetEnabled(_batchRunning);
            UpdateReturnControls();
        }

        private void Execute(bool rebuild)
        {
            if (_batchRunning || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            { _status.text = "请先保存制作内容、退出运行，并等待资源导入结束。"; return; }
            try
            {
                if (_testActionId <= 0) { _status.text = "请先选择要测量的动作。"; return; }
                CharacterTestPlayableCharacterCatalog.Refresh();
                CharacterTestDefaultBakedWeaponTraceSource.InvalidateCachedInstanceForMeasurement();
                CharacterTestDefaultWeaponTraceBindingResolver.InvalidateCachedInstanceForMeasurement();
                var measurement = _fixtureMeasurement ? CharacterTestTacticalSummaryPreparation.DualLoadoutFixtureMeasurement()
                    : CharacterTestTacticalSummaryPreparation.StandardMeasurement();
                var equipment = new CharacterTestCharacterEquipmentComponent(_useDefaultEquipment, _equipmentId);
                using (var probe = CharacterActionReferenceProbeSession.ForTestAction(_characterId, measurement.ReferenceDummy, _testActionId, equipment))
                {
                    if (!CharacterTestTacticalSummaryPreparation.TryDescribe(probe.Runtime, probe.Source, measurement, out var request, out string issue, _testActionId))
                    { _status.text = "输入准备失败：" + issue; return; }
                    _details.text = "角色：" + request.CharacterId + "\n动作：" + request.ActionId + "\nActionSet：" + request.ActionSetId
                        + "\n内容：" + request.ContentHash;
                    if (!string.IsNullOrEmpty(_expectedHash) && (_expectedHash != request.ContentHash
                        || _expectedAction != request.ActionId || _expectedActionSet != request.ActionSetId))
                    { _status.text = "正式输入已与原运行版本不同。请回到制作上下文重新准备下一轮请求；本次未覆盖摘要。"; return; }
                    var store = new CharacterActionTacticalMetricsStore();
                    CharacterActionTacticalMetricsLoadResult result;
                    if (rebuild)
                    {
                        if (!CharacterTestTacticalSummaryPreparation.TryRebuild(request, store, out result, out issue))
                        { _status.text = "重建未就绪：" + issue; return; }
                    }
                    else result = store.Query(CharacterActionTacticalInput.FromSession(probe), measurement);
                    _status.text = result.Issue.Contains("SummaryLegacyFormatRequiresRemeasure") ? "旧格式摘要需要重新测量。请点击重建所选动作摘要或全量重建。"
                        : result.Status == CharacterActionTacticalMetricsLoadStatus.Valid
                        ? (rebuild ? "摘要已重建并重新核对。" : "摘要与当前输入一致。") + " 返回 AI Lab 后重新编译并开始新测试。"
                        : "摘要状态：" + result.Status + "。可以显式重建；不支持的动作会保留明确诊断。";
                    _details.text += "\n缓存：" + store.GetPath(request.CharacterId, new CharacterActionId(request.ActionId))
                        + "\n诊断：" + result.Issue;
                }
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is System.IO.IOException)
            { _status.text = "摘要准备失败：" + error.Message; }
        }
    }
}
