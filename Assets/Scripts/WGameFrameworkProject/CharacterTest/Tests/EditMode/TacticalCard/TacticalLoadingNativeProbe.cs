#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using MxFramework.Game.CharacterTest;
using MxFramework.Game.CharacterTest.TacticalCard;
using Newtonsoft.Json;
using UnityEngine;

namespace MxFramework.Tests
{
    // Editor-only acceptance driver. The gate delays the REAL isolated save callback;
    // it never sets a loading stage or fabricates a progress value.
    public static class TacticalLoadingNativeProbe
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private static GateStore _gate;
        private static CharacterTacticalProgressStore _progress;
        private static readonly FieldInfo StoreField = typeof(CharacterTacticalProgressStore).GetField("_store", Hidden);
        private static GameManager Manager => UnityEngine.Object.FindAnyObjectByType<GameManager>();
        private static TacticalCardSliceCoordinator Coordinator => Manager.GetComponent<TacticalCardSliceCoordinator>();

        public static string Install()
        {
            var root = Environment.GetEnvironmentVariable("MX_CHARACTERTEST_TACTICAL_SAVE_ROOT");
            if (!Application.isPlaying || string.IsNullOrEmpty(root) || !root.Contains("LoadingValidation"))
                throw new InvalidOperationException("This probe requires Play Mode and an isolated LoadingValidation save root.");
            if (Manager?.RuntimeContext?.UiRuntime == null || Coordinator == null)
                throw new InvalidOperationException("The formal runtime has not initialized. Resolve its startup error before installing the save gate.");
            _progress = (CharacterTacticalProgressStore)typeof(CharacterTacticalServices).GetField("_progress", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            if (_gate == null)
            {
                _gate = new GateStore((ICharacterTacticalStateStore)StoreField.GetValue(_progress));
                StoreField.SetValue(_progress, _gate);
            }
            return Snapshot();
        }

        public static string Arm() { _gate.Arm(); return Snapshot(); }
        public static string StartContinue() { Coordinator.RequestStartOrContinue(); return Snapshot(); }
        public static string StartNewGame() { Coordinator.RequestNewGameOrRetry(); return Snapshot(); }
        public static string Retry() { var ok = Coordinator.RetryLoading(); return Snapshot("retry=" + ok); }
        public static string Back() { var ok = Coordinator.ReturnFromLoadingFailure(); return Snapshot("back=" + ok); }
        public static string ReturnHome() { Coordinator.RequestCloseOrExit(); return Snapshot(); }
        public static string ReturnPreparation() { var ok = Coordinator.ReturnToPreparation(); return Snapshot("return=" + ok); }
        public static string Deploy() { var ok = Coordinator.TryDeployPreparedBattle(out string issue); return Snapshot("deploy=" + ok + ";" + issue); }
        public static string Release(bool succeed) { _gate.Release(succeed); return Snapshot(); }
        public static string Disable() { Coordinator.enabled = false; return Snapshot(); }
        public static string Enable() { Coordinator.enabled = true; return Snapshot(); }
        public static string CloseView() { Manager.RuntimeContext.UiRuntime.FairyGuiLoadingHost.enabled = false; return Snapshot(); }
        public static string ReopenView() { Manager.RuntimeContext.UiRuntime.FairyGuiLoadingHost.enabled = true; return Snapshot(); }
        public static string Capture(string file) { Manager.StartCoroutine(CaptureFrame(file)); return Snapshot(); }
        private static IEnumerator CaptureFrame(string file)
        {
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Path.Combine("Logs/TacticalLoadingUI", file + ".png"), image.EncodeToPNG()); }
            finally { UnityEngine.Object.Destroy(image); }
            Snapshot(file);
        }

        public static string Snapshot(string marker = "")
        {
            if (Manager?.RuntimeContext?.UiRuntime == null || Coordinator == null)
                return JsonConvert.SerializeObject(new { marker, runtimeReady = false });
            var c = Coordinator; var runtime = Manager.RuntimeContext; var host = runtime.UiRuntime.FairyGuiLoadingHost;
            var hud = Manager.GetComponent<TacticalCardHud>();
            var value = new { marker, frame = Time.frameCount, phase = c.State.ToString(), busy = c.IsBusy,
                attempt = c.Loading.Attempt, loading = c.Loading.Status.ToString(), stage = c.Loading.Stage.ToString(),
                c.Loading.CompletedUnits, c.Loading.TotalUnits, c.Loading.Issue, c.Loading.CanRetry,
                configured = host?.IsConfigured, open = host?.IsOpen, visibleFrameCount = host?.VisibleFrameCount,
                blocksInput = runtime.UiRuntime.HasTacticalLoading, battleBlocked = hud?.BattleInputBlocked,
                entities = c.CharacterCount, scale = Time.timeScale, profile = CharacterTacticalServices.ActiveProfileId,
                saveCalls = _gate?.Calls, pendingSave = _gate?.Pending, lastStatusMessage = host?.LastStatusMessage };
            string json = JsonConvert.SerializeObject(value);
            Directory.CreateDirectory("Logs/TacticalLoadingUI");
            File.AppendAllText("Logs/TacticalLoadingUI/native-flow.jsonl", json + Environment.NewLine);
            return json;
        }

        public static string Cleanup()
        {
            if (_gate != null) { _gate.Release(true); if (_progress != null) StoreField.SetValue(_progress, _gate.Inner); }
            _gate = null; _progress = null;
            return Snapshot("cleanup");
        }

        private sealed class GateStore : ICharacterTacticalStateStore
        {
            internal readonly ICharacterTacticalStateStore Inner;
            private TaskCompletionSource<bool> _pending;
            private bool _armed;
            internal int Calls;
            internal bool Pending => _pending != null && !_pending.Task.IsCompleted;
            internal GateStore(ICharacterTacticalStateStore inner) { Inner = inner; }
            internal void Arm() { if (Pending) throw new InvalidOperationException("Save already pending"); _armed = true; _pending = null; }
            internal void Release(bool succeed) { _pending?.TrySetResult(succeed); }
            public Task<string> LoadAsync() => Inner.LoadAsync();
            public Task ResetAsync(string json) => throw new InvalidOperationException("Acceptance never resets a save.");
            public async Task SaveAsync(string json)
            {
                Calls++;
                if (_armed)
                {
                    _armed = false; _pending = new TaskCompletionSource<bool>();
                    if (!await _pending.Task) throw new IOException("隔离验收：保存被拒绝（未写入进度）。");
                }
                await Inner.SaveAsync(json);
            }
        }
    }
}
#endif
