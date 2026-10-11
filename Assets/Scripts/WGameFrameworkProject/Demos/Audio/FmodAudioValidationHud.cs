using MxFramework.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace MxFramework.Demo
{
    [AddComponentMenu("MxFramework/Audio/FMOD Audio Validation HUD")]
    public sealed class FmodAudioValidationHud : MonoBehaviour
    {
        [SerializeField] private FmodAudioDemoRunner _runner;
        [SerializeField] private UIDocument _document;

        private MxStatusBadge _statusBadge;
        private Label _snapshotLabel;
        private TextField _reportField;
        private FmodAudioSmokeStatus _lastStatus = (FmodAudioSmokeStatus)(-1);
        private float _nextRefreshTime;

        public void Configure(FmodAudioDemoRunner runner, UIDocument document)
        {
            _runner = runner;
            _document = document;
        }

        private void Start()
        {
            Bind();
            Refresh(force: true);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.unscaledTime + 0.2f;
            Refresh(force: false);
        }

        private void Bind()
        {
            _runner = _runner != null ? _runner : GetComponent<FmodAudioDemoRunner>();
            _document = _document != null ? _document : GetComponent<UIDocument>();
            if (_document == null)
                return;

            VisualElement root = _document.rootVisualElement;
            VisualElement statusSlot = root.Q<VisualElement>("status-slot");
            if (statusSlot != null)
            {
                _statusBadge = new MxStatusBadge("NotRun", MxUiTone.Neutral);
                _statusBadge.name = "smoke-status";
                statusSlot.Add(_statusBadge);
            }

            _snapshotLabel = root.Q<Label>("snapshot");
            _reportField = root.Q<TextField>("report");
            VisualElement actions = root.Q<VisualElement>("actions");
            if (actions != null && _runner != null)
            {
                actions.Add(new MxCommandButton(_runner.RunAutomatedSmoke, "运行完整 Smoke"));
                actions.Add(new MxCommandButton(_runner.PlayOneShot, "播放 2D One-shot"));
                actions.Add(new MxCommandButton(_runner.StartLoop, "Start Event"));
                actions.Add(new MxCommandButton(_runner.StopLoop, "Stop Event"));
            }
        }

        private void Refresh(bool force)
        {
            if (_runner == null)
                return;

            FmodAudioSmokeStatus status = _runner.SmokeStatus;
            if (force || status != _lastStatus)
            {
                _lastStatus = status;
                _statusBadge?.Set(status.ToString(), ToTone(status));
            }

            Audio.AudioDebugSnapshot snapshot = _runner.Snapshot;
            if (_snapshotLabel != null)
            {
                _snapshotLabel.text =
                    "Backend " + (snapshot.Initialized ? "Ready" : "Unavailable")
                    + "  |  Plays " + snapshot.TotalPlayRequests
                    + "  |  Stops " + snapshot.TotalStopRequests
                    + "  |  Active " + snapshot.ActiveEventCount
                    + "  |  Errors " + snapshot.RecentErrors.Count;
            }

            if (_reportField != null && _reportField.value != _runner.LastSmokeReport)
                _reportField.SetValueWithoutNotify(_runner.LastSmokeReport);
        }

        private static MxUiTone ToTone(FmodAudioSmokeStatus status)
        {
            switch (status)
            {
                case FmodAudioSmokeStatus.Passed:
                    return MxUiTone.Positive;
                case FmodAudioSmokeStatus.Failed:
                    return MxUiTone.Danger;
                case FmodAudioSmokeStatus.Running:
                    return MxUiTone.Warning;
                default:
                    return MxUiTone.Neutral;
            }
        }
    }
}
