using System;
using MxFramework.UI;
using UnityEngine;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    internal sealed partial class CharacterTestFairyGuiRuntimeAiLabBehaviorCardTimelineBinder
    {
        private StageSlot GetStageSlot(int index)
        {
            while (_stagePool.Count <= index)
            {
                var slot = new StageSlot();
                slot.Visual = CreateRequiredComponent(StageComponentName);
                slot.Visual.name = "runtimeStage" + _stagePool.Count;
                ConfigureStageSlot(slot);
                _stagePool.Add(slot);
                _contentLayer.AddChild(slot.Visual);
            }
            return _stagePool[index];
        }

        private DetectorSlot GetDetectorSlot(int index)
        {
            while (_detectorPool.Count <= index)
            {
                var slot = new DetectorSlot();
                slot.Visual = CreateRequiredComponent(DetectorComponentName);
                slot.Visual.name = "runtimeDetector" + _detectorPool.Count;
                ConfigureDetectorSlot(slot);
                _detectorPool.Add(slot);
                _contentLayer.AddChild(slot.Visual);
            }
            return _detectorPool[index];
        }

        private TransitionSlot GetTransitionSlot(int index)
        {
            while (_transitionPool.Count <= index)
            {
                var slot = new TransitionSlot();
                slot.Visual = CreateRequiredComponent(TransitionComponentName);
                slot.Visual.name = "runtimeTransition" + _transitionPool.Count;
                ConfigureTransitionSlot(slot);
                _transitionPool.Add(slot);
                _contentLayer.AddChild(slot.Visual);
            }
            return _transitionPool[index];
        }

        private Fgui.GComponent CreateRequiredComponent(string componentName)
        {
            Fgui.GComponent result = Fgui.UIPackage.CreateObject(
                CharacterTestFairyGuiRuntimeAiPlannerDevPanelIds.PackageId,
                componentName)?.asCom;
            if (result == null)
                throw new InvalidOperationException("Runtime AI Lab package is missing '" + componentName + "'. Apply and publish UI Composer sources.");
            return result;
        }

        private void ConfigureStageSlot(StageSlot slot)
        {
            slot.ConflictHatches = new Fgui.GObject[ConflictHatchCount];
            for (int i = 0; i < ConflictHatchCount; i++)
                slot.ConflictHatches[i] = slot.Visual.GetChild("conflictHatch" + i);
            slot.CompactFrame = new Fgui.GGraph
            {
                name = "compactFrame",
                touchable = false,
                visible = false
            };
            slot.Visual.AddChildAt(slot.CompactFrame, 0);
            slot.CompactHeaderDivider = new Fgui.GGraph
            {
                name = "compactHeaderDivider",
                touchable = false,
                visible = false
            };
            slot.Visual.AddChild(slot.CompactHeaderDivider);
            slot.CompactLaneDivider = new Fgui.GGraph
            {
                name = "compactLaneDivider",
                touchable = false,
                visible = false
            };
            slot.Visual.AddChild(slot.CompactLaneDivider);
            slot.Visual.onTouchBegin.Set(context => BeginStagePointerGesture(
                slot,
                CharacterTestRuntimeAiLabStageGestureKind.Move,
                context));
            slot.Visual.onTouchMove.Set(UpdateStagePointerGesture);
            slot.Visual.onTouchEnd.Set(EndStagePointerGesture);
            slot.Visual.onClick.Set(() =>
            {
                if (_behaviorLogicMode)
                    SelectStageForInspector(slot);
            });
            if (slot.Visual.displayObject != null)
            {
                slot.Visual.displayObject.onMouseWheel.Set(
                    context => ScrollBehaviorLogicStageDetectors(slot, context));
            }
            Fgui.GObject title = slot.Visual.GetChild("title");
            if (title != null)
            {
                title.onTouchBegin.Set(context => context.StopPropagation());
                title.onClick.Set(context =>
                {
                    context.StopPropagation();
                    if (!TryCommitActiveGraphFieldBeforeNavigation()
                        || !TryCommitActiveStageFieldBeforeNavigation())
                        return;
                    _commands?.SelectStage(
                        slot.StableId,
                        CharacterTestRuntimeAiLabStageSelectionModifier.None);
                    if (context.inputEvent?.clickCount >= 2)
                        _commands?.ToggleStageExpanded(slot.StableId);
                });
            }
            BindStageInspectorEntry(slot, "kind");
            BindStageInspectorEntry(slot, "time");
            BindStageResizeHandle(slot);
            slot.InputPort = GetRequiredGraph(slot.Visual, "inputPort");
            slot.InputPort.touchable = false;
        }

        private void BindStageInspectorEntry(StageSlot slot, string childName)
        {
            BindSlotButton(slot.Visual, childName, () => SelectStageForInspector(slot));
        }

        private void SelectStageForInspector(StageSlot slot)
        {
            if (slot == null
                || !TryCommitActiveGraphFieldBeforeNavigation()
                || !TryCommitActiveStageFieldBeforeNavigation())
                return;
            HideTransitionResultMenu();
            _commands?.SelectStage(
                slot.StableId,
                CharacterTestRuntimeAiLabStageSelectionModifier.None);
        }

        private static void SetStageConflictVisible(StageSlot slot, bool visible)
        {
            SetObjectVisible(slot.Visual, "conflict", visible);
            for (int i = 0; i < slot.ConflictHatches.Length; i++)
            {
                if (slot.ConflictHatches[i] != null)
                    slot.ConflictHatches[i].visible = visible;
            }
        }

        private static Fgui.GGraph GetRequiredGraph(Fgui.GComponent root, string childName)
        {
            Fgui.GGraph graph = root?.GetChild(childName)?.asGraph;
            if (graph == null)
                throw new InvalidOperationException(
                    "Runtime AI Lab component is missing required Graph '" + childName + "'. Apply and publish UI Composer sources.");
            return graph;
        }

        private void BindStageResizeHandle(StageSlot slot)
        {
            Fgui.GObject handle = slot.Visual.GetChild("resizeHandle");
            if (handle == null)
                return;
            handle.onTouchBegin.Set(context =>
            {
                context?.StopPropagation();
                BeginStagePointerGesture(slot, CharacterTestRuntimeAiLabStageGestureKind.Resize, context);
            });
            handle.onTouchMove.Set(context =>
            {
                context?.StopPropagation();
                UpdateStagePointerGesture(context);
            });
            handle.onTouchEnd.Set(context =>
            {
                context?.StopPropagation();
                EndStagePointerGesture(context);
            });
        }

        private void ConfigureDetectorSlot(DetectorSlot slot)
        {
            slot.Visual.onTouchBegin.Set(context => context.StopPropagation());
            slot.Visual.onClick.Set(() =>
            {
                if (!TryCommitActiveGraphFieldBeforeNavigation())
                    return;
                HideTransitionResultMenu();
                Enqueue(
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectDetector,
                    slot.StableId,
                    0,
                    CharacterTestRuntimeAiLabLogicElementKind.Detector);
            });
            BindSlotButton(slot.Visual, "btnFact", () => Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleDetectorFact,
                slot.StableId));
            BindSlotButton(slot.Visual, "btnTrigger", () => Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleDetectorTrigger,
                slot.StableId));
            BindSlotButton(slot.Visual, "btnWindowMode", () => Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleDetectorWindowMode,
                slot.StableId));
            BindSlotButton(slot.Visual, "btnWindowMinus", () => MoveDetector(slot, -1));
            BindSlotButton(slot.Visual, "btnWindowPlus", () => MoveDetector(slot, 1));
            BindSlotButton(slot.Visual, "btnDurationMinus", () => Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ResizeDetectorWindow,
                slot.StableId,
                -1));
            BindSlotButton(slot.Visual, "btnDurationPlus", () => Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.ResizeDetectorWindow,
                slot.StableId,
                1));
            BindSlotButton(slot.Visual, "btnDuplicate", () => Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.DuplicateDetector,
                slot.StableId));
            BindSlotButton(slot.Visual, "btnAddTransition", () =>
            {
                if (_behaviorLogicMode)
                    ToggleTransitionResultMenu(slot);
                else
                    Enqueue(
                        CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AddTransition,
                        slot.StableId);
            });
            BindSlotButton(slot.Visual, "btnPriorityUp", () => Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AdjustDetectorPriority,
                slot.StableId,
                -1));
            BindSlotButton(slot.Visual, "btnPriorityDown", () => Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.AdjustDetectorPriority,
                slot.StableId,
                1));
            BindSlotButton(slot.Visual, "btnDelete", () => Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.RemoveSelectedElement,
                slot.StableId,
                0,
                CharacterTestRuntimeAiLabLogicElementKind.Detector));
            ConfigureDetectorEditorHandlers(slot);
            ConfigureDetectorPointerGestures(slot);
        }

        private void ConfigureTransitionSlot(TransitionSlot slot)
        {
            slot.Visual.onTouchBegin.Set(context => context.StopPropagation());
            slot.Visual.onClick.Set(() =>
            {
                if (!TryCommitActiveGraphFieldBeforeNavigation())
                    return;
                HideTransitionResultMenu();
                Enqueue(
                    CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.SelectTransition,
                    slot.StableId,
                    0,
                    CharacterTestRuntimeAiLabLogicElementKind.Transition);
                if (slot.RuntimeFrame >= 0)
                {
                    _commandSink?.Enqueue(new MxUiCommand(
                        _viewId,
                        CharacterTestFairyGuiRuntimeAiPlannerDevPanelCommandIds
                            .BehaviorTransitionLocateRuntimeFrame,
                        new CharacterTestRuntimeAiLabBehaviorTransitionNavigationCommand(
                            slot.StableId,
                            slot.RuntimeFrame)));
                }
            });
            BindSlotButton(slot.Visual, "btnPolicy", () => Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleTransitionPolicy,
                slot.StableId));
            BindSlotButton(slot.Visual, "btnTargetPrevious", () => Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleTransitionTarget,
                slot.StableId,
                -1));
            BindSlotButton(slot.Visual, "btnTargetNext", () => Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.CycleTransitionTarget,
                slot.StableId,
                1));
            BindSlotButton(slot.Visual, "btnDelete", () => Enqueue(
                CharacterTestFairyGuiRuntimeAiLabBehaviorCardCommandIds.RemoveSelectedElement,
                slot.StableId,
                0,
                CharacterTestRuntimeAiLabLogicElementKind.Transition));
            ConfigureTransitionEditorHandlers(slot);
        }

        private static void BindSlotButton(Fgui.GComponent root, string childName, Action action)
        {
            Fgui.GObject child = root.GetChild(childName);
            if (child == null)
                return;
            child.touchable = true;
            child.onTouchBegin.Set(context => context.StopPropagation());
            child.onClick.Set(context =>
            {
                context.StopPropagation();
                action();
            });
        }

        private void BeginStagePointerGesture(
            StageSlot slot,
            CharacterTestRuntimeAiLabStageGestureKind kind,
            Fgui.EventContext context)
        {
            if (_behaviorLogicMode)
                return;
            if (context?.inputEvent == null || slot == null || !_canEdit)
                return;
            if (kind == CharacterTestRuntimeAiLabStageGestureKind.Move)
            {
                Vector2 cardLocal = slot.Visual.GlobalToLocal(new Vector2(
                    context.inputEvent.x,
                    context.inputEvent.y));
                if (cardLocal.y < 0f || cardLocal.y > 66f)
                    return;
            }
            if (!TryCommitActiveStageFieldBeforeNavigation())
                return;
            CharacterTestRuntimeAiLabStageSelectionModifier modifier = ResolveStageSelectionModifier(context);
            bool alreadySelected = _commands?.IsStageSelected(slot.StableId) ?? false;
            if (modifier != CharacterTestRuntimeAiLabStageSelectionModifier.None || !alreadySelected)
            {
                if (!(_commands?.SelectStage(slot.StableId, modifier) ?? false))
                    return;
                if (modifier != CharacterTestRuntimeAiLabStageSelectionModifier.None)
                    return;
            }

            float pixelsPerFrame = (CanvasRight - CanvasLeft)
                / Math.Max(1f, _lastRulerEndFrame - _lastRulerStartFrame);
            if (!(_commands?.BeginStageGesture(
                    kind,
                    slot.StableId,
                    context.inputEvent.x,
                    pixelsPerFrame,
                    CharacterTestRuntimeAiLabStageSelectionModifier.None,
                    out _stageGestureEpoch) ?? false))
            {
                return;
            }

            EnsureStageGestureScratch();
            CaptureTimelineTransientVisuals();
            BeginTimelineTransientViewport();
            _stageGesturePointerStartX = context.inputEvent.x;
            _stageGesturePixelsPerFrame = pixelsPerFrame;
            _stageGesturePrimaryId = slot.StableId;
            _stageGestureKind = kind;
            _stageGestureCaptured = true;
            _stageGestureMoved = false;
            context.CaptureTouch();
            context.StopPropagation();
        }

        private void UpdateStagePointerGesture(Fgui.EventContext context)
        {
            if (!_stageGestureCaptured || context?.inputEvent == null)
                return;
            float pointerX = context.inputEvent.x;
            if (!_stageGestureMoved && Math.Abs(pointerX - _stageGesturePointerStartX) < 4f)
                return;
            _stageGestureMoved = true;
            UpdateTimelineEdgeAutoScroll(pointerX, context.inputEvent.y);
            float semanticPointerX = pointerX
                + _stageTransientViewportFrameOffset * _stageGesturePixelsPerFrame;
            CharacterTestRuntimeAiLabStageGesturePreview preview = _commands.UpdateStageGesture(
                _stageGestureEpoch,
                semanticPointerX,
                context.inputEvent.ctrl);
            if (!preview.Active)
            {
                CancelStagePointerGesture(clearSelection: false);
                return;
            }
            ApplyStageGesturePreview(preview);
            context.StopPropagation();
        }

        private void EndStagePointerGesture(Fgui.EventContext context)
        {
            if (!_stageGestureCaptured)
                return;
            bool moved = _stageGestureMoved;
            string primaryId = _stageGesturePrimaryId;
            long epoch = _stageGestureEpoch;
            int viewportFrameDelta = _stageTransientViewportFrameOffset;
            ClearStagePointerGestureState();
            if (moved)
                _commands?.CommitStageGesture(epoch, viewportFrameDelta);
            else
            {
                _commands?.CancelStageGesture();
                _commands?.SelectStage(
                    primaryId,
                    CharacterTestRuntimeAiLabStageSelectionModifier.None);
            }
            context?.StopPropagation();
        }

        private void ApplyStageGesturePreview(in CharacterTestRuntimeAiLabStageGesturePreview preview)
        {
            float deltaX = preview.FrameDelta * _stageGesturePixelsPerFrame;
            float viewportShiftX = _stageTransientViewportFrameOffset * _stageGesturePixelsPerFrame;
            for (int i = 0; i < _stagePool.Count; i++)
            {
                StageSlot slot = _stagePool[i];
                if (!slot.Visual.visible)
                    continue;
                bool selected = _commands?.IsStageSelected(slot.StableId) ?? false;
                slot.Visual.x = _stageGestureBaseX[i] - viewportShiftX;
                if (_stageGestureKind == CharacterTestRuntimeAiLabStageGestureKind.Resize)
                {
                    if (selected && string.Equals(slot.StableId, _stageGesturePrimaryId, StringComparison.Ordinal))
                        slot.Visual.SetSize(Math.Max(96f, _stageGestureBaseWidth[i] + deltaX), slot.Visual.height);
                }
                else if (selected)
                {
                    slot.Visual.x += deltaX;
                }
                SetStageConflictVisible(
                    slot,
                    _stageGestureBaseConflict[i] || (preview.HasActionConflict && selected));
            }
            ApplyTimelineTransientViewportToAuxiliaryVisuals(viewportShiftX);
            Fgui.GObject tooltip = _root?.GetChild("timelineGesturePreview");
            if (tooltip != null)
            {
                tooltip.visible = true;
                if (tooltip.asTextField != null)
                    tooltip.asTextField.text = ResolveStageGesturePreviewText(
                        preview.FrameDelta,
                        preview.HasActionConflict);
            }
        }

        private void CancelStagePointerGesture(bool clearSelection)
        {
            if (_stageGestureCaptured || (_commands?.HasActiveStageGesture ?? false))
                _commands?.CancelStageEditSessions(clearSelection);
            ClearStagePointerGestureState();
        }

        private void ClearStagePointerGestureState()
        {
            RestoreTimelineTransientVisuals();
            _stageGestureCaptured = false;
            _stageGestureMoved = false;
            _stageGestureEpoch = 0L;
            _stageGesturePointerStartX = 0f;
            _stageGesturePixelsPerFrame = 0f;
            _stageGesturePrimaryId = string.Empty;
            _stageGestureKind = CharacterTestRuntimeAiLabStageGestureKind.None;
            ResetTimelineTransientViewport();
            SetObjectVisible("timelineGesturePreview", false);
        }

        private void EnsureStageGestureScratch()
        {
            EnsureFloatScratch(ref _stageGestureBaseX, _stagePool.Count);
            EnsureFloatScratch(ref _stageGestureBaseWidth, _stagePool.Count);
            EnsureBoolScratch(ref _stageGestureBaseConflict, _stagePool.Count);
            EnsureFloatScratch(ref _detectorGestureBaseX, _detectorPool.Count);
            EnsureFloatScratch(ref _transitionGestureBaseX, _transitionPool.Count);
            EnsureFloatScratch(ref _connectorGestureBaseX, _connectorPool.Count);
        }

        private static void EnsureFloatScratch(ref float[] values, int count)
        {
            if (values.Length >= count)
                return;
            int capacity = Math.Max(count, Math.Max(8, values.Length * 2));
            Array.Resize(ref values, capacity);
        }

        private static void EnsureBoolScratch(ref bool[] values, int count)
        {
            if (values.Length >= count)
                return;
            int capacity = Math.Max(count, Math.Max(8, values.Length * 2));
            Array.Resize(ref values, capacity);
        }

        private void CaptureTimelineTransientVisuals()
        {
            for (int i = 0; i < _stagePool.Count; i++)
            {
                _stageGestureBaseX[i] = _stagePool[i].Visual.x;
                _stageGestureBaseWidth[i] = _stagePool[i].Visual.width;
                _stageGestureBaseConflict[i] = _stagePool[i].Visual.GetChild("conflict")?.visible ?? false;
            }
            for (int i = 0; i < _detectorPool.Count; i++)
                _detectorGestureBaseX[i] = _detectorPool[i].Visual.x;
            for (int i = 0; i < _transitionPool.Count; i++)
                _transitionGestureBaseX[i] = _transitionPool[i].Visual.x;
            for (int i = 0; i < _connectorPool.Count; i++)
                _connectorGestureBaseX[i] = _connectorPool[i].x;
        }

        private void BeginTimelineTransientViewport()
        {
            _stageTransientViewportBeginFrame = Math.Max(0, _lastRulerStartFrame);
            _stageTransientViewportSpan = Math.Max(1, _lastRulerEndFrame - _lastRulerStartFrame);
            _stageTransientViewportFrameOffset = 0;
            SetObjectVisible("timelineEdgeScrollLeft", false);
            SetObjectVisible("timelineEdgeScrollRight", false);
        }

        /// <remarks>NoAlloc after visual scratch capacity is established.</remarks>
        private void UpdateTimelineEdgeAutoScroll(float pointerGlobalX, float pointerGlobalY)
        {
            if (_contentLayer == null || _stageGesturePixelsPerFrame <= 0f)
                return;
            Vector2 local = _contentLayer.GlobalToLocal(new Vector2(pointerGlobalX, pointerGlobalY));
            _stageTransientViewportFrameOffset = CharacterTestRuntimeAiLabStageEdgeAutoScroll.Advance(
                _stageTransientViewportBeginFrame,
                _stageTransientViewportSpan,
                _stageTransientViewportFrameOffset,
                local.x,
                CanvasRight - CanvasLeft,
                _stageGesturePixelsPerFrame);
            SetObjectVisible("timelineEdgeScrollLeft", local.x < CharacterTestRuntimeAiLabStageEdgeAutoScroll.EdgeWidth);
            SetObjectVisible(
                "timelineEdgeScrollRight",
                local.x > CanvasRight - CanvasLeft - CharacterTestRuntimeAiLabStageEdgeAutoScroll.EdgeWidth);
        }

        private void ApplyTimelineTransientViewportToAuxiliaryVisuals(float viewportShiftX)
        {
            for (int i = 0; i < _detectorPool.Count; i++)
            {
                if (_detectorPool[i].Visual.visible)
                    _detectorPool[i].Visual.x = _detectorGestureBaseX[i] - viewportShiftX;
            }
            for (int i = 0; i < _transitionPool.Count; i++)
            {
                if (_transitionPool[i].Visual.visible)
                    _transitionPool[i].Visual.x = _transitionGestureBaseX[i] - viewportShiftX;
            }
            for (int i = 0; i < _connectorPool.Count; i++)
            {
                if (_connectorPool[i].visible)
                    _connectorPool[i].x = _connectorGestureBaseX[i] - viewportShiftX;
            }
        }

    }
}
