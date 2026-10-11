using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Game.CharacterTest.Editor
{
    internal sealed partial class CharacterTestTacticalSummaryPreparationWindow
    {
        [SerializeField] private CharacterTestRuntimeAiPlannerDevSceneController _requestController;
        [SerializeField] private string _returnBookmark;
        [SerializeField] private bool _returnPending;
        private Button _returnToLab;
        private double _returnStarted;

        internal static void OpenFromLab(CharacterTestRuntimeAiPlannerDevSceneController controller, CharacterTestTacticalSummaryRequest request)
        {
            QueueLibraryLaunch(controller, request, batch: false, run: false);
        }

        internal static void OpenLibraryFromLab(CharacterTestRuntimeAiPlannerDevSceneController controller, CharacterTestTacticalSummaryRequest request, bool batch)
        {
            QueueLibraryLaunch(controller, request, batch, run: true);
        }

        private void AddReturnControls(VisualElement root)
        {
            _returnToLab = new Button(ReturnToLab) { text = "返回 AI Lab 原卡制作" };
            root.Add(_returnToLab);
        }

        private void ReturnToLab()
        {
            if (string.IsNullOrEmpty(_returnBookmark) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            _returnPending = true; _returnStarted = EditorApplication.timeSinceStartup;
            EditorApplication.isPlaying = true;
        }

        private void UpdateReturnControls()
        {
            _returnToLab?.SetEnabled(!_batchRunning && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling
                && !string.IsNullOrEmpty(_returnBookmark));
            if (!_returnPending || !EditorApplication.isPlaying || EditorApplication.isCompiling || _status == null) return;
            if (_returnStarted == 0) _returnStarted = EditorApplication.timeSinceStartup;
            var bookmark = JsonConvert.DeserializeObject<CharacterTestTacticalSummaryReturn>(_returnBookmark);
            string issue = "等待 AI Lab 就绪。";
            foreach (var controller in UnityEngine.Resources.FindObjectsOfTypeAll<CharacterTestRuntimeAiPlannerDevSceneController>().Where(c => c.gameObject.scene.IsValid()))
                if (controller.TryRestoreTacticalSummaryReturn(bookmark, out issue))
                { controller.RefreshActionBrowser(); _status.text = issue; _returnPending = false; return; }
            if (EditorApplication.timeSinceStartup - _returnStarted > 30)
            { _returnPending = false; _status.text = issue + " 制作内容仍保存在原工作副本中。"; }
        }
    }
}
