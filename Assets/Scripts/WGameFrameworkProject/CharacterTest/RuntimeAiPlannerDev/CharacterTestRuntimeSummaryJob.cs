using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MxFramework.CharacterAction;
using Newtonsoft.Json;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Queues missing or stale card metrics for isolated, frame-budgeted presentation preparation.</summary>
    /// <remarks>Requests are low frequency. Tick advances at most one existing measurement job per UI frame.</remarks>
    internal sealed class CharacterTestCardSummaryPreparation : IDisposable
    {
        private sealed class Request
        {
            internal CharacterTestTacticalRoleData Role;
            internal CharacterTestActionBrowserEntry Action;
            internal string Fingerprint, Result;
            internal bool Succeeded;
            internal CharacterTestCharacterEquipmentComponent Equipment;
        }
        private readonly Dictionary<(int Character, int Action, string Hash, string Catalog,
            EquipmentVersionSnapshot Equipment, bool DefaultLoadout), Request> _requests = new();
        private readonly Queue<Request> _pending = new();
        private readonly CharacterActionTacticalMetricsStore _store;
        private CharacterTestRuntimeSummaryJob _job;
        private Request _current;
        internal long Revision { get; private set; }

        internal CharacterTestCardSummaryPreparation(CharacterActionTacticalMetricsStore store = null)
            => _store = store ?? new CharacterActionTacticalMetricsStore();

        internal static bool NeedsMeasurement(string diagnostic) => CharacterTestTacticalSummaryPreparation.NeedsRebuild(diagnostic);

        internal string Prepare(CharacterTestTacticalRoleData role, int actionId, string diagnostic)
        {
            if (!NeedsMeasurement(diagnostic)) return diagnostic;
            var catalog = CharacterTestActionBrowserCatalog.Get();
            var action = catalog.Actions.FirstOrDefault(a => a.ActionId == actionId);
            if (action?.Category != CharacterActionCategory.BasicAttack || action.Issues.Length > 0) return diagnostic;
            var input = role.Input(actionId);
            if (!CharacterActionContentHashUtility.TryCompute(input.Action, input.Source, input.Physics, input.Bakes,
                input.Bindings, out string hash, out string issue)) return issue;
            // New catalog inputs replace obsolete requests even when the selected action's hash stays the same.
            var equipment=role.Source.Equipment;
            var key = (role.Source.Identity.CharacterId, actionId, hash,catalog.Fingerprint,equipment.ToSnapshot(),equipment.UseDefaultLoadout);
            if (!_requests.TryGetValue(key, out var request))
            {
                // Completed failures remain deduplicated until their actual measurement inputs change.
                if (_requests.Count >= 256)
                    foreach (var completed in _requests.Where(pair => pair.Value.Result != null).Select(pair => pair.Key).ToArray())
                        _requests.Remove(completed);
                request = new Request { Role = role, Action = action, Fingerprint = catalog.Fingerprint, Equipment=equipment };
                _requests.Add(key, request); _pending.Enqueue(request); Revision++;
            }
            else if(request.Succeeded && _store.Query(input,CharacterTestTacticalSummaryPreparation.StandardMeasurement()).Status
                != CharacterActionTacticalMetricsLoadStatus.Valid)
            {
                // A completed request is not a permanent cache-validity flag. Rebuild if its derived data expires again.
                request.Role=role;request.Action=action;request.Equipment=equipment;request.Result=null;request.Succeeded=false;
                _pending.Enqueue(request);Revision++;
            }
            return request.Result ?? (ReferenceEquals(request, _current) ? "正在自动测量：" + _job.Status : "等待自动测量摘要。");
        }

        internal void Tick()
        {
            if (_job == null && _pending.Count > 0)
            {
                _current = _pending.Dequeue();
                var request = _current;
                _job = new CharacterTestRuntimeSummaryJob(request.Role.Source.Identity.CharacterId, request.Equipment,
                    CharacterTestTacticalSummaryPreparation.StandardMeasurement(), new[] { request.Action },
                    () => CharacterTestActionBrowserCatalog.Get().Fingerprint == request.Fingerprint
                        && request.Role.Source.Equipment.UseDefaultLoadout==request.Equipment.UseDefaultLoadout
                        && request.Role.Source.Equipment.ToSnapshot()==request.Equipment.ToSnapshot(), _store);
            }
            if (_job == null) return;
            _job.Tick();
            if (_job.Running) return;
            _current.Succeeded=_job.CommittedCount > 0;
            _current.Result = _job.CommittedCount > 0 ? "摘要已自动更新。" : "自动测量未完成：" + _job.Status + "\n" + _job.Details;
            _job.Dispose(); _job = null; _current = null; Revision++;
        }

        public void Dispose()
        {
            _job?.Dispose(); _job = null; _current = null; _pending.Clear(); _requests.Clear(); Revision++;
        }
    }

    /// <summary>Explicit authoring work advanced by real UI frames, independent of the battle clock.</summary>
    internal sealed class CharacterTestRuntimeSummaryJob : IDisposable
    {
        internal sealed class Entry
        {
            public int ActionId;
            public string Name, Status, Reason, Diagnostic;
        }

        private readonly int _character;
        private readonly CharacterTestCharacterEquipmentComponent _equipment;
        private readonly CharacterActionSpatialMeasurement _measurement;
        private readonly CharacterTestActionBrowserEntry[] _actions;
        private readonly CharacterActionTacticalMetricsStore _store;
        private readonly Func<bool> _inputsCurrent;
        private readonly List<Entry> _results = new();
        private readonly Stopwatch _budget = new();
        private CharacterActionReferenceProbeSession _probe;
        private IEnumerator<object> _steps;
        private CharacterActionRangeProbeResult _measured;
        private int _index, _sample, _sampleCount;
        private string _state = "重建中";
        internal bool Running { get; private set; } = true;
        internal int CommittedCount { get; private set; }
        internal IReadOnlyList<Entry> Results => _results;
        internal string ReportPath { get; private set; }
        internal string ReportIssue { get; private set; }
        internal string Status => _state + " · " + _index + "/" + _actions.Length
            + " · 成功 " + CommittedCount + " / 失败 " + _results.Count(r => r.Status == "失败") + " / 跳过 " + _results.Count(r => r.Status == "跳过")
            + (Running && _index < _actions.Length ? " · " + _actions[_index].Name
                + (_sample == 0 ? " · 准备动作" : " · 采样 " + _sample + "/" + _sampleCount) : "")
            + (!Running && _actions.Length == 1 && _results.Count == 1 && _results[0].Status != "成功" ? " · " + _results[0].Reason : "");
        internal string Details => string.Join("\n", _results.Select(r => r.Status + " · " + r.Name + "：" + r.Reason
                + (r.Diagnostic != r.Reason ? "\n" + r.Diagnostic : "")))
            + (string.IsNullOrEmpty(ReportPath) ? "" : "\n结果报告：" + ReportPath)
            + (string.IsNullOrEmpty(ReportIssue) ? "" : "\n报告保存失败：" + ReportIssue);

        internal CharacterTestRuntimeSummaryJob(int character, CharacterTestCharacterEquipmentComponent equipment,
            CharacterActionSpatialMeasurement measurement, CharacterTestActionBrowserEntry[] actions,
            Func<bool> inputsCurrent, CharacterActionTacticalMetricsStore store = null)
        {
            _character = character; _equipment = equipment; _measurement = measurement;
            _actions = actions.ToArray(); _inputsCurrent = inputsCurrent;
            _store = store ?? new CharacterActionTacticalMetricsStore();
        }

        // A nominal tick or distance query is indivisible. Yield after at most four steps or 4 ms.
        internal void Tick()
        {
            if (!Running) return;
            try
            {
                if (_inputsCurrent?.Invoke() == false) { Cancel("已取消：角色、装备或动作数据已变化"); return; }
                if (_index >= _actions.Length) { Finish("重建完成"); return; }
                var action = _actions[_index];
                if (_probe == null)
                {
                    _sample = 0; _sampleCount = 0;
                    if (action.Category != CharacterActionCategory.BasicAttack)
                    { Record("跳过", action.Category == CharacterActionCategory.Dodge ? "闪避不需要空间摘要" : "当前测量器只支持攻击类型"); return; }
                    if (action.Issues.Length > 0) { Record("失败", string.Join("；", action.Issues)); return; }
                    _probe = CharacterActionReferenceProbeSession.ForTestAction(_character, _measurement.ReferenceDummy, action.ActionId, _equipment);
                    if (!CharacterTestTacticalCardInputs.TryValidateAction(_probe.Action, out string issue)) { Record("跳过", issue); return; }
                    _steps = CharacterActionRangeProber.MeasureSteps(_probe, _measurement, result => _measured = result,
                        (done, total) => { _sample = done; _sampleCount = total; return Running; }).GetEnumerator();
                    return; // Session setup never shares its frame with a distance sweep.
                }
                _budget.Restart();
                for (int i = 0; i < 4; i++)
                {
                    if (!_steps.MoveNext())
                    {
                        var result = _store.CommitMeasurement(_probe, _measurement, _measured, requireTacticalValidity: true,
                            continueMeasurement: (done, total) => Running && (_inputsCurrent?.Invoke() ?? true));
                        if (result.Status == CharacterActionTacticalMetricsLoadStatus.Valid) { CommittedCount++; Record("成功", "摘要已更新"); }
                        else Record("失败", result.Issue);
                        return;
                    }
                    if (_budget.ElapsedMilliseconds >= 4) return;
                }
            }
            catch (Exception error) { Record("失败", error.Message); }
        }

        private void Record(string status, string reason)
        {
            var action = _actions[_index];
            string diagnostic = reason;
            if (CharacterTestTacticalSummaryDiagnostics.TryDescribe(reason, out string label, out string guidance))
                reason = label + "；" + guidance;
            _results.Add(new Entry { ActionId = action.ActionId, Name = action.Name, Status = status, Reason = reason, Diagnostic = diagnostic });
            ReleaseProbe(); _index++;
            if (_index == _actions.Length) Finish("重建完成");
        }

        internal void Cancel(string reason = "已取消，未完成的动作未写入")
        { if (Running) Finish(reason); }

        private void Finish(string state)
        {
            if (state == "重建完成") state = _results.Any(r => r.Status == "失败")
                ? CommittedCount > 0 ? "部分重建失败" : "重建失败"
                : CommittedCount > 0 ? "重建成功" : "未生成新摘要";
            Running = false; _state = state; ReleaseProbe();
            try
            {
                string directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                    "../Library/MxFramework/CharacterActionTacticalMetrics/BatchReports"));
                Directory.CreateDirectory(directory);
                ReportPath = Path.Combine(directory, "runtime-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(ReportPath, JsonConvert.SerializeObject(new { state, characterId = _character,
                    equipment = _equipment, measurement = _measurement, total = _actions.Length,
                    unprocessed = _actions.Skip(_index).Select(a => a.ActionId).ToArray(), entries = _results }, Formatting.Indented));
            }
            catch (Exception error) { ReportIssue = error.Message; }
        }

        private void ReleaseProbe()
        { _steps?.Dispose(); _steps = null; _probe?.Dispose(); _probe = null; _measured = null; }
        public void Dispose() => Cancel();
    }
}
