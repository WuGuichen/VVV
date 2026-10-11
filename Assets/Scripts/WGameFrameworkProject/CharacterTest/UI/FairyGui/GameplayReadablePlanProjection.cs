using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MxFramework.Game.CharacterTest
{
    internal enum GameplayReadablePlanExitTone
    {
        Success,
        Failure,
        Condition,
        Invalid
    }

    internal enum GameplayReadablePlanExitRuntimeState
    {
        Idle,
        Listening,
        Triggered,
        Pending,
        Committed,
        Invalid
    }

    internal enum GameplayReadablePlanDetailKind
    {
        None,
        Stage,
        Action,
        Exit
    }

    internal enum GameplayReadablePlanProgressMode
    {
        None,
        Determinate,
        Indeterminate,
        Completed,
        Failed,
        Overrun
    }

    internal enum GameplayReadablePlanMetricKind
    {
        None,
        Distance,
        Frames,
        Boolean,
        Range
    }

    internal sealed class GameplayReadablePlanStageProjection
    {
        internal int SourceIndex;
        internal string StableId = string.Empty;
        internal string StageLabel = string.Empty;
        internal string ActionLabel = string.Empty;
        internal string RunningLabel = string.Empty;
        internal string WindowText = string.Empty;
        internal string TooltipText = string.Empty;
        internal int StartFrame;
        internal int DurationFrames;
        internal int ActualDurationFrames = -1;
        internal int ActionStartFrame;
        internal int ActionDurationFrames;
        internal int ActionProgressFrames = -1;
        internal long ActionInstanceId;
        internal GameplayReadablePlanProgressMode ActionProgressMode;
        internal GameplayReadablePlanMetricKind MetricKind;
        internal string MetricCurrentText = string.Empty;
        internal string MetricTargetText = string.Empty;
        internal string MetricCompletionText = string.Empty;
        internal bool OwnsPrimaryAction;
        internal bool Selected;
        internal bool Current;
        internal bool ActionRunning;
        internal bool ActionConflict;
    }

    internal sealed class GameplayReadablePlanExitProjection
    {
        internal int SourceTransitionIndex = -1;
        internal int SourceDetectorIndex = -1;
        internal int SourceStageIndex = -1;
        internal int TargetStageIndex = -1;
        internal CharacterTestRuntimeAiLabLogicElementKind ElementKind;
        internal GameplayReadablePlanExitTone Tone;
        internal string StableId = string.Empty;
        internal string DetectorStableId = string.Empty;
        internal string Label = string.Empty;
        internal string ListeningLabel = string.Empty;
        internal string TriggeredLabel = string.Empty;
        internal string CommittedLabel = string.Empty;
        internal string FactText = string.Empty;
        internal string TargetText = string.Empty;
        internal string WindowText = string.Empty;
        internal string TooltipText = string.Empty;
        internal int WindowStartFrame;
        internal int WindowDurationFrames;
        internal bool CardScope;
        internal bool Selected;
        internal bool DetectorActive;
        internal bool DetectorTriggered;
        internal bool Pending;
        internal bool Committed;
        internal bool Invalid;
        internal int ProgressFrames = -1;
        internal int ProgressDurationFrames;
        internal GameplayReadablePlanProgressMode ProgressMode;

        internal GameplayReadablePlanExitRuntimeState RuntimeState
        {
            get
            {
                if (Invalid)
                    return GameplayReadablePlanExitRuntimeState.Invalid;
                if (Committed)
                    return GameplayReadablePlanExitRuntimeState.Committed;
                if (Pending)
                    return GameplayReadablePlanExitRuntimeState.Pending;
                if (DetectorTriggered)
                    return GameplayReadablePlanExitRuntimeState.Triggered;
                return DetectorActive
                    ? GameplayReadablePlanExitRuntimeState.Listening
                    : GameplayReadablePlanExitRuntimeState.Idle;
            }
        }
    }

    internal sealed class GameplayReadablePlanExitGroupProjection
    {
        internal int SourceStageIndex = -1;
        internal int SourceDetectorIndex = -1;
        internal int FirstExitIndex;
        internal int ExitCount;
        internal int AnchorFrame;
        internal int WindowStartFrame;
        internal int WindowDurationFrames;
        internal bool CardScope;
        internal bool AnchorOutsideViewport;
        internal string StableKey = string.Empty;
        internal string ListeningLabel = string.Empty;
        internal string TooltipText = string.Empty;
    }

    /// <summary>
    /// Projects the authored behavior plan into Chinese-first gameplay concepts without changing
    /// the compiled Logic v2 program or any runtime authority state.
    /// </summary>
    /// <remarks>
    /// LowFreqAlloc: strings and high-water projection slots are created only when card structure,
    /// viewport, or presentation selection changes. <see cref="UpdateRuntime"/> reuses every slot
    /// and performs no collection allocation.
    /// </remarks>
    internal sealed partial class GameplayReadablePlanProjection
    {
        private readonly List<GameplayReadablePlanStageProjection> _stagePool =
            new List<GameplayReadablePlanStageProjection>(16);
        private readonly List<GameplayReadablePlanExitProjection> _exitPool =
            new List<GameplayReadablePlanExitProjection>(24);
        private readonly List<GameplayReadablePlanExitGroupProjection> _groupPool =
            new List<GameplayReadablePlanExitGroupProjection>(16);
        private bool[] _projectedTransitions = Array.Empty<bool>();
        private bool[] _orderedStages = Array.Empty<bool>();
        private int[] _stagePresentationStarts = Array.Empty<int>();
        private int _stageCount;
        private int _exitCount;
        private int _groupCount;

        private const int OpenStagePresentationFrames = 60;
        private const int StagePresentationGapFrames = 12;
        private const int StageColumnStrideFrames = 72;
        private const int DistanceLabelLimitDecimeters = 1000;
        private static readonly string[] DistanceCurrentLabels = CreateDistanceCurrentLabels();

        internal int StageCount => _stageCount;
        internal int ExitCount => _exitCount;
        internal int GroupCount => _groupCount;
        internal bool SnapshotVisible { get; private set; }
        internal int SnapshotFrame { get; private set; } = -1;
        internal int SnapshotStageIndex { get; private set; } = -1;
        internal string ExecutionStatusText { get; private set; } = string.Empty;
        internal int FlowFrame { get; private set; }
        internal int PresentationStartFrame { get; private set; }
        internal int PresentationEndFrame { get; private set; } = OpenStagePresentationFrames;

        internal GameplayReadablePlanStageProjection GetStage(int index)
        {
            return index >= 0 && index < _stageCount ? _stagePool[index] : null;
        }

        internal GameplayReadablePlanExitProjection GetExit(int index)
        {
            return index >= 0 && index < _exitCount ? _exitPool[index] : null;
        }

        internal GameplayReadablePlanExitGroupProjection GetGroup(int index)
        {
            return index >= 0 && index < _groupCount ? _groupPool[index] : null;
        }

        internal GameplayReadablePlanStageProjection FindStage(string stableId)
        {
            for (int i = 0; i < _stageCount; i++)
            {
                GameplayReadablePlanStageProjection stage = _stagePool[i];
                if (string.Equals(stage.StableId, stableId, StringComparison.Ordinal))
                    return stage;
            }
            return null;
        }

        internal GameplayReadablePlanExitProjection FindExit(string stableId)
        {
            for (int i = 0; i < _exitCount; i++)
            {
                GameplayReadablePlanExitProjection exit = _exitPool[i];
                if (string.Equals(exit.StableId, stableId, StringComparison.Ordinal))
                    return exit;
            }
            return null;
        }

        internal GameplayReadablePlanStageProjection FindCurrentStage()
        {
            for (int i = 0; i < _stageCount; i++)
            {
                if (_stagePool[i].Current)
                    return _stagePool[i];
            }
            return null;
        }

        internal GameplayReadablePlanExitProjection FindPrimaryExit(
            int sourceStageIndex,
            GameplayReadablePlanExitTone tone)
        {
            for (int i = 0; i < _exitCount; i++)
            {
                GameplayReadablePlanExitProjection exit = _exitPool[i];
                if (exit.SourceStageIndex == sourceStageIndex && exit.Tone == tone)
                    return exit;
            }
            return null;
        }

        internal void Rebuild(CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline)
        {
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages =
                timeline?.Stages ?? Array.Empty<CharacterTestRuntimeAiLabLogicStageViewModel>();
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors =
                timeline?.Detectors ?? Array.Empty<CharacterTestRuntimeAiLabLogicDetectorViewModel>();
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions =
                timeline?.Transitions ?? Array.Empty<CharacterTestRuntimeAiLabLogicTransitionViewModel>();
            int stageCount = Math.Min(timeline?.StageCount ?? 0, stages.Length);
            int detectorCount = Math.Min(timeline?.DetectorCount ?? 0, detectors.Length);
            int transitionCount = Math.Min(timeline?.TransitionCount ?? 0, transitions.Length);

            EnsureTransitionScratch(transitionCount);
            Array.Clear(_projectedTransitions, 0, transitionCount);
            _stageCount = 0;
            _exitCount = 0;
            _groupCount = 0;

            int minimumColumn = 0;
            bool hasColumn = false;
            for (int i = 0; i < stageCount; i++)
            {
                CharacterTestRuntimeAiLabLogicStageViewModel stage = stages[i];
                if (stage == null)
                    continue;
                if (!hasColumn || stage.Column < minimumColumn)
                {
                    minimumColumn = stage.Column;
                    hasColumn = true;
                }
            }

            EnsureStageScratch(stageCount);
            Array.Clear(_orderedStages, 0, stageCount);
            int presentationCursor = 0;
            int presentationEnd = OpenStagePresentationFrames;
            for (int presentationIndex = 0; presentationIndex < stageCount; presentationIndex++)
            {
                int sourceIndex = FindNextPresentationStage(
                    stages,
                    stageCount,
                    _orderedStages);
                if (sourceIndex < 0)
                    break;
                CharacterTestRuntimeAiLabLogicStageViewModel stage = stages[sourceIndex];
                _orderedStages[sourceIndex] = true;
                int structuralDuration = ResolvePresentationStageDuration(stage);
                int actionDuration = Math.Max(0, stage.ActionDurationFrames);
                int columnStart = Math.Max(0, stage.Column - minimumColumn)
                    * StageColumnStrideFrames;
                int authoredStart = Math.Max(0, stage.StartFrame);
                int start = Math.Max(
                    presentationCursor,
                    Math.Max(columnStart, authoredStart));
                var authoredRange = new BehaviorPlanTimeRange(
                    authoredStart,
                    structuralDuration,
                    start + Math.Max(structuralDuration, actionDuration));
                _stagePresentationStarts[sourceIndex] = start;
                int end = authoredRange.PresentationEndRelativeFrame;
                presentationEnd = Math.Max(presentationEnd, end);
                presentationCursor = end + StagePresentationGapFrames;
            }

            for (int i = 0; i < stageCount; i++)
            {
                CharacterTestRuntimeAiLabLogicStageViewModel stage = stages[i];
                if (stage == null)
                    continue;
                GameplayReadablePlanStageProjection projected = GetOrCreateStage(_stageCount++);
                projected.SourceIndex = i;
                projected.StableId = stage.StageId ?? string.Empty;
                projected.StageLabel = CharacterTestRuntimeAiPlannerDevPresentationText
                    .BuildBehaviorStageLabel(stage.Title, stage.Kind);
                projected.ActionLabel = CharacterTestRuntimeAiPlannerDevPresentationText
                    .BuildBehaviorGameplayActionLabel(stage);
                projected.MetricKind = GameplayReadablePlanMetricKind.None;
                projected.MetricCurrentText = string.Empty;
                projected.MetricTargetText = string.Empty;
                projected.MetricCompletionText = string.Empty;
                if (stage.Kind == CharacterTestRuntimeAiLabCardStepKind.MoveToTarget
                    && stage.MoveMinDistance > 0f)
                {
                    projected.MetricKind = GameplayReadablePlanMetricKind.Distance;
                    projected.MetricCompletionText = "≤"
                        + stage.MoveMinDistance.ToString("0.#", CultureInfo.InvariantCulture)
                        + "m";
                    projected.MetricTargetText = "/ ≤"
                        + stage.MoveMinDistance.ToString("0.#", CultureInfo.InvariantCulture)
                        + "m";
                }
                projected.RunningLabel = "正在" + projected.ActionLabel;
                projected.WindowText = stage.DurationMode == CharacterTestRuntimeAiLabLogicDurationMode.Fixed
                    ? "计划时长 " + Math.Max(1, stage.DurationFrames)
                        .ToString(CultureInfo.InvariantCulture) + " 帧"
                    : "开放阶段";
                projected.TooltipText = projected.StageLabel
                    + "\n玩法：" + projected.ActionLabel
                    + (projected.MetricKind == GameplayReadablePlanMetricKind.Distance
                        ? "\n完成条件：距离 ≤"
                            + stage.MoveMinDistance.ToString("0.#", CultureInfo.InvariantCulture)
                            + "m"
                        : string.Empty)
                    + "\n" + projected.WindowText
                    + "\n阶段标识：" + projected.StableId
                    + "\n原始名称：" + (stage.Title ?? string.Empty);
                int structuralDuration = ResolvePresentationStageDuration(stage);
                projected.StartFrame = _stagePresentationStarts[i];
                projected.DurationFrames = structuralDuration;
                projected.ActionStartFrame = projected.StartFrame;
                projected.ActionDurationFrames = Math.Max(0, stage.ActionDurationFrames);
                projected.OwnsPrimaryAction = stage.OwnsPrimaryAction
                    || stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Logic
                    || stage.Kind == CharacterTestRuntimeAiLabCardStepKind.Wait;
                CopyStageRuntime(projected, stage);
                UpdateStageMetricRuntime(projected, timeline);
                int projectedEnd = projected.StartFrame + projected.DurationFrames;
                if (projected.ActionDurationFrames > 0)
                {
                    projectedEnd = Math.Max(
                        projectedEnd,
                        projected.ActionStartFrame + projected.ActionDurationFrames);
                }
                presentationEnd = Math.Max(presentationEnd, projectedEnd);
            }

            RefineWaitActionLabels(stages, stageCount, detectors, detectorCount);

            PresentationStartFrame = 0;
            PresentationEndFrame = Math.Max(
                PresentationStartFrame + 1,
                presentationEnd + StagePresentationGapFrames);

            for (int stageIndex = 0; stageIndex < stageCount; stageIndex++)
            {
                if (HasStageExitGroup(stageIndex, detectors, detectorCount, transitions, transitionCount))
                {
                    AddExitGroup(
                        stageIndex,
                        -1,
                        false,
                        timeline,
                        stages,
                        stageCount,
                        detectors,
                        detectorCount,
                        transitions,
                        transitionCount);
                }
            }

            for (int detectorIndex = 0; detectorIndex < detectorCount; detectorIndex++)
            {
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector = detectors[detectorIndex];
                if (detector == null || !detector.CardScope)
                    continue;
                AddExitGroup(
                    -1,
                    detectorIndex,
                    true,
                    timeline,
                    stages,
                    stageCount,
                    detectors,
                    detectorCount,
                    transitions,
                    transitionCount);
            }

            for (int i = 0; i < transitionCount; i++)
            {
                if (_projectedTransitions[i] || transitions[i] == null)
                    continue;
                AddExitGroup(
                    transitions[i].SourceStageIndex,
                    -1,
                    false,
                    timeline,
                    stages,
                    stageCount,
                    detectors,
                    detectorCount,
                    transitions,
                    transitionCount,
                    orphanTransitionIndex: i);
            }

            UpdateRuntime(timeline);
        }

        /// <remarks>NoAlloc after projection high-water initialization.</remarks>
        internal void UpdateRuntime(CharacterTestRuntimeAiLabBehaviorCardTimelineViewModel timeline)
        {
            CharacterTestRuntimeAiLabLogicStageViewModel[] stages =
                timeline?.Stages ?? Array.Empty<CharacterTestRuntimeAiLabLogicStageViewModel>();
            CharacterTestRuntimeAiLabLogicDetectorViewModel[] detectors =
                timeline?.Detectors ?? Array.Empty<CharacterTestRuntimeAiLabLogicDetectorViewModel>();
            CharacterTestRuntimeAiLabLogicTransitionViewModel[] transitions =
                timeline?.Transitions ?? Array.Empty<CharacterTestRuntimeAiLabLogicTransitionViewModel>();

            SnapshotVisible = false;
            SnapshotFrame = -1;
            SnapshotStageIndex = -1;
            ExecutionStatusText = timeline?.Historical == true
                ? "历史帧"
                : "暂无运行";
            FlowFrame = Math.Max(0, timeline?.RuntimeFrame ?? 0);
            for (int i = 0; i < _stageCount; i++)
            {
                GameplayReadablePlanStageProjection projected = _stagePool[i];
                CharacterTestRuntimeAiLabLogicStageViewModel stage = projected.SourceIndex >= 0
                    && projected.SourceIndex < stages.Length
                        ? stages[projected.SourceIndex]
                        : null;
                if (stage == null)
                    continue;
                CopyStageRuntime(projected, stage);
                UpdateStageMetricRuntime(projected, timeline);
                if (timeline?.Historical == true)
                    projected.ActionProgressMode = GameplayReadablePlanProgressMode.None;
                if (timeline?.Historical != true
                    && stage.Current
                    && stage.ActualDurationFrames >= 0)
                {
                    SnapshotVisible = true;
                    SnapshotFrame = timeline.RuntimeFrame >= 0
                        ? timeline.RuntimeFrame
                        : stage.StartFrame + stage.ActualDurationFrames;
                    SnapshotStageIndex = projected.SourceIndex;
                    ExecutionStatusText = "执行中";
                    if (stage.ActionRuntimeState == (int)CharacterTestRuntimeAiLabLogicActionState.Completed)
                        ExecutionStatusText = "动作完成 · 监听中";
                    else if (stage.ActionRuntimeState == (int)CharacterTestRuntimeAiLabLogicActionState.Failed)
                        ExecutionStatusText = "动作失败 · 监听中";
                }
            }

            if (!SnapshotVisible
                && timeline?.Historical != true
                && timeline?.RuntimeFrame > 0)
            {
                ExecutionStatusText = "未执行";
            }

            for (int i = 0; i < _exitCount; i++)
            {
                GameplayReadablePlanExitProjection exit = _exitPool[i];
                CharacterTestRuntimeAiLabLogicDetectorViewModel detector =
                    exit.SourceDetectorIndex >= 0 && exit.SourceDetectorIndex < detectors.Length
                        ? detectors[exit.SourceDetectorIndex]
                        : null;
                CharacterTestRuntimeAiLabLogicTransitionViewModel transition =
                    exit.SourceTransitionIndex >= 0 && exit.SourceTransitionIndex < transitions.Length
                        ? transitions[exit.SourceTransitionIndex]
                        : null;
                exit.Selected = transition?.Selected == true || detector?.Selected == true;
                exit.DetectorActive = detector?.Active == true;
                exit.DetectorTriggered = detector?.Triggered == true || transition?.DetectorHit == true;
                exit.Pending = transition?.Pending == true;
                exit.Committed = transition?.Committed == true;
                exit.Invalid = exit.Tone == GameplayReadablePlanExitTone.Invalid
                    || transition?.Invalid == true;
                if (timeline?.Historical == true)
                {
                    exit.ProgressMode = GameplayReadablePlanProgressMode.None;
                    exit.ProgressFrames = -1;
                }
                else
                {
                    UpdateExitProgress(exit, detector);
                }
            }
        }

        private static void UpdateExitProgress(
            GameplayReadablePlanExitProjection exit,
            CharacterTestRuntimeAiLabLogicDetectorViewModel detector)
        {
            exit.ProgressFrames = -1;
            bool fixedWindow = detector?.WindowEndMode
                == CharacterTestRuntimeAiLabLogicDetectorWindowEndMode.Fixed;
            exit.ProgressDurationFrames = fixedWindow
                ? Math.Max(0, detector?.DurationFrames ?? 0)
                : 0;
            exit.ProgressMode = GameplayReadablePlanProgressMode.None;
            if (exit.Invalid)
            {
                exit.ProgressMode = GameplayReadablePlanProgressMode.Failed;
                return;
            }
            if (exit.Committed || exit.DetectorTriggered)
            {
                exit.ProgressFrames = fixedWindow
                    ? Math.Max(1, exit.ProgressDurationFrames)
                    : Math.Max(0, detector?.RuntimeElapsedFrames ?? 0);
                exit.ProgressMode = GameplayReadablePlanProgressMode.Completed;
                return;
            }
            if (detector?.Active != true)
                return;
            if (fixedWindow)
            {
                exit.ProgressFrames = Math.Max(0, detector.RuntimeElapsedFrames);
                exit.ProgressMode = exit.ProgressDurationFrames > 0
                    && exit.ProgressFrames > exit.ProgressDurationFrames
                        ? GameplayReadablePlanProgressMode.Overrun
                        : GameplayReadablePlanProgressMode.Determinate;
                return;
            }
            exit.ProgressFrames = Math.Max(0, detector.RuntimeElapsedFrames);
            exit.ProgressMode = GameplayReadablePlanProgressMode.Indeterminate;
        }

        internal static string GetRuntimeStateText(GameplayReadablePlanExitRuntimeState state)
        {
            switch (state)
            {
                case GameplayReadablePlanExitRuntimeState.Listening:
                    return "正在监听";
                case GameplayReadablePlanExitRuntimeState.Triggered:
                    return "条件已命中";
                case GameplayReadablePlanExitRuntimeState.Pending:
                    return "等待动作边界";
                case GameplayReadablePlanExitRuntimeState.Committed:
                    return "本帧已采用";
                case GameplayReadablePlanExitRuntimeState.Invalid:
                    return "配置无效";
                default:
                    return "尚未命中";
            }
        }

        internal static string GetToneText(GameplayReadablePlanExitTone tone)
        {
            switch (tone)
            {
                case GameplayReadablePlanExitTone.Success:
                    return "成功出口";
                case GameplayReadablePlanExitTone.Failure:
                    return "失败出口";
                case GameplayReadablePlanExitTone.Condition:
                    return "条件出口";
                default:
                    return "无效出口";
            }
        }

    }
}
