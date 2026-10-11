using System;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestTacticalSummaryPreparationWindow
    {
        private const string PendingLaunchKey = "MxFramework.CharacterTest.PendingSummaryLaunch.v1";

        private sealed class SummaryLaunch
        {
            public int CharacterId, ActionId;
            public bool Batch, Run, FixtureMeasurement, UseDefaultEquipment;
            public string EquipmentId, ExpectedHash, ExpectedActionSet, ReturnBookmark;
        }

        [InitializeOnLoadMethod]
        private static void RegisterSummaryLaunch()
        {
            EditorApplication.update -= OpenPendingSummaryLaunch;
            EditorApplication.update += OpenPendingSummaryLaunch;
        }

        private static void QueueLibraryLaunch(CharacterTestRuntimeAiPlannerDevSceneController controller,
            CharacterTestTacticalSummaryRequest request, bool batch, bool run)
        {
            ValidateMeasurement(request);
            foreach (var window in UnityEngine.Resources.FindObjectsOfTypeAll<CharacterTestTacticalSummaryPreparationWindow>())
                if (window._batchRunning) throw new InvalidOperationException("已有摘要重建正在进行，请等待完成或取消。");
            if (!batch && request.ActionId <= 0) throw new InvalidOperationException("请先选择要重建的动作。");
            if (!string.IsNullOrEmpty(SessionState.GetString(PendingLaunchKey, "")))
                throw new InvalidOperationException("摘要重建正在切换到编辑器，请稍候。");
            string bookmark = null;
            if (EditorApplication.isPlaying)
            {
                if (controller == null) throw new InvalidOperationException("当前卡片制作上下文不可用，未退出播放。");
                if (!controller.TrySaveTacticalSummaryReturn(out var saved, out string issue))
                    throw new InvalidOperationException("保存失败，保持当前运行：" + issue);
                bookmark = JsonConvert.SerializeObject(saved);
            }
            var launch = new SummaryLaunch {
                CharacterId = request.CharacterId, ActionId = request.ActionId, Batch = batch, Run = run,
                FixtureMeasurement = request.Measurement.ReferenceDummy.Version == 2,
                UseDefaultEquipment = request.Equipment.UseDefaultLoadout, EquipmentId = request.Equipment.EquipmentStateStableId,
                ExpectedHash = request.ContentHash, ExpectedActionSet = request.ActionSetId, ReturnBookmark = bookmark
            };
            // SessionState survives the Play Mode domain reload without retaining scene objects.
            SessionState.SetString(PendingLaunchKey, JsonConvert.SerializeObject(launch));
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        private static void OpenPendingSummaryLaunch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string json = SessionState.GetString(PendingLaunchKey, "");
            if (string.IsNullOrEmpty(json)) return;
            SessionState.EraseString(PendingLaunchKey);
            SummaryLaunch launch = null;
            try
            {
                launch = JsonConvert.DeserializeObject<SummaryLaunch>(json);
                if (launch == null) throw new InvalidOperationException("摘要重建请求不可用。");
                var measurement = launch.FixtureMeasurement ? CharacterTestTacticalSummaryPreparation.DualLoadoutFixtureMeasurement()
                    : CharacterTestTacticalSummaryPreparation.StandardMeasurement();
                var equipment = new CharacterTestCharacterEquipmentComponent(launch.UseDefaultEquipment, launch.EquipmentId);
                OpenForRequest(new CharacterTestTacticalSummaryRequest(launch.CharacterId, launch.ActionId, launch.ExpectedActionSet,
                    equipment, launch.ExpectedHash, measurement, true));
                var window = GetWindow<CharacterTestTacticalSummaryPreparationWindow>();
                window._requestController = null;
                window._returnBookmark = launch.ReturnBookmark; window._returnPending = false;
                window._status.text = "制作上下文已保存，Unity 已退出播放。";
                window.UpdateReturnControls();
                if (launch.Run)
                {
                    if (launch.Batch) window.StartBatch();
                    else window.Execute(true);
                }
            }
            catch (Exception error)
            {
                var window = GetWindow<CharacterTestTacticalSummaryPreparationWindow>("动作摘要准备");
                if (launch != null) window._returnBookmark = launch.ReturnBookmark;
                window.CreateGUI();
                window._status.text = "重建未开始：" + error.Message + "。保存的制作内容仍保留，可返回原卡。";
            }
        }
    }
}
