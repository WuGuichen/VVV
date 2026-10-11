using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MxFramework.CharacterAction;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestTacticalSummaryPreparationWindow
    {
        private Button _batchStart, _batchCancel;
        private ProgressBar _batchProgress;
        private Label _batchResult;
        private bool _batchRunning, _cancelBatch;
        private int _batchIndex, _batchCharacter;
        private string _batchFingerprint;
        private CharacterTestCharacterEquipmentComponent _batchEquipment;
        private CharacterActionSpatialMeasurement _batchMeasurement;
        private CharacterTestActionBrowserEntry[] _batchActions;
        private readonly List<BatchEntry> _batchEntries = new();
        [SerializeField] private string _batchReportText = "尚未开始批量重建。";
        [SerializeField] private string _batchReportPath;

        private sealed class BatchEntry
        {
            public int ActionId;
            public string Name, Status, Reason;
        }

        private void AddBatchControls(VisualElement root)
        {
            _batchStart = new Button(StartBatch) { text = "重建当前角色全部动作摘要" };
            _batchCancel = new Button(() => _cancelBatch = true) { text = "取消批量重建" };
            _batchProgress = new ProgressBar { lowValue = 0, highValue = 100, title = "等待开始" };
            root.Add(_batchStart); root.Add(_batchCancel); root.Add(_batchProgress);
            var foldout = new Foldout { text = "批量结果（成功／失败／跳过）", value = true };
            var scroll = new ScrollView(); scroll.style.maxHeight = 220;
            _batchResult = new Label(_batchReportText); _batchResult.style.whiteSpace = WhiteSpace.Normal;
            scroll.Add(_batchResult); foldout.Add(scroll);
            foldout.Add(new Button(() => {
                if (!string.IsNullOrEmpty(_batchReportPath) && File.Exists(_batchReportPath)) EditorUtility.RevealInFinder(_batchReportPath);
            }) { text = "定位结果报告" });
            root.Add(foldout);
        }

        private void StartBatch()
        {
            if (_batchRunning || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                var data = CharacterTestActionBrowserCatalog.Get(refresh: true);
                if (data.Issues.Length > 0) { _status.text = "动作目录存在问题：" + data.Issues[0]; return; }
                CharacterTestPlayableCharacterCatalog.Refresh();
                CharacterTestDefaultBakedWeaponTraceSource.InvalidateCachedInstanceForMeasurement();
                CharacterTestDefaultWeaponTraceBindingResolver.InvalidateCachedInstanceForMeasurement();
                _batchActions = data.Actions; _batchFingerprint = data.Fingerprint;
                _batchCharacter = _characterId;
                _batchEquipment = new CharacterTestCharacterEquipmentComponent(_useDefaultEquipment, _equipmentId);
                _batchMeasurement = _fixtureMeasurement ? CharacterTestTacticalSummaryPreparation.DualLoadoutFixtureMeasurement()
                    : CharacterTestTacticalSummaryPreparation.StandardMeasurement();
                _batchEntries.Clear(); _batchIndex = 0; _cancelBatch = false; _batchRunning = true;
                _batchReportPath = null;
                _status.text = "批量重建中。可以取消；已成功的缓存保留，当前未完成动作不写入。";
                RefreshBatchReport("进行中"); RefreshEnabled();
            }
            catch (Exception error) { _status.text = "批量重建未开始：" + error.Message; }
        }

        private void TickBatch()
        {
            if (!_batchRunning) return;
            if (_cancelBatch) { FinishBatch("已取消"); return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            { FinishBatch("已中断（编辑器状态发生变化）"); return; }
            if (_batchIndex >= _batchActions.Length) { FinishBatch("已完成"); return; }
            var entry = _batchActions[_batchIndex];
            try
            {
                if (CharacterTestActionBrowserCatalog.Get(refresh: true).Fingerprint != _batchFingerprint)
                { FinishBatch("已中断（动作作者源或分类发生变化，请重新开始）"); return; }
                if (entry.Category != CharacterActionCategory.BasicAttack)
                {
                    AddBatchResult(entry, "跳过", entry.Category == CharacterActionCategory.Dodge ? "闪避不需要空间摘要" : "当前测量器只支持攻击类型");
                    return;
                }
                using (var probe = CharacterActionReferenceProbeSession.ForTestAction(_batchCharacter, _batchMeasurement.ReferenceDummy,
                    entry.ActionId, _batchEquipment))
                {
                    if (!CharacterTestTacticalCardInputs.TryValidateAction(probe.Action, out string issue))
                    { AddBatchResult(entry, "跳过", "动作结构暂不支持：" + issue); return; }
                    var result = new CharacterActionTacticalMetricsStore().Refresh(probe, _batchMeasurement,
                        requireTacticalValidity: true, continueMeasurement: (done, total) => {
                            float amount = (_batchIndex + (float)done / Math.Max(1, total)) / Math.Max(1, _batchActions.Length);
                            _batchProgress.value = amount * 100;
                            _batchProgress.title = (_batchIndex + 1) + " / " + _batchActions.Length + " · " + entry.Name;
                            if (EditorUtility.DisplayCancelableProgressBar("批量重建动作摘要",
                                entry.Name + "（" + entry.ActionId + "） · 采样 " + done + "/" + total, amount)) _cancelBatch = true;
                            return !_cancelBatch && !EditorApplication.isPlayingOrWillChangePlaymode;
                        });
                    if (_cancelBatch || result.Issue == "MeasurementCancelled") { FinishBatch("已取消"); return; }
                    AddBatchResult(entry, result.Status == CharacterActionTacticalMetricsLoadStatus.Valid ? "成功" : "失败",
                        result.Status == CharacterActionTacticalMetricsLoadStatus.Valid ? "摘要已写入" : "原缓存未覆盖：" + result.Issue);
                }
            }
            catch (Exception error) { AddBatchResult(entry, "失败", "原缓存未覆盖：" + error.Message); }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private void AddBatchResult(CharacterTestActionBrowserEntry action, string status, string reason)
        {
            _batchEntries.Add(new BatchEntry { ActionId = action.ActionId, Name = action.Name, Status = status, Reason = reason });
            _batchIndex++;
            _batchProgress.value = 100f * _batchIndex / Math.Max(1, _batchActions.Length);
            _batchProgress.title = _batchIndex + " / " + _batchActions.Length;
            RefreshBatchReport("进行中"); Repaint();
        }

        private void RefreshBatchReport(string state)
        {
            _batchReportText = state + " · " + _batchIndex + "/" + _batchActions.Length
                + " · 成功 " + _batchEntries.Count(e => e.Status == "成功")
                + " · 失败 " + _batchEntries.Count(e => e.Status == "失败")
                + " · 跳过 " + _batchEntries.Count(e => e.Status == "跳过")
                + " · 未处理 " + (_batchActions.Length - _batchIndex)
                + "\n" + string.Join("\n", _batchEntries.Select(e => e.Status + " · " + e.Name + "（" + e.ActionId + "）：" + e.Reason));
            if (_batchResult != null) _batchResult.text = _batchReportText;
        }

        private void FinishBatch(string state)
        {
            if (!_batchRunning) return;
            _batchRunning = false; EditorUtility.ClearProgressBar();
            RefreshBatchReport(state);
            try
            {
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/MxFramework/CharacterActionTacticalMetrics/BatchReports"));
                Directory.CreateDirectory(folder);
                _batchReportPath = Path.Combine(folder, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".json");
                File.WriteAllText(_batchReportPath, JsonConvert.SerializeObject(new {
                    state, characterId = _batchCharacter, equipment = _batchEquipment, measurement = _batchMeasurement,
                    sourceFingerprint = _batchFingerprint, total = _batchActions.Length,
                    unprocessed = _batchActions.Skip(_batchIndex).Select(a => a.ActionId).ToArray(), entries = _batchEntries
                }, Formatting.Indented));
                if (_status != null) _status.text = state + "。可返回 AI Lab，刷新动作库查看可选状态。";
            }
            catch (Exception error) { if (_status != null) _status.text = state + "；报告保存失败：" + error.Message; }
            if (_requestController != null) _requestController.RefreshActionBrowser();
            RefreshEnabled(); Repaint();
        }
    }
}
