using System;
using System.Collections.Generic;
using System.Linq;
using MxFramework.UI;
using MxFramework.UI.FairyGui;
using UnityEngine;
using UnityEngine.InputSystem;
using Fgui = global::FairyGUI;

namespace MxFramework.Game.CharacterTest
{
    /// <summary>Card descriptions and all parameter details shown while hovering the card.</summary>
    /// <remarks>LowFreqAlloc. Binds only when card content, language or simulation timing changes.</remarks>
    internal sealed class CharacterTestFairyGuiTacticalDescriptionHints : IDisposable
    {
        internal bool CardHovered => _cardHovered;
        internal bool KeepEnemyHover { get; set; }
        internal bool PointerWithin => _cardHovered || OwnsWindows && _parameterList?.visible == true && _floatingHovered;

        // Only callers displaying execution facts opt in. Preparation and hand descriptions retain their appearance.
        internal void ApplyEnemyProgress(CharacterTestEnemyCardPresentation presentation)
        {
            foreach (var row in _rows.Values)
            {
                var state = presentation.Intent(row.Segment);
                row.Root.alpha = state == CharacterTestEnemyIntentState.Completed ? .38f
                    : state == CharacterTestEnemyIntentState.NotStarted ? .64f : 1f;
                var color = state == CharacterTestEnemyIntentState.Running ? new Color32(13, 116, 130, 255)
                    : state == CharacterTestEnemyIntentState.Failed ? new Color32(164, 61, 48, 255)
                    : new Color32(25, 54, 79, 255);
                SetEnemyInk(row.Root, color);
            }
        }
        private static void SetEnemyInk(Fgui.GComponent root, Color color)
        {
            foreach (var child in root.GetChildren())
            {
                if (child.asTextField != null) child.asTextField.color = color;
                if (child.asCom != null) SetEnemyInk(child.asCom, color);
            }
        }
        internal bool HoldingWithAlt => UsesFrostedTheme && OwnsWindows && _parameterList?.visible == true && AltHeld;
        internal static string TooltipText(CharacterTestCardUiPreviewData data)
            => string.Join("\n\n", new[] { data.Description, data.DescriptionDetails }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        private sealed class Row
        {
            internal Fgui.GComponent Root, Movement, Action, Retreat;
            internal CharacterTestTacticalDescriptionSegment Segment;
            internal string ActionPath, Template;
            internal int Indent;
            internal bool FullWidth;
        }
        private enum ParameterKind { Retreat, Movement, Action, Orbit, KeepDistance, Arrival, FacingStep }
        private readonly struct ParameterWindow
        {
            internal ParameterWindow(ParameterKind kind, CharacterTestTacticalDescriptionSegment segment)
            { Kind = kind; Segment = segment; }
            internal readonly ParameterKind Kind;
            internal readonly CharacterTestTacticalDescriptionSegment Segment;
        }
        private readonly Fgui.GComponent _card, _panel, _floatingHost;
        private readonly Fgui.GList _list, _parameterList;
        private readonly Action<string, Fgui.GObject> _chooseAction;
        private readonly Dictionary<Fgui.GComponent, Row> _rows = new();
        private readonly List<ParameterWindow> _windows = new();
        private readonly Dictionary<Fgui.GComponent, CharacterTestFairyGuiTacticalParameterGrid> _parameterGrids = new();
        private readonly CharacterTestFairyGuiCardTooltip _parameterTooltips;
        private bool UsesFrostedTheme => _floatingHost?.GetChild("deckSurface") != null
            || _floatingHost?.name == "CharacterTest.TacticalBattleHud";
        private CharacterTestCardUiPreviewData _data;
        private IMxUiTextProvider _textProvider;
        private bool _cardHovered, _floatingHovered;
        private readonly bool _sharedWindows;
        private bool OwnsWindows => !_sharedWindows || ReferenceEquals(_parameterList?.data, this);

        internal CharacterTestFairyGuiTacticalDescriptionHints(Fgui.GComponent card, Fgui.GComponent panel,
            Action<string, Fgui.GObject> chooseAction, Fgui.GComponent floatingHost = null)
        {
            _card = card; _panel = panel; _chooseAction = chooseAction;
            _parameterTooltips = new CharacterTestFairyGuiCardTooltip(floatingHost?.GetChild("deckSurface") != null
                || floatingHost?.name == "CharacterTest.TacticalBattleHud");
            _list = card.GetChild("behaviorHints")?.asList
                ?? throw new InvalidOperationException("卡面分段描述组件尚未发布，请重新打开 AI Lab。");
            // Preparation reuses the same inline layout without the authoring parameter popup.
            if (panel == null && floatingHost == null)
            {
                _card.touchable = true; _card.opaque = true;
                card.GetChild("behaviorHint").visible = false;
                _list.touchable = true; _list.autoResizeItem = false; _list.itemRenderer = RenderRow;
                return;
            }
            _sharedWindows = floatingHost != null;
            _floatingHost = floatingHost ?? panel.parent;
            _parameterList = _floatingHost.GetChild("parameterWindows")?.asList
                ?? throw new InvalidOperationException("卡旁参数浮窗尚未发布，请重新打开 AI Lab。");
            _card.touchable = true; _card.opaque = true;
            _card.onRollOver.Add(EnterCard);
            _card.onRollOut.Add(LeaveCard);
            card.GetChild("behaviorHint").visible = false;
            foreach (string name in new[] { "behaviorHintDetails", "movementHintDetails", "retreatHintDetails", "orbitHintDetails", "keepDistanceHintDetails" })
                if (panel != null) panel.GetChild(name).visible = false;
            _list.touchable = true; _list.autoResizeItem = false; _list.itemRenderer = RenderRow;
            if (floatingHost?.name == "CharacterTest.TacticalBattleHud") _list.scrollPane.touchEffect = false;
            _parameterList.touchable = true; _parameterList.opaque = true; _parameterList.autoResizeItem = false;
            if (!_sharedWindows) ConfigureWindows();
            HideDetails();
        }

        private void ConfigureWindows()
        {
            if (UsesFrostedTheme) _parameterList.margin = new Fgui.Margin { left = 16, right = 16, top = 16, bottom = 16 };
            _parameterList.itemProvider = index => Fgui.UIPackage.GetItemURL(_card.packageItem.owner.name,
                (UsesFrostedTheme ? "Preparation" : "") + (_windows[index].Kind == ParameterKind.FacingStep ? "TacticalFacingStepDetails"
                    : _windows[index].Kind == ParameterKind.KeepDistance || _windows[index].Kind == ParameterKind.Arrival ? "TacticalKeepDistanceDetails"
                    : _windows[index].Kind == ParameterKind.Orbit ? "TacticalOrbitDetails"
                    : _windows[index].Kind == ParameterKind.Retreat ? "TacticalRetreatDetails"
                    : _windows[index].Kind == ParameterKind.Movement ? "TacticalBasicMovementDetails" : "TacticalApproachActionDetails"));
            _parameterList.itemRenderer = RenderParameter;
            _parameterList.sortingOrder = 101;
            _parameterList.onRollOver.Add(EnterFloating);
            _parameterList.onRollOut.Add(LeaveFloating);
            _parameterList.onClick.Set(context => context.StopPropagation());
        }

        internal void Bind(CharacterTestCardUiPreviewData data, IMxUiTextProvider provider, int preferredSize)
        {
            bool sameCard = _data?.Id == data.Id && _data.Token == data.Token;
            bool cardHovered = sameCard && _cardHovered;
            bool floatingHovered = sameCard && _floatingHovered;
            bool detailsVisible = sameCard && OwnsWindows && _parameterList?.visible == true;
            float scroll = sameCard ? _list.scrollPane.posY : 0;
            float parameterScroll = sameCard && OwnsWindows && _parameterList != null ? _parameterList.scrollPane.posY : 0;
            HideDetails();
            _data = data; _textProvider = provider;
            _list.visible = data.HasStructuredDescription && data.Segments?.Length > 0;
            _card.GetChild("description").visible = !_list.visible;
            if (_parameterList != null && OwnsWindows) _parameterList.numItems = 0;
            _windows.Clear();
            if (!_list.visible) { _list.numItems = 0; return; }
            _list.layout = Fgui.ListLayoutType.FlowHorizontal;
            _list.columnGap = 0;
            _list.numItems = 0; _list.numItems = data.DescriptionFlow?.Rows?.Length ?? data.Segments.Length;
            if (UsesFrostedTheme) FrostedInk(_list);
            for (int size = preferredSize; size >= Math.Max(18, preferredSize - (UsesFrostedTheme ? 8 : 4)); size--)
            {
                for (int i = 0; i < _list.numChildren; i++)
                    Layout(_rows[_list.GetChildAt(i).asCom], size, _list.width, UsesFrostedTheme);
                _list.EnsureBoundsCorrect();
                if (_list.scrollPane.contentHeight <= _list.height) break;
            }
            _list.EnsureBoundsCorrect(); _list.scrollPane.SetPosY(scroll, false);
            if (UsesFrostedTheme)
            {
                FrostedInk(_list);
                foreach (var row in _rows.Values)
                {
                    row.Action.GetChild("background").visible = false;
                    row.Action.GetChild("edit").visible = false;
                    var actionName = row.Action.GetChild("name").asTextField;
                    var link = actionName.textFormat; link.underline = _chooseAction != null;
                    link.color = _chooseAction != null ? new Color32(25, 104, 149, 255) : new Color32(25,54,79,255); actionName.textFormat = link; actionName.color = link.color;
                }
            }
            _cardHovered = cardHovered; _floatingHovered = floatingHovered;
            if (_parameterList != null && (detailsVisible || cardHovered))
            {
                ShowDetails();
                _parameterList.scrollPane.SetPosY(parameterScroll, false);
                if (!cardHovered && !floatingHovered) ScheduleHide();
            }
        }

        private void RenderParameter(int index, Fgui.GObject item)
        {
            var window = _windows[index]; var root = item.asCom;
            var segment = window.Segment;
            root.touchable = true; root.opaque = true;
            if (!_parameterGrids.TryGetValue(root, out var grid))
            {
                InitializeParameter(root, window.Kind);
                grid = new CharacterTestFairyGuiTacticalParameterGrid(root);
                _parameterGrids.Add(root, grid);
            }
            string title;
            if (window.Kind == ParameterKind.FacingStep)
            {
                title = L("facing_step_parameters", "垫步参数");
                root.GetChild("rotationMultiplier").text = segment.FacingStep.RotationText;
                root.GetChild("facing").text = segment.FacingStep.FacingText;
                root.GetChild("preparationDuration").text = segment.FacingStep.PreparationText;
                root.GetChild("duration").text = segment.FacingStep.DurationText;
                BindFieldTooltip(root, "rotation", "rotation_multiplier", "旋转倍率");
                BindFieldTooltip(root, "facing", "alignment_tolerance", "面向容差");
                BindFieldTooltip(root, "preparationDuration", "alignment_limit", "转向时限", segment.FacingStep.DetailsText);
                BindFieldTooltip(root, "duration", "expected_duration", "预期时长");
            }
            else if (window.Kind == ParameterKind.Retreat || window.Kind == ParameterKind.Orbit || window.Kind == ParameterKind.KeepDistance || window.Kind == ParameterKind.Arrival)
            {
                bool orbit = window.Kind == ParameterKind.Orbit;
                bool keepDistance = window.Kind == ParameterKind.KeepDistance;
                bool arrival = window.Kind == ParameterKind.Arrival;
                title = arrival ? (segment.Arrival.IsRetreat ? L("retreat_parameters", "后撤参数") : L("approach_parameters", "接近参数")) : keepDistance ? L("keep_distance_parameters", "守距参数")
                    : orbit ? L("orbit_parameters", "环绕参数") : L("retreat_parameters", "后撤参数");
                root.GetChild("movementMultiplier").text = segment.Movement.MovementText;
                root.GetChild("rotationMultiplier").text = segment.Movement.RotationText;
                root.GetChild("duration").text = arrival ? segment.Arrival.DurationText : keepDistance ? segment.KeepDistance.DurationText
                    : orbit ? segment.Orbit.DurationText : segment.Retreat.DurationText;
                BindFieldTooltip(root, "duration", arrival ? "execution_limit" : keepDistance ? "keep_distance_duration" : orbit ? "orbit_duration" : "retreat_duration",
                    arrival ? "执行时限" : keepDistance ? "持续时长" : orbit ? "环绕时长" : "后撤时长");
                if (orbit || keepDistance || arrival)
                {
                    root.GetChild("distance").text = arrival ? segment.Arrival.RangeText : keepDistance ? segment.KeepDistance.RangeText : segment.Orbit.RangeText;
                    BindFieldTooltip(root, "distance", "expected_distance", "期望距离");
                }
                if (orbit)
                {
                    root.GetChild("direction").text = segment.Orbit.DirectionText;
                    BindFieldTooltip(root, "direction", "orbit_direction", "环绕方向");
                }
            }
            else if (window.Kind == ParameterKind.Movement)
            {
                title = segment.MovementLabel;
                root.GetChild("movementMultiplier").text = segment.Movement.MovementText;
                root.GetChild("rotationMultiplier").text = segment.Movement.RotationText;
            }
            else
            {
                title = segment.Action.IsActionOnly ? L("action_parameters", "出招参数") : L("attack_requirements", "出手条件");
                root.GetChild("distance").text = segment.Action.RangeText;
                root.GetChild("facing").text = segment.Action.FacingText;
                root.GetChild("duration").text = segment.Action.DurationText ?? "—";
                BindFieldTooltip(root, "duration", "expected_duration", "预期时长");
                BindFieldTooltip(root, "distance", "attack_distance", "出手距离");
                BindFieldTooltip(root, "facing", "facing_angle", "朝向角度");
            }
            if (window.Kind != ParameterKind.Action && window.Kind != ParameterKind.FacingStep)
            {
                BindFieldTooltip(root, "movement", "movement_multiplier", "移动倍率");
                BindFieldTooltip(root, "rotation", "rotation_multiplier", "旋转倍率");
            }
            root.GetChild("title").text = title;
            grid.Layout();
        }

        private void BindFieldTooltip(Fgui.GComponent root, string field, string key, string fallback, string details = null)
            => _parameterTooltips.Bind(root.GetChild(field + "Field"), L(key, fallback)
                + (string.IsNullOrEmpty(details) ? string.Empty : "\n" + details),
                new MxFairyGuiTooltipOptions(MxFairyGuiTooltipPlacement.Cursor, allowPin: false));

        private static void InitializeParameter(Fgui.GComponent root, ParameterKind kind)
        {
            // Each published hit area covers the field's icon and value, including their gap.
            foreach (var child in root.GetChildren())
            {
                child.touchable = child.name.EndsWith("Field", StringComparison.Ordinal);
                if (child.touchable)
                    child.asGraph.DrawRect(child.width, child.height, 0, Color.clear, Color.clear);
            }
            if (kind == ParameterKind.FacingStep)
            {
                Rectangles(root, "separator", "clockMinute", "clockHour", "preparationClockMinute", "preparationClockHour");
                Circles(root, "clockOuter", "clockInner", "preparationClockOuter", "preparationClockInner");
                DrawFacing(root); DrawRotation(root); return;
            }
            if (kind == ParameterKind.Action)
            {
                Rectangles(root, "separator", "distanceLeft", "distanceRight", "distanceLine");
                Rectangles(root, "clockMinute", "clockHour"); Circles(root, "clockOuter", "clockInner");
                DrawFacing(root);
                return;
            }
            if (kind == ParameterKind.Retreat || kind == ParameterKind.Orbit || kind == ParameterKind.KeepDistance || kind == ParameterKind.Arrival)
            {
                Rectangles(root, "separator", "clockMinute", "clockHour");
                Circles(root, "clockOuter", "clockInner");
                if (kind == ParameterKind.Orbit || kind == ParameterKind.KeepDistance || kind == ParameterKind.Arrival) Rectangles(root, "distanceLeft", "distanceRight", "distanceLine");
            }
            else Rectangles(root, "separator");
            Circles(root, "footLeft", "heelLeft", "footRight", "heelRight");
            DrawRotation(root);
        }

        private static Color32 ParameterInk(Fgui.GComponent root) => root.packageItem?.name.StartsWith("Preparation", StringComparison.Ordinal) == true
            ? new Color32(35, 77, 108, 255) : new Color32(225, 223, 211, 255);

        private static void FrostedInk(Fgui.GComponent root)
        {
            foreach (var child in root.GetChildren())
            {
                if (child.asTextField != null)
                {
                    var text = child.asTextField; var format = text.textFormat;
                    format.bold = true; format.color = new Color32(25, 54, 79, 255); text.textFormat = format;
                    text.color = format.color;
                }
                if (child.asCom != null) FrostedInk(child.asCom);
            }
        }

        private static void DrawFacing(Fgui.GComponent root)
            => root.GetChild("facingIcon").asGraph.DrawPolygon(20, 20, new[] {
                new Vector2(10, 19), new Vector2(1, 4), new Vector2(4, 2), new Vector2(7, 1), new Vector2(10, 0),
                new Vector2(13, 1), new Vector2(16, 2), new Vector2(19, 4) }, Color.clear, 2, ParameterInk(root));

        private static void DrawRotation(Fgui.GComponent root)
        {
            // A circular arrow distinguishes rotation from the duration clock.
            var arc = new Vector2[42];
            for (int i = 0; i <= 20; i++)
            {
                float angle = (45 + 270f * i / 20) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                arc[i] = new Vector2(12, 12) + direction * 10;
                arc[41 - i] = new Vector2(12, 12) + direction * 7;
            }
            var color = ParameterInk(root);
            root.GetChild("rotationArc").asGraph.DrawPolygon(24, 24, arc, color);
            root.GetChild("rotationArrow").asGraph.DrawPolygon(24, 24,
                new[] { new Vector2(22, 9), new Vector2(15, 8), new Vector2(21, 2) }, color);
        }

        private string L(string key, string fallback) => CharacterTestUiText.Resolve(_textProvider,
            "ui.character_test.tactical.description." + key, fallback);

        private void RenderRow(int index, Fgui.GObject item)
        {
            var root = item.asCom;
            if (!_rows.TryGetValue(root, out var row))
            {
                row = new Row { Root = root, Movement = root.GetChild("movement").asCom,
                    Action = root.GetChild("action").asCom, Retreat = root.GetChild("retreat").asCom };
                _rows.Add(root, row); Wire(row);
            }
            var flowRow = _data.DescriptionFlow?.Rows?[index];
            row.Indent = flowRow?.Indent ?? 0;
            row.FullWidth = _data.DescriptionFlow?.Rows?.Any(r => r.Indent > 0 || r.Kind != "intent") == true;
            row.Segment = flowRow == null ? _data.Segments[index]
                : flowRow.SegmentIndex.HasValue ? _data.Segments[flowRow.SegmentIndex.Value] : null;
            row.ActionPath = null;
            if (row.Segment == null)
            {
                row.Template = CompactBattleTemplate(flowRow.Text);
                row.Movement.visible = row.Action.visible = row.Retreat.visible = false;
                return;
            }
            row.Template = flowRow == null ? row.Segment.TextTemplate
                : flowRow.Prefix + (flowRow.StripSentenceEnd ? CharacterTestTacticalDescriptionFlow.Term(row.Segment.TextTemplate) : row.Segment.TextTemplate);
            row.Template = CompactBattleTemplate(row.Template);
            row.Movement.visible = row.Segment.Action?.IsActionOnly != true;
            row.Movement.GetChild("label").text = row.Segment.MovementLabel;
            row.Action.visible = row.Segment.Action != null; row.Retreat.visible = row.Segment.Action == null;
            row.ActionPath = null;
            if (IsPlain(row.Segment))
            { row.Movement.visible = row.Action.visible = row.Retreat.visible = false; return; }
            if (row.Segment.Action is CharacterTestTacticalApproachActionHint action)
            {
                var location = Array.Find(_data.Actions, a => a.Path == action.ActionSlotPath || "preview-legacy/" + a.Path == action.ActionSlotPath);
                row.ActionPath = location?.Path;
                row.Action.GetChild("name").text = action.HasSelectedAction ? action.ActionName : action.ActionPrompt;
                row.Action.GetChild("name").asTextField.autoSize = Fgui.AutoSizeType.Shrink;
            }
            else row.Retreat.GetChild("label").text = row.Segment.Retreat?.RetreatLabel ?? row.Segment.Orbit?.OrbitLabel
                ?? row.Segment.KeepDistance?.KeepDistanceLabel ?? row.Segment.Arrival.DistanceLabel;
        }

        private string CompactBattleTemplate(string template) => _floatingHost?.name=="CharacterTest.TacticalBattleHud"
            ? template?.Replace("等待目标进入指定距离",L("wait_near_compact","等待目标靠近")) : template;

        private void Wire(Row row)
        {
            row.Root.touchable = true; row.Root.opaque = false;
            foreach (var child in row.Root.GetChildren()) child.touchable = false;
            row.Action.touchable = _chooseAction != null; row.Action.opaque = _chooseAction != null;
            foreach (var child in row.Action.GetChildren()) child.touchable = false;
            row.Action.onClick.Set(context => { context.StopPropagation(); if (row.ActionPath != null) _chooseAction?.Invoke(row.ActionPath, row.Action); });
            row.Action.GetChild("edit").visible = _chooseAction != null;
            row.Action.GetChild("edit").asImage.color = new Color32(64, 88, 73, 255);
            row.Action.onRollOver.Set(() => row.Action.GetChild("background").asImage.color = new Color32(246, 255, 244, 255));
            row.Action.onRollOut.Set(() => row.Action.GetChild("background").asImage.color = Color.white);
        }

        private void EnterCard()
        {
            Fgui.Timers.inst.Remove(HideIfLeft);
            _cardHovered = true;
            ShowDetails();
        }

        private void LeaveCard()
        {
            _cardHovered = false;
            if (UsesFrostedTheme && !KeepEnemyHover) { if (!AltHeld) HideDetails(); return; }
            ScheduleHide();
        }

        private static bool AltHeld => Keyboard.current != null
            && (Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed);

        internal void UpdatePointerHold()
        {
            if (!UsesFrostedTheme || _parameterList == null) return;
            if (_cardHovered && !OwnsWindows && !AltHeld) ShowDetails();
            if (!OwnsWindows || !_parameterList.visible) return;
            _parameterList.touchable = KeepEnemyHover || AltHeld;
            if (!_cardHovered && !AltHeld && !KeepEnemyHover) HideDetails();
        }

        private void EnterFloating()
        { _floatingHovered = true; Fgui.Timers.inst.Remove(HideIfLeft); }

        private void LeaveFloating()
        { _floatingHovered = false; ScheduleHide(); }

        private void ScheduleHide()
        {
            Fgui.Timers.inst.Remove(HideIfLeft);
            Fgui.Timers.inst.Add(0.18f, 1, HideIfLeft);
        }

        private void HideIfLeft(object unused)
        {
            if (OwnsWindows && !_cardHovered && !_floatingHovered)
            {
                _parameterTooltips.Hide();
                _parameterList.visible = false;
            }
        }

        private void ShowDetails()
        {
            if (_parameterList == null || !_list.visible) return;
            if (_sharedWindows && !OwnsWindows)
            {
                if (_parameterList.data is CharacterTestFairyGuiTacticalDescriptionHints previous)
                {
                    if (UsesFrostedTheme && AltHeld && _parameterList.visible) return;
                    previous.ReleaseWindows();
                }
                _parameterList.data = this;
                ConfigureWindows();
            }
            if (_parameterList.visible) return;
            _parameterList.numItems = 0; _windows.Clear();
            for (int i = 0; i < _data.Segments.Length; i++)
            {
                var segment = _data.Segments[i];
                if (IsPlain(segment) && segment.FacingStep == null) continue; // Plain text has no parameter window.
                if (segment.FacingStep != null) _windows.Add(new ParameterWindow(ParameterKind.FacingStep, segment));
                else if (segment.Retreat != null) _windows.Add(new ParameterWindow(ParameterKind.Retreat, segment));
                else if (segment.Orbit != null) _windows.Add(new ParameterWindow(ParameterKind.Orbit, segment));
                else if (segment.KeepDistance != null) _windows.Add(new ParameterWindow(ParameterKind.KeepDistance, segment));
                else if (segment.Arrival != null) _windows.Add(new ParameterWindow(ParameterKind.Arrival, segment));
                else
                {
                    if (!segment.Action.IsActionOnly) _windows.Add(new ParameterWindow(ParameterKind.Movement, segment));
                    _windows.Add(new ParameterWindow(ParameterKind.Action, segment));
                }
            }
            _parameterList.numItems = _windows.Count;
            _parameterList.visible = _windows.Count > 0;
            if (UsesFrostedTheme) _parameterList.touchable = KeepEnemyHover || AltHeld;
            RefreshPlacement();
            _parameterList.scrollPane.SetPosY(0, false);
        }

        internal void HideForInteraction()
        {
            HideDetails(); _parameterTooltips.Hide();
        }

        internal void RefreshPlacement()
        {
            if (_parameterList == null || !OwnsWindows || !_parameterList.visible) return;
            for (Fgui.GObject owner = _card; owner != null; owner = owner.parent)
                if (!owner.visible) { HideDetails(); return; }
            const float margin = 12;
            float gap = UsesFrostedTheme ? 6 : 12;
            float scale = _panel?.scaleX ?? 1f;
            float width = 0, height = 0;
            for (int i = 0; i < _parameterList.numChildren; i++)
            {
                var item = _parameterList.GetChildAt(i);
                width = Math.Max(width, item.width);
                height += item.height + (i > 0 ? _parameterList.lineGap : 0);
            }
            if (UsesFrostedTheme) { width += 32; height += 32; }
            else width += 10;
            bool battle = _floatingHost.name == "CharacterTest.TacticalBattleHud";
            // Battle hand cards enlarge on hover; keep details next to the displayed card and above its resource rail.
            var topLeft = _floatingHost.GlobalToLocal(battle ? _card.LocalToGlobal(Vector2.zero)
                : _card.parent.LocalToGlobal(new Vector2(_card.xMin, _card.yMin)));
            var bottomRight = _floatingHost.GlobalToLocal(battle ? _card.LocalToGlobal(new Vector2(_card.width, _card.height))
                : _card.parent.LocalToGlobal(new Vector2(_card.xMin + _card.width, _card.yMin + _card.height)));
            float bottomLimit = battle ? _floatingHost.GetChild("motionSurface").y - 8 : _floatingHost.height - margin;
            float leftSpace = topLeft.x - margin - gap, rightSpace = _floatingHost.width - bottomRight.x - margin - gap;
            // Open toward the screen center; a centered card opens to the right.
            bool left = (topLeft.x + bottomRight.x) / 2 > _floatingHost.width / 2;
            // Keep the entire card readable even in a narrow Game view.
            scale = Math.Min(scale, Math.Max(0.1f, (left ? leftSpace : rightSpace) / width));
            _parameterList.SetScale(scale, scale);
            _parameterList.SetSize(width, Math.Min(height, Math.Max(1, (bottomLimit - margin) / scale)));
            _parameterList.EnsureBoundsCorrect();
            float centerY = (topLeft.y + bottomRight.y) / 2;
            float inset = UsesFrostedTheme ? 16 * scale : 0;
            float x = left ? topLeft.x - gap - width * scale + inset : bottomRight.x + gap - inset;
            float y = centerY - _parameterList.height * scale / 2;
            _parameterList.SetXY(Mathf.Clamp(x, margin, Math.Max(margin, _floatingHost.width - width * scale - margin)),
                Mathf.Clamp(y, margin, Math.Max(margin, bottomLimit - _parameterList.height * scale)));
        }

        private static bool IsPlain(CharacterTestTacticalDescriptionSegment segment) =>
            segment.Action == null && segment.Retreat == null && segment.Orbit == null
                && segment.KeepDistance == null && segment.Arrival == null;

        private static float Layout(Row row, int size, float width, bool compact = false)
        {
            float indent = Math.Min(width / 3, row.Indent * size);
            float available = Math.Max(1, width - indent);
            if (row.Segment == null || IsPlain(row.Segment) || row.Template.IndexOf('{') < 0)
            {
                row.Movement.visible = row.Action.visible = row.Retreat.visible = false;
                var plain = row.Root.GetChild("leadPrefix").asTextField;
                row.Root.GetChild("leadSuffix").visible = false; row.Root.GetChild("verb").visible = false;
                plain.visible = true; plain.text = row.Template; plain.singleLine = false;
                var format = plain.textFormat; format.size = size; plain.textFormat = format;
                plain.autoSize = Fgui.AutoSizeType.Height; plain.SetXY(indent, 0); plain.width = available;
                plain.height = Math.Max(size + (compact ? 4 : 8), plain.textHeight);
                row.Root.SetSize(row.FullWidth ? width : Math.Min(width, indent + Mathf.Ceil(plain.textWidth)), plain.height);
                return row.Root.height;
            }
            string token = row.Segment.Action != null ? "{action}" : row.Segment.Orbit != null ? "{orbit}"
                : row.Segment.KeepDistance != null ? "{keepDistance}" : row.Segment.Arrival != null ? row.Segment.Arrival.DistanceToken : "{retreat}";
            int movement = row.Template.IndexOf("{movement}", StringComparison.Ordinal);
            int secondToken = row.Template.IndexOf(token, StringComparison.Ordinal);
            Fgui.GObject[] items;
            // Behind-target orbit descriptions intentionally have only the movement term.
            if (secondToken < 0 && movement >= 0)
            {
                var prefix = row.Root.GetChild("leadPrefix").asTextField;
                var suffix = row.Root.GetChild("verb").asTextField;
                prefix.text = row.Template.Substring(0, movement);
                row.Root.GetChild("leadSuffix").visible = false;
                suffix.text = row.Template.Substring(movement + "{movement}".Length);
                row.Action.visible = row.Retreat.visible = false;
                items = new Fgui.GObject[] { prefix, row.Movement, suffix };
            }
            else
            {
            if (secondToken < 0 || movement < 0 && row.Segment.Action?.IsActionOnly != true) throw new InvalidOperationException("描述翻译缺少必要的参数标记。");
            bool movementFirst = movement < secondToken;
            int first = Math.Min(movement, secondToken), second = Math.Max(movement, secondToken);
            int firstLength = movementFirst ? "{movement}".Length : token.Length;
            int secondLength = movementFirst ? token.Length : "{movement}".Length;
            var prefix = row.Root.GetChild("leadPrefix").asTextField;
            var middle = row.Root.GetChild("leadSuffix").asTextField;
            var suffix = row.Root.GetChild("verb").asTextField;
            Fgui.GComponent operation = row.Segment.Action != null ? row.Action : row.Retreat;

            if (movement < 0)
            {
                prefix.text = row.Template.Substring(0, secondToken); middle.text = string.Empty; middle.visible = false;
                suffix.text = row.Template.Substring(secondToken + token.Length);
                items = new Fgui.GObject[] { prefix, operation, suffix };
            }
            else
            {
                prefix.text = row.Template.Substring(0, first);
                middle.text = row.Template.Substring(first + firstLength, second - first - firstLength);
                suffix.text = row.Template.Substring(second + secondLength);
                items = new Fgui.GObject[] { prefix, movementFirst ? row.Movement : operation, middle, movementFirst ? operation : row.Movement, suffix };
            }
            }
            float line = size + (compact ? 4 : 8), x = 0, y = 0, rowHeight = line, usedWidth = 0;
            SizeTerm(row.Movement, size, available, line);
            if (row.Retreat.visible) SizeTerm(row.Retreat, size, available, line);
            if (row.Action.visible) SizeAction(row.Action, size, available, line);
            foreach (var item in items)
            {
                if (item is Fgui.GTextField text)
                {
                    text.visible = text.text.Length > 0;
                    if (!text.visible) continue;
                    var format = text.textFormat; format.size = size; text.textFormat = format;
                    text.singleLine = true; text.autoSize = Fgui.AutoSizeType.Both;
                    float measured = Mathf.Ceil(text.textWidth);
                    text.autoSize = Fgui.AutoSizeType.None; text.SetSize(Math.Min(available, measured), line);
                    if (measured > available) { text.singleLine = false; text.autoSize = Fgui.AutoSizeType.Height; text.height = Math.Max(line, text.textHeight); }
                }
                if (x > 0 && x + item.width > available) { y += rowHeight; x = 0; rowHeight = line; }
                item.SetXY(indent + x, y); x += item.width; usedWidth = Math.Max(usedWidth, x); rowHeight = Math.Max(rowHeight, item.height);
            }
            row.Root.SetSize(row.FullWidth ? width : Math.Min(width, indent + usedWidth), y + rowHeight);
            return row.Root.height;
        }

        private static void SizeTerm(Fgui.GComponent term, int size, float width, float line)
        {
            var label = term.GetChild("label").asTextField;
            var format = label.textFormat; format.size = size; label.textFormat = format;
            label.singleLine = true; label.autoSize = Fgui.AutoSizeType.Both;
            float termWidth = Math.Min(width, Mathf.Ceil(label.textWidth) + 6);
            term.SetSize(termWidth, line); label.SetXY(0, 0);
            label.autoSize = Fgui.AutoSizeType.Shrink; label.SetSize(termWidth, line);
        }

        private static void SizeAction(Fgui.GComponent action, int size, float available, float line)
        {
            var name = action.GetChild("name").asTextField;
            var edit = action.GetChild("edit");
            var format = name.textFormat; format.size = size; name.textFormat = format;
            name.singleLine = true; name.autoSize = Fgui.AutoSizeType.Both;
            float inset = Math.Min(8, available / 4), editWidth = edit.visible ? Math.Min(18, available / 4) : 0;
            float width = Math.Min(available, Mathf.Ceil(name.textWidth) + inset * 2 + (edit.visible ? editWidth + 4 : 0));
            action.SetSize(width, line);
            var background = action.GetChild("background"); background.SetXY(0, 0); background.SetSize(width, line);
            name.autoSize = Fgui.AutoSizeType.Shrink; name.SetXY(inset, 0);
            name.SetSize(Math.Max(1, width - inset * 2 - (edit.visible ? editWidth + 4 : 0)), line);
            edit.SetSize(editWidth, editWidth); edit.SetXY(width - inset - editWidth, (line - editWidth) / 2);
        }

        private static void Rectangles(Fgui.GComponent root, params string[] names)
        { foreach (string name in names) { var graph = root.GetChild(name).asGraph; graph.DrawRect(graph.width, graph.height, 0, Color.clear, graph.color); } }
        private static void Circles(Fgui.GComponent root, params string[] names)
        { foreach (string name in names) { var graph = root.GetChild(name).asGraph; graph.DrawEllipse(graph.width, graph.height, graph.color); } }
        internal void HideDetails()
        {
            Fgui.Timers.inst.Remove(HideIfLeft);
            _parameterTooltips.Hide();
            _cardHovered = false; _floatingHovered = false;
            if (_parameterList != null && OwnsWindows && !_parameterList.isDisposed) _parameterList.visible = false;
        }
        private void ReleaseWindows()
        {
            HideDetails();
            _parameterList.onRollOver.Remove(EnterFloating); _parameterList.onRollOut.Remove(LeaveFloating);
            _parameterList.itemProvider = null; _parameterList.itemRenderer = null;
            if (_sharedWindows && ReferenceEquals(_parameterList.data, this)) _parameterList.data = null;
        }
        public void Dispose()
        {
            HideDetails();
            _parameterTooltips.Dispose();
            _card.onRollOver.Remove(EnterCard); _card.onRollOut.Remove(LeaveCard);
            if (_parameterList != null && OwnsWindows) ReleaseWindows();
            _list.itemRenderer = null;

            _rows.Clear(); _windows.Clear(); _parameterGrids.Clear(); _data = null;
        }
    }
}
